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
/// Package 11 B1 - Level 4A, Leonardo's Legacy Level (Florence, 1503 - the workshop). Copied structurally
/// from A12's <c>Level04AEinsteinContentTests</c> exemplar, which is itself the
/// <c>LevelNNContentTests</c> pattern.
///
/// <para>Pins this variant's contract with the campaign: the scene resolves at the
/// per-hero route path, the level ID and its two checkpoint IDs and roles match the
/// F12 convention, the dialogue set and every line key resolve through localization,
/// the authored encounter table matches the locked 4A economy row (15
/// required-encounter + 25 boss + 10 optional dust), the whole kit is required once
/// each, the single Font sits after the Eraser and before PreBoss, and the boss arena
/// is wide enough for the authored boss's ranged band.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level04ALeonardoContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_04A_leonardo.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_04a_leonardo_dialogue.tres";
    private const string PoolConfigPath =
        "res://resources/Pools/level_pool_configs/level_04a_leonardo_pool_config.tres";
    private const int ScratchSlot = 2;

    // The locked 4A ledger row (docs/design-contracts/DUST_ECONOMY.md).
    private const int LockedRequiredEncounterDust = 15;
    private const int LockedBossDust = 25;
    private const int LockedOptionalDust = 10;

    // === Scene and identity ===

    [TestCase]
    public void TheSceneResolvesAtTheHeroRoutePathAndInstantiatesAsTheController() {
        using var session = new ScratchSession("leonardo");
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus)).IsEqual(ScenePath);
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus, "leonardo"))
            .IsEqual(ScenePath);
        AssertThat(ResourceLoader.Exists(ScenePath)).IsTrue();

        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        Node instance = packed.Instantiate();
        try {
            AssertThat(instance is Level04ALeonardoController)
                .OverrideFailureMessage("Level_04A_leonardo.tscn must instantiate as Level04ALeonardoController.")
                .IsTrue();
        } finally {
            instance.Free();
        }
    }

    [TestCase]
    public void TheLevelReadiesInTheTreeWithItsManagerCameraAndLegacyFurnitureIntact() {
        using var fixture = new LegacyFixture();
        Level04ALeonardoController level = fixture.Level;

        AssertString(level.LevelID).IsEqual(Level04ALeonardoController.ID);
        AssertString(level.HeroCharacterID).IsEqual("leonardo");
        AssertThat(level.Level == CampaignLevel.LegacyNexus).IsTrue();
        AssertString(level.LevelTitleKey).IsEqual("legacy_leonardo_level_title");

        var manager = level.GetNodeOrNull<LevelManager>("LevelManager");
        AssertObject(manager).IsNotNull();
        AssertString(manager.LevelID).IsEqual(Level04ALeonardoController.ID);

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
        AssertThat(Level04ALeonardoController.CheckpointIDs.Length)
            .OverrideFailureMessage("F12 Option A: exactly two 4A checkpoints.").IsEqual(2);
        for (int index = 0; index < 2; index++) {
            AssertString(Level04ALeonardoController.CheckpointIDs[index])
                .IsEqual($"{Level04ALeonardoController.ID}_checkpoint_{index}");
        }

        using var fixture = new LegacyFixture();
        Level04ALeonardoController level = fixture.Level;
        AssertString(level.EntryCheckpointID).IsEqual(Level04ALeonardoController.Checkpoint0);
        AssertString(level.PreBossCheckpointID).IsEqual(Level04ALeonardoController.Checkpoint1);

        // The role is authored beside the stable ID; nothing may infer PreBoss from
        // the "_1" suffix, and both roles are live on every difficulty.
        AssertThat(level.CheckpointRoles.Count).IsEqual(2);
        AssertString(level.CheckpointRoles[level.EntryCheckpointID])
            .IsEqual(LegacyCheckpointRoles.Entry);
        AssertString(level.CheckpointRoles[level.PreBossCheckpointID])
            .IsEqual(LegacyCheckpointRoles.PreBoss);
        AssertThat(level.EntryCheckpoint.Role).IsEqual(CheckpointRole.Entry);
        AssertThat(level.PreBossCheckpoint.Role).IsEqual(CheckpointRole.PreBoss);
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            AssertThat(level.IsCheckpointEnabled(level.PreBossCheckpointID, difficulty))
                .OverrideFailureMessage($"4A PreBoss must stay enabled on {difficulty}.").IsTrue();
        }

        foreach (string id in Level04ALeonardoController.CheckpointIDs) {
            AssertThat(level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered with the LevelManager.")
                .IsTrue();
        }
    }

    // === Dialogue and localization ===

    [TestCase]
    public void TheDialogueSetResolvesWithTheThreeBeatsAndEveryLineKeyLocalized() {
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        AssertObject(set).IsNotNull();
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_04a_leonardo");

        var byID = new Dictionary<string, DialogueSequenceData>(StringComparer.Ordinal);
        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertObject(sequence).IsNotNull();
            byID[sequence.DialogueID] = sequence;
        }
        foreach (string beat in new[] {
            "level_04a_leonardo.entrance", "level_04a_leonardo.boss_intro", "level_04a_leonardo.exit"
        }) {
            AssertThat(byID.ContainsKey(beat))
                .OverrideFailureMessage($"Dialogue sequence '{beat}' is missing from the 4A Leonardo set.")
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

        using var fixture = new LegacyFixture();
        AssertString(fixture.Level.DialogueSetPath).IsEqual(DialoguePath);
        AssertString(fixture.Level.EntranceDialogueID).IsEqual("level_04a_leonardo.entrance");
        AssertString(fixture.Level.BossIntroDialogueID).IsEqual("level_04a_leonardo.boss_intro");
        AssertString(fixture.Level.ExitDialogueID).IsEqual("level_04a_leonardo.exit");
    }

    [TestCase]
    public void EveryVisibleLevelStringHasALocalizationEntry() {
        foreach (string key in new[] {
            "legacy_leonardo_level_title",
            "legacy_leonardo_room_arno", "legacy_leonardo_room_workshop", "legacy_leonardo_room_hall",
            "legacy_leonardo_objective_gates", "legacy_leonardo_objective_boss",
            "legacy_leonardo_objective_complete",
            "legacy_gate_strike_prompt", "legacy_gate_zone_prompt",
            "legacy_gate_traversal_prompt", "legacy_gate_nexus_prompt",
            "nexus_source_prompt", "nexus_source_armed",
            "eraser_debut_bark",
            "boss_leonardo_legacy_overseer_name", "speaker_leonardo_legacy_overseer",
            "campaign_level_legacy_nexus"
        }) {
            AssertLocalized(key);
        }
    }

    // === Kit-keyed design: the whole kit, once each ===

    [TestCase]
    public void TheLevelRequiresLeonardosWholeKitExactlyOnceEach() {
        using var fixture = new LegacyFixture();
        Level04ALeonardoController level = fixture.Level;

        // Leonardo's four canonical ability IDs, one gate each.
        AssertString(level.MovementGateAbilityID).IsEqual("leonardo_ornithopter_flight");
        AssertString(level.SpecialOneGateAbilityID).IsEqual("leonardo_golden_ratio");
        AssertString(level.SpecialTwoGateAbilityID).IsEqual("leonardo_clockwork_turret");
        AssertString(level.UltimateAbilityID).IsEqual("leonardo_vitruvian_matrix");

        var required = new List<string>();
        foreach (LegacyKitGate gate in level.KitGates) {
            AssertThat(string.IsNullOrWhiteSpace(gate.RequiredAbilityID))
                .OverrideFailureMessage($"Gate '{gate.GateID}' requires no ability at all.").IsFalse();
            AssertThat(required.Contains(gate.RequiredAbilityID))
                .OverrideFailureMessage($"'{gate.RequiredAbilityID}' is required by two gates.").IsFalse();
            required.Add(gate.RequiredAbilityID);
        }
        AssertThat(required.Count).IsEqual(4);

        // Every required ability is one Leonardo actually has, and it is the
        // CANONICAL kit - never a purchased Resonance node (V01c).
        var character = AuthoredResources.Load<FTT.Characters.CharacterData>(
            "res://resources/Characters/leonardo_data.tres");
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
                .OverrideFailureMessage($"Gate ability '{abilityID}' is not in Leonardo's authored kit.")
                .IsTrue();
        }

        // The Ultimate is the one gate the Nexus source answers (F04).
        AssertThat(level.UltimateGate.Mode).IsEqual(LegacyGateMode.Nexus);
        AssertString(level.NexusSource.RequiredUltimateAbilityID).IsEqual(level.UltimateAbilityID);
        AssertThat(level.NexusSource.Gate).IsEqual(level.UltimateGate);
        // The movement gate is traversal; the two specials carry their own shapes.
        AssertThat(level.MovementGate.Mode).IsEqual(LegacyGateMode.Traversal);
        AssertThat(level.SpecialOneGate.Mode).IsEqual(LegacyGateMode.Zone);
        AssertThat(level.SpecialTwoGate.Mode).IsEqual(LegacyGateMode.Strike);

        // Turret bolts deliver through an enemy-hurtbox shape query, so the sighting armature carries a resonant effigy the bolts can reach. It forwards to the SAME gate and accepts nothing the gate
        // itself would not (V01c is unchanged).
        AssertObject(level.ArmatureEffigy)
            .OverrideFailureMessage("The Special 2 gate needs its resonant effigy or it is unopenable.")
            .IsNotNull();
        AssertThat(level.ArmatureEffigy.Gate).IsEqual(level.SpecialTwoGate);
        AssertThat(level.ArmatureEffigy.IsInert).IsFalse();

        // A gate accepts its one authored ability and nothing else.
        AssertThat(level.SpecialOneGate.TryResolve("leonardo_clockwork_turret"))
            .OverrideFailureMessage("A kit gate accepted another ability's ID.").IsFalse();
        AssertThat(level.SpecialOneGate.IsResolved).IsFalse();
    }

    [TestCase]
    public void TheMovementGapIsWiderThanOrdinaryTraversalAndTheLandingIsPastIt() {
        // The movement gate has to be a real gate: the Arno span is a gap Leonardo
        // cannot simply walk or double-jump across, with its landing on the far side.
        float gap = Level04ALeonardoController.SpanGapEndX - Level04ALeonardoController.SpanGapStartX;
        AssertThat(gap >= 400f)
            .OverrideFailureMessage($"The Ornithopter Flight gap is only {gap} px - ordinary traversal would clear it.")
            .IsTrue();

        using var fixture = new LegacyFixture();
        AssertThat(fixture.Level.MovementGate.Position.X > Level04ALeonardoController.SpanGapEndX)
            .OverrideFailureMessage("The movement gate's landing box must sit past the gap.").IsTrue();
    }

    // === Route order ===

    [TestCase]
    public void TheRoutePutsTheFontAfterTheEraserAndBeforePreBossAndGatesTheStrike() {
        using var fixture = new LegacyFixture();
        Level04ALeonardoController level = fixture.Level;

        // Exactly one Font, on the late approach (LEGACY_CHECKPOINTS.md).
        AssertObject(level.Font).IsNotNull();
        AssertThat(level.Font.Position.X > level.EraserDebut.Position.X)
            .OverrideFailureMessage("The Font must sit after the Eraser debut.").IsTrue();
        AssertThat(level.Font.Position.X < level.PreBossCheckpoint.Position.X)
            .OverrideFailureMessage("The Font must sit before PreBoss - it is not a third checkpoint.")
            .IsTrue();

        // The four kit gates and the Eraser encounter are all outstanding on a fresh
        // entry, and PreBoss physically cannot be struck until they are done.
        AssertThat(level.MandatoryObjectivesRemaining).IsEqual(5);
        AssertThat(level.CanActivatePreBoss).IsFalse();
        AssertThat(level.PreBossStrikeArmed)
            .OverrideFailureMessage("PreBoss was strikeable with the approach outstanding.").IsFalse();
    }

    // === Locked encounter economy ===

    [TestCase]
    public void TheAuthoredSpawnTableIsAllStandardsWithNoAuthoredElites() {
        int standards = 0;
        int elites = 0;
        foreach ((string enemyID, Vector2 _) in Level04ALeonardoController.ApproachSpawns) {
            EnemyData data = EnemyFactory.LoadData(enemyID);
            AssertObject(data)
                .OverrideFailureMessage($"Authored spawn '{enemyID}' has no enemy resource.")
                .IsNotNull();
            if (data.Tier == EnemyTier.Standard) standards++;
            else elites++;
        }
        AssertThat(standards).IsEqual(Level04ALeonardoController.AuthoredStandardCount);
        AssertThat(elites)
            .OverrideFailureMessage(
                "4A authors no elites of its own: its only elite is the scripted Eraser debut.")
            .IsEqual(Level04ALeonardoController.AuthoredEliteCount);
        AssertThat(Level04ALeonardoController.AuthoredEliteCount).IsEqual(0);
    }

    [TestCase]
    public void TheLockedLedgerRowIsFifteenRequiredTwentyFiveBossAndTenOptional() {
        // DUST_ECONOMY.md row 4A: identical for every character variant, regardless
        // of layout or enemy count.
        AssertThat(LegacyLevelControllerBase.RequiredEncounterDust).IsEqual(LockedRequiredEncounterDust);
        AssertThat(LegacyLevelControllerBase.BossDust).IsEqual(LockedBossDust);
        AssertThat(LegacyLevelControllerBase.OptionalDust).IsEqual(LockedOptionalDust);

        var boss = AuthoredResources.Load<BossData>(Level04ALeonardoController.BossResource);
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
        AssertString(config.ConfigID).IsEqual("level_04a_leonardo_pools");

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

        AssertThat(Level04ALeonardoController.AuthoredStandardCount <= standardWarmUp)
            .OverrideFailureMessage(
                $"The whole approach ({Level04ALeonardoController.AuthoredStandardCount}) exceeds the " +
                $"warm standard_enemy budget ({standardWarmUp}).")
            .IsTrue();
    }

    // === Boss ===

    [TestCase]
    public void TheLegacyBossIsATwoPhaseDuplicateWhoseRangeBandsFitTheArena() {
        var data = AuthoredResources.Load<BossData>(Level04ALeonardoController.BossResource);
        AssertObject(data).IsNotNull();
        AssertString(data.BossID).IsEqual("leonardo_legacy_overseer");
        AssertString(data.DisplayNameKey).IsEqual("boss_leonardo_legacy_overseer_name");

        // Two phases, standard boss rules (plan 2.4).
        AssertThat(data.PhaseThresholds.Length)
            .OverrideFailureMessage("A 4A boss is two phases.").IsEqual(1);
        AssertThat(data.PhaseSpeedMultipliers.Length).IsEqual(2);
        foreach (int minPhase in data.AbilityMinPhase) {
            AssertThat(minPhase <= 1)
                .OverrideFailureMessage($"AbilityMinPhase {minPhase} names a phase a two-phase boss lacks.")
                .IsTrue();
        }

        // The variant HP row, and proof it did not come from editing the shared era
        // boss. The shared resource's own MaxHP is deliberately NOT pinned here:
        // A7b is applying the V7.6 2.E HP rows in this same wave, and pinning a
        // literal would make this suite fail on someone else's approved change.
        AssertThat(data.MaxHP).IsEqual(700);
        var shared = AuthoredResources.Load<BossData>(Level04ALeonardoController.EraBossResource);
        AssertObject(shared).IsNotNull();
        AssertString(shared.BossID).IsEqual("borgia_inquisitor");
        AssertThat(shared.MaxHP != data.MaxHP)
            .OverrideFailureMessage("The shared era boss must be untouched by the Legacy duplicate.")
            .IsTrue();
        AssertThat(shared.ChronalDustDrop != data.ChronalDustDrop)
            .OverrideFailureMessage("The Legacy duplicate must carry its own 25-dust row.").IsTrue();

        using var fixture = new LegacyFixture();
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertObject(encounter.Data).IsNotNull();
        AssertString(encounter.Data.BossID).IsEqual("leonardo_legacy_overseer");

        // 60 px per world unit (BossController.PixelsPerUnit): the ranged band has
        // to fit inside the boss room or the distance filter can never satisfy it.
        float arenaWidth = Level04ALeonardoController.BossArenaWidth;
        AssertThat(data.RangedRangeThreshold * 60f < arenaWidth)
            .OverrideFailureMessage(
                $"Boss ranged band ({data.RangedRangeThreshold * 60f} px) does not fit the {arenaWidth} px arena.")
            .IsTrue();
    }

    // === Audio ===

    [TestCase]
    public void TheVariantUsesTheSharedLegacyAudioSet() {
        // All nine variants share one set (P01 Option A: reuse suitable era themes).
        AssertString(AudioSetPaths.Legacy).IsEqual("res://resources/Audio/level_04a_audio.tres");
        AssertThat(ResourceLoader.Exists(AudioSetPaths.Legacy)).IsTrue();

        // The 4A level ID must NOT resolve through the shared slot derivation -
        // that would silently point at level_04_audio.tres (Paris).
        AssertString(AudioSetPaths.ForStoryLevel(Level04ALeonardoController.ID)).IsEqual("");
    }

    // === Route benchmarks ===

    [TestCase]
    public void TheVariantCarriesItsOwnParAndEntryRecoveryBudget() {
        using var fixture = new LegacyFixture();
        // V01a: each distinct 4A route has its own benchmark; nothing pools them.
        AssertThat(fixture.Level.ParSeconds).IsEqual(320);
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
        public readonly Level04ALeonardoController Level;
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
            session.SelectedCharacterID = "leonardo";
            GameManager.Instance.CurrentSession = session;

            var packed = ResourceLoader.Load<PackedScene>(ScenePath);
            Level = packed.Instantiate<Level04ALeonardoController>();
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
