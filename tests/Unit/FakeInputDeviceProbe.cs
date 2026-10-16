using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.Tests.Unit;

/// <summary>
/// A scripted keyboard, mouse and set of pads for <see cref="InputManager.DeviceActivityProbeForTesting"/>
/// (9.1 / 9.2, CM-INPUT). Not a test suite: the device-follow, select-screen
/// join and Ultimate chord cases share it. Install it, drive it, and always put
/// <see cref="Restore"/> in a finally block.
/// </summary>
internal sealed class FakeInputDeviceProbe : IInputDeviceProbe {
    private readonly HashSet<(int Device, JoyButton Button)> _buttons = new();
    private readonly Dictionary<(int Device, JoyAxis Axis), float> _axes = new();
    private readonly HashSet<Key> _keys = new();
    private readonly HashSet<MouseButton> _mouse = new();

    public bool IsJoyButtonPressed(int device, JoyButton button) => _buttons.Contains((device, button));
    public float GetJoyAxis(int device, JoyAxis axis) => _axes.GetValueOrDefault((device, axis));
    public bool IsPhysicalKeyPressed(Key key) => _keys.Contains(key);
    public bool IsMouseButtonPressed(MouseButton button) => _mouse.Contains(button);

    public void SetButton(int device, JoyButton button, bool pressed) {
        if (pressed) _buttons.Add((device, button));
        else _buttons.Remove((device, button));
    }

    public void SetAxis(int device, JoyAxis axis, float value) => _axes[(device, axis)] = value;

    public void SetKey(Key key, bool pressed) {
        if (pressed) _keys.Add(key);
        else _keys.Remove(key);
    }

    public void SetMouse(MouseButton button, bool pressed) {
        if (pressed) _mouse.Add(button);
        else _mouse.Remove(button);
    }

    public void ReleaseAll() {
        _buttons.Clear();
        _axes.Clear();
        _keys.Clear();
        _mouse.Clear();
    }

    /// <summary>The first joypad button an action is bound to in the live InputMap.</summary>
    public static JoyButton PadButtonFor(string action) {
        Godot.Collections.Array<InputEvent> events = InputMap.ActionGetEvents(action);
        using var lifetime = events.AsDisposable();
        foreach (InputEvent inputEvent in events) {
            if (inputEvent is InputEventJoypadButton button) return button.ButtonIndex;
        }
        return JoyButton.Invalid;
    }

    /// <summary>The first physical key an action is bound to in the live InputMap.</summary>
    public static Key KeyFor(string action) {
        Godot.Collections.Array<InputEvent> events = InputMap.ActionGetEvents(action);
        using var lifetime = events.AsDisposable();
        foreach (InputEvent inputEvent in events) {
            if (inputEvent is InputEventKey key) {
                return key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
            }
        }
        return Key.None;
    }

    /// <summary>Installs a fresh fake on the live manager with the given pads connected.</summary>
    public static FakeInputDeviceProbe Install(InputManager manager, params int[] connectedPads) {
        var probe = new FakeInputDeviceProbe();
        manager.DeviceActivityProbeForTesting = probe;
        manager.SetConnectedJoypadsForTesting(connectedPads);
        return probe;
    }

    /// <summary>Puts the live manager back to its boot state: real devices, keyboard on slot 0, following.</summary>
    public static void Restore(InputManager manager) {
        if (manager == null) return;
        manager.DeviceActivityProbeForTesting = null;
        manager.BlockModeOverrideForTesting = null;
        for (int slot = 0; slot < manager.MaxPlayers; slot++) manager.ClearInputSource(slot);
        manager.RestoreConnectedDevicesForTesting();
        manager.AutoAssignDevices();
    }
}
