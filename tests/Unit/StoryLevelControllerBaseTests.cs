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
    public void TallyForTest(int amount) => TallyDust(amount);
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
    public void KilledEnemiesTallyForTheResultsPanelAndReRaiseTheDustAward() {
        using var fixture = new LevelFixture(null);
        int awarded = 0;
        void OnDust(int amount) => awarded += amount;
        EventBus.Instance.OnChronalDustCollected += OnDust;
        try {
            EventBus.Instance.RaiseEnemyKilled(new EnemyKilledPayload {
                EnemyID = "chrono_slasher", ChronalDustDrop = 5
            });
            EventBus.Instance.RaiseEnemyKilled(new EnemyKilledPayload {
                EnemyID = "tech_enforcer", ChronalDustDrop = 10
            });

            AssertThat(fixture.Level.DustEarnedThisLevel).IsEqual(15);
            AssertThat(awarded).IsEqual(15);

            // Boss and extractor tallies add to the panel without a second award.
            fixture.Level.TallyForTest(35);
            AssertThat(fixture.Level.DustEarnedThisLevel).IsEqual(50);
            AssertThat(awarded).IsEqual(15);
        } finally {
            EventBus.Instance.OnChronalDustCollected -= OnDust;
        }
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

    private static string PrefixOf(string levelID) {
        string[] parts = levelID.Split('_');
        return parts.Length >= 2 ? $"{parts[0]}_{parts[1]}" : levelID;
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
