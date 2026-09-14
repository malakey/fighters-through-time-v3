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
/// Package 11 B3 — Level 4A, Mozart's Legacy Level (Vienna, 1782). Follows the
/// <c>LevelNNContentTests</c> pattern and mirrors
/// <c>Level04AEinsteinContentTests</c>, the A12 exemplar.
///
/// <para>Pins the variant's contract with the campaign: the scene resolves at the
/// per-hero route path, the level ID and its two checkpoint IDs match the F12
/// convention, the dialogue set and every line key resolve through localization, the
/// authored encounter table matches the locked 4A economy row (15 required-encounter
/// + 25 boss + 10 optional dust), the whole kit is required once each, the route
/// order puts the Font after the Eraser and before a gated PreBoss, and the boss
/// arena is wide enough for the authored boss's ranged band.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level04AMozartContentTests {

    private const string Hero = "mozart";
    private const string ScenePath = "res://scenes/campaign/Level_04A_mozart.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_04a_mozart_dialogue.tres";
    private const string PoolConfigPath =
        "res://resources/Pools/level_pool_configs/level_04a_mozart_pool_config.tres";
    private const int ScratchSlot = 2;

    // The locked 4A ledger row (docs/design-contracts/DUST_ECONOMY.md).
    private const int LockedRequiredEncounterDust = 15;
    private const int LockedBossDust = 25;
    private const int LockedOptionalDust = 10;

    // === Scene and identity ===

    [TestCase]
    public void TheSceneResolvesAtTheHeroRoutePathAndInstantiatesAsTheController() {
        using var session = new ScratchSession(Hero);
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus)).IsEqual(ScenePath);
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus, Hero)).IsEqual(ScenePath);
        AssertThat(ResourceLoader.Exists(ScenePath)).IsTrue();

        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        Node instance = packed.Instantiate();
        try {
            AssertThat(instance is Level04AMozartController)
                .OverrideFailureMessage("Level_04A_mozart.tscn must instantiate as Level04AMozartController.")
                .IsTrue();
        } finally {
            instance.Free();
        }
    }

    [TestCase]
    public void TheLevelReadiesInTheTreeWithItsManagerCameraAndLegacyFurnitureIntact() {
        using var fixture = new Level04AFixture();
        Level04AMozartController level = fixture.Level;

        AssertString(level.LevelID).IsEqual(Level04AMozartController.ID);
        AssertString(level.HeroCharacterID).IsEqual(Hero);
        AssertThat(level.Level == CampaignLevel.LegacyNexus).IsTrue();
        AssertString(level.LevelTitleKey).IsEqual("legacy_mozart_level_title");

        var manager = level.GetNodeOrNull<LevelManager>("LevelManager");
        AssertObject(manager).IsNotNull();
        AssertString(manager.LevelID).IsEqual(Level04AMozartController.ID);

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
        AssertThat(Level04AMozartController.CheckpointIDs.Length)
            .OverrideFailureMessage("F12 Option A: exactly two 4A checkpoints.").IsEqual(2);
        for (int index = 0; index < 2; index++) {
            AssertString(Level04AMozartController.CheckpointIDs[index])
                .IsEqual($"{Level04AMozartController.ID}_checkpoint_{index}");
        }

        using var fixture = new Level04AFixture();
        AssertString(fixture.Level.EntryCheckpointID).IsEqual(Level04AMozartController.Checkpoint0);
        AssertString(fixture.Level.PreBossCheckpointID).IsEqual(Level04AMozartController.Checkpoint1);

        // Roles are authored, never inferred from the numeric suffix.
        AssertString(fixture.Level.CheckpointRoles[Level04AMozartController.Checkpoint0])
            .IsEqual(LegacyCheckpointRoles.Entry);
        AssertString(fixture.Level.CheckpointRoles[Level04AMozartController.Checkpoint1])
            .IsEqual(LegacyCheckpointRoles.PreBoss);
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            foreach (string id in Level04AMozartController.CheckpointIDs) {
                AssertThat(fixture.Level.IsCheckpointEnabled(id, difficulty))
                    .OverrideFailureMessage($"Checkpoint '{id}' must stay enabled on {difficulty}.")
                    .IsTrue();
            }
        }

        foreach (string id in Level04AMozartController.CheckpointIDs) {
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
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_04a_mozart");

        var byID = new Dictionary<string, DialogueSequenceData>(StringComparer.Ordinal);
        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertObject(sequence).IsNotNull();
            byID[sequence.DialogueID] = sequence;
        }
        foreach (string beat in new[] {
            "level_04a_mozart.entrance", "level_04a_mozart.boss_intro", "level_04a_mozart.exit"
        }) {
            AssertThat(byID.ContainsKey(beat))
                .OverrideFailureMessage($"Dialogue sequence '{beat}' is missing from the 4A Mozart set.")
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

        using var fixture = new Level04AFixture();
        AssertString(fixture.Level.DialogueSetPath).IsEqual(DialoguePath);
        AssertString(fixture.Level.EntranceDialogueID).IsEqual("level_04a_mozart.entrance");
        AssertString(fixture.Level.BossIntroDialogueID).IsEqual("level_04a_mozart.boss_intro");
        AssertString(fixture.Level.ExitDialogueID).IsEqual("level_04a_mozart.exit");
    }

    [TestCase]
    public void EveryVisibleLevelStringHasALocalizationEntry() {
        foreach (string key in new[] {
            "legacy_mozart_level_title",
            "legacy_mozart_room_platz", "legacy_mozart_room_stage", "legacy_mozart_room_vault",
            "legacy_mozart_objective_gates", "legacy_mozart_objective_boss",
            "legacy_mozart_objective_complete",
            "legacy_gate_strike_prompt", "legacy_gate_traversal_prompt", "legacy_gate_nexus_prompt",
            "nexus_source_prompt", "nexus_source_armed",
            "eraser_debut_bark",
            "boss_mozart_legacy_impresario_name", "speaker_mozart_legacy_impresario",
            "campaign_level_legacy_nexus"
        }) {
            AssertLocalized(key);
        }
    }

    // === Kit-keyed design: the whole kit, once each ===

    [TestCase]
    public void TheLevelRequiresMozartsWholeKitExactlyOnceEach() {
        using var fixture = new Level04AFixture();
        Level04AMozartController level = fixture.Level;

        AssertString(level.MovementGateAbilityID).IsEqual("mozart_sonata_drift");
        AssertString(level.SpecialOneGateAbilityID).IsEqual("mozart_requiem_chord");
        AssertString(level.SpecialTwoGateAbilityID).IsEqual("mozart_fortissimo_wave");
        AssertString(level.UltimateAbilityID).IsEqual("mozart_symphony_of_sorrow");

        var required = new List<string>();
        foreach (LegacyKitGate gate in level.KitGates) {
            AssertThat(string.IsNullOrWhiteSpace(gate.RequiredAbilityID))
                .OverrideFailureMessage($"Gate '{gate.GateID}' requires no ability at all.").IsFalse();
            AssertThat(required.Contains(gate.RequiredAbilityID))
                .OverrideFailureMessage($"'{gate.RequiredAbilityID}' is required by two gates.").IsFalse();
            required.Add(gate.RequiredAbilityID);
        }
        AssertThat(required.Count).IsEqual(4);

        // Every required ability is one Mozart actually has, and it is his
        // CANONICAL kit - never a purchased Resonance node (V01c).
        var character = AuthoredResources.Load<FTT.Characters.CharacterData>(
            "res://resources/Characters/mozart_data.tres");
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
                .OverrideFailureMessage($"Gate ability '{abilityID}' is not in Mozart's authored kit.")
                .IsTrue();
        }

        // The Ultimate is the one gate the Nexus source answers (F04).
        AssertThat(level.UltimateGate.Mode).IsEqual(LegacyGateMode.Nexus);
        AssertString(level.NexusSource.RequiredUltimateAbilityID).IsEqual(level.UltimateAbilityID);
        AssertThat(level.NexusSource.Gate).IsEqual(level.UltimateGate);
        // The movement gate is traversal. Both of Mozart's specials are
        // projectiles, so both are struck mechanisms - and a Strike gate accepts
        // only its own AttackID, so the two can never open each other.
        AssertThat(level.MovementGate.Mode).IsEqual(LegacyGateMode.Traversal);
        AssertThat(level.SpecialOneGate.Mode).IsEqual(LegacyGateMode.Strike);
        AssertThat(level.SpecialTwoGate.Mode).IsEqual(LegacyGateMode.Strike);
    }

    [TestCase]
    public void TheOrchestraPitIsWiderThanOrdinaryTraversalAndTheLandingBoxIsOverIt() {
        // The movement gate has to be a real gate: a pit Mozart cannot simply walk
        // or double-jump across. Sonata Drift lays a platform under his feet rather
        // than moving him, so the landing box sits OVER the pit at staff altitude -
        // he is standing in it at the cast, inside the 30-frame traversal grace.
        float gap = Level04AMozartController.PitGapEndX - Level04AMozartController.PitGapStartX;
        AssertThat(gap >= 400f)
            .OverrideFailureMessage($"The orchestra pit is only {gap} px — ordinary traversal would clear it.")
            .IsTrue();

        using var fixture = new Level04AFixture();
        Vector2 gate = fixture.Level.MovementGate.Position;
        AssertThat(gate.X > Level04AMozartController.PitGapStartX
                && gate.X < Level04AMozartController.PitGapEndX)
            .OverrideFailureMessage(
                $"The movement gate's landing box (x={gate.X}) must sit over the pit, not on either lip.")
            .IsTrue();
        AssertThat(gate.Y < 850f)
            .OverrideFailureMessage("The landing box must sit above the boards, at staff-platform altitude.")
            .IsTrue();
    }

    // === Route order ===

    [TestCase]
    public void TheFontSitsAfterTheEraserAndBeforeAGatedPreBoss() {
        using var fixture = new Level04AFixture();
        Level04AMozartController level = fixture.Level;

        // Exactly one Font: a healing object on the late approach, not a third
        // checkpoint (LEGACY_CHECKPOINTS.md).
        AssertThat(level.Font.Position.X > level.EraserDebut.Position.X)
            .OverrideFailureMessage("The Restoration Font must sit after the Eraser debut.").IsTrue();
        AssertThat(level.Font.Position.X < level.PreBossCheckpoint.Position.X)
            .OverrideFailureMessage("The Restoration Font must sit before PreBoss.").IsTrue();

        // On a fresh entry every mandatory objective is outstanding - the four kit
        // gates plus the Eraser encounter - and PreBoss physically cannot be struck.
        AssertThat(level.MandatoryObjectivesRemaining).IsEqual(5);
        AssertThat(level.CanActivatePreBoss).IsFalse();
        AssertThat(level.PreBossStrikeArmed)
            .OverrideFailureMessage("PreBoss must stay inert until the whole approach is complete.")
            .IsFalse();
    }

    // === Locked encounter economy ===

    [TestCase]
    public void TheAuthoredSpawnTableIsAllStandardsWithNoAuthoredElites() {
        int standards = 0;
        int elites = 0;
        foreach ((string enemyID, Vector2 _) in Level04AMozartController.ApproachSpawns) {
            EnemyData data = EnemyFactory.LoadData(enemyID);
            AssertObject(data)
                .OverrideFailureMessage($"Authored spawn '{enemyID}' has no enemy resource.")
                .IsNotNull();
            if (data.Tier == EnemyTier.Standard) standards++;
            else elites++;
        }
        AssertThat(standards).IsEqual(Level04AMozartController.AuthoredStandardCount);
        AssertThat(elites)
            .OverrideFailureMessage(
                "4A authors no elites of its own: its only elite is the scripted Eraser debut.")
            .IsEqual(Level04AMozartController.AuthoredEliteCount);
        AssertThat(Level04AMozartController.AuthoredEliteCount).IsEqual(0);
    }

    [TestCase]
    public void TheLockedLedgerRowIsFifteenRequiredTwentyFiveBossAndTenOptional() {
        AssertThat(LegacyLevelControllerBase.RequiredEncounterDust).IsEqual(LockedRequiredEncounterDust);
        AssertThat(LegacyLevelControllerBase.BossDust).IsEqual(LockedBossDust);
        AssertThat(LegacyLevelControllerBase.OptionalDust).IsEqual(LockedOptionalDust);

        var boss = AuthoredResources.Load<BossData>(Level04AMozartController.BossResource);
        AssertObject(boss).IsNotNull();
        AssertThat(boss.ChronalDustDrop)
            .OverrideFailureMessage("The 4A boss pays 25 dust as a Large physical pickup.")
            .IsEqual(LockedBossDust);

        // 4A's optional allocation is its secret, not an Extractor row.
        using var fixture = new Level04AFixture();
        AssertThat(fixture.Level.Extractors.Count).IsEqual(0);
    }

    [TestCase]
    public void NoWaveExceedsThePoolConfigConcurrencyBudget() {
        var config = AuthoredResources.Load<ScenePoolConfig>(PoolConfigPath);
        AssertObject(config).IsNotNull();
        AssertString(config.ConfigID).IsEqual("level_04a_mozart_pools");

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

        AssertThat(Level04AMozartController.AuthoredStandardCount <= standardWarmUp)
            .OverrideFailureMessage(
                $"The whole approach ({Level04AMozartController.AuthoredStandardCount}) exceeds the " +
                $"warm standard_enemy budget ({standardWarmUp}).")
            .IsTrue();
    }

    // === Boss ===

    [TestCase]
    public void TheLegacyBossIsATwoPhaseDuplicateWhoseRangeBandsFitTheArena() {
        var data = AuthoredResources.Load<BossData>(Level04AMozartController.BossResource);
        AssertObject(data).IsNotNull();
        AssertString(data.BossID).IsEqual("mozart_legacy_impresario");
        AssertString(data.DisplayNameKey).IsEqual("boss_mozart_legacy_impresario_name");

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
        // Reused era resource: the Globe Theatre's Tragedy King (Level 10).
        AssertThat(data.MaxHP).IsEqual(700);
        var shared = AuthoredResources.Load<BossData>("res://resources/Bosses/tragedy_king.tres");
        AssertObject(shared).IsNotNull();
        AssertString(shared.BossID).IsEqual("tragedy_king");
        AssertThat(shared.MaxHP)
            .OverrideFailureMessage("The shared era boss must be untouched by the Legacy duplicate.")
            .IsEqual(800);
        AssertThat(shared.ChronalDustDrop)
            .OverrideFailureMessage("The Legacy duplicate must not have rewritten the shared boss's dust.")
            .IsEqual(50);

        using var fixture = new Level04AFixture();
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertObject(encounter.Data).IsNotNull();
        AssertString(encounter.Data.BossID).IsEqual("mozart_legacy_impresario");

        // 60 px per world unit (BossController.PixelsPerUnit): the ranged band has
        // to fit inside the boss room or the distance filter can never satisfy it.
        float arenaWidth = Level04AMozartController.BossArenaWidth;
        AssertThat(data.RangedRangeThreshold * 60f < arenaWidth)
            .OverrideFailureMessage(
                $"Boss ranged band ({data.RangedRangeThreshold * 60f} px) does not fit the {arenaWidth} px arena.")
            .IsTrue();
    }

    // === Audio ===

    [TestCase]
    public void TheVariantUsesTheSharedLegacyAudioSetRatherThanTheSlotDerivation() {
        // All nine variants share one set (P01 Option A: reuse suitable era themes).
        AssertThat(ResourceLoader.Exists(AudioSetPaths.Legacy)).IsTrue();
        var set = AuthoredResources.Load<StageAudioSet>(AudioSetPaths.Legacy);
        AssertObject(set).IsNotNull();
        AssertString(set.StageID).IsEqual(AudioSetPaths.LegacyStageID);

        // The 4A level ID must NOT resolve through the shared slot derivation —
        // that would silently point at level_04_audio.tres (Paris).
        AssertString(AudioSetPaths.ForStoryLevel(Level04AMozartController.ID)).IsEqual("");
    }

    // === Route benchmarks ===

    [TestCase]
    public void TheVariantCarriesItsOwnParAndEntryRecoveryBudget() {
        using var fixture = new Level04AFixture();
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
    private sealed class Level04AFixture : IDisposable {
        public readonly Level04AMozartController Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;

        public Level04AFixture() {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;
            SessionData session = GameManager.Instance.CurrentSession;
            _originalSlot = session.ActiveSaveSlot;
            _originalCharacter = session.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = Hero;
            GameManager.Instance.CurrentSession = session;

            var packed = ResourceLoader.Load<PackedScene>(ScenePath);
            Level = packed.Instantiate<Level04AMozartController>();
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
