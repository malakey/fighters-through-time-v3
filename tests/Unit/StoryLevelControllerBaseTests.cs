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
        BuildCheckpoint(200, 850, $"{TestLevelID}_checkpoint_0");
        MidCheckpoint = BuildCheckpoint(2000, 850, $"{TestLevelID}_checkpoint_1");
        BuildCheckpoint(3600, 850, $"{TestLevelID}_checkpoint_2");
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
        }
    }

    [TestCase]
    public void ExtractorDestructionSpawnsAPickupAndCollectionEntersTheTallyOnceAndItemizes() {
        // Audit M-1 reworked by the V7.3 Single Icon Rule: destruction spawns a
        // physical pickup; the wallet is paid — and the extractor line labeled —
        // only when it is collected.
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
            AssertThat(pickup.ForceLargeTier).IsTrue();

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
