using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for Shakespeare's canonical ultimate,
/// All the World's a Stage (S04, Package 13 W6): the press spends the meter and
/// sends the quill's six-unit ink stroke (A02); its contact raises the Globe on
/// the held victim — three 14-damage phantom strikes (the Witches, Romeo &amp;
/// Juliet, the chorus) at the authored 20-frame cadence, then Hamlet's
/// 34-damage finale (76), the only hit that carries the authored knockback.
/// The strokes reach beyond generic melee range, bypass block, and the whole
/// lifecycle is snapshot/rollback safe.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShakespeareUltimateTests {

    /// <summary>
    /// These tests churn large deterministic-simulation allocations alongside
    /// throwaway Godot resource instances. Draining pending finalizers here
    /// keeps them from racing a later suite's ResourceLoader work, which can
    /// trip the engine's script-instance dispose FATAL mid-load.
    /// </summary>
    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    private const int StageZoneTypeID = (int)FighterCharacterID.Shakespeare * 10 + 3;
    private const int StrikeDamage = 14;
    private const int StrikeCount = 3;
    private const int StrikeIntervalFrames = 20;
    private const int FinaleDamage = 34;
    private const int UltimateTotal = StrikeCount * StrikeDamage + FinaleDamage;
    private const int SequenceFrames = 90;

    [TestCase]
    public void TheInkStrokeContactRaisesTheStage() {
        var simulation = BuildSimulation(seed: 71);
        int tick = FillMeterWithBasicStrikes(simulation);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.RawValue).IsEqual(
            xpTURN.Klotho.Deterministic.Math.FP64.FromInt(100).RawValue);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();

        // D03h: spent on acceptance, no re-credit.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent spent)).IsTrue();
        AssertThat(spent.Influence.RawValue).IsEqual(
            xpTURN.Klotho.Deterministic.Math.FP64.Zero.RawValue);
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual(StageZoneTypeID);
        AssertThat(zone.Damage).IsEqual(StrikeDamage);

        // Exactly one impulse-free phantom strike landed on the contact tick.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(before.CurrentHP - StrikeDamage);
        AssertThat(target.HitstunFrames).IsEqual(0);
    }

    [TestCase]
    public void PhantomStrikesLandSequentiallyThenTheFinale() {
        var simulation = BuildSimulation(seed: 72);
        int tick = FillMeterWithBasicStrikes(simulation);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterFirst)).IsTrue();
        AssertThat(afterFirst.CurrentHP).IsEqual(before.CurrentHP - StrikeDamage);

        // Ten frames later the second strike has not yet landed: the phantoms
        // strike sequentially at the authored 20-frame cadence, not as one lump.
        tick = UltimateActivationTestKit.Idle(simulation, tick, 10);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent midGap)).IsTrue();
        AssertThat(midGap.CurrentHP).IsEqual(before.CurrentHP - StrikeDamage);

        tick = UltimateActivationTestKit.Idle(simulation, tick, 10);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterSecond)).IsTrue();
        AssertThat(afterSecond.CurrentHP).IsEqual(before.CurrentHP - 2 * StrikeDamage);

        // The full sequence totals three strikes and Hamlet's finale, then the
        // Globe set leaves.
        UltimateActivationTestKit.Idle(simulation, tick, SequenceFrames);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent finished)).IsTrue();
        AssertThat(finished.CurrentHP).IsEqual(before.CurrentHP - UltimateTotal);
        AssertThat(simulation.ZoneCount).IsEqual(0);
    }

    [TestCase]
    public void FinaleStrikeCarriesTheAuthoredUltimateImpulse() {
        var simulation = BuildSimulation(seed: 73);
        int tick = FillMeterWithBasicStrikes(simulation);
        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick);

        // Strikes 1-3 (contact tick, +20, +40) are impulse-free ultimate-class
        // ticks: no hitstun right up to the finale.
        for (int step = 1; step < StrikeCount * StrikeIntervalFrames; step++, tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent midway)).IsTrue();
            AssertThat(midway.HitstunFrames).IsEqual(0);
        }

        // +60 is Hamlet's finale: authored ultimate knockback (5) and the shared
        // 30-frame finale hitstun, launching the target along the caster's facing.
        simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent launched)).IsTrue();
        AssertThat(launched.HitstunFrames).IsEqual(UltimateActivationRules.FinaleHitstunFrames);
        AssertThat(launched.Velocity.x > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();
        AssertThat(launched.Velocity.y > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();
        AssertThat(launched.IsGrounded).IsEqual(0);
    }

    [TestCase]
    public void TheInkStrokeReachesBeyondGenericMeleeRange() {
        var simulation = BuildSimulation(seed: 74);
        int tick = FillMeterWithBasicStrikes(simulation);

        // The target retreats until the gap exceeds the generic 2-unit reach
        // while staying inside the stroke's six units.
        for (int step = 0; step < 24; step++, tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 127, GameplayButtons.None));
        }
        tick = UltimateActivationTestKit.Idle(simulation, tick, 10);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent distant)).IsTrue();
        xpTURN.Klotho.Deterministic.Math.FP64 gap =
            xpTURN.Klotho.Deterministic.Math.FP64.Abs(distant.Position.x - caster.Position.x);
        AssertThat(gap > xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2)).IsTrue();
        AssertThat(gap < xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(6.5)).IsTrue();

        int hpBefore = distant.CurrentHP;
        UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        AssertThat(struck.CurrentHP).IsEqual(hpBefore - StrikeDamage);
    }

    [TestCase]
    public void PhantomStrikesBypassBlockWithoutSpendingCharges() {
        var simulation = BuildSimulation(seed: 75);
        int tick = FillMeterWithBasicStrikes(simulation);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        AssertThat(before.BlockCharges).IsEqual(3);

        // The target holds Block through the entire sequence: the stroke is
        // unblockable, the strikes ignore the shield (no charge drain, full
        // damage) and the finale's impulse still lands.
        tick = UltimateActivationTestKit.CastAndConnect(
            simulation, tick, out bool connected, victimHeld: GameplayButtons.Block);
        AssertThat(connected).IsTrue();
        bool finaleLaunched = false;
        for (int step = 0; step < SequenceFrames; step++, tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.Block));
            if (simulation.TryGetFighter(1, out FighterStateComponent blocking) && blocking.HitstunFrames > 0) {
                finaleLaunched = true;
            }
        }

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.CurrentHP).IsEqual(before.CurrentHP - UltimateTotal);
        AssertThat(after.BlockCharges).IsEqual(3);
        AssertThat(finaleLaunched).IsTrue();
    }

    [TestCase]
    public void UltimateLifecycleIsSnapshotAndRollbackSafe() {
        var uninterrupted = BuildSimulation(seed: 76);
        var restored = BuildSimulation(seed: 76);

        int tick = FillMeterWithBasicStrikes(uninterrupted);
        tick = UltimateActivationTestKit.CastAndConnect(uninterrupted, tick);
        // Mid-sequence snapshot: two phantom strikes down, one strike and the
        // finale still pending inside the live zone and hold.
        tick = UltimateActivationTestKit.Idle(uninterrupted, tick, 25);
        AssertThat(uninterrupted.ZoneCount).IsEqual(1);

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        for (int step = 0; step < 300; step++, tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    private static FighterSimulation BuildSimulation(int seed) => new(
        FighterLoadoutFactory.FromCharacterData(BuildStageCharacter()),
        FighterLoadoutFactory.FromCharacterData(BuildDurableTarget()),
        seed: seed,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    /// <summary>
    /// Charges the caster's meter to exactly 100 through three zero-knockback
    /// basic strikes (influence gains 1 per HP dealt and caps at 100; each hit
    /// deals at least 40 against the durable target).
    /// </summary>
    private static int FillMeterWithBasicStrikes(FighterSimulation simulation) =>
        BasicStringTestDriver.LandChainedBasics(simulation, 0, 3);

    private static CharacterData BuildStageCharacter() => new() {
        CharacterID = "shakespeare",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        // High-damage, zero-knockback basics keep the meter fill fast without
        // moving the sparring target off its spawn.
        BasicAttackDamage = 50f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData {
            ExecutionType = AbilityExecutionType.Cinematic,
            BaseDamage = StrikeDamage,
            IsMultiHit = true,
            HitCount = StrikeCount,
            DamageTickIntervalFrames = StrikeIntervalFrames,
            FinaleDamage = FinaleDamage,
            FinaleLaunches = true,
            KnockbackForce = new Vector2(5f, -3f),
            Lifetime = 1.3333334f
        }
    };

    private static CharacterData BuildDurableTarget() => new() {
        CharacterID = "joan",
        MaxHP = 400,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 14f }
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };
}
