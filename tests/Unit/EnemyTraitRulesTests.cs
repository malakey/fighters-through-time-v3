using FTT.Combat;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Gameplay review 2026-11-03 (rulings 2b, 2b+; plan section 1.12): the pure
/// rules of the three soft-counter traits. Engine-free on purpose: no
/// <c>[RequireGodotRuntime]</c>. The mob's half is <c>EnemyControllerTraitTests</c>.
/// </summary>
[TestSuite]
public class EnemyTraitRulesTests {

    [TestCase]
    public void ArmourHalvesBasicAndSpecialHitsFromAnIntactBody() {
        AssertThat(EnemyTraitRules.ArmouredDamageScale).IsEqual(0.5f);
        AssertThat(EnemyTraitRules.ArmouredIntakeScale(true, AttackClass.Basic, HitOrigin.Basic, HitDelivery.DirectHit)).IsEqual(0.5f);
        AssertThat(EnemyTraitRules.ArmouredIntakeScale(true, AttackClass.Special, HitOrigin.Special, HitDelivery.DirectHit)).IsEqual(0.5f);
        AssertThat(EnemyTraitRules.ArmouredIntakeScale(true, AttackClass.Basic, HitOrigin.Special, HitDelivery.Construct)).IsEqual(0.5f);
        AssertThat(EnemyTraitRules.ArmouredIntakeScale(true, AttackClass.Special, HitOrigin.Special, HitDelivery.Tick)).IsEqual(0.5f);
        // Thrown: nothing is cut.
        AssertThat(EnemyTraitRules.ArmouredIntakeScale(false, AttackClass.Basic, HitOrigin.Basic, HitDelivery.DirectHit)).IsEqual(1f);
    }

    [TestCase]
    public void ArmourNeverCutsAnUltimateAThrowTheEnvironmentOrAHazard() {
        AssertThat(EnemyTraitRules.ArmourReduces(AttackClass.Ultimate, HitOrigin.Ultimate, HitDelivery.DirectHit)).IsFalse();
        // An Ultimate's own Special-class pieces count by their origin.
        AssertThat(EnemyTraitRules.ArmourReduces(AttackClass.Special, HitOrigin.Ultimate, HitDelivery.Tick)).IsFalse();
        AssertThat(EnemyTraitRules.ArmourReduces(AttackClass.Basic, HitOrigin.Throw, HitDelivery.DirectHit)).IsFalse();
        AssertThat(EnemyTraitRules.ArmourReduces(AttackClass.Basic, HitOrigin.Environment, HitDelivery.DirectHit)).IsFalse();
        AssertThat(EnemyTraitRules.ArmourReduces(AttackClass.Hazard, HitOrigin.Environment, HitDelivery.Hazard)).IsFalse();
        AssertThat(EnemyTraitRules.ArmourReduces(AttackClass.Basic, HitOrigin.Basic, HitDelivery.Hazard)).IsFalse();
    }

    [TestCase]
    public void TheCatcherCatchesOnlyStringHitsOneAndTwo() {
        AssertThat(StoryEnemyCombatRules.CatcherCatchesHit(AttackClass.Basic, "combo_1")).IsTrue();
        AssertThat(StoryEnemyCombatRules.CatcherCatchesHit(AttackClass.Basic, "combo_2")).IsTrue();
        AssertThat(StoryEnemyCombatRules.CatcherCatchesHit(AttackClass.Basic, "combo_3")).IsFalse();
        AssertThat(StoryEnemyCombatRules.CatcherCatchesHit(AttackClass.Basic, BasicComboRules.UpAttackHitboxID)).IsFalse();
        AssertThat(StoryEnemyCombatRules.CatcherCatchesHit(AttackClass.Basic, BasicComboRules.DownAirHitboxID)).IsFalse();
        AssertThat(StoryEnemyCombatRules.CatcherCatchesHit(AttackClass.Basic, BasicComboRules.AirAttackHitboxID)).IsFalse();
        AssertThat(StoryEnemyCombatRules.CatcherCatchesHit(AttackClass.Basic, "primary")).IsFalse();
        AssertThat(StoryEnemyCombatRules.CatcherCatchesHit(AttackClass.Basic, null)).IsFalse();
        // A Special or an Ultimate is the answer, whatever its box is called.
        AssertThat(StoryEnemyCombatRules.CatcherCatchesHit(AttackClass.Special, "combo_1")).IsFalse();
        AssertThat(StoryEnemyCombatRules.CatcherCatchesHit(AttackClass.Ultimate, "combo_2")).IsFalse();
    }

