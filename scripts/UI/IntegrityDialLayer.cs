using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// TG-HUDS (Temporal Glass, UI_SPEC_GLASS 3.0 G9 and 3.4.4; first written by UR-HUDS as the pulp stopwatch bake). One of
    /// the two drawn layers of the thin-ring Integrity dial, in the 112 px dial's own box:
    /// <list type="bullet">
    /// <item><see cref="DialPart.Face"/>, under the wedge: the <c>glass_dial_radial</c> disc (rgba(glow, .22) at 34 % 26 %
    /// to rgba(5, 9, 24, .90) at 70 %), an 18 px inner glow at .20, a 22 px outer glow at .16 and the 1.5 px <c>edge</c>
    /// ring just outside the disc;</item>
    /// <item><see cref="DialPart.Ticks"/>, over the wedge: 12 marks 2 degrees wide, centred on 0, 30, ... 330 degrees, from
    /// radius 43.64 to 52.64, in <c>ink2</c>.</item>
    /// </list>
    /// The wedge between them is the HUD's <c>ClockWedge</c> (a <see cref="RadialProgress"/> ring from radius 23.15 to
    /// 43.68). Geometry is built once per size (<see cref="GeometryBuilds"/>); a draw allocates nothing.
    /// </summary>
    public partial class IntegrityDialLayer : Control {

        public enum DialPart { Face, Ticks }

        /// <summary>The dial's diameter at 100 % (G9).</summary>
        public const float DialSize = 112f;
        /// <summary>The wedge annulus: a disc inset 11 % (radius 43.68), the wedge from 53 % of it (23.15) to its rim.</summary>
        public const float WedgeOuterRadius = DialSize / 2f * (1f - 2f * 0.11f), WedgeInnerRadius = WedgeOuterRadius * 0.53f;
        /// <summary>The tick marks' inner and outer radius and angular width.</summary>
        public const float TickInnerRadius = 43.64f, TickOuterRadius = 52.64f, TickDegrees = 2f;
        public const int TickCount = 12;
        public const float InnerGlowWidth = 18f;
        public const int OuterGlowSize = 22;
        private const int CircleSegments = 64;
        private static readonly Color InnerGlowColor = new(UIPalette.Accent, 0.20f);
        private static readonly Color OuterGlowColor = new(UIPalette.Accent, 0.16f);

        private readonly DialPart _part;
        private readonly StyleBoxFlat _glowBox = new() { DrawCenter = false, CornerDetail = 16, ShadowSize = OuterGlowSize };
        private readonly Vector2[] _disc = new Vector2[CircleSegments];
        private readonly Vector2[] _discUvs = new Vector2[CircleSegments];
        private readonly Color[] _discColors = new Color[CircleSegments];
        private readonly Vector2[] _glowPoints = new Vector2[CircleSegments * 2];
        private readonly Color[] _glowColors = new Color[CircleSegments * 2];
        private readonly int[] _glowIndices = new int[CircleSegments * 6];
        private readonly Vector2[][] _ticks = new Vector2[TickCount][];
        private Vector2 _builtFor = new(-1f, -1f);

        public IntegrityDialLayer() : this(DialPart.Face) { }

        public IntegrityDialLayer(DialPart part) {
            _part = part;
            Name = part == DialPart.Face ? "DialFace" : "DialTicks";
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            _glowBox.ShadowColor = OuterGlowColor;
            for (int i = 0; i < TickCount; i++) _ticks[i] = new Vector2[4];
            for (int i = 0; i < CircleSegments; i++) {
                int a = i, b = (i + 1) % CircleSegments, c = CircleSegments + i, d = CircleSegments + (i + 1) % CircleSegments;
                _glowIndices[i * 6] = a; _glowIndices[i * 6 + 1] = b; _glowIndices[i * 6 + 2] = d;
                _glowIndices[i * 6 + 3] = a; _glowIndices[i * 6 + 4] = d; _glowIndices[i * 6 + 5] = c;
            }
            CustomMinimumSize = new Vector2(DialSize, DialSize);
            Size = CustomMinimumSize;
        }

        public DialPart Part => _part;

        /// <summary>How many times the polygons were built (once per size).</summary>
        public int GeometryBuilds { get; private set; }

        /// <summary>The tick quads (read-only), each from radius 43.64 to 52.64 and 2 degrees wide, scaled with the box.</summary>
        public Vector2[][] TickPolygons { get { EnsureGeometry(); return _ticks; } }

        public override void _Notification(int what) {
            if (what == NotificationResized) QueueRedraw();
        }

        private void EnsureGeometry() {
            Vector2 size = Size.X > 0f && Size.Y > 0f ? Size : new Vector2(DialSize, DialSize);
            if (size == _builtFor) return;
            _builtFor = size;
            GeometryBuilds++;
            float scale = Mathf.Min(size.X, size.Y) / DialSize;
            Vector2 centre = size / 2f;
            float radius = DialSize / 2f * scale;
            var box = new Rect2(centre - Vector2.One * radius, Vector2.One * radius * 2f);
            GlassGeometry.Circle(centre, radius, CircleSegments, box, _disc, _discUvs);
            for (int i = 0; i < CircleSegments; i++) _discColors[i] = Colors.White;
            float inner = Mathf.Max(0f, radius - InnerGlowWidth * scale);
            for (int i = 0; i < CircleSegments; i++) {
                float angle = Mathf.Tau * i / CircleSegments;
                var direction = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
                _glowPoints[i] = centre + direction * radius;
                _glowPoints[CircleSegments + i] = centre + direction * inner;
                _glowColors[i] = InnerGlowColor;
                _glowColors[CircleSegments + i] = new Color(InnerGlowColor, 0f);
            }
            int corner = Mathf.RoundToInt(radius);
            // The outer glow follows the dial's size (22 at 112; the compact Story HUD's 64 px dial draws 13).
            _glowBox.ShadowSize = Mathf.Max(1, Mathf.RoundToInt(OuterGlowSize * scale));
            _glowBox.CornerRadiusTopLeft = _glowBox.CornerRadiusTopRight = corner;
            _glowBox.CornerRadiusBottomLeft = _glowBox.CornerRadiusBottomRight = corner;
            float half = Mathf.DegToRad(TickDegrees) / 2f;
            for (int k = 0; k < TickCount; k++) {
                float angle = Mathf.Tau * k / TickCount;
                // Clockwise from 12 o'clock: (sin, -cos).
                Vector2 a0 = new(Mathf.Sin(angle - half), -Mathf.Cos(angle - half));
                Vector2 a1 = new(Mathf.Sin(angle + half), -Mathf.Cos(angle + half));
                _ticks[k][0] = centre + a0 * TickInnerRadius * scale;
                _ticks[k][1] = centre + a0 * TickOuterRadius * scale;
                _ticks[k][2] = centre + a1 * TickOuterRadius * scale;
                _ticks[k][3] = centre + a1 * TickInnerRadius * scale;
            }
        }

        public override void _Draw() {
            EnsureGeometry();
            if (_part == DialPart.Ticks) {
                foreach (Vector2[] tick in _ticks) DrawColoredPolygon(tick, UIPalette.Ink2);
                return;
            }
            Vector2 centre = _builtFor / 2f;
            float radius = Mathf.Min(_builtFor.X, _builtFor.Y) / 2f;
            DrawStyleBox(_glowBox, new Rect2(centre - Vector2.One * radius, Vector2.One * radius * 2f));
            Texture2D fill = PulpKit.Texture("glass_dial_radial");
            if (fill != null) DrawPolygon(_disc, _discColors, _discUvs, fill);
            else DrawColoredPolygon(_disc, UIPalette.DialFill);
            RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), _glowIndices, _glowPoints, _glowColors);
            DrawArc(centre, radius + GlassGeometry.EdgeWidth / 2f, 0f, Mathf.Tau, 96, UIPalette.Edge, GlassGeometry.EdgeWidth, true);
        }
    }
}
