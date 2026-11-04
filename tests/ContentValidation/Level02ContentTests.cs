using System;
using System.Collections.Generic;
using System.IO;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 5 Wave A - Level 2, Orléans 1429. Pins the contracts the campaign flow
/// depends on: the scene lives at the exact path <c>StoryManager</c> routes to, the
/// controller reports the manifest identity and the three locked checkpoint IDs, the
/// authored encounter table matches the locked dust-economy row (8 standards /
/// 0 elites / 3 extractors), the era identity is mechanically real (two destructible
/// generators actually holding barriers up), and every authored string resolves.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level02ContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_02_Orleans.tscn";
    private const int ScratchSlot = 2;

    // === Scene ===

    [TestCase]
    public void TheSceneLoadsAtTheRoutedPathInstantiatesAndFreesCleanly() {
        // The path is a hard contract: StoryManager.LevelScenePaths[2] names it and
        // refuses to change scenes when it cannot resolve.
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.Orleans)).IsEqual(ScenePath);

        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).IsNotNull();

        Node instance = scene.Instantiate();
        AssertObject(instance).IsNotNull();
        AssertThat(instance is Level02Controller).IsTrue();
        instance.Free();
    }

    [TestCase]
    public void TheControllerReportsTheManifestIdentityAndDialogueWiring() {
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;

        AssertString(level.LevelID).IsEqual("level_02_orleans");
        AssertThat(level.Level).IsEqual(CampaignLevel.Orleans);
        AssertString(level.LevelTitleKey).IsEqual("orleans_level_title");
        AssertString(level.DialogueSetPath).IsEqual("res://resources/Dialogue/level_02_dialogue.tres");
        AssertString(level.EntranceDialogueID).IsEqual("level_02.entrance");
        AssertString(level.BossIntroDialogueID).IsEqual("level_02.boss_intro");
        AssertString(level.ExitDialogueID).IsEqual("level_02.exit");

        // The LevelManager node name is what CheckpointTrigger resolves by literal path.
        var manager = level.GetNodeOrNull<LevelManager>("LevelManager");
        AssertObject(manager).IsNotNull();
        AssertString(manager.LevelID).IsEqual("level_02_orleans");
    }

    [TestCase]
    public void ExactlyThreeCheckpointsRegisterUnderTheLockedIDs() {
        using var fixture = new Level02Fixture(null);
        foreach (string id in new[] {
            Level02Controller.Checkpoint0, Level02Controller.Checkpoint1, Level02Controller.Checkpoint2 }) {
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered with the LevelManager.")
                .IsTrue();
        }

        int checkpoints = 0;
        foreach (Node child in Children(fixture.Level)) {
            if (child is CheckpointTrigger) checkpoints++;
        }
        AssertThat(checkpoints).OverrideFailureMessage(
            "Level 2 must ship exactly three checkpoints (entry anchor, midpoint, pre-boss).")
            .IsEqual(3);
    }

    [TestCase]
    public void RoomConfinementCoversTheWholeLevelAndTheBossArenaFitsAViewport() {
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;

        AssertThat(level.RoomTriggers.Count).OverrideFailureMessage(
            "Level 2 is authored as four camera-confined rooms and the Rampart run.").IsEqual(5);
        foreach (RoomTransitionTrigger trigger in level.RoomTriggers) {
            // A1 back-fills CameraPath after the player spawns; without it room
            // confinement silently does nothing.
            AssertThat(trigger.CameraPath != null && !trigger.CameraPath.IsEmpty)
                .OverrideFailureMessage($"Room '{trigger.RoomID}' has no camera path.").IsTrue();
            AssertThat(trigger.CameraBounds.Size.X).OverrideFailureMessage(
                $"Room '{trigger.RoomID}' is narrower than the 1920 px reference viewport.")
                .IsGreaterEqual(1920f);
        }

        // Plan §5.1 sizes campaign levels at Florence scale.
        float width = level.LevelBounds.Size.X;
        AssertThat(width).IsEqual(Level02Controller.LevelWidth);
        AssertThat(width).IsGreaterEqual(9000f);
        AssertThat(width).IsLessEqual(11000f);
    }

    // === Era identity: kinetic shield generators ===

    [TestCase]
    public void TwoDestructibleGeneratorsHoldTheGateBarriersUpUntilTheyAreBroken() {
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;

        ShieldGeneratorTower[] towers = { level.ShieldTowerA, level.ShieldTowerB };
        float[] gates = { Level02Controller.GateAX, Level02Controller.GateBX };
        float[] anchors = { Level02Controller.TowerAX, Level02Controller.TowerBX };

        AssertThat(level.TowersDestroyed).IsEqual(0);
        for (int index = 0; index < towers.Length; index++) {
            ShieldGeneratorTower tower = towers[index];
            AssertObject(tower).OverrideFailureMessage(
                $"Shield generator {index} is missing from the scene.").IsNotNull();
            // The template's barrier hangs at a fixed local offset, so the generator
            // has to stand exactly that far west of the gate it seals.
            AssertThat(Mathf.IsEqualApprox(tower.Position.X, anchors[index])).OverrideFailureMessage(
                $"Generator {index} is at x={tower.Position.X}, but its barrier only covers " +
                $"gate x={gates[index]} when it stands at x={anchors[index]}.").IsTrue();
            AssertThat(tower.ActiveBarrierCount).OverrideFailureMessage(
                $"Generator {index} must hold a live barrier while it stands.").IsEqual(1);

            foreach (ForcefieldBarrier barrier in tower.Barriers()) {
                AssertThat(Mathf.IsEqualApprox(barrier.GlobalPosition.X, gates[index])).IsTrue();
                AssertThat(barrier.CollisionLayer).IsEqual(CollisionLayers.Environment);
            }
        }

        // Breaking a generator is how the player advances: the barrier must drop.
        level.ShieldTowerA.TakeEnvironmentDamage(level.ShieldTowerA.MaxHP);
        AssertThat(level.ShieldTowerA.IsDestroyed).IsTrue();
        AssertThat(level.ShieldTowerA.ActiveBarrierCount).IsEqual(0);
        AssertThat(level.TowersDestroyed).IsEqual(1);

        level.ShieldTowerB.TakeEnvironmentDamage(level.ShieldTowerB.MaxHP);
        AssertThat(level.ShieldTowerB.ActiveBarrierCount).IsEqual(0);
        AssertThat(level.TowersDestroyed).IsEqual(Level02Controller.TowerCount);
    }

    [TestCase]
    public void TheMortarLanesAreTelegraphedCyclicHazards() {
        using var fixture = new Level02Fixture(null);
        var hazards = new List<StoryCyclicHazard>();
        foreach (Node child in Children(fixture.Level)) {
            if (child is StoryCyclicHazard hazard) hazards.Add(hazard);
        }

        AssertThat(hazards.Count).OverrideFailureMessage(
            "The siege needs an authored mortar lane, not a single emplacement.").IsGreaterEqual(4);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (StoryCyclicHazard hazard in hazards) {
            AssertThat(ids.Add(hazard.HazardID)).OverrideFailureMessage(
                $"Duplicate hazard id '{hazard.HazardID}'.").IsTrue();
            AssertThat(hazard.HazardID.StartsWith("level_02.")).IsTrue();
            // Warning frames are the whole point: a mortar with no telegraph is
            // unreactable, and the design calls for dashing past cannon fire.
            AssertThat(hazard.WarningDuration).IsGreater(0f);
            AssertThat(hazard.ActiveDuration).IsGreater(0f);
            AssertThat(hazard.CooldownDuration).IsGreater(hazard.ActiveDuration);
            AssertThat(hazard.Phase).IsEqual(HazardPhase.Cooldown);
        }
    }

    // === Locked encounter economy ===

    [TestCase]
    public void TheAuthoredEncounterTableMatchesTheLockedEconomyRow() {
        // docs/DUST_ECONOMY.md / DustEconomyTests row 2: 8 standards, 0 elites,
        // 1 boss, 3 extractors. These numbers are locked; content conforms to them.
        AssertThat(Level02Controller.AuthoredStandardCount).OverrideFailureMessage(
            "Level 2 authors exactly 8 standard enemies at Normal.").IsEqual(8);
        AssertThat(Level02Controller.AuthoredEliteCount).OverrideFailureMessage(
            "The locked row says E = 0, so Level 2 places NO elites.").IsEqual(0);
        AssertThat(Level02Controller.AuthoredExtractorCount).IsEqual(3);

        var eraRoster = new HashSet<string>(StringComparer.Ordinal) { "laser_archer", "chrono_slasher" };
        var extractorIDs = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string id, Vector2 _) in Level02Controller.ExtractorPlacements) {
            AssertThat(extractorIDs.Add(id)).OverrideFailureMessage($"Duplicate extractor id '{id}'.").IsTrue();
        }

        foreach ((string enemyID, Vector2 _, Vector2? _, Vector2? _) in AllAuthoredSpawns()) {
            AssertThat(eraRoster.Contains(enemyID)).OverrideFailureMessage(
                $"'{enemyID}' is not on the Orléans roster (laser_archer + chrono_slasher).").IsTrue();
            EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertObject(data).IsNotNull();
            AssertThat(data.Tier).OverrideFailureMessage(
                $"'{enemyID}' must be a Standard-tier enemy; Level 2 places no elites.")
                .IsEqual(EnemyTier.Standard);
        }
    }

    [TestCase]
    public void ThePlacedExtractorsAndBossEncounterMatchTheAuthoredTables() {
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;

        AssertThat(level.Extractors.Count).IsEqual(Level02Controller.AuthoredExtractorCount);
        foreach (ChronalExtractor extractor in level.Extractors) {
            // 15 dust each is resource-owned (docs/DUST_ECONOMY.md) and must not be
            // re-specified by level code.
            AssertThat(extractor.DustReward).IsEqual(3);
        }

        AssertThat(level.BossEncounters.Count).IsEqual(1);
        AssertString(level.BossEncounters[0].Data.BossID).IsEqual("siegemaster_duke");

        // Live standards never exceed the siege line's soldiers and its swarm, inside the
        // level_02 pool config's standard_enemy warm count of 16.
        int largestWave = Level02Controller.Room2Spawns.Length + Level02Controller.SwarmSpawnXs.Length;
        AssertThat(largestWave).IsLessEqual(16);
    }

    [TestCase]
    public void TheBossResourceResolvesAndItsRangedBandFitsTheArena() {
        BossData boss = FTT.Core.AuthoredResources.Load<BossData>(Level02Controller.BossResourcePath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("siegemaster_duke");
        AssertThat(boss.PhaseThresholds.Length).OverrideFailureMessage(
            "The Siegemaster Duke is authored as a two-phase fight.").IsEqual(1);

        // BossController converts range thresholds at 60 px per world unit; the
        // arena has to be wide enough for the 9.0 m band to be reachable.
        const float pixelsPerUnit = 60f;
        float rangedPixels = boss.RangedRangeThreshold * pixelsPerUnit;
        float arenaWidth = Level02Controller.LevelWidth - 8960f;
        AssertThat(arenaWidth).OverrideFailureMessage(
            $"Boss arena is {arenaWidth} px but the ranged band alone needs {rangedPixels} px.")
            .IsGreater(rangedPixels * 2f);
    }

    // === Localization ===

    [TestCase]
    public void TheDialogueSetResolvesAndEveryAuthoredKeyIsLocalized() {
        var set = FTT.Core.AuthoredResources.Load<DialogueSetData>(Level02Controller.DialogueResourcePath);
        AssertObject(set).IsNotNull();
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_02");
        // Package 13 W4 (S23): four base beats (entrance, the vanguard absence, boss
        // intro, exit) plus the two N03 Joan variants - the recognition now lives in
        // the absence slot it replaces, not appended to the entrance.
        AssertThat(set.Sequences.Length).IsEqual(6);
        foreach (string variant in new[] { "level_02.absence@joan", "level_02.exit@joan" }) {
            DialogueSequenceData branch = set.Find(variant);
            AssertObject(branch).OverrideFailureMessage(
                $"The N03 recognition variant '{variant}' is missing.").IsNotNull();
            AssertString(branch.HeroConditionCharacterID).IsEqual("joan");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        TranslationServer.SetLocale("en");

        var required = new List<string> {
            "orleans_level_title", "orleans_room_vanguard", "orleans_room_rampart", "orleans_room_siege_line",
            "orleans_room_battlement", "orleans_room_boss", "orleans_objective_advance",
            "orleans_objective_towers", "orleans_objective_breach", "orleans_objective_defeat_boss",
            "orleans_objective_complete", "orleans_gate_shielded"
        };

        foreach (string sequenceID in new[] {
            "level_02.entrance", "level_02.absence", "level_02.boss_intro", "level_02.exit" }) {
            DialogueSequenceData sequence = set.Find(sequenceID);
            AssertObject(sequence).OverrideFailureMessage(
                $"Dialogue sequence '{sequenceID}' is missing from the set.").IsNotNull();
            AssertThat(sequence.LineCount).IsGreater(0);
            AssertThat(sequence.LineKeys.Length).OverrideFailureMessage(
                $"'{sequenceID}' has a speaker/line count mismatch.")
                .IsEqual(sequence.SpeakerNameKeys.Length);
            for (int index = 0; index < sequence.LineCount; index++) {
                required.Add(sequence.GetLineKey(index));
                required.Add(sequence.GetSpeakerKey(index));
                required.Add(sequence.GetEmotionKey(index));
            }
        }

        foreach (string key in required) {
            AssertThat(keys.Contains(key)).OverrideFailureMessage(
                $"'{key}' is missing from localization/en.csv.").IsTrue();
            // Node.Tr() reads the compiled translation, not the CSV: if they drift,
            // the level renders raw keys.
            AssertString(TranslationServer.Translate(key).ToString()).OverrideFailureMessage(
                $"'{key}' does not resolve through localization/en.en.translation. " +
                "Re-import localization/en.csv.").IsNotEqual(key);
        }
    }

    // === GU-E (2026-10-12): the three encounter locks ===

    [TestCase]
    public void TheThreeApproachRoomsAreEncounterLocksOneWavePerManifestRow() {
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;
        try {
            AssertThat(level.EncounterLocks.Count).IsEqual(3);
            EncounterLock vanguard = level.VanguardLock;
            EncounterLock siegeLine = level.SiegeLineLock;
            EncounterLock battlement = level.BattlementLock;
            AssertString(vanguard.LockID).IsEqual(Level02Controller.LockVanguard);
            AssertString(siegeLine.LockID).IsEqual(Level02Controller.LockSiegeLine);
            AssertString(battlement.LockID).IsEqual(Level02Controller.LockBattlement);

            // The plan's areas: 300-2000 (GR-PILOT carved it from 300-3200), 3220-6290 (to Gate A),
            // 6580-8690 (to Gate B; FIX-E moved its west edge from 6460 to widen the Middle anchor's corridor).
            AssertThat(vanguard.Area.Position.X).IsEqual(300f);
            AssertThat(vanguard.Area.End.X).IsEqual(2000f);
            AssertThat(siegeLine.Area.Position.X).IsEqual(3220f);
            AssertThat(siegeLine.Area.End.X).IsEqual(6290f);
            AssertThat(battlement.Area.Position.X).IsEqual(6580f);
            AssertThat(battlement.Area.End.X).IsEqual(8690f);

            // One wave per manifest row, the authored tables, the plan's triggers.
            AssertString(vanguard.Spec.Waves[0].RowID).IsEqual(Level02Controller.Room1RowID);
            AssertString(siegeLine.Spec.Waves[0].RowID).IsEqual(Level02Controller.Room2RowID);
            AssertString(battlement.Spec.Waves[0].RowID).IsEqual(Level02Controller.Room3RowID);
            AssertThat(vanguard.Spec.Waves[0].Spawns.Length).IsEqual(Level02Controller.Room1Spawns.Length);
            AssertThat(siegeLine.Spec.Waves[0].Spawns.Length).IsEqual(Level02Controller.Room2Spawns.Length);
            // GR-PILOT: the siege line's swarm is its second wave, its own manifest row.
            AssertThat(siegeLine.Spec.Waves.Length).IsEqual(2);
            AssertString(siegeLine.Spec.Waves[1].RowID).IsEqual(Level02Controller.SwarmRowID);
            AssertThat(siegeLine.Spec.Waves[1].Trigger).IsEqual(EncounterWaveTrigger.WhenAliveAtMost);
            AssertThat(battlement.Spec.Waves[0].Spawns.Length).IsEqual(Level02Controller.Room3Spawns.Length);
            AssertThat(vanguard.Spec.Waves[0].Trigger).IsEqual(EncounterWaveTrigger.OnBuild);
            AssertThat(siegeLine.Spec.Waves[0].Trigger).IsEqual(EncounterWaveTrigger.OnEngage);
            AssertThat(battlement.Spec.Waves[0].Trigger).IsEqual(EncounterWaveTrigger.OnBuild);

            // Armed at load: the pre-placed rooms stand dormant, the siege line's
            // soldiers come over the earthworks only once the hero is in.
            foreach (EncounterLock encounterLock in level.EncounterLocks) {
                AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);
                AssertThat(encounterLock.ExitBarrier.IsClosed).IsTrue();
                AssertThat(encounterLock.EntryBarrier.IsClosed).IsFalse();
            }
            Difficulty difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            AssertThat(vanguard.PlannedTotal)
                .IsEqual(StoryDifficultyTuning.ScaleEncounterCount(Level02Controller.Room1Spawns.Length, difficulty));
            AssertThat(vanguard.LivingMemberCount).IsEqual(vanguard.PlannedTotal);
            AssertThat(siegeLine.LivingMemberCount).IsEqual(0);
            AssertThat(battlement.LivingMemberCount).IsEqual(battlement.PlannedTotal);
            foreach (EnemyController member in vanguard.LivingMembers) AssertThat(member.IsEncounterDormant).IsTrue();

            // The siege line's exit fills Gate A under its column; the battlement's fills Gate B.
            AssertThat(siegeLine.ExitBarrier.CenterX).IsEqual(Level02Controller.GateAX);
            AssertThat(siegeLine.ExitBarrier.TopY).IsEqual(600f);
            AssertThat(battlement.ExitBarrier.CenterX).IsEqual(Level02Controller.GateBX);
            AssertThat(battlement.ExitBarrier.TopY).IsEqual(240f);
            AssertThat(siegeLine.Spec.RequiredBreakables.Length).IsEqual(1);
            AssertThat(siegeLine.Spec.RequiredBreakables[0] == level.ShieldTowerA).IsTrue();

            // Every later anchor lists the locks before it.
            IReadOnlyDictionary<string, string[]> baselines = level.AuthoredEncounterBaselines;
            AssertThat(baselines[Level02Controller.Checkpoint0].Length).IsEqual(0);
            AssertThat(string.Join(",", baselines[Level02Controller.Checkpoint1]))
                .IsEqual($"{Level02Controller.LockVanguard},{Level02Controller.LockSiegeLine}");
            AssertThat(string.Join(",", baselines[Level02Controller.Checkpoint2]))
                .IsEqual($"{Level02Controller.LockVanguard},{Level02Controller.LockSiegeLine},{Level02Controller.LockBattlement}");
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    [TestCase]
    public void TheMiddleAnchorStandsInTheCorridorPastGateAOutsideEveryLock() {
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;
        try {
            CheckpointTrigger middle = null;
            foreach (Node child in Children(level)) {
                if (child is CheckpointTrigger checkpoint && checkpoint.CheckpointID == Level02Controller.Checkpoint1) middle = checkpoint;
            }
            AssertObject(middle).IsNotNull();
            AssertThat(middle.Role).IsEqual(CheckpointRole.Middle);
            AssertThat(middle.GlobalPosition.X).IsEqual(Level02Controller.MiddleCheckpointX);
            // Past Gate A's barrier (east face 6324) and short of the battlement's entry (6560).
            AssertThat(middle.GlobalPosition.X - 30f > Level02Controller.GateAX + 24f).IsTrue();
            AssertThat(middle.GlobalPosition.X + 30f < level.BattlementLock.EntryBarrier.Footprint.Position.X).IsTrue();
            foreach (EncounterLock encounterLock in level.EncounterLocks) {
                bool inside = middle.GlobalPosition.X + 30f > encounterLock.Area.Position.X
                    && middle.GlobalPosition.X - 30f < encounterLock.Area.End.X;
                AssertThat(inside).OverrideFailureMessage($"The Middle anchor stands inside '{encounterLock.LockID}'.").IsFalse();
            }
            // FIX-E (2026-10-13): out of swing reach from inside the siege line. A
            // hero pressed against its closed exit barrier swings the roster's
            // longest basic hit (widest reach, largest AttackRange minor) short of
            // the strike surface's west edge (the anchor's x - 30). It stood at
            // 6370 and a template hit 1 reached it through the barrier.
            float pivot = level.SiegeLineLock.ExitBarrier.Footprint.Position.X - HeroHalfWidthPixels;
            float reach = LongestBasicSwingPixels();
            AssertThat(pivot + reach < middle.GlobalPosition.X - 30f)
                .OverrideFailureMessage($"A swing from the siege line reaches x {pivot + reach:0}, the Middle anchor's surface starts at {middle.GlobalPosition.X - 30f:0}.")
                .IsTrue();
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    /// <summary>
    /// FIX-E (2026-10-13), the review's scenario on the real scene: with the siege
    /// line engaged, a strike that reaches the Middle anchor's surface stabilizes
    /// nothing — its baseline lists the siege line, which a reload would build
    /// Cleared (and MarkWavesClearedThrough would break generator A). Once the
    /// siege line is cleared the same strike takes.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheMiddleAnchorRefusesAStrikeUntilTheSiegeLineIsCleared() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        StoryManager story = StoryManager.Instance;
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;
        FTT.Characters.PlayerController player = level.Player;
        try {
            for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            DialogueManager dialogue = DialogueManager.Instance;
            for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
            AssertThat(tree.Paused).IsFalse();
            CheckpointTrigger middle = level.GetNode<CheckpointTrigger>($"Checkpoint_{Level02Controller.Checkpoint1}");
            var surface = middle.GetNode<EnvironmentHurtboxAdapter>("StrikeSurface");
            // The vanguard is behind the hero (cleared, as a run reaching the siege line has it).
            level.VanguardLock.RestoreCleared();

            EncounterLock siegeLine = level.SiegeLineLock;
            player.GlobalPosition = new Vector2(3600f, 850f);
            player.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(siegeLine.State).IsEqual(EncounterLockState.Engaged);
            // Pressed against Gate A, inside the engaged lock.
            player.GlobalPosition = new Vector2(6250f, 850f);
            player.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(siegeLine.State).IsEqual(EncounterLockState.Engaged);

            AssertThat(middle.IsHeldByEncounterLock).IsTrue();
            surface.TakeHit(PlayerStrike());
            AssertThat(middle.IsActivated)
                .OverrideFailureMessage("The Middle anchor was stabilized from inside the engaged siege line.")
                .IsFalse();
            AssertThat(story.IsCheckpointActivated(Level02Controller.Checkpoint1)).IsFalse();

            await KillEveryMember(tree, siegeLine);
            level.ShieldTowerA.TakeEnvironmentDamage(level.ShieldTowerA.MaxHP);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(siegeLine.State).IsEqual(EncounterLockState.Cleared);
            AssertThat(middle.IsHeldByEncounterLock).IsFalse();
            surface.TakeHit(PlayerStrike());
            AssertThat(middle.IsActivated).IsTrue();
            AssertThat(story.IsCheckpointActivated(Level02Controller.Checkpoint1)).IsTrue();
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
            SessionExitGuard.ClearMarker();
            story.ClearLevelAttemptState();
        }
    }

    /// <summary>
    /// The siege line clears only when its soldiers are down AND generator A has
    /// fallen (a required breakable), and then both barriers open — in real
    /// physics frames on the real scene.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task ClearingTheSiegeLineNeedsItsGeneratorAsWellAsItsSoldiers() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;
        FTT.Characters.PlayerController player = level.Player;
        try {
            for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            DialogueManager dialogue = DialogueManager.Instance;
            for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
            AssertThat(tree.Paused).IsFalse();

            EncounterLock siegeLine = level.SiegeLineLock;
            player.GlobalPosition = new Vector2(3600f, 850f);
            player.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(siegeLine.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(siegeLine.EntryBarrier.IsClosed).IsTrue();
            // The soldiers on engage; the swarm comes later (Level02TraversalStretchTests).
            AssertThat(siegeLine.LivingMemberCount).IsEqual(Level02Controller.Room2Spawns.Length);

            await KillEveryMember(tree, siegeLine);
            AssertThat(siegeLine.LivingMemberCount).IsEqual(0);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(siegeLine.State)
                .OverrideFailureMessage("The siege line must hold while generator A still stands.")
                .IsEqual(EncounterLockState.Engaged);

            level.ShieldTowerA.TakeEnvironmentDamage(level.ShieldTowerA.MaxHP);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(siegeLine.State).IsEqual(EncounterLockState.Cleared);
            AssertThat(siegeLine.EntryBarrier.IsClosed).IsFalse();
            AssertThat(siegeLine.ExitBarrier.IsClosed).IsFalse();
            // The vanguard behind the hero is untouched by the teleport past it.
            AssertThat(level.VanguardLock.State).IsEqual(EncounterLockState.Armed);
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    // === Helpers ===

    /// <summary>Kills a lock's members until none is left, the waves their deaths bring included.</summary>
    private static async System.Threading.Tasks.Task KillEveryMember(SceneTree tree, EncounterLock encounterLock) {
        for (int round = 0; round < 8; round++) {
            foreach (EnemyController member in new List<EnemyController>(encounterLock.LivingMembers)) member.TakeDamage(99999);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            if (encounterLock.LivingMemberCount == 0) return;
        }
    }

    /// <summary>The 40 px hero body's half-width.</summary>
    private const float HeroHalfWidthPixels = 20f;

    /// <summary>
    /// FIX-E: the farthest a Story basic string hit reaches in front of the pivot
    /// over the whole roster — every step's far edge from the Story box resolver,
    /// at each hero's reach profile with every AttackRange minor of its grid bought.
    /// </summary>
    private static float LongestBasicSwingPixels() {
        float longest = 0f;
        foreach (string id in CharacterRoster.IDs) {
            float minor = 0f;
            ResonanceGridData grid = ResonanceProgression.LoadGrid(id);
            foreach (ResonanceNodeData node in grid?.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node != null && node.StatModifierKey == ResonanceStatKeys.AttackRange) minor += node.StatModifierValue;
            }
            FTT.Combat.BasicStringProfile profile = FTT.Combat.BasicComboRules.StringProfileFor(id);
            for (int step = 0; step < FTT.Combat.BasicComboRules.StringBoxHalfWidthUnits.Length; step++) {
                FTT.Characters.PlayerController.ResolveGroundStringBoxPixels(step, profile, 1f + minor,
                    out Vector2 size, out Vector2 offset);
                longest = Mathf.Max(longest, offset.X + size.X * 0.5f);
            }
        }
        return longest;
    }

    private static FTT.Combat.HitPayload PlayerStrike() => new() {
        AttackerIndex = 0,
        AttackID = "einstein.basic",
        HitboxID = "combo_1",
        AttackClass = FTT.Combat.AttackClass.Basic,
        Damage = 10f,
        ContactId = FTT.Combat.HitClassification.NextContactId()
    };

    private static IEnumerable<(string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)> AllAuthoredSpawns() {
        foreach (var spawn in Level02Controller.Room1Spawns) yield return spawn;
        foreach (var spawn in Level02Controller.Room2Spawns) yield return spawn;
        foreach (var spawn in Level02Controller.Room3Spawns) yield return spawn;
    }

    private static List<Node> Children(Node parent) {
        var collected = new List<Node>();
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) collected.Add(child);
        return collected;
    }

    // === D13 (2026-10-11): the Restoration Font on the real scene ===

    /// <summary>
    /// D13 (PROVISIONAL-FONT-PLACEMENT): the Orléans font stands where a hero can
    /// channel it, in real physics frames, and refuses once the level is complete
    /// — completion re-keys the use registry to the next level, so a channel
    /// running then is interrupted (with a refund) and none can start.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheOrleansFontIsUsableWhereItStandsAndRefusesOnceTheLevelIsComplete() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        // Completion advances the campaign: hand the position back after the
        // fixture (declared first, so it is disposed last).
        using var position = new CampaignPositionGuard();
        StoryManager.Instance.PrepareDirectLevel(CampaignLevel.Orleans, "einstein", Difficulty.Normal);
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;
        FTT.Characters.PlayerController player = level.Player;
        try {
            AssertObject(player).IsNotNull();
            RestorationFont font = level.PlacedRestorationFont;
            AssertObject(font).IsNotNull();
            AssertString(font.FontID).IsEqual("level_02.restoration_font");
            var area = font.GetNode<InteractionArea>("InteractionArea");

            // Settle the scene: drain any opening dialogue (it pauses gameplay).
            for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            DialogueManager dialogue = DialogueManager.Instance;
            for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) {
                dialogue.AdvanceLine();
            }
            AssertThat(tree.Paused).IsFalse();

            // Stand at the font, holding Interact.
            var source = new BufferedInputSource();
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Interact));
            InputManager.Instance.SetInputSource(player.PlayerIndex, source);
            player.GlobalPosition = font.GlobalPosition + new Vector2(0f, -50f);
            player.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 30 && !area.OverlapsBody(player); frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(area.OverlapsBody(player))
                .OverrideFailureMessage($"The hero at {player.GlobalPosition} never entered the font's area at {font.GlobalPosition}.")
                .IsTrue();
            AssertThat(player.GlobalPosition.DistanceTo(font.GlobalPosition) <= font.ChannelRangePixels)
                .OverrideFailureMessage($"The hero at {player.GlobalPosition} stands outside the channel range.")
                .IsTrue();
            AssertThat(area.TryInteract(player)).IsTrue();
            AssertThat(font.IsChanneling).IsTrue();

            // Completion: the running channel is interrupted, and then the font
            // refuses, though a use remains.
            level.ShowCompletionResults();
            AssertThat(level.LevelComplete).IsTrue();
            AssertThat(font.RefusesWhile()).IsTrue();
            for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(font.IsChanneling)
                .OverrideFailureMessage("A channel running at completion must be interrupted.")
                .IsFalse();
            AssertThat(font.ChannelRadial.Visible).IsFalse();
            // No channel and no restore run now, and a use remains, so only the
            // completion refusal can say no here.
            AssertThat(font.UsesRemaining > 0).IsTrue();
            AssertThat(area.OverlapsBody(player)).IsTrue();
            AssertThat(font.CanInteract(player))
                .OverrideFailureMessage("A complete level's font must refuse although a use remains.")
                .IsFalse();
            AssertThat(area.TryInteract(player)).IsFalse();
            AssertThat(font.IsChanneling).IsFalse();
        } finally {
            if (player != null && GodotObject.IsInstanceValid(player)) {
                InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            }
            LevelRewardDirectory.ResetAttempt();
        }
    }

    /// <summary>
    /// Captures the campaign position (level, difficulty, character, slot, wallet)
    /// and hands it back on dispose. Declare it BEFORE the level fixture so it is
    /// disposed after it: the fixture restores the slot it captured, and
    /// PrepareDirectLevel clears it, so the slot is restored last.
    /// </summary>
    private sealed class CampaignPositionGuard : IDisposable {
        private readonly CampaignLevel _level;
        private readonly Difficulty _difficulty;
        private readonly string _character;
        private readonly int _slot;
        private readonly int _wallet;

        public CampaignPositionGuard() {
            StoryManager story = StoryManager.Instance;
            _level = story.CurrentLevel;
            _difficulty = GameManager.Instance.CurrentSession.Difficulty;
            _character = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _wallet = story.ChronalDustCollected;
        }

        public void Dispose() {
            StoryManager story = StoryManager.Instance;
            story.StopLevelRun();
            story.PrepareDirectLevel(_level, string.IsNullOrEmpty(_character) ? "einstein" : _character, _difficulty);
            GameManager.Instance.CurrentSession.SelectedCharacterID = _character;
            story.SetDust(_wallet);
            story.ClearLevelAttemptState();
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _slot;
        }
    }

    /// <summary>
    /// Instantiates the authored scene in the runner tree against an optional scratch
    /// save, then hands back every shared singleton it touched. The gameplay pause is
    /// sampled and restored too: a leaked <c>SceneTree.Paused</c> freezes GdUnit's own
    /// transport node and hangs the session (CLAUDE.md failure signature 4).
    /// </summary>
    private sealed class Level02Fixture : IDisposable {
        public readonly Level02Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public Level02Fixture(StorySaveData save) {
            var tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level02Controller>();
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
