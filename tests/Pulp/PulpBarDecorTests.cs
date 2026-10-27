using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>
/// The glass tube (TG-WIDGETS step 2, GAUGE-A, UI_SPEC_GLASS 3.4.1 and 3.6; first written by UR-WIDGETS for the pulp
/// overlay): fractions, the low pulse, the bar's own fill left alone, the lean and its mirror, the echo band and the boss tube.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpBarDecorTests {

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    private static async Task Frames(int count = 2) {
        for (int i = 0; i < count; i++) await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
    }

    private static ProgressBar Bar(Vector2 size, string variation, double value) {
        var bar = new ProgressBar {
            ThemeTypeVariation = variation, MinValue = 0, MaxValue = 100, Value = value, ShowPercentage = false,
            CustomMinimumSize = size, Size = size
        };
        UIPalette.ApplyTheme(bar);
        Tree.Root.AddChild(bar);
        return bar;
    }

    private static bool Near(Vector2 a, Vector2 b) => a.DistanceTo(b) < 0.01f;

    private static bool Has(Vector2[] points, Vector2 point) {
        foreach (Vector2 p in points) if (Near(p, point)) return true;
        return false;
    }

    private static int IndexOf(Vector2[] points, Vector2 point) {
        for (int i = 0; i < points.Length; i++) if (Near(points[i], point)) return i;
        return -1;
    }

    [TestCase]
    public void TheFractionsAreClampedToTheBarsRange() {
        PulpBarDecor.Fractions f = PulpBarDecor.FractionsFor(72, 85, 80, 0, 100);
        AssertThat(f.Value).IsEqual(0.72f);
        AssertThat(f.Lag).IsEqual(0.85f);
        AssertThat(f.Echo).IsEqual(0.8f);
        PulpBarDecor.Fractions clamped = PulpBarDecor.FractionsFor(-5, 400, 50, 0, 200);
        AssertThat(clamped.Value).IsEqual(0f);
        AssertThat(clamped.Lag).IsEqual(1f);
        AssertThat(clamped.Echo).IsEqual(0.25f);
        AssertThat(PulpBarDecor.FractionsFor(1, 1, 1, 5, 5).Value).IsEqual(0f);
    }

    /// <summary>TG-WIDGETS update of TheLowGlowIsOffStaticWhenReducedAndASlowSineOtherwise (MOT-A row 11).</summary>
    [TestCase]
    public void TheLowGlowPulsesAtHalfAHertzAndIsSteadyUnderReducedEffects() {
        AssertThat(PulpBarDecor.GlowStrengthAt(false, 0.3, false)).IsEqual(0f);
        AssertThat(PulpBarDecor.GlowStrengthAt(false, 0.3, true)).IsEqual(0f);
        // Reduced: a steady 7 px glow at full alpha.
        AssertThat(PulpBarDecor.GlowStrengthAt(true, 0.3, true)).IsEqual(1f);
        AssertThat(PulpBarDecor.GlowStrengthAt(true, 7.7, true)).IsEqual(1f);
        // 0.5 Hz: nothing at the ends of each 2 s cycle, full in its middle.
        AssertThat(PulpBarDecor.LowPulseSeconds).IsEqual(2f);
        AssertThat(Mathf.IsZeroApprox(PulpBarDecor.GlowStrengthAt(true, 0.0, false))).IsTrue();
        AssertThat(Mathf.IsEqualApprox(PulpBarDecor.GlowStrengthAt(true, 1.0, false), 1f)).IsTrue();
        AssertThat(Mathf.IsZeroApprox(PulpBarDecor.GlowStrengthAt(true, 2.0, false))).IsTrue();
        AssertThat(Mathf.IsEqualApprox(PulpBarDecor.GlowStrengthAt(true, 3.0, false), 1f)).IsTrue();
        // The curve is continuous: no two samples a frame apart differ by much, so it can never strobe.
        for (int i = 0; i < 240; i++) {
            float a = PulpBarDecor.GlowStrengthAt(true, i / 60.0, false);
            float b = PulpBarDecor.GlowStrengthAt(true, (i + 1) / 60.0, false);
            if (Mathf.Abs(a - b) > 0.06f) AssertThat($"glow jumped {a} -> {b} at frame {i}").IsEqual("");
        }
    }

    [TestCase]
    public void TheDecorOverlaysTheBarWithoutTouchingItsFill() {
        var bar = new ProgressBar {
            ThemeTypeVariation = UIPalette.Variations.PulpBar, MinValue = 0, MaxValue = 100, Value = 40,
            ShowPercentage = false, CustomMinimumSize = new Vector2(380f, 30f), Size = new Vector2(380f, 30f)
        };
        UIPalette.ApplyTheme(bar);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(bar);
        try {
            StyleBox fillBefore = bar.GetThemeStylebox("fill");
            bool overrideBefore = bar.HasThemeStyleboxOverride("fill");
            PulpBarDecor decor = PulpBarDecor.Attach(bar);
            AssertThat(PulpBarDecor.Attach(bar)).IsEqual(decor);
            AssertThat(decor.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            AssertThat(decor.GetParent()).IsEqual(bar);
            AssertThat(bar.HasThemeStyleboxOverride("fill")).IsEqual(overrideBefore);
            AssertThat(bar.GetThemeStylebox("fill")).IsEqual(fillBefore);
            AssertThat(fillBefore is StyleBoxFlat).IsTrue();
            // TG-CLOSE: the bar variation is a value carrier: an empty background, and a fill that holds its colour (the
            // tube reads it; FighterHUD.TintFill writes it) but draws nothing, so only the tube shows.
            AssertThat(bar.GetThemeStylebox("background") is StyleBoxEmpty).IsTrue();
            AssertThat(((StyleBoxFlat)fillBefore).DrawCenter).IsFalse();
            AssertThat(((StyleBoxFlat)fillBefore).BgColor.IsEqualApprox(UIPalette.Hp)).IsTrue();
            decor.LagValue = 10f;
            decor.DropLagTo(60f);
            AssertThat(decor.LagValue).IsEqual(60f);
            decor.Low = true;
            decor.Low = false;
            AssertThat(decor.GlowStrength).IsEqual(0f);
        } finally {
            bar.Free();
        }
    }

    [TestCase]
    public async Task TheTubeLeansAndAMirroredBarMirrorsItsLeanAnchorGradientAndCap() {
        ProgressBar bar = Bar(new Vector2(380f, 30f), UIPalette.Variations.PulpBar, 72);
        var problems = new List<string>();
        try {
            PulpBarDecor decor = PulpBarDecor.Attach(bar);
            await Frames();
            // The bar stays the value carrier and draws nothing of its own: its variation's styles are empty (TG-CLOSE retired
            // the hide-self material), so the bar carries no material; its SelfModulate stays the HUDs' flash channel (the
            // not-yet tint), and the tube follows it.
            AssertObject(bar.Material).IsNull();
            AssertThat(bar.GetThemeStylebox("background") is StyleBoxEmpty).IsTrue();
            AssertThat(decor.UseParentMaterial).IsFalse();
            AssertThat(bar.SelfModulate).IsEqual(Colors.White);
            AssertThat(decor.Visible).IsTrue();
            var flash = new Color(0f, 0f, 0f, 1f);
            bar.SelfModulate = flash;
            await Frames();
            AssertThat(decor.SelfModulate).IsEqual(flash);
            bar.SelfModulate = Colors.White;
            await Frames();
            AssertThat(decor.SelfModulate).IsEqual(Colors.White);
            // 3.4.1: one parallelogram leaning "/", run 12 at any height; the inner one inset 1.5 on run 11.
            Vector2[] outer = decor.OuterPolygon;
            foreach (Vector2 p in new[] { new Vector2(12f, 0f), new Vector2(380f, 0f), new Vector2(368f, 30f), new Vector2(0f, 30f) }) {
                if (!Has(outer, p)) problems.Add($"outer lacks {p}");
            }
            Vector2[] inner = decor.InnerPolygon;
            foreach (Vector2 p in new[] { new Vector2(12.5f, 1.5f), new Vector2(378.5f, 1.5f), new Vector2(367.5f, 28.5f), new Vector2(1.5f, 28.5f) }) {
                if (!Has(inner, p)) problems.Add($"inner lacks {p}");
            }
            // The fill: from the anchored (slanted) left end to a vertical moving end at 72 % of the inner width.
            float moving = 1.5f + 0.72f * 377f;
            Vector2[] fill = decor.FillPolygon;
            if (!Has(fill, new Vector2(12.5f, 1.5f)) || !Has(fill, new Vector2(1.5f, 28.5f))) problems.Add("the fill does not follow the slant at its anchored end");
            if (!Has(fill, new Vector2(moving, 1.5f)) || !Has(fill, new Vector2(moving, 28.5f))) problems.Add($"the fill does not end on a vertical at {moving}");
            foreach (Vector2 p in fill) if (p.X > moving + 0.01f) problems.Add($"the fill reaches {p}");
            // Its gradient runs hp2 at the anchored end to hp at the moving end.
            AssertThat(decor.FillGradient.Anchored).IsEqual(UIPalette.Hp2);
            AssertThat(decor.FillGradient.Moving).IsEqual(UIPalette.Hp);
            Color[] colors = decor.FillColors;
            if (!colors[IndexOf(fill, new Vector2(1.5f, 28.5f))].IsEqualApprox(UIPalette.Hp2)) problems.Add("the anchored corner is not hp2");
            if (!colors[IndexOf(fill, new Vector2(moving, 1.5f))].IsEqualApprox(UIPalette.Hp)) problems.Add("the moving edge is not hp");
            // The 3 px cap sits on the moving edge; ten decile ticks.
            foreach (Vector2 p in decor.CapPolygon) if (p.X < moving - 3.01f || p.X > moving + 0.01f) problems.Add($"the cap reaches {p}");
            AssertThat(decor.TickPolygons).IsEqual(10);

            // Mirrored (R6): FillMode EndToBegin leans the tube "\" and anchors fill, gradient and cap on the right.
            int builds = decor.GeometryBuilds;
            bar.FillMode = (int)ProgressBar.FillModeEnum.EndToBegin;
            await Frames();
            AssertThat(decor.Mirrored).IsTrue();
            AssertThat(decor.GeometryBuilds > builds).IsTrue();
            outer = decor.OuterPolygon;
            foreach (Vector2 p in new[] { new Vector2(0f, 0f), new Vector2(368f, 0f), new Vector2(380f, 30f), new Vector2(12f, 30f) }) {
                if (!Has(outer, p)) problems.Add($"mirrored outer lacks {p}");
            }
            float mirroredMoving = 378.5f - 0.72f * 377f;
            fill = decor.FillPolygon;
            if (!Has(fill, new Vector2(367.5f, 1.5f)) || !Has(fill, new Vector2(378.5f, 28.5f))) problems.Add("the mirrored fill does not follow the slant at its anchored right end");
            if (!Has(fill, new Vector2(mirroredMoving, 1.5f)) || !Has(fill, new Vector2(mirroredMoving, 28.5f))) problems.Add($"the mirrored fill does not end on a vertical at {mirroredMoving}");
            foreach (Vector2 p in fill) if (p.X < mirroredMoving - 0.01f) problems.Add($"the mirrored fill reaches {p}");
            colors = decor.FillColors;
            if (!colors[IndexOf(fill, new Vector2(378.5f, 28.5f))].IsEqualApprox(UIPalette.Hp2)) problems.Add("the mirrored anchored corner is not hp2");
            if (!colors[IndexOf(fill, new Vector2(mirroredMoving, 1.5f))].IsEqualApprox(UIPalette.Hp)) problems.Add("the mirrored moving edge is not hp");
            foreach (Vector2 p in decor.CapPolygon) if (p.X < mirroredMoving - 0.01f || p.X > mirroredMoving + 3.01f) problems.Add($"the mirrored cap reaches {p}");

            // A redraw with no change rebuilds nothing.
            builds = decor.GeometryBuilds;
            for (int i = 0; i < 3; i++) {
                decor.QueueRedraw();
                await Frames(1);
            }
            AssertThat(decor.GeometryBuilds).IsEqual(builds);

            // Taken off its bar, the decor gives the bar its own drawing back.
            bar.RemoveChild(decor);
            AssertThat(bar.Material == null).IsTrue();
            decor.Free();
        } finally {
            bar.Free();
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    private static float TrackerAlpha(PulpBarDecor decor) {
        Color[] colors = decor.TrackerColors;
        return colors.Length == 0 ? -1f : colors[0].A;
    }

    /// <summary>
    /// TG-CLOSE polish, MOT-A row 7 under Reduced Temporal Effects: "tracker fades out over 200 ms instead of draining".
    /// The damage tracker keeps its extent (no drain) while its alpha falls to nothing over 0.2 s, then it is gone and the
    /// next hit's tracker shows at full strength again, a heal in the middle of a fade included. It used to sit unchanged
    /// for 0.2 s and then snap (DropLagTo passed no fade).
    /// </summary>
    [TestCase]
    public async Task UnderReducedEffectsTheDamageTrackerFadesOutOverTwoHundredMillisecondsInsteadOfDraining() {
        bool reduced = FTT.Core.ComfortSettings.ReducedTemporalEffects;
        ProgressBar bar = Bar(new Vector2(380f, 30f), UIPalette.Variations.PulpBar, 100);
        var problems = new List<string>();
        try {
            FTT.Core.ComfortSettings.Apply(true);
            PulpBarDecor decor = PulpBarDecor.Attach(bar);
            decor.SnapLagTo(100f);
            await Frames();
            bar.Value = 60;
            decor.DropLagTo(60f);
            float full = TrackerAlpha(decor);
            if (full < 0.5f) problems.Add($"the tracker starts at alpha {full}");
            await Frames(6);   // 0.1 s in
            float mid = TrackerAlpha(decor);
            if (decor.LagValue != 100f) problems.Add($"the tracker drained to {decor.LagValue} (it should only fade)");
            if (!(mid > full * 0.15f && mid < full * 0.85f)) problems.Add($"0.1 s in, the tracker's alpha is {mid} of {full} (fading over 0.2 s it is about half)");
            await Frames(9);   // 0.25 s in
            if (decor.LagValue != 60f) problems.Add($"after the fade the tracker is at {decor.LagValue}, not the fill");
            if (decor.TrackerPolygon.Length != 0) problems.Add("after the fade the tracker still shows");

            // A heal in the middle of a fade ends it, and the next hit's tracker is at full strength.
            bar.Value = 30;
            decor.DropLagTo(30f);
            await Frames(4);
            bar.Value = 50;
            decor.SnapLagTo(50f);
            bar.Value = 20;
            decor.DropLagTo(20f);
            float again = TrackerAlpha(decor);
            if (Mathf.Abs(again - full) > 0.01f) problems.Add($"after a heal mid-fade the next tracker shows at alpha {again}, not {full}");
        } finally {
            FTT.Core.ComfortSettings.Apply(reduced);
            bar.Free();
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    [TestCase]
    public async Task TheEchoBandRunsFromTheFillToHpPlusEchoInsetTwentyTwoPercent() {
        ProgressBar bar = Bar(new Vector2(380f, 30f), UIPalette.Variations.PulpBar, 60);
        var problems = new List<string>();
        try {
            PulpBarDecor decor = PulpBarDecor.Attach(bar);
            decor.EchoValue = 70f;
            decor.LagValue = 85f;
            await Frames();
            // D1: from the fill's end (60 %) to HP + echo (70 %), inset 22 % of the inner height top and bottom.
            float x0 = 1.5f + 0.6f * 377f, x1 = 1.5f + 0.7f * 377f;
            float y0 = 1.5f + 0.22f * 27f, y1 = 28.5f - 0.22f * 27f;
            Vector2[] echo = decor.EchoPolygon;
            AssertThat(echo.Length).IsEqual(4);
            foreach (Vector2 p in new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) }) {
                if (!Has(echo, p)) problems.Add($"the band lacks {p}");
            }
            // The tracker runs from the anchored end to the lag (85 %), under the fill.
            float lag = 1.5f + 0.85f * 377f;
            Vector2[] tracker = decor.TrackerPolygon;
            if (!Has(tracker, new Vector2(lag, 1.5f)) || !Has(tracker, new Vector2(lag, 28.5f))) problems.Add($"the tracker does not end at {lag}");
            // No echo past the fill: no band; and the band follows the value.
            decor.EchoValue = 55f;
            await Frames(1);
            AssertThat(decor.EchoPolygon.Length).IsEqual(0);
            decor.EchoValue = 90f;
            await Frames(1);
            if (!Has(decor.EchoPolygon, new Vector2(1.5f + 0.9f * 377f, y0))) problems.Add("the band does not follow the echo");
        } finally {
            bar.Free();
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    [TestCase]
    public async Task TheBossTubeDrawsTheBossEdgeAndNoCapOrTicks() {
        ProgressBar bar = Bar(new Vector2(840f, 34f), UIPalette.Variations.PulpBossBar, 64);
        var problems = new List<string>();
        try {
            PulpBarDecor decor = PulpBarDecor.Attach(bar, PulpBarStyle.Boss);
            decor.LagValue = 80f;
            decor.PhaseNotches = new[] { 0.5f };
            await Frames();
            // 3.6: run 16; the 110 degree boss edge; boss2 to boss; a white tracker; no cap and no ticks.
            AssertThat(decor.Run).IsEqual(16f);
            Vector2[] outer = decor.OuterPolygon;
            foreach (Vector2 p in new[] { new Vector2(16f, 0f), new Vector2(840f, 0f), new Vector2(824f, 34f), new Vector2(0f, 34f) }) {
                if (!Has(outer, p)) problems.Add($"outer lacks {p}");
            }
            var box = new Rect2(0f, 0f, 840f, 34f);
            Color[] edge = decor.EdgeColors;
            for (int i = 0; i < outer.Length; i++) {
                Color expected = GlassGeometry.Stop3(new Color(UIPalette.BossEdge, 0.95f), new Color(UIPalette.BossEdge, 0.25f), 0.40f,
                    new Color(1f, 1f, 1f, 0.5f), GlassGeometry.GradientT(outer[i], box, 110f));
                if (!edge[i].IsEqualApprox(expected)) problems.Add($"edge vertex {i} is {edge[i]}, not {expected}");
                if (edge[i].IsEqualApprox(UIPalette.Edge)) problems.Add($"edge vertex {i} is the gold edge");
            }
            AssertThat(decor.FillGradient.Anchored).IsEqual(UIPalette.Boss2);
            AssertThat(decor.FillGradient.Moving).IsEqual(UIPalette.Boss);
            AssertThat(decor.TrackerPolygon.Length >= 3).IsTrue();
            AssertThat(decor.CapPolygon.Length).IsEqual(0);
            AssertThat(decor.TickPolygons).IsEqual(0);
            // An HP tube on the same values keeps its cap and ticks.
            decor.Style = PulpBarStyle.Hp;
            await Frames(1);
            AssertThat(decor.CapPolygon.Length >= 3).IsTrue();
            AssertThat(decor.TickPolygons).IsEqual(10);
        } finally {
            bar.Free();
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }
}
