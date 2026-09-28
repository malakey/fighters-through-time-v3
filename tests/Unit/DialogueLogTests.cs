using System.Linq;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W6 (G10). The session Dialogue Log: the last 100 lines, fed by the
/// completion path <b>and</b> the confirmed-skip path, never saved, readable from
/// a Log action during dialogue. Text Speed is read at sequence start.
///
/// <para>Every case restores <see cref="SceneTree.Paused"/>, the session and the
/// scratch slot in a finally block (the chosen sequence pauses gameplay).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DialogueLogTests {

    private const string DialogueSetPath = "res://resources/Dialogue/level_08_dialogue.tres";
    private const string SequenceID = "level_08.postboss";

    [TestCase]
    public void AWatchedSequenceLogsEachLineAsItIsShown() {
        DialogueManager dialogue = Attach();
        WithScratchSlot(() => {
            DialogueLog.Clear();
            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();
            int lines = dialogue.FindSequence(SequenceID).LineCount;
            AssertThat(DialogueLog.Count).IsEqual(1);
            while (dialogue.IsSequenceActive) dialogue.AdvanceLine();
            AssertThat(DialogueLog.Count).IsEqual(lines);
            AssertThat(DialogueLog.Entries.All(entry => !entry.FromSkip)).IsTrue();
            AssertThat(DialogueLog.Entries.All(entry => entry.Text.Length > 0 && entry.SequenceID == SequenceID)).IsTrue();
        }, dialogue);
    }

    [TestCase]
    public void AConfirmedSkipStillAddsTheUnseenLinesSoTheSceneCanBeRead() {
        DialogueManager dialogue = Attach();
        WithScratchSlot(() => {
            DialogueLog.Clear();
            FTT.Core.SaveManager.Instance.GlobalData.SeenDialogueIDs.Remove(SequenceID);
            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();
            int lines = dialogue.FindSequence(SequenceID).LineCount;
            AssertThat(lines > 1).IsTrue();

            dialogue.AdvanceSkipHold(DialogueManager.HoldToSkipSeconds + 0.1f);
            AssertThat(dialogue.IsSkipConfirmOpen).IsTrue();
            dialogue.ConfirmSkip();

            AssertThat(dialogue.IsSequenceActive).IsFalse();
            AssertThat(DialogueLog.Count).IsEqual(lines);
            AssertThat(DialogueLog.Entries.Count(entry => entry.FromSkip)).IsEqual(lines - 1);
        }, dialogue);
    }

    [TestCase]
    public void TheLogKeepsTheLastHundredLinesAndIsNeverInASavePayload() {
        DialogueLog.Clear();
        try {
            for (int index = 0; index < 130; index++) {
                DialogueLog.Append(new DialogueLog.Entry("seq", "Speaker", $"line {index}", "", false));
            }
            AssertThat(DialogueLog.Count).IsEqual(DialogueLog.Capacity);
            AssertThat(DialogueLog.Entries.First().Text).IsEqual("line 30");
            AssertThat(DialogueLog.Entries.Last().Text).IsEqual("line 129");

            string global = JsonConvert.SerializeObject(new GlobalSaveData());
            string story = JsonConvert.SerializeObject(new StorySaveData());
            AssertThat(global.Contains("line 129") || global.Contains("DialogueLog")).IsFalse();
            AssertThat(story.Contains("line 129") || story.Contains("DialogueLog")).IsFalse();
        } finally {
            DialogueLog.Clear();
        }
    }

    [TestCase]
    public void TheLogActionOpensTheLogDuringDialogueAndItHoldsTheReveal() {
        DialogueManager dialogue = Attach();
        WithScratchSlot(() => {
            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();
            var press = new InputEventAction { Action = InputManager.Actions.DialogueLog, Pressed = true };
            dialogue._UnhandledInput(press);
            AssertThat(dialogue.IsLogOpen).IsTrue();

            // While the log is up, a confirm does not advance the scene underneath.
            int before = DialogueLog.Count;
            dialogue._UnhandledInput(new InputEventAction { Action = "ui_accept", Pressed = true });
            AssertThat(DialogueLog.Count).IsEqual(before);

            var screen = dialogue.GetNode<DialogueLogScreen>("DialogueLogScreen");
            AssertThat(screen.Rows.GetChildCount()).IsEqual(DialogueLog.Count);
            screen.Close();
            AssertThat(dialogue.IsLogOpen).IsFalse();
            AssertThat(dialogue.IsSequenceActive).IsTrue();
        }, dialogue);
    }

    [TestCase]
    public void TheSavedTextSpeedIsReadWhenASequenceStarts() {
        DialogueManager dialogue = Attach();
        GlobalSaveData data = SaveManager.Instance.GlobalData;
        TextSpeed original = data.DialogueTextSpeed;
        WithScratchSlot(() => {
            data.DialogueTextSpeed = TextSpeed.Instant;
            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();
            AssertThat(dialogue.Reveal.Speed).IsEqual(TextSpeed.Instant);
            AssertThat(dialogue.Reveal.IsComplete).IsTrue();
        }, dialogue);
        data.DialogueTextSpeed = original;
    }

    // === Helpers (the DialoguePresentationTests scratch-slot idiom) ===

    private static void WithScratchSlot(System.Action body, DialogueManager dialogue) {
        const int scratchSlot = 2;
        var saveManager = SaveManager.Instance;
        var gameManager = GameManager.Instance;
        StorySaveData original = saveManager.SaveSlots[scratchSlot];
        SessionData originalSession = gameManager.CurrentSession;
        var originalSeen = new System.Collections.Generic.HashSet<string>(
            saveManager.GlobalData.SeenDialogueIDs ?? new System.Collections.Generic.HashSet<string>(),
            System.StringComparer.Ordinal);
        var tree = (SceneTree)Engine.GetMainLoop();
        try {
            saveManager.SaveSlots[scratchSlot] = new StorySaveData { SelectedCharacterID = "einstein" };
            SessionData session = gameManager.CurrentSession;
            session.ActiveSaveSlot = scratchSlot;
            session.SelectedCharacterID = "einstein";
            gameManager.CurrentSession = session;
            body();
        } finally {
            if (dialogue != null && GodotObject.IsInstanceValid(dialogue)) {
                dialogue.GetParent()?.RemoveChild(dialogue);
                dialogue.Free();
            }
            tree.Paused = false;
            saveManager.SaveSlots[scratchSlot] = original;
            gameManager.CurrentSession = originalSession;
            saveManager.GlobalData.SeenDialogueIDs = originalSeen;
        }
    }

    private static DialogueManager Attach() {
        DialogueManager dialogue = DialogueManager.CreateDefault();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(dialogue);
        return dialogue;
    }
}
