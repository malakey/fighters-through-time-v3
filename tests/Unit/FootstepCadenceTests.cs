using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B5. The footstep rhythm <c>PlayerController</c> drives from its
/// grounded movement.
///
/// <para>The cadence is distance-based rather than a frame timer, which is the whole
/// reason it is worth testing: the property that matters is that a slower character
/// takes proportionally fewer steps over the same wall-clock time, and that is
/// exactly what a frame-interval implementation would silently get wrong.</para>
///
/// <para>Speeds are chosen to be exact multiples of the 60 Hz step (60 px/s is one
/// pixel per frame) so the assertions test the cadence rather than float
/// accumulation error.</para>
/// </summary>
[TestSuite]
public class FootstepCadenceTests {
    private const float Frame = 1f / 60f;

    [TestCase]
    public void TheFirstGroundedMovingFrameStepsImmediately() {
        var cadence = new FootstepCadence();
        AssertThat(cadence.IsPrimed).IsTrue();
        AssertThat(cadence.Advance(true, 420f, Frame)).IsTrue();
        AssertThat(cadence.StepCount).IsEqual(1);
        AssertThat(cadence.IsPrimed).IsFalse();
    }

    [TestCase]
    public void StepsArriveOneStrideApartRatherThanEveryFrame() {
        var cadence = new FootstepCadence { StrideDistance = 100f };
        cadence.Advance(true, 120f, Frame); // consumes the primed step

        int steps = 0;
        // One second at 120 px/s covers 120 px: one stride, not sixty.
        for (int frame = 0; frame < 60; frame++) {
            if (cadence.Advance(true, 120f, Frame)) steps++;
        }
        AssertThat(steps).IsEqual(1);
    }

    [TestCase]
    public void AFasterCharacterStepsProportionallyMoreOften() {
        int slow = CountSteps(60f, 120);
        int fast = CountSteps(240f, 120);
        AssertThat(slow).IsEqual(2);
        // Four times the speed over the same span is four times the strides.
        AssertThat(fast).IsEqual(slow * 4);
    }

    [TestCase]
    public void AirborneAndNearStationaryFramesAreSilent() {
        var cadence = new FootstepCadence();
        AssertThat(cadence.Advance(false, 600f, Frame)).IsFalse();
        AssertThat(cadence.Advance(true, 5f, Frame)).IsFalse();
        AssertThat(cadence.StepCount).IsEqual(0);
    }

    /// <summary>
    /// Landing must plant a foot at once. Without the priming, a short hop between
    /// two close platforms is completely silent, which reads as missing audio rather
    /// than as a cadence choice.
    /// </summary>
    [TestCase]
    public void LandingAfterAJumpPlantsAFootOnTheFirstGroundedFrame() {
        var cadence = new FootstepCadence();
        cadence.Advance(true, 400f, Frame);
        for (int frame = 0; frame < 30; frame++) cadence.Advance(false, 400f, Frame);

        AssertThat(cadence.IsPrimed).IsTrue();
        AssertThat(cadence.Advance(true, 400f, Frame)).IsTrue();
    }

    /// <summary>
    /// A single frame that banks several strides — a long stall, or a movement
    /// ability's landing frame — must sound once and then leave no backlog to
    /// machine-gun out over the following frames.
    /// </summary>
    [TestCase]
    public void AFrameThatBanksSeveralStridesSoundsOnceAndDropsTheBacklog() {
        var cadence = new FootstepCadence { StrideDistance = 100f };
        cadence.Advance(true, 120f, Frame); // primed step

        AssertThat(cadence.Advance(true, 2000f, 0.5f)).IsTrue();
        AssertThat(cadence.StepCount).IsEqual(2);
        AssertThat(cadence.AccumulatedDistance < 100f).IsTrue();
        AssertThat(cadence.Advance(true, 120f, Frame)).IsFalse();
    }

    [TestCase]
    public void DirectionDoesNotChangeTheCadence() {
        AssertThat(CountSteps(-300f, 120)).IsEqual(CountSteps(300f, 120));
        AssertThat(CountSteps(300f, 120)).IsEqual(10);
    }

    [TestCase]
    public void ResetReturnsTheCadenceToAFreshCharacter() {
        var cadence = new FootstepCadence();
        cadence.Advance(true, 400f, Frame);
        cadence.Advance(true, 400f, Frame);

        cadence.Reset();
        AssertThat(cadence.StepCount).IsEqual(0);
        AssertThat(cadence.AccumulatedDistance).IsEqual(0f);
        AssertThat(cadence.Advance(true, 400f, Frame)).IsTrue();
    }

    /// <summary>Counts steps after the primed one, over a fixed number of 60 Hz frames.</summary>
    private static int CountSteps(float speed, int frames) {
        var cadence = new FootstepCadence { StrideDistance = 60f };
        cadence.Advance(true, speed, Frame); // consume the primed step
        int steps = 0;
        for (int frame = 0; frame < frames; frame++) {
            if (cadence.Advance(true, speed, Frame)) steps++;
        }
        return steps;
    }
}
