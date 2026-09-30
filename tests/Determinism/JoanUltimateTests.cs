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
/// ultimate (J05, Package 13 W6): the press spends the meter and starts the
/// banner-charge activation strike (A02) — a melee lunge of about five units —
/// whose contact starts the stampede on the held victim: eight 7-damage trample
/// pulses and then the 20-damage final charge (76), with shield bypass and
/// snapshot/rollback convergence. The stampede is an ultimate-slot zone (type
/// 13) plus a forward dash on Joan herself; the final charge is the D15 finale.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class JoanUltimateTests {

    // Charge: three 50-base basics (combo 0.8x/1.0x/1.5x = 40+50+75 damage)
    // cap the meter at 100 (1 influence per damage dealt). The opponent has
    // 1000 HP and Joan's basics carry zero knockback, so nobody moves or
    // loses a stock.
    private const int MeterChargeDamage = 165;
    private const int UltimateDamagePerHit = 7;
    private const int UltimateHitCount = 8;
    private const int FinaleDamage = 20;
    private const int UltimateTotal = UltimateDamagePerHit * UltimateHitCount + FinaleDamage;

    [TestCase]
    public void BannerChargeLungesAndItsContactStartsTheStampede() {
        var simulation = BuildSimulation(seed: 71, spawnDistance: 1);
        int tick = ChargeMeter(simulation);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.RawValue)
            .IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(100).RawValue);

        // The press spends the meter and spawns nothing yet.
        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        tick++;
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent joan)).IsTrue();
        AssertThat(joan.Influence.RawValue).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.Zero.RawValue);

        bool connected = false;
        for (int i = 0; i < 40 && !connected; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
            connected = UltimateActivationTestKit.IsCinematic(simulation, 0);
        }
        AssertThat(connected).IsTrue();
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual(13);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(1000 - MeterChargeDamage - UltimateDamagePerHit);
        // V7.6 D03h: Ultimate-origin damage re-credits nothing.
        AssertThat(simulation.TryGetFighter(0, out joan)).IsTrue();
        AssertThat(joan.Influence.RawValue).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.Zero.RawValue);
    }

    [TestCase]
    public void GrandCrusadeLandsItsTramplesAndFinalCharge() {
        var simulation = BuildSimulation(seed: 72, spawnDistance: 1);
        int tick = ChargeMeter(simulation);
        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();
        UltimateActivationTestKit.Idle(simulation, tick, 60);
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(1000 - MeterChargeDamage - UltimateTotal);
        // The final charge carried real knockback toward the blast zone.
        AssertThat(target.IsGrounded == 0 || target.HitstunFrames > 0 || target.Velocity.x != xpTURN.Klotho.Deterministic.Math.FP64.Zero)
            .IsTrue();
    }

    [TestCase]
    public void BannerChargeReachesWellBeyondMeleeRange() {
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

        var startX = joan.Position.x;
        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();
        // The lance charge carried Joan forward to reach the distant opponent.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charging)).IsTrue();
        AssertThat(charging.Position.x - startX > xpTURN.Klotho.Deterministic.Math.FP64.One).IsTrue();
        UltimateActivationTestKit.Idle(simulation, tick, 60);
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
        tick = UltimateActivationTestKit.CastAndConnect(
            simulation, tick, out bool connected, victimHeld: GameplayButtons.Block);
        AssertThat(connected).IsTrue();
        UltimateActivationTestKit.Idle(simulation, tick, 60, GameplayButtons.Block);
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
        tick = UltimateActivationTestKit.CastAndConnect(uninterrupted, tick);
        // Stop mid-stampede with the zone alive and pulses pending.
        tick = UltimateActivationTestKit.Idle(uninterrupted, tick, 10);
        AssertThat(uninterrupted.ZoneCount).IsEqual(1);

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        for (int step = 0; step < 300; step++) {
            long expected = uninterrupted.Advance(
                Frame(tick + step, 0, GameplayButtons.None), Frame(tick + step, -60, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick + step, 0, GameplayButtons.None), Frame(tick + step, -60, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    /// <summary>
    /// Caps Joan's meter at 100 with the real chained three-hit string
    /// (40+50+75 damage) and returns the next free tick, settled to neutral.
    /// </summary>
    private static int ChargeMeter(FighterSimulation simulation) =>
        BasicStringTestDriver.LandChainedBasics(simulation, 0, 3);

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
            BaseDamage = 7f,
            IsMultiHit = true,
            HitCount = 8,
            DamageTickIntervalFrames = 6,
            FinaleDamage = 20f,
            FinaleLaunches = true,
            ActivationShape = UltimateActivationShape.Melee,
            ActivationRange = 300f,
            ActivationHitboxSize = new Vector2(90f, 70f),
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
