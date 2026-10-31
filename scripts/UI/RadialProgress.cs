using Godot;

namespace FTT.UI {

    /// <summary>How a <see cref="RadialProgress"/> ring colours its filled arc (TG-WIDGETS step 2, additive).</summary>
    public enum RadialGradientMode { None, Arc, Loop }

    /// <summary>
    /// Package 11 A8. A small circular fill indicator, drawn rather than themed.
    ///
    /// <para>HUD_CONTRACT asks for duration radials on both status slots and
    /// readiness radials on the three cooldown icons and the Ultimate meter. The
    /// repository had no radial widget at all — every readiness readout was a
    /// <c>ProgressBar</c> — and Godot's <c>TextureProgressBar</c> radial mode needs
    /// authored art that does not exist yet. So this follows the pattern already
    /// used by <c>BossPhaseNotchOverlay</c> and <c>SequenceGlyph</c>: a tiny
    /// <c>_Draw</c> control whose geometry is correct now and whose art can be
    /// swapped in later without moving the node.</para>
    ///
    /// <para>It renders nothing on its own clock. <see cref="Fraction"/> is written
    /// by whichever surface owns the authoritative value — which is what lets a
    /// status radial stop dead during Time Freeze instead of counting down a
    /// clock that is not running (F24).</para>
    ///
    /// <para>TG-WIDGETS step 2 (additive only): <see cref="GradientMode"/> colours the
    /// filled arc from <see cref="FillColor"/> to <see cref="FillColorEnd"/> (the glass
    /// lens's Ultimate ring), or loops it round the whole ring (its ready ring). The
    /// existing modes and defaults are unchanged; the default track is written out as
    /// the pulp <c>TrackBackground</c> value so the world-space channel displays
    /// (<c>RestorationFont</c>, <c>ResonanceHoldNode</c>) never follow a palette change.</para>
    /// </summary>
    public partial class RadialProgress : Control {

        /// <summary>The default track: #3A3144, the pulp <c>TrackBackground</c> this widget has always drawn.</summary>
        public static readonly Color DefaultTrackColor = new(0x3A / 255f, 0x31 / 255f, 0x44 / 255f);

        private float _fraction = 1f;
        private Color _fillColor = UIPalette.Res2;
        private Color _trackColor = DefaultTrackColor;
        private Color _fillColorEnd = UIPalette.Res2;
        private RadialGradientMode _gradientMode = RadialGradientMode.None;

        /// <summary>
        /// UR-HUDS: draw a filled pie (the Integrity dial's wedge) instead of a ring. The track is the
        /// unfilled remainder of the disc, <see cref="InnerRingColor"/> a 3 px ring on its edge.
        /// </summary>
        public bool Pie {
            get => _pie;
            set { if (_pie == value) return; _pie = value; QueueRedraw(); }
        }

        /// <summary>The ring a pie wears on its edge (transparent for none).</summary>
        public Color InnerRingColor {
            get => _innerRing;
            set { if (_innerRing == value) return; _innerRing = value; QueueRedraw(); }
        }

        private float _notchFraction = -1f;
        private Color _notchColor = UIPalette.Ink3;

        /// <summary>
        /// MM-HUD: a short radial tick across the ring at this share of the way round (clockwise from twelve o'clock; 0.5 is six
        /// o'clock), for the Ultimate lens's Defy notch. Negative (default) draws none. Ring mode only.
        /// </summary>
        public float NotchFraction {
            get => _notchFraction;
            set { if (_notchFraction == value) return; _notchFraction = value; QueueRedraw(); }
        }

        /// <summary>The notch's colour.</summary>
        public Color NotchColor {
            get => _notchColor;
            set { if (_notchColor == value) return; _notchColor = value; QueueRedraw(); }
        }

        /// <summary>The notch's width and how far it sticks out past each edge of the ring.</summary>
        public const float NotchWidth = 2f, NotchOverhang = 1.5f;

        private bool _pie;
        private Color _innerRing = new(0f, 0f, 0f, 0f);

        /// <summary>Ring thickness in pixels.</summary>
        [Export] public float Thickness { get; set; } = 3f;

        /// <summary>Arc segments. 24 is smooth at HUD sizes and cheap.</summary>
        [Export] public int Segments { get; set; } = 24;

        /// <summary>
        /// Fill in 0..1, clockwise from twelve o'clock. Setting it queues a redraw
        /// only when the value actually moved, so a 60 Hz HUD does not redraw four
        /// radials per frame for nothing.
        /// </summary>
        [Export]
        public float Fraction {
            get => _fraction;
            set {
                float clamped = Mathf.Clamp(value, 0f, 1f);
                if (Mathf.IsEqualApprox(clamped, _fraction)) return;
                _fraction = clamped;
                QueueRedraw();
            }
        }

        /// <summary>Colour of the filled arc.</summary>
        public Color FillColor {
            get => _fillColor;
            set {
                if (_fillColor == value) return;
                _fillColor = value;
                QueueRedraw();
            }
        }

