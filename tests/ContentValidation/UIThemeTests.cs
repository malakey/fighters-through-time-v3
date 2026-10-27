using System.Collections.Generic;
using System.Reflection;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 A1, re-pinned by TG-THEME for the Temporal Glass rebuild (UI_SPEC_GLASS). Contract for the single
/// shared UI Theme.
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

    /// <summary>The 3.1 slab presets: (top, sides, bottom) padding.</summary>
    private static readonly (string Name, int Top, int Sides, int Bottom)[] PanelPresets = {
        ("GlassPanelPause", 30, 20, 28), ("GlassPanelVitals", 20, 26, 20), ("GlassPanelAbilities", 22, 30, 14),
        ("GlassPanelTimeTools", 14, 26, 14), ("GlassPanelDust", 14, 24, 14), ("GlassPanelTimer", 12, 32, 12),
        ("GlassPanelStats", 20, 26, 18), ("GlassPanelContinue", 16, 20, 16),
    };

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
        RequireStyle("PopupMenu", "hover");
        RequireStyle("Button", "normal");
        RequireStyle("Button", "hover");
        RequireStyle("Button", "pressed");
        RequireStyle("Button", "disabled");
        RequireStyle("Button", "focus");
        RequireStyle("HSlider", "slider");
        RequireStyle("HSlider", "grabber_area");
        RequireStyle("HSlider", "grabber_area_highlight");
        RequireStyle("VSlider", "slider");
        RequireStyle("VScrollBar", "scroll");
        RequireStyle("VScrollBar", "grabber");
        RequireStyle("HScrollBar", "grabber");
        RequireStyle("ProgressBar", "background");
        RequireStyle("ProgressBar", "fill");
        RequireStyle("TabContainer", "panel");
        RequireStyle("TabContainer", "tab_selected");
        RequireStyle("TabContainer", "tab_unselected");
        RequireStyle("TabContainer", "tab_hovered");
        RequireStyle("TabContainer", "tab_focus");
        RequireStyle("LineEdit", "normal");
        RequireStyle("LineEdit", "focus");
        RequireStyle("TooltipPanel", "panel");
        RequireStyle("CheckBox", "focus");
        RequireStyle("HSeparator", "separator");
        // The chevrons' selected look (3.2): not a box Godot draws by itself; the button decor swaps it in.
        foreach (string chevron in new[] { UIPalette.Variations.PulpButton, UIPalette.Variations.TemporalGlassButton,
                     UIPalette.Variations.PulpButtonCompact }) {
            RequireStyle(chevron, "selected");
            RequireStyle(chevron, "hover_pressed");
        }
        RequireStyle(UIPalette.Variations.ReadyBadge, "panel");
        RequireStyle(UIPalette.Variations.ReadyBadgeIdle, "panel");
        foreach (var preset in PanelPresets) RequireStyle(preset.Name, "panel");

        // TG-CLOSE folds (plan 9's closeout list): the OptionButton arrow is the kit's 18 px chevron_down, tinted by the
        // field's font colour; the slider grabber is a 44 px canvas, so a slider stands 44 px tall (it was 64); a hovered
        // tab is the quiet lit tab with white text, not the selected gold with the selected ink.
        Texture2D arrow = theme.GetIcon("arrow", "OptionButton");
        if (arrow == null || arrow.ResourcePath != "res://assets/sprites/ui/glass/icons/chevron_down_18.png" || arrow.GetSize() != new Vector2(18f, 18f)) {
            missing.Add($"OptionButton/icons/arrow is {arrow?.ResourcePath ?? "missing"}, not the 18 px chevron_down");
        }
        if (theme.GetConstant("modulate_arrow", "OptionButton") != 1) missing.Add("OptionButton/constants/modulate_arrow must tint the arrow by the font colour");
        foreach (string slider in new[] { "HSlider", "VSlider" }) {
            Texture2D grabber = theme.GetIcon("grabber", slider);
            if (grabber == null || grabber.GetHeight() != 44 || grabber.GetWidth() != 44) {
                missing.Add($"{slider}/icons/grabber is {grabber?.GetSize().ToString() ?? "missing"}, not 44 x 44");
            }
        }
        if (theme.GetColor("font_hover_color", UIPalette.Variations.GlassTab) != UIPalette.TextFocus) missing.Add("GlassTab hovers in the selected ink, not white");
        if (theme.GetColor("font_hovered_color", "TabContainer") != UIPalette.TextFocus) missing.Add("TabContainer hovers in the selected ink, not white");
        if (theme.GetStylebox("tab_hovered", "TabContainer") is StyleBoxTexture hovered) {
            Color centre = CentrePixel(hovered);
            if (centre.A > 0.4f) missing.Add($"the hovered tab's centre is {centre}: as strong as the selected tab");
        } else {
            missing.Add("TabContainer/styles/tab_hovered is not the kit texture");
        }

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
        // TG-CLOSE: a slider and a focusable label (the select's Remote Play notice) wear the glass field's focus overlay
        // from the theme (Settings borrowed it per slider; a label showed Godot's default frame).
        foreach (string type in new[] { "HSlider", "VSlider", "Label" }) {
            if (theme.GetStylebox("focus", type) is not StyleBoxTexture overlay || overlay.Texture?.ResourcePath != "res://assets/sprites/ui/glass/glass_field_focus.png") {
                missing.Add($"{type}/styles/focus is not the glass field's focus overlay");
            }
        }

        if (missing.Count > 0) AssertThat(string.Join(" | ", missing)).IsEqual("");
    }

    private static Color CentrePixel(StyleBoxTexture box) {
        Image image = box.Texture.GetImage();
        return image.GetPixel(image.GetWidth() / 2, image.GetHeight() / 2);
    }

    /// <summary>What a box paints at its centre: a flat box's fill, or the centre texel of a nine-slice.</summary>
    private static Color Fill(StyleBox box) => box switch {
        StyleBoxFlat flat => flat.DrawCenter ? flat.BgColor : new Color(0f, 0f, 0f, 0f),
        StyleBoxTexture texture => CentrePixel(texture),
        _ => new Color(0f, 0f, 0f, 0f)
    };

    private static bool Near(Color a, Color b, float tolerance = 0.02f) =>
        Mathf.Abs(a.R - b.R) <= tolerance && Mathf.Abs(a.G - b.G) <= tolerance
        && Mathf.Abs(a.B - b.B) <= tolerance && Mathf.Abs(a.A - b.A) <= tolerance;

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

        // The glass field (the default Button): ink text, white when lit, ink3 when disabled.
        RequireColor("Button", "font_color", UIPalette.TextPrimary);
        RequireColor("Button", "font_disabled_color", UIPalette.TextDisabled);
        RequireColor("Button", "font_focus_color", UIPalette.TextFocus);
        RequireColor("Button", "font_hover_color", UIPalette.TextFocus);
        RequireColor("Button", "font_pressed_color", UIPalette.TextFocus);
        // The chevrons (T10, T11): ink2, white when focused, hovered or pressed, ink3 when disabled.
        foreach (string chevron in new[] { UIPalette.Variations.PulpButton, UIPalette.Variations.TemporalGlassButton,
                     UIPalette.Variations.PulpButtonCompact }) {
            RequireColor(chevron, "font_color", UIPalette.ButtonLabel);
            RequireColor(chevron, "font_focus_color", UIPalette.TextFocus);
            RequireColor(chevron, "font_hover_color", UIPalette.TextFocus);
            RequireColor(chevron, "font_pressed_color", UIPalette.TextFocus);
            RequireColor(chevron, "font_disabled_color", UIPalette.TextDisabled);
        }
        RequireColor("Label", "font_color", UIPalette.TextPrimary);
        RequireColor("ProgressBar", "font_color", UIPalette.TextPrimary);
        RequireColor("TabContainer", "font_selected_color", UIPalette.TextOnAccent);  // the selected tab is a gold fill

        // The painted surfaces, not just the type colours: what each box paints at its centre. A flat box paints its
        // fill; a glass nine-slice (TG-ART's kit) bakes its fill over its own shadow, edge and washes, so its centre
        // texel is compared with the constant composited the way the kit bakes it, within the kit's rounding.
        void RequireFill(string type, string box, Color expected, float tolerance = 0.02f) {
            Color actual = Fill(theme.GetStylebox(box, type));
            if (!Near(actual, expected, tolerance)) drift.Add($"{type}/styles/{box} paints {actual} but UIPalette says {expected}");
        }
        StyleBox slabBox = theme.GetStylebox("panel", "PanelContainer");
        if (slabBox is StyleBoxTexture) {
            // glass_panel bakes the fill at its centre over the slab's soft drop shadow.
            RequireFill("PanelContainer", "panel", OverAlpha(UIPalette.PanelBackground, UIPalette.PanelShadow), KitTolerance);
        } else {
            RequireFill("PanelContainer", "panel", UIPalette.PanelBackground);
        }
        foreach (string chevron in new[] { UIPalette.Variations.PulpButton, UIPalette.Variations.TemporalGlassButton,
                     UIPalette.Variations.PulpButtonCompact }) {
            StyleBox normal = theme.GetStylebox("normal", chevron), hover = theme.GetStylebox("hover", chevron);
            StyleBox pressed = theme.GetStylebox("pressed", chevron), focus = theme.GetStylebox("focus", chevron);
            StyleBox disabled = theme.GetStylebox("disabled", chevron);
            float tolerance = normal is StyleBoxTexture ? KitTolerance : 0.02f;
            RequireFill(chevron, "normal", UIPalette.ButtonNormal, tolerance);
            RequireFill(chevron, "hover", UIPalette.ButtonHover, tolerance);
            // Orchestrator ruling (step 2): a chevron's hover box is its normal box, because the decor takes focus on
            // mouse-enter, so hovered, focused and both draw the normal box under the focus overlay, identically.
            if (hover.GetInstanceId() != normal.GetInstanceId()) drift.Add($"{chevron}/styles/hover must be its normal box");
            if (pressed is StyleBoxTexture) {
                // TG-ART solved glass_btn_pressed's interior against the focus overlay (plan 0.4 P5): a pressed row is
                // (almost always) a focused one, and pressed under focus must compose into the pressed fill and read
                // brighter than a focused normal row, which a focus box with a fill of its own would hide.
                Color pressedFocused = OverAlpha(Fill(focus), Fill(pressed));
                Color normalFocused = OverAlpha(Fill(focus), Fill(normal));
                if (!Near(pressedFocused, UIPalette.ButtonPressed, 0.05f)) drift.Add($"{chevron} pressed under focus paints {pressedFocused} but UIPalette says {UIPalette.ButtonPressed}");
                Color black = new(0f, 0f, 0f);
                if (Luminance(Over(pressedFocused, black)) < Luminance(Over(normalFocused, black)) + 0.02) {
                    drift.Add($"{chevron} pressed under focus ({pressedFocused}) does not read brighter than normal under focus ({normalFocused})");
                }
            } else {
                RequireFill(chevron, "pressed", UIPalette.ButtonPressed);
            }
            if (disabled is StyleBoxTexture disabledTexture) {
                // TG-ART 6.2: the disabled fill over the averaged stripe haze rgba(glow, .15) (the CSS stripes run
                // under the fill's backdrop blur, which averages them); only the ring shows the dashes, which tile.
                RequireFill(chevron, "disabled", OverAlpha(UIPalette.ButtonDisabled, new Color(UIPalette.Accent, 0.15f)), 0.02f);
                if (disabledTexture.AxisStretchHorizontal != StyleBoxTexture.AxisStretchMode.Tile) drift.Add($"{chevron}/styles/disabled must tile its dashes");
                if (Fill(disabled).A >= Fill(normal).A) drift.Add($"{chevron}/styles/disabled must read fainter than its normal box");
            } else {
                RequireFill(chevron, "disabled", UIPalette.ButtonDisabled);
            }
        }
        RequireFill("Button", "normal", UIPalette.ButtonNormal, theme.GetStylebox("normal", "Button") is StyleBoxTexture ? KitTolerance : 0.02f);

        // The focus box is an overlay with no fill of its own (plan 0.4 P5), so a focused, pressed row still reads as
        // pressed; it carries the lit edge and the 14 px glow outside the shape.
        foreach (string type in new[] { "Button", UIPalette.Variations.PulpButton, UIPalette.Variations.TemporalGlassButton,
                     UIPalette.Variations.PulpButtonCompact, "LineEdit" }) {
            StyleBox focus = theme.GetStylebox("focus", type);
            if (Fill(focus).A > 0.5f) drift.Add($"{type}/styles/focus paints an opaque fill: it must be an overlay");
            if (focus is StyleBoxFlat flat) {
                if (!flat.BorderColor.IsEqualApprox(UIPalette.FocusOutline)) drift.Add($"{type} focus edge is {flat.BorderColor} but UIPalette says {UIPalette.FocusOutline}");
                if (flat.ShadowSize < 14 || !flat.ShadowColor.IsEqualApprox(UIPalette.FocusGlow)) drift.Add($"{type} focus glow is {flat.ShadowSize} px {flat.ShadowColor}");
            } else if (focus is StyleBoxTexture overlay) {
                // The glow runs 14 px outside the shape (the expand margins), so the texel at the expand margin is the
                // lit edge and the one just outside it is the glow.
                for (int side = 0; side < 4; side++) {
                    if (overlay.GetExpandMargin((Side)side) != 14) drift.Add($"{type} focus overlay expands {overlay.GetExpandMargin((Side)side)} px on side {side}, not the 14 px glow");
                }
                Image art = overlay.Texture.GetImage();
                int x = Mathf.Clamp((int)overlay.GetExpandMargin(Side.Left), 0, art.GetWidth() - 1), y = art.GetHeight() / 2;
                Color edge = art.GetPixel(x, y);
                if (!Near(edge, UIPalette.FocusOutline)) drift.Add($"{type} focus edge is {edge} but UIPalette says {UIPalette.FocusOutline}");
                Color glow = x > 0 ? art.GetPixel(x - 1, y) : new Color(0f, 0f, 0f, 0f);
                if (!Near(new Color(glow, 1f), new Color(UIPalette.FocusGlow, 1f)) || glow.A <= 0f || glow.A > UIPalette.FocusGlow.A) {
                    drift.Add($"{type} focus glow is {glow} but UIPalette says {UIPalette.FocusGlow}");
                }
            }
        }
        if (slabBox is StyleBoxFlat slab && !slab.BorderColor.IsEqualApprox(UIPalette.PanelBorder)) {
            drift.Add($"the slab's edge is {slab.BorderColor} but UIPalette says {UIPalette.PanelBorder}");
        } else if (slabBox is StyleBoxTexture slabTexture) {
            // The glass_panel hairline sits at the expand margin, anti-aliased against the shadow and the fill, so its
            // hue is compared (the green and blue channels over the red), not its strength.
            Image art = slabTexture.Texture.GetImage();
            Color edge = art.GetPixel(Mathf.Clamp((int)slabTexture.GetExpandMargin(Side.Left), 0, art.GetWidth() - 1),
                art.GetHeight() / 2);
            Color border = UIPalette.PanelBorder;
            if (edge.R <= 0.25f || Mathf.Abs(edge.G / edge.R - border.G / border.R) > 0.03f
                || Mathf.Abs(edge.B / edge.R - border.B / border.R) > 0.03f) {
                drift.Add($"the slab's edge is {edge}, not the hue of UIPalette.PanelBorder {border}");
            }
        }

        // The glass fields and tabs hover with their own full-look boxes (TG-ART's glass_field_hover, glass_tab_hover):
        // a hovered field is lit like a focused one and a hovered tab reads like the selected gold.
        foreach ((string type, string box, string stem) in new[] {
                     ("Button", "hover", "glass_field_hover"), ("LineEdit", "focus", "glass_field_focus"),
                     ("TabContainer", "tab_hovered", "glass_tab_hover"), (UIPalette.Variations.GlassTab, "hover", "glass_tab_hover") }) {
            StyleBox style = theme.GetStylebox(box, type);
            string path = (style as StyleBoxTexture)?.Texture?.ResourcePath ?? "";
            if (!path.EndsWith($"/{stem}.png")) drift.Add($"{type}/styles/{box} draws {path}, not {stem}");
        }

        if (drift.Count > 0) AssertThat(string.Join(" | ", drift)).IsEqual("");
    }

    /// <summary>The colour tolerance of a glass nine-slice's baked centre against its constant (the kit's rounding).</summary>
    private const float KitTolerance = 0.03f;

    /// <summary><paramref name="top"/> composited over a translucent <paramref name="under"/> (straight alpha, "over").</summary>
    private static Color OverAlpha(Color top, Color under) {
        float a = top.A + under.A * (1f - top.A);
        if (a <= 0f) return new Color(0f, 0f, 0f, 0f);
        return new Color((top.R * top.A + under.R * under.A * (1f - top.A)) / a,
            (top.G * top.A + under.G * under.A * (1f - top.A)) / a,
            (top.B * top.A + under.B * under.A * (1f - top.A)) / a, a);
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

        // Every UI_SPEC_GLASS 2.3 role the kit names (T01-T60): present, a Label
        // variation, at its authored size, never under the 18 px floor.
        foreach (var role in UIPalette.LabelRoleTable) {
            RequireVariation(role.Name, role.Size);
            if (role.Size < UIPalette.MinLabelFontSize) {
                drift.Add($"{role.Name} is {role.Size} px, under the {UIPalette.MinLabelFontSize} px floor");
            }
        }

        if (drift.Count > 0) AssertThat(string.Join(" | ", drift)).IsEqual("");
    }

    private const string OutfitPath = "res://assets/fonts/outfit/Outfit-Variable.ttf";
    private const string CinzelPath = "res://assets/fonts/cinzel/Cinzel-Variable.ttf";
    // OpenType 'wght' as the integer tag FontVariation.VariationOpentype is keyed by.
    private const long WghtTag = 2003265652;

    private static string BaseFontPath(Font font) =>
        (font as FontVariation)?.BaseFont?.ResourcePath ?? "";

    private static double Weight(Font font) {
        var axes = (font as FontVariation)?.VariationOpentype;
        return axes != null && axes.ContainsKey(WghtTag) ? (double)axes[WghtTag] : -1;
    }

    /// <summary>
    /// UR-THEME rewrite of ThemeWorksWithoutAFontResource: the fonts ship now.
    /// The default font is Outfit 400 in the G05 FontVariation slot, and the
    /// slot's fallbacks list stays empty: it is reserved for the CJK face and
    /// must never carry Cinzel or Outfit.
    /// </summary>
    [TestCase]
    public void TheShippedFontsAreTheDefaultAndTheFallbackSlotStaysEmpty() {
        Theme theme = LoadTheme();

        AssertThat(ResourceLoader.Exists(OutfitPath)).IsTrue();
        AssertThat(ResourceLoader.Exists(CinzelPath)).IsTrue();
        AssertThat(FileAccess.FileExists("res://assets/fonts/outfit/OFL.txt")).IsTrue();
        AssertThat(FileAccess.FileExists("res://assets/fonts/cinzel/OFL.txt")).IsTrue();

        var slot = theme.DefaultFont as FontVariation;
        AssertObject(slot)
            .OverrideFailureMessage("The shared theme must carry the G05 default-font slot (a FontVariation).")
            .IsNotNull();
        AssertObject(slot.BaseFont)
            .OverrideFailureMessage("The default font must be the shipped Outfit face.")
            .IsNotNull();
        AssertThat(BaseFontPath(slot)).IsEqual(OutfitPath);
        AssertThat(Weight(slot)).IsEqual(400.0);
        AssertThat(slot.Fallbacks.Count)
            .OverrideFailureMessage("The reserved fallback-font slot ships empty until a CJK face is bundled.")
            .IsEqual(0);
        AssertThat(theme.DefaultFontSize).IsEqual(UIPalette.BodyFontSize);
    }

    /// <summary>
    /// Fonts flow through variations, never per-node overrides: each Label role names its pinned face and weight,
    /// the face is the one the role's table row says, and its tracking is the CSS em value times the size, as
    /// FontVariation.spacing_glyph in whole pixels (2.5).
    /// </summary>
    [TestCase]
    public void EveryLabelRolePinsItsFaceAndWeight() {
        Theme theme = LoadTheme();
        var drift = new List<string>();
        foreach (var role in UIPalette.LabelRoleTable) {
            Font font = theme.HasFont("font", role.Name) ? theme.GetFont("font", role.Name) : null;
            if (font == null) { drift.Add($"{role.Name}/fonts/font missing"); continue; }
            string expectedPath = role.Face == "cinzel" ? CinzelPath : OutfitPath;
            if (BaseFontPath(font) != expectedPath) {
                drift.Add($"{role.Name} uses {BaseFontPath(font)} but the table says {role.Face}");
            }
            if (Weight(font) != role.Weight) {
                drift.Add($"{role.Name} is wght {Weight(font)} but the table says {role.Weight}");
            }
            int spacing = (font as FontVariation)?.SpacingGlyph ?? 0;
            int expectedSpacing = Mathf.RoundToInt(role.Tracking * role.Size);
            if (spacing != expectedSpacing) {
                drift.Add($"{role.Name} tracks {spacing} px but {role.Tracking} em at {role.Size} px is {expectedSpacing}");
            }
        }
        // TG-CLOSE: the roles Phase B lacked (plan 9's closeout list), at UI_SPEC_GLASS 9.1 / 9.2 / 3.4.1's values, so no
        // screen stands in with the nearest role and a colour override: name, face, weight, size, colour, halo.
        var folded = new (string Name, string Face, int Weight, int Size, Color Colour, string Halo)[] {
            ("DescriptionLabel", "outfit", 400, 24, UIPalette.Ink2, ""),       // the difficulty description
            ("BodyCompactLabel", "outfit", 400, 24, UIPalette.Ink, ""),        // 9.2 body at 24 (Systems Card, log, boot)
            ("BodySmallDimLabel", "outfit", 400, 22, UIPalette.Ink2, ""),      // secondary small copy (Move List stats)
            ("SlotCaption", "outfit", 600, 18, UIPalette.Accent, ""),          // the Move List slot name
            ("LogSpeaker", "cinzel", 700, 22, UIPalette.Accent, ""),           // the dialogue log's speaker
            ("CreditsHeading", "cinzel", 700, 30, UIPalette.Accent, ""),       // the credits' role headings
            ("HudNumeralMaxScene", "outfit", 400, 21, UIPalette.TextFocus, "S"), // the Fighter "/max" (20.9)
        };
        foreach (var f in folded) {
            if (!theme.HasFont("font", f.Name) || theme.GetTypeVariationBase(f.Name) != "Label") { drift.Add($"the folded role {f.Name} is missing"); continue; }
            Font font = theme.GetFont("font", f.Name);
            if (BaseFontPath(font) != (f.Face == "cinzel" ? CinzelPath : OutfitPath) || Weight(font) != f.Weight) drift.Add($"{f.Name} is not {f.Face} {f.Weight}");
            if (theme.GetFontSize("font_size", f.Name) != Mathf.RoundToInt(f.Size * UIPalette.AppliedUiScale)) drift.Add($"{f.Name} is {theme.GetFontSize("font_size", f.Name)} px, not {f.Size}");
            if (!theme.GetColor("font_color", f.Name).IsEqualApprox(f.Colour)) drift.Add($"{f.Name} is {theme.GetColor("font_color", f.Name)}, not {f.Colour}");
            bool halo = theme.HasConstant("outline_size", f.Name) && theme.GetConstant("outline_size", f.Name) > 0;
            if (halo != (f.Halo != "")) drift.Add($"{f.Name}'s halo is {(halo ? "on" : "off")}");
        }
        if (drift.Count > 0) AssertThat(string.Join(" | ", drift)).IsEqual("");
    }

    /// <summary>
    /// Every kit variation exists on its base type, and (TG-CLOSE) no retired variation, Label role or palette entry
    /// remains in the theme or the generator's palette; the five bar variations are value carriers that draw nothing.
    /// </summary>
    [TestCase]
    public void EveryKitVariationExistsOnItsBaseTypeAndNoRetiredNameRemains() {
        Theme theme = LoadTheme();
        var drift = new List<string>();
        void Require(string name, string baseType, string styleName) {
            if (theme.GetTypeVariationBase(name) != baseType) drift.Add($"{name} must be a {baseType} variation");
            if (!theme.HasStylebox(styleName, name)) drift.Add($"{name}/styles/{styleName} missing");
        }
        foreach (string name in new[] {
            UIPalette.Variations.PulpButton, UIPalette.Variations.PulpButtonCompact,
            UIPalette.Variations.TemporalGlassButton, UIPalette.Variations.GlassField,
            UIPalette.Variations.GlassTab, UIPalette.Variations.GlassTabSelected }) {
            Require(name, "Button", "normal");
            Require(name, "Button", "focus");
        }
        var panels = new List<string> {
            UIPalette.Variations.InkPanel, UIPalette.Variations.DialogueGlassPanel, UIPalette.Variations.PanelTagPlate,
            UIPalette.Variations.ScrimPanel, UIPalette.Variations.ReadyBadge, UIPalette.Variations.ReadyBadgeIdle };
        foreach (var preset in PanelPresets) panels.Add(preset.Name);
        foreach (string name in panels) Require(name, "PanelContainer", "panel");
        foreach (string name in new[] { UIPalette.Variations.PulpBar, UIPalette.Variations.PulpBarH44, UIPalette.Variations.PulpBarH16, UIPalette.Variations.PulpBarH14, UIPalette.Variations.PulpBossBar }) {
            Require(name, "ProgressBar", "background");
            Require(name, "ProgressBar", "fill");
            // FighterHUD.TintFill recolours the fill by reading it as a StyleBoxFlat, and the bar decor reads its colour.
            if (theme.GetStylebox("fill", name) is not StyleBoxFlat fill) drift.Add($"{name} fill must stay a StyleBoxFlat");
            // TG-CLOSE: a value carrier under the glass tube decor draws nothing of its own (plan 2.2).
            else if (fill.DrawCenter || fill.BorderWidthLeft + fill.BorderWidthTop + fill.BorderWidthRight + fill.BorderWidthBottom > 0
                     || fill.ShadowSize > 0) drift.Add($"{name} fill draws something; it must only hold its colour");
            if (theme.GetStylebox("background", name) is not StyleBoxEmpty) drift.Add($"{name} background must be empty");
        }
        if (theme.GetStylebox("fill", "ProgressBar") is not StyleBoxFlat) drift.Add("ProgressBar fill must stay a StyleBoxFlat");

        // TG-CLOSE: the retired pulp variations, roles and palette entries are gone from the theme, the generated
        // name tables and the generator's palette (the lists live in UiLegacyKitReferenceScanTests).
        // Theme.HasFontSize also answers true through the default font size, so a retired type is looked up in the type list.
        var themeTypes = new HashSet<string>(theme.GetTypeList());
        var variationNames = new HashSet<string>();
        foreach (FieldInfo field in typeof(UIPalette.Variations).GetFields()) variationNames.Add(field.Name);
        foreach (FieldInfo field in typeof(UIPalette.LabelRoles).GetFields()) variationNames.Add(field.Name);
        foreach (string name in FTT.Tests.ContentValidation.UiLegacyKitReferenceScanTests.RetiredVariations) {
            if (themeTypes.Contains(name)) drift.Add($"the retired variation {name} is still in the theme");
            if (variationNames.Contains(name)) drift.Add($"UIPalette.Variations still names the retired {name}");
        }
        foreach (string name in FTT.Tests.ContentValidation.UiLegacyKitReferenceScanTests.RetiredRoles) {
            if (themeTypes.Contains(name)) drift.Add($"the retired role {name} is still in the theme");
            if (variationNames.Contains(name)) drift.Add($"UIPalette.LabelRoles still names the retired {name}");
        }
        var paletteNames = new HashSet<string>();
        foreach (var (name, _) in UIPalette.GeneratorPalette) paletteNames.Add(name);
        foreach (string name in FTT.Tests.ContentValidation.UiLegacyKitReferenceScanTests.RetiredPaletteConstants) {
            if (paletteNames.Contains(name)) drift.Add($"the generator's palette still has the retired {name}");
            if (typeof(UIPalette).GetField(name, BindingFlags.Public | BindingFlags.Static) != null) drift.Add($"UIPalette.{name} is still defined");
        }
        if (drift.Count > 0) AssertThat(string.Join(" | ", drift)).IsEqual("");
    }

    /// <summary>
    /// TG-THEME rewrite of TheFoldedKitGapsCarryTheirSpecSizesAndPlates: every glass slab carries the padding of its
    /// 3.1 preset as its content margins (top, sides, bottom), the tag its 6.4 content margins, and the two text plates
    /// the pulp closeout folded in keep their contract (the dialogue line's RichTextLabel variation at 33 px; the
    /// credits hint on its own plate, now the glass tag).
    /// </summary>
    [TestCase]
    public void TheGlassPanelPresetsCarryTheirSpecPaddings() {
        Theme theme = LoadTheme();
        var drift = new List<string>();
        void RequirePadding(string type, string box, int top, int sides, int bottom) {
            StyleBox style = theme.GetStylebox(box, type);
            float l = style.GetContentMargin(Side.Left), t = style.GetContentMargin(Side.Top);
            float r = style.GetContentMargin(Side.Right), b = style.GetContentMargin(Side.Bottom);
            if (t != top || l != sides || r != sides || b != bottom) {
                drift.Add($"{type} pads {t} / {l}-{r} / {b} but 3.1 says {top} / {sides} / {bottom}");
            }
        }
        foreach (var preset in PanelPresets) RequirePadding(preset.Name, "panel", preset.Top, preset.Sides, preset.Bottom);
        RequirePadding("PanelContainer", "panel", 22, 28, 22);
        RequirePadding(UIPalette.Variations.InkPanel, "panel", 22, 28, 22);
        RequirePadding(UIPalette.Variations.DialogueGlassPanel, "panel", 22, 26, 22);
        StyleBox tag = theme.GetStylebox("panel", UIPalette.Variations.PanelTagPlate);
        if (tag.GetContentMargin(Side.Left) != 14 || tag.GetContentMargin(Side.Right) != 14
            || tag.GetContentMargin(Side.Top) != 3 || tag.GetContentMargin(Side.Bottom) != 3) {
            drift.Add("PanelTagPlate must carry the 6.4 content margins 14 / 3 / 14 / 3");
        }
        // TG-CLOSE folds: the empty (disabled) slab is a quiet flat slab at the Continue card's padding with the slab's
        // 18 px chamfer at top-left and bottom-right (its dashed edge is drawn by the slab frame); the compact footer
        // chevron (no prompt badge) pads its label 30 on the right, not 40 (4.2's footer).
        if (theme.GetTypeVariationBase(UIPalette.Variations.GlassPanelEmpty) != "PanelContainer"
            || theme.GetStylebox("panel", UIPalette.Variations.GlassPanelEmpty) is not StyleBoxFlat empty) {
            drift.Add("GlassPanelEmpty must be a flat PanelContainer slab");
        } else {
            if (!empty.BgColor.IsEqualApprox(UIPalette.ButtonFillDisabled)) drift.Add($"GlassPanelEmpty paints {empty.BgColor}");
            if (empty.CornerRadiusTopLeft != 18 || empty.CornerRadiusBottomRight != 18 || empty.CornerRadiusTopRight != 0
                || empty.CornerRadiusBottomLeft != 0 || empty.CornerDetail != 1) drift.Add("GlassPanelEmpty must carry the 18 px chamfer at TL and BR");
            RequirePadding(UIPalette.Variations.GlassPanelEmpty, "panel", 16, 20, 16);
        }
        if (theme.GetTypeVariationBase(UIPalette.Variations.PulpButtonCompactFooter) != "Button") {
            drift.Add("PulpButtonCompactFooter must be a Button variation");
        } else {
            StyleBox footer = theme.GetStylebox("normal", UIPalette.Variations.PulpButtonCompactFooter);
            StyleBox compact = theme.GetStylebox("normal", UIPalette.Variations.PulpButtonCompact);
            if (footer.GetContentMargin(Side.Right) != 30 || footer.GetContentMargin(Side.Left) != compact.GetContentMargin(Side.Left)) {
                drift.Add($"PulpButtonCompactFooter pads {footer.GetContentMargin(Side.Left)} / {footer.GetContentMargin(Side.Right)}, not 52 / 30");
            }
            if (theme.GetFont("font", UIPalette.Variations.PulpButtonCompactFooter) != theme.GetFont("font", UIPalette.Variations.PulpButtonCompact)) {
                drift.Add("PulpButtonCompactFooter must draw the compact chevron's font");
            }
        }
        // The tag plate stands 24 px tall (6.4) around its PanelTag label (the role's line box trimmed to 18).
        var tagPlate = new PanelContainer { Theme = theme, ThemeTypeVariation = UIPalette.Variations.PanelTagPlate };
        tagPlate.AddChild(new Label { Text = "STORY MODE", ThemeTypeVariation = UIPalette.LabelRoles.PanelTag });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(tagPlate);
        try {
            float height = tagPlate.GetCombinedMinimumSize().Y;
            if (Mathf.Abs(height - 24f * UIPalette.AppliedUiScale) > 1f) drift.Add($"the tag plate is {height} px tall, not 24");
        } finally {
            tagPlate.Free();
        }
        if (theme.GetTypeVariationBase(UIPalette.Variations.DialogueBodyRich) != "RichTextLabel") drift.Add("DialogueBodyRich must be a RichTextLabel variation");
        if (theme.GetFontSize("normal_font_size", UIPalette.Variations.DialogueBodyRich) != 33) drift.Add("DialogueBodyRich must be 33 px");
        if (!theme.HasStylebox("normal", UIPalette.LabelRoles.HintLinePlate)
            || theme.GetStylebox("normal", UIPalette.LabelRoles.HintLinePlate).GetContentMargin(Side.Left) != 14) {
            drift.Add("HintLinePlate must carry its own plate, the glass tag");
        }
        if (drift.Count > 0) AssertThat(string.Join(" | ", drift)).IsEqual("");
    }

    /// <summary>
    /// Player ownership colours are read from the combat slot palette, never copied.
    /// Cyan is player one's and the Severed's, no longer the chrome accent.
    /// </summary>
    [TestCase]
    public void OwnershipColoursReadTheCombatSlotPaletteAndChromeIsGold() {
        // Select the default palette explicitly: the developer's own loaded settings may pick Blue / Orange or High Contrast.
        FTT.Core.GlobalSaveData data = FTT.Core.SaveManager.Instance?.GlobalData;
        if (data == null) AssertThat("GlobalData is missing").IsEqual("");
        int savedPalette = data.PlayerSlotPalette;
        try {
            data.PlayerSlotPalette = 0;
            AssertThat(UIPalette.P1.IsEqualApprox(FTT.Combat.GlowPalette.SlotColor(0))).IsTrue();
            AssertThat(UIPalette.P2.IsEqualApprox(FTT.Combat.GlowPalette.SlotColor(1))).IsTrue();
        } finally {
            data.PlayerSlotPalette = savedPalette;
        }
        AssertThat(UIPalette.Accent.IsEqualApprox(UIPalette.Cyan)).IsFalse();
        AssertThat(UIPalette.TextAccent.IsEqualApprox(UIPalette.Accent)).IsTrue();
        // The grammar is untouched by the redesign.
        AssertThat(UIPalette.Gold.IsEqualApprox(UIPalette.ResonanceGold)).IsTrue();
        AssertThat(UIPalette.GoldBright.IsEqualApprox(UIPalette.ResonanceGoldBright)).IsTrue();
        AssertThat(UIPalette.Cyan.IsEqualApprox(new Color(0f, 0.9f, 0.9f))).IsTrue();
        AssertThat(UIPalette.UnboundCold.IsEqualApprox(new Color(0.2f, 0.95f, 1f))).IsTrue();
    }

    private static double Luminance(Color c) {
        static double Lin(float v) => v <= 0.03928f ? v / 12.92 : System.Math.Pow((v + 0.055) / 1.055, 2.4);
        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }

    private static double Contrast(Color fg, Color bg) {
        double a = Luminance(fg), b = Luminance(bg);
        return (System.Math.Max(a, b) + 0.05) / (System.Math.Min(a, b) + 0.05);
    }

    /// <summary><paramref name="top"/> composited over an opaque <paramref name="under"/>, in sRGB space as the 2D canvas blends.</summary>
    private static Color Over(Color top, Color under) =>
        new(top.R * top.A + under.R * (1f - top.A), top.G * top.A + under.G * (1f - top.A),
            top.B * top.A + under.B * (1f - top.A), 1f);

    /// <summary>
    /// UI_SPEC_GLASS 1.5: the build fills hold text at 4.5:1 (3:1 for large text and non-text) with pure white behind
    /// the glass, which bounds every scene art; text over art, a fill or lens art holds it through the scene halo.
    /// Every fill is composited over white first, so a lighter fill (the mock's .80 without its blur) fails here.
    /// </summary>
    [TestCase]
    public void TheThemesTextPairsClearTheContrastFloor() {
        Color white = new(1f, 1f, 1f);
        Color glow = UIPalette.Accent;
        Color top = Over(UIPalette.GlassFillTop, white);
        Color topSheen = Over(new Color(1f, 1f, 1f, 0.05f), top);
        Color streak = Over(new Color(1f, 1f, 1f, 0.075f), top);
        Color bottom = Over(UIPalette.GlassFillBottom, white);
        Color flatSlab = Over(UIPalette.GlassFillFlat, white);
        Color button = Over(UIPalette.ButtonFill, white);
        Color focusBase = Over(UIPalette.ButtonFillFocus, white);
        Color focusWash = Over(new Color(glow, 0.34f), focusBase);
        Color pressedStrongest = Over(new Color(glow, 0.50f), focusBase);
        Color pressedFlat = Over(UIPalette.ButtonFillPressed, white);
        Color tile = Over(UIPalette.TileFillTop, white);
        Color haloRim = Over(UIPalette.HaloOutlineS, white);

        var failing = new List<string>();
        void Pair(string name, Color fg, Color bg, double floor) {
            double ratio = Contrast(fg, bg);
            if (ratio < floor) failing.Add($"{name} {ratio:0.00}:1 under {floor}");
        }
        Pair("Ink on the panel (sheen)", UIPalette.Ink, topSheen, 4.5);
        Pair("Ink2 on the panel (sheen)", UIPalette.Ink2, topSheen, 4.5);
        Pair("Accent on the panel (sheen)", UIPalette.Accent, topSheen, 4.5);
        Pair("Res2 on the panel (sheen)", UIPalette.Res2, topSheen, 4.5);
        Pair("Ink2 across a panel streak", UIPalette.Ink2, streak, 4.5);
        Pair("Ink on the panel's bottom stop", UIPalette.Ink, bottom, 4.5);
        Pair("Ink2 on the panel's bottom stop", UIPalette.Ink2, bottom, 4.5);
        Pair("Ink2 on a flat slab", UIPalette.Ink2, flatSlab, 4.5);
        Pair("Ink2 (normal label) on a chevron", UIPalette.Ink2, button, 4.5);
        Pair("White (focused label) on the focus wash", UIPalette.TextFocus, focusWash, 4.5);
        Pair("White (pressed label) on the flat pressed fill", UIPalette.TextFocus, pressedFlat, 4.5);
        Pair("White (34 px pressed label) on the strongest pressed wash", UIPalette.TextFocus, pressedStrongest, 3.0);
        Pair("Ink2 (tile name) on a select tile", UIPalette.Ink2, tile, 4.5);
        Pair("InkInv on Res", UIPalette.InkInv, UIPalette.Res, 4.5);
        Pair("TabSelectedInk on Accent", UIPalette.TabSelectedInk, UIPalette.Accent, 4.5);
        Pair("Ink on the scene halo", UIPalette.Ink, haloRim, 4.5);
        Pair("Ink2 on the scene halo", UIPalette.Ink2, haloRim, 4.5);
        Pair("Res2 on the scene halo", UIPalette.Res2, haloRim, 4.5);
        Pair("White on the scene halo", UIPalette.TextFocus, haloRim, 4.5);
        Pair("Ink3 (disabled, non-text) on the panel", UIPalette.Ink3, topSheen, 3.0);
        Pair("Danger (large) on the panel", UIPalette.Danger, topSheen, 3.0);
        if (failing.Count > 0) AssertThat(string.Join(" | ", failing)).IsEqual("");
    }

    /// <summary>
    /// UI_SPEC_GLASS 2.4: every text drawn over art, the Fighter top shade, the pause dim, a bar fill or lens art carries
    /// the scene halo, a 2 px (S) or 3 px (L) black rim with a soft shadow, and text on glass carries none. The lists are
    /// the plan's (section 2.2), held here independently of the generator, so a role that loses or gains a halo fails.
    /// </summary>
    [TestCase]
    public void EverySceneTextRoleCarriesItsHaloTier() {
        var haloL = new HashSet<string> { "CoverTitle", "BannerHeading", "PauseTitle", "BossName", "SideCardName" };
        var haloS = new HashSet<string> {
            "LevelTitle", "ObjectiveLine", "HeroNameScene", "PromptCaption", "SceneCaption", "VersionCaption",
            "HudNumeral", "HudNumeralSmall", "HudNumeralMax", "HudNumeralMaxScene", "SlotKeyTagScene", "SlotNumber", "StatusName", "StatusTime",
            "IntegrityPercent", "IntegrityCaption", "BossPhaseTag", "BossHpValue", "NoteLabel", "HintLine",
            "SelectCountdown", "UltimateReadyLabel" };
        Theme theme = LoadTheme();
        var drift = new List<string>();
        foreach (var role in UIPalette.LabelRoleTable) {
            string name = role.Name;
            int outline = theme.HasConstant("outline_size", name) ? theme.GetConstant("outline_size", name) : 0;
            int shadowOutline = theme.HasConstant("shadow_outline_size", name) ? theme.GetConstant("shadow_outline_size", name) : 0;
            int shadowY = theme.HasConstant("shadow_offset_y", name) ? theme.GetConstant("shadow_offset_y", name) : 0;
            int shadowX = theme.HasConstant("shadow_offset_x", name) ? theme.GetConstant("shadow_offset_x", name) : 0;
            Color rim = theme.HasColor("font_outline_color", name) ? theme.GetColor("font_outline_color", name) : new Color(0, 0, 0, 0);
            Color shade = theme.HasColor("font_shadow_color", name) ? theme.GetColor("font_shadow_color", name) : new Color(0, 0, 0, 0);
            if (haloL.Contains(name)) {
                if (outline != 3 || !rim.IsEqualApprox(new Color(0f, 0f, 0f, 0.70f)) || shadowX != 0 || shadowY != 3
                    || shadowOutline != 8 || !shade.IsEqualApprox(new Color(0f, 0f, 0f, 0.50f))) {
                    drift.Add($"{name} needs halo L but has rim {outline} {rim}, shadow ({shadowX}, {shadowY}) {shadowOutline} {shade}");
                }
            } else if (haloS.Contains(name)) {
                int wantedShadowOutline = name == "IntegrityCaption" ? 6 : 4;   // T29: halo S with a 6 px shadow outline
                if (outline != 2 || !rim.IsEqualApprox(new Color(0f, 0f, 0f, 0.85f)) || shadowX != 0 || shadowY != 1
                    || shadowOutline != wantedShadowOutline || !shade.IsEqualApprox(new Color(0f, 0f, 0f, 0.60f))) {
                    drift.Add($"{name} needs halo S but has rim {outline} {rim}, shadow ({shadowX}, {shadowY}) {shadowOutline} {shade}");
                }
            } else if (outline != 0 || shade.A > 0f || shadowOutline != 0) {
                drift.Add($"{name} is plate text but carries a halo (rim {outline}, shadow {shadowOutline} {shade})");
            }
        }
        foreach (string name in haloL) if (!theme.IsTypeVariation(name, "Label")) drift.Add($"{name} is not a Label role");
        foreach (string name in haloS) if (!theme.IsTypeVariation(name, "Label")) drift.Add($"{name} is not a Label role");
        if (drift.Count > 0) AssertThat(string.Join(" | ", drift)).IsEqual("");
    }

    /// <summary>
    /// The glass constants carry the spec's values (UI_SPEC_GLASS 1.1, 1.3, 1.4, 1.6), and every colour the generator
    /// paints (its palette table, written into UIPalette.Roles.cs) has a UIPalette constant of the same name and value:
    /// the generator and the palette cannot drift apart.
    /// </summary>
    [TestCase]
    public void TheGlassPaletteConstantsMatchTheSpecAndTheGenerator() {
        var drift = new List<string>();

        static Color? Constant(string name) {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            FieldInfo field = typeof(UIPalette).GetField(name, flags);
            if (field?.FieldType == typeof(Color)) return (Color)field.GetValue(null);
            PropertyInfo property = typeof(UIPalette).GetProperty(name, flags);
            if (property?.PropertyType == typeof(Color)) return (Color)property.GetValue(null);
            return null;
        }

        foreach (var (name, value) in UIPalette.GeneratorPalette) {
            Color? constant = Constant(name);
            if (constant == null) drift.Add($"the generator paints '{name}' but UIPalette has no such colour");
            else if (!constant.Value.IsEqualApprox(value)) drift.Add($"{name}: the generator says {value}, UIPalette {constant.Value}");
        }
        if (UIPalette.GeneratorPalette.Length < 60) drift.Add($"the generator's palette table has only {UIPalette.GeneratorPalette.Length} entries");

        // The spec's values: (name, r, g, b, alpha), bytes as the spec writes them.
        var spec = new (string Name, int R, int G, int B, float A)[] {
            ("Backdrop", 0x05, 0x07, 0x1A, 1f), ("Ink", 0xF3, 0xF0, 0xE6, 1f), ("Ink2", 0xC0, 0xBA, 0xA6, 1f),
            ("Ink3", 0x8A, 0x83, 0x74, 1f), ("InkInv", 0x1A, 0x12, 0x04, 1f), ("Accent", 0xF3, 0xC9, 0x5B, 1f),
            ("Accent2", 0xFF, 0xE3, 0x9A, 1f), ("Focus", 0xFF, 0xD4, 0x5C, 1f), ("Edge", 243, 201, 91, 0.55f),
            ("EdgeHi", 0xFF, 0xD4, 0x5C, 1f), ("EdgeDim", 243, 201, 91, 0.25f), ("Res", 0xF3, 0xC9, 0x5B, 1f),
            ("Res2", 0xFF, 0xF0, 0xB0, 1f), ("Hp", 0x8C, 0xF0, 0xB8, 1f), ("Hp2", 0x4F, 0xCF, 0x8A, 1f),
            ("HpLag", 0xFF, 0x5A, 0x5A, 1f), ("Echo", 0xFF, 0xF7, 0xDA, 1f), ("Meter", 0xF3, 0xC9, 0x5B, 1f),
            ("Meter2", 0xFF, 0xB3, 0x47, 1f), ("Shield", 0xDF, 0xE8, 0xFF, 1f), ("Danger", 0xFF, 0x5A, 0x5A, 1f),
            ("Boss", 0xFF, 0x5A, 0x5A, 1f), ("Boss2", 0xA8, 0x2A, 0x2A, 1f), ("PanelShadow", 0x00, 0x02, 0x0C, 0.60f),
            ("StatusDamage", 0xFF, 0x7A, 0x5C, 1f), ("StatusControl", 0x8F, 0xC7, 0xFF, 1f),
            ("GlassFillTop", 15, 24, 52, 0.92f), ("GlassFillBottom", 6, 10, 26, 0.95f), ("GlassFillFlat", 15, 24, 52, 0.93f),
            ("ButtonFill", 8, 13, 32, 0.90f), ("ButtonFillFocus", 8, 16, 38, 0.92f), ("ButtonFillDisabled", 8, 13, 32, 0.50f),
            ("TileFillTop", 26, 38, 74, 0.90f), ("TileFillBottom", 6, 10, 26, 0.94f), ("LensFill", 6, 10, 26, 0.92f),
            ("BadgeFill", 8, 12, 30, 0.86f), ("DialFill", 5, 9, 24, 0.90f), ("PortraitFill", 6, 10, 26, 0.95f),
            ("TubeTrackTop", 2, 5, 14, 0.95f), ("TubeTrackBottom", 10, 16, 36, 0.82f),
            ("BossTrackTop", 0x05, 0x06, 0x0F, 1f), ("BossTrackBottom", 0x12, 0x0A, 0x18, 1f),
            ("SweepShade", 2, 5, 14, 0.76f), ("WedgeRemainder", 2, 5, 14, 0.55f), ("ToggleTrackFill", 6, 10, 26, 0.90f),
            ("SliderTrack", 255, 255, 255, 0.12f), ("ScrollTrack", 255, 255, 255, 0.06f), ("TickLine", 255, 255, 255, 0.13f),
            ("RingTrack", 243, 201, 91, 0.16f), ("FocusGlow", 243, 201, 91, 0.55f),
            ("ReadyBadgeFill", 243, 201, 91, 0.14f), ("ReadyBadgeGlow", 243, 201, 91, 0.30f),
            ("PipGlow", 143, 216, 255, 0.60f), ("DefyReadyGlow", 255, 232, 122, 0.75f), ("BossEdge", 255, 77, 98, 1f),
            ("BossGlow", 255, 77, 98, 0.30f), ("BossTracker", 255, 255, 255, 0.55f), ("TabSelectedInk", 0x04, 0x14, 0x1A, 1f),
            ("PauseDim", 3, 5, 14, 0.70f), ("GameOverDim", 3, 5, 14, 0.82f), ("ScrimMenu", 4, 7, 20, 0.96f),
            ("SelectVignette", 3, 5, 16, 0.55f), ("TopShade", 6, 4, 12, 0.80f), ("TitleTop", 0xFF, 0xFF, 0xFF, 1f),
            ("TitleMid", 0xDF, 0xE8, 0xFF, 1f), ("TitleBottom", 0x8D, 0x9B, 0xC6, 1f),
            ("HaloOutlineS", 0, 0, 0, 0.85f), ("HaloShadowS", 0, 0, 0, 0.60f),
            ("HaloOutlineL", 0, 0, 0, 0.70f), ("HaloShadowL", 0, 0, 0, 0.50f),
        };
        foreach (var (name, r, g, b, a) in spec) {
            Color? constant = Constant(name);
            Color expected = new(r / 255f, g / 255f, b / 255f, a);
            if (constant == null) drift.Add($"UIPalette.{name} is missing");
            else if (!constant.Value.IsEqualApprox(expected)) drift.Add($"UIPalette.{name} is {constant.Value} but the spec says {expected}");
        }
        // The cold light stays the grammar's single cold literal (1.1 cold = UnboundCold).
        if (!UIPalette.UnboundCold.IsEqualApprox(new Color(0.2f, 0.95f, 1f))) drift.Add("UnboundCold moved");
        if (drift.Count > 0) AssertThat(string.Join(" | ", drift)).IsEqual("");
    }

    /// <summary>
    /// Cinzel and Outfit ship Latin and Latin-extended only. Every character
    /// the English translation uses must exist in both faces, so no shipped
    /// string silently falls back to the engine face. The allow-list is empty
    /// on purpose: add a character only with a written justification.
    /// </summary>
    [TestCase]
    public void BothShippedFontsCoverEveryCharacterTheEnglishStringsUse() {
        var outfit = ResourceLoader.Load<FontFile>(OutfitPath);
        var cinzel = ResourceLoader.Load<FontFile>(CinzelPath);
        AssertObject(outfit).IsNotNull();
        AssertObject(cinzel).IsNotNull();

        using var file = FileAccess.Open("res://localization/en.csv", FileAccess.ModeFlags.Read);
        AssertObject(file).IsNotNull();
        // Comment lines (a leading #) are never rendered.
        var kept = new List<string>();
        foreach (string row in file.GetAsText().Split('\n')) if (!row.StartsWith("#")) kept.Add(row);
        string text = string.Join("\n", kept);

        var allowed = new HashSet<int>();   // justified exceptions: none today
        var seen = new SortedSet<int>();
        foreach (System.Text.Rune rune in text.EnumerateRunes()) {
            if (rune.Value < 0x20 || rune.Value == 0xFEFF) continue;   // control characters and the BOM
            seen.Add(rune.Value);
        }
        var missing = new List<string>();
        foreach (int cp in seen) {
            if (allowed.Contains(cp)) continue;
            if (!outfit.HasChar(cp)) missing.Add($"U+{cp:X4} '{char.ConvertFromUtf32(cp)}' not in Outfit");
            if (!cinzel.HasChar(cp)) missing.Add($"U+{cp:X4} '{char.ConvertFromUtf32(cp)}' not in Cinzel");
        }
        if (missing.Count > 0) AssertThat(string.Join(" | ", missing)).IsEqual("");
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
