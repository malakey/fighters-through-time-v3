using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>UR-WIDGETS step 2. The tagged panel container: sorting, tag placement and the minimum size.</summary>
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
    public void TheTagKeepsItsRawKeyIsTiltedAndAnEmptyKeyHidesIt() {
        var (tagged, _, parent) = Build("menu_settings");
        try {
            AssertThat(tagged.TagLabel.Text).IsEqual("menu_settings");
            AssertThat(tagged.TagLabel.Uppercase).IsTrue();
            AssertThat(tagged.Tag.Visible).IsTrue();
            AssertThat(tagged.Tag.ZIndex).IsEqual(1);
            tagged.Notification((int)Container.NotificationSortChildren);
            AssertThat(Mathf.IsEqualApprox(tagged.Tag.Rotation, Mathf.DegToRad(-1.2f))).IsTrue();
            AssertThat(tagged.Tag.Position).IsEqual(new Vector2(3f, 3f) + PulpTaggedPanel.TagOffset);
            tagged.TagKey = "";
            AssertThat(tagged.Tag.Visible).IsFalse();
        } finally {
            parent.Free();
        }
    }
}
