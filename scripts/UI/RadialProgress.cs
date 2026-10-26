using Godot;

namespace FTT.UI {

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
    /// </summary>
    public partial class RadialProgress : Control {

        private float _fraction = 1f;
        private Color _fillColor = UIPalette.Res2;
        private Color _trackColor = UIPalette.TrackBackground;

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
            if (_fraction <= 0f) return;
            DrawArc(centre, radius, start, start + Mathf.Tau * _fraction, Segments, _fillColor, Thickness,
                antialiased: true);
        }

        private void DrawPie() {
            Vector2 centre = Size * 0.5f;
            float radius = Mathf.Max(1f, Mathf.Min(Size.X, Size.Y) * 0.5f);
            const float start = -Mathf.Pi * 0.5f;
            DrawCircle(centre, radius, _trackColor);
            if (_fraction >= 1f) {
                DrawCircle(centre, radius, _fillColor);
            } else if (_fraction > 0f) {
                int steps = Mathf.Max(3, Mathf.CeilToInt(Segments * 3 * _fraction));
                var points = new Vector2[steps + 2];
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
