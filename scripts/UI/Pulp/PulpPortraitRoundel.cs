using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// The G5 portrait frame (UI_SPEC_GLASS 3.0, 3.4.4; TG-WIDGETS restyled the pulp roundel, which keeps its name): a
    /// square of any size (112 Story, 110 Fighter, 86 Continue card, 150 dialogue, 320 versus) with its top-left and
    /// bottom-right corners cut at 22 %. A 2 px edge with the 135 degree gradient (<c>edge-hi</c>, rgba(glow,.25) at 45 %,
    /// <c>edge</c>); inside it the radial fill (<c>glass_portrait_radial</c>: rgba(glow,.38) to rgba(6,10,26,.95) at 78 %)
    /// and the portrait art at 142 % of the inner box, offset (-21 %, -6.8 %) (a face crop), clipped to the inner polygon by
    /// its UVs; a soft drop shadow (0, 6) 12 below. <see cref="Diameter"/> keeps its name (the frame's side).
    ///
    /// <para>Geometry, colours and UVs are rebuilt only when the size or the art changes (<see cref="GeometryBuilds"/>);
    /// the draw allocates nothing. The portrait is drawn into a square, so it should be square art.</para>
    /// </summary>
    public partial class PulpPortraitRoundel : Control {

        /// <summary>The Story Vitals frame size, the reference the spec's numbers are given at.</summary>
        public const float ReferenceDiameter = 112f;
        public const float PortraitZoom = 1.42f;
        public static readonly Vector2 PortraitOffset = new(-0.21f, -0.068f);
        /// <summary>The corner cut as a share of the side (22 %).</summary>
        public const float ChamferShare = 0.22f;
        /// <summary>The frame's edge (2 px, 6.6).</summary>
        public const float EdgeWidth = 2f;
        public const float EdgeMidStop = 0.45f;
        public static readonly Color EdgeMid = new(UIPalette.Accent, 0.25f);
        public const int ShadowSize = 12;
        public static readonly Vector2 ShadowOffset = new(0f, 6f);

        private float _diameter = ReferenceDiameter;
        private Texture2D _portrait;
        private readonly Vector2[] _outer = new Vector2[6];
        private readonly Vector2[] _inner = new Vector2[6];
        private readonly Vector2[] _scratch = new Vector2[8];
        private Vector2[] _edgeA = System.Array.Empty<Vector2>(), _edgeB = System.Array.Empty<Vector2>();
        private Color[] _edgeColorsA = System.Array.Empty<Color>(), _edgeColorsB = System.Array.Empty<Color>();
        private readonly Vector2[] _innerUvs = new Vector2[6];
        private readonly Vector2[] _artUvs = new Vector2[6];
        private readonly Color[] _white = { Colors.White, Colors.White, Colors.White, Colors.White, Colors.White, Colors.White };
        private readonly StyleBoxFlat _shadow;
        private float _builtFor = -1f;

        public PulpPortraitRoundel() {
            Name = "PulpPortraitRoundel";
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            _shadow = new StyleBoxFlat {
                DrawCenter = false, BgColor = new Color(0, 0, 0, 0), ShadowColor = UIPalette.PanelShadow,
                ShadowSize = ShadowSize, ShadowOffset = ShadowOffset, AntiAliasing = true, CornerDetail = 1
            };
            Layout();
        }

        /// <summary>The frame's side: 112 (Story), 110 (Fighter), 86 (Continue card), 150 (dialogue), 320 (versus).</summary>
        public float Diameter { get => _diameter; set { _diameter = value; Layout(); } }
        public Texture2D Portrait { get => _portrait; set { _portrait = value; QueueRedraw(); } }
        /// <summary>How many times the frame's polygons were rebuilt.</summary>
        public int GeometryBuilds { get; private set; }

        /// <summary>The inner box (the frame inset by its 2 px edge) for a frame of side <paramref name="diameter"/>.</summary>
        public static Rect2 InnerBox(float diameter) =>
            new(EdgeWidth, EdgeWidth, Mathf.Max(0f, diameter - 2f * EdgeWidth), Mathf.Max(0f, diameter - 2f * EdgeWidth));

        /// <summary>The portrait's rectangle relative to the frame's own box: 142 % of the inner box, offset (-21 %, -6.8 %) of it.</summary>
        public static Rect2 PortraitRect(float diameter) {
            Rect2 inner = InnerBox(diameter);
            return new Rect2(inner.Position + PortraitOffset * inner.Size.X, new Vector2(PortraitZoom, PortraitZoom) * inner.Size.X);
        }

        /// <summary>The frame's outer chamfer polygon (its edge layer).</summary>
        public Vector2[] OuterPolygon { get { EnsureGeometry(); return _outer; } }
        /// <summary>The inner polygon the fill and the art are clipped to.</summary>
        public Vector2[] InnerPolygon { get { EnsureGeometry(); return _inner; } }

        private void Layout() {
            CustomMinimumSize = new Vector2(_diameter, _diameter);
            Size = CustomMinimumSize;
            QueueRedraw();
        }

        private void EnsureGeometry() {
            if (Mathf.IsEqualApprox(_builtFor, _diameter)) return;
            _builtFor = _diameter;
            GeometryBuilds++;
            var box = new Rect2(0f, 0f, _diameter, _diameter);
            Rect2 inner = InnerBox(_diameter);
            GlassGeometry.Chamfer(box, _diameter * ChamferShare, _outer);
            GlassGeometry.Chamfer(inner, inner.Size.X * ChamferShare, _inner);
            // The 135 degree edge gradient has three stops: split the polygon at its middle stop so each half is linear.
            Vector2 d = GlassGeometry.GradientDirection(135f);
            float mid = GlassGeometry.GradientOffset(box, 135f, EdgeMidStop);
            _edgeA = Clip(_outer, d, mid);
            _edgeB = Clip(_outer, -d, -mid);
            _edgeColorsA = new Color[_edgeA.Length];
            _edgeColorsB = new Color[_edgeB.Length];
            for (int i = 0; i < _edgeA.Length; i++) _edgeColorsA[i] = GlassGeometry.Stop3(UIPalette.EdgeHi, EdgeMid, EdgeMidStop, UIPalette.Edge, GlassGeometry.GradientT(_edgeA[i], box, 135f));
            for (int i = 0; i < _edgeB.Length; i++) _edgeColorsB[i] = GlassGeometry.Stop3(UIPalette.EdgeHi, EdgeMid, EdgeMidStop, UIPalette.Edge, GlassGeometry.GradientT(_edgeB[i], box, 135f));
            GlassGeometry.BoxUvs(_inner, 6, inner, _innerUvs);
            GlassGeometry.BoxUvs(_inner, 6, PortraitRect(_diameter), _artUvs);
            float cut = _diameter * ChamferShare;
            _shadow.CornerRadiusTopLeft = Mathf.RoundToInt(cut);
            _shadow.CornerRadiusBottomRight = Mathf.RoundToInt(cut);
        }

        private Vector2[] Clip(Vector2[] polygon, Vector2 normal, float offset) {
            int count = GlassGeometry.ClipHalfPlane(polygon, polygon.Length, normal, offset, _scratch);
            var result = new Vector2[count];
            System.Array.Copy(_scratch, result, count);
            return result;
        }

        public override void _Draw() {
            EnsureGeometry();
            DrawStyleBox(_shadow, new Rect2(0f, 0f, _diameter, _diameter));
            if (_edgeA.Length >= 3) DrawPolygon(_edgeA, _edgeColorsA);
            if (_edgeB.Length >= 3) DrawPolygon(_edgeB, _edgeColorsB);
            Texture2D fill = PulpKit.Texture("glass_portrait_radial");
            if (fill != null) DrawPolygon(_inner, _white, _innerUvs, fill);
            else DrawColoredPolygon(_inner, UIPalette.PortraitFill);
            if (_portrait != null) DrawPolygon(_inner, _white, _artUvs, _portrait);
        }
    }
}
