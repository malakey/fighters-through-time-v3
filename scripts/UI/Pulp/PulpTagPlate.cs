using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// Closeout (2026-10-26). The one place a panel tag is built and placed (UI_SPEC 3.1: a <c>PanelTagPlate</c>
    /// holding an upper-case <c>PanelTag</c> label, rotated -1.2 degrees about its bottom-left corner). Four
    /// panels wear a tag and three of them (<see cref="PulpTaggedPanel"/>, <c>PanelTagAnchor</c>, <c>PausePanel</c>)
    /// used to build it by hand; they differ only in where the tag hangs, because their node paths differ and
    /// tests pin those paths. The main menu's authored <c>TagHolder/Tag</c> nodes stay scene-authored for the
    /// same reason and use the same variations and offsets.
    /// </summary>
    public static class PulpTagPlate {

        /// <summary>Builds the tag plate (hidden until it has text). <paramref name="label"/> is its <c>PanelTag</c> label.</summary>
        public static PanelContainer Create(out Label label) {
            label = new Label {
                Name = "TagLabel", Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.PanelTag,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            var tag = new PanelContainer {
                Name = "Tag", ThemeTypeVariation = UIPalette.Variations.PanelTagPlate,
                MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 1, Visible = false
            };
            tag.AddChild(label);
            return tag;
        }

        /// <summary>The margin kept between a tag's right edge and its panel's.</summary>
        public const float RightMargin = 24f;

        /// <summary>
        /// Shrinks the tag to its text, puts its top-left at <paramref name="position"/> and tilts it. When
        /// <paramref name="maxRight"/> is given (the panel's right edge less <see cref="RightMargin"/>, in the tag's
        /// parent space) a tag whose natural width would run past it is clamped to fit and its label ellipsized.
        /// </summary>
        public static void Place(Control tag, Vector2 position, float maxRight = float.PositiveInfinity) {
            var label = tag.GetChildCount() > 0 ? tag.GetChild(0) as Label : null;
            tag.ResetSize();
            if (label != null) {
                // A clipped label reports about no minimum width and a container's cached minimum lags a ClipText
                // toggle, so the natural width is measured from the label's own font and the plate sized explicitly.
                float natural = NaturalWidth(tag, label);
                bool clamp = !float.IsPositiveInfinity(maxRight) && position.X + natural > maxRight;
                label.ClipText = clamp;
                label.TextOverrunBehavior = clamp ? TextServer.OverrunBehavior.TrimEllipsis : TextServer.OverrunBehavior.NoTrimming;
                float width = clamp ? Mathf.Max(maxRight - position.X, 0f) : natural;
                tag.Size = new Vector2(width, tag.Size.Y);
            }
            tag.Position = position;
            tag.PivotOffset = new Vector2(0f, tag.Size.Y);
            tag.Rotation = Mathf.DegToRad(PulpTaggedPanel.TagDegrees);
        }

        private static float NaturalWidth(Control tag, Label label) {
            string text = label.Tr(label.Text);
            if (label.Uppercase) text = text.ToUpperInvariant();
            float font = label.GetThemeFont("font").GetStringSize(text, HorizontalAlignment.Left, -1f, label.GetThemeFontSize("font_size")).X;
            float labelChrome = label.GetThemeStylebox("normal")?.GetMinimumSize().X ?? 0f;
            float plateChrome = tag.GetThemeStylebox("panel")?.GetMinimumSize().X ?? 0f;
            return Mathf.Ceil(font + labelChrome + plateChrome);
        }
    }
}
