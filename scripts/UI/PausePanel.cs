using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-PAUSE. The PAUSE-A panel (UI_SPEC 3.1 and 4.3): the dark <c>InkPanel</c> plate with the pause padding
    /// (30 top, 20 sides, 28 bottom inside the 3 px border), the halftone corner behind its content and the
    /// level-name tag on its top-left corner (the <c>PanelTagPlate</c>, an upper-case <c>PanelTag</c> label,
    /// rotated -1.2 degrees, at the padding box's (-14, -24)).
    ///
    /// <para>It is a <see cref="PanelContainer"/> so the authored node paths <c>Panel/Layout/...</c> that the pause
    /// suites and the focus chain read stay as they were; the tag is the one child the sort lays out of flow.
    /// <see cref="FTT.UI.Pulp.PulpTaggedPanel"/> would put the plate one node deeper, which is why the tag is drawn
    /// here with the same constants rather than through it.</para>
    /// </summary>
    public partial class PausePanel : PanelContainer {

        public const int PadTop = 30, PadSides = 20, PadBottom = 28, Border = 3;

        private readonly PanelContainer _tag;
        private readonly Label _tagLabel;

        public PausePanel() {
            ThemeTypeVariation = UIPalette.Variations.InkPanel;
            _tagLabel = new Label {
                Name = "TagLabel", Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.PanelTag,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _tag = new PanelContainer {
                Name = "Tag", ThemeTypeVariation = UIPalette.Variations.PanelTagPlate, MouseFilter = MouseFilterEnum.Ignore,
                ZIndex = 1, Visible = false
            };
            _tag.AddChild(_tagLabel);
            AddChild(_tag);
        }

        /// <summary>The tag's text, already resolved (the level name); empty hides the tag.</summary>
        public string TagText {
            get => _tagLabel.Text;
            set { _tagLabel.Text = value ?? ""; _tag.Visible = _tagLabel.Text.Length > 0; QueueSort(); }
        }

        public Control Tag => _tag;

        public override void _Ready() {
            // The theme's ink panel carries 22 / 18 / 28 / 28 content margins; the pause panel pads 30 / 20 / 28.
            if (GetThemeStylebox("panel") is StyleBoxTexture shared && !HasThemeStyleboxOverride("panel")) {
                var own = (StyleBoxTexture)shared.Duplicate();
                own.ContentMarginLeft = Border + PadSides;
                own.ContentMarginRight = Border + PadSides;
                own.ContentMarginTop = Border + PadTop;
                own.ContentMarginBottom = Border + PadBottom;
                AddThemeStyleboxOverride("panel", own);
            }
            Pulp.HalftoneCorner.Attach(this);
        }

        public override void _Notification(int what) {
            if (what != NotificationSortChildren || _tag == null) return;
            _tag.ResetSize();
            _tag.Position = new Vector2(Border, Border) + Pulp.PulpTaggedPanel.TagOffset;
            _tag.PivotOffset = new Vector2(0f, _tag.Size.Y);
            _tag.Rotation = Mathf.DegToRad(Pulp.PulpTaggedPanel.TagDegrees);
        }
    }
}
