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
/// Package 5 Wave B: content contracts for Level 10 - The Globe Theatre, London 1599.
/// <para>
/// Covers the per-level gate from docs/PACKAGE5_CAMPAIGN_PLAN.md §5: the scene
/// resolves at the exact <see cref="StoryManager"/> path (the Globe/London filename
/// tripwire), the controller reports the manifest level id and its three
/// checkpoints, the dialogue set and every authored line key are localized, the
/// authored encounter table matches the locked 10/0/1/3 economy row, the Tragedy
/// King's authored ranged band fits the tiring-house arena, and all three era
/// mechanics are driven for real: a stage board shakes before it drops and is
/// non-solid while open, the gallery rigging is the tight fast indoor discipline
/// rather than Nassau's open-air crossings, and the audience arms only after the
/// idle threshold and disarms the moment the player moves.
/// </para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level10ContentTests {
    private const string ScenePath = "res://scenes/campaign/Level_10_Globe.tscn";
    private const string LevelID = "level_10_globe";
    private const string DialoguePath = "res://resources/Dialogue/level_10_dialogue.tres";
    private const string BossPath = "res://resources/Bosses/tragedy_king.tres";

    /// <summary>Story world units to pixels, matching BossController.PixelsPerUnit.</summary>
    private const float PixelsPerUnit = 60f;

    /// <summary>One 60 Hz physics step, for driving the era mechanics by hand.</summary>
    private const float Step = 1f / 60f;

    private const int ScratchSlot = 2;

    // === Scene and identity ===

    [TestCase]
    public void TheGlobeSceneLoadsInstantiatesAndFreesCleanly() {
        // The Globe/London filename tripwire: the manifest row and the pool config
        // both say "globe", and StoryManager was corrected to match. A rename here
        // silently breaks the campaign route.
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.London)).IsEqual(ScenePath);

        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).OverrideFailureMessage($"{ScenePath} did not load.").IsNotNull();

        var instance = scene.Instantiate<Level10Controller>();
        AssertObject(instance).IsNotNull();
        AssertString(instance.LevelID).IsEqual(LevelID);
        instance.Free();
    }

    [TestCase]
    public void TheControllerReportsTheManifestIdentityAndRegistersExactlyThreeCheckpoints() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;

        AssertString(level.LevelID).IsEqual(LevelID);
        AssertThat(level.Level).IsEqual(CampaignLevel.London);
        AssertString(level.LevelTitleKey).IsEqual("globe_level_title");
        AssertString(level.DialogueSetPath).IsEqual(DialoguePath);
        AssertString(level.DialoguePrefix).IsEqual("level_10");
        AssertString(level.EntranceDialogueID).IsEqual("level_10.entrance");
        AssertString(level.BossIntroDialogueID).IsEqual("level_10.boss_intro");
        AssertString(level.ExitDialogueID).IsEqual("level_10.exit");

        // CheckpointTrigger resolves the manager by literal node name.
        AssertObject(level.GetNodeOrNull<LevelManager>("LevelManager")).IsNotNull();

        for (int index = 0; index < 3; index++) {
            string id = $"{LevelID}_checkpoint_{index}";
            AssertThat(level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered.").IsTrue();
        }
        AssertThat(level.Levels.TryGetCheckpointPosition($"{LevelID}_checkpoint_3", out Vector2 _)).IsFalse();
    }

    [TestCase]
    public void TheLevelIsFourConfinedRoomsClimbingFromTheCellarToTheUpperGallery() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;

        AssertThat(Mathf.IsEqualApprox(level.LevelBounds.Size.X, Level10Controller.LevelWidth)).IsTrue();
        // Plan §5 allows 8000-14000 px; the agent brief narrows the Globe to 9000-11000.
        AssertThat(level.LevelBounds.Size.X >= 9000f && level.LevelBounds.Size.X <= 11000f).IsTrue();
        AssertThat(level.RoomTriggers.Count).IsEqual(4);

        foreach (RoomTransitionTrigger room in level.RoomTriggers) {
            AssertThat(string.IsNullOrWhiteSpace(room.RoomID)).IsFalse();
            // A confiner narrower than the 1920 px reference viewport clamps to
            // nothing useful (L02 precedent).
            AssertThat(room.CameraBounds.Size.X >= 1920f)
                .OverrideFailureMessage($"Room '{room.RoomID}' confines to {room.CameraBounds.Size.X} px.").IsTrue();
            AssertThat(level.LevelBounds.Encloses(room.CameraBounds))
                .OverrideFailureMessage($"Room '{room.RoomID}' escapes the level bounds.").IsTrue();
            // A1 back-fills CameraPath; an unlinked room silently loses confinement.
            AssertThat(room.CameraPath != null && !room.CameraPath.IsEmpty).IsTrue();
            AssertThat(room.GetNodeOrNull<StoryCameraConfiner>(room.CameraPath) == level.Camera).IsTrue();
        }

        // The Globe is a building, not a corridor: the cellar boards and the upper
        // gallery have to be far enough apart for the climb to exist at all.
        AssertThat(Level10Controller.LevelHeight > 1080f).IsTrue();
        float highestPlatform = Level10Controller.LevelHeight;
        int platforms = 0;
        foreach (Node child in level.GetChildren()) {
            if (child is not StaticBody2D body) continue;
            if (!body.Name.ToString().StartsWith("Platform_", StringComparison.Ordinal)) continue;
            platforms++;
            highestPlatform = Mathf.Min(highestPlatform, body.Position.Y);
        }
        AssertThat(platforms).IsGreater(12);
        AssertThat(highestPlatform <= 420f)
            .OverrideFailureMessage($"Highest gallery platform is at y={highestPlatform}; no real vertical section.")
            .IsTrue();
        // ...and the under-stage cellar is genuinely below the boards.
        AssertThat(Level10Controller.CellarY > Level10Controller.GroundY + 300f).IsTrue();
    }

    // === Dialogue and localization ===

    [TestCase]
    public void TheDialogueSetResolvesAndEveryAuthoredKeyIsInTheEnglishTable() {
        var dialogue = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        AssertObject(dialogue).OverrideFailureMessage($"{DialoguePath} did not load.").IsNotNull();
        AssertString(dialogue.DialogueSetID).IsEqual("dialogue_level_10");

        HashSet<string> keys = EnglishKeys();
        // Package 11 A6: the two N03 Shakespeare hero variants join the three base beats.
        string[] expectedSequences = {
            "level_10.entrance", "level_10.boss_intro", "level_10.exit",
            "level_10.entrance@shakespeare", "level_10.exit@shakespeare" };
        AssertThat(dialogue.Sequences.Length).IsEqual(expectedSequences.Length);

        foreach (string dialogueID in expectedSequences) {
            DialogueSequenceData sequence = dialogue.Find(dialogueID);
            AssertObject(sequence).OverrideFailureMessage($"Sequence '{dialogueID}' missing.").IsNotNull();
            AssertThat(sequence.LineCount).IsGreater(0);
            AssertThat(sequence.LineKeys.Length).IsEqual(sequence.SpeakerNameKeys.Length);

            for (int index = 0; index < sequence.LineCount; index++) {
                string lineKey = sequence.GetLineKey(index);
                AssertThat(lineKey.StartsWith("dlg_l10_", StringComparison.Ordinal))
                    .OverrideFailureMessage($"'{lineKey}' does not follow the dlg_lNN_ convention.").IsTrue();
                AssertThat(keys.Contains(lineKey))
                    .OverrideFailureMessage($"Line key '{lineKey}' is not in localization/en.csv.").IsTrue();
                AssertThat(keys.Contains(sequence.GetSpeakerKey(index)))
                    .OverrideFailureMessage($"Speaker key '{sequence.GetSpeakerKey(index)}' is not in localization/en.csv.").IsTrue();
            }
        }
    }

    [TestCase]
    public void EveryVisibleGlobeStringHasAnEnglishEntry() {
        HashSet<string> keys = EnglishKeys();
        string[] required = {
            "globe_level_title",
            "globe_room_yard", "globe_room_stage", "globe_room_galleries", "globe_room_tiring_house",
            "globe_objective_reach_stage", "globe_objective_cross_stage", "globe_objective_heckled",
            "globe_objective_climb", "globe_objective_defeat_boss", "globe_objective_complete",
            "speaker_tragedy_king",
            // Reused shared keys the level depends on.
            "checkpoint", "campaign_level_globe", "speaker_player", "speaker_sarah"
        };
        TranslationServer.SetLocale("en");
        foreach (string key in required) {
            AssertThat(keys.Contains(key))
                .OverrideFailureMessage($"'{key}' is missing from localization/en.csv.").IsTrue();
            // The compiled en.en.translation must be in step with the CSV or Node.Tr()
            // renders the raw key. `--headless --quit` does NOT rebuild it; only
            // `--headless --import` does.
            AssertString(TranslationServer.Translate(key).ToString())
                .OverrideFailureMessage(
                    $"'{key}' does not resolve through localization/en.en.translation. " +
                    "Re-import localization/en.csv.")
                .IsNotEqual(key);
        }
    }

    // === Locked encounter economy: 10 standards / 0 elites / 1 boss / 3 extractors ===

    [TestCase]
    public void TheAuthoredSpawnTableIsExactlyTenStandardsAndNoElites() {
        var byTier = new Dictionary<EnemyTier, int>();
        var byID = new Dictionary<string, int>(StringComparer.Ordinal);
        int total = 0;

        foreach ((string enemyID, Vector2 _, Vector2 _, Vector2 _) in Level10Controller.AllStandardSpawns) {
            total++;
            EnemyData data = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertObject(data).OverrideFailureMessage($"Enemy resource missing for '{enemyID}'.").IsNotNull();
            AssertString(data.EnemyID).IsEqual(enemyID);
            byTier[data.Tier] = byTier.GetValueOrDefault(data.Tier) + 1;
            byID[enemyID] = byID.GetValueOrDefault(enemyID) + 1;
        }

        // docs/DUST_ECONOMY.md level 10 row: S=10, E=0.
        AssertThat(total).IsEqual(10);
        AssertThat(Level10Controller.AuthoredStandardCount).IsEqual(10);
        AssertThat(Level10Controller.AuthoredEliteCount).IsEqual(0);
        AssertThat(byTier.GetValueOrDefault(EnemyTier.Standard)).IsEqual(10);
        AssertThat(byTier.GetValueOrDefault(EnemyTier.Elite)).IsEqual(0);

        // Era roster: the London standard mixed with the cultist standard, nothing else.
        AssertThat(byID.Count).IsEqual(2);
        AssertThat(byID["holo_page"]).IsEqual(5);
        AssertThat(byID["chrono_slasher"]).IsEqual(5);

        // Per-wave concurrency must fit level_10_pool_config's standard_enemy warm 12.
        AssertThat(Level10Controller.Room1Spawns.Length).IsEqual(3);
        AssertThat(Level10Controller.Room2Spawns.Length).IsEqual(3);
        AssertThat(Level10Controller.Room3Spawns.Length).IsEqual(4);
    }

    [TestCase]
    public void NoActorIsPostedOverATrapdoorGap() {
        // The stage dropping out is a threat to the player, not a free enemy
        // delete: every authored post stands on boards that stay solid.
        foreach ((string enemyID, Vector2 position, Vector2 patrolA, Vector2 patrolB)
                 in Level10Controller.AllStandardSpawns) {
            AssertThat(Level10Controller.IsOverATrapdoor(position.X))
                .OverrideFailureMessage($"'{enemyID}' at {position} is posted over a trapdoor.").IsFalse();
            AssertThat(Level10Controller.IsOverATrapdoor(patrolA.X))
                .OverrideFailureMessage($"'{enemyID}' patrols onto a trapdoor at {patrolA}.").IsFalse();
            AssertThat(Level10Controller.IsOverATrapdoor(patrolB.X))
                .OverrideFailureMessage($"'{enemyID}' patrols onto a trapdoor at {patrolB}.").IsFalse();
            AssertThat(!patrolA.IsEqualApprox(patrolB))
                .OverrideFailureMessage($"'{enemyID}' at {position} never moves.").IsTrue();
        }
    }

    [TestCase]
    public void ThreeChronalExtractorsArePlacedFromTheAuthoredTable() {
        AssertThat(Level10Controller.ExtractorPlacements.Length).IsEqual(3);
        AssertThat(Level10Controller.AuthoredExtractorCount).IsEqual(3);

        using var fixture = new GlobeFixture();
        AssertThat(fixture.Level.Extractors.Count).IsEqual(3);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ChronalExtractor extractor in fixture.Level.Extractors) {
            AssertThat(extractor.ObjectID.StartsWith("level_10.", StringComparison.Ordinal)).IsTrue();
            AssertThat(ids.Add(extractor.ObjectID))
                .OverrideFailureMessage($"Duplicate extractor id '{extractor.ObjectID}'.").IsTrue();
            // Dust stays resource-owned at the locked 15; level code must not override it.
            AssertThat(extractor.DustReward).IsEqual(3);
        }

        // One of them is under the stage, which is only reachable by taking a fall
        // through a trapdoor - the era mechanic is what guards the side path.
        bool underStage = false;
        foreach ((string _, Vector2 position) in Level10Controller.ExtractorPlacements) {
            if (position.Y > Level10Controller.GroundY) underStage = true;
        }
        AssertThat(underStage)
            .OverrideFailureMessage("No extractor rewards dropping into the under-stage cellar.").IsTrue();
    }

    [TestCase]
    public void TheTragedyKingIsWiredAndItsRangeBandFitsTheTiringHouseArena() {
        var boss = AuthoredResources.Load<BossData>(BossPath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("tragedy_king");
        AssertThat(boss.ChronalDustDrop).IsEqual(25);

        using var fixture = new GlobeFixture();
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertThat(encounter.Data == boss)
            .OverrideFailureMessage("The encounter must reuse the pinned authored BossData instance.").IsTrue();

        // Asserted against the resource, never a hardcoded width: re-tuning the boss
        // then cannot silently outgrow the arena.
        Rect2 arena = fixture.Level.RoomTriggers[3].CameraBounds;
        AssertThat(arena.Size.X > boss.RangedRangeThreshold * PixelsPerUnit)
            .OverrideFailureMessage(
                $"The tiring-house arena is {arena.Size.X} px but the boss ranged band is " +
                $"{boss.RangedRangeThreshold * PixelsPerUnit} px.").IsTrue();
        AssertThat(arena.HasPoint(encounter.Position))
            .OverrideFailureMessage("The boss anchor sits outside the tiring-house camera bounds.").IsTrue();
        AssertThat(Level10Controller.IsOverATrapdoor(encounter.Position.X))
            .OverrideFailureMessage("The boss spawns on a trapdoor.").IsFalse();
    }

    // === Era mechanic 1: the trapdoor stage floor ===

    [TestCase]
    public void AStageBoardShakesBeforeItDropsAndIsNonSolidWhileOpen() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;

        AssertThat(level.StageTrapdoors.Count).IsEqual(3);
        AssertThat(level.ArenaTrapdoors.Count).IsEqual(2);

        TrapdoorPlatform board = level.StageTrapdoors[0];
        var shape = board.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        AssertObject(shape).OverrideFailureMessage("The trapdoor lost its collision child.").IsNotNull();
        AssertThat(board.AutoCycle)
            .OverrideFailureMessage("A stage board that never cycles cannot strand or threaten anyone.").IsTrue();

        board.Close();
        AssertThat(board.State).IsEqual(TrapdoorState.Closed);
        AssertThat(board.IsSolid).IsTrue();
        AssertThat(shape.Disabled).IsFalse();

        // It warns first, and the warning is a readable telegraph, not a frame.
        AssertThat(board.WarningShakeSeconds >= 0.6f)
            .OverrideFailureMessage($"A {board.WarningShakeSeconds}s telegraph is not a warning.").IsTrue();
        board._PhysicsProcess(board.ClosedDurationSeconds + 0.01);
        AssertThat(board.State).IsEqual(TrapdoorState.Warning);
        AssertThat(board.IsSolid)
            .OverrideFailureMessage("The board went non-solid during its own warning.").IsTrue();
        AssertThat(shape.Disabled).IsFalse();

        // ...and only then does the floor actually leave.
        board._PhysicsProcess(board.WarningShakeSeconds + 0.01);
        AssertThat(board.State).IsEqual(TrapdoorState.Open);
        AssertThat(board.IsSolid).IsFalse();
        AssertThat(shape.Disabled)
            .OverrideFailureMessage("An open trapdoor is still solid; nobody can fall through it.").IsTrue();

        // ...and it always comes back, so the cellar is never a soft-lock.
        board._PhysicsProcess(board.OpenDurationSeconds + 0.01);
        AssertThat(board.State).IsEqual(TrapdoorState.Closed);
        AssertThat(board.IsSolid).IsTrue();
        AssertThat(shape.Disabled).IsFalse();
    }

    [TestCase]
    public void EveryTrapdoorSitsFlushInAGapInTheStageBoards() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;

        var boardSpans = new List<(float Start, float End)>();
        foreach (Node child in level.GetChildren()) {
            if (child is not StaticBody2D body) continue;
            if (!body.Name.ToString().StartsWith("Floor_", StringComparison.Ordinal)) continue;
            if (!Mathf.IsEqualApprox(body.Position.Y, Level10Controller.GroundY)) continue;
            if (body.GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is not { Shape: RectangleShape2D rect }) {
                // BuildFloor names its collision child by index; find it by type.
                foreach (Node grandchild in body.GetChildren()) {
                    if (grandchild is CollisionShape2D { Shape: RectangleShape2D found }) {
                        boardSpans.Add((body.Position.X - found.Size.X / 2f, body.Position.X + found.Size.X / 2f));
                        break;
                    }
                }
                continue;
            }
            boardSpans.Add((body.Position.X - rect.Size.X / 2f, body.Position.X + rect.Size.X / 2f));
        }
        AssertThat(boardSpans.Count).IsGreater(4);

        var authoredCentres = new HashSet<float>();
        foreach (float centre in Level10Controller.TrapdoorCentresX) authoredCentres.Add(centre);

        int seen = 0;
        foreach (TrapdoorPlatform door in level.AllTrapdoors) {
            seen++;
            AssertThat(authoredCentres.Contains(door.Position.X))
                .OverrideFailureMessage($"Trapdoor '{door.TrapdoorID}' at x={door.Position.X} is not in TrapdoorCentresX.")
                .IsTrue();
            // Flush with the board surface: the trap IS the floor there.
            AssertFloat(door.Position.Y).IsEqualApprox(Level10Controller.TrapdoorY, 0.001f);

            float start = door.Position.X - Level10Controller.TrapdoorWidth / 2f;
            float end = door.Position.X + Level10Controller.TrapdoorWidth / 2f;
            foreach ((float boardStart, float boardEnd) in boardSpans) {
                AssertThat(boardStart + 0.5f < end && start < boardEnd - 0.5f)
                    .OverrideFailureMessage(
                        $"Solid boards {boardStart}..{boardEnd} overlap trapdoor '{door.TrapdoorID}' " +
                        $"({start}..{end}); the floor would never actually open.")
                    .IsFalse();
            }
        }
        AssertThat(seen).IsEqual(Level10Controller.TrapdoorCentresX.Length);
    }

    [TestCase]
    public void TheBossArenaBoardsAreTelegraphedFarHarderThanTheStageBoards() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;

        float slowestStageWarning = 0f;
        foreach (TrapdoorPlatform door in level.StageTrapdoors) {
            slowestStageWarning = Mathf.Max(slowestStageWarning, door.WarningShakeSeconds);
        }
        foreach (TrapdoorPlatform door in level.ArenaTrapdoors) {
            AssertThat(door.WarningShakeSeconds > slowestStageWarning)
                .OverrideFailureMessage(
                    $"Arena board '{door.TrapdoorID}' warns for {door.WarningShakeSeconds}s, no longer than " +
                    "the stage boards. A boss arena that eats you for standing on the wrong plank is not fair.")
                .IsTrue();
            // ...and it is open for less of its cycle than a stage board.
            AssertThat(door.ClosedDurationSeconds > door.OpenDurationSeconds * 3f).IsTrue();
        }

        // The boards in room 2 are deliberately out of step with each other, so the
        // stage never reads as one metronome.
        var closedDurations = new HashSet<float>();
        foreach (TrapdoorPlatform door in level.StageTrapdoors) {
            AssertThat(closedDurations.Add(door.ClosedDurationSeconds))
                .OverrideFailureMessage("Two stage boards share a cycle length; the stage becomes one timing window.")
                .IsTrue();
        }
    }

    // === Era mechanic 2: the gallery rigging ===

    [TestCase]
    public void TheGlobeRiggingIsTightFastAndStackedRatherThanNassausOpenAirCrossings() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;

        AssertThat(level.Rigging.Count).IsEqual(4);

        var phases = new HashSet<float>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (PendulumAnchor anchor in level.Rigging) {
            AssertThat(ids.Add(anchor.AnchorID))
                .OverrideFailureMessage($"Duplicate rigging id '{anchor.AnchorID}'.").IsTrue();
            AssertThat(anchor.AnchorID.StartsWith("level_10.", StringComparison.Ordinal)).IsTrue();

            // Tight indoor arcs, not a wide open-air sweep.
            AssertThat(anchor.AmplitudeDegrees <= Level10Controller.MaxRiggingAmplitudeDegrees)
                .OverrideFailureMessage(
                    $"'{anchor.AnchorID}' swings {anchor.AmplitudeDegrees} degrees; stage rigging is capped at " +
                    $"{Level10Controller.MaxRiggingAmplitudeDegrees}.")
                .IsTrue();
            // Fast: a Globe swing is over in well under a second and a half.
            AssertThat(anchor.PeriodSeconds <= Level10Controller.MaxRiggingPeriodSeconds)
                .OverrideFailureMessage(
                    $"'{anchor.AnchorID}' takes {anchor.PeriodSeconds}s; that is a Nassau rope, not stage rigging.")
                .IsTrue();
            // Capped hand-off: the rigging lifts a tier, it does not fling you across a bay.
            AssertThat(anchor.MaxLaunchSpeed <= Level10Controller.RiggingMaxLaunchSpeed).IsTrue();
            AssertThat(anchor.ReleaseLaunchAssist < 1f).IsTrue();
            AssertThat(anchor.CarryOccupant).IsTrue();

            // Staggered, so a chain of them has to be timed rather than walked.
            AssertThat(phases.Add(anchor.PhaseOffset))
                .OverrideFailureMessage($"'{anchor.AnchorID}' repeats a phase offset; the chain collapses to one window.")
                .IsTrue();

            // The swing's horizontal reach is short next to its own drop: vertical
            // travel, not distance travel.
            float reach = Level10Controller.RiggingRopeLength * Mathf.Sin(Mathf.DegToRad(anchor.AmplitudeDegrees));
            AssertThat(reach < Level10Controller.RiggingRopeLength * 0.6f).IsTrue();
        }

        // Vertically stacked: each line hangs strictly higher than the last, so the
        // chain ladders up the balcony tiers.
        for (int index = 1; index < level.Rigging.Count; index++) {
            PendulumAnchor previous = level.Rigging[index - 1];
            PendulumAnchor current = level.Rigging[index];
            AssertThat(current.Position.Y < previous.Position.Y)
                .OverrideFailureMessage(
                    $"'{current.AnchorID}' hangs at y={current.Position.Y}, no higher than " +
                    $"'{previous.AnchorID}' at y={previous.Position.Y}; the rigging is not a climb.")
                .IsTrue();
            // ...and the gaps between them are short.
            AssertThat(Mathf.Abs(current.Position.X - previous.Position.X) <= 520f).IsTrue();
        }

        // Every line is in the gallery room, above the balconies it serves.
        Rect2 galleries = level.RoomTriggers[2].CameraBounds;
        foreach (PendulumAnchor anchor in level.Rigging) {
            AssertThat(galleries.HasPoint(anchor.Position))
                .OverrideFailureMessage($"'{anchor.AnchorID}' hangs outside the gallery room.").IsTrue();
        }
    }

    // === Era mechanic 3: the audience ===

    [TestCase]
    public void TheAudienceArmsOnlyAfterTheIdleThresholdAndDisarmsTheMomentThePlayerMoves() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;

        StoryCyclicHazard prop = level.AudienceThrow;
        AssertObject(prop).OverrideFailureMessage("The audience throw hazard is not authored.").IsNotNull();
        AssertString(prop.HazardID).IsEqual("level_10.audience_throw");
        AssertThat(prop.Enabled)
            .OverrideFailureMessage("The prop must not run its own cycle; an idle punish is not a metronome.")
            .IsFalse();

        PlayerController player = level.Player;
        player.RestoreStoryCheckpoint(new Vector2(900f, Level10Controller.EnemyGroundY), player.MaximumHP, 0f);
        level.ResetAudienceWatch();

        // Nothing happens before the threshold.
        int belowThreshold = Mathf.FloorToInt(Level10Controller.AudienceIdleArmSeconds / Step) - 4;
        for (int tick = 0; tick < belowThreshold; tick++) level.TickAudience(Step);
        AssertThat(level.AudienceArmed)
            .OverrideFailureMessage(
                $"The galleries armed after {level.AudienceIdleSeconds}s, before the authored " +
                $"{Level10Controller.AudienceIdleArmSeconds}s threshold.")
            .IsFalse();
        AssertThat(prop.Phase).IsEqual(HazardPhase.Cooldown);
        AssertThat(level.AudienceIdleSeconds > Level10Controller.AudienceIdleArmSeconds - 0.2f).IsTrue();

        // A single step of real movement wipes the idle clock.
        player.GlobalPosition += new Vector2(200f, 0f);
        level.TickAudience(Step);
        AssertFloat(level.AudienceIdleSeconds).IsEqual(0f);
        AssertThat(level.AudienceArmed).IsFalse();

        // Stand still again and the crowd winds up - telegraphed, never instant.
        int armedAfter = TickUntilArmed(level);
        AssertThat(armedAfter).OverrideFailureMessage("The galleries never armed.").IsGreater(0);
        AssertThat(armedAfter >= Mathf.FloorToInt(Level10Controller.AudienceIdleArmSeconds / Step))
            .OverrideFailureMessage($"Armed after only {armedAfter} frames of idling.").IsTrue();
        AssertThat(prop.Phase).IsEqual(HazardPhase.Warning);
        AssertFloat(prop.GlobalPosition.X).IsEqualApprox(player.GlobalPosition.X, 1f);
        AssertThat(level.AudienceThrowsLanded).IsEqual(0);

        // Walking out of the marked spot cancels the throw outright.
        int hpBefore = player.CurrentHP;
        player.GlobalPosition += new Vector2(Level10Controller.AudienceEscapeRadius + 20f, 0f);
        level.TickAudience(Step);
        AssertThat(level.AudienceArmed)
            .OverrideFailureMessage("Moving away did not disarm the throw; this is a cheap shot.").IsFalse();
        AssertThat(prop.Phase).IsEqual(HazardPhase.Cooldown);
        AssertThat(level.AudienceThrowsLanded).IsEqual(0);
        AssertThat(player.CurrentHP).IsEqual(hpBefore);
    }

    [TestCase]
    public void StandingThroughTheWholeTelegraphIsWhatActuallyGetsHit() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;
        PlayerController player = level.Player;

        player.RestoreStoryCheckpoint(new Vector2(1400f, Level10Controller.EnemyGroundY), player.MaximumHP, 0f);
        player.Velocity = Vector2.Zero;
        level.ResetAudienceWatch();

        AssertThat(TickUntilArmed(level)).IsGreater(0);
        AssertThat(level.AudienceArmed).IsTrue();
        int hpBefore = player.CurrentHP;

        // The prop is in the air for the whole authored telegraph and hits nobody.
        int duringTelegraph = Mathf.FloorToInt((Level10Controller.AudienceWarningSeconds - 0.1f) / Step);
        for (int tick = 0; tick < duringTelegraph; tick++) level.TickAudience(Step);
        AssertThat(level.AudienceThrowsLanded)
            .OverrideFailureMessage("The prop landed during its own telegraph.").IsEqual(0);
        AssertThat(player.CurrentHP).IsEqual(hpBefore);
        AssertThat(level.AudienceArmed).IsTrue();

        // ...and then it lands.
        for (int tick = 0; tick < 12; tick++) level.TickAudience(Step);
        AssertThat(level.AudienceThrowsLanded).IsEqual(1);
        AssertThat(player.CurrentHP).IsLess(hpBefore);
        // A heckle is a nudge, not a kill.
        AssertThat(player.CurrentHP).IsGreater(0);
        AssertThat(player.CurrentState != CharacterState.Dead).IsTrue();
        AssertThat(level.AudienceThrow.Damage < player.MaximumHP).IsTrue();

        // The watch re-arms from scratch rather than shredding a pinned player: a
        // second prop costs another full idle threshold, not another telegraph.
        AssertThat(level.AudienceArmed).IsFalse();
        AssertThat(level.AudienceIdleSeconds < Level10Controller.AudienceIdleArmSeconds).IsTrue();
        AssertThat(level.AudienceThrowsLanded).IsEqual(1);
    }

    // === Resume safety ===

    [TestCase]
    public void NoCheckpointResumesOverAnOpenTrapdoorOrIntoAnArmedAudience() {
        // Every registered respawn stands on boards that are not a trapdoor.
        using (var fixture = new GlobeFixture()) {
            for (int index = 0; index < 3; index++) {
                string id = $"{LevelID}_checkpoint_{index}";
                AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 respawn)).IsTrue();
                AssertThat(Level10Controller.IsOverATrapdoor(respawn.X))
                    .OverrideFailureMessage($"Checkpoint '{id}' respawns at x={respawn.X}, over a trapdoor.")
                    .IsFalse();
            }
        }

        // ...and a resumed run finds shut boards and a quiet house.
        foreach (string checkpointID in new[] { $"{LevelID}_checkpoint_1", $"{LevelID}_checkpoint_2" }) {
            using var fixture = new GlobeFixture(checkpointID);
            Level10Controller level = fixture.Level;

            AssertThat(level.ResumedMidLevel).IsTrue();
            AssertString(level.ResumedCheckpointID).IsEqual(checkpointID);

            foreach (TrapdoorPlatform door in level.AllTrapdoors) {
                AssertThat(door.State)
                    .OverrideFailureMessage($"Resuming at '{checkpointID}' left '{door.TrapdoorID}' mid-cycle.")
                    .IsEqual(TrapdoorState.Closed);
                AssertThat(door.IsSolid).IsTrue();
            }

            AssertThat(level.AudienceArmed)
                .OverrideFailureMessage($"Resuming at '{checkpointID}' loaded into an armed heckle.").IsFalse();
            AssertFloat(level.AudienceIdleSeconds).IsEqual(0f);
            AssertThat(level.AudienceThrow.Phase).IsEqual(HazardPhase.Cooldown);
        }
    }

    // === Helpers ===

    /// <summary>Steps the idle watch one frame at a time until the galleries arm.</summary>
    private static int TickUntilArmed(Level10Controller level) {
        for (int tick = 0; tick < 600; tick++) {
            level.TickAudience(Step);
            if (level.AudienceArmed) return tick + 1;
        }
        return -1;
    }

    private static HashSet<string> EnglishKeys() {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int rows = 0;
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma <= 0) continue;
            keys.Add(line[..comma]);
            rows++;
        }
        AssertThat(rows).OverrideFailureMessage(
            $"Only {rows} rows were read from localization/en.csv.").IsGreater(500);
        return keys;
    }

    /// <summary>
    /// Builds the real Globe scene in the runner tree against a scratch save, then
    /// hands every shared singleton back on dispose.
    /// <para>
    /// The fixture always resumes at a checkpoint. That is deliberate: a fresh entry
    /// defers the entrance dialogue, which is authored <c>PausesGameplay = true</c>,
    /// and a leaked <c>SceneTree.Paused</c> stops GdUnit4's transport node and hangs
    /// the whole session (docs/PACKAGE5_CAMPAIGN_PLAN.md §9). The pause flag is
    /// restored anyway.
    /// </para>
    /// </summary>
    private sealed class GlobeFixture : IDisposable {
        public readonly Level10Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public GlobeFixture() : this($"{LevelID}_checkpoint_0") { }

        public GlobeFixture(string resumeCheckpointID) : this(NewSave(resumeCheckpointID)) { }

        public GlobeFixture(StorySaveData save) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            PackedScene packed = ResourceLoader.Load<PackedScene>(ScenePath);
            Level = packed.Instantiate<Level10Controller>();
            Level.Name = "Level10GlobeFixture";
            tree.Root.AddChild(Level);
        }

        public static StorySaveData NewSave(string checkpointID) => new() {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.London),
            LastCheckpointID = checkpointID,
            CurrentHP = 100,
            CurrentUltimateMeter = 50f
        };

        public void Dispose() {
            // Belt and braces: StoryLevelControllerBase._ExitTree already hands the
            // level's pooled objects back through PoolManager.ReleaseActiveUnder.
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