    [TestCase]
    public void OnlyTheSecondStringHitEarnsTheCounterOffCooldownWithNoAttackInFlight() {
        AssertThat(StoryEnemyCombatRules.CatcherCounterTelegraphFrames).IsEqual(12);
        AssertThat(StoryEnemyCombatRules.CatcherCounterCooldownFrames).IsEqual(150);
        AssertThat(StoryEnemyCombatRules.CatcherCounterFires(1, 0, false)).IsTrue();
        AssertThat(StoryEnemyCombatRules.CatcherCounterFires(0, 0, false)).IsFalse();
        AssertThat(StoryEnemyCombatRules.CatcherCounterFires(2, 0, false)).IsFalse();
        AssertThat(StoryEnemyCombatRules.CatcherCounterFires(-1, 0, false)).IsFalse();
        AssertThat(StoryEnemyCombatRules.CatcherCounterFires(1, 1, false)).IsFalse();
        AssertThat(StoryEnemyCombatRules.CatcherCounterFires(1, 0, true)).IsFalse();
    }

    [TestCase]
    public void AJumpInIsADescentOffTheFloorInAJumpInState() {
        AssertThat(EnemyTraitRules.HeroIsJumpingIn(true, false, 1f)).IsTrue();
        AssertThat(EnemyTraitRules.HeroIsJumpingIn(false, false, 1f)).IsFalse();
        AssertThat(EnemyTraitRules.HeroIsJumpingIn(true, true, 1f)).IsFalse();
        AssertThat(EnemyTraitRules.HeroIsJumpingIn(true, false, 0f)).IsFalse();
        AssertThat(EnemyTraitRules.HeroIsJumpingIn(true, false, -300f)).IsFalse();
    }

    [TestCase]
    public void TheAntiAirTriggerReadsItsBandCooldownAndAnAttackInFlight() {
        float x = EnemyTraitRules.AntiAirReachHalfWidthPixels;
        float low = EnemyTraitRules.AntiAirMinHeightPixels;
        float high = EnemyTraitRules.AntiAirMaxHeightPixels;
        AssertThat(EnemyTraitRules.AntiAirFires(true, x, low, 0, false)).IsTrue();
        AssertThat(EnemyTraitRules.AntiAirFires(true, 0f, high, 0, false)).IsTrue();
        AssertThat(EnemyTraitRules.AntiAirFires(true, x + 1f, low, 0, false)).IsFalse();
        AssertThat(EnemyTraitRules.AntiAirFires(true, 0f, low - 1f, 0, false)).IsFalse();
        AssertThat(EnemyTraitRules.AntiAirFires(true, 0f, high + 1f, 0, false)).IsFalse();
        AssertThat(EnemyTraitRules.AntiAirFires(false, 0f, low, 0, false)).IsFalse();
        AssertThat(EnemyTraitRules.AntiAirFires(true, 0f, low, 1, false)).IsFalse();
        AssertThat(EnemyTraitRules.AntiAirFires(true, 0f, low, 0, true)).IsFalse();
    }

    [TestCase]
    public void AMissedRisingStrikeCostsTheRuledFortyFrames() {
        AssertThat(EnemyTraitRules.AntiAirMissRecoveryFrames).IsEqual(40);
        AssertThat(EnemyTraitRules.AntiAirHitRecoveryFrames < EnemyTraitRules.AntiAirMissRecoveryFrames).IsTrue();
    }

    [TestCase]
    public void TheRisingStrikeMeetsAHeroAtTheFallCapAndNeverAStandingOne() {
        // The hero's hurtbox is 64 px tall on its feet. Heights are over the mob's feet.
        float boxLow = -EnemyTraitRules.AntiAirBoxOffsetYPixels - EnemyTraitRules.AntiAirBoxHeightPixels / 2f;
        float boxHigh = boxLow + EnemyTraitRules.AntiAirBoxHeightPixels;
        float heroHeight = StoryCombatRules.HeroHurtboxHeightPixels;
        AssertThat(boxLow > heroHeight)
            .OverrideFailureMessage("A hero standing on the mob's floor must be under the strike: grounded pressure is the answer.")
            .IsTrue();
        // The lowest trigger, falling at the 12 u/s cap, still has its feet over the
        // mob's floor when the strike goes active, and inside the box.
        float capPerFrame = UniversalMovementRules.TerminalFallSpeed;
        float feetAtActive = EnemyTraitRules.AntiAirMinHeightPixels - capPerFrame * EnemyTraitRules.AntiAirTelegraphFrames;
        AssertThat(feetAtActive > 0f).OverrideFailureMessage($"Feet at {feetAtActive}: the hero has landed.").IsTrue();
        AssertThat(feetAtActive + heroHeight > boxLow && feetAtActive < boxHigh).IsTrue();
        // The hit reaches past a right-facing box's far edge by the hero's half width.
        float reach = EnemyTraitRules.AntiAirBoxOffsetXPixels + EnemyTraitRules.AntiAirBoxWidthPixels / 2f
            + StoryCombatRules.HeroHurtboxWidthPixels / 2f;
        AssertThat(reach >= EnemyTraitRules.AntiAirReachHalfWidthPixels - 5f).IsTrue();
    }
}
