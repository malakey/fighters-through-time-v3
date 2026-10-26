using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>The tag anchor: a plain control the panel fits to its content area, holding the tag plate.</summary>
    public partial class PanelTagAnchor : Control {
        private readonly PanelContainer _tag;
        private readonly Label _label;

        public PanelTagAnchor() {
            MouseFilter = MouseFilterEnum.Ignore;
            _tag = PulpTagPlate.Create(out _label);
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
            Vector2 contentOrigin = Vector2.Zero;
            if (GetParent() is PanelContainer panel && panel.GetThemeStylebox("panel") is StyleBox style) {
                contentOrigin = style.GetOffset();
            }
            float maxRight = float.PositiveInfinity;
            if (GetParent() is PanelContainer host) maxRight = host.Size.X - contentOrigin.X - PulpTagPlate.RightMargin;
            PulpTagPlate.Place(_tag, PulpScreenKit.TagFromPanelCorner - contentOrigin, maxRight);
        }
    }
}
