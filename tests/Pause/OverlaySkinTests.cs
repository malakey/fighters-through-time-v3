using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pause;

/// <summary>
/// UR-PAUSE: the overlays (Settings, Move List, Systems Card, confirm modal, dialogue box, dialogue log) wear the
/// pulp parts: toggle tracks, banner buttons, the tagged panel, the 150 px roundel and the hourglass caret.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class OverlaySkinTests {

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    private static void Free(Node node) {
        if (node != null && GodotObject.IsInstanceValid(node)) {
            node.GetParent()?.RemoveChild(node);
            node.Free();
        }
        Tree.Paused = false;
    }

    // A kit widget's own text (a button decor's label and badge, the toggle's ON / OFF, the prompt bar's key-caps) is the
    // widget's to colour.
    private static bool InsideButton(Node node) {
        for (Node up = node.GetParent(); up != null; up = up.GetParent()) if (up is BaseButton or PulpPromptBar) return true;
        return false;
    }

    private static void Collect<T>(Node node, List<T> into) where T : Node {
        if (node is T match) into.Add(match);
        for (int index = 0; index < node.GetChildCount(); index++) Collect(node.GetChild(index), into);
    }

    [TestCase]
    public void EverySettingsSwitchIsAPulpToggleAndNoSettingsLabelCarriesAStyleOverride() {
        var settings = new SettingsMenu { Name = "SettingsSkin" };
        Tree.Root.AddChild(settings);
        try {
            settings.Show();
            var toggles = new List<CheckButton>();
            Collect(settings, toggles);
            AssertThat(toggles.Count > 5).OverrideFailureMessage("Settings lost its switches.").IsTrue();
            foreach (CheckButton toggle in toggles) {
                AssertObject(toggle.GetNodeOrNull<PulpToggleDecor>("PulpToggleDecor"))
                    .OverrideFailureMessage(toggle.Name + " is not a pulp toggle.").IsNotNull();
            }
            var labels = new List<Label>();
            Collect(settings, labels);
            var styled = new List<string>();
            foreach (Label label in labels) {
                if (InsideButton(label)) continue;   // a button decor's own text (the toggle ON / OFF, the prompt badge) is the widget's
                if (label.HasThemeFontSizeOverride("font_size") || label.HasThemeColorOverride("font_color")) styled.Add(label.Name);
            }
            if (styled.Count > 0) AssertThat(string.Join(" | ", styled)).IsEqual("");
            AssertObject(settings.Root.GetNodeOrNull<PausePanel>("Margin/Panel")).IsNotNull();
        } finally {
            Free(settings);
        }
    }

    [TestCase]
    public void TheMoveListAndTheSystemsCardSitOnTheInkPanelWithBannerFooters() {
        var list = new MoveListScreen { Name = "MoveListSkin" };
        var card = new SystemsCardScreen { Name = "SystemsCardSkin" };
        Tree.Root.AddChild(list);
        Tree.Root.AddChild(card);
        try {
            list.Open("einstein");
            card.Open();
            var tagged = list.Root.GetNodeOrNull<PausePanel>("Center/Panel");
            AssertObject(tagged).IsNotNull();
            AssertThat(tagged.Tag.Visible).IsTrue();
            AssertObject(list.Root.GetNodeOrNull<Button>("Center/Panel/Layout/FooterRow/CloseButton")
                ?.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor")).IsNotNull();
            AssertObject(card.Root.GetNodeOrNull<PausePanel>("Center/Panel")).IsNotNull();
            AssertObject(card.Root.GetNodeOrNull<Button>("Center/Panel/Layout/FooterRow/CloseButton")
                ?.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor")).IsNotNull();
            AssertThat(card.Root.GetNodeOrNull<PanelContainer>("Shade")?.ThemeTypeVariation.ToString()).IsEqual("ScrimPanel");
        } finally {
            Free(list);
            Free(card);
        }
    }

    [TestCase]
    public void TheConfirmModalUsesTheInkPanelAndCompactDecoratedButtons() {
        ConfirmModal modal = ConfirmModal.Create("pause_quit_confirm", titleKey: "pause_lower_difficulty");
        Tree.Root.AddChild(modal);
        try {
            modal.Open();
            AssertObject(modal.GetNodeOrNull<PausePanel>("Center/Panel")).IsNotNull();
            foreach (Button button in new[] { modal.CancelButton, modal.ConfirmButton }) {
                AssertThat(button.ThemeTypeVariation.ToString()).IsEqual("PulpButtonCompact");
                AssertObject(button.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor")).IsNotNull();
            }
            var title = modal.GetNodeOrNull<Label>("Center/Panel/Layout/Title");
            AssertThat(title.HasThemeColorOverride("font_color")).OverrideFailureMessage("The title rides the role colour.").IsFalse();
        } finally {
            Free(modal);
        }
    }

    [TestCase]
    public void TheDialogueBoxShowsTheRoundelAndTheHourglassCaretOnceTheLineLands() {
        DialogueManager dialogue = DialogueManager.CreateDefault();
        Tree.Root.AddChild(dialogue);
        try {
            AssertThat(dialogue.RegisterSetFromPath("res://resources/Dialogue/level_08_dialogue.tres")).IsTrue();
            var frame = dialogue.GetNode<PanelContainer>("Panel/Layout/PortraitFrame");
            var roundel = frame.GetNodeOrNull<PulpPortraitRoundel>("Roundel");
            AssertObject(roundel).OverrideFailureMessage("The portrait roundel is missing.").IsNotNull();
            AssertThat(roundel.Diameter).IsEqual(DialogueTheme.PortraitSize);
            var caret = dialogue.GetNodeOrNull<PulpIcon>("CaretHost/AdvanceCaret");
            AssertObject(caret).OverrideFailureMessage("The advance caret is missing.").IsNotNull();
            AssertThat(caret.IconName).IsEqual("hourglass_caret");   // TG-PAUSE: the solid caret (UI_SPEC_GLASS 3.9)
            AssertThat(caret.Visible).IsFalse();

            AssertThat(dialogue.StartSequence("level_08.postboss")).IsTrue();
            AssertThat(caret.Visible).OverrideFailureMessage("The caret waits for the reveal.").IsFalse();
            for (int step = 0; step < 200 && !dialogue.Reveal.IsComplete; step++) dialogue._Process(0.25);
            AssertThat(caret.Visible).IsTrue();

            var panel = dialogue.GetNode<PanelContainer>("Panel");
            AssertThat(panel.OffsetRight - panel.OffsetLeft).IsEqual(DialogueTheme.BoxWidth);
            var speaker = dialogue.GetNode<Label>("Panel/Layout/Content/SpeakerRow/SpeakerLabel");
            AssertThat(speaker.HasThemeColorOverride("font_color")).IsFalse();
        } finally {
            Free(dialogue);
        }
    }

    [TestCase]
    public void TheDialogueLogRowsUseRoundelsAndTheSpeakerRole() {
        var log = new DialogueLogScreen { Name = "LogSkin" };
        Tree.Root.AddChild(log);
        try {
            DialogueLog.Clear();
            DialogueLog.Append(new DialogueLog.Entry("test", "Joan", "Hold the line.", "", false));
            log.Open();
            var rows = new List<PulpPortraitRoundel>();
            Collect(log, rows);
            AssertThat(rows.Count).IsEqual(1);
            var labels = new List<Label>();
            Collect(log, labels);
            Label speaker = labels.Find(l => l.Name == "Speaker");
            AssertObject(speaker).IsNotNull();
            // TG-PAUSE (9.2): the log's speaker is Cinzel 700 22 in accent, the HeroName role (SpeakerName is the box's T50).
            AssertThat(speaker.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.HeroName);
        } finally {
            DialogueLog.Clear();
            Free(log);
        }
    }

    private static bool Same(GodotObject a, GodotObject b) => a != null && b != null && a.GetInstanceId() == b.GetInstanceId();

    private static async Task Frames(int count) {
        for (int i = 0; i < count; i++) await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>
    /// TG-PAUSE (UI_SPEC_GLASS 3.3, 9.2): Settings wears the glass parts. The tab titles are drawn upper-case on the
    /// theme's glass tabs; every option button is a glass field at least 300 x 44; every slider is 360 wide with the glass
    /// field's focus look and its value in the 90 px T53 column, all three on one centre line; a toggle row is 62 tall; the
    /// Audio, Display and Gameplay rows scroll inside their tab behind the glass scroll bar, which keeps a focused row in
    /// view, as the Controls remap list scrolls behind it.
    /// </summary>
    [TestCase]
    public async Task SettingsTabsFieldsSlidersAndScrollBarsWearTheGlassLooks() {
        var settings = new SettingsMenu { Name = "SettingsGlass" };
        Tree.Root.AddChild(settings);
        try {
            settings.Show();
            await Frames(3);
            Theme theme = UIPalette.LoadTheme();
            TabContainer tabs = settings.Tabs;
            string[] titleKeys = { "settings_tab_audio", "settings_tab_display", "settings_tab_gameplay", "settings_tab_controls" };
            for (int index = 0; index < titleKeys.Length; index++) {
                string title = tabs.GetTabTitle(index);
                AssertThat(title).IsEqual(SettingsMenu.TabTitle(titleKeys[index]));
                AssertThat(title).IsEqual(title.ToUpperInvariant());
            }
            AssertThat(tabs.GetThemeStylebox("tab_selected").GetInstanceId()).IsEqual(theme.GetStylebox("tab_selected", "TabContainer").GetInstanceId());
            AssertThat(tabs.GetThemeStylebox("tab_unselected").GetInstanceId()).IsEqual(theme.GetStylebox("tab_unselected", "TabContainer").GetInstanceId());

            var issues = new List<string>();
            for (int index = 0; index < 3; index++) {
                tabs.CurrentTab = index;
                await Frames(3);
                var layout = tabs.GetChild(index) as SettingsRowLayout;
                if (layout == null) { issues.Add($"tab {index} is not a settings row layout"); continue; }
                if (!layout.Scrolls || layout.ScrollBar == null) issues.Add($"{layout.Name} does not scroll");
                else if (!Same(layout.ScrollBar.GetThemeStylebox("scroll"), theme.GetStylebox("scroll", "VScrollBar"))) {
                    issues.Add($"{layout.Name}'s scroll bar is not the glass bar");
                }
                var fields = new List<OptionButton>();
                Collect(layout, fields);
                foreach (OptionButton field in fields) {
                    if (!Same(field.GetThemeStylebox("normal"), theme.GetStylebox("normal", "OptionButton"))) issues.Add(field.Name + " is not a glass field");
                    if (field.Size.X < 300f - 0.5f || field.Size.Y < 44f - 0.5f) issues.Add($"{field.Name} is {field.Size}");
                }
                var sliders = new List<HSlider>();
                Collect(layout, sliders);
                foreach (HSlider slider in sliders) {
                    if (!Mathf.IsEqualApprox(slider.Size.X, 360f)) issues.Add($"{slider.Name} is {slider.Size.X} wide");
                    if (!Same(slider.GetThemeStylebox("focus"), theme.GetStylebox("focus", "GlassField"))) issues.Add(slider.Name + " has no glass focus look");
                    var value = slider.GetParent().GetNodeOrNull<Label>(slider.Name.ToString().Replace("Slider", "Value"));
                    if (value == null) { issues.Add(slider.Name + " has no value column"); continue; }
                    if (value.ThemeTypeVariation != UIPalette.LabelRoles.SettingsRowValue) issues.Add(value.Name + " is not T53");
                    if (value.Text != SettingsMenu.FormatSliderValue(slider.Value)) issues.Add($"{value.Name} reads '{value.Text}'");
                    if (!Mathf.IsEqualApprox(value.Size.X, 90f)) issues.Add($"{value.Name} is {value.Size.X} wide");
                    // One row: the label, the slider and the value share a centre line.
                    var label = slider.GetParent().GetNodeOrNull<Label>(slider.Name.ToString().Replace("Slider", "Label"));
                    float centre = slider.Position.Y + slider.Size.Y / 2f;
                    if (label != null && Mathf.Abs(label.Position.Y + label.Size.Y / 2f - centre) > 1f) issues.Add(label.Name + " is off its row");
                    if (Mathf.Abs(value.Position.Y + value.Size.Y / 2f - centre) > 1f) issues.Add(value.Name + " is off its row");
                }
                var toggles = new List<CheckButton>();
                Collect(layout, toggles);
                foreach (CheckButton toggle in toggles) {
                    if (!Mathf.IsEqualApprox(toggle.Size.Y, SettingsRowLayout.SpecRowHeight)) issues.Add($"{toggle.Name}'s row is {toggle.Size.Y} tall");
                }
            }
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

            // The Gameplay rows run past their tab at 100 %: focusing the last one scrolls it fully into view.
            tabs.CurrentTab = 2;
            await Frames(3);
            var gameplay = tabs.GetNode<SettingsRowLayout>("Gameplay");
            AssertThat(gameplay.ScrollBar.Visible).OverrideFailureMessage("The Gameplay rows fit; nothing to scroll.").IsTrue();
            AssertThat(gameplay.ScrollOffset).IsEqual(0f);
            var last = tabs.GetNode<Control>("Gameplay/CrashReportsRow/CrashReportsDropdown");
            last.GrabFocus();
            await Frames(3);
            AssertThat(gameplay.ScrollOffset).IsGreater(0f);
            Rect2 view = gameplay.GetGlobalRect();
            AssertThat(last.GetGlobalRect().End.Y <= view.End.Y + 1f).OverrideFailureMessage("The focused row is still below the tab.").IsTrue();
            AssertThat(last.GetGlobalRect().Position.Y >= view.Position.Y - 1f).IsTrue();

            ScrollContainer remaps = tabs.GetNode<ScrollContainer>("Controls/Scroll");
            AssertThat(Same(remaps.GetVScrollBar().GetThemeStylebox("scroll"), theme.GetStylebox("scroll", "VScrollBar"))).IsTrue();
        } finally {
            Free(settings);
        }
    }
}
