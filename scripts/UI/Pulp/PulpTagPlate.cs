using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// The one place a panel tag is built and placed (UI_SPEC_GLASS 3.1, G2): a <c>PanelTagPlate</c> (the kit's
    /// <c>glass_tag</c>: a 2 px <c>edge-hi</c> bar on the left, the gold wash, the slanted right end) holding an upper-case
    /// <c>PanelTag</c> label, anchored at the panel's (26, -11) with no tilt. Three hosts wear it
    /// (<see cref="PulpTaggedPanel"/>, <c>PanelTagAnchor</c> and, through an anchor, <c>PausePanel</c>); they differ only in
    /// where the tag hangs, because their node paths differ and tests pin those paths. The main menu's authored
    /// <c>TagHolder/Tag</c> nodes stay scene-authored for the same reason and use the same variations.
    ///
    /// <para>The clamp rule (UR-POLISH2) is kept: a tag never comes closer than <see cref="RightMargin"/> to its panel's
    /// right edge, and its label ellipsizes. A host places the tag again once a theme change has reached the label (its
    /// font size is cached until then), never from inside the change itself.</para>
    /// </summary>
    public static class PulpTagPlate {

        /// <summary>The tag's top-left relative to its panel's top-left (3.1: chamfer 18 + 8, -11 for the 24 px tag).</summary>
        public static readonly Vector2 PanelCornerOffset = new(26f, -11f);

        /// <summary>The spec's tag height (3 + T07 18 + 3). The theme plate is taller (Outfit's natural line); the tag keeps the spec's centre.</summary>
        public const float SpecHeight = 24f;

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
        /// Shrinks the tag to its text and puts its left edge at <paramref name="position"/>.X, centred on the spec's 24 px
        /// tag whose top is <paramref name="position"/>.Y; no tilt. When <paramref name="maxRight"/> is given (the panel's
        /// right edge less <see cref="RightMargin"/>, in the tag's parent space) a tag whose natural width would run past
        /// it is clamped to fit and its label ellipsized.
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
            tag.Position = new Vector2(position.X, position.Y + Mathf.Round((SpecHeight - tag.Size.Y) / 2f));
            tag.PivotOffset = new Vector2(0f, tag.Size.Y);
            tag.Rotation = Mathf.DegToRad(PulpTaggedPanel.TagDegrees);
        }

        /// <summary>The tag plate's natural width for its label's current text and font size.</summary>
        public static float NaturalWidth(Control tag, Label label) {
            string text = label.Tr(label.Text);
            if (label.Uppercase) text = text.ToUpperInvariant();
            float font = label.GetThemeFont("font").GetStringSize(text, HorizontalAlignment.Left, -1f, label.GetThemeFontSize("font_size")).X;
            float labelChrome = label.GetThemeStylebox("normal")?.GetMinimumSize().X ?? 0f;
            float plateChrome = tag.GetThemeStylebox("panel")?.GetMinimumSize().X ?? 0f;
            return Mathf.Ceil(font + labelChrome + plateChrome);
        }
    }
}
