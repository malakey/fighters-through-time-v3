using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        // Package 13 W4 (S35): the backstage absence joins the three base beats; the
        // N03 Shakespeare variants are the absence-slot recognition and the exit.
        string[] expectedSequences = {
            "level_10.entrance", "level_10.absence", "level_10.boss_intro", "level_10.exit",
            "level_10.absence@shakespeare", "level_10.exit@shakespeare" };
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

    // === The space under the boards (VERIFY-L10-ARENA-RECESS-EXIT) ===

    /// <summary>
    /// Nothing that goes below the boards can leave sideways into the void
    /// (2026-10-09). The arena recess used to end at its floor's edges, 100 px
    /// under the boards with nothing beyond, and three bot runs walked or were
    /// carried out under a board and fell out of the world.
    ///
    /// <para>From under every trapdoor and every board-height one-way (the room 2
    /// stair head and the arena island), a hero-sized box is flooded through every
    /// position it fits in below the boards' underside, moving freely in all four
    /// directions. That over-approximates walking, rolling, jumping, air drift and
    /// being carried by an ability. Solid level geometry blocks the box; one-way
    /// platforms and trapdoors do not. The box is the hero's body at its smallest
    /// (40 wide, crouched to 70 % of 64), so a gap a crouch slide could squeeze
    /// through counts as open. The flood must never reach the bottom of the level
    /// or its sides, and the space under the arena traps must be the authored
    /// recess, wall to wall.</para>
    /// </summary>
    [TestCase]
    public void NothingBelowTheBoardsOpensSidewaysOntoTheVoid() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;

        var seeds = new List<(string Label, float X)>();
        foreach (TrapdoorPlatform door in level.AllTrapdoors) seeds.Add((door.TrapdoorID, door.Position.X));
        foreach (Node child in level.GetChildren()) {
            if (child is OneWayPlatform oneWay && Mathf.IsEqualApprox(oneWay.Position.Y, Level10Controller.GroundY)) {
                seeds.Add((oneWay.Name.ToString(), oneWay.Position.X));
            }
        }
        // Five trapdoors, the room 2 stair head and the arena island.
        AssertThat(seeds.Count).IsEqual(Level10Controller.TrapdoorCentresX.Length + 2);

        var flood = new UnderBoardFlood(SolidLevelRects(level), HeroWidth,
            HeroHeight * FTT.Combat.StoryCombatRules.CrouchHurtboxScale);
        var issues = new List<string>();
        foreach ((string label, float x) in seeds) {
            UnderBoardFlood.Region region = flood.From(x);
            if (region == null) {
                issues.Add($"no room for a body under '{label}' (x {x})");
            } else if (region.Escape is Vector2 at) {
                issues.Add($"from under '{label}' (x {x}) a body reaches {at}, out of the level " +
                    $"(it spreads over x {region.MinX:0}..{region.MaxX:0} under the boards)");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

        // Both arena traps open onto the one recess, and it is exactly the authored one.
        AssertThat(level.ArenaTrapdoors.Count).IsEqual(2);
        foreach (TrapdoorPlatform door in level.ArenaTrapdoors) {
            UnderBoardFlood.Region region = flood.From(door.Position.X);
            AssertFloat(region.MinX).IsEqualApprox(Level10Controller.ArenaRecessWestX, 0.01f);
            AssertFloat(region.MaxX).IsEqualApprox(Level10Controller.ArenaRecessEastX, 0.01f);
            AssertFloat(region.MaxY).IsEqualApprox(Level10Controller.ArenaRecessFloorY, FloodStep);
        }
    }

    /// <summary>
    /// The same closure in real physics frames: a Story hero set down in the arena
    /// recess and driven east, then west, at a full run with a roll every half
    /// second, reaches both walls and never leaves the recess or drops below its
    /// floor. Against the open recess the hero ran off the east end under the
    /// board and fell out of the world.
    /// </summary>
    [TestCase]
    public async Task AHeroDrivenHardBothWaysInTheArenaRecessStaysInIt() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GlobeFixture();
        // The resume has been read. This case runs real frames, so go slotless:
        // no per-second snapshot or checkpoint write may reach a real slot file.
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level10Controller level = fixture.Level;
        // The recess lies inside the King's arena, whose reveal starts a dialogue
        // that pauses the tree; this case is about the walls, not the boss.
        AssertObject(level.KingEncounter).IsNotNull();
        level.KingEncounter.ProcessMode = Node.ProcessModeEnum.Disabled;
        level.AudienceWatchEnabled = false;

        PlayerController hero = level.Player;
        var input = new BufferedInputSource();
        InputManager.Instance.SetInputSource(hero.PlayerIndex, input);
        try {
            hero.GlobalPosition = new Vector2(9320f, Level10Controller.ArenaRecessFloorY - 1f);
            hero.Velocity = Vector2.Zero;
            float westmost = Level10Controller.ArenaRecessWestX + HeroWidth / 2f;
            float eastmost = Level10Controller.ArenaRecessEastX - HeroWidth / 2f;
            float minX = hero.GlobalPosition.X;
            float maxX = hero.GlobalPosition.X;
            float lowestFeet = hero.GlobalPosition.Y;
            bool paused = false;
            uint tick = 0;
            foreach ((float direction, int frames) in new[] { (1f, 100), (-1f, 180) }) {
                for (int frame = 0; frame < frames && !paused; frame++) {
                    GameplayButtons held = frame % 30 == 10 ? GameplayButtons.Roll : GameplayButtons.None;
                    input.SetNextFrame(PlayerInputFrame.Create(tick++, direction, 0f, held));
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                    paused = tree.Paused;
                    Vector2 at = hero.GlobalPosition;
                    minX = Mathf.Min(minX, at.X);
                    maxX = Mathf.Max(maxX, at.X);
                    lowestFeet = Mathf.Max(lowestFeet, at.Y);
                }
            }
            AssertThat(paused).OverrideFailureMessage("Something paused the tree mid-drive.").IsFalse();
            AssertThat(maxX <= eastmost + 0.5f && minX >= westmost - 0.5f)
                .OverrideFailureMessage(
                    $"The hero left the recess sideways: its centre ranged over x {minX:0.0}..{maxX:0.0}, " +
                    $"and the recess holds it within {westmost}..{eastmost}.")
                .IsTrue();
            AssertThat(lowestFeet <= Level10Controller.ArenaRecessFloorY + 0.5f)
                .OverrideFailureMessage($"The hero's feet dropped to y {lowestFeet:0.0}, below the recess floor.")
                .IsTrue();
            // ...and it really was driven into both walls.
            AssertThat(maxX >= eastmost - 2f && minX <= westmost + 2f)
                .OverrideFailureMessage(
                    $"The hero only ranged over x {minX:0.0}..{maxX:0.0}; it never reached both walls.")
                .IsTrue();
        } finally {
            InputManager.Instance.ClearInputSource(hero.PlayerIndex);
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

    /// <summary>
    /// The climb is the route (2026-10-08). The yard wall used to stop at y 700:
    /// 420 px above the yard, with 72 px under the bridging span, room for the
    /// 64 px hero to slip across its top, and the apex hang put that inside
    /// Cleopatra's double jump. The wall now rises to the span, so no gap a hero
    /// fits through is left between them, and the span itself stands beyond every
    /// hero's whole jump budget from the yard (the <see cref="StoryAirPhysics"/>
    /// integrated arc, with 10% to spare). Movement abilities are out of scope
    /// here, as they were for the original wall.
    /// </summary>
    [TestCase]
    public void TheYardWallLeavesNoGapUnderTheBridgeAndTheBridgeIsBeyondEveryJump() {
        const float heroHeight = 64f;
        const float jumpMargin = 1.1f;
        using var fixture = new GlobeFixture();
        Rect2 wall = BodyRect(fixture.Level,
            $"Wall_{Mathf.RoundToInt(Level10Controller.YardWallX)}_{Mathf.RoundToInt(Level10Controller.YardWallTopY)}");
        Rect2 bridge = BodyRect(fixture.Level,
            $"Platform_{Mathf.RoundToInt(Level10Controller.GalleryBridgeX)}_{Mathf.RoundToInt(Level10Controller.GalleryBridgeY)}");
        AssertThat(wall.Size.X > 0f && bridge.Size.X > 0f)
            .OverrideFailureMessage("The yard wall or the bridging span is missing.").IsTrue();

        // The wall stands under the span, from the yard floor up.
        AssertThat(wall.Position.X >= bridge.Position.X && wall.End.X <= bridge.End.X)
            .OverrideFailureMessage($"The yard wall {wall} does not stand under the span {bridge}.").IsTrue();
        AssertThat(wall.End.Y >= Level10Controller.GroundY).IsTrue();
        float gap = wall.Position.Y - bridge.End.Y;
        AssertThat(gap < heroHeight)
            .OverrideFailureMessage($"{gap} px between the span and the yard wall lets a {heroHeight} px hero across.")
            .IsTrue();

        float best = 0f;
        string bestID = "";
        foreach (string id in CharacterRoster.IDs) {
            var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            float rise = StoryAirPhysics.MultiJumpRise(StoryAirPhysics.JumpLaunchSpeed(data.MaxJumpForce), data.Weight,
                Math.Max(1, data.MaxJumpCount));
            if (rise <= best) continue;
            best = rise;
            bestID = id;
        }
        float bridgeRise = Level10Controller.GroundY - bridge.Position.Y;
        AssertThat(bridgeRise > best * jumpMargin)
            .OverrideFailureMessage(
                $"The span stands {bridgeRise} px over the yard; '{bestID}' reaches {best:0} px, so the climb can be jumped.")
            .IsTrue();
    }

    /// <summary>The world rect of a level body's first (rectangle) collision shape; empty when missing.</summary>
    private static Rect2 BodyRect(Node level, string bodyName) {
        var body = level.GetNodeOrNull<StaticBody2D>(bodyName);
        if (body == null || body.GetChildCount() == 0) return new Rect2();
        if (body.GetChild(0) is not CollisionShape2D { Shape: RectangleShape2D rect } shape) return new Rect2();
        Vector2 centre = body.Position + shape.Position;
        return new Rect2(centre - rect.Size / 2f, rect.Size);
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

    /// <summary>
    /// Package 12 W1 (GAP-11): the galleries are part of the world. A Time
    /// Freeze (and the R03 Post-Landing Hold, which holds the world the same
    /// way) stops them: no idle build-up, no telegraph countdown, no throw, and
    /// nothing catches up at the thaw.
    /// </summary>
    [TestCase]
    public void TheAudienceNeverArmsOrThrowsWhileTheWorldIsFrozen() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;
        PlayerController player = level.Player;
        player.RestoreStoryCheckpoint(new Vector2(1400f, Level10Controller.EnemyGroundY), player.MaximumHP, 0f);
        player.Velocity = Vector2.Zero;
        level.ResetAudienceWatch();
        try {
            // Standing still through a whole freeze's worth of time arms nothing.
            player.TimeFrozen = true;
            int wellPastThreshold = Mathf.CeilToInt(Level10Controller.AudienceIdleArmSeconds / Step) * 2;
            for (int tick = 0; tick < wellPastThreshold; tick++) level.TickAudience(Step);
            AssertThat(level.AudienceArmed)
                .OverrideFailureMessage("The galleries armed during a Time Freeze.").IsFalse();
            AssertFloat(level.AudienceIdleSeconds).IsEqual(0f);

            // An armed telegraph started before the freeze holds; it neither
            // lands during the freeze nor catches up at the thaw.
            player.TimeFrozen = false;
            AssertThat(TickUntilArmed(level)).IsGreater(0);
            int hpBefore = player.CurrentHP;
            player.TimeFrozen = true;
            int pastTelegraph = Mathf.CeilToInt(Level10Controller.AudienceWarningSeconds / Step) * 3;
            for (int tick = 0; tick < pastTelegraph; tick++) level.TickAudience(Step);
            AssertThat(level.AudienceThrowsLanded)
                .OverrideFailureMessage("The throw landed while the world was frozen.").IsEqual(0);
            AssertThat(level.AudienceArmed).IsTrue();
            player.TimeFrozen = false;
            level.TickAudience(Step);
            AssertThat(level.AudienceThrowsLanded)
                .OverrideFailureMessage("The thaw replayed the frozen telegraph as catch-up.").IsEqual(0);
            AssertThat(player.CurrentHP).IsEqual(hpBefore);
        } finally {
            player.TimeFrozen = false;
        }
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

    /// <summary>
    /// GU-L8 (2026-10-12): turn-taking makes standing still normal inside an
    /// encounter lock (a waiter rotating in, a blocked swing), so the audience's
    /// stillness clock pauses while one is engaged — it neither builds nor resets —
    /// and resumes once the lock clears.
    /// </summary>
    [TestCase]
    public void TheAudienceStillnessClockPausesWhileALockIsEngaged() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;
        PlayerController player = level.Player;
        EncounterLock yard = level.YardLock;
        try {
            player.RestoreStoryCheckpoint(new Vector2(1400f, Level10Controller.EnemyGroundY), player.MaximumHP, 0f);
            player.Velocity = Vector2.Zero;
            level.ResetAudienceWatch();

            // Some stillness builds before the lock engages.
            int partial = Mathf.FloorToInt(Level10Controller.AudienceIdleArmSeconds / Step / 2f);
            for (int tick = 0; tick < partial; tick++) level.TickAudience(Step);
            float before = level.AudienceIdleSeconds;
            AssertThat(before).IsGreater(0f);

            yard.Tick(Step);
            AssertThat(yard.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(level.AudienceHeldByEncounterLock).IsTrue();
            int wellPastThreshold = Mathf.CeilToInt(Level10Controller.AudienceIdleArmSeconds / Step) * 2;
            for (int tick = 0; tick < wellPastThreshold; tick++) level.TickAudience(Step);
            AssertThat(level.AudienceArmed)
                .OverrideFailureMessage("The galleries armed on a hero standing still inside an engaged lock.").IsFalse();
            AssertFloat(level.AudienceIdleSeconds).IsEqualApprox(before, 0.0001f);

            // Cleared: the clock picks up where it stood.
            foreach (EnemyController member in yard.LivingMembers) member.TakeDamage(99999);
            yard.Tick(Step);
            AssertThat(yard.State).IsEqual(EncounterLockState.Cleared);
            AssertThat(level.AudienceHeldByEncounterLock).IsFalse();
            level.TickAudience(Step);
            AssertThat(level.AudienceIdleSeconds).IsGreater(before);
            AssertThat(TickUntilArmed(level)).IsGreater(0);
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    // === GU-L8 (2026-10-12): the three encounter locks ===

    [TestCase]
    public void TheThreeFightsAreEncounterLocksOneWavePerManifestRow() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;
        try {
            AssertThat(level.EncounterLocks.Count).IsEqual(3);
            EncounterLock yard = level.YardLock;
            EncounterLock stage = level.StageLock;
            EncounterLock galleries = level.GalleriesLock;
            AssertString(yard.LockID).IsEqual(Level10Controller.LockYard);
            AssertString(stage.LockID).IsEqual(Level10Controller.LockStage);
            AssertString(galleries.LockID).IsEqual(Level10Controller.LockGalleries);

            // The areas: the yard 300-2540, the stage with its cellar 2560-5740, the
            // galleries 6080-8330 (to the bridging span's east end).
            AssertThat(yard.Area.Position.X).IsEqual(300f);
            AssertThat(yard.Area.End.X).IsEqual(2540f);
            AssertThat(stage.Area.Position.X).IsEqual(2560f);
            AssertThat(stage.Area.End.X).IsEqual(5740f);
            AssertThat(stage.Area.End.Y).IsEqual(Level10Controller.LevelHeight);
            AssertThat(galleries.Area.Position.X).IsEqual(6080f);
            AssertThat(galleries.Area.End.X)
                .IsEqual(Level10Controller.GalleryBridgeX + Level10Controller.GalleryBridgeWidth / 2f);

            // The stage's exit barrier stands on the east cellar wall and runs down to
            // its top, so the tiring-house stair out of the cellar is inside the lock.
            AssertThat(stage.ExitBarrier.Footprint.Position.X).IsEqual(5740f);
            AssertThat(stage.ExitBarrier.BottomY).IsEqual(1180f);
            // The galleries' exit is the bridge into the tiring-house: the barrier stands
            // on the span's east end, down to the boards.
            AssertThat(galleries.ExitBarrier.Footprint.Position.X).IsEqual(galleries.Area.End.X);
            AssertThat(galleries.ExitBarrier.BottomY).IsEqual(Level10Controller.GroundY);
            AssertThat(galleries.ExitBarrier.Footprint.Position.X).IsGreater(Level10Controller.YardWallX);
            // Lock barriers are not gates: no gate column (ArenaGeometryContentTests).
            AssertThat(level.GateColumns.Count).IsEqual(0);

            (EncounterLock Lock, string Row, int Length, EncounterWaveTrigger Trigger)[] waves = {
                (yard, Level10Controller.Room1RowID, Level10Controller.Room1Spawns.Length, EncounterWaveTrigger.OnBuild),
                (stage, Level10Controller.Room2RowID, Level10Controller.Room2Spawns.Length, EncounterWaveTrigger.OnEngage),
                (galleries, Level10Controller.Room3RowID, Level10Controller.Room3Spawns.Length, EncounterWaveTrigger.OnBuild)
            };
            foreach ((EncounterLock encounterLock, string row, int length, EncounterWaveTrigger trigger) in waves) {
                AssertThat(encounterLock.Spec.Waves.Length).IsEqual(1);
                AssertString(encounterLock.Spec.Waves[0].RowID).IsEqual(row);
                AssertThat(encounterLock.Spec.Waves[0].Spawns.Length).IsEqual(length);
                AssertThat(encounterLock.Spec.Waves[0].Trigger).IsEqual(trigger);
                AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);
                AssertThat(encounterLock.ExitBarrier.IsClosed).IsTrue();
            }
            AssertThat(stage.LivingMemberCount).IsEqual(0);

            // Every galleries actor stands on the yard floor or the lower tier (the
            // middle-tier prompt-boy and the upper-tier slasher moved down), inside
            // the lock's area.
            foreach ((string enemyID, Vector2 position, Vector2 _, Vector2 _) in Level10Controller.Room3Spawns) {
                bool floor = Mathf.IsEqualApprox(position.Y, Level10Controller.EnemyGroundY);
                bool lowerTier = Mathf.IsEqualApprox(position.Y, 830f);
                AssertThat(floor || lowerTier).OverrideFailureMessage($"{enemyID} at {position} is posted above the lower tier.").IsTrue();
                AssertThat(position.X > galleries.Area.Position.X && position.X < galleries.Area.End.X).IsTrue();
            }

            IReadOnlyDictionary<string, string[]> baselines = level.AuthoredEncounterBaselines;
            AssertThat(baselines[Level10Controller.Checkpoint0].Length).IsEqual(0);
            AssertThat(string.Join(",", baselines[Level10Controller.Checkpoint1]))
                .IsEqual($"{Level10Controller.LockYard},{Level10Controller.LockStage}");
            AssertThat(string.Join(",", baselines[Level10Controller.Checkpoint2]))
                .IsEqual($"{Level10Controller.LockYard},{Level10Controller.LockStage},{Level10Controller.LockGalleries}");
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    [TestCase]
    public void TheMiddleAndPreBossAnchorsStandOutsideEveryLockBeyondSwingReach() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;
        try {
            var middle = level.GetNode<CheckpointTrigger>($"Checkpoint_{Level10Controller.Checkpoint1}");
            var preBoss = level.GetNode<CheckpointTrigger>($"Checkpoint_{Level10Controller.Checkpoint2}");
            AssertThat(middle.GlobalPosition.X).IsEqual(Level10Controller.MiddleCheckpointX);
            AssertThat(preBoss.GlobalPosition.X).IsEqual(Level10Controller.PreBossCheckpointX);
            foreach (CheckpointTrigger anchor in new[] { middle, preBoss }) {
                foreach (EncounterLock encounterLock in level.EncounterLocks) {
                    bool inside = anchor.GlobalPosition.X + 30f > encounterLock.Area.Position.X
                        && anchor.GlobalPosition.X - 30f < encounterLock.Area.End.X;
                    AssertThat(inside).OverrideFailureMessage($"{anchor.CheckpointID} stands inside '{encounterLock.LockID}'.").IsFalse();
                }
            }
            // The Middle's respawn body clears the ground-level approach step (its
            // underside 1008, x 5800-6000) and sits under the lower tier.
            AssertThat(middle.GlobalPosition.X - 20f).IsGreaterEqual(6000f);

            float fromStage = LevelLockSwingReach.EastwardReachFrom(level.StageLock);
            AssertThat(fromStage < LevelLockSwingReach.StrikeSurfaceWestEdge(middle.GlobalPosition.X))
                .OverrideFailureMessage($"A swing from the stage reaches x {fromStage:0}.").IsTrue();
            // Inside the galleries a hero on the yard floor is stopped by the yard wall
            // (west face 8280) before the lock's exit barrier on the bridge; the swing
            // from there is the reach the PreBoss anchor clears.
            float fromYardWall = Level10Controller.YardWallX - LevelLockSwingReach.HeroHalfWidthPixels
                + LevelLockSwingReach.LongestBasicSwingPixels();
            AssertThat(fromYardWall < LevelLockSwingReach.StrikeSurfaceWestEdge(preBoss.GlobalPosition.X))
                .OverrideFailureMessage($"A swing from the galleries' floor reaches x {fromYardWall:0}.").IsTrue();
            AssertThat(middle.IsHeldByEncounterLock).IsTrue();
            AssertThat(preBoss.IsHeldByEncounterLock).IsTrue();
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    // === Resume safety ===

    /// <summary>
    /// FIX-LEVEL (2026-10-13, Phase B review item 8): GU-L8 moved the Middle
    /// anchor into the galleries (6040), east of their room trigger (5750–5830),
    /// which a resume there never crosses, so the objective read "cross the stage"
    /// in the galleries — and again when the galleries lock cleared, because the
    /// lock's release recomputes the level's own objective. A Middle resume now
    /// counts the galleries as reached. In real frames on the real scene.
    /// </summary>
    [TestCase]
    public async Task AMiddleResumeReadsTheClimbObjectiveInTheGalleriesAndAfterTheirClear() {
        using var fixture = new GlobeFixture($"{LevelID}_checkpoint_1");
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level10Controller level = fixture.Level;
        level.AudienceWatchEnabled = false;
        Label objective = level.HUD.GetNode<Label>("SafeArea/TopLeft/Objective");
        string climb = TranslationServer.Translate("globe_objective_climb").ToString();
        try {
            AssertThat(level.ResumedMidLevel).IsTrue();
            AssertThat(objective.Text)
                .OverrideFailureMessage($"A Middle resume in the galleries reads '{objective.Text}'.").IsEqual(climb);

            EncounterLock galleries = level.GalleriesLock;
            AssertThat(galleries.State).IsEqual(EncounterLockState.Armed);
            LevelLockTestKit.PlaceHero(level.Player, Level10Controller.GalleriesLockMinX + 120f, Level10Controller.GroundY - 1f);
            await LevelLockTestKit.Frames(4);
            AssertThat(galleries.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(objective.Text).IsEqual(TranslationServer.Translate("encounter_lock_objective_clear").ToString());

            AssertThat(await LevelLockTestKit.ClearByForce(galleries)).IsTrue();
            AssertThat(objective.Text)
                .OverrideFailureMessage($"The galleries' clear handed back '{objective.Text}'.").IsEqual(climb);
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

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

    /// <summary>
    /// Package 12 W9 (GAP-07): the Tragedy King's actors "perform scripted attack
    /// scenes while the stage trapdoors cycle". The arena boards hold through the
    /// King's first act and begin their slow cycle the moment Phase 2 starts.
    /// </summary>
    [TestCase]
    public void TheArenaTrapdoorsHoldUntilTheKingsSecondActThenCycle() {
        using var fixture = new GlobeFixture();
        Level10Controller level = fixture.Level;
        FTT.Enemies.BossEncounterController encounter = level.KingEncounter;
        AssertObject(encounter).IsNotNull();
        AssertThat(level.ArenaTrapdoorsCycling).IsFalse();
        foreach (TrapdoorPlatform door in level.ArenaTrapdoors) {
            AssertThat(door.Enabled).IsFalse();
            AssertThat(door.IsSolid).IsTrue();
        }
        // The stage boards in room two are untouched by the boss rule.
        foreach (TrapdoorPlatform door in level.StageTrapdoors) AssertThat(door.Enabled).IsTrue();

        FTT.Enemies.BossController king = encounter.Boss;
        king.TakeDamage(king.ScaledMaxHP / 2 + 1);
        AssertThat(king.CurrentPhase).IsEqual(1);
        AssertThat(level.ArenaTrapdoorsCycling).IsTrue();
        foreach (TrapdoorPlatform door in level.ArenaTrapdoors) {
            AssertThat(door.Enabled).IsTrue();
            AssertThat(door.AutoCycle).IsTrue();
        }
    }

    // === Helpers ===

    /// <summary>The Story hero's body (CharacterFactory: 40 x 64).</summary>
    private const float HeroWidth = 40f;
    private const float HeroHeight = 64f;

    /// <summary>
    /// The under-board flood's grid, in pixels. Every authored coordinate below
    /// the boards is even, so a 2 px grid sets the box flush against every face.
    /// </summary>
    private const float FloodStep = 2f;

    /// <summary>
    /// Every solid rectangle of the level's static geometry in world space: the
    /// Environment-layer static bodies (floors, platforms, walls). One-way
    /// platforms, trapdoors and every other moving body are left out, so the
    /// flood treats them as open.
    /// </summary>
    private static List<Rect2> SolidLevelRects(Node level) {
        var rects = new List<Rect2>();
        var stack = new Stack<Node>();
        stack.Push(level);
        while (stack.Count > 0) {
            Node node = stack.Pop();
            Godot.Collections.Array<Node> children = node.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) stack.Push(child);
            if (node is not StaticBody2D body || body is AnimatableBody2D || body is OneWayPlatform) continue;
            if ((body.CollisionLayer & CollisionLayers.Environment) == 0) continue;
            foreach (Node child in children) {
                if (child is not CollisionShape2D { Shape: RectangleShape2D rect } shape || shape.Disabled) continue;
                Transform2D transform = shape.GlobalTransform;
                Vector2 half = rect.Size / 2f;
                Vector2 a = transform * new Vector2(-half.X, -half.Y);
                Vector2 b = transform * new Vector2(half.X, half.Y);
                rects.Add(new Rect2(new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y)),
                    (b - a).Abs()));
            }
        }
        return rects;
    }

    /// <summary>
    /// A hero-sized box flooded on a <see cref="FloodStep"/> grid through every
    /// position below <see cref="Level10Controller.BoardUndersideY"/> where it
    /// overlaps no solid, stepping in all four directions. The grid runs one
    /// column past each side of the level and below its bottom, so a box that
    /// gets there has left the level. Each connected region is flooded once and
    /// shared by every seed inside it.
    /// </summary>
    private sealed class UnderBoardFlood {
        public sealed class Region {
            /// <summary>The westmost left edge and eastmost right edge the box reached.</summary>
            public float MinX = float.MaxValue;
            public float MaxX = float.MinValue;
            /// <summary>The lowest bottom edge the box reached inside the level.</summary>
            public float MaxY = float.MinValue;
            /// <summary>The centre of the nearest box position outside the level, or null.</summary>
            public Vector2? Escape;
        }

        private const float Epsilon = 0.01f;
        private readonly List<Rect2> _solids = new();
        private readonly float _width;
        private readonly float _height;
        private readonly float _originX;
        private readonly float _originY = Level10Controller.BoardUndersideY;
        private readonly int _columns;
        private readonly int _rows;
        private readonly int[] _labels;
        private readonly List<Region> _regions = new() { null };

        public UnderBoardFlood(List<Rect2> solids, float width, float height) {
            _width = width;
            _height = height;
            foreach (Rect2 solid in solids) {
                if (solid.End.Y > _originY + Epsilon) _solids.Add(solid);
            }
            _originX = -width - FloodStep;
            _columns = Mathf.CeilToInt((Level10Controller.LevelWidth + FloodStep - _originX) / FloodStep) + 1;
            _rows = Mathf.CeilToInt((Level10Controller.LevelHeight - _originY) / FloodStep) + 1;
            _labels = new int[_columns * _rows];
        }

        /// <summary>
        /// The region a body that goes below the boards at <paramref name="centreX"/>
        /// can reach, or null when no body fits right under the boards there.
        /// </summary>
        public Region From(float centreX) {
            int start = Mathf.RoundToInt((centreX - _width / 2f - _originX) / FloodStep);
            if (start < 0 || start >= _columns || !IsFree(start, 0)) return null;
            if (_labels[start] != 0) return _regions[_labels[start]];

            var region = new Region();
            _regions.Add(region);
            int label = _regions.Count - 1;
            var queue = new Queue<int>();
            _labels[start] = label;
            queue.Enqueue(start);
            while (queue.Count > 0) {
                int index = queue.Dequeue();
                int column = index % _columns;
                int row = index / _columns;
                Rect2 box = Box(column, row);
                if (box.Position.X < 0f || box.End.X > Level10Controller.LevelWidth
                    || box.End.Y > Level10Controller.LevelHeight) {
                    region.Escape ??= box.GetCenter();
                    continue;
                }
                region.MinX = Mathf.Min(region.MinX, box.Position.X);
                region.MaxX = Mathf.Max(region.MaxX, box.End.X);
                region.MaxY = Mathf.Max(region.MaxY, box.End.Y);
                Visit(column - 1, row, label, queue);
                Visit(column + 1, row, label, queue);
                Visit(column, row - 1, label, queue);
                Visit(column, row + 1, label, queue);
            }
            return region;
        }

        private void Visit(int column, int row, int label, Queue<int> queue) {
            if (column < 0 || column >= _columns || row < 0 || row >= _rows) return;
            int index = row * _columns + column;
            if (_labels[index] != 0 || !IsFree(column, row)) return;
            _labels[index] = label;
            queue.Enqueue(index);
        }

        private Rect2 Box(int column, int row) =>
            new(_originX + column * FloodStep, _originY + row * FloodStep, _width, _height);

        private bool IsFree(int column, int row) {
            Rect2 box = Box(column, row);
            foreach (Rect2 solid in _solids) {
                if (box.Position.X < solid.End.X - Epsilon && box.End.X > solid.Position.X + Epsilon
                    && box.Position.Y < solid.End.Y - Epsilon && box.End.Y > solid.Position.Y + Epsilon) {
                    return false;
                }
            }
            return true;
        }
    }

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
