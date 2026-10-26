using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-SCREENS. The few helpers the remaining screens share, built only from the UR-WIDGETS parts
    /// (nothing here owns a look): a panel tag that does not change the panel's node path, the halftone
    /// corner, a bottom-right prompt bar, banner-button decoration for a whole container, and a leading
    /// icon for a Label whose text must stay one string (the results tests read it).
    /// </summary>
    public static partial class PulpScreenKit {

        /// <summary>The widest decorated button that sits in a horizontal row (the 480 px Move List card button).</summary>
        public const float RowButtonMaxWidth = 480f;

        /// <summary>The theme's focus plate draws this far past the button's right and left edges (GlassBtn expand margins 10 and 6).</summary>
        public const float FocusPlateExpandRight = 10f, FocusPlateExpandLeft = 6f;

        /// <summary>
        /// The least gap between two neighbouring decorated buttons in a horizontal row for the focused one's pose to clear
        /// the other: the pose's own slide and scale growth, both plate margins and 2 px of air. Derived from the pose's
        /// numbers (<see cref="FTT.UI.Motion.PulpMotionRules"/>), not chosen.
        /// </summary>
        public static readonly float FocusRowGap = FTT.UI.Motion.PulpMotionRules.FocusTranslateX
            + (FTT.UI.Motion.PulpMotionRules.FocusScale - 1f) / 2f * RowButtonMaxWidth
            + FocusPlateExpandRight + FocusPlateExpandLeft + 2f;

        /// <summary>Widens a horizontal row's separation to at least <see cref="FocusRowGap"/>; a vertical stack needs nothing.</summary>
        public static void SeparateFocusRow(BoxContainer row) {
            if (row == null || row.Vertical) return;
            int wanted = Mathf.CeilToInt(FocusRowGap);
            if (row.GetThemeConstant("separation") < wanted) row.AddThemeConstantOverride("separation", wanted);
        }

        public const string TagAnchorName = "PanelTagAnchor";
        public const string PromptBarName = "ScreenPromptBar";

        /// <summary>The tag's offset from the panel rectangle's corner, as <c>PulpTaggedPanel</c> places it.</summary>
        public static readonly Vector2 TagFromPanelCorner = PulpTaggedPanel.TagOffset
            + new Vector2(PulpTaggedPanel.PaddingBoxInset, PulpTaggedPanel.PaddingBoxInset);

        /// <summary>
        /// Draws the panel's title tag without wrapping the panel (so its node path and its parent are
        /// untouched): a plain <see cref="Control"/> child, which a <see cref="PanelContainer"/> fits to its
        /// content area, holds a <c>PanelTagPlate</c> positioned back out to the panel's corner. An empty
        /// key hides the tag. Idempotent: a second call re-keys the first tag.
        /// </summary>
        public static Label AddTag(PanelContainer panel, string tagKey) {
            if (panel == null) return null;
            var anchor = panel.GetNodeOrNull<PanelTagAnchor>(TagAnchorName);
            if (anchor == null) {
                anchor = new PanelTagAnchor { Name = TagAnchorName };
                panel.AddChild(anchor);
            }
            anchor.SetKey(tagKey);
            return anchor.TagLabel;
        }

        /// <summary>The halftone corner (behind the content) and, when a key is given, the title tag.</summary>
        public static void DressPanel(PanelContainer panel, string tagKey = null, bool halftone = true,
            bool buttonInset = false) {
            if (panel == null) return;
            if (buttonInset) InsetRight(panel);
            if (halftone && panel.GetNodeOrNull<HalftoneCorner>("HalftoneCorner") == null) HalftoneCorner.Attach(panel);
            if (!string.IsNullOrEmpty(tagKey)) AddTag(panel, tagKey);
        }

        /// <summary>The right content margin that keeps a focused banner button's pose (+16 px, tilt) inside the plate.</summary>
        public const float ButtonStackRightInset = 40f;

        /// <summary>Gives a panel its own copy of its plate with at least <see cref="ButtonStackRightInset"/> on the right.</summary>
        public static void InsetRight(PanelContainer panel) {
            if (panel.GetThemeStylebox("panel") is not StyleBox plate) return;
            if (plate.GetContentMargin(Side.Right) >= ButtonStackRightInset) return;
            var copy = (StyleBox)plate.Duplicate();
            copy.ContentMarginRight = ButtonStackRightInset;
            panel.AddThemeStyleboxOverride("panel", copy);
        }

        /// <summary>
        /// A <see cref="PulpPromptBar"/> pinned bottom-right of <paramref name="host"/> (UI_SPEC 3.7: 70 px
        /// from the right edge, 40 px from the bottom). Returns it.
        /// </summary>
        public static PulpPromptBar AddPromptBar(Control host, params (string Action, string CaptionKey)[] prompts) {
            if (host == null) return null;
            var bar = new PulpPromptBar { Name = PromptBarName };
            foreach ((string action, string caption) in prompts) bar.AddPrompt(action, caption);
            host.AddChild(bar);
            bar.AnchorLeft = 1f;
            bar.AnchorRight = 1f;
            bar.AnchorTop = 1f;
            bar.AnchorBottom = 1f;
            bar.GrowHorizontal = Control.GrowDirection.Begin;
            bar.GrowVertical = Control.GrowDirection.Begin;
            bar.OffsetRight = -70f;
            bar.OffsetBottom = -40f;
            return bar;
        }

        /// <summary>
        /// Keeps a long, wrapping button label clear of the focused row's prompt badge: every plate the
        /// button draws gets its own copy with the right content margin raised by <paramref name="pixels"/>.
        /// </summary>
        public static void ReserveBadgeSpace(Button button, float pixels = 84f) {
            foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" }) {
                if (button.GetThemeStylebox(state) is not StyleBox plate) continue;
                var copy = (StyleBox)plate.Duplicate();
                copy.ContentMarginRight = Mathf.Max(0f, plate.GetContentMargin(Side.Right)) + pixels;
                button.AddThemeStyleboxOverride(state, copy);
            }
        }

        /// <summary>Gives every <see cref="Button"/> under <paramref name="root"/> the banner-button decor.</summary>
        public static void DecorateButtons(Node root, bool compact = false) {
            if (root == null) return;
            for (int index = 0; index < root.GetChildCount(); index++) {
                Node child = root.GetChild(index);
                if (child is Button button && button.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor") == null
                    && !HasDecor(button)) {
                    PulpButtonDecor.Apply(button, compact);
                }
                DecorateButtons(child, compact);
            }
        }

        private static bool HasDecor(Button button) {
            for (int index = 0; index < button.GetChildCount(); index++) {
                if (button.GetChild(index) is PulpButtonDecor) return true;
            }
            return false;
        }

        /// <summary>
        /// Puts a kit icon at the left of a Label while the Label's <c>Text</c> stays one string: the left
        /// content margin makes room and the icon is a child centred on the line. Idempotent per label.
        /// </summary>
        public static PulpIcon AddLeadingIcon(Label label, string iconName, Color tint, float size = 28f) {
            if (label == null) return null;
            if (label.GetNodeOrNull<PulpIcon>("LeadingIcon") is { } existing) {
                existing.IconName = iconName;
                existing.Tint = tint;
                return existing;
            }
            float gap = 10f;
            var box = new StyleBoxEmpty { ContentMarginLeft = size + gap };
            label.AddThemeStyleboxOverride("normal", box);
            PulpIcon icon = PulpIcon.Create(iconName, tint, size);
            icon.Name = "LeadingIcon";
            label.AddChild(icon);
            icon.AnchorTop = 0.5f;
            icon.AnchorBottom = 0.5f;
            icon.OffsetLeft = 0f;
            icon.OffsetRight = size;
            icon.OffsetTop = -size / 2f;
            icon.OffsetBottom = size / 2f;
            return icon;
        }
    }
}
