using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The ability lockout (the special global cooldown, user direction
/// 2026-10-08): the one shared rule both modes read. A swing start, a Special
/// cast or an accepted Ultimate arms 30 frames during which Special 1, Special 2
/// and the Ultimate are refused. Story keeps the counter on
/// <c>PlayerController</c>, the sim in <c>FighterRuntimeComponent</c>; both
/// arm to <see cref="BasicComboRules.AbilityLockoutFrames"/> and tick through
/// <see cref="BasicComboRules.TickAbilityLockout"/>. Pure C#: nothing here
/// touches the Godot runtime.
/// </summary>
[TestSuite]
public class AbilityLockoutRulesTests {

    [TestCase]
    public void TheLockoutIsThirtyFramesAndLocksOnlyWhilePositive() {
        AssertThat(BasicComboRules.AbilityLockoutFrames)
            .OverrideFailureMessage("The lockout is half a second at 60 Hz.")
            .IsEqual(30);

        AssertThat(BasicComboRules.IsAbilityLocked(30)).IsTrue();
        AssertThat(BasicComboRules.IsAbilityLocked(1))
            .OverrideFailureMessage("The last frame of the lockout still refuses.")
            .IsTrue();
        AssertThat(BasicComboRules.IsAbilityLocked(0)).IsFalse();
        AssertThat(BasicComboRules.IsAbilityLocked(-1))
            .OverrideFailureMessage("A negative counter reads as clear, never as locked.")
            .IsFalse();
    }

    [TestCase]
    public void OneTickCountsDownByOneAndFloorsAtZero() {
        AssertThat(BasicComboRules.TickAbilityLockout(30)).IsEqual(29);
        AssertThat(BasicComboRules.TickAbilityLockout(1)).IsEqual(0);
        AssertThat(BasicComboRules.TickAbilityLockout(0)).IsEqual(0);
        AssertThat(BasicComboRules.TickAbilityLockout(-3))
            .OverrideFailureMessage("A corrupt negative counter is repaired to zero, never kept.")
            .IsEqual(0);
    }

    [TestCase]
    public void AnArmedLockoutClearsExactlyOnItsThirtiethTick() {
        // Armed on tick T, ticked once per later tick: locked through T + 29,
        // clear on T + 30.
        int remaining = BasicComboRules.AbilityLockoutFrames;
        for (int tick = 1; tick <= BasicComboRules.AbilityLockoutFrames; tick++) {
            remaining = BasicComboRules.TickAbilityLockout(remaining);
            AssertThat(remaining).IsEqual(BasicComboRules.AbilityLockoutFrames - tick);
            AssertThat(BasicComboRules.IsAbilityLocked(remaining))
                .OverrideFailureMessage($"Tick {tick} after arming has the wrong lock state.")
                .IsEqual(tick < BasicComboRules.AbilityLockoutFrames);
        }
        AssertThat(remaining).IsEqual(0);
        AssertThat(BasicComboRules.TickAbilityLockout(remaining))
            .OverrideFailureMessage("A run-out lockout stays at zero.")
            .IsEqual(0);
    }

    // === D1 / D2 (user direction 2026-10-11) ===

    [TestCase]
    public void TheSpecialGateFollowsTheSpecialSourceOnceTheSwingHasLanded() {
        AssertThat(BasicComboRules.SpecialGateFrames(30, 0, swingLanded: true))
            .OverrideFailureMessage("A landed swing releases the Specials from its own lockout.")
            .IsEqual(0);
        AssertThat(BasicComboRules.SpecialGateFrames(30, 12, swingLanded: true))
            .OverrideFailureMessage("A Special accepted inside the last 30 frames still holds the other Special.")
            .IsEqual(12);
        AssertThat(BasicComboRules.SpecialGateFrames(30, 0, swingLanded: false))
            .OverrideFailureMessage("A whiffed swing still gates on the whole lockout.")
            .IsEqual(30);
        AssertThat(BasicComboRules.SpecialGateFrames(0, 0, swingLanded: false)).IsEqual(0);

        AssertThat(BasicComboRules.IsSpecialLocked(30, 0, swingLanded: true)).IsFalse();
        AssertThat(BasicComboRules.IsSpecialLocked(30, 12, swingLanded: true)).IsTrue();
        AssertThat(BasicComboRules.IsSpecialLocked(30, 0, swingLanded: false)).IsTrue();
        AssertThat(BasicComboRules.IsSpecialLocked(0, 0, swingLanded: false)).IsFalse();
        AssertThat(BasicComboRules.IsAbilityLocked(30))
            .OverrideFailureMessage("The Ultimate keeps the whole lockout: no hit-confirm for it.")
            .IsTrue();
    }

    [TestCase]
    public void MomentumRefundsTwentyPerHitSixtyOnTheFinisherAndCapsEachCycleAtOneTwenty() {
        AssertThat(BasicComboRules.MomentumRefundFramesFor(BasicComboRules.VariantChain, 0)).IsEqual(20);
        AssertThat(BasicComboRules.MomentumRefundFramesFor(BasicComboRules.VariantChain, 1)).IsEqual(20);
        AssertThat(BasicComboRules.MomentumRefundFramesFor(BasicComboRules.VariantChain, 2)).IsEqual(60);
        AssertThat(BasicComboRules.MomentumRefundFramesFor(BasicComboRules.VariantUpAttack, 0)).IsEqual(20);
        AssertThat(BasicComboRules.MomentumRefundFramesFor(BasicComboRules.VariantDownAir, 0)).IsEqual(20);

        AssertThat(BasicComboRules.MomentumRefundFramesForHitbox("combo_1")).IsEqual(20);
        AssertThat(BasicComboRules.MomentumRefundFramesForHitbox("combo_2")).IsEqual(20);
        AssertThat(BasicComboRules.MomentumRefundFramesForHitbox("combo_3")).IsEqual(60);
        AssertThat(BasicComboRules.MomentumRefundFramesForHitbox(BasicComboRules.UpAttackHitboxID)).IsEqual(20);
        AssertThat(BasicComboRules.MomentumRefundFramesForHitbox(BasicComboRules.DownAirHitboxID)).IsEqual(20);
        AssertThat(BasicComboRules.MomentumRefundFramesForHitbox("throw"))
            .OverrideFailureMessage("A throw is not a swing and refunds nothing.")
            .IsEqual(0);
        AssertThat(BasicComboRules.MomentumRefundFramesForHitbox(null)).IsEqual(0);

        AssertThat(BasicComboRules.MomentumRefundCapFramesPerCycle).IsEqual(120);
        AssertThat(BasicComboRules.MomentumGrant(60, 110, 500))
            .OverrideFailureMessage("The cap counts frames: only 10 are left of 120.")
            .IsEqual(10);
        AssertThat(BasicComboRules.MomentumGrant(20, 0, 7))
            .OverrideFailureMessage("A grant never removes more than the cooldown has left.")
            .IsEqual(7);
        AssertThat(BasicComboRules.MomentumGrant(20, 120, 500)).IsEqual(0);
        AssertThat(BasicComboRules.MomentumGrant(60, 0, 0))
            .OverrideFailureMessage("A ready slot takes nothing.")
            .IsEqual(0);
        AssertThat(BasicComboRules.MomentumGrant(60, 130, 500))
            .OverrideFailureMessage("A grant is never negative.")
            .IsEqual(0);
    }
}
