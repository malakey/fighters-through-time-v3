using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W1 — A01 (Fighter half) and D10. An ordinary blocked player
/// Special spends <c>min(2, charges)</c> with the ordinary 8-frame shieldstun
/// when it does not shatter; only an authored <see cref="BlockClass.ShieldBreaker"/>
/// spends every charge. The loadout projects the three authored Shield-Breakers
/// and the signed knockback vector (D10) instead of the retired
/// <c>max(|x|, |y|)</c> flattening. The Story half is
/// <c>tests/Unit/StorySpecialBlockChargeCostTests.cs</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SpecialBlockChargeCostTests {

    private const int BlockButton = 1 << 6;

    [TestCase]
    public void AnOrdinarySpecialAtThreeChargesLeavesOneWithShieldstunAndNoDaze() {
        Blocked(charges: 3, shieldBreaker: false, out FighterStateComponent target, out FighterVerbComponent verb, out bool landed);
        AssertThat(landed).IsFalse();
        AssertThat(target.BlockCharges)
            .OverrideFailureMessage("A01: a full shield keeps one charge after an ordinary Special.")
            .IsEqual(1);
        AssertThat(target.DazeFrames).IsEqual(0);
        AssertThat(verb.BlockLockoutFrames).IsEqual(0);
        AssertThat(verb.ShieldStunFrames)
            .OverrideFailureMessage("A non-shattering Special block applies the ordinary shieldstun.")
            .IsEqual(BasicComboRules.ShieldstunFrames);
        AssertThat(target.CurrentHP).IsEqual(100);
    }

    [TestCase]
    public void AnOrdinarySpecialShattersAtOneOrTwoCharges() {
        foreach (int charges in new[] { 1, 2 }) {
            Blocked(charges, shieldBreaker: false, out FighterStateComponent target, out FighterVerbComponent verb, out _);
            AssertThat(target.BlockCharges)
                .OverrideFailureMessage($"{charges} charge(s): the two-charge Special shatters the shield.")
                .IsEqual(0);
            AssertThat(target.DazeFrames).IsEqual(60);
            AssertThat(verb.BlockLockoutFrames).IsEqual(BasicComboRules.BlockShatterLockoutFrames);
        }
    }

    [TestCase]
    public void AShieldBreakerShattersAFullShield() {
        Blocked(charges: 3, shieldBreaker: true, out FighterStateComponent target, out FighterVerbComponent verb, out _);
        AssertThat(target.BlockCharges).IsEqual(0);
        AssertThat(target.DazeFrames).IsEqual(60);
        AssertThat(verb.BlockLockoutFrames).IsEqual(BasicComboRules.BlockShatterLockoutFrames);
        AssertThat(verb.ShieldStunFrames).IsEqual(0);
    }

    [TestCase]
    public void TheLoadoutProjectsExactlyTheThreeAuthoredShieldBreakers() {
        FighterLoadout joan = Load("joan");
        FighterLoadout lincoln = Load("lincoln");
        FighterLoadout tesla = Load("tesla");
        AssertThat(joan.AbilityModes.SpecialTwoHit.ShieldBreaker).IsTrue();
        AssertThat(joan.AbilityModes.SpecialOneHit.ShieldBreaker).IsFalse();
        AssertThat(lincoln.AbilityModes.SpecialOneHit.ShieldBreaker).IsTrue();
        AssertThat(lincoln.AbilityModes.SpecialTwoHit.ShieldBreaker).IsTrue();
        AssertThat(tesla.AbilityModes.SpecialOneHit.ShieldBreaker).IsFalse();
        AssertThat(tesla.AbilityModes.SpecialTwoHit.ShieldBreaker).IsFalse();
    }

    [TestCase]
    public void TheLoadoutProjectsTheSignedKnockbackVector() {
        // D10: Splitting Strike authors (4, 5) — Godot Y-down, i.e. a spike.
        // The sim is Y-up, so the projected vertical is -5, not max(4, 5).
        FighterAbilityHitData splitting = Load("lincoln").AbilityModes.SpecialTwoHit;
        AssertThat(splitting.HasKnockbackVector).IsTrue();
        AssertThat(splitting.KnockbackX.RawValue).IsEqual(FP64.FromFloat(4f).RawValue);
        AssertThat(splitting.KnockbackY.RawValue).IsEqual(FP64.FromFloat(-5f).RawValue);

        // The spike reaches the victim through ApplyFighterHit: a negative
        // vertical velocity, not the flattened upward pop.
        FighterStateComponent attacker = NewFighter(0, facingRight: true);
        FighterRuntimeComponent attackerRuntime = default;
        FighterVerbComponent attackerVerb = default;
        FighterStateComponent target = NewFighter(1, facingRight: false);
        target.IsGrounded = 0;
        target.Position = new FPVector2(FP64.One, FP64.FromInt(3));
        FighterRuntimeComponent targetRuntime = default;
        FighterVerbComponent targetVerb = default;
        FighterDefenseComponent defense = default;
        FighterTuningComponent tuning = default;
        FighterDamageRules.ApplyFighterHit(
            ref attacker, ref attackerRuntime, ref attackerVerb,
            ref target, ref targetRuntime, ref targetVerb, ref defense, in tuning,
            FighterDamageRules.SpecialAttackClass, 10, splitting.KnockbackX, 12,
            (int)StatusType.None, 0, FP64.One, attacker.Position.x,
            hasKnockbackVector: true, knockbackVertical: splitting.KnockbackY);
        AssertThat(target.Velocity.y < FP64.Zero)
            .OverrideFailureMessage("A signed downward vector must spike the victim.")
            .IsTrue();
        AssertThat(target.Velocity.x > FP64.Zero)
            .OverrideFailureMessage("The horizontal component still pushes away from the attacker.")
            .IsTrue();
    }

    private static FighterLoadout Load(string characterID) =>
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres"));

    private static void Blocked(
        int charges, bool shieldBreaker,
        out FighterStateComponent target, out FighterVerbComponent targetVerb, out bool landed) {
        FighterStateComponent attacker = NewFighter(0, facingRight: false);
        FighterRuntimeComponent attackerRuntime = default;
        FighterVerbComponent attackerVerb = default;
        target = NewFighter(1, facingRight: true);
        target.BlockCharges = charges;
        FighterRuntimeComponent targetRuntime = new() { HeldButtons = BlockButton };
        targetVerb = default;
        FighterDefenseComponent defense = default;
        FighterTuningComponent tuning = default;
        AssertThat(FighterBasicAttackRules.IsBlockStance(in target, in targetRuntime, in targetVerb)).IsTrue();
        // The attacker stands in front (to the right) of the right-facing blocker.
        landed = FighterDamageRules.ApplyFighterHit(
            ref attacker, ref attackerRuntime, ref attackerVerb,
            ref target, ref targetRuntime, ref targetVerb, ref defense, in tuning,
            FighterDamageRules.SpecialAttackClass, 20, FP64.FromInt(4), 18,
            (int)StatusType.None, 0, FP64.One,
            target.Position.x + FP64.One,
            shieldBreaker: shieldBreaker);
    }

    private static FighterStateComponent NewFighter(int playerID, bool facingRight) => new() {
        PlayerID = playerID,
        Stocks = 3,
        MaxHP = 100,
        CurrentHP = 100,
        BlockCharges = 3,
        IsGrounded = 1,
        FacingRight = facingRight ? 1 : 0,
        Weight = FP64.One
    };
}
