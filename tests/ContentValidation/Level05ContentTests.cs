using System;
using System.Collections.Generic;
using System.IO;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 5 Wave A: Level 5 - The Sinking Titanic, the Act I finale.
///
/// What is pinned here is the contract the level cannot silently drift out of:
/// the manifest scene path and level id, the three checkpoint ids, the four-beat
/// dialogue set (the extra `preboss` beat belongs to levels 5/12/15) with every
/// line key localized, the DUST_ECONOMY-locked 12/1/1/4 encounter budget authored
/// with cultist-only ids, and the era mechanic itself - water that really
/// submerges and really drowns, plus the resume guarantee that a player who saved
/// at a checkpoint never wakes up underwater.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level05ContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_05_Titanic.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_05_dialogue.tres";
    private const int ScratchSlot = 2;

    // === Scene and identity ===

    [TestCase]
    public void TheAuthoredSceneLoadsInstantiatesAndFreesCleanly() {
        // Mirrors the smoke-test contract: instantiate without entering the tree, so
        // this case proves the .tscn resolves its script and template instances
        // without also running the whole level build.
        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).IsNotNull();

        Node instance = scene.Instantiate();
        AssertObject(instance).IsNotNull();
        AssertThat(instance is Level05Controller).IsTrue();
        AssertObject(instance.GetNodeOrNull<StoryDropSystem>("StoryDropSystem")).IsNotNull();
        // Ruptured steam pipes: the authored era-flavour hazards on the flooding decks.
        AssertObject(instance.GetNodeOrNull<StoryCyclicHazard>("RupturedSteamPipeA")).IsNotNull();
        AssertObject(instance.GetNodeOrNull<StoryCyclicHazard>("RupturedSteamPipeC")).IsNotNull();
        instance.Free();
    }

    [TestCase]
    public void TheControllerCarriesTheManifestIdentityAndTheCampaignSlot() {
        var level = new Level05Controller();
        try {
            AssertString(level.LevelID).IsEqual("level_05_titanic");
            AssertThat(level.Level).IsEqual(CampaignLevel.Titanic);
            AssertString(level.LevelTitleKey).IsEqual("titanic_level_title");
            AssertString(level.DialogueSetPath).IsEqual(DialoguePath);
            // The scene must sit exactly where StoryManager routes the campaign.
            AssertString(StoryManager.GetLevelScenePath(CampaignLevel.Titanic)).IsEqual(ScenePath);

            AssertString(level.EntranceDialogueID).IsEqual("level_05.entrance");
            AssertString(level.PreBossDialogueID).IsEqual("level_05.preboss");
            AssertString(level.BossIntroDialogueID).IsEqual("level_05.boss_intro");
            AssertString(level.ExitDialogueID).IsEqual("level_05.exit");
        } finally {
            level.Free();
        }
    }

    [TestCase]
    public void ExactlyThreeCheckpointsRegisterUnderTheLockedIDs() {
        using var fixture = new TitanicFixture(null);
        AssertThat(Level05Controller.CheckpointIDs.Length).IsEqual(3);
        for (int index = 0; index < 3; index++) {
            string id = $"level_05_titanic_checkpoint_{index}";
            AssertString(Level05Controller.CheckpointIDs[index]).IsEqual(id);
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered.").IsTrue();
        }
    }

    // === Dialogue ===

    [TestCase]
    public void TheDialogueSetCarriesAllFourBeatsIncludingThePreBossSequence() {
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        AssertObject(set).IsNotNull();
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_05");

        var ids = new List<string>();
        foreach (DialogueSequenceData sequence in set.Sequences) ids.Add(sequence.DialogueID);
        // preboss is the Act-finale extra beat (plan section 2.3: levels 5, 12, 15);
        // postboss is Package 13 W4's S18/S19 exchange at the sealing anchor.
        AssertThat(ids).ContainsExactlyInAnyOrder(
            "level_05.entrance", "level_05.preboss", "level_05.boss_intro",
            "level_05.postboss", "level_05.exit");
    }

    [TestCase]
    public void EveryDialogueLineSpeakerAndLevelKeyResolvesInTheEnglishTable() {
        HashSet<string> keys = LocalizationKeys();
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);

        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertThat(sequence.LineKeys.Length).IsGreater(0);
            // A speaker/emotion array that drifts out of step silently mislabels lines.
            AssertThat(sequence.SpeakerNameKeys.Length).IsEqual(sequence.LineKeys.Length);
            AssertThat(sequence.EmotionKeys.Length).IsEqual(sequence.LineKeys.Length);

            foreach (string key in sequence.LineKeys) {
                AssertThat(keys.Contains(key))
                    .OverrideFailureMessage($"Line key '{key}' is missing from localization/en.csv.")
                    .IsTrue();
                AssertThat(key.StartsWith("dlg_l05_", StringComparison.Ordinal)).IsTrue();
            }
            foreach (string key in sequence.SpeakerNameKeys) {
                AssertThat(keys.Contains(key))
                    .OverrideFailureMessage($"Speaker key '{key}' is missing from localization/en.csv.")
                    .IsTrue();
            }
        }

        foreach (string key in new[] {
            "titanic_level_title", "titanic_objective_reach_stern", "titanic_objective_boat_deck",
            "titanic_objective_defeat_boss", "titanic_objective_complete",
            "titanic_room_grand_staircase", "titanic_room_boiler_casing",
            "titanic_room_boat_deck", "titanic_room_stern_rail"
        }) {
            AssertThat(keys.Contains(key))
                .OverrideFailureMessage($"Level key '{key}' is missing from localization/en.csv.")
                .IsTrue();
        }
    }

    [TestCase]
    public void TheExitBeatPlantsTheEncryptedKeyLeadThatLevelTwelvePaysOff() {
        // The Act I hook (design §16, Level 5 exit): the Overseer carried an
        // encrypted key that points off this world - Level 11's exit says it is
        // nearly cracked and Level 12 opens on it. Package 13 W4 replaced the
        // cargo-uplink clue (the old dlg_l05_exit_4) with this line.
        string exitLine = LocalizationValue("dlg_l05_exit_1").ToLowerInvariant();
        AssertString(exitLine).Contains("encrypted key");
        AssertString(exitLine).Contains("off this world");
        AssertString(LocalizationValue("dlg_l12_entrance_1").ToLowerInvariant()).Contains("key's cracked");
    }

    // === Encounter economy (locked by docs/DUST_ECONOMY.md) ===

    [TestCase]
    public void TheAuthoredSpawnTableMatchesTheLockedTwelveOneOneFourBudget() {
        AssertThat(Level05Controller.StandardEnemyCount)
            .OverrideFailureMessage("Level 5 is locked at 12 standard enemies by DUST_ECONOMY.md.")
            .IsEqual(12);
        AssertThat(Level05Controller.EliteEnemyCount)
            .OverrideFailureMessage("Level 5 is locked at 1 elite by DUST_ECONOMY.md.")
            .IsEqual(1);
        AssertThat(Level05Controller.SpawnTable.Length).IsEqual(13);
        AssertThat(Level05Controller.ExtractorPlacements.Length)
            .OverrideFailureMessage("Level 5 is locked at 4 Chronal Extractors.")
            .IsEqual(4);

        var extractorIDs = new HashSet<string>();
        foreach ((string id, Vector2 _) in Level05Controller.ExtractorPlacements) {
            AssertThat(extractorIDs.Add(id))
                .OverrideFailureMessage($"Duplicate extractor id '{id}'.").IsTrue();
        }
    }

    [TestCase]
    public void OnlyCultistEnemiesCrewTheTitanicAndBothTiersResolve() {
        // design-godot.md 2363: a civilian liner has no local military population to
        // brainwash, so the Archive deploys only its own operatives here. An
        // era-altered local id showing up in this table is a narrative regression.
        foreach ((string enemyID, int wave, Vector2 _) in Level05Controller.SpawnTable) {
            AssertThat(enemyID is Level05Controller.StandardEnemyID or Level05Controller.EliteEnemyID)
                .OverrideFailureMessage(
                    $"'{enemyID}' is not a Future Cultist; the Titanic roster is cultist-only.")
                .IsTrue();
            AssertThat(wave >= 1 && wave <= 3).IsTrue();
        }

        var standard = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level05Controller.StandardEnemyID}.tres");
        var elite = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level05Controller.EliteEnemyID}.tres");
        AssertObject(standard).IsNotNull();
        AssertObject(elite).IsNotNull();
        AssertString(standard.EnemyID).IsEqual("chrono_slasher");
        AssertString(elite.EnemyID).IsEqual("tech_enforcer");
        AssertThat(standard.Tier).IsEqual(EnemyTier.Standard);
        AssertThat(elite.Tier).IsEqual(EnemyTier.Elite);
    }

    [TestCase]
    public void EveryWaveFitsInsideTheLevelPoolWarmCounts() {
        // Warm counts are concurrency caps (plan section 2.5): standard_enemy 14,
        // elite_enemy 4 for level_05_titanic_pools.
        var perWave = new Dictionary<int, int>();
        int eliteTotal = 0;
        foreach ((string enemyID, int wave, Vector2 _) in Level05Controller.SpawnTable) {
            perWave.TryGetValue(wave, out int running);
            perWave[wave] = running + 1;
            if (enemyID == Level05Controller.EliteEnemyID) eliteTotal++;
        }
        foreach (KeyValuePair<int, int> wave in perWave) {
            AssertThat(wave.Value)
                .OverrideFailureMessage($"Wave {wave.Key} spawns {wave.Value} enemies at once, over the warm count.")
                .IsLessEqual(14);
        }
        AssertThat(eliteTotal).IsLessEqual(4);
    }

    // === Boss ===

    [TestCase]
    public void TheTidalEraserResolvesAndItsRangedBandFitsTheSternArena() {
        var boss = AuthoredResources.Load<BossData>(Level05Controller.BossResourcePath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("tidal_eraser");
        // One threshold = two phases, and phase 2 is what opens the arena flood.
        AssertThat(boss.PhaseThresholds.Length).IsEqual(1);
        AssertThat(boss.AttackPattern).IsEqual(BossAttackPattern.DistanceBased);

        // BossController converts range thresholds at 60 px per world unit; the
        // arena has to be wide enough for the boss to actually use its ranged band.
        const float pixelsPerUnit = 60f;
        float arenaWidth = Level05Controller.LevelWidth - Level05Controller.ArenaStartX;
        AssertThat(boss.RangedRangeThreshold * pixelsPerUnit < arenaWidth)
            .OverrideFailureMessage(
                $"Ranged band {boss.RangedRangeThreshold * pixelsPerUnit} px does not fit a {arenaWidth} px arena.")
            .IsTrue();
        AssertThat(boss.MeleeRangeThreshold < boss.RangedRangeThreshold).IsTrue();
    }

    // === Era identity: the rising water ===

    [TestCase]
    public void TheHullFloodRisesInAuthoredStepsThenSubmergesAndDrownsThePlayer() {
        using var fixture = new TitanicFixture(null);
        Level05Controller level = fixture.Level;
        RisingWaterZone hull = level.HullFlood;
        AssertObject(hull).IsNotNull();
        AssertThat(hull.StepCount).IsEqual(Level05Controller.HullStepHeights.Length);
        AssertThat(hull.CurrentStep).IsEqual(0);

        // Rising water means a smaller Y: every step must lift the line.
        float previousLine = hull.WaterLineGlobalY;
        for (int step = 1; step < hull.StepCount; step++) {
            AssertThat(hull.AdvanceWaterLevel()).IsTrue();
            AssertThat(hull.WaterLineGlobalY < previousLine)
                .OverrideFailureMessage($"Step {step} did not raise the water line.").IsTrue();
            previousLine = hull.WaterLineGlobalY;
        }
        AssertThat(hull.AdvanceWaterLevel())
            .OverrideFailureMessage("The hull flood must stop at its top authored step.").IsFalse();

        PlayerController player = level.Player;
        player.GlobalPosition = new Vector2(1200f, hull.WaterLineGlobalY + 120f);
        hull.AddPlayer(player);
        hull.TickSubmersion(0.1f);
        AssertThat(hull.IsPlayerSubmerged(player)).IsTrue();
        AssertFloat(player.EnvironmentMoveMultiplier).IsLess(1f);

        int startingHP = player.CurrentHP;
        // Past the grace period the drowning ticks have to actually bite.
        for (int tick = 0; tick < 80; tick++) hull.TickSubmersion(0.1f);
        AssertThat(player.CurrentHP).IsLess(startingHP);

        // Climbing above the line surfaces the player and hands the speed back.
        player.GlobalPosition = new Vector2(1200f, hull.WaterLineGlobalY - 200f);
        hull.TickSubmersion(0.1f);
        AssertThat(hull.IsPlayerSubmerged(player)).IsFalse();
        AssertFloat(player.EnvironmentMoveMultiplier).IsEqualApprox(1f, 0.001f);
    }

    [TestCase]
    public void TheSternArenaStaysDryUntilBossPhaseTwoAndNeverDrownsTheGantries() {
        using var fixture = new TitanicFixture(null);
        Level05Controller level = fixture.Level;
        RisingWaterZone arena = level.ArenaFlood;
        AssertObject(arena).IsNotNull();
        AssertThat(arena.Enabled)
            .OverrideFailureMessage("The stern arena must be sealed until the boss opens it.").IsFalse();
        AssertThat(arena.CurrentStep).IsEqual(0);
        // Non-overlapping zones: two live water sources would stack their slows.
        AssertThat(arena.WaterID).IsNotEqual(level.HullFlood.WaterID);

        // Phase 2 of the Tidal Eraser is what makes it the drowning-arena boss.
        EventBus.Instance.RaiseBossPhaseChanged(1);
        AssertThat(arena.Enabled).IsTrue();
        AssertFloat(arena.AutoAdvanceSeconds).IsGreater(0f);

        const float sternDeckY = 800f;
        const float gantryY = 600f;
        arena.SetWaterLevel(1);
        AssertThat(sternDeckY >= arena.WaterLineGlobalY)
            .OverrideFailureMessage("Phase 2 must actually flood the stern deck.").IsTrue();

        arena.SetWaterLevel(arena.StepCount - 1);
        AssertThat(gantryY < arena.WaterLineGlobalY)
            .OverrideFailureMessage(
                "Even the deepest arena step must leave the rail gantries dry: the flood raises " +
                "pressure, it is not a kill floor.").IsTrue();
    }

    // === Checkpoint resume must never be a drowning ===

    [TestCase]
    public void ResumingAtAnyCheckpointRestoresASurvivableWaterState() {
        foreach (string checkpointID in Level05Controller.CheckpointIDs) {
            using var fixture = new TitanicFixture(new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Titanic),
                LastCheckpointID = checkpointID,
                CurrentHP = 70,
                CurrentUltimateMeter = 25f
            });
            Level05Controller level = fixture.Level;

            bool isEntryAnchor = checkpointID == Level05Controller.Checkpoint0;
            AssertThat(level.ResumedMidLevel)
                .OverrideFailureMessage($"'{checkpointID}' did not restore.").IsTrue();
            AssertString(level.ResumedCheckpointID).IsEqual(checkpointID);

            // The whole point: the player must not wake up under the water line.
            AssertThat(level.IsCheckpointResumeDry(checkpointID))
                .OverrideFailureMessage($"Resuming at '{checkpointID}' leaves the player submerged.")
                .IsTrue();
            AssertThat(level.HullFlood.IsPlayerSubmerged(level.Player))
                .OverrideFailureMessage($"The player spawned underwater at '{checkpointID}'.")
                .IsFalse();

            // The arena flood is never carried into a resume.
            AssertThat(level.ArenaFlood.Enabled).IsFalse();
            AssertThat(level.ArenaFlood.CurrentStep).IsEqual(0);

            // Later checkpoints resume with the ship further gone, and every wave
            // behind the checkpoint stays cleared instead of replaying.
            if (isEntryAnchor) {
                AssertThat(level.HullFlood.CurrentStep).IsEqual(0);
                AssertThat(level.HasWaveSpawned(2)).IsFalse();
            } else {
                AssertThat(level.HullFlood.CurrentStep).IsGreater(0);
                AssertThat(level.HasWaveSpawned(1)).IsTrue();
                AssertThat(level.HasWaveSpawned(2)).IsTrue();
            }
            if (checkpointID == Level05Controller.Checkpoint2) {
                AssertThat(level.HasWaveSpawned(3)).IsTrue();
            }
        }
    }

    [TestCase]
    public void TheResumeGuardLowersTheWaterWhenAnAuthoredStepWouldDrownTheCheckpoint() {
        using var fixture = new TitanicFixture(null);
        Level05Controller level = fixture.Level;

        // Drive the flood past every authored checkpoint step, then resume: the
        // guard has to walk the water back down rather than trust the table.
        while (level.HullFlood.AdvanceWaterLevel()) { }
        int floodedStep = level.HullFlood.CurrentStep;
        AssertThat(floodedStep).IsGreater(Level05Controller.HullStepForCheckpoint(
            Level05Controller.Checkpoint2));

        level.RestoreFloodForCheckpoint(Level05Controller.Checkpoint2);
        AssertThat(level.HullFlood.CurrentStep).IsLess(floodedStep);
        AssertThat(level.IsCheckpointResumeDry(Level05Controller.Checkpoint2)).IsTrue();

        level.Levels.TryGetCheckpointPosition(Level05Controller.Checkpoint2, out Vector2 respawn);
        AssertThat(respawn.Y < level.HullFlood.WaterLineGlobalY).IsTrue();
    }

    // === Helpers ===

    private static HashSet<string> LocalizationKeys() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        AssertThat(keys.Count).OverrideFailureMessage("localization/en.csv did not read.").IsGreater(500);
        return keys;
    }

    private static string LocalizationValue(string key) {
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0 && line[..comma] == key) return line[(comma + 1)..];
        }
        return "";
    }

    /// <summary>
    /// Builds the authored scene in the runner tree against an optional scratch
    /// save, then hands every shared singleton back untouched: session slot,
    /// character, save row, pooled enemies, and the gameplay pause flag. A leaked
    /// pause would freeze GdUnit's own transport node (CLAUDE.md signature 4).
    /// </summary>
    private sealed class TitanicFixture : IDisposable {
        public readonly Level05Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public TitanicFixture(StorySaveData save) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level05Controller>();
            Level.Name = "Level_05_Titanic_Test";
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            // Pooled enemies are children of the level; hand them back before the
            // level frees them, or the pool keeps freed nodes in its Active list.
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
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
