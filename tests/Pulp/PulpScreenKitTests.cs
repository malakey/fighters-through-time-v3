using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>The shared screen helpers (TG-WIDGETS restyled UR-SCREENS'): a pause row styled outside the tree, a tag that leaves the panel path alone, the sheen, the leading icon, the button inset.</summary>
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
    public void APauseRowStyledBeforeItsScreenIsInTheTreeDrawsTheThemesGlassBoxes() {
        // As the Fighter pause builds its rows: styled first, added to the tree after.
        var row = new Button { Name = "Row", Text = "menu_resume" };
        PulpScreenKit.StylePauseButton(row, 760f);
        var (host, panel) = Build();
        try {
            panel.AddChild(row);
            AssertThat(row.CustomMinimumSize.X).IsEqual(760f);
            // The glass theme already pads 58, so no box was copied: the row draws the theme's own glass boxes.
            foreach (string box in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" }) {
                if (row.HasThemeStyleboxOverride(box)) AssertThat($"the pause row froze a copy of its {box} box").IsEqual("");
            }
            StyleBox normal = row.GetThemeStylebox("normal");
            AssertThat(ReferenceEquals(normal, UIPalette.LoadTheme().GetStylebox("normal", UIPalette.Variations.TemporalGlassButton))).IsTrue();
            AssertThat(normal.GetContentMargin(Side.Left)).IsEqual(PulpScreenKit.RowLabelInset);
        } finally {
            host.Free();
        }
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
    public void DressingAPanelPutsTheSheenBehindItsContentOnce() {
        var (host, panel) = Build();
        try {
            PulpScreenKit.DressPanel(panel, "common_back");
            PulpScreenKit.DressPanel(panel, "common_back");
            // UI_SPEC_GLASS 3.1: the sheen and streaks draw over the slab and under the content, once.
            AssertThat(panel.GetChild(0) is GlassSheen).IsTrue();
            int sheens = 0;
            for (int i = 0; i < panel.GetChildCount(); i++) if (panel.GetChild(i) is GlassSheen) sheens++;
            AssertThat(sheens).IsEqual(1);
            AssertThat(((GlassSheen)panel.GetChild(0)).MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            AssertThat(panel.GetNodeOrNull("HalftoneCorner")).IsNull();
            // Without the sheen asked for, nothing is added.
            var (plainHost, plain) = Build();
            try {
                PulpScreenKit.DressPanel(plain, null, sheen: false);
                AssertThat(plain.GetNodeOrNull(GlassSheen.NodeName)).IsNull();
            } finally {
                plainHost.Free();
            }
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
    public void AButtonStackPanelAddsTheFourteenPixelInsetToItsOwnPaddingOnce() {
        var (host, panel) = Build();
        try {
            float own = panel.GetThemeStylebox("panel").GetContentMargin(Side.Right);
            PulpScreenKit.InsetRight(panel);
            PulpScreenKit.InsetRight(panel);
            // 3.2 D13: the stack's right inset is the focus slide (14 px) on top of the panel's padding (pause: 20 + 14).
            AssertThat(PulpScreenKit.ButtonStackRightInset).IsEqual(FTT.UI.Motion.PulpMotionRules.FocusTranslateX);
            AssertThat(panel.GetThemeStylebox("panel").GetContentMargin(Side.Right)).IsEqual(own + PulpScreenKit.ButtonStackRightInset);
            // The shared theme plate is untouched.
            var other = new PanelContainer { ThemeTypeVariation = UIPalette.Variations.InkPanel };
            host.AddChild(other);
            AssertThat(other.GetThemeStylebox("panel").GetContentMargin(Side.Right)).IsEqual(own);
        } finally {
            host.Free();
        }
    }
}
