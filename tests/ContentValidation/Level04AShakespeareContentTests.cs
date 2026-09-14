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
/// Package 11 B2 - Level 4A, Shakespeare's Legacy Level (London, 1599 - the Globe's opening season). Follows the
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
public class Level04AShakespeareContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_04A_shakespeare.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_04a_shakespeare_dialogue.tres";
    private const string PoolConfigPath =
        "res://resources/Pools/level_pool_configs/level_04a_shakespeare_pool_config.tres";
    private const string CharacterPath = "res://resources/Characters/shakespeare_data.tres";
    private const int ScratchSlot = 2;

    // The locked 4A ledger row (docs/design-contracts/DUST_ECONOMY.md).
    private const int LockedRequiredEncounterDust = 15;
    private const int LockedBossDust = 25;
    private const int LockedOptionalDust = 10;

    // === Scene and identity ===

    [TestCase]
    public void TheSceneResolvesAtTheHeroRoutePathAndInstantiatesAsTheController() {
        using var session = new ScratchSession("shakespeare");
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus)).IsEqual(ScenePath);
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus, "shakespeare"))
            .IsEqual(ScenePath);
        AssertThat(ResourceLoader.Exists(ScenePath)).IsTrue();

        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        Node instance = packed.Instantiate();
        try {
            AssertThat(instance is Level04AShakespeareController)
                .OverrideFailureMessage("Level_04A_shakespeare.tscn must instantiate as Level04AShakespeareController.")
                .IsTrue();
        } finally {
            instance.Free();
        }
    }

    [TestCase]
    public void TheLevelReadiesInTheTreeWithItsManagerCameraAndLegacyFurnitureIntact() {
        using var fixture = new LegacyFixture();
        Level04AShakespeareController level = fixture.Level;

        AssertString(level.LevelID).IsEqual(Level04AShakespeareController.ID);
        AssertString(level.HeroCharacterID).IsEqual("shakespeare");
        AssertThat(level.Level == CampaignLevel.LegacyNexus).IsTrue();
        AssertString(level.LevelTitleKey).IsEqual("legacy_shakespeare_level_title");

        var manager = level.GetNodeOrNull<LevelManager>("LevelManager");
        AssertObject(manager).IsNotNull();
        AssertString(manager.LevelID).IsEqual(Level04AShakespeareController.ID);

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
        AssertThat(Level04AShakespeareController.CheckpointIDs.Length)
            .OverrideFailureMessage("F12 Option A: exactly two 4A checkpoints.").IsEqual(2);
        for (int index = 0; index < 2; index++) {
            AssertString(Level04AShakespeareController.CheckpointIDs[index])
                .IsEqual($"{Level04AShakespeareController.ID}_checkpoint_{index}");
        }

        using var fixture = new LegacyFixture();
        AssertString(fixture.Level.EntryCheckpointID).IsEqual(Level04AShakespeareController.Checkpoint0);
        AssertString(fixture.Level.PreBossCheckpointID).IsEqual(Level04AShakespeareController.Checkpoint1);

        // The roles are authored, never inferred from a numeric suffix: Hard's
        // "middle inactive" rule must not disable a PreBoss whose ID ends _1.
        AssertString(fixture.Level.CheckpointRoles[Level04AShakespeareController.Checkpoint0])
            .IsEqual(LegacyCheckpointRoles.Entry);
        AssertString(fixture.Level.CheckpointRoles[Level04AShakespeareController.Checkpoint1])
            .IsEqual(LegacyCheckpointRoles.PreBoss);
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            foreach (string id in Level04AShakespeareController.CheckpointIDs) {
                AssertThat(fixture.Level.IsCheckpointEnabled(id, difficulty))
                    .OverrideFailureMessage($"Checkpoint '{id}' is disabled on {difficulty}.").IsTrue();
            }
        }

        foreach (string id in Level04AShakespeareController.CheckpointIDs) {
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
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_04a_shakespeare");

        var byID = new Dictionary<string, DialogueSequenceData>(StringComparer.Ordinal);
        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertObject(sequence).IsNotNull();
            byID[sequence.DialogueID] = sequence;
        }
        foreach (string beat in new[] {
            "level_04a_shakespeare.entrance", "level_04a_shakespeare.boss_intro", "level_04a_shakespeare.exit"
        }) {
            AssertThat(byID.ContainsKey(beat))
                .OverrideFailureMessage($"Dialogue sequence '{beat}' is missing from the 4A Shakespeare set.")
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
        AssertString(fixture.Level.EntranceDialogueID).IsEqual("level_04a_shakespeare.entrance");
        AssertString(fixture.Level.BossIntroDialogueID).IsEqual("level_04a_shakespeare.boss_intro");
        AssertString(fixture.Level.ExitDialogueID).IsEqual("level_04a_shakespeare.exit");
    }

    [TestCase]
    public void EveryVisibleLevelStringHasALocalizationEntry() {
        foreach (string key in new[] {
            "legacy_shakespeare_level_title",
            "legacy_shakespeare_room_bankside",
            "legacy_shakespeare_room_yard",
            "legacy_shakespeare_room_understage",
            "legacy_shakespeare_objective_gates", "legacy_shakespeare_objective_boss",
            "legacy_shakespeare_objective_complete",
            "legacy_gate_strike_prompt", "legacy_gate_zone_prompt",
            "legacy_gate_traversal_prompt", "legacy_gate_nexus_prompt",
            "nexus_source_prompt", "nexus_source_armed",
            "eraser_debut_bark",
            "boss_shakespeare_legacy_king_name", "speaker_shakespeare_legacy_king",
            "campaign_level_legacy_nexus"
        }) {
            AssertLocalized(key);
        }
    }

    // === Kit-keyed design: the whole CANONICAL kit, once each ===

    [TestCase]
    public void TheLevelRequiresTheHerosWholeCanonicalKitExactlyOnceEach() {
        using var fixture = new LegacyFixture();
        Level04AShakespeareController level = fixture.Level;

        AssertString(level.MovementGateAbilityID).IsEqual("shakespeare_prosperos_flight");
        AssertString(level.SpecialOneGateAbilityID).IsEqual("shakespeare_yoricks_lament");
        AssertString(level.SpecialTwoGateAbilityID).IsEqual("shakespeare_the_tempest");
        AssertString(level.UltimateAbilityID).IsEqual("shakespeare_all_the_worlds_a_stage");

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
        AssertThat(level.SpecialTwoGate.Mode).IsEqual(LegacyGateMode.Zone);
    }

    [TestCase]
    public void TheCastRecognisedGateHasAWatcherBoundToItsOwnAbility() {
        // The Tempest lifts the caster and pushes bodies: it spawns no zone and raises no
        // hitbox, so nothing in the combat pipeline can observe it. Without a recogniser that gate is unopenable and the whole
        // route strands at PreBoss.
        using var fixture = new LegacyFixture();
        Level04AShakespeareController level = fixture.Level;

        AssertObject(level.TempestWatcher)
            .OverrideFailureMessage("The cast-recognised gate has no LegacyCastGateWatcher.")
            .IsNotNull();
        AssertThat(level.TempestWatcher.Gate).IsEqual(level.SpecialTwoGate);
        AssertString(level.TempestWatcher.RequiredAbilityID).IsEqual(level.SpecialTwoGateAbilityID);
        AssertString(level.SpecialTwoGate.RequiredAbilityID).IsEqual(level.TempestWatcher.RequiredAbilityID);

        // V01c: the watcher accepts its one ability and nothing else, and only
        // within range of the mechanism.
        AssertThat(level.TempestWatcher.TryRecognize("combo_1", level.TempestWatcher.GlobalPosition))
            .OverrideFailureMessage("A basic attack opened a kit gate.").IsFalse();
        AssertThat(level.TempestWatcher.TryRecognize(level.TempestWatcher.RequiredAbilityID,
            level.TempestWatcher.GlobalPosition + new Vector2(level.TempestWatcher.ResolveRadius + 200f, 0f)))
            .OverrideFailureMessage("The gate resolved from outside its own area.").IsFalse();
        AssertThat(level.SpecialTwoGate.IsResolved).IsFalse();

        AssertThat(level.TempestWatcher.TryRecognize(level.TempestWatcher.RequiredAbilityID,
            level.TempestWatcher.GlobalPosition)).IsTrue();
        AssertThat(level.SpecialTwoGate.IsResolved).IsTrue();
    }

    [TestCase]
    public void TheTraversalGapIsWiderThanOrdinaryTraversalAndTheLandingIsPastIt() {
        // The movement gate has to be a real gate: a collapsed span the hero cannot simply
        // walk or double-jump across, with its landing shelf on the far side.
        float gap = Level04AShakespeareController.SpanGapEndX - Level04AShakespeareController.SpanGapStartX;
        AssertThat(gap >= 400f)
            .OverrideFailureMessage($"The collapsed span is only {gap} px - ordinary traversal would clear it.")
            .IsTrue();

        using var fixture = new LegacyFixture();
        AssertThat(fixture.Level.MovementGate.Position.X > Level04AShakespeareController.SpanGapEndX)
            .OverrideFailureMessage("The movement gate's landing box must sit past the gap.").IsTrue();
    }

    // === Locked encounter economy ===

    [TestCase]
    public void TheAuthoredSpawnTableIsAllStandardsWithNoAuthoredElites() {
        int standards = 0;
        int elites = 0;
        foreach ((string enemyID, Vector2 _) in Level04AShakespeareController.ApproachSpawns) {
            EnemyData data = EnemyFactory.LoadData(enemyID);
            AssertObject(data)
                .OverrideFailureMessage($"Authored spawn '{enemyID}' has no enemy resource.")
                .IsNotNull();
            if (data.Tier == EnemyTier.Standard) standards++;
            else elites++;
        }
        AssertThat(standards).IsEqual(Level04AShakespeareController.AuthoredStandardCount);
        AssertThat(elites)
            .OverrideFailureMessage(
                "4A authors no elites of its own: its only elite is the scripted Eraser debut.")
            .IsEqual(Level04AShakespeareController.AuthoredEliteCount);
        AssertThat(Level04AShakespeareController.AuthoredEliteCount).IsEqual(0);
    }

    [TestCase]
    public void TheLockedLedgerRowIsFifteenRequiredTwentyFiveBossAndTenOptional() {
        // DUST_ECONOMY.md row 4A: identical for every character variant, regardless
        // of layout or enemy count.
        AssertThat(LegacyLevelControllerBase.RequiredEncounterDust).IsEqual(LockedRequiredEncounterDust);
        AssertThat(LegacyLevelControllerBase.BossDust).IsEqual(LockedBossDust);
        AssertThat(LegacyLevelControllerBase.OptionalDust).IsEqual(LockedOptionalDust);

        var boss = AuthoredResources.Load<BossData>(Level04AShakespeareController.BossResource);
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
        AssertString(config.ConfigID).IsEqual("level_04a_shakespeare_pools");

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

        AssertThat(Level04AShakespeareController.AuthoredStandardCount <= standardWarmUp)
            .OverrideFailureMessage(
                $"The whole approach ({Level04AShakespeareController.AuthoredStandardCount}) exceeds the " +
                $"warm standard_enemy budget ({standardWarmUp}).")
            .IsTrue();
    }

    // === Boss ===

    [TestCase]
    public void TheLegacyBossIsATwoPhaseDuplicateWhoseRangeBandsFitTheArena() {
        var data = AuthoredResources.Load<BossData>(Level04AShakespeareController.BossResource);
        AssertObject(data).IsNotNull();
        AssertString(data.BossID).IsEqual("shakespeare_legacy_king");
        AssertString(data.DisplayNameKey).IsEqual("boss_shakespeare_legacy_king_name");

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
        AssertThat(Level04AShakespeareController.BossResource.Contains("/Bosses/legacy/"))
            .OverrideFailureMessage("A Legacy boss lives under resources/Bosses/legacy/.").IsTrue();
        var era = AuthoredResources.Load<BossData>(Level04AShakespeareController.EraBossResource);
        AssertObject(era)
            .OverrideFailureMessage("The reused era boss resource is missing.").IsNotNull();
        AssertString(era.BossID).IsEqual("tragedy_king");
        AssertThat(era.BossID == data.BossID)
            .OverrideFailureMessage("The Legacy boss must carry its own ID, not the era boss's.")
            .IsFalse();

        using var fixture = new LegacyFixture();
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertObject(encounter.Data).IsNotNull();
        AssertString(encounter.Data.BossID).IsEqual("shakespeare_legacy_king");

        // 60 px per world unit (BossController.PixelsPerUnit): the ranged band has to
        // fit inside the boss room or the distance filter can never satisfy it.
        float arenaWidth = Level04AShakespeareController.BossArenaWidth;
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
        AssertString(AudioSetPaths.ForStoryLevel(Level04AShakespeareController.ID)).IsEqual("");
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
        public readonly Level04AShakespeareController Level;
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
            session.SelectedCharacterID = "shakespeare";
            GameManager.Instance.CurrentSession = session;

            var packed = ResourceLoader.Load<PackedScene>(ScenePath);
            Level = packed.Instantiate<Level04AShakespeareController>();
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
