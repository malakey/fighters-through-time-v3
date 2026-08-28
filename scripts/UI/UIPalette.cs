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
    public static class UIPalette {

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

        /// <summary>Panel and menu background.</summary>
        public static readonly Color Navy = new(0.06f, 0.06f, 0.12f);

        /// <summary>Full-screen background (loading, letterbox fills).</summary>
        public static readonly Color NavyDeep = new(0.05f, 0.05f, 0.1f);

        /// <summary>Translucent scrim behind a modal or results panel.</summary>
        public static readonly Color Shade = new(0.02f, 0.03f, 0.08f, 0.85f);

        /// <summary>Warning/attention copy (controller disconnect, conflicts).</summary>
        public static readonly Color Warning = new(1f, 0.75f, 0.3f);

        // ---- Derived control colours (what the Theme actually paints) --------

        public static readonly Color PanelBackground = new(0.06f, 0.06f, 0.12f, 0.94f);
        public static readonly Color PanelBorder = new(0f, 0.55f, 0.6f, 0.9f);

        public static readonly Color ButtonNormal = new(0.1f, 0.12f, 0.2f, 0.95f);
        public static readonly Color ButtonHover = new(0.14f, 0.2f, 0.3f, 0.98f);
        public static readonly Color ButtonPressed = new(0f, 0.3f, 0.34f, 1f);
        public static readonly Color ButtonDisabled = new(0.08f, 0.08f, 0.12f, 0.7f);

        /// <summary>
        /// Focus ring colour. Controller and keyboard players navigate entirely
        /// by this outline, so every focusable control in the theme draws it.
        /// </summary>
        public static readonly Color FocusOutline = new(0f, 0.9f, 0.9f);

        public static readonly Color TextPrimary = new(0.7f, 0.75f, 0.85f);
        public static readonly Color TextAccent = new(0f, 0.9f, 0.9f);
        public static readonly Color TextDisabled = new(0.4f, 0.42f, 0.5f);

        /// <summary>Slider and progress-bar troughs.</summary>
        public static readonly Color TrackBackground = new(0.03f, 0.04f, 0.08f, 0.95f);

        // ---- Type scale -----------------------------------------------------
        //
        // V7.3: every non-HUD screen expresses these roles through the theme's
        // Label type variations (below) rather than per-label
        // AddThemeFontSizeOverride calls, because UIPalette.ApplyUiScale rescales
        // the theme's font-size entries in place — an override is invisible to it
        // and freezes that label out of the accessibility UI scale. The constants
        // stay as the C# mirror UIThemeTests pins against the authored theme.

        public const int SmallFontSize = 14;
        public const int BodyFontSize = 18;
        public const int HeadingFontSize = 22;
        public const int EmphasisFontSize = 26;
        public const int TitleFontSize = 28;

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
