using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The authored default gamepad layout (2026-10-11, ledger
/// <c>VERIFY-GAMEPAD-ULTIMATE-CHORD</c> (b)).
///
/// <para>Until then <c>project.godot</c> carried the Godot 3 button numbers for
/// the shoulders: Movement Ability on 4, Special 2 on 5 and the Ultimate chord on
/// 4 + 5. Godot 4's <see cref="JoyButton"/> enum reads 4 as <b>Back</b> and 5 as
/// <b>Guide</b> (the Xbox / PS button the OS or Steam usually claims), so Special
/// 2 sat on the Guide button and the Movement Ability shared Back with Time
/// Freeze. The design's table (GDD Controls, C01c) puts the Movement Ability on
/// LB, Special 2 on RB and the Ultimate shortcut on LB + RB at defaults.</para>
///
/// <para>Later the same day Time Freeze itself moved off Back onto <b>L3</b>
/// (<c>JoyButton.LeftStick</c> = 7, user direction D11, ledger
/// <c>USER-GAMEPAD-TIME-FREEZE-L3</c>), so Back now carries no authored action.</para>
///
/// <para>Every case reads the defaults straight from project.godot's
/// <c>[input]</c> section through <see cref="ProjectSettings"/>, never from the
/// live InputMap, which a saved override may have rewritten at boot. Nothing
/// here writes to the InputMap.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GamepadDefaultBindingTests {

    /// <summary>The project-authored UI actions that carry a gamepad default.</summary>
    private static readonly string[] AuthoredUiActions = {
        InputManager.Actions.Pause, InputManager.Actions.DialogueLog
    };

    /// <summary>One action's authored default events, converted to the binding schema.</summary>
    private static List<InputBindingEvent> AuthoredEvents(string action) {
        var events = new List<InputBindingEvent>();
        string key = "input/" + action;
        if (!ProjectSettings.HasSetting(key)) return events;
        using Godot.Collections.Dictionary setting = ProjectSettings.GetSetting(key).AsGodotDictionary();
        if (!setting.ContainsKey("events")) return events;
        using Godot.Collections.Array authored = setting["events"].AsGodotArray();
        foreach (Variant entry in authored) {
            InputBindingEvent converted = InputBindingService.FromInputEvent(entry.As<InputEvent>());
            if (converted != null) events.Add(converted);
        }
        return events;
    }

    /// <summary>The authored joypad button indices of one action, ascending.</summary>
    private static List<int> AuthoredJoyButtons(string action) =>
        AuthoredEvents(action)
            .Where(binding => binding.Kind == InputBindingKind.JoyButton)
            .Select(binding => binding.Code)
            .OrderBy(code => code)
            .ToList();

    /// <summary>Every gameplay action in the InputMap plus the project-authored UI actions.</summary>
    private static List<string> AuthoredActions() {
        var actions = new List<string>();
        Godot.Collections.Array<StringName> names = InputMap.GetActions();
        using (names.AsDisposable()) {
            foreach (StringName name in names) {
                string action = name.ToString();
                if (action.StartsWith("gameplay_")) actions.Add(action);
            }
        }
        actions.AddRange(AuthoredUiActions);
        return actions;
    }

    private static string Describe(IEnumerable<int> buttons) => "[" + string.Join(", ", buttons) + "]";

    [TestCase]
    public void TheMovementAbilityAndSpecialTwoDefaultToTheShoulderButtons() {
        // The Godot 4 numbering the authored file relies on: the shoulders are 9
        // and 10, and 4 / 5 are Back / Guide. If an engine upgrade ever renumbers
        // the enum, this fails before a player finds out.
        AssertThat((int)JoyButton.Back).IsEqual(4);
        AssertThat((int)JoyButton.Guide).IsEqual(5);
        AssertThat((int)JoyButton.LeftShoulder).IsEqual(9);
        AssertThat((int)JoyButton.RightShoulder).IsEqual(10);

        List<int> movement = AuthoredJoyButtons(InputManager.Actions.MovementAbility);
        AssertThat(Describe(movement))
            .OverrideFailureMessage($"The Movement Ability must default to LB alone, not {Describe(movement)}.")
            .IsEqual(Describe(new[] { (int)JoyButton.LeftShoulder }));

        List<int> special2 = AuthoredJoyButtons(InputManager.Actions.Special2);
        AssertThat(Describe(special2))
            .OverrideFailureMessage($"Special 2 must default to RB alone, not {Describe(special2)}.")
            .IsEqual(Describe(new[] { (int)JoyButton.RightShoulder }));
    }

    [TestCase]
    public void TheUltimateChordIsItsPresetRecipeOnBothShoulders() {
        // C01c: the chord is the Movement Ability + Special 2 recipe, "LB+RB at
        // defaults". Since 9.2 (2026-10-16) InputManager recognizes it from those
        // components' live bindings (no longer from the Ultimate action's own pad
        // events), so the recipe's component defaults ARE the chord, and the
        // Controls tab's chord label (which renders the components) describes it.
        InputShortcuts.Recipe recipe = InputShortcuts.RecipeFor(InputShortcuts.UltimateAction);
        AssertObject(recipe).IsNotNull();
        AssertThat(string.Join(",", recipe.Components))
            .IsEqual($"{InputManager.Actions.MovementAbility},{InputManager.Actions.Special2}");
        AssertThat(InputShortcuts.AppliesTo(recipe, InputDeviceKind.Joypad)).IsTrue();
        AssertThat(InputShortcuts.AppliesTo(recipe, InputDeviceKind.Keyboard)).IsFalse();
        List<int> components = recipe.Components
            .SelectMany(component => AuthoredJoyButtons(component))
            .Distinct()
            .OrderBy(code => code)
            .ToList();
        AssertThat(Describe(components))
            .OverrideFailureMessage($"The chord's components default to {Describe(components)}, not LB + RB.")
            .IsEqual(Describe(new[] { (int)JoyButton.LeftShoulder, (int)JoyButton.RightShoulder }));
    }

    /// <summary>
    /// 9.2 (2026-10-16, ledger <c>USER-PAD-ULTIMATE-CHORD-LENIENCY</c>): "No direct
    /// bind". The Ultimate's gamepad direct slot is authored empty — C01c's
    /// "initially Unbound" — so the pad Ultimate is only the recipe chord, while
    /// the keyboard keeps its direct U.
    /// </summary>
    [TestCase]
    public void TheGamepadUltimateDirectSlotIsAuthoredEmptyAndTheKeyboardKeepsU() {
        List<InputBindingEvent> authored = AuthoredEvents(InputManager.Actions.Ultimate);
        List<InputBindingEvent> pad = authored.Where(binding => binding.DeviceKind == InputDeviceKind.Joypad).ToList();
        AssertThat(pad.Count)
            .OverrideFailureMessage($"The gamepad Ultimate direct slot must be empty, not [{string.Join(", ", pad.Select(binding => binding.Signature))}].")
            .IsEqual(0);
        AssertThat(authored.Any(binding => binding.Kind == InputBindingKind.Key && binding.Code == (int)Key.U))
            .OverrideFailureMessage("The keyboard Ultimate must stay U (physical keycode 85).")
            .IsTrue();
        // The chord's two buttons now belong to their components alone.
        AssertThat(Describe(AuthoredJoyButtons(InputManager.Actions.MovementAbility)))
            .IsEqual(Describe(new[] { (int)JoyButton.LeftShoulder }));
        AssertThat(Describe(AuthoredJoyButtons(InputManager.Actions.Special2)))
            .IsEqual(Describe(new[] { (int)JoyButton.RightShoulder }));
    }

    [TestCase]
    public void TimeFreezeDefaultsToTheLeftStickClickAlone() {
        // D11 (2026-10-11, USER-GAMEPAD-TIME-FREEZE-L3): the gamepad Time Freeze
        // is L3, the left-stick click. Pin the enum value the authored file
        // relies on, then the authored joypad half, then the unchanged keyboard R.
        AssertThat((int)JoyButton.LeftStick).IsEqual(7);

        List<int> pad = AuthoredJoyButtons(InputManager.Actions.TimeFreeze);
        AssertThat(Describe(pad))
            .OverrideFailureMessage($"Time Freeze must default to L3 alone, not {Describe(pad)}.")
            .IsEqual(Describe(new[] { (int)JoyButton.LeftStick }));

        bool keyboardR = AuthoredEvents(InputManager.Actions.TimeFreeze)
            .Any(binding => binding.Kind == InputBindingKind.Key && binding.Code == (int)Key.R);
        AssertThat(keyboardR)
            .OverrideFailureMessage("Time Freeze's keyboard default must stay R (physical keycode 82).")
            .IsTrue();
        AssertThat((int)Key.R).IsEqual(82);
    }

    [TestCase]
    public void NoAuthoredActionDefaultsToTheGuideButton() {
        List<string> actions = AuthoredActions();
        // A floor, not an exact count: every gameplay verb plus Pause and the
        // Dialogue Log must actually have been read.
        AssertThat(actions.Contains(InputManager.Actions.Special2)).IsTrue();
        AssertThat(actions.Contains(InputManager.Actions.TimeFreeze)).IsTrue();
        AssertThat(actions.Count >= 17).IsTrue();

        var offenders = new List<string>();
        foreach (string action in actions) {
            if (AuthoredJoyButtons(action).Contains((int)JoyButton.Guide)) offenders.Add(action);
        }
        if (offenders.Count > 0) {
            AssertThat("Guide (5) is the system button: " + string.Join(", ", offenders)).IsEqual("");
        }
    }

    [TestCase]
    public void NoTwoActionsShareADefaultGamepadButton() {
        // Every default button belongs to exactly one action. Until 9.2
        // (2026-10-16) the Ultimate's own events reused the Movement Ability and
        // Special 2 buttons as its chord, and this case exempted them; the pad
        // Ultimate is now the recipe chord with an empty direct slot, so there is
        // no exception left. The Godot 3 numbering broke this too: the Movement
        // Ability and Time Freeze both sat on Back (4), so a gamepad Movement
        // press in Story also froze time.
        var owners = new Dictionary<int, List<string>>();
        foreach (string action in AuthoredActions()) {
            List<int> buttons = AuthoredJoyButtons(action);
            foreach (int button in buttons.Distinct()) {
                if (!owners.TryGetValue(button, out List<string> list)) owners[button] = list = new List<string>();
                list.Add(action);
            }
        }

        var shared = owners
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => $"button {pair.Key}: {string.Join(" + ", pair.Value)}")
            .ToList();
        if (shared.Count > 0) {
            AssertThat("Shared default gamepad buttons: " + string.Join(" | ", shared)).IsEqual("");
        }
    }
}
