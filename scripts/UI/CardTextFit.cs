using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-SELECT. Shrink-to-fit for the text of the select's fixed-size card art. UI scale is an accessibility setting, and the
    /// cards cannot grow with it, so a name that no longer fits its box is scaled down as a node (never a font-size override),
    /// but never below the size the same text has at UI scale 1.0 (where every name fits by design).
    /// </summary>
    public static class CardTextFit {

        /// <summary>
        /// The node scale for text <paramref name="textWidth"/> wide in a box <paramref name="boxWidth"/> wide at UI scale
        /// <paramref name="uiScale"/>: 1 when it fits, else the fit, floored at the 1.0-scale size, or (TG-SELECT) at
        /// <paramref name="minScaleAtOne"/> of it for text that may not fit even at 1.0 (a stage name).
        /// </summary>
        public static float ScaleFor(float textWidth, float boxWidth, float uiScale, float minScaleAtOne = 1f) {
            if (textWidth <= 0f || boxWidth <= 0f || textWidth <= boxWidth) return 1f;
            float floor = Mathf.Min(1f, minScaleAtOne / Mathf.Max(1f, uiScale));
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
        public static float Fit(Label label, float boxLeft, float boxWidth, bool centred, float minScaleAtOne = 1f) {
            float text = TextWidth(label);
            float scale = ScaleFor(text, boxWidth, UIPalette.AppliedUiScale, minScaleAtOne);
            float width = Mathf.Max(boxWidth, text + 2f);
            label.Size = new Vector2(width, label.Size.Y);
            label.Position = new Vector2(centred ? boxLeft + (boxWidth - width) / 2f : boxLeft, label.Position.Y);
            label.PivotOffset = new Vector2(centred ? width / 2f : 0f, label.Size.Y / 2f);
            label.Scale = new Vector2(scale, scale);
            return scale;
        }

        /// <summary>
        /// TG-SELECT, UI_SPEC_GLASS D10 / AC5: how far a select side-head hero name (T40, 48 px) may shrink, 36 / 48 of its size.
        /// </summary>
        public const float HeroNameShrinkFloor = 0.75f;

        /// <summary>
        /// TG-SELECT (provisional): how far a stage tile's name (T35, 18 px, in 244 px) may shrink at UI scale 1.0, where the
        /// longest ("ALEXANDRIA CHAMBERS", about 268 px) does not fit: to 0.75 of its size, the D10 floor.
        /// </summary>
        public const float StageNameShrinkFloor = 0.75f;

        /// <summary>The steps the two-line shrink tries (scale 1, 0.99 ... the floor).</summary>
        public const float HeroNameShrinkStep = 0.01f;

        /// <summary>
        /// The two-line shrink's floor at UI scale <paramref name="uiScale"/>: <see cref="HeroNameShrinkFloor"/> at 1.0, and the
        /// same 36 px at a larger scale (<c>0.75 / uiScale</c>), so a two-line name is never smaller than it is at 1.0.
        /// </summary>
        public static float HeroNameShrinkFloorAt(float uiScale) => HeroNameShrinkFloor / Mathf.Max(1f, uiScale);

        /// <summary>
        /// The one-line half of the D10 rule for a hero name <paramref name="textWidth"/> wide (at the role's current size) in a
        /// head <paramref name="boxWidth"/> wide: (1, no wrap) when it fits, (the fit, no wrap) while the fit is at least
        /// <see cref="HeroNameShrinkFloor"/>, else (1, wrap): two lines, which <see cref="FitHeroName"/> shrinks further when
        /// the name's words do not break into two lines of the box ("WOLFGANG AMADEUS MOZART" needs three at 48 px in 440).
        /// </summary>
        public static (float Scale, bool Wrap) HeroNameFit(float textWidth, float boxWidth) {
            if (textWidth <= 0f || boxWidth <= 0f || textWidth <= boxWidth) return (1f, false);
            float scale = boxWidth / textWidth;
            return scale >= HeroNameShrinkFloor ? (scale, false) : (1f, true);
        }

        /// <summary>
        /// Applies D10 to <paramref name="label"/> (a child of a non-container at the box's left edge, so its scale sticks): one
        /// line, scaled about its left or right edge and its middle; or two wrapped lines, at the full size when the words break
        /// into two lines of <paramref name="boxWidth"/>, else at the largest scale down to <see cref="HeroNameShrinkFloorAt"/>
        /// at which they break into two lines of the box (the label is laid out <c>box / scale</c> wide and scaled about its top
        /// edge). Returns the height the name takes on screen.
        /// </summary>
        public static float FitHeroName(Label label, float boxWidth, bool alignRight) {
            (float scale, bool wrap) = HeroNameFit(TextWidth(label), boxWidth);
            label.HorizontalAlignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            label.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
            label.MaxLinesVisible = wrap ? 2 : -1;
            // Measured untrimmed: an autowrapping label that trims reports a minimum height of 1, which would leave one line.
            label.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
            label.Scale = Vector2.One;
            float width = boxWidth;
            bool overflow = false;
            if (wrap) {
                // At a large UI scale the two lines may shrink further, to the floor's size at UI scale 1.0 (36 px): the
                // name is then never smaller than it is at 1.0, where "WOLFGANG AMADEUS MOZART" already needs the shrink.
                float floor = HeroNameShrinkFloorAt(UIPalette.AppliedUiScale);
                for (float s = 1f; s >= floor - 0.0001f; s -= HeroNameShrinkStep) {
                    scale = Mathf.Max(s, floor);
                    width = boxWidth / scale;
                    label.Size = new Vector2(width, 0f);
                    if (label.GetLineCount() <= 2) break;
                }
                overflow = label.GetLineCount() > 2;
            }
            label.Size = new Vector2(width, 0f);
            float height = label.GetMinimumSize().Y;
            label.Size = new Vector2(width, height);
            // A one-line name the fit shrank is laid out at its full text width (a label never narrows below its text), so
            // it is placed and pivoted by the width it really has: the scaled text then ends on the box's edge.
            float actual = label.Size.X;
            label.Position = new Vector2(alignRight ? boxWidth - actual : 0f, label.Position.Y);
            label.PivotOffset = new Vector2(alignRight ? actual : 0f, wrap ? 0f : height / 2f);
            label.Scale = new Vector2(scale, scale);
            // Still more than two lines at the floor (no name today): the second line ends in an ellipsis. The size is set
            // already, so the trimmed label's smaller minimum cannot shrink it.
            if (overflow) label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            return wrap ? height * scale : height;
        }

        /// <summary>
        /// TG-SELECT. Fits a word-wrapped name (an ability name under its lens) to a box <paramref name="boxWidth"/> wide in at
        /// most <paramref name="maxLines"/> lines without breaking a word: when the widest word is wider than the box at the
        /// role's current size (a large UI scale), the label is scaled down until it fits, never below its UI-scale-1.0 size
        /// (<see cref="ScaleFor"/>), laid out <c>box / scale</c> wide and scaled about its top-left corner.
        /// <paramref name="label"/> must be the child of a non-container, placed at the box's top-left. A
        /// <paramref name="scaleCap"/> below 1 (a sibling's fit) keeps a row of names at one size. Returns the height it takes
        /// on screen.
        /// </summary>
        public static float FitWrappedName(Label label, float boxWidth, int maxLines, float scaleCap = 1f) {
            float uiScale = UIPalette.AppliedUiScale;
            float floor = uiScale > 1f ? 1f / uiScale : 1f;
            // A hair of room past the measured word: the shaped line can come out a fraction wider than the measure, and a
            // hyphenated word ("MASS-ENERGY") would then break at its hyphen.
            float scale = Mathf.Max(floor, Mathf.Min(scaleCap, ScaleFor(WidestWordWidth(label) + WordFitSlack, boxWidth, uiScale)));
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.MaxLinesVisible = maxLines;
            label.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
            label.Scale = Vector2.One;
            float width = boxWidth / scale;
            label.Size = new Vector2(width, 0f);
            // Still more lines than allowed: shrink on, down to the floor.
            while (label.GetLineCount() > maxLines && scale > floor + 0.0001f) {
                scale = Mathf.Max(floor, scale - HeroNameShrinkStep);
                width = boxWidth / scale;
                label.Size = new Vector2(width, 0f);
            }
            float height = label.GetMinimumSize().Y;
            label.Size = new Vector2(width, height);
            label.Position = Vector2.Zero;
            label.PivotOffset = Vector2.Zero;
            label.Scale = new Vector2(scale, scale);
            if (label.GetLineCount() > maxLines) label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            return height * scale;
        }

        /// <summary>The room <see cref="FitWrappedName"/> leaves past the widest word's measured width.</summary>
        public const float WordFitSlack = 2f;

        /// <summary>The width of the label's widest space-separated word at its role's current size (translated and upper-cased as drawn).</summary>
        public static float WidestWordWidth(Label label) {
            Font font = label.GetThemeFont("font");
            if (font == null || string.IsNullOrEmpty(label.Text)) return 0f;
            int size = label.GetThemeFontSize("font_size");
            string text = label.AutoTranslateMode == Node.AutoTranslateModeEnum.Disabled ? label.Text : label.Tr(label.Text);
            if (label.Uppercase) text = text.ToUpperInvariant();
            float widest = 0f;
            foreach (string word in text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries)) {
                widest = Mathf.Max(widest, font.GetStringSize(word, HorizontalAlignment.Left, -1f, size).X);
            }
            return widest;
        }
    }
}
