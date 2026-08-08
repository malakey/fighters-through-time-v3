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

        /// <summary>Translucent fill. Alpha is the whole point: the scene reads through it.</summary>
        public static readonly Color GlassBackground = new(0.06f, 0.06f, 0.12f, 0.72f);

        /// <summary>Accent border, dimmer and more translucent than a solid UI panel's.</summary>
        public static readonly Color GlassBorder = new(0f, 0.9f, 0.9f, 0.55f);

        /// <summary>Drop shadow that lifts the panel off the scene behind it.</summary>
        public static readonly Color GlassShadow = new(0f, 0.02f, 0.04f, 0.45f);

        public const int GlassCornerRadius = 10;
        public const int GlassBorderWidth = 2;
        public const int GlassShadowSize = 12;

        // ---- Portrait frame --------------------------------------------------

        /// <summary>
        /// Backing fill behind the portrait. Darker and more opaque than the
        /// panel so a light portrait still has contrast against the glass.
        /// </summary>
        public static readonly Color PortraitBackground = new(0.03f, 0.04f, 0.08f, 0.85f);

        public const int PortraitCornerRadius = 6;
        public const int PortraitSize = 160;

        // ---- Box animation ---------------------------------------------------

        /// <summary>Slide/fade duration for the box entering or leaving, in seconds.</summary>
        public const float BoxAnimationSeconds = 0.18f;

        /// <summary>How far below its resting position the box starts, in pixels.</summary>
        public const float BoxSlideDistance = 48f;

        /// <summary>
        /// Builds the per-emotion portrait frame box. Constructed in code rather
        /// than duplicated from the theme because its border colour changes on
        /// every line — an authored resource mutated at runtime would be a
        /// shared cached instance every other dialogue box also draws.
        /// </summary>
        public static StyleBoxFlat CreatePortraitFrameStyle() {
            var box = new StyleBoxFlat {
                BgColor = PortraitBackground,
                BorderColor = DialogueEmotionTreatment.Resolve(DialogueEmotion.Neutral).FrameColor,
                ContentMarginLeft = 6,
                ContentMarginTop = 6,
                ContentMarginRight = 6,
                ContentMarginBottom = 6,
                CornerRadiusTopLeft = PortraitCornerRadius,
                CornerRadiusTopRight = PortraitCornerRadius,
                CornerRadiusBottomLeft = PortraitCornerRadius,
                CornerRadiusBottomRight = PortraitCornerRadius
            };
            box.SetBorderWidthAll(DialogueEmotionTreatment.CalmFrameWidth);
            return box;
        }
    }
}
