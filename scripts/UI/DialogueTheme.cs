using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 8 B3. The dialogue box's glass treatment, expressed as a theme
    /// type variation on the shared A1 theme plus the few metrics that only code
    /// can apply.
    ///
    /// <para><b>What "glass" means under gl_compatibility.</b> A real frosted
    /// panel needs a backbuffer copy and a blur pass; the renderer this project
    /// ships on has no <c>BackBufferCopy</c> support worth relying on in a
    /// CanvasLayer overlay, and A3 already recorded how narrow the shader
    /// envelope is here. The treatment is therefore built from what a
    /// <see cref="StyleBoxFlat"/> can actually do: a translucent navy fill that
    /// lets the scene read through, a soft drop shadow that separates the panel
    /// from the world behind it, an accent border, and a wider corner radius
    /// than the utility panels use. It reads as glass at 1080p and costs one
    /// draw call.</para>
    ///
    /// <para>The constants here are pinned against the authored theme by
    /// <c>DialoguePresentationTests</c>, the same contract A1 established
    /// between <see cref="UIPalette"/> and <c>ftt_theme.tres</c>: changing one
    /// side alone fails.</para>
    /// </summary>
    public static class DialogueTheme {

        /// <summary>Theme type variation carried by the dialogue panel (base type PanelContainer).</summary>
        public const string GlassPanelVariation = "DialogueGlassPanel";

        // ---- Glass panel -----------------------------------------------------

        /// <summary>
        /// The glass slab's translucent fill (TG-THEME cross-edit: the theme's glass slab, <see cref="UIPalette.PanelBackground"/>,
        /// was the pulp ink plate). Alpha is the whole point: the scene reads through it. The slab's edge, shadow and
        /// chamfer come from the theme, so no border, shadow or radius constant lives here.
        /// </summary>
        public static readonly Color GlassBackground = UIPalette.PanelBackground;

        // ---- Box and roundel (UI_SPEC Appendix B) -----------------------------

        /// <summary>The box is 1500 px wide, centred, its bottom edge 40 px above the screen's.</summary>
        public const float BoxWidth = 1500f;
        public const float BoxBottomMargin = 40f;

        /// <summary>The speaker's G5 portrait frame (UI_SPEC_GLASS 3.9: 150).</summary>
        public const float PortraitSize = 150f;

        /// <summary>
        /// The solid <c>hourglass_caret</c> advance caret (3.9): 34 px in <c>res2</c>, 26 px from the box's right edge and
        /// 18 px above its bottom.
        /// </summary>
        public const string CaretIcon = "hourglass_caret";
        public const float CaretSize = 34f;
        public const float CaretRight = 26f;
        public const float CaretBottom = 18f;

        /// <summary>3.9: the row (frame, the name-and-line column, the other speaker's frame) is 26 apart; the column 10.</summary>
        public const int RowGap = 26;
        public const int ColumnGap = 10;

        /// <summary>Gap between the portrait frame and the emotion frame drawn around it.</summary>
        public const int RingInset = 4;

        // ---- Box animation ---------------------------------------------------

        /// <summary>Slide/fade duration for the box entering or leaving, in seconds.</summary>
        public const float BoxAnimationSeconds = 0.18f;

        /// <summary>How far below its resting position the box starts, in pixels.</summary>
        public const float BoxSlideDistance = 48f;

        /// <summary>
        /// Builds the per-emotion frame that surrounds the portrait: no fill, a border whose colour and width change on
        /// every line (an authored resource mutated at runtime would be a shared cached instance every other dialogue box
        /// also draws). TG-PAUSE: it follows the G5 frame's shape (a square cut at 22 % top-left and bottom-right, drawn as
        /// 45 degree bevels with <c>corner_detail</c> 1) instead of the pulp build's circle.
        /// </summary>
        public static StyleBoxFlat CreatePortraitFrameStyle() {
            var box = new StyleBoxFlat {
                BgColor = new Color(0f, 0f, 0f, 0f),
                DrawCenter = false,
                BorderColor = DialogueEmotionTreatment.Resolve(DialogueEmotion.Neutral).FrameColor,
                ContentMarginLeft = RingInset,
                ContentMarginTop = RingInset,
                ContentMarginRight = RingInset,
                ContentMarginBottom = RingInset,
                CornerDetail = 1,
                AntiAliasing = true
            };
            int cut = Mathf.RoundToInt((PortraitSize + 2f * RingInset) * Pulp.PulpPortraitRoundel.ChamferShare);
            box.CornerRadiusTopLeft = cut;
            box.CornerRadiusBottomRight = cut;
            box.SetBorderWidthAll(DialogueEmotionTreatment.CalmFrameWidth);
            return box;
        }
    }
}
