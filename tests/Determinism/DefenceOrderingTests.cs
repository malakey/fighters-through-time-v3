using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 11 A1b — the V7.6 D01/D02b defence ORDER at the deterministic
/// chokepoint, plus the D03a/D03b/D03d/D03e coverage rules that ride it.
///
/// <para>Order per distinct eligible hit contact: hit eligibility /
/// invulnerability → projectile immunity → Temporal Aegis → HP barriers →
/// ordinary block, stopping at the first layer that fully prevents the hit.
/// Full absorption spends no block charge, causes no block response and awards
/// no successful-block perk; partial absorption passes only the remainder to a
/// legal block, at the ORIGINAL attack classification.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DefenceOrderingTests {

    [TestCase]
    public void AegisResolvesBeforeTheBarrierAndLeavesItsCapacityIntact() {
        // DEFENSIVE_EFFECTS D02b's worked example: "with a 10-HP barrier and
        // active Aegis, an eligible 8-damage hit consumes Aegis and leaves the
        // barrier at 10". The barrier must not be subtracted first even though
        // it could absorb the whole hit.
        Fixture f = Fixture.New();
        f.Defense.AegisActive = 1;
        f.Defense.BarrierPoints = 10;
        f.Defense.BarrierCapacity = 10;
        f.Defense.BarrierRemainingFrames = FighterDefenseRules.GrantedShieldLifetimeTicks;

        bool landed = f.Hit(FighterDamageRules.BasicAttackClass, damage: 8);

        AssertThat(landed).IsFalse();
        AssertThat(f.Defense.AegisActive)
            .OverrideFailureMessage("Aegis is one hit: it must be spent by this contact.")
            .IsEqual(0);
        AssertThat(f.Defense.BarrierPoints)
            .OverrideFailureMessage("Aegis fully prevented the hit, so the barrier must be untouched.")
            .IsEqual(10);
        AssertThat(f.Target.CurrentHP).IsEqual(100);
    }

    [TestCase]
    public void AFullBarrierAbsorptionSpendsNoBlockChargeAndAppliesNoEffects() {
        // D01 + D03a: a hit the guard absorbs in full never reaches block. The
        // blocker holds Block throughout — "holding Block does not turn a shield
        // absorption into a successful block" — and takes no hitstun, no
        // knockback and no status.
        Fixture f = Fixture.New();
        f.Runtime.HeldButtons = (int)GameplayButtons.Block;
        f.Defense.BarrierPoints = 30;
        f.Defense.BarrierCapacity = 30;

        bool landed = f.Hit(
            FighterDamageRules.BasicAttackClass,
            damage: 20,
            knockback: FP64.FromInt(4),
            hitstunFrames: 20,
            statusType: (int)StatusType.Venom,
            statusFrames: 120);

        AssertThat(landed).IsFalse();
        AssertThat(f.Defense.BarrierPoints).IsEqual(10);
        AssertThat(f.Target.BlockCharges)
            .OverrideFailureMessage("A fully absorbed hit must spend no block charge.")
            .IsEqual(3);
        AssertThat(f.Verb.ShieldStunFrames)
            .OverrideFailureMessage("A shield absorption is not a block response: no shieldstun.")
            .IsEqual(0);
        AssertThat(f.Target.HitstunFrames).IsEqual(0);
        AssertThat(f.Target.CurrentHP).IsEqual(100);
        AssertThat(f.Runtime.DamageStatusType)
            .OverrideFailureMessage("D03a: a fully absorbed hit applies no attached status.")
            .IsEqual((int)StatusType.None);
    }

    [TestCase]
    public void AnExactlyDepletingAbsorptionStillPreventsTheHitsEffects() {
        // D03a is explicit: "a hit exactly exhausting the remaining shield is
        // still fully absorbed — remove the depleted shield, but do not apply
        // that hit's effects to the unprotected actor afterward."
        Fixture f = Fixture.New();
        f.Defense.BarrierPoints = 20;
        f.Defense.BarrierCapacity = 20;
        f.Defense.BarrierRemainingFrames = 120;

        bool landed = f.Hit(
            FighterDamageRules.BasicAttackClass, damage: 20,
            knockback: FP64.FromInt(4), hitstunFrames: 20);

        AssertThat(landed).IsFalse();
        AssertThat(f.Target.CurrentHP).IsEqual(100);
        AssertThat(f.Target.HitstunFrames).IsEqual(0);
        AssertThat(f.Defense.BarrierPoints).IsEqual(0);
        AssertThat(f.Defense.BarrierEffectID)
            .OverrideFailureMessage("A broken shield disappears immediately.")
            .IsEqual(0);
        AssertThat(f.Defense.BarrierRemainingFrames)
            .OverrideFailureMessage("The depleted instance takes its lifetime with it.")
            .IsEqual(0);
    }

    [TestCase]
    public void APartialAbsorptionPassesOnlyTheRemainderAtTheOriginalClass() {
        // D01's worked example: "a 30-damage Basic hit meets a barrier with 10
        // absorption remaining. Consume the 10, then a legal block absorbs the
        // remaining 20 for ONE charge." The class is unchanged by the barrier.
        Fixture f = Fixture.New();
        f.Runtime.HeldButtons = (int)GameplayButtons.Block;
        f.Defense.BarrierPoints = 10;
        f.Defense.BarrierCapacity = 30;

        bool landed = f.Hit(
            FighterDamageRules.BasicAttackClass, damage: 30,
            knockback: FP64.FromInt(4), hitstunFrames: 20);

        AssertThat(landed)
            .OverrideFailureMessage("The remainder was blocked, so the hit did not land.")
            .IsFalse();
        AssertThat(f.Defense.BarrierPoints).IsEqual(0);
        AssertThat(f.Target.BlockCharges)
            .OverrideFailureMessage("A Basic remainder costs exactly one charge.")
            .IsEqual(2);
        AssertThat(f.Verb.ShieldStunFrames)
            .OverrideFailureMessage("A real blocked remainder DOES produce the ordinary shieldstun.")
            .IsEqual(BasicComboRules.ShieldstunFrames);
        AssertThat(f.Target.CurrentHP).IsEqual(100);
    }

    [TestCase]
    public void APartialAbsorptionOfASpecialStillFullShattersTheRemainder() {
        // D01: "the same remainder from a Special still causes the existing full
        // shatter" — the barrier does not downgrade the attack's classification.
        Fixture f = Fixture.New();
        f.Runtime.HeldButtons = (int)GameplayButtons.Block;
        f.Defense.BarrierPoints = 10;
        f.Defense.BarrierCapacity = 30;

        f.Hit(FighterDamageRules.SpecialAttackClass, damage: 30,
              knockback: FP64.FromInt(4), hitstunFrames: 20);

        AssertThat(f.Target.BlockCharges)
            .OverrideFailureMessage("A Special remainder still takes every charge.")
            .IsEqual(0);
        AssertThat(f.Target.DazeFrames)
            .OverrideFailureMessage("The shatter still dazes.")
            .IsEqual(60);
        AssertThat(f.Verb.BlockLockoutFrames).IsEqual(BasicComboRules.BlockShatterLockoutFrames);
    }

    [TestCase]
    public void AnUnblockableIsAbsorbedByTheBarrierWithoutTouchingBlock() {
        // D03b: Unblockable bypasses the ordinary charge-based block ONLY.
        // Eligible barriers still absorb, only the remainder reaches HP, and no
        // charge, shieldstun or shatter is produced either way.
        Fixture f = Fixture.New();
        f.Runtime.HeldButtons = (int)GameplayButtons.Block;
        f.Defense.BarrierPoints = 10;
        f.Defense.BarrierCapacity = 10;

        bool landed = f.Hit(FighterDamageRules.UltimateAttackClass, damage: 20,
                            knockback: FP64.FromInt(4), hitstunFrames: 20);

        AssertThat(landed).IsTrue();
        AssertThat(f.Defense.BarrierPoints).IsEqual(0);
        AssertThat(f.Target.CurrentHP)
            .OverrideFailureMessage("Only the 10-damage remainder may reach HP.")
            .IsEqual(90);
        AssertThat(f.Target.BlockCharges)
            .OverrideFailureMessage("An unblockable spends no block charge, absorbed or not.")
            .IsEqual(3);
        AssertThat(f.Verb.ShieldStunFrames).IsEqual(0);
    }

    [TestCase]
    public void AZeroDamagePulseConsumesNeitherAegisNorBarrier() {
        // D03c's boundary case: "zero-damage warnings, non-damaging statuses and
        // harmless overlaps consume no Aegis or barrier HP." The shipped Aegis
        // branch fired for ANY hit, so a 0-damage expiry launch ate the bubble.
        Fixture f = Fixture.New();
        f.Defense.AegisActive = 1;
        f.Defense.BarrierPoints = 10;
        f.Defense.BarrierCapacity = 10;

        f.Hit(FighterDamageRules.SpecialAttackClass, damage: 0,
              knockback: FP64.FromInt(4), hitstunFrames: 12);

        AssertThat(f.Defense.AegisActive)
            .OverrideFailureMessage("A zero-damage pulse must not eat the Aegis bubble.")
            .IsEqual(1);
        AssertThat(f.Defense.BarrierPoints).IsEqual(10);
    }

    [TestCase]
    public void APrimaryThrowBypassesTheFiniteShieldsWithoutConsumingThem() {
        // D03d: a legal primary throw deals normal damage and applies its normal
        // launch WITHOUT consuming Aegis or barrier capacity. Before this an
        // active Aegis absorbed the throw entirely — exactly the case the
        // contract forbids.
        Fixture f = Fixture.New();
        f.Defense.AegisActive = 1;
        f.Defense.BarrierPoints = 20;
        f.Defense.BarrierCapacity = 20;

        bool landed = f.Hit(FighterDamageRules.BasicAttackClass, damage: 15,
                            knockback: FP64.FromInt(4), hitstunFrames: 20,
                            bypassesFiniteShields: true);

        AssertThat(landed).IsTrue();
        AssertThat(f.Target.CurrentHP)
            .OverrideFailureMessage("The throw deals its full damage through the bypass.")
            .IsEqual(85);
        AssertThat(f.Defense.AegisActive)
            .OverrideFailureMessage("The unspent bubble stays available for a later eligible hit.")
            .IsEqual(1);
        AssertThat(f.Defense.BarrierPoints)
            .OverrideFailureMessage("The surviving victim retains its barrier capacity in full.")
            .IsEqual(20);
    }

    /// <summary>The four ref components every sim-side hit needs, in one place.</summary>
    private struct Fixture {
        public FighterStateComponent Attacker;
        public FighterRuntimeComponent AttackerRuntime;
        public FighterVerbComponent AttackerVerb;
        public FighterStateComponent Target;
        public FighterRuntimeComponent Runtime;
        public FighterVerbComponent Verb;
        public FighterDefenseComponent Defense;
        public FighterTuningComponent Tuning;

        public static Fixture New() => new() {
            Attacker = NewFighter(0),
            Target = NewFighter(1),
            Tuning = new FighterTuningComponent { MaxBlockCharges = 3, MaxJumpCount = 2 }
        };

        public bool Hit(
            int attackClass,
            int damage,
            FP64 knockback = default,
            int hitstunFrames = 0,
            int statusType = (int)StatusType.None,
            int statusFrames = 0,
            bool bypassesFiniteShields = false) {
            // The target faces RIGHT, so "in front" is +x: the attacker stands
            // to the target's right and a held stance can legally absorb.
            Attacker.Position = new FPVector2(Target.Position.x + FP64.One, Target.Position.y);
            return FighterDamageRules.ApplyFighterHit(
                ref Attacker, ref AttackerRuntime, ref AttackerVerb,
                ref Target, ref Runtime, ref Verb, ref Defense, in Tuning,
                attackClass, damage, knockback, hitstunFrames,
                statusType, statusFrames, FP64.One,
                Attacker.Position.x,
                bypassesFiniteShields: bypassesFiniteShields);
        }
    }

    private static FighterStateComponent NewFighter(int playerID) => new() {
        PlayerID = playerID,
        Stocks = 3,
        MaxHP = 100,
        CurrentHP = 100,
        BlockCharges = 3,
        IsGrounded = 1,
        FacingRight = 1,
        Weight = FP64.One
    };
}
