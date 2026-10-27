using System;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// TG-WIDGETS. The Temporal Glass shapes as plain math (UI_SPEC_GLASS 0.2, 0.3, 3.0-3.4): the chamfer polygon, the
    /// chevron, the parallelogram, a region clipped by half-planes (a tube fill's slanted anchored end and vertical moving
    /// end), circle polygons with UVs, and the CSS angle gradient's <c>t</c> with its vertex colours. Pure: no node, no
    /// resource, no engine call, so the widgets build their geometry here once per size and tests check it directly.
    ///
    /// <para>Every method that returns an array allocates; each has an <c>into</c> overload (or a count-returning form)
    /// for a caller that keeps a buffer. Widgets call these only when their size or state changes, never per frame.</para>
    /// </summary>
    public static class GlassGeometry {

        /// <summary>The hairline every glass surface leaves between its edge layer and its fill layer (0.3).</summary>
        public const float EdgeWidth = 1.5f;
        /// <summary>The chevron's fixed horizontal run to its tip, at any height (3.2).</summary>
        public const float ChevronRun = 30f;

        // ---- Chamfer (G1, G4, G5) --------------------------------------------------------------------------

        /// <summary>
        /// The six corners of <paramref name="box"/> with its top-left and bottom-right corners cut at 45 degrees by
        /// <paramref name="n"/> (0.2 "chamfer N TL/BR"), clockwise from the top-left cut: (n, 0), (W, 0), (W, H - n),
        /// (W - n, H), (0, H), (0, n). <paramref name="n"/> is clamped to half the shorter side.
        /// </summary>
        public static Vector2[] Chamfer(Rect2 box, float n) {
            var points = new Vector2[6];
            Chamfer(box, n, points);
            return points;
        }

        /// <summary>Writes the chamfer polygon into <paramref name="into"/> (length 6).</summary>
        public static void Chamfer(Rect2 box, float n, Vector2[] into) {
            n = Mathf.Clamp(n, 0f, Mathf.Min(box.Size.X, box.Size.Y) / 2f);
            float l = box.Position.X, t = box.Position.Y, r = box.End.X, b = box.End.Y;
            into[0] = new Vector2(l + n, t);
            into[1] = new Vector2(r, t);
            into[2] = new Vector2(r, b - n);
            into[3] = new Vector2(r - n, b);
            into[4] = new Vector2(l, b);
            into[5] = new Vector2(l, t + n);
        }

        /// <summary>
        /// The fill layer of a chamfered glass surface: the box inset by <paramref name="edge"/> on every side, with the
        /// chamfer reduced by one pixel per 1.5 px of inset (3.1: <c>polygon(17 0, ...)</c> for chamfer 18), so the hairline
        /// along each 45 degree cut keeps 1.41 px. For a 2 px inset (the portrait frame, G5) the cut follows the inset box's
        /// own percentage instead; pass the cut explicitly with <see cref="Chamfer(Rect2, float)"/> there.
        /// </summary>
        public static Vector2[] ChamferFill(Rect2 box, float n, float edge = EdgeWidth) {
            var points = new Vector2[6];
            ChamferFill(box, n, edge, points);
            return points;
        }

        /// <summary>Writes the chamfer fill polygon into <paramref name="into"/> (length 6).</summary>
        public static void ChamferFill(Rect2 box, float n, float edge, Vector2[] into) =>
            Chamfer(box.Grow(-edge), Mathf.Max(0f, n - edge / 1.5f), into);

        // ---- Chevron (G3) ----------------------------------------------------------------------------------

        /// <summary>
        /// The chevron button's edge shape (3.2): a square left end and an arrow tip at mid-height whose slanted sides keep
        /// a fixed horizontal <paramref name="run"/> whatever the height: (0, 0), (W - run, 0), (W, H / 2), (W - run, H), (0, H).
        /// </summary>
        public static Vector2[] Chevron(Rect2 box, float run = ChevronRun) {
            float l = box.Position.X, t = box.Position.Y, r = box.End.X, b = box.End.Y;
            run = Mathf.Clamp(run, 0f, box.Size.X);
            return new[] {
                new Vector2(l, t), new Vector2(r - run, t), new Vector2(r, t + box.Size.Y / 2f),
                new Vector2(r - run, b), new Vector2(l, b)
            };
        }

        /// <summary>
        /// The chevron's fill layer (3.2): the box inset by 1.5 px, clipped to <c>polygon(0 0, W' - 31 0, W' - 1 H'/2,
        /// W' - 31 H', 0 H')</c> in the inset box's own coordinates (W' = W - 3, H' = H - 3).
        /// </summary>
        public static Vector2[] ChevronFill(Rect2 box, float run = ChevronRun, float edge = EdgeWidth) {
            Rect2 inner = box.Grow(-edge);
            float l = inner.Position.X, t = inner.Position.Y, w = inner.Size.X, h = inner.Size.Y;
            return new[] {
                new Vector2(l, t), new Vector2(l + w - run - 1f, t), new Vector2(l + w - 1f, t + h / 2f),
                new Vector2(l + w - run - 1f, t + h), new Vector2(l, t + h)
            };
        }

        // ---- Parallelogram (G10, G11, G13) ---------------------------------------------------------------

        /// <summary>
        /// A parallelogram filling <paramref name="box"/> whose top edge is shifted <paramref name="run"/> px right of its
        /// bottom edge, leaning "/" (0.2): (R, 0), (W, 0), (W - R, H), (0, H). <paramref name="mirrored"/> leans it "\":
        /// (0, 0), (W - R, 0), (W, H), (R, H). Both slanted ends have the same run R at any height.
        /// </summary>
        public static Vector2[] Parallelogram(Rect2 box, float run, bool mirrored = false) {
            var points = new Vector2[4];
            Parallelogram(box, run, mirrored, points);
            return points;
        }

        /// <summary>Writes the parallelogram into <paramref name="into"/> (length 4).</summary>
        public static void Parallelogram(Rect2 box, float run, bool mirrored, Vector2[] into) {
            float l = box.Position.X, t = box.Position.Y, r = box.End.X, b = box.End.Y;
            run = Mathf.Clamp(run, 0f, box.Size.X);
            if (!mirrored) {
                into[0] = new Vector2(l + run, t); into[1] = new Vector2(r, t);
                into[2] = new Vector2(r - run, b); into[3] = new Vector2(l, b);
            } else {
                into[0] = new Vector2(l, t); into[1] = new Vector2(r - run, t);
                into[2] = new Vector2(r, b); into[3] = new Vector2(l + run, b);
            }
        }

        // ---- Clipping ---------------------------------------------------------------------------------------

        /// <summary>
        /// Clips a convex polygon to the half-plane <c>dot(p, normal) &lt;= offset</c> (Sutherland-Hodgman) into
        /// <paramref name="output"/> and returns the vertex count. The output needs room for <c>count + 1</c> points.
        /// </summary>
        public static int ClipHalfPlane(Vector2[] input, int count, Vector2 normal, float offset, Vector2[] output) {
            int written = 0;
            if (count == 0) return 0;
            Vector2 previous = input[count - 1];
            float previousSide = previous.Dot(normal) - offset;
            for (int i = 0; i < count; i++) {
                Vector2 current = input[i];
                float side = current.Dot(normal) - offset;
                bool inside = side <= 0f, previousInside = previousSide <= 0f;
                if (inside != previousInside) {
                    float t = previousSide / (previousSide - side);
                    output[written++] = previous + (current - previous) * t;
                }
                if (inside) output[written++] = current;
                previous = current;
                previousSide = side;
            }
            return written;
        }

        /// <summary>
        /// The part of a convex polygon between two vertical lines <c>x0 &lt;= x &lt;= x1</c>: a fill clipped to a tube keeps
        /// the tube's slanted end where it reaches it and ends on a vertical edge where it stops (3.4.1). Allocates the result.
        /// </summary>
        public static Vector2[] ClipVerticalSlab(Vector2[] polygon, float x0, float x1) {
            var a = new Vector2[polygon.Length + 2];
            var b = new Vector2[polygon.Length + 2];
            int n = ClipHalfPlane(polygon, polygon.Length, new Vector2(-1f, 0f), -x0, a);
            n = ClipHalfPlane(a, n, new Vector2(1f, 0f), x1, b);
            var result = new Vector2[n];
            Array.Copy(b, result, n);
            return result;
        }

        /// <summary>
        /// A tube fill (3.4.1): the region of the parallelogram <paramref name="tube"/> leaning by <paramref name="run"/> from
        /// its anchored end to <paramref name="fraction"/> of its width. The anchored end is the left end, or the right end
        /// when <paramref name="mirrored"/> (the tube then leans "\" and the fill grows from the right); the moving end is
        /// vertical. Empty for a fraction of 0.
        /// </summary>
        public static Vector2[] ClippedFill(Rect2 tube, float run, float fraction, bool mirrored = false) {
            fraction = Mathf.Clamp(fraction, 0f, 1f);
            if (fraction <= 0f) return Array.Empty<Vector2>();
            Vector2[] shape = Parallelogram(tube, run, mirrored);
            float w = tube.Size.X * fraction;
            return mirrored
                ? ClipVerticalSlab(shape, tube.End.X - w, tube.End.X)
                : ClipVerticalSlab(shape, tube.Position.X, tube.Position.X + w);
        }

        // ---- Circles --------------------------------------------------------------------------------------

        /// <summary>
        /// A circle polygon of <paramref name="segments"/> points starting at 12 o'clock, clockwise, with UVs mapping
        /// <paramref name="uvBox"/> to 0..1 (for a radial gradient or art clipped to the circle). Both buffers must hold
        /// <paramref name="segments"/> points.
        /// </summary>
        public static void Circle(Vector2 centre, float radius, int segments, Rect2 uvBox, Vector2[] points, Vector2[] uvs) {
            for (int i = 0; i < segments; i++) {
                float a = Mathf.Tau * i / segments;
                var p = new Vector2(centre.X + Mathf.Sin(a) * radius, centre.Y - Mathf.Cos(a) * radius);
                points[i] = p;
                if (uvs != null) uvs[i] = new Vector2((p.X - uvBox.Position.X) / uvBox.Size.X, (p.Y - uvBox.Position.Y) / uvBox.Size.Y);
            }
        }

        /// <summary>UVs of <paramref name="points"/> inside <paramref name="uvBox"/> (0..1 across the box), into <paramref name="uvs"/>.</summary>
        public static void BoxUvs(Vector2[] points, int count, Rect2 uvBox, Vector2[] uvs) {
            for (int i = 0; i < count; i++) {
                uvs[i] = new Vector2((points[i].X - uvBox.Position.X) / uvBox.Size.X, (points[i].Y - uvBox.Position.Y) / uvBox.Size.Y);
            }
        }

        // ---- The CSS angle gradient (0.2) ---------------------------------------------------------------

        /// <summary>The CSS gradient direction for <paramref name="degrees"/>: (sin, -cos) in screen space (y down).</summary>
        public static Vector2 GradientDirection(float degrees) {
            float a = Mathf.DegToRad(degrees);
            return new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
        }

        /// <summary>The CSS gradient line length of a box: <c>W |sin| + H |cos|</c>.</summary>
        public static float GradientLength(Vector2 size, float degrees) {
            float a = Mathf.DegToRad(degrees);
            return size.X * Mathf.Abs(Mathf.Sin(a)) + size.Y * Mathf.Abs(Mathf.Cos(a));
        }

        /// <summary>
        /// Where <paramref name="p"/> sits on the CSS linear gradient of <paramref name="box"/> at <paramref name="degrees"/>
        /// (0.2): <c>t = 0.5 + ((p - centre) . d) / L</c>, unclamped. 90 degrees runs left to right, 180 top to bottom.
        /// </summary>
        public static float GradientT(Vector2 p, Rect2 box, float degrees) {
            float length = GradientLength(box.Size, degrees);
            if (length <= 0f) return 0f;
            return 0.5f + (p - box.GetCenter()).Dot(GradientDirection(degrees)) / length;
        }

        /// <summary>
        /// The half-plane offset for "<c>t &lt;= value</c>" on the gradient of <paramref name="box"/>: points with
        /// <c>dot(p, GradientDirection) &lt;= offset</c>. Pair it with <see cref="ClipHalfPlane"/>.
        /// </summary>
        public static float GradientOffset(Rect2 box, float degrees, float t) =>
            box.GetCenter().Dot(GradientDirection(degrees)) + (t - 0.5f) * GradientLength(box.Size, degrees);

        /// <summary>A two-stop colour at <paramref name="t"/> (clamped), as the vertex colours of a gradient quad interpolate it.</summary>
        public static Color Stop2(Color from, Color to, float t) => from.Lerp(to, Mathf.Clamp(t, 0f, 1f));

        /// <summary>A three-stop colour (from at 0, mid at <paramref name="midAt"/>, to at 1) at <paramref name="t"/> (clamped).</summary>
        public static Color Stop3(Color from, Color mid, float midAt, Color to, float t) {
            t = Mathf.Clamp(t, 0f, 1f);
            if (t <= midAt) return midAt <= 0f ? mid : from.Lerp(mid, t / midAt);
            return midAt >= 1f ? mid : mid.Lerp(to, (t - midAt) / (1f - midAt));
        }

        /// <summary>
        /// The vertex colours of a two-stop linear gradient over <paramref name="points"/>, as <paramref name="box"/>'s CSS
        /// gradient at <paramref name="degrees"/> paints it. Exact for any convex polygon, because the colour is linear in
        /// the position (a fan triangulation interpolates it exactly). Writes into <paramref name="colors"/>.
        /// </summary>
        public static void GradientColors(Vector2[] points, int count, Rect2 box, float degrees, Color from, Color to, Color[] colors) {
            for (int i = 0; i < count; i++) colors[i] = Stop2(from, to, GradientT(points[i], box, degrees));
        }

        /// <summary>Allocating form of <see cref="GradientColors(Vector2[], int, Rect2, float, Color, Color, Color[])"/>.</summary>
        public static Color[] GradientColors(Vector2[] points, Rect2 box, float degrees, Color from, Color to) {
            var colors = new Color[points.Length];
            GradientColors(points, points.Length, box, degrees, from, to, colors);
            return colors;
        }

        // ---- Soft glow --------------------------------------------------------------------------------------

        /// <summary>
        /// The alpha a CSS blur of radius <paramref name="blur"/> (sigma = blur / 2) leaves at <paramref name="distance"/>
        /// outside a straight edge, for a full-alpha shape: half at the edge, falling to about 2 % at the blur radius.
        /// </summary>
        public static float BlurAlpha(float distance, float blur) {
            if (blur <= 0f) return distance <= 0f ? 1f : 0f;
            float sigma = blur / 2f;
            return 0.5f * Erfc(distance / (sigma * Mathf.Sqrt2));
        }

        // Abramowitz-Stegun 7.1.26 (|error| < 1.5e-7).
        private static float Erfc(float x) {
            float z = Mathf.Abs(x);
            float t = 1f / (1f + 0.3275911f * z);
            float poly = t * (0.254829592f + t * (-0.284496736f + t * (1.421413741f + t * (-1.453152027f + t * 1.061405429f))));
            float erfc = poly * Mathf.Exp(-z * z);
            return x >= 0f ? erfc : 2f - erfc;
        }
    }
}
