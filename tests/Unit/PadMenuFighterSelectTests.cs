using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-PADUI (2026-10-16; decision 9.1): pad-only players on the Fighter select.
/// Pad A is now <c>ui_accept</c>, so it presses a focused footer button (Move
/// List, Systems Card, Back) — and it is also the polled token flow's Up (Jump).
/// Pad B is now <c>ui_cancel</c>, so it backs out of the stage phase, the Versus
/// CPU panel and an onboarding overlay — and it is also the token flow's
/// Interact confirm. The stick and d-pad move the token and are also Godot's
/// built-in focus moves. One press must do one thing: these cases pin that a
/// menu press never also steps or locks a token on the next polled tick, that
/// a token step never leaves Player 1's A pointing at a footer button, that
/// Player 2's pad never drives the shared footer, and that Player 1's panel
/// says how to join while nobody has (CM-FIXA-INPUT removed the pre-claimed
/// Player 1).
///
/// <para>Real physics frames, real <see cref="InputEvent"/>s pushed through the
/// viewport for the GUI, and a fake device probe for the polled frames, as
/// <c>CharacterSelectSceneTests</c> does. A press that a menu acts on is made
/// in idle time (<see cref="Idle"/>), where real input arrives. Every case
/// restores the session, the join and the devices in <c>finally</c>.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PadMenuFighterSelectTests {

    private const string ScenePath = "res://scenes/menus/CharacterSelect.tscn";
    private const string SelectRoot = "SelectPhase/Root/";

    private static SessionData? _savedSession;

    [TestCase]
    public async Task PadAPressesAFocusedFooterButtonWithoutSteppingTheTokenAndPadBClosesItsOverlayWithoutConfirming() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        try {
            await Frames(tree, 2);
            await JoinWithPadA(tree, probe, 0);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);
            int cursor = screen.GetCursor(0);

            screen.GetNode<Button>(SelectRoot + "ButtonRow/MoveListButton").GrabFocus();
            await Idle(tree);
            probe.SetButton(0, JoyButton.A, true);
            screen.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            await Frames(tree, 3);
            AssertThat(screen.GetCursor(0))
                .OverrideFailureMessage("Pad A on the focused Move List button also stepped Player 1's token up.")
                .IsEqual(cursor);
            AssertThat(MoveListOpen(screen)).IsFalse();

            await Idle(tree);
            probe.SetButton(0, JoyButton.A, false);
            screen.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            AssertThat(MoveListOpen(screen))
                .OverrideFailureMessage("Pad A did not press the focused Move List button.")
                .IsTrue();
            await Frames(tree, 2);

            await Idle(tree);
            probe.SetButton(0, JoyButton.B, true);
            screen.GetViewport().PushInput(Pad(0, JoyButton.B, pressed: true));
            AssertThat(MoveListOpen(screen))
                .OverrideFailureMessage("Pad B did not close the Move List.")
                .IsFalse();
            await Frames(tree, 3);
            AssertThat(screen.IsSlotReady(0))
                .OverrideFailureMessage("The B that closed the Move List also locked Player 1's token.")
                .IsFalse();
            AssertThat(screen.GetCursor(0)).IsEqual(cursor);
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public async Task PadBBacksOutOfTheStagePhaseWithoutRelockingAndUpThenAPressesBack() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        int backs = 0;
        screen.BackOverrideForTesting = () => backs++;
        try {
            await Frames(tree, 2);
            await JoinWithPadA(tree, probe, 0);
            AssertThat(screen.TryConfirm(0)).IsTrue();
            AssertThat(screen.TryConfirm(1)).IsTrue();
            screen.AdvanceCountdown(3.5f);
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.StageSelect);

            await Idle(tree);
            probe.SetButton(0, JoyButton.B, true);
            screen.GetViewport().PushInput(Pad(0, JoyButton.B, pressed: true));
            AssertThat(screen.Phase)
                .OverrideFailureMessage("Pad B did not back out of the stage phase.")
                .IsEqual(SelectScreenPhase.Selection);
            await Frames(tree, 3);
            AssertThat(screen.IsSlotReady(0))
                .OverrideFailureMessage("The B that backed out of the stage phase re-locked Player 1's token.")
                .IsFalse();
            AssertThat(backs).IsEqual(0);

            // A pad-only Player 1 reaches Back with Up (the wrapping footer chain)
            // and presses it with A.
            await Idle(tree);
            probe.SetButton(0, JoyButton.B, false);
            await Frames(tree, 2);
            await Idle(tree);
            screen.GetViewport().PushInput(Pad(0, JoyButton.DpadUp, pressed: true));
            screen.GetViewport().PushInput(Pad(0, JoyButton.DpadUp, pressed: false));
            AssertThat(FocusName(screen))
                .OverrideFailureMessage("Up from the notice no longer walks Player 1's focus onto Back.")
                .IsEqual("BackButton");
            await Frames(tree, 2);
            await Idle(tree);
            probe.SetButton(0, JoyButton.A, true);
            screen.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            await Frames(tree, 2);
            await Idle(tree);
            probe.SetButton(0, JoyButton.A, false);
            screen.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            AssertThat(backs)
                .OverrideFailureMessage("Pad A did not press the focused Back button.")
                .IsEqual(1);
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public async Task PadBClosesTheVersusCpuPanelWithoutReopeningIt() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        input.AssignJoypadToPlayer(0, 0);
        CharacterSelectScreen screen = Open(out Node host, FighterMatchOrigin.VersusCpu);
        SceneTree tree = screen.GetTree();
        try {
            await Frames(tree, 2);
            AssertThat(screen.TryConfirm(0)).IsTrue();
            AssertThat(screen.CpuConfigPanel != null).IsTrue();

            await Idle(tree);
            probe.SetButton(0, JoyButton.B, true);
            screen.GetViewport().PushInput(Pad(0, JoyButton.B, pressed: true));
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Selection);
            await Frames(tree, 3);
            AssertThat(screen.CpuConfigPanel == null)
                .OverrideFailureMessage("The B that closed the CPU panel confirmed Player 1 again and re-opened it.")
                .IsTrue();
            AssertThat(screen.IsSlotReady(0)).IsFalse();
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>
    /// The d-pad's Down steps Player 1's token and is also Godot's <c>ui_down</c>,
    /// which walks the GUI focus from the notice onto Move List. If the focus
    /// stayed there, Player 1's next A (the token's Up) would open the Move List
    /// instead; the token step hands the focus back.
    /// </summary>
    [TestCase]
    public async Task ATokenStepNeverLeavesPlayerOnesAOnAFooterButton() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        try {
            await Frames(tree, 2);
            await JoinWithPadA(tree, probe, 0);
            int cursor = screen.GetCursor(0);
            AssertThat(FocusName(screen)).IsEqual("RemotePlayNotice");

            await Idle(tree);
            probe.SetButton(0, JoyButton.DpadDown, true);
            screen.GetViewport().PushInput(Pad(0, JoyButton.DpadDown, pressed: true));
            await Frames(tree, 2);
            AssertThat(screen.GetCursor(0))
                .OverrideFailureMessage("The d-pad's Down no longer steps Player 1's token.")
                .IsNotEqual(cursor);
            AssertThat(FocusName(screen))
                .OverrideFailureMessage("A token step left Player 1's focus on a footer button.")
                .IsEqual("RemotePlayNotice");
            await Idle(tree);
            probe.SetButton(0, JoyButton.DpadDown, false);
            screen.GetViewport().PushInput(Pad(0, JoyButton.DpadDown, pressed: false));
            await Frames(tree, 2);

            // A is the token's Up again, and opens nothing.
            await Idle(tree);
            probe.SetButton(0, JoyButton.A, true);
            screen.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            await Frames(tree, 2);
            await Idle(tree);
            probe.SetButton(0, JoyButton.A, false);
            screen.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            await Frames(tree, 2);
            AssertThat(screen.GetCursor(0)).IsEqual(cursor);
            AssertThat(MoveListOpen(screen))
                .OverrideFailureMessage("Player 1's A after a token step opened the Move List.")
                .IsFalse();
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>
    /// The GUI focus is shared, but the footer is Player 1's: Player 2's A is
    /// Player 2's token Up, never a press of the Back button Player 1 is on, and
    /// Player 2's d-pad never moves Player 1's focus.
    /// </summary>
    [TestCase]
    public async Task PlayerTwosPadNeverDrivesTheSharedFooter() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0, 1);
        input.AutoAssignDevices();
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        int backs = 0;
        screen.BackOverrideForTesting = () => backs++;
        try {
            await Frames(tree, 2);
            await JoinWithPadA(tree, probe, 0);
            await JoinWithPadA(tree, probe, 1);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(1);

            var back = screen.GetNode<Button>(SelectRoot + "ButtonRow/BackButton");
            back.GrabFocus();
            await Idle(tree);
            screen.GetViewport().PushInput(Pad(1, JoyButton.DpadUp, pressed: true));
            screen.GetViewport().PushInput(Pad(1, JoyButton.DpadUp, pressed: false));
            AssertThat(FocusName(screen))
                .OverrideFailureMessage("Player 2's d-pad moved the footer focus Player 1 holds.")
                .IsEqual("BackButton");

            // Player 2 steps their token down, then presses A to step it back up.
            await Idle(tree);
            probe.SetButton(1, JoyButton.DpadDown, true);
            screen.GetViewport().PushInput(Pad(1, JoyButton.DpadDown, pressed: true));
            await Frames(tree, 2);
            await Idle(tree);
            probe.SetButton(1, JoyButton.DpadDown, false);
            screen.GetViewport().PushInput(Pad(1, JoyButton.DpadDown, pressed: false));
            await Frames(tree, 2);
            int cursor = screen.GetCursor(1);
            AssertThat(FocusName(screen)).IsEqual("BackButton");

            await Idle(tree);
            probe.SetButton(1, JoyButton.A, true);
            screen.GetViewport().PushInput(Pad(1, JoyButton.A, pressed: true));
            await Frames(tree, 2);
            await Idle(tree);
            probe.SetButton(1, JoyButton.A, false);
            screen.GetViewport().PushInput(Pad(1, JoyButton.A, pressed: false));
            await Frames(tree, 1);
            AssertThat(backs)
                .OverrideFailureMessage("Player 2's A pressed the Back button Player 1's focus was on.")
                .IsEqual(0);
            AssertThat(screen.GetCursor(1))
                .OverrideFailureMessage("Player 2's A no longer steps Player 2's token up.")
                .IsNotEqual(cursor);

            // Control: Player 1's own A on the same button presses it.
            await Idle(tree);
            probe.SetButton(0, JoyButton.A, true);
            screen.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            await Frames(tree, 1);
            await Idle(tree);
            probe.SetButton(0, JoyButton.A, false);
            screen.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            AssertThat(backs).IsEqual(1);
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public async Task PlayerOnesJoinPromptShowsUntilADeviceClaimsSlotZero() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        var prompt = screen.GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1JoinPrompt");
        try {
            await Frames(tree, 2);
            AssertThat(prompt.Text).IsEqual("select_press_to_join_player_one");
            AssertThat(TranslationServer.Translate(prompt.Text).ToString())
                .IsNotEqual("select_press_to_join_player_one");
            AssertThat(screen.IsPlayerOneJoinPromptShown).IsTrue();
            AssertThat(prompt.Visible)
                .OverrideFailureMessage("Nobody has joined, but Player 1's panel does not say how to join.")
                .IsTrue();

            await JoinWithPadA(tree, probe, 0);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);
            AssertThat(screen.IsPlayerOneJoinPromptShown).IsFalse();
            AssertThat(prompt.Visible).IsFalse();
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }

        // Versus CPU never joins: Player 1 follows the last-used device, so no prompt.
        CharacterSelectScreen cpu = Open(out Node cpuHost, FighterMatchOrigin.VersusCpu);
        try {
            AssertThat(cpu.IsPlayerOneJoinPromptShown).IsFalse();
            AssertThat(cpu.GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1JoinPrompt").Visible).IsFalse();
        } finally {
            Teardown(cpuHost);
        }
    }

    /// <summary>
    /// CM-FIXB-PADUI (finding B17): the keyboard's Up arrow is only Godot's
    /// built-in <c>ui_up</c> — <c>gameplay_up</c> is W — so the swallow, which
    /// required a token action too, let a joined keyboard Player 2's Up arrow
    /// walk Player 1's footer focus onto Back, and Player 1's next A (the
    /// token's Up) pressed it and threw both players out. Every directional
    /// focus move from another player's device is now swallowed.
    /// </summary>
    [TestCase]
    public async Task AKeyboardPlayerTwosUpArrowNeverMovesPlayerOnesFooterFocus() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        int backs = 0;
        screen.BackOverrideForTesting = () => backs++;
        Key interact = FakeInputDeviceProbe.KeyFor(InputManager.Actions.Interact);
        try {
            await Frames(tree, 2);
            await JoinWithPadA(tree, probe, 0);
            probe.SetKey(interact, true);
            await Frames(tree, 3);
            probe.SetKey(interact, false);
            await Frames(tree, 2);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(InputManager.KeyboardDevice);
            AssertThat(FocusName(screen)).IsEqual("RemotePlayNotice");

            await Idle(tree);
            screen.GetViewport().PushInput(KeyEvent(Godot.Key.Up, pressed: true));
            screen.GetViewport().PushInput(KeyEvent(Godot.Key.Up, pressed: false));
            AssertThat(FocusName(screen))
                .OverrideFailureMessage("A keyboard Player 2's Up arrow moved the footer focus Player 1 holds.")
                .IsEqual("RemotePlayNotice");

            // Player 1's next A is the token's Up, never a press of Back.
            await Idle(tree);
            probe.SetButton(0, JoyButton.A, true);
            screen.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: true));
            await Frames(tree, 2);
            await Idle(tree);
            probe.SetButton(0, JoyButton.A, false);
            screen.GetViewport().PushInput(Pad(0, JoyButton.A, pressed: false));
            await Frames(tree, 1);
            AssertThat(backs)
                .OverrideFailureMessage("After a keyboard Player 2's Up arrow, Player 1's A pressed Back.")
                .IsEqual(0);

            // Control: Player 1's own Up still walks the footer.
            await Idle(tree);
            screen.GetViewport().PushInput(Pad(0, JoyButton.DpadUp, pressed: true));
            screen.GetViewport().PushInput(Pad(0, JoyButton.DpadUp, pressed: false));
            AssertThat(FocusName(screen))
                .OverrideFailureMessage("Control: Player 1's d-pad Up no longer walks the footer onto Back.")
                .IsEqual("BackButton");
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>
    /// CM-FIXB-PADUI (finding B18): a pad that disconnects in a local join left
    /// its slot unclaimed without raising <c>DeviceAssigned</c>, so the join
    /// prompt that "shows exactly while the slot is unclaimed" stayed hidden.
    /// Each prompt now returns with the drop and hides again on the reconnect.
    /// </summary>
    [TestCase]
    public async Task AJoinPromptReturnsWhenThatPlayersPadDisconnects() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0, 1);
        input.AutoAssignDevices();
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        var playerOnePrompt = screen.GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1JoinPrompt");
        var playerTwoPrompt = screen.GetNode<Label>(SelectRoot + "PlayersRow/P2Panel/P2JoinPrompt");
        try {
            await Frames(tree, 2);
            await JoinWithPadA(tree, probe, 0);
            await JoinWithPadA(tree, probe, 1);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(1);
            AssertThat(playerOnePrompt.Visible).IsFalse();
            AssertThat(playerTwoPrompt.Visible).IsFalse();

            input.SetConnectedJoypadsForTesting(1);
            AssertThat(screen.IsPlayerOneJoinPromptShown).IsTrue();
            AssertThat(playerOnePrompt.Visible)
                .OverrideFailureMessage("Player 1's pad disconnected, but Player 1's panel does not say how to join again.")
                .IsTrue();

            input.SetConnectedJoypadsForTesting(0, 1);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);
            AssertThat(playerOnePrompt.Visible)
                .OverrideFailureMessage("Player 1's pad reconnected, but the join prompt still shows.")
                .IsFalse();

            input.SetConnectedJoypadsForTesting(0);
            AssertThat(screen.IsPlayerTwoJoined).IsFalse();
            AssertThat(playerTwoPrompt.Visible)
                .OverrideFailureMessage("Player 2's pad disconnected, but Player 2's panel does not say how to join again.")
                .IsTrue();
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    // ---- helpers ------------------------------------------------------------

    private static InputEventKey KeyEvent(Key key, bool pressed) =>
        new() { Keycode = key, PhysicalKeycode = key, Pressed = pressed };

    /// <summary>A pad's A press claims a slot in the local join (the press does nothing else), then is let go.</summary>
    private static async Task JoinWithPadA(SceneTree tree, FakeInputDeviceProbe probe, int device) {
        probe.SetButton(device, JoyButton.A, true);
        await Frames(tree, 3);
        probe.SetButton(device, JoyButton.A, false);
        await Frames(tree, 2);
    }

    private static string FocusName(CharacterSelectScreen screen) =>
        screen.GetViewport().GuiGetFocusOwner()?.Name.ToString() ?? "";

    private static bool MoveListOpen(CharacterSelectScreen screen) =>
        screen.GetNodeOrNull<MoveListScreen>("MoveListScreen")?.Visible == true;

    private static InputEventJoypadButton Pad(int device, JoyButton button, bool pressed) =>
        new() { Device = device, ButtonIndex = button, Pressed = pressed };

    private static async Task Frames(SceneTree tree, int count) {
        for (int frame = 0; frame < count; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    /// <summary>
    /// Waits for the next idle step, where the engine delivers real input:
    /// between physics ticks. A press made inside a tick would still reach that
    /// same tick's capture, which no player's press ever does.
    /// </summary>
    private static async Task Idle(SceneTree tree) => await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

    private static CharacterSelectScreen Open(
        out Node host, FighterMatchOrigin origin = FighterMatchOrigin.LocalVersus) {
        if (GameManager.Instance != null) {
            _savedSession ??= GameManager.Instance.CurrentSession;
            SessionData session = GameManager.Instance.CurrentSession;
            session.FighterMatchOrigin = origin;
            session.ResumeAtStageSelect = false;
            session.HasPendingMatchSeed = false;
            GameManager.Instance.CurrentSession = session;
        }
        host = new Node { Name = "PadMenuSelectHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var screen = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<CharacterSelectScreen>();
        host.AddChild(screen);
        return screen;
    }

    private static void Teardown(Node host) {
        if (GameManager.Instance != null && _savedSession.HasValue) {
            GameManager.Instance.CurrentSession = _savedSession.Value;
            _savedSession = null;
        }
        // A Local Versus screen begins a join that only a scene load ends.
        InputManager.Instance?.EndLocalJoin();
        if (host == null || !GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
