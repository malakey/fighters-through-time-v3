using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The lingering-kit pass's pure rulebook (plan <c>docs/KIT_LINGERING_2026-10-31_PLAN.md</c> 1 and 2,
/// workstream KL-RULES): every provisional number is pinned to the plan, the Momentum payout reads
/// the existing unit scale, Venom is 3 x intensity per second, the heal block states its rule, and
/// the slow formula keeps its shape while a boss never drops below the x0.7 floor. No Godot types.
/// </summary>
[TestSuite]
public class LingeringKitRulesTests {

    [TestCase]
    public void EveryPlanNumberIsPinned() {
        AssertThat(LingeringKitRules.CoilCharges).IsEqual(3);
        AssertThat(LingeringKitRules.TurretCharges).IsEqual(4);
        AssertThat(LingeringKitRules.TurretOverdriveBonusCharges).IsEqual(1);
        AssertThat(LingeringKitRules.NestCharges).IsEqual(3);

        AssertThat(LingeringKitRules.CoilOwnArcDamage).IsEqual(5);
        AssertThat(LingeringKitRules.CoilOwnArcIntervalFrames).IsEqual(120);
        AssertThat(LingeringKitRules.CoilSyncedArcDamage).IsEqual(8);
        AssertThat(LingeringKitRules.CoilSyncedArcHitstunFrames).IsEqual(20);
        AssertThat(LingeringKitRules.CoilSyncedReachUnits).IsEqual(4.0);

        AssertThat(LingeringKitRules.DischargeOwnerRangeUnits).IsEqual(8.0);
        AssertThat(LingeringKitRules.DischargeDamagePerCharge).IsEqual(6);
        AssertThat(LingeringKitRules.DischargeHitstunFrames).IsEqual(20);
        AssertThat(LingeringKitRules.DischargeCoilReachUnits).IsEqual(4.0);
        AssertThat(LingeringKitRules.DischargeMarkedReachUnits).IsEqual(8.0);

        AssertThat(LingeringKitRules.TurretOwnShotDamage).IsEqual(4);
        AssertThat(LingeringKitRules.TurretOwnShotIntervalFrames).IsEqual(180);
        AssertThat(LingeringKitRules.TurretLifetimeSeconds).IsEqual(15);
        AssertThat(LingeringKitRules.TurretOwnShotReachStoryUnits).IsEqual(30.0);
        AssertThat(LingeringKitRules.TurretSyncedBoltDamage).IsEqual(8);
        AssertThat(LingeringKitRules.TurretSyncedBoltHitstunFrames).IsEqual(20);
        AssertThat(LingeringKitRules.TurretSyncedBoltSpeedUnitsPerSecond).IsEqual(24.0);
        AssertThat(LingeringKitRules.TurretSyncedReachUnits).IsEqual(10.0);

        AssertThat(LingeringKitRules.NestOwnNipDamage).IsEqual(3);
        AssertThat(LingeringKitRules.NestOwnNipIntervalFrames).IsEqual(60);
        AssertThat(LingeringKitRules.NestOwnReachPixels).IsEqual(60);
        AssertThat(LingeringKitRules.NestSyncedLungeDamage).IsEqual(6);
        AssertThat(LingeringKitRules.NestSyncedLungeHoldFrames).IsEqual(60);
        AssertThat(LingeringKitRules.NestSyncedLungeVenomSeconds).IsEqual(4.0);
        AssertThat(LingeringKitRules.NestSyncedReachUnits).IsEqual(2.5);

        AssertThat(LingeringKitRules.RiftCollapseDamage).IsEqual(8);
        AssertThat(LingeringKitRules.VortexPullUnitsPerSecondStory).IsEqual(7.0);
        AssertThat(LingeringKitRules.VortexPullMilliPerSecondSim).IsEqual(7000);
        AssertThat(LingeringKitRules.GoldenRatioPullPercent).IsEqual(50);
        AssertThat(LingeringKitRules.GoldenRatioPullCapUnits).IsEqual(1.5);
    }

    [TestCase]
    public void ThePerModeNumbersAgreeUntilARulingSplitsThem() {
        // The Vortex pull is a named pair; the plan gives both modes 7.0 u/s.
        AssertThat(LingeringKitRules.VortexPullMilliPerSecondSim / 1000.0)
            .IsEqual(LingeringKitRules.VortexPullUnitsPerSecondStory);
        AssertThat(LingeringKitRules.HeroSlowLingerFrames)
            .IsEqual((int)System.Math.Round(LingeringKitRules.HeroSlowLingerSeconds * 60.0));
    }