        /// <summary>Colour of the unfilled remainder.</summary>
        public Color TrackColor {
            get => _trackColor;
            set {
                if (_trackColor == value) return;
                _trackColor = value;
                QueueRedraw();
            }
        }

        /// <summary>How the filled arc is coloured (ring mode only): flat, along the arc, or looped round the ring.</summary>
        public RadialGradientMode GradientMode {
            get => _gradientMode;
            set { if (_gradientMode == value) return; _gradientMode = value; QueueRedraw(); }
        }

        /// <summary>The gradient's other colour: at the arc's end (<see cref="RadialGradientMode.Arc"/>) or half way round (<see cref="RadialGradientMode.Loop"/>).</summary>
        public Color FillColorEnd {
            get => _fillColorEnd;
            set { if (_fillColorEnd == value) return; _fillColorEnd = value; QueueRedraw(); }
        }

        /// <summary>The filled arc's colour at <paramref name="t"/> of the arc (0 at 12 o'clock) for a gradient mode.</summary>
        public static Color GradientColorAt(RadialGradientMode mode, Color from, Color to, float t) {
            t = Mathf.Clamp(t, 0f, 1f);
            return mode switch {
                RadialGradientMode.Arc => from.Lerp(to, t),
                RadialGradientMode.Loop => t <= 0.5f ? from.Lerp(to, t * 2f) : to.Lerp(from, (t - 0.5f) * 2f),
                _ => from
            };
        }

        public override void _Ready() {
            MouseFilter = MouseFilterEnum.Ignore;
            QueueRedraw();
        }

        public override void _Draw() {
            if (_pie) {
                DrawPie();
                return;
            }
            Vector2 centre = Size * 0.5f;
            float radius = Mathf.Max(1f, Mathf.Min(Size.X, Size.Y) * 0.5f - Thickness * 0.5f);
            // Twelve o'clock is -PI/2 in Godot's screen-space angles.
            const float start = -Mathf.Pi * 0.5f;
            DrawArc(centre, radius, start, start + Mathf.Tau, Segments, _trackColor, Thickness, antialiased: true);
            if (_fraction > 0f) {
                if (_gradientMode != RadialGradientMode.None) DrawGradientArc(centre, radius, start);
                else DrawArc(centre, radius, start, start + Mathf.Tau * _fraction, Segments, _fillColor, Thickness, antialiased: true);
            }
            if (_notchFraction >= 0f && _notchFraction <= 1f) {
                float angle = start + Mathf.Tau * _notchFraction;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float half = Thickness * 0.5f + NotchOverhang;
                DrawLine(centre + dir * (radius - half), centre + dir * (radius + half), _notchColor, NotchWidth, true);
            }
        }

        // Per-point colours along the filled arc; one buffer pair per point count, so a redraw allocates nothing.
        private readonly System.Collections.Generic.Dictionary<int, (Vector2[] Points, Color[] Colors)> _arcBuffers = new();

        private void DrawGradientArc(Vector2 centre, float radius, float start) {
            int count = Mathf.Max(2, Mathf.CeilToInt(Mathf.Max(Segments, 48) * _fraction) + 1);
            if (!_arcBuffers.TryGetValue(count, out (Vector2[] Points, Color[] Colors) buffer)) {
                buffer = (new Vector2[count], new Color[count]);
                _arcBuffers[count] = buffer;
            }
            for (int i = 0; i < count; i++) {
                float u = i / (float)(count - 1);
                float angle = start + Mathf.Tau * _fraction * u;
                buffer.Points[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                // Arc: along the drawn arc; Loop: by the position round the whole ring.
                float t = _gradientMode == RadialGradientMode.Loop ? u * _fraction : u;
                buffer.Colors[i] = GradientColorAt(_gradientMode, _fillColor, _fillColorEnd, t);
            }
            DrawPolylineColors(buffer.Points, buffer.Colors, Thickness, antialiased: true);
        }

        private Vector2[] _pieBuffer;

        private void DrawPie() {
            Vector2 centre = Size * 0.5f;
            float radius = Mathf.Max(1f, Mathf.Min(Size.X, Size.Y) * 0.5f);
            const float start = -Mathf.Pi * 0.5f;
            DrawCircle(centre, radius, _trackColor);
            if (_fraction >= 1f) {
                DrawCircle(centre, radius, _fillColor);
            } else if (_fraction > 0f) {
                int steps = Mathf.Max(3, Mathf.CeilToInt(Segments * 3 * _fraction));
                if (_pieBuffer == null || _pieBuffer.Length != steps + 2) _pieBuffer = new Vector2[steps + 2];
                Vector2[] points = _pieBuffer;
                points[0] = centre;
                for (int i = 0; i <= steps; i++) {
                    float angle = start + Mathf.Tau * _fraction * i / steps;
                    points[i + 1] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                DrawColoredPolygon(points, _fillColor);
            }
            if (_innerRing.A > 0f) {
                DrawArc(centre, radius - 1.5f, 0f, Mathf.Tau, 64, _innerRing, 3f, antialiased: true);
            }
        }
    }
}
