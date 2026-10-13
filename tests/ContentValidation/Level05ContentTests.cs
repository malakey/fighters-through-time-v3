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
            // GU-L4: four waves, one per encounter lock (the boat deck's old wave 3 is split in two).
            AssertThat(wave >= 1 && wave <= Level05Controller.WaveCount).IsTrue();
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

            // Later checkpoints resume with the ship further gone, and every lock
            // behind the checkpoint is built Cleared instead of replaying (GU-L4:
            // the encounters are the four locks).
            bool preBoss = checkpointID == Level05Controller.Checkpoint2;
            if (isEntryAnchor) {
                AssertThat(level.HullFlood.CurrentStep).IsEqual(0);
                AssertThat(level.StaircaseLock.State).IsEqual(EncounterLockState.Armed);
                AssertThat(level.BoilerCasingLock.State).IsEqual(EncounterLockState.Armed);
            } else {
                AssertThat(level.HullFlood.CurrentStep).IsGreater(0);
                AssertThat(level.StaircaseLock.RestoredCleared).IsTrue();
                AssertThat(level.BoilerCasingLock.RestoredCleared).IsTrue();
                AssertThat(level.EDeckLock.RestoredCleared).IsEqual(preBoss);
                AssertThat(level.BoatDeckLock.RestoredCleared).IsEqual(preBoss);
            }
            // A restored clear raises nothing: the anchor's own step stands.
            AssertThat(level.PendingHullStep).IsEqual(0);
        }
    }

    // === GU-L4 (2026-10-12): the encounter locks and the flood ===

    /// <summary>
    /// The four rooms are encounter locks, one wave per manifest row (the old
    /// wave 3 split between the E-deck run and the boat deck), each barrier
    /// standing on the deck under it; the debut Eraser is the E-deck lock's
    /// unscaled third wave; room 1 has a room trigger and camera rect; every
    /// anchor lists the locks before it.
    /// </summary>
    [TestCase]
    public void TheFourRoomsAreEncounterLocksAndTheDebutEraserIsAMemberOfTheEDeckLock() {
        using var fixture = new TitanicFixture(null);
        Level05Controller level = fixture.Level;
        AssertThat(level.EncounterLocks.Count).IsEqual(4);

        var expected = new (EncounterLock Lock, string ID, float MinX, float MaxX, float EntryFloor, float ExitFloor, int Wave, EncounterWaveTrigger Trigger)[] {
            (level.StaircaseLock, Level05Controller.LockStaircase, 380f, 3400f, 1300f, 1140f, 1, EncounterWaveTrigger.OnBuild),
            (level.BoilerCasingLock, Level05Controller.LockBoilerCasing, 3420f, 5880f, 1140f, 940f, 2, EncounterWaveTrigger.OnEngage),
            (level.EDeckLock, Level05Controller.LockEDeck, 6160f, 7520f, 940f, 800f, 3, EncounterWaveTrigger.OnBuild),
            (level.BoatDeckLock, Level05Controller.LockBoatDeck, 7540f, 8820f, 800f, 800f, 4, EncounterWaveTrigger.OnBuild)
        };
        foreach ((EncounterLock encounterLock, string id, float minX, float maxX, float entryFloor, float exitFloor,
                     int wave, EncounterWaveTrigger trigger) in expected) {
            AssertObject(encounterLock).OverrideFailureMessage($"'{id}' was not built.").IsNotNull();
            AssertString(encounterLock.LockID).IsEqual(id);
            AssertFloat(encounterLock.Area.Position.X).IsEqualApprox(minX, 0.5f);
            AssertFloat(encounterLock.Area.End.X).IsEqualApprox(maxX, 0.5f);
            AssertFloat(encounterLock.EntryBarrier.BottomY).IsEqualApprox(entryFloor, 0.5f);
            AssertFloat(encounterLock.ExitBarrier.BottomY).IsEqualApprox(exitFloor, 0.5f);
            EncounterLockWave first = encounterLock.Spec.Waves[0];
            AssertString(first.RowID).IsEqual(Level05Controller.WaveRowID(wave));
            AssertThat(first.Trigger).IsEqual(trigger);
            var authored = new List<string>();
            foreach ((string enemyID, int entryWave, Vector2 position) in Level05Controller.SpawnTable) {
                if (entryWave != wave) continue;
                authored.Add(enemyID);
                AssertThat(position.X > minX && position.X < maxX)
                    .OverrideFailureMessage($"Wave {wave}'s {enemyID} at {position} stands outside '{id}' {minX}..{maxX}.")
                    .IsTrue();
            }
            AssertString(string.Join(",", first.EnemyIDs)).IsEqual(string.Join(",", authored));
        }

        // The debut Eraser: the E-deck lock's unscaled third wave, dropping in
        // once one E-deck slasher is left; the route trigger no longer spawns.
        AssertThat(level.EDeckLock.Spec.Waves.Length).IsEqual(2);
        EncounterLockWave debut = level.EDeckLock.Spec.Waves[1];
        AssertString(debut.RowID).IsEqual(Level05Controller.EraserDebutRowID);
        AssertThat(debut.Scaled).IsFalse();
        AssertThat(debut.Trigger).IsEqual(EncounterWaveTrigger.WhenAliveAtMost);
        AssertThat(debut.AliveAtMost).IsEqual(1);
        AssertString(string.Join(",", debut.EnemyIDs)).IsEqual(level.EraserDebut.EnemyID);
        AssertThat(level.EraserDebut.RouteTriggerSpawns).IsFalse();
        foreach (EncounterLock other in new[] { level.StaircaseLock, level.BoilerCasingLock, level.BoatDeckLock }) {
            AssertThat(other.Spec.Waves.Length).IsEqual(1);
        }

        // Room 1 confines the camera now (it had no trigger at all).
        RoomTransitionTrigger room1 = null;
        foreach (RoomTransitionTrigger room in level.RoomTriggers) {
            if (room.RoomID == "titanic_grand_staircase") room1 = room;
        }
        AssertObject(room1).OverrideFailureMessage("Room 1 has no room trigger.").IsNotNull();
        AssertThat(room1.CameraBounds).IsEqual(Level05Controller.Room1CameraBounds);
        // The old x triggers are gone: the boiler surge is the casing's clear now.
        AssertObject(level.GetNodeOrNull("BoilerSurgeTrigger")).IsNull();

        IReadOnlyDictionary<string, string[]> map = level.AuthoredEncounterBaselines;
        AssertThat(map[Level05Controller.Checkpoint0].Length).IsEqual(0);
        AssertString(string.Join(",", map[Level05Controller.Checkpoint1]))
            .IsEqual($"{Level05Controller.LockStaircase},{Level05Controller.LockBoilerCasing}");
        AssertString(string.Join(",", map[Level05Controller.Checkpoint2]))
            .IsEqual($"{Level05Controller.LockStaircase},{Level05Controller.LockBoilerCasing},"
                + $"{Level05Controller.LockEDeck},{Level05Controller.LockBoatDeck},{Level05Controller.EraserDebutTriggerID}");
    }

    /// <summary>
    /// The anchors, the Restoration Font and the pre-boss beat's trigger stand
    /// outside every lock, and each anchor's strike surface lies beyond the
    /// longest basic swing from inside the locks its baseline lists.
    /// </summary>
    [TestCase]
    public void TheAnchorsTheFontAndThePreBossBeatStandOutsideEveryLock() {
        using var fixture = new TitanicFixture(null);
        Level05Controller level = fixture.Level;
        float swing = EncounterLockReach.LongestBasicSwingPixels();

        float middleWest = Level05Controller.MiddleCheckpointX - EncounterLockReach.StrikeSurfaceHalfWidthPixels;
        float casingReach = EncounterLockReach.EastReachFromInside(Level05Controller.BoilerCasingLockMaxX, swing);
        AssertThat(casingReach < middleWest)
            .OverrideFailureMessage($"A swing from the boiler casing reaches x {casingReach:0}; the Middle surface starts at {middleWest:0}.")
            .IsTrue();
        float preBossWest = Level05Controller.PreBossCheckpointX - EncounterLockReach.StrikeSurfaceHalfWidthPixels;
        float boatReach = EncounterLockReach.EastReachFromInside(Level05Controller.BoatDeckLockMaxX, swing);
        AssertThat(boatReach < preBossWest)
            .OverrideFailureMessage($"A swing from the boat deck reaches x {boatReach:0}; the PreBoss surface starts at {preBossWest:0}.")
            .IsTrue();

        AssertThat(level.Levels.TryGetCheckpointPosition(Level05Controller.Checkpoint2, out Vector2 preBoss)).IsTrue();
        AssertFloat(preBoss.X).IsEqualApprox(Level05Controller.PreBossCheckpointX, 0.5f);
        // The PreBoss anchor stands past the hull flood, on the stern rail, short of the arena.
        AssertThat(preBoss.X > Level05Controller.ArenaStartX && preBoss.X < Level05Controller.BossArenaBounds.Position.X).IsTrue();

        float fontX = level.AuthoredRestorationFontPosition.Value.X;
        var preBossTrigger = level.GetNodeOrNull<Area2D>("PreBossTrigger");
        AssertObject(preBossTrigger).IsNotNull();
        foreach (EncounterLock encounterLock in level.EncounterLocks) {
            Rect2 area = encounterLock.Area;
            AssertThat(fontX + 30f <= area.Position.X || fontX - 30f >= area.End.X)
                .OverrideFailureMessage($"The font at {fontX} stands inside '{encounterLock.LockID}'.").IsTrue();
            // The pre-boss beat pauses gameplay: it never fires inside a fight.
            float beatX = preBossTrigger.GlobalPosition.X;
            AssertThat(beatX + 35f <= area.Position.X || beatX - 35f >= area.End.X)
                .OverrideFailureMessage($"The pre-boss trigger at {beatX} stands inside '{encounterLock.LockID}'.").IsTrue();
            // So does the stern-rail room trigger, whose camera would take the view when the lock clears.
            foreach (RoomTransitionTrigger room in level.RoomTriggers) {
                if (room.RoomID != "titanic_stern_rail") continue;
                AssertThat(room.GlobalPosition.X - 40f >= area.End.X || room.GlobalPosition.X + 40f <= area.Position.X)
                    .OverrideFailureMessage($"The stern-rail trigger at {room.GlobalPosition.X} reaches into '{encounterLock.LockID}'.").IsTrue();
            }
        }
    }

    /// <summary>
    /// The ruling for the flood (plan §5): the hull steps advance on the locks'
    /// clears, never on x triggers, and never over a fight — each step rises once
    /// the hero stands on the next dry deck (Provisional), the boiler surge after
    /// the casing's clear, step 3 after the E-deck's, and the boat deck's 14 s
    /// timer starts only after the boat deck clears. The debut Eraser drops in as
    /// an E-deck member once one slasher is left and the trigger adopts it for its
    /// bark. In real frames on the real scene, resumed at the entry anchor.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheHullFloodRisesOnEachLockClearOnlyOnceTheHeroIsClearOfIt() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new TitanicFixture(new StorySaveData {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Titanic),
            LastCheckpointID = Level05Controller.Checkpoint0,
            CurrentHP = 100
        });
        // Real frames: go slotless so nothing reaches a slot file.
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level05Controller level = fixture.Level;
        RisingWaterZone hull = level.HullFlood;
        PlayerController hero = level.Player;
        AssertThat(hull.CurrentStep).IsEqual(0);

        async System.Threading.Tasks.Task Engage(EncounterLock encounterLock, Vector2 at) {
            Place(hero, at);
            for (int frame = 0; frame < 30 && encounterLock.State != EncounterLockState.Engaged; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(encounterLock.State)
                .OverrideFailureMessage($"'{encounterLock.LockID}' did not engage with the hero at {at}.")
                .IsEqual(EncounterLockState.Engaged);
        }
        async System.Threading.Tasks.Task KillAll(EncounterLock encounterLock) {
            for (int pass = 0; pass < 6 && encounterLock.State == EncounterLockState.Engaged; pass++) {
                foreach (EnemyController member in encounterLock.LivingMembers) member.TakeDamage(99999);
                for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
            AssertThat(encounterLock.ForceCleared).IsFalse();
        }

        // Area 1, cleared with the hero still on D1: step 1 would flood it, so it waits.
        await Engage(level.StaircaseLock, new Vector2(1600f, 1210f));
        await KillAll(level.StaircaseLock);
        AssertThat(hull.CurrentStep).IsEqual(0);
        AssertThat(level.PendingHullStep).IsEqual(1);
        // On D2 the deck is above step 1's line: the water comes in behind the hero.
        Place(hero, new Vector2(3000f, 1130f));
        for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(hull.CurrentStep).IsEqual(1);
        AssertThat(level.PendingHullStep).IsEqual(0);

        // Area 2 (its crew spawns on engage), cleared on D2: the boiler surge waits for E2.
        await Engage(level.BoilerCasingLock, new Vector2(3800f, 1130f));
        await KillAll(level.BoilerCasingLock);
        AssertThat(hull.CurrentStep).IsEqual(1);
        AssertThat(level.PendingHullStep).IsEqual(2);
        Place(hero, new Vector2(5700f, 930f));
        for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(hull.CurrentStep).IsEqual(2);

        // Area 3a: the debut Eraser drops in once one slasher is left, as a member,
        // and the trigger adopts it for Sarah's bark.
        EncounterLock eDeck = level.EDeckLock;
        await Engage(eDeck, new Vector2(6500f, 930f));
        IReadOnlyList<EnemyController> slashers = eDeck.LivingMembers;
        AssertThat(slashers.Count).IsGreater(0);
        bool eraserSeen = false;
        for (int pass = 0; pass < 6 && !eraserSeen; pass++) {
            IReadOnlyList<EnemyController> living = eDeck.LivingMembers;
            if (living.Count > 1) living[0].TakeDamage(99999);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            foreach (EnemyController member in eDeck.LivingMembers) {
                if (member.Data?.EnemyID == EraserDebutTrigger.EraserEnemyID) {
                    eraserSeen = true;
                    AssertThat(member.IsEncounterMember).IsTrue();
                    AssertString(member.EncounterLockID).IsEqual(Level05Controller.LockEDeck);
                }
            }
        }
        AssertThat(eraserSeen).OverrideFailureMessage("The debut Eraser never joined the E-deck lock.").IsTrue();
        AssertThat(level.EraserDebut.EncounterLive).IsTrue();
        AssertThat(level.EraserDebut.BarkSeen).IsTrue();
        AssertThat(level.EraserDebut.SpawnCount).IsEqual(1);
        await KillAll(eDeck);
        // Cleared on E2: step 3 floods E2, so it waits for the boat deck.
        AssertThat(hull.CurrentStep).IsEqual(2);
        AssertThat(level.PendingHullStep).IsEqual(3);
        Place(hero, new Vector2(7520f, 790f));
        for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(hull.CurrentStep).IsEqual(3);
        AssertObject(level.GetNodeOrNull<Timer>("BoatDeckFloodTimer"))
            .OverrideFailureMessage("The boat deck's timer started before its fight was cleared.").IsNull();

        // Area 3b: its clear starts the last race.
        await Engage(level.BoatDeckLock, new Vector2(8000f, 790f));
        await KillAll(level.BoatDeckLock);
        AssertObject(level.GetNodeOrNull<Timer>("BoatDeckFloodTimer"))
            .OverrideFailureMessage("The boat deck's clear did not start the 14 s timer.").IsNotNull();
        AssertThat(hull.CurrentStep).IsEqual(3);
    }

    private static void Place(PlayerController hero, Vector2 at) {
        hero.GlobalPosition = at;
        hero.Velocity = Vector2.Zero;
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
