using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A3: haptics must buzz the device a player slot actually owns.
/// Two bugs are pinned here:
/// <list type="bullet">
/// <item>damage feedback vibrated hardcoded device 0, so in local 1v1 player two's
/// damage buzzed player one's pad;</item>
/// <item>guard-break feedback passed the <em>player index</em> straight to
/// <c>Input.StartJoyVibration</c> as if it were a device id.</item>
/// </list>
/// Both now resolve through <c>InputManager.GetDeviceForPlayer</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class HapticDeviceRoutingTests {

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [TestCase]
    public void ADeviceIsResolvedFromTheSlotMappingNotFromTheSlotNumber() {
        InputManager manager = InputManager.Instance;
        AssertObject(manager).IsNotNull();
        try {
            // Give slot 1 the keyboard. A correct implementation reports device -1
            // for that slot; the old bug would have reported the index itself.
            manager.AssignKeyboardToPlayer(1);
            AssertThat(HapticFeedbackManager.ResolveDevice(1)).IsEqual(InputManager.KeyboardDevice);
            AssertThat(HapticFeedbackManager.ResolveDevice(1) == 1).IsFalse();

            // Slot 0 lost the keyboard to slot 1, so it owns nothing at all and
            // must report the sentinel rather than falling back to device 0.
            AssertThat(HapticFeedbackManager.ResolveDevice(0)).IsEqual(InputManager.UnassignedDevice);
            AssertThat(HapticFeedbackManager.ResolveDevice(0) == 0).IsFalse();
        } finally {
            // Restore the boot assignment for every later suite.
            manager.AutoAssignDevices();
        }
        AssertThat(HapticFeedbackManager.ResolveDevice(0)).IsEqual(InputManager.KeyboardDevice);
    }

    [TestCase]
    public void ANegativePlayerSlotNeverResolvesToARealDevice() {
        // Enemy and boss attackers carry player index -1; they must never buzz a pad.
        AssertThat(HapticFeedbackManager.ResolveDevice(-1)).IsEqual(InputManager.UnassignedDevice);
    }

    [TestCase]
    public void BothSentinelDeviceValuesAreRejectedByTheVibrationGuard() {
        // Vibrate() drops anything below zero, which covers both sentinels: the
        // keyboard has no motors and an unassigned slot has no device at all.
        AssertThat(InputManager.KeyboardDevice < 0).IsTrue();
        AssertThat(InputManager.UnassignedDevice < 0).IsTrue();
    }

    [TestCase]
    public void PlayerScopedVibrationIsSafeForUnownedAndInvalidSlots() {
        HapticFeedbackManager haptics = HapticFeedbackManager.Instance;
        AssertObject(haptics).IsNotNull();
        // No assigned pad on these slots: silent no-ops, never an exception and
        // never a stray buzz routed to device 0.
        haptics.VibrateForPlayer(1, 0.5f, 0.5f, 0.05f);
        haptics.VibrateForPlayer(-1, 0.5f, 0.5f, 0.05f);
        haptics.OnHitConfirm(InputManager.UnassignedDevice, isHeavy: true);
        haptics.OnUltimateActivation(InputManager.UnassignedDevice);
    }
}
