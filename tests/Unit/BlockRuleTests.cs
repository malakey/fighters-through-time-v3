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
    public void BasicConsumesOneChargeAndSpecialConsumesAll() {
        AssertThat(BlockRules.ChargeCost(AttackClass.Basic, 3)).IsEqual(1);
        AssertThat(BlockRules.ChargeCost(AttackClass.Special, 3)).IsEqual(3);
    }

    [TestCase]
    public void UltimatesAndHazardsBypassBlock() {
        AssertThat(BlockRules.BypassesBlock(AttackClass.Ultimate)).IsTrue();
        AssertThat(BlockRules.BypassesBlock(AttackClass.Hazard)).IsTrue();
        AssertThat(BlockRules.BypassesBlock(AttackClass.Basic)).IsFalse();
        AssertThat(BlockRules.BypassesBlock(AttackClass.Special)).IsFalse();
    }
}
