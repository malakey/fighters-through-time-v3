using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W1 — A11 (Story only). Henry's Bastion, Royal Aegis and Leaf
/// Barrier are grant SOURCES of one shared <see cref="StoryShieldEffect.ResonanceBarrier"/>:
/// 10 % max HP, the 480-tick D02c lifetime, refill-not-stack on a valid grant
/// from any source, one instance per recipient, the source recorded for
/// presentation. Wardenclyffe stays a separate effect.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceBarrierTests {

    [TestCase]
    public void EverySourceGrantsTheOneSharedBarrierAtTenPercentForEightSeconds() {
        PlayerController player = NewPlayer();
        try {
            foreach (StoryShieldEffect source in new[] {
                         StoryShieldEffect.HenrysBastion, StoryShieldEffect.RoyalAegis, StoryShieldEffect.LeafBarrier }) {
                AssertThat(player.GrantResonanceBarrier(source, 100 + (int)source)).IsTrue();
                AssertThat(player.StoryShieldEffectId).IsEqual(StoryShieldEffect.ResonanceBarrier);
                AssertThat(player.StoryShieldSource).IsEqual(source);
                AssertThat(player.StoryShieldCapacity)
                    .IsEqual(StoryDefenseRules.GrantedShieldCapacityShare * player.MaximumHP);
                AssertThat(player.StoryShieldRemainingFrames).IsEqual(StoryDefenseRules.GrantedShieldLifetimeFrames);
            }
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void AGrantFromAnotherSourceRefillsTheSameInstanceWithoutStacking() {
        PlayerController player = NewPlayer();
        try {
            // A legacy-style call with a source effect is routed to the shared status.
            AssertThat(player.GrantStoryShield(StoryShieldEffect.HenrysBastion, 10f, 480, 1)).IsTrue();
            AssertThat(player.StoryShieldEffectId).IsEqual(StoryShieldEffect.ResonanceBarrier);
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = 1, TargetIndex = 0, AttackID = "test.hit", HitboxID = "primary",
                AttackClass = AttackClass.Basic, Damage = 6f,
                HitOrigin = player.GlobalPosition + new Vector2(40f, 0f)
            });
            AssertThat(player.StoryShieldPoints).IsEqual(4f);

            AssertThat(player.GrantStoryShield(StoryShieldEffect.RoyalAegis, 10f, 480, 1))
                .OverrideFailureMessage("Another source's grant with a coincident event ID is a genuine grant.")
                .IsTrue();
            AssertThat(player.StoryShieldPoints)
                .OverrideFailureMessage("Refill, never stack: back to the 10-point cap, not 14.")
                .IsEqual(10f);
            AssertThat(player.StoryShieldSource).IsEqual(StoryShieldEffect.RoyalAegis);
            AssertThat(player.GrantStoryShield(StoryShieldEffect.RoyalAegis, 10f, 480, 1))
                .OverrideFailureMessage("A duplicate callback of the same source and grant is refused.")
                .IsFalse();
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void WardenclyffeIsNotAResonanceBarrierSource() {
        PlayerController player = NewPlayer();
        try {
            AssertThat(StoryDefenseRules.IsResonanceBarrierSource(StoryShieldEffect.Wardenclyffe)).IsFalse();
            AssertThat(player.GrantResonanceBarrier(StoryShieldEffect.Wardenclyffe, 7)).IsFalse();
            AssertThat(player.StoryShieldEffectId).IsEqual(StoryShieldEffect.None);
            // The appended member keeps every earlier ordinal stable.
            AssertThat((int)StoryShieldEffect.Wardenclyffe).IsEqual(4);
            AssertThat((int)StoryShieldEffect.ResonanceBarrier).IsEqual(5);
        } finally {
            player.Free();
        }
    }

    private static PlayerController NewPlayer() {
        PlayerController player = CharacterFactory.CreateCharacter("shakespeare");
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        return player;
    }
}
