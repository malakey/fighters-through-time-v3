using System;
using System.Collections.Generic;
using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// The two authored checkpoint roles a Level 4A variant carries (F12 Option A).
    ///
    /// <para>Deliberately <b>string constants, not an enum</b>: A3 is adding the
    /// shared <c>CheckpointRole</c> export (<c>Entry | Middle | PreBoss</c>) to
    /// <see cref="CheckpointTrigger"/> in the same wave, and a second enum of the
    /// same shape would collide at merge. The Legacy levels declare their roles here
    /// with the same spellings A3 uses, and role tagging is applied to the triggers
    /// at merge — see the plan's §9 A12 block.</para>
    /// </summary>
    public static class LegacyCheckpointRoles {
        public const string Entry = "Entry";
        public const string PreBoss = "PreBoss";
    }

    /// <summary>
    /// The shared framework for Level 4A, the per-character Legacy Level (V7.6,
    /// Package 11 §2.4 and <c>docs/design-contracts/LEGACY_CHECKPOINTS.md</c>).
    ///
    /// <para><b>What this base owns</b> — identical for all nine variants, so no
    /// variant re-authors it:</para>
    /// <list type="bullet">
    ///   <item><b>Identity.</b> <c>level_04a_&lt;hero&gt;</c>,
    ///     <see cref="CampaignLevel.LegacyNexus"/>, the per-hero dialogue set path,
    ///     and the shared 4A audio set.</item>
    ///   <item><b>Exactly two checkpoints</b>, roles Entry and PreBoss, both enabled
    ///     on Easy, Normal and Hard. No Middle. Enablement and the boss-clock lock
    ///     read <see cref="CheckpointRoles"/>, never the ID suffix — Hard's
    ///     "middle inactive" rule cannot disable a role-PreBoss checkpoint whose
    ///     default ID happens to end <c>_1</c>.</item>
    ///   <item><b>The required-route ordering gate.</b> Entry → the four kit gates and
    ///     the independent Eraser debut → the late Font approach → PreBoss → boss.
    ///     PreBoss physically cannot be struck until every mandatory pre-boss
    ///     objective — all four kit gates including the F04 Nexus set-piece, and the
    ///     Eraser encounter — is complete.</item>
    ///   <item><b>One Restoration Font</b>, placed on the late approach after the
    ///     Eraser and before PreBoss, with the existing difficulty uses/potency. It
    ///     is a healing object, not a third checkpoint.</item>
    ///   <item><b>The boss slot</b>, the 15/25/10 dust ledger row, a
    ///     <see cref="ParSeconds"/> slot for A3's Integrity timer, and the F11 Entry
    ///     recovery budget slot.</item>
    /// </list>
    ///
    /// <para><b>What a variant supplies</b> — the complete per-hero surface B1–B3
    /// implement, and nothing else:</para>
    /// <list type="number">
    ///   <item><see cref="HeroCharacterID"/> — the lowercase roster ID. Drives the
    ///     level ID, the scene path, the dialogue set and the pool config.</item>
    ///   <item><see cref="LevelTitleKey"/> and the three objective keys.</item>
    ///   <item><see cref="BossResourcePath"/> — <c>res://resources/Bosses/legacy/&lt;hero&gt;_legacy_boss.tres</c>,
    ///     a duplicate of an existing era boss with a variant HP row. Never edit a
    ///     shared boss resource.</item>
    ///   <item>The four kit-gate ability IDs: <see cref="MovementGateAbilityID"/>,
    ///     <see cref="SpecialOneGateAbilityID"/>, <see cref="SpecialTwoGateAbilityID"/>,
    ///     <see cref="UltimateAbilityID"/>, plus <see cref="SpecialOneGateMode"/> and
    ///     <see cref="SpecialTwoGateMode"/> where the hero's specials are not the
    ///     projectile/zone shapes Einstein's are.</item>
    ///   <item>The eleven authored placements (<see cref="EntryCheckpointPosition"/>
    ///     through <see cref="BossSpawnPosition"/>) and the level's bounds/spawn.</item>
    ///   <item><see cref="BuildNexusGeometry"/> — rooms, floors, platforms, room
    ///     transitions, decoration and the era palette.</item>
    ///   <item><see cref="AuthoredStandardSpawns"/> — the approach encounters.</item>
    ///   <item><see cref="ParSeconds"/> and <see cref="EntryRecoveryBudgetSeconds"/>
    ///     (V01a/F11: its own hero/route benchmark, never pooled with another
    ///     variant's).</item>
    /// </list>
    ///
    /// <para>Story-only throughout. Nothing here is visible to
    /// <c>scripts/FighterSim/</c>.</para>
    /// </summary>
    public abstract partial class LegacyLevelControllerBase : StoryLevelControllerBase {

        // === The locked ledger row (docs/design-contracts/DUST_ECONOMY.md) =========
        // Every variant pays the same envelope regardless of layout or enemy count.
        // A10 supplies the per-source allocator in Wave 2; these are the budget.

        /// <summary>Required-encounter dust for the whole 4A approach.</summary>
        public const int RequiredEncounterDust = 15;

        /// <summary>Boss dust, as a Large physical pickup at the arena centre.</summary>
        public const int BossDust = 25;

        /// <summary>The level's whole optional allocation, never a per-object reward.</summary>
        public const int OptionalDust = 10;

        // === Identity ===

        /// <summary>The lowercase roster character ID this variant belongs to.</summary>
        public abstract string HeroCharacterID { get; }

        public sealed override string LevelID => StoryManager.LegacyLevelID(HeroCharacterID);

        public sealed override CampaignLevel Level => CampaignLevel.LegacyNexus;

        public override string DialogueSetPath => $"res://resources/Dialogue/{LevelID}_dialogue.tres";

        /// <summary>
        /// All nine variants share one audio set (P01 Option A: reuse suitable era
        /// themes; a variant needing its own overrides this).
        /// </summary>
        protected override string AudioSetPath => AudioSetPaths.Legacy;

        /// <summary>Dialogue IDs are <c>level_04a_&lt;hero&gt;.entrance</c> and friends.</summary>
        public override string DialoguePrefix => LevelID;

        // === Authored identifiers (LEGACY_CHECKPOINTS.md defaults) ===

        public string EntryCheckpointID => $"{LevelID}_checkpoint_0";
        public string PreBossCheckpointID => $"{LevelID}_checkpoint_1";
        public string EraserDebutTriggerID => $"{LevelID}_eraser_debut";
        public string RestorationFontID => $"{LevelID}_font";
        public string NexusSourceID => $"{LevelID}_nexus";
        public string MovementGateID => $"{LevelID}_gate_movement";
        public string SpecialOneGateID => $"{LevelID}_gate_special_1";
        public string SpecialTwoGateID => $"{LevelID}_gate_special_2";
        public string UltimateGateID => $"{LevelID}_gate_ultimate";

        /// <summary>
        /// The authored ID → role map. Exactly two entries, both enabled on every
        /// difficulty. The role is authored separately from the stable ID precisely
        /// so nothing ever infers PreBoss from a numeric suffix.
        /// </summary>
        public IReadOnlyDictionary<string, string> CheckpointRoles =>
            new Dictionary<string, string>(StringComparer.Ordinal) {
                { EntryCheckpointID, LegacyCheckpointRoles.Entry },
                { PreBossCheckpointID, LegacyCheckpointRoles.PreBoss }
            };

        /// <summary>
        /// F12 Option A: both 4A roles are enabled on Easy, Normal and Hard. The Hard
        /// "middle inactive" rule applies to shared Acts I–II levels only, and 4A has
        /// no Middle to disable.
        /// </summary>
        public bool IsCheckpointEnabled(string checkpointID, Difficulty difficulty) =>
            CheckpointRoles.ContainsKey(checkpointID);

        // === Per-hero surface the variants implement ===

        /// <summary>The variant's boss resource, under <c>resources/Bosses/legacy/</c>.</summary>
        public abstract string BossResourcePath { get; }

        /// <summary>Movement-ability ID; always a Traversal gate.</summary>
        public abstract string MovementGateAbilityID { get; }

        /// <summary>Special 1 ability ID.</summary>
        public abstract string SpecialOneGateAbilityID { get; }

        /// <summary>Special 2 ability ID.</summary>
        public abstract string SpecialTwoGateAbilityID { get; }

        /// <summary>Ultimate ability ID; always resolved through the Nexus source.</summary>
        public abstract string UltimateAbilityID { get; }

        /// <summary>How Special 1 is recognised. Default: a struck mechanism.</summary>
        protected virtual LegacyGateMode SpecialOneGateMode => LegacyGateMode.Strike;

        /// <summary>How Special 2 is recognised. Default: a placed zone.</summary>
        protected virtual LegacyGateMode SpecialTwoGateMode => LegacyGateMode.Zone;

        /// <summary>V01a: this route's own Normal median, rounded up. Never pooled across variants.</summary>
        public abstract int NexusParSeconds { get; }

        /// <summary>Package 11 integration: forwards the variant's authored par into A3's
        /// Integrity clock. The earlier <c>abstract int ParSeconds</c> HID the base
        /// <c>virtual float ParSeconds</c>, so no 4A variant ever started its clock.</summary>
        public sealed override float ParSeconds => NexusParSeconds;

        /// <summary>F11: the Entry recovery budget covering the full mandatory approach.</summary>
        public virtual int EntryRecoveryBudgetSeconds => NexusParSeconds;

        protected abstract Vector2 EntryCheckpointPosition { get; }
        protected abstract Vector2 MovementGatePosition { get; }
        protected abstract Vector2 SpecialOneGatePosition { get; }
        protected abstract Vector2 SpecialTwoGatePosition { get; }
        protected abstract Vector2 NexusSourcePosition { get; }
        protected abstract Vector2 UltimateTargetPosition { get; }
        protected abstract Vector2 EraserDebutPosition { get; }
        protected abstract Vector2 RestorationFontPosition { get; }
        protected abstract Vector2 PreBossCheckpointPosition { get; }
        protected abstract Vector2 BossSpawnPosition { get; }

        /// <summary>Distance at which the boss reveals. Variants widen it for a bigger arena.</summary>
        protected virtual float BossRevealDistance => 900f;

        /// <summary>Rooms, surfaces, room transitions, decoration — everything era-specific.</summary>
        protected abstract void BuildNexusGeometry();

        /// <summary>The approach encounters, in authored order.</summary>
        public abstract IReadOnlyList<(string EnemyID, Vector2 Position)> AuthoredStandardSpawns { get; }

        // === Runtime handles (test seams and variant hooks) ===

        public LegacyKitGate MovementGate { get; private set; }
        public LegacyKitGate SpecialOneGate { get; private set; }
        public LegacyKitGate SpecialTwoGate { get; private set; }
        public LegacyKitGate UltimateGate { get; private set; }
        public NexusResonanceSource NexusSource { get; private set; }
        public EraserDebutTrigger EraserDebut { get; private set; }
        public RestorationFont Font { get; private set; }
        public CheckpointTrigger EntryCheckpoint { get; private set; }
        public CheckpointTrigger PreBossCheckpoint { get; private set; }

        /// <summary>Every kit gate, in route order.</summary>
        public IReadOnlyList<LegacyKitGate> KitGates => _kitGates;
        private readonly List<LegacyKitGate> _kitGates = new();

        /// <summary>
        /// The mandatory pre-boss objectives: the four kit gates plus the Eraser
        /// encounter. PreBoss cannot activate while any of them is outstanding.
        /// </summary>
        public int MandatoryObjectivesRemaining {
            get {
                int remaining = 0;
                foreach (LegacyKitGate gate in _kitGates) {
                    if (IsInstanceValid(gate) && !gate.IsResolved) remaining++;
                }
                if (EraserDebut != null && IsInstanceValid(EraserDebut) && !EraserDebut.EncounterCleared) {
                    remaining++;
                }
                return remaining;
            }
        }

        /// <summary>True once every mandatory pre-boss objective is complete.</summary>
        public bool CanActivatePreBoss => MandatoryObjectivesRemaining == 0;

        // === Construction ===

        /// <summary>
        /// Sealed: the Legacy route order is the contract, not a variant choice. A
        /// variant supplies its geometry through <see cref="BuildNexusGeometry"/> and
        /// its placements through the position properties.
        /// </summary>
        protected sealed override void BuildLevel() {
            BuildNexusGeometry();

            EntryCheckpoint = BuildCheckpoint(
                EntryCheckpointPosition.X, EntryCheckpointPosition.Y, EntryCheckpointID, CheckpointRole.Entry);

            BuildKitGates();
            BuildEraserDebut();
            BuildRestorationFont();

            PreBossCheckpoint = BuildCheckpoint(
                PreBossCheckpointPosition.X, PreBossCheckpointPosition.Y, PreBossCheckpointID, CheckpointRole.PreBoss);
            // The route gate is physical: the fracture's strike surface stays inert
            // until every mandatory objective is complete, so PreBoss can never be
            // struck early and lock the Integrity clock ahead of the approach.
            SetPreBossStrikeEnabled(CanActivatePreBoss);

            BuildBossEncounter(BossResourcePath, BossSpawnPosition,
                $"{HeroCharacterID}_legacy_boss_encounter", BossRevealDistance);
        }

        private void BuildKitGates() {
            _kitGates.Clear();
            MovementGate = BuildKitGate(MovementGateID, MovementGateAbilityID,
                LegacyGateMode.Traversal, MovementGatePosition);
            SpecialOneGate = BuildKitGate(SpecialOneGateID, SpecialOneGateAbilityID,
                SpecialOneGateMode, SpecialOneGatePosition);
            SpecialTwoGate = BuildKitGate(SpecialTwoGateID, SpecialTwoGateAbilityID,
                SpecialTwoGateMode, SpecialTwoGatePosition);
            UltimateGate = BuildKitGate(UltimateGateID, UltimateAbilityID,
                LegacyGateMode.Nexus, UltimateTargetPosition);

            NexusSource = new NexusResonanceSource {
                Name = "NexusResonanceSource",
                SourceID = NexusSourceID,
                RequiredUltimateAbilityID = UltimateAbilityID,
                Position = NexusSourcePosition,
                Gate = UltimateGate
            };
            NexusSource.AddChild(BuildInteractionArea(new Vector2(180f, 200f)));
            NexusSource.Solved += _ => OnMandatoryObjectiveChanged();
            AddChild(NexusSource);
        }

        private LegacyKitGate BuildKitGate(string gateID, string abilityID,
            LegacyGateMode mode, Vector2 position) {
            var gate = new LegacyKitGate {
                Name = $"KitGate_{gateID}",
                GateID = gateID,
                RequiredAbilityID = abilityID,
                Mode = mode,
                Position = position
            };
            gate.Resolved += _ => OnMandatoryObjectiveChanged();
            AddChild(gate);
            _kitGates.Add(gate);
            return gate;
        }

        private void BuildEraserDebut() {
            EraserDebut = new EraserDebutTrigger {
                Name = "EraserDebut",
                TriggerID = EraserDebutTriggerID,
                Position = EraserDebutPosition
            };
            EraserDebut.DebutBegan += OnEraserDebutBegan;
            EraserDebut.DebutCleared += _ => OnMandatoryObjectiveChanged();
            AddChild(EraserDebut);
        }

        private void BuildRestorationFont() {
            Font = new RestorationFont {
                Name = "RestorationFont",
                FontID = RestorationFontID,
                Position = RestorationFontPosition
            };
            // The proximity box is deliberately narrower than the Font's own
            // ChannelRangePixels (90): a player who can start the channel must not
            // already be standing at the edge of the range that interrupts it.
            Font.AddChild(BuildInteractionArea(new Vector2(120f, 180f)));
            AddChild(Font);
        }

        /// <summary>
        /// An <see cref="InteractionArea"/> with a real collision shape. The area is
        /// built in code rather than instanced from a template, and an Area2D with no
        /// shape never fires <c>BodyEntered</c> — the prompt would simply never appear
        /// and the prop would be silently uninteractable.
        /// </summary>
        private static InteractionArea BuildInteractionArea(Vector2 size) {
            var area = new InteractionArea {
                Name = "Interaction",
                TargetPath = "..",
                PromptLabelPath = "Prompt"
            };
            area.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = size },
                Position = new Vector2(0f, -size.Y / 2f)
            });
            return area;
        }

        // === Route ordering ===

        /// <summary>
        /// Re-evaluates the PreBoss gate after any mandatory objective changes.
        /// Virtual so a variant can layer presentation, but the gate write itself
        /// stays with the base.
        /// </summary>
        protected virtual void OnMandatoryObjectiveChanged() {
            SetPreBossStrikeEnabled(CanActivatePreBoss);
            RefreshLegacyObjective();
        }

        /// <summary>
        /// Enables or disables the PreBoss fracture's strike surface. Uses the
        /// physics-safe setters: this runs from hit and body callbacks, where the
        /// engine refuses direct monitoring writes.
        /// </summary>
        private void SetPreBossStrikeEnabled(bool enabled) {
            if (PreBossCheckpoint == null || !IsInstanceValid(PreBossCheckpoint)) return;
            var surface = PreBossCheckpoint.GetNodeOrNull<Area2D>("StrikeSurface");
            if (surface == null) return;
            surface.SetMonitoringSafe(enabled);
            surface.SetMonitorableSafe(enabled);
        }

        /// <summary>True while the PreBoss fracture will actually answer a strike.</summary>
        public bool PreBossStrikeArmed {
            get {
                var surface = PreBossCheckpoint?.GetNodeOrNull<Area2D>("StrikeSurface");
                return surface != null && surface.Monitorable;
            }
        }

        /// <summary>
        /// Sarah's non-blocking debut bark. Never pauses gameplay — it is a barked
        /// line over a live ambush, not a sequence. Variants may override to route it
        /// through their own presentation.
        /// </summary>
        protected virtual void OnEraserDebutBegan(EraserDebutTrigger trigger) {
            SetObjective(EraserDebutTrigger.DebutBarkKey);
        }

        // === Lifecycle ===

        protected override void SpawnInitialEnemies() {
            foreach ((string enemyID, Vector2 position) in AuthoredStandardSpawns) {
                FTT.Enemies.EnemyFactory.Spawn(enemyID, this, position);
            }
        }

        protected override void OnLevelReady() {
            SetPreBossStrikeEnabled(CanActivatePreBoss);
            RefreshLegacyObjective();
        }

        /// <summary>
        /// F10 reconstruction. Before PreBoss the approach is rebuilt and every gate
        /// is outstanding again; at PreBoss the saved baseline treats the whole
        /// required approach as complete — all four gates latched, the Eraser cleared,
        /// its reward still claimed — and the Nexus source restores <b>disabled</b>
        /// rather than armed.
        /// </summary>
        protected override void MarkWavesClearedThrough(string checkpointID) {
            bool approachComplete = checkpointID == PreBossCheckpointID;
            EraserDebut?.RestoreFromCheckpoint(approachComplete, rewardAlreadyClaimed: approachComplete);
            if (approachComplete) {
                foreach (LegacyKitGate gate in _kitGates) {
                    if (IsInstanceValid(gate)) gate.ForceResolve();
                }
            }
            NexusSource?.RestoreFromCheckpoint(approachComplete);
        }

        // === Objectives ===

        private void RefreshLegacyObjective() {
            if (IsBossDefeated) { SetObjective(CompletionObjectiveKey); return; }
            if (CanActivatePreBoss) { SetObjective(BossObjectiveKey); return; }
            SetObjective(InitialObjectiveKey, 4 - GatesRemaining(), 4);
        }

        private int GatesRemaining() {
            int remaining = 0;
            foreach (LegacyKitGate gate in _kitGates) {
                if (IsInstanceValid(gate) && !gate.IsResolved) remaining++;
            }
            return remaining;
        }

        protected override void OnBossDefeated(BossEncounterController encounter, BossDefeatedPayload payload) {
            base.OnBossDefeated(encounter, payload);
            RefreshLegacyObjective();
        }
    }
}
