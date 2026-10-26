using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 8 A1. The single source of truth for UI colour, type scale, and
    /// layout metrics.
    ///
    /// Before this existed the same six colours were retyped as literals across
    /// roughly fifteen UI scripts and scenes, so a palette change meant a
    /// repo-wide search. The values here are the ones those literals already
    /// used — this class names them, it does not re-tune them. The authored
    /// theme at <see cref="ThemePath"/> is built from exactly these numbers and
    /// <c>UIThemeTests</c> pins the two together, so changing a colour here
    /// requires changing the matching entry in the theme resource.
    ///
    /// Screens adopt the theme by setting <c>Theme</c> on their root
    /// <see cref="Control"/>; Godot propagates it down the subtree. Reach for a
    /// constant here only for the things a Theme cannot express — a
    /// <see cref="ColorRect"/> fill, a runtime tint, a drawn overlay.
    /// </summary>
    public static partial class UIPalette {

        /// <summary>The shared Theme every UI root should adopt.</summary>
        public const string ThemePath = "res://resources/UI/ftt_theme.tres";

        // ---- Brand colours (the pre-existing ad-hoc palette, named) ----------

        /// <summary>Primary accent: titles, focus, Temporal Resonance highlights.</summary>
        public static readonly Color Cyan = new(0f, 0.9f, 0.9f);

        /// <summary>Dimmed accent for borders and inactive accents.</summary>
        public static readonly Color CyanDim = new(0f, 0.55f, 0.6f);

        /// <summary>Chronal Dust and reward emphasis.</summary>
        public static readonly Color Gold = new(0.95f, 0.8f, 0.3f);

        /// <summary>Player-two and future-echo accent in the Temporal Glass menu system.</summary>
        public static readonly Color TemporalViolet = new(0.75f, 0.42f, 1f);

        /// <summary>Brighter gold used by the Dust tiers and hyper-armor shell.</summary>
        public static readonly Color GoldBright = new(1f, 0.92f, 0.35f);

        /// <summary>Boss bars, danger states, destructive confirmations.</summary>
        public static readonly Color BossRed = new(0.95f, 0.25f, 0.25f);

        /// <summary>Default body-copy colour.</summary>
        public static readonly Color Slate = new(0.7f, 0.75f, 0.85f);

        /// <summary>Secondary/subdued body copy.</summary>
        public static readonly Color SlateDim = new(0.45f, 0.5f, 0.6f);

        /// <summary>
        /// CM-STORY (decision 8.2, 2026-10-16): a Story damage number whose hit the
        /// chain proration reduced (a taxed second Special, a decayed chain hit). A
        /// dimmed near-white kept distinct from <see cref="Slate"/>, the warded
        /// boss's refused grey "0"; always paired with the smaller
        /// <c>StoryEnemyCombatRules.ProratedDamageNumberScale</c>, so colour is never
        /// the only channel.
        /// </summary>
        public static readonly Color DamageNumberReduced = new(0.66f, 0.68f, 0.74f);

        /// <summary>Panel and menu background.</summary>
        public static readonly Color Navy = new(0.06f, 0.06f, 0.12f);

        /// <summary>Full-screen background (loading, letterbox fills).</summary>
        public static readonly Color NavyDeep = new(0.05f, 0.05f, 0.1f);

        /// <summary>Translucent scrim behind a modal or results panel.</summary>
        public static readonly Color Shade = new(0.02f, 0.03f, 0.08f, 0.85f);

        /// <summary>Warning/attention copy (controller disconnect, conflicts).</summary>
        public static readonly Color Warning = new(1f, 0.75f, 0.3f);

        // ---- The two-colour visual grammar (V7.6, Package 11 A6b) -----------
        //
        // design-godot.md §2, "The Visual Grammar of Resonance", is an authored
        // visual language that holds in every mode and every mirror:
        //
        //   COLD SYNTHETIC LIGHT = THE UNBOUND.  Extraction beams, siphons,
        //   neural visors, plasma. Jagged and desaturating — what their machines
        //   touch drains grey.
        //
        //   WARM GOLD = HISTORY'S RESONANCE.  The hero's ignition and persistent
        //   aura, Chronal Dust, restoration vignettes, the ending release. Gold
        //   always flows FROM the era INTO its people.
        //
        // The pigments below are not new: the repo already painted extractors
        // cold and Dust gold. What was missing was the CONTRACT — nothing said
        // the two families were load-bearing, so every new siphon or beam site
        // invented its own literal and the language quietly eroded (Level 0's
        // own fracture had drifted to violet). These are the named constants
        // every such site must use, and ResonanceGrammarTests fails the build
        // when one defines its own cold literal instead.
        //
        // Deliberately expressed as the pigments the game already paints rather
        // than as new numbers: the grammar names what exists, it does not
        // re-tune it. They add no theme entries, so UIThemeTests is unaffected.

        /// <summary>
        /// The Unbound's cold synthetic light: extraction beams, siphon tethers,
        /// Unbound rift tears, neural visors, plasma. Never used for anything the
        /// hero or an era generates.
        /// </summary>
        public static readonly Color UnboundCold = new(0.2f, 0.95f, 1f);

        /// <summary>
        /// The same cold light at the alpha every discharge and beam site draws
        /// it at. <c>ChronalExtractor.DischargeColor</c> is exactly this value —
        /// the extractor was already correct, merely mislabelled "Apex Archive
        /// cyan" before the V7.5 faction rename.
        /// </summary>
        public static readonly Color UnboundColdDischarge = new(0.2f, 0.95f, 1f, 0.85f);

        /// <summary>
        /// Dimmed cold, for a siphon's resting state and the halo around a cold
        /// beam — the light before it bites.
        /// </summary>
        public static readonly Color UnboundColdDim = new(0.12f, 0.55f, 0.68f, 0.28f);

        /// <summary>
        /// History's resonance: the hero's ignition and persistent aura, Chronal
        /// Dust, restoration vignettes. The warm half of the grammar. Same value
        /// as <see cref="Gold"/>, named so a grammar site reads as grammar.
        /// </summary>
        public static readonly Color ResonanceGold = new(0.95f, 0.8f, 0.3f);

        /// <summary>
        /// Resonance at full ignition — the brighter gold the Dust tiers and the
        /// hyper-armor shell already use (<see cref="GoldBright"/>).
        /// </summary>
        public static readonly Color ResonanceGoldBright = new(1f, 0.92f, 0.35f);

        /// <summary>
        /// The persistent hero aura's tint. Warm gold biased toward white so it
        /// reads as a glow the hero carries rather than a repaint of the sprite.
        /// Matches <c>ChronalDustPickup.GlowColor</c>'s hue: the recurrence
        /// contract says every Dust pickup restates the hero's origin.
        /// </summary>
        public static readonly Color ResonanceAura = new(1f, 0.86f, 0.42f);

        /// <summary>
        /// A Warden portal's clean steady geometry. Warm, because the Wardens are
        /// history's side of the grammar — the contrast with an Unbound rift is
        /// temperature AND shape, and Level 0's final beat establishes both by
        /// standing the two doors side by side.
        /// </summary>
        public static readonly Color WardenPortal = new(0.98f, 0.84f, 0.38f, 0.85f);

        // ---- Pulp tokens (PAL-B "Resonance Gold", UI redesign 2026-10-26) ------
        //
        // UI_SPEC section 1 resolved to constants. The chrome accent is gold;
        // cyan stays the Player-1 ownership colour and the Severed's cold light
        // (UnboundCold, above) and is no longer the chrome. There is no
        // "pulp red" token (orchestrator ruling Q1): functional damage cues use
        // Danger. Colours are written as byte triples so they read as the
        // spec's hex values; UIThemeTests pins the authored theme to them.

        private static Color Hex(int r, int g, int b, float a = 1f) => new(r / 255f, g / 255f, b / 255f, a);

        /// <summary>bg1: the translucent ink plate fill (#090C1E at 0.80).</summary>
        public static readonly Color PlateFill = Hex(0x09, 0x0C, 0x1E, 0.80f);
        /// <summary>bg2: raised fill for settings tabs (#1E1C34 at 0.78).</summary>
        public static readonly Color PlateRaised = Hex(0x1E, 0x1C, 0x34, 0.78f);
        /// <summary>Screen backdrop under art (#05071A).</summary>
        public static readonly Color Backdrop = Hex(0x05, 0x07, 0x1A);
        /// <summary>Text 1 (#F3F0E6).</summary>
        public static readonly Color Ink = Hex(0xF3, 0xF0, 0xE6);
        /// <summary>Text 2 (#C0BAA6): captions.</summary>
        public static readonly Color Ink2 = Hex(0xC0, 0xBA, 0xA6);
        /// <summary>Text 3 (#8A8374): secondary and dormant, never the only cue.</summary>
        public static readonly Color Ink3 = Hex(0x8A, 0x83, 0x74);
        /// <summary>The chrome accent (#F3C95B): focus fill, tags, banners.</summary>
        public static readonly Color Accent = Hex(0xF3, 0xC9, 0x5B);
        /// <summary>Accent light (#FFE39A): pressed fill, rewind and Time Freeze tint.</summary>
        public static readonly Color Accent2 = Hex(0xFF, 0xE3, 0x9A);
        /// <summary>Resonance gold (#F3C95B): dust, Ultimate ring start.</summary>
        public static readonly Color Res = Hex(0xF3, 0xC9, 0x5B);
        /// <summary>Resonance bright (#FFF0B0): Defy ready, objective, full meter.</summary>
        public static readonly Color Res2 = Hex(0xFF, 0xF0, 0xB0);
        /// <summary>Ultimate meter start (#F3C95B).</summary>
        public static readonly Color Meter = Hex(0xF3, 0xC9, 0x5B);
        /// <summary>Ultimate meter end and the Fighter meter fill (#FFB347).</summary>
        public static readonly Color Meter2 = Hex(0xFF, 0xB3, 0x47);
        /// <summary>HP fill (#8CF0B8).</summary>
        public static readonly Color Hp = Hex(0x8C, 0xF0, 0xB8);
        /// <summary>HP damage tracker (#FF5A5A), the delayed underfill.</summary>
        public static readonly Color HpLag = Hex(0xFF, 0x5A, 0x5A);
        /// <summary>Rally echo band (#FFF7DA).</summary>
        public static readonly Color Echo = Hex(0xFF, 0xF7, 0xDA);
        /// <summary>Block pip (#DFE8FF).</summary>
        public static readonly Color Shield = Hex(0xDF, 0xE8, 0xFF);
        /// <summary>Low HP and warnings (#FF5A5A).</summary>
        public static readonly Color Danger = Hex(0xFF, 0x5A, 0x5A);
        /// <summary>Boss red (#FF5A5A).</summary>
        public static readonly Color Boss = Hex(0xFF, 0x5A, 0x5A);
        /// <summary>Paper (#F3F0E6): panel border, selected button, bubbles.</summary>
        public static readonly Color Paper = Hex(0xF3, 0xF0, 0xE6);

        /// <summary>
        /// Player 1 ownership colour. Reads the combat slot palette
        /// (<see cref="FTT.Combat.GlowPalette.SlotColor"/>) so the two can never drift.
        /// </summary>
        public static Color P1 => FTT.Combat.GlowPalette.SlotColor(0);

        /// <summary>Player 2 ownership colour, read from the combat slot palette.</summary>
        public static Color P2 => FTT.Combat.GlowPalette.SlotColor(1);

        // UI_SPEC 1.2: literals the selected components hard-code.
        /// <summary>#000000: every ring, border and hard shadow.</summary>
        public static readonly Color InkBlack = Hex(0, 0, 0);
        /// <summary>#0C0A10: dark text on gold or paper, card base.</summary>
        public static readonly Color LabelDark = Hex(0x0C, 0x0A, 0x10);
        /// <summary>#14101A: paper-panel text 1.</summary>
        public static readonly Color PaperInk = Hex(0x14, 0x10, 0x1A);
        /// <summary>#3A3144: paper-panel text 2 (stat labels).</summary>
        public static readonly Color PaperInk2 = Hex(0x3A, 0x31, 0x44);
        /// <summary>#6B6150: paper-panel text 3, disabled border.</summary>
        public static readonly Color PaperInk3 = Hex(0x6B, 0x61, 0x50);
        /// <summary>#3A3144: empty bar, ring and toggle tracks.</summary>
        public static readonly Color Track = Hex(0x3A, 0x31, 0x44);
        /// <summary>#2A2434: ability slot frame fill.</summary>
        public static readonly Color SlotFrame = Hex(0x2A, 0x24, 0x34);
        /// <summary>#FFF3D8: prompt text, key circles, hint lines.</summary>
        public static readonly Color PromptCream = Hex(0xFF, 0xF3, 0xD8);
        /// <summary>#FF7A5C: damage-over-time status badge.</summary>
        public static readonly Color StatusDamage = Hex(0xFF, 0x7A, 0x5C);
        /// <summary>#8FC7FF: control status badge.</summary>
        public static readonly Color StatusControl = Hex(0x8F, 0xC7, 0xFF);
        /// <summary>#2C2733: disabled button base stripe.</summary>
        public static readonly Color DisabledFill = Hex(0x2C, 0x27, 0x33);
        /// <summary>
        /// Disabled label (#A0987F). The spec's #7A705A reads 2.97:1 on the disabled
        /// stripe; this is lifted past 4.5:1 (plan item 14, accessibility).
        /// </summary>
        public static readonly Color DisabledLabel = Hex(0xA0, 0x98, 0x7F);

        // ---- Derived control colours (what the Theme actually paints) --------

        public static readonly Color PanelBackground = PlateFill;
        public static readonly Color PanelBorder = Paper;

        public static readonly Color ButtonNormal = PlateFill;
        public static readonly Color ButtonHover = Accent;
        public static readonly Color ButtonPressed = Accent2;
        public static readonly Color ButtonDisabled = DisabledFill;

        /// <summary>
        /// Focus ring colour for ring-style focus boxes (line edits, check boxes).
        /// A button's focus is the filled accent plate instead
        /// (<see cref="FocusFill"/>). Controller and keyboard players navigate
        /// entirely by these, so every focusable control in the theme draws one.
        /// </summary>
        public static readonly Color FocusOutline = Accent;

        /// <summary>The filled plate a focused or hovered button draws.</summary>
        public static readonly Color FocusFill = Accent;

        /// <summary>Label colour on a focused, hovered or pressed (accent-filled) button.</summary>
        public static readonly Color TextOnAccent = LabelDark;

        public static readonly Color TextPrimary = Ink;
        public static readonly Color TextAccent = Accent;
        public static readonly Color TextDisabled = DisabledLabel;

        /// <summary>Slider and progress-bar troughs.</summary>
        public static readonly Color TrackBackground = Track;

        // ---- Type scale -----------------------------------------------------
        //
        // V7.3: every non-HUD screen expresses these roles through the theme's
        // Label type variations (below) rather than per-label
        // AddThemeFontSizeOverride calls, because UIPalette.ApplyUiScale rescales
        // the theme's font-size entries in place — an override is invisible to it
        // and freezes that label out of the accessibility UI scale. The constants
        // stay as the C# mirror UIThemeTests pins against the authored theme.

        public const int SmallFontSize = 18;
        public const int BodyFontSize = 20;
        public const int HeadingFontSize = 26;
        public const int EmphasisFontSize = 30;
        public const int TitleFontSize = 40;

        /// <summary>No UI label renders below this at 1080p (plan item 14).</summary>
        public const int MinLabelFontSize = 18;

        /// <summary>Label type variation names authored in the shared theme
        /// (ftt_theme.tres), one per type-scale role. The body role is the
        /// Label default (no variation).</summary>
        public const string SmallLabelVariation = "SmallLabel";
        public const string HeadingLabelVariation = "HeadingLabel";
        public const string EmphasisLabelVariation = "EmphasisLabel";
        public const string TitleLabelVariation = "TitleLabel";

        // ---- Layout metrics -------------------------------------------------

        public const int ButtonMinWidth = 320;
        public const int ButtonMinHeight = 44;
        public const int PanelSeparation = 14;
        public const int CornerRadius = 4;
        public const int BorderWidth = 2;

        /// <summary>
        /// Loads the shared theme, or returns null when it is missing. Callers
        /// fall back to unthemed defaults rather than failing: a missing theme
        /// must never take a screen down.
        /// </summary>
        public static Theme LoadTheme() {
            if (!ResourceLoader.Exists(ThemePath)) return null;
            return ResourceLoader.Load<Theme>(ThemePath);
        }

        /// <summary>Applies the shared theme to a control root when available.</summary>
        public static void ApplyTheme(Control root) {
            if (root == null || root.Theme != null) return;
            Theme theme = LoadTheme();
            if (theme != null) root.Theme = theme;
        }

        // ---- Accessibility UI scale ----------------------------------------
        //
        // Design "Committed Accessibility Additions": a 90%–140% UI scale
        // applied to the shared theme's font sizes and the HUD layout. Eleven
        // scenes attach the theme resource directly and the rest adopt it via
        // ApplyTheme, so the scale is applied by mutating the one cached theme
        // instance in place: every screen — scene-attached or code-applied,
        // already open or not — picks the change up through Godot's theme
        // change propagation, with no per-screen wiring. The authored sizes are
        // captured on the first apply so repeated applies never compound.
        // The HUDs are the exception: they scale their whole layout through
        // UiScaleBinder and therefore use NewUnscaledTheme to avoid scaling
        // their fonts twice.

        /// <summary>The saved UI scale, clamped; 1 when no save is loaded.</summary>
        public static float CurrentUiScale => Mathf.Clamp(
            FTT.Core.SaveManager.Instance?.GlobalData?.UiScale ?? 1f,
            FTT.Core.GlobalSaveData.MinUiScale, FTT.Core.GlobalSaveData.MaxUiScale);

        /// <summary>The factor most recently applied to the shared theme.</summary>
        public static float AppliedUiScale { get; private set; } = 1f;

        private static System.Collections.Generic.Dictionary<(string Type, string Name), int> _authoredFontSizes;
        private static int _authoredDefaultFontSize = -1;

        /// <summary>Applies the persisted setting to the shared theme (boot and settings-change entry point).</summary>
        public static void ApplySavedUiScale() => ApplyUiScale(CurrentUiScale);

        /// <summary>
        /// Rescales the shared theme's default font size and every per-type
        /// font-size entry to <paramref name="factor"/> times the authored
        /// value. Idempotent: always scales from the captured authored sizes,
        /// never from the current ones.
        /// </summary>
        public static void ApplyUiScale(float factor) {
            factor = Mathf.Clamp(factor,
                FTT.Core.GlobalSaveData.MinUiScale, FTT.Core.GlobalSaveData.MaxUiScale);
            Theme theme = LoadTheme();
            if (theme == null) return;
            CaptureAuthoredSizes(theme);
            if (_authoredDefaultFontSize > 0) {
                theme.DefaultFontSize = ScaleFontSize(_authoredDefaultFontSize, factor);
            }
            foreach (var entry in _authoredFontSizes) {
                theme.SetFontSize(entry.Key.Name, entry.Key.Type, ScaleFontSize(entry.Value, factor));
            }
            AppliedUiScale = factor;
        }

        /// <summary>
        /// A duplicate of the shared theme carrying the authored (unscaled)
        /// font sizes, for surfaces that scale their whole layout through
        /// <see cref="UiScaleBinder"/> instead. Null when the theme is missing.
        /// </summary>
        public static Theme NewUnscaledTheme() {
            Theme theme = LoadTheme();
            if (theme == null) return null;
            var copy = (Theme)theme.Duplicate(true);
            if (_authoredFontSizes == null) return copy;
            if (_authoredDefaultFontSize > 0) copy.DefaultFontSize = _authoredDefaultFontSize;
            foreach (var entry in _authoredFontSizes) {
                copy.SetFontSize(entry.Key.Name, entry.Key.Type, entry.Value);
            }
            return copy;
        }

        private static void CaptureAuthoredSizes(Theme theme) {
            if (_authoredFontSizes != null) return;
            _authoredFontSizes = new System.Collections.Generic.Dictionary<(string, string), int>();
            _authoredDefaultFontSize = theme.HasDefaultFontSize() ? theme.DefaultFontSize : -1;
            foreach (string themeType in theme.GetFontSizeTypeList()) {
                foreach (string sizeName in theme.GetFontSizeList(themeType)) {
                    _authoredFontSizes[(themeType, sizeName)] = theme.GetFontSize(sizeName, themeType);
                }
            }
        }

        /// <summary>Rounds a scaled font size, never below 1 px.</summary>
        public static int ScaleFontSize(int size, float factor) =>
            Mathf.Max(1, Mathf.RoundToInt(size * factor));
    }
}
