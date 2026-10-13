using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Minimal concrete level used to exercise <see cref="StoryLevelControllerBase"/>:
/// three checkpoints, a floor, one drop-through platform, and one room. Top level
/// rather than nested because Godot's generator requires every containing type of
/// a GodotObject subclass to be partial.
/// </summary>
internal partial class FrameworkTestLevelController : StoryLevelControllerBase {
    public const string TestLevelID = "level_02_orleans";
    public const CampaignLevel TestLevel = CampaignLevel.Orleans;

    public override string LevelID => TestLevelID;
    public override CampaignLevel Level => TestLevel;
    public override string LevelTitleKey => "orleans_level_title";
    // Blank on purpose: StorySceneBootstrapper skips dialogue registration and
    // these cases never depend on authored dialogue content.
    public override string DialogueSetPath => "";
    public override Vector2 PlayerSpawnPosition => new(200, 850);
    public override Rect2 LevelBounds => new(0, 0, 6000, 1080);
    /// <summary>Timed, so the F01 clock arms and the Collapse Tremor attaches.</summary>
    public override float ParSeconds => 360f;

    public readonly List<string> MarkedCheckpoints = new();
    public readonly List<string> CompletedDialogues = new();
    public bool BuildLevelRan;
    public bool SpawnInitialEnemiesRan;
    public bool LevelReadyRan;

    public StaticBody2D Floor;
    public OneWayPlatform DropThrough;
    public CheckpointTrigger MidCheckpoint;

    protected override void BuildLevel() {
        BuildLevelRan = true;
        Floor = BuildFloor(0, 900, 4000);
        BuildPlatform(600, 700, 240);
        DropThrough = BuildOneWayPlatform(1200, 620, 240);
        BuildWall(0, 0, 1080);
        BuildHazardSpikes(900, 890, 120);
        BuildCheckpoint(200, 850, $"{TestLevelID}_checkpoint_0", CheckpointRole.Entry);
        MidCheckpoint = BuildCheckpoint(2000, 850, $"{TestLevelID}_checkpoint_1", CheckpointRole.Middle);
        BuildCheckpoint(3600, 850, $"{TestLevelID}_checkpoint_2", CheckpointRole.PreBoss);
        BuildRoomTransition("orleans_room_2", new Vector2(1800, 600), new Rect2(1500, 0, 2000, 1080));
    }

    protected override void SpawnInitialEnemies() => SpawnInitialEnemiesRan = true;

    protected override void MarkWavesClearedThrough(string checkpointID) =>
        MarkedCheckpoints.Add(checkpointID);

    protected override void OnLevelReady() => LevelReadyRan = true;

    protected override void OnDialogueSequenceComplete(string dialogueID) =>
        CompletedDialogues.Add(dialogueID);

    public LevelResultsPanel CompleteForTest() => ShowCompletionResults();
    public ChronalExtractor BuildExtractorForTest(string extractorID, Vector2 position) =>
        BuildExtractor(extractorID, position);
    public CrumblingPlatform BuildFracturePlatformForTest(string id, float x, float y, float width) =>
        BuildFracturePlatform(id, x, y, width);
    public StoryKillBoundary BuildKillBoundaryForTest(string id, float x, float y, float width) =>
        BuildKillBoundary(id, x, y, width);
}

/// <summary>
/// Drives the post-boss beat chain without a DialogueManager: the beat list is
/// writable, dialogue starts are recorded, and the beat delay is zero so a case
/// can step the chain synchronously. A real sequence here would pause the tree
/// (signature 4) and the fixture would have to unwind it.
/// </summary>
internal partial class PostBossChainTestLevelController : StoryLevelControllerBase {
    public override string LevelID => FrameworkTestLevelController.TestLevelID;
    public override CampaignLevel Level => FrameworkTestLevelController.TestLevel;
    public override string LevelTitleKey => "orleans_level_title";
    public override string DialogueSetPath => "";
    public override Vector2 PlayerSpawnPosition => new(200, 850);
    public override Rect2 LevelBounds => new(0, 0, 6000, 1080);

    public readonly List<string> Beats = new();
    public readonly List<string> StartedDialogues = new();
    public readonly List<string> RoutedToSubclass = new();
    public bool DialogueStartsSucceed = true;

    protected override IReadOnlyList<string> PostBossDialogueIDs => Beats;
    protected override float ExitDialogueDelaySeconds => 0f;

    protected override void BuildLevel() => BuildFloor(0, 900, 4000);

    protected override bool StartDialogue(string dialogueID) {
        StartedDialogues.Add(dialogueID);
        return DialogueStartsSucceed;
    }

    protected override void OnDialogueSequenceComplete(string dialogueID) =>
        RoutedToSubclass.Add(dialogueID);

    public void DefeatBossForTest(int dust = 50) {
        // Mirrors the V7.3 Single Icon Rule defeat flow: the encounter spawns a
        // physical pickup whose COLLECTION raises the single wallet award plus
        // the boss-line attribution payload; the defeat itself only routes the
        // payload into the level's bookkeeping. The two raises here stand in
        // for ChronalDustPickup.Collect().
        EventBus.Instance?.RaiseChronalDustCollected(dust);
        EventBus.Instance?.RaiseDustAwardCollected(new DustAwardCollectedPayload {
            Amount = dust,
            Source = DustAwardSource.Boss
        });
        OnBossDefeated(null, new BossDefeatedPayload { BossID = "test_boss", ChronalDustDrop = dust });
    }
}

