using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// GR-LV2 (ruling 14; design §6): the Vulcan Decimator's Phase 2 is an arena change. The
    /// floor cracks into lava vents on a visible cadence and the eruption's debris rains
    /// down. The worked example is the Duke's boiler rupture, Level 2.
    ///
    /// <para>The vents are cyclic hazards built up front, disabled, inside the arena, so no
    /// collision body is created mid-fight; his Phase 2 starts them. They are
    /// <c>story_hazard</c>, so a hold freezes them, and (GR-HAZ) they hit a launched or
    /// thrown mob too. They stop when he falls: the hero still has to walk to the sealing
    /// anchor. Every number here is provisional (<c>PROVISIONAL-BOSS-ARENA-RULES</c>).</para>
    ///
    /// <para>The design reads "the eruption debris rains at double rate". The Collapse Tremor
    /// retires at the PreBoss lock, so Phase 1 has no debris to double: Phase 2 rains
    /// <see cref="FallingDebris"/> at twice the Tremor's first rate. The pieces keep the Tremor's
    /// telegraph, damage and airborne cap, are hero-only as all debris is, and stay off the
    /// PreBoss anchor as debris stays off an activated fracture.</para>
    /// </summary>
    public partial class Level06Controller {
        /// <summary>The vents, west to east, inside the arena. Each strip is 120 px wide, so 120 px of safe floor lies between two.</summary>
        public static readonly float[] VulcanVentXs = { 9370f, 9610f, 9850f, 10090f, 10330f };

        public const float VulcanVentWarningSeconds = 1f;
        public const float VulcanVentActiveSeconds = 0.9f;
        public const float VulcanVentCooldownSeconds = 2.4f;
        public const int VulcanVentDamage = 14;
        /// <summary>How much later each vent starts than the one west of it, so the eruption walks across the floor.</summary>
        public const float VulcanVentStaggerSeconds = 0.86f;

        /// <summary>Twice the Tremor's first rate (one piece every 4 s).</summary>
        public const float EruptionDebrisIntervalSeconds = CollapseTremorRules.Stage1DebrisIntervalSeconds / 2f;

        private readonly List<StoryCyclicHazard> _vulcanVents = new();
        private readonly List<FallingDebris> _eruptionDebris = new();
        private readonly RandomNumberGenerator _eruptionRandom = new();
        private float _eruptionDebrisTimer;
        private bool _eruptionPiecesFrozen;

        public IReadOnlyList<StoryCyclicHazard> VulcanVents => _vulcanVents;

        /// <summary>True once Phase 2 has started the vents and the debris, and until the Vulcan falls.</summary>
        public bool VulcanEruptionRaised { get; private set; }

        /// <summary>Pieces of eruption debris launched. Test seam.</summary>
        public int EruptionDebrisLaunched { get; private set; }

        /// <summary>Airborne eruption pieces right now. Test seam.</summary>
        public IReadOnlyList<FallingDebris> EruptionDebrisAirborne {
            get {
                PruneEruptionDebris();
                return _eruptionDebris;
            }
        }

        private void BuildVulcanVents() {
            for (int index = 0; index < VulcanVentXs.Length; index++) {
                _vulcanVents.Add(BuildCyclicHazardLane($"level_06.vulcan_vent_{index}", VulcanVentXs[index], GroundY,
                    VulcanVentWarningSeconds, VulcanVentActiveSeconds, VulcanVentCooldownSeconds, VulcanVentDamage, startEnabled: false));
            }
        }

        private void OnVulcanPhaseEntered(int phase) {
            if (phase < 1) return;
            // PhaseEntered fires inside the crossing hit's physics flush, and the raise starts
            // the vents' Area2D work and pooled debris: hop off it.
            if (PhysicsCallbackGuard.IsInPhysicsCallback) Callable.From(RaiseVulcanEruption).CallDeferred();
            else RaiseVulcanEruption();
        }

        /// <summary>Starts the vents on their staggered warnings and the debris. Idempotent; public as a test seam.</summary>
        public void RaiseVulcanEruption() {
            if (VulcanEruptionRaised || IsBossDefeated || _vulcanVents.Count == 0) return;
            VulcanEruptionRaised = true;
            _eruptionRandom.Randomize();
            _eruptionDebrisTimer = EruptionDebrisIntervalSeconds;
            bool held = IsInsideTree() && ChronalRewindManager.IsWorldHeld(GetTree());
            for (int index = 0; index < _vulcanVents.Count; index++) {
                StoryCyclicHazard vent = _vulcanVents[index];
                vent.Enabled = true;
                // Never a telegraph that starts inside the hold: it would run out unseen.
                if (!held) vent.ForcePhase(HazardPhase.Warning, VulcanVentWarningSeconds + index * VulcanVentStaggerSeconds);
            }
        }

        /// <summary>Stops the vents and the debris once the Vulcan falls.</summary>
        private void LowerVulcanEruption() {
            VulcanEruptionRaised = false;
            foreach (StoryCyclicHazard vent in _vulcanVents) {
                if (vent == null || !IsInstanceValid(vent)) continue;
                vent.Enabled = false;
                vent.ForcePhase(HazardPhase.Cooldown, VulcanVentCooldownSeconds);
            }
            SetEruptionPiecesFrozen(false);
        }

        public override void _PhysicsProcess(double delta) {
            if (!VulcanEruptionRaised) return;
            // A held or paused world holds the rain too: no new piece, and the pieces in the
            // air wait with it (nothing else holds debris; the Tremor retires before this).
            bool suspended = ChronalRewindManager.IsWorldHeld(GetTree()) || StoryManager.Instance is { IsIntegrityClockPaused: true };
            SetEruptionPiecesFrozen(suspended);
            if (suspended) return;
            _eruptionDebrisTimer -= (float)delta;
            if (_eruptionDebrisTimer > 0f) return;
            _eruptionDebrisTimer = EruptionDebrisIntervalSeconds;
            TryLaunchEruptionDebris();
        }

        /// <summary>One piece near the hero, inside the arena, unless two are already in the air.</summary>
        private void TryLaunchEruptionDebris() {
            PruneEruptionDebris();
            if (_eruptionDebris.Count >= CollapseTremorRules.MaxAirborneDebris) return;
            PlayerController hero = Player;
            if (hero == null || !IsInstanceValid(hero) || hero.CurrentState == CharacterState.Dead) return;
            float spread = _eruptionRandom.RandfRange(-CollapseTremorRules.DebrisSpreadPixels, CollapseTremorRules.DebrisSpreadPixels);
            var target = new Vector2(EruptionTargetX(hero.GlobalPosition.X + spread), hero.GlobalPosition.Y);
            if (PoolManager.Instance?.Spawn(FallingDebris.PoolID, target, this) is not FallingDebris debris) return;
            debris.Launch(target);
            _eruptionDebris.Add(debris);
            EruptionDebrisLaunched++;
        }

        /// <summary>A landing x inside the arena and off the PreBoss anchor (9060), an activated fracture being a safe beat.</summary>
        public static float EruptionTargetX(float x) => Mathf.Clamp(x,
            Mathf.Max(BossArenaBounds.Position.X, PreBossCheckpointX + CollapseTremorRules.SafeZoneRadiusPixels),
            BossArenaBounds.End.X);

        private void SetEruptionPiecesFrozen(bool frozen) {
            if (frozen == _eruptionPiecesFrozen) return;
            _eruptionPiecesFrozen = frozen;
            PruneEruptionDebris();
            foreach (FallingDebris debris in _eruptionDebris) debris.SetFrozen(frozen);
        }

        private void PruneEruptionDebris() {
            for (int index = _eruptionDebris.Count - 1; index >= 0; index--) {
                FallingDebris debris = _eruptionDebris[index];
                if (debris == null || !IsInstanceValid(debris) || !debris.IsAirborne) _eruptionDebris.RemoveAt(index);
            }
        }
    }
}
