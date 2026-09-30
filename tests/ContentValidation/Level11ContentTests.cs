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
/// Package 5 Wave B - Level 11, Gettysburg 1863. The campaign's most linear level, so
/// the contracts worth pinning are the ones that make a straight line good rather than
/// dull: the advance/cover/artillery beat is fair (every gap between two shell columns
/// holds cover, and no cover stands inside one), the artillery really telegraphs before
/// it strikes, the two shielding arrays are an offensive objective rather than an
/// Orléans-style wall across the corridor, and the Siege Cannon's 12-unit ranged band -
/// the widest in the roster - actually fits its arena.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level11ContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_11_Gettysburg.tscn";
    private const int ScratchSlot = 2;

    /// <summary>BossController converts range thresholds at 60 px per world unit.</summary>
    private const float PixelsPerUnit = 60f;

    // === Scene and identity ===

    [TestCase]
    public void TheSceneLoadsAtTheRoutedPathInstantiatesAndFreesCleanly() {
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.Gettysburg)).IsEqual(ScenePath);

        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).IsNotNull();

        Node instance = scene.Instantiate();
        AssertObject(instance).IsNotNull();
        AssertThat(instance is Level11Controller).IsTrue();
        instance.Free();
    }

    [TestCase]
    public void TheControllerReportsTheManifestIdentityAndDialogueWiring() {
        using var fixture = new GettysburgFixture();
        Level11Controller level = fixture.Level;

        AssertString(level.LevelID).IsEqual("level_11_gettysburg");
        AssertThat(level.Level).IsEqual(CampaignLevel.Gettysburg);
        AssertString(level.LevelTitleKey).IsEqual("gettysburg_level_title");
        AssertString(level.DialogueSetPath).IsEqual("res://resources/Dialogue/level_11_dialogue.tres");
        AssertString(level.EntranceDialogueID).IsEqual("level_11.entrance");
        AssertString(level.BossIntroDialogueID).IsEqual("level_11.boss_intro");
        AssertString(level.ExitDialogueID).IsEqual("level_11.exit");

        // CheckpointTrigger resolves the manager by literal node name.
        var manager = level.GetNodeOrNull<LevelManager>("LevelManager");
        AssertObject(manager).IsNotNull();
        AssertString(manager.LevelID).IsEqual("level_11_gettysburg");
    }

    [TestCase]
    public void ExactlyThreeCheckpointsRegisterUnderTheLockedIDs() {
        using var fixture = new GettysburgFixture();
        foreach (string id in new[] {
            Level11Controller.Checkpoint0, Level11Controller.Checkpoint1, Level11Controller.Checkpoint2 }) {
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered with the LevelManager.")
                .IsTrue();
        }

        int checkpoints = 0;
        foreach (Node child in Children(fixture.Level)) {
            if (child is CheckpointTrigger) checkpoints++;
        }
        AssertThat(checkpoints).OverrideFailureMessage(
            "Level 11 must ship exactly three checkpoints (entry anchor, midpoint, pre-boss).")
            .IsEqual(3);
    }

    [TestCase]
    public void TheLinearAssaultIsFourCameraConfinedRoomsAcrossAWideField() {
        using var fixture = new GettysburgFixture();
        Level11Controller level = fixture.Level;

        AssertThat(level.RoomTriggers.Count).OverrideFailureMessage(
            "Level 11 is authored as four camera-confined rooms.").IsEqual(4);
        foreach (RoomTransitionTrigger trigger in level.RoomTriggers) {
            // The base back-fills CameraPath after the player spawns; without it room
            // confinement silently does nothing.
            AssertThat(trigger.CameraPath != null && !trigger.CameraPath.IsEmpty)
                .OverrideFailureMessage($"Room '{trigger.RoomID}' has no camera path.").IsTrue();
            AssertThat(trigger.CameraBounds.Size.X).OverrideFailureMessage(
                $"Room '{trigger.RoomID}' is narrower than the 1920 px reference viewport.")
                .IsGreaterEqual(1920f);
        }

        // A linear assault wants length; the plan sizes this one wider than usual.
        float width = level.LevelBounds.Size.X;
        AssertThat(width).IsEqual(Level11Controller.LevelWidth);
        AssertThat(width).IsGreaterEqual(10000f);
        AssertThat(width).IsLessEqual(12000f);
    }

    // === Era identity 1: the cover / artillery beat ===

    [TestCase]
    public void TheSceneArtilleryMatchesTheAuthoredImpactTable() {
        using var fixture = new GettysburgFixture();

        var authored = new List<float>(Level11Controller.ArtilleryImpactPoints);
        authored.Sort();

        var placed = new List<float>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (Node child in Children(fixture.Level)) {
            if (child is not StoryCyclicHazard hazard) continue;
            placed.Add(hazard.Position.X);
            AssertThat(ids.Add(hazard.HazardID)).OverrideFailureMessage(
                $"Duplicate hazard id '{hazard.HazardID}'.").IsTrue();
            AssertThat(hazard.HazardID.StartsWith("level_11.")).IsTrue();
            AssertThat(Mathf.IsEqualApprox(hazard.Position.Y, Level11Controller.GroundY))
                .OverrideFailureMessage("Artillery columns stand on the advance line.").IsTrue();
        }
        placed.Sort();

        AssertThat(placed.Count).OverrideFailureMessage(
            "The scene's artillery no longer matches Level11Controller.ArtilleryImpactPoints. " +
            "The controller table is what the cover invariant is checked against, so the two " +
            "must not drift.").IsEqual(authored.Count);
        for (int index = 0; index < authored.Count; index++) {
            AssertThat(Mathf.IsEqualApprox(placed[index], authored[index])).OverrideFailureMessage(
                $"Artillery column {index} is at x={placed[index]}, table says {authored[index]}.")
                .IsTrue();
        }
    }

    [TestCase]
    public void TheArtilleryTelegraphsForTwoSecondsAndNeverStrikesDuringTheWarning() {
        using var fixture = new GettysburgFixture();
        StoryCyclicHazard hazard = FirstHazard(fixture.Level);
        AssertObject(hazard).IsNotNull();

        // Every lane is authored with a readable telegraph; the design quantifies ~2 s.
        foreach (StoryCyclicHazard lane in AllHazards(fixture.Level)) {
            AssertThat(lane.WarningDuration).OverrideFailureMessage(
                $"'{lane.HazardID}' warns for only {lane.WarningDuration}s. Artillery the " +
                "player cannot react to turns the advance into memorisation.")
                .IsGreaterEqual(Level11Controller.MinTelegraphSeconds);
            AssertThat(lane.ActiveDuration).IsGreater(0f);
            AssertThat(lane.CooldownDuration).IsGreater(lane.ActiveDuration);
            AssertThat(lane.Phase).IsEqual(HazardPhase.Cooldown);
        }

        // Drive the real cycle: cooldown -> warning -> active, never cooldown -> active.
        const double step = 1.0 / 60.0;
        hazard.ForcePhase(HazardPhase.Cooldown, 0.05f);
        hazard._PhysicsProcess(0.1);
        AssertThat(hazard.Phase).OverrideFailureMessage(
            "A shell fired straight out of cooldown with no warning phase.")
            .IsEqual(HazardPhase.Warning);

        float warned = 0f;
        int guard = 0;
        while (hazard.Phase == HazardPhase.Warning && guard++ < 1200) {
            hazard._PhysicsProcess(step);
            warned += (float)step;
        }
        AssertThat(hazard.Phase).IsEqual(HazardPhase.Active);
        AssertThat(warned).OverrideFailureMessage(
            $"The telegraph only lasted {warned}s of real cycle time.")
            .IsGreaterEqual(Level11Controller.MinTelegraphSeconds);

        // And the telegraph must be harmless: a player standing on the impact point
        // takes damage when the shell lands, never while it is being aimed.
        hazard.ForcePhase(HazardPhase.Warning, hazard.WarningDuration);
        AssertThat(hazard.ApplyToPlayer(fixture.Level.Player)).OverrideFailureMessage(
            "The warning phase dealt damage; the telegraph has to be a free read.")
            .IsFalse();
        hazard.ForcePhase(HazardPhase.Active, hazard.ActiveDuration);
        AssertThat(hazard.ApplyToPlayer(fixture.Level.Player)).OverrideFailureMessage(
            "The active phase dealt no damage; the barrage is decorative.").IsTrue();
    }

    [TestCase]
    public void EveryArtilleryGapHoldsCoverAndNoCoverStandsInsideABlastColumn() {
        // The rhythm the design asks for is advance -> take cover -> advance. That only
        // works if there is somewhere to stand between every pair of shell columns.
        IReadOnlyList<(float From, float To)> uncovered = Level11Controller.UncoveredArtilleryGaps();
        var report = new List<string>();
        foreach ((float from, float to) in uncovered) report.Add($"x {from}..{to}");
        AssertThat(uncovered.Count).OverrideFailureMessage(
            "Artillery gaps with no cover in them: " + string.Join(", ", report) +
            ". A gap with no cover is a gauntlet, not a beat.").IsEqual(0);

        foreach ((float x, Level11Controller.CoverKind kind) in Level11Controller.CoverPositions) {
            AssertThat(Level11Controller.IsInsideBlastColumn(x)).OverrideFailureMessage(
                $"Cover ({kind}) at x={x} stands inside a blast column - it is a trap, not cover.")
                .IsFalse();
            // The whole piece has to be clear, not just its centre.
            (float width, float _, Color _) = Level11Controller.CoverProfile(kind);
            AssertThat(Level11Controller.IsInsideBlastColumn(x - width / 2f)).IsFalse();
            AssertThat(Level11Controller.IsInsideBlastColumn(x + width / 2f)).IsFalse();
        }

        // A resume must never drop the player under a live shell.
        foreach (float checkpointX in new[] { 200f, 6250f, 9060f }) {
            AssertThat(Level11Controller.IsInsideBlastColumn(checkpointX)).OverrideFailureMessage(
                $"A checkpoint at x={checkpointX} respawns the player inside a blast column.")
                .IsFalse();
        }
    }

    [TestCase]
    public void TheAuthoredCoverIsRealSolidGeometryOnTheAdvanceLine() {
        using var fixture = new GettysburgFixture();

        var placed = new List<float>();
        foreach (Node child in Children(fixture.Level)) {
            if (child is StaticBody2D body && body.IsInGroup("battlefield_cover")) placed.Add(body.Position.X);
        }
        AssertThat(placed.Count).OverrideFailureMessage(
            "Cover is authored from Level11Controller.CoverPositions and must all be built.")
            .IsEqual(Level11Controller.CoverPositions.Length);
        AssertThat(placed.Count).IsGreaterEqual(Level11Controller.ArtilleryImpactPoints.Length);
    }

    // === Era identity 2: shielding arrays, and how they differ from Orléans ===

    [TestCase]
    public void DestroyingBothShieldArraysOpensTheUnionAdvanceAndTheBreastwork() {
        using var fixture = new GettysburgFixture();
        Level11Controller level = fixture.Level;

        AssertObject(level.ShieldArrayAlpha).OverrideFailureMessage(
            "Shielding array Alpha is missing from the scene.").IsNotNull();
        AssertObject(level.ShieldArrayBravo).OverrideFailureMessage(
            "Shielding array Bravo is missing from the scene.").IsNotNull();
        AssertThat(level.ArraysDestroyed).IsEqual(0);
        AssertThat(level.UnionAdvanceOpen).IsFalse();
        AssertThat(level.BreastworkOpen).OverrideFailureMessage(
            "The railcut must be sealed until the Union line takes the breastwork.").IsFalse();

        level.ShieldArrayAlpha.TakeEnvironmentDamage(level.ShieldArrayAlpha.MaxHP);
        AssertThat(level.ShieldArrayAlpha.IsDestroyed).IsTrue();
        AssertThat(level.ShieldArrayAlpha.ActiveBarrierCount).OverrideFailureMessage(
            "Breaking an array must drop the pane it holds up.").IsEqual(0);
        AssertThat(level.ArraysDestroyed).IsEqual(1);
        AssertThat(level.UnionAdvanceOpen).OverrideFailureMessage(
            "One array is not the objective; both have to fall.").IsFalse();
        AssertThat(level.BreastworkOpen).IsFalse();

        level.ShieldArrayBravo.TakeEnvironmentDamage(level.ShieldArrayBravo.MaxHP);
        AssertThat(level.ShieldArrayBravo.ActiveBarrierCount).IsEqual(0);
        AssertThat(level.ArraysDestroyed).IsEqual(Level11Controller.ArrayCount);
        AssertThat(level.UnionAdvanceOpen).OverrideFailureMessage(
            "Both arrays are down but the objective did not advance.").IsTrue();
        AssertThat(level.BreastworkOpen).OverrideFailureMessage(
            "Both arrays are down but the breastwork still seals the railcut.").IsTrue();
    }

    [TestCase]
    public void ThePanesSealTheRaisedUnionLaneAndNeverThePlayersOwnAdvance() {
        using var fixture = new GettysburgFixture();
        Level11Controller level = fixture.Level;

        ShieldGeneratorTower[] arrays = { level.ShieldArrayAlpha, level.ShieldArrayBravo };
        float[] paneX = { Level11Controller.LaneAlphaPaneX, Level11Controller.LaneBravoPaneX };
        float[] anchors = { Level11Controller.ArrayAlphaX, Level11Controller.ArrayBravoX };

        for (int index = 0; index < arrays.Length; index++) {
            ShieldGeneratorTower array = arrays[index];
            // The template's pane hangs at a fixed local offset, so the array has to
            // stand exactly that far west of the lane it seals (the L02 constraint).
            AssertThat(Mathf.IsEqualApprox(array.Position.X, anchors[index])).OverrideFailureMessage(
                $"Array {index} is at x={array.Position.X}; its pane only lands on lane " +
                $"x={paneX[index]} when it stands at x={anchors[index]}.").IsTrue();
            AssertThat(Mathf.IsEqualApprox(array.Position.Y, Level11Controller.ArrayY))
                .OverrideFailureMessage(
                    $"Array {index} is not standing on the gallery deck.").IsTrue();
            AssertThat(array.ActiveBarrierCount).IsEqual(1);

            foreach (ForcefieldBarrier pane in array.Barriers()) {
                AssertThat(Mathf.IsEqualApprox(pane.GlobalPosition.X, paneX[index])).IsTrue();
                AssertThat(Mathf.IsEqualApprox(pane.GlobalPosition.Y, Level11Controller.BarrierCenterY))
                    .IsTrue();
                AssertThat(pane.CollisionLayer).IsEqual(CollisionLayers.Environment);
            }
        }

        // The seal is real in both directions: flush with the gallery deck below and
        // with the earthwork roof mass above, so the lane cannot simply be jumped.
        AssertThat(Mathf.IsEqualApprox(Level11Controller.LaneSealBottomY, Level11Controller.CrestTopY))
            .OverrideFailureMessage("The pane does not reach the gallery deck.").IsTrue();
        AssertThat(Level11Controller.LaneRoofBottomY).IsGreater(0f);

        // ...and this is the whole difference from Orléans, whose barriers straddle the
        // walking surface so the player must break them to pass. Here the arrays are an
        // offensive objective: nothing they hold up ever intrudes on the ground route.
        float advanceCeiling = Level11Controller.GroundY - Level11Controller.PlayerAdvanceHeadroom;
        AssertThat(Level11Controller.LaneSealBottomY).OverrideFailureMessage(
            $"A shield pane reaches down to y={Level11Controller.LaneSealBottomY}, inside the " +
            $"player's advance band (y {advanceCeiling}..{Level11Controller.GroundY}). That makes " +
            "Level 11 a copy of the Orléans gate instead of an objective under fire.")
            .IsLess(advanceCeiling);

        // The gallery also has to clear the tallest cover the player stands on below it.
        float galleryUnderside = Level11Controller.CrestTopY + 32f;
        float tallestCoverTop = Level11Controller.GroundY - Level11Controller.TallestCoverHeight;
        AssertThat(tallestCoverTop - galleryUnderside).OverrideFailureMessage(
            "The Union gallery hangs too low over the cover line.").IsGreaterEqual(120f);
    }

    [TestCase]
    public void ResumingPastTheBreastworkNeverResealsTheRailcut() {
        // Checkpoint 2 stands east of the breastwork. A resume that lost the array state
        // would strand the player in the boss arena with the objective behind a wall.
        using var fixture = new GettysburgFixture(Level11Controller.Checkpoint2);
        Level11Controller level = fixture.Level;

        AssertThat(level.ResumedMidLevel).IsTrue();
        AssertString(level.ResumedCheckpointID).IsEqual(Level11Controller.Checkpoint2);
        AssertThat(level.ArraysDestroyed).OverrideFailureMessage(
            "A pre-boss resume must agree with a run that really broke both arrays.")
            .IsEqual(Level11Controller.ArrayCount);
        AssertThat(level.ShieldArrayAlpha.IsDestroyed).IsTrue();
        AssertThat(level.ShieldArrayBravo.IsDestroyed).IsTrue();
        AssertThat(level.ShieldArrayAlpha.ActiveBarrierCount).IsEqual(0);
        AssertThat(level.ShieldArrayBravo.ActiveBarrierCount).IsEqual(0);
        AssertThat(level.UnionAdvanceOpen).IsTrue();
        AssertThat(level.BreastworkOpen).OverrideFailureMessage(
            "The breastwork re-sealed behind a checkpoint-2 resume.").IsTrue();

        // Resuming at the midpoint must NOT hand the arrays over for free.
        using var midpoint = new GettysburgFixture(Level11Controller.Checkpoint1);
        AssertThat(midpoint.Level.ArraysDestroyed).IsEqual(0);
        AssertThat(midpoint.Level.BreastworkOpen).IsFalse();
    }

    // === Locked encounter economy ===

    [TestCase]
    public void TheAuthoredEncounterTableMatchesTheLockedEconomyRow() {
        // docs/DUST_ECONOMY.md / DustEconomyTests row 11: 12 standards, 1 elite,
        // 1 boss, 3 extractors. Locked; content conforms to it.
        AssertThat(Level11Controller.AuthoredStandardCount).OverrideFailureMessage(
            "Level 11 authors exactly 12 standard enemies at Normal.").IsEqual(12);
        AssertThat(Level11Controller.AuthoredEliteCount).OverrideFailureMessage(
            "Level 11 authors exactly one elite.").IsEqual(1);
        AssertThat(Level11Controller.AuthoredExtractorCount).IsEqual(3);

        var standardRoster = new HashSet<string>(StringComparer.Ordinal) {
            "laser_rifle_infantry", "chrono_slasher"
        };
        foreach ((string enemyID, Vector2 _, Vector2? _, Vector2? _) in AllStandardSpawns()) {
            AssertThat(standardRoster.Contains(enemyID)).OverrideFailureMessage(
                $"'{enemyID}' is not on the Gettysburg standard roster " +
                "(laser_rifle_infantry + chrono_slasher).").IsTrue();
            EnemyData data = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertObject(data).IsNotNull();
            AssertThat(data.Tier).OverrideFailureMessage(
                $"'{enemyID}' must be Standard tier.").IsEqual(EnemyTier.Standard);
        }

        foreach ((string enemyID, Vector2 _, Vector2? _, Vector2? _) in Level11Controller.EliteSpawns) {
            AssertString(enemyID).IsEqual("cyber_cavalry_commander");
            EnemyData data = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertObject(data).IsNotNull();
            AssertThat(data.Tier).IsEqual(EnemyTier.Elite);
        }

        var extractorIDs = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string id, Vector2 _) in Level11Controller.ExtractorPlacements) {
            AssertThat(extractorIDs.Add(id)).OverrideFailureMessage($"Duplicate extractor id '{id}'.").IsTrue();
        }

        // This is a firefight level - the mix has to lean on the ranged era standard,
        // which is why level_11 carries the campaign's highest enemy_projectile budget.
        int ranged = 0;
        foreach ((string enemyID, Vector2 _, Vector2? _, Vector2? _) in AllStandardSpawns()) {
            if (enemyID == "laser_rifle_infantry") ranged++;
        }
        AssertThat(ranged).OverrideFailureMessage(
            "Most of the Gettysburg standards should be the ranged era enemy.").IsGreaterEqual(7);
    }

    [TestCase]
    public void ThePlacedExtractorsAndBossEncounterMatchTheAuthoredTables() {
        using var fixture = new GettysburgFixture();
        Level11Controller level = fixture.Level;

        AssertThat(level.Extractors.Count).IsEqual(Level11Controller.AuthoredExtractorCount);
        foreach (ChronalExtractor extractor in level.Extractors) {
            // 15 dust each is resource-owned (docs/DUST_ECONOMY.md).
            AssertThat(extractor.DustReward).IsEqual(3);
        }

        // Two of the three sit on the raised lane beyond an array's pane, so sabotage pays.
        int gated = 0;
        foreach ((string _, Vector2 position) in Level11Controller.ExtractorPlacements) {
            if (position.X > Level11Controller.LaneAlphaPaneX &&
                position.X < Level11Controller.LaneAlphaPaneX + 800f) gated++;
            else if (position.X > Level11Controller.LaneBravoPaneX &&
                position.X < Level11Controller.LaneBravoPaneX + 800f) gated++;
        }
        AssertThat(gated).OverrideFailureMessage(
            "The Union-lane extractors should sit beyond the panes the arrays hold up.")
            .IsEqual(2);

        AssertThat(level.BossEncounters.Count).IsEqual(1);
        AssertString(level.BossEncounters[0].Data.BossID).IsEqual("siege_cannon");

        // Peak concurrency against the level_11 pool config warm counts (14 / 4).
        int largestStandardWave = Math.Max(Level11Controller.Room1Spawns.Length,
            Math.Max(Level11Controller.Room2Spawns.Length,
                Math.Max(Level11Controller.Room3Spawns.Length, Level11Controller.AngleSpawns.Length)));
        AssertThat(largestStandardWave).IsLessEqual(14);
        AssertThat(Level11Controller.EliteSpawns.Length).IsLessEqual(4);
    }

    [TestCase]
    public void TheSiegeCannonsRangedBandFitsTheRailcut() {
        BossData boss = AuthoredResources.Load<BossData>(Level11Controller.BossResourcePath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("siege_cannon");
        AssertThat(boss.PhaseThresholds.Length).OverrideFailureMessage(
            "The Siege Cannon is authored as a two-phase fight.").IsEqual(1);

        // Asserted against the resource, never a literal: re-tuning the boss must not be
        // able to silently outgrow the arena. 12 units is the widest band in the roster.
        float rangedPixels = boss.RangedRangeThreshold * PixelsPerUnit;
        float arenaWidth = Level11Controller.LevelWidth - Level11Controller.Room4StartX;
        AssertThat(arenaWidth).OverrideFailureMessage(
            $"The railcut is {arenaWidth} px but the Siege Cannon's ranged band alone needs " +
            $"{rangedPixels} px, and a railcar boss that cannot use its kit is a melee fight.")
            .IsGreater(rangedPixels * 2f);
        AssertThat(boss.MeleeRangeThreshold * PixelsPerUnit).IsLess(arenaWidth);
    }

    [TestCase]
    public void TheCavalryCommandersPostIsFlankableFromAbove() {
        // cyber_cavalry_commander carries FrontalDamageReduction 0.4, so a post with no
        // way to get behind it turns the level's only elite into a damage sponge.
        EnemyData data = AuthoredResources.Load<EnemyData>(
            "res://resources/Enemies/cyber_cavalry_commander.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.FrontalDamageReduction).IsGreater(0f);

        (string _, Vector2 position, Vector2? patrolA, Vector2? patrolB) = Level11Controller.EliteSpawns[0];
        AssertThat(patrolA.HasValue && patrolB.HasValue).OverrideFailureMessage(
            "The commander must patrol, or its back is never turned.").IsTrue();
        AssertThat(Mathf.Abs(patrolB.Value.X - patrolA.Value.X)).OverrideFailureMessage(
            "The commander's patrol collapsed to a point.").IsGreater(120f);

        float platformLeft = Level11Controller.FlankPlatformX - Level11Controller.FlankPlatformWidth / 2f;
        float platformRight = Level11Controller.FlankPlatformX + Level11Controller.FlankPlatformWidth / 2f;
        AssertThat(patrolA.Value.X).OverrideFailureMessage(
            "The drop-through flank platform does not span the commander's patrol.")
            .IsGreaterEqual(platformLeft);
        AssertThat(patrolB.Value.X).IsLessEqual(platformRight);
        AssertThat(position.X).IsBetween(platformLeft, platformRight);

        using var fixture = new GettysburgFixture();
        bool found = false;
        foreach (Node child in Children(fixture.Level)) {
            if (child is OneWayPlatform platform &&
                Mathf.IsEqualApprox(platform.Position.X, Level11Controller.FlankPlatformX) &&
                Mathf.IsEqualApprox(platform.Position.Y, Level11Controller.FlankPlatformY)) {
                found = true;
                break;
            }
        }
        AssertThat(found).OverrideFailureMessage(
            "The flank platform over the commander's post is missing from the built level.")
            .IsTrue();
    }

    // === Localization ===

    [TestCase]
    public void TheDialogueSetResolvesAndEveryAuthoredKeyIsLocalized() {
        var set = AuthoredResources.Load<DialogueSetData>(Level11Controller.DialogueResourcePath);
        AssertObject(set).IsNotNull();
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_11");
        // Package 13 W4 (S30): four base beats (the silent-telegraph absence added)
        // plus the two N03 Lincoln variants (absence-slot recognition and exit).
        AssertThat(set.Sequences.Length).IsEqual(6);
        foreach (string variant in new[] { "level_11.absence@lincoln", "level_11.exit@lincoln" }) {
            DialogueSequenceData branch = set.Find(variant);
            AssertObject(branch).OverrideFailureMessage(
                $"The N03 recognition variant '{variant}' is missing.").IsNotNull();
            AssertString(branch.HeroConditionCharacterID).IsEqual("lincoln");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        TranslationServer.SetLocale("en");

        var required = new List<string> {
            "gettysburg_level_title", "gettysburg_room_seminary_ridge", "gettysburg_room_wheatfield",
            "gettysburg_room_angle", "gettysburg_room_railcut", "gettysburg_objective_advance",
            "gettysburg_objective_arrays", "gettysburg_objective_breach",
            "gettysburg_objective_defeat_boss", "gettysburg_objective_complete",
            "gettysburg_lane_shielded", "gettysburg_gate_sealed"
        };

        foreach (string sequenceID in new[] {
            "level_11.entrance", "level_11.absence", "level_11.boss_intro", "level_11.exit" }) {
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
            // Node.Tr() reads the compiled translation, not the CSV: if they drift, the
            // level renders raw keys. Re-run --headless --import after editing en.csv.
            AssertString(TranslationServer.Translate(key).ToString()).OverrideFailureMessage(
                $"'{key}' does not resolve through localization/en.en.translation. " +
                "Re-import localization/en.csv.").IsNotEqual(key);
        }
    }

    // === Helpers ===

    private static IEnumerable<(string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)> AllStandardSpawns() {
        foreach (var spawn in Level11Controller.Room1Spawns) yield return spawn;
        foreach (var spawn in Level11Controller.Room2Spawns) yield return spawn;
        foreach (var spawn in Level11Controller.Room3Spawns) yield return spawn;
        foreach (var spawn in Level11Controller.AngleSpawns) yield return spawn;
    }

    private static List<StoryCyclicHazard> AllHazards(Node level) {
        var hazards = new List<StoryCyclicHazard>();
        foreach (Node child in Children(level)) {
            if (child is StoryCyclicHazard hazard) hazards.Add(hazard);
        }
        return hazards;
    }

    private static StoryCyclicHazard FirstHazard(Node level) {
        List<StoryCyclicHazard> hazards = AllHazards(level);
        return hazards.Count > 0 ? hazards[0] : null;
    }

    private static List<Node> Children(Node parent) {
        var collected = new List<Node>();
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) collected.Add(child);
        return collected;
    }

    /// <summary>
    /// Instantiates the authored scene in the runner tree against a scratch save, then
    /// hands back every shared singleton it touched.
    ///
    /// The fixture always resumes at a checkpoint (default: the entry anchor). A fresh
    /// entry defers the entrance dialogue, which is authored <c>PausesGameplay = true</c>;
    /// if that deferred call lands it leaks <c>SceneTree.Paused</c>, which freezes
    /// GdUnit's own transport node and hangs the session (CLAUDE.md failure signature 4).
    /// The pause flag is sampled and restored anyway, belt and braces.
    /// </summary>
    private sealed class GettysburgFixture : IDisposable {
        public readonly Level11Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public GettysburgFixture() : this(Level11Controller.Checkpoint0) { }

        public GettysburgFixture(string resumeCheckpointID) {
            var tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            SaveManager.Instance.SaveSlots[ScratchSlot] = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Gettysburg),
                LastCheckpointID = resumeCheckpointID,
                CurrentHP = 100,
                CurrentUltimateMeter = 50f
            };

            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level11Controller>();
            tree.Root.AddChild(Level);
        }

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
