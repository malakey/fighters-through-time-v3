using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-PAUSE. The PAUSE-A panel (UI_SPEC 3.1 and 4.3): the <c>InkPanel</c> plate with the pause padding
    /// (30 top, 20 sides, 28 bottom inside the 3 px border), the glass sheen behind its content and the level-name tag on
    /// its top-left corner (the <c>PanelTagPlate</c>, an upper-case <c>PanelTag</c> label).
    ///
    /// <para>It is a <see cref="PanelContainer"/> so the authored node paths <c>Panel/Layout/...</c> that the pause
    /// suites and the focus chain read stay as they were. TG-WIDGETS cross-edit (Temporal Glass, step 1): the sheen
    /// (<see cref="Pulp.GlassSheen"/>) replaces the halftone corner, and the tag hangs from a <see cref="PanelTagAnchor"/>
    /// child, whose zero minimum size keeps an overlong tag from widening the panel (it clamps and ellipsizes instead) and
    /// which re-places the tag after a theme change has reached its label. Restyling the panel is TG-PAUSE's.</para>
    /// </summary>
    public partial class PausePanel : PanelContainer {

        public const int PadTop = 30, PadSides = 20, PadBottom = 28, Border = 3;

        private readonly PanelTagAnchor _anchor;

        public PausePanel() {
            ThemeTypeVariation = UIPalette.Variations.InkPanel;
            _anchor = new PanelTagAnchor { Name = PulpScreenKit.TagAnchorName };
            AddChild(_anchor);
        }

        /// <summary>The tag's text, already resolved (the level name); empty hides the tag.</summary>
        public string TagText {
            get => _anchor.TagLabel.Text;
            set => _anchor.SetKey(value);
        }

        public Control Tag => _anchor.Tag;

        public override void _Ready() {
            // The theme's slab carries the default panel padding; the pause panel pads 30 / 20 / 28. (TG-THEME cross-edit:
            // any StyleBox, since the step-1 glass slab is a StyleBoxFlat rather than the pulp nine-slice.)
            if (GetThemeStylebox("panel") is StyleBox shared && !HasThemeStyleboxOverride("panel")) {
                var own = (StyleBox)shared.Duplicate();
                own.ContentMarginLeft = Border + PadSides;
                own.ContentMarginRight = Border + PadSides;
                own.ContentMarginTop = Border + PadTop;
                own.ContentMarginBottom = Border + PadBottom;
                AddThemeStyleboxOverride("panel", own);
            }
            Pulp.GlassSheen.Attach(this);
        }
    }
}
