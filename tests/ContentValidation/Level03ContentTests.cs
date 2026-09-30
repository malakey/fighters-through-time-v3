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
/// Package 5 Wave A - Level 3, Chicago 1893 (World's Columbian Exposition).
///
/// Pins the level's contract with the campaign: the scene resolves at the exact
/// <see cref="StoryManager"/> path, the LevelID and the three checkpoint IDs match
/// the plan convention, every authored dialogue line resolves through localization,
/// the authored encounter table matches the locked economy row (8 standards /
/// 0 elites / 1 boss / 3 extractors), and - the era identity - the beam-routing
/// puzzle actually solves and opens the Court of Honor door.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level03ContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_03_Chicago.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_03_dialogue.tres";
    private const int ScratchSlot = 2;

    // The locked Package 5 §4 economy row for level 3.
    private const int LockedStandardCount = 8;
    private const int LockedEliteCount = 0;
    private const int LockedExtractorCount = 3;

    // === Scene ===

    [TestCase]
    public void TheSceneResolvesAtTheCampaignRoutePathAndInstantiatesAsTheController() {
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.Chicago)).IsEqual(ScenePath);
        AssertThat(ResourceLoader.Exists(ScenePath)).IsTrue();

        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        Node instance = packed.Instantiate();
        try {
            AssertThat(instance is Level03Controller)
                .OverrideFailureMessage("Level_03_Chicago.tscn must instantiate as Level03Controller.")
                .IsTrue();
        } finally {
            instance.Free();
        }
    }

    [TestCase]
    public void TheLevelReadiesInTheTreeWithItsManagerCameraAndAuthoredPuzzleIntact() {
        using var fixture = new Level03Fixture(null);
        Level03Controller level = fixture.Level;

        AssertString(level.LevelID).IsEqual(Level03Controller.ChicagoLevelID);
        AssertThat(level.Level == CampaignLevel.Chicago).IsTrue();
        AssertString(level.LevelTitleKey).IsEqual("chicago_level_title");

        var manager = level.GetNodeOrNull<LevelManager>("LevelManager");
        AssertObject(manager).IsNotNull();
        AssertString(manager.LevelID).IsEqual(Level03Controller.ChicagoLevelID);

        AssertObject(level.Player).IsNotNull();
        AssertObject(level.Camera).IsNotNull();
        AssertObject(level.BeamPuzzle).IsNotNull();

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
    public void TheThreeCheckpointsUseThePlanIDsAndRegisterRespawnPositions() {
        AssertThat(Level03Controller.CheckpointIDs.Length).IsEqual(3);
        for (int index = 0; index < 3; index++) {
            AssertString(Level03Controller.CheckpointIDs[index])
                .IsEqual($"{Level03Controller.ChicagoLevelID}_checkpoint_{index}");
        }

        using var fixture = new Level03Fixture(null);
        foreach (string id in Level03Controller.CheckpointIDs) {
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
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_03");

        var byID = new Dictionary<string, DialogueSequenceData>(StringComparer.Ordinal);
        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertObject(sequence).IsNotNull();
            byID[sequence.DialogueID] = sequence;
        }
        // Package 13 W4 (S23): the powerhouse absence joins the three base beats, and
        // the two N03 Tesla variants are the absence-slot recognition and the exit.
        // Package 12's {MissingSoFar} sequence variants are gone (the token resolves it).
        AssertThat(set.Sequences.Length).IsEqual(6);
        foreach (string beat in new[] {
            "level_03.entrance", "level_03.absence", "level_03.boss_intro", "level_03.exit",
            "level_03.absence@tesla", "level_03.exit@tesla" }) {
            AssertThat(byID.ContainsKey(beat))
                .OverrideFailureMessage($"Dialogue sequence '{beat}' is missing from the level 3 set.")
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
    }

    [TestCase]
    public void EveryVisibleLevelStringHasALocalizationEntry() {
        foreach (string key in new[] {
            "chicago_level_title",
            "chicago_room_midway", "chicago_room_electricity",
            "chicago_room_routing", "chicago_room_boss",
            "chicago_door_sealed",
            "chicago_objective_fairgrounds", "chicago_objective_reroute",
            "chicago_objective_reach_boss", "chicago_objective_defeat_boss",
            "chicago_objective_complete",
            "campaign_level_chicago", "interaction_rotate_coil"
        }) {
            AssertLocalized(key);
        }
    }

    // === Locked encounter economy ===

    [TestCase]
    public void TheAuthoredSpawnTableIsExactlyEightStandardsAndNoElites() {
        int standards = 0;
        int elites = 0;
        foreach (Level03Controller.EnemySpawn spawn in Level03Controller.AllAuthoredSpawns) {
            EnemyData data = EnemyFactory.LoadData(spawn.EnemyID);
            AssertObject(data)
                .OverrideFailureMessage($"Authored spawn '{spawn.EnemyID}' has no enemy resource.")
                .IsNotNull();
            if (data.Tier == FTT.Enemies.EnemyTier.Standard) standards++;
            else elites++;
        }
        AssertThat(standards)
            .OverrideFailureMessage("Level 3's locked economy row is 8 standards.").IsEqual(LockedStandardCount);
        AssertThat(elites)
            .OverrideFailureMessage("Level 3's locked economy row places NO elites.").IsEqual(LockedEliteCount);
    }

    [TestCase]
    public void TheRosterIsTheChicagoDroneMixedWithTheCultistStandard() {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Level03Controller.EnemySpawn spawn in Level03Controller.AllAuthoredSpawns) {
            counts[spawn.EnemyID] = counts.GetValueOrDefault(spawn.EnemyID) + 1;
        }
        AssertThat(counts.Count).IsEqual(2);
        AssertThat(counts.GetValueOrDefault("voltaic_shock_drone")).IsEqual(4);
        AssertThat(counts.GetValueOrDefault("chrono_slasher")).IsEqual(4);

        // The Chicago standard is the flying one; that is what the level's
        // elevated spawn placements assume.
        AssertThat(EnemyFactory.LoadData("voltaic_shock_drone").Behavior == DefaultBehavior.Flying).IsTrue();
    }

    [TestCase]
    public void NoWaveExceedsThePoolConfigConcurrencyBudget() {
        // level_03_pool_config.tres warms 10 standard_enemy slots; the largest
        // authored wave is what can be live at once.
        var config = AuthoredResources.Load<ScenePoolConfig>(
            "res://resources/Pools/level_pool_configs/level_03_pool_config.tres");
        AssertObject(config).IsNotNull();

        int standardWarmUp = 0;
        foreach (PoolDefinition definition in config.PoolDefinitions) {
            if (definition?.PoolID == "standard_enemy") standardWarmUp = definition.WarmUpCount;
        }
        AssertThat(standardWarmUp > 0).IsTrue();

        int largestWave = Math.Max(Level03Controller.MidwayWave.Length,
            Math.Max(Level03Controller.ElectricityWave.Length, Level03Controller.RoutingHallWave.Length));
        AssertThat(largestWave <= standardWarmUp)
            .OverrideFailureMessage($"Largest wave ({largestWave}) exceeds the warm standard_enemy budget ({standardWarmUp}).")
            .IsTrue();
    }

    [TestCase]
    public void ThreeChronalExtractorsArePlacedAndInstantiateFromTheAuthoredTemplate() {
        AssertThat(Level03Controller.ExtractorPlacements.Length).IsEqual(LockedExtractorCount);
        using var fixture = new Level03Fixture(null);
        AssertThat(fixture.Level.Extractors.Count).IsEqual(LockedExtractorCount);
        foreach (ChronalExtractor extractor in fixture.Level.Extractors) {
            AssertObject(extractor).IsNotNull();
            // Dust value stays resource-owned (DUST_ECONOMY.md: 15 each); the
            // level must never override it from placement code.
            AssertThat(extractor.DustReward).IsEqual(3);
        }
    }

    // === Boss ===

    [TestCase]
    public void TheChronalInventorEncounterWiresUpAndItsRangeBandsFitTheArena() {
        var data = AuthoredResources.Load<BossData>(Level03Controller.BossResourcePath);
        AssertObject(data).IsNotNull();
        AssertString(data.BossID).IsEqual("chronal_inventor");
        AssertThat(data.AttackPattern == BossAttackPattern.DistanceBased).IsTrue();
        AssertThat(data.PhaseThresholds.Length).IsEqual(1); // two phases

        using var fixture = new Level03Fixture(null);
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertObject(encounter.Data).IsNotNull();
        AssertString(encounter.Data.BossID).IsEqual("chronal_inventor");

        // 60 px per world unit (BossController.PixelsPerUnit): the ranged band has
        // to fit inside the boss room or the distance filter can never satisfy it.
        float arenaWidth = Level03Controller.LevelWidth - Level03Controller.BossStartX;
        AssertThat(data.RangedRangeThreshold * 60f < arenaWidth)
            .OverrideFailureMessage($"Boss ranged band ({data.RangedRangeThreshold * 60f} px) does not fit the {arenaWidth} px arena.")
            .IsTrue();
    }

    // === Era identity: the beam-routing puzzle ===

    [TestCase]
    public void TheBeamPuzzleStartsMisroutedIntoTheCrowdWithTheDoorSealed() {
        using var fixture = new Level03Fixture(null);
        Level03Controller level = fixture.Level;

        AssertString(level.BeamPuzzle.PuzzleID).IsEqual(Level03Controller.BeamPuzzleID);
        AssertThat(level.BeamPuzzle.PersistCompletionToSave)
            .OverrideFailureMessage("Completion must persist so a checkpoint resume keeps the door open.")
            .IsTrue();
        AssertThat(level.BeamPuzzle.IsCompleted).IsFalse();
        AssertThat(level.CourtDoorOpen).IsFalse();

        // The mis-aimed first coil dumps the beam into the spectator stands: that
        // live hazard is the read-plan-execute tell.
        var tapA = level.GetNodeOrNull<PowerRoutingNode>("BeamRoutingPuzzle/CrowdTapA");
        var crowdArcA = level.GetNodeOrNull<StoryCyclicHazard>("CrowdArcA");
        AssertObject(tapA).IsNotNull();
        AssertObject(crowdArcA).IsNotNull();
        AssertThat(tapA.IsPowered).IsTrue();
        AssertThat(crowdArcA.Enabled).IsTrue();

        var receiver = level.GetNodeOrNull<BeamReceiver>("BeamRoutingPuzzle/Receiver");
        AssertObject(receiver).IsNotNull();
        AssertThat(receiver.IsPowered).IsFalse();
    }

    [TestCase]
    public void AllThreeCoilsMustBeRotatedBeforeTheBeamReachesTheReceiver() {
        using var fixture = new Level03Fixture(null);
        Level03Controller level = fixture.Level;
        var coilA = level.GetNodeOrNull<ConductiveCoil>("BeamRoutingPuzzle/CoilA");
        var coilB = level.GetNodeOrNull<ConductiveCoil>("BeamRoutingPuzzle/CoilB");
        var coilC = level.GetNodeOrNull<ConductiveCoil>("BeamRoutingPuzzle/CoilC");
        var receiver = level.GetNodeOrNull<BeamReceiver>("BeamRoutingPuzzle/Receiver");
        AssertObject(coilA).IsNotNull();
        AssertObject(coilB).IsNotNull();
        AssertObject(coilC).IsNotNull();

        // Authored start orientations: none of the three is already correct.
        AssertThat(coilA.QuarterTurns).IsNotEqual(3);
        AssertThat(coilB.QuarterTurns).IsNotEqual(0);
        AssertThat(coilC.QuarterTurns).IsNotEqual(1);

        // Each coil alone is not enough - the chain only lights end to end.
        coilA.SetQuarterTurns(3);
        AssertThat(coilB.IsPowered).IsTrue();
        AssertThat(receiver.IsPowered).IsFalse();
        coilB.SetQuarterTurns(0);
        AssertThat(coilC.IsPowered).IsTrue();
        AssertThat(receiver.IsPowered).IsFalse();
        coilC.SetQuarterTurns(1);
        AssertThat(receiver.IsPowered).IsTrue();
    }

    [TestCase]
    public void SolvingTheRoutingPuzzleCompletesItOpensTheDoorAndClearsTheCrowdStands() {
        using var fixture = new Level03Fixture(null);
        Level03Controller level = fixture.Level;
        var crowdArcA = level.GetNodeOrNull<StoryCyclicHazard>("CrowdArcA");
        var crowdArcB = level.GetNodeOrNull<StoryCyclicHazard>("CrowdArcB");

        AssertThat(level.CourtDoorOpen).IsFalse();
        AssertThat(level.SolveBeamPuzzleForTest()).IsTrue();

        AssertThat(level.BeamPuzzle.IsCompleted)
            .OverrideFailureMessage("Routing the beam into the receiver must complete the puzzle.").IsTrue();
        AssertThat(level.BeamPuzzle.IsConditionSatisfied("beam_routed")).IsTrue();
        AssertThat(level.CourtDoorOpen)
            .OverrideFailureMessage("Puzzle completion must open the Court of Honor door.").IsTrue();
        // Current no longer feeds either spectator tap.
        AssertThat(crowdArcA.Enabled).IsFalse();
        AssertThat(crowdArcB.Enabled).IsFalse();
    }

    [TestCase]
    public void ResumingAtThePreBossCheckpointNeverLeavesThePlayerWalledIn() {
        // The pre-boss checkpoint stands past the routing door. A save taken there
        // must not reload behind a sealed gate, whether or not the puzzle flag
        // survived - this is the soft-lock the plan calls out.
        using var fixture = new Level03Fixture(new StorySaveData {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Chicago),
            LastCheckpointID = Level03Controller.CheckpointPreBoss,
            CurrentHP = 70,
            CurrentUltimateMeter = 25f
        });
        Level03Controller level = fixture.Level;

        AssertThat(level.ResumedMidLevel).IsTrue();
        AssertString(level.ResumedCheckpointID).IsEqual(Level03Controller.CheckpointPreBoss);
        AssertThat(level.CourtDoorOpen)
            .OverrideFailureMessage("A run resumed at the pre-boss checkpoint must find the door already open.")
            .IsTrue();
        AssertThat(level.Player.GlobalPosition.X > Level03Controller.BossStartX).IsTrue();
    }

    /// <summary>
    /// Package 12 W9 (GAP-07): "P2: deploys two siphon coils at the arena corners
    /// that shield him until destroyed." The coils arrive exactly at Phase 2, one
    /// per corner of the Court of Honor, and the Inventor refuses damage until
    /// both are gone.
    /// </summary>
    [TestCase]
    public void ThePhaseTwoInventorDeploysTwoCornerCoilsThatShieldHimUntilBothAreDestroyed() {
        using var fixture = new Level03Fixture(null);
        Level03Controller level = fixture.Level;
        BossEncounterController encounter = level.InventorEncounter;
        AssertObject(encounter).IsNotNull();
        BossController inventor = encounter.Boss;
        AssertThat(level.InventorCoils.Count).IsEqual(0);

        inventor.TakeDamage(inventor.ScaledMaxHP / 2 + 1);
        AssertThat(inventor.CurrentPhase).IsEqual(1);
        AssertThat(level.InventorCoils.Count).IsEqual(2);
        AssertThat(inventor.LiveGuardianCount).IsEqual(2);

        // One coil per corner, both inside the Court of Honor.
        float west = Mathf.Min(level.InventorCoils[0].Position.X, level.InventorCoils[1].Position.X);
        float east = Mathf.Max(level.InventorCoils[0].Position.X, level.InventorCoils[1].Position.X);
        AssertThat(west > Level03Controller.BossStartX && west < inventor.GlobalPosition.X).IsTrue();
        AssertThat(east > inventor.GlobalPosition.X && east < Level03Controller.LevelWidth).IsTrue();
        foreach (ArenaGuardian coil in level.InventorCoils) {
            AssertThat(coil.MaxHP).IsEqual(encounter.Data.ArenaGuardianHP);
        }

        // Let the transition window close; the coils are now the only shield.
        for (int frame = 0; frame < 300 && inventor.CurrentState == BossState.PhaseTransitioning; frame++) {
            inventor._PhysicsProcess(1f / 60f);
        }
        int hp = inventor.CurrentHP;
        inventor.TakeDamage(30);
        AssertThat(inventor.CurrentHP).IsEqual(hp);

        level.InventorCoils[0].ApplyDamage(encounter.Data.ArenaGuardianHP);
        inventor.TakeDamage(30);
        AssertThat(inventor.CurrentHP).IsEqual(hp);

        level.InventorCoils[1].ApplyDamage(encounter.Data.ArenaGuardianHP);
        inventor.TakeDamage(30);
        AssertThat(inventor.CurrentHP < hp).IsTrue();
    }

    // === Helpers ===

    private static void AssertLocalized(string key) {
        AssertThat(TranslationServer.Translate(key).ToString() != key)
            .OverrideFailureMessage($"Key '{key}' is missing from localization/en.csv.")
            .IsTrue();
    }

    /// <summary>
    /// Instantiates the authored scene in the runner tree against an optional
    /// scratch save, restoring every shared singleton it touches on dispose.
    /// </summary>
    private sealed class Level03Fixture : IDisposable {
        public readonly Level03Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPuzzleCompleted;

        public Level03Fixture(StorySaveData save) {
            SessionData session = GameManager.Instance.CurrentSession;
            _originalSlot = session.ActiveSaveSlot;
            _originalCharacter = session.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];
            _originalPuzzleCompleted =
                SaveManager.Instance.IsPuzzleCompleted(Level03Controller.BeamPuzzleID);

            session.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            session.SelectedCharacterID = "einstein";
            GameManager.Instance.CurrentSession = session;
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;
            // The puzzle reads persisted completion in its own _Ready; a stale flag
            // from an earlier case would open the door before the beam is routed.
            SaveManager.Instance.SetPuzzleCompleted(Level03Controller.BeamPuzzleID, false);

            var packed = ResourceLoader.Load<PackedScene>(ScenePath);
            Level = packed.Instantiate<Level03Controller>();
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(Level);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            SaveManager.Instance.SetPuzzleCompleted(
                Level03Controller.BeamPuzzleID, _originalPuzzleCompleted);
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            SessionData session = GameManager.Instance.CurrentSession;
            session.ActiveSaveSlot = _originalSlot;
            session.SelectedCharacterID = _originalCharacter;
            GameManager.Instance.CurrentSession = session;
        }
    }
}
