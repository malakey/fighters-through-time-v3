using System.Collections.Generic;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 A1. Contract for the single shared UI Theme.
///
/// The theme is referenced by path from several parallel worktrees and adopted by
/// every screen, so two things have to stay true: the entries screens rely on
/// must exist, and the authored colours must not drift from the
/// <see cref="UIPalette"/> constants that scripts use for the things a Theme
/// cannot express (ColorRect fills, runtime tints). A silent drift between the
/// two produces a UI that is half one palette and half another, which reads as
/// sloppiness rather than as a bug and so tends to survive review.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class UIThemeTests {

    private static Theme LoadTheme() {
        AssertThat(ResourceLoader.Exists(UIPalette.ThemePath))
            .OverrideFailureMessage($"Shared theme missing at {UIPalette.ThemePath}").IsTrue();
        var theme = ResourceLoader.Load<Theme>(UIPalette.ThemePath);
        AssertObject(theme).IsNotNull();
        return theme;
    }

    [TestCase]
    public void ThemeCarriesTheStyleEntriesScreensDependOn() {
        Theme theme = LoadTheme();
        var missing = new List<string>();

        void RequireStyle(string type, string name) {
            if (!theme.HasStylebox(name, type)) missing.Add($"{type}/styles/{name}");
        }

        RequireStyle("PanelContainer", "panel");
        RequireStyle("Panel", "panel");
        RequireStyle("PopupPanel", "panel");
        RequireStyle("PopupMenu", "panel");
        RequireStyle("Button", "normal");
        RequireStyle("Button", "hover");
        RequireStyle("Button", "pressed");
        RequireStyle("Button", "disabled");
        RequireStyle("HSlider", "slider");
        RequireStyle("HSlider", "grabber_area");
        RequireStyle("VSlider", "slider");
        RequireStyle("ProgressBar", "background");
        RequireStyle("ProgressBar", "fill");
        RequireStyle("TabContainer", "panel");
        RequireStyle("TabContainer", "tab_selected");
        RequireStyle("TabContainer", "tab_unselected");
        RequireStyle("LineEdit", "normal");

        if (missing.Count > 0) AssertThat(string.Join(" | ", missing)).IsEqual("");
    }

    [TestCase]
    public void EveryKeyboardNavigableTypeDrawsAFocusStyle() {
        Theme theme = LoadTheme();
        var missing = new List<string>();

        // Controller and keyboard players navigate entirely by the focus ring. A
        // focusable type without a focus stylebox is invisible to navigate.
        foreach (string type in new[] { "Button", "CheckBox", "OptionButton", "LineEdit" }) {
            if (!theme.HasStylebox("focus", type)) missing.Add($"{type}/styles/focus");
        }

        if (missing.Count > 0) AssertThat(string.Join(" | ", missing)).IsEqual("");
    }

    [TestCase]
    public void AuthoredColoursMatchTheUIPaletteConstants() {
        Theme theme = LoadTheme();
        var drift = new List<string>();

        void RequireColor(string type, string name, Color expected) {
            if (!theme.HasColor(name, type)) {
                drift.Add($"{type}/colors/{name} missing");
                return;
            }
            Color actual = theme.GetColor(name, type);
            if (!actual.IsEqualApprox(expected)) {
                drift.Add($"{type}/colors/{name} is {actual} but UIPalette says {expected}");
            }
        }

        RequireColor("Button", "font_color", UIPalette.TextPrimary);
        RequireColor("Button", "font_disabled_color", UIPalette.TextDisabled);
        RequireColor("Button", "font_focus_color", UIPalette.FocusOutline);
        RequireColor("Label", "font_color", UIPalette.TextPrimary);
        RequireColor("ProgressBar", "font_color", UIPalette.TextPrimary);
        RequireColor("TabContainer", "font_selected_color", UIPalette.TextAccent);

        // The painted surfaces, not just the type colours.
        var panel = theme.GetStylebox("panel", "PanelContainer") as StyleBoxFlat;
        AssertObject(panel).IsNotNull();
        if (!panel.BgColor.IsEqualApprox(UIPalette.PanelBackground)) {
            drift.Add($"PanelContainer bg is {panel.BgColor} but UIPalette says {UIPalette.PanelBackground}");
        }
        if (!panel.BorderColor.IsEqualApprox(UIPalette.PanelBorder)) {
            drift.Add($"PanelContainer border is {panel.BorderColor} but UIPalette says {UIPalette.PanelBorder}");
        }

        var focus = theme.GetStylebox("focus", "Button") as StyleBoxFlat;
        AssertObject(focus).IsNotNull();
        if (!focus.BorderColor.IsEqualApprox(UIPalette.FocusOutline)) {
            drift.Add($"Button focus ring is {focus.BorderColor} but UIPalette says {UIPalette.FocusOutline}");
        }

        if (drift.Count > 0) AssertThat(string.Join(" | ", drift)).IsEqual("");
    }

    /// <summary>
    /// V7.3: the four Label type variations are the theme half of UIPalette's
    /// type scale. Screens set ThemeTypeVariation instead of font-size
    /// overrides so UIPalette.ApplyUiScale (which rescales every per-type
    /// font-size entry) reaches them; a missing or drifted variation silently
    /// drops that label back to the body size.
    /// </summary>
    [TestCase]
    public void LabelTypeVariationsMatchTheUIPaletteTypeScale() {
        Theme theme = LoadTheme();
        var drift = new List<string>();

        void RequireVariation(string variation, int expectedSize) {
            if (!theme.HasFontSize("font_size", variation)) {
                drift.Add($"{variation}/font_sizes/font_size missing");
                return;
            }
            if (theme.GetTypeVariationBase(variation) != "Label") {
                drift.Add($"{variation} must be a Label variation");
            }
            int actual = theme.GetFontSize("font_size", variation);
            if (actual != expectedSize) {
                drift.Add($"{variation} is {actual} px but UIPalette says {expectedSize}");
            }
        }

        RequireVariation(UIPalette.SmallLabelVariation, UIPalette.SmallFontSize);
        RequireVariation(UIPalette.HeadingLabelVariation, UIPalette.HeadingFontSize);
        RequireVariation(UIPalette.EmphasisLabelVariation, UIPalette.EmphasisFontSize);
        RequireVariation(UIPalette.TitleLabelVariation, UIPalette.TitleFontSize);

        if (drift.Count > 0) AssertThat(string.Join(" | ", drift)).IsEqual("");
    }

    [TestCase]
    public void ThemeWorksWithoutAFontResource() {
        Theme theme = LoadTheme();

        // assets/fonts/ is empty. The theme must set the type scale and still
        // render against Godot's built-in default face, so that dropping a
        // production font in later is a one-line addition rather than a rescue.
        AssertObject(theme.DefaultFont)
            .OverrideFailureMessage(
                "The shared theme must not require a font resource while assets/fonts/ is empty.")
            .IsNull();
        AssertThat(theme.DefaultFontSize).IsEqual(UIPalette.BodyFontSize);
    }

    [TestCase]
    public void ApplyThemeAdoptsTheSharedThemeWithoutOverwritingALocalOne() {
        var themed = AutoFree(new Control());
        UIPalette.ApplyTheme(themed);
        AssertObject(themed.Theme).IsNotNull();

        var custom = new Theme();
        var overridden = AutoFree(new Control { Theme = custom });
        UIPalette.ApplyTheme(overridden);
        AssertObject(overridden.Theme)
            .OverrideFailureMessage("ApplyTheme must not clobber a deliberately assigned theme.")
            .IsSame(custom);
    }
}
