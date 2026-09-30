using Godot;
using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.Enemies {

    /// <summary>
    /// Level 13 Mirror Paradox (design-godot.md Section 6): a clone of the player's
    /// locked campaign character driven by the Fighter CPU decision engine at the
    /// <b>active Story difficulty</b>. It deliberately bypasses
    /// <see cref="BossController"/> entirely — there is no
    /// <see cref="BossData.BossAbilities"/> pattern, no phase threshold, and no
    /// telegraph/rest cadence. The clone fights with the player's own basic string,
    /// specials, movement ability, and ultimate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>V7.6 F20 campaign profile</b> (<c>docs/design-contracts/MIRROR_PARADOX.md</c>).
    /// The tier is the Story difficulty, never the saved Holodeck CPU setting, and it
    /// selects the reaction window (30–45 / 15–20 / 4–8 frames), the block/hitstun/DI
    /// attempt rates (10-40-80 / 10-45-85 / 0-40-80), the HP pool (700 / 1000 / 1500
    /// from the authored 1000 base) and the outgoing damage multiplier
    /// (0.5x / 1.0x / 1.5x). The band comes from
    /// <see cref="CpuBandTuning.BossOverride"/>, so the boss keeps its <b>full core
    /// kit on every difficulty</b> — Easy's practice preset would otherwise zero both
    /// Specials, the movement ability and the Ultimate.
    /// </para>
    /// <para>
    /// On Easy and Normal the clone is built from <b>normalized base resources
    /// only</b>. On <b>Hard</b> it mirrors the player's purchased Resonance grid
    /// nodes — stat profile and <see cref="PlayerController.StoryAbilityPerks"/> —
    /// and nothing else: no wallet, no current HP, no cooldowns, no spent flags. The
    /// V7.5 Legacy ability-slot gate is never installed on any tier, because F20 puts
    /// core-kit access outside Story ability locks. The encounter stays one phase.
    /// </para>
    /// <para>
    /// Decisions come from <see cref="FighterCpuController"/> itself, driven through
    /// the mode-neutral <see cref="CpuDecisionObservation"/>. Story state never flows
    /// back into <c>scripts/FighterSim/</c>; the adapter below is a one-way projection
    /// from Godot state into the shared decision table, and the quantized intents come
    /// back as a <see cref="PlayerInputFrame"/> the clone consumes like any input device.
    /// </para>
    /// </remarks>
    public partial class MirrorParadoxController : Node2D, IStoryRewindSimulation, IStoryTimeFreezable {
        /// <summary>Scene-tree group the clone joins so level logic can tell it from the player.</summary>
        public const string MirrorGroup = "MirrorParadox";

        /// <summary>Local slot the mirror occupies; slot 1 maps to the Enemy collision layers.</summary>
        public const int DefaultMirrorPlayerIndex = 1;

        private static int _spawnCounter;

        [ExportGroup("Encounter")]
        [Export] public BossData Data;
        /// <summary>Overrides the session's locked character; encounters leave this empty.</summary>
        [Export] public string CharacterIDOverride = "";
        [Export] public int MirrorPlayerIndex = DefaultMirrorPlayerIndex;
        [Export] public Vector2 SpawnOffset;
        [Export] public bool SpawnOnReady = true;
        /// <summary>
        /// When false the clone spawns inert and waits for <see cref="BeginEncounter"/>.
        /// <see cref="MirrorParadoxEncounterController"/> sets this false so the fight
        /// starts on reveal; a level dropping the controller in bare keeps the default.
        /// </summary>
        [Export] public bool ActivateOnSpawn = true;
        /// <summary>Non-zero pins the CPU decision stream for tests and replays.</summary>
        [Export] public ulong DecisionSeed;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.PreserveCurrentState;

        public PlayerController Clone { get; private set; }
        public MirrorParadoxDecisionAdapter Decisions { get; private set; }
        public string MirroredCharacterID { get; private set; } = "";
        public bool IsDefeated { get; private set; }
        public bool IsStoryRewindFrozen { get; private set; }

        /// <summary>True once <see cref="BeginEncounter"/> has handed the clone to the CPU.</summary>
        public bool IsEncounterActive { get; private set; }

        /// <summary>Story difficulty-scaled HP pool; the canonical 1000 stays in BossData.</summary>
        public int ScaledMaxHP { get; private set; }

        // === Package 11 A7b — F20 campaign AI profile ===

        /// <summary>
        /// The Story difficulty this encounter was built at, latched at spawn.
        /// F20: "Select this boss profile from the active Story difficulty,
        /// independently of the saved Holodeck CPU setting." Nothing here reads the
        /// Holodeck preference, so that independence is structural.
        /// </summary>
        public Difficulty EncounterDifficulty { get; private set; } = Difficulty.Normal;

        /// <summary>The CPU tier the profile selected; 1:1 with the Story tier.</summary>
        public CpuDifficulty EncounterCpuDifficulty => ToCpuDifficulty(EncounterDifficulty);

        /// <summary>
        /// F20: the clone mirrors the player's <b>purchased</b> grid perks on Hard
        /// only. Easy and Normal fight the normalized character.
        /// </summary>
        public bool MirrorsPurchasedPerks => EncounterDifficulty == Difficulty.Hard;

        /// <summary>
        /// The campaign outgoing-damage multiplier applied to the clone
        /// (0.5x / 1.0x / 1.5x), from the single Story source
        /// <see cref="StoryDifficultyTuning.GetEnemyDamageMultiplier"/>. Applied
        /// exactly once, at spawn.
        /// </summary>
        public float OutgoingDamageMultiplier =>
            StoryDifficultyTuning.GetEnemyDamageMultiplier(EncounterDifficulty);

        /// <summary>Story tier to CPU tier. One mapping, no third ladder.</summary>
        public static CpuDifficulty ToCpuDifficulty(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => CpuDifficulty.Easy,
            Difficulty.Hard => CpuDifficulty.Hard,
            _ => CpuDifficulty.Normal
        };

        public int CurrentHP => Clone != null && IsInstanceValid(Clone) ? Clone.CurrentHP : 0;

        /// <summary>Single phase, always. The Mirror Paradox has no phase transitions.</summary>
        public int PhaseCount => 1;

        private bool _eventsBound;
        private bool _spawnAnnounced;
        private bool _inputSourceRegistered;
        private Vector2 _spawnPosition;
        private Vector2 _checkpointPosition;
        private int _checkpointHP;
        private bool _checkpointCaptured;

        public override void _Ready() {
            _spawnPosition = GlobalPosition + SpawnOffset;
            // The controller joins the mirror group itself so ChronalRewindManager's
            // world-freeze sweep finds this IStoryRewindSimulation: the clone is a
            // PlayerController and never joins "Enemies", which is exactly how the
            // Mirror escaped the rewind freeze (audit H-8).
            AddToGroup(MirrorGroup);
            BindEvents();
            if (SpawnOnReady) SpawnMirror();
        }

        public override void _ExitTree() {
            UnbindEvents();
            ReleaseInputSource();
        }

        /// <summary>
        /// Stops a subtree simulating and unhooks it from the physics broadphase.
        /// Used by <see cref="DespawnMirror"/> while the clone is still safely in the
        /// tree; never from <c>_ExitTree</c>, where mutating physics state during
        /// tree removal is itself unsupported.
        /// </summary>
        private static void MakeSubtreeInert(Node node) {
            if (node == null || !IsInstanceValid(node)) return;
            node.SetPhysicsProcess(false);
            node.SetProcess(false);
            if (node is Area2D area) {
                area.Monitoring = false;
                area.Monitorable = false;
            }
            if (node is CollisionObject2D collision) {
                collision.CollisionLayer = 0;
                collision.CollisionMask = 0;
            }
            foreach (Node child in node.GetChildren()) MakeSubtreeInert(child);
        }

        /// <summary>
        /// Explicit safe teardown for a level that ends the encounter without
        /// unloading the whole scene. Detaches the clone before freeing it so the
        /// destruction never happens while the node is still inside the tree.
        /// </summary>
        public void DespawnMirror() {
            ReleaseInputSource();
            IsEncounterActive = false;
            if (Clone == null || !IsInstanceValid(Clone)) {
                Clone = null;
                return;
            }
            MakeSubtreeInert(Clone);
            Clone.RemoveFromGroup(MirrorGroup);
            Clone.RemoveFromGroup("Players");
            // Detach first, then free. Out of the tree the destruction is immediate
            // and deterministic, with no orphan left sitting in the deletion queue.
            RemoveChild(Clone);
            Clone.Free();
            Clone = null;
            Decisions = null;
        }

        // === Spawn ===

        /// <summary>
        /// Builds the clone from the locked character and wires it to the Hard CPU
        /// decision engine. Safe to call twice; the second call is a no-op.
        /// </summary>
        public PlayerController SpawnMirror() {
            if (Clone != null && IsInstanceValid(Clone)) return Clone;

            MirroredCharacterID = ResolveCharacterID();
            if (string.IsNullOrWhiteSpace(MirroredCharacterID)) {
                GD.PushWarning("Mirror Paradox has no locked character to mirror.");
                return null;
            }

            // F20: the whole profile keys off the Story difficulty, latched once
            // here so a mid-encounter settings change cannot re-tier the fight.
            EncounterDifficulty = StoryDifficultyTuning.CurrentStoryDifficulty;

            // 1000 base x 0.7/1.0/1.5 = the contract's 700/1000/1500, applied once.
            ScaledMaxHP = StoryDifficultyTuning.ScaleEnemyHP(
                Data?.MaxHP ?? 1000, EncounterDifficulty);

            // F20 clone construction:
            //  - Hard mirrors the player's PURCHASED Resonance nodes (stat profile
            //    and ability perks). Easy and Normal get normalized base resources
            //    only, exactly as before.
            //  - The V7.5 Legacy ability-slot gate is never installed on either
            //    tier: "Core-kit access is independent of Story ability locks at
            //    encounter time", so a half-unlocked campaign still faces a mirror
            //    with both Specials, its movement ability and its Ultimate.
            //  - Nothing copies the player's wallet, current HP, cooldowns or spent
            //    flags: CreateCharacter reads only the grid, never live state.
            Clone = CharacterFactory.CreateCharacter(
                MirroredCharacterID, MirrorPlayerIndex,
                applyStoryProgression: MirrorsPurchasedPerks,
                applyLegacyUnlockLocks: false);
            Clone.Name = "MirrorParadox";
            // Single application of the HP modifier: EncounterMaxHPOverride REPLACES
            // the character baseline outright (PlayerController.MaximumHP), so a
            // mirrored MaxHP perk can never stack a second base pool on top of the
            // boss pool. F20: "without copying the player's current HP or stacking a
            // second character base-HP pool."
            Clone.EncounterMaxHPOverride = ScaledMaxHP;
            ApplyCampaignDamageMultiplier(Clone);
            // A02 (Package 13 W6): "a boss or CPU using a player kit against the
            // player (the Mirror Paradox clone) uses the Fighter activation-strike
            // version" — its Ultimate must connect before the cinematic plays.
            Clone.UsesFighterUltimateActivation = true;
            // Hostile marker: the clone's projectiles join the enemy_projectile
            // group so a rewind's world clear removes them (audit H-8).
            Clone.IsStoryHostile = true;
            Clone.Position = SpawnOffset;
            AddChild(Clone);

            // The mirror is an opponent, not the campaign avatar: level flow, the
            // Story camera, rewind, and the HUD all resolve "StoryPlayer".
            Clone.RemoveFromGroup("StoryPlayer");
            Clone.AddToGroup(MirrorGroup);

            Decisions = new MirrorParadoxDecisionAdapter(
                ResolveDecisionSeed(), EncounterCpuDifficulty);
            Decisions.Bind(Clone, FindCampaignPlayer());

            // The clone stands inert until the encounter actually begins. Before
            // that it must not consume input, simulate, or spawn attack VFX and
            // pooled objects into the level (BossEncounterController has the same
            // reveal-gated semantics).
            Clone.SetPhysicsProcess(false);
            AnnounceSpawn();
            if (ActivateOnSpawn) BeginEncounter();
            return Clone;
        }

        /// <summary>
        /// Starts the fight: the clone begins simulating and the Hard CPU engine
        /// takes over its input slot. Idempotent.
        /// </summary>
        public void BeginEncounter() {
            if (IsEncounterActive || IsDefeated) return;
            if (Clone == null || !IsInstanceValid(Clone)) return;
            IsEncounterActive = true;
            InputManager.Instance?.SetInputSource(MirrorPlayerIndex, Decisions);
            _inputSourceRegistered = InputManager.Instance != null;
            Clone.SetPhysicsProcess(true);
        }

        /// <summary>
        /// F20's "Outgoing campaign damage multiplier" row (0.5x / 1.0x / 1.5x),
        /// applied exactly once at spawn.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The clone is a <see cref="PlayerController"/>, so it never passes through
        /// <c>EnemyAbilityExecutor.DamageMultiplier</c> — the chokepoint every
        /// ordinary Story enemy uses. The equivalent seam on a character body is the
        /// pair of Story damage multipliers, which both the basic string
        /// (<c>StartComboHit</c> / <c>StartDirectionalAttack</c> recompute
        /// <c>BasicAttackDamage x StoryBasicDamageMultiplier</c> every swing) and
        /// every <c>BaseSpecial</c> (which reads
        /// <c>Owner.StorySpecialDamageMultiplier</c> when it builds a hitbox) already
        /// consume. On Hard those lanes may already carry the mirrored Resonance
        /// damage perks, so this composes with them rather than replacing them —
        /// each modifier applied once, which is exactly F20's rule.
        /// </para>
        /// <para>
        /// The one gap that multiplication alone leaves is the handful of hitboxes
        /// <c>CharacterFactory</c> builds at construction time and an ability never
        /// re-damages; those are scaled in place below. A hitbox that <i>is</i>
        /// re-damaged at cast simply has that construction-time value overwritten by
        /// the multiplied one, so nothing is scaled twice.
        /// </para>
        /// </remarks>
        private void ApplyCampaignDamageMultiplier(PlayerController clone) {
            float multiplier = OutgoingDamageMultiplier;
            clone.StoryBasicDamageMultiplier *= multiplier;
            clone.StorySpecialDamageMultiplier *= multiplier;
            if (Mathf.IsEqualApprox(multiplier, 1f)) return;
            ScaleAuthoredHitboxDamage(clone, multiplier);
        }

        private static void ScaleAuthoredHitboxDamage(Node node, float multiplier) {
            if (node is Hitbox hitbox) hitbox.Damage *= multiplier;
            foreach (Node child in node.GetChildren()) ScaleAuthoredHitboxDamage(child, multiplier);
        }

        private string ResolveCharacterID() {
            if (!string.IsNullOrWhiteSpace(CharacterIDOverride)) return CharacterIDOverride.Trim();
            return GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "";
        }

        private int ResolveDecisionSeed() => DecisionSeed != 0
            ? unchecked((int)DecisionSeed)
            : unchecked(1301_13 + System.Threading.Interlocked.Increment(ref _spawnCounter));

        private PlayerController FindCampaignPlayer() {
            if (!IsInsideTree()) return null;
            Godot.Collections.Array<Node> players = GetTree().GetNodesInGroup("Players");
            using var playersLifetime = players.AsDisposable();
            foreach (Node node in players) {
                if (node is PlayerController player && player != Clone) return player;
            }
            return GetTree().GetFirstNodeInGroup("StoryPlayer") as PlayerController;
        }

        public override void _PhysicsProcess(double delta) {
            if (IsDefeated || IsStoryRewindFrozen || Decisions == null) return;
            if (Decisions.Target == null || !IsInstanceValid(Decisions.Target)) {
                Decisions.Bind(Clone, FindCampaignPlayer());
            }
        }

        // === Boss events ===

        private void BindEvents() {
            if (_eventsBound || EventBus.Instance == null) return;
            _eventsBound = true;
            EventBus.Instance.OnPlayerHPChanged += OnMirrorHPChanged;
            EventBus.Instance.OnPlayerDied += OnMirrorDied;
            EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
            EventBus.Instance.OnRewindTriggered += OnRewindTriggered;
        }

        private void UnbindEvents() {
            if (!_eventsBound) return;
            _eventsBound = false;
            if (EventBus.Instance == null) return;
            EventBus.Instance.OnPlayerHPChanged -= OnMirrorHPChanged;
            EventBus.Instance.OnPlayerDied -= OnMirrorDied;
            EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
            EventBus.Instance.OnRewindTriggered -= OnRewindTriggered;
        }

        private void AnnounceSpawn() {
            if (_spawnAnnounced) return;
            _spawnAnnounced = true;
            EventBus.Instance?.RaiseBossSpawned(new BossSpawnedPayload {
                BossID = Data?.BossID ?? "mirror_paradox",
                DisplayNameKey = Data?.DisplayNameKey ?? "",
                CurrentHP = CurrentHP,
                MaxHP = ScaledMaxHP,
                Position = Clone?.GlobalPosition ?? GlobalPosition
            });
            RaiseHPChanged();
        }

        private void OnMirrorHPChanged(PlayerHPPayload payload) {
            if (IsDefeated || payload.PlayerIndex != MirrorPlayerIndex) return;
            RaiseHPChanged();
        }

        private void OnMirrorDied(int playerIndex) {
            if (playerIndex != MirrorPlayerIndex) return;
            Die();
        }

        private void RaiseHPChanged() {
            EventBus.Instance?.RaiseBossHPChanged(new BossHPPayload {
                BossID = Data?.BossID ?? "mirror_paradox",
                CurrentHP = CurrentHP,
                MaxHP = ScaledMaxHP
            });
        }

        /// <summary>
        /// Ends the encounter: the clone stops taking decisions and the shared boss
        /// defeat event carries the authored dust drop.
        /// </summary>
        public void Die() {
            if (IsDefeated) return;
            IsDefeated = true;
            IsEncounterActive = false;
            ReleaseInputSource();
            // A dead mirror stops simulating immediately; the corpse stays in the
            // scene for the level's outro to dispose of.
            if (Clone != null && IsInstanceValid(Clone)) Clone.SetPhysicsProcess(false);
            RaiseHPChanged();
            EventBus.Instance?.RaiseBossDefeated(new BossDefeatedPayload {
                BossID = Data?.BossID ?? "mirror_paradox",
                Position = Clone != null && IsInstanceValid(Clone) ? Clone.GlobalPosition : GlobalPosition,
                ChronalDustDrop = Data?.ChronalDustDrop ?? 25
            });
        }

        private void ReleaseInputSource() {
            if (!_inputSourceRegistered) return;
            _inputSourceRegistered = false;
            InputManager.Instance?.ClearInputSource(MirrorPlayerIndex);
        }

        // === Rewind ===

        /// <summary>Freezes like an enemy: the clone stops simulating during a rewind.</summary>
        public void SetStoryRewindFrozen(bool frozen) {
            IsStoryRewindFrozen = frozen;
            if (Clone == null || !IsInstanceValid(Clone)) return;
            Clone.SetRewindSuspended(frozen);
            // Unfreezing restores the encounter's own activation state; it must not
            // start an unrevealed or already-defeated mirror simulating.
            Clone.SetPhysicsProcess(!frozen && IsEncounterActive && !IsDefeated);
            if (frozen) Clone.Velocity = Vector2.Zero;
        }

        /// <summary>
        /// V7.6 Time Freeze. The clone is a PlayerController, so freezing it means
        /// suspending its own simulation — but unlike the rewind path this must
        /// NOT zero its velocity: the mirror resumes mid-leap exactly where it
        /// stopped, with no catch-up tick.
        /// </summary>
        public void SetTimeFrozen(bool frozen) {
            if (Clone == null || !IsInstanceValid(Clone)) return;
            Clone.SetPhysicsProcess(!frozen && IsEncounterActive && !IsDefeated);
        }

        public void CaptureCheckpointState(string checkpointID) {
            if (IsDefeated || Clone == null || !IsInstanceValid(Clone)) return;
            _checkpointPosition = Clone.GlobalPosition;
            _checkpointHP = Clone.CurrentHP;
            _checkpointCaptured = true;
        }

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            if (Clone == null || !IsInstanceValid(Clone)) return;
            bool useCheckpoint = RewindPolicy == StoryRewindPolicy.RestoreCheckpointState && _checkpointCaptured;
            Vector2 position = useCheckpoint ? _checkpointPosition : _spawnPosition;
            int hp = useCheckpoint && _checkpointHP > 0 ? _checkpointHP : ScaledMaxHP;
            Clone.RestoreStoryCheckpoint(position, hp, 0f);
            Clone.Velocity = Vector2.Zero;
            RaiseHPChanged();
        }

        private void OnRewindTriggered(Vector2 targetPosition) => ApplyStoryRewind();
    }

    /// <summary>
    /// Story-side adapter that drives the deterministic <see cref="FighterCpuController"/>
    /// from Godot state. It projects the clone and the campaign player into a
    /// <see cref="CpuDecisionObservation"/>, then hands the quantized intents back to
    /// the clone as an ordinary <see cref="PlayerInputFrame"/> through
    /// <see cref="InputManager.SetInputSource"/>. Nothing flows the other way, so the
    /// deterministic simulation stays free of Godot and Story dependencies.
    /// </summary>
    public sealed class MirrorParadoxDecisionAdapter : IPlayerInputSource {
        /// <summary>Story world scale: <c>PlayerController</c> works in pixels, the CPU table in units.</summary>
        public const float StoryPixelsPerUnit = 60f;

        private readonly FighterCpuController _cpu;
        private PlayerController _self;

        /// <summary>
        /// Package 11 A7b (F20). The tier is supplied by the encounter, mapped from
        /// the active <b>Story</b> difficulty — it is no longer hardcoded to Hard.
        /// The band comes from <see cref="CpuBandTuning.BossOverride"/> rather than
        /// <c>CpuBandTuning.For</c>, so Easy keeps the boss's full core kit instead
        /// of inheriting the practice CPU's stripped preset.
        /// </summary>
        public MirrorParadoxDecisionAdapter(int seed, CpuDifficulty difficulty) {
            _cpu = new FighterCpuController(
                difficulty, seed, geometry: null, world: null,
                tuningOverride: CpuBandTuning.BossOverride(difficulty));
        }

        public PlayerController Target { get; private set; }

        /// <summary>The tier this adapter's decision engine runs at.</summary>
        public CpuDifficulty Difficulty => _cpu.Difficulty;

        /// <summary>The boss-override band in force; tests pin the F20 matrix here.</summary>
        public CpuBandTuning Tuning => _cpu.Tuning;

        /// <summary>
        /// Reaction window for the selected tier: F20's 30–45 / 15–20 / 4–8 frames,
        /// read straight from the shared engine so the two can never drift.
        /// </summary>
        public int ReactionDelayMinFrames => _cpu.GetReactionDelayBounds(out _);

        public int ReactionDelayMaxFrames {
            get {
                _cpu.GetReactionDelayBounds(out int maximum);
                return maximum;
            }
        }

        public void Bind(PlayerController self, PlayerController target) {
            _self = self;
            Target = target;
        }

        public PlayerInputFrame Sample(uint tick, in PlayerInputFrame previousFrame) {
            if (_self == null || !GodotObject.IsInstanceValid(_self)) {
                return PlayerInputFrame.Create(tick, 0f, 0f, GameplayButtons.None, previousFrame.Held);
            }
            CpuDecisionObservation observation = Observe();
            return _cpu.Sample(tick, in observation, in previousFrame);
        }

        /// <summary>
        /// Projects the current Story frame onto the shared decision observation.
        /// </summary>
        /// <remarks>
        /// Story world units are pixels with <b>+Y down</b>, so every Y quantity is
        /// negated to match the Fighter simulation's +Y-up convention. The stage,
        /// orb, hazard and floor-topology blocks are left at their "absent"
        /// sentinels (<c>HasStageBounds</c>/<c>HasOrb</c>/<c>HasHazard</c>/
        /// <c>HasFloorSegments</c> all zero): a campaign level has no blast zone,
        /// no Chronal Orbs, no Fighter stage hazards and no authored floor
        /// segments, and those flags are exactly what keep the shared table's
        /// off-stage recovery, orb pursuit, hazard evasion and pit-awareness
        /// branches from firing here. Package 6 §2.5 requires both Observe paths to
        /// stay field-for-field aligned.
        /// </remarks>
        public CpuDecisionObservation Observe() {
            if (_self == null || !GodotObject.IsInstanceValid(_self)) return default;
            bool targetValid = Target != null && GodotObject.IsInstanceValid(Target);
            float selfUnitsX = _self.GlobalPosition.X / StoryPixelsPerUnit;
            float selfUnitsY = -_self.GlobalPosition.Y / StoryPixelsPerUnit;
            float targetUnitsX = targetValid ? Target.GlobalPosition.X / StoryPixelsPerUnit : selfUnitsX;
            float targetUnitsY = targetValid ? -Target.GlobalPosition.Y / StoryPixelsPerUnit : selfUnitsY;
            bool alive = _self.CurrentState != CharacterState.Dead
                && _self.CurrentState != CharacterState.Respawning;

            var observation = new CpuDecisionObservation {
                SelfPositionXRaw = FP64.FromFloat(selfUnitsX).RawValue,
                SelfPositionYRaw = FP64.FromFloat(selfUnitsY).RawValue,
                SelfVelocityXRaw = FP64.FromFloat(_self.Velocity.X / StoryPixelsPerUnit).RawValue,
                SelfVelocityYRaw = FP64.FromFloat(-_self.Velocity.Y / StoryPixelsPerUnit).RawValue,
                TargetPositionXRaw = FP64.FromFloat(targetUnitsX).RawValue,
                TargetPositionYRaw = FP64.FromFloat(targetUnitsY).RawValue,
                Stocks = alive ? 1 : 0,
                HitstunFrames = _self.CurrentState == CharacterState.Stunned ? 1 : 0,
                DazeFrames = _self.CurrentState == CharacterState.Dazed ? 1 : 0,
                IsGrounded = _self.IsOnFloor() ? 1 : 0,
                IsLedgeHanging = _self.CurrentState == CharacterState.LedgeHanging ? 1 : 0,
                RemainingJumps = _self.RemainingJumps,
                SelfCurrentHP = _self.CurrentHP,
                SelfMaxHP = _self.MaximumHP,
                InfluenceRaw = FP64.FromFloat(Math.Max(0f, _self.CurrentUltimateMeter)).RawValue,
                SpecialOneCooldownFrames = ToFrames(_self.SpecialOneCooldownTimer),
                SpecialTwoCooldownFrames = ToFrames(_self.SpecialTwoCooldownTimer),
                MovementCooldownFrames = ToFrames(_self.MovementAbilityCooldownTimer),
                TargetCurrentHP = targetValid ? Target.CurrentHP : 0,
                TargetMaxHP = targetValid ? Target.MaximumHP : 0,
                TargetHitstunFrames = targetValid && Target.CurrentState == CharacterState.Stunned ? 1 : 0,
                TargetPressedButtons = targetValid ? (int)Target.CurrentInputFrame.Pressed : 0,
                TargetInfluenceRaw = targetValid
                    ? FP64.FromFloat(Math.Max(0f, Target.CurrentUltimateMeter)).RawValue
                    : 0
                // HasStageBounds / HasOrb / HasHazard / HasFloorSegments /
                // HasVerbState / HasTargetVerbState / SuppressGameplayInput stay
                // 0: Story has no Fighter stage, no orbs, no stage hazards, no
                // authored floor segments and no verb layer, and the encounter's
                // own reveal gate owns whether the clone acts at all. The verb
                // sentinels (Package 11 A9b) are what keep the F19 grab and Echo
                // Step branches out of a campaign level entirely.
            };
            ProjectNearestHostileProjectile(ref observation);
            return observation;
        }

        /// <summary>
        /// M-8 mirror of <c>FighterSimulationWorldObserver.TryGetNearestHostileProjectile</c>:
        /// the nearest live Story projectile owned by another player slot (the
        /// campaign player's shots), projected into world units with +Y up. Enemy
        /// shots (owner &lt; 0) are not hostile to the mirror, which fights on the
        /// Enemy side. Story projectiles travel horizontally, so the Y velocity is
        /// zero and the X sign carries the closing direction the decision table
        /// reads.
        /// </summary>
        private void ProjectNearestHostileProjectile(ref CpuDecisionObservation observation) {
            if (!_self.IsInsideTree()) return;
            Godot.Collections.Array<Node> projectiles =
                _self.GetTree().GetNodesInGroup("story_projectile");
            using var projectilesLifetime = projectiles.AsDisposable();
            bool found = false;
            float best = 0f;
            foreach (Node node in projectiles) {
                if (node is not PlaceholderProjectile projectile
                    || !GodotObject.IsInstanceValid(projectile)) continue;
                if (projectile.OwnerPlayerIndex < 0
                    || projectile.OwnerPlayerIndex == _self.PlayerIndex) continue;
                Vector2 delta = projectile.GlobalPosition - _self.GlobalPosition;
                float distance = Mathf.Abs(delta.X) + Mathf.Abs(delta.Y);
                if (found && distance >= best) continue;
                best = distance;
                found = true;
                observation.HasHostileProjectile = 1;
                observation.ProjectileRelativeXRaw =
                    FP64.FromFloat(delta.X / StoryPixelsPerUnit).RawValue;
                observation.ProjectileRelativeYRaw =
                    FP64.FromFloat(-delta.Y / StoryPixelsPerUnit).RawValue;
                observation.ProjectileVelocityXRaw =
                    FP64.FromFloat(projectile.HorizontalVelocity / StoryPixelsPerUnit).RawValue;
                observation.ProjectileVelocityYRaw = 0;
            }
        }

        private static int ToFrames(float seconds) =>
            seconds <= 0f ? 0 : Mathf.Max(1, Mathf.CeilToInt(seconds * 60f));
    }
}
