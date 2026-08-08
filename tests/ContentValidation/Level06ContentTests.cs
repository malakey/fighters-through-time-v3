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
    public void TheLevelIsFourConfinedRoomsInsideTheAuthoredBounds() {
        using var fixture = new PompeiiFixture();
        Level06Controller level = fixture.Level;

        AssertThat(Mathf.IsEqualApprox(level.LevelBounds.Size.X, Level06Controller.LevelWidth)).IsTrue();
        // Plan §5: 8000-14000 px; the agent brief narrows Pompeii to 9000-11000.
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
        string[] expectedSequences = { "level_06.entrance", "level_06.boss_intro", "level_06.exit" };
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

    // === Locked encounter economy: 10 standards / 0 elites / 1 boss / 3 extractors ===

    [TestCase]
    public void TheAuthoredSpawnTableIsExactlyTenStandardsAndNoElites() {
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

        // docs/DUST_ECONOMY.md level 6 row: S=10, E=0.
        AssertThat(total).IsEqual(10);
        AssertThat(Level06Controller.AuthoredStandardCount).IsEqual(10);
        AssertThat(Level06Controller.AuthoredEliteCount).IsEqual(0);
        AssertThat(byTier.GetValueOrDefault(EnemyTier.Standard)).IsEqual(10);
        AssertThat(byTier.GetValueOrDefault(EnemyTier.Elite)).IsEqual(0);

        // Era roster: the Pompeii standard mixed with the cultist standard, nothing else.
        AssertThat(byID.Count).IsEqual(2);
        AssertThat(byID["shock_shield_legionnaire"]).IsEqual(6);
        AssertThat(byID["chrono_slasher"]).IsEqual(4);

        // Per-wave concurrency must fit level_06_pool_config's standard_enemy warm 12.
        AssertThat(Level06Controller.Room1Spawns.Length).IsEqual(3);
        AssertThat(Level06Controller.Room2Spawns.Length).IsEqual(3);
        AssertThat(Level06Controller.Room3Spawns.Length).IsEqual(4);
    }

    [TestCase]
    public void EveryLegionnairePostIsFlankableFromAbove() {
        // shock_shield_legionnaire carries FrontalDamageReduction = 0.5, so trading
        // blows into the shield is the wrong answer. Every post is authored under a
        // drop-through platform and with a patrol that turns its back.
        var legionnaire = AuthoredResources.Load<EnemyData>("res://resources/Enemies/shock_shield_legionnaire.tres");
        AssertObject(legionnaire).IsNotNull();
        AssertFloat(legionnaire.FrontalDamageReduction).IsEqualApprox(0.5f, 0.001f);

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
            AssertThat(extractor.DustReward).IsEqual(15);
        }
    }

    [TestCase]
    public void TheVulcanDecimatorIsWiredAndItsRangeBandFitsTheCalderaArena() {
        var boss = AuthoredResources.Load<BossData>(BossPath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("vulcan_decimator");
        AssertThat(boss.ChronalDustDrop).IsEqual(50);

        using var fixture = new PompeiiFixture();
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertThat(encounter.Data == boss)
            .OverrideFailureMessage("The encounter must reuse the pinned authored BossData instance.").IsTrue();

        // Asserted against the resource, never a hardcoded width: re-tuning the boss
        // then cannot silently outgrow the arena.
        Rect2 arena = fixture.Level.RoomTriggers[3].CameraBounds;
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
