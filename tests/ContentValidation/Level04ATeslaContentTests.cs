using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 11 B2 - Level 4A, Tesla's Legacy Level (Chicago, 1893 - the World's Columbian Exposition). Follows the
/// <c>LevelNNContentTests</c> pattern established by the A12 Einstein exemplar.
///
/// <para>Pins the variant's contract with the campaign: the scene resolves at the
/// per-hero route path, the level ID and its two checkpoint IDs match the F12
/// convention and carry authored roles enabled on every difficulty, the dialogue set
/// and every line key resolve through localization, the authored encounter table
/// matches the locked 4A economy row (15 required-encounter + 25 boss + 10 optional
/// dust), the whole <b>canonical</b> kit is required once each with no purchased
/// Resonance node anywhere in the route, and the boss arena is wide enough for the
/// authored boss's ranged band.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level04ATeslaContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_04A_tesla.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_04a_tesla_dialogue.tres";
    private const string PoolConfigPath =
        "res://resources/Pools/level_pool_configs/level_04a_tesla_pool_config.tres";
    private const string CharacterPath = "res://resources/Characters/tesla_data.tres";
    private const int ScratchSlot = 2;

    // The locked 4A ledger row (docs/design-contracts/DUST_ECONOMY.md).
    private const int LockedRequiredEncounterDust = 15;
    private const int LockedBossDust = 25;
    private const int LockedOptionalDust = 10;

    // === Scene and identity ===

    [TestCase]
    public void TheSceneResolvesAtTheHeroRoutePathAndInstantiatesAsTheController() {
        using var session = new ScratchSession("tesla");
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus)).IsEqual(ScenePath);
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus, "tesla"))
            .IsEqual(ScenePath);
        AssertThat(ResourceLoader.Exists(ScenePath)).IsTrue();

        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        Node instance = packed.Instantiate();
        try {
            AssertThat(instance is Level04ATeslaController)
                .OverrideFailureMessage("Level_04A_tesla.tscn must instantiate as Level04ATeslaController.")
                .IsTrue();
        } finally {
            instance.Free();
        }
    }

    [TestCase]
    public void TheLevelReadiesInTheTreeWithItsManagerCameraAndLegacyFurnitureIntact() {
        using var fixture = new LegacyFixture();
        Level04ATeslaController level = fixture.Level;

        AssertString(level.LevelID).IsEqual(Level04ATeslaController.ID);
        AssertString(level.HeroCharacterID).IsEqual("tesla");
        AssertThat(level.Level == CampaignLevel.LegacyNexus).IsTrue();
        AssertString(level.LevelTitleKey).IsEqual("legacy_tesla_level_title");

        var manager = level.GetNodeOrNull<LevelManager>("LevelManager");
        AssertObject(manager).IsNotNull();
        AssertString(manager.LevelID).IsEqual(Level04ATeslaController.ID);

        AssertObject(level.Player).IsNotNull();
        AssertObject(level.Camera).IsNotNull();
        AssertObject(level.NexusSource).IsNotNull();
        AssertObject(level.EraserDebut).IsNotNull();
        AssertObject(level.Font).IsNotNull();
        AssertThat(level.KitGates.Count).IsEqual(4);

        // Every room trigger must have been back-filled with the camera path or
        // room confinement silently does nothing.
        AssertThat(level.RoomTriggers.Count > 0).IsTrue();
        foreach (RoomTransitionTrigger trigger in level.RoomTriggers) {
            AssertThat(trigger.CameraPath != null && !trigger.CameraPath.IsEmpty)
                .OverrideFailureMessage($"Room trigger '{trigger.RoomID}' has no camera path.")
                .IsTrue();
        }
    }

    [TestCase]
    public void TheTwoCheckpointsUseTheContractIDsRolesAndRegisterRespawnPositions() {
        AssertThat(Level04ATeslaController.CheckpointIDs.Length)
            .OverrideFailureMessage("F12 Option A: exactly two 4A checkpoints.").IsEqual(2);
        for (int index = 0; index < 2; index++) {
            AssertString(Level04ATeslaController.CheckpointIDs[index])
                .IsEqual($"{Level04ATeslaController.ID}_checkpoint_{index}");
        }

        using var fixture = new LegacyFixture();
        AssertString(fixture.Level.EntryCheckpointID).IsEqual(Level04ATeslaController.Checkpoint0);
        AssertString(fixture.Level.PreBossCheckpointID).IsEqual(Level04ATeslaController.Checkpoint1);

        // The roles are authored, never inferred from a numeric suffix: Hard's
        // "middle inactive" rule must not disable a PreBoss whose ID ends _1.
        AssertString(fixture.Level.CheckpointRoles[Level04ATeslaController.Checkpoint0])
            .IsEqual(LegacyCheckpointRoles.Entry);
        AssertString(fixture.Level.CheckpointRoles[Level04ATeslaController.Checkpoint1])
            .IsEqual(LegacyCheckpointRoles.PreBoss);
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            foreach (string id in Level04ATeslaController.CheckpointIDs) {
                AssertThat(fixture.Level.IsCheckpointEnabled(id, difficulty))
                    .OverrideFailureMessage($"Checkpoint '{id}' is disabled on {difficulty}.").IsTrue();
            }
        }

        foreach (string id in Level04ATeslaController.CheckpointIDs) {
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered with the LevelManager.")
                .IsTrue();
        }
    }

    // === Dialogue and localization ===

    [TestCase]
    public void TheDialogueSetResolvesWithTheThreeBeatsAndEveryLineKeyLocalized() {
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        AssertObject(set).IsNotNull();
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_04a_tesla");

        var byID = new Dictionary<string, DialogueSequenceData>(StringComparer.Ordinal);
        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertObject(sequence).IsNotNull();
            byID[sequence.DialogueID] = sequence;
        }
        foreach (string beat in new[] {
            "level_04a_tesla.entrance", "level_04a_tesla.boss_intro", "level_04a_tesla.exit"
        }) {
            AssertThat(byID.ContainsKey(beat))
                .OverrideFailureMessage($"Dialogue sequence '{beat}' is missing from the 4A Tesla set.")
                .IsTrue();
        }

        foreach (DialogueSequenceData sequence in byID.Values) {
            AssertThat(sequence.LineKeys.Length > 0).IsTrue();
            // Speaker/emotion arrays are read positionally against the lines.
            AssertThat(sequence.SpeakerNameKeys.Length).IsEqual(sequence.LineKeys.Length);
            AssertThat(sequence.EmotionKeys.Length).IsEqual(sequence.LineKeys.Length);
            foreach (string key in sequence.LineKeys) AssertLocalized(key);
            foreach (string key in sequence.SpeakerNameKeys) AssertLocalized(key);
        }

        // The controller's derived dialogue IDs must be the authored ones: the
        // Legacy prefix is the whole level ID, not the shared "level_NN" slice.
        using var fixture = new LegacyFixture();
        AssertString(fixture.Level.DialogueSetPath).IsEqual(DialoguePath);
        AssertString(fixture.Level.EntranceDialogueID).IsEqual("level_04a_tesla.entrance");
        AssertString(fixture.Level.BossIntroDialogueID).IsEqual("level_04a_tesla.boss_intro");
        AssertString(fixture.Level.ExitDialogueID).IsEqual("level_04a_tesla.exit");
    }

    [TestCase]
    public void EveryVisibleLevelStringHasALocalizationEntry() {
        foreach (string key in new[] {
            "legacy_tesla_level_title",
            "legacy_tesla_room_court",
            "legacy_tesla_room_electricity",
            "legacy_tesla_room_dynamo",
            "legacy_tesla_objective_gates", "legacy_tesla_objective_boss",
            "legacy_tesla_objective_complete",
            "legacy_gate_strike_prompt", "legacy_gate_zone_prompt",
            "legacy_gate_traversal_prompt", "legacy_gate_nexus_prompt",
            "nexus_source_prompt", "nexus_source_armed",
            "eraser_debut_bark",
            "boss_tesla_legacy_inventor_name", "speaker_tesla_legacy_inventor",
            "campaign_level_legacy_nexus"
        }) {
            AssertLocalized(key);
        }
    }

    // === Kit-keyed design: the whole CANONICAL kit, once each ===

    [TestCase]
    public void TheLevelRequiresTheHerosWholeCanonicalKitExactlyOnceEach() {
        using var fixture = new LegacyFixture();
        Level04ATeslaController level = fixture.Level;

        AssertString(level.MovementGateAbilityID).IsEqual("tesla_lightning_blink");
        AssertString(level.SpecialOneGateAbilityID).IsEqual("tesla_tesla_coil");
        AssertString(level.SpecialTwoGateAbilityID).IsEqual("tesla_lorentz_pulse");
        AssertString(level.UltimateAbilityID).IsEqual("tesla_wardenclyffe_cataclysm");

        var required = new List<string>();
        foreach (LegacyKitGate gate in level.KitGates) {
            AssertThat(string.IsNullOrWhiteSpace(gate.RequiredAbilityID))
                .OverrideFailureMessage($"Gate '{gate.GateID}' requires no ability at all.").IsFalse();
            AssertThat(required.Contains(gate.RequiredAbilityID))
                .OverrideFailureMessage($"'{gate.RequiredAbilityID}' is required by two gates.").IsFalse();
            required.Add(gate.RequiredAbilityID);
        }
        AssertThat(required.Count).IsEqual(4);

        // V01c: every gate requires a CANONICAL kit slot. A 4A gate may never depend
        // on a purchased Resonance node, so each required ID is matched against the
        // character resource's own four slots, by slot.
        var character = AuthoredResources.Load<FTT.Characters.CharacterData>(CharacterPath);
        AssertObject(character).IsNotNull();
        AssertString(character.MovementAbility?.AbilityID).IsEqual(level.MovementGateAbilityID);
        AssertString(character.SpecialAttackOne?.AbilityID).IsEqual(level.SpecialOneGateAbilityID);
        AssertString(character.SpecialAttackTwo?.AbilityID).IsEqual(level.SpecialTwoGateAbilityID);
        AssertString(character.UltimateAttack?.AbilityID).IsEqual(level.UltimateAbilityID);

        // The Ultimate is the one gate the Nexus source answers (F04).
        AssertThat(level.UltimateGate.Mode).IsEqual(LegacyGateMode.Nexus);
        AssertString(level.NexusSource.RequiredUltimateAbilityID).IsEqual(level.UltimateAbilityID);
        AssertThat(level.NexusSource.Gate).IsEqual(level.UltimateGate);
        // The movement gate is traversal; neither special is.
        AssertThat(level.MovementGate.Mode).IsEqual(LegacyGateMode.Traversal);
        AssertThat(level.SpecialOneGate.Mode).IsEqual(LegacyGateMode.Strike);
        AssertThat(level.SpecialTwoGate.Mode).IsEqual(LegacyGateMode.Strike);
    }

    [TestCase]
    public void TheCastRecognisedGateHasAWatcherBoundToItsOwnAbility() {
        // the Tesla Coil is a deployed construct: its arcs sweep only EnemyHurtbox-layer
        // hurtboxes and never reach a gate surface. Without a recogniser that gate is unopenable and the whole
        // route strands at PreBoss.
        using var fixture = new LegacyFixture();
        Level04ATeslaController level = fixture.Level;

        AssertObject(level.CoilWatcher)
            .OverrideFailureMessage("The cast-recognised gate has no LegacyCastGateWatcher.")
            .IsNotNull();
        AssertThat(level.CoilWatcher.Gate).IsEqual(level.SpecialOneGate);
        AssertString(level.CoilWatcher.RequiredAbilityID).IsEqual(level.SpecialOneGateAbilityID);
        AssertString(level.SpecialOneGate.RequiredAbilityID).IsEqual(level.CoilWatcher.RequiredAbilityID);

        // V01c: the watcher accepts its one ability and nothing else, and only
        // within range of the mechanism.
        AssertThat(level.CoilWatcher.TryRecognize("combo_1", level.CoilWatcher.GlobalPosition))
            .OverrideFailureMessage("A basic attack opened a kit gate.").IsFalse();
        AssertThat(level.CoilWatcher.TryRecognize(level.CoilWatcher.RequiredAbilityID,
            level.CoilWatcher.GlobalPosition + new Vector2(level.CoilWatcher.ResolveRadius + 200f, 0f)))
            .OverrideFailureMessage("The gate resolved from outside its own area.").IsFalse();
        AssertThat(level.SpecialOneGate.IsResolved).IsFalse();

        AssertThat(level.CoilWatcher.TryRecognize(level.CoilWatcher.RequiredAbilityID,
            level.CoilWatcher.GlobalPosition)).IsTrue();
        AssertThat(level.SpecialOneGate.IsResolved).IsTrue();
    }

    [TestCase]
    public void TheTraversalGapIsWiderThanOrdinaryTraversalAndTheLandingIsPastIt() {
        // The movement gate has to be a real gate: a severed gantry the hero cannot simply
        // walk or double-jump across, with its landing shelf on the far side.
        float gap = Level04ATeslaController.GantryGapEndX - Level04ATeslaController.GantryGapStartX;
        AssertThat(gap >= 400f)
            .OverrideFailureMessage($"The severed gantry is only {gap} px - ordinary traversal would clear it.")
            .IsTrue();

        using var fixture = new LegacyFixture();
        AssertThat(fixture.Level.MovementGate.Position.X > Level04ATeslaController.GantryGapEndX)
            .OverrideFailureMessage("The movement gate's landing box must sit past the gap.").IsTrue();
    }

    [TestCase]
    public void TheRetimedBlinkStillArrivesInsideTheTraversalGateWindow() {
        // Package 12 W4 retimed Lightning Blink to 6 startup / 12 translation /
        // 10 recovery frames over 3.0 units. The Traversal gate counts an arrival
        // while the hero is UsingMovementAbility and for TraversalGraceFrames
        // after; the translation must finish inside the cast so the landing is
        // counted even before the grace window, and the whole action must fit
        // the design's 1 s cap.
        var blink = AuthoredResources.Load<FTT.Combat.MovementAbilityData>(
            "res://resources/Abilities/tesla/movement.tres");
        int translationEnds = blink.StartupFrames + blink.ActiveFrames;
        int castEnds = translationEnds + blink.RecoveryFrames;
        AssertThat(translationEnds < castEnds).IsTrue();
        AssertThat(castEnds + LegacyKitGate.TraversalGraceFrames > translationEnds).IsTrue();
        AssertThat(castEnds <= 60).IsTrue();
        AssertFloat(blink.DistanceMoved).IsEqual(180f);
        using var fixture = new LegacyFixture();
        AssertThat(fixture.Level.MovementGate.Mode).IsEqual(LegacyGateMode.Traversal);
        AssertString(fixture.Level.MovementGate.RequiredAbilityID).IsEqual(blink.AbilityID);
    }

    // === Locked encounter economy ===

    [TestCase]
    public void TheAuthoredSpawnTableIsAllStandardsWithNoAuthoredElites() {
        int standards = 0;
        int elites = 0;
        foreach ((string enemyID, Vector2 _) in Level04ATeslaController.ApproachSpawns) {
            EnemyData data = EnemyFactory.LoadData(enemyID);
            AssertObject(data)
                .OverrideFailureMessage($"Authored spawn '{enemyID}' has no enemy resource.")
                .IsNotNull();
            if (data.Tier == EnemyTier.Standard) standards++;
            else elites++;
        }
        AssertThat(standards).IsEqual(Level04ATeslaController.AuthoredStandardCount);
        AssertThat(elites)
            .OverrideFailureMessage(
                "4A authors no elites of its own: its only elite is the scripted Eraser debut.")
            .IsEqual(Level04ATeslaController.AuthoredEliteCount);
        AssertThat(Level04ATeslaController.AuthoredEliteCount).IsEqual(0);
    }

    [TestCase]
    public void TheLockedLedgerRowIsFifteenRequiredTwentyFiveBossAndTenOptional() {
        // DUST_ECONOMY.md row 4A: identical for every character variant, regardless
        // of layout or enemy count.
        AssertThat(LegacyLevelControllerBase.RequiredEncounterDust).IsEqual(LockedRequiredEncounterDust);
        AssertThat(LegacyLevelControllerBase.BossDust).IsEqual(LockedBossDust);
        AssertThat(LegacyLevelControllerBase.OptionalDust).IsEqual(LockedOptionalDust);

        var boss = AuthoredResources.Load<BossData>(Level04ATeslaController.BossResource);
        AssertObject(boss).IsNotNull();
        AssertThat(boss.ChronalDustDrop)
            .OverrideFailureMessage("The 4A boss pays 25 dust as a Large physical pickup.")
            .IsEqual(LockedBossDust);

        // 4A's optional allocation is its secret, not an Extractor row.
        using var fixture = new LegacyFixture();
        AssertThat(fixture.Level.Extractors.Count).IsEqual(0);
    }

    [TestCase]
    public void NoWaveExceedsThePoolConfigConcurrencyBudget() {
        var config = AuthoredResources.Load<ScenePoolConfig>(PoolConfigPath);
        AssertObject(config).IsNotNull();
        AssertString(config.ConfigID).IsEqual("level_04a_tesla_pools");

        int standardWarmUp = 0;
        int eliteWarmUp = 0;
        foreach (PoolDefinition definition in config.PoolDefinitions) {
            if (definition?.PoolID == "standard_enemy") standardWarmUp = definition.WarmUpCount;
            if (definition?.PoolID == "elite_enemy") eliteWarmUp = definition.WarmUpCount;
        }
        AssertThat(standardWarmUp > 0).IsTrue();
        // The Eraser debut is an elite: its pool has to be warm or the ambush
        // silently fails to spawn and strands the route gate.
        AssertThat(eliteWarmUp > 0)
            .OverrideFailureMessage("The Eraser debut needs a warm elite_enemy budget.").IsTrue();

        AssertThat(Level04ATeslaController.AuthoredStandardCount <= standardWarmUp)
            .OverrideFailureMessage(
                $"The whole approach ({Level04ATeslaController.AuthoredStandardCount}) exceeds the " +
                $"warm standard_enemy budget ({standardWarmUp}).")
            .IsTrue();
    }

    // === Boss ===

    [TestCase]
    public void TheLegacyBossIsATwoPhaseDuplicateWhoseRangeBandsFitTheArena() {
        var data = AuthoredResources.Load<BossData>(Level04ATeslaController.BossResource);
        AssertObject(data).IsNotNull();
        AssertString(data.BossID).IsEqual("tesla_legacy_inventor");
        AssertString(data.DisplayNameKey).IsEqual("boss_tesla_legacy_inventor_name");

        // Two phases, standard boss rules (2.4).
        AssertThat(data.PhaseThresholds.Length)
            .OverrideFailureMessage("A 4A boss is two phases.").IsEqual(1);
        AssertThat(data.PhaseSpeedMultipliers.Length).IsEqual(2);
        foreach (int minPhase in data.AbilityMinPhase) {
            AssertThat(minPhase <= 1)
                .OverrideFailureMessage($"AbilityMinPhase {minPhase} names a phase a two-phase boss lacks.")
                .IsTrue();
        }

        // The variant HP row, and proof it is a duplicate rather than an edit of the
        // shared era boss. The era boss's own numbers are deliberately NOT pinned
        // here: A7b and A10 both edit shared boss resources in this same wave.
        AssertThat(data.MaxHP).IsEqual(700);
        AssertThat(Level04ATeslaController.BossResource.Contains("/Bosses/legacy/"))
            .OverrideFailureMessage("A Legacy boss lives under resources/Bosses/legacy/.").IsTrue();
        var era = AuthoredResources.Load<BossData>(Level04ATeslaController.EraBossResource);
        AssertObject(era)
            .OverrideFailureMessage("The reused era boss resource is missing.").IsNotNull();
        AssertString(era.BossID).IsEqual("chronal_inventor");
        AssertThat(era.BossID == data.BossID)
            .OverrideFailureMessage("The Legacy boss must carry its own ID, not the era boss's.")
            .IsFalse();

        using var fixture = new LegacyFixture();
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertObject(encounter.Data).IsNotNull();
        AssertString(encounter.Data.BossID).IsEqual("tesla_legacy_inventor");

        // 60 px per world unit (BossController.PixelsPerUnit): the ranged band has to
        // fit inside the boss room or the distance filter can never satisfy it.
        float arenaWidth = Level04ATeslaController.BossArenaWidth;
        AssertThat(data.RangedRangeThreshold * 60f < arenaWidth)
            .OverrideFailureMessage(
                $"Boss ranged band ({data.RangedRangeThreshold * 60f} px) does not fit the {arenaWidth} px arena.")
            .IsTrue();
    }

    // === Audio ===

    [TestCase]
    public void TheSharedLegacyAudioSetIsUsedRatherThanTheParisSlotDerivation() {
        // All nine variants share one set (P01 Option A: reuse suitable era themes).
        // The 4A level ID must NOT resolve through the shared slot derivation - that
        // would silently point at level_04_audio.tres (Paris).
        AssertString(AudioSetPaths.ForStoryLevel(Level04ATeslaController.ID)).IsEqual("");
        AssertThat(ResourceLoader.Exists(AudioSetPaths.Legacy)).IsTrue();

        var set = AuthoredResources.Load<StageAudioSet>(AudioSetPaths.Legacy);
        AssertObject(set).IsNotNull();
        AssertString(set.StageID).IsEqual(AudioSetPaths.LegacyStageID);
    }

    // === Route benchmarks ===

    [TestCase]
    public void TheVariantCarriesItsOwnParAndEntryRecoveryBudget() {
        using var fixture = new LegacyFixture();
        // V01a: each distinct 4A route has its own benchmark; nothing pools them.
        AssertThat(fixture.Level.ParSeconds > 0)
            .OverrideFailureMessage("Every 4A variant authors its own par (V01a).").IsTrue();
        AssertThat(fixture.Level.EntryRecoveryBudgetSeconds > 0)
            .OverrideFailureMessage("Every 4A variant authors an F11 Entry recovery budget.").IsTrue();
    }

    // === Helpers ===

    private static void AssertLocalized(string key) {
        AssertThat(TranslationServer.Translate(key).ToString() != key)
            .OverrideFailureMessage($"Key '{key}' is missing from localization/en.csv.")
            .IsTrue();
    }

    private sealed class ScratchSession : IDisposable {
        private readonly string _originalCharacter;
        private readonly int _originalSlot;

        public ScratchSession(string character) {
            SessionData session = GameManager.Instance.CurrentSession;
            _originalCharacter = session.SelectedCharacterID;
            _originalSlot = session.ActiveSaveSlot;
            session.SelectedCharacterID = character;
            session.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession = session;
        }

        public void Dispose() {
            SessionData session = GameManager.Instance.CurrentSession;
            session.SelectedCharacterID = _originalCharacter;
            session.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession = session;
        }
    }

    /// <summary>
    /// Instantiates the authored scene in the runner tree, restoring every shared
    /// singleton it touches on dispose.
    /// </summary>
    private sealed class LegacyFixture : IDisposable {
        public readonly Level04ATeslaController Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;

        public LegacyFixture() {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;
            SessionData session = GameManager.Instance.CurrentSession;
            _originalSlot = session.ActiveSaveSlot;
            _originalCharacter = session.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = "tesla";
            GameManager.Instance.CurrentSession = session;

            var packed = ResourceLoader.Load<PackedScene>(ScenePath);
            Level = packed.Instantiate<Level04ATeslaController>();
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(Level);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            SessionData session = GameManager.Instance.CurrentSession;
            session.ActiveSaveSlot = _originalSlot;
            session.SelectedCharacterID = _originalCharacter;
            GameManager.Instance.CurrentSession = session;
        }
    }
}
