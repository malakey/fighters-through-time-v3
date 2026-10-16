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

    /// <summary>
    /// CM-FIXA-INPUT (2026-10-16): the triggers are axes, not buttons, and the
    /// poll used to read only buttons and the left stick — so the default pad
    /// Block (LT) and Roll (RT) never took player 1 off the keyboard, and the
    /// hero read no Block. A bound axis crossing out of its action's deadzone is
    /// now activity, and the pull that moves the slot reaches the game as a
    /// fresh edge; a trigger resting inside the deadzone is not.
    /// </summary>
    [TestCase]
    public void ATriggerPullMovesPlayerOneAndItsFirstFrameCarriesTheVerb() {
        InputManager input = InputManager.Instance;
        AssertObject(input).IsNotNull();
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, PadA);
        try {
            input.AutoAssignDevices();
            input.BlockModeOverrideForTesting = BlockMode.Hold;
            Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);

            // A trigger resting a little in, inside Block's 0.5 deadzone.
            probe.SetAxis(PadA, JoyAxis.TriggerLeft, 0.3f);
            Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);

            probe.SetAxis(PadA, JoyAxis.TriggerLeft, 1.0f);
            PlayerInputFrame block = Tick(input);
            AssertThat(input.GetDeviceForPlayer(0))
                .OverrideFailureMessage("Pulling LT did not move player 1 onto the pad.")
                .IsEqual(PadA);
            AssertThat(block.IsPressed(GameplayButtons.Block))
                .OverrideFailureMessage("The LT pull that moved player 1 did not reach the game as a Block press.")
                .IsTrue();

            // Back to the keyboard, then RT: the same for Roll.
            probe.SetAxis(PadA, JoyAxis.TriggerLeft, 0f);
            Tick(input);
            Key space = FakeInputDeviceProbe.KeyFor(InputManager.Actions.Jump);
            probe.SetKey(space, true);
            Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);
            probe.SetKey(space, false);
            Tick(input);
            probe.SetAxis(PadA, JoyAxis.TriggerRight, 1.0f);
            PlayerInputFrame roll = Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(PadA);
            AssertThat(roll.IsPressed(GameplayButtons.Roll)).IsTrue();
        } finally {
            probe.ReleaseAll();
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>
    /// CM-FIXA-INPUT (2026-10-16; decision 9.1 read literally): beginning a local
    /// join pre-claims no slot — not even the device that drove the menus. The
    /// first device to make a join press is player 1 and the next different one
    /// player 2, a left click (Basic Attack) joining the keyboard-and-mouse
    /// device. A slot 0 nobody claimed falls back to the keyboard when the join ends.
    /// </summary>
    [TestCase]
    public void ALocalJoinPreClaimsNoSlotAndTheFirstTwoDevicesToPressAreTheTwoPlayers() {
        InputManager input = InputManager.Instance;
        AssertObject(input).IsNotNull();
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, PadA, PadB);
        try {
            input.AutoAssignDevices();
            Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);
            input.BeginLocalJoin();
            AssertThat(input.GetDeviceForPlayer(0))
                .OverrideFailureMessage("The keyboard that drove the menus was pre-claimed as player 1.")
                .IsEqual(InputManager.UnassignedDevice);
            AssertThat(input.HasFreeSlot()).IsTrue();
            AssertThat(input.AnySlotJoined()).IsFalse();

            // The join press an unclaimed pad makes is a GUI event the select consumes.
            var padBPress = new InputEventJoypadButton { Device = PadB, ButtonIndex = JoyButton.B, Pressed = true };
            AssertThat(input.IsJoinPressFromUnclaimedDevice(padBPress)).IsTrue();
            AssertThat(input.IsJoinPressFromUnclaimedDevice(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true }))
                .OverrideFailureMessage("Escape is not a join press.")
                .IsFalse();

            probe.SetButton(PadB, JoyButton.B, true);
            PlayerInputFrame first = Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(PadB);
            AssertThat(first.Pressed)
                .OverrideFailureMessage("The press that joined player 1 also reached the game.")
                .IsEqual(GameplayButtons.None);
            AssertThat(input.IsJoinPressFromUnclaimedDevice(padBPress))
                .OverrideFailureMessage("A claimed pad's press is still read as a join.")
                .IsFalse();

            MouseButton click = FakeInputDeviceProbe.MouseButtonFor(InputManager.Actions.BasicAttack);
            AssertThat(click).IsNotEqual(MouseButton.None);
            probe.SetMouse(click, true);
            Tick(input);
            AssertThat(input.GetDeviceForPlayer(1))
                .OverrideFailureMessage("A left click did not join the keyboard and mouse as player 2.")
                .IsEqual(InputManager.KeyboardDevice);
            AssertThat(input.HasFreeSlot()).IsFalse();

            // No free slot: a third device's press joins nothing and is not consumed.
            var padAPress = new InputEventJoypadButton { Device = PadA, ButtonIndex = JoyButton.A, Pressed = true };
            AssertThat(input.IsJoinPressFromUnclaimedDevice(padAPress)).IsFalse();
            probe.SetButton(PadA, JoyButton.A, true);
            Tick(input);
            AssertThat(input.GetPlayerForDevice(PadA)).IsEqual(-1);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(PadB);

            // Ending the join keeps player 1's device.
            input.EndLocalJoin();
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(PadB);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(InputManager.UnassignedDevice);

            // A join nobody pressed into hands slot 0 back to the keyboard.
            input.BeginLocalJoin();
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.UnassignedDevice);
            input.EndLocalJoin();
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);
            AssertThat(input.JoinMode).IsEqual(LocalJoinMode.FollowLastUsed);
        } finally {
            probe.ReleaseAll();
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
