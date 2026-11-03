using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The running attack's rulebook (GR-RUN, user ruling 9b, 2026-11-03): the one
/// selection rule, Story's predicate, the shared numbers and its place in the
/// universal basic set's S4, S5, S2 and Momentum reads. Pure C#. Every number is
/// provisional (<c>PROVISIONAL-RUN-ATTACK-RULES</c>). The sim half is
/// <c>FighterRunAttackTests</c>, the Story half <c>StoryRunAttackTests</c>.
/// </summary>
[TestSuite]
public class RunAttackRulesTests {

    [TestCase]
    public void TheSelectionOrderIsUpThenDownAirThenAirStrikeThenRunAttackThenChain() {
        (bool Up, bool Down, bool Air, bool Running, int Expected)[] table = {
            (true, false, false, true, BasicComboRules.VariantUpAttack),
            (true, true, true, true, BasicComboRules.VariantUpAttack),
            (false, true, true, true, BasicComboRules.VariantDownAir),
            (false, false, true, true, BasicComboRules.VariantAirAttack),
            (false, false, false, true, BasicComboRules.VariantRunAttack),
            // A grounded Down-attack stays the string's opener, running or not.
            (false, true, false, true, BasicComboRules.VariantChain),
            (false, false, false, false, BasicComboRules.VariantChain)
        };
        foreach ((bool up, bool down, bool air, bool running, int expected) in table) {
            AssertThat(BasicComboRules.SelectAttackVariant(up, down, air, running))
                .OverrideFailureMessage($"up={up} down={down} air={air} running={running}")
                .IsEqual(expected);
        }
        // Every caller that states nothing keeps today's answer: a D3 cancel and a
        // chain-hold press pass no "running".
        AssertThat(BasicComboRules.SelectAttackVariant(upHeld: false, downHeld: false, airborne: false))
            .IsEqual(BasicComboRules.VariantChain);
    }

    [TestCase]
    public void StorysPredicateNeedsTheRunningStateAndTheSpeedAlongTheFacing() {
        const float max = 300f;
        float atFloor = max * (float)BasicComboRules.RunAttackMinSpeedFraction;
        AssertThat(BasicComboRules.IsRunningForAttack(true, atFloor, max)).IsTrue();
        AssertThat(BasicComboRules.IsRunningForAttack(true, max, max)).IsTrue();
        AssertThat(BasicComboRules.IsRunningForAttack(true, atFloor - 1f, max))
            .OverrideFailureMessage("The first frames of a push are not a run.")
            .IsFalse();
        AssertThat(BasicComboRules.IsRunningForAttack(false, max, max))
            .OverrideFailureMessage("Sliding at speed out of the Running state is not a run.")
            .IsFalse();
        AssertThat(BasicComboRules.IsRunningForAttack(true, -max, max))
            .OverrideFailureMessage("Moving against the facing is not a run.")
            .IsFalse();
        AssertThat(BasicComboRules.IsRunningForAttack(true, 0f, 0f))
            .OverrideFailureMessage("A rooted hero (a maximum of 0) is not running.")
            .IsFalse();
    }

    [TestCase]
    public void TheRunAttacksNumbersAreTheOnesTheRulingProvisionallyChose() {
        AssertThat(BasicComboRules.RunAttackStartupFrames).IsEqual(9);
        AssertThat(BasicComboRules.RunAttackActiveFrames).IsEqual(6);
        AssertThat(BasicComboRules.RunAttackRecoveryFrames).IsEqual(22);
        AssertThat(BasicComboRules.RunAttackDamageMultiplier).IsEqual(1.2f);
        AssertThat(BasicComboRules.RunAttackHitstunFrames).IsEqual(30);
        AssertThat(BasicComboRules.RunAttackLaunches).IsFalse();
        AssertThat(BasicComboRules.RunAttackKnockbackMultiplier).IsEqual(1.5f);
        AssertThat(BasicComboRules.RunAttackLungeUnits).IsEqual(0.5);
        AssertThat(BasicComboRules.RunAttackReachBonusUnits).IsEqual(0.4);
        AssertThat(BasicComboRules.RunAttackMinSpeedFraction).IsEqual(0.6);
        AssertThat(BasicComboRules.RunAttackPoseStringStep).IsEqual(1);
        // The lunge speed is derived, so the active frames always cover the lunge.
        double travelled = BasicComboRules.RunAttackLungeSpeedUnits * BasicComboRules.RunAttackActiveFrames / 60.0;
        AssertThat(System.Math.Abs(travelled - BasicComboRules.RunAttackLungeUnits) < 1e-12).IsTrue();
    }

    [TestCase]
    public void ItIsInTheBasicSetSoS4S5AndS2ReadItLikeAStringOpener() {
        const string id = BasicComboRules.RunAttackHitboxID;
        AssertThat(BasicComboRules.IsBasicStringHitbox(id)).IsTrue();
        // S4 and the 8.1 floor.
        AssertThat(StoryEnemyCombatRules.IsBasicsPacingHit(AttackClass.Basic, id)).IsTrue();
        // S5: hit 1's floor, and the elite budget keeps charging the cross-mode one.
        AssertThat(StoryEnemyCombatRules.BasicStringStunFloorFrames(id))
            .IsEqual(StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_1"));
        AssertThat(StoryEnemyCombatRules.EliteBudgetStunFloorFrames(id))
            .IsEqual(BasicComboRules.EnemyBasicStunFloorFrames);
        // It opens a chain but is no chain hit: no finisher carry, no D1 chain step.
        AssertThat(StoryEnemyCombatRules.StringStepOf(id)).IsEqual(-1);
        // S2: a basic is never exempt from the chain decay.
        AssertThat(StoryEnemyCombatRules.IsProrationExempt(AttackClass.Basic, HitOrigin.Basic, HitDelivery.DirectHit))
            .IsFalse();
        // S3: a running attack plus a whole string stays under the standard's budget.
        int chain = StoryEnemyCombatRules.BasicStringStunFloorFrames(id)
            + StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_1")
            + StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_2")
            + StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_3");
        AssertThat(chain / 60f < StoryEnemyCombatRules.StandardStaggerBudgetSeconds)
            .OverrideFailureMessage($"A run attack and a string charge {chain} frames of poise.")
            .IsTrue();
    }

    [TestCase]
    public void ItGainsTheSingleStrikesMomentumInBothModesReads() {
        // Through the names, never the values: GR-MOM owns the values.
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantRunAttack, 0))
            .IsEqual(BasicComboRules.MomentumDirectionalGainUnits);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox(BasicComboRules.RunAttackHitboxID))
            .IsEqual(BasicComboRules.MomentumDirectionalGainUnits);
    }
}
