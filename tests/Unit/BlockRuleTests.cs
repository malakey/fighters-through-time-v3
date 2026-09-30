using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
[RequireGodotRuntime]
public class BlockRuleTests {
    [TestCase]
    public void FacingRightBlocksOnlyHitsOriginatingOnTheRight() {
        Vector2 defender = new(100f, 0f);

        AssertThat(BlockRules.IsHitInFront(defender, true, new Vector2(120f, 0f))).IsTrue();
        AssertThat(BlockRules.IsHitInFront(defender, true, new Vector2(80f, 0f))).IsFalse();
    }

    [TestCase]
    public void FacingLeftBlocksOnlyHitsOriginatingOnTheLeft() {
        Vector2 defender = new(100f, 0f);

        AssertThat(BlockRules.IsHitInFront(defender, false, new Vector2(80f, 0f))).IsTrue();
        AssertThat(BlockRules.IsHitInFront(defender, false, new Vector2(120f, 0f))).IsFalse();
    }

    [TestCase]
    public void BasicConsumesOneChargeSpecialTwoAndShieldBreakerAll() {
        // A01 (Package 13 W1), rewritten in place: an ordinary Special costs
        // min(2, charges); only a Shield-Breaker takes every charge.
        AssertThat(BlockRules.ChargeCost(AttackClass.Basic, 3)).IsEqual(1);
        AssertThat(BlockRules.ChargeCost(AttackClass.Special, 3)).IsEqual(2);
        AssertThat(BlockRules.ChargeCost(AttackClass.Special, 2)).IsEqual(2);
        AssertThat(BlockRules.ChargeCost(AttackClass.Special, 1)).IsEqual(1);
        AssertThat(BlockRules.ChargeCost(AttackClass.Special, 3, shieldBreaker: true)).IsEqual(3);
        AssertThat(BlockRules.ChargeCost(AttackClass.Basic, 3, shieldBreaker: true))
            .OverrideFailureMessage("The Shield-Breaker flag only means anything on a Special.")
            .IsEqual(1);
    }

    [TestCase]
    public void OnlyUltimatesBypassBlock() {
        AssertThat(BlockRules.BypassesBlock(AttackClass.Ultimate)).IsTrue();
        AssertThat(BlockRules.BypassesBlock(AttackClass.Hazard)).IsFalse();
        AssertThat(BlockRules.BypassesBlock(AttackClass.Basic)).IsFalse();
        AssertThat(BlockRules.BypassesBlock(AttackClass.Special)).IsFalse();
        AssertThat(BlockRules.ChargeCost(AttackClass.Hazard, 3)).IsEqual(1);
    }
}
