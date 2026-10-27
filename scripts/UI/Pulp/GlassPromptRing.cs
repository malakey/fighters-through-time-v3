using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// TG-WIDGETS. The pad prompt glyph's circle (UI_SPEC_GLASS 3.4.4): a 2 px ring, no fill, in <see cref="RingColor"/>
    /// (ink in a prompt bar, the label colour inside a focused button), filling its rect. The letter is a Label beside it in
    /// the glyph. Its points are cached per size; nothing allocates per draw.
    /// </summary>
    public partial class GlassPromptRing : Control {

        public const float RingWidth = 2f;
        private const int Segments = 48;

        private Color _color = UIPalette.Ink;
        private readonly Vector2[] _points = new Vector2[Segments + 1];
        private Vector2 _builtFor = new(-1f, -1f);

        public GlassPromptRing() {
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public Color RingColor { get => _color; set { _color = value; QueueRedraw(); } }

        /// <summary>How many times the ring's points were rebuilt (only a size change rebuilds them).</summary>
        public int GeometryBuilds { get; private set; }

        public override void _Notification(int what) {
            if (what == NotificationResized) QueueRedraw();
        }

        public override void _Draw() {
            if (Size != _builtFor) {
                _builtFor = Size;
                GeometryBuilds++;
                float radius = Mathf.Min(Size.X, Size.Y) / 2f - RingWidth / 2f;
                Vector2 centre = Size / 2f;
                for (int i = 0; i <= Segments; i++) {
                    float a = Mathf.Tau * i / Segments;
                    _points[i] = centre + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * radius;
                }
            }
            DrawPolyline(_points, _color, RingWidth, true);
        }
    }
}
