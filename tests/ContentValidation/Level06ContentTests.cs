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
/// Package 5 Wave B: content contracts for Level 6 - Pompeii, 79 AD.
/// <para>
/// Covers the per-level gate from docs/PACKAGE5_CAMPAIGN_PLAN.md §5: the scene
/// resolves at the exact <see cref="StoryManager"/> path, the controller reports the
/// manifest level id and its three checkpoints, the dialogue set and every authored
/// line key are localized, the authored encounter table matches the locked
/// 10/0/1/3 economy row, the Vulcan Decimator's authored ranged band fits the caldera
/// arena, and all three era mechanics are driven for real: the lava front hurts
/// without killing, the counterweight winch balances and hauls the rockfall clear,
/// and both civilians can be evacuated.
/// </para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level06ContentTests {
    private const string ScenePath = "res://scenes/campaign/Level_06_Pompeii.tscn";
    private const string LevelID = "level_06_pompeii";
    private const string DialoguePath = "res://resources/Dialogue/level_06_dialogue.tres";
    private const string BossPath = "res://resources/Bosses/vulcan_decimator.tres";

    /// <summary>Story world units to pixels, matching BossController.PixelsPerUnit.</summary>
    private const float PixelsPerUnit = 60f;

    private const int ScratchSlot = 2;

    // === Scene and identity ===

    [TestCase]
    public void ThePompeiiSceneLoadsInstantiatesAndFreesCleanly() {
        // StoryManager must route here; a renamed scene silently breaks the campaign.
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.Pompeii)).IsEqual(ScenePath);

        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).OverrideFailureMessage($"{ScenePath} did not load.").IsNotNull();

        var instance = scene.Instantiate<Level06Controller>();
        AssertObject(instance).IsNotNull();
        AssertString(instance.LevelID).IsEqual(LevelID);
        instance.Free();
    }

    [TestCase]
    public void TheControllerReportsTheManifestIdentityAndRegistersExactlyThreeCheckpoints() {
        using var fixture = new PompeiiFixture();
        Level06Controller level = fixture.Level;

        AssertString(level.LevelID).IsEqual(LevelID);
        AssertThat(level.Level).IsEqual(CampaignLevel.Pompeii);
        AssertString(level.LevelTitleKey).IsEqual("pompeii_level_title");
        AssertString(level.DialogueSetPath).IsEqual(DialoguePath);
        AssertString(level.DialoguePrefix).IsEqual("level_06");
        AssertString(level.EntranceDialogueID).IsEqual("level_06.entrance");
        AssertString(level.BossIntroDialogueID).IsEqual("level_06.boss_intro");
        AssertString(level.ExitDialogueID).IsEqual("level_06.exit");

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
    public void TheLevelIsFiveConfinedRoomsInsideTheAuthoredBounds() {
        using var fixture = new PompeiiFixture();
        Level06Controller level = fixture.Level;

        AssertThat(Mathf.IsEqualApprox(level.LevelBounds.Size.X, Level06Controller.LevelWidth)).IsTrue();
        // Plan §5: 8000-14000 px; the agent brief narrows Pompeii to 9000-11000.
        AssertThat(level.LevelBounds.Size.X >= 9000f && level.LevelBounds.Size.X <= 11000f).IsTrue();
        AssertThat(level.RoomTriggers.Count).IsEqual(5);

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

        // The vertical section: the villa terraces must actually climb.
        var oneWays = new List<OneWayPlatform>();
        foreach (Node child in level.GetChildren()) {
            if (child is OneWayPlatform platform) oneWays.Add(platform);
        }
        AssertThat(oneWays.Count).IsGreater(6);
        float highest = 1080f;
        foreach (OneWayPlatform platform in oneWays) highest = Mathf.Min(highest, platform.Position.Y);
        AssertThat(highest < 400f)
            .OverrideFailureMessage($"Highest drop-through platform is at y={highest}; no real vertical section.")
            .IsTrue();
    }

    // === Dialogue and localization ===

    [TestCase]
    public void TheDialogueSetResolvesAndEveryAuthoredKeyIsInTheEnglishTable() {
        var dialogue = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        AssertObject(dialogue).OverrideFailureMessage($"{DialoguePath} did not load.").IsNotNull();
        AssertString(dialogue.DialogueSetID).IsEqual("dialogue_level_06");

        HashSet<string> keys = EnglishKeys();
        // Package 13 W4 (S37): Pliny's absence beat plays for every hero.
        string[] expectedSequences = { "level_06.entrance", "level_06.absence", "level_06.boss_intro", "level_06.exit" };
        AssertThat(dialogue.Sequences.Length).IsEqual(expectedSequences.Length);

        foreach (string dialogueID in expectedSequences) {
            DialogueSequenceData sequence = dialogue.Find(dialogueID);
            AssertObject(sequence).OverrideFailureMessage($"Sequence '{dialogueID}' missing.").IsNotNull();
            AssertThat(sequence.LineCount).IsGreater(0);
            AssertThat(sequence.LineKeys.Length).IsEqual(sequence.SpeakerNameKeys.Length);

            for (int index = 0; index < sequence.LineCount; index++) {
                string lineKey = sequence.GetLineKey(index);
                AssertThat(lineKey.StartsWith("dlg_l06_", StringComparison.Ordinal))
                    .OverrideFailureMessage($"'{lineKey}' does not follow the dlg_lNN_ convention.").IsTrue();
                AssertThat(keys.Contains(lineKey))
                    .OverrideFailureMessage($"Line key '{lineKey}' is not in localization/en.csv.").IsTrue();
                AssertThat(keys.Contains(sequence.GetSpeakerKey(index)))
                    .OverrideFailureMessage($"Speaker key '{sequence.GetSpeakerKey(index)}' is not in localization/en.csv.").IsTrue();
            }
        }
    }

    [TestCase]
    public void EveryVisiblePompeiiStringHasAnEnglishEntry() {
        HashSet<string> keys = EnglishKeys();
        string[] required = {
            "pompeii_level_title",
            "pompeii_room_forum", "pompeii_room_vault", "pompeii_room_ashroad", "pompeii_room_caldera",
            "pompeii_objective_evacuate", "pompeii_objective_rockfall", "pompeii_objective_ashroad",
            "pompeii_objective_outrun", "pompeii_objective_caught", "pompeii_objective_anchor",
            "pompeii_objective_civilians", "pompeii_objective_defeat_boss", "pompeii_objective_complete",
            "pompeii_rockfall_blocked", "speaker_vulcan_decimator",
            // Reused shared keys the level depends on.
            "interaction_rescue", "checkpoint", "campaign_level_pompeii",
            "speaker_player", "speaker_sarah"
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

    // === Encounter economy: 10 standards / 1 elite (V7.6) / 1 boss / 3 extractors ===

    [TestCase]
    public void TheAuthoredSpawnTableIsTenStandardsAndTheChronoWardenDebut() {
        var byTier = new Dictionary<EnemyTier, int>();
        var byID = new Dictionary<string, int>(StringComparer.Ordinal);
        int total = 0;

        foreach ((string enemyID, Vector2 _, Vector2 _, Vector2 _) in Level06Controller.AllStandardSpawns) {
            total++;
            EnemyData data = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertObject(data).OverrideFailureMessage($"Enemy resource missing for '{enemyID}'.").IsNotNull();
            AssertString(data.EnemyID).IsEqual(enemyID);
            byTier[data.Tier] = byTier.GetValueOrDefault(data.Tier) + 1;
            byID[enemyID] = byID.GetValueOrDefault(enemyID) + 1;
        }

        // docs/DUST_ECONOMY.md level 6 row was S=10, E=0. V7.6 (Package 11 A7a)
        // places the Chrono-Warden here, which is the enemy's authored DEBUT under
        // V7.1 design law ("first appearance Level 6, then salted through Levels
        // 7-15") - a law that had never been executed: the resource, its three
        // ability kits and its tests all existed with zero spawn sites anywhere in
        // the campaign (recon G8). The elite row moves 0 -> 1; F05 (A10) retires
        // the flat per-tier award this row encoded in the same wave.
        AssertThat(total).IsEqual(11);
        AssertThat(Level06Controller.AuthoredStandardCount).IsEqual(10);
        AssertThat(Level06Controller.AuthoredEliteCount).IsEqual(1);
        AssertThat(byTier.GetValueOrDefault(EnemyTier.Standard)).IsEqual(10);
        AssertThat(byTier.GetValueOrDefault(EnemyTier.Elite)).IsEqual(1);

        // Era roster: the Pompeii standard, the cultist standard, and the Warden.
        // No Eraser: the two elites may not share a room before Act III.
        AssertThat(byID.Count).IsEqual(3);
        AssertThat(byID["shock_shield_legionnaire"]).IsEqual(6);
        AssertThat(byID["chrono_slasher"]).IsEqual(4);
        AssertThat(byID[Level06Controller.WardenEnemyID]).IsEqual(1);

        // Per-wave concurrency must fit level_06_pool_config's standard_enemy warm
        // 12 and elite_enemy warm 3.
        AssertThat(Level06Controller.Room1Spawns.Length).IsEqual(3);
        AssertThat(Level06Controller.Room2Spawns.Length).IsEqual(3);
        AssertThat(Level06Controller.Room3Spawns.Length).IsEqual(5);
    }

    [TestCase]
    public void EveryLegionnairePostIsFlankableFromAbove() {
        // shock_shield_legionnaire was a frontal-shield mob when these posts were
        // authored; it is Armoured since the gameplay review (2b+), and a throw is the
        // answer. Every post is still under a drop-through platform with a patrol
        // that turns its back.
        var legionnaire = AuthoredResources.Load<EnemyData>("res://resources/Enemies/shock_shield_legionnaire.tres");
        AssertObject(legionnaire).IsNotNull();
        AssertThat(legionnaire.Trait).IsEqual(FTT.Combat.EnemyTrait.Armoured);

        using var fixture = new PompeiiFixture();
        var oneWays = new List<OneWayPlatform>();
        foreach (Node child in fixture.Level.GetChildren()) {
            if (child is OneWayPlatform platform) oneWays.Add(platform);
        }

        foreach ((string enemyID, Vector2 position, Vector2 patrolA, Vector2 patrolB)
                 in Level06Controller.AllStandardSpawns) {
            if (enemyID != "shock_shield_legionnaire") continue;

            AssertThat(!patrolA.IsEqualApprox(patrolB))
                .OverrideFailureMessage($"Legionnaire at {position} never turns; it cannot be flanked.").IsTrue();

            bool covered = false;
            foreach (OneWayPlatform platform in oneWays) {
                if (platform.Position.Y >= position.Y) continue;
                if (Mathf.Abs(platform.Position.X - position.X) <= 220f) { covered = true; break; }
            }
            AssertThat(covered).OverrideFailureMessage(
                $"Legionnaire post at {position} has no drop-through platform overhead.").IsTrue();
        }
    }

    [TestCase]
    public void ThreeChronalExtractorsArePlacedFromTheAuthoredTable() {
        AssertThat(Level06Controller.ExtractorPlacements.Length).IsEqual(3);
        AssertThat(Level06Controller.AuthoredExtractorCount).IsEqual(3);

        using var fixture = new PompeiiFixture();
        AssertThat(fixture.Level.Extractors.Count).IsEqual(3);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ChronalExtractor extractor in fixture.Level.Extractors) {
            AssertThat(extractor.ObjectID.StartsWith("level_06.", StringComparison.Ordinal)).IsTrue();
            AssertThat(ids.Add(extractor.ObjectID))
                .OverrideFailureMessage($"Duplicate extractor id '{extractor.ObjectID}'.").IsTrue();
            // Dust stays resource-owned at the locked 15; level code must not override it.
            AssertThat(extractor.DustReward).IsEqual(3);
        }
    }

    [TestCase]
    public void TheVulcanDecimatorIsWiredAndItsRangeBandFitsTheCalderaArena() {
        var boss = AuthoredResources.Load<BossData>(BossPath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("vulcan_decimator");
        AssertThat(boss.ChronalDustDrop).IsEqual(25);

        using var fixture = new PompeiiFixture();
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertThat(encounter.Data == boss)
            .OverrideFailureMessage("The encounter must reuse the pinned authored BossData instance.").IsTrue();

        // Asserted against the resource, never a hardcoded width: re-tuning the boss
        // then cannot silently outgrow the arena.
        Rect2 arena = fixture.Level.RoomTriggers[4].CameraBounds;
        AssertThat(arena.Size.X > boss.RangedRangeThreshold * PixelsPerUnit)
            .OverrideFailureMessage(
                $"Caldera arena is {arena.Size.X} px but the boss ranged band is " +
                $"{boss.RangedRangeThreshold * PixelsPerUnit} px.").IsTrue();
        AssertThat(arena.HasPoint(encounter.Position))
            .OverrideFailureMessage("The boss anchor sits outside the caldera camera bounds.").IsTrue();
    }

    // === Era mechanic 1: the advancing lava front ===

    [TestCase]
    public void TheLavaFrontHurtsAndKnocksTheRunnerForwardWithoutKillingOutright() {
        using var fixture = new PompeiiFixture($"{LevelID}_checkpoint_1");
        Level06Controller level = fixture.Level;

        EscapeSequenceController escape = level.Escape;
        AssertObject(escape).OverrideFailureMessage("The Ash Road escape sequence is not authored.").IsNotNull();
        AssertString(escape.SequenceID).IsEqual("level_06.lava_front");
        AssertThat(escape.AutoBegin).IsFalse();
        AssertThat(escape.IsRunning).IsFalse();

        // The front must stop short of the boss arena and behind the finish line.
        AssertFloat(escape.StartX).IsEqualApprox(Level06Controller.EscapeStartX, 0.001f);
        AssertFloat(escape.EndX).IsEqualApprox(Level06Controller.EscapeEndX, 0.001f);
        AssertFloat(escape.FinishX).IsEqualApprox(Level06Controller.EscapeFinishX, 0.001f);
        AssertThat(escape.EndX < escape.FinishX).IsTrue();
        AssertThat(escape.FinishX < Level06Controller.Room4CameraBounds.End.X).IsTrue();

        // Entering the Ash Road starts the run and the front actually advances.
        AssertThat(level.BeginEscape()).IsTrue();
        AssertThat(level.EscapeTriggered).IsTrue();
        AssertThat(escape.IsRunning).IsTrue();
        float before = escape.FrontX;
        escape.AdvanceFront(1f);
        AssertFloat(escape.FrontX).IsEqualApprox(before + escape.AdvanceSpeed, 0.5f);

        // Being caught is heavy damage plus forward knockback - never an instant
        // kill, because Chronal Rewind is the level's failure state.
        var player = level.Player;
        player.RestoreStoryCheckpoint(new Vector2(escape.FrontX, 850f), player.MaximumHP, 0f);
        player.Velocity = Vector2.Zero;
        int hpBefore = player.CurrentHP;

        AssertThat(escape.CatchDamage).IsGreater(0);
        AssertThat(escape.CatchDamage < player.MaximumHP)
            .OverrideFailureMessage("A single catch must not be able to kill outright.").IsTrue();

        AssertThat(escape.CatchPlayer(player)).IsTrue();
        AssertThat(player.CurrentHP).IsLess(hpBefore);
        AssertThat(player.CurrentHP).IsGreater(0);
        AssertThat(player.CurrentState != CharacterState.Dead)
            .OverrideFailureMessage("The lava front killed the player outright; rewind stops being meaningful.")
            .IsTrue();
        AssertThat(escape.CatchCount).IsEqual(1);
        AssertThat(player.Velocity.X > 0f)
            .OverrideFailureMessage("A caught runner must be knocked forward, along the escape route.").IsTrue();
        AssertThat(player.Velocity.Y < 0f).IsTrue();

        // The catch cooldown stops the front from shredding a stuck player.
        AssertThat(escape.CatchPlayer(player)).IsFalse();
        AssertThat(escape.CatchCount).IsEqual(1);

        // Crossing the finish line closes the run and halts the front.
        player.RestoreStoryCheckpoint(new Vector2(escape.FinishX + 40f, 850f), player.MaximumHP, 0f);
        AssertThat(escape.NotifyPlayerReachedFinish(player)).IsTrue();
        AssertThat(escape.IsCompleted).IsTrue();
        AssertThat(escape.IsRunning).IsFalse();
    }

    [TestCase]
    public void ResumingPastTheAshRoadNeverDropsThePlayerIntoALiveLavaFront() {
        using var fixture = new PompeiiFixture($"{LevelID}_checkpoint_2");
        Level06Controller level = fixture.Level;

        AssertThat(level.ResumedMidLevel).IsTrue();
        AssertString(level.ResumedCheckpointID).IsEqual($"{LevelID}_checkpoint_2");

        // The pre-boss checkpoint stands east of the run: the front is parked at its
        // start, halted, and the run is already closed.
        EscapeSequenceController escape = level.Escape;
        AssertThat(escape.IsRunning)
            .OverrideFailureMessage("A resume at the pre-boss checkpoint restarted the lava front.").IsFalse();
        AssertThat(escape.IsCompleted).IsTrue();
        AssertFloat(escape.FrontX).IsEqualApprox(escape.StartX, 0.001f);
        AssertThat(level.EscapeTriggered).IsTrue();

        // ...and the road behind the player is not re-sealed.
        AssertThat(level.RockfallCleared).IsTrue();
    }

    // === Era mechanic 2: the counterweight winch ===

    [TestCase]
    public void BalancingTheWinchHaulsTheRockfallOffTheRoad() {
        using var fixture = new PompeiiFixture();
        Level06Controller level = fixture.Level;

        AssertObject(level.RockfallPuzzle).IsNotNull();
        AssertString(level.RockfallPuzzle.PuzzleID).IsEqual(Level06Controller.RockfallPuzzleID);
        AssertThat(level.RockfallPuzzle.PersistCompletionToSave)
            .OverrideFailureMessage("The rockfall must persist or a checkpoint resume soft-locks.").IsTrue();
        AssertObject(level.Winch).IsNotNull();
        AssertObject(level.LeftPan).IsNotNull();
        AssertObject(level.RightPan).IsNotNull();
        AssertObject(level.WinchWeight).IsNotNull();
        AssertObject(level.WedgeLock).IsNotNull();

        // The basalt boulder is authored already resting in the west pan, so that
        // side reads its 3-unit threshold from the first frame.
        AssertFloat(level.BasaltBoulder.WeightUnits).IsEqualApprox(3f, 0.001f);
        AssertFloat(level.LeftPan.CurrentWeight).IsEqualApprox(3f, 0.001f);
        AssertThat(level.LeftPan.IsPressed).IsTrue();
        AssertThat(level.RightPan.IsPressed).IsFalse();
        AssertThat(level.RockfallPuzzle.IsCompleted).IsFalse();
        AssertThat(level.RockfallCleared).IsFalse();
        AssertObject(level.GetNodeOrNull<StaticBody2D>("Rockfall")).IsNotNull();

        // The pumice block is held by a destructible wedge on the gallery above the
        // east pan. Smashing it takes the authored hit count.
        AssertThat(level.WedgeLock.HitsToBreak).IsEqual(3);
        level.WedgeLock.TakeEnvironmentDamage(999f);
        AssertThat(level.WedgeLock.IsDestroyed).IsFalse();
        level.WedgeLock.TakeEnvironmentDamage(999f);
        level.WedgeLock.TakeEnvironmentDamage(999f);
        AssertThat(level.WedgeLock.IsDestroyed).IsTrue();

        // The block drops into the east pan: 2 units, one short of the threshold.
        AssertFloat(level.PumiceBoulder.WeightUnits).IsEqualApprox(2f, 0.001f);
        level.RightPan.RegisterBody(level.PumiceBoulder);
        AssertFloat(level.RightPan.CurrentWeight).IsEqualApprox(2f, 0.001f);
        AssertThat(level.RightPan.IsPressed).IsFalse();
        AssertThat(level.Winch.Evaluate()).IsFalse();
        AssertThat(level.RockfallCleared)
            .OverrideFailureMessage("An unbalanced winch opened the road.").IsFalse();

        // The missing unit is the player: standing on the pan balances the winch.
        AssertFloat(level.RightPan.PlayerWeight).IsEqualApprox(1f, 0.001f);
        level.RightPan.RegisterBody(level.Player);
        AssertFloat(level.RightPan.CurrentWeight).IsEqualApprox(3f, 0.001f);
        AssertThat(level.RightPan.IsPressed).IsTrue();

        AssertThat(level.RockfallPuzzle.IsConditionSatisfied(Level06Controller.BalanceConditionID)).IsTrue();
        AssertThat(level.RockfallPuzzle.IsCompleted).IsTrue();
        AssertThat(level.RockfallCleared).IsTrue();

        // The counterweight visibly rides the load.
        AssertFloat(level.WinchWeight.CurrentLoad).IsEqualApprox(6f, 0.001f);

        // The path is physically clear.
        var rockfall = level.GetNodeOrNull<StaticBody2D>("Rockfall");
        AssertThat(rockfall == null || rockfall.IsQueuedForDeletion())
            .OverrideFailureMessage("The rockfall is still solid after the winch balanced.").IsTrue();

        // Stepping back off cannot re-seal the road: completion latches.
        level.RightPan.UnregisterBody(level.Player);
        AssertThat(level.RightPan.IsPressed).IsFalse();
        AssertThat(level.RockfallPuzzle.IsCompleted).IsTrue();
        AssertThat(level.RockfallCleared).IsTrue();
    }

    [TestCase]
    public void ResumingPastTheVaultWithTheWinchAlreadySolvedDoesNotSoftLock() {
        // PuzzleManager re-emits a save-restored completion one deferred frame after
        // _Ready, so the gate opener has to be idempotent; the controller also forces
        // the haul from MarkWavesClearedThrough in case the save lost the flag.
        var save = PompeiiFixture.NewSave($"{LevelID}_checkpoint_1");
        save.CompletedPuzzleIDs.Add(Level06Controller.RockfallPuzzleID);

        using var fixture = new PompeiiFixture(save);
        Level06Controller level = fixture.Level;

        AssertThat(level.ResumedMidLevel).IsTrue();
        AssertThat(level.RockfallPuzzle.IsCompleted).IsTrue();
        AssertThat(level.RockfallCleared).IsTrue();
        var rockfall = level.GetNodeOrNull<StaticBody2D>("Rockfall");
        AssertThat(rockfall == null || rockfall.IsQueuedForDeletion())
            .OverrideFailureMessage("The rockfall is still solid after a completed resume.").IsTrue();

        // The Ash Road is still ahead of this checkpoint, so the front must be idle.
        AssertThat(level.EscapeTriggered).IsFalse();
        AssertThat(level.Escape.IsRunning).IsFalse();
        AssertThat(level.Escape.IsCompleted).IsFalse();
    }

    // === Era mechanic 3: the civilians ===

    [TestCase]
    public void BothRomanCiviliansCanBeEvacuated() {
        using var fixture = new PompeiiFixture();
        Level06Controller level = fixture.Level;

        AssertThat(level.Civilians.Count).IsEqual(2);
        AssertThat(level.CiviliansRescued).IsEqual(0);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (RescuableNPC civilian in level.Civilians) {
            AssertThat(civilian.NpcID.StartsWith("level_06.", StringComparison.Ordinal)).IsTrue();
            AssertThat(ids.Add(civilian.NpcID))
                .OverrideFailureMessage($"Duplicate civilian id '{civilian.NpcID}'.").IsTrue();
            AssertString(civilian.InteractionPromptKey).IsEqual("interaction_rescue");
            AssertThat(civilian.IsRescued).IsFalse();
            AssertThat(civilian.CanInteract(level.Player)).IsTrue();
        }

        // One in the Forum, one mid-escape on the Ash Road: the second rescue costs
        // seconds against the lava front.
        AssertThat(level.Civilians[0].Position.X < Level06Controller.RockfallX).IsTrue();
        AssertThat(level.Civilians[1].Position.X > Level06Controller.EscapeStartX).IsTrue();
        AssertThat(level.Civilians[1].Position.X < Level06Controller.EscapeFinishX).IsTrue();

        level.Civilians[0].Interact(level.Player);
        AssertThat(level.Civilians[0].IsRescued).IsTrue();
        AssertThat(level.CiviliansRescued).IsEqual(1);
        // A rescued civilian is out of the world and cannot be counted twice.
        AssertThat(level.Civilians[0].CanInteract(level.Player)).IsFalse();
        AssertThat(level.Civilians[0].Rescue()).IsFalse();
        AssertThat(level.CiviliansRescued).IsEqual(1);

        AssertThat(level.Civilians[1].Rescue()).IsTrue();
        AssertThat(level.CiviliansRescued).IsEqual(2);
    }

    // === GU-L4 (2026-10-12): the encounter locks ===

    /// <summary>
    /// The Forum, the vault and the Ash Road are encounter locks, one wave per
    /// manifest row: IDs, areas, the vault's exit filling the rockfall under its
    /// gate column, the winch as the vault's clear condition, the Warden a member
    /// of the road (its Phase Skip clamped inside the road), and the baselines.
    /// </summary>
    [TestCase]
    public void TheForumTheVaultAndTheAshRoadAreEncounterLocksOneWavePerManifestRow() {
        using var fixture = new PompeiiFixture();
        Level06Controller level = fixture.Level;
        AssertThat(level.EncounterLocks.Count).IsEqual(3);

        var expected = new (EncounterLock Lock, string ID, float MinX, float MaxX, string Row,
            (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] Table,
            EncounterWaveTrigger Trigger, string Puzzle)[] {
            (level.ForumLock, Level06Controller.LockForum, 300f, 2880f, "level_06.room1",
                Level06Controller.Room1Spawns, EncounterWaveTrigger.OnBuild, ""),
            (level.VaultLock, Level06Controller.LockVault, 2900f, 5390f, "level_06.room2",
                Level06Controller.Room2Spawns, EncounterWaveTrigger.OnBuild, Level06Controller.RockfallPuzzleID),
            (level.AshRoadLock, Level06Controller.LockAshRoad, 6720f, 8500f, "level_06.room3",
                Level06Controller.Room3Spawns, EncounterWaveTrigger.OnEngage, "")
        };
        foreach ((EncounterLock encounterLock, string id, float minX, float maxX, string row,
                     (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] table,
                     EncounterWaveTrigger trigger, string puzzle) in expected) {
            AssertObject(encounterLock).OverrideFailureMessage($"'{id}' was not built.").IsNotNull();
            AssertString(encounterLock.LockID).IsEqual(id);
            AssertFloat(encounterLock.Area.Position.X).IsEqualApprox(minX, 0.5f);
            AssertFloat(encounterLock.Area.End.X).IsEqualApprox(maxX, 0.5f);
            // GR-LV2: the Forum also carries the swarm row, read by Level06TraversalStretchTests.
            AssertThat(encounterLock.Spec.Waves.Length).IsEqual(encounterLock == level.ForumLock ? 2 : 1);
            EncounterLockWave wave = encounterLock.Spec.Waves[0];
            AssertString(wave.RowID).IsEqual(row);
            AssertThat(wave.Trigger).IsEqual(trigger);
            AssertThat(wave.Spawns.Length).IsEqual(table.Length);
            for (int index = 0; index < table.Length; index++) {
                AssertString(wave.Spawns[index].EnemyID).IsEqual(table[index].EnemyID);
                Vector2 spawn = table[index].Position;
                AssertThat(spawn.X > minX && spawn.X < maxX).OverrideFailureMessage(
                    $"'{id}' spawns {table[index].EnemyID} at {spawn}, outside {minX}..{maxX}.").IsTrue();
                // Patrols stay inside the lock (an engaged member that loses its target patrols).
                AssertThat(table[index].PatrolA.X > minX && table[index].PatrolB.X < maxX).IsTrue();
            }
            AssertString(string.Join(",", encounterLock.Spec.RequiredPuzzleIDs)).IsEqual(puzzle);
        }

        // The vault's exit barrier fills the rockfall from the gate column's foot to the floor.
        EncounterLockBarrier vaultExit = level.VaultLock.ExitBarrier;
        AssertFloat(vaultExit.CenterX).IsEqualApprox(Level06Controller.RockfallX, 0.5f);
        AssertFloat(vaultExit.TopY).IsEqualApprox(280f, 0.5f);
        AssertFloat(vaultExit.BottomY).IsEqualApprox(900f, 0.5f);

        // The Warden debuts in the road lock (the last entry, so Easy's prefix
        // still leaves it out), and its manifest row now carries it.
        var road = level.AshRoadLock.Spec.Waves[0];
        AssertString(road.Spawns[^1].EnemyID).IsEqual(Level06Controller.WardenEnemyID);

        IReadOnlyDictionary<string, string[]> map = level.AuthoredEncounterBaselines;
        AssertThat(map[Level06Controller.Checkpoint0].Length).IsEqual(0);
        AssertString(string.Join(",", map[Level06Controller.Checkpoint1]))
            .IsEqual($"{Level06Controller.LockForum},{Level06Controller.LockVault}");
        AssertString(string.Join(",", map[Level06Controller.Checkpoint2]))
            .IsEqual($"{Level06Controller.LockForum},{Level06Controller.LockVault},{Level06Controller.LockAshRoad}");
        AssertObject(level.GetNodeOrNull("Room2WaveTrigger")).IsNull();
    }

    /// <summary>
    /// The anchors and the Restoration Font stand outside every lock, and each
    /// anchor's strike surface lies beyond the longest basic swing from inside the
    /// locks its baseline lists.
    /// </summary>
    [TestCase]
    public void TheAnchorsAndTheFontStandOutsideEveryLockAndBeyondASwingFromInside() {
        using var fixture = new PompeiiFixture();
        Level06Controller level = fixture.Level;
        float swing = EncounterLockReach.LongestBasicSwingPixels();
        AssertThat(level.Levels.TryGetCheckpointPosition(Level06Controller.Checkpoint1, out Vector2 middle)).IsTrue();
        AssertThat(level.Levels.TryGetCheckpointPosition(Level06Controller.Checkpoint2, out Vector2 preBoss)).IsTrue();

        float vaultReach = EncounterLockReach.EastReachFromInside(Level06Controller.VaultLockMaxX, swing);
        AssertThat(vaultReach < middle.X - EncounterLockReach.StrikeSurfaceHalfWidthPixels)
            .OverrideFailureMessage($"A swing from inside the vault reaches x {vaultReach:0}; the Middle anchor stands at {middle.X}.")
            .IsTrue();
        float roadReach = EncounterLockReach.EastReachFromInside(Level06Controller.AshRoadLockMaxX, swing);
        AssertThat(roadReach < preBoss.X - EncounterLockReach.StrikeSurfaceHalfWidthPixels)
            .OverrideFailureMessage($"A swing from inside the Ash Road reaches x {roadReach:0}; the PreBoss anchor stands at {preBoss.X}.")
            .IsTrue();
        // The Middle anchor stands between the rockfall and the road.
        AssertThat(middle.X - 30f > Level06Controller.RockfallX && middle.X + 30f < Level06Controller.AshRoadLockMinX).IsTrue();

        float fontX = level.AuthoredRestorationFontPosition.Value.X;
        foreach (EncounterLock encounterLock in level.EncounterLocks) {
            AssertThat(fontX + 30f <= encounterLock.Area.Position.X || fontX - 30f >= encounterLock.Area.End.X)
                .OverrideFailureMessage($"The font at {fontX} stands inside '{encounterLock.LockID}'.").IsTrue();
        }
    }

    /// <summary>
    /// The ruling for the Ash Road (plan §5): its enemies are fought before the
    /// lava front, which starts when the road lock clears. Entering the road no
    /// longer starts the front; clearing the road starts it
    /// <see cref="Level06Controller.EscapeHeadStartPixels"/> behind the hero, never
    /// west of the authored start. The Warden, a road member, is clamped inside the
    /// road. In real frames on the real scene, resumed at the Middle anchor.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheLavaFrontStartsOnlyWhenTheAshRoadLockClearsAHeadStartBehindTheHero() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new PompeiiFixture($"{LevelID}_checkpoint_1");
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level06Controller level = fixture.Level;
        EncounterLock road = level.AshRoadLock;
        AssertThat(level.VaultLock.RestoredCleared).IsTrue();
        AssertThat(road.State).IsEqual(EncounterLockState.Armed);

        PlayerController hero = level.Player;
        hero.GlobalPosition = new Vector2(7600f, 880f);
        hero.Velocity = Vector2.Zero;
        for (int frame = 0; frame < 30 && road.State != EncounterLockState.Engaged; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(road.State).IsEqual(EncounterLockState.Engaged);
        // Stepping onto the road spawned the crowd, but the front waits for the clear.
        AssertThat(road.LivingMemberCount).IsGreater(0);
        AssertThat(level.EscapeTriggered).IsFalse();
        AssertThat(level.Escape.IsRunning).IsFalse();

        // The road's members are clamped inside it (the Warden's Phase Skip included).
        foreach (EnemyController member in road.LivingMembers) {
            AssertThat(member.EncounterBounds).IsEqual(road.Area);
            if (member.Data?.EnemyID != Level06Controller.WardenEnemyID) continue;
            member.GlobalPosition = new Vector2(Level06Controller.AshRoadLockMaxX + 300f, member.GlobalPosition.Y);
            member.ApplyEncounterBoundsForTest();
            AssertThat(member.GlobalPosition.X < Level06Controller.AshRoadLockMaxX)
                .OverrideFailureMessage($"The Warden stands at {member.GlobalPosition.X}, past the road's end.").IsTrue();
        }

        for (int pass = 0; pass < 6 && road.State == EncounterLockState.Engaged; pass++) {
            foreach (EnemyController member in road.LivingMembers) member.TakeDamage(99999);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(road.State).IsEqual(EncounterLockState.Cleared);
        AssertThat(level.EscapeTriggered).IsTrue();
        AssertThat(level.Escape.IsRunning).IsTrue();
        float expectedStart = Level06Controller.EscapeStartFor(hero.GlobalPosition.X);
        AssertFloat(level.Escape.StartX).IsEqualApprox(expectedStart, 60f);
        AssertThat(level.Escape.FrontX < hero.GlobalPosition.X)
            .OverrideFailureMessage($"The front starts at {level.Escape.FrontX}, ahead of the hero at {hero.GlobalPosition.X}.")
            .IsTrue();
        // The road's second civilian waits in the run, between the road lock and the finish.
        AssertThat(level.Civilians[1].GlobalPosition.X > Level06Controller.AshRoadLockMaxX
            && level.Civilians[1].GlobalPosition.X < Level06Controller.EscapeFinishX).IsTrue();

        // The head-start rule itself: the head start behind the hero wherever it
        // stands (FIX-LEVEL: no floor at the authored start, which lies inside the
        // road lock), never past the front's end.
        AssertFloat(Level06Controller.EscapeStartFor(5600f))
            .IsEqualApprox(5600f - Level06Controller.EscapeHeadStartPixels, 0.01f);
        AssertFloat(Level06Controller.EscapeStartFor(8000f))
            .IsEqualApprox(8000f - Level06Controller.EscapeHeadStartPixels, 0.01f);
        AssertFloat(Level06Controller.EscapeStartFor(9500f)).IsEqualApprox(Level06Controller.EscapeEndX, 0.01f);
    }

    /// <summary>
    /// FIX-LEVEL (2026-10-13, review items 9 and 16): a hero who lands the road's
    /// last kill pressed against its entry barrier still gets the head start. The
    /// front used to be floored at <see cref="Level06Controller.EscapeStartX"/>
    /// (5700), inside the road lock (5660): its zone, the 320 px west of the front,
    /// covered such a hero, and the first tick caught it and knocked it west,
    /// behind the front. In real frames on the real scene.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task ClearingTheRoadAgainstItsEntryBarrierStillStartsTheFrontBehindTheHero() {
        using var fixture = new PompeiiFixture($"{LevelID}_checkpoint_1");
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level06Controller level = fixture.Level;
        EncounterLock road = level.AshRoadLock;
        PlayerController hero = level.Player;
        try {
            LevelLockTestKit.PlaceHero(hero, Level06Controller.AshRoadLockMinX + 100f, 899f);
            await LevelLockTestKit.Frames(4);
            AssertThat(road.State).IsEqual(EncounterLockState.Engaged);
            // The fight ends with the hero against the closed entry barrier.
            float heroX = road.EntryBarrier.Footprint.End.X + EncounterLockReach.HeroHalfWidthPixels + 4f;
            LevelLockTestKit.PlaceHero(hero, heroX, 899f);
            await LevelLockTestKit.Frames(1);
            AssertThat(await LevelLockTestKit.ClearByForce(road)).IsTrue();
            heroX = hero.GlobalPosition.X;
            // GR-LV2: the carve moved this barrier east of the old floor's reach, so the case
            // pins the rule directly as well: a start is never floored at the authored one.
            AssertThat(heroX < road.Area.Position.X + 2f * EncounterLockReach.HeroHalfWidthPixels + 8f)
                .OverrideFailureMessage($"The hero stands at {heroX}, not against the entry barrier.").IsTrue();
            AssertThat(Level06Controller.EscapeStartFor(Level06Controller.EscapeStartX))
                .IsEqual(Level06Controller.EscapeStartX - Level06Controller.EscapeHeadStartPixels);
            EscapeSequenceController escape = level.Escape;
            AssertThat(escape.IsRunning).IsTrue();
            AssertFloat(escape.StartX)
                .IsEqualApprox(heroX - Level06Controller.EscapeHeadStartPixels, 20f);
            AssertThat(escape.FrontX < heroX - EncounterLockReach.HeroHalfWidthPixels)
                .OverrideFailureMessage($"The front starts at {escape.FrontX}, over the hero's body at {heroX}.").IsTrue();
            await LevelLockTestKit.Frames(30);
            AssertThat(escape.CatchCount)
                .OverrideFailureMessage("The front caught the hero the moment the road cleared.").IsEqual(0);
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    /// <summary>
    /// FIX-LEVEL (2026-10-13, review items 3 and 14): a hero <b>walking</b> east
    /// past the finish line completes the lava run. The finish trigger is centred
    /// on the line, so it meets a walking hero short of
    /// <see cref="Level06Controller.EscapeFinishX"/> (refused) and never fires
    /// again on the way through, and the front parks short of the line, so it
    /// only ever overlapped bodies behind it: the run stayed live for the rest of
    /// the level (the objective stuck on "outrun", the parked front burning the
    /// road's second civilian, the play bot held past the line for good). The
    /// escape now completes on the first running tick its runner stands past the
    /// line — one rule, shared with Level 15. Real frames, real input.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task AHeroWalkingPastTheFinishLineCompletesTheLavaRun() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new PompeiiFixture($"{LevelID}_checkpoint_1");
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level06Controller level = fixture.Level;
        EscapeSequenceController escape = level.Escape;
        PlayerController hero = level.Player;
        // The road behind the hero is won; this case is the run, not the fight.
        level.AshRoadLock.RestoreCleared();
        LevelLockTestKit.PlaceHero(hero, 8600f, 899f);
        AssertThat(level.BeginEscape()).IsTrue();

        Area2D finish = escape.GetNode<Area2D>("FinishTrigger");
        float metTheTriggerAt = float.NaN;
        void OnFinishEntered(Node2D body) {
            if (body == hero && float.IsNaN(metTheTriggerAt)) metTheTriggerAt = hero.GlobalPosition.X;
        }
        finish.BodyEntered += OnFinishEntered;
        var input = new BufferedInputSource();
        InputManager.Instance.SetInputSource(hero.PlayerIndex, input);
        try {
            uint tick = 0;
            float target = Level06Controller.EscapeFinishX + 80f;
            for (int frame = 0; frame < 240 && hero.GlobalPosition.X < target; frame++) {
                input.SetNextFrame(PlayerInputFrame.Create(tick++, 1f, 0f, GameplayButtons.None));
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            input.SetNextFrame(PlayerInputFrame.Create(tick, 0f, 0f, GameplayButtons.None));
            AssertThat(hero.GlobalPosition.X >= target)
                .OverrideFailureMessage($"The hero only walked to x {hero.GlobalPosition.X:0}.").IsTrue();
            // The trigger met the hero short of the line, which is why it alone never completed the run.
            AssertThat(!float.IsNaN(metTheTriggerAt) && metTheTriggerAt < Level06Controller.EscapeFinishX)
                .OverrideFailureMessage($"The finish trigger first met the hero at x {metTheTriggerAt}.").IsTrue();
            AssertThat(escape.IsCompleted)
                .OverrideFailureMessage("A hero who walked past the finish line left the lava run live.").IsTrue();
            AssertThat(escape.IsRunning).IsFalse();
            AssertThat(level.EscapeSurvived).IsTrue();
            Label objective = level.HUD.GetNode<Label>("SafeArea/TopLeft/Objective");
            AssertThat(objective.Text).IsEqual(TranslationServer.Translate("pompeii_objective_anchor").ToString());
        } finally {
            finish.BodyEntered -= OnFinishEntered;
            InputManager.Instance.ClearInputSource(hero.PlayerIndex);
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    /// <summary>
    /// FIX-LEVEL (2026-10-13): a death rewind during the run restarts the front
    /// behind the landing (the level's signature mechanic must not switch off),
    /// but once the run is won a death never restarts it. The escape's own reset
    /// (<c>ResetToInitialState</c>) clears <c>IsCompleted</c> before the level's
    /// handler runs, so the old <c>IsCompleted</c> guard never held and a death
    /// in the caldera started the lava again behind the hero.
    /// </summary>
    [TestCase]
    public void ADeathRewindRestartsTheLavaBehindTheLandingButNeverOnceTheRunIsWon() {
        using var fixture = new PompeiiFixture($"{LevelID}_checkpoint_1");
        Level06Controller level = fixture.Level;
        EscapeSequenceController escape = level.Escape;
        PlayerController hero = level.Player;
        level.AshRoadLock.RestoreCleared();
        LevelLockTestKit.PlaceHero(hero, 8000f, 899f);
        AssertThat(level.BeginEscape()).IsTrue();
        escape.AdvanceFront(1f);

        // Mid-run: the rewind lands the hero behind the front's start, which moves back behind it.
        LevelLockTestKit.PlaceHero(hero, 7200f, 899f);
        EventBus.Instance.RaiseRewindTriggered(new Vector2(7200f, 899f));
        AssertThat(escape.IsRunning).OverrideFailureMessage("A death mid-run switched the lava off.").IsTrue();
        AssertThat(escape.FrontX < 7200f - EncounterLockReach.HeroHalfWidthPixels).IsTrue();
        AssertThat(level.EscapeSurvived).IsFalse();

        // Won: past the line the run completes on the next running tick.
        LevelLockTestKit.PlaceHero(hero, Level06Controller.EscapeFinishX + 120f, 899f);
        escape.AdvanceFront(1f / 60f);
        AssertThat(escape.IsCompleted).IsTrue();
        AssertThat(level.EscapeSurvived).IsTrue();

        // A later death, even one that lands short of the line, never restarts it.
        LevelLockTestKit.PlaceHero(hero, 8700f, 899f);
        EventBus.Instance.RaiseRewindTriggered(new Vector2(8700f, 899f));
        AssertThat(escape.IsRunning)
            .OverrideFailureMessage("A death after the run was won restarted the lava front.").IsFalse();
        AssertThat(escape.IsCompleted).IsTrue();
        escape.AdvanceFront(1f);
        AssertThat(escape.CatchCount).IsEqual(0);
    }

    // === Helpers ===

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
    /// Builds the real Pompeii scene in the runner tree against a scratch save, then
    /// hands every shared singleton back on dispose.
    /// <para>
    /// The fixture always resumes at a checkpoint. That is deliberate: a fresh entry
    /// defers the entrance dialogue, which is authored <c>PausesGameplay = true</c>,
    /// and a leaked <c>SceneTree.Paused</c> stops GdUnit4's transport node and hangs
    /// the whole session (docs/PACKAGE5_CAMPAIGN_PLAN.md §9). The pause flag is
    /// restored anyway.
    /// </para>
    /// <para>
    /// It also clears the persisted rockfall flag, because the puzzle is authored
    /// <c>PersistCompletionToSave = true</c> and one case solving it would otherwise
    /// open the road for every later case (L03 precedent).
    /// </para>
    /// </summary>
    private sealed class PompeiiFixture : IDisposable {
        public readonly Level06Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public PompeiiFixture() : this(NewSave($"{LevelID}_checkpoint_0")) { }

        public PompeiiFixture(string resumeCheckpointID) : this(NewSave(resumeCheckpointID)) { }

        public PompeiiFixture(StorySaveData save) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            PackedScene packed = ResourceLoader.Load<PackedScene>(ScenePath);
            Level = packed.Instantiate<Level06Controller>();
            Level.Name = "Level06PompeiiFixture";
            tree.Root.AddChild(Level);
        }

        public static StorySaveData NewSave(string checkpointID) => new() {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Pompeii),
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
