using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// UR-WIDGETS. A panel with its title tag (UI_SPEC 3.1): the first non-tag child is the panel and fills the
    /// rect; the tag sits on top of it at the padding box's (-14, -24), rotated -1.2 degrees, as a
    /// <c>PanelTagPlate</c> holding an upper-case <c>PanelTag</c> label whose <c>Text</c> is the raw key. The
    /// minimum size is the panel's, so it lives inside any box container. An empty <see cref="TagKey"/> hides the tag.
    /// </summary>
    public partial class PulpTaggedPanel : Container {

        public static readonly Vector2 TagOffset = new(-14f, -24f);
        public const float PaddingBoxInset = 3f;
        public const float TagDegrees = -1.2f;

        private readonly PanelContainer _tag;
        private readonly Label _tagLabel;

        public PulpTaggedPanel() {
            Name = "PulpTaggedPanel";
            MouseFilter = MouseFilterEnum.Ignore;
            _tagLabel = new Label {
                Name = "TagLabel", Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.PanelTag, MouseFilter = MouseFilterEnum.Ignore
            };
            _tag = new PanelContainer {
                Name = "Tag", ThemeTypeVariation = UIPalette.Variations.PanelTagPlate, MouseFilter = MouseFilterEnum.Ignore,
                ZIndex = 1, Visible = false
            };
            _tag.AddChild(_tagLabel);
            AddChild(_tag);
        }

        /// <summary>A raw translation key; empty hides the tag.</summary>
        public string TagKey {
            get => _tagLabel.Text;
            set { _tagLabel.Text = value ?? ""; _tag.Visible = _tagLabel.Text.Length > 0; QueueSort(); UpdateMinimumSize(); }
        }

        public Control Tag => _tag;
        public Label TagLabel => _tagLabel;

        /// <summary>The first child that is a visible control and not the tag.</summary>
        public Control Panel {
            get {
                foreach (Node child in GetChildren()) {
                    if (child is Control c && c != _tag && c.Visible) return c;
                }
                return null;
            }
        }

        public override Vector2 _GetMinimumSize() => Panel?.GetCombinedMinimumSize() ?? Vector2.Zero;

        public override void _Notification(int what) {
            if (what == NotificationSortChildren) {
                Control panel = Panel;
                if (panel != null) FitChildInRect(panel, new Rect2(Vector2.Zero, Size));
                _tag.ResetSize();
                _tag.Position = new Vector2(PaddingBoxInset, PaddingBoxInset) + TagOffset;
                _tag.PivotOffset = new Vector2(0f, _tag.Size.Y);
                _tag.Rotation = Mathf.DegToRad(TagDegrees);
            } else if (what == NotificationThemeChanged) {
                QueueSort();
            }
        }
    }
}
