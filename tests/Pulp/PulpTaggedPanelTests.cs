using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>The tagged panel container (TG-WIDGETS: the glass tag; first written by UR-WIDGETS): sorting, tag placement and the minimum size.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpTaggedPanelTests {

    private static (PulpTaggedPanel tagged, PanelContainer panel, VBoxContainer parent) Build(string key) {
        var parent = new VBoxContainer();
        UIPalette.ApplyTheme(parent);
        var tagged = new PulpTaggedPanel { TagKey = key };
        var panel = new PanelContainer { ThemeTypeVariation = UIPalette.Variations.InkPanel, CustomMinimumSize = new Vector2(300f, 160f) };
        panel.AddChild(new Label { Text = "x" });
        tagged.AddChild(panel);
        parent.AddChild(tagged);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(parent);
        return (tagged, panel, parent);
    }

    [TestCase]
    public void ThePanelFillsTheRectAndTheMinimumSizeIsThePanels() {
        var (tagged, panel, parent) = Build("menu_settings");
        try {
            parent.ResetSize();
            tagged.ForceUpdateTransform();
            AssertThat(tagged.GetCombinedMinimumSize()).IsEqual(panel.GetCombinedMinimumSize());
            AssertThat(panel.Size).IsEqual(tagged.Size);
            AssertThat(panel.Position).IsEqual(Vector2.Zero);
            AssertThat(tagged.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            AssertThat(tagged.Panel).IsEqual(panel);
        } finally {
            parent.Free();
        }
    }

    [TestCase]
    public void TheGlassTagKeepsItsRawKeyHangsAtTwentySixMinusElevenUntiltedAndAnEmptyKeyHidesIt() {
        var (tagged, _, parent) = Build("menu_settings");
        try {
            AssertThat(tagged.TagLabel.Text).IsEqual("menu_settings");
            AssertThat(tagged.TagLabel.Uppercase).IsTrue();
            AssertThat(tagged.Tag.Visible).IsTrue();
            AssertThat(tagged.Tag.ZIndex).IsEqual(1);
            tagged.Notification((int)Container.NotificationSortChildren);
            // UI_SPEC_GLASS 3.1: at the panel's (chamfer 18 + 8, -11), no tilt; the plate keeps the 24 px tag's centre.
            AssertThat(PulpTaggedPanel.TagOffset).IsEqual(new Vector2(26f, -11f));
            AssertThat(tagged.Tag.Rotation).IsEqual(0f);
            AssertThat(tagged.Tag.Position.X).IsEqual(26f);
            float centreY = tagged.Tag.Position.Y + tagged.Tag.Size.Y / 2f;
            AssertThat(Mathf.Abs(centreY - (-11f + 12f)) <= 0.5f).OverrideFailureMessage($"the tag's centre is at {centreY}").IsTrue();
            AssertThat(tagged.Tag.ThemeTypeVariation.ToString()).IsEqual(UIPalette.Variations.PanelTagPlate);
            tagged.TagKey = "";
            AssertThat(tagged.Tag.Visible).IsFalse();
        } finally {
            parent.Free();
        }
    }
}
