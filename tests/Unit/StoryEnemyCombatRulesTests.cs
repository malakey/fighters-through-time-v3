using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 2026-10-04 playtest-feel pass, workstream ENEMY: the pure Story rulebook
/// (<see cref="StoryEnemyCombatRules"/>) behind S1–S5, F5's victim half, F9 and
/// P4. Engine-free on purpose — no <c>[RequireGodotRuntime]</c>, so nothing
/// here may touch a Godot API. The controller behaviour these numbers drive is
/// pinned in <c>StoryEnemyCombatTests</c>.
/// </summary>
[TestSuite]
public class StoryEnemyCombatRulesTests {

    [TestCase]
    public void ConfirmProrationIsFreeForThreeHitsThenDecaysToTheFloor() {
        for (int hit = 1; hit <= StoryEnemyCombatRules.ConfirmChainFreeHits; hit++) {
            AssertThat(StoryEnemyCombatRules.ConfirmProrationScale(hit, specialOnHitstun: false))
                .IsEqualApprox(1f, 0.0001f);
        }
        AssertThat(StoryEnemyCombatRules.ConfirmProrationScale(4, false)).IsEqualApprox(0.85f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.ConfirmProrationScale(5, false)).IsEqualApprox(0.7225f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.ConfirmProrationScale(7, false)).IsEqualApprox(0.52200625f, 0.0001f);
        // 0.85^5 = 0.4437 is under the floor.
        AssertThat(StoryEnemyCombatRules.ConfirmProrationScale(8, false))
            .IsEqualApprox(StoryEnemyCombatRules.ConfirmChainDamageFloor, 0.0001f);
        AssertThat(StoryEnemyCombatRules.ConfirmProrationScale(40, false))
            .IsEqualApprox(StoryEnemyCombatRules.ConfirmChainDamageFloor, 0.0001f);

        // A Special landing on hitstun is cut on top of the chain decay.
        AssertThat(StoryEnemyCombatRules.ConfirmProrationScale(2, specialOnHitstun: true))
            .IsEqualApprox(0.6f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.ConfirmProrationScale(5, specialOnHitstun: true))
            .IsEqualApprox(0.7225f * 0.6f, 0.0001f);
    }

