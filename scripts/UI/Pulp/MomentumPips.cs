using FTT.UI.Motion;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// MM-HUD (meter and Defy update, 2026-10-31; ledger <c>USER-MOMENTUM-HUD</c>, <c>PROVISIONAL-MOMENTUM-HUD-LAYOUT</c>). The
    /// Momentum gauge as four code-drawn pips in a row: each pip is a stadium that fills left to right in proportion to the
    /// gauge, continuously inside a pip (so the regen creeps instead of stepping), over a dim track with a hairline edge. No
    /// art, no theme role: it reuses <see cref="UIPalette.Accent"/>, <see cref="UIPalette.Ink3"/> and <see cref="UIPalette.Edge"/>.
    ///
    /// <para>The owner writes <see cref="Value"/> (0 to <see cref="PipCount"/>, in pips) and <see cref="Dimmed"/> (Levels 0 to
    /// 2, where both Specials are Dormant but the gauge still fills, G4.1); nothing here ticks. A pip completing plays one
    /// bloom (<see cref="PulpMotion.Bloom"/>, which is skipped under Reduced Temporal Effects). The polygons are rebuilt only
    /// when the size or the value moves; a draw allocates nothing.</para>
    /// </summary>
    public partial class MomentumPips : Control {

        /// <summary>The pips in a row (the gauge's capacity, <c>BasicComboRules.MomentumMaxPips</c>).</summary>
        public const int PipCount = FTT.Combat.BasicComboRules.MomentumMaxPips;
        /// <summary>The row's height and the gap between pips, in pixels at 100 %.</summary>
        public const float RowHeight = 9f, PipGap = 5f;
        /// <summary>The dimmed row's alpha (the Dormant overlay's look).</summary>
        public const float DimAlpha = 0.4f;

        private const int CapSegments = 6;
        private const int StadiumPoints = (CapSegments + 1) * 2;

        private static readonly Color Track = new(UIPalette.Ink3, 0.28f);
        private static readonly Color Fill = UIPalette.Accent;
        private static readonly Color Full = UIPalette.GoldBright;

        private float _value;
        private bool _dimmed;
        private Tween _bloom;
        private Vector2 _builtFor = new(-1f, -1f);
        private float _builtValue = -1f;
        private readonly Vector2[] _clip = new Vector2[StadiumPoints + 4];
        private readonly int[] _fillCounts = new int[PipCount];
        private readonly Vector2[][] _fills = new Vector2[PipCount][];
        private readonly Vector2[][] _tracks = new Vector2[PipCount][];
        private readonly Vector2[][] _edges = new Vector2[PipCount][];

        public MomentumPips() {
            Name = "MomentumPips";
            MouseFilter = MouseFilterEnum.Ignore;
            CustomMinimumSize = new Vector2(0f, RowHeight);
            for (int i = 0; i < PipCount; i++) {
                _fills[i] = System.Array.Empty<Vector2>();
                _tracks[i] = new Vector2[StadiumPoints];
                _edges[i] = new Vector2[StadiumPoints + 1];
            }
        }

        /// <summary>The gauge in pips, clamped to 0..4. Writing a value that completes a pip plays one bloom.</summary>
        public float Value {
            get => _value;
            set {
                float next = Mathf.Clamp(float.IsNaN(value) ? 0f : value, 0f, PipCount);
                if (Mathf.IsEqualApprox(next, _value)) return;
                // A pip completing by regen or a gain (under a pip's worth of change), not a seed or a restore jumping the gauge.
                bool completed = Mathf.FloorToInt(next + 0.0001f) > Mathf.FloorToInt(_value + 0.0001f) && next - _value < 1f;
                _value = next;
                if (completed) StartBloom();
                QueueRedraw();
            }
        }

        /// <summary>True while the row is dimmed (both Specials Dormant); the gauge still shows its fill.</summary>
        public bool Dimmed {
            get => _dimmed;
            set { if (_dimmed == value) return; _dimmed = value; QueueRedraw(); }
        }

        /// <summary>Whole pips full, 0 to 4. Test seam.</summary>
        public int FullPips => Mathf.FloorToInt(_value + 0.0001f);

        /// <summary>How many pip-completion blooms were played (none under Reduced Temporal Effects). Test seam.</summary>
        public int Blooms { get; private set; }

        /// <summary>How many times the polygons were rebuilt (a size or value change). Test seam.</summary>
        public int GeometryBuilds { get; private set; }

        /// <summary>The fill share of pip <paramref name="index"/> (0 to 1) for a gauge of <paramref name="pips"/>.</summary>
        public static float PipFill(float pips, int index) => Mathf.Clamp(pips - index, 0f, 1f);

        /// <summary>The row's width needed for pips of <paramref name="pipWidth"/>.</summary>
        public static float RowWidthFor(float pipWidth) => PipCount * pipWidth + (PipCount - 1) * PipGap;

        public override void _Notification(int what) {
            if (what == NotificationResized) QueueRedraw();
        }

        public override void _ExitTree() => _bloom?.Kill();

        private void StartBloom() {
            if (!IsInsideTree()) return;
            _bloom?.Kill();
            _bloom = PulpMotion.Bloom(this);
            if (_bloom != null) Blooms++;
        }

        private float PipWidth => Mathf.Max(1f, (Size.X - (PipCount - 1) * PipGap) / PipCount);

        // A stadium (a rectangle with semicircular ends) filling the pip's box, clockwise from the top of the right cap.
        private void Stadium(float x, float width, float height, Vector2[] into) {
            float r = Mathf.Min(height, width) / 2f;
            float cy = height / 2f;
            float left = x + r, right = x + width - r;
            int n = 0;
            for (int i = 0; i <= CapSegments; i++) {
                float a = -Mathf.Pi / 2f + Mathf.Pi * i / CapSegments;
                into[n++] = new Vector2(right + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            for (int i = 0; i <= CapSegments; i++) {
                float a = Mathf.Pi / 2f + Mathf.Pi * i / CapSegments;
                into[n++] = new Vector2(left + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
        }

        private void EnsureGeometry() {
            if (Size == _builtFor && Mathf.IsEqualApprox(_value, _builtValue)) return;
            _builtFor = Size;
            _builtValue = _value;
            GeometryBuilds++;
            float width = PipWidth, height = Size.Y;
            for (int i = 0; i < PipCount; i++) {
                float x = i * (width + PipGap);
                Stadium(x, width, height, _tracks[i]);
                for (int k = 0; k < StadiumPoints; k++) _edges[i][k] = _tracks[i][k];
                _edges[i][StadiumPoints] = _tracks[i][0];
                float fill = PipFill(_value, i);
                int n = 0;
                if (fill >= 1f) {
                    n = StadiumPoints;
                    if (_fills[i].Length != n) _fills[i] = new Vector2[n];
                    System.Array.Copy(_tracks[i], _fills[i], n);
                } else if (fill > 0f) {
                    n = GlassGeometry.ClipHalfPlane(_tracks[i], StadiumPoints, new Vector2(1f, 0f), x + width * fill, _clip);
                    if (_fills[i].Length != n) _fills[i] = new Vector2[n];
                    System.Array.Copy(_clip, _fills[i], n);
                }
                _fillCounts[i] = n;
            }
        }

        public override void _Draw() {
            if (Size.X <= 0f || Size.Y <= 0f) return;
            EnsureGeometry();
            float alpha = _dimmed ? DimAlpha : 1f;
            var trackColor = new Color(Track, Track.A * alpha);
            var edgeColor = new Color(UIPalette.Edge, UIPalette.Edge.A * alpha);
            for (int i = 0; i < PipCount; i++) {
                DrawColoredPolygon(_tracks[i], trackColor);
                if (_fillCounts[i] >= 3) {
                    Color c = PipFill(_value, i) >= 1f ? Full : Fill;
                    DrawColoredPolygon(_fills[i], new Color(c, c.A * alpha));
                }
                DrawPolyline(_edges[i], edgeColor, 1f, true);
            }
        }
    }
}
