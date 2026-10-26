using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>UR-WIDGETS step 2. The bar overlay: fractions, the low glow curve, and that the bar's own fill is left alone.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpBarDecorTests {

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

    [TestCase]
    public void TheLowGlowIsOffStaticWhenReducedAndASlowSineOtherwise() {
        AssertThat(PulpBarDecor.GlowStrengthAt(false, 0.3, false)).IsEqual(0f);
        AssertThat(PulpBarDecor.GlowStrengthAt(true, 0.3, true)).IsEqual(0.6f);
        AssertThat(PulpBarDecor.GlowStrengthAt(true, 7.7, true)).IsEqual(0.6f);
        float peak = PulpBarDecor.GlowStrengthAt(true, 0.0, false);
        float trough = PulpBarDecor.GlowStrengthAt(true, PulpBarDecor.LowPulseSeconds / 2.0, false);
        AssertThat(Mathf.IsEqualApprox(peak, 1f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(trough, 0f)).IsTrue();
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
            AssertThat(Mathf.IsEqualApprox(decor.Skew, 0.2126f)).IsTrue();
            Rect2 inner = decor.InnerRect();
            AssertThat(inner.Position).IsEqual(new Vector2(3f, 3f));
            AssertThat(inner.Size).IsEqual(new Vector2(374f, 24f));
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
}
