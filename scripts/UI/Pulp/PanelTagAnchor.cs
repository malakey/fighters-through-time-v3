using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// The tag anchor: a plain control the panel fits to its content area, holding the tag plate positioned back out to
    /// the panel's corner (UI_SPEC_GLASS 3.1). Its minimum size is zero, so the tag never widens the panel; the clamp keeps
    /// it inside instead.
    ///
    /// <para>Codex finding 2 of REVIEW-UI-PULP-CODEX-3: a theme change reaches this control before the tag's label, whose
    /// font size stays cached until the label itself is notified, so placing the tag from inside the change measured the
    /// old size (a tag that fits at UI scale 1.0 and overflows at 1.4 stayed unclipped, and the reverse stayed clipped).
    /// A theme change now queues one deferred placement, which runs after the change has propagated.</para>
    /// </summary>
    public partial class PanelTagAnchor : Control {
        private readonly PanelContainer _tag;
        private readonly Label _label;
        private bool _placeQueued;

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
            if (what == NotificationThemeChanged) {
                QueuePlace();
            } else if (what == NotificationResized || what == NotificationParented || what == NotificationVisibilityChanged) {
                Place();
            }
        }

        private void QueuePlace() {
            if (_placeQueued) return;
            _placeQueued = true;
            Callable.From(PlaceQueued).CallDeferred();
        }

        private void PlaceQueued() {
            _placeQueued = false;
            if (IsInstanceValid(this) && IsInsideTree()) Place();
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
