using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// TG-HUDS (Temporal Glass, UI_SPEC_GLASS 3.0 G7 and 3.4.4; first written by UR-HUDS as the pulp sticker badge). The
    /// 46 px glass badge of the Story HUD's time tools (the rewind counter's hourglass, the Time Freeze snowflake, the Act
    /// III Beacon): the <c>glass_badge_radial</c> disc with a 12 px inner glow and a 14 px outer glow, a 1.5 px
    /// <c>edge</c> ring just outside it, the glyph at 62 % (the bold weight below 30 px) in its tint, and an optional
    /// duration ring: 3 px with its outer edge 5 px outside the badge (Time Freeze), clockwise from 12 o'clock. Written by
    /// the HUD; it never ticks. The disc, its UVs and the glow annulus are built once per size; a draw allocates nothing.
    /// </summary>
    public partial class HudBadge : Control {

        /// <summary>The duration ring's width (3 px) and how far its outer edge sits outside the badge (5 px).</summary>
        public const float RingWidth = 3f, RingOutset = 5f;
        public const float GlyphShare = 0.62f;
        /// <summary>The CSS inset glow: rgba(glow, .18) at the rim fading to 0 over 12 px.</summary>
        public const float InnerGlowWidth = 12f;
        public const int OuterGlowSize = 14;
        private const int CircleSegments = 48;
        private static readonly Color InnerGlowColor = new(UIPalette.Accent, 0.18f);
        private static readonly Color OuterGlowColor = new(UIPalette.Accent, 0.12f);

        private readonly PulpIcon _icon;
        private float _size = 46f;
        private float _ring;
        private Color _ringColor = UIPalette.Accent;
        private float _ringWidth = RingWidth;
        private float _ringOutset = RingOutset;
        private float _iconShare = GlyphShare;
        private readonly StyleBoxFlat _glowBox = new() { DrawCenter = false, CornerDetail = 16, ShadowSize = OuterGlowSize };
        private readonly Vector2[] _disc = new Vector2[CircleSegments];
        private readonly Vector2[] _discUvs = new Vector2[CircleSegments];
        private readonly Color[] _discColors = new Color[CircleSegments];
        private readonly Vector2[] _glowPoints = new Vector2[CircleSegments * 2];
        private readonly Color[] _glowColors = new Color[CircleSegments * 2];
        private int[] _glowIndices;
        private float _builtFor = -1f;

        public HudBadge() {
            Name = "HudBadge";
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            _glowBox.ShadowColor = OuterGlowColor;
            _icon = new PulpIcon { Name = "Glyph", Tint = UIPalette.Accent2 };
            AddChild(_icon);
            Relayout();
        }

        /// <summary>The disc's diameter (46 for the time tools).</summary>
        public float BadgeSize { get => _size; set { _size = value; Relayout(); } }
        public string IconName { get => _icon.IconName; set => _icon.IconName = value; }
        public Color Tint { get => _icon.Tint; set => _icon.Tint = value; }
        /// <summary>Remaining share of a ring around the disc, 0 (no ring) to 1.</summary>
        public float Ring { get => _ring; set { if (!Mathf.IsEqualApprox(_ring, value)) { _ring = Mathf.Clamp(value, 0f, 1f); QueueRedraw(); } } }
        public Color RingColor { get => _ringColor; set { if (_ringColor == value) return; _ringColor = value; QueueRedraw(); } }
        public PulpIcon Glyph => _icon;

        /// <summary>The duration ring's width (3 by default; the compact Story HUD's 26 px badges use 2).</summary>
        public float RingThickness { get => _ringWidth; set { _ringWidth = value; QueueRedraw(); } }

        /// <summary>How far the ring's outer edge sits outside the badge (5 by default; the compact Story HUD uses 3).</summary>
        public float RingOutsetPixels { get => _ringOutset; set { _ringOutset = value; QueueRedraw(); } }

        /// <summary>The glyph as a share of the badge (0.62 by default; the compact Story HUD's Defy seal draws 20 in 28).</summary>
        public float IconShare { get => _iconShare; set { _iconShare = value; Relayout(); } }

        /// <summary>The duration ring's centre-line radius as drawn (this badge's ring; <see cref="RingRadius"/> for the defaults).</summary>
        public float DrawnRingRadius => _size / 2f + _ringOutset - _ringWidth / 2f;

        /// <summary>The glows' scale: 1 at 46 px and above, the badge over 46 below it (the compact Story HUD's 26 and 28 px badges).</summary>
        public float EffectScale => Mathf.Min(1f, _size / 46f);
        /// <summary>How many times the disc geometry was built (once per size).</summary>
        public int GeometryBuilds { get; private set; }

        /// <summary>The duration ring's centre-line radius for a badge: its outer edge <see cref="RingOutset"/> outside (46 -> 26.5).</summary>
        public static float RingRadius(float badge) => badge / 2f + RingOutset - RingWidth / 2f;

        private void Relayout() {
            CustomMinimumSize = new Vector2(_size, _size);
            Size = CustomMinimumSize;
            float glyph = _size * _iconShare;
            _icon.Size = new Vector2(glyph, glyph);
            _icon.Position = Vector2.One * ((_size - glyph) / 2f);
            QueueRedraw();
        }

        private void EnsureGeometry() {
            if (Mathf.IsEqualApprox(_builtFor, _size)) return;
            _builtFor = _size;
            GeometryBuilds++;
            var box = new Rect2(Vector2.Zero, new Vector2(_size, _size));
            float radius = _size / 2f;
            Vector2 centre = box.GetCenter();
            GlassGeometry.Circle(centre, radius, CircleSegments, box, _disc, _discUvs);
            for (int i = 0; i < CircleSegments; i++) _discColors[i] = Colors.White;
            if (_glowIndices == null) {
                _glowIndices = new int[CircleSegments * 6];
                for (int i = 0; i < CircleSegments; i++) {
                    int a = i, b = (i + 1) % CircleSegments, c = CircleSegments + i, d = CircleSegments + (i + 1) % CircleSegments;
                    _glowIndices[i * 6] = a; _glowIndices[i * 6 + 1] = b; _glowIndices[i * 6 + 2] = d;
                    _glowIndices[i * 6 + 3] = a; _glowIndices[i * 6 + 4] = d; _glowIndices[i * 6 + 5] = c;
                }
            }
            float inner = Mathf.Max(0f, radius - InnerGlowWidth * EffectScale);
            for (int i = 0; i < CircleSegments; i++) {
                float angle = Mathf.Tau * i / CircleSegments;
                var direction = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
                _glowPoints[i] = centre + direction * radius;
                _glowPoints[CircleSegments + i] = centre + direction * inner;
                _glowColors[i] = InnerGlowColor;
                _glowColors[CircleSegments + i] = new Color(InnerGlowColor, 0f);
            }
            int corner = Mathf.RoundToInt(radius);
            _glowBox.ShadowSize = Mathf.Max(1, Mathf.RoundToInt(OuterGlowSize * EffectScale));
            _glowBox.CornerRadiusTopLeft = _glowBox.CornerRadiusTopRight = corner;
            _glowBox.CornerRadiusBottomLeft = _glowBox.CornerRadiusBottomRight = corner;
        }

        public override void _Draw() {
            EnsureGeometry();
            float radius = _size / 2f;
            var centre = new Vector2(radius, radius);
            DrawStyleBox(_glowBox, new Rect2(Vector2.Zero, new Vector2(_size, _size)));
            Texture2D fill = PulpKit.Texture("glass_badge_radial");
            if (fill != null) DrawPolygon(_disc, _discColors, _discUvs, fill);
            else DrawColoredPolygon(_disc, UIPalette.BadgeFill);
            RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), _glowIndices, _glowPoints, _glowColors);
            DrawArc(centre, radius + GlassGeometry.EdgeWidth / 2f, 0f, Mathf.Tau, 64, UIPalette.Edge, GlassGeometry.EdgeWidth, true);
            if (_ring > 0f) {
                DrawArc(centre, DrawnRingRadius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + _ring * Mathf.Tau, 64, _ringColor, _ringWidth, true);
            }
        }
    }
}
