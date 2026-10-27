using FTT.UI.Motion;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// TG-WIDGETS. The glass slab's sheen and its two refraction streaks (UI_SPEC_GLASS 3.1, PNL-A layers 2 and 3), which
    /// the nine-slice cannot carry because stretching would shear them: on the 112 degree gradient line of the slab's fill
    /// box, white @0.05 over <c>t &lt;= 0.34</c> and two hard-edged bands, white @0.075 for t 0.570-0.575 and @0.04 for
    /// t 0.630-0.634, all clipped to the fill's chamfer polygon (the box inset 1.5 px, chamfer 17). The bands scale with
    /// the panel's own rect (a 620 x 214 slab: 3.3 and 2.6 px; a 600 x 768.6 slab: 4.2 and 3.4 px). Never animated, except
    /// the one light band <see cref="PulpMotion.PopIn"/> plays when the panel appears (MOT-A row 4: white @0.10, 40 px wide,
    /// along the streak direction, crossing left to right once over 600 ms; never under Reduced Temporal Effects).
    ///
    /// <para><see cref="Attach"/> adds it as the panel's first child, so it draws over the slab and under the content. In a
    /// <see cref="Container"/> it is fitted to the content rect; it reads the panel's own size and draws there, outside
    /// its rect. Geometry is rebuilt only when the panel's size changes (<see cref="GeometryBuilds"/> counts it); the draw
    /// allocates nothing.</para>
    /// </summary>
    public partial class GlassSheen : Control, IPanelLightBand {

        public const string NodeName = "GlassSheen";
        public const float Degrees = 112f;
        public const float Chamfer = 18f;
        public const float SheenEnd = 0.34f;
        public const float Band1Start = 0.570f, Band1End = 0.575f;
        public const float Band2Start = 0.630f, Band2End = 0.634f;
        public static readonly Color SheenColor = new(1f, 1f, 1f, 0.05f);
        public static readonly Color Band1Color = new(1f, 1f, 1f, 0.075f);
        public static readonly Color Band2Color = new(1f, 1f, 1f, 0.04f);

        private readonly Vector2[] _fill = new Vector2[6];
        private readonly Vector2[] _scratch = new Vector2[10];
        private readonly Vector2[] _scratch2 = new Vector2[10];
        private Vector2[] _sheen = System.Array.Empty<Vector2>();
        private Vector2[] _band1 = System.Array.Empty<Vector2>();
        private Vector2[] _band2 = System.Array.Empty<Vector2>();
        private readonly Vector2[][] _lightBuffers = new Vector2[11][];
        private int _lightCount;
        private Vector2 _builtFor = new(-1f, -1f);
        private Vector2 _builtOffset;
        private Control _host;
        private Tween _bandTween;
        private float _bandProgress = -1f;

        /// <summary>How many times the polygons were rebuilt (a redraw with no size change rebuilds nothing).</summary>
        public int GeometryBuilds { get; private set; }

        /// <summary>The fill-box rect the sheen is computed for, in this control's coordinates.</summary>
        public Rect2 FillBox { get; private set; }
        /// <summary>The sheen polygon for the current panel size (read-only).</summary>
        public Vector2[] SheenPolygon { get { EnsureGeometry(); return _sheen; } }
        /// <summary>The first and second streak polygons for the current panel size (read-only).</summary>
        public Vector2[] Band1Polygon { get { EnsureGeometry(); return _band1; } }
        public Vector2[] Band2Polygon { get { EnsureGeometry(); return _band2; } }
        /// <summary>The fill polygon the sheen is clipped to (read-only).</summary>
        public Vector2[] FillPolygon { get { EnsureGeometry(); return _fill; } }
        /// <summary>True while the appear light band is crossing.</summary>
        public bool LightBandPlaying => _bandProgress >= 0f;

        public GlassSheen() {
            Name = NodeName;
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.FullRect);
        }

        /// <summary>Adds a sheen as <paramref name="panel"/>'s first child (over the slab, under the content); a second call returns it.</summary>
        public static GlassSheen Attach(Control panel) {
            if (panel == null) return null;
            if (panel.GetNodeOrNull<GlassSheen>(NodeName) is { } existing) return existing;
            var sheen = new GlassSheen();
            panel.AddChild(sheen);
            panel.MoveChild(sheen, 0);
            return sheen;
        }

        public override void _EnterTree() {
            _host = GetParent() as Control;
            if (_host != null) _host.Resized += OnRectChanged;
            ItemRectChanged += OnRectChanged;
            QueueRedraw();
        }

        public override void _ExitTree() {
            _bandTween?.Kill();
            ItemRectChanged -= OnRectChanged;
            if (_host != null && IsInstanceValid(_host)) _host.Resized -= OnRectChanged;
            _host = null;
        }

        // A container may move this control (its content origin) without resizing it: either change moves the panel rect.
        private void OnRectChanged() => QueueRedraw();

        /// <summary>The panel's rect in this control's coordinates (this control may be fitted to the panel's content rect).</summary>
        private Rect2 PanelRect() {
            if (_host == null) return new Rect2(Vector2.Zero, Size);
            return new Rect2(-Position, _host.Size);
        }

        private void EnsureGeometry() {
            Rect2 panel = PanelRect();
            if (panel.Size == _builtFor && panel.Position == _builtOffset) return;
            _builtFor = panel.Size;
            _builtOffset = panel.Position;
            GeometryBuilds++;
            // The fill box: the slab inset by its hairline, cut at 17 (3.1).
            Rect2 fill = panel.Grow(-GlassGeometry.EdgeWidth);
            FillBox = fill;
            GlassGeometry.ChamferFill(panel, Chamfer, GlassGeometry.EdgeWidth, _fill);
            _sheen = Region(fill, float.NegativeInfinity, SheenEnd);
            _band1 = Region(fill, Band1Start, Band1End);
            _band2 = Region(fill, Band2Start, Band2End);
        }

        /// <summary>The part of the fill polygon with <c>from &lt;= t &lt;= to</c> on the 112 degree line of <paramref name="box"/>.</summary>
        private Vector2[] Region(Rect2 box, float from, float to) {
            Vector2 d = GlassGeometry.GradientDirection(Degrees);
            int count = GlassGeometry.ClipHalfPlane(_fill, 6, d, GlassGeometry.GradientOffset(box, Degrees, to), _scratch);
            if (!float.IsNegativeInfinity(from)) count = GlassGeometry.ClipHalfPlane(_scratch, count, -d, -GlassGeometry.GradientOffset(box, Degrees, from), _scratch2);
            else System.Array.Copy(_scratch, _scratch2, count);
            var result = new Vector2[count];
            System.Array.Copy(_scratch2, result, count);
            return result;
        }

        public override void _Draw() {
            EnsureGeometry();
            if (_sheen.Length >= 3) DrawColoredPolygon(_sheen, SheenColor);
            if (_band1.Length >= 3) DrawColoredPolygon(_band1, Band1Color);
            if (_band2.Length >= 3) DrawColoredPolygon(_band2, Band2Color);
            if (_bandProgress >= 0f && _lightCount >= 3) DrawColoredPolygon(_lightBuffers[_lightCount], new Color(1f, 1f, 1f, PulpMotionRules.PanelBandAlpha));
        }

        /// <summary>MOT-A row 4: the light band crosses the panel once, left to right, linear (<see cref="IPanelLightBand"/>).</summary>
        public void PlayLightBand(float seconds) {
            _bandTween?.Kill();
            if (PulpMotion.Reduced || seconds <= 0f || !IsInsideTree()) return;
            SetBand(0f);
            _bandTween = PulpMotion.NewTween(this);
            _bandTween.TweenMethod(Callable.From<float>(SetBand), 0f, 1f, seconds).SetTrans(Tween.TransitionType.Linear);
            _bandTween.TweenCallback(Callable.From(EndBand));
        }

        private void EndBand() {
            _bandProgress = -1f;
            _lightCount = 0;
            QueueRedraw();
        }

        // The band is a 40 px strip along the streak direction whose centre runs from before the fill's start (t < 0) to past
        // its end (t > 1). Clipped into a buffer kept per vertex count, so the crossing allocates nothing after its first frames.
        private void SetBand(float progress) {
            EnsureGeometry();
            _bandProgress = progress;
            Rect2 box = FillBox;
            float length = GlassGeometry.GradientLength(box.Size, Degrees);
            if (length <= 0f) { _lightCount = 0; QueueRedraw(); return; }
            float half = PulpMotionRules.PanelBandWidth / 2f / length;
            float centre = Mathf.Lerp(-half, 1f + half, progress);
            Vector2 d = GlassGeometry.GradientDirection(Degrees);
            int count = GlassGeometry.ClipHalfPlane(_fill, 6, d, GlassGeometry.GradientOffset(box, Degrees, centre + half), _scratch);
            count = GlassGeometry.ClipHalfPlane(_scratch, count, -d, -GlassGeometry.GradientOffset(box, Degrees, centre - half), _scratch2);
            _lightCount = count;
            if (count >= 3) {
                _lightBuffers[count] ??= new Vector2[count];
                System.Array.Copy(_scratch2, _lightBuffers[count], count);
            }
            QueueRedraw();
        }
    }
}
