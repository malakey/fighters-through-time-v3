using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>UR-SCREENS. The shared screen helpers: a tag that leaves the panel path alone, the halftone, the leading icon, the button inset.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpScreenKitTests {

    private static (Control host, PanelContainer panel) Build() {
        var host = new Control();
        UIPalette.ApplyTheme(host);
        var panel = new PanelContainer { Name = "Panel", ThemeTypeVariation = UIPalette.Variations.InkPanel };
        panel.AddChild(new Label { Name = "Body", Text = "x" });
        host.AddChild(panel);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return (host, panel);
    }

    [TestCase]
    public void ATagIsAChildOfThePanelAndShowsOnlyWithAKey() {
        var (host, panel) = Build();
        try {
            Label tag = PulpScreenKit.AddTag(panel, "common_back");
            var anchor = panel.GetNodeOrNull<PanelTagAnchor>(PulpScreenKit.TagAnchorName);
            AssertThat(anchor != null).IsTrue();
            AssertThat(tag.Text).IsEqual("common_back");
            AssertThat(anchor.Tag.Visible).IsTrue();
            AssertThat(panel.GetNodeOrNull<Label>("Body") != null).OverrideFailureMessage("The panel own children must keep their paths.").IsTrue();
            PulpScreenKit.AddTag(panel, "");
            AssertThat(anchor.Tag.Visible).IsFalse();
            AssertThat(panel.GetChildCount()).IsEqual(2);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void DressingAPanelPutsTheHalftoneBehindItsContentOnce() {
        var (host, panel) = Build();
        try {
            PulpScreenKit.DressPanel(panel, "common_back");
            PulpScreenKit.DressPanel(panel, "common_back");
            AssertThat(panel.GetChild(0) is HalftoneCorner).IsTrue();
            int corners = 0;
            for (int i = 0; i < panel.GetChildCount(); i++) if (panel.GetChild(i) is HalftoneCorner) corners++;
            AssertThat(corners).IsEqual(1);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ALeadingIconKeepsTheLabelTextOneString() {
        var host = new Control();
        UIPalette.ApplyTheme(host);
        var label = new Label { Text = "Dust: 12" };
        host.AddChild(label);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            PulpIcon icon = PulpScreenKit.AddLeadingIcon(label, "dust", UIPalette.Res, 24f);
            PulpScreenKit.AddLeadingIcon(label, "dust", UIPalette.Res, 24f);
            AssertThat(label.Text).IsEqual("Dust: 12");
            AssertThat(icon.IconName).IsEqual("dust");
            AssertThat(label.GetChildCount()).IsEqual(1);
            AssertThat(label.GetThemeStylebox("normal").GetContentMargin(Side.Left) > 24f).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void AButtonStackPanelKeepsTheFocusPoseInsideItsPlate() {
        var (host, panel) = Build();
        try {
            PulpScreenKit.InsetRight(panel);
            AssertThat(panel.GetThemeStylebox("panel").GetContentMargin(Side.Right) >= PulpScreenKit.ButtonStackRightInset).IsTrue();
            // The shared theme plate is untouched.
            var other = new PanelContainer { ThemeTypeVariation = UIPalette.Variations.InkPanel };
            host.AddChild(other);
            AssertThat(other.GetThemeStylebox("panel").GetContentMargin(Side.Right) < PulpScreenKit.ButtonStackRightInset).IsTrue();
        } finally {
            host.Free();
        }
    }
}
