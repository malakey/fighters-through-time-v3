using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// A panel with its title tag (UI_SPEC_GLASS 3.1, G2): the first non-tag child is the panel and fills the rect; the tag
    /// sits on top of it at the panel's (26, -11) with no tilt, a <c>PanelTagPlate</c> holding an upper-case
    /// <c>PanelTag</c> label whose <c>Text</c> is the raw key. The minimum size is the panel's, so it lives inside any box
    /// container. An empty <see cref="TagKey"/> hides the tag. A theme change re-places the tag once it has reached the
    /// tag's label (one coalesced deferred placement), so the clamp never measures a stale font size.
    /// </summary>
    public partial class PulpTaggedPanel : Container {

        /// <summary>The tag's offset from the panel's top-left corner (3.1: chamfer 18 + 8, -11).</summary>
        public static readonly Vector2 TagOffset = PulpTagPlate.PanelCornerOffset;
        /// <summary>The glass tag is not tilted (the pulp tag leaned -1.2 degrees).</summary>
        public const float TagDegrees = 0f;

        private readonly PanelContainer _tag;
        private readonly Label _tagLabel;
        private bool _placeQueued;

        public PulpTaggedPanel() {
            Name = "PulpTaggedPanel";
            MouseFilter = MouseFilterEnum.Ignore;
            _tag = PulpTagPlate.Create(out _tagLabel);
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
                // Indexed, not GetChildren(): this runs on every sort and minimum-size query and an undisposed engine
                // collection wrapper is a teardown-crash risk (AGENTS.md).
                int count = GetChildCount();
                for (int i = 0; i < count; i++) {
                    if (GetChild(i) is Control c && c != _tag && c.Visible) return c;
                }
                return null;
            }
        }

        public override Vector2 _GetMinimumSize() => Panel?.GetCombinedMinimumSize() ?? Vector2.Zero;

        private void PlaceTag() =>
            PulpTagPlate.Place(_tag, TagOffset, Size.X - PulpTagPlate.RightMargin);

        public override void _Notification(int what) {
            if (what == NotificationSortChildren) {
                Control panel = Panel;
                if (panel != null) FitChildInRect(panel, new Rect2(Vector2.Zero, Size));
                PlaceTag();
            } else if (what == NotificationThemeChanged) {
                // The change reaches this container before the tag's label: place once it has propagated.
                QueueSort();
                QueuePlace();
            }
        }

        private void QueuePlace() {
            if (_placeQueued) return;
            _placeQueued = true;
            Callable.From(PlaceQueued).CallDeferred();
        }

        private void PlaceQueued() {
            _placeQueued = false;
            if (IsInstanceValid(this) && IsInsideTree()) PlaceTag();
        }
    }
}
