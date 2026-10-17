using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-PADUI (2026-10-16; decision 9.1, pad-only play first-class): pad A is now
/// <c>ui_accept</c> and pad B <c>ui_cancel</c>, so a pad confirms a focused
/// button and backs out of a screen. Both buttons are gameplay buttons too (A
/// Jump, B Interact), and Godot's buttons never mark the confirm handled, so
/// these cases also pin that one press does one thing:
/// <list type="bullet">
/// <item>the press that closes a pause, or ends a gameplay-pausing dialogue,
/// never reaches the game as a fresh press on the first live tick;</item>
/// <item>a dialogue line under an open pause menu never advances;</item>
/// <item>a focused menu button never takes the same press as the screen over
/// it (the credits replay skip).</item>
/// </list>
///
/// <para>Real physics frames and real <see cref="InputEvent"/>s pushed through
/// the viewport; the polled game frames come from a fake device probe. Every
/// case restores the tree pause, the session, the devices and the global seen
/// set it touched in <c>finally</c>.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PadMenuConfirmCancelTests {

    [TestCase]
    public async Task PadAPressesTheFocusedModalButtonAndPadBCancelsTheModal() {
        var host = new Control { Name = "PadModalHost" };
        host.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(host);
        ConfirmModal modal = ConfirmModal.Create("pause_quit_confirm");
        host.AddChild(modal);
        int confirmed = 0;
        int cancelled = 0;
        modal.Confirmed += () => confirmed++;
        modal.Cancelled += () => cancelled++;
        try {
            await Frames(tree, 1);
            // Cancel takes initial focus: A arms it on the press and fires on the release.
            modal.Open();
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            AssertThat(cancelled).IsEqual(0);
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            AssertThat(cancelled)
                .OverrideFailureMessage("Pad A did not press the modal's focused Cancel button.")
                .IsEqual(1);
            AssertThat(modal.IsOpen).IsFalse();

            modal.Open();
            modal.ConfirmButton.GrabFocus();
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            AssertThat(confirmed).IsEqual(1);

            modal.Open();
            host.GetViewport().PushInput(Pad(1, JoyButton.B, pressed: true));
            AssertThat(cancelled)
                .OverrideFailureMessage("Pad B did not back out of the modal.")
                .IsEqual(2);
            AssertThat(modal.IsOpen).IsFalse();
            AssertThat(confirmed).IsEqual(1);
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    [TestCase]
    public async Task PadBBacksOutOfTheStoryPauseAndItsPressNeverReachesTheHero() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        input.AssignJoypadToPlayer(0, 0);
        var host = new Node { Name = "PadPauseHost" };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(host);
        PauseMenu pause = PauseMenu.CreateDefault();
        host.AddChild(pause);
        try {
            await Frames(tree, 2);
            pause.SetPaused(true);
            // Real input arrives between physics ticks, never inside one.
            await Idle(tree);
            probe.SetButton(0, JoyButton.B, true);
            host.GetViewport().PushInput(Pad(0, JoyButton.B, pressed: true));
            AssertThat(pause.IsPaused)
                .OverrideFailureMessage("Pad B did not back out of the Story pause.")
                .IsFalse();
            AssertThat(tree.Paused).IsFalse();

            PlayerInputFrame frame = await NextTickFrame(tree);
            AssertThat(frame.IsHeld(GameplayButtons.Interact)).IsTrue();
            AssertThat(frame.IsPressed(GameplayButtons.Interact))
                .OverrideFailureMessage("The B that closed the pause reached the hero as a fresh Interact press.")
                .IsFalse();

            // Control: the next B press is the game's again.
            await AssertNextPressReachesTheGame(tree, probe, JoyButton.B, GameplayButtons.Interact);
        } finally {
            probe.ReleaseAll();
            tree.Paused = false;
            host.GetParent()?.RemoveChild(host);
            host.Free();
            tree.Paused = false;
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public async Task PadAWorksTheStoryPauseAndPadBClosesItsOverlayBeforeThePause() {
        var host = new Node { Name = "PadPauseOverlayHost" };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(host);
        PauseMenu pause = PauseMenu.CreateDefault();
        host.AddChild(pause);
        try {
            await Frames(tree, 1);
            pause.SetPaused(true);
            // Pad A on the focused Log button opens the Dialogue Log inside the pause.
            pause.GetNode<Button>("Root/Center/Panel/Layout/LogButton").GrabFocus();
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            AssertThat(pause.LogScreen != null && pause.LogScreen.IsOpen)
                .OverrideFailureMessage("Pad A did not press the pause menu's focused Log button.")
                .IsTrue();

            // Pad B closes the overlay first and leaves the pause open...
            host.GetViewport().PushInput(Pad(0, JoyButton.B, pressed: true));
            AssertThat(pause.LogScreen.IsOpen).IsFalse();
            AssertThat(pause.IsPaused)
                .OverrideFailureMessage("One B closed both the Dialogue Log and the pause.")
                .IsTrue();

            // ...then backs out of the pause.
            host.GetViewport().PushInput(Pad(0, JoyButton.B, pressed: true));
            AssertThat(pause.IsPaused).IsFalse();

            // Pad A on the focused Resume button resumes too.
            pause.SetPaused(true);
            pause.GetNode<Button>("Root/Center/Panel/Layout/ResumeButton").GrabFocus();
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            AssertThat(pause.IsPaused).IsFalse();
            AssertThat(tree.Paused).IsFalse();
        } finally {
            tree.Paused = false;
            host.GetParent()?.RemoveChild(host);
            host.Free();
            tree.Paused = false;
        }
    }

    [TestCase]
    public async Task PadBCancelsAKeyboardListenThenClosesSettings() {
        var host = new Node { Name = "PadSettingsHost" };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(host);
        var menu = new SettingsMenu { Name = "SettingsMenu" };
        host.AddChild(menu);
        bool closed = false;
        menu.Closed += () => closed = true;
        try {
            menu.Show();
            await Frames(tree, 1);
            Node row = menu.Root.FindChild($"Row_{InputManager.Actions.Jump}", recursive: true, owned: false);
            AssertObject(row).IsNotNull();
            row.GetChild<Button>(1).EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(menu.IsListening).IsTrue();

            // A pad's B while a keyboard slot listens cancels the listen, as Escape
            // does for a pad slot, and binds nothing.
            host.GetViewport().PushInput(Pad(0, JoyButton.B, pressed: true));
            AssertThat(menu.IsListening).IsFalse();
            AssertThat(menu.Visible)
                .OverrideFailureMessage("The B that cancelled a listen also closed Settings.")
                .IsTrue();
            AssertThat(closed).IsFalse();

            // The next B backs out of Settings.
            host.GetViewport().PushInput(Pad(0, JoyButton.B, pressed: true));
            AssertThat(menu.Visible)
                .OverrideFailureMessage("Pad B did not close Settings.")
                .IsFalse();
            AssertThat(closed).IsTrue();
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    [TestCase]
    public async Task PadAStepsADialogueOneLineAtATimeAndItsLastPressNeverReachesTheHero() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        input.AssignJoypadToPlayer(0, 0);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var scope = new DialogueScope();
        DialogueManager dialogue = scope.Attach(tree.Root);
        try {
            await Frames(tree, 2);
            dialogue.StartSequence(DialogueScope.TwoLines(pauses: true));
            AssertThat(tree.Paused).IsTrue();
            int shown = DialogueLog.Count;

            // One press, one step: complete line 1, show line 2, complete it, end.
            TapA(dialogue);
            AssertThat(dialogue.Reveal.IsComplete).IsTrue();
            AssertThat(DialogueLog.Count).OverrideFailureMessage("One A both completed and advanced line 1.").IsEqual(shown);
            TapA(dialogue);
            AssertThat(DialogueLog.Count).IsEqual(shown + 1);
            AssertThat(dialogue.Reveal.IsComplete).IsFalse();
            TapA(dialogue);
            AssertThat(dialogue.IsSequenceActive).IsTrue();

            // The press that ends the scene is held on into gameplay.
            await Idle(tree);
            probe.SetButton(0, JoyButton.A, true);
            dialogue.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            AssertThat(dialogue.IsSequenceActive).IsFalse();
            AssertThat(tree.Paused).IsFalse();
            PlayerInputFrame frame = await NextTickFrame(tree);
            AssertThat(frame.IsHeld(GameplayButtons.Jump)).IsTrue();
            AssertThat(frame.IsPressed(GameplayButtons.Jump))
                .OverrideFailureMessage("The A that ended the dialogue reached the hero as a fresh Jump.")
                .IsFalse();

            await AssertNextPressReachesTheGame(tree, probe, JoyButton.A, GameplayButtons.Jump);
        } finally {
            probe.ReleaseAll();
            scope.Teardown();
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public async Task AnOpenPauseMenuOwnsPadPressesOverADialogue() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = "PadPauseOverDialogueHost" };
        tree.Root.AddChild(host);
        PauseMenu pause = PauseMenu.CreateDefault();
        host.AddChild(pause);
        // The box sits AFTER the pause in the tree, so it hears every unhandled
        // press first: only its own guard keeps it from acting under the pause.
        using var scope = new DialogueScope();
        DialogueManager dialogue = scope.Attach(tree.Root);
        try {
            await Frames(tree, 1);
            dialogue.StartSequence(DialogueScope.TwoLines(pauses: true));
            int shown = DialogueLog.Count;
            pause.SetPaused(true);

            // Pad B backs out of the pause; it is Interact too, but the line stays.
            host.GetViewport().PushInput(Pad(0, JoyButton.B, pressed: true));
            AssertThat(pause.IsPaused).IsFalse();
            AssertThat(dialogue.Reveal.IsComplete)
                .OverrideFailureMessage("Pad B on the pause menu also completed the dialogue line underneath.")
                .IsFalse();

            // Pad A presses the pause's focused Resume button and nothing else.
            pause.SetPaused(true);
            pause.GetNode<Button>("Root/Center/Panel/Layout/ResumeButton").GrabFocus();
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            AssertThat(dialogue.Reveal.IsComplete)
                .OverrideFailureMessage("Pad A on the pause menu also completed the dialogue line underneath.")
                .IsFalse();
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            AssertThat(pause.IsPaused).IsFalse();
            AssertThat(DialogueLog.Count).IsEqual(shown);

            // With the pause gone, the box takes its presses again.
            TapA(dialogue);
            AssertThat(dialogue.Reveal.IsComplete).IsTrue();
            AssertThat(DialogueLog.Count).IsEqual(shown);
        } finally {
            scope.Teardown();
            host.GetParent()?.RemoveChild(host);
            host.Free();
            tree.Paused = false;
        }
    }

    [TestCase]
    public async Task ADialogueConfirmHeldFromBeforeItStartedCannotHoldToSkipIt() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var scope = new DialogueScope();
        DialogueManager dialogue = scope.Attach(tree.Root);
        try {
            // A full hop on pad A (ui_accept now) held into a dialogue trigger.
            Input.ActionPress(PadMenuInput.AcceptAction);
            dialogue.StartSequence(DialogueScope.TwoLines(pauses: true));
            await ProcessFrames(tree, 12);
            AssertThat(dialogue.SkipHoldProgress)
                .OverrideFailureMessage("A confirm already held when the scene began counted toward hold-to-skip.")
                .IsEqual(0f);

            // Let go and hold again: that hold is the player's, and it counts.
            Input.ActionRelease(PadMenuInput.AcceptAction);
            await ProcessFrames(tree, 2);
            Input.ActionPress(PadMenuInput.AcceptAction);
            await ProcessFrames(tree, 6);
            AssertThat(dialogue.SkipHoldProgress > 0f)
                .OverrideFailureMessage("A fresh hold no longer counts toward hold-to-skip.")
                .IsTrue();
        } finally {
            Input.ActionRelease(PadMenuInput.AcceptAction);
            scope.Teardown();
        }
    }

    [TestCase]
    public async Task PadASkipsACreditsReplayWithoutReopeningIt() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        SessionData session = GameManager.Instance?.CurrentSession ?? default;
        var host = new Node { Name = "PadCreditsHost" };
        tree.Root.AddChild(host);
        MainMenu menu = ResourceLoader.Load<PackedScene>("res://scenes/menus/MainMenu.tscn").Instantiate<MainMenu>();
        host.AddChild(menu);
        try {
            await Frames(tree, 1);
            menu.GetNode<Button>("RootScreen/Center/Panel/Layout/ExtrasButton")
                .EmitSignal(BaseButton.SignalName.Pressed);
            var creditsButton = menu.GetNode<Button>("ExtrasScreen/Center/Panel/Layout/CreditsButton");
            creditsButton.GrabFocus();
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            AssertThat(LiveCredits(menu))
                .OverrideFailureMessage("Pad A did not press the focused Credits button.")
                .IsEqual(1);

            // A skips the roll; the release that follows must not start it again.
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            AssertThat(LiveCredits(menu)).IsEqual(0);
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            AssertThat(LiveCredits(menu))
                .OverrideFailureMessage("The A that skipped the credits replay re-opened it on its release.")
                .IsEqual(0);
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
            if (GameManager.Instance != null) GameManager.Instance.CurrentSession = session;
        }
    }

    /// <summary>
    /// The Story results overlay does not pause, so the hero is still live under
    /// it. Pad A presses its focused Return button on the release, and the press
    /// is the menu's: it never reaches the hero as a jump (InputManager's
    /// <c>_Input</c> consumes a confirm that a focused button takes).
    /// </summary>
    [TestCase]
    public async Task PadAPressesAFocusedResultsButtonAndThePressNeverReachesTheHero() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        input.AssignJoypadToPlayer(0, 0);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        LevelResultsPanel results = LevelResultsPanel.CreateDefault();
        int returns = 0;
        results.ReturnRequested += () => returns++;
        tree.Root.AddChild(results);
        try {
            await Frames(tree, 2);
            AssertThat(tree.Root.GuiGetFocusOwner() is Button)
                .OverrideFailureMessage("The results overlay no longer focuses its Return button.")
                .IsTrue();

            await Idle(tree);
            probe.SetButton(0, JoyButton.A, true);
            tree.Root.PushInput(Pad(0, JoyButton.A, pressed: true));
            PlayerInputFrame frame = await NextTickFrame(tree);
            AssertThat(frame.IsHeld(GameplayButtons.Jump)).IsTrue();
            AssertThat(frame.IsPressed(GameplayButtons.Jump))
                .OverrideFailureMessage("Pad A on the focused Return button also reached the hero as a jump.")
                .IsFalse();
            AssertThat(returns).IsEqual(0);

            await Idle(tree);
            probe.SetButton(0, JoyButton.A, false);
            tree.Root.PushInput(Pad(0, JoyButton.A, pressed: false));
            AssertThat(returns)
                .OverrideFailureMessage("Pad A did not press the focused Return button.")
                .IsEqual(1);

            // Control: with nothing focused, A is the game's jump again.
            tree.Root.GuiReleaseFocus();
            await AssertNextPressReachesTheGame(tree, probe, JoyButton.A, GameplayButtons.Jump, pushEvents: true);
            AssertThat(returns).IsEqual(1);
        } finally {
            probe.ReleaseAll();
            results.GetParent()?.RemoveChild(results);
            results.Free();
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>
    /// Pad B is Interact and (since CM-PADUI) <c>ui_cancel</c>. The hub opens the
    /// Holodeck console on Interact in <c>_Input</c>, and the console is already
    /// in the tree when the same press reaches <c>_UnhandledInput</c>: unless the
    /// hub spends the press, the B that opened the console also closed it.
    /// </summary>
    [TestCase]
    public async Task PadBOpensTheHubHolodeckConsoleAndOnlyTheNextBClosesIt() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        SessionData original = GameManager.Instance?.CurrentSession ?? default;
        Node hub = null;
        try {
            if (GameManager.Instance != null) {
                SessionData session = original;
                session.ActiveSaveSlot = -1;
                session.ArriveAtHolodeckConsole = true;
                session.ReopenHolodeckConsole = false;
                GameManager.Instance.CurrentSession = session;
            }
            hub = ResourceLoader.Load<PackedScene>("res://scenes/campaign/HubWorld.tscn").Instantiate();
            tree.Root.AddChild(hub);
            // The hero arrives at the console; let its trigger see the body.
            await Frames(tree, 4);
            AssertObject(HolodeckPanel(hub)).IsNull();

            await Idle(tree);
            tree.Root.PushInput(Pad(0, JoyButton.B, pressed: true));
            AssertObject(HolodeckPanel(hub))
                .OverrideFailureMessage("Pad B (Interact) at the console did not open the Holodeck console.")
                .IsNotNull();
            tree.Root.PushInput(Pad(0, JoyButton.B, pressed: false));
            await Frames(tree, 2);
            AssertObject(HolodeckPanel(hub))
                .OverrideFailureMessage("The B that opened the Holodeck console also closed it.")
                .IsNotNull();

            // The next B backs out of the console and does not reopen it.
            await Idle(tree);
            tree.Root.PushInput(Pad(0, JoyButton.B, pressed: true));
            tree.Root.PushInput(Pad(0, JoyButton.B, pressed: false));
            await Frames(tree, 2);
            AssertObject(HolodeckPanel(hub))
                .OverrideFailureMessage("Pad B did not back out of the Holodeck console, or reopened it.")
                .IsNull();
        } finally {
            if (hub != null && GodotObject.IsInstanceValid(hub)) {
                hub.GetParent()?.RemoveChild(hub);
                hub.Free();
            }
            if (GameManager.Instance != null) GameManager.Instance.CurrentSession = original;
            tree.Paused = originalPaused;
        }
    }

    // ---- helpers ------------------------------------------------------------

    private static HolodeckConsolePanel HolodeckPanel(Node hub) {
        var panel = hub.GetNodeOrNull<HolodeckConsolePanel>("HubHUD/HolodeckConsolePanel");
        return panel != null && !panel.IsQueuedForDeletion() ? panel : null;
    }

    /// <summary>
    /// Waits for the next idle step, where the engine delivers real input:
    /// between physics ticks. A press made inside a tick (after an awaited
    /// <c>physics_frame</c>) would still reach that same tick's capture, which
    /// no player's press ever does.
    /// </summary>
    private static async Task Idle(SceneTree tree) => await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

    /// <summary>
    /// Player 1's frame for the next physics tick, read at the top of the tick
    /// before any node's <c>_PhysicsProcess</c>: the frame the game reads then.
    /// </summary>
    private static async Task<PlayerInputFrame> NextTickFrame(SceneTree tree) {
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        return InputManager.Instance.GetFrame(0);
    }

    /// <summary>The control half: let go, then a fresh press is the game's press again.</summary>
    private static async Task AssertNextPressReachesTheGame(
        SceneTree tree, FakeInputDeviceProbe probe, JoyButton button, GameplayButtons gameplay, bool pushEvents = false) {
        await Idle(tree);
        probe.SetButton(0, button, false);
        if (pushEvents) tree.Root.PushInput(Pad(0, button, pressed: false));
        await NextTickFrame(tree);
        await Idle(tree);
        probe.SetButton(0, button, true);
        if (pushEvents) tree.Root.PushInput(Pad(0, button, pressed: true));
        PlayerInputFrame frame = await NextTickFrame(tree);
        AssertThat(frame.IsPressed(gameplay))
            .OverrideFailureMessage($"Control: a fresh {button} press no longer reached the game.")
            .IsTrue();
        probe.SetButton(0, button, false);
        if (pushEvents) tree.Root.PushInput(Pad(0, button, pressed: false));
    }

    private static int LiveCredits(MainMenu menu) =>
        menu.GetChildren().OfType<CreditsController>().Count(credits => !credits.IsQueuedForDeletion() && !credits.IsFinished);

    private static void TapA(Node node) {
        node.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
        node.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
    }

    private static InputEventJoypadButton Pad(int device, JoyButton button, bool pressed) =>
        new() { Device = device, ButtonIndex = button, Pressed = pressed };

    private static async Task Frames(SceneTree tree, int count) {
        for (int frame = 0; frame < count; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    private static async Task ProcessFrames(SceneTree tree, int count) {
        for (int frame = 0; frame < count; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>
    /// A dialogue box with no story slot, Normal text speed and the global seen
    /// set and session restored afterwards. Not a suite.
    /// </summary>
    private sealed class DialogueScope : System.IDisposable {
        private readonly SessionData _session = GameManager.Instance?.CurrentSession ?? default;
        private readonly HashSet<string> _seen;
        private readonly TextSpeed _speed;
        private DialogueManager _dialogue;
        private bool _tornDown;

        public DialogueScope() {
            GlobalSaveData global = SaveManager.Instance?.GlobalData;
            _seen = global?.SeenDialogueIDs != null ? new HashSet<string>(global.SeenDialogueIDs) : null;
            _speed = global?.DialogueTextSpeed ?? TextSpeed.Normal;
            if (global != null) global.DialogueTextSpeed = TextSpeed.Normal;
            if (GameManager.Instance != null) {
                SessionData slotless = _session;
                slotless.ActiveSaveSlot = -1;
                GameManager.Instance.CurrentSession = slotless;
            }
            ((SceneTree)Engine.GetMainLoop()).Root.GetViewport().GuiReleaseFocus();
        }

        public DialogueManager Attach(Node parent) {
            _dialogue = DialogueManager.CreateDefault();
            parent.AddChild(_dialogue);
            return _dialogue;
        }

        public static DialogueSequenceData TwoLines(bool pauses) => new() {
            DialogueID = "cm_padui.probe",
            SpeakerNameKeys = new[] { "speaker_sarah", "speaker_sarah" },
            LineKeys = new[] { "dlg_l15_postboss_1", "dlg_l15_postboss_2" },
            EmotionKeys = new[] { "emotion_neutral", "emotion_neutral" },
            PausesGameplay = pauses
        };

        public void Teardown() {
            if (_tornDown) return;
            _tornDown = true;
            if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue)) {
                _dialogue.GetParent()?.RemoveChild(_dialogue);
                _dialogue.Free();
            }
            ((SceneTree)Engine.GetMainLoop()).Paused = false;
            GlobalSaveData global = SaveManager.Instance?.GlobalData;
            if (global != null) {
                global.DialogueTextSpeed = _speed;
                if (_seen != null) global.SeenDialogueIDs = _seen;
                else global.SeenDialogueIDs?.Remove("cm_padui.probe");
            }
            if (GameManager.Instance != null) GameManager.Instance.CurrentSession = _session;
        }

        public void Dispose() => Teardown();
    }
}
