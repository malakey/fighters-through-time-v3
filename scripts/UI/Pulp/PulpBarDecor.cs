using System;
using FTT.UI.Motion;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>Which glass tube a <see cref="PulpBarDecor"/> draws (UI_SPEC_GLASS 3.4.1, 3.6). Append only.</summary>
    public enum PulpBarStyle { Hp, Boss, Meter, Stat }

    /// <summary>
    /// TG-WIDGETS (GAUGE-A, UI_SPEC_GLASS 3.4.1 and 3.6; first written by UR-WIDGETS as the GAUGE-B overlay). The glass tube
    /// over a <see cref="ProgressBar"/>, which stays the value carrier and draws nothing of its own (the five bar
    /// variations, <c>PulpBar</c> ... <c>PulpBossBar</c>, have an empty background and a fill that holds its colour but
    /// draws nothing: TG-CLOSE, which retired the decor's hide-self material). The tube follows the bar's
    /// <c>SelfModulate</c> (the HUDs' flash channel: the not-yet tint) and draws itself in code, every layer a polygon
    /// clipped to the inner parallelogram, so a fill's anchored end follows the slant and its moving end is vertical:
    /// <list type="bullet">
    /// <item>the track: the outer parallelogram (run 12, boss 16) in <c>edge</c> (boss: its 110 degree gradient), the inner
    /// one inset 1.5 px on run 11 (15) with the 180 degree track gradient, and the tube's glow behind it;</item>
    /// <item>the tracker to <see cref="LagValue"/>, the Rally echo band from the fill to <see cref="EchoValue"/> inset 22 % of
    /// the inner height (D1), the fill as a vertex gradient from the anchored to the moving end, the top-46 % highlight, the
    /// 3 px cap with its soft glow and the decile ticks (the boss tube: a white tracker, no cap, no ticks, its phase
    /// notches);</item>
    /// <item>styles: <see cref="PulpBarStyle.Hp"/> (the fill's moving colour is the bar's fill <c>StyleBoxFlat.bg_color</c>,
    /// so <c>FighterHUD.TintFill</c> keeps working; <see cref="Low"/> turns it <c>boss2</c> to <c>danger</c> with the 7 px
    /// 0.5 Hz pulse, steady under Reduced Temporal Effects), <see cref="PulpBarStyle.Meter"/> (full: <c>res</c> to
    /// <c>res2</c> with its glow), <see cref="PulpBarStyle.Stat"/> (no glow) and <see cref="PulpBarStyle.Boss"/>;</item>
    /// <item>mirrored by the bar's <c>FillMode</c>: <c>EndToBegin</c> leans the tube "\" and anchors everything at the right
    /// (R6).</item>
    /// </list>
    /// The owner writes every value (nothing here ticks but the low pulse). Geometry is rebuilt only when a value, the size,
    /// the style or a colour changes (<see cref="GeometryBuilds"/>); a draw allocates nothing.
    /// </summary>
    public partial class PulpBarDecor : Control {

        /// <summary>The layer fractions of a bar, each clamped to [0, 1] of the bar's range.</summary>
        public readonly record struct Fractions(float Value, float Lag, float Echo);

        /// <summary>The low-HP pulse period (MOT-A row 11: 0.5 Hz).</summary>
        public const float LowPulseSeconds = PulpMotionRules.LowPulseSeconds;
        public const float TubeRun = 12f, BossRun = 16f;
        public const float EchoInsetShare = 0.22f;
        public const float HighlightShare = 0.46f;
        public const float CapWidth = 3f;
        public const int TickCount = 10;
        public const int TubeGlowSize = 10, BossGlowSize = 12, LowGlowSize = 7, FullGlowSize = 14;
        public const int MaxNotches = 4;

        private ProgressBar _bar;
        private PulpBarStyle _style = PulpBarStyle.Hp;
        private float _lagValue;
        private float _echoValue;
        private bool _low;
        private float[] _notches = Array.Empty<float>();
        private Tween _lagTween;
        private double _clock;

        /// <summary>The spec's resonance glow, rgb(242, 204, 77): the full meter's glow and a ready lens's.</summary>
        public static readonly Color ResGlow = new(0.949f, 0.8f, 0.302f);

        public PulpBarDecor() {
            Name = "PulpBarDecor";
            MouseFilter = MouseFilterEnum.Ignore;
            // Always, so the tube follows a flash the bar's own tween finishes while the tree is paused; the low pulse
            // still stops with the bar (see _Process).
            ProcessMode = ProcessModeEnum.Always;
            SetAnchorsPreset(LayoutPreset.FullRect);
            _glowBox.DrawCenter = false;
        }

        /// <summary>The bar this decor draws for.</summary>
        public ProgressBar Bar => _bar;

        /// <summary>
        /// Adds the tube as the last child of <paramref name="bar"/> (the bar's other children are untouched). The bar wears
        /// one of the five bar variations, whose styles draw nothing, so only the tube shows.
        /// </summary>
        public static PulpBarDecor Attach(ProgressBar bar, PulpBarStyle style = PulpBarStyle.Hp) {
            if (Of(bar) is { } existing) return existing;
            var decor = new PulpBarDecor { _bar = bar, _style = style };
            bar.AddChild(decor);
            return decor;
        }

        /// <summary>The decor <paramref name="bar"/> carries, or null.</summary>
        public static PulpBarDecor Of(ProgressBar bar) {
            if (bar == null) return null;
            for (int i = 0, n = bar.GetChildCount(); i < n; i++) if (bar.GetChild(i) is PulpBarDecor decor) return decor;
            return null;
        }

        public PulpBarStyle Style { get => _style; set { if (_style == value) return; _style = value; Invalidate(); } }

        /// <summary>The delayed damage tracker, in the bar's units (it shows between the fill and this value).</summary>
        public float LagValue { get => _lagValue; set { if (_lagValue == value) return; _lagValue = value; Invalidate(); } }

        /// <summary>The Rally echo's end, in the bar's units: <c>HP + echo</c>; the band runs from the fill end to here.</summary>
        public float EchoValue { get => _echoValue; set { if (_echoValue == value) return; _echoValue = value; Invalidate(); } }

        /// <summary>Low HP: the <c>boss2</c> to <c>danger</c> fill and the 7 px pulse on the track (steady under Reduced Temporal Effects).</summary>
        public bool Low {
            get => _low;
            set {
                if (_low == value) return;
                _low = value;
                Invalidate();
            }
        }

        /// <summary>Boss phase notches as fractions of the bar's range (at most four are drawn).</summary>
        public float[] PhaseNotches { get => _notches; set { _notches = value ?? Array.Empty<float>(); Invalidate(); } }

        /// <summary>The low glow's current alpha in [0, 1]: 0 when not low, 1 under Reduced Temporal Effects.</summary>
        public float GlowStrength => GlowStrengthAt(_low, _clock, PulpMotion.Reduced);

        /// <summary>The pure low-glow curve (MOT-A row 11): 0 off, a steady 1 reduced, else 0 to 1 and back over <see cref="LowPulseSeconds"/>.</summary>
        public static float GlowStrengthAt(bool low, double seconds, bool reduced) {
            if (!low) return 0f;
            if (reduced) return 1f;
            return PulpMotionRules.LowPulseAt((float)seconds);
        }

        /// <summary>The fractions a bar of the given range draws: the fill, the tracker and the echo end.</summary>
        public static Fractions FractionsFor(double value, double lag, double echo, double min, double max) {
            double range = max - min;
            if (range <= 0.0) return new Fractions(0f, 0f, 0f);
            return new Fractions(Clamp01((value - min) / range), Clamp01((lag - min) / range), Clamp01((echo - min) / range));
        }

        private static float Clamp01(double x) => (float)Mathf.Clamp(x, 0.0, 1.0);

        /// <summary>
        /// The HP damage tracker (MOT-A row 7): a rise sets <see cref="LagValue"/> at once; a drop holds, then drains through
        /// <c>PulpMotion.TrackerDrop</c> (a fade under Reduced Temporal Effects).
        /// </summary>
        public void DropLagTo(float target) {
            StopLag();
            if (target >= _lagValue || !IsInsideTree()) {
                LagValue = target;
                return;
            }
            _lagTween = PulpMotion.TrackerDrop(this, _lagValue, target, v => LagValue = v);
        }

        /// <summary>
        /// Stops the tracker's hold-and-drain, if one runs, and puts the tracker on <paramref name="value"/> at once: for a
        /// heal, a new maximum, a first presentation or a reset (REVIEW-UI-GLASS-CODEX-2). Assigning <see cref="LagValue"/>
        /// alone does not stop it: the drain writes through that setter from its own start, so a heal that only assigned
        /// the value let the earlier hit's drain bring a damage band back above the healed fill.
        /// </summary>
        public void SnapLagTo(float value) {
            StopLag();
            LagValue = value;
        }

        private void StopLag() {
            if (_lagTween != null && _lagTween.IsValid()) _lagTween.Kill();
            _lagTween = null;
        }

        public override void _EnterTree() {
            _bar ??= GetParent() as ProgressBar;
            if (_bar != null) {
                _bar.ValueChanged += OnBarChanged;
                _bar.Resized += Invalidate;
                _bar.Changed += Invalidate;
                _bar.Draw += QueueRedraw;   // a fill-mode or tint change redraws the bar; the tube follows
            }
            Invalidate();
        }

        public override void _Ready() => SetProcess(true);

        public override void _ExitTree() {
            StopLag();
            if (_bar != null && IsInstanceValid(_bar)) {
                _bar.ValueChanged -= OnBarChanged;
                _bar.Resized -= Invalidate;
                _bar.Changed -= Invalidate;
                _bar.Draw -= QueueRedraw;
            }
        }

        public override void _Notification(int what) {
            if (what == NotificationThemeChanged || what == NotificationResized) Invalidate();
        }

        private void OnBarChanged(double value) => Invalidate();

        private void Invalidate() {
            _dirty = true;
            QueueRedraw();
        }

        // Every frame: the tube takes the bar's SelfModulate (the not-yet tint); the low pulse runs only while
        // the bar itself processes (a paused HUD holds it). Allocation free.
        public override void _Process(double delta) {
            if (_bar == null) return;
            Color tint = _bar.SelfModulate;
            if (tint != SelfModulate) SelfModulate = tint;
            if (!_low || !_bar.CanProcess()) return;
            _clock += delta;
            QueueRedraw();
        }

        // ---- the tube ----

        /// <summary>The tube's lean in pixels: 12, the boss tube 16 (whatever the height).</summary>
        public float Run => _style == PulpBarStyle.Boss ? BossRun : TubeRun;

        /// <summary>True when the bar fills from its end (<c>FillMode.EndToBegin</c>): the tube leans "\" and anchors on the right.</summary>
        public bool Mirrored => _bar != null && _bar.FillMode == (int)ProgressBar.FillModeEnum.EndToBegin;

        /// <summary>How many times the polygons were rebuilt (a redraw with no change rebuilds nothing).</summary>
        public int GeometryBuilds { get; private set; }

        public Vector2[] OuterPolygon { get { EnsureGeometry(); return _outer.Points; } }
        public Color[] EdgeColors { get { EnsureGeometry(); return _outer.Colors; } }
        public Vector2[] InnerPolygon { get { EnsureGeometry(); return _inner.Points; } }
        public Vector2[] TrackerPolygon { get { EnsureGeometry(); return _tracker.Points; } }
        public Vector2[] EchoPolygon { get { EnsureGeometry(); return _echo.Points; } }
        public Vector2[] FillPolygon { get { EnsureGeometry(); return _fill.Points; } }
        public Color[] FillColors { get { EnsureGeometry(); return _fill.Colors; } }
        public Vector2[] CapPolygon { get { EnsureGeometry(); return _cap.Points; } }
        /// <summary>The decile ticks drawn (10 on a tube with ticks, 0 on the boss tube).</summary>
        public int TickPolygons { get { EnsureGeometry(); return _tickCount; } }
        /// <summary>The fill's colours at its anchored and its moving end.</summary>
        public (Color Anchored, Color Moving) FillGradient { get { EnsureGeometry(); return (_anchoredColor, _movingColor); } }
        /// <summary>The inner box the layers are measured in: the bar rect inset by the 1.5 px edge.</summary>
        public Rect2 TubeInnerBox => new Rect2(Vector2.Zero, Size).Grow(-GlassGeometry.EdgeWidth);

        /// <summary>The fill's colours (anchored, moving) for a style; <paramref name="tint"/> is the bar's fill colour.</summary>
        public static (Color Anchored, Color Moving) FillColorsFor(PulpBarStyle style, Color tint, bool low, bool full) {
            switch (style) {
                case PulpBarStyle.Boss: return (UIPalette.Boss2, UIPalette.Boss);
                case PulpBarStyle.Meter: return full ? (UIPalette.Res, UIPalette.Res2) : (UIPalette.Meter, UIPalette.Meter2);
                case PulpBarStyle.Stat: return (UIPalette.Accent, UIPalette.Accent2);
            }
            if (low) return (UIPalette.Boss2, UIPalette.Danger);
            if (tint.IsEqualApprox(UIPalette.Hp)) return (UIPalette.Hp2, UIPalette.Hp);
            if (tint.IsEqualApprox(UIPalette.Danger) || tint.IsEqualApprox(UIPalette.BossRed)) return (UIPalette.Boss2, tint);
            return (tint.Darkened(0.28f), tint);
        }

        private static readonly Color EchoHalo = new(UIPalette.Echo, 0.35f);
        private static readonly Color TrackerColor = new(UIPalette.HpLag, 0.9f);
        private static readonly Color CapGlowWhite = new(1f, 1f, 1f, 0.85f);
        private static readonly Color CapGlowMeter = new(UIPalette.Accent, 0.9f);
        private static readonly Color TubeGlow = new(UIPalette.Accent, 0.16f);
        private static readonly Color FullGlow = new(ResGlow, 0.70f);
        private static readonly Color BossEdgeFrom = new(UIPalette.BossEdge, 0.95f);
        private static readonly Color BossEdgeMid = new(UIPalette.BossEdge, 0.25f);
        private static readonly Color BossEdgeTo = new(1f, 1f, 1f, 0.5f);
        private static readonly Color NotchLine = new(1f, 1f, 1f, 0.9f);
        private static readonly Color NotchGlow = new(1f, 1f, 1f, 0.22f);

        // One reusable polygon: exact-length buffers kept per vertex count, so a rebuild allocates only on first use.
        private sealed class Poly {
            private readonly Vector2[][] _points = new Vector2[17][];
            private readonly Color[][] _colors = new Color[17][];
            public Vector2[] Points = Array.Empty<Vector2>();
            public Color[] Colors = Array.Empty<Color>();
            public bool Visible => Points.Length >= 3;

            public void Set(Vector2[] source, int count) {
                if (count < 3) { Points = Array.Empty<Vector2>(); Colors = Array.Empty<Color>(); return; }
                Points = _points[count] ??= new Vector2[count];
                Colors = _colors[count] ??= new Color[count];
                Array.Copy(source, Points, count);
            }

            public void Fill(Color color) { for (int i = 0; i < Colors.Length; i++) Colors[i] = color; }
            public void Clear() { Points = Array.Empty<Vector2>(); Colors = Array.Empty<Color>(); }
        }

        private readonly Poly _outer = new(), _inner = new(), _tracker = new(), _echo = new(), _echoHalo = new(), _fill = new(),
            _highlight = new(), _cap = new(), _capGlowA = new(), _capGlowB = new();
        private readonly Poly[] _ticks = { new(), new(), new(), new(), new(), new(), new(), new(), new(), new() };
        private readonly Poly[] _notchLines = { new(), new(), new(), new() }, _notchGlows = { new(), new(), new(), new() },
            _notchDiamonds = { new(), new(), new(), new() };
        private int _tickCount;
        private readonly Vector2[] _a = new Vector2[16], _b = new Vector2[16], _quad = new Vector2[4];
        private readonly StyleBoxFlat _glowBox = new();
        private bool _dirty = true;
        private Vector2 _builtSize = new(-1f, -1f);
        private Color _builtTint;
        private bool _builtMirrored;
        private Color _anchoredColor, _movingColor;
        private Rect2 _glowRect;
        private Color _baseGlow;
        private int _baseGlowSize;
        private bool _full;

        private Color FillTint => (_bar?.GetThemeStylebox("fill") as StyleBoxFlat)?.BgColor ?? UIPalette.Hp;

        private void EnsureGeometry() {
            Color tint = FillTint;
            bool mirroredNow = Mirrored;
            if (!_dirty && Size == _builtSize && tint == _builtTint && mirroredNow == _builtMirrored) return;
            _dirty = false;
            _builtSize = Size;
            _builtTint = tint;
            _builtMirrored = mirroredNow;
            GeometryBuilds++;
            var box = new Rect2(Vector2.Zero, Size);
            bool mirrored = Mirrored, boss = _style == PulpBarStyle.Boss;
            float run = Run;
            Rect2 inner = box.Grow(-GlassGeometry.EdgeWidth);

            // The track: the edge parallelogram and the inner one, with their gradients.
            GlassGeometry.Parallelogram(box, run, mirrored, _quad);
            _outer.Set(_quad, 4);
            for (int i = 0; i < 4; i++) {
                _outer.Colors[i] = boss
                    ? GlassGeometry.Stop3(BossEdgeFrom, BossEdgeMid, 0.40f, BossEdgeTo, GlassGeometry.GradientT(_quad[i], box, 110f))
                    : UIPalette.Edge;
            }
            GlassGeometry.Parallelogram(inner, Mathf.Max(0f, run - 1f), mirrored, _quad);
            _inner.Set(_quad, 4);
            Color top = boss ? UIPalette.BossTrackTop : UIPalette.TubeTrackTop, bottom = boss ? UIPalette.BossTrackBottom : UIPalette.TubeTrackBottom;
            for (int i = 0; i < 4; i++) _inner.Colors[i] = GlassGeometry.Stop2(top, bottom, (_quad[i].Y - inner.Position.Y) / Mathf.Max(1f, inner.Size.Y));

            double min = _bar?.MinValue ?? 0.0, max = _bar?.MaxValue ?? 100.0, value = _bar?.Value ?? 0.0;
            Fractions f = FractionsFor(value, _lagValue, _echoValue, min, max);
            _full = _style == PulpBarStyle.Meter && f.Value >= 1f;
            (_anchoredColor, _movingColor) = FillColorsFor(_style, tint, _low, _full);
            float anchoredX = mirrored ? inner.End.X : inner.Position.X;
            float X(float fraction) => mirrored ? inner.End.X - fraction * inner.Size.X : inner.Position.X + fraction * inner.Size.X;
            float movingX = X(f.Value);

            // The tracker (behind the fill, so it shows between the fill and the lag) and the Rally echo band.
            if (f.Lag > f.Value) {
                Slab(anchoredX, X(f.Lag), _tracker);
                _tracker.Fill(boss ? UIPalette.BossTracker : TrackerColor);
            } else {
                _tracker.Clear();
            }
            if (!boss && f.Echo > f.Value) {
                float y0 = inner.Position.Y + EchoInsetShare * inner.Size.Y, y1 = inner.End.Y - EchoInsetShare * inner.Size.Y;
                SlabRows(movingX, X(f.Echo), y0, y1, _echo);
                for (int i = 0; i < _echo.Colors.Length; i++) {
                    _echo.Colors[i] = GlassGeometry.Stop2(Colors.White, UIPalette.Echo, (_echo.Points[i].Y - y0) / Mathf.Max(1f, y1 - y0));
                }
                SlabRows(movingX, X(f.Echo), y0 - 3f, y1 + 3f, _echoHalo);
                _echoHalo.Fill(EchoHalo);
            } else {
                _echo.Clear();
                _echoHalo.Clear();
            }

            // The fill: a vertex gradient from the anchored to the moving end; its top-46 % highlight.
            if (f.Value > 0f) {
                Slab(anchoredX, movingX, _fill);
                float span = Mathf.Max(0.001f, Mathf.Abs(movingX - anchoredX));
                for (int i = 0; i < _fill.Colors.Length; i++) {
                    _fill.Colors[i] = GlassGeometry.Stop2(_anchoredColor, _movingColor, Mathf.Abs(_fill.Points[i].X - anchoredX) / span);
                }
                float hlBottom = inner.Position.Y + HighlightShare * inner.Size.Y;
                int n = GlassGeometry.ClipHalfPlane(_fill.Points, _fill.Points.Length, new Vector2(0f, 1f), hlBottom, _a);
                _highlight.Set(_a, n);
                float hlAlpha = boss ? 0.40f : 0.44f;
                for (int i = 0; i < _highlight.Colors.Length; i++) {
                    float t = (_highlight.Points[i].Y - inner.Position.Y) / Mathf.Max(1f, hlBottom - inner.Position.Y);
                    _highlight.Colors[i] = new Color(1f, 1f, 1f, hlAlpha * (1f - Mathf.Clamp(t, 0f, 1f)));
                }
            } else {
                _fill.Clear();
                _highlight.Clear();
            }

            // The cap on the moving edge, with its soft glow (two halves fading out from the cap's centre).
            if (!boss && f.Value > 0f) {
                float dir = mirrored ? -1f : 1f;
                float capOuter = movingX, capInner = movingX - dir * CapWidth, capCentre = movingX - dir * CapWidth / 2f;
                Slab(Mathf.Min(capInner, capOuter), Mathf.Max(capInner, capOuter), _cap);
                _cap.Fill(Colors.White);
                if (_style != PulpBarStyle.Stat) {
                    Color glow = _style == PulpBarStyle.Meter ? CapGlowMeter : CapGlowWhite;
                    Color peak = new(glow, glow.A * 0.45f), none = new(glow, 0f);
                    GlowHalf(capCentre, capCentre - dir * 9f, peak, none, _capGlowA);
                    GlowHalf(capCentre, capCentre + dir * 6f, peak, none, _capGlowB);
                } else {
                    _capGlowA.Clear();
                    _capGlowB.Clear();
                }
            } else {
                _cap.Clear();
                _capGlowA.Clear();
                _capGlowB.Clear();
            }

            // The decile ticks: 1 px at the end of each 10 % of the inner width, full height, over the fill.
            _tickCount = 0;
            for (int k = 0; k < TickCount; k++) {
                if (boss) { _ticks[k].Clear(); continue; }
                float x = inner.Position.X + (k + 1) / (float)TickCount * inner.Size.X;
                float x0 = x - 1f, x1 = x;
                if (mirrored) { x0 = inner.End.X - (x - inner.Position.X); x1 = x0 + 1f; }
                Slab(x0, x1, _ticks[k]);
                _ticks[k].Fill(UIPalette.TickLine);
                if (_ticks[k].Visible) _tickCount++;
            }

            // The glows behind the tube: a skewed flat box (6.3) on a rect a run narrower, centred.
            _glowRect = new Rect2(run / 2f, 0f, Mathf.Max(1f, box.Size.X - run), box.Size.Y);
            _glowBox.Skew = new Vector2((mirrored ? -1f : 1f) * run / Mathf.Max(1f, box.Size.Y), 0f);
            _baseGlow = boss ? UIPalette.BossGlow : TubeGlow;
            _baseGlowSize = boss ? BossGlowSize : TubeGlowSize;

            // The boss's phase notches: a 2 px white line from 8 above to 14 below, a 12 px accent diamond 16 below.
            for (int i = 0; i < MaxNotches; i++) {
                if (!boss || i >= _notches.Length || _notches[i] <= 0f || _notches[i] >= 1f) {
                    _notchLines[i].Clear(); _notchGlows[i].Clear(); _notchDiamonds[i].Clear();
                    continue;
                }
                float x = _notches[i] * box.Size.X;
                Rect(new Rect2(x - 1f, -8f, 2f, box.Size.Y + 22f), _notchLines[i], NotchLine);
                Rect(new Rect2(x - 3f, -10f, 6f, box.Size.Y + 26f), _notchGlows[i], NotchGlow);
                var c = new Vector2(x, box.Size.Y + 16f);
                float h = 6f * Mathf.Sqrt2;
                _quad[0] = c + new Vector2(0f, -h); _quad[1] = c + new Vector2(h, 0f); _quad[2] = c + new Vector2(0f, h); _quad[3] = c + new Vector2(-h, 0f);
                _notchDiamonds[i].Set(_quad, 4);
                _notchDiamonds[i].Fill(UIPalette.Accent);
            }
        }

        // The inner parallelogram between two verticals.
        private void Slab(float x0, float x1, Poly into) {
            if (x1 < x0) (x0, x1) = (x1, x0);
            int n = GlassGeometry.ClipHalfPlane(_inner.Points, _inner.Points.Length, new Vector2(-1f, 0f), -x0, _a);
            n = GlassGeometry.ClipHalfPlane(_a, n, new Vector2(1f, 0f), x1, _b);
            into.Set(_b, n);
        }

        // The inner parallelogram between two verticals and two horizontals.
        private void SlabRows(float x0, float x1, float y0, float y1, Poly into) {
            if (x1 < x0) (x0, x1) = (x1, x0);
            int n = GlassGeometry.ClipHalfPlane(_inner.Points, _inner.Points.Length, new Vector2(-1f, 0f), -x0, _a);
            n = GlassGeometry.ClipHalfPlane(_a, n, new Vector2(1f, 0f), x1, _b);
            n = GlassGeometry.ClipHalfPlane(_b, n, new Vector2(0f, -1f), -y0, _a);
            n = GlassGeometry.ClipHalfPlane(_a, n, new Vector2(0f, 1f), y1, _b);
            into.Set(_b, n);
        }

        // One half of the cap's glow: from the cap's centre (peak) to its edge (none), clipped to the tube.
        private void GlowHalf(float from, float to, Color peak, Color none, Poly into) {
            Slab(from, to, into);
            float span = Mathf.Max(0.001f, Mathf.Abs(to - from));
            for (int i = 0; i < into.Colors.Length; i++) into.Colors[i] = peak.Lerp(none, Mathf.Clamp(Mathf.Abs(into.Points[i].X - from) / span, 0f, 1f));
        }

        private void Rect(Rect2 rect, Poly into, Color color) {
            _quad[0] = rect.Position; _quad[1] = new Vector2(rect.End.X, rect.Position.Y);
            _quad[2] = rect.End; _quad[3] = new Vector2(rect.Position.X, rect.End.Y);
            into.Set(_quad, 4);
            into.Fill(color);
        }

        private void DrawGlow(int size, Color color) {
            if (color.A <= 0f) return;
            _glowBox.ShadowSize = size;
            _glowBox.ShadowColor = color;
            DrawStyleBox(_glowBox, _glowRect);
        }

        private void DrawPoly(Poly poly) {
            if (poly.Visible) DrawPolygon(poly.Points, poly.Colors);
        }

        public override void _Draw() {
            if (_bar == null) return;
            EnsureGeometry();
            DrawGlow(_baseGlowSize, _baseGlow);
            if (_low && _style == PulpBarStyle.Hp) DrawGlow(LowGlowSize, new Color(UIPalette.Danger, GlowStrength));
            if (_full) DrawGlow(FullGlowSize, FullGlow);
            DrawPoly(_outer);
            DrawPoly(_inner);
            DrawPoly(_tracker);
            DrawPoly(_echoHalo);
            DrawPoly(_echo);
            DrawPoly(_fill);
            DrawPoly(_highlight);
            DrawPoly(_capGlowA);
            DrawPoly(_capGlowB);
            DrawPoly(_cap);
            foreach (Poly tick in _ticks) DrawPoly(tick);
            for (int i = 0; i < MaxNotches; i++) {
                DrawPoly(_notchGlows[i]);
                DrawPoly(_notchLines[i]);
                DrawPoly(_notchDiamonds[i]);
            }
        }
    }
}
