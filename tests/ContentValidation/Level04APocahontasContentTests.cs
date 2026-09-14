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
/// Package 11 B3 — Level 4A, Pocahontas's Legacy Level (Tsenacommacah, 1607). Follows the
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
public class Level04APocahontasContentTests {

    private const string Hero = "pocahontas";
    private const string ScenePath = "res://scenes/campaign/Level_04A_pocahontas.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_04a_pocahontas_dialogue.tres";
    private const string PoolConfigPath =
        "res://resources/Pools/level_pool_configs/level_04a_pocahontas_pool_config.tres";
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
            AssertThat(instance is Level04APocahontasController)
                .OverrideFailureMessage("Level_04A_pocahontas.tscn must instantiate as Level04APocahontasController.")
                .IsTrue();
        } finally {
            instance.Free();
        }
    }

    [TestCase]
    public void TheLevelReadiesInTheTreeWithItsManagerCameraAndLegacyFurnitureIntact() {
        using var fixture = new Level04AFixture();
        Level04APocahontasController level = fixture.Level;

        AssertString(level.LevelID).IsEqual(Level04APocahontasController.ID);
        AssertString(level.HeroCharacterID).IsEqual(Hero);
        AssertThat(level.Level == CampaignLevel.LegacyNexus).IsTrue();
        AssertString(level.LevelTitleKey).IsEqual("legacy_pocahontas_level_title");

        var manager = level.GetNodeOrNull<LevelManager>("LevelManager");
        AssertObject(manager).IsNotNull();
        AssertString(manager.LevelID).IsEqual(Level04APocahontasController.ID);

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
        AssertThat(Level04APocahontasController.CheckpointIDs.Length)
            .OverrideFailureMessage("F12 Option A: exactly two 4A checkpoints.").IsEqual(2);
        for (int index = 0; index < 2; index++) {
            AssertString(Level04APocahontasController.CheckpointIDs[index])
                .IsEqual($"{Level04APocahontasController.ID}_checkpoint_{index}");
        }

        using var fixture = new Level04AFixture();
        AssertString(fixture.Level.EntryCheckpointID).IsEqual(Level04APocahontasController.Checkpoint0);
        AssertString(fixture.Level.PreBossCheckpointID).IsEqual(Level04APocahontasController.Checkpoint1);

        // Roles are authored, never inferred from the numeric suffix.
        AssertString(fixture.Level.CheckpointRoles[Level04APocahontasController.Checkpoint0])
            .IsEqual(LegacyCheckpointRoles.Entry);
        AssertString(fixture.Level.CheckpointRoles[Level04APocahontasController.Checkpoint1])
            .IsEqual(LegacyCheckpointRoles.PreBoss);
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            foreach (string id in Level04APocahontasController.CheckpointIDs) {
                AssertThat(fixture.Level.IsCheckpointEnabled(id, difficulty))
                    .OverrideFailureMessage($"Checkpoint '{id}' must stay enabled on {difficulty}.")
                    .IsTrue();
            }
        }

        foreach (string id in Level04APocahontasController.CheckpointIDs) {
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
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_04a_pocahontas");

        var byID = new Dictionary<string, DialogueSequenceData>(StringComparer.Ordinal);
        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertObject(sequence).IsNotNull();
            byID[sequence.DialogueID] = sequence;
        }
        foreach (string beat in new[] {
            "level_04a_pocahontas.entrance", "level_04a_pocahontas.boss_intro", "level_04a_pocahontas.exit"
        }) {
            AssertThat(byID.ContainsKey(beat))
                .OverrideFailureMessage($"Dialogue sequence '{beat}' is missing from the 4A Pocahontas set.")
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
        AssertString(fixture.Level.EntranceDialogueID).IsEqual("level_04a_pocahontas.entrance");
        AssertString(fixture.Level.BossIntroDialogueID).IsEqual("level_04a_pocahontas.boss_intro");
        AssertString(fixture.Level.ExitDialogueID).IsEqual("level_04a_pocahontas.exit");
    }

    [TestCase]
    public void EveryVisibleLevelStringHasALocalizationEntry() {
        foreach (string key in new[] {
            "legacy_pocahontas_level_title",
            "legacy_pocahontas_room_riverbank", "legacy_pocahontas_room_weir",
            "legacy_pocahontas_room_hollow",
            "legacy_pocahontas_objective_gates", "legacy_pocahontas_objective_boss",
            "legacy_pocahontas_objective_complete",
            "legacy_gate_strike_prompt", "legacy_gate_zone_prompt",
            "legacy_gate_traversal_prompt", "legacy_gate_nexus_prompt",
            "nexus_source_prompt", "nexus_source_armed",
            "eraser_debut_bark",
            "boss_pocahontas_legacy_tidereaver_name", "speaker_pocahontas_legacy_tidereaver",
            "campaign_level_legacy_nexus"
        }) {
            AssertLocalized(key);
        }
    }

    // === Kit-keyed design: the whole kit, once each ===

    [TestCase]
    public void TheLevelRequiresPocahontasWholeKitExactlyOnceEach() {
        using var fixture = new Level04AFixture();
        Level04APocahontasController level = fixture.Level;

        AssertString(level.MovementGateAbilityID).IsEqual("pocahontas_breeze_glide");
        AssertString(level.SpecialOneGateAbilityID).IsEqual("pocahontas_spirit_strike");
        AssertString(level.SpecialTwoGateAbilityID).IsEqual("pocahontas_vine_snare");
        AssertString(level.UltimateAbilityID).IsEqual("pocahontas_tidewater_tempest");

        var required = new List<string>();
        foreach (LegacyKitGate gate in level.KitGates) {
            AssertThat(string.IsNullOrWhiteSpace(gate.RequiredAbilityID))
                .OverrideFailureMessage($"Gate '{gate.GateID}' requires no ability at all.").IsFalse();
            AssertThat(required.Contains(gate.RequiredAbilityID))
                .OverrideFailureMessage($"'{gate.RequiredAbilityID}' is required by two gates.").IsFalse();
            required.Add(gate.RequiredAbilityID);
        }
        AssertThat(required.Count).IsEqual(4);

        // Every required ability is one Pocahontas actually has, and it is his
        // CANONICAL kit - never a purchased Resonance node (V01c).
        var character = AuthoredResources.Load<FTT.Characters.CharacterData>(
            "res://resources/Characters/pocahontas_data.tres");
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
                .OverrideFailureMessage($"Gate ability '{abilityID}' is not in Pocahontas's authored kit.")
                .IsTrue();
        }

        // The Ultimate is the one gate the Nexus source answers (F04).
        AssertThat(level.UltimateGate.Mode).IsEqual(LegacyGateMode.Nexus);
        AssertString(level.NexusSource.RequiredUltimateAbilityID).IsEqual(level.UltimateAbilityID);
        AssertThat(level.NexusSource.Gate).IsEqual(level.UltimateGate);
        // The movement gate is traversal and Spirit Strike is a struck mechanism.
        // Vine Snare deploys a persistent construct rather than a story_zone, so
        // the shared Zone poll cannot see it: the variant-local resolver drives the
        // gate's own TryResolve with the same authored ability ID. The fifth gate
        // mode this really wants is recorded in the plan's section 9.
        AssertThat(level.MovementGate.Mode).IsEqual(LegacyGateMode.Traversal);
        AssertThat(level.SpecialOneGate.Mode).IsEqual(LegacyGateMode.Strike);
        AssertThat(level.SpecialTwoGate.Mode).IsEqual(LegacyGateMode.Zone);
        AssertObject(level.SnareResolver)
            .OverrideFailureMessage("The Vine Snare gate has no construct resolver; it could never open.")
            .IsNotNull();
        AssertThat(level.SnareResolver.Gate).IsEqual(level.SpecialTwoGate);
        AssertString(level.SnareResolver.RequiredAbilityID).IsEqual(level.SpecialTwoGateAbilityID);
    }

    [TestCase]
    public void TheRiverGorgeIsWiderThanOrdinaryTraversalAndTheLandingBoxIsInsideTheGlideWindow() {
        // The movement gate has to be a real gate: a gorge Pocahontas cannot simply
        // walk or double-jump across. Breeze Glide carries her for seconds, but the
        // gate's traversal grace is only 30 frames past the cast, so the landing box
        // sits just out over the gorge rather than on the far bank.
        float gap = Level04APocahontasController.GorgeGapEndX - Level04APocahontasController.GorgeGapStartX;
        AssertThat(gap >= 400f)
            .OverrideFailureMessage($"The river gorge is only {gap} px - ordinary traversal would clear it.")
            .IsTrue();

        using var fixture = new Level04AFixture();
        Vector2 gate = fixture.Level.MovementGate.Position;
        AssertThat(gate.X > Level04APocahontasController.GorgeGapStartX
                && gate.X < Level04APocahontasController.GorgeGapEndX)
            .OverrideFailureMessage(
                $"The movement gate's landing box (x={gate.X}) must sit over the gorge, not on either bank.")
            .IsTrue();
        // The dash carries roughly 120 px through the cast frames; the box has to be
        // inside that reach, not at the far lip the glide takes seconds to make.
        AssertThat(gate.X - Level04APocahontasController.GorgeGapStartX < 300f)
            .OverrideFailureMessage(
                "The landing box sits beyond the dash and grace reach; the gate would never latch.")
            .IsTrue();
    }

    // === Route order ===

    [TestCase]
    public void TheFontSitsAfterTheEraserAndBeforeAGatedPreBoss() {
        using var fixture = new Level04AFixture();
        Level04APocahontasController level = fixture.Level;

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
        foreach ((string enemyID, Vector2 _) in Level04APocahontasController.ApproachSpawns) {
            EnemyData data = EnemyFactory.LoadData(enemyID);
            AssertObject(data)
                .OverrideFailureMessage($"Authored spawn '{enemyID}' has no enemy resource.")
                .IsNotNull();
            if (data.Tier == EnemyTier.Standard) standards++;
            else elites++;
        }
        AssertThat(standards).IsEqual(Level04APocahontasController.AuthoredStandardCount);
        AssertThat(elites)
            .OverrideFailureMessage(
                "4A authors no elites of its own: its only elite is the scripted Eraser debut.")
            .IsEqual(Level04APocahontasController.AuthoredEliteCount);
        AssertThat(Level04APocahontasController.AuthoredEliteCount).IsEqual(0);
    }

    [TestCase]
    public void TheLockedLedgerRowIsFifteenRequiredTwentyFiveBossAndTenOptional() {
        AssertThat(LegacyLevelControllerBase.RequiredEncounterDust).IsEqual(LockedRequiredEncounterDust);
        AssertThat(LegacyLevelControllerBase.BossDust).IsEqual(LockedBossDust);
        AssertThat(LegacyLevelControllerBase.OptionalDust).IsEqual(LockedOptionalDust);

        var boss = AuthoredResources.Load<BossData>(Level04APocahontasController.BossResource);
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
        AssertString(config.ConfigID).IsEqual("level_04a_pocahontas_pools");

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

        AssertThat(Level04APocahontasController.AuthoredStandardCount <= standardWarmUp)
            .OverrideFailureMessage(
                $"The whole approach ({Level04APocahontasController.AuthoredStandardCount}) exceeds the " +
                $"warm standard_enemy budget ({standardWarmUp}).")
            .IsTrue();
    }

    // === Boss ===

    [TestCase]
    public void TheLegacyBossIsATwoPhaseDuplicateWhoseRangeBandsFitTheArena() {
        var data = AuthoredResources.Load<BossData>(Level04APocahontasController.BossResource);
        AssertObject(data).IsNotNull();
        AssertString(data.BossID).IsEqual("pocahontas_legacy_tidereaver");
        AssertString(data.DisplayNameKey).IsEqual("boss_pocahontas_legacy_tidereaver_name");

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
        // Reused era resource: the Titanic's Tidal Eraser (Level 5).
        AssertThat(data.MaxHP).IsEqual(700);
        var shared = AuthoredResources.Load<BossData>("res://resources/Bosses/tidal_eraser.tres");
        AssertObject(shared).IsNotNull();
        AssertString(shared.BossID).IsEqual("tidal_eraser");
        AssertThat(shared.MaxHP)
            .OverrideFailureMessage("The shared era boss must be untouched by the Legacy duplicate.")
            .IsEqual(640);
        // P11 A10 (F05) relocked every boss in the game at 25; the shared era boss
        // moved 50 -> 25 with the rest of resources/Bosses/. MaxHP above is what
        // proves the Legacy duplicate did not rewrite this resource.
        AssertThat(shared.ChronalDustDrop)
            .OverrideFailureMessage("The shared era boss must carry the locked 25-dust row.")
            .IsEqual(LegacyLevelControllerBase.BossDust);

        using var fixture = new Level04AFixture();
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertObject(encounter.Data).IsNotNull();
        AssertString(encounter.Data.BossID).IsEqual("pocahontas_legacy_tidereaver");

        // 60 px per world unit (BossController.PixelsPerUnit): the ranged band has
        // to fit inside the boss room or the distance filter can never satisfy it.
        float arenaWidth = Level04APocahontasController.BossArenaWidth;
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
        AssertString(AudioSetPaths.ForStoryLevel(Level04APocahontasController.ID)).IsEqual("");
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
        public readonly Level04APocahontasController Level;
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
            Level = packed.Instantiate<Level04APocahontasController>();
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
