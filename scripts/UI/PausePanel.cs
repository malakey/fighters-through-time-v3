using System.Collections.Generic;
using FTT.UI.Motion;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// The PAUSE-A panel (UI_SPEC_GLASS 3.1 and 4.3; first built by UR-PAUSE, restyled for Temporal Glass by TG-PAUSE): the
    /// glass slab G1 with the pause preset's padding (<c>GlassPanelPause</c>: 30 top, 20 sides, 28 bottom), the glass sheen
    /// behind its content and the level-name tag on its top-left corner (the glass tag G2 at (26, -11)).
    ///
    /// <para>It is a <see cref="PanelContainer"/> so the authored node paths <c>Panel/Layout/...</c> that the pause
    /// suites and the focus chain read stay as they were. The tag hangs from a <see cref="PanelTagAnchor"/> child, whose zero
    /// minimum size keeps an overlong tag from widening the panel (it clamps and ellipsizes instead) and which re-places the
    /// tag after a theme change has reached its label. Overlays that reuse the class for its tag and sheen (Settings, the Move
    /// List, the confirm modal, the Dialogue Log, the boot cards) set their own slab variation; the pause preset is only the
    /// default.</para>
    ///
    /// <para><b>Motion</b> (MOT-A rows 4 and 14): <see cref="PlayAppear"/> fades a host dim in to its alpha over 0.16 s
    /// (linear) while the panel fades in and rises 24 px over 0.22 s and its sheen plays the light band once; under Reduced
    /// Temporal Effects the dim and the panel only fade. Every tween keeps running while the tree is paused (a pause menu is
    /// the main user), and nothing here writes <see cref="SceneTree.Paused"/>.</para>
    /// </summary>
    public partial class PausePanel : PanelContainer {

        /// <summary>The PAUSE-A padding (3.1), carried by the <c>GlassPanelPause</c> preset of the shared theme.</summary>
        public const int PadTop = 30, PadSides = 20, PadBottom = 28;

        /// <summary>
        /// Nothing is added for a border: the glass slab's padding is measured from its box, and its 1.5 px edge is drawn
        /// inside the nine-slice art (the pulp plate had a 3 px frame the padding sat inside).
        /// </summary>
        public const int Border = 0;

        /// <summary>The D6 floor of a pause row (3.2: the pause stack's rows are at least 560 wide).</summary>
        public const float RowMinWidth = PulpScreenKit.PauseButtonWidth;

        /// <summary>The D6 cap of a menu stack at UI scale 1.0 (3.2: 700); a pause stack scales it with the UI scale.</summary>
        public const float RowCapAtOne = 700f;

        private readonly PanelTagAnchor _anchor;
        private Tween _dimTween;
        private Tween _panelTween;

        public PausePanel() {
            // The pause preset is the default only. A scene that authors its own slab variation has already set it when the
            // script attaches (a .tscn sets the built-in properties written before "script = ..." first, then constructs
            // this), so it is kept rather than overwritten; code that builds a panel sets its own after this runs.
            if (ThemeTypeVariation.IsEmpty) ThemeTypeVariation = UIPalette.Variations.GlassPanelPause;
            _anchor = new PanelTagAnchor { Name = PulpScreenKit.TagAnchorName };
            AddChild(_anchor);
        }

        /// <summary>The tag's text, already resolved (the level name); empty hides the tag.</summary>
        public string TagText {
            get => _anchor.TagLabel.Text;
            set => _anchor.SetKey(value);
        }

        public Control Tag => _anchor.Tag;

        /// <summary>The panel-in tween of the last <see cref="PlayAppear"/>, or null once it ended or was stopped. Test surface.</summary>
        internal Tween AppearTween => _panelTween != null && _panelTween.IsValid() ? _panelTween : null;

        /// <summary>The dim's fade tween of the last <see cref="PlayAppear"/>, or null. Test surface.</summary>
        internal Tween DimTween => _dimTween != null && _dimTween.IsValid() ? _dimTween : null;

        public override void _Ready() {
            GlassSheen.Attach(this);
        }

        public override void _ExitTree() {
            KillTweens();
        }

        /// <summary>
        /// MOT-A row 4 (with row 14 when <paramref name="dim"/> is given): the panel fades in and rises 24 px over 0.22 s,
        /// its sheen's light band crossing once, while <paramref name="dim"/> (a <c>ScrimPanel</c> dim, or null when the
        /// overlay sits on a host's dim) fades from 0 to <paramref name="dimAlpha"/> over 0.16 s, linear. Reduced Temporal
        /// Effects: the panel fades over 0.15 s with no rise and no band; the dim still fades (a fade is the stable cue).
        /// Both tweens run while the tree is paused. A second call restarts the motion.
        /// </summary>
        public void PlayAppear(CanvasItem dim = null, float dimAlpha = PulpScreenKit.ScrimAlpha) {
            KillTweens();
            if (dim != null && GodotObject.IsInstanceValid(dim) && dim.IsInsideTree()) {
                Color rest = dim.SelfModulate;
                dim.SelfModulate = new Color(rest.R, rest.G, rest.B, 0f);
                _dimTween = PulpMotion.NewTween(dim);
                _dimTween.TweenProperty(dim, "self_modulate:a", dimAlpha, PulpMotionRules.PauseDimSeconds)
                    .SetTrans(Tween.TransitionType.Linear);
            }
            if (IsInsideTree()) _panelTween = PulpMotion.PopIn(this);
        }

        /// <summary>
        /// Ends a running <see cref="PlayAppear"/> on its resting look: the panel opaque and, when given, the dim at
        /// <paramref name="dimAlpha"/>. The panel's position is its container's again on the container's next sort.
        /// </summary>
        public void StopAppear(CanvasItem dim = null, float dimAlpha = PulpScreenKit.ScrimAlpha) {
            PulpMotion.Settle(this);   // the rise's offset goes too, not only its tween (TG-CLOSE)
            KillTweens();
            Modulate = new Color(Modulate.R, Modulate.G, Modulate.B, 1f);
            if (dim != null && GodotObject.IsInstanceValid(dim)) {
                dim.SelfModulate = new Color(dim.SelfModulate.R, dim.SelfModulate.G, dim.SelfModulate.B, dimAlpha);
            }
        }

        private void KillTweens() {
            if (_dimTween != null && _dimTween.IsValid()) _dimTween.Kill();
            if (_panelTween != null && _panelTween.IsValid()) _panelTween.Kill();
            _dimTween = null;
            _panelTween = null;
        }

        /// <summary>
        /// The D6 width of a pause stack (3.2): every row shares the widest required width (the upper-case label, the
        /// paddings and the badge room), at least <see cref="RowMinWidth"/> and at most <paramref name="capAtOne"/> times
        /// the UI scale, so a row that fits at 1.0 keeps its label size at 1.4 instead of shrinking. A pause applies it when
        /// it builds and every time it shows its panel, so a UI-scale change made in Settings is picked up on the way back.
        /// </summary>
        public static float ApplyRowWidths(IReadOnlyList<Button> rows, float capAtOne = RowCapAtOne) {
            if (rows == null || rows.Count == 0) return 0f;
            return PulpButtonDecor.ApplyStackWidth(rows, RowMinWidth, capAtOne * PulpButtonDecor.UiScaleFactor);
        }

        /// <summary>The pause title's padding below it (UI_SPEC_GLASS 2.3 T04 and 4.3 row 3b: 16 px).</summary>
        public const float TitleBottomPadding = 16f;

        private static readonly StringName TitleTrimMeta = "glass_title_trim";

        /// <summary>
        /// Sets the pause title's box to the spec's (UI_SPEC_GLASS 4.3 row 3b): T04 stands on a line height of 1, its 78 px
        /// size, with a 16 px padding below, while a Godot label's box is the font's full height (Cinzel's ascent and descent
        /// run some 31 px past 78), which left the title 15 px and everything under it 27 px lower in the slab than the mock.
        /// The title gets its own copy of its role's font whose negative top and bottom spacing trims the box: half the excess
        /// above; below, half the excess less what the layout's separation leaves of the 16 px padding. It is recomputed
        /// when the theme changes (a UI-scale change); the size, colour and halo stay the role's. Once per label.
        /// </summary>
        public static void TrimTitleBox(Label title) {
            if (title == null || title.HasMeta(TitleTrimMeta)) return;
            Theme theme = UIPalette.LoadTheme();
            string role = title.ThemeTypeVariation.ToString();
            if (theme == null || role.Length == 0 || !theme.HasFont("font", role)) return;
            Font roleFont = theme.GetFont("font", role);
            FontVariation trimmed = roleFont is FontVariation variation && variation.Duplicate() is FontVariation copy
                ? copy
                : new FontVariation { BaseFont = roleFont };
            title.SetMeta(TitleTrimMeta, true);
            void Apply() {
                if (!GodotObject.IsInstanceValid(title)) return;
                int size = title.GetThemeFontSize("font_size");
                float excess = Mathf.Max(0f, roleFont.GetHeight(size) - size);
                int separation = title.GetParent() is BoxContainer box ? box.GetThemeConstant("separation") : 0;
                float padding = Mathf.Max(0f, TitleBottomPadding * PulpButtonDecor.UiScaleFactor - separation);
                int top = Mathf.RoundToInt(excess / 2f);
                int bottom = Mathf.RoundToInt(Mathf.Max(0f, excess / 2f - padding));
                if (trimmed.SpacingTop != -top) trimmed.SpacingTop = -top;
                if (trimmed.SpacingBottom != -bottom) trimmed.SpacingBottom = -bottom;
            }
            Apply();
            title.AddThemeFontOverride("font", trimmed);
            title.ThemeChanged += Apply;
        }
    }
}
