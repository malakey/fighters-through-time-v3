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
/// Package 11 A12 — Level 4A, Einstein's Legacy Level (Princeton, 1945). Follows the
/// <c>LevelNNContentTests</c> pattern and is the template the three B-wave agents
/// copy for the other eight heroes.
///
/// <para>Pins the variant's contract with the campaign: the scene resolves at the
/// per-hero route path, the level ID and its two checkpoint IDs match the F12
/// convention, the dialogue set and every line key resolve through localization, the
/// authored encounter table matches the locked 4A economy row (15 required-encounter
/// + 25 boss + 10 optional dust), the whole kit is required once each, and the boss
/// arena is wide enough for the authored boss's ranged band.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level04AEinsteinContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_04A_einstein.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_04a_einstein_dialogue.tres";
    private const string PoolConfigPath =
        "res://resources/Pools/level_pool_configs/level_04a_einstein_pool_config.tres";
    private const int ScratchSlot = 2;

    // The locked 4A ledger row (docs/design-contracts/DUST_ECONOMY.md).
    private const int LockedRequiredEncounterDust = 15;
    private const int LockedBossDust = 25;
    private const int LockedOptionalDust = 10;

    // === Scene and identity ===

    [TestCase]
    public void TheSceneResolvesAtTheHeroRoutePathAndInstantiatesAsTheController() {
        using var session = new ScratchSession("einstein");
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus)).IsEqual(ScenePath);
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus, "einstein"))
            .IsEqual(ScenePath);
        AssertThat(ResourceLoader.Exists(ScenePath)).IsTrue();

        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        Node instance = packed.Instantiate();
        try {
            AssertThat(instance is Level04AEinsteinController)
                .OverrideFailureMessage("Level_04A_einstein.tscn must instantiate as Level04AEinsteinController.")
                .IsTrue();
        } finally {
            instance.Free();
        }
    }

    [TestCase]
    public void TheLevelReadiesInTheTreeWithItsManagerCameraAndLegacyFurnitureIntact() {
        using var fixture = new Level04AFixture(null);
        Level04AEinsteinController level = fixture.Level;

        AssertString(level.LevelID).IsEqual(Level04AEinsteinController.ID);
        AssertString(level.HeroCharacterID).IsEqual("einstein");
        AssertThat(level.Level == CampaignLevel.LegacyNexus).IsTrue();
        AssertString(level.LevelTitleKey).IsEqual("legacy_einstein_level_title");

        var manager = level.GetNodeOrNull<LevelManager>("LevelManager");
        AssertObject(manager).IsNotNull();
        AssertString(manager.LevelID).IsEqual(Level04AEinsteinController.ID);

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
    public void TheTwoCheckpointsUseTheContractIDsAndRegisterRespawnPositions() {
        AssertThat(Level04AEinsteinController.CheckpointIDs.Length)
            .OverrideFailureMessage("F12 Option A: exactly two 4A checkpoints.").IsEqual(2);
        for (int index = 0; index < 2; index++) {
            AssertString(Level04AEinsteinController.CheckpointIDs[index])
                .IsEqual($"{Level04AEinsteinController.ID}_checkpoint_{index}");
        }

        using var fixture = new Level04AFixture(null);
        AssertString(fixture.Level.EntryCheckpointID).IsEqual(Level04AEinsteinController.Checkpoint0);
        AssertString(fixture.Level.PreBossCheckpointID).IsEqual(Level04AEinsteinController.Checkpoint1);
        foreach (string id in Level04AEinsteinController.CheckpointIDs) {
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
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_04a_einstein");

        var byID = new Dictionary<string, DialogueSequenceData>(StringComparer.Ordinal);
        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertObject(sequence).IsNotNull();
            byID[sequence.DialogueID] = sequence;
        }
        foreach (string beat in new[] {
            "level_04a_einstein.entrance", "level_04a_einstein.boss_intro", "level_04a_einstein.exit"
        }) {
            AssertThat(byID.ContainsKey(beat))
                .OverrideFailureMessage($"Dialogue sequence '{beat}' is missing from the 4A Einstein set.")
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
        using var fixture = new Level04AFixture(null);
        AssertString(fixture.Level.DialogueSetPath).IsEqual(DialoguePath);
        AssertString(fixture.Level.EntranceDialogueID).IsEqual("level_04a_einstein.entrance");
        AssertString(fixture.Level.BossIntroDialogueID).IsEqual("level_04a_einstein.boss_intro");
        AssertString(fixture.Level.ExitDialogueID).IsEqual("level_04a_einstein.exit");
    }

    [TestCase]
    public void EveryVisibleLevelStringHasALocalizationEntry() {
        foreach (string key in new[] {
            "legacy_einstein_level_title",
            "legacy_einstein_room_lawn", "legacy_einstein_room_containment",
            "legacy_einstein_room_vault",
            "legacy_einstein_objective_gates", "legacy_einstein_objective_boss",
            "legacy_einstein_objective_complete",
            "legacy_gate_strike_prompt", "legacy_gate_zone_prompt",
            "legacy_gate_traversal_prompt", "legacy_gate_nexus_prompt",
            "nexus_source_prompt", "nexus_source_armed",
            "eraser_debut_bark",
            "boss_einstein_legacy_overseer_name", "speaker_einstein_legacy_overseer",
            "campaign_level_legacy_nexus"
        }) {
            AssertLocalized(key);
        }
    }

    // === Kit-keyed design: the whole kit, once each ===

    [TestCase]
    public void TheLevelRequiresEinsteinsWholeKitExactlyOnceEach() {
        using var fixture = new Level04AFixture(null);
        Level04AEinsteinController level = fixture.Level;

        // Einstein's four canonical ability IDs, one gate each.
        AssertString(level.MovementGateAbilityID).IsEqual("einstein_relativity_warp");
        AssertString(level.SpecialOneGateAbilityID).IsEqual("einstein_mass_energy_conversion");
        AssertString(level.SpecialTwoGateAbilityID).IsEqual("einstein_relativity_rift");
        AssertString(level.UltimateAbilityID).IsEqual("einstein_cosmological_constant");

        var required = new List<string>();
        foreach (LegacyKitGate gate in level.KitGates) {
            AssertThat(string.IsNullOrWhiteSpace(gate.RequiredAbilityID))
                .OverrideFailureMessage($"Gate '{gate.GateID}' requires no ability at all.").IsFalse();
            AssertThat(required.Contains(gate.RequiredAbilityID))
                .OverrideFailureMessage($"'{gate.RequiredAbilityID}' is required by two gates.").IsFalse();
            required.Add(gate.RequiredAbilityID);
        }
        AssertThat(required.Count).IsEqual(4);

        // Every required ability is one Einstein actually has.
        var character = AuthoredResources.Load<FTT.Characters.CharacterData>(
            "res://resources/Characters/einstein_data.tres");
        AssertObject(character).IsNotNull();
        var authored = new HashSet<string>(StringComparer.Ordinal);
        foreach (FTT.Combat.AbilityData ability in new FTT.Combat.AbilityData[] {
            character.SpecialAttackOne, character.SpecialAttackTwo,
            character.MovementAbility, character.UltimateAttack
        }) {
            if (ability != null) authored.Add(ability.AbilityID);
        }
        AssertThat(authored.Count).IsEqual(4);
        foreach (string abilityID in required) {
            AssertThat(authored.Contains(abilityID))
                .OverrideFailureMessage($"Gate ability '{abilityID}' is not in Einstein's authored kit.")
                .IsTrue();
        }

        // The Ultimate is the one gate the Nexus source answers (F04).
        AssertThat(level.UltimateGate.Mode).IsEqual(LegacyGateMode.Nexus);
        AssertString(level.NexusSource.RequiredUltimateAbilityID).IsEqual(level.UltimateAbilityID);
        AssertThat(level.NexusSource.Gate).IsEqual(level.UltimateGate);
        // The movement gate is traversal; neither special is.
        AssertThat(level.MovementGate.Mode).IsEqual(LegacyGateMode.Traversal);
        AssertThat(level.SpecialOneGate.Mode).IsEqual(LegacyGateMode.Strike);
        AssertThat(level.SpecialTwoGate.Mode).IsEqual(LegacyGateMode.Zone);
    }

    [TestCase]
    public void TheWarpGapIsWiderThanOrdinaryTraversalAndTheLandingIsPastIt() {
        // The movement gate has to be a real gate: a gap Einstein cannot simply
        // walk or double-jump across, with its landing shelf on the far side.
        float gap = Level04AEinsteinController.SpanGapEndX - Level04AEinsteinController.SpanGapStartX;
        AssertThat(gap >= 400f)
            .OverrideFailureMessage($"The Warp span is only {gap} px — ordinary traversal would clear it.")
            .IsTrue();

        using var fixture = new Level04AFixture(null);
        AssertThat(fixture.Level.MovementGate.Position.X > Level04AEinsteinController.SpanGapEndX)
            .OverrideFailureMessage("The movement gate's landing box must sit past the span.").IsTrue();
    }

    // === Locked encounter economy ===

    [TestCase]
    public void TheAuthoredSpawnTableIsAllStandardsWithNoAuthoredElites() {
        int standards = 0;
        int elites = 0;
        foreach ((string enemyID, Vector2 _) in Level04AEinsteinController.ApproachSpawns) {
            EnemyData data = EnemyFactory.LoadData(enemyID);
            AssertObject(data)
                .OverrideFailureMessage($"Authored spawn '{enemyID}' has no enemy resource.")
                .IsNotNull();
            if (data.Tier == EnemyTier.Standard) standards++;
            else elites++;
        }
        AssertThat(standards).IsEqual(Level04AEinsteinController.AuthoredStandardCount);
        AssertThat(elites)
            .OverrideFailureMessage(
                "4A authors no elites of its own: its only elite is the scripted Eraser debut.")
            .IsEqual(Level04AEinsteinController.AuthoredEliteCount);
        AssertThat(Level04AEinsteinController.AuthoredEliteCount).IsEqual(0);
    }

    [TestCase]
    public void TheLockedLedgerRowIsFifteenRequiredTwentyFiveBossAndTenOptional() {
        // DUST_ECONOMY.md row 4A: identical for every character variant, regardless
        // of layout or enemy count.
        AssertThat(LegacyLevelControllerBase.RequiredEncounterDust).IsEqual(LockedRequiredEncounterDust);
        AssertThat(LegacyLevelControllerBase.BossDust).IsEqual(LockedBossDust);
        AssertThat(LegacyLevelControllerBase.OptionalDust).IsEqual(LockedOptionalDust);

        var boss = AuthoredResources.Load<BossData>(Level04AEinsteinController.BossResource);
        AssertObject(boss).IsNotNull();
        AssertThat(boss.ChronalDustDrop)
            .OverrideFailureMessage("The 4A boss pays 25 dust as a Large physical pickup.")
            .IsEqual(LockedBossDust);

        // 4A's optional allocation is its secret, not an Extractor row.
        using var fixture = new Level04AFixture(null);
        AssertThat(fixture.Level.Extractors.Count).IsEqual(0);
    }

    [TestCase]
    public void NoWaveExceedsThePoolConfigConcurrencyBudget() {
        var config = AuthoredResources.Load<ScenePoolConfig>(PoolConfigPath);
        AssertObject(config).IsNotNull();
        AssertString(config.ConfigID).IsEqual("level_04a_einstein_pools");

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

        AssertThat(Level04AEinsteinController.AuthoredStandardCount <= standardWarmUp)
            .OverrideFailureMessage(
                $"The whole approach ({Level04AEinsteinController.AuthoredStandardCount}) exceeds the " +
                $"warm standard_enemy budget ({standardWarmUp}).")
            .IsTrue();
    }

    // === Boss ===

    [TestCase]
    public void TheLegacyBossIsATwoPhaseDuplicateWhoseRangeBandsFitTheArena() {
        var data = AuthoredResources.Load<BossData>(Level04AEinsteinController.BossResource);
        AssertObject(data).IsNotNull();
        AssertString(data.BossID).IsEqual("einstein_legacy_overseer");
        AssertString(data.DisplayNameKey).IsEqual("boss_einstein_legacy_overseer_name");

        // Two phases, standard boss rules (§2.4).
        AssertThat(data.PhaseThresholds.Length)
            .OverrideFailureMessage("A 4A boss is two phases.").IsEqual(1);
        AssertThat(data.PhaseSpeedMultipliers.Length).IsEqual(2);
        foreach (int minPhase in data.AbilityMinPhase) {
            AssertThat(minPhase <= 1)
                .OverrideFailureMessage($"AbilityMinPhase {minPhase} names a phase a two-phase boss lacks.")
                .IsTrue();
        }

        // The variant HP row, and proof it did not come from editing a shared boss.
        AssertThat(data.MaxHP).IsEqual(700);
        var shared = AuthoredResources.Load<BossData>("res://resources/Bosses/gravity_overseer.tres");
        AssertObject(shared).IsNotNull();
        AssertString(shared.BossID).IsEqual("gravity_overseer");
        AssertThat(shared.MaxHP)
            .OverrideFailureMessage("The shared era boss must be untouched by the Legacy duplicate.")
            .IsEqual(950);

        using var fixture = new Level04AFixture(null);
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertObject(encounter.Data).IsNotNull();
        AssertString(encounter.Data.BossID).IsEqual("einstein_legacy_overseer");

        // 60 px per world unit (BossController.PixelsPerUnit): the ranged band has
        // to fit inside the boss room or the distance filter can never satisfy it.
        float arenaWidth = Level04AEinsteinController.BossArenaWidth;
        AssertThat(data.RangedRangeThreshold * 60f < arenaWidth)
            .OverrideFailureMessage(
                $"Boss ranged band ({data.RangedRangeThreshold * 60f} px) does not fit the {arenaWidth} px arena.")
            .IsTrue();
    }

    // === Audio ===

    [TestCase]
    public void TheSharedLegacyAudioSetIsAuthoredAndSatisfiesTheStemContract() {
        // All nine variants share one set (P01 Option A: reuse suitable era themes).
        // It is named rather than derived, because AudioSetPaths.ForStoryLevel reads
        // a two-digit slot and 4A has none.
        AssertString(AudioSetPaths.Legacy).IsEqual("res://resources/Audio/level_04a_audio.tres");
        AssertThat(ResourceLoader.Exists(AudioSetPaths.Legacy)).IsTrue();

        var set = AuthoredResources.Load<StageAudioSet>(AudioSetPaths.Legacy);
        AssertObject(set).IsNotNull();
        AssertString(set.SetID).IsEqual("audio_level_04a");
        AssertString(set.StageID).IsEqual(AudioSetPaths.LegacyStageID);
        AssertObject(set.AmbientStem).IsNotNull();
        AssertObject(set.CombatStem).IsNotNull();
        AssertObject(set.ClimaxStem).IsNotNull();

        // The 4A level ID must NOT resolve through the shared slot derivation —
        // that would silently point at level_04_audio.tres (Paris).
        AssertString(AudioSetPaths.ForStoryLevel(Level04AEinsteinController.ID)).IsEqual("");
    }

    // === Route benchmarks ===

    [TestCase]
    public void TheVariantCarriesItsOwnParAndEntryRecoveryBudget() {
        using var fixture = new Level04AFixture(null);
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
    /// Instantiates the authored scene in the runner tree against an optional
    /// scratch save, restoring every shared singleton it touches on dispose.
    /// </summary>
    private sealed class Level04AFixture : IDisposable {
        public readonly Level04AEinsteinController Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;

        public Level04AFixture(StorySaveData save) {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;
            SessionData session = GameManager.Instance.CurrentSession;
            _originalSlot = session.ActiveSaveSlot;
            _originalCharacter = session.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            session.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            session.SelectedCharacterID = "einstein";
            GameManager.Instance.CurrentSession = session;
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            var packed = ResourceLoader.Load<PackedScene>(ScenePath);
            Level = packed.Instantiate<Level04AEinsteinController>();
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
