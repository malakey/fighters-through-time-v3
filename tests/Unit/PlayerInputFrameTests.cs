using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace FTT.Tests.Unit;

[TestSuite]
public class PlayerInputFrameTests {
    [TestCase]
    public void SerializationRoundTripPreservesEveryField() {
        // GameplayButtons.Dash is a reserved wire bit (the universal dash
        // mechanic was removed 2026-08-09): production never sets it, but the
        // protocol v2 layout keeps the bit and it must round-trip losslessly.
        PlayerInputFrame original = PlayerInputFrame.Create(
            42,
            -0.75f,
            0.5f,
            GameplayButtons.Jump | GameplayButtons.Block | GameplayButtons.Special2 | GameplayButtons.Roll | GameplayButtons.Dash,
            GameplayButtons.Block | GameplayButtons.BasicAttack);

        PlayerInputFrame restored = PlayerInputFrame.Deserialize(original.Serialize());

        AssertThat(restored.Equals(original)).IsTrue();
        AssertThat(restored.IsPressed(GameplayButtons.Jump)).IsTrue();
        AssertThat(restored.IsReleased(GameplayButtons.BasicAttack)).IsTrue();
        AssertThat(restored.IsHeld(GameplayButtons.Roll)).IsTrue();
        AssertThat(restored.IsHeld(GameplayButtons.Dash)).IsTrue();
    }

    [TestCase]
    public void EdgeBitsAreDerivedFromPriorHeldBits() {
        GameplayButtons previous = GameplayButtons.Block | GameplayButtons.Jump;
        GameplayButtons current = GameplayButtons.Block | GameplayButtons.Special1;

        PlayerInputFrame frame = PlayerInputFrame.Create(7, 0, 0, current, previous);

        AssertThat(frame.IsHeld(GameplayButtons.Block)).IsTrue();
        AssertThat(frame.IsPressed(GameplayButtons.Special1)).IsTrue();
        AssertThat(frame.IsReleased(GameplayButtons.Jump)).IsTrue();
        AssertThat(frame.IsPressed(GameplayButtons.Block)).IsFalse();
    }

    [TestCase]
    public void QuantizedAxesAreStableAcrossRoundTrip() {
        PlayerInputFrame frame = PlayerInputFrame.Create(8, 2.0f, -2.0f, GameplayButtons.None);
        PlayerInputFrame restored = PlayerInputFrame.Deserialize(frame.Serialize());

        AssertThat(restored.MoveX).IsEqual((sbyte)127);
        AssertThat(restored.MoveY).IsEqual((sbyte)-127);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RollHasKeyboardAndRightTriggerDefaults() {
        AssertThat(InputMap.HasAction(InputManager.Actions.Roll)).IsTrue();
        bool hasKeyboardO = false;
        bool hasRightTrigger = false;
        foreach (InputEvent inputEvent in InputMap.ActionGetEvents(InputManager.Actions.Roll)) {
            if (inputEvent is InputEventKey key
                && (key.PhysicalKeycode == Key.O || key.Keycode == Key.O)) hasKeyboardO = true;
            if (inputEvent is InputEventJoypadMotion motion
                && motion.Axis == JoyAxis.TriggerRight
                && motion.AxisValue > 0f) hasRightTrigger = true;
        }
        AssertThat(hasKeyboardO).IsTrue();
        AssertThat(hasRightTrigger).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void InjectedPlayerSourcesRemainIsolated() {
        var manager = new InputManager { MaxPlayers = 2 };
        var playerOne = new BufferedInputSource();
        var playerTwo = new BufferedInputSource();
        playerOne.SetNextFrame(PlayerInputFrame.Create(0, -1, 0, GameplayButtons.BasicAttack));
        playerTwo.SetNextFrame(PlayerInputFrame.Create(0, 1, 0, GameplayButtons.Block));
        manager.SetInputSource(0, playerOne);
        manager.SetInputSource(1, playerTwo);

        PlayerInputFrame first = manager.CaptureFrame(0, 100);
        PlayerInputFrame second = manager.CaptureFrame(1, 100);

        AssertThat(first.MoveX).IsEqual((sbyte)-127);
        AssertThat(second.MoveX).IsEqual((sbyte)127);
        AssertThat(first.IsHeld(GameplayButtons.BasicAttack)).IsTrue();
        AssertThat(first.IsHeld(GameplayButtons.Block)).IsFalse();
        AssertThat(second.IsHeld(GameplayButtons.Block)).IsTrue();
        AssertThat(second.IsHeld(GameplayButtons.BasicAttack)).IsFalse();
        manager.Free();
    }
}
