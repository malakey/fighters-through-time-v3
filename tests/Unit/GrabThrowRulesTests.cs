using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Grab pass 2026-10-31 (step A): the pure hold-phase rule both modes call.
/// Engine-free: no Godot API is touched, so no runtime is required.
/// </summary>
[TestSuite]
public class GrabThrowRulesTests {

    [TestCase]
    public void TheNumbersAreTheDecidedOnes() {
        AssertThat(BasicComboRules.ThrowDecisionFrames).IsEqual(20);
        AssertThat(BasicComboRules.ThrowEarlyMinFrames).IsEqual(6);
        AssertThat(BasicComboRules.GrabCatchHitstopFrames).IsEqual(6);
    }

    [TestCase]
    public void NothingHeldThrowsForwardOnlyAtTheAutoFrame() {
        for (int elapsed = 0; elapsed < BasicComboRules.ThrowDecisionFrames; elapsed++) {
            AssertThat(GrabThrowRules.ThrowTriggerThisTick(elapsed, false, GrabThrowIntent.Neutral, out _))
                .OverrideFailureMessage($"hold tick {elapsed}: nothing pressed must not throw")
                .IsFalse();
        }
        AssertThat(GrabThrowRules.ThrowTriggerThisTick(
            BasicComboRules.ThrowDecisionFrames, false, GrabThrowIntent.Neutral, out GrabThrowDirection auto)).IsTrue();
        AssertThat(auto).IsEqual(GrabThrowDirection.Forward);
    }

    [TestCase]
    public void AnAttackPressOrAHeldDirectionThrowsOnlyFromTheMinimum() {
        int min = BasicComboRules.ThrowEarlyMinFrames;
        foreach (GrabThrowIntent intent in new[] { GrabThrowIntent.Forward, GrabThrowIntent.Back, GrabThrowIntent.Up }) {
            AssertThat(GrabThrowRules.ThrowTriggerThisTick(min - 1, false, intent, out _)).IsFalse();
            AssertThat(GrabThrowRules.ThrowTriggerThisTick(min, false, intent, out _)).IsTrue();
        }
        AssertThat(GrabThrowRules.ThrowTriggerThisTick(min - 1, true, GrabThrowIntent.Neutral, out _)).IsFalse();
        AssertThat(GrabThrowRules.ThrowTriggerThisTick(min, true, GrabThrowIntent.Neutral, out GrabThrowDirection pressed)).IsTrue();
        AssertThat(pressed)
            .OverrideFailureMessage("An Attack press with a neutral stick throws forward.")
            .IsEqual(GrabThrowDirection.Forward);
    }

    [TestCase]
    public void TheDirectionFollowsTheStickAndUpBeatsBackBeatsForward() {
        AssertThat(GrabThrowRules.ClassifyStick(0, -127, true)).IsEqual(GrabThrowIntent.Up);
        AssertThat(GrabThrowRules.ClassifyStick(127, -127, true)).IsEqual(GrabThrowIntent.Up);
        AssertThat(GrabThrowRules.ClassifyStick(127, 0, true)).IsEqual(GrabThrowIntent.Forward);
        AssertThat(GrabThrowRules.ClassifyStick(-127, 0, true)).IsEqual(GrabThrowIntent.Back);
        AssertThat(GrabThrowRules.ClassifyStick(127, 0, false)).IsEqual(GrabThrowIntent.Back);
        AssertThat(GrabThrowRules.ClassifyStick(-127, 0, false)).IsEqual(GrabThrowIntent.Forward);
        AssertThat(GrabThrowRules.ClassifyStick(30, 30, true)).IsEqual(GrabThrowIntent.Neutral);
        AssertThat(GrabThrowRules.DirectionFor(GrabThrowIntent.Back)).IsEqual(GrabThrowDirection.Back);
        AssertThat(GrabThrowRules.DirectionFor(GrabThrowIntent.Up)).IsEqual(GrabThrowDirection.Up);
        AssertThat(GrabThrowRules.DirectionFor(GrabThrowIntent.Neutral)).IsEqual(GrabThrowDirection.Forward);
    }

    [TestCase]
    public void AutoThrowAtTheDecisionFrameTakesTheHeldDirection() {
        AssertThat(GrabThrowRules.ThrowTriggerThisTick(
            BasicComboRules.ThrowDecisionFrames, false, GrabThrowIntent.Back, out GrabThrowDirection back)).IsTrue();
        AssertThat(back).IsEqual(GrabThrowDirection.Back);
    }
}