    [TestCase]
    public void UltimatesTicksAndHazardsAreExemptFromConfirmProration() {
        AssertThat(StoryEnemyCombatRules.IsProrationExempt(AttackClass.Ultimate, HitOrigin.Ultimate, HitDelivery.DirectHit)).IsTrue();
        AssertThat(StoryEnemyCombatRules.IsProrationExempt(AttackClass.Special, HitOrigin.Ultimate, HitDelivery.DirectHit)).IsTrue();
        AssertThat(StoryEnemyCombatRules.IsProrationExempt(AttackClass.Special, HitOrigin.Special, HitDelivery.Tick)).IsTrue();
        AssertThat(StoryEnemyCombatRules.IsProrationExempt(AttackClass.Hazard, HitOrigin.Environment, HitDelivery.Hazard)).IsTrue();

        AssertThat(StoryEnemyCombatRules.IsProrationExempt(AttackClass.Basic, HitOrigin.Basic, HitDelivery.DirectHit)).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsProrationExempt(AttackClass.Special, HitOrigin.Special, HitDelivery.DirectHit)).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsProrationExempt(AttackClass.Basic, HitOrigin.Special, HitDelivery.Construct)).IsFalse();
    }

    [TestCase]
    public void IntakeScalingRoundsHalfAwayFromZeroAndNeverZeroesADamagingHit() {
        AssertThat(StoryEnemyCombatRules.ScaleIntakeDamage(3, StoryEnemyCombatRules.StandardBasicDamageScale)).IsEqual(4);
        AssertThat(StoryEnemyCombatRules.ScaleIntakeDamage(10, StoryEnemyCombatRules.StandardBasicDamageScale)).IsEqual(13);
        AssertThat(StoryEnemyCombatRules.ScaleIntakeDamage(4, 0.6f)).IsEqual(2);
        AssertThat(StoryEnemyCombatRules.ScaleIntakeDamage(1, 0.3f)).IsEqual(1);
        AssertThat(StoryEnemyCombatRules.ScaleIntakeDamage(7, 1f)).IsEqual(7);
        AssertThat(StoryEnemyCombatRules.ScaleIntakeDamage(0, 1.25f)).IsEqual(0);
    }

    [TestCase]
    public void BasicsPacingCoversOnlyTheUniversalBasicSet() {
        AssertThat(StoryEnemyCombatRules.StandardBasicDamageScale).IsEqualApprox(1.25f, 0.0001f);
        foreach (string id in new[] { "combo_1", "combo_2", "combo_3",
                     BasicComboRules.UpAttackHitboxID, BasicComboRules.DownAirHitboxID }) {
            AssertThat(StoryEnemyCombatRules.IsBasicsPacingHit(AttackClass.Basic, id)).IsTrue();
        }
        AssertThat(StoryEnemyCombatRules.IsBasicsPacingHit(AttackClass.Basic, "turret_shot")).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsBasicsPacingHit(AttackClass.Basic, BasicComboRules.ThrowHitboxID)).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsBasicsPacingHit(AttackClass.Special, "combo_1")).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsBasicsPacingHit(AttackClass.Basic, null)).IsFalse();
    }

    [TestCase]
    public void TheHitIndexedStunFloorSpansEveryBufferedStringGap() {
        // Template connect-to-connect gaps (hitstop freezes both parties, so it
        // is not counted): hit 1 -> 2 is 6 + 15 + 7 = 28, hit 2 -> 3 is
        // 7 + 16 + 15 = 38; Lincoln's 17-frame finisher stretches the second to 40.
        int gapOneToTwo = BasicComboRules.GroundActiveFrames[0] + BasicComboRules.GroundRecoveryFrames[0]
            + BasicComboRules.GroundStartupFrames[1];
        int lincolnGapTwoToThree = BasicComboRules.GroundActiveFrames[1] + BasicComboRules.GroundRecoveryFrames[1]
            + BasicComboRules.StringProfileFor("lincoln").GroundStartupFrames[2];

        AssertThat(StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_1")).IsEqual(34);
        AssertThat(StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_2")).IsEqual(44);
        AssertThat(StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_1") > gapOneToTwo).IsTrue();
        AssertThat(StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_2") > lincolnGapTwoToThree).IsTrue();

        // The exchange enders keep the cross-mode floor; non-basic sources get none.
        AssertThat(StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_3"))
            .IsEqual(BasicComboRules.EnemyBasicStunFloorFrames);
        AssertThat(StoryEnemyCombatRules.BasicStringStunFloorFrames(BasicComboRules.UpAttackHitboxID))
            .IsEqual(BasicComboRules.EnemyBasicStunFloorFrames);
        AssertThat(StoryEnemyCombatRules.BasicStringStunFloorFrames(BasicComboRules.DownAirHitboxID))
            .IsEqual(BasicComboRules.EnemyBasicStunFloorFrames);
        AssertThat(StoryEnemyCombatRules.BasicStringStunFloorFrames("turret_shot")).IsEqual(0);
        AssertThat(StoryEnemyCombatRules.BasicStringStunFloorFrames(null)).IsEqual(0);
        AssertThat(StoryEnemyCombatRules.StringStepOf("combo_2")).IsEqual(1);
        AssertThat(StoryEnemyCombatRules.StringStepOf("combo_x")).IsEqual(-1);
    }

    [TestCase]
    public void AFullStringFitsTheStandardPoiseBudgetButAStringPlusSpecialDoesNot() {
        float stringStun = (StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_1")
            + StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_2")
            + StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_3")) / 60f;
        AssertThat(stringStun <= StoryEnemyCombatRules.StandardStaggerBudgetSeconds)
            .OverrideFailureMessage("One full basic string must complete on a standard mob.")
            .IsTrue();

        // The roster's common Special hitstun is 12 frames.
        float specialCost = StoryEnemyCombatRules.StandardBudgetCost(12 / 60f, fromSpecial: true);
        AssertThat(specialCost).IsEqualApprox(0.3f, 0.0001f);
        AssertThat(stringStun + specialCost > StoryEnemyCombatRules.StandardStaggerBudgetSeconds)
            .OverrideFailureMessage("A string plus a Special confirm must trip Armored Recovery.")
            .IsTrue();
        AssertThat(StoryEnemyCombatRules.StandardBudgetCost(0.5f, fromSpecial: false)).IsEqualApprox(0.5f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.StandardArmoredRecoverySeconds
                < EnemyStaggerRules.EliteArmoredRecoverySeconds).IsTrue();
    }

    [TestCase]
    public void TheGetupClearsAStandardsPoiseAndAResistantElitesBudgetKeepsItsPreS5Charge() {
        float stringStun = (StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_1")
            + StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_2")
            + StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_3")) / 60f;
        AssertThat(StoryEnemyCombatRules.StandardStaggerDecayPerSecond * EnemyStaggerRules.GetupArmorSeconds
                >= stringStun)
            .OverrideFailureMessage("The getup armor after one string must drain a standard's whole string credit.")
            .IsTrue();
        AssertThat(StoryEnemyCombatRules.StandardStaggerDecayPerSecond > EnemyStaggerRules.StaggerDecayPerSecond)
            .IsTrue();
        AssertThat(EnemyStaggerRules.StaggerDecayPerSecond).IsEqualApprox(1f, 0.0001f);

        // Elites keep V7.4's 1 s/s drain, so their budget is charged the pre-S5
        // shared floor: the S5 floor lengthens the stun, never the elite's poise cost.
        foreach (string id in new[] { "combo_1", "combo_2", "combo_3",
                     BasicComboRules.UpAttackHitboxID, BasicComboRules.DownAirHitboxID }) {
            AssertThat(StoryEnemyCombatRules.EliteBudgetStunFloorFrames(id))
                .IsEqual(BasicComboRules.EnemyBasicStunFloorFrames);
        }
        AssertThat(StoryEnemyCombatRules.EliteBudgetStunFloorFrames("turret_shot")).IsEqual(0);
        AssertThat(StoryEnemyCombatRules.EliteBudgetStunFloorFrames(null)).IsEqual(0);

        // A resistant elite (StunResistance 0.5) taking two back-to-back strings,
        // with the V7.4 getup armor's drain between them.
        const float resistance = 0.5f;
        float EliteStringCharge(System.Func<int, int> floorFrames) {
            float charge = 0f;
            for (int step = 0; step < BasicComboRules.ComboHits; step++) {
                float resisted = BasicComboRules.HitstunFrames[step] / 60f * (1f - resistance);
                charge += System.Math.Max(resisted, floorFrames(step) / 60f);
            }
            return charge;
        }
        float getupDrain = EnemyStaggerRules.StaggerDecayPerSecond * EnemyStaggerRules.GetupArmorSeconds;
        float preS5 = EliteStringCharge(step => StoryEnemyCombatRules.EliteBudgetStunFloorFrames($"combo_{step + 1}"));
        AssertThat(preS5 - getupDrain + preS5 <= EnemyStaggerRules.EliteStaggerBudgetSeconds)
            .OverrideFailureMessage("Two back-to-back strings must complete on a resistant elite.")
            .IsTrue();
        // Charging the S5 floors instead would trip that elite mid-second-string.
        float s5 = EliteStringCharge(step => StoryEnemyCombatRules.BasicStringStunFloorFrames($"combo_{step + 1}"));
        AssertThat(s5 - getupDrain + s5 > EnemyStaggerRules.EliteStaggerBudgetSeconds).IsTrue();
    }

    [TestCase]
    public void TheVictimHitstopIsTheAttackersLaunchAwareRule() {
        foreach (int damage in new[] { 0, 1, 5, 8, 12, 25, 60 }) {
            // The attacker freezes for BasicComboRules.HitstopFrames(dealt,
            // payload.Launches); the mob/boss victim must read the same rule.
            AssertThat(StoryEnemyCombatRules.VictimHitstopFrames(damage, launches: false))
                .IsEqual(BasicComboRules.HitstopFrames(damage, launches: false));
            AssertThat(StoryEnemyCombatRules.VictimHitstopFrames(damage, launches: true))
                .IsEqual(BasicComboRules.HitstopFrames(damage, launches: true));
            // F6's launch bonus is not dropped on the victim side.
            AssertThat(StoryEnemyCombatRules.VictimHitstopFrames(damage, launches: true))
                .IsEqual(StoryEnemyCombatRules.VictimHitstopFrames(damage, launches: false)
                    + BasicComboRules.LaunchHitstopBonusFrames);
        }
    }

    [TestCase]
    public void UltimateSourcedStunsChargeNoStandardPoise() {
        AssertThat(StoryEnemyCombatRules.ChargesStandardPoise(AttackClass.Ultimate, HitOrigin.Ultimate)).IsFalse();
        // A class-only or an origin-only Ultimate is still Ultimate-sourced.
        AssertThat(StoryEnemyCombatRules.ChargesStandardPoise(AttackClass.Ultimate, HitOrigin.Basic)).IsFalse();
        AssertThat(StoryEnemyCombatRules.ChargesStandardPoise(AttackClass.Special, HitOrigin.Ultimate)).IsFalse();
        // Basics and Specials keep charging the S3 budget.
        AssertThat(StoryEnemyCombatRules.ChargesStandardPoise(AttackClass.Special, HitOrigin.Special)).IsTrue();
        AssertThat(StoryEnemyCombatRules.ChargesStandardPoise(AttackClass.Basic, HitOrigin.Basic)).IsTrue();
    }

    [TestCase]
    public void AnAbilitysAuthoredShakeWinsAndBasicsKeepTheDamageCurve() {
        // An ability payload carries its AbilityData shake: x12 like the player-hit path.
        AssertThat(StoryEnemyCombatRules.UsesAuthoredShake(AttackClass.Special, 0.2f)).IsTrue();
        AssertThat(StoryEnemyCombatRules.UsesAuthoredShake(AttackClass.Ultimate, 0.6f)).IsTrue();
        AssertThat(StoryEnemyCombatRules.DealtHitShakeIntensity(AttackClass.Ultimate, 0.6f, damage: 30, launches: true))
            .IsEqualApprox(0.6f * StoryEnemyCombatRules.AuthoredShakePixelsPerIntensity, 0.0001f);
        AssertThat(StoryEnemyCombatRules.AuthoredShakePixelsPerIntensity).IsEqualApprox(12f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.DealtHitShakeDuration(AttackClass.Special, 0.8f, 0.3f, launches: false))
            .IsEqualApprox(0.3f, 0.0001f);
        // An authored intensity with no duration keeps the curve's duration.
        AssertThat(StoryEnemyCombatRules.DealtHitShakeDuration(AttackClass.Special, 0.8f, 0f, launches: true))
            .IsEqualApprox(StoryEnemyCombatRules.DealtHitShakeDuration(true), 0.0001f);

        // Basics carry the hitbox default, not a per-move value: damage curve.
        AssertThat(StoryEnemyCombatRules.UsesAuthoredShake(AttackClass.Basic, 0.2f)).IsFalse();
        AssertThat(StoryEnemyCombatRules.DealtHitShakeIntensity(AttackClass.Basic, 0.2f, damage: 12, launches: true))
            .IsEqualApprox(StoryEnemyCombatRules.DealtHitShakeIntensity(12, launches: true), 0.0001f);
        AssertThat(StoryEnemyCombatRules.DealtHitShakeDuration(AttackClass.Basic, 0.2f, 0.1f, launches: false))
            .IsEqualApprox(StoryEnemyCombatRules.DealtHitShakeDuration(false), 0.0001f);
        // An ability payload with no authored shake falls back too.
        AssertThat(StoryEnemyCombatRules.UsesAuthoredShake(AttackClass.Special, 0f)).IsFalse();
        AssertThat(StoryEnemyCombatRules.DealtHitShakeIntensity(AttackClass.Special, 0f, damage: 8, launches: false))
            .IsEqualApprox(StoryEnemyCombatRules.DealtHitShakeIntensity(8, launches: false), 0.0001f);
    }

    [TestCase]
    public void TheOverheadHoldNeedsATargetCloseAboveNotBeside() {
        AssertThat(StoryEnemyCombatRules.HoldsUnderTarget(10f, -250f)).IsTrue();
        AssertThat(StoryEnemyCombatRules.HoldsUnderTarget(-StoryEnemyCombatRules.ChaseHoldUnderTargetPixels,
            -StoryEnemyCombatRules.ChaseHoldUnderTargetMinRisePixels)).IsTrue();
        // Beside the mob at its own height is not overhead: it must still turn.
        AssertThat(StoryEnemyCombatRules.HoldsUnderTarget(-10f, 0f)).IsFalse();
        AssertThat(StoryEnemyCombatRules.HoldsUnderTarget(10f, -20f)).IsFalse();
        AssertThat(StoryEnemyCombatRules.HoldsUnderTarget(10f, 250f)).IsFalse();
        AssertThat(StoryEnemyCombatRules.HoldsUnderTarget(
            StoryEnemyCombatRules.ChaseHoldUnderTargetPixels + 1f, -250f)).IsFalse();
    }

    [TestCase]
    public void OnlyDirectHitsFreezeOrShakeAndOnlyPlayersDealFeedback() {
        AssertThat(StoryEnemyCombatRules.VictimSkipsHitstop(HitDelivery.DirectHit, false)).IsFalse();
        AssertThat(StoryEnemyCombatRules.VictimSkipsHitstop(HitDelivery.DirectHit, true)).IsTrue();
        AssertThat(StoryEnemyCombatRules.VictimSkipsHitstop(HitDelivery.Tick, false)).IsTrue();
        AssertThat(StoryEnemyCombatRules.VictimSkipsHitstop(HitDelivery.Construct, false)).IsTrue();
        AssertThat(StoryEnemyCombatRules.VictimSkipsHitstop(HitDelivery.Hazard, false)).IsTrue();
        AssertThat(StoryEnemyCombatRules.DealtHitShakes(HitDelivery.DirectHit, false)).IsTrue();
        AssertThat(StoryEnemyCombatRules.DealtHitShakes(HitDelivery.Construct, false)).IsFalse();

        AssertThat(StoryEnemyCombatRules.IsPlayerDealt(0, AttackClass.Basic, HitDelivery.DirectHit)).IsTrue();
        AssertThat(StoryEnemyCombatRules.IsPlayerDealt(-1, AttackClass.Basic, HitDelivery.DirectHit)).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsPlayerDealt(0, AttackClass.Hazard, HitDelivery.Hazard)).IsFalse();
    }

    [TestCase]
    public void DealtHitShakeScalesWithDamageCapsAndAddsALaunchBonus() {
        float light = StoryEnemyCombatRules.DealtHitShakeIntensity(4, launches: false);
        float heavy = StoryEnemyCombatRules.DealtHitShakeIntensity(20, launches: false);
        AssertThat(light < heavy).IsTrue();
        AssertThat(StoryEnemyCombatRules.DealtHitShakeIntensity(1000, launches: false))
            .IsEqualApprox(StoryEnemyCombatRules.DealtHitShakeMaxPixels, 0.0001f);
        AssertThat(StoryEnemyCombatRules.DealtHitShakeIntensity(20, launches: true))
            .IsEqualApprox(heavy + StoryEnemyCombatRules.DealtHitShakeLaunchBonusPixels, 0.0001f);
        AssertThat(StoryEnemyCombatRules.DealtHitShakeDuration(true)
                > StoryEnemyCombatRules.DealtHitShakeDuration(false)).IsTrue();
    }

    [TestCase]
    public void TheChaseLeashHoldsInsideTheRoomOrInsideTheWidenedRadius() {
        const float deAggro = 720f;
        float leash = deAggro * StoryEnemyCombatRules.ChaseLeashDeAggroFactor;
        AssertThat(StoryEnemyCombatRules.KeepsChasing(deAggro + 1f, deAggro, targetInRoom: false)).IsTrue();
        AssertThat(StoryEnemyCombatRules.KeepsChasing(leash, deAggro, targetInRoom: false)).IsTrue();
        AssertThat(StoryEnemyCombatRules.KeepsChasing(leash + 1f, deAggro, targetInRoom: false)).IsFalse();
        AssertThat(StoryEnemyCombatRules.KeepsChasing(leash * 4f, deAggro, targetInRoom: true)).IsTrue();
    }
}
