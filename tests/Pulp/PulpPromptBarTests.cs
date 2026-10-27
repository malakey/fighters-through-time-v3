using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>The prompt bar (TG-WIDGETS restyled UR-WIDGETS'): the glass pad ring and key-cap, raw-key captions and the device switch.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpPromptBarTests {

    private static PulpPromptBar BuildBar() {
        var bar = new PulpPromptBar();
        bar.AddPrompt("ui_accept", "ui_prompt_select");
        bar.AddPrompt("ui_cancel", "common_back");
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(bar);
        return bar;
    }

    private static bool HasLetter(Control glyph, string letter) {
        foreach (Node child in glyph.GetChildren()) if (child is Label l && l.Text == letter) return true;
        return false;
    }

    [TestCase]
    public void ThePadBarShowsCirclesWithAAndBAndKeepsTheRawCaptionKeys() {
        PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Pad;
        PulpPromptBar bar = BuildBar();
        try {
            AssertThat(bar.PairCount).IsEqual(2);
            AssertThat(bar.Device).IsEqual(PromptDevice.Pad);
            AssertThat(bar.GlyphAt(0).CustomMinimumSize).IsEqual(new Vector2(34f, 34f));
            AssertThat(HasLetter(bar.GlyphAt(0), "A")).IsTrue();
            AssertThat(HasLetter(bar.GlyphAt(1), "B")).IsTrue();
            // 3.4.4: a 2 px ring in ink around the letter (in ink, with the halo S over the scene).
            var ring = bar.GlyphAt(0).GetNodeOrNull<GlassPromptRing>("Circle");
            AssertObject(ring).IsNotNull();
            AssertThat(ring.RingColor).IsEqual(FTT.UI.UIPalette.Ink);
            var letter = bar.GlyphAt(0).GetNode<Label>("Letter");
            AssertThat(letter.GetThemeColor("font_color")).IsEqual(FTT.UI.UIPalette.Ink);
            AssertThat(letter.GetThemeConstant("outline_size")).IsEqual(FTT.UI.UIPalette.HaloSOutline);
            AssertThat(bar.CaptionAt(0).Text).IsEqual("ui_prompt_select");
            AssertThat(bar.CaptionAt(1).Text).IsEqual("common_back");
            AssertThat(bar.CaptionAt(0).ThemeTypeVariation.ToString()).IsEqual("PromptCaption");
            AssertThat(bar.GetThemeConstant("separation")).IsEqual(30);
        } finally {
            PulpPromptGlyphs.DeviceOverrideForTesting = null;
            bar.Free();
        }
    }

    [TestCase]
    public void TheKeyboardBarShowsKeyCapsLabelledWithTheBoundKeysAndSwitchesLive() {
        PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Keyboard;
        PulpPromptBar bar = BuildBar();
        try {
            string key = PulpPromptGlyphs.KeyLabelFor("ui_accept");
            AssertThat(key).IsEqual("ENTER");
            AssertThat(bar.GlyphAt(0) is PanelContainer).IsTrue();
            AssertThat(HasLetter(bar.GlyphAt(0), key)).IsTrue();
            // 3.4.4: a key-cap of the glyph's height, corner radius 6, a 2 px ink border, at least 34 wide, padding 9.
            var cap = (PanelContainer)bar.GlyphAt(0);
            var plate = cap.GetThemeStylebox("panel") as StyleBoxFlat;
            AssertObject(plate).IsNotNull();
            AssertThat(plate.DrawCenter).IsFalse();
            AssertThat(plate.BorderWidthLeft).IsEqual(2);
            AssertThat(plate.CornerRadiusTopLeft).IsEqual(6);
            AssertThat(plate.BorderColor).IsEqual(FTT.UI.UIPalette.Ink);
            AssertThat(plate.ContentMarginLeft).IsEqual(9f);
            AssertThat(cap.CustomMinimumSize).IsEqual(new Vector2(34f, 34f));

            PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Pad;
            bar.Refresh();
            AssertThat(bar.Device).IsEqual(PromptDevice.Pad);
            AssertThat(bar.GlyphAt(0) is PanelContainer).IsFalse();
            AssertThat(bar.PairCount).IsEqual(2);
        } finally {
            PulpPromptGlyphs.DeviceOverrideForTesting = null;
            bar.Free();
        }
    }

    [TestCase]
    public void PadButtonsMapToLettersAndTheGenericLabel() {
        AssertThat(PulpPromptGlyphs.PadButtonLabel(0)).IsEqual("A");
        AssertThat(PulpPromptGlyphs.PadButtonLabel(1)).IsEqual("B");
        AssertThat(PulpPromptGlyphs.PadButtonLabel(2)).IsEqual("X");
        AssertThat(PulpPromptGlyphs.PadButtonLabel(3)).IsEqual("Y");
        AssertThat(PulpPromptGlyphs.PadButtonLabel(9)).IsEqual("LB");
        AssertThat(PulpPromptGlyphs.PadButtonLabel(10)).IsEqual("RB");
        AssertThat(PulpPromptGlyphs.PadButtonLabel(7).Contains("7")).IsTrue();
    }
}
