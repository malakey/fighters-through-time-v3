using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 9.1 (2026-10-16, CM-INPUT; ledger <c>USER-LAST-USED-DEVICE</c>, agent choices
/// <c>PROVISIONAL-DEVICE-FOLLOW-RULES</c>): player 1 follows the most recently
/// used device instead of the keyboard being player 1 forever and a lone pad
/// player 2. The live <see cref="InputManager"/> polls each device's state once
/// per tick (never input events) and moves slot 0 before sampling it, so the
/// switching press reaches the game as a fresh edge.
///
/// <para>Every case drives the manager through explicit ticks far from the
/// engine's own (<see cref="InputManager.CaptureFrame"/>), mostly with
/// <see cref="FakeInputDeviceProbe"/>, and puts the manager back to its boot
/// state in a finally block.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class InputDeviceFollowTests {
    private const int PadA = 0;
    private const int PadB = 1;
    private static uint _tick = 7_000_000u;

    /// <summary>One manager tick: the poll, then both slots sampled. Returns slot 0's frame.</summary>
    private static PlayerInputFrame Tick(InputManager input) {
        _tick++;
        PlayerInputFrame slotZero = input.CaptureFrame(0, _tick);
        input.CaptureFrame(1, _tick);
        return slotZero;
    }

    [TestCase]
    public void APadPressMovesPlayerOneAndAKeyMovesItBack() {
        InputManager input = InputManager.Instance;
        AssertObject(input).IsNotNull();
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, PadA);
        try {
            input.AutoAssignDevices();
            Tick(input);
            AssertThat(input.JoinMode).IsEqual(LocalJoinMode.FollowLastUsed);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);

            JoyButton jump = FakeInputDeviceProbe.PadButtonFor(InputManager.Actions.Jump);
            probe.SetButton(PadA, jump, true);
            PlayerInputFrame padFrame = Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(PadA);
            AssertThat(padFrame.IsPressed(GameplayButtons.Jump))
                .OverrideFailureMessage("The press that moved player 1 onto the pad did not reach the game as an edge.")
                .IsTrue();
            AssertThat(input.IsJoypadConnected(0)).IsTrue();

            probe.SetButton(PadA, jump, false);
            Tick(input);
            Key space = FakeInputDeviceProbe.KeyFor(InputManager.Actions.Jump);
            probe.SetKey(space, true);
            PlayerInputFrame keyFrame = Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);
            AssertThat(keyFrame.IsPressed(GameplayButtons.Jump)).IsTrue();
            AssertThat(input.IsJoypadConnected(0)).IsFalse();
        } finally {
            probe.ReleaseAll();
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public void MouseMotionAndSubDeadzoneStickDriftDoNotMovePlayerOne() {
        InputManager input = InputManager.Instance;
        AssertObject(input).IsNotNull();
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, PadA);
        try {
            input.AutoAssignDevices();
            Tick(input);
            // A resting stick drifting inside its deadzone (0.15 by default).
            probe.SetAxis(PadA, JoyAxis.LeftX, 0.1f);
            probe.SetAxis(PadA, JoyAxis.LeftY, -0.05f);
            Tick(input);
            Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);
            // Crossing out of the deadzone is activity.
            probe.SetAxis(PadA, JoyAxis.LeftX, 0.7f);
            Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(PadA);

            // Mouse motion never counts. The real Input singleton this time: the
            // poll reads its button and key state, never a motion event.
            input.DeviceActivityProbeForTesting = null;
            Tick(input);
            Input.ParseInputEvent(new InputEventMouseMotion {
                Position = new Vector2(400f, 300f),
                Relative = new Vector2(120f, -40f)
            });
            Input.FlushBufferedEvents();
            Tick(input);
            AssertThat(input.GetDeviceForPlayer(0))
                .OverrideFailureMessage("Mouse motion took player 1 away from the pad.")
                .IsEqual(PadA);
        } finally {
            probe.ReleaseAll();
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public void SlotTwoStaysUnassignedWithTwoPadsOutsideALocalJoin() {
        InputManager input = InputManager.Instance;
        AssertObject(input).IsNotNull();
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, PadA, PadB);
        try {
            input.AutoAssignDevices();
            Tick(input);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(InputManager.UnassignedDevice);

            probe.SetButton(PadB, JoyButton.A, true);
            Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(PadB);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(InputManager.UnassignedDevice);

            probe.SetButton(PadB, JoyButton.A, false);
            probe.SetButton(PadA, JoyButton.A, true);
            Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(PadA);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(InputManager.UnassignedDevice);
            AssertThat(input.IsSlotJoined(1)).IsFalse();
        } finally {
            probe.ReleaseAll();
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public void ADeviceSwitchDropsTheToggleBlockLatch() {
        InputManager input = InputManager.Instance;
        AssertObject(input).IsNotNull();
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, PadA);
        try {
            input.AutoAssignDevices();
            input.BlockModeOverrideForTesting = BlockMode.Toggle;
            Tick(input);
            Key block = FakeInputDeviceProbe.KeyFor(InputManager.Actions.Block);
            AssertThat(block).IsNotEqual(Key.None);
            probe.SetKey(block, true);
            Tick(input);
            probe.SetKey(block, false);
            PlayerInputFrame latched = Tick(input);
            AssertThat(input.IsBlockLatched(0)).IsTrue();
            AssertThat(latched.IsHeld(GameplayButtons.Block)).IsTrue();

            probe.SetButton(PadA, JoyButton.Y, true);
            PlayerInputFrame afterSwitch = Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(PadA);
            AssertThat(input.IsBlockLatched(0))
                .OverrideFailureMessage("A Toggle Block latch survived the hand-over to another device.")
                .IsFalse();
            AssertThat(afterSwitch.IsHeld(GameplayButtons.Block)).IsFalse();
        } finally {
            probe.ReleaseAll();
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public void AnInputSourceOnSlotZeroIsUnaffectedByDeviceActivity() {
        InputManager input = InputManager.Instance;
        AssertObject(input).IsNotNull();
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, PadA);
        var source = new BufferedInputSource();
        try {
            input.AutoAssignDevices();
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.BasicAttack));
            input.SetInputSource(0, source);
            Tick(input);
            probe.SetButton(PadA, FakeInputDeviceProbe.PadButtonFor(InputManager.Actions.Jump), true);
            PlayerInputFrame frame = Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);
            AssertThat(frame.IsHeld(GameplayButtons.BasicAttack)).IsTrue();
            AssertThat(frame.IsHeld(GameplayButtons.Jump)).IsFalse();
            AssertThat(input.IsSlotJoined(0)).IsTrue();
        } finally {
            probe.ReleaseAll();
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public void APressTheSceneAlreadyHandledStillMovesPlayerOne() {
        // Real input state, and a node that marks every event handled: the poll
        // reads Input's per-device state, not event propagation, so the scene
        // swallowing the press changes nothing. (An autoload sees _input after
        // the scene's handlers, and BootSequenceOverlay, SettingsMenu and
        // PauseMenu all mark events handled.)
        InputManager input = InputManager.Instance;
        AssertObject(input).IsNotNull();
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var swallower = new InputSwallowingNode { Name = "DeviceFollowInputSwallower" };
        tree.Root.AddChild(swallower);
        var press = new InputEventJoypadButton { Device = PadA, ButtonIndex = JoyButton.A, Pressed = true };
        try {
            input.DeviceActivityProbeForTesting = null;
            input.SetConnectedJoypadsForTesting(PadA);
            input.AutoAssignDevices();
            Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);

            Input.ParseInputEvent(press);
            Input.FlushBufferedEvents();
            // Delivered through the scene as well, so the swallower certainly
            // handled it before anything else could.
            tree.Root.PushInput(press);
            AssertThat(swallower.Handled > 0)
                .OverrideFailureMessage("Harness: the swallowing node never saw the press.")
                .IsTrue();
            AssertThat(Input.IsJoyButtonPressed(PadA, JoyButton.A)).IsTrue();

            Tick(input);
            AssertThat(input.GetDeviceForPlayer(0))
                .OverrideFailureMessage("A press the scene marked handled did not move player 1.")
                .IsEqual(PadA);
        } finally {
            Input.ParseInputEvent(new InputEventJoypadButton { Device = PadA, ButtonIndex = JoyButton.A, Pressed = false });
            Input.FlushBufferedEvents();
            tree.Root.RemoveChild(swallower);
            swallower.Free();
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public void ALoadToAStageKeepsTheJoinAndALoadToTheMenuOrHubEndsIt() {
        InputManager input = InputManager.Instance;
        AssertObject(input).IsNotNull();
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, PadA, PadB);
        try {
            input.AutoAssignDevices();
            string stage = FTT.Environment.FighterStageCatalog.LoadDefault()?.Stages?[0]?.ScenePath;
            AssertThat(string.IsNullOrEmpty(stage)).IsFalse();
            AssertThat(InputManager.DestinationKeepsLocalJoin(stage)).IsTrue();
            AssertThat(InputManager.DestinationKeepsLocalJoin("res://scenes/arenas/TestArena.tscn")).IsTrue();
            AssertThat(InputManager.DestinationKeepsLocalJoin(FighterFlowRoutes.CharacterSelectScenePath)).IsTrue();
            foreach (string leave in new[] {
                         FighterFlowRoutes.MainMenuScenePath, FighterFlowRoutes.FighterMenuScenePath,
                         FighterFlowRoutes.HubScenePath, "res://scenes/campaign/Level_02_Orleans.tscn" }) {
                AssertThat(InputManager.DestinationKeepsLocalJoin(leave))
                    .OverrideFailureMessage($"A load to {leave} would keep a local join.")
                    .IsFalse();
            }

            // Into a match and back to the select: the players keep their devices.
            input.BeginLocalJoin();
            input.AssignJoypadToPlayer(PadB, 1);
            input.ApplyJoinLifetimeForDestination(stage);
            input.ApplyJoinLifetimeForDestination(FighterFlowRoutes.CharacterSelectScenePath);
            AssertThat(input.JoinMode).IsEqual(LocalJoinMode.Joined);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(PadB);
            // Back to the menu: the join ends and player 2's slot empties.
            input.ApplyJoinLifetimeForDestination(FighterFlowRoutes.MainMenuScenePath);
            AssertThat(input.JoinMode).IsEqual(LocalJoinMode.FollowLastUsed);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(InputManager.UnassignedDevice);

            input.BeginLocalJoin();
            input.ApplyJoinLifetimeForDestination(FighterFlowRoutes.HubScenePath);
            AssertThat(input.JoinMode).IsEqual(LocalJoinMode.FollowLastUsed);
        } finally {
            probe.ReleaseAll();
            FakeInputDeviceProbe.Restore(input);
        }
    }
}

/// <summary>Marks every input event handled; the device-follow "already handled" case's stand-in for a menu.</summary>
public partial class InputSwallowingNode : Node {
    public int Handled { get; private set; }

    public override void _Input(InputEvent @event) {
        Handled++;
        GetViewport()?.SetInputAsHandled();
    }
}
