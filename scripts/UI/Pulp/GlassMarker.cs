using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// TG-WIDGETS. The chevron button's diamond marker (UI_SPEC_GLASS 3.2): a 14 px square rotated 45 degrees (a 19.8 px
    /// diagonal) drawn per state. Normal (and pressed without focus): a 1.5 px <c>edge</c> outline, no fill. Focus: filled
    /// <c>focus</c> #FFD45C with a 1.5 px white outline, a 4 px ring rgba(glow,.30) around it and a 16 px <c>focus</c>
    /// glow. Selected: filled and outlined <c>accent</c>. Disabled: a dashed (3 px) <c>edge</c> outline at .5 opacity.
    ///
    /// <para>It derives from <see cref="PulpIcon"/> so <c>PulpButtonDecor.Marker</c> keeps its type; <c>Tint</c> reports
    /// the state's main colour and <c>ShadowEnabled</c> whether the glow is drawn. The square scales with
    /// <see cref="Scale"/> (the decor passes the UI scale). Drawn with a rotated canvas transform from cached shapes:
    /// nothing allocates per draw.</para>
    /// </summary>
    public partial class GlassMarker : PulpIcon {

        /// <summary>The square's side at 100 %.</summary>
        public const float Side = 14f;
        public const float OutlineWidth = 1.5f;
        public const float RingWidth = 4f;
        public const float GlowSize = 16f;
        public const float DashLength = 3f;
        /// <summary>The 4 px ring rgba(glow,.30).</summary>
        public static readonly Color RingColor = new(UIPalette.Accent, 0.30f);
        /// <summary>Godot's shadow fades linearly from its colour at the edge, a CSS blur from half: the glow starts at .55.</summary>
        public static readonly Color GlowColor = new(UIPalette.Focus, 0.55f);

        private static StyleBoxFlat _glowBox;
        private PulpButtonDecor.MarkerState _state = PulpButtonDecor.MarkerState.Normal;
        private float _scale = 1f;
        private readonly Vector2[] _outline = new Vector2[5];
        private float _builtScale = -1f;

        public GlassMarker() {
            Name = "Marker";
            MouseFilter = MouseFilterEnum.Ignore;
            Tint = PulpButtonDecor.MarkerTint(_state);
            ShadowEnabled = PulpButtonDecor.MarkerHasShadow(_state);
        }

        /// <summary>The state drawn.</summary>
        public PulpButtonDecor.MarkerState State {
            get => _state;
            set {
                if (_state == value) return;
                _state = value;
                Tint = PulpButtonDecor.MarkerTint(value);
                ShadowEnabled = PulpButtonDecor.MarkerHasShadow(value);
                QueueRedraw();
            }
        }

        /// <summary>The UI scale the square, ring, glow and outline follow (UI_SPEC_GLASS 6.7 item 8).</summary>
        public float MarkerScale {
            get => _scale;
            set { if (Mathf.IsEqualApprox(_scale, value)) return; _scale = value; QueueRedraw(); }
        }

        /// <summary>How many times the outline path was rebuilt (only a scale change rebuilds it).</summary>
        public int GeometryBuilds { get; private set; }

        /// <summary>The diamond's diagonal at the current scale (the control's size).</summary>
        public float Diagonal => Side * _scale * Mathf.Sqrt2;

        private static StyleBoxFlat GlowBox(float size) {
            _glowBox ??= new StyleBoxFlat { DrawCenter = false, BgColor = new Color(0, 0, 0, 0), ShadowColor = GlowColor, AntiAliasing = true };
            _glowBox.ShadowSize = Mathf.RoundToInt(size);
            return _glowBox;
        }

        private void EnsureOutline() {
            if (Mathf.IsEqualApprox(_builtScale, _scale)) return;
            _builtScale = _scale;
            GeometryBuilds++;
            // The CSS border lies inside the 14 px square: the path runs half the outline width in from its edge.
            float h = Side * _scale / 2f - OutlineWidth * _scale / 2f;
            _outline[0] = new Vector2(-h, -h);
            _outline[1] = new Vector2(h, -h);
            _outline[2] = new Vector2(h, h);
            _outline[3] = new Vector2(-h, h);
            _outline[4] = new Vector2(-h, -h);
        }

        public override void _Draw() {
            EnsureOutline();
            float s = Side * _scale;
            var square = new Rect2(-s / 2f, -s / 2f, s, s);
            float width = OutlineWidth * _scale;
            DrawSetTransform(Size / 2f, Mathf.Pi / 4f, Vector2.One);
            switch (_state) {
                case PulpButtonDecor.MarkerState.Focus:
                    DrawStyleBox(GlowBox(GlowSize * _scale), square);
                    DrawRect(square.Grow(RingWidth * _scale), RingColor);
                    DrawRect(square, UIPalette.Focus);
                    DrawPolyline(_outline, Colors.White, width, true);
                    break;
                case PulpButtonDecor.MarkerState.Selected:
                    DrawRect(square, UIPalette.Accent);
                    DrawPolyline(_outline, UIPalette.Accent, width, true);
                    break;
                case PulpButtonDecor.MarkerState.Disabled: {
                    Color dim = PulpButtonDecor.MarkerDisabledTint;
                    for (int i = 0; i < 4; i++) DrawDashedLine(_outline[i], _outline[i + 1], dim, width, DashLength * _scale, true, true);
                    break;
                }
                default:
                    DrawPolyline(_outline, UIPalette.Edge, width, true);
                    break;
            }
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
    }
}
