using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W1 — A01 (Story half). A blocked ordinary player Special spends
/// <c>min(2, charges)</c> with shieldstun when it does not shatter; an authored
/// Shield-Breaker spends every charge. Kit payloads carry the resource's block
/// class through <see cref="BaseSpecial.WithAbilityContract"/>, a construct hit
/// never breaks, and the classification never reaches Shield of Orléans'
/// explicit two-charge Guard-Crush cost.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StorySpecialBlockChargeCostTests {

    [TestCase]
    public void AnOrdinarySpecialLeavesOneChargeWithShieldstun() {
        PlayerController player = NewPlayer(out BlockSystem block);
        try {
            block.StartBlock();
            BlockResult result = block.ResolveHit(SpecialHit(player, shieldBreaker: false));
            AssertThat(result).IsEqual(BlockResult.Blocked);
            AssertThat(block.CurrentCharges).IsEqual(1);
            AssertThat(block.IsInShieldStun).IsTrue();
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.Dazed);
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void AShieldBreakerShattersAFullShield() {
        PlayerController player = NewPlayer(out BlockSystem block);
        try {
            block.StartBlock();
            BlockResult result = block.ResolveHit(SpecialHit(player, shieldBreaker: true));
            AssertThat(result).IsEqual(BlockResult.GuardBroken);
            AssertThat(block.CurrentCharges).IsEqual(0);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Dazed);
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void KitPayloadsCarryTheAuthoredShieldBreakerClassButConstructsNever() {
        AbilityData emancipator = AuthoredResources.Load<AbilityData>("res://resources/Abilities/lincoln/special_1.tres");
        AbilityData emc2 = AuthoredResources.Load<AbilityData>("res://resources/Abilities/einstein/special_1.tres");
        AssertThat(emancipator.BlockClass).IsEqual(BlockClass.ShieldBreaker);

        HitPayload breaker = BaseSpecial.WithAbilityContract(
            new HitPayload { AttackClass = AttackClass.Special }, emancipator, null);
        AssertThat(breaker.ShieldBreaker).IsTrue();
        AssertThat(breaker.BlockChargeCost)
            .OverrideFailureMessage("A Shield-Breaker is not a Guard-Crush: Shield of Orléans must not see a 2 here.")
            .IsEqual(0);

        HitPayload ordinary = BaseSpecial.WithAbilityContract(
            new HitPayload { AttackClass = AttackClass.Special }, emc2, null);
        AssertThat(ordinary.ShieldBreaker).IsFalse();

        HitPayload construct = BaseSpecial.WithAbilityContract(
            new HitPayload { AttackClass = AttackClass.Basic }, emancipator, null, HitDelivery.Construct);
        AssertThat(construct.ShieldBreaker)
            .OverrideFailureMessage("Persistent-object hits are Basic-class and never break a shield.")
            .IsFalse();
    }

    private static HitPayload SpecialHit(PlayerController player, bool shieldBreaker) => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.special",
        HitboxID = "primary",
        AttackClass = AttackClass.Special,
        ShieldBreaker = shieldBreaker,
        Damage = 20f,
        Knockback = new Vector2(4f, -2f),
        HitstunDuration = 0.25f,
        // The player faces right by default: origin in front.
        HitOrigin = player.GlobalPosition + new Vector2(40f, 0f),
        AttackerFacingRight = false
    };

    private static PlayerController NewPlayer(out BlockSystem block) {
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        block = player.GetNode<BlockSystem>("BlockSystem");
        return player;
    }
}