    [TestCase]
    public void TheRiftAndTheVortexAreTwoAndAHalfUnitsWide() {
        AssertThat(KitMotionRules.RelativityRiftRadiusUnits).IsEqual(2.5f);
        AssertThat(KitReachRules.SandstormVortexRadiusUnits).IsEqual(2.5);
        // The throw distances did not move.
        AssertThat(KitMotionRules.RelativityRiftThrowUnits).IsEqual(5.0f);
        AssertThat(KitReachRules.SandstormVortexMaxThrowUnits).IsEqual(5.0);
        AssertThat(KitMotionRules.RelativityRiftLingerFrames).IsEqual(LingeringKitRules.HeroSlowLingerFrames);
    }

    [TestCase]
    public void MomentumPaysOnTheExistingUnitScaleOncePerEvent() {
        AssertThat(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.Detonation)).IsEqual(1440);
        AssertThat(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.SyncedStrike)).IsEqual(360);
        AssertThat(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.HoldCast)).IsEqual(360);
        // 0.2 pip and 0.05 pip of 7200.
        AssertThat(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.Detonation) * 5)
            .IsEqual(BasicComboRules.MomentumUnitsPerPip);
        AssertThat(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.SyncedStrike) * 20)
            .IsEqual(BasicComboRules.MomentumUnitsPerPip);
    }

    [TestCase]
    public void VenomDealsThreeHPPerSecondPerPointOfIntensity() {
        AssertThat(LingeringKitRules.VenomDamagePerSecondPerIntensity).IsEqual(3);
        AssertThat(LingeringKitRules.VenomDamagePerTick(1.0)).IsEqual(3);
        AssertThat(LingeringKitRules.VenomDamagePerTick(2.0)).IsEqual(6);
        AssertThat(LingeringKitRules.VenomDamagePerTick(0.5)).IsEqual(2);
        // A non-positive intensity reads as 1; a tick never deals less than 1.
        AssertThat(LingeringKitRules.VenomDamagePerTick(0.0)).IsEqual(3);
        AssertThat(LingeringKitRules.VenomDamagePerTick(0.1)).IsEqual(1);
        AssertThat(LingeringKitRules.VenomTickFrames).IsEqual(60);
    }

    [TestCase]
    public void VenomBlocksEveryHealButDefys() {
        AssertThat(LingeringKitRules.VenomBlocksHeal(venomActive: true, isDefyHeal: false)).IsTrue();
        AssertThat(LingeringKitRules.VenomBlocksHeal(venomActive: true, isDefyHeal: true)).IsFalse();
        AssertThat(LingeringKitRules.VenomBlocksHeal(venomActive: false, isDefyHeal: false)).IsFalse();
    }

    [TestCase]
    public void TheHeroSlowIsFortyPercentSpeedAndABossNeverDropsBelowSeventy() {
        AssertThat(LingeringKitRules.HeroSlowIntensity).IsEqual(1.2);
        AssertThat(LingeringKitRules.HeroSlowLingerSeconds).IsEqual(1.75);
        // The unchanged formula max(0.1, 1 - 0.5 I) at the hero intensity is x0.4.
        AssertThat(LingeringKitRules.TimeDilationMoveMultiplier(LingeringKitRules.HeroSlowIntensity))
            .IsEqualApprox(0.4, 0.0001);
        AssertThat(LingeringKitRules.TimeDilationMoveMultiplier(1.0)).IsEqual(0.5);
        AssertThat(LingeringKitRules.TimeDilationMoveMultiplier(5.0)).IsEqual(0.1);
        // A boss: floor x0.7, and a milder slow than the floor is untouched.
        AssertThat(LingeringKitRules.BossTimeDilationMoveMultiplier(LingeringKitRules.HeroSlowIntensity))
            .IsEqual(0.7);
        AssertThat(LingeringKitRules.BossTimeDilationMoveMultiplier(1.0)).IsEqual(0.7);
        AssertThat(LingeringKitRules.BossTimeDilationMoveMultiplier(0.4)).IsEqualApprox(0.8, 0.0001);
    }
}
