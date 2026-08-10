using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using GdUnit4;
using Godot;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A4. The InputMap binding override schema: normalization, the
/// encrypted-envelope round trip, the restore-before-gameplay push, per-action
/// and global reset, and the conflict rules the remap UI enforces.
///
/// <para>Every test that writes to the InputMap restores it with
/// <c>InputMap.LoadFromProjectSettings()</c> in a finally block. A leaked
/// override would break every later suite that reads a default binding —
/// <c>PlayerInputFrameTests.RollHasKeyboardAndRightTriggerDefaults</c> first.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class InputBindingSchemaTests {

    [TestCase]
    public void NormalizeClampsEveryEventShapeAndDropsUnusableRows() {
        var set = new InputBindingSet();
        set.Actions["gameplay_jump"] = new List<InputBindingEvent> {
            new(InputBindingKind.Key, -5),                       // invalid: clamps to 0
            new(InputBindingKind.Key, (int)Key.Space),
            new(InputBindingKind.Key, (int)Key.Space),           // duplicate
            new((InputBindingKind)99, (int)Key.Q),               // unknown kind -> Key
            new(InputBindingKind.JoyAxis, 0, -7),                // axis sign clamps to -1
            null
        };
        set.Actions["   "] = new List<InputBindingEvent> { new(InputBindingKind.Key, (int)Key.A) };
        set.Actions["gameplay_block"] = new List<InputBindingEvent>();

        set.Normalize();

        AssertThat(set.Actions.ContainsKey("   ")).IsFalse();
        AssertThat(set.Actions.ContainsKey("gameplay_block")).IsFalse();
        List<InputBindingEvent> jump = set.Actions["gameplay_jump"];
        // Space, the unknown-kind event coerced to Key Q, and the axis survive.
        AssertThat(jump.Count).IsEqual(3);
        AssertThat(jump[0].Code).IsEqual((int)Key.Space);
        AssertThat(jump.Last().Kind).IsEqual(InputBindingKind.JoyAxis);
        AssertThat(jump.Last().AxisSign).IsEqual(-1);
        AssertThat(jump[1].Kind).IsEqual(InputBindingKind.Key);
        // Non-axis kinds never keep a sign.
        AssertThat(jump[0].AxisSign).IsEqual(0);
    }

    [TestCase]
    public void DeviceKindSeparatesKeyboardMouseFromJoypadSlots() {
        AssertThat(new InputBindingEvent(InputBindingKind.Key, 65).DeviceKind).IsEqual(InputDeviceKind.Keyboard);
        AssertThat(new InputBindingEvent(InputBindingKind.MouseButton, 1).DeviceKind).IsEqual(InputDeviceKind.Keyboard);
        AssertThat(new InputBindingEvent(InputBindingKind.JoyButton, 0).DeviceKind).IsEqual(InputDeviceKind.Joypad);
        AssertThat(new InputBindingEvent(InputBindingKind.JoyAxis, 5, 1).DeviceKind).IsEqual(InputDeviceKind.Joypad);
    }

    [TestCase]
    public void MultiEventBindingsRoundTripThroughTheEncryptedGlobalEnvelope() {
        // gameplay_move_left ships four events; the dead Dictionary<string,string>
        // this schema replaced could not represent even one of them.
        var data = new GlobalSaveData();
        data.InputBindings.Set("gameplay_move_left", new[] {
            new InputBindingEvent(InputBindingKind.Key, (int)Key.A),
            new InputBindingEvent(InputBindingKind.Key, (int)Key.Left),
            new InputBindingEvent(InputBindingKind.JoyAxis, (int)JoyAxis.LeftX, -1),
            new InputBindingEvent(InputBindingKind.JoyButton, (int)JoyButton.DpadLeft)
        });
        data.InputBindings.Set("gameplay_basic_attack", new[] {
            new InputBindingEvent(InputBindingKind.MouseButton, (int)MouseButton.Left)
        });
        data.Normalize();

        byte[] key = SequentialKey();
        byte[] envelope = SaveEnvelopeCodec.Encode(
            "global", data.SaveVersion, JsonConvert.SerializeObject(data), key, 42L);

        AssertThat(SaveEnvelopeCodec.TryDecode(envelope, key, out DecodedSaveEnvelope decoded, out _)).IsTrue();
        GlobalSaveData restored = SaveSchemaMigrator.DeserializeGlobal(decoded.Json);

        IReadOnlyList<InputBindingEvent> left = restored.InputBindings.For("gameplay_move_left");
        AssertThat(left.Count).IsEqual(4);
        AssertThat(left[0].Kind).IsEqual(InputBindingKind.Key);
        AssertThat(left[0].Code).IsEqual((int)Key.A);
        AssertThat(left[2].Kind).IsEqual(InputBindingKind.JoyAxis);
        AssertThat(left[2].Code).IsEqual((int)JoyAxis.LeftX);
        AssertThat(left[2].AxisSign).IsEqual(-1);
        AssertThat(left[3].Code).IsEqual((int)JoyButton.DpadLeft);
        AssertThat(restored.InputBindings.For("gameplay_basic_attack")[0].Kind)
            .IsEqual(InputBindingKind.MouseButton);
    }

    [TestCase]
    public void SavedOverridesReachTheInputMapAndUnlistedActionsKeepProjectDefaults() {
        List<InputBindingEvent> jumpDefault = InputBindingService.CaptureAction(InputManager.Actions.Jump);
        List<InputBindingEvent> rollDefault = InputBindingService.CaptureAction(InputManager.Actions.Roll);
        try {
            var overrides = new InputBindingSet();
            overrides.Set(InputManager.Actions.Jump, new[] {
                new InputBindingEvent(InputBindingKind.Key, (int)Key.Z),
                new InputBindingEvent(InputBindingKind.JoyButton, (int)JoyButton.Y)
            });

            InputBindingService.Apply(overrides);

            List<InputBindingEvent> jumpNow = InputBindingService.CaptureAction(InputManager.Actions.Jump);
            AssertThat(jumpNow.Count).IsEqual(2);
            AssertThat(jumpNow[0].Code).IsEqual((int)Key.Z);
            AssertThat(jumpNow[1].Code).IsEqual((int)JoyButton.Y);

            // The unlisted action is untouched — no blanket rewrite of the map.
            List<InputBindingEvent> rollNow = InputBindingService.CaptureAction(InputManager.Actions.Roll);
            AssertThat(rollNow.Count).IsEqual(rollDefault.Count);
            for (int index = 0; index < rollNow.Count; index++) {
                AssertThat(rollNow[index].Matches(rollDefault[index])).IsTrue();
            }
        } finally {
            InputMap.LoadFromProjectSettings();
        }

        List<InputBindingEvent> jumpRestored = InputBindingService.CaptureAction(InputManager.Actions.Jump);
        AssertThat(jumpRestored.Count).IsEqual(jumpDefault.Count);
    }

    [TestCase]
    public void SaveManagerPushesSavedBindingsIntoTheInputMap() {
        SaveManager manager = SaveManager.Instance;
        AssertObject(manager).IsNotNull();
        InputBindingSet original = manager.GlobalData.InputBindings;
        try {
            var overrides = new InputBindingSet();
            overrides.Set(InputManager.Actions.Interact, new[] {
                new InputBindingEvent(InputBindingKind.Key, (int)Key.F)
            });
            manager.GlobalData.InputBindings = overrides;

            manager.ApplySavedInputBindings();

            List<InputBindingEvent> interact = InputBindingService.CaptureAction(InputManager.Actions.Interact);
            AssertThat(interact.Count).IsEqual(1);
            AssertThat(interact[0].Code).IsEqual((int)Key.F);
        } finally {
            manager.GlobalData.InputBindings = original;
            InputMap.LoadFromProjectSettings();
        }
    }

    [TestCase]
    public void ResettingRestoresProjectDefaultsPerActionAndGlobally() {
        List<InputBindingEvent> blockDefault = InputBindingService.CaptureAction(InputManager.Actions.Block);
        try {
            var overrides = new InputBindingSet();
            overrides.Set(InputManager.Actions.Block, new[] {
                new InputBindingEvent(InputBindingKind.Key, (int)Key.F14)
            });
            InputBindingService.Apply(overrides);
            AssertThat(InputBindingService.CaptureAction(InputManager.Actions.Block)[0].Code)
                .IsEqual((int)Key.F14);

            InputBindingService.ResetActionToProjectDefault(InputManager.Actions.Block);
            List<InputBindingEvent> afterAction = InputBindingService.CaptureAction(InputManager.Actions.Block);
            AssertThat(afterAction.Count).IsEqual(blockDefault.Count);
            AssertThat(afterAction[0].Matches(blockDefault[0])).IsTrue();

            InputBindingService.Apply(overrides);
            InputBindingService.ResetToProjectDefaults();
            AssertThat(InputBindingService.CaptureAction(InputManager.Actions.Block)[0].Matches(blockDefault[0]))
                .IsTrue();
        } finally {
            InputMap.LoadFromProjectSettings();
        }
    }

    [TestCase]
    public void OnlyActionsThatDifferFromProjectDefaultsArePersisted() {
        // Compare against the captured project defaults rather than the live map so
        // the assertion does not depend on whatever the local global.sav holds.
        InputBindingSet effective = InputBindingService.ProjectDefaults.Clone();
        InputBindingSet unchanged = InputBindingService.BuildOverrides(effective);
        AssertThat(unchanged.Actions.Count).IsEqual(0);

        effective.Set(InputManager.Actions.Special1, new[] {
            new InputBindingEvent(InputBindingKind.Key, (int)Key.N)
        });
        InputBindingSet changed = InputBindingService.BuildOverrides(effective);
        AssertThat(changed.Actions.Count).IsEqual(1);
        AssertThat(changed.HasOverride(InputManager.Actions.Special1)).IsTrue();
    }

    [TestCase]
    public void ConflictDetectionBlocksAnInputAlreadyOwnedByAnotherAction() {
        var effective = new InputBindingSet();
        effective.Set(InputManager.Actions.Jump, new[] {
            new InputBindingEvent(InputBindingKind.Key, (int)Key.Space)
        });
        effective.Set(InputManager.Actions.Block, new[] {
            new InputBindingEvent(InputBindingKind.JoyAxis, (int)JoyAxis.TriggerLeft, 1)
        });

        // Space already drives Jump: refuse, and name the owner.
        AssertThat(InputBindingConflicts.FindConflictingAction(
            effective, InputManager.Actions.Roll, new InputBindingEvent(InputBindingKind.Key, (int)Key.Space)))
            .IsEqual(InputManager.Actions.Jump);

        // A free key applies.
        AssertThat(InputBindingConflicts.IsFree(
            effective, InputManager.Actions.Roll, new InputBindingEvent(InputBindingKind.Key, (int)Key.P)))
            .IsTrue();

        // The same axis with the opposite sign is a different physical input.
        AssertThat(InputBindingConflicts.IsFree(
            effective,
            InputManager.Actions.Roll,
            new InputBindingEvent(InputBindingKind.JoyAxis, (int)JoyAxis.TriggerLeft, -1)))
            .IsTrue();

        // Re-binding an event the same action already owns reports that action, so
        // the UI explains the no-op rather than duplicating the event.
        AssertThat(InputBindingConflicts.FindConflictingAction(
            effective, InputManager.Actions.Jump, new InputBindingEvent(InputBindingKind.Key, (int)Key.Space)))
            .IsEqual(InputManager.Actions.Jump);
    }

    [TestCase]
    public void RestoreRefusesNonRemappableActionsFromATamperedPayload() {
        // Audit H-2 blast radius / §5.2: the restore path used to apply saved
        // bindings to ANY existing InputMap action, so a tampered global payload
        // could rebind ui_* navigation or the read-only gameplay_ultimate chord.
        List<InputBindingEvent> acceptDefault = InputBindingService.CaptureAction("ui_accept");
        List<InputBindingEvent> ultimateDefault = InputBindingService.CaptureAction(InputManager.Actions.Ultimate);
        try {
            var tampered = new InputBindingSet();
            tampered.Set("ui_accept", new[] { new InputBindingEvent(InputBindingKind.Key, (int)Key.F13) });
            tampered.Set(InputManager.Actions.Ultimate, new[] { new InputBindingEvent(InputBindingKind.Key, (int)Key.F14) });

            InputBindingService.Apply(tampered);

            List<InputBindingEvent> acceptNow = InputBindingService.CaptureAction("ui_accept");
            AssertThat(acceptNow.Count).IsEqual(acceptDefault.Count);
            for (int index = 0; index < acceptNow.Count; index++) {
                AssertThat(acceptNow[index].Matches(acceptDefault[index])).IsTrue();
            }
            List<InputBindingEvent> ultimateNow = InputBindingService.CaptureAction(InputManager.Actions.Ultimate);
            AssertThat(ultimateNow.Count).IsEqual(ultimateDefault.Count);
            for (int index = 0; index < ultimateNow.Count; index++) {
                AssertThat(ultimateNow[index].Matches(ultimateDefault[index])).IsTrue();
            }
        } finally {
            InputMap.LoadFromProjectSettings();
        }
    }

    [TestCase]
    public void RestoreCapsTheEventCountPerAction() {
        try {
            var overrides = new InputBindingSet();
            var events = new List<InputBindingEvent>();
            for (int index = 0; index < InputBindingService.MaxEventsPerAction + 4; index++) {
                events.Add(new InputBindingEvent(InputBindingKind.Key, (int)Key.A + index));
            }
            overrides.Set(InputManager.Actions.Jump, events);

            InputBindingService.Apply(overrides);

            AssertThat(InputBindingService.CaptureAction(InputManager.Actions.Jump).Count)
                .IsEqual(InputBindingService.MaxEventsPerAction);
        } finally {
            InputMap.LoadFromProjectSettings();
        }
    }

    [TestCase]
    public void TheUltimateChordIsExcludedFromRemapping() {
        AssertThat(InputManager.RemappableActions.Contains(InputManager.Actions.Ultimate)).IsFalse();
        AssertThat(InputManager.ReadOnlyActions.Contains(InputManager.Actions.Ultimate)).IsTrue();
        // The universal dash mechanic was removed (2026-08-09): no InputMap
        // action for it may ever reappear.
        AssertThat(InputMap.HasAction("gameplay_dash")).IsFalse();
        foreach (string action in InputManager.RemappableActions) {
            AssertThat(InputMap.HasAction(action))
                .OverrideFailureMessage($"Remappable action {action} is not in the InputMap.").IsTrue();
        }
    }

    [TestCase]
    public void EveryActionLabelAndBindingDescriptionResolvesThroughTheTranslationTable() {
        TranslationServer.SetLocale("en");
        var unresolved = new List<string>();
        foreach (string action in InputManager.RemappableActions.Concat(InputManager.ReadOnlyActions)) {
            string key = InputManager.ActionLabelKey(action);
            if (TranslationServer.Translate(key).ToString() == key) unresolved.Add(key);
        }
        string[] uiKeys = {
            "settings_tab_audio", "settings_tab_display", "settings_tab_gameplay", "settings_tab_controls",
            "settings_window_mode", "settings_window_windowed", "settings_window_fullscreen",
            "settings_window_borderless", "controls_hint", "controls_listening", "controls_conflict",
            "controls_reset_all", "controls_reset_action", "controls_binding_unbound",
            "controls_ultimate_readonly"
        };
        foreach (string key in uiKeys) {
            if (TranslationServer.Translate(key).ToString() == key) unresolved.Add(key);
        }

        if (unresolved.Count > 0) {
            AssertThat("Unresolved through the compiled en.en.translation: " + string.Join(", ", unresolved))
                .IsEqual("");
        }
    }

    private static byte[] SequentialKey() {
        byte[] key = new byte[32];
        for (int index = 0; index < key.Length; index++) key[index] = (byte)(index + 7);
        return key;
    }
}
