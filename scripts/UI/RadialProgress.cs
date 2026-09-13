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
        private Color _fillColor = UIPalette.Cyan;
        private Color _trackColor = UIPalette.TrackBackground;

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
            Vector2 centre = Size * 0.5f;
            float radius = Mathf.Max(1f, Mathf.Min(Size.X, Size.Y) * 0.5f - Thickness * 0.5f);
            // Twelve o'clock is -PI/2 in Godot's screen-space angles.
            const float start = -Mathf.Pi * 0.5f;
            DrawArc(centre, radius, start, start + Mathf.Tau, Segments, _trackColor, Thickness, antialiased: true);
            if (_fraction <= 0f) return;
            DrawArc(centre, radius, start, start + Mathf.Tau * _fraction, Segments, _fillColor, Thickness,
                antialiased: true);
        }
    }
}
