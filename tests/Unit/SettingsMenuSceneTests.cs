using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A4. The authored settings screen: it instantiates, its tab titles
/// and labels are localized rather than raw node names, the dead difficulty
/// dropdown is gone, the remap rows exist with per-device-kind slots, conflicts
/// are refused, and — the defect this suite exists for — closing the screen never
/// touches <see cref="SceneTree.Paused"/>.
///
/// <para>Pause discipline, per CLAUDE.md failure signature 4: every test that
/// pauses restores the tree in a finally block.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SettingsMenuSceneTests {

    [TestCase]
    public void TheAuthoredSceneInstantiatesWithAllFourTabs() {
        var host = NewHost("SettingsSceneHost");
        try {
            SettingsMenu menu = OpenMenu(host);
            AssertObject(menu.Root).IsNotNull();
            AssertObject(menu.Tabs).IsNotNull();
            AssertThat(menu.Tabs.GetTabCount()).IsEqual(4);

            string[] names = { "Audio", "Display", "Gameplay", "Controls" };
            for (int index = 0; index < names.Length; index++) {
                AssertThat(menu.Tabs.GetChild(index).Name.ToString()).IsEqual(names[index]);
            }
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TabTitlesAndControlsCopyAreLocalizedRatherThanRawNodeNames() {
        TranslationServer.SetLocale("en");
        var host = NewHost("SettingsLocalizationHost");
        try {
            SettingsMenu menu = OpenMenu(host);
            string[] expected = {
                TranslationServer.Translate("settings_tab_audio").ToString(),
                TranslationServer.Translate("settings_tab_display").ToString(),
                TranslationServer.Translate("settings_tab_gameplay").ToString(),
                TranslationServer.Translate("settings_tab_controls").ToString()
            };
            for (int index = 0; index < expected.Length; index++) {
                AssertThat(menu.Tabs.GetTabTitle(index)).IsEqual(expected[index]);
            }

            var hint = menu.Tabs.GetNode<Label>("Controls/Hint");
            AssertThat(hint.Text).IsEqual(TranslationServer.Translate("controls_hint").ToString());
            AssertThat(hint.Text).IsNotEqual("controls_hint");

            var readOnly = menu.Tabs.GetNode<Label>("Controls/ReadOnlyInfo");
            AssertThat(readOnly.Text.Contains("controls_ultimate_readonly")).IsFalse();
            AssertThat(readOnly.Text.Length > 0).IsTrue();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheDeadDifficultyDropdownIsGone() {
        var host = NewHost("SettingsDifficultyHost");
        try {
            SettingsMenu menu = OpenMenu(host);
            // The old Gameplay tab carried a difficulty OptionButton that nothing
            // ever read or persisted; campaign difficulty is chosen per save slot.
            var gameplay = menu.Tabs.GetNode<VBoxContainer>("Gameplay");
            Godot.Collections.Array<Node> children = gameplay.GetChildren();
            using var lifetime = children.AsDisposable();
            AssertThat(children.Any(child => child is OptionButton)).IsFalse();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void ClosingTheScreenLeavesThePauseOwnerInCharge() {
        var host = NewHost("SettingsPauseHost");
        SceneTree tree = host.GetTree();
        try {
            SettingsMenu menu = OpenMenu(host);

            // Stand in for a pause menu that opened Settings while holding the pause.
            tree.Paused = true;
            bool closedRaised = false;
            menu.Closed += () => closedRaised = true;

            menu.Close();

            AssertThat(menu.Visible).IsFalse();
            AssertThat(closedRaised).IsTrue();
            // The old OnClosePressed set this to false unconditionally, silently
            // resuming gameplay behind an open pause menu.
            AssertThat(tree.Paused).IsTrue();
        } finally {
            tree.Paused = false;
            Teardown(host);
        }
    }

    [TestCase]
    public void LeavingTheTreeDoesNotStealThePause() {
        var host = NewHost("SettingsTeardownHost");
        SceneTree tree = host.GetTree();
        try {
            SettingsMenu menu = OpenMenu(host);
            tree.Paused = true;

            host.RemoveChild(menu);
            menu.Free();

            AssertThat(tree.Paused).IsTrue();
        } finally {
            tree.Paused = false;
            Teardown(host);
        }
    }

    [TestCase]
    public void EveryBindableActionGetsAKeyboardAJoypadAnUnbindAndAResetControl() {
        var host = NewHost("SettingsRowsHost");
        try {
            SettingsMenu menu = OpenMenu(host);
            var rows = menu.Tabs.GetNode<VBoxContainer>("Controls/Scroll/ActionRows");
            System.Collections.Generic.List<string> bindable = InputBindingService.BindableActions();
            AssertThat(rows.GetChildCount()).IsEqual(bindable.Count);

            foreach (string action in bindable) {
                var row = rows.GetNode<HBoxContainer>($"Row_{action}");
                AssertObject(row).IsNotNull();
                // C01c: label + keyboard slot + joypad slot + Unbind + reset. The
                // Unbind control is the new one — without it the player could
                // never reach the explicit-Unbound state the save format stores.
                AssertThat(row.GetChildCount()).IsEqual(5);
            }

            // Ultimate has a real row now rather than the Package 8 read-only line.
            AssertObject(rows.GetNodeOrNull<HBoxContainer>(
                $"Row_{InputManager.Actions.Ultimate}")).IsNotNull();
        } finally {
            Teardown(host);
        }
    }

    /// <summary>
    /// C01c's preset shortcuts get an On/Off control per applicable device kind,
    /// and the Ultimate chord is gamepad-only — the contract adds no new keyboard
    /// Ultimate chord, so offering a keyboard toggle for one would be a lie.
    /// </summary>
    [TestCase]
    public void ThePresetShortcutsEachGetAnOnOffToggleForTheirDeviceKinds() {
        var host = NewHost("SettingsShortcutsHost");
        try {
            SettingsMenu menu = OpenMenu(host);
            var rows = menu.Tabs.GetNode<VBoxContainer>("Controls/ShortcutRows");

            AssertObject(rows.GetNodeOrNull<HBoxContainer>(
                $"Shortcut_{InputShortcuts.UltimateAction}_{(int)InputDeviceKind.Joypad}")).IsNotNull();
            AssertObject(rows.GetNodeOrNull<HBoxContainer>(
                $"Shortcut_{InputShortcuts.UltimateAction}_{(int)InputDeviceKind.Keyboard}")).IsNull();

            foreach (string action in new[] { InputShortcuts.GrabAction, InputShortcuts.EchoStepAction }) {
                AssertObject(rows.GetNodeOrNull<HBoxContainer>(
                    $"Shortcut_{action}_{(int)InputDeviceKind.Keyboard}")).IsNotNull();
                AssertObject(rows.GetNodeOrNull<HBoxContainer>(
                    $"Shortcut_{action}_{(int)InputDeviceKind.Joypad}")).IsNotNull();
            }

            // Legacy payloads reproduce the shipped chords: every toggle starts On.
            foreach (Node child in rows.GetChildren()) {
                var toggle = child.GetChild<CheckButton>(0);
                AssertThat(toggle.ButtonPressed)
                    .OverrideFailureMessage($"{child.Name} must default ON.").IsTrue();
            }
        } finally {
            Teardown(host);
        }
    }

    /// <summary>
    /// C01a: the toggle sits in Settings → Gameplay beside Screen Shake, and its
    /// help text is authored copy rather than a raw key.
    /// </summary>
    [TestCase]
    public void TheGameplayTabCarriesTheReducedTemporalEffectsToggleAndItsHelpText() {
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        if (data == null) return;
        bool original = data.ReducedTemporalEffects;
        var host = NewHost("SettingsComfortHost");
        try {
            TranslationServer.SetLocale("en");
            data.ReducedTemporalEffects = true;
            SettingsMenu menu = OpenMenu(host);

            var toggle = menu.Tabs.GetNode<CheckButton>("Gameplay/ReducedEffectsToggle");
            var help = menu.Tabs.GetNode<Label>("Gameplay/ReducedEffectsHelp");
            AssertThat(toggle.ButtonPressed)
                .OverrideFailureMessage("The toggle must load from the saved value.").IsTrue();
            AssertThat(toggle.Text)
                .IsEqual(TranslationServer.Translate("settings_reduced_temporal_effects").ToString());
            AssertThat(help.Text.Length > 0).IsTrue();
            AssertThat(help.Text)
                .IsEqual(TranslationServer.Translate("settings_reduced_temporal_effects_help").ToString());

            // It sits beside Screen Shake, not in some other tab.
            AssertObject(menu.Tabs.GetNodeOrNull<HSlider>("Gameplay/ScreenShakeSlider")).IsNotNull();
        } finally {
            data.ReducedTemporalEffects = original;
            ComfortSettings.Apply(original);
            Teardown(host);
        }
    }

    [TestCase]
    public void ACaptureThatCollidesWithAnotherActionIsRefused() {
        var host = NewHost("SettingsConflictHost");
        try {
            SettingsMenu menu = OpenMenu(host);
            IReadOnlyList<InputBindingEvent> rollBefore =
                menu.WorkingBindings.For(InputManager.Actions.Roll).Select(e => e.Clone()).ToList();

            // Space already drives Jump in project.godot.
            string conflict = menu.CaptureForTesting(
                InputManager.Actions.Roll,
                InputDeviceKind.Keyboard,
                new InputBindingEvent(InputBindingKind.Key, (int)Key.Space));

            AssertThat(conflict).IsEqual(InputManager.Actions.Jump);
            IReadOnlyList<InputBindingEvent> rollAfter = menu.WorkingBindings.For(InputManager.Actions.Roll);
            AssertThat(rollAfter.Count).IsEqual(rollBefore.Count);
            AssertThat(menu.IsListening).IsFalse();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void AFreeCaptureReplacesOnlyItsOwnDeviceKind() {
        var host = NewHost("SettingsCaptureHost");
        try {
            SettingsMenu menu = OpenMenu(host);
            List<InputBindingEvent> joypadBefore = menu.WorkingBindings
                .For(InputManager.Actions.Roll)
                .Where(binding => binding.DeviceKind == InputDeviceKind.Joypad)
                .Select(binding => binding.Clone())
                .ToList();

            string conflict = menu.CaptureForTesting(
                InputManager.Actions.Roll,
                InputDeviceKind.Keyboard,
                new InputBindingEvent(InputBindingKind.Key, (int)Key.F13));

            AssertThat(conflict).IsEqual("");
            List<InputBindingEvent> after = menu.WorkingBindings.For(InputManager.Actions.Roll).ToList();
            List<InputBindingEvent> keyboardAfter =
                after.Where(binding => binding.DeviceKind == InputDeviceKind.Keyboard).ToList();
            List<InputBindingEvent> joypadAfter =
                after.Where(binding => binding.DeviceKind == InputDeviceKind.Joypad).ToList();

            AssertThat(keyboardAfter.Count).IsEqual(1);
            AssertThat(keyboardAfter[0].Code).IsEqual((int)Key.F13);
            AssertThat(joypadAfter.Count).IsEqual(joypadBefore.Count);
            for (int index = 0; index < joypadAfter.Count; index++) {
                AssertThat(joypadAfter[index].Matches(joypadBefore[index])).IsTrue();
            }
        } finally {
            InputMap.LoadFromProjectSettings();
            Teardown(host);
        }
    }

    [TestCase]
    public void ShowingTheScreenAuthorsFocusAndRebuildsTheChainOnTabSwitch() {
        // H-10 (audit 2026-08-08): the screen previously had zero focus authoring —
        // a controller/keyboard player could not reach a single control and input
        // kept driving the surface beneath the overlay.
        var host = NewHost("SettingsFocusHost");
        try {
            SettingsMenu menu = OpenMenu(host);

            AssertThat(menu.FocusChain.Count > 0)
                .OverrideFailureMessage("Show() must author a focus chain.").IsTrue();
            foreach (Control control in menu.FocusChain) {
                AssertThat(control.FocusMode)
                    .OverrideFailureMessage($"{control.Name} is not focusable")
                    .IsEqual(Control.FocusModeEnum.All);
                bool linked = !control.FocusNeighborTop.IsEmpty || !control.FocusNeighborBottom.IsEmpty;
                AssertThat(linked)
                    .OverrideFailureMessage($"{control.Name} has no focus neighbour")
                    .IsTrue();
            }

            // The internal tab bar is spliced in so tabs are switchable at all.
            AssertThat(menu.FocusChain.Any(control => control is TabBar)).IsTrue();

            // Focus is held inside the settings surface, not the opener beneath it.
            Control owner = menu.Root.GetViewport().GuiGetFocusOwner();
            AssertObject(owner).IsNotNull();
            AssertThat(menu.IsAncestorOf(owner))
                .OverrideFailureMessage("Initial focus must land inside Settings.").IsTrue();

            // Switching to the Controls tab swaps the visible controls, so the
            // chain must be rebuilt to include them.
            menu.Tabs.CurrentTab = 3;
            AssertThat(menu.FocusChain.Any(control =>
                    control is Button button && button.Name.ToString().Length > 0
                    && menu.Tabs.GetNode<VBoxContainer>("Controls/Scroll/ActionRows").IsAncestorOf(button)))
                .OverrideFailureMessage("The rebuilt chain must reach the remap rows.").IsTrue();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void SaveLoadNoticesResolveThroughTheTranslationTable() {
        TranslationServer.SetLocale("en");
        string recovered = SaveManager.FormatNotice("save_notice_slot_recovered", new[] { "2" });
        AssertThat(recovered.Contains("2")).IsTrue();
        AssertThat(recovered).IsNotEqual("save_notice_slot_recovered");

        AssertThat(SaveManager.FormatNotice("save_notice_global_corrupt", null))
            .IsNotEqual("save_notice_global_corrupt");
        AssertThat(SaveManager.FormatNotice("save_notice_error", new[] { "boom" })).IsEqual("boom");
        AssertThat(SaveManager.FormatNotice("", null)).IsEqual("");
        AssertThat(SaveManager.Instance?.LastLoadNoticeKey).IsNotNull();
    }

    private static Node NewHost(string name) {
        var host = new Node { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    private static SettingsMenu OpenMenu(Node host) {
        var menu = new SettingsMenu { Name = "SettingsMenu" };
        host.AddChild(menu);
        menu.Show();
        return menu;
    }

    /// <summary>
    /// Frees the host immediately rather than queueing it. The authored settings
    /// scene is ~60 nodes; a deferred free leaves every one of them counted as an
    /// orphan when GdUnit4 tallies at the end of the test.
    /// </summary>
    private static void Teardown(Node host) {
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
