using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 5 A1: the end-of-campaign chain. Level 15 calls
/// <see cref="CampaignCompletionSequence.Begin"/>; the credits roll (skippable),
/// the active save gets <c>IsCompleted</c> written through SaveManager, and the
/// hub's Temporal Portal stands down afterwards.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CampaignCompletionTests {
    /// <summary>Slot 2 is the scratch slot; every case restores it in a finally block.</summary>
    private const int ScratchSlot = 2;

    // === SaveManager completion helper ===

    [TestCase]
    public void MarkCampaignCompletedWritesTheFlagAndSurvivesAReloadFromDisk() {
        StorySaveData original = SaveManager.Instance.SaveSlots[ScratchSlot];
        try {
            SaveManager.Instance.SaveSlots[ScratchSlot] = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Alexandria),
                CompletedLevels = new List<string> { "level_15_alexandria" }
            };
            AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].IsCompleted).IsFalse();

            AssertThat(SaveManager.Instance.MarkCampaignCompleted(ScratchSlot)).IsTrue();
            AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].IsCompleted).IsTrue();

            // Round trip: the flag must come back out of the encrypted envelope.
            AssertThat(SaveManager.Instance.LoadStorySlot(ScratchSlot)).IsTrue();
            AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].IsCompleted).IsTrue();
            AssertString(SaveManager.Instance.SaveSlots[ScratchSlot].SelectedCharacterID).IsEqual("einstein");
        } finally {
            RestoreScratchSlot(original);
        }
    }

    [TestCase]
    public void MarkCampaignCompletedRefusesEmptyAndOutOfRangeSlots() {
        StorySaveData original = SaveManager.Instance.SaveSlots[ScratchSlot];
        try {
            SaveManager.Instance.SaveSlots[ScratchSlot] = null;
            AssertThat(SaveManager.Instance.MarkCampaignCompleted(ScratchSlot)).IsFalse();
            AssertThat(SaveManager.Instance.MarkCampaignCompleted(-1)).IsFalse();
            AssertThat(SaveManager.Instance.MarkCampaignCompleted(99)).IsFalse();
        } finally {
            RestoreScratchSlot(original);
        }
    }

    [TestCase]
    public void ActiveCampaignCompletionTracksTheSessionSlot() {
        StorySaveData original = SaveManager.Instance.SaveSlots[ScratchSlot];
        SessionData session = GameManager.Instance.CurrentSession;
        int originalSlot = session.ActiveSaveSlot;
        try {
            session.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession = session;
            SaveManager.Instance.SaveSlots[ScratchSlot] = new StorySaveData { SelectedCharacterID = "joan" };

            AssertThat(SaveManager.Instance.IsActiveCampaignCompleted()).IsFalse();
            AssertThat(SaveManager.Instance.MarkCampaignCompleted()).IsTrue();
            AssertThat(SaveManager.Instance.IsActiveCampaignCompleted()).IsTrue();
        } finally {
            RestoreScratchSlot(original);
            SessionData restore = GameManager.Instance.CurrentSession;
            restore.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession = restore;
        }
    }

    // === Credits roll ===

    [TestCase]
    public void CreditsRollFinishesOnItsOwnAfterScrollingThroughTheWholeList() {
        CreditsController credits = AttachCredits();
        int finished = 0;
        credits.CreditsFinished += () => finished++;
        try {
            AssertThat(credits.IsFinished).IsFalse();
            AssertThat(credits.TotalScrollDistance > 0f).IsTrue();

            // Advance past the end of the roll in one big frame.
            credits._Process(credits.TotalScrollDistance / credits.ScrollSpeed + 1.0);

            AssertThat(credits.IsFinished).IsTrue();
            AssertThat(finished).IsEqual(1);

            // Further frames must not re-raise the signal.
            credits._Process(1.0);
            AssertThat(finished).IsEqual(1);
        } finally {
            DetachAndFree(credits);
        }
    }

    [TestCase]
    public void SkippingTheCreditsEndsThemImmediatelyAndOnlyOnce() {
        CreditsController credits = AttachCredits();
        int finished = 0;
        credits.CreditsFinished += () => finished++;
        try {
            credits._Process(0.1);
            AssertThat(credits.IsFinished).IsFalse();
            AssertThat(credits.ScrolledDistance < credits.TotalScrollDistance).IsTrue();

            credits.Skip();
            AssertThat(credits.IsFinished).IsTrue();
            AssertThat(finished).IsEqual(1);

            credits.Skip();
            credits._Process(5.0);
            AssertThat(finished).IsEqual(1);
        } finally {
            DetachAndFree(credits);
        }
    }

    [TestCase]
    public void CreditsRunAlwaysSoAPausedTreeStillRollsAndEveryLineIsLocalized() {
        TranslationServer.SetLocale("en");
        CreditsController credits = AttachCredits();
        try {
            AssertThat(credits.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);
            AssertThat(CreditsController.CreditLines.Length > 0).IsTrue();
            foreach (CreditsController.CreditLine line in CreditsController.CreditLines) {
                AssertThat(TranslationServer.Translate(line.Key).ToString() != line.Key)
                    .OverrideFailureMessage($"Credit key '{line.Key}' is missing from localization/en.csv.")
                    .IsTrue();
            }
            AssertThat(TranslationServer.Translate("credits_skip_hint").ToString() != "credits_skip_hint").IsTrue();
        } finally {
            DetachAndFree(credits);
        }
    }

    [TestCase]
    public void TheAuthoredCreditsSceneInstantiatesAsTheController() {
        AssertThat(ResourceLoader.Exists(CreditsController.SceneResourcePath)).IsTrue();
        var packed = ResourceLoader.Load<PackedScene>(CreditsController.SceneResourcePath);
        AssertObject(packed).IsNotNull();
        Node instance = packed.Instantiate();
        AssertThat(instance is CreditsController).IsTrue();
        instance.Free();
    }

    // === Full chain ===

    [TestCase]
    public void TheCompletionChainRollsCreditsThenWritesTheCompletedFlagExactlyOnce() {
        StorySaveData original = SaveManager.Instance.SaveSlots[ScratchSlot];
        SessionData session = GameManager.Instance.CurrentSession;
        int originalSlot = session.ActiveSaveSlot;
        var host = new Node { Name = "CompletionHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);

        int finishedSignals = 0;
        CampaignCompletionSequence sequence = null;
        try {
            session.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession = session;
            SaveManager.Instance.SaveSlots[ScratchSlot] = new StorySaveData { SelectedCharacterID = "einstein" };

            // returnToMainMenu: false keeps the runner's scene tree in place.
            sequence = CampaignCompletionSequence.Begin(host, null, "", returnToMainMenu: false);
            sequence.SequenceFinished += () => finishedSignals++;

            AssertObject(sequence).IsNotNull();
            AssertObject(sequence.Credits).OverrideFailureMessage(
                "With no ending dialogue the chain must roll credits immediately.").IsNotNull();
            AssertThat(sequence.IsFinished).IsFalse();
            AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].IsCompleted).IsFalse();

            sequence.SkipToEnd();

            AssertThat(sequence.IsFinished).IsTrue();
            AssertThat(sequence.CampaignMarkedCompleted).IsTrue();
            AssertThat(finishedSignals).IsEqual(1);
            AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].IsCompleted).IsTrue();

            // Idempotent: a second skip cannot double-fire or re-save.
            sequence.SkipToEnd();
            AssertThat(finishedSignals).IsEqual(1);
        } finally {
            DetachAndFree(host);
            RestoreScratchSlot(original);
            SessionData restore = GameManager.Instance.CurrentSession;
            restore.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession = restore;
        }
    }

    [TestCase]
    public void TheChainWaitsForTheEndingDialogueBeforeRollingCredits() {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = "EndingDialogueHost" };
        tree.Root.AddChild(host);
        DialogueManager dialogue = DialogueManager.CreateDefault();
        host.AddChild(dialogue);
        dialogue.RegisterSetFromPath("res://resources/Dialogue/level_01_dialogue.tres");

        CampaignCompletionSequence sequence = null;
        bool pausedAfterTeardown;
        try {
            sequence = CampaignCompletionSequence.Begin(
                host, dialogue, "level_01.exit", returnToMainMenu: false);

            AssertThat(sequence.IsEndingDialogueActive)
                .OverrideFailureMessage("The chain must start the ending sequence itself.").IsTrue();
            AssertObject(sequence.Credits)
                .OverrideFailureMessage("Credits must not roll until the ending dialogue finishes.").IsNull();

            EventBus.Instance.RaiseDialogueComplete("some.other.sequence");
            AssertObject(sequence.Credits).IsNull();

            EventBus.Instance.RaiseDialogueComplete("level_01.exit");
            AssertObject(sequence.Credits).IsNotNull();
            AssertThat(sequence.IsEndingDialogueActive).IsFalse();
        } finally {
            DetachAndFree(host);
            // level_01.exit is a PausesGameplay sequence, and the EventBus route
            // above deliberately bypasses DialogueManager.EndSequence(). Sample the
            // pause flag, clear it unconditionally so a regression can never wedge
            // the rest of the session, then assert on the sample.
            pausedAfterTeardown = tree.Paused;
            tree.Paused = false;
        }
        AssertThat(pausedAfterTeardown).OverrideFailureMessage(
            "DialogueManager left SceneTree.Paused set after teardown.").IsFalse();
    }

    /// <summary>
    /// Regression guard for the Package 5 A1 suite hang. A PausesGameplay sequence
    /// owns SceneTree.Paused; if the manager is torn down mid-sequence nothing else
    /// gives it back and every later frame in the process is frozen - in game the
    /// next scene loads dead, and under GdUnit the runner's transport node (default
    /// Inherit process mode) stops pumping and the session times out.
    /// </summary>
    [TestCase]
    public void ADialogueManagerTornDownMidSequenceHandsBackTheGameplayPause() {
        var tree = (SceneTree)Engine.GetMainLoop();
        bool pausedDuringSequence;
        bool pausedAfterTeardown;
        var host = new Node { Name = "PauseLeakHost" };
        tree.Root.AddChild(host);
        try {
            DialogueManager dialogue = DialogueManager.CreateDefault();
            host.AddChild(dialogue);
            AssertThat(dialogue.RegisterSetFromPath("res://resources/Dialogue/level_01_dialogue.tres"))
                .IsTrue();

            AssertThat(dialogue.StartSequence("level_01.exit")).IsTrue();
            pausedDuringSequence = tree.Paused;

            DetachAndFree(host);
            host = null;
            pausedAfterTeardown = tree.Paused;
        } finally {
            DetachAndFree(host);
            tree.Paused = false;
        }

        AssertThat(pausedDuringSequence).OverrideFailureMessage(
            "level_01.exit is authored PausesGameplay = true; it must pause the tree.").IsTrue();
        AssertThat(pausedAfterTeardown).OverrideFailureMessage(
            "Freeing the DialogueManager mid-sequence must release SceneTree.Paused.").IsFalse();
    }

    // === Post-campaign hub ===

    [TestCase]
    public void TheHubPortalStandsDownOnceTheCampaignIsComplete() {
        AssertThat(HubWorldController.ResolvePortalAction(false, false))
            .IsEqual(HubWorldController.HubPortalAction.OpenMission);
        AssertThat(HubWorldController.ResolvePortalAction(false, true))
            .IsEqual(HubWorldController.HubPortalAction.TimelineRestartChoice);
        // Completion wins over a pending collapse: there is no level left to retry.
        AssertThat(HubWorldController.ResolvePortalAction(true, false))
            .IsEqual(HubWorldController.HubPortalAction.TimelineRestored);
        AssertThat(HubWorldController.ResolvePortalAction(true, true))
            .IsEqual(HubWorldController.HubPortalAction.TimelineRestored);
    }

    [TestCase]
    public void TheHubReadsCompletionFromTheActiveSaveAndHasLocalizedCopyForIt() {
        TranslationServer.SetLocale("en");
        foreach (string key in new[] {
            HubWorldController.TimelineRestoredMessageKey, "hub_campaign_complete", "save_completed"
        }) {
            AssertThat(TranslationServer.Translate(key).ToString() != key)
                .OverrideFailureMessage($"Hub/menu completion key '{key}' is missing from localization/en.csv.")
                .IsTrue();
        }

        StorySaveData original = SaveManager.Instance.SaveSlots[ScratchSlot];
        SessionData session = GameManager.Instance.CurrentSession;
        int originalSlot = session.ActiveSaveSlot;
        try {
            session.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession = session;
            SaveManager.Instance.SaveSlots[ScratchSlot] = new StorySaveData { SelectedCharacterID = "joan" };
            AssertThat(HubWorldController.IsCampaignCompleted()).IsFalse();

            SaveManager.Instance.SaveSlots[ScratchSlot].IsCompleted = true;
            AssertThat(HubWorldController.IsCampaignCompleted()).IsTrue();
        } finally {
            RestoreScratchSlot(original);
            SessionData restore = GameManager.Instance.CurrentSession;
            restore.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession = restore;
        }
    }

    // === Helpers ===

    private static CreditsController AttachCredits() {
        CreditsController credits = CreditsController.CreateDefault();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(credits);
        return credits;
    }

    /// <summary>
    /// Puts the scratch slot back the way the suite found it, on disk as well as
    /// in memory, so a developer's real save is untouched by a test run.
    /// </summary>
    private static void RestoreScratchSlot(StorySaveData original) {
        int activeSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        SaveManager.Instance.SaveSlots[ScratchSlot] = original;
        // DeleteStorySlot clears the session pointer when it matches; put it back.
        if (original == null) SaveManager.Instance.DeleteStorySlot(ScratchSlot);
        else SaveManager.Instance.SaveStorySlot(ScratchSlot);
        GameManager.Instance.CurrentSession.ActiveSaveSlot = activeSlot;
    }

    private static void DetachAndFree(Node node) {
        if (node == null || !GodotObject.IsInstanceValid(node)) return;
        node.GetParent()?.RemoveChild(node);
        node.Free();
    }
}
