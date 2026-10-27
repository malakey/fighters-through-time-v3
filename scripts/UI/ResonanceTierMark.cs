using Godot;

namespace FTT.UI {

    /// <summary>
    /// The tier shown by shape as well as colour: a circle (tier 1), a diamond (tier 2) or a four-point star (tier 3) at
    /// the chip's top-right corner. TG-SCREENS (Temporal Glass): no ink rim (glass draws no black outlines); the shape is
    /// a hairline-edged glyph over the glass chip, its polygon rebuilt only when its size or tier changes, so the draw
    /// allocates nothing.
    /// </summary>
    public partial class ResonanceTierMark : Control {
        private int _tier = 1;
        private Vector2 _builtFor = new(-1f, -1f);
        private int _builtTier = -1;
        private Vector2[] _shape = System.Array.Empty<Vector2>();
        private Vector2[] _outline = System.Array.Empty<Vector2>();

        public int Tier { get => _tier; set { _tier = value; QueueRedraw(); } }

        public ResonanceTierMark() {
            MouseFilter = MouseFilterEnum.Ignore;
            AnchorLeft = 1f; AnchorRight = 1f;
            OffsetLeft = -34f; OffsetRight = -10f; OffsetTop = 8f; OffsetBottom = 32f;
        }

        /// <summary>Corner count of the shape: 0 circle, 4 diamond, 8 star.</summary>
        public static int PointsFor(int tier) => tier <= 1 ? 0 : tier == 2 ? 4 : 8;

        /// <summary>The fill of a tier's glyph: ink2, the accent gold, the light gold.</summary>
        public static Color FillFor(int tier) => tier <= 1 ? UIPalette.Ink2 : tier == 2 ? UIPalette.Accent : UIPalette.Accent2;

        public override void _Notification(int what) {
            if (what == NotificationResized) QueueRedraw();
        }

        private void EnsureShape() {
            if (Size == _builtFor && _tier == _builtTier) return;
            _builtFor = Size;
            _builtTier = _tier;
            int points = PointsFor(_tier);
            if (points == 0) {
                _shape = System.Array.Empty<Vector2>();
                _outline = System.Array.Empty<Vector2>();
                return;
            }
            Vector2 c = Size / 2f;
            float r = Mathf.Min(Size.X, Size.Y) / 2f - 2f;
            _shape = new Vector2[points];
            _outline = new Vector2[points + 1];
            for (int i = 0; i < points; i++) {
                float angle = -Mathf.Pi / 2f + i * Mathf.Tau / points;
                float radius = points == 8 && i % 2 == 1 ? r * 0.45f : r;
                _shape[i] = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                _outline[i] = _shape[i];
            }
            _outline[points] = _shape[0];
        }

        public override void _Draw() {
            EnsureShape();
            Color fill = FillFor(_tier);
            if (_shape.Length == 0) {
                Vector2 c = Size / 2f;
                float r = Mathf.Min(Size.X, Size.Y) / 2f - 4f;
                DrawCircle(c, r, fill);
                DrawArc(c, r + 1f, 0f, Mathf.Tau, 24, UIPalette.Edge, 1.5f, true);
                return;
            }
            DrawColoredPolygon(_shape, fill);
            DrawPolyline(_outline, UIPalette.Edge, 1.5f, true);
        }
    }
}