/// <summary>
/// Package 5 A1: the shared level framework campaign levels 2-15 extend. What is
/// pinned here is exactly what every level agent depends on — the "LevelManager"
/// child, geometry/collision conventions, camera confinement, checkpoint resume
/// with wave marking, the dust tally, and the completion chain.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryLevelControllerBaseTests {
    private const int ScratchSlot = 2;
    private const string TestLevelID = FrameworkTestLevelController.TestLevelID;

    // === Structure ===

    [TestCase]
    public void TheLevelManagerChildIsNamedExactlyLevelManagerAndCarriesTheManifestID() {
        using var fixture = new LevelFixture(null);
        // CheckpointTrigger resolves this by literal node name off the scene root,
        // so the name is a hard contract, not a convention.
        var manager = fixture.Level.GetNodeOrNull<LevelManager>("LevelManager");
        AssertObject(manager).IsNotNull();
        AssertString(manager.LevelID).IsEqual(TestLevelID);
        AssertString(manager.LevelDisplayName).IsEqual("orleans_level_title");
        AssertThat(fixture.Level.Levels == manager).IsTrue();
    }

    [TestCase]
    public void ReadyRunsBuildSpawnAndServicesInTheFlorenceOrder() {
        using var fixture = new LevelFixture(null);
        FrameworkTestLevelController level = fixture.Level;
        AssertThat(level.BuildLevelRan).IsTrue();
        AssertThat(level.SpawnInitialEnemiesRan).IsTrue();
        AssertThat(level.LevelReadyRan).IsTrue();
        AssertObject(level.Player).IsNotNull();
        AssertObject(level.Services).IsNotNull();
        AssertObject(level.HUD).IsNotNull();
        AssertObject(level.Services.Dialogue).IsNotNull();
        AssertObject(level.Services.RewindManager).IsNotNull();
    }

    [TestCase]
    public void GeometryBuildersUseTheCanonicalCollisionLayers() {
        using var fixture = new LevelFixture(null);
        FrameworkTestLevelController level = fixture.Level;
        AssertThat(level.Floor.CollisionLayer).IsEqual(CollisionLayers.Environment);
        AssertThat(level.Floor.CollisionMask).IsEqual(0u);
        AssertThat(level.DropThrough.CollisionLayer).IsEqual(CollisionLayers.OneWayPlatform);
        AssertThat(level.DropThrough.IsInGroup("OneWayPlatform")).IsTrue();
        AssertThat(level.MidCheckpoint.CollisionLayer).IsEqual(CollisionLayers.Trigger);
        AssertThat(level.MidCheckpoint.CollisionMask).IsEqual(CollisionLayers.Player);
    }

    [TestCase]
    public void TheCameraIsAConfinerBoundToTheLevelAndLinkedIntoEveryRoomTrigger() {
        using var fixture = new LevelFixture(null);
        FrameworkTestLevelController level = fixture.Level;
        AssertObject(level.Camera).IsNotNull();
        AssertThat(level.Camera.LimitRight).IsEqual(6000);
        AssertThat(level.Camera.LimitBottom).IsEqual(1080);
        AssertThat(level.Camera.GetParent() == level.Player).IsTrue();

        AssertThat(level.RoomTriggers.Count).IsEqual(1);
        RoomTransitionTrigger room = level.RoomTriggers[0];
        // Rooms are built before the player exists, so the base must patch the
        // camera path in afterwards or room confinement silently does nothing.
        AssertThat(room.CameraPath != null && !room.CameraPath.IsEmpty).IsTrue();
        AssertThat(room.GetNodeOrNull<StoryCameraConfiner>(room.CameraPath) == level.Camera).IsTrue();
    }

    [TestCase]
    public void AllThreeCheckpointsRegisterTheirRespawnPositionsWithTheLevelManager() {
        using var fixture = new LevelFixture(null);
        for (int index = 0; index < 3; index++) {
            string id = $"{TestLevelID}_checkpoint_{index}";
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 position))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered.").IsTrue();
            AssertThat(Mathf.IsEqualApprox(position.Y, 800f)).IsTrue();
        }
    }

    // === Checkpoint resume ===

    [TestCase]
    public void ResumingAtASavedCheckpointMovesThePlayerAndMarksTheWavesBehindIt() {
        using var fixture = new LevelFixture(new StorySaveData {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(FrameworkTestLevelController.TestLevel),
            LastCheckpointID = $"{TestLevelID}_checkpoint_1",
            CurrentHP = 63,
            CurrentUltimateMeter = 40f
        });
        FrameworkTestLevelController level = fixture.Level;

        AssertThat(level.ResumedMidLevel).IsTrue();
        AssertString(level.ResumedCheckpointID).IsEqual($"{TestLevelID}_checkpoint_1");
        AssertThat(level.MarkedCheckpoints.Count).IsEqual(1);
        AssertString(level.MarkedCheckpoints[0]).IsEqual($"{TestLevelID}_checkpoint_1");
        AssertThat(Mathf.IsEqualApprox(level.Player.GlobalPosition.X, 2000f)).IsTrue();
        AssertThat(level.Player.CurrentHP).IsEqual(63);
    }

    [TestCase]
    public void AFreshEntryStartsAtTheSpawnPointAndNeverMarksWavesCleared() {
        using var fixture = new LevelFixture(new StorySaveData {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(FrameworkTestLevelController.TestLevel),
            LastCheckpointID = ""
        });
        AssertThat(fixture.Level.ResumedMidLevel).IsFalse();
        AssertThat(fixture.Level.MarkedCheckpoints.Count).IsEqual(0);
        AssertThat(Mathf.IsEqualApprox(fixture.Level.Player.Position.X, 200f)).IsTrue();
    }

    [TestCase]
    public void ACheckpointSavedInADifferentLevelIsIgnored() {
        // Same checkpoint id, wrong scene path: the guard must reject it or a
        // resumed player would teleport into another level's geometry.
        using var fixture = new LevelFixture(new StorySaveData {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Florence),
            LastCheckpointID = $"{TestLevelID}_checkpoint_1"
        });
        AssertThat(fixture.Level.ResumedMidLevel).IsFalse();
        AssertThat(fixture.Level.MarkedCheckpoints.Count).IsEqual(0);
        AssertThat(Mathf.IsEqualApprox(fixture.Level.Player.Position.X, 200f)).IsTrue();
    }

    // === Dust tally and completion ===

    [TestCase]
    public void AKillPaysTheWalletExactlyOnceThroughThePhysicalPickup() {
        // Audit H-1: the controller used to tally AND re-raise the award while the
        // drop system's pickup carried the same amount and raised the same event
        // again on collection — every kill paid the wallet twice. The physical
        // pickup is the single awarding path; the controller only tallies what the
        // wallet was actually paid.
        // A synthetic framework level is not a campaign level, so it runs on the
        // advisory-fallback path (P11 A10 F05) — the authored ChronalDustDrop, not
        // some other campaign level's ledger that the session happens to be on.
        FTT.Environment.LevelRewardDirectory.SuppressLedgerForTest = true;
        using var fixture = new LevelFixture(null);
        FrameworkTestLevelController level = fixture.Level;
        var drops = new StoryDropSystem { Name = "StoryDropSystem", RandomSeed = 12345UL };
        level.AddChild(drops);

        int awards = 0;
        int awarded = 0;
        void OnDust(int amount) { awards++; awarded += amount; }
        EventBus.Instance.OnChronalDustCollected += OnDust;
        try {
            EventBus.Instance.RaiseEnemyKilled(new EnemyKilledPayload {
                EnemyID = "chrono_slasher",
                Position = level.Player.GlobalPosition,
                ChronalDustDrop = 5
            });

            // The kill itself must not touch the wallet or the tally...
            AssertThat(awards).IsEqual(0);
            AssertThat(level.DustEarnedThisLevel).IsEqual(0);

            // ...only collecting the physical pickup does. It spawned on top of the
            // player, so the magnet resolves on the first stepped frame.
            ChronalDustPickup pickup = FindPickupUnder(level);
            AssertObject(pickup)
                .OverrideFailureMessage("StoryDropSystem spawned no ChronalDustPickup for the kill.")
                .IsNotNull();
            AssertThat(pickup.DustAmount).IsEqual(5);
            for (int frame = 0; frame < 10 && awards == 0; frame++) {
                pickup._PhysicsProcess(1.0 / 60.0);
            }

            AssertThat(awards).IsEqual(1);
            AssertThat(awarded).IsEqual(5);
            AssertThat(level.DustEarnedThisLevel).IsEqual(5);
            AssertThat(level.MobDustEarned).IsEqual(5);
            AssertThat(level.BossDustEarned).IsEqual(0);
            AssertThat(level.ExtractorDustEarned).IsEqual(0);
        } finally {
            EventBus.Instance.OnChronalDustCollected -= OnDust;
            FTT.Environment.LevelRewardDirectory.ResetAttempt();
        }
    }

    [TestCase]
    public void ExtractorDestructionSpawnsAPickupAndCollectionEntersTheTallyOnceAndItemizes() {
        // Audit M-1 reworked by the V7.3 Single Icon Rule: destruction spawns a
        // physical pickup; the wallet is paid — and the extractor line labeled —
        // only when it is collected.
        // Advisory-fallback path: see the kill test above.
        FTT.Environment.LevelRewardDirectory.SuppressLedgerForTest = true;
        using var fixture = new LevelFixture(null);
        FrameworkTestLevelController level = fixture.Level;
        ChronalExtractor extractor = level.BuildExtractorForTest("orleans_test_extractor", new Vector2(500, 850));
        AssertObject(extractor).IsNotNull();

        int awards = 0;
        int awarded = 0;
        void OnDust(int amount) { awards++; awarded += amount; }
        EventBus.Instance.OnChronalDustCollected += OnDust;
        try {
            extractor.TakeEnvironmentDamage(extractor.MaxHP);
            AssertThat(extractor.IsDestroyed).IsTrue();

            // Destruction pays nothing — the award is physical now.
            AssertThat(awards).IsEqual(0);
            AssertThat(level.DustEarnedThisLevel).IsEqual(0);
            ChronalDustPickup pickup = FindPickupUnder(level);
            AssertObject(pickup)
                .OverrideFailureMessage("Destruction spawned no ChronalDustPickup.")
                .IsNotNull();
            AssertThat(pickup.DustAmount).IsEqual(extractor.DustReward);
            AssertThat(pickup.Source).IsEqual(DustAwardSource.Extractor);
            AssertThat(pickup.NeverExpires).IsTrue();
            // P11 A10 (F05 §9): only the BOSS still forces a Large milestone
            // plate. An ordinary Extractor now shows the icon its actual award
            // earns, so its pickup carries no forced tier.
            AssertThat(pickup.ForceLargeTier).IsFalse();

            // One wallet award at collection, banked AND labeled on its line.
            pickup.Collect();
            AssertThat(awards).IsEqual(1);
            AssertThat(awarded).IsEqual(extractor.DustReward);
            AssertThat(level.DustEarnedThisLevel).IsEqual(extractor.DustReward);
            AssertThat(level.ExtractorDustEarned).IsEqual(extractor.DustReward);
            AssertThat(level.MobDustEarned).IsEqual(0);
            AssertThat(level.BossDustEarned).IsEqual(0);
        } finally {
            EventBus.Instance.OnChronalDustCollected -= OnDust;
            FTT.Environment.LevelRewardDirectory.ResetAttempt();
        }
    }

    private static ChronalDustPickup FindPickupUnder(Node level) {
        Godot.Collections.Array<Node> children = level.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is ChronalDustPickup pickup) return pickup;
        }
        return null;
    }

    [TestCase]
    public void TheExitDialogueEndsTheLevelWhileOtherSequencesRouteToTheSubclass() {
        // Completion advances the campaign level: hand the position back after
        // the fixture (declared first, so it is disposed last).
        using var position = new CampaignPositionGuard();
        using var fixture = new LevelFixture(null);
        FrameworkTestLevelController level = fixture.Level;
        string completedLevelID = null;
        void OnComplete(string id) => completedLevelID = id;
        EventBus.Instance.OnLevelComplete += OnComplete;
        try {
            AssertString(level.EntranceDialogueID).IsEqual("level_02.entrance");
            AssertString(level.BossIntroDialogueID).IsEqual("level_02.boss_intro");
            AssertString(level.ExitDialogueID).IsEqual("level_02.exit");

            EventBus.Instance.RaiseDialogueComplete("level_02.entrance");
            AssertThat(level.CompletedDialogues.Count).IsEqual(1);
            AssertThat(level.LevelComplete).IsFalse();

            EventBus.Instance.RaiseDialogueComplete("level_02.exit");
            AssertThat(level.LevelComplete).IsTrue();
            AssertString(completedLevelID).IsEqual(TestLevelID);
            // The exit sequence completes the level instead of routing to the hook.
            AssertThat(level.CompletedDialogues.Count).IsEqual(1);

            // Completion is one-shot: no second overlay, no second advance.
            AssertObject(level.CompleteForTest()).IsNull();
        } finally {
            EventBus.Instance.OnLevelComplete -= OnComplete;
        }
    }

    // === D12 (2026-10-11): completion banks the dust on the ground =========

    [TestCase]
    public void CompletionBanksLiveDustPickupsFirstAndNothingPaysAfterIt() {
        // USER-DUST-NEVER-EXPIRES / PROVISIONAL-DUST-SETTLE-RULES (2)-(3): the
        // completion collects every pickup still on the ground (a mob drop and,
        // per the 2026-10-11 addendum, a milestone award) BEFORE it raises
        // OnLevelComplete, so the deposit and the results see them; from then
        // on nothing under the level pays and no kill issues dust.
        // Pinned on the framework level's own slot (Orleans) first, so the
        // completion's advance re-keys the directory to Chicago, whose ledger
        // authors chrono_slasher: the post-completion kill below then pins the
        // StoryDropSystem guard instead of passing on an unauthored enemy
        // whatever level an earlier case left the session on. PrepareDirectLevel
        // resets the directory, so the advisory-fallback seam (see the kill test
        // above) is set after it.
        using var position = new CampaignPositionGuard();
        StoryManager.Instance.PrepareDirectLevel(
            FrameworkTestLevelController.TestLevel, "einstein", Difficulty.Normal);
        FTT.Environment.LevelRewardDirectory.SuppressLedgerForTest = true;
        using var fixture = new LevelFixture(null);
        FrameworkTestLevelController level = fixture.Level;
        var drops = new StoryDropSystem { Name = "StoryDropSystem", RandomSeed = 12345UL };
        level.AddChild(drops);

        int awards = 0;
        void OnDust(int amount) => awards++;
        int awardsAtCompletion = -1;
        int mobDustAtCompletion = -1;
        int extractorDustAtCompletion = -1;
        void OnComplete(string id) {
            awardsAtCompletion = awards;
            mobDustAtCompletion = level.MobDustEarned;
            extractorDustAtCompletion = level.ExtractorDustEarned;
        }
        EventBus.Instance.OnChronalDustCollected += OnDust;
        EventBus.Instance.OnLevelComplete += OnComplete;
        try {
            // Both well outside the 150 px magnet.
            Vector2 away = level.Player.GlobalPosition + new Vector2(1500f, 0f);
            EventBus.Instance.RaiseEnemyKilled(new EnemyKilledPayload {
                EnemyID = "chrono_slasher",
                Position = away,
                ChronalDustDrop = 5
            });
            ChronalDustPickup mobPickup = FindPickupUnder(level);
            AssertObject(mobPickup).IsNotNull();
            AssertThat(mobPickup.NeverExpires).IsFalse();
            ChronalDustPickup extractorPickup = StoryDropSystem.SpawnDustAward(
                15, away, level, DustAwardSource.Extractor);
            AssertObject(extractorPickup).IsNotNull();
            AssertThat(awards).IsEqual(0);

            EventBus.Instance.RaiseDialogueComplete("level_02.exit");
            AssertThat(level.LevelComplete).IsTrue();
            AssertThat(level.Levels.IsComplete).IsTrue();

            // Settled first: both paid before OnLevelComplete was raised.
            AssertThat(awardsAtCompletion)
                .OverrideFailureMessage("The dust on the ground must be banked before OnLevelComplete.")
                .IsEqual(2);
            AssertThat(mobDustAtCompletion).IsEqual(5);
            AssertThat(extractorDustAtCompletion).IsEqual(15);
            AssertThat(mobPickup.IsCollected).IsTrue();
            AssertThat(extractorPickup.IsCollected).IsTrue();
            AssertObject(FindPickupUnder(level)).IsNull();

            // After completion a pickup touched under the results overlay
            // releases unpaid...
            ChronalDustPickup late = StoryDropSystem.SpawnDustAward(
                9, away, level, DustAwardSource.Secret);
            AssertObject(late).IsNotNull();
            late.Collect();
            AssertThat(awards)
                .OverrideFailureMessage("A pickup collected after completion must pay nothing.")
                .IsEqual(2);
            AssertThat(level.OptionalDustEarned).IsEqual(0);
            AssertThat(late.IsCollected).IsTrue();
            AssertThat(late.GetParent() == level).IsFalse();

            // ...and a kill issues no dust at all, although the re-keyed
            // directory (Chicago's ledger) would otherwise pay this enemy.
            AssertThat(StoryManager.Instance.CurrentLevel).IsEqual(CampaignLevel.Chicago);
            EventBus.Instance.RaiseEnemyKilled(new EnemyKilledPayload {
                EnemyID = "chrono_slasher",
                Position = away,
                ChronalDustDrop = 5
            });
            AssertObject(FindPickupUnder(level))
                .OverrideFailureMessage("A kill after completion must spawn no dust pickup.")
                .IsNull();
            AssertThat(awards).IsEqual(2);
        } finally {
            EventBus.Instance.OnChronalDustCollected -= OnDust;
            EventBus.Instance.OnLevelComplete -= OnComplete;
            FTT.Environment.LevelRewardDirectory.ResetAttempt();
        }
    }

    [TestCase]
    public void TheResultsPanelShowsTheTierBonusAndLossesTheCompletionApplied() {
        // RESULTS-DUST-LINES-AFTER-RESET: the panel used to read the reward
        // directory's tier bonus and loss tally AFTER the completion's attempt
        // reset had zeroed both, so both lines read 0 on every level.
        StoryManager story = StoryManager.Instance;
        using var position = new CampaignPositionGuard();
        story.PrepareDirectLevel(FrameworkTestLevelController.TestLevel, "einstein", Difficulty.Normal);
        story.BeginLevelRun();
        using var fixture = new LevelFixture(null);
        FrameworkTestLevelController level = fixture.Level;
        try {
            story.SetDust(40);
            FTT.Environment.LevelRewardDirectory.RecordDustLoss(3);

            EventBus.Instance.RaiseDialogueComplete("level_02.exit");
            AssertThat(level.LevelComplete).IsTrue();

            int expected = RewardAllocator.TierBonus(40, story.LastLevelIntegrityPercent, true);
            AssertThat(expected > 0)
                .OverrideFailureMessage("The fixture must earn a tier bonus for this case to mean anything.")
                .IsTrue();
            AssertThat(story.LastLevelTierBonusDust).IsEqual(expected);
            AssertThat(story.LastLevelDustLost).IsEqual(3);

            LevelResultsPanel panel = null;
            Godot.Collections.Array<Node> children = level.GetChildren();
            using (children.AsDisposable()) {
                foreach (Node child in children) {
                    if (child is LevelResultsPanel found) panel = found;
                }
            }
            AssertObject(panel).IsNotNull();
            Label bonusLine = panel.GetNodeOrNull<Label>("Shade/Panel/Layout/DustTierBonus");
            Label lossLine = panel.GetNodeOrNull<Label>("Shade/Panel/Layout/DustLosses");
            AssertObject(bonusLine).IsNotNull();
            AssertObject(lossLine).IsNotNull();
            AssertString(bonusLine.Text)
                .IsEqual(string.Format(TranslationServer.Translate("results_dust_tier_bonus"), expected));
            AssertString(lossLine.Text)
                .IsEqual(string.Format(TranslationServer.Translate("results_dust_losses"), 3));
        } finally {
            FTT.Environment.LevelRewardDirectory.ResetAttempt();
        }
    }

    [TestCase]
    public void AfterACollapseAndTheAnchorResumeTheResultsAddUpToWhatTheLevelBanked() {
        // RESULTS-DUST-LINES-AFTER-RESET (review fix-up): every fee is followed
        // by a reload before the level can complete, and that reload's
        // RestoreAttemptStateFromSave -> ClearLevelAttemptState -> ResetAttempt
        // used to zero the in-memory loss tally, so "Lost to collapse" read 0
        // in every slotted run. And the category lines are scene-local, so the
        // 80 held across the Collapse appeared on no line: the panel showed
        // 50 + bonus - 0 while the level banked 130 + bonus. Driven through the
        // production path: a slotted fresh entry, a real Collapse, the hub
        // portal's resume-from-anchor, then the real level's completion.
        const string hero = "einstein";
        StoryManager story = StoryManager.Instance;
        using var position = new CampaignPositionGuard();
        StorySaveData originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];
        story.PrepareDirectLevel(FrameworkTestLevelController.TestLevel, hero, Difficulty.Normal);
        var save = new StorySaveData {
            SelectedCharacterID = hero,
            Difficulty = Difficulty.Normal,
            CurrentLevelID = StoryManager.GetLevelScenePath(FrameworkTestLevelController.TestLevel),
            CurrentHP = 100
        };
        save.DepositedChronalDust[hero] = 50;
        SaveManager.Instance.SaveSlots[ScratchSlot] = save;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
        story.SuppressSceneLoadsForTesting = true;
        try {
            story.BeginLevelRun();
            story.CollectDust(100);
            story.BeginTimelineCollapse($"{TestLevelID}_checkpoint_1");
            AssertThat(story.AttemptHeldDust).IsEqual(80);
            AssertThat(FTT.Environment.LevelRewardDirectory.DustLostThisAttempt).IsEqual(20);

            AssertThat(story.RestartCollapsedLevel(resumeFromTimelineAnchor: true)).IsTrue();
            AssertThat(FTT.Environment.LevelRewardDirectory.DustLostThisAttempt)
                .OverrideFailureMessage("The same attempt's resume must carry its loss tally across the reset.")
                .IsEqual(20);
            AssertThat(story.LevelDustCarriedIn)
                .OverrideFailureMessage("This load carries the 80 held plus the 20 already lost from it.")
                .IsEqual(100);

            using var fixture = new LevelFixture(save);
            FrameworkTestLevelController level = fixture.Level;
            AssertThat(level.ResumedMidLevel).IsTrue();
            EventBus.Instance.RaiseChronalDustCollected(50);
            AssertThat(story.ChronalDustCollected).IsEqual(130);

            EventBus.Instance.RaiseDialogueComplete("level_02.exit");
            AssertThat(level.LevelComplete).IsTrue();

            int bonus = story.LastLevelTierBonusDust;
            AssertThat(bonus).IsEqual(RewardAllocator.TierBonus(130, story.LastLevelIntegrityPercent, true));
            AssertThat(bonus > 0)
                .OverrideFailureMessage("The resumed attempt must earn a tier bonus for this case to mean anything.")
                .IsTrue();
            AssertThat(story.LastLevelDustLost).IsEqual(20);
            AssertThat(story.LastLevelDustCarriedIn).IsEqual(100);
            AssertThat(story.LastCompletionDepositDust).IsEqual(130 + bonus);
            AssertThat(save.DepositedChronalDust[hero]).IsEqual(50 + 130 + bonus);

            LevelResultsPanel panel = null;
            Godot.Collections.Array<Node> children = level.GetChildren();
            using (children.AsDisposable()) {
                foreach (Node child in children) {
                    if (child is LevelResultsPanel found) panel = found;
                }
            }
            AssertObject(panel).IsNotNull();
            string Line(string name) => panel.GetNode<Label>($"Shade/Panel/Layout/{name}").Text;
            string Format(string key, int amount) =>
                string.Format(TranslationServer.Translate(key).ToString(), amount);
            AssertString(Line("DustMobs")).IsEqual(Format("results_dust_mobs", 50));
            Label carried = panel.GetNode<Label>("Shade/Panel/Layout/DustCarried");
            AssertThat(carried.Visible).IsTrue();
            AssertString(carried.Text).IsEqual(Format("results_dust_carried", 100));
            AssertThat(TranslationServer.Translate("results_dust_carried").ToString())
                .OverrideFailureMessage("results_dust_carried must resolve through the compiled translation.")
                .IsNotEqual("results_dust_carried");
            AssertThat(carried.Text.Contains("100")).IsTrue();
            AssertString(Line("DustLosses")).IsEqual(Format("results_dust_losses", 20));
            AssertString(Line("DustTierBonus")).IsEqual(Format("results_dust_tier_bonus", bonus));
            AssertString(Line("DustEarned"))
                .OverrideFailureMessage("The panel's total must equal what the completion banked.")
                .IsEqual(Format("results_dust_earned", 130 + bonus));
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            story.TryConsumeCompletionDepositNotice(out _);
            FTT.Environment.LevelRewardDirectory.ResetAttempt();
            SaveManager.Instance.SaveSlots[ScratchSlot] = originalSave;
            SessionExitGuard.ClearMarker();
        }
    }

    [TestCase]
    public void TheDialoguePrefixFollowsTheLevelNNConventionForEveryRosterID() {
        // `level_NN.<beat>` is what the dialogue resources and en.csv keys are
        // authored against; a bad prefix silently mutes a level.
        using var fixture = new LevelFixture(null);
        AssertString(fixture.Level.DialoguePrefix).IsEqual("level_02");
        AssertString(PrefixOf("level_13_chronal_void")).IsEqual("level_13");
        AssertString(PrefixOf("level_15_alexandria")).IsEqual("level_15");
    }

    // === Post-boss beat chain (Package 5 Wave B integration) ===

    [TestCase]
    public void WithNoPostBossBeatsDefeatGoesStraightToTheExitDialogueExactlyAsBefore() {
        // Levels 2-7 and 9-12 declare no beats; the chain must not insert an extra
        // hop or an extra delay between the boss dying and the exit sequence.
        using var fixture = new PostBossFixture();
        PostBossChainTestLevelController level = fixture.Level;
        level.DefeatBossForTest(50);

        AssertThat(level.IsBossDefeated).IsTrue();
        // The encounter's single award is the total; the defeat labels it as boss
        // income for the itemized results without adding it a second time.
        AssertThat(level.DustEarnedThisLevel).IsEqual(50);
        AssertThat(level.BossDustEarned).IsEqual(50);
        AssertThat(level.MobDustEarned).IsEqual(0);
        AssertThat(level.StartedDialogues).ContainsExactly("level_02.exit");
        AssertString(level.ActivePostBossDialogueID).IsEqual("");
    }

    [TestCase]
    public void APostBossBeatRunsBetweenDefeatAndExitWithoutCostingTheBaseItsDefeatFlag() {
        // Level 8's Cleopatra scene, and the shape Level 15's ending chain reuses.
        // Before this hook existed L8 overrode OnBossDefeated wholesale and
        // IsBossDefeated stayed false for the whole level.
        using var fixture = new PostBossFixture();
        PostBossChainTestLevelController level = fixture.Level;
        level.Beats.Add("level_02.postboss");

        level.DefeatBossForTest(50);
        AssertThat(level.IsBossDefeated).IsTrue();
        AssertThat(level.StartedDialogues).ContainsExactly("level_02.postboss");
        AssertString(level.ActivePostBossDialogueID).IsEqual("level_02.postboss");
        AssertThat(level.LevelComplete).IsFalse();

        // Finishing the beat advances the chain, and still routes to the subclass
        // hook so a level can react to its own beat.
        EventBus.Instance.RaiseDialogueComplete("level_02.postboss");
        AssertThat(level.RoutedToSubclass).ContainsExactly("level_02.postboss");
        AssertThat(level.StartedDialogues).ContainsExactly("level_02.postboss", "level_02.exit");
        AssertString(level.ActivePostBossDialogueID).IsEqual("");
        AssertThat(level.LevelComplete).IsFalse();

        EventBus.Instance.RaiseDialogueComplete("level_02.exit");
        AssertThat(level.LevelComplete).IsTrue();
    }

    [TestCase]
    public void ABeatThatWillNotStartFallsThroughInsteadOfStrandingThePlayer() {
        // A missing sequence must never leave the player in a finished arena with
        // no results overlay.
        using var fixture = new PostBossFixture();
        PostBossChainTestLevelController level = fixture.Level;
        level.Beats.Add("level_02.postboss");
        level.DialogueStartsSucceed = false;

        level.DefeatBossForTest(50);
        AssertThat(level.StartedDialogues).ContainsExactly("level_02.postboss", "level_02.exit");
        AssertThat(level.LevelComplete).IsTrue();
    }

    [TestCase]
    public void EveryTimedCampaignLevelAuthorsAParSecondsAndTheClockArmsFromIt() {
        // V7.6 F01: a timed level's clock is normalized against an authored
        // par. These are PROVISIONAL values (rooms x 90 s, rounded up to 30 s)
        // recorded under VERIFY-PAR-SECONDS, so the pin is on the TABLE rather
        // than on any individual number being correct: a later measurement pass
        // is a single-file edit and this case is the one place it lands.
        var expected = new Dictionary<CampaignLevel, float> {
            { CampaignLevel.Orleans, 360f },      // 4 rooms
            { CampaignLevel.Chicago, 540f },      // 6 rooms
            { CampaignLevel.Paris, 360f },        // 4 rooms
            { CampaignLevel.Titanic, 270f },      // 3 rooms
            { CampaignLevel.Pompeii, 360f },      // 4 rooms
            { CampaignLevel.Nassau, 630f },       // 7 rooms
            { CampaignLevel.Egypt, 360f },        // 4 rooms
            { CampaignLevel.Berlin, 360f },       // 4 rooms
            { CampaignLevel.London, 360f },       // 4 rooms
            { CampaignLevel.Gettysburg, 360f },   // 4 rooms
            { CampaignLevel.Lunar, 540f },        // 6 rooms
            { CampaignLevel.ChronalVoid, 450f },  // 5 rooms
            { CampaignLevel.NeoEarth, 540f },     // 6 rooms
            { CampaignLevel.Alexandria, 540f }    // 6 rooms
        };
        var issues = new List<string>();
        foreach (KeyValuePair<CampaignLevel, float> row in expected) {
            if (row.Value <= 0f) issues.Add($"{row.Key} authors a non-positive par");
            // Every provisional value is a whole number of 30-second steps.
            if (!Mathf.IsEqualApprox(row.Value % 30f, 0f)) {
                issues.Add($"{row.Key} par {row.Value} is not rounded to 30 s");
            }
        }
        AssertThat(expected.Count)
            .OverrideFailureMessage("All fourteen StoryLevelControllerBase levels must author a par.")
            .IsEqual(14);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

        // And the base controller actually arms the clock from its own par.
        using var fixture = new LevelFixture(null);
        AssertThat(fixture.Level.ParSeconds).IsGreater(0f);
        AssertThat(StoryManager.Instance.ParSecondsForCurrentLevel)
            .OverrideFailureMessage("_Ready must arm the F01 clock from the level's authored par.")
            .IsEqual(fixture.Level.ParSeconds);
        AssertThat(fixture.Level.Tremor)
            .OverrideFailureMessage("A timed level attaches the Collapse Tremor; an untimed one does not.")
            .IsNotNull();
    }

    [TestCase]
    public void FractureEligibleIsOptInAndTheExcludedSurfaceTypesNeverCarryIt() {
        using var fixture = new LevelFixture(null);
        FrameworkTestLevelController level = fixture.Level;

        CrumblingPlatform flagged = level.BuildFracturePlatformForTest("tremor_gap", 2600f, 700f, 200f);
        AssertThat(flagged.FractureEligible).IsTrue();
        AssertThat(flagged.RespawnDuration)
            .OverrideFailureMessage("An unstable platform must always come back within 5 s.")
            .IsEqual(CollapseTremorRules.FracturePlatformRespawnSeconds);

        // The exclusions are enforced by omission, and that is checkable: no
        // excluded surface type in the repository may carry the flag, and the
        // default is off, so an author has to opt in deliberately.
        var issues = new List<string>();
        if (new CrumblingPlatform().FractureEligible) issues.Add("FractureEligible must default off");
        foreach (Node child in level.GetChildren()) {
            if (child is PathMovingPlatform) issues.Add("PathMovingPlatform is never Tremor-eligible");
            if (child is PressurePlate) issues.Add("PressurePlate is never Tremor-eligible");
        }
        // The ordinary builders produce nothing eligible: only the dedicated
        // BuildFracturePlatform helper does.
        foreach (Node child in level.GetChildren()) {
            if (child is CrumblingPlatform platform && platform != flagged && platform.FractureEligible) {
                issues.Add($"{platform.Name} was flagged without going through BuildFracturePlatform");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    private static string PrefixOf(string levelID) {
        string[] parts = levelID.Split('_');
        return parts.Length >= 2 ? $"{parts[0]}_{parts[1]}" : levelID;
    }

    /// <summary>
    /// Same shape as <see cref="LevelFixture"/> for the beat-chain controller, plus
    /// the pause guard every level fixture needs: the base defers the entrance
    /// dialogue and a landed deferred call must not leave the tree paused.
    /// </summary>
    private sealed class PostBossFixture : IDisposable {
        public readonly PostBossChainTestLevelController Level;
        private readonly int _originalSlot;
        private readonly bool _originalPaused;

        public PostBossFixture() {
            var tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;

            Level = new PostBossChainTestLevelController { Name = "PostBossChainTestLevel" };
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }

    /// <summary>
    /// D12 hygiene: a case that completes a level advances the campaign level,
    /// banks or bonuses the wallet and leaves the run state behind. This hands
    /// the campaign position back (level, difficulty, character, wallet, slot)
    /// on dispose. Declare it BEFORE the level fixture so it is disposed after
    /// it: the fixture restores the slot it captured, PrepareDirectLevel clears
    /// it, so the slot is restored last.
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
    /// Builds a level controller in the runner tree against an optional scratch
    /// save, then restores the session and slot on dispose. The framework reads
    /// shared singletons, so every case has to hand them back untouched.
    /// </summary>
    private sealed class LevelFixture : IDisposable {
        public readonly FrameworkTestLevelController Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;

        public LevelFixture(StorySaveData save) {
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            Level = new FrameworkTestLevelController { Name = "FrameworkTestLevel" };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(Level);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
        }
    }
}
