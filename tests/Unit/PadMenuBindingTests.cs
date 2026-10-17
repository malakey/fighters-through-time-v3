using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-PADUI (2026-10-16; decision 9.1, pad-only play first-class): Godot 4.7.1's
/// built-in <c>ui_accept</c> is Enter, Keypad Enter and Space and its built-in
/// <c>ui_cancel</c> is Escape, so a gamepad could move GUI focus but never press
/// a focused button or back out of a screen. <c>project.godot</c> now authors
/// both actions with the built-in keyboard events kept and pad A / pad B added.
///
/// <para>The two actions are menu navigation, not gameplay: the Controls tab
/// never offers them, a saved profile never stores or restores them, and the
/// load-time binding repairs never touch them, so pad A can be Jump and
/// ui_accept, and pad B Interact and ui_cancel, at the same time.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PadMenuBindingTests {

    private const string Accept = PadMenuInput.AcceptAction;
    private const string Cancel = PadMenuInput.CancelAction;

    [TestCase]
    public void TheProjectAddsPadAToUiAcceptAndPadBToUiCancelAndKeepsTheKeyboardDefaults() {
        // Read from project.godot itself, not the live map a saved profile may
        // have rewritten. The keys stay logical keycodes, as Godot's built-ins are.
        AssertThat(Describe(AuthoredEvents(Accept))).IsEqual("key:Enter|key:KpEnter|key:Space|pad:0");
        AssertThat(Describe(AuthoredEvents(Cancel))).IsEqual("key:Escape|pad:1");

        // And the live map answers the presses a player makes.
        AssertThat(Pad(0, JoyButton.A).IsActionPressed(Accept)).IsTrue();
        AssertThat(Pad(3, JoyButton.A).IsActionPressed(Accept))
            .OverrideFailureMessage("ui_accept must answer pad A on any device, not only pad 0.")
            .IsTrue();
        AssertThat(Pad(1, JoyButton.B).IsActionPressed(Cancel)).IsTrue();
        AssertThat(Pad(0, JoyButton.B).IsActionPressed(Accept)).IsFalse();
        AssertThat(Pad(0, JoyButton.A).IsActionPressed(Cancel)).IsFalse();
        AssertThat(Pad(0, JoyButton.X).IsActionPressed(Accept) || Pad(0, JoyButton.Y).IsActionPressed(Cancel))
            .IsFalse();
        foreach (Key key in new[] { Key.Enter, Key.KpEnter, Key.Space }) {
            AssertThat(KeyPress(key).IsActionPressed(Accept))
                .OverrideFailureMessage($"{key} no longer confirms.")
                .IsTrue();
        }
        AssertThat(KeyPress(Key.Escape).IsActionPressed(Cancel)).IsTrue();
        // The two pads' gameplay meaning is unchanged: A is still Jump, B Interact.
        AssertThat(Pad(0, JoyButton.A).IsActionPressed(InputManager.Actions.Jump)).IsTrue();
        AssertThat(Pad(0, JoyButton.B).IsActionPressed(InputManager.Actions.Interact)).IsTrue();
    }

    [TestCase]
    public void TheControlsTabAndTheSavedProfileNeverTreatUiAcceptOrUiCancelAsBindable() {
        // Not a Controls-tab row, not in the effective working set, not a default
        // the binding service snapshots.
        List<string> bindable = InputBindingService.BindableActions();
        AssertThat(bindable.Contains(Accept) || bindable.Contains(Cancel)).IsFalse();
        AssertThat(InputBindingService.CaptureEffective().HasOverride(Accept)).IsFalse();
        AssertThat(InputBindingService.ProjectDefaults.HasOverride(Cancel)).IsFalse();

        // A working set that somehow carried ui rows never persists them.
        InputBindingSet effective = InputBindingService.CaptureEffective();
        effective.Set(Accept, new[] { new InputBindingEvent(InputBindingKind.Key, (int)Key.F13) });
        effective.Set(Cancel, new[] { new InputBindingEvent(InputBindingKind.Key, (int)Key.F14) });
        InputBindingSet overrides = InputBindingService.BuildOverrides(effective);
        AssertThat(overrides.HasOverride(Accept) || overrides.HasOverride(Cancel)).IsFalse();

        try {
            // A tampered save carrying keyboard-only ui rows (which would strip pad
            // A and pad B) goes through the load path's repairs and apply: none of
            // them touches menu navigation.
            var saved = new InputBindingSet();
            saved.Set(Accept, new[] { new InputBindingEvent(InputBindingKind.Key, (int)Key.F13) });
            saved.Set(Cancel, new[] {
                new InputBindingEvent(InputBindingKind.JoyButton, 4),
                new InputBindingEvent(InputBindingKind.JoyButton, 5)
            });
            saved.RepairGodot3ShoulderRows(InputBindingService.ProjectDefaults);
            saved.RestoreUnreachableUltimateChord(InputBindingService.ProjectDefaults);
            AssertThat(Describe(saved.For(Cancel))).IsEqual("pad:4|pad:5");
            InputBindingService.Apply(saved);
            AssertThat(Pad(0, JoyButton.A).IsActionPressed(Accept)).IsTrue();
            AssertThat(Pad(0, JoyButton.B).IsActionPressed(Cancel)).IsTrue();
            AssertThat(KeyPress(Key.F13).IsActionPressed(Accept)).IsFalse();

            // A Controls-tab "reset all" reloads project.godot, pad events included.
            InputMap.ActionEraseEvents(Accept);
            InputMap.ActionEraseEvents(Cancel);
            InputBindingService.ResetToProjectDefaults();
            AssertThat(Pad(0, JoyButton.A).IsActionPressed(Accept)).IsTrue();
            AssertThat(Pad(0, JoyButton.B).IsActionPressed(Cancel)).IsTrue();
        } finally {
            InputMap.LoadFromProjectSettings();
        }
    }

    // ---- helpers ------------------------------------------------------------

    private static List<InputEvent> AuthoredEvents(string action) {
        var events = new List<InputEvent>();
        string key = "input/" + action;
        AssertThat(ProjectSettings.HasSetting(key)).IsTrue();
        using Godot.Collections.Dictionary setting = ProjectSettings.GetSetting(key).AsGodotDictionary();
        using Godot.Collections.Array authored = setting["events"].AsGodotArray();
        foreach (Variant entry in authored) events.Add(entry.As<InputEvent>());
        return events;
    }

    private static string Describe(IEnumerable<InputEvent> events) =>
        string.Join("|", events.Select(inputEvent => inputEvent switch {
            InputEventKey key when key.PhysicalKeycode == Key.None && key.Device == -1 => $"key:{key.Keycode}",
            InputEventKey key => $"key:{key.Keycode}/physical:{key.PhysicalKeycode}/device:{key.Device}",
            InputEventJoypadButton pad when pad.Device == -1 => $"pad:{(int)pad.ButtonIndex}",
            InputEventJoypadButton pad => $"pad:{(int)pad.ButtonIndex}/device:{pad.Device}",
            _ => inputEvent?.GetType().Name ?? "null"
        }));

    private static string Describe(IReadOnlyList<InputBindingEvent> events) =>
        string.Join("|", events.Select(binding => binding.Kind == InputBindingKind.JoyButton
            ? $"pad:{binding.Code}"
            : $"{binding.Kind}:{binding.Code}"));

    private static InputEventJoypadButton Pad(int device, JoyButton button) =>
        new() { Device = device, ButtonIndex = button, Pressed = true };

    private static InputEventKey KeyPress(Key key) =>
        new() { Keycode = key, PhysicalKeycode = key, Pressed = true };
}
