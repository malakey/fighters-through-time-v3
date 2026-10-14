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
/// Package 5 Wave B: Level 7 - Nassau, 1715.
///
/// What is pinned here is the contract the level cannot silently drift out of:
/// the manifest scene path and level id, the three checkpoint ids, the three-beat
/// dialogue set with every line key localized, the DUST_ECONOMY-locked 10/1/1/3
/// encounter budget authored from the Nassau roster, the Dread Admiral's ranged
/// band fitting the burning deck, and - the point of the level - a rope swing that
/// is load-bearing traversal rather than decoration: the water gap is wider than
/// any character can jump, the anchor chain closes it hop by hop, riding an anchor
/// really moves the player, and letting go really hands over momentum.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level07ContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_07_Nassau.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_07_dialogue.tres";
    private const int ScratchSlot = 2;
    private const float Step = 1f / 60f;

    /// <summary>BossController converts range thresholds at 60 px per world unit.</summary>
    private const float PixelsPerUnit = 60f;

    // === Scene and identity ===

    [TestCase]
    public void TheAuthoredSceneLoadsInstantiatesAndFreesCleanly() {
        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).IsNotNull();

        Node instance = scene.Instantiate();
        AssertObject(instance).IsNotNull();
        AssertThat(instance is Level07Controller).IsTrue();
        AssertObject(instance.GetNodeOrNull<StoryDropSystem>("StoryDropSystem")).IsNotNull();

        // The era mechanics are authored template instances, not code-built.
        foreach (string name in Level07Controller.AnchorNodeNames) {
            AssertObject(instance.GetNodeOrNull<PendulumAnchor>(name))
                .OverrideFailureMessage($"Rope anchor '{name}' is missing from the scene.").IsNotNull();
        }
        foreach (string name in Level07Controller.SkiffNodeNames) {
            AssertObject(instance.GetNodeOrNull<PathMovingPlatform>(name))
                .OverrideFailureMessage($"Boarding skiff '{name}' is missing from the scene.").IsNotNull();
        }
        AssertObject(instance.GetNodeOrNull<StoryCyclicHazard>("MortarChannelA")).IsNotNull();
        AssertObject(instance.GetNodeOrNull<StoryCyclicHazard>("FlagshipFireB")).IsNotNull();

        instance.Free();
    }

    [TestCase]
    public void TheControllerCarriesTheManifestIdentityAndTheCampaignSlot() {
        var level = new Level07Controller();
        try {
            AssertString(level.LevelID).IsEqual("level_07_nassau");
            AssertThat(level.Level).IsEqual(CampaignLevel.Nassau);
            AssertString(level.LevelTitleKey).IsEqual("nassau_level_title");
            AssertString(level.DialogueSetPath).IsEqual(DialoguePath);
            AssertString(StoryManager.GetLevelScenePath(CampaignLevel.Nassau)).IsEqual(ScenePath);

            AssertString(level.EntranceDialogueID).IsEqual("level_07.entrance");
            AssertString(level.BossIntroDialogueID).IsEqual("level_07.boss_intro");
            AssertString(level.ExitDialogueID).IsEqual("level_07.exit");

            // Florence-scale, with the headroom the rigging needs.
            AssertFloat(Level07Controller.LevelWidth).IsBetween(9000f, 11000f);
        } finally {
            level.Free();
        }
    }

    [TestCase]
    public void ExactlyThreeCheckpointsRegisterUnderTheLockedIDs() {
        using var fixture = new NassauFixture(null);
        AssertThat(Level07Controller.CheckpointIDs.Length).IsEqual(3);
        for (int index = 0; index < 3; index++) {
            string id = $"level_07_nassau_checkpoint_{index}";
            AssertString(Level07Controller.CheckpointIDs[index]).IsEqual(id);
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered.").IsTrue();
        }
    }

    // === Dialogue ===

    [TestCase]
    public void TheDialogueSetCarriesTheThreeAuthoredBeats() {
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        AssertObject(set).IsNotNull();
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_07");

        var ids = new List<string>();
        foreach (DialogueSequenceData sequence in set.Sequences) ids.Add(sequence.DialogueID);
        AssertThat(ids).ContainsExactlyInAnyOrder(
            "level_07.entrance", "level_07.boss_intro", "level_07.exit");
    }

    [TestCase]
    public void EveryDialogueLineSpeakerAndLevelKeyResolvesInTheEnglishTable() {
        HashSet<string> keys = LocalizationKeys();
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);

        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertThat(sequence.LineKeys.Length).IsGreater(0);
            AssertThat(sequence.SpeakerNameKeys.Length).IsEqual(sequence.LineKeys.Length);
            AssertThat(sequence.EmotionKeys.Length).IsEqual(sequence.LineKeys.Length);

            foreach (string key in sequence.LineKeys) {
                AssertThat(keys.Contains(key))
                    .OverrideFailureMessage($"Line key '{key}' is missing from localization/en.csv.")
                    .IsTrue();
                AssertThat(key.StartsWith("dlg_l07_", StringComparison.Ordinal)).IsTrue();
            }
            foreach (string key in sequence.SpeakerNameKeys) {
                AssertThat(keys.Contains(key))
                    .OverrideFailureMessage($"Speaker key '{key}' is missing from localization/en.csv.")
                    .IsTrue();
            }
        }

        foreach (string key in new[] {
            "nassau_level_title", "nassau_objective_reach_moorings", "nassau_objective_board_flagship",
            "nassau_objective_defeat_boss", "nassau_objective_complete",
            "nassau_room_harbour", "nassau_room_rigging", "nassau_room_channel", "nassau_room_flagship",
            "campaign_level_nassau"
        }) {
            AssertThat(keys.Contains(key))
                .OverrideFailureMessage($"Level key '{key}' is missing from localization/en.csv.")
                .IsTrue();
        }
    }

    // === Encounter economy (locked by docs/DUST_ECONOMY.md) ===

    [TestCase]
    public void TheAuthoredSpawnTableMatchesTheLockedTenOneOneThreeBudget() {
        AssertThat(Level07Controller.StandardEnemyCount)
            .OverrideFailureMessage("Level 7 is locked at 10 standard enemies by DUST_ECONOMY.md.")
            .IsEqual(10);
        // V7.6 (Package 11 A7a): the Eraser is salted through Levels 7-15 on top
        // of the pre-F05 locked row. It is an Unbound hunter, not an era enemy, and
        // it draws its share of the required-encounter budget rather than a flat
        // per-tier award (F05, A10) - which is why the elite count moves without
        // the era roster changing.
        AssertThat(Level07Controller.EliteEnemyCount)
            .OverrideFailureMessage("Level 7 authors 1 era elite plus 1 salted Eraser.")
            .IsEqual(2);
        AssertThat(Level07Controller.EraserCount).IsEqual(1);
        AssertThat(Level07Controller.SpawnTable.Length).IsEqual(12);
        AssertThat(Level07Controller.ExtractorCount)
            .OverrideFailureMessage("Level 7 is locked at 3 Chronal Extractors.")
            .IsEqual(3);

        var extractorIDs = new HashSet<string>();
        foreach ((string id, Vector2 _) in Level07Controller.ExtractorPlacements) {
            AssertThat(extractorIDs.Add(id))
                .OverrideFailureMessage($"Duplicate extractor id '{id}'.").IsTrue();
        }
    }

    [TestCase]
    public void TheNassauRosterMixesDeckhandsWithCultistsUnderOneCannonMaster() {
        foreach ((string enemyID, int wave, Vector2 _) in Level07Controller.SpawnTable) {
            AssertThat(enemyID is Level07Controller.StandardEnemyID
                    or Level07Controller.CultistEnemyID
                    or Level07Controller.EliteEnemyID
                    or Level07Controller.EraserEnemyID)
                .OverrideFailureMessage($"'{enemyID}' is not on the Nassau roster.").IsTrue();
            // GU-L4: four waves, two per encounter lock.
            AssertThat(wave >= 1 && wave <= Level07Controller.WaveCount).IsTrue();
        }

        var deckhand = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level07Controller.StandardEnemyID}.tres");
        var cultist = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level07Controller.CultistEnemyID}.tres");
        var elite = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level07Controller.EliteEnemyID}.tres");
        AssertObject(deckhand).IsNotNull();
        AssertObject(cultist).IsNotNull();
        AssertObject(elite).IsNotNull();
        AssertThat(deckhand.Tier).IsEqual(EnemyTier.Standard);
        AssertThat(cultist.Tier).IsEqual(EnemyTier.Standard);
        AssertThat(elite.Tier).IsEqual(EnemyTier.Elite);
        // The era identity of the deckhand: rapid, low-damage, ranged.
        AssertFloat(deckhand.AttackCooldown).IsLess(cultist.AttackCooldown + 0.01f);
        AssertObject(deckhand.PrimaryAttack).IsNotNull();
    }

    [TestCase]
    public void EveryWaveFitsInsideTheLevelPoolWarmCounts() {
        // level_07_nassau_pools warm counts are concurrency caps (plan section 2.5):
        // standard_enemy 12, elite_enemy 4, enemy_projectile 26.
        var perWave = new Dictionary<int, int>();
        var eliteWave = new Dictionary<int, int>();
        var rangedWave = new Dictionary<int, int>();
        foreach ((string enemyID, int wave, Vector2 _) in Level07Controller.SpawnTable) {
            perWave.TryGetValue(wave, out int running);
            perWave[wave] = running + 1;
            if (enemyID is Level07Controller.EliteEnemyID or Level07Controller.EraserEnemyID) {
                eliteWave.TryGetValue(wave, out int elites);
                eliteWave[wave] = elites + 1;
            }
            if (enemyID != Level07Controller.CultistEnemyID) {
                rangedWave.TryGetValue(wave, out int ranged);
                rangedWave[wave] = ranged + 1;
            }
        }
        foreach (KeyValuePair<int, int> wave in perWave) {
            AssertThat(wave.Value)
                .OverrideFailureMessage($"Wave {wave.Key} spawns {wave.Value} enemies at once, over the warm count.")
                .IsLessEqual(12);
        }
        foreach (KeyValuePair<int, int> wave in eliteWave) AssertThat(wave.Value).IsLessEqual(4);
        // Deckhands are rapid-fire; even at four shots each in flight the wave has
        // to stay under the enemy_projectile warm count.
        foreach (KeyValuePair<int, int> wave in rangedWave) {
            AssertThat(wave.Value * 4)
                .OverrideFailureMessage($"Wave {wave.Key}'s ranged pressure can exceed enemy_projectile 26.")
                .IsLessEqual(26);
        }
    }

    // === Boss ===

    [TestCase]
    public void TheDreadAdmiralResolvesAndItsRangedBandFitsTheBurningDeck() {
        var boss = AuthoredResources.Load<BossData>(Level07Controller.BossResourcePath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("dread_admiral");
        AssertThat(boss.PhaseThresholds.Length).IsEqual(1);
        AssertThat(boss.AttackPattern).IsEqual(BossAttackPattern.DistanceBased);
        AssertThat(boss.MeleeRangeThreshold < boss.RangedRangeThreshold).IsTrue();

        // Asserted against the resource rather than a hardcoded width, so retuning
        // the gatling zoner cannot silently outgrow its arena.
        float arenaWidth = Level07Controller.ArenaWidth;
        AssertThat(boss.RangedRangeThreshold * PixelsPerUnit < arenaWidth)
            .OverrideFailureMessage(
                $"Ranged band {boss.RangedRangeThreshold * PixelsPerUnit} px does not fit a {arenaWidth} px deck.")
            .IsTrue();
    }

    [TestCase]
    public void TheBossEncounterIsWiredIntoTheBurningDeckRoom() {
        using var fixture = new NassauFixture(null);
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertObject(encounter.Data).IsNotNull();
        AssertString(encounter.Data.BossID).IsEqual("dread_admiral");
        AssertFloat(encounter.Position.X).IsGreater(Level07Controller.Room4StartX);
        AssertFloat(encounter.Position.X).IsLess(Level07Controller.LevelWidth);
    }

    // === Era identity: the rope swing has to be the route ===

    [TestCase]
    public void TheRopeSwingSpansAChannelNoCharacterCanJump() {
        float bestJump = BestUnaidedJumpDistance();
        AssertFloat(bestJump).IsGreater(0f);

        // 25% headroom over the best jump in the roster, so the claim survives a
        // movement retune without the level quietly becoming a long jump.
        AssertThat(Level07Controller.GapWidth > bestJump * 1.25f)
            .OverrideFailureMessage(
                $"The {Level07Controller.GapWidth} px channel is inside the best unaided jump " +
                $"({bestJump:F0} px); the swing would be decoration.")
            .IsTrue();

        // ... and nothing solid bridges it. Every authored deck run either ends at
        // the launch yard or begins at the far hull.
        foreach ((float startX, float endX, float _) in Level07Controller.SolidDeckSpans) {
            bool overlapsGap = endX > Level07Controller.SwingGapStartX
                && startX < Level07Controller.SwingGapEndX;
            AssertThat(overlapsGap)
                .OverrideFailureMessage($"Deck span {startX}-{endX} walks straight across the swing channel.")
                .IsFalse();
        }
    }

    [TestCase]
    public void TheAnchorChainClosesTheChannelInHopsTheWeakestCharacterCanMake() {
        float weakestHop = WeakestUnaidedJumpDistance();
        AssertFloat(weakestHop).IsGreater(0f);
        // Same drift tolerance in the other direction: every hop must sit well
        // inside the shortest jump in the roster.
        float budget = weakestHop * 0.75f;

        float grabReach = Level07Controller.AnchorGrabRadius
            * Mathf.Sin(Mathf.DegToRad(Level07Controller.AnchorAmplitudeDegrees));
        float hangReach = Level07Controller.AnchorHangRadius
            * Mathf.Sin(Mathf.DegToRad(Level07Controller.AnchorAmplitudeDegrees));

        AssertThat(Level07Controller.AnchorPivotsX.Length).IsEqual(3);

        // Hop 1: off the launch yard onto the first rope.
        float previousReleaseX = Level07Controller.SwingGapStartX;
        foreach (float pivotX in Level07Controller.AnchorPivotsX) {
            float grabWestX = pivotX - grabReach;
            AssertFloat(grabWestX - previousReleaseX)
                .OverrideFailureMessage($"Reaching the rope at {pivotX} needs more than one hop.")
                .IsBetween(0f, budget);
            previousReleaseX = pivotX + hangReach;
        }
        // Final hop: off the last rope onto the far hull's receiving yard.
        AssertFloat(Level07Controller.ReceivingYardWestX - previousReleaseX)
            .OverrideFailureMessage("The last rope does not reach the far hull.")
            .IsBetween(0f, budget);

        // The ropes swing over the channel only, clear of both yards: the swing is
        // the crossing, never a hazard over a deck. (The rope body itself has been
        // non-solid since 2026-10-09, PendulumRopeBarTests, so it shoves nobody.)
        AssertFloat(Level07Controller.AnchorPivotsX[0] - grabReach)
            .IsGreater(Level07Controller.SwingGapStartX);
        AssertFloat(Level07Controller.AnchorPivotsX[2] + hangReach)
            .IsLess(Level07Controller.ReceivingYardWestX);
    }

    [TestCase]
    public void TheSceneAuthorsEachRopeExactlyWhereTheChainNeedsIt() {
        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        Node instance = scene.Instantiate();
        try {
            for (int index = 0; index < Level07Controller.AnchorNodeNames.Length; index++) {
                var anchor = instance.GetNodeOrNull<PendulumAnchor>(Level07Controller.AnchorNodeNames[index]);
                AssertObject(anchor).IsNotNull();
                AssertFloat(anchor.Position.X).IsEqualApprox(Level07Controller.AnchorPivotsX[index], 0.5f);
                AssertFloat(anchor.Position.Y).IsEqualApprox(Level07Controller.AnchorPivotY, 0.5f);
                AssertFloat(anchor.AmplitudeDegrees)
                    .IsEqualApprox(Level07Controller.AnchorAmplitudeDegrees, 0.01f);
                // A dead-stop anchor would break the hand-off the chain depends on.
                AssertFloat(anchor.ReleaseLaunchAssist).IsGreater(0f);
                AssertThat(anchor.CarryOccupant).IsTrue();
            }
        } finally {
            instance.Free();
        }
    }

    [TestCase]
    public void RidingARopeCarriesThePlayerForwardAndLettingGoHandsOverMomentum() {
        using var fixture = new NassauFixture(null);
        Level07Controller level = fixture.Level;
        AssertThat(level.RopeAnchors.Count).IsEqual(3);

        PendulumAnchor anchor = level.RopeAnchors[0];
        PlayerController player = level.Player;
        LedgeGrabPoint ledge = anchor.Ledge;
        AssertObject(ledge).IsNotNull();
        AssertThat(ledge.TryAcquire(player)).IsTrue();

        // Ride a full period and watch where the occupant actually goes.
        anchor.AdvanceSwing(Step);
        float minX = player.GlobalPosition.X;
        float maxX = minX;
        for (int frame = 0; frame < 150; frame++) {
            anchor.AdvanceSwing(Step);
            minX = Mathf.Min(minX, player.GlobalPosition.X);
            maxX = Mathf.Max(maxX, player.GlobalPosition.X);
            AssertFloat(player.GlobalPosition.DistanceTo(ledge.HangPosition)).IsLess(0.01f);
        }
        float sweep = maxX - minX;
        // A 45-degree arc on the template's 360 px rope sweeps ~509 px.
        AssertFloat(sweep)
            .OverrideFailureMessage($"The rope only carried the player {sweep:F0} px.")
            .IsGreater(400f);

        // Let go while the rope is travelling east and the launch has to arrive.
        Vector2 launch = Vector2.Zero;
        for (int frame = 0; frame < 200 && anchor.AnchorVelocity.X < 300f; frame++) {
            anchor.AdvanceSwing(Step);
        }
        AssertFloat(anchor.AnchorVelocity.X)
            .OverrideFailureMessage("The rope never reached a useful eastward speed.")
            .IsGreater(300f);

        anchor.OccupantLaunched += (_, velocity) => launch = velocity;
        float velocityBefore = player.Velocity.X;
        ledge.Release(player);
        anchor.AdvanceSwing(Step);

        AssertFloat(player.Velocity.X - velocityBefore)
            .OverrideFailureMessage("Releasing the rope imparted no forward momentum.")
            .IsGreater(300f);
        AssertFloat(launch.X).IsGreater(300f);
    }

    // === Era identity: the boarding skiffs ===

    [TestCase]
    public void TheBoardingSkiffsFerryAcrossGapsThatCannotSimplyBeStepped() {
        (float StartX, float EndX)[] crossings = SkiffCrossings();
        (float StartX, float EndX)[] envelopes = SkiffEnvelopes();
        AssertThat(envelopes.Length).IsEqual(crossings.Length);

        float weakestHop = WeakestUnaidedJumpDistance();
        for (int index = 0; index < crossings.Length; index++) {
            (float fromX, float toX) = crossings[index];
            (float westX, float eastX) = envelopes[index];
            AssertFloat(toX - fromX)
                .OverrideFailureMessage($"Crossing {index} is a step, not a ferry.")
                .IsGreater(weakestHop);
            AssertFloat(westX)
                .OverrideFailureMessage($"Skiff {index} never reaches its west boarding edge.")
                .IsLessEqual(fromX);
            AssertFloat(eastX)
                .OverrideFailureMessage($"Skiff {index} never reaches its east landing edge.")
                .IsGreaterEqual(toX);
        }
    }

    [TestCase]
    public void ASkiffRunsItsAuthoredCrossingAndCarriesOnArrival() {
        using var fixture = new NassauFixture(null);
        PathMovingPlatform skiff = fixture.Level.BoardingSkiffs[0];
        AssertObject(skiff).IsNotNull();

        (Vector2 position, float westOffset, float eastOffset, float _) = Level07Controller.Skiffs[0];
        // _Ready parks the platform on waypoint 0, the west (boarding) end.
        AssertFloat(skiff.Position.X).IsEqualApprox(position.X + westOffset, 1f);

        float travelled = 0f;
        for (int frame = 0; frame < 400; frame++) {
            float before = skiff.Position.X;
            skiff.AdvancePath(Step);
            travelled = Mathf.Max(travelled, skiff.Position.X - (position.X + westOffset));
            if (skiff.Position.X >= position.X + eastOffset - 0.5f && before < skiff.Position.X) break;
        }
        AssertFloat(skiff.Position.X)
            .OverrideFailureMessage("The skiff never reached its far waypoint.")
            .IsEqualApprox(position.X + eastOffset, 1.5f);
        AssertFloat(travelled).IsGreater(0f);
        // Godot derives the carry velocity from the transform delta, so a skiff that
        // teleports rather than moves would ferry nothing.
        AssertFloat(skiff.Speed).IsGreater(0f);
    }

    // === Era identity: the listing, burning deck (and no flood) ===

    [TestCase]
    public void TheFlagshipDeckListsToStarboardAsStaticGeometryAndNassauNeverFloods() {
        using var fixture = new NassauFixture(null);
        Level07Controller level = fixture.Level;

        // Starboard list: the deck drops as it runs east, and it never moves again.
        AssertFloat(Level07Controller.ListDeckWestY).IsLess(Level07Controller.ListDeckMidY);
        AssertFloat(Level07Controller.ListDeckMidY).IsLess(Level07Controller.ListDeckEastY);

        int tiltedSlabs = 0;
        int floodZones = 0;
        Godot.Collections.Array<Node> children = level.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is RisingWaterZone) floodZones++;
            if (child is StaticBody2D slab && slab.Position.X > Level07Controller.Room4StartX
                && Mathf.Abs(slab.RotationDegrees) > 1f) {
                tiltedSlabs++;
            }
        }
        AssertThat(tiltedSlabs)
            .OverrideFailureMessage("The burning deck has no authored slope; the list is only prose.")
            .IsGreaterEqual(2);
        // Level 5 is the flooding ship. Nassau's list is fixed - if a RisingWaterZone
        // ever appears here the two levels have collapsed into one another.
        AssertThat(floodZones).IsEqual(0);

        // The two wooden yards stay dead horizontal, which is what reads the tilt.
        foreach (string name in new[] { "Platform_9300_700", "Platform_10100_700" }) {
            var yard = level.GetNodeOrNull<StaticBody2D>(name);
            AssertObject(yard).OverrideFailureMessage($"Wooden yard '{name}' is missing.").IsNotNull();
            AssertFloat(yard.RotationDegrees).IsEqualApprox(0f, 0.001f);
        }
    }

    [TestCase]
    public void GoingOverTheSideCostsHealthAndPutsThePlayerBackOnTheNearDeck() {
        using var fixture = new NassauFixture(null);
        Level07Controller level = fixture.Level;
        PlayerController player = level.Player;

        foreach ((string id, float startX, float endX, Vector2 rescue) in Level07Controller.WaterSpans) {
            // Every rescue anchor has to stand on real deck west of its own water.
            AssertThat(IsOnSolidDeck(rescue.X))
                .OverrideFailureMessage($"Rescue anchor for '{id}' is not over a deck.").IsTrue();
            AssertFloat(rescue.X).IsLess(startX);
            AssertFloat(endX).IsGreater(startX);

            int hpBefore = player.CurrentHP;
            player.GlobalPosition = new Vector2((startX + endX) / 2f, Level07Controller.SeaSurfaceY + 60f);
            player.Velocity = new Vector2(400f, 900f);
            level.HaulOutOfTheWater(player, rescue);

            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("Falling in cost nothing.").IsLess(hpBefore);
            // Heavy, but never an instadeath: rewind has to stay the failure state.
            AssertThat(player.CurrentHP).IsGreater(0);
            AssertFloat(player.GlobalPosition.X).IsEqualApprox(rescue.X, 0.5f);
            AssertFloat(player.Velocity.Length()).IsEqualApprox(0f, 0.01f);
        }
    }

    // === Rooms and checkpoint resume ===

    [TestCase]
    public void EveryRoomConfinesTheCameraToAtLeastTheReferenceViewport() {
        using var fixture = new NassauFixture(null);
        AssertThat(fixture.Level.RoomTriggers.Count).IsGreaterEqual(4);
        foreach (RoomTransitionTrigger trigger in fixture.Level.RoomTriggers) {
            AssertFloat(trigger.CameraBounds.Size.X)
                .OverrideFailureMessage($"Room '{trigger.RoomID}' confines narrower than the 1920 px viewport.")
                .IsGreaterEqual(1920f);
            // Level agents never set CameraPath; the base back-fills it (A1 deviation).
            AssertThat(trigger.CameraPath == null || trigger.CameraPath.IsEmpty).IsFalse();
        }
    }

    [TestCase]
    public void EveryCheckpointStandsOnSolidDeckClearOfTheWaterAndTheSkiffs() {
        using var fixture = new NassauFixture(null);
        foreach (string checkpointID in Level07Controller.CheckpointIDs) {
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(checkpointID, out Vector2 respawn)).IsTrue();

            AssertThat(IsOnSolidDeck(respawn.X))
                .OverrideFailureMessage($"'{checkpointID}' does not stand on an authored deck.").IsTrue();
            foreach ((string id, float startX, float endX, Vector2 _) in Level07Controller.WaterSpans) {
                AssertThat(respawn.X > startX && respawn.X < endX)
                    .OverrideFailureMessage($"'{checkpointID}' resumes over the water '{id}'.").IsFalse();
            }
            foreach ((float westX, float eastX) in Level07Controller.SkiffTravelSpans) {
                AssertThat(respawn.X > westX && respawn.X < eastX)
                    .OverrideFailureMessage($"'{checkpointID}' resumes inside a skiff's run; it may have sailed.")
                    .IsFalse();
            }
        }
    }

    [TestCase]
    public void ResumingAtACheckpointKeepsClearedWavesClearedAndConfinesTheCamera() {
        foreach (string checkpointID in Level07Controller.CheckpointIDs) {
            using var fixture = new NassauFixture(new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Nassau),
                LastCheckpointID = checkpointID,
                CurrentHP = 70,
                CurrentUltimateMeter = 25f
            });
            Level07Controller level = fixture.Level;

            AssertThat(level.ResumedMidLevel)
                .OverrideFailureMessage($"'{checkpointID}' did not restore.").IsTrue();
            AssertString(level.ResumedCheckpointID).IsEqual(checkpointID);

            // GU-L4: the encounters are the two locks; every lock behind the
            // anchor is built Cleared, everything ahead of it re-arms in full.
            if (checkpointID == Level07Controller.Checkpoint0) {
                AssertThat(level.QuayLock.State).IsEqual(EncounterLockState.Armed);
                AssertThat(level.QuayLock.RegisteredCount).IsGreater(0);
                AssertThat(level.BoardingLock.State).IsEqual(EncounterLockState.Armed);
            } else {
                AssertThat(level.QuayLock.RestoredCleared).IsTrue();
                AssertThat(level.QuayLock.RegisteredCount).IsEqual(0);
            }
            AssertThat(level.BoardingLock.RestoredCleared).IsEqual(checkpointID == Level07Controller.Checkpoint2);

            // The base class confines the camera to the room the resume starts in.
            AssertObject(level.Camera).IsNotNull();
            AssertFloat(level.Camera.ActiveBounds.Size.X).IsLessEqual(Level07Controller.LevelWidth);
            AssertThat(level.Player.Position.X >= level.Camera.ActiveBounds.Position.X
                && level.Player.Position.X <= level.Camera.ActiveBounds.End.X)
                .OverrideFailureMessage($"'{checkpointID}' resumes outside its own camera bounds.").IsTrue();
        }
    }

    // === GU-L4 (2026-10-12): the encounter locks ===

    /// <summary>
    /// Both channels are water, so the two locks sit on the decks: the quay
    /// (waves 1 and 2) and the boarding action from the Bonnet across the channel
    /// to the ramp (waves 3 and 4), each barrier standing on a deck. Every member
    /// spawns on solid deck inside its lock — the Eraser no longer spawns over the
    /// boarding channel — and the anchors stand clear of both elites.
    /// </summary>
    [TestCase]
    public void TheTwoDecksAreEncounterLocksAndEveryMemberIncludingTheEraserSpawnsOnSolidDeck() {
        using var fixture = new NassauFixture(null);
        Level07Controller level = fixture.Level;
        AssertThat(level.EncounterLocks.Count).IsEqual(2);

        var expected = new (EncounterLock Lock, string ID, float MinX, float MaxX, int[] Waves, EncounterWaveTrigger[] Triggers)[] {
            (level.QuayLock, Level07Controller.LockQuay, 380f, 3580f, new[] { 1, 2 },
                new[] { EncounterWaveTrigger.OnBuild, EncounterWaveTrigger.WhenAliveAtMost }),
            (level.BoardingLock, Level07Controller.LockBoarding, 5780f, 8480f, new[] { 3, 4 },
                new[] { EncounterWaveTrigger.OnBuild, EncounterWaveTrigger.AfterPreviousCleared })
        };
        var claimed = new HashSet<int>();
        foreach ((EncounterLock encounterLock, string id, float minX, float maxX, int[] waves,
                     EncounterWaveTrigger[] triggers) in expected) {
            AssertObject(encounterLock).OverrideFailureMessage($"'{id}' was not built.").IsNotNull();
            AssertString(encounterLock.LockID).IsEqual(id);
            AssertFloat(encounterLock.Area.Position.X).IsEqualApprox(minX, 0.5f);
            AssertFloat(encounterLock.Area.End.X).IsEqualApprox(maxX, 0.5f);
            // Both barriers stand on a deck.
            foreach (EncounterLockBarrier barrier in new[] { encounterLock.EntryBarrier, encounterLock.ExitBarrier }) {
                AssertThat(IsOnSolidDeck(barrier.CenterX))
                    .OverrideFailureMessage($"'{id}''s barrier at {barrier.CenterX} stands over water.").IsTrue();
            }
            AssertThat(encounterLock.Spec.Waves.Length).IsEqual(waves.Length);
            for (int index = 0; index < waves.Length; index++) {
                EncounterLockWave wave = encounterLock.Spec.Waves[index];
                AssertString(wave.RowID).IsEqual(Level07Controller.WaveRowID(waves[index]));
                AssertThat(wave.Trigger).IsEqual(triggers[index]);
                AssertThat(claimed.Add(waves[index])).IsTrue();
                var authored = new List<string>();
                foreach ((string enemyID, int entryWave, Vector2 position) in Level07Controller.SpawnTable) {
                    if (entryWave != waves[index]) continue;
                    authored.Add(enemyID);
                    AssertThat(position.X > minX && position.X < maxX)
                        .OverrideFailureMessage($"Wave {entryWave}'s {enemyID} at {position} is outside '{id}'.").IsTrue();
                    AssertThat(IsOnSolidDeck(position.X))
                        .OverrideFailureMessage($"Wave {entryWave}'s {enemyID} at {position} spawns over water.").IsTrue();
                    foreach ((string _, float startX, float endX, Vector2 _) in Level07Controller.WaterSpans) {
                        AssertThat(position.X > startX && position.X < endX)
                            .OverrideFailureMessage($"{enemyID} at {position} spawns over the channel {startX}..{endX}.")
                            .IsFalse();
                    }
                }
                AssertString(string.Join(",", wave.EnemyIDs)).IsEqual(string.Join(",", authored));
            }
            AssertThat(encounterLock.Spec.Waves[0].Scaled && encounterLock.Spec.Waves[1].Scaled).IsTrue();
        }
        AssertThat(claimed.Count).IsEqual(Level07Controller.WaveCount);
        // The quay's reinforcements come when one dockside fighter is left.
        AssertThat(level.QuayLock.Spec.Waves[1].AliveAtMost).IsEqual(1);

        // The anchors stand clear of both elites (both spawn on the ramp, behind
        // the boarding lock's exit), and outside both locks.
        AssertThat(level.Levels.TryGetCheckpointPosition(Level07Controller.Checkpoint2, out Vector2 preBoss)).IsTrue();
        foreach ((string enemyID, int _, Vector2 position) in Level07Controller.SpawnTable) {
            if (enemyID is not (Level07Controller.EliteEnemyID or Level07Controller.EraserEnemyID)) continue;
            AssertThat(position.X < level.BoardingLock.Area.End.X).IsTrue();
            AssertThat(preBoss.X - position.X > 300f)
                .OverrideFailureMessage($"The PreBoss anchor at {preBoss.X} stands beside the elite {enemyID} at {position.X}.")
                .IsTrue();
        }

        IReadOnlyDictionary<string, string[]> map = level.AuthoredEncounterBaselines;
        AssertThat(map[Level07Controller.Checkpoint0].Length).IsEqual(0);
        AssertString(string.Join(",", map[Level07Controller.Checkpoint1])).IsEqual(Level07Controller.LockQuay);
        AssertString(string.Join(",", map[Level07Controller.Checkpoint2]))
            .IsEqual($"{Level07Controller.LockQuay},{Level07Controller.LockBoarding}");
    }

    /// <summary>
    /// The movers and the sea respect the locks. No skiff's travel envelope and
    /// no rope's swing (grab box or hang point) crosses a lock barrier, so none
    /// needs holding at a near stop; every sea haul-out lands on a deck inside a
    /// lock area whose exit lies east of that channel, so going over the side can
    /// never land the hero past an uncleared exit; and the anchors and the font
    /// stand outside both locks, beyond a swing from inside.
    /// </summary>
    [TestCase]
    public void NoMoverCrossesABarrierAndNoHaulOutLandsPastAnUnclearedExit() {
        using var fixture = new NassauFixture(null);
        Level07Controller level = fixture.Level;
        var barriers = new List<float>();
        foreach (EncounterLock encounterLock in level.EncounterLocks) {
            barriers.Add(encounterLock.EntryBarrier.CenterX);
            barriers.Add(encounterLock.ExitBarrier.CenterX);
        }

        foreach ((float westX, float eastX) in Level07Controller.SkiffTravelSpans) {
            foreach (float barrierX in barriers) {
                AssertThat(barrierX > westX - 10f && barrierX < eastX + 10f)
                    .OverrideFailureMessage($"A skiff sweeping {westX}..{eastX} crosses the lock barrier at {barrierX}.")
                    .IsFalse();
            }
        }
        float reach = Mathf.Max(Level07Controller.AnchorGrabRadius, Level07Controller.AnchorHangRadius)
            * Mathf.Sin(Mathf.DegToRad(Level07Controller.AnchorAmplitudeDegrees));
        foreach (float pivotX in Level07Controller.AnchorPivotsX) {
            foreach (float barrierX in barriers) {
                AssertThat(barrierX > pivotX - reach - 30f && barrierX < pivotX + reach + 30f)
                    .OverrideFailureMessage($"The rope at {pivotX} swings across the lock barrier at {barrierX}.")
                    .IsFalse();
            }
        }

        foreach ((string id, float startX, float endX, Vector2 rescue) in Level07Controller.WaterSpans) {
            // The haul-out lands west of its channel, on a deck inside a lock area...
            AssertThat(rescue.X < startX).IsTrue();
            bool inside = false;
            foreach (EncounterLock encounterLock in level.EncounterLocks) {
                if (rescue.X > encounterLock.Area.Position.X && rescue.X < encounterLock.Area.End.X) inside = true;
                // ...and never past an exit the hero could still owe: any lock whose
                // exit lies west of the landing was already behind the hero when
                // it reached this channel (its exit is at or west of the channel).
                float exitX = encounterLock.ExitBarrier.CenterX;
                if (exitX <= rescue.X) {
                    AssertThat(exitX <= startX + 0.5f)
                        .OverrideFailureMessage($"'{id}''s haul-out at {rescue} lands past '{encounterLock.LockID}''s exit ({exitX}), east of the channel.")
                        .IsTrue();
                }
            }
            AssertThat(inside).OverrideFailureMessage($"'{id}''s haul-out at {rescue} lands outside every lock.").IsTrue();
        }

        float swing = EncounterLockReach.LongestBasicSwingPixels();
        AssertThat(level.Levels.TryGetCheckpointPosition(Level07Controller.Checkpoint1, out Vector2 middle)).IsTrue();
        AssertThat(level.Levels.TryGetCheckpointPosition(Level07Controller.Checkpoint2, out Vector2 preBoss)).IsTrue();
        float quayReach = EncounterLockReach.EastReachFromInside(level.QuayLock.Area.End.X, swing);
        AssertThat(quayReach < middle.X - EncounterLockReach.StrikeSurfaceHalfWidthPixels).IsTrue();
        float boardingReach = EncounterLockReach.EastReachFromInside(level.BoardingLock.Area.End.X, swing);
        AssertThat(boardingReach < preBoss.X - EncounterLockReach.StrikeSurfaceHalfWidthPixels)
            .OverrideFailureMessage($"A swing from the boarding lock reaches x {boardingReach:0}; the PreBoss anchor stands at {preBoss.X}.")
            .IsTrue();
        float fontX = level.AuthoredRestorationFontPosition.Value.X;
        foreach (EncounterLock encounterLock in level.EncounterLocks) {
            foreach (float x in new[] { middle.X, preBoss.X, fontX }) {
                AssertThat(x + 30f <= encounterLock.Area.Position.X || x - 30f >= encounterLock.Area.End.X)
                    .OverrideFailureMessage($"An anchor or the font at {x} stands inside '{encounterLock.LockID}'.").IsTrue();
            }
        }
    }

    /// <summary>
    /// The boarding action in real frames: the Bonnet and the gun pier's crew is
    /// pre-placed; the flagship's boarding crew (the cannon master and the Eraser
    /// among them) musters on the ramp only once they are down, and Sarah's first
    /// Eraser bark fires then. The haul-out from the boarding channel lands inside
    /// the engaged lock and leaves it engaged.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheBoardingCrewMustersOnTheRampOnceTheBonnetAndThePierAreClear() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new NassauFixture(new StorySaveData {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Nassau),
            LastCheckpointID = Level07Controller.Checkpoint1,
            CurrentHP = 100
        });
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level07Controller level = fixture.Level;
        EncounterLock boarding = level.BoardingLock;
        AssertThat(level.QuayLock.RestoredCleared).IsTrue();
        AssertThat(boarding.State).IsEqual(EncounterLockState.Armed);
        AssertThat(level.FirstEraserBarkShown).IsFalse();

        PlayerController hero = level.Player;
        hero.GlobalPosition = new Vector2(6100f, 880f);
        hero.Velocity = Vector2.Zero;
        for (int frame = 0; frame < 30 && boarding.State != EncounterLockState.Engaged; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(boarding.State).IsEqual(EncounterLockState.Engaged);
        int pierCrew = boarding.LivingMemberCount;
        AssertThat(pierCrew).IsGreater(0);
        foreach (EnemyController member in boarding.LivingMembers) {
            AssertThat(member.GlobalPosition.X < Level07Controller.ChannelStartX).IsTrue();
        }
        AssertThat(level.FirstEraserBarkShown).IsFalse();

        // Going over the side mid-fight hauls the hero out onto the pier, still inside.
        level.HaulOutOfTheWater(hero, Level07Controller.WaterSpans[1].Rescue);
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(boarding.State).IsEqual(EncounterLockState.Engaged);

        foreach (EnemyController member in boarding.LivingMembers) member.TakeDamage(99999);
        bool mustered = false;
        for (int frame = 0; frame < 10 && !mustered; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            mustered = boarding.LivingMemberCount > 0;
        }
        AssertThat(mustered).OverrideFailureMessage("The boarding crew never mustered.").IsTrue();
        AssertThat(boarding.State).IsEqual(EncounterLockState.Engaged);
        foreach (EnemyController member in boarding.LivingMembers) {
            AssertThat(member.GlobalPosition.X > Level07Controller.ChannelEndX)
                .OverrideFailureMessage($"A boarding-crew member stands at {member.GlobalPosition.X}, off the ramp.").IsTrue();
        }
        bool eraserMustered = false;
        foreach (EnemyController member in boarding.LivingMembers) {
            if (member.Data?.EnemyID == Level07Controller.EraserEnemyID) eraserMustered = true;
        }
        if (eraserMustered) {
            for (int frame = 0; frame < 3 && !level.FirstEraserBarkShown; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(level.FirstEraserBarkShown).OverrideFailureMessage("The Eraser mustered without its bark.").IsTrue();
        }

        for (int pass = 0; pass < 6 && boarding.State == EncounterLockState.Engaged; pass++) {
            foreach (EnemyController member in boarding.LivingMembers) member.TakeDamage(99999);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(boarding.State).IsEqual(EncounterLockState.Cleared);
        AssertThat(boarding.ExitBarrier.IsClosed).IsFalse();
    }

    /// <summary>
    /// FIX-LEVEL (2026-10-13, Phase B review item 2): the Middle anchor is struck
    /// where rope C lands the hero, before the boarding lock engages. GU-L4 put it
    /// on the Bonnet's deck under the solid receiving yard, reachable only from
    /// the yard's east end (5950), past the lock's engage line (5824): walking off
    /// the yard engaged the lock, its entry barrier shut between the hero and the
    /// anchor, and the boarding fight had no checkpoint. Real frames, real input,
    /// a real swing: the hero lands on the yard's west lip, walks to the anchor and
    /// strikes it, and the boarding lock is still Armed.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheMiddleAnchorIsStruckFromTheRopeLandingBeforeTheBoardingLockEngages() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        StoryManager story = StoryManager.Instance;
        using var fixture = new NassauFixture(null);
        Level07Controller level = fixture.Level;
        PlayerController hero = level.Player;
        var input = new BufferedInputSource();
        try {
            await LevelLockTestKit.Frames(2);
            LevelLockTestKit.SkipDialogue();
            // The quay fight and the ropes are behind the hero.
            level.QuayLock.RestoreCleared();
            CheckpointTrigger middle = level.GetNode<CheckpointTrigger>($"Checkpoint_{Level07Controller.Checkpoint1}");
            EncounterLock boarding = level.BoardingLock;
            float engageX = boarding.Area.Position.X + LevelLockTestKit.HeroHalfWidthPixels + EncounterLockRules.EngageInsetPixels;
            AssertThat(middle.GlobalPosition.X + 30f < engageX).IsTrue();

            // Rope C's release lands the hero on the yard's west lip.
            LevelLockTestKit.PlaceHero(hero, Level07Controller.ReceivingYardWestX + 30f, Level07Controller.ReceivingYardY - 1f);
            await LevelLockTestKit.Frames(8);
            AssertThat(hero.IsOnFloor() && Mathf.Abs(hero.GlobalPosition.Y - Level07Controller.ReceivingYardY) < 2f)
                .OverrideFailureMessage($"The hero did not land on the receiving yard: {hero.GlobalPosition}.").IsTrue();

            InputManager.Instance.SetInputSource(hero.PlayerIndex, input);
            uint tick = 0;
            float strikeFromX = middle.GlobalPosition.X - 45f;
            for (int frame = 0; frame < 60 && hero.GlobalPosition.X < strikeFromX; frame++) {
                input.SetNextFrame(PlayerInputFrame.Create(tick++, 1f, 0f, GameplayButtons.None));
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            input.SetNextFrame(PlayerInputFrame.Create(tick++, 0f, 0f, GameplayButtons.None));
            await LevelLockTestKit.Frames(6);
            AssertThat(boarding.State).IsEqual(EncounterLockState.Armed);
            AssertThat(middle.IsHeldByEncounterLock).IsFalse();
            for (int frame = 0; frame < 30 && !middle.IsActivated; frame++) {
                GameplayButtons press = frame < 2 ? GameplayButtons.BasicAttack : GameplayButtons.None;
                input.SetNextFrame(PlayerInputFrame.Create(tick++, 0f, 0f, press));
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(middle.IsActivated)
                .OverrideFailureMessage($"A swing from x {hero.GlobalPosition.X:0} on the yard did not stabilize the Middle anchor.")
                .IsTrue();
            AssertThat(boarding.State)
                .OverrideFailureMessage("The boarding lock engaged before the Middle anchor was struck.")
                .IsEqual(EncounterLockState.Armed);
            AssertThat(hero.GlobalPosition.X < engageX).IsTrue();
        } finally {
            InputManager.Instance.ClearInputSource(hero.PlayerIndex);
            PoolManager.Instance?.ReleaseActiveUnder(level);
            SessionExitGuard.ClearMarker();
            story.ClearLevelAttemptState();
        }
    }

    /// <summary>
    /// FIX-LEVEL (2026-10-13, Phase B review item 4): the boarding channel's room
    /// trigger (6040–6120) stands 200 px inside the boarding lock, and crossing it
    /// posted "board the flagship" over the engaged lock's own objective for the
    /// rest of the fight. A room trigger now never posts over an engaged lock
    /// that posts its own (StoryLevelControllerBase, every level); its objective
    /// waits for the lock's clear.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheBoardingChannelSeamNeverPostsOverTheEngagedLocksObjective() {
        using var fixture = new NassauFixture(new StorySaveData {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Nassau),
            LastCheckpointID = Level07Controller.Checkpoint1,
            CurrentHP = 100
        });
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level07Controller level = fixture.Level;
        EncounterLock boarding = level.BoardingLock;
        Label objective = level.HUD.GetNode<Label>("SafeArea/TopLeft/Objective");
        string lockLine = TranslationServer.Translate("encounter_lock_objective_clear").ToString();
        try {
            LevelLockTestKit.PlaceHero(level.Player, Level07Controller.BoardingLockMinX + 100f, Level07Controller.DeckY - 1f);
            await LevelLockTestKit.Frames(4);
            AssertThat(boarding.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(objective.Text).IsEqual(lockLine);

            // Into the channel seam's east trigger, mid-fight.
            LevelLockTestKit.PlaceHero(level.Player, Level07Controller.Room3StartX + 80f, Level07Controller.DeckY - 1f);
            await LevelLockTestKit.Frames(4);
            AssertThat(boarding.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(objective.Text)
                .OverrideFailureMessage("The channel seam replaced the engaged boarding lock's objective.")
                .IsEqual(lockLine);

            AssertThat(await LevelLockTestKit.ClearByForce(boarding)).IsTrue();
            AssertThat(objective.Text)
                .IsEqual(TranslationServer.Translate("nassau_objective_board_flagship").ToString());
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    // === Helpers ===

    private static bool IsOnSolidDeck(float x) {
        foreach ((float startX, float endX, float _) in Level07Controller.SolidDeckSpans) {
            if (x >= startX && x <= endX) return true;
        }
        return false;
    }

    /// <summary>Piling and deck edges each skiff has to bridge, west to east.</summary>
    private static (float StartX, float EndX)[] SkiffCrossings() {
        (float centerX, float topY, float width)[] pilings = Level07Controller.Pilings;
        float piling0West = pilings[0].centerX - pilings[0].width / 2f;
        float piling0East = pilings[0].centerX + pilings[0].width / 2f;
        float piling1West = pilings[1].centerX - pilings[1].width / 2f;
        float piling1East = pilings[1].centerX + pilings[1].width / 2f;
        return new[] {
            (Level07Controller.ChannelStartX, piling0West),
            (piling0East, piling1West),
            (piling1East, Level07Controller.ChannelEndX)
        };
    }

    private static (float StartX, float EndX)[] SkiffEnvelopes() {
        var spans = new List<(float, float)>();
        foreach ((float startX, float endX) in Level07Controller.SkiffTravelSpans) spans.Add((startX, endX));
        return spans.ToArray();
    }

    /// <summary>
    /// Longest horizontal distance any character in the roster can cover with an
    /// unaided jump from flat ground: <see cref="StoryAirPhysics.JumpDistance"/>,
    /// the controller's own per-tick air step integrated (apex hang, fall
    /// multiplier and terminal included; a double jump presses at the first apex)
    /// while air control holds MaxMoveSpeed * 60 the whole way. Until 2026-10-08
    /// this was a continuous closed form (1.8x falls, a 600 px/s terminal).
    ///
    /// Movement abilities (Leonardo's glide, warps, dashes, leaps) are excluded on
    /// purpose - the claim being pinned is that the channel beats a *jump*, which is
    /// what makes the rope swing the route. The callers apply headroom on top so a
    /// movement retune cannot quietly falsify the level design.
    /// </summary>
    private static float BestUnaidedJumpDistance() {
        float best = 0f;
        foreach (CharacterData data in RosterCharacters()) {
            best = Mathf.Max(best, JumpDistance(data));
        }
        return best;
    }

    private static float WeakestUnaidedJumpDistance() {
        float weakest = float.MaxValue;
        foreach (CharacterData data in RosterCharacters()) {
            weakest = Mathf.Min(weakest, JumpDistance(data, singleJumpOnly: true));
        }
        return weakest == float.MaxValue ? 0f : weakest;
    }

    private static float JumpDistance(CharacterData data, bool singleJumpOnly = false) =>
        StoryAirPhysics.JumpDistance(
            StoryAirPhysics.JumpLaunchSpeed(data.MaxJumpForce),
            data.Weight,
            data.MaxMoveSpeed * StoryAirPhysics.PixelsPerUnit,
            singleJumpOnly ? 1 : Mathf.Max(1, data.MaxJumpCount));

    private static IEnumerable<CharacterData> RosterCharacters() {
        string[] files = Directory.GetFiles("resources/Characters", "*_data.tres");
        AssertThat(files.Length)
            .OverrideFailureMessage("The nine-character roster did not read from resources/Characters.")
            .IsEqual(9);
        foreach (string file in files) {
            var data = AuthoredResources.Load<CharacterData>(
                $"res://resources/Characters/{Path.GetFileName(file)}");
            if (data != null) yield return data;
        }
    }

    private static HashSet<string> LocalizationKeys() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        AssertThat(keys.Count).OverrideFailureMessage("localization/en.csv did not read.").IsGreater(500);
        return keys;
    }

    /// <summary>
    /// Builds the authored scene in the runner tree against an optional scratch
    /// save, then hands every shared singleton back untouched: session slot,
    /// character, save row, pooled enemies, and the gameplay pause flag. A leaked
    /// pause would freeze GdUnit's own transport node (CLAUDE.md signature 4).
    /// </summary>
    private sealed class NassauFixture : IDisposable {
        public readonly Level07Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public NassauFixture(StorySaveData save) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level07Controller>();
            Level.Name = "Level_07_Nassau_Test";
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
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
