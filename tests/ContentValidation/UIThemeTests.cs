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

    private static Color CentrePixel(StyleBoxTexture box) {
        Image image = box.Texture.GetImage();
        return image.GetPixel(image.GetWidth() / 2, image.GetHeight() / 2);
    }

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

        RequireColor("Button", "font_color", UIPalette.TextPrimary);
        RequireColor("Button", "font_disabled_color", UIPalette.TextDisabled);
        RequireColor("Button", "font_focus_color", UIPalette.TextOnAccent);
        RequireColor("Button", "font_hover_color", UIPalette.TextOnAccent);
        RequireColor("Button", "font_pressed_color", UIPalette.TextOnAccent);
        RequireColor("Label", "font_color", UIPalette.TextPrimary);
        RequireColor("ProgressBar", "font_color", UIPalette.TextPrimary);
        RequireColor("TabContainer", "font_selected_color", UIPalette.TextOnAccent);  // the selected tab is an accent plate

        // The painted surfaces, not just the type colours.
        // Since step 2 the plates are the UR-ART kit's StyleBoxTextures, so the painted surfaces are
        // checked by sampling the centre pixel of each plate's texture against the palette token
        // (the same drift this test always guarded: the art and UIPalette must say the same colour).
        var panel = theme.GetStylebox("panel", "PanelContainer") as StyleBoxTexture;
        AssertObject(panel).OverrideFailureMessage("PanelContainer/panel must be the kit's ink panel texture").IsNotNull();
        Color panelCentre = CentrePixel(panel);
        if (!Near(panelCentre, UIPalette.PanelBackground)) {
            drift.Add($"PanelContainer centre is {panelCentre} but UIPalette says {UIPalette.PanelBackground}");
        }

        // A button's focus is the filled accent plate (UI_SPEC 3.2), and hover = focus.
        var focus = theme.GetStylebox("focus", "Button") as StyleBoxTexture;
        AssertObject(focus).OverrideFailureMessage("Button/focus must be the kit's focus plate texture").IsNotNull();
        Color focusCentre = CentrePixel(focus);
        if (!Near(focusCentre, UIPalette.FocusFill)) {
            drift.Add($"Button focus fill is {focusCentre} but UIPalette says {UIPalette.FocusFill}");
        }
        var hover = theme.GetStylebox("hover", "Button") as StyleBoxTexture;
        AssertObject(hover).IsNotNull();
        if (!Near(CentrePixel(hover), UIPalette.ButtonHover)) {
            drift.Add($"Button hover fill is {CentrePixel(hover)} but UIPalette says {UIPalette.ButtonHover}");
        }
        AssertThat(hover.Texture.ResourcePath).IsEqual(focus.Texture.ResourcePath);
        var pressed = theme.GetStylebox("pressed", "Button") as StyleBoxTexture;
        AssertObject(pressed).IsNotNull();
        if (!Near(CentrePixel(pressed), UIPalette.ButtonPressed)) {
            drift.Add($"Button pressed fill is {CentrePixel(pressed)} but UIPalette says {UIPalette.ButtonPressed}");
        }
        var normal = theme.GetStylebox("normal", "Button") as StyleBoxTexture;
        AssertObject(normal).IsNotNull();
        if (!Near(CentrePixel(normal), UIPalette.ButtonNormal)) {
            drift.Add($"Button normal fill is {CentrePixel(normal)} but UIPalette says {UIPalette.ButtonNormal}");
        }
        // The disabled plate's stripes tile instead of stretching.
        var disabled = theme.GetStylebox("disabled", "Button") as StyleBoxTexture;
        AssertObject(disabled).IsNotNull();
        AssertThat(disabled.AxisStretchHorizontal).IsEqual(StyleBoxTexture.AxisStretchMode.Tile);
        var lineFocus = theme.GetStylebox("focus", "LineEdit") as StyleBoxFlat;
        AssertObject(lineFocus).IsNotNull();
        if (!lineFocus.BorderColor.IsEqualApprox(UIPalette.FocusOutline)) {
            drift.Add($"LineEdit focus ring is {lineFocus.BorderColor} but UIPalette says {UIPalette.FocusOutline}");
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

        // Every UI_SPEC 2.2 role (T01-T50) the kit names: present, a Label
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
    /// Fonts flow through variations, never per-node overrides: each Label
    /// role names its pinned face and weight, and the face is the one the
    /// role's table row says.
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
        }
        if (drift.Count > 0) AssertThat(string.Join(" | ", drift)).IsEqual("");
    }

    [TestCase]
    public void EveryPulpVariationExistsOnItsBaseType() {
        Theme theme = LoadTheme();
        var drift = new List<string>();
        void Require(string name, string baseType, string styleName) {
            if (theme.GetTypeVariationBase(name) != baseType) drift.Add($"{name} must be a {baseType} variation");
            if (!theme.HasStylebox(styleName, name)) drift.Add($"{name}/styles/{styleName} missing");
        }
        foreach (string name in new[] {
            UIPalette.Variations.PulpButton, UIPalette.Variations.PulpButtonCompact,
            UIPalette.Variations.TemporalGlassButton }) {
            Require(name, "Button", "normal");
            Require(name, "Button", "focus");
        }
        foreach (string name in new[] {
            UIPalette.Variations.InkPanel, UIPalette.Variations.PaperPanel, UIPalette.Variations.HudPlatePanel,
            UIPalette.Variations.DialogueGlassPanel, UIPalette.Variations.BannerPanel,
            UIPalette.Variations.PanelTagPlate, UIPalette.Variations.BossNameplate,
            UIPalette.Variations.BossPhaseTab, UIPalette.Variations.SlotPlate,
            UIPalette.Variations.SlotPlateReady, UIPalette.Variations.KeyTagPlate,
            UIPalette.Variations.CardPlate, UIPalette.Variations.CardPlatePick,
            UIPalette.Variations.BubblePlate,
            UIPalette.Variations.StampPlate, UIPalette.Variations.ScrimPanel,
            UIPalette.Variations.CoverBannerPanel }) {
            Require(name, "PanelContainer", "panel");
        }
        foreach (string name in new[] { UIPalette.Variations.PulpBar, UIPalette.Variations.PulpBarH44, UIPalette.Variations.PulpBarH16, UIPalette.Variations.PulpBarH14, UIPalette.Variations.PulpBossBar }) {
            Require(name, "ProgressBar", "background");
            Require(name, "ProgressBar", "fill");
            // FighterHUD.TintFill recolours the fill by reading it as a StyleBoxFlat.
            if (theme.GetStylebox("fill", name) is not StyleBoxFlat) drift.Add($"{name} fill must stay a StyleBoxFlat");
        }
        if (theme.GetStylebox("fill", "ProgressBar") is not StyleBoxFlat) drift.Add("ProgressBar fill must stay a StyleBoxFlat");
        if (drift.Count > 0) AssertThat(string.Join(" | ", drift)).IsEqual("");
    }

    /// <summary>
    /// The closeout folded the Phase B kit gaps into the generator: the three bar tracks, the cover banner, the
    /// dialogue line's RichTextLabel variation (UI_SPEC T49, 33 px) and the text-on-ink-plate role.
    /// </summary>
    [TestCase]
    public void TheFoldedKitGapsCarryTheirSpecSizesAndPlates() {
        Theme theme = LoadTheme();
        var drift = new List<string>();
        if (theme.GetTypeVariationBase(UIPalette.Variations.DialogueBodyRich) != "RichTextLabel") drift.Add("DialogueBodyRich must be a RichTextLabel variation");
        if (theme.GetFontSize("normal_font_size", UIPalette.Variations.DialogueBodyRich) != 33) drift.Add("DialogueBodyRich must be 33 px");
        if (theme.GetStylebox("normal", UIPalette.LabelRoles.HintLinePlate) is not StyleBoxFlat) drift.Add("HintLinePlate must carry its own ink plate");
        foreach (var (name, stem) in new[] { (UIPalette.Variations.PulpBarH44, "bar_track_h44"), (UIPalette.Variations.PulpBarH16, "bar_track_h16"), (UIPalette.Variations.PulpBarH14, "bar_track_h14") }) {
            if (theme.GetStylebox("background", name) is not StyleBoxTexture track || !track.Texture.ResourcePath.EndsWith(stem + ".png")) drift.Add($"{name} background must be {stem}");
        }
        if (theme.GetStylebox("panel", UIPalette.Variations.CoverBannerPanel) is not StyleBoxTexture banner || !banner.Texture.ResourcePath.EndsWith("banner_cover.png")) drift.Add("CoverBannerPanel must wear banner_cover");
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

    /// <summary>Plan item 14: text at least 4.5:1 on the surface it sits on (UI_SPEC 1.3, with the disabled label lifted).</summary>
    [TestCase]
    public void TheThemesTextPairsClearTheContrastFloor() {
        // Plates are drawn over scene art; contrast is measured on the opaque fill.
        Color plate = new Color(UIPalette.PlateFill.R, UIPalette.PlateFill.G, UIPalette.PlateFill.B);
        var failing = new List<string>();
        void Pair(string name, Color fg, Color bg, double floor) {
            double ratio = Contrast(fg, bg);
            if (ratio < floor) failing.Add($"{name} {ratio:0.00}:1 under {floor}");
        }
        Pair("Ink on plate", UIPalette.Ink, plate, 4.5);
        Pair("Ink2 on plate", UIPalette.Ink2, plate, 4.5);
        Pair("Ink3 on plate", UIPalette.Ink3, plate, 4.5);
        Pair("Accent on plate", UIPalette.Accent, plate, 4.5);
        Pair("LabelDark on Accent", UIPalette.LabelDark, UIPalette.Accent, 4.5);
        Pair("LabelDark on Accent2", UIPalette.LabelDark, UIPalette.Accent2, 4.5);
        Pair("LabelDark on Paper", UIPalette.LabelDark, UIPalette.Paper, 4.5);
        Pair("PaperInk2 on Paper", UIPalette.PaperInk2, UIPalette.Paper, 4.5);
        Pair("DisabledLabel on DisabledFill", UIPalette.DisabledLabel, UIPalette.DisabledFill, 4.5);
        Pair("LabelDark on Danger", UIPalette.LabelDark, UIPalette.Danger, 4.5);
        if (failing.Count > 0) AssertThat(string.Join(" | ", failing)).IsEqual("");
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
