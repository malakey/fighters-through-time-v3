using System.Collections.Generic;
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

    private static bool InsideButton(Node node) {
        for (Node up = node.GetParent(); up != null; up = up.GetParent()) if (up is BaseButton) return true;
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
            AssertThat(caret.IconName).IsEqual("hourglass");
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
            AssertThat(speaker.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.SpeakerName);
        } finally {
            DialogueLog.Clear();
            Free(log);
        }
    }
}
