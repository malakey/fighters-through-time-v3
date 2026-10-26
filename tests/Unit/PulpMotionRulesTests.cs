using FTT.UI.Motion;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-THEME. The MOT-B numbers as plain C# (UI_SPEC section 5). Pure: no Godot
/// type is touched, so this suite needs no runtime (CLAUDE.md failure signature 7).
/// </summary>
[TestSuite]
public class PulpMotionRulesTests {

    [TestCase]
    public void ANumberThunkIsThreeEqualStepsThatLandExactlyOnTheTarget() {
        int[] up = PulpMotionRules.ThunkValues(100, 70);
        AssertThat(up.Length).IsEqual(3);
        AssertThat(up[0]).IsEqual(90);
        AssertThat(up[1]).IsEqual(80);
        AssertThat(up[2]).IsEqual(70);

        // A change that does not divide by three still ends on the target, never one short.
        int[] odd = PulpMotionRules.ThunkValues(0, 10);
        AssertThat(odd[2]).IsEqual(10);
        AssertThat(odd[0] <= odd[1] && odd[1] <= odd[2]).IsTrue();
    }

    [TestCase]
    public void SteppedProgressKeepsItsEndpointsAndQuantisesTheMiddle() {
        AssertThat(PulpMotionRules.Stepped(0f, 8)).IsEqual(0f);
        AssertThat(PulpMotionRules.Stepped(1f, 8)).IsEqual(1f);
        AssertThat(PulpMotionRules.Stepped(0.01f, 8)).IsEqual(0.125f);
        AssertThat(PulpMotionRules.Stepped(0.5f, 8)).IsEqual(0.5f);
        AssertThat(PulpMotionRules.Stepped(0.51f, 8)).IsEqual(0.625f);
        // The wipe slab starts one full width left, covers at 0, and leaves one width right.
        AssertThat(PulpMotionRules.WipeOffsetCover(0f)).IsEqual(-1f);
        AssertThat(PulpMotionRules.WipeOffsetCover(1f)).IsEqual(0f);
        AssertThat(PulpMotionRules.WipeOffsetReveal(0f)).IsEqual(0f);
        AssertThat(PulpMotionRules.WipeOffsetReveal(1f)).IsEqual(1f);
    }

    [TestCase]
    public void TheTimingsAreTheSpecsAndNothingFlashesFasterThanOnceASecond() {
        AssertThat(PulpMotionRules.FocusInSeconds).IsEqual(0.14f);
        AssertThat(PulpMotionRules.FocusOutSeconds).IsEqual(0.10f);
        AssertThat(PulpMotionRules.WipeSteps).IsEqual(8);
        AssertThat(PulpMotionRules.WipeSeconds).IsEqual(0.26f);
        AssertThat(PulpMotionRules.ThunkSteps).IsEqual(3);
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
