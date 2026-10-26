using FTT.UI;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// The tier shown by shape as well as colour: a circle (tier 1), a diamond (tier 2) or a
    /// four-point star (tier 3), drawn with a black ink rim at the chip's top-right corner.
    /// </summary>
    public partial class ResonanceTierMark : Control {
        public int Tier { get; set; } = 1;

        public ResonanceTierMark() {
            MouseFilter = MouseFilterEnum.Ignore;
            AnchorLeft = 1f; AnchorRight = 1f;
            OffsetLeft = -34f; OffsetRight = -10f; OffsetTop = 8f; OffsetBottom = 32f;
        }

        /// <summary>Corner count of the shape: 0 circle, 4 diamond, 8 star.</summary>
        public static int PointsFor(int tier) => tier <= 1 ? 0 : tier == 2 ? 4 : 8;

        public override void _Draw() {
            Vector2 c = Size / 2f;
            float r = Mathf.Min(Size.X, Size.Y) / 2f - 2f;
            int points = PointsFor(Tier);
            Color fill = Tier <= 1 ? UIPalette.Ink2 : Tier == 2 ? UIPalette.Accent : UIPalette.Accent2;
            if (points == 0) {
                DrawCircle(c, r + 2f, Colors.Black);
                DrawCircle(c, r, fill);
                return;
            }
            var poly = new Vector2[points];
            var rim = new Vector2[points + 1];
            for (int i = 0; i < points; i++) {
                float angle = -Mathf.Pi / 2f + i * Mathf.Tau / points;
                float radius = points == 8 && i % 2 == 1 ? r * 0.45f : r;
                poly[i] = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                rim[i] = poly[i];
            }
            rim[points] = poly[0];
            DrawColoredPolygon(poly, fill);
            DrawPolyline(rim, Colors.Black, 2.5f, true);
        }
    }
}
