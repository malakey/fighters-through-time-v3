using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for Cleopatra's canonical Wrath of the
/// Nile ultimate (audit gap X7): the full-meter dispatch replaces the generic
/// melee ultimate with an arena-engulfing sandstorm zone (zone type 43) that
/// deals 10 impulse-free ticks of the authored per-hit damage over 3.5 s,
/// carries the authored heavy Venom, bypasses block, reaches beyond melee
/// range, and stays snapshot/rollback safe.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CleopatraUltimateTests {

    /// <summary>
    /// The ultimate tests churn large deterministic-simulation allocations
    /// alongside throwaway Godot resource instances. Draining pending
    /// finalizers here keeps them from racing a later suite's ResourceLoader
    /// work, which can trip the engine's script-instance dispose FATAL
    /// mid-load.
    /// </summary>
    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    private const int StormZoneTypeID = (int)FighterCharacterID.Cleopatra * 10 + 3;

    [TestCase]
    public void UltimateConsumesFullMeterAndSpawnsStormWithoutGenericDoubleHit() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 71, out int tick);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent beforeTarget)).IsTrue();
        int hpBeforeUltimate = beforeTarget.CurrentHP;

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        tick++;

        // The meter is consumed by the dispatch; the storm's first tick (the
        // V7.6 D03h (Package 11 A1b): Ultimate-origin damage awards its
        // caster ZERO damage-dealt meter, so the same-frame tick no longer
        // re-credits anything - the meter reads exactly 0 after the cast.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence.RawValue).IsEqual(
            xpTURN.Klotho.Deterministic.Math.FP64.Zero.RawValue);
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual(StormZoneTypeID);
        AssertThat(zone.Damage).IsEqual(8);
        AssertThat(zone.StatusType).IsEqual((int)StatusType.Venom);
        AssertThat(zone.StatusFrames).IsEqual(300);

        // No generic melee ultimate double-hit: the zone system runs after the
        // combat system, so the storm's first 8-damage tick lands on the press
        // frame — but only that tick. A generic double-hit would add another 8
        // damage plus a 30-frame impulse hitstun; the storm's ticks are
        // impulse-free, so hitstun must stay zero.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent pressFrame)).IsTrue();
        AssertThat(pressFrame.HitstunFrames).IsEqual(0);
        AssertThat(hpBeforeUltimate - pressFrame.CurrentHP).IsEqual(8);
    }

    [TestCase]
    public void StormDealsTenAuthoredTicksAndHeavyVenomSticksAndTicksAfterward() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 72, out int tick);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hpBefore = before.CurrentHP;

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        int ultimateTick = tick;
        tick++;

        // Run through the full 210-frame storm plus a few frames. Every zone
        // tick re-applies Venom (resetting its 60-frame DoT timer), so no venom
        // chip lands during the storm itself: the HP loss at storm end is
        // exactly the 10 authored ticks of 8.
        while (tick <= ultimateTick + 215) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent stormEnd)).IsTrue();
        AssertThat(hpBefore - stormEnd.CurrentHP).IsEqual(80);

        // The heavy Venom survives the storm at the authored 5 s / 1.5
        // intensity and keeps ticking (3 HP per second) afterward.
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
        // Venom occupies the V7 damage status slot; the intensity round-trips
        // through the slot's deterministic thousandths quantization.
        AssertThat(runtime.DamageStatusType).IsEqual((int)StatusType.Venom);
        AssertThat(runtime.DamageStatusIntensity.RawValue).IsEqual(
            (xpTURN.Klotho.Deterministic.Math.FP64.FromInt(1500)
                / xpTURN.Klotho.Deterministic.Math.FP64.FromInt(1000)).RawValue);
        for (int i = 0; i < 70; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent poisoned)).IsTrue();
        AssertThat(poisoned.CurrentHP < stormEnd.CurrentHP).IsTrue();
    }

    [TestCase]
    public void StormReachesOpponentBeyondMeleeRange() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 73, out int tick);

        // Warp Cleopatra away so the opponent sits far outside the 2-unit
        // generic melee range before the ultimate fires.
        simulation.Advance(Frame(tick, -127, GameplayButtons.MovementAbility), Frame(tick, 0, GameplayButtons.None));
        tick++;
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        var separation = xpTURN.Klotho.Deterministic.Math.FP64.Abs(target.Position.x - caster.Position.x);
        AssertThat(separation > xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2)).IsTrue();
        int hpBefore = target.CurrentHP;

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        tick++;
        AssertThat(simulation.ZoneCount).IsEqual(1);
        for (int i = 0; i < 60; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent hit)).IsTrue();
        AssertThat(hit.CurrentHP < hpBefore).IsTrue();
    }

    [TestCase]
    public void StormBypassesBlockWithoutSpendingBlockCharges() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 74, out int tick);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hpBefore = before.CurrentHP;

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.Block));
        tick++;
        for (int i = 0; i < 220; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.Block));
            tick++;
        }

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent blocked)).IsTrue();
        AssertThat(hpBefore - blocked.CurrentHP >= 80).IsTrue();
        AssertThat(blocked.BlockCharges).IsEqual(3);
    }

    [TestCase]
    public void StormLifecycleIsSnapshotAndRollbackSafe() {
        FighterLoadout cleopatra = FighterLoadoutFactory.FromCharacterData(BuildUltimateCharacter());
        FighterLoadout opponent = FighterLoadoutFactory.FromCharacterData(BuildTankOpponent());
        var uninterrupted = new FighterSimulation(
            cleopatra, opponent, seed: 75, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            cleopatra, opponent, seed: 75, spawnDistance: 1, rules: FighterMatchRules.Disabled);

        // Drive both simulations with the identical input script so each
        // reaches the charged state deterministically.
        int tick = ChargeMeter(uninterrupted, startTick: 0);
        ChargeMeter(restored, startTick: 0);

        uninterrupted.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        restored.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        tick++;

        // Snapshot mid-storm and restore into the second simulation.
        for (int i = 0; i < 40; i++) {
            uninterrupted.Advance(Frame(tick + i, 0, GameplayButtons.None), Frame(tick + i, -60, GameplayButtons.None));
        }
        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // Cover the remaining storm ticks, zone expiry, the surviving Venom
        // DoT, and its expiry.
        for (int i = 40; i < 560; i++) {
            long expected = uninterrupted.Advance(
                Frame(tick + i, 0, GameplayButtons.None), Frame(tick + i, -60, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick + i, 0, GameplayButtons.None), Frame(tick + i, -60, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    /// <summary>
    /// Builds a simulation with Cleopatra at melee range of a high-HP opponent
    /// and drives three zero-knockback basics (40 + 50 + 75 damage) to fill the
    /// Influence meter to 100 deterministically.
    /// </summary>
    private static FighterSimulation BuildChargedSimulation(int seed, out int nextTick) {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildUltimateCharacter()),
            FighterLoadoutFactory.FromCharacterData(BuildTankOpponent()),
            seed: seed,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);
        nextTick = ChargeMeter(simulation, startTick: 0);
        return simulation;
    }

    private static int ChargeMeter(FighterSimulation simulation, int startTick) {
        int tick = BasicStringTestDriver.LandChainedBasics(simulation, startTick, 3);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.RawValue).IsEqual(
            xpTURN.Klotho.Deterministic.Math.FP64.FromInt(100).RawValue);
        return tick;
    }

    /// <summary>
    /// Cleopatra with the canonical Wrath of the Nile numbers from
    /// cleopatra/ultimate.tres (per-hit 8, 10 hits, 21-frame ticks, 3.5 s,
    /// heavy Venom 5 s at 1.5 intensity), zero-knockback basics so the meter
    /// charge never pushes the opponent out of range, and a long teleport for
    /// the beyond-melee-range case.
    /// </summary>
    private static CharacterData BuildUltimateCharacter() => new() {
        CharacterID = "cleopatra",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 50f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData {
            MovementType = MovementType.Teleport,
            MovementDuration = 0.3f,
            DistanceMoved = 360f,
            MovementSpeed = 600f,
            CooldownDuration = 5f
        },
        UltimateAttack = new AbilityData {
            ExecutionType = AbilityExecutionType.Cinematic,
            BaseDamage = 8f,
            IsMultiHit = true,
            HitCount = 10,
            DamageTickIntervalFrames = 21,
            KnockbackForce = new Vector2(4f, -3f),
            Lifetime = 3.5f,
            AppliedStatus = StatusType.Venom,
            StatusDuration = 5f,
            StatusIntensity = 1.5f
        }
    };

    private static CharacterData BuildTankOpponent() => new() {
        CharacterID = "joan",
        MaxHP = 1000,
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
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
