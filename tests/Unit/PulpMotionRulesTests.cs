using FTT.UI.Motion;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-THEME, re-pinned by TG-THEME. The MOT-A "glass drift" numbers and curves as plain C# (UI_SPEC_GLASS section 5).
/// Pure: no Godot type is touched, so this suite needs no runtime (CLAUDE.md failure signature 7).
/// </summary>
[TestSuite]
public class PulpMotionRulesTests {

    private static bool Close(float a, float b, float tolerance = 1e-4f) => System.Math.Abs(a - b) <= tolerance;

    /// <summary>TG-THEME rewrite of SteppedProgressKeepsItsEndpointsAndQuantisesTheMiddle: the tracker drains smoothly now.</summary>
    [TestCase]
    public void TheTrackerHoldsThenDrainsSmoothlyAndFadesUnderReducedEffects() {
        // Held at the old value through the hold.
        AssertThat(PulpMotionRules.TrackerValueAt(0f, 100f, 40f)).IsEqual(100f);
        AssertThat(PulpMotionRules.TrackerValueAt(PulpMotionRules.TrackerHoldSeconds, 100f, 40f)).IsEqual(100f);
        // Halfway through the drain it is exactly halfway (Sine InOut is symmetric)...
        float half = PulpMotionRules.TrackerHoldSeconds + PulpMotionRules.TrackerDrainSeconds / 2f;
        AssertThat(Close(PulpMotionRules.TrackerValueAt(half, 100f, 40f), 70f)).IsTrue();
        // ...it eases in (the first 10 ms move it less than a tenth of a percent of the drop)...
        float early = PulpMotionRules.TrackerValueAt(PulpMotionRules.TrackerHoldSeconds + 0.01f, 100f, 40f);
        AssertThat(early < 100f && early > 99.94f).OverrideFailureMessage($"the drain should start gently, was {early}").IsTrue();
        // ...never steps back up, and lands exactly on the new value.
        float previous = 100f;
        for (float t = 0f; t <= 1.5f; t += 0.01f) {
            float value = PulpMotionRules.TrackerValueAt(t, 100f, 40f);
            AssertThat(value <= previous + 1e-4f).OverrideFailureMessage($"the tracker rose at {t}").IsTrue();
            previous = value;
        }
        AssertThat(PulpMotionRules.TrackerValueAt(PulpMotionRules.TrackerHoldSeconds + PulpMotionRules.TrackerDrainSeconds, 100f, 40f)).IsEqual(40f);
        AssertThat(PulpMotionRules.TrackerValueAt(5f, 100f, 40f)).IsEqual(40f);

        // Reduced Temporal Effects: the tracker fades out over 200 ms instead of draining.
        AssertThat(PulpMotionRules.TrackerFadeAlphaAt(0f)).IsEqual(1f);
        AssertThat(Close(PulpMotionRules.TrackerFadeAlphaAt(0.1f), 0.5f)).IsTrue();
        AssertThat(PulpMotionRules.TrackerFadeAlphaAt(PulpMotionRules.TrackerFadeSeconds)).IsEqual(0f);
        AssertThat(PulpMotionRules.TrackerFadeAlphaAt(1f)).IsEqual(0f);
    }

