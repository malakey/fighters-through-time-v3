using Godot;

namespace FTT.UI {

    /// <summary>
    /// The <c>24 × 24 px</c> Warden Beacon icon at the head of the Story HUD's Act
    /// III anchor readout (M15, Package 12 W10), authored as
    /// <c>TemporalRow/BeaconAnchors/BeaconIcon</c> immediately right of the rewind
    /// hourglass.
    ///
    /// <para>With charges remaining it is a solid gold beacon. At <b>zero</b> it
    /// becomes a hollow outline with a steady last-stand tint — no pulse — because
    /// the next collapse is Smothered. The hollow shape carries the meaning, so it
    /// reads without colour. Placeholder vector art until Package 10.</para>
    /// </summary>
    public partial class BeaconAnchorIcon : Control {
        /// <summary>The steady last-stand tint at zero charges.</summary>
        public static readonly Color LastStandTint = UIPalette.BossRed;

        private bool _lastStand;

        /// <summary>True while the icon shows the hollow last-stand state.</summary>
        public bool LastStand => _lastStand;

        /// <summary>The colour currently drawn.</summary>
        public Color CurrentColor => _lastStand ? LastStandTint : UIPalette.Gold;

        public void SetLastStand(bool lastStand) {
            if (_lastStand == lastStand) return;
            _lastStand = lastStand;
            QueueRedraw();
        }

        public override void _Draw() {
            Vector2 size = Size.X > 0f && Size.Y > 0f ? Size : CustomMinimumSize;
            float w = size.X;
            float h = size.Y;
            // A beacon: a flared base, a tapering tower and a diamond lamp on top.
            Vector2[] tower = {
                new(w * 0.22f, h * 0.95f),
                new(w * 0.78f, h * 0.95f),
                new(w * 0.62f, h * 0.45f),
                new(w * 0.38f, h * 0.45f)
            };
            Vector2[] lamp = {
                new(w * 0.5f, h * 0.05f),
                new(w * 0.7f, h * 0.25f),
                new(w * 0.5f, h * 0.42f),
                new(w * 0.3f, h * 0.25f)
            };
            Color colour = CurrentColor;
            if (_lastStand) {
                DrawPolyline(Closed(tower), colour, 1.5f);
                DrawPolyline(Closed(lamp), colour, 1.5f);
            } else {
                DrawColoredPolygon(tower, colour);
                DrawColoredPolygon(lamp, UIPalette.GoldBright);
            }
        }

        private static Vector2[] Closed(Vector2[] points) {
            var closed = new Vector2[points.Length + 1];
            points.CopyTo(closed, 0);
            closed[points.Length] = points[0];
            return closed;
        }
    }
}
