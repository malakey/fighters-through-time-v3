using System.Linq;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Meter and Defy update (2026-10-31, MM-INPUT; ledger <c>USER-DEFY-OWN-BUTTON</c>,
/// <c>PROVISIONAL-DEFY-INPUT</c>): <c>gameplay_defy</c> is a remappable action on
/// keyboard F and pad R3 that sets <see cref="GameplayButtons.Defy"/> on the device
/// frame.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DefyBindingTests {
    private const int Pad = 0;
    private static uint _tick = 9_000_000u;

    private static PlayerInputFrame Tick(InputManager input) {
        _tick++;
        PlayerInputFrame slotZero = input.CaptureFrame(0, _tick);
        input.CaptureFrame(1, _tick);
        return slotZero;
    }

    [TestCase]
    public void KeyboardFAndPadR3EachSetTheDefyBitOnTheirOwnDevice() {
        InputManager input = InputManager.Instance;
        AssertObject(input).IsNotNull();
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, Pad);
        try {
            input.AutoAssignDevices();
            Tick(input);
            AssertThat(Tick(input).IsHeld(GameplayButtons.Defy)).IsFalse();

            probe.SetKey(Key.F, true);
            PlayerInputFrame keyFrame = Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);
            AssertThat(keyFrame.IsPressed(GameplayButtons.Defy)).IsTrue();
            probe.SetKey(Key.F, false);
            Tick(input);

            probe.SetButton(Pad, JoyButton.RightStick, true);
            PlayerInputFrame padFrame = Tick(input);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(Pad);
            AssertThat(padFrame.IsPressed(GameplayButtons.Defy)).IsTrue();
            probe.SetButton(Pad, JoyButton.RightStick, false);
            AssertThat(Tick(input).IsHeld(GameplayButtons.Defy)).IsFalse();
        } finally {
            probe.ReleaseAll();
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public void DefyIsRemappableAndListedWithItsControlsLabel() {
        AssertThat(InputManager.RemappableActions.Contains(InputManager.Actions.Defy)).IsTrue();
        AssertThat(InputManager.ActionLabelKey(InputManager.Actions.Defy)).IsEqual("controls_action_defy");
        AssertThat(InputBindingService.BindableActions().Contains(InputManager.Actions.Defy)).IsTrue();
        AssertThat(InputMap.HasAction(InputManager.Actions.Defy)).IsTrue();
        // A single button: no recipe and no reachability requirement.
        AssertObject(InputShortcuts.RecipeFor(InputManager.Actions.Defy)).IsNull();
        AssertThat(InputShortcuts.RequiredReachableActions.Contains(InputManager.Actions.Defy)).IsFalse();
    }

    [TestCase]
    public void ResetToDefaultRestoresFAndR3() {
        string defy = InputManager.Actions.Defy;
        try {
            var overrides = new InputBindingSet();
            overrides.Set(defy, new[] { new InputBindingEvent(InputBindingKind.Key, (int)Key.G) });
            InputBindingService.Apply(overrides);
            AssertThat(InputBindingService.CaptureAction(defy).Any(binding => binding.Code == (int)Key.F)).IsFalse();

            InputBindingService.ResetActionToProjectDefault(defy);
            var restored = InputBindingService.CaptureAction(defy);
            AssertThat(restored.Any(binding => binding.Kind == InputBindingKind.Key && binding.Code == (int)Key.F)).IsTrue();
            AssertThat(restored.Any(binding => binding.Kind == InputBindingKind.JoyButton && binding.Code == 8)).IsTrue();
        } finally {
            InputBindingService.ResetActionToProjectDefault(defy);
        }
    }
}
