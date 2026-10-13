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
    public void AFullStringPlusTheLargestFirstSpecialFitsTheStandardPoiseButASecondSpecialTrips() {
        // D4 (2026-10-11): the budget is re-derived so one full string plus the
        // chain's first Special (charged x1.0) fits, and a second Special trips.
        int stringFrames = StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_1")
            + StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_2")
            + StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_3");
        AssertThat(stringFrames).IsEqual(102);
        float budget = StoryEnemyCombatRules.StandardStaggerBudgetSeconds;
        AssertThat((stringFrames + StoryEnemyCombatRules.FirstSpecialStunAllowanceFrames + 1) / 60f <= budget)
            .OverrideFailureMessage("A string plus the largest first Special must fit with a frame to spare.")
            .IsTrue();

        // The smallest second Special: one 12-frame hit, V7.4-diminished (a first
        // Special stunned the mob within 4 s) and taxed x1.5: 9 frames.
        float secondCost = StoryEnemyCombatRules.StandardBudgetCost(
            12 * EnemyStaggerRules.SpecialStunDiminishFactor / 60f, taxedSpecial: true);
        AssertThat(secondCost * 60f).IsEqualApprox(9f, 0.001f);
        // Joan's ruled route, Righteous Smite (30 frames of authored hitstun,
        // read from the real resource in StoryFirstSpecialTaxTests) -> hit 1 ->
        // hit 2 -> hit 3, plus any second Special trips with a frame to spare.
        const int smiteHitstunFrames = 30;
        AssertThat((stringFrames + smiteHitstunFrames) / 60f + secondCost > budget + 1f / 60f)
            .OverrideFailureMessage("The ruled route plus a second Special must trip Armored Recovery.")
            .IsTrue();

        AssertThat(StoryEnemyCombatRules.StandardBudgetCost(0.5f, taxedSpecial: false)).IsEqualApprox(0.5f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.StandardBudgetCost(0.5f, taxedSpecial: true)).IsEqualApprox(0.75f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.StandardSpecialStunBudgetCostMultiplier).IsEqualApprox(1.5f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.StandardArmoredRecoverySeconds
                < EnemyStaggerRules.EliteArmoredRecoverySeconds).IsTrue();
    }

    [TestCase]
    public void TheChainsFirstSpecialIsUntaxedWhileLaterSpecialsAndMovementHitsKeepTheConfirmTax() {
        // D4: the first Special-class, non-Ultimate hit claims the chain's slot.
        AssertThat(StoryEnemyCombatRules.ClassifyChainSpecial(
                AttackClass.Special, HitOrigin.Special, false, "joan_righteous_smite", null))
            .IsEqual(StoryEnemyCombatRules.ChainSpecialRole.FirstSpecial);
        AssertThat(StoryEnemyCombatRules.ClassifyChainSpecial(
                AttackClass.Special, HitOrigin.Special, false, "joan_righteous_smite", ""))
            .IsEqual(StoryEnemyCombatRules.ChainSpecialRole.FirstSpecial);
        // Every hit of the recorded first Special shares its role.
        AssertThat(StoryEnemyCombatRules.ClassifyChainSpecial(
                AttackClass.Special, HitOrigin.Special, false, "joan_righteous_smite", "joan_righteous_smite"))
            .IsEqual(StoryEnemyCombatRules.ChainSpecialRole.FirstSpecial);
        // Any other Special is a second Special.
        AssertThat(StoryEnemyCombatRules.ClassifyChainSpecial(
                AttackClass.Special, HitOrigin.Special, false, "joan_divine_piercing", "joan_righteous_smite"))
            .IsEqual(StoryEnemyCombatRules.ChainSpecialRole.OtherSpecial);
        // A movement-ability hit never claims the slot, even on a fresh chain.
        AssertThat(StoryEnemyCombatRules.ClassifyChainSpecial(
                AttackClass.Special, HitOrigin.Special, true, "lincoln_rail_charge", null))
            .IsEqual(StoryEnemyCombatRules.ChainSpecialRole.OtherSpecial);
        // A Special with no identity is never the first.
        AssertThat(StoryEnemyCombatRules.ClassifyChainSpecial(
                AttackClass.Special, HitOrigin.Special, false, "", null))
            .IsEqual(StoryEnemyCombatRules.ChainSpecialRole.OtherSpecial);
        AssertThat(StoryEnemyCombatRules.ClassifyChainSpecial(
                AttackClass.Special, HitOrigin.Special, false, null, null))
            .IsEqual(StoryEnemyCombatRules.ChainSpecialRole.OtherSpecial);
        // Ultimates (origin or class) and non-Specials are not Specials at all.
        AssertThat(StoryEnemyCombatRules.ClassifyChainSpecial(
                AttackClass.Special, HitOrigin.Ultimate, false, "joan_grand_crusade", null))
            .IsEqual(StoryEnemyCombatRules.ChainSpecialRole.NotSpecial);
        AssertThat(StoryEnemyCombatRules.ClassifyChainSpecial(
                AttackClass.Ultimate, HitOrigin.Ultimate, false, "joan_grand_crusade", null))
            .IsEqual(StoryEnemyCombatRules.ChainSpecialRole.NotSpecial);
        AssertThat(StoryEnemyCombatRules.ClassifyChainSpecial(
                AttackClass.Basic, HitOrigin.Basic, false, "joan.basic", null))
            .IsEqual(StoryEnemyCombatRules.ChainSpecialRole.NotSpecial);

        // The first Special lands x1.0 at any chain index, confirm or not.
        foreach (int index in new[] { 1, 4, 9 }) {
            AssertThat(StoryEnemyCombatRules.ChainHitScale(
                    StoryEnemyCombatRules.ChainSpecialRole.FirstSpecial, index, specialConfirm: true))
                .IsEqualApprox(1f, 0.0001f);
            AssertThat(StoryEnemyCombatRules.ChainHitScale(
                    StoryEnemyCombatRules.ChainSpecialRole.OtherSpecial, index, specialConfirm: true))
                .IsEqualApprox(StoryEnemyCombatRules.ConfirmProrationScale(index, true), 0.0001f);
            AssertThat(StoryEnemyCombatRules.ChainHitScale(
                    StoryEnemyCombatRules.ChainSpecialRole.NotSpecial, index, specialConfirm: false))
                .IsEqualApprox(StoryEnemyCombatRules.ConfirmProrationScale(index, false), 0.0001f);
        }
        AssertThat(StoryEnemyCombatRules.ChainHitScale(
                StoryEnemyCombatRules.ChainSpecialRole.OtherSpecial, 4, specialConfirm: true))
            .IsEqualApprox(0.85f * 0.6f, 0.0001f);
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
        // D4: and the whole budget, so a chain that filled it without tripping
        // (a string plus the largest first Special) starts the next chain empty.
        AssertThat(StoryEnemyCombatRules.StandardStaggerDecayPerSecond * EnemyStaggerRules.GetupArmorSeconds
                >= StoryEnemyCombatRules.StandardStaggerBudgetSeconds)
            .OverrideFailureMessage("The getup armor must drain a standard's whole poise budget.")
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

    // === D8 (user direction 2026-10-11): the Punish window ===

    [TestCase]
    public void ThePunishWindowIsSixtyFramesAtOnePointTwoForPlayerDirectHitsOnly() {
        // The user's numbers (USER-PUNISH-WINDOW).
        AssertThat(StoryEnemyCombatRules.PunishWindowFrames).IsEqual(60);
        AssertThat(StoryEnemyCombatRules.PunishDamageScale).IsEqualApprox(1.2f, 0.0001f);
        // The non-colour channel of a punished number enlarges it.
        AssertThat(StoryEnemyCombatRules.PunishDamageNumberScale > 1f).IsTrue();

        // A player's direct hit of any class but a hazard is eligible...
        AssertThat(StoryEnemyCombatRules.IsPunishEligible(0, AttackClass.Basic, HitDelivery.DirectHit)).IsTrue();
        AssertThat(StoryEnemyCombatRules.IsPunishEligible(0, AttackClass.Special, HitDelivery.DirectHit)).IsTrue();
        AssertThat(StoryEnemyCombatRules.IsPunishEligible(0, AttackClass.Ultimate, HitDelivery.DirectHit)).IsTrue();
        // ...while ticks, construct hits, hazards and enemy-sourced hits are not.
        AssertThat(StoryEnemyCombatRules.IsPunishEligible(0, AttackClass.Special, HitDelivery.Tick)).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsPunishEligible(0, AttackClass.Basic, HitDelivery.Construct)).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsPunishEligible(0, AttackClass.Hazard, HitDelivery.Hazard)).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsPunishEligible(0, AttackClass.Hazard, HitDelivery.DirectHit)).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsPunishEligible(-1, AttackClass.Basic, HitDelivery.DirectHit)).IsFalse();

        // One composition, one rounding: Joan's 6-damage hit 1 on a punished
        // standard is 6 x 1.25 (S4) x 1.2 = 9, against 8 unpunished; a punished
        // first Smite is 28 x 1.2 = 33.6 -> 34.
        AssertThat(StoryEnemyCombatRules.ScaleIntakeDamage(6,
            StoryEnemyCombatRules.StandardBasicDamageScale * StoryEnemyCombatRules.PunishDamageScale)).IsEqual(9);
        AssertThat(StoryEnemyCombatRules.ScaleIntakeDamage(6, StoryEnemyCombatRules.StandardBasicDamageScale))
            .IsEqual(8);
        AssertThat(StoryEnemyCombatRules.ScaleIntakeDamage(28, StoryEnemyCombatRules.PunishDamageScale))
            .IsEqual(34);
    }

    [TestCase]
    public void AMeleeWhiffIsNearOnlyInsideItsPivotReachPlusTheMargin() {
        // The one shared reach helper (spec B §0.3 / spec D D6c): the legacy
        // scalar melee (a 40 px box at offset 30) reaches 70 px against the
        // Story hero's 20 px hurtbox half-width.
        float heroHalfWidth = StoryCombatRules.HeroHurtboxWidthPixels / 2f;
        AssertThat(heroHalfWidth).IsEqualApprox(20f, 0.0001f);
        float reach = StoryEnemyCombatRules.MeleePivotReachPixels(30f, 40f, 0f, heroHalfWidth);
        AssertThat(reach).IsEqualApprox(70f, 0.0001f);
        // A mirrored (negative) authored offset reaches as far, and a charge's travel adds.
        AssertThat(StoryEnemyCombatRules.MeleePivotReachPixels(-30f, 40f, 0f, 20f)).IsEqualApprox(70f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.MeleePivotReachPixels(30f, 40f, 65f, 20f)).IsEqualApprox(135f, 0.0001f);

        // Near: within the reach plus the 40 px margin, and within 96 px vertically.
        AssertThat(StoryEnemyCombatRules.PunishWhiffNearMarginPixels).IsEqualApprox(40f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.PunishWhiffIsNear(reach + 40f, 0f, reach)).IsTrue();
        AssertThat(StoryEnemyCombatRules.PunishWhiffIsNear(reach + 41f, 0f, reach)).IsFalse();
        AssertThat(StoryEnemyCombatRules.PunishWhiffIsNear(0f, 96f, reach)).IsTrue();
        AssertThat(StoryEnemyCombatRules.PunishWhiffIsNear(0f, 97f, reach)).IsFalse();
    }

    // === D6a / D6c / D9 (user direction 2026-10-11, spec D sub-pass (ii)) ===

    [TestCase]
    public void TheFinisherLandsOneStepOutsideTheAttackersReachAndAHeavyFinisherOutlandsTheWidestReach() {
        // The widest authored profile is Cleopatra's 120 %, so against a Standard
        // (23 px hurtbox half-width) the widest hit-1 pivot reach is 96 x 1.2 + 23.
        AssertThat(BasicComboRules.WidestStringReachWidthPercent).IsEqual(120);
        AssertThat(StoryEnemyCombatRules.TemplateHit1FarEdgePixels).IsEqualApprox(96f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.WidestHit1PivotReachPixels(23f)).IsEqualApprox(138.2f, 0.001f);

        // A template hero (R1 119) with an ordinary 13.5 finisher: one step out.
        AssertThat(StoryEnemyCombatRules.FinisherLandingTargetPixels(119f, 138.2f, 13.5f)).IsEqualApprox(149f, 0.001f);
        // A heavy finisher (Lincoln, 22.5) lands past the widest reach's step.
        AssertThat(StoryEnemyCombatRules.FinisherLandingTargetPixels(104.6f, 138.2f, 22.5f)).IsEqualApprox(183.2f, 0.001f);
        AssertThat(StoryEnemyCombatRules.FinisherLandingTargetPixels(142.8f, 138.2f, 22.5f)).IsEqualApprox(183.2f, 0.001f);
        // ... and only at or above the heavy force.
        AssertThat(StoryEnemyCombatRules.FinisherLandingTargetPixels(104.6f, 138.2f, 17.99f)).IsEqualApprox(134.6f, 0.001f);

        // The carry never goes negative: the rule never lengthens a carry.
        AssertThat(StoryEnemyCombatRules.FinisherCarryPixels(149f, 56f)).IsEqualApprox(93f, 0.001f);
        AssertThat(StoryEnemyCombatRules.FinisherCarryPixels(149f, 200f)).IsEqual(0f);

        // The deceleration, stepped at 60 Hz (speed slowed before each move),
        // travels the carry to within half a pixel.
        foreach ((float speed, float carry) in new[] { (686f, 103f), (1331f, 81f), (428f, 125f) }) {
            float decel = StoryEnemyCombatRules.FinisherCarryDeceleration(speed, carry);
            float v = speed;
            float travelled = 0f;
            for (int frame = 0; frame < 600 && v > 0f; frame++) {
                v = System.Math.Max(0f, v - decel / 60f);
                travelled += v / 60f;
            }
            AssertThat(System.Math.Abs(travelled - carry) <= 0.5f)
                .OverrideFailureMessage($"{speed} px/s over {carry} px travelled {travelled:F3} px.")
                .IsTrue();
        }
        AssertThat(StoryEnemyCombatRules.FinisherCarryDeceleration(0f, 100f)).IsEqual(0f);
        AssertThat(StoryEnemyCombatRules.FinisherCarryDeceleration(-50f, 100f)).IsEqual(0f);
        // A zero carry stops the mob on its first step.
        float stop = StoryEnemyCombatRules.FinisherCarryDeceleration(500f, 0f);
        AssertThat(500f - stop / 60f <= 0.0001f).IsTrue();

        // Only a launching Basic combo_3 is a finisher carry.
        AssertThat(StoryEnemyCombatRules.IsFinisherCarry(AttackClass.Basic, "combo_3", launches: true)).IsTrue();
        AssertThat(StoryEnemyCombatRules.IsFinisherCarry(AttackClass.Basic, "combo_2", launches: true)).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsFinisherCarry(AttackClass.Basic, BasicComboRules.UpAttackHitboxID, true)).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsFinisherCarry(AttackClass.Basic, BasicComboRules.DownAirHitboxID, true)).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsFinisherCarry(AttackClass.Special, "combo_3", launches: true)).IsFalse();
        AssertThat(StoryEnemyCombatRules.IsFinisherCarry(AttackClass.Basic, "combo_3", launches: false)).IsFalse();
    }

    [TestCase]
    public void AMeleeMobCommitsInsideItsHitboxReachAndRestsOutsidePushboxContact() {
        AssertThat(StoryEnemyCombatRules.MeleePivotReachPixels(30f, 40f, 0f, 20f)).IsEqualApprox(70f, 0.0001f);
        // A ChargeDash's travel adds to its reach.
        AssertThat(StoryEnemyCombatRules.MeleePivotReachPixels(30f, 40f, 700f * 18f / 60f, 20f))
            .IsEqualApprox(280f, 0.001f);

        AssertThat(StoryEnemyCombatRules.MeleeCommitRangePixels(90f, 70f)).IsEqualApprox(60f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.MeleeCommitRangePixels(50f, 70f)).IsEqualApprox(50f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.MeleeCommitRangePixels(90f, 8f)).IsEqualApprox(90f, 0.0001f);

        // The slasher: commit 60, contact 37 (19 + 18), 300 px/s, 8-frame stop.
        // 0.85 x 60 = 51 loses to 37 + 4 + 300 x 9 / 120 = 63.5.
        AssertThat(StoryEnemyCombatRules.MeleeStandOffEngagePixels(60f, 0.85f, 37f, 300f, 8f))
            .IsEqualApprox(63.5f, 0.0001f);
        // A long commit line keeps the 0.85 fraction.
        AssertThat(StoryEnemyCombatRules.MeleeStandOffEngagePixels(200f, 0.85f, 37f, 300f, 8f))
            .IsEqualApprox(170f, 0.0001f);

        // The legacy 40 px box at y -25 spans -45 .. -5 above the mob's feet.
        AssertThat(StoryEnemyCombatRules.MeleeBoxReachesVertically(-25f, 40f, 0f, 64f)).IsTrue();
        AssertThat(StoryEnemyCombatRules.MeleeBoxReachesVertically(-25f, 40f, -60f, 64f)).IsFalse();
        // Touching edges do not overlap.
        AssertThat(StoryEnemyCombatRules.MeleeBoxReachesVertically(-25f, 40f, -45f, 64f)).IsFalse();
        AssertThat(StoryEnemyCombatRules.MeleeBoxReachesVertically(-25f, 40f, 69f, 64f)).IsFalse();
        AssertThat(StoryEnemyCombatRules.MeleeBoxReachesVertically(-25f, 40f, 58f, 64f)).IsTrue();
    }

    [TestCase]
    public void AWallBounceNeedsAFastLaunchIntoAFacingWallAndHurtsATenthOfTheIntake() {
        AssertThat(StoryEnemyCombatRules.BouncesOffWall(301f, -1f)).IsTrue();
        AssertThat(StoryEnemyCombatRules.BouncesOffWall(-301f, 1f)).IsTrue();
        AssertThat(StoryEnemyCombatRules.BouncesOffWall(300f, -1f)).IsFalse();
        AssertThat(StoryEnemyCombatRules.BouncesOffWall(500f, -0.5f)).IsFalse();
        AssertThat(StoryEnemyCombatRules.BouncesOffWall(500f, 0.5f)).IsFalse();
        AssertThat(StoryEnemyCombatRules.BouncesOffWall(500f, 1f)).IsFalse();
        AssertThat(StoryEnemyCombatRules.BouncesOffWall(500f, -0.7f)).IsTrue();

        AssertThat(StoryEnemyCombatRules.WallBounceVelocityX(500f)).IsEqualApprox(-200f, 0.0001f);
        AssertThat(StoryEnemyCombatRules.WallBounceVelocityX(-500f)).IsEqualApprox(200f, 0.0001f);

        AssertThat(StoryEnemyCombatRules.WallImpactDamage(10)).IsEqual(1);
        AssertThat(StoryEnemyCombatRules.WallImpactDamage(15)).IsEqual(2);
        AssertThat(StoryEnemyCombatRules.WallImpactDamage(16)).IsEqual(2);
        AssertThat(StoryEnemyCombatRules.WallImpactDamage(25)).IsEqual(3);
        AssertThat(StoryEnemyCombatRules.WallImpactDamage(4)).IsEqual(1);
        AssertThat(StoryEnemyCombatRules.WallImpactDamage(0)).IsEqual(0);

        AssertThat(StoryEnemyCombatRules.ArmsWallBounce(0, AttackClass.Basic, HitDelivery.DirectHit, launches: true, phasesThroughWalls: false)).IsTrue();
        AssertThat(StoryEnemyCombatRules.ArmsWallBounce(0, AttackClass.Ultimate, HitDelivery.DirectHit, true, false)).IsTrue();
        AssertThat(StoryEnemyCombatRules.ArmsWallBounce(0, AttackClass.Basic, HitDelivery.DirectHit, launches: false, phasesThroughWalls: false)).IsFalse();
        AssertThat(StoryEnemyCombatRules.ArmsWallBounce(0, AttackClass.Hazard, HitDelivery.Hazard, true, false)).IsFalse();
        AssertThat(StoryEnemyCombatRules.ArmsWallBounce(0, AttackClass.Basic, HitDelivery.Hazard, true, false)).IsFalse();
        AssertThat(StoryEnemyCombatRules.ArmsWallBounce(-1, AttackClass.Basic, HitDelivery.DirectHit, true, false)).IsFalse();
        AssertThat(StoryEnemyCombatRules.ArmsWallBounce(0, AttackClass.Basic, HitDelivery.DirectHit, true, phasesThroughWalls: true)).IsFalse();
    }
}
