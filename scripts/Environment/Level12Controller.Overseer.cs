using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// GR-LV4 (rulings 14, 14+; design §6): the Gravity Overseer's arena. Phase 2 marks two
    /// zones of the pad whose gravity cycles between light and heavy, on a 1 s telegraph, with
    /// no literal inversion. Phase 3 stands them down and opens vents that gust toward the arena
    /// edges. The worked example is the Duke's boiler rupture, Level 2.
    ///
    /// <para>The pad's dampers tile the whole arena, and <see cref="EnvironmentPlayerModifiers"/>
    /// multiplies every live source, so a zone cycles at the ruled scale divided by the
    /// dampers': a hero reads exactly 0.15 or 2.5, with no seam to straddle. Both zones and all
    /// the vents are built up front, off, inside the arena, and they are <c>story_hazard</c>, so
    /// a hold freezes them. Both stop when the Overseer falls: the hero still has to walk to the
    /// sealing anchor. Every number but the two scales and the 1 s telegraph is provisional
    /// (<c>PROVISIONAL-BOSS-ARENA-RULES</c>).</para>
    ///
    /// <para>The bots' flight model reads the dampers' static scale here, not the cycling one
    /// (<c>PlaytestNav.GravityScaleAt</c> takes the first zone it meets), so they plan a jump at
    /// 0.48 and re-plan when it goes wrong.</para>
    /// </summary>
    public partial class Level12Controller {
        /// <summary>Ruling 14+: the light scale a hero reads in a marked zone.</summary>
        public const float OverseerLightGravityScale = 0.15f;
        /// <summary>Ruling 14+: the heavy scale a hero reads in a marked zone.</summary>
        public const float OverseerHeavyGravityScale = 2.5f;
        /// <summary>Ruling 14+: the telegraph before every shift.</summary>
        public const float OverseerFieldTelegraphSeconds = 1f;
        /// <summary>A whole cycle step, telegraph included.</summary>
        public const float OverseerFieldCycleSeconds = 4f;

        /// <summary>The two marked zones, either side of the boss's spawn (10500). Spans, west to east.</summary>
        public static readonly (string ID, float StartX, float EndX, bool LightFirst)[] OverseerFieldSpans = {
            ("level_12.overseer_field_west", 10000f, 10400f, false),
            ("level_12.overseer_field_east", 10650f, 11050f, true)
        };

        /// <summary>
        /// The gust vents' inner edges. A strip runs 120 px outward from its inner edge, and
        /// <see cref="StoryCyclicHazard"/> pushes a body away from its node, so everything in a
        /// strip is pushed toward the arena edge on its side. Negative side is west.
        /// </summary>
        public static readonly (string ID, float InnerEdgeX, int Side, float FirstDelaySeconds)[] OverseerGustVents = {
            ("level_12.overseer_gust_west_inner", 10350f, -1, 0f),
            ("level_12.overseer_gust_west_outer", 10050f, -1, 1.8f),
            ("level_12.overseer_gust_east_inner", 10850f, 1, 0f),
            ("level_12.overseer_gust_east_outer", 11150f, 1, 1.8f)
        };

        public const float OverseerGustWarningSeconds = 1f;
        public const float OverseerGustActiveSeconds = 0.5f;
        public const float OverseerGustCooldownSeconds = 3f;
        public const int OverseerGustDamage = 8;
        public static readonly Vector2 OverseerGustKnockback = new(620f, -260f);
        private const float GustStripHalfWidth = 60f;

        private static readonly Color OverseerLightTint = new(0.55f, 0.8f, 1f, 0.32f);
        private static readonly Color OverseerHeavyTint = new(1f, 0.45f, 0.4f, 0.32f);

        private BossEncounterController _overseerEncounter;
        private readonly List<GravityFieldZone> _overseerFields = new();
        private readonly List<StoryCyclicHazard> _overseerGusts = new();

        public BossEncounterController OverseerEncounter => _overseerEncounter;
        public IReadOnlyList<GravityFieldZone> OverseerFields => _overseerFields;
        public IReadOnlyList<StoryCyclicHazard> OverseerGusts => _overseerGusts;

        /// <summary>True from Phase 2 until Phase 3 stands the zones down or the Overseer falls.</summary>
        public bool OverseerFieldsCycling { get; private set; }
        /// <summary>True from Phase 3 until the Overseer falls.</summary>
        public bool OverseerGustsOpen { get; private set; }

        private bool _overseerFieldsDone;

        private void BuildOverseerArena() {
            foreach ((string id, float startX, float endX, bool _) in OverseerFieldSpans) {
                // Scale 1 while off, so the dampers alone rule. The zone's first timer is its
                // telegraph: Phase 2 opens with the first shift already on its way.
                GravityFieldZone field = BuildGravityField(id, startX, endX, 1f, RegolithFieldColor);
                field.Enabled = false;
                field.RewindPolicy = StoryRewindPolicy.PreserveCurrentState;
                field.TelegraphSeconds = OverseerFieldTelegraphSeconds;
                field.CycleSeconds = OverseerFieldTelegraphSeconds;
                field.IdleTint = OverseerLightTint;
                SetFieldVisualVisible(field, false);
                field.GravityShifted += scale => field.IdleTint = scale >= 1f ? OverseerHeavyTint : OverseerLightTint;
                _overseerFields.Add(field);
            }
            foreach ((string id, float innerEdgeX, int side, float _) in OverseerGustVents) {
                StoryCyclicHazard gust = BuildCyclicHazardLane(id, innerEdgeX, SurfaceY, OverseerGustWarningSeconds,
                    OverseerGustActiveSeconds, OverseerGustCooldownSeconds, OverseerGustDamage, startEnabled: false);
                gust.Knockback = OverseerGustKnockback;
                ShiftStripOutward(gust, side);
                _overseerGusts.Add(gust);
            }
        }

        /// <summary>Moves the strip off its node, so the node is the strip's inner edge and every body in it is pushed the same way.</summary>
        private static void ShiftStripOutward(StoryCyclicHazard gust, int side) {
            var offset = new Vector2(side * GustStripHalfWidth, 0f);
            foreach (string childName in new[] { "CollisionShape2D", "WarningVisual", "ActiveVisual" }) {
                if (gust.GetNodeOrNull<Node2D>(childName) is Node2D child) child.Position += offset;
            }
        }

        private static void SetFieldVisualVisible(GravityFieldZone field, bool visible) {
            if (field.GetNodeOrNull<CanvasItem>("Visual") is CanvasItem visual) visual.Visible = visible;
        }

        private void OnOverseerPhaseEntered(int phase) {
            // A phase change arrives inside the hit's physics flush.
            if (PhysicsCallbackGuard.IsInPhysicsCallback) {
                Callable.From(() => ApplyOverseerPhase(phase)).CallDeferred();
            } else {
                ApplyOverseerPhase(phase);
            }
        }

        private void ApplyOverseerPhase(int phase) {
            if (phase == 1) RaiseOverseerFields();
            else if (phase >= 2) OpenOverseerGusts();
        }

        /// <summary>Phase 2. Public and idempotent (test seam); nothing after Phase 3 or the Overseer's fall.</summary>
        public void RaiseOverseerFields() {
            if (OverseerFieldsCycling || _overseerFieldsDone || IsBossDefeated) return;
            OverseerFieldsCycling = true;
            for (int index = 0; index < _overseerFields.Count; index++) {
                GravityFieldZone field = _overseerFields[index];
                float light = OverseerLightGravityScale / PadGravityScale;
                float heavy = OverseerHeavyGravityScale / PadGravityScale;
                // AdvanceCycle moves to index 1 first, so the scale wanted first goes second.
                field.CycleScales = OverseerFieldSpans[index].LightFirst ? new[] { heavy, light } : new[] { light, heavy };
                field.CycleSeconds = OverseerFieldCycleSeconds;
                field.Enabled = true;
                SetFieldVisualVisible(field, true);
            }
        }

        /// <summary>Phase 3: the zones stand down and the vents open. Public and idempotent (test seam).</summary>
        public void OpenOverseerGusts() {
            if (OverseerGustsOpen || IsBossDefeated) return;
            StandDownOverseerFields();
            OverseerGustsOpen = true;
            for (int index = 0; index < _overseerGusts.Count; index++) {
                StoryCyclicHazard gust = _overseerGusts[index];
                gust.Enabled = true;
                float delay = OverseerGustVents[index].FirstDelaySeconds;
                if (ChronalRewindManager.IsWorldHeld(GetTree())) continue;
                if (delay > 0f) gust.ForcePhase(HazardPhase.Cooldown, delay);
                else gust.ForcePhase(HazardPhase.Warning, gust.WarningDuration);
            }
        }

        /// <summary>The Overseer is down: the hero still has to walk to the sealing anchor.</summary>
        private void StandDownOverseerArena() {
            StandDownOverseerFields();
            OverseerGustsOpen = false;
            foreach (StoryCyclicHazard gust in _overseerGusts) {
                if (!IsInstanceValid(gust)) continue;
                gust.Enabled = false;
                gust.ForcePhase(HazardPhase.Cooldown, gust.CooldownDuration);
            }
        }

        private void StandDownOverseerFields() {
            OverseerFieldsCycling = false;
            _overseerFieldsDone = true;
            foreach (GravityFieldZone field in _overseerFields) {
                if (!IsInstanceValid(field)) continue;
                field.Enabled = false;
                field.SetScale(1f);
                SetFieldVisualVisible(field, false);
            }
        }
    }
}
