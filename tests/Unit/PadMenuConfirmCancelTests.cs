using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
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

    // ---- CM-FIXB-PADUI (2026-10-16) -----------------------------------------

    /// <summary>
    /// Finding B14 / B22 (a): the menu-press consume used to drop the release
    /// edge as well as the press. A Jump let go of during a pause then never
    /// reached the Fighter sim as released, so its short-hop latch (component
    /// 323 bit 1) kept "held" and the rest of that airtime got the full hop and
    /// the held-Jump apex hang. The frames fed to the sim here are exactly the
    /// ones the production capture builds (a fake pad, real physics ticks).
    /// </summary>
    [TestCase]
    public async Task AJumpLetGoOfDuringAPauseReachesTheFighterSimAsReleased() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        input.AssignJoypadToPlayer(0, 0);
        var host = new Node { Name = "PadPauseJumpHost" };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(host);
        PauseMenu pause = PauseMenu.CreateDefault();
        host.AddChild(pause);
        FighterLoadout loadout = FighterLoadout.Default(FighterCharacterID.Joan);
        var simulation = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Einstein), seed: 8191, rules: FighterMatchRules.Disabled);
        PlayerInputFrame idle = PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None);
        try {
            await Frames(tree, 2);
            // A held full hop on pad A: the press tick and three held ticks.
            await Idle(tree);
            probe.SetButton(0, JoyButton.A, true);
            for (int held = 0; held < 4; held++) {
                PlayerInputFrame frame = await NextTickFrame(tree);
                if (held == 0) AssertThat(frame.IsPressed(GameplayButtons.Jump)).IsTrue();
                simulation.Advance(frame, idle);
            }
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent rising)).IsTrue();
            AssertThat(rising.IsGrounded == 0 && rising.Velocity.y > FP64.Zero)
                .OverrideFailureMessage("Harness: the fighter is not rising from its jump.")
                .IsTrue();
            AssertThat(JumpReleasedLatch(simulation))
                .OverrideFailureMessage("Harness: the latch reads released while Jump is held.")
                .IsFalse();

            // Paused mid-rise; Jump is let go of in the menu; the pause resumes.
            pause.SetPaused(true);
            await Idle(tree);
            probe.SetButton(0, JoyButton.A, false);
            await Idle(tree);
            pause.SetPaused(false);

            PlayerInputFrame resumed = await NextTickFrame(tree);
            AssertThat(resumed.IsHeld(GameplayButtons.Jump)).IsFalse();
            AssertThat(resumed.Pressed).IsEqual(GameplayButtons.None);
            AssertThat(resumed.IsReleased(GameplayButtons.Jump))
                .OverrideFailureMessage("The resume tick dropped the release of a Jump let go of during the pause.")
                .IsTrue();
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
            simulation.Advance(resumed, idle);
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
            AssertThat(JumpReleasedLatch(simulation))
                .OverrideFailureMessage("The Fighter sim still reads Jump as held after it was let go of during a pause.")
                .IsTrue();
            AssertThat(before.Velocity.y > FP64.Zero && after.IsGrounded == 0)
                .OverrideFailureMessage("Harness: the rise ended before the resume tick.")
                .IsTrue();
            FP64 shortHop = FighterAirPhysics.StepMovement(before.Velocity.y, false, false,
                FighterAirPhysics.HasJumpedThisAirtime(before.RemainingJumps, loadout.MaxJumpCount),
                jumpHeld: false, riseFromOwnJump: true);
            AssertThat(after.Velocity.y.RawValue)
                .OverrideFailureMessage("The released rise after the resume did not take the short-hop step.")
                .IsEqual(shortHop.RawValue);
        } finally {
            probe.ReleaseAll();
            tree.Paused = false;
            host.GetParent()?.RemoveChild(host);
            host.Free();
            tree.Paused = false;
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>
    /// Finding B22 (b): with the Ultimate affordable, a lone shoulder pressed
    /// during a pause and held through the resume waited in the chord window on
    /// the consume tick and came out as a fresh Special 2 press three ticks
    /// later, after the consume — a cast three frames into the resume. It now
    /// reaches the game as held on the resume tick, as every other button does.
    /// </summary>
    [TestCase]
    public async Task AShoulderPressedDuringAPauseReachesTheGameAsHeldNeverAsALatePress() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        JoyButton rb = FakeInputDeviceProbe.PadButtonFor(InputManager.Actions.Special2);
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        input.AssignJoypadToPlayer(0, 0);
        var host = new Node { Name = "PadPauseShoulderHost" };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(host);
        PauseMenu pause = PauseMenu.CreateDefault();
        host.AddChild(pause);
        try {
            for (int frame = 0; frame < 2; frame++) {
                input.ReportUltimateAffordable(0, true);
                await Frames(tree, 1);
            }
            pause.SetPaused(true);
            await Idle(tree);
            probe.SetButton(0, rb, true);
            await Idle(tree);
            input.ReportUltimateAffordable(0, true);
            pause.SetPaused(false);

            for (int frame = 0; frame <= 2 * UltimateChordWindow.LeniencyFrames; frame++) {
                PlayerInputFrame resumed = await NextTickFrame(tree);
                input.ReportUltimateAffordable(0, true);
                AssertThat(resumed.IsPressed(GameplayButtons.Special2))
                    .OverrideFailureMessage($"Tick {frame} after the resume: the shoulder held through the pause came back as a fresh Special 2 press.")
                    .IsFalse();
                AssertThat(resumed.IsHeld(GameplayButtons.Special2))
                    .OverrideFailureMessage($"Tick {frame} after the resume: the shoulder held through the pause is not held.")
                    .IsTrue();
                AssertThat(resumed.IsHeld(GameplayButtons.Ultimate)).IsFalse();
            }

            // Control: let go and pressed again, it waits the window for its
            // partner as before, then is a press.
            await Idle(tree);
            probe.SetButton(0, rb, false);
            await NextTickFrame(tree);
            input.ReportUltimateAffordable(0, true);
            await Idle(tree);
            probe.SetButton(0, rb, true);
            int presses = 0;
            for (int frame = 0; frame <= UltimateChordWindow.LeniencyFrames + 1; frame++) {
                PlayerInputFrame live = await NextTickFrame(tree);
                input.ReportUltimateAffordable(0, true);
                if (live.IsPressed(GameplayButtons.Special2)) presses++;
            }
            AssertThat(presses)
                .OverrideFailureMessage("Control: a fresh shoulder press after the resume no longer reached the game once.")
                .IsEqual(1);
        } finally {
            probe.ReleaseAll();
            tree.Paused = false;
            host.GetParent()?.RemoveChild(host);
            host.Free();
            tree.Paused = false;
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>
    /// Finding B15: in a sequence that leaves gameplay running (Level 15's
    /// phase-3 bark), pad A is both the confirm and the hero's Jump. A full hop
    /// held on it armed hold-to-skip and, on a first viewing, opened the skip
    /// confirmation over the live fight. A Jump-shared confirm now never counts
    /// toward the hold while gameplay runs; the advertised hold (Interact, pad
    /// B) still skips a non-pausing sequence, so the send-off and the
    /// Homecoming stay skippable.
    /// </summary>
    [TestCase]
    public async Task APadAJumpHeldThroughANonPausingSequenceNeverArmsHoldToSkip() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var scope = new DialogueScope();
        DialogueManager dialogue = scope.Attach(tree.Root);
        try {
            await ProcessFrames(tree, 1);
            dialogue.StartSequence(DialogueScope.TwoLines(pauses: false));
            AssertThat(tree.Paused).IsFalse();

            // The hop's press still finishes the line's reveal, as before...
            dialogue.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            AssertThat(dialogue.Reveal.IsComplete)
                .OverrideFailureMessage("Pad A no longer completes a non-pausing line's reveal.")
                .IsTrue();
            // ...and the held button is both the confirm and Jump.
            Input.ActionPress(PadMenuInput.AcceptAction);
            Input.ActionPress(InputManager.Actions.Jump);
            await ProcessFrames(tree, 20);
            AssertThat(dialogue.SkipHoldProgress)
                .OverrideFailureMessage("A jump held on pad A during a non-pausing sequence counted toward hold-to-skip.")
                .IsEqual(0f);
            await ProcessFrames(tree, 60);
            AssertThat(dialogue.IsSkipConfirmOpen)
                .OverrideFailureMessage("A jump held on pad A opened the skip confirmation over live gameplay.")
                .IsFalse();
            AssertThat(dialogue.IsSequenceActive).IsTrue();

            // Control: the advertised hold still skips a non-pausing sequence.
            Input.ActionRelease(PadMenuInput.AcceptAction);
            Input.ActionRelease(InputManager.Actions.Jump);
            dialogue.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            await ProcessFrames(tree, 2);
            Input.ActionPress(InputManager.Actions.Interact);
            await ProcessFrames(tree, 6);
            AssertThat(dialogue.SkipHoldProgress > 0f)
                .OverrideFailureMessage("Control: holding Interact no longer counts toward skipping a non-pausing sequence.")
                .IsTrue();
        } finally {
            Input.ActionRelease(PadMenuInput.AcceptAction);
            Input.ActionRelease(InputManager.Actions.Jump);
            Input.ActionRelease(InputManager.Actions.Interact);
            scope.Teardown();
        }
    }

    /// <summary>
    /// Finding B16: a pause opened over a gameplay-pausing dialogue (Start, or a
    /// window focus loss) wrote <c>SceneTree.Paused = false</c> on every resume,
    /// so the hero, the mobs and the boss ran under the still-open box on free
    /// time while every pad A both paged a line and jumped. Every resume path —
    /// pad B, pad A on Resume, a focus-loss pause, a controller-lost resolution
    /// — now hands the tree back to the dialogue that still holds it, and the
    /// dialogue releases it when it ends.
    /// </summary>
    [TestCase]
    public async Task ResumingAPauseOverAGameplayPausingDialogueLeavesTheWorldPausedUntilTheDialogueEnds() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        InputManager input = InputManager.Instance;
        var host = new Node { Name = "PadPauseOverPausingDialogueHost" };
        tree.Root.AddChild(host);
        PauseMenu pause = PauseMenu.CreateDefault();
        host.AddChild(pause);
        using var scope = new DialogueScope();
        DialogueManager dialogue = scope.Attach(tree.Root);
        try {
            await Frames(tree, 1);
            dialogue.StartSequence(DialogueScope.TwoLines(pauses: true));
            AssertThat(tree.Paused).IsTrue();

            pause.SetPaused(true);
            host.GetViewport().PushInput(Pad(0, JoyButton.B, pressed: true));
            AssertThat(pause.IsPaused).IsFalse();
            AssertThat(tree.Paused)
                .OverrideFailureMessage("Pad B backing out of the pause unpaused the world under a gameplay-pausing dialogue.")
                .IsTrue();

            pause.SetPaused(true);
            pause.GetNode<Button>("Root/Center/Panel/Layout/ResumeButton").GrabFocus();
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            host.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            AssertThat(pause.IsPaused).IsFalse();
            AssertThat(tree.Paused)
                .OverrideFailureMessage("Resume unpaused the world under a gameplay-pausing dialogue.")
                .IsTrue();

            AssertThat(pause.HandleFocusLost()).IsTrue();
            pause.SetPaused(false);
            AssertThat(tree.Paused)
                .OverrideFailureMessage("Resuming a focus-loss pause unpaused the world under a gameplay-pausing dialogue.")
                .IsTrue();

            if (input != null) {
                pause.ForceControllerLostPause();
                AssertThat(pause.ResolveControllerLost(InputManager.KeyboardDevice)).IsTrue();
                AssertThat(pause.IsPaused).IsFalse();
                AssertThat(tree.Paused)
                    .OverrideFailureMessage("A controller-lost resolution unpaused the world under a gameplay-pausing dialogue.")
                    .IsTrue();
            }
            AssertThat(dialogue.IsSequenceActive).IsTrue();

            // The dialogue still hands the tree back itself when it ends.
            for (int tap = 0; tap < 4 && dialogue.IsSequenceActive; tap++) TapA(dialogue);
            AssertThat(dialogue.IsSequenceActive).IsFalse();
            AssertThat(tree.Paused)
                .OverrideFailureMessage("The world stayed paused after the dialogue ended.")
                .IsFalse();
        } finally {
            scope.Teardown();
            host.GetParent()?.RemoveChild(host);
            host.Free();
            tree.Paused = false;
            input?.AutoAssignDevices();
        }
    }

    /// <summary>
    /// Finding B16, the teardown half: whichever of the two pause owners leaves
    /// first, the tree stays paused while the other still holds it and is
    /// released when it lets go. A pause freed over a pausing dialogue must not
    /// unpause the world under the box, and a dialogue freed under an open pause
    /// must not unpause the world under the menu. Restart Level and Quit to
    /// Main Menu leave the scene, so their close releases the tree outright:
    /// the scene load runs on <c>GameManager._Process</c>, which a paused tree
    /// stops.
    /// </summary>
    [TestCase]
    public async Task APauseAndAGameplayPausingDialogueHandTheTreeBackInEitherTeardownOrder() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();

        // The pause leaves first.
        var pauseHost = new Node { Name = "PauseLeavesFirstHost" };
        tree.Root.AddChild(pauseHost);
        PauseMenu pause = PauseMenu.CreateDefault();
        pauseHost.AddChild(pause);
        var scope = new DialogueScope();
        DialogueManager dialogue = scope.Attach(tree.Root);
        try {
            await Frames(tree, 1);
            dialogue.StartSequence(DialogueScope.TwoLines(pauses: true));
            pause.SetPaused(true);
            pauseHost.GetParent().RemoveChild(pauseHost);
            pauseHost.Free();
            AssertThat(tree.Paused)
                .OverrideFailureMessage("A pause freed over a gameplay-pausing dialogue unpaused the world under the box.")
                .IsTrue();
            for (int tap = 0; tap < 4 && dialogue.IsSequenceActive; tap++) TapA(dialogue);
            AssertThat(dialogue.IsSequenceActive).IsFalse();
            AssertThat(tree.Paused).IsFalse();
        } finally {
            scope.Teardown();
            if (GodotObject.IsInstanceValid(pauseHost)) {
                pauseHost.GetParent()?.RemoveChild(pauseHost);
                pauseHost.Free();
            }
            tree.Paused = false;
        }

        // The dialogue leaves first, under an open pause.
        var host = new Node { Name = "DialogueLeavesFirstHost" };
        tree.Root.AddChild(host);
        pause = PauseMenu.CreateDefault();
        host.AddChild(pause);
        scope = new DialogueScope();
        dialogue = scope.Attach(tree.Root);
        try {
            await Frames(tree, 1);
            dialogue.StartSequence(DialogueScope.TwoLines(pauses: true));
            pause.SetPaused(true);
            dialogue.GetParent().RemoveChild(dialogue);
            dialogue.Free();
            AssertThat(tree.Paused)
                .OverrideFailureMessage("A dialogue freed under an open pause menu unpaused the world under the menu.")
                .IsTrue();
            pause.SetPaused(false);
            AssertThat(tree.Paused)
                .OverrideFailureMessage("The pause did not hand the tree back once the dialogue was gone.")
                .IsFalse();

            // Restart Level / Quit to Main Menu over a pausing dialogue release the tree.
            DialogueManager second = scope.Attach(tree.Root);
            await Frames(tree, 1);
            second.StartSequence(DialogueScope.TwoLines(pauses: true));
            pause.SetPaused(true);
            pause.ClosePauseForSceneExit();
            AssertThat(pause.IsPaused).IsFalse();
            AssertThat(tree.Paused)
                .OverrideFailureMessage("Closing the pause to leave the scene kept the tree paused, which stalls the scene load.")
                .IsFalse();
        } finally {
            scope.Teardown();
            host.GetParent()?.RemoveChild(host);
            host.Free();
            tree.Paused = false;
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

    /// <summary>Component 323's <c>JumpReleasedSincePress</c> bit on player one.</summary>
    private static bool JumpReleasedLatch(FighterSimulation simulation) {
        AssertThat(simulation.TryGetFighterActionState(0, out FighterActionStateComponent action)).IsTrue();
        return (action.ActionFlags & FighterActionStateComponent.ActionFlagJumpReleasedSincePress) != 0;
    }

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
