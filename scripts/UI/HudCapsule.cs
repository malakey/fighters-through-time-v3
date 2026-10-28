using Godot;

namespace FTT.UI {

    /// <summary>
    /// The compact Story HUD's recessed capsule (2026-10-27, the user's grouping "A": the two Specials share one capsule and
    /// Movement has its own). A <see cref="MarginContainer"/> whose children sit 7 px inside a pill (radius = half its
    /// height) drawn under them: a darker vertical gradient, rgba(2, 5, 14, .70) at the top to rgba(10, 16, 36, .42) at the
    /// bottom, a soft inner shadow along the top and a 1.5 px gold hairline at .5 just inside the rim (the mock's
    /// <c>.hc-grp</c>). Drawn in code; no art. The polygons are rebuilt only when the size changes; a draw allocates
    /// nothing.
    /// </summary>
    public partial class HudCapsule : MarginContainer {

        /// <summary>The children's inset from the pill's rim (the mock's 7 px padding).</summary>
        public const int Padding = 7;
        /// <summary>The hairline's width (1.5 px) and alpha (.5 of the gold glow).</summary>
        public const float HairlineWidth = 1.5f, HairlineAlpha = 0.5f;
        /// <summary>The inner shadow: rgba(0, 0, 0, .5) falling off over 8 px from the top (CSS inset 0 2px 8px).</summary>
        public const float ShadowDepth = 8f, ShadowAlpha = 0.5f;

        public static readonly Color FillTop = new(2f / 255f, 5f / 255f, 14f / 255f, 0.70f);
        public static readonly Color FillBottom = new(10f / 255f, 16f / 255f, 36f / 255f, 0.42f);
        public static readonly Color Hairline = new(UIPalette.Accent, HairlineAlpha);

        private const int CapSegments = 18;
        private const int Points = (CapSegments + 1) * 2;
        private readonly Vector2[] _pill = new Vector2[Points];
        private readonly Color[] _pillColors = new Color[Points];
        private readonly Vector2[] _rim = new Vector2[Points + 1];
        private readonly Vector2[] _shade = new Vector2[Points];
        private readonly Color[] _shadeColors = new Color[Points];
        private Vector2 _builtFor = new(-1f, -1f);

        public HudCapsule() {
            MouseFilter = MouseFilterEnum.Ignore;
            AddThemeConstantOverride("margin_left", Padding);
            AddThemeConstantOverride("margin_top", Padding);
            AddThemeConstantOverride("margin_right", Padding);
            AddThemeConstantOverride("margin_bottom", Padding);
        }

        /// <summary>How many times the pill was rebuilt (once per size). Test seam.</summary>
        public int GeometryBuilds { get; private set; }

        /// <summary>The pill's corner radius for the current size: half its height.</summary>
        public float Radius => Mathf.Min(Size.X, Size.Y) / 2f;

        public override void _Notification(int what) {
            if (what == NotificationResized) QueueRedraw();
        }

        // The pill outline, clockwise from the top of the left cap, inset by `inset`.
        private void Pill(Vector2[] into, float inset) {
            float h = Size.Y - 2f * inset;
            float r = Mathf.Max(0f, h / 2f);
            float left = inset + r, right = Size.X - inset - r;
            float cy = Size.Y / 2f;
            int n = 0;
            // The right cap from the top (-90 degrees) to the bottom (+90), then the left cap from the bottom to the top.
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
            if (Size == _builtFor) return;
            _builtFor = Size;
            GeometryBuilds++;
            Pill(_pill, 0f);
            float height = Mathf.Max(1f, Size.Y);
            for (int i = 0; i < Points; i++) {
                float t = Mathf.Clamp(_pill[i].Y / height, 0f, 1f);
                _pillColors[i] = FillTop.Lerp(FillBottom, t);
                // The inner shadow: the pill again, dark at the top fading out by ShadowDepth (2 px lower than the rim).
                _shade[i] = _pill[i];
                float depth = Mathf.Clamp((_pill[i].Y - 2f) / ShadowDepth, 0f, 1f);
                _shadeColors[i] = new Color(0f, 0f, 0f, ShadowAlpha * (1f - depth));
            }
            Pill(_rim, HairlineWidth / 2f);
            _rim[Points] = _rim[0];
        }

        public override void _Draw() {
            if (Size.X <= 0f || Size.Y <= 0f) return;
            EnsureGeometry();
            DrawPolygon(_pill, _pillColors);
            DrawPolygon(_shade, _shadeColors);
            DrawPolyline(_rim, Hairline, HairlineWidth, true);
        }
    }
}
