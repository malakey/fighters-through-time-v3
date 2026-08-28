using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.3 checkpoint save ordering: activation raises OnCheckpointReached (the
/// gameplay half — rewind-pool refresh included) and THEN
/// OnCheckpointCommitted (the persistence half SaveManager listens to), so
/// the checkpoint save always captures post-refresh state. Under the old
/// single event, SaveManager subscribed at boot — before any level's rewind
/// manager — and wrote save.CurrentLives pre-refresh.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CheckpointSaveOrderingTests {

    [TestCase]
    public void ReachedFiresBeforeCommittedOnOneActivation() {
        StoryManager.Instance.ClearLevelAttemptState();
        var trigger = new CheckpointTrigger {
            Name = "OrderPinCheckpoint",
            CheckpointID = "order_pin_checkpoint"
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(trigger);
        var order = new System.Collections.Generic.List<string>();
        void OnReached(string id) { if (id == "order_pin_checkpoint") order.Add("reached"); }
        void OnActivated(CheckpointReachedPayload payload) {
            if (payload.CheckpointID == "order_pin_checkpoint") order.Add($"activated:{payload.FirstActivation}");
        }
        void OnCommitted(string id) { if (id == "order_pin_checkpoint") order.Add("committed"); }
        EventBus.Instance.OnCheckpointReached += OnReached;
        EventBus.Instance.OnCheckpointActivated += OnActivated;
        EventBus.Instance.OnCheckpointCommitted += OnCommitted;
        try {
            trigger.Activate();
            AssertThat(string.Join(",", order))
                .OverrideFailureMessage("Gameplay (reached/activated) must precede persistence (committed).")
                .IsEqual("reached,activated:True,committed");

            order.Clear();
            trigger.Activate();
            AssertThat(string.Join(",", order))
                .OverrideFailureMessage("Re-activation still saves, but is no longer a first activation.")
                .IsEqual("reached,activated:False,committed");
        } finally {
            EventBus.Instance.OnCheckpointReached -= OnReached;
            EventBus.Instance.OnCheckpointActivated -= OnActivated;
            EventBus.Instance.OnCheckpointCommitted -= OnCommitted;
            SessionExitGuard.ClearMarker();
            StoryManager.Instance.ClearLevelAttemptState();
            trigger.Free();
        }
    }

    [TestCase]
    public void TheCheckpointSaveCapturesThePostRefreshRewindPool() {
        const int scratchSlot = 2;
        StoryManager story = StoryManager.Instance;
        SaveManager saveManager = SaveManager.Instance;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        StorySaveData original = saveManager.SaveSlots[scratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        int originalRewinds = story.ChronalRewindsRemaining;
        story.ClearLevelAttemptState();
        var manager = new ChronalRewindManager { Name = "OrderingRewindManager" };
        var trigger = new CheckpointTrigger {
            Name = "OrderingCheckpoint",
            CheckpointID = "ordering_test_checkpoint"
        };
        try {
            saveManager.SaveSlots[scratchSlot] = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLives = 1
            };
            GameManager.Instance.CurrentSession.ActiveSaveSlot = scratchSlot;
            // Normal difficulty, one rewind left: the checkpoint refresh
            // restores +1, and the save must record 2, never the stale 1.
            story.SetRewinds(1);
            tree.Root.AddChild(manager);
            tree.Root.AddChild(trigger);

            trigger.Activate();

            AssertThat(manager.RemainingRewinds).IsEqual(2);
            AssertThat(saveManager.SaveSlots[scratchSlot].CurrentLives)
                .OverrideFailureMessage(
                    "save.CurrentLives must be the POST-refresh pool — persistence runs on Committed.")
                .IsEqual(2);
            // And the attempt registry rode along in the same save.
            AssertThat(saveManager.SaveSlots[scratchSlot].ActivatedCheckpointIDs)
                .Contains("ordering_test_checkpoint");
        } finally {
            SessionExitGuard.ClearMarker();
            story.ClearLevelAttemptState();
            story.SetRewinds(originalRewinds);
            manager.Free();
            trigger.Free();
            saveManager.SaveSlots[scratchSlot] = original;
            if (original == null) saveManager.DeleteStorySlot(scratchSlot);
            else saveManager.SaveStorySlot(scratchSlot);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
        }
    }
}
