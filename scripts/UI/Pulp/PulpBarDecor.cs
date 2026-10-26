using FTT.Core;
using FTT.UI.Motion;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>Which GAUGE-B bar look a <see cref="PulpBarDecor"/> draws.</summary>
    public enum PulpBarStyle { Hp, Boss }

    /// <summary>
    /// UR-WIDGETS. The GAUGE-B overlay for a <see cref="ProgressBar"/> (UI_SPEC 3.3.1 and 3.5): the damage
    /// tracker, the Rally echo band, the hatch and highlight on the fill, the decile ticks (boss: phase
    /// notches). One shader draws every layer over the bar, so the bar keeps its own <c>fill</c> stylebox:
    /// <b>never replace it, and it never needs to be a texture</b>; <c>FighterHUD.TintFill</c> can keep
    /// recolouring a <c>StyleBoxFlat</c> fill. The owner writes every value (nothing here ticks); the skew
    /// is read from the fill's <c>StyleBoxFlat.Skew</c> and <c>ProgressBar.FillMode</c> is respected.
    /// </summary>
    public partial class PulpBarDecor : Control {

        /// <summary>The layer fractions of a bar, each clamped to [0, 1] of the bar's range.</summary>
        public readonly record struct Fractions(float Value, float Lag, float Echo);

        public const string ShaderPath = "res://assets/shaders/pulp_bar_layers.gdshader";
        public const float LowPulseSeconds = 2f;

        private readonly ShaderMaterial _material;
        private readonly Control _glow;
        private ProgressBar _bar;
        private PulpBarStyle _style = PulpBarStyle.Hp;
        private float _lagValue;
        private float _echoValue;
        private bool _low;
        private float[] _notches = System.Array.Empty<float>();
        private Tween _lagTween;
        private double _clock;

        public PulpBarDecor() {
            Name = "PulpBarDecor";
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.FullRect);
            _material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
            Material = _material;
            _glow = new BarGlow(this) { Name = "LowGlow", MouseFilter = MouseFilterEnum.Ignore };
            _glow.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_glow);
            _glow.ShowBehindParent = true;
        }

        /// <summary>The bar this decor overlays.</summary>
        public ProgressBar Bar => _bar;

        /// <summary>Adds the overlay as the last child of <paramref name="bar"/> (the bar's other children are untouched).</summary>
        public static PulpBarDecor Attach(ProgressBar bar, PulpBarStyle style = PulpBarStyle.Hp) {
            for (int i = 0, n = bar.GetChildCount(); i < n; i++) if (bar.GetChild(i) is PulpBarDecor existing) return existing;
            var decor = new PulpBarDecor { _bar = bar, _style = style };
            bar.AddChild(decor);
            return decor;
        }

        public PulpBarStyle Style { get => _style; set { if (_style == value) return; _style = value; _styleDirty = true; QueueRedraw(); } }

        /// <summary>The delayed damage tracker, in the bar's units (the fill end to this value draws in the tracker colour).</summary>
        public float LagValue { get => _lagValue; set { if (_lagValue == value) return; _lagValue = value; QueueRedraw(); } }

        /// <summary>The Rally echo's end, in the bar's units: <c>HP + echo</c>; the band runs from the fill end to here.</summary>
        public float EchoValue { get => _echoValue; set { if (_echoValue == value) return; _echoValue = value; QueueRedraw(); } }

        /// <summary>Low-HP state: a 2 s ease-in-out danger glow on the track (static under Reduced Temporal Effects).</summary>
        public bool Low {
            get => _low;
            set {
                if (_low == value) return;
                _low = value;
                SetProcess(value);
                _glow.QueueRedraw();
            }
        }

        /// <summary>Boss phase notches as fractions of the bar's range (at most four are drawn).</summary>
        public float[] PhaseNotches { get => _notches; set { _notches = value ?? System.Array.Empty<float>(); _styleDirty = true; QueueRedraw(); } }

        /// <summary>The current glow strength in [0, 1]: 0 when not low, a fixed 0.6 under Reduced Temporal Effects.</summary>
        public float GlowStrength => GlowStrengthAt(_low, _clock, PulpMotion.Reduced);

        /// <summary>The pure glow curve: 0 off, steady 0.6 reduced, else an ease-in-out sine over <see cref="LowPulseSeconds"/> starting at full strength.</summary>
        public static float GlowStrengthAt(bool low, double seconds, bool reduced) {
            if (!low) return 0f;
            if (reduced) return 0.6f;
            return 0.5f + 0.5f * Mathf.Cos((float)(seconds / LowPulseSeconds * Mathf.Tau));
        }

        /// <summary>The fractions a bar of the given range draws: the fill, the tracker and the echo end.</summary>
        public static Fractions FractionsFor(double value, double lag, double echo, double min, double max) {
            double range = max - min;
            if (range <= 0.0) return new Fractions(0f, 0f, 0f);
            return new Fractions(Clamp01((value - min) / range), Clamp01((lag - min) / range), Clamp01((echo - min) / range));
        }

        private static float Clamp01(double x) => (float)Mathf.Clamp(x, 0.0, 1.0);

        /// <summary>
        /// The HP damage tracker (UI_SPEC 5 row 6): a rise sets <see cref="LagValue"/> at once; a drop holds,
        /// then steps down through <c>PulpMotion.TrackerDrop</c> (a slide under Reduced Temporal Effects).
        /// </summary>
        public void DropLagTo(float target) {
            _lagTween?.Kill();
            if (target >= _lagValue || !IsInsideTree()) {
                LagValue = target;
                return;
            }
            _lagTween = PulpMotion.TrackerDrop(this, _lagValue, target, v => LagValue = v);
        }

        public override void _Ready() {
            _bar ??= GetParent() as ProgressBar;
            if (_bar != null) {
                _bar.ValueChanged += OnBarChanged;
                _bar.Resized += QueueRedraw;
                _bar.Changed += QueueRedraw;
            }
            SetProcess(_low);
        }

        public override void _ExitTree() {
            _lagTween?.Kill();
            if (_bar != null && IsInstanceValid(_bar)) {
                _bar.ValueChanged -= OnBarChanged;
                _bar.Resized -= QueueRedraw;
                _bar.Changed -= QueueRedraw;
            }
        }

        private void OnBarChanged(double value) => QueueRedraw();

        public override void _Process(double delta) {
            _clock += delta;
            _glow.QueueRedraw();
        }

        // ---- geometry shared with the glow layer ----

        private StyleBox Background => _bar?.GetThemeStylebox("background");

        /// <summary>The inner area (the bar rect inset by the background stylebox's content margins), in this control's pixels.</summary>
        public Rect2 InnerRect() {
            StyleBox bg = Background;
            float l = bg?.GetContentMargin(Side.Left) ?? 3f, t = bg?.GetContentMargin(Side.Top) ?? 3f;
            float r = bg?.GetContentMargin(Side.Right) ?? 3f, b = bg?.GetContentMargin(Side.Bottom) ?? 3f;
            return new Rect2(l, t, Mathf.Max(0f, Size.X - l - r), Mathf.Max(0f, Size.Y - t - b));
        }

        /// <summary>The fill's horizontal skew (a <c>StyleBoxFlat.Skew.X</c>), 0 when the fill is not a flat box.</summary>
        public float Skew => (_bar?.GetThemeStylebox("fill") as StyleBoxFlat)?.Skew.X ?? 0f;

        private static readonly StringName PRectSize = "rect_size", PInnerPos = "inner_pos", PInnerSize = "inner_size",
            PSkewOff = "skew_off", PReverse = "reverse", PV = "v", PLag = "lag", PEchoTo = "echo_to",
            PLagColor = "lag_color", PEchoColor = "echo_color", PHatchDark = "hatch_dark", PHatchClear = "hatch_clear",
            PHatchAlpha = "hatch_alpha", PHlTop = "hl_top", PHlHeight = "hl_height", PHlAlpha = "hl_alpha",
            PTicksOn = "ticks_on", PNotches = "notches";
        private bool _styleDirty = true;

        /// <summary>The shader parameters that depend only on the style and the notches (set when they change, not on every redraw).</summary>
        private void ApplyStyleParameters() {
            _styleDirty = false;
            bool boss = _style == PulpBarStyle.Boss;
            _material.SetShaderParameter(PLagColor, boss ? UIPalette.Accent : UIPalette.HpLag);
            _material.SetShaderParameter(PEchoColor, UIPalette.Echo);
            _material.SetShaderParameter(PHatchDark, boss ? 6f : 5f);
            _material.SetShaderParameter(PHatchClear, boss ? 8f : 6f);
            _material.SetShaderParameter(PHatchAlpha, boss ? 0.20f : 0.22f);
            _material.SetShaderParameter(PHlTop, boss ? 4f : 3f);
            _material.SetShaderParameter(PHlHeight, boss ? 6f : 5f);
            _material.SetShaderParameter(PHlAlpha, boss ? 0.55f : 0.60f);
            _material.SetShaderParameter(PTicksOn, boss ? 0f : 1f);
            var n = new Vector4(-1f, -1f, -1f, -1f);
            for (int i = 0; i < Mathf.Min(4, _notches.Length); i++) n[i] = _notches[i];
            _material.SetShaderParameter(PNotches, n);
        }

        public override void _Draw() {
            if (_bar == null) return;
            Rect2 inner = InnerRect();
            Fractions f = FractionsFor(_bar.Value, _lagValue, _echoValue, _bar.MinValue, _bar.MaxValue);
            _material.SetShaderParameter(PRectSize, Size);
            _material.SetShaderParameter(PInnerPos, inner.Position);
            _material.SetShaderParameter(PInnerSize, inner.Size);
            _material.SetShaderParameter(PSkewOff, Mathf.Abs(Skew) * inner.Size.Y);
            _material.SetShaderParameter(PReverse, _bar.FillMode == (int)ProgressBar.FillModeEnum.EndToBegin ? 1f : 0f);
            _material.SetShaderParameter(PV, f.Value);
            _material.SetShaderParameter(PLag, f.Lag);
            _material.SetShaderParameter(PEchoTo, f.Echo);
            if (_styleDirty) ApplyStyleParameters();
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.White);
        }

        /// <summary>The low-HP glow: a few soft rings around the track, drawn without the bar shader.</summary>
        private partial class BarGlow : Control {
            private readonly PulpBarDecor _owner;
            private readonly Vector2[] _ring = new Vector2[5];
            public BarGlow(PulpBarDecor owner) { _owner = owner; }

            public override void _Draw() {
                float strength = _owner.GlowStrength;
                if (strength <= 0f) return;
                float slant = _owner.Skew * Size.Y;
                Color danger = UIPalette.Danger;
                for (int ring = 7; ring >= 1; ring--) {
                    float falloff = 1f - ring / 8f;
                    var color = new Color(danger.R, danger.G, danger.B, strength * 0.22f * falloff);
                    float d = ring;
                    Vector2[] pts = _ring;
                    pts[0] = new Vector2(-d + slant, -d); pts[1] = new Vector2(Size.X + d, -d);
                    pts[2] = new Vector2(Size.X + d - slant, Size.Y + d); pts[3] = new Vector2(-d, Size.Y + d); pts[4] = pts[0];
                    DrawPolyline(pts, color, 2f);
                }
            }
        }
    }
}
