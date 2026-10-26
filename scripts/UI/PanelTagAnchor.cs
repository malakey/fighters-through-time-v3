using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>The tag anchor: a plain control the panel fits to its content area, holding the tag plate.</summary>
    public partial class PanelTagAnchor : Control {
        private readonly PanelContainer _tag;
        private readonly Label _label;

        public PanelTagAnchor() {
            MouseFilter = MouseFilterEnum.Ignore;
            _label = new Label {
                Name = "TagLabel", Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.PanelTag,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _tag = new PanelContainer {
                Name = "Tag", ThemeTypeVariation = UIPalette.Variations.PanelTagPlate,
                MouseFilter = MouseFilterEnum.Ignore, ZIndex = 1, Visible = false
            };
            _tag.AddChild(_label);
            AddChild(_tag);
        }

        public Label TagLabel => _label;
        public Control Tag => _tag;

        public void SetKey(string key) {
            _label.Text = key ?? "";
            _tag.Visible = _label.Text.Length > 0;
            Place();
        }

        public override void _Notification(int what) {
            if (what == NotificationResized || what == NotificationParented || what == NotificationThemeChanged
                || what == NotificationVisibilityChanged) {
                Place();
            }
        }

        private void Place() {
            if (_tag == null) return;
            _tag.ResetSize();
            Vector2 contentOrigin = Vector2.Zero;
            if (GetParent() is PanelContainer panel && panel.GetThemeStylebox("panel") is StyleBox style) {
                contentOrigin = style.GetOffset();
            }
            _tag.Position = PulpScreenKit.TagFromPanelCorner - contentOrigin;
            _tag.PivotOffset = new Vector2(0f, _tag.Size.Y);
            _tag.Rotation = Mathf.DegToRad(PulpTaggedPanel.TagDegrees);
        }
    }
}
