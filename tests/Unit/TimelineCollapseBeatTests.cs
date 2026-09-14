using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.3 Timeline Collapse beat: the campaign's only failure state earns a
/// ~4 second fracture presentation before the extraction to the hub — never
/// a bare scene cut. The first viewing is unskippable; once seen (persisted
/// on the save as HasSeenCollapseBeat), a press skips it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TimelineCollapseBeatTests {

    [TestCase]
    public void TheBeatLastsFourSecondsAndTheFirstViewingIsUnskippable() {
        AssertThat(ChronalRewindManager.CollapseBeatSeconds).IsEqual(4f);
        // The pure countdown rule: a skip press is ignored until skippable.
        AssertThat(ChronalRewindManager.TickCollapseBeat(4f, 1f, skipPressed: true, skippable: false)).IsEqual(3f);
        AssertThat(ChronalRewindManager.TickCollapseBeat(3f, 1f, skipPressed: false, skippable: true)).IsEqual(2f);
        AssertThat(ChronalRewindManager.TickCollapseBeat(3f, 1f, skipPressed: true, skippable: true)).IsEqual(0f);
        AssertThat(ChronalRewindManager.TickCollapseBeat(0.25f, 1f, skipPressed: false, skippable: false)).IsEqual(0f);
    }

    [TestCase]
    public void TheManagerRunsTheBeatOnceUnskippableThenSkippableAndPersistsTheFlag() {
        StoryManager story = StoryManager.Instance;
        Difficulty difficulty = GameManager.Instance.CurrentSession.Difficulty;
        var manager = new ChronalRewindManager { Name = "CollapseBeatManager" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(manager);
        int completions = 0;
        manager.CollapseCompletionOverrideForTesting = () => completions++;
        try {
            story.ResetCampaignState(difficulty);
            AssertThat(story.HasSeenCollapseBeat).IsFalse();

            // First viewing: unskippable, runs the whole 4 seconds.
            manager.BeginCollapseBeat();
            AssertThat(manager.IsCollapseBeatActive).IsTrue();
            AssertThat(manager.IsCollapseBeatSkippable).IsFalse();
            AssertThat(manager.AdvanceCollapseBeat(1f, skipPressed: true)).IsFalse();
            AssertThat(manager.CollapseBeatSecondsRemaining).IsEqual(3f);
            AssertThat(manager.AdvanceCollapseBeat(2.5f, skipPressed: true)).IsFalse();
            AssertThat(manager.AdvanceCollapseBeat(0.5f, skipPressed: false)).IsTrue();
            AssertThat(manager.IsCollapseBeatActive).IsFalse();
            AssertThat(completions).IsEqual(1);
            AssertThat(story.HasSeenCollapseBeat)
                .OverrideFailureMessage("Completing the beat must mark it seen for later skips.")
                .IsTrue();

            // Second collapse: the beat is skippable, and a press ends it.
            manager.BeginCollapseBeat();
            AssertThat(manager.IsCollapseBeatSkippable).IsTrue();
            AssertThat(manager.AdvanceCollapseBeat(0.1f, skipPressed: true)).IsTrue();
            AssertThat(completions).IsEqual(2);

            // The flag rides the attempt-state save block (schema v5).
            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            story.WriteAttemptStateToSave(save);
            AssertThat(save.HasSeenCollapseBeat).IsTrue();
            story.ResetCampaignState(difficulty);
            AssertThat(story.HasSeenCollapseBeat).IsFalse();
            story.RestoreAttemptStateFromSave(save);
            AssertThat(story.HasSeenCollapseBeat).IsTrue();
        } finally {
            manager.CollapseCompletionOverrideForTesting = null;
            story.ResetCampaignState(difficulty);
            manager.Free();
        }
    }

    [TestCase]
    public void TheCollapsePayloadCarriesTheFractureTreatmentAndSkipPrompt() {
        RewindPresentationPayload first = ChronalRewindManager.CreateCollapsePayload(
            new Vector2(10, 20), skippable: false);
        AssertThat(first.Phase).IsEqual(RewindPresentationPhase.TimelineCollapse);
        AssertThat(first.CollapseSkipPromptEnabled).IsFalse();
        AssertThat(first.ScreenTintEnabled).IsTrue();

        RewindPresentationPayload seen = ChronalRewindManager.CreateCollapsePayload(
            Vector2.Zero, skippable: true);
        AssertThat(seen.CollapseSkipPromptEnabled)
            .OverrideFailureMessage("Once seen, the beat advertises press-to-skip.")
            .IsTrue();
    }

    // === Package 11 A3b: the cause split and the Act III branch ============

    [TestCase]
    public void ATimerCausedCollapseIsDistinctFromADeathAndCarriesSarahsEraLostLine() {
        // V7.6 F01 gave the beat two causes. Only the timer cause grants an F11
        // recovery minimum, and only it plays the era-lost transmission — so the
        // cause has to survive from the trigger to the resolution, not be
        // re-derived at the far end.
        AssertThat(ChronalRewindManager.EraLostCollapseLineKey)
            .IsEqual("dialogue_collapse_sarah_era_lost");

        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        const int scratchSlot = 2;
        StorySaveData original = saves.SaveSlots[scratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        CampaignLevel originalLevel = story.CurrentLevel;
        story.SuppressSceneLoadsForTesting = true;
        try {
            story.PrepareDirectLevel(CampaignLevel.Orleans, "einstein", Difficulty.Normal);
            saves.SaveSlots[scratchSlot] = new StorySaveData { SelectedCharacterID = "einstein" };
            GameManager.Instance.CurrentSession.ActiveSaveSlot = scratchSlot;
            story.BeginLevelRun();

            story.BeginTimelineCollapse("orleans_checkpoint_1", TimelineCollapseCause.Timer);
            AssertThat(story.PendingCollapseCause).IsEqual(TimelineCollapseCause.Timer);
            AssertThat(story.LastRequestedScenePath)
                .OverrideFailureMessage("Acts I-II collapse still extracts to the Time-Ship.")
                .IsEqual(StoryManager.HubScenePath);
            AssertThat(story.CurrentAttempt.Status)
                .OverrideFailureMessage("The hub portal finishes the committed paid recovery.")
                .IsEqual(StoryAttemptStatus.AwaitingHubResume);
            AssertThat(story.CurrentAttempt.RecoveryEvent.Cause).IsEqual(StoryRecoveryCause.Collapse);
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            saves.SaveSlots[scratchSlot] = original;
            story.PrepareDirectLevel(originalLevel, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void TheActIIIBranchResolvesASnapInsteadOfAHubExtraction() {
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        const int scratchSlot = 2;
        StorySaveData original = saves.SaveSlots[scratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        CampaignLevel originalLevel = story.CurrentLevel;
        story.SuppressSceneLoadsForTesting = true;
        try {
            story.PrepareDirectLevel(CampaignLevel.Alexandria, "einstein", Difficulty.Normal);
            saves.SaveSlots[scratchSlot] = new StorySaveData { SelectedCharacterID = "einstein" };
            GameManager.Instance.CurrentSession.ActiveSaveSlot = scratchSlot;
            story.BeginLevelRun();
            AssertThat(story.AnchorChargesRemaining).IsEqual(2);

            // The SAME entry point a death with no rewind charge uses. In Act III
            // it never reaches the hub extraction at all.
            story.BeginTimelineCollapse("level_15_alexandria_checkpoint_1");
            AssertThat(story.LastRequestedScenePath)
                .OverrideFailureMessage("Act III has no hub leg: the Snap stays in the level.")
                .IsEqual(StoryManager.GetLevelScenePath(CampaignLevel.Alexandria));
            AssertThat(story.AnchorChargesRemaining).IsEqual(1);
            AssertThat(story.HasPendingTimelineRestart)
                .OverrideFailureMessage("There is no hub restart choice to offer in the gauntlet.")
                .IsFalse();
            AssertThat(story.CurrentAttempt.RecoveryEvent.Cause).IsEqual(StoryRecoveryCause.AnchorSnap);

            // And A3's hook is armed for the timer path rather than left null.
            AssertThat(story.ActIIICollapseOverride)
                .OverrideFailureMessage("BeginLevelRun arms the Act III collapse hook.")
                .IsNotNull();
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            saves.SaveSlots[scratchSlot] = original;
            story.PrepareDirectLevel(originalLevel, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }
}
