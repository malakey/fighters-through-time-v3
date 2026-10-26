using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-SELECT. Shrink-to-fit for the text of the select's fixed-size card art. UI scale is an accessibility setting, and the
    /// cards cannot grow with it, so a name that no longer fits its box is scaled down as a node (never a font-size override),
    /// but never below the size the same text has at UI scale 1.0 (where every name fits by design).
    /// </summary>
    public static class CardTextFit {

        /// <summary>The node scale for text <paramref name="textWidth"/> wide in a box <paramref name="boxWidth"/> wide at UI scale <paramref name="uiScale"/>: 1 when it fits, else the fit, floored at the 1.0-scale size.</summary>
        public static float ScaleFor(float textWidth, float boxWidth, float uiScale) {
            if (textWidth <= 0f || boxWidth <= 0f || textWidth <= boxWidth) return 1f;
            float floor = uiScale > 1f ? 1f / uiScale : 1f;
            return Mathf.Clamp(boxWidth / textWidth, floor, 1f);
        }

        /// <summary>The width the label's role draws its current text at (upper-cased when the label is).</summary>
        public static float TextWidth(Label label) {
            Font font = label.GetThemeFont("font");
            int size = label.GetThemeFontSize("font_size");
            string text = label.Uppercase ? label.Text.ToUpperInvariant() : label.Text;
            return font == null ? 0f : font.GetStringSize(text, HorizontalAlignment.Left, -1f, size).X;
        }

        /// <summary>
        /// Fits <paramref name="label"/> (a child of a non-container, so its scale sticks) to the box that starts at
        /// <paramref name="boxLeft"/> and is <paramref name="boxWidth"/> wide. The label is widened to its text so nothing clips,
        /// kept centred (or left-aligned) on the box, and scaled about its centre (or left edge). Returns the scale applied.
        /// </summary>
        public static float Fit(Label label, float boxLeft, float boxWidth, bool centred) {
            float text = TextWidth(label);
            float scale = ScaleFor(text, boxWidth, UIPalette.AppliedUiScale);
            float width = Mathf.Max(boxWidth, text + 2f);
            label.Size = new Vector2(width, label.Size.Y);
            label.Position = new Vector2(centred ? boxLeft + (boxWidth - width) / 2f : boxLeft, label.Position.Y);
            label.PivotOffset = new Vector2(centred ? width / 2f : 0f, label.Size.Y / 2f);
            label.Scale = new Vector2(scale, scale);
            return scale;
        }
    }
}