    [TestCase]
    public void TheTimingsAreTheSpecsAndNothingFlashesFasterThanOnceASecond() {
        // Focus: a 14 px slide over 120 ms, no tilt and no scale; the press moves the row 8 px back and scales it 0.985.
        AssertThat(PulpMotionRules.FocusInSeconds).IsEqual(0.12f);
        AssertThat(PulpMotionRules.FocusOutSeconds).IsEqual(0.12f);
        AssertThat(PulpMotionRules.FocusTranslateX).IsEqual(14f);
        AssertThat(PulpMotionRules.FocusTiltDegrees).IsEqual(0f);
        AssertThat(PulpMotionRules.FocusScale).IsEqual(1f);
        AssertThat(PulpMotionRules.FocusTranslateX - PulpMotionRules.PressTranslate).IsEqual(8f);
        AssertThat(PulpMotionRules.PressScale).IsEqual(0.985f);
        AssertThat(PulpMotionRules.PressReleaseSeconds).IsEqual(0.12f);
        // Panels: fade and rise 24 px over 220 ms in; fade and sink 12 px over 160 ms out; Reduced fades of 150 / 120 ms.
        AssertThat(PulpMotionRules.PanelInSeconds).IsEqual(0.22f);
        AssertThat(PulpMotionRules.PanelRisePixels).IsEqual(24f);
        AssertThat(PulpMotionRules.PanelOutSeconds).IsEqual(0.16f);
        AssertThat(PulpMotionRules.PanelSinkPixels).IsEqual(12f);
        AssertThat(PulpMotionRules.PanelFadeSeconds).IsEqual(0.15f);
        AssertThat(PulpMotionRules.PanelFadeOutSeconds).IsEqual(0.12f);
        AssertThat(PulpMotionRules.PanelRiseAt(0f)).IsEqual(24f);
        AssertThat(PulpMotionRules.PanelRiseAt(1f)).IsEqual(0f);
        AssertThat(PulpMotionRules.PanelBandSeconds).IsEqual(0.6f);
        // The tracker, the healing rise, the screen dissolve and the pause dim.
        AssertThat(PulpMotionRules.TrackerHoldSeconds).IsEqual(0.5f);
        AssertThat(PulpMotionRules.TrackerDrainSeconds).IsEqual(0.6f);
        AssertThat(PulpMotionRules.TrackerFadeSeconds).IsEqual(0.2f);
        AssertThat(PulpMotionRules.HealRiseSeconds).IsEqual(0.2f);
        AssertThat(PulpMotionRules.DissolveSeconds).IsEqual(0.2f);
        AssertThat(PulpMotionRules.PauseDimSeconds).IsEqual(0.16f);
        // The ready bloom: one 600 ms bloom peaking at 1.35 and 1.5 x glow at 300 ms, settling at the steady look; no loop.
        (float peakModulate, float peakGlow) = PulpMotionRules.BloomAt(PulpMotionRules.BloomPeakSeconds);
        AssertThat(Close(peakModulate, 1.35f) && Close(peakGlow, 1.5f)).IsTrue();
        AssertThat(PulpMotionRules.BloomAt(0f) == (1f, 0f)).IsTrue();
        AssertThat(PulpMotionRules.BloomAt(PulpMotionRules.BloomSeconds) == (1f, 1f)).IsTrue();
        AssertThat(PulpMotionRules.BloomAt(PulpMotionRules.BloomSeconds + 2.3f) == (1f, 1f)).IsTrue();
        // Nothing flashes faster than once a second: the slowest repeating cue (low HP) is a 2 s, 0.5 Hz breath,
        // and the legacy hit flash stays rate-limited until the closeout deletes it.
        AssertThat(PulpMotionRules.LowPulseSeconds >= 1f).IsTrue();
        AssertThat(PulpMotionRules.LowPulseAt(0f)).IsEqual(0f);
        AssertThat(Close(PulpMotionRules.LowPulseAt(PulpMotionRules.LowPulseSeconds / 2f), 1f)).IsTrue();
        AssertThat(Close(PulpMotionRules.LowPulseAt(PulpMotionRules.LowPulseSeconds), 0f)).IsTrue();
        AssertThat(PulpMotionRules.HitFlashMinIntervalSeconds >= 1f).IsTrue();
    }

    [TestCase]
    public void TheHitFlashLimiterAllowsOneFlashPerSecondPerBar() {
        var bar = new HitFlashLimiter();
        AssertThat(bar.TryFlash(10.0)).IsTrue();
        AssertThat(bar.TryFlash(10.2)).IsFalse();     // a multi-hit string inside the window
        AssertThat(bar.TryFlash(10.99)).IsFalse();
        AssertThat(bar.TryFlash(11.0)).IsTrue();
        // Limiters are per bar: another bar flashes independently.
        var other = new HitFlashLimiter();
        AssertThat(other.TryFlash(10.2)).IsTrue();
        bar.Reset();
        AssertThat(bar.TryFlash(11.1)).IsTrue();
    }
}
