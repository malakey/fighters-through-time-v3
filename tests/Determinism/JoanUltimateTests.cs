using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for Joan's canonical Grand Crusade
/// ultimate: full-meter consumption without a generic double-hit, the six
/// 12-damage trample pulses landing their 72 total over the charge, reach well
/// beyond melee range, shield bypass, and snapshot/rollback convergence.
/// The charge is a wide forward-offset ultimate-slot zone (type 13) plus a
/// forward dash impulse on Joan herself; only the final pulse carries the
/// authored knockback.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class JoanUltimateTests {

    // Charge: three 50-base basics (combo 0.8x/1.0x/1.5x = 40+50+75 damage,
    // 18-frame cooldown) cap the meter at 100 (1 influence per damage dealt).
    // The opponent has 1000 HP and Joan's basics carry zero knockback, so
    // nobody moves or loses a stock.
    private const int MeterChargedTick = 39;
    private const int MeterChargeDamage = 165;
    private const int UltimateDamagePerHit = 12;
    private const int UltimateHitCount = 6;
    private const int UltimateTotal = UltimateDamagePerHit * UltimateHitCount;

    [TestCase]
    public void GrandCrusadeConsumesTheMeterWithoutAGenericDoubleHit() {
        var simulation = BuildSimulation(seed: 71, spawnDistance: 1);
        int tick = ChargeMeter(simulation);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.RawValue)
            .IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(100).RawValue);

        // The press frame spawns the charge zone, dashes Joan forward, and lands
        // exactly the first 12-damage pulse — not 12 plus the generic 20-damage
        // melee ultimate (the dispatch consumed the meter before intent build).
        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual(13);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent joan)).IsTrue();
        AssertThat(joan.Velocity.x > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(1000 - MeterChargeDamage - UltimateDamagePerHit);
        // Meter reset on use: only the first pulse's damage has re-credited it.
        AssertThat(simulation.TryGetFighter(0, out joan)).IsTrue();
        AssertThat(joan.Influence.RawValue)
            .IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(UltimateDamagePerHit).RawValue);
    }

    [TestCase]
    public void GrandCrusadeLandsItsFullMultiHitTotalOverTheCharge() {
        var simulation = BuildSimulation(seed: 72, spawnDistance: 1);
        int tick = ChargeMeter(simulation);
        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        for (int step = 1; step <= 40; step++) {
            simulation.Advance(
                Frame(tick + step, 0, GameplayButtons.None),
                Frame(tick + step, 0, GameplayButtons.None));
        }
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(1000 - MeterChargeDamage - UltimateTotal);
        // The final trample pulse carried real knockback (hitstun), so the
        // opponent was launched toward the blast zone at least once.
        AssertThat(target.IsGrounded == 0 || target.HitstunFrames > 0 || target.Velocity.y != xpTURN.Klotho.Deterministic.Math.FP64.Zero)
            .IsTrue();
    }

    [TestCase]
    public void GrandCrusadeReachesWellBeyondMeleeRange() {
        var simulation = BuildSimulation(seed: 73, spawnDistance: 1);
        int tick = ChargeMeter(simulation);

        // The opponent retreats out of the generic 2-unit melee attack range.
        for (int step = 0; step < 20; step++) {
            simulation.Advance(
                Frame(tick + step, 0, GameplayButtons.None),
                Frame(tick + step, 127, GameplayButtons.None));
        }
        tick += 20;
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent joan)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.Position.x - joan.Position.x > xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2))
            .IsTrue();

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        for (int step = 1; step <= 40; step++) {
            simulation.Advance(
                Frame(tick + step, 0, GameplayButtons.None),
                Frame(tick + step, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        AssertThat(struck.CurrentHP).IsEqual(1000 - MeterChargeDamage - UltimateTotal);
    }

    [TestCase]
    public void GrandCrusadeBypassesBlockEntirely() {
        var simulation = BuildSimulation(seed: 74, spawnDistance: 1);
        int tick = ChargeMeter(simulation);

        // Let the last basic's hitstun lapse, then the opponent holds Block.
        for (int step = 0; step < 15; step++) {
            simulation.Advance(
                Frame(tick + step, 0, GameplayButtons.None),
                Frame(tick + step, 0, GameplayButtons.Block));
        }
        tick += 15;
        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.Block));
        for (int step = 1; step <= 40; step++) {
            simulation.Advance(
                Frame(tick + step, 0, GameplayButtons.None),
                Frame(tick + step, 0, GameplayButtons.Block));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent defender)).IsTrue();
        AssertThat(defender.CurrentHP).IsEqual(1000 - MeterChargeDamage - UltimateTotal);
        AssertThat(defender.BlockCharges).IsEqual(3);
        AssertThat(defender.DazeFrames).IsEqual(0);
    }

    [TestCase]
    public void GrandCrusadeIsSnapshotAndRollbackSafeMidCharge() {
        var uninterrupted = BuildSimulation(seed: 75, spawnDistance: 1);
        var restored = BuildSimulation(seed: 75, spawnDistance: 1);

        int tick = ChargeMeter(uninterrupted);
        uninterrupted.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        // Stop mid-charge with the zone alive and pulses pending.
        for (int step = 1; step <= 10; step++) {
            uninterrupted.Advance(
                Frame(tick + step, 0, GameplayButtons.None),
                Frame(tick + step, 0, GameplayButtons.None));
        }
        AssertThat(uninterrupted.ZoneCount).IsEqual(1);

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        for (int step = 11; step <= 310; step++) {
            long expected = uninterrupted.Advance(
                Frame(tick + step, 0, GameplayButtons.None), Frame(tick + step, -60, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick + step, 0, GameplayButtons.None), Frame(tick + step, -60, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    /// <summary>
    /// Caps Joan's meter at 100 with three zero-knockback basics (40+50+75
    /// damage) and returns the next free tick.
    /// </summary>
    private static int ChargeMeter(FighterSimulation simulation) {
        for (int hit = 0; hit < 3; hit++) {
            int attackTick = hit * 19;
            simulation.Advance(
                Frame(attackTick, 0, GameplayButtons.BasicAttack),
                Frame(attackTick, 0, GameplayButtons.None));
            for (int tick = attackTick + 1; tick < attackTick + 19 && hit < 2; tick++) {
                simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            }
        }
        return MeterChargedTick;
    }

    private static FighterSimulation BuildSimulation(int seed, int spawnDistance) => new(
        FighterLoadoutFactory.FromCharacterData(BuildJoan()),
        FighterLoadoutFactory.FromCharacterData(BuildDurableOpponent()),
        seed: seed,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);

    private static CharacterData BuildJoan() => new() {
        CharacterID = "joan",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 50f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData {
            BaseDamage = 12f,
            IsMultiHit = true,
            HitCount = 6,
            KnockbackForce = new Vector2(8f, -2f)
        }
    };

    private static CharacterData BuildDurableOpponent() => new() {
        CharacterID = "tesla",
        MaxHP = 1000,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
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
