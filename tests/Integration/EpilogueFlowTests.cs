using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Integration;

/// <summary>
/// Package 13 W3 (S39/S40/S43/S48): the ending closes on the Time-Ship. After the
/// release at the Founding comes the send-off (Okafor, Wren, Sarah, the hero's
/// farewell, the step through the portal), then the credits — the completion
/// flag still written as they begin — then the once-only After-Credits
/// Homecoming. A completed save's hub portal offers <b>Go home</b>, which replays
/// the send-off and the credits and never the Homecoming.
///
/// <para>Every case works on scratch slot 2, restores it, and frees its host in
/// <c>finally</c>. The epilogue sequences do not pause, so no case takes the
/// tree's pause; each still restores it defensively.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EpilogueFlowTests {

    private const int ScratchSlot = 2;

    [TestCase]
    public void TheEpilogueSetAuthorsTheSendOffAndTheHomecomingAsDesigned() {
        var set = AuthoredResources.Load<DialogueSetData>(CampaignCompletionSequence.EpilogueDialoguePath);
        AssertObject(set).IsNotNull();
        DialogueSequenceData sendOff = set.Find(CampaignCompletionSequence.SendOffSequenceID);
        AssertThat(sendOff.SpeakerNameKeys).ContainsExactly(
            "speaker_okafor", "speaker_wren", "speaker_sarah", "speaker_player", "speaker_narration");
        AssertThat(sendOff.PausesGameplay)
            .OverrideFailureMessage("The send-off hands off to the credits and a scene change; it must not pause.")
            .IsFalse();
        DialogueSequenceData homecoming = set.Find(CampaignCompletionSequence.HomecomingSequenceID);
        AssertThat(homecoming.SpeakerNameKeys).ContainsExactly(
            "speaker_meridian_officer", "speaker_sarah", "speaker_meridian_officer",
            "speaker_sarah", "speaker_meridian_officer", "speaker_sarah");
        AssertThat(homecoming.PausesGameplay).IsFalse();

        TranslationServer.SetLocale("en");
        foreach (string key in new[] { "speaker_wren", "speaker_meridian_officer", "speaker_unknown_voice" }) {
            AssertThat(TranslationServer.Translate(key).ToString() != key)
                .OverrideFailureMessage($"{key} does not resolve through the compiled translation.").IsTrue();
        }
        AssertString(TranslationServer.Translate("dlg_epilogue_homecoming_6").ToString())
            .IsEqual("It's a long one. Start with what I got wrong.");
    }

    [TestCase]
    public void TheFullChainRunsSendOffThenCreditsThenHomecomingOnce() {
        var tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        StorySaveData original = SaveManager.Instance.SaveSlots[ScratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        var host = new Node { Name = "EpilogueHost" };
        tree.Root.AddChild(host);
        try {
            UseScratch(new StorySaveData { SelectedCharacterID = "joan" });
            DialogueManager dialogue = NewDialogue(host);

            CampaignCompletionSequence chain = CampaignCompletionSequence.Begin(
                host, dialogue, "", returnToMainMenu: false,
                sendOffDialogueID: CampaignCompletionSequence.SendOffSequenceID,
                homecomingDialogueID: CampaignCompletionSequence.HomecomingSequenceID);
            AssertThat(chain.IsSendOffActive).IsTrue();
            AssertObject(chain.Credits).IsNull();
            AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].IsCompleted)
                .OverrideFailureMessage("The flag is written as the credits begin, not during the send-off.")
                .IsFalse();

            EventBus.Instance.RaiseDialogueComplete(CampaignCompletionSequence.SendOffSequenceID);
            AssertThat(chain.CreditsRolling).IsTrue();
            AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].IsCompleted).IsTrue();

            chain.Credits.Skip();
            AssertThat(chain.IsHomecomingActive).IsTrue();
            AssertThat(chain.HomecomingPlayed).IsTrue();
            AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].HomecomingSeen)
                .OverrideFailureMessage("The Homecoming records itself as it starts, so a quit never replays it.")
                .IsTrue();
            AssertThat(chain.IsFinished).IsFalse();

            EventBus.Instance.RaiseDialogueComplete(CampaignCompletionSequence.HomecomingSequenceID);
            AssertThat(chain.IsFinished).IsTrue();
        } finally {
            Teardown(host, original, originalSlot, tree, originalPaused);
        }
    }

    [TestCase]
    public void TheHomecomingPlaysOncePerCampaign() {
        var tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        StorySaveData original = SaveManager.Instance.SaveSlots[ScratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        var host = new Node { Name = "HomecomingOnceHost" };
        tree.Root.AddChild(host);
        try {
            UseScratch(new StorySaveData { SelectedCharacterID = "tesla", HomecomingSeen = true });
            DialogueManager dialogue = NewDialogue(host);
            CampaignCompletionSequence chain = CampaignCompletionSequence.Begin(
                host, dialogue, "", returnToMainMenu: false,
                sendOffDialogueID: "", homecomingDialogueID: CampaignCompletionSequence.HomecomingSequenceID);
            AssertThat(chain.CreditsRolling).IsTrue();
            chain.Credits.Skip();
            AssertThat(chain.HomecomingPlayed).IsFalse();
            AssertThat(chain.IsFinished).IsTrue();
        } finally {
            Teardown(host, original, originalSlot, tree, originalPaused);
        }
    }

    [TestCase]
    public void GoHomeReplaysTheSendOffAndCreditsButNeverTheHomecoming() {
        var tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        StorySaveData original = SaveManager.Instance.SaveSlots[ScratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        var host = new Node { Name = "GoHomeHost" };
        tree.Root.AddChild(host);
        try {
            UseScratch(new StorySaveData { SelectedCharacterID = "mozart", IsCompleted = true });
            AssertThat(HubWorldController.ResolvePortalAction(campaignCompleted: true, pendingTimelineRestart: false))
                .IsEqual(HubWorldController.HubPortalAction.GoHome);

            DialogueManager dialogue = NewDialogue(host);
            CampaignCompletionSequence chain =
                CampaignCompletionSequence.BeginGoHome(host, dialogue, returnToMainMenu: false);
            AssertString(chain.EndingDialogueID).IsEqual("");
            AssertThat(chain.IsSendOffActive).IsTrue();

            EventBus.Instance.RaiseDialogueComplete(CampaignCompletionSequence.SendOffSequenceID);
            AssertThat(chain.CreditsRolling).IsTrue();
            chain.Credits.Skip();
            AssertThat(chain.HomecomingPlayed).IsFalse();
            AssertThat(chain.IsFinished).IsTrue();
            AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].HomecomingSeen)
                .OverrideFailureMessage("Go home must not consume the Homecoming.").IsFalse();
        } finally {
            Teardown(host, original, originalSlot, tree, originalPaused);
        }
    }

    private static DialogueManager NewDialogue(Node host) {
        DialogueManager dialogue = DialogueManager.CreateDefault();
        host.AddChild(dialogue);
        return dialogue;
    }

    private static void UseScratch(StorySaveData save) {
        SaveManager.Instance.SaveSlots[ScratchSlot] = save;
        SessionData session = GameManager.Instance.CurrentSession;
        session.ActiveSaveSlot = ScratchSlot;
        GameManager.Instance.CurrentSession = session;
    }

    private static void Teardown(Node host, StorySaveData original, int originalSlot, SceneTree tree, bool paused) {
        if (host != null && GodotObject.IsInstanceValid(host)) {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
        SaveManager.Instance.SaveSlots[ScratchSlot] = original;
        if (original != null) SaveManager.Instance.SaveStorySlot(ScratchSlot);
        else SaveManager.Instance.DeleteStorySlot(ScratchSlot);
        SessionData session = GameManager.Instance.CurrentSession;
        session.ActiveSaveSlot = originalSlot;
        GameManager.Instance.CurrentSession = session;
        tree.Paused = paused;
    }
}
