using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the V7.3 block-model closure on the Story side (BlockSystem +
/// PlayerController): the shatter arms a five-second lockout during which the
/// stance cannot rise and charge regeneration is held (charge #1 lands one
/// regen interval after the lockout expires), a Chronal Orb shield restore
/// ends the lockout, an empty shield never raises the stance, and a blocked
/// hit locks the blocker in shieldstun. Numbers come from
/// <see cref="BasicComboRules"/> — the same tables the Fighter sim reads.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryBlockModelTests {

    [TestCase]
    public void GuardBreakArmsTheLockoutHoldsRegenAndTheEmptyShieldRaisesNoStance() {
        PlayerController player = NewPlayer(out BlockSystem block);
        try {
            AssertThat(block.DepleteCharges(block.MaxCharges)).IsEqual(BlockResult.GuardBroken);
            AssertThat(block.CurrentCharges).IsEqual(0);
            AssertThat(block.CanRaiseStance)
                .OverrideFailureMessage("The shatter lockout must refuse the stance.")
                .IsFalse();
            block.StartBlock();
            AssertThat(block.IsBlocking)
                .OverrideFailureMessage("StartBlock must not raise the stance during the lockout.")
                .IsFalse();

            // Regen is held through the whole five-second lockout; charge #1
            // lands one regen interval after it expires (shatter + 480f). The
            // float timers earn a small tolerance band.
            int expected = BasicComboRules.BlockShatterLockoutFrames + BasicComboRules.BlockChargeRegenFrames;
            int frame = 0;
            for (; frame < expected - 15; frame++) block._PhysicsProcess(1.0 / 60.0);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("Regen must be held until the lockout expires (first charge at shatter+480f).")
                .IsEqual(0);
            // 0-charge window after the lockout: still no stance.
            AssertThat(block.CanRaiseStance)
                .OverrideFailureMessage("An empty shield must never raise the stance.")
                .IsFalse();
            for (; frame < expected + 15; frame++) block._PhysicsProcess(1.0 / 60.0);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("The first charge must land one regen interval after the lockout.")
                .IsEqual(1);
            AssertThat(block.CanRaiseStance).IsTrue();
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void AShieldRestoreDoesNotEndTheShatterLockout() {
        // V7.6 F17 (Package 11 A1b), rewritten in place: the V7.3 sentence
        // "a Chronal Shield-Restore orb ends the lockout along with restoring
        // charges (the orb is the authored fast exit)" is DELETED. Temporal
        // Aegis is a separate one-hit shield; the only specified block-recovery
        // rules are normal regeneration and the existing perk exceptions, and a
        // refunded charge stays unusable until the five seconds elapse.
        PlayerController player = NewPlayer(out BlockSystem block);
        try {
            AssertThat(block.DepleteCharges(block.MaxCharges)).IsEqual(BlockResult.GuardBroken);
            AssertThat(block.CanRaiseStance).IsFalse();

            block.RestoreAllCharges();
            AssertThat(block.CurrentCharges).IsEqual(block.MaxCharges);
            AssertThat(block.CanRaiseStance)
                .OverrideFailureMessage("F17: restored charges stay unusable until the lockout ends.")
                .IsFalse();
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void ABlockedHitLocksTheBlockerInShieldstunForTheSharedWindow() {
        PlayerController player = NewPlayer(out BlockSystem block);
        try {
            block.StartBlock();
            AssertThat(block.IsBlocking).IsTrue();
            AssertThat(block.IsInShieldStun).IsFalse();

            BlockResult result = block.ResolveHit(new HitPayload {
                AttackerIndex = 1,
                TargetIndex = 0,
                AttackID = "test.hit",
                HitboxID = "combo_1",
                AttackClass = AttackClass.Basic,
                Damage = 10f,
                Knockback = new Vector2(4f, -2f),
                HitstunDuration = 0.25f,
                // The player faces right by default: origin in front.
                HitOrigin = player.GlobalPosition + new Vector2(40f, 0f),
                AttackerFacingRight = false
            });
            AssertThat(result).IsEqual(BlockResult.Blocked);
            AssertThat(block.IsInShieldStun)
                .OverrideFailureMessage("A non-shatter blocked hit must arm the shieldstun window.")
                .IsTrue();

            // The window is the shared ShieldstunFramesFor(10) (8f — CM-BLOCK
            // 4.1 scales it by the blocked damage), then it releases.
            for (int frame = 0; frame < BasicComboRules.ShieldstunFramesFor(10) - 1; frame++) {
                block._PhysicsProcess(1.0 / 60.0);
                AssertThat(block.IsInShieldStun)
                    .OverrideFailureMessage($"Shieldstun must run its whole window (frame {frame}).")
                    .IsTrue();
            }
            for (int frame = 0; frame < 3; frame++) block._PhysicsProcess(1.0 / 60.0);
            AssertThat(block.IsInShieldStun).IsFalse();
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void AMobHitsShieldstunScalesWithTheDamageTheBlockTookAfterAbsorption() {
        // CM-BLOCK (decisions 4.1 / 11.2): a 20-damage mob strike locks the
        // stance for clamp(6 + 20 / 4, 6, 16) = 11 frames, and the basis is the
        // DELIVERED damage — what is left after a barrier's partial absorption.
        AssertThat(BasicComboRules.ShieldstunFramesFor(20)).IsEqual(11);
        PlayerController player = NewPlayer(out BlockSystem block);
        try {
            player.IsFacingRight = true;
            block.StartBlock();
            player.TransitionTo(CharacterState.Blocking);
            AssertThat(player.IsPerfectBlockWindowOpen).IsFalse();
            Hurtbox hurtbox = player.GetNode<Hurtbox>("Hurtbox");

            hurtbox.TakeHit(MobStrike(player, damage: 20f));
            AssertThat(player.CurrentState).IsEqual(CharacterState.Blocking);
            AssertShieldstunLasts(block, BasicComboRules.ShieldstunFramesFor(20));

            // A Resonance Barrier absorbs its capacity first; the remainder (22,
            // whose 11 frames a rounding of the capacity by one cannot move)
            // reaches the block and earns the shieldstun. The whole hit would
            // have earned the 16-frame cap.
            AssertThat(player.GrantResonanceBarrier(StoryShieldEffect.HenrysBastion, 9301)).IsTrue();
            int capacity = Mathf.RoundToInt(player.StoryShieldPoints);
            AssertThat(capacity).IsGreater(0);
            AssertThat(BasicComboRules.ShieldstunFramesFor(22 + capacity)).IsGreater(BasicComboRules.ShieldstunFramesFor(22));
            hurtbox.TakeHit(MobStrike(player, damage: 22f + capacity));
            AssertThat(player.StoryShieldPoints <= 0.001f)
                .OverrideFailureMessage("Harness: the barrier must absorb its whole capacity.")
                .IsTrue();
            AssertShieldstunLasts(block, BasicComboRules.ShieldstunFramesFor(22));
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void AGrabbingPlayerHasNoFunctioningShield() {
        // V7.3 grab-triangle pin (Story was already correct): only the
        // Blocking state consults BlockSystem.ResolveHit, so a player mid-grab
        // takes full damage — attack beats grab.
        PlayerController player = NewPlayer(out BlockSystem block);
        try {
            block.StartBlock();
            AssertThat(block.IsBlocking).IsTrue();

            player.TransitionTo(CharacterState.Grabbing);
            int chargesBefore = block.CurrentCharges;
            int hpBefore = player.CurrentHP;
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = 1,
                TargetIndex = 0,
                AttackID = "test.hit",
                HitboxID = "primary",
                AttackClass = AttackClass.Basic,
                Damage = 10f,
                Knockback = new Vector2(4f, -2f),
                HitstunDuration = 0.25f,
                HitOrigin = player.GlobalPosition + new Vector2(40f, 0f),
                AttackerFacingRight = false
            });

            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("A grabbing player must take the hit in full — no shield mid-grab.")
                .IsEqual(hpBefore - 10);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("No charge may be spent while grabbing.")
                .IsEqual(chargesBefore);
        } finally {
            player.Free();
        }
    }

    /// <summary>A mob strike in front of a right-facing hero.</summary>
    private static HitPayload MobStrike(PlayerController player, float damage) => new() {
        AttackerIndex = -1,
        TargetIndex = 0,
        AttackID = "test.mob_strike",
        HitboxID = "primary",
        AttackClass = AttackClass.Basic,
        Damage = damage,
        Knockback = Vector2.Zero,
        HitstunDuration = 0.2f,
        HitOrigin = player.GlobalPosition + new Vector2(40f, 0f),
        AttackerFacingRight = false,
        SourceIsStandardMob = true,
        // No freeze: BlockSystem's timers pause in the owner's hitstop, which
        // only the hero's own tick would run down.
        ExemptFromHitstop = true,
        ContactId = HitClassification.NextContactId()
    };

    /// <summary>The stance stays locked for <paramref name="frames"/> block ticks, then releases (float timers: one tick of slack).</summary>
    private static void AssertShieldstunLasts(BlockSystem block, int frames) {
        AssertThat(block.IsInShieldStun).OverrideFailureMessage("The block must arm shieldstun.").IsTrue();
        for (int frame = 0; frame < frames - 1; frame++) {
            block._PhysicsProcess(1.0 / 60.0);
            AssertThat(block.IsInShieldStun)
                .OverrideFailureMessage($"Shieldstun must run its {frames}-frame window (frame {frame}).")
                .IsTrue();
        }
        for (int frame = 0; frame < 2; frame++) block._PhysicsProcess(1.0 / 60.0);
        AssertThat(block.IsInShieldStun)
            .OverrideFailureMessage($"Shieldstun must release after its {frames}-frame window.")
            .IsFalse();
    }

    private static PlayerController NewPlayer(out BlockSystem block) {
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        block = player.GetNode<BlockSystem>("BlockSystem");
        return player;
    }
}
