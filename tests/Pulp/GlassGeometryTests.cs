using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>
/// TG-WIDGETS. The Temporal Glass shapes as UI_SPEC_GLASS states them (0.2, 0.3, 3.1, 3.2, 3.4.1): the chamfer and its
/// fill's hairline, the chevron's fixed run, a tube fill's vertical moving edge and its mirror, and the CSS angle gradient.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GlassGeometryTests {

    private static bool Near(Vector2 a, Vector2 b, float tolerance = 0.01f) => a.DistanceTo(b) <= tolerance;

    private static bool Has(Vector2[] points, Vector2 point) {
        foreach (Vector2 p in points) if (Near(p, point)) return true;
        return false;
    }

    [TestCase]
    public void TheChamferCutsTopLeftAndBottomRightAndItsFillKeepsAOnePointFourOnePixelHairline() {
        var box = new Rect2(0f, 0f, 620f, 214f);
        Vector2[] outer = GlassGeometry.Chamfer(box, 18f);
        Vector2[] expected = { new(18f, 0f), new(620f, 0f), new(620f, 196f), new(602f, 214f), new(0f, 214f), new(0f, 18f) };
        for (int i = 0; i < 6; i++) {
            if (!Near(outer[i], expected[i])) AssertThat($"chamfer point {i} is {outer[i]}, not {expected[i]}").IsEqual("");
        }
        // 3.1: the fill is the box inset 1.5 px with the cut at 17, leaving 1.41 px along the 45 degree edge.
        Vector2[] fill = GlassGeometry.ChamferFill(box, 18f);
        AssertThat(Near(fill[0], new Vector2(1.5f + 17f, 1.5f))).IsTrue();
        AssertThat(Near(fill[3], new Vector2(618.5f - 17f, 212.5f))).IsTrue();
        AssertThat(Near(fill[5], new Vector2(1.5f, 1.5f + 17f))).IsTrue();
        // The distance between the outer cut (x + y = 18) and the inner cut (x + y = 1.5 + 17 + 1.5) along the normal.
        float gap = (fill[0].X + fill[0].Y - (outer[0].X + outer[0].Y)) / Mathf.Sqrt2;
        AssertThat(Mathf.Abs(gap - 1.414f) < 0.01f).OverrideFailureMessage($"the chamfer hairline is {gap} px").IsTrue();
        // A chamfer larger than half the shorter side is clamped.
        Vector2[] small = GlassGeometry.Chamfer(new Rect2(0f, 0f, 20f, 10f), 18f);
        AssertThat(Near(small[0], new Vector2(5f, 0f))).IsTrue();
    }

    [TestCase]
    public void TheChevronKeepsAThirtyPixelRunToAMidHeightTipAtEveryHeight() {
        foreach (float height in new[] { 58f, 64f, 68f, 70f }) {
            Vector2[] edge = GlassGeometry.Chevron(new Rect2(0f, 0f, 560f, height));
            AssertThat(edge.Length).IsEqual(5);
            AssertThat(Near(edge[1], new Vector2(530f, 0f))).IsTrue();
            AssertThat(Near(edge[2], new Vector2(560f, height / 2f))).OverrideFailureMessage($"the tip at height {height} is {edge[2]}").IsTrue();
            AssertThat(Near(edge[3], new Vector2(530f, height))).IsTrue();
            // 3.2: the fill is the box inset 1.5 px clipped to (W' - 31, 0), (W' - 1, H'/2) in its own coordinates.
            Vector2[] fill = GlassGeometry.ChevronFill(new Rect2(0f, 0f, 560f, height));
            float w = 557f, h = height - 3f;
            AssertThat(Near(fill[1], new Vector2(1.5f + w - 31f, 1.5f))).IsTrue();
            AssertThat(Near(fill[2], new Vector2(1.5f + w - 1f, 1.5f + h / 2f))).IsTrue();
        }
        // The run does not follow the width either.
        Vector2[] compact = GlassGeometry.Chevron(new Rect2(10f, 20f, 290f, 58f));
        AssertThat(Near(compact[1], new Vector2(270f, 20f))).IsTrue();
    }

    [TestCase]
    public void AClippedFillEndsOnAVerticalEdgeAndItsMirrorAnchorsOnTheRight() {
        var tube = new Rect2(0f, 0f, 380f, 30f);
        Vector2[] half = GlassGeometry.ClippedFill(tube, 12f, 0.5f);
        // The anchored end follows the tube's "/" slant; the moving end is vertical at 190.
        AssertThat(Has(half, new Vector2(12f, 0f))).IsTrue();
        AssertThat(Has(half, new Vector2(0f, 30f))).IsTrue();
        AssertThat(Has(half, new Vector2(190f, 0f))).IsTrue();
        AssertThat(Has(half, new Vector2(190f, 30f))).IsTrue();
        foreach (Vector2 p in half) {
            if (p.X > 190.01f) AssertThat($"the fill reaches {p} past its moving edge").IsEqual("");
        }
        // Mirrored (R6): the tube leans "\", the fill grows from the right, its moving end vertical at 190.
        Vector2[] mirrored = GlassGeometry.ClippedFill(tube, 12f, 0.5f, mirrored: true);
        AssertThat(Has(mirrored, new Vector2(368f, 0f))).IsTrue();
        AssertThat(Has(mirrored, new Vector2(380f, 30f))).IsTrue();
        AssertThat(Has(mirrored, new Vector2(190f, 0f))).IsTrue();
        AssertThat(Has(mirrored, new Vector2(190f, 30f))).IsTrue();
        foreach (Vector2 p in mirrored) {
            if (p.X < 189.99f) AssertThat($"the mirrored fill reaches {p} past its moving edge").IsEqual("");
        }
        AssertThat(GlassGeometry.ClippedFill(tube, 12f, 0f).Length).IsEqual(0);
        // A full fill is the whole parallelogram.
        Vector2[] full = GlassGeometry.ClippedFill(tube, 12f, 1f);
        AssertThat(Has(full, new Vector2(380f, 0f)) && Has(full, new Vector2(368f, 30f))).IsTrue();
    }

    [TestCase]
    public void TheAngleGradientMapsTheCssLineOntoTheBox() {
        var box = new Rect2(0f, 0f, 620f, 214f);
        // 90 degrees runs left to right, 180 top to bottom; the centre is always 0.5.
        AssertThat(Mathf.Abs(GlassGeometry.GradientT(new Vector2(0f, 107f), box, 90f)) < 1e-4f).IsTrue();
        AssertThat(Mathf.Abs(GlassGeometry.GradientT(new Vector2(620f, 107f), box, 90f) - 1f) < 1e-4f).IsTrue();
        AssertThat(Mathf.Abs(GlassGeometry.GradientT(new Vector2(310f, 214f), box, 180f) - 1f) < 1e-4f).IsTrue();
        AssertThat(Mathf.Abs(GlassGeometry.GradientT(box.GetCenter(), box, 112f) - 0.5f) < 1e-4f).IsTrue();
        // 3.1: the 620 x 214 panel's 112 degree line is 655.0 px long, so the first streak (t .570-.575) is 3.3 px wide.
        float length = GlassGeometry.GradientLength(box.Size, 112f);
        AssertThat(Mathf.Abs(length - 655.0f) < 0.2f).OverrideFailureMessage($"the 112 degree line is {length}").IsTrue();
        AssertThat(Mathf.Abs(0.005f * length - 3.3f) < 0.05f).IsTrue();
        // 1.4: the 1920 x 1080 118 degree line is 2202.3 px.
        AssertThat(Mathf.Abs(GlassGeometry.GradientLength(new Vector2(1920f, 1080f), 118f) - 2202.3f) < 0.2f).IsTrue();
        // A point on an offset line sits at that t; the corners of a 135 degree gradient run 0 to 1.
        float offset = GlassGeometry.GradientOffset(box, 112f, 0.34f);
        Vector2 d = GlassGeometry.GradientDirection(112f);
        Vector2 onLine = d * offset;
        AssertThat(Mathf.Abs(GlassGeometry.GradientT(onLine, box, 112f) - 0.34f) < 1e-4f).IsTrue();
        var square = new Rect2(0f, 0f, 100f, 100f);
        AssertThat(Mathf.Abs(GlassGeometry.GradientT(Vector2.Zero, square, 135f)) < 1e-4f).IsTrue();
        AssertThat(Mathf.Abs(GlassGeometry.GradientT(new Vector2(100f, 100f), square, 135f) - 1f) < 1e-4f).IsTrue();
        // Two- and three-stop colours.
        Color mid = GlassGeometry.Stop3(Colors.White, Colors.Red, 0.4f, Colors.Black, 0.4f);
        AssertThat(mid).IsEqual(Colors.Red);
        AssertThat(GlassGeometry.Stop2(Colors.Black, Colors.White, 2f)).IsEqual(Colors.White);
    }
}
