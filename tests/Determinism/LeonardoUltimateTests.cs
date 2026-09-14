using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for Leonardo's canonical Vitruvian
/// Matrix ultimate (audit gap X7): a full meter spawns the ultimate-slot trap
/// zone (type 23) instead of the generic melee ultimate, its 8 bombardment
/// ticks carry the authored per-hit damage plus the refreshing Root hold, the
/// hits bypass block via the ultimate attack class, the zone reaches well
/// beyond melee range, and the whole lifecycle (including the expiry
/// explosion) is snapshot/rollback safe.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LeonardoUltimateTests {

    /// <summary>
    /// The ultimate tests churn large deterministic-simulation allocations
    /// alongside throwaway Godot resource instances. Draining pending
    /// finalizers here keeps them from racing a later suite's ResourceLoader
    /// work, which can trip the engine's script-instance dispose FATAL mid-load.
    /// </summary>
    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [TestCase]
    public void UltimateConsumesTheMeterAndReplacesTheGenericMeleeHit() {
        var simulation = CreateSimulation(spawnDistance: 1, seed: 71);

        // One landed basic (125 x 0.8 = 100) fills the meter exactly; the
        // driver walks the real phased swing and settles back to idle.
        int tick = BasicStringTestDriver.LandChainedBasics(simulation, 0, 1);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.ToFloat()).IsEqual(100f);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterCharge)).IsTrue();
        AssertThat(afterCharge.CurrentHP).IsEqual(300);

        // The ultimate press dispatches the bespoke trap zone, consumes the
        // meter, and the generic melee ultimate cannot double-fire — the cast
        // frame's damage is exactly one 10-damage bombardment tick.
        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual((int)FighterCharacterID.Leonardo * 10 + 3);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        // The cast zeroes the meter; the only influence left is the standard
        // 1-per-HP credit from the first 10-damage bombardment tick.
        // V7.6 D03h (Package 11 A1b): Ultimate-origin damage awards its
        // caster ZERO damage-dealt meter, so the same-frame tick no longer
        // re-credits anything - the meter reads exactly 0 after the cast.
        AssertThat(caster.Influence.ToFloat()).IsEqual(0f);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterCast)).IsTrue();
        AssertThat(afterCast.CurrentHP).IsEqual(290);
    }

    [TestCase]
    public void TrapRootHoldsTheTargetInsideTheMatrix() {
        var simulation = CreateSimulation(spawnDistance: 1, seed: 72);
        int tick = BasicStringTestDriver.LandChainedBasics(simulation, 0, 1);
        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));

        // The first bombardment tick applies the authored Root hold.
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent trapped)).IsTrue();
        AssertThat(trapped.StatusType).IsEqual((int)StatusType.Root);
        AssertThat(trapped.StatusFrames > 0).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent beforeHold)).IsTrue();
        var heldX = beforeHold.Position.x;

        // The target mashes run-away input for the whole bombardment window but
        // the refreshed Root keeps them caged (the pulses are impulse-free, so
        // nothing else moves them either).
        for (int step = 1; step <= 119; step++) {
            simulation.Advance(
                Frame(tick + step, 0, GameplayButtons.None),
                Frame(tick + step, 127, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent midWindow)).IsTrue();
        AssertThat(midWindow.StatusType).IsEqual((int)StatusType.Root);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent held)).IsTrue();
        AssertThat(held.Position.x == heldX).IsTrue();
    }

    [TestCase]
    public void BombardmentLandsTheFullMultiHitTotalBeyondMeleeRange() {
        // Fighters spawn at +/-2 (4 units apart), beyond the 2-unit generic
        // melee/ultimate attack range; the meter is charged with a 100-damage
        // projectile special instead.
        var simulation = CreateSimulation(spawnDistance: 2, seed: 73);
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick <= 30; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.ToFloat()).IsEqual(100f);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterCharge)).IsTrue();
        AssertThat(afterCharge.CurrentHP).IsEqual(300);

        // The bespoke ultimate fires despite the generic melee range whiffing.
        simulation.Advance(Frame(31, 0, GameplayButtons.Ultimate), Frame(31, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual((int)FighterCharacterID.Leonardo * 10 + 3);

        // All 8 ticks of 10 land across the 2.4 s window (80 total) and the
        // expiry explosion launches the target with real knockback.
        var xBeforeExpiry = FP64.FromInt(2);
        for (int tick = 32; tick <= 200; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent bombarded)).IsTrue();
        AssertThat(bombarded.CurrentHP).IsEqual(220);
        AssertThat(bombarded.Position.x > xBeforeExpiry).IsTrue();
    }

    [TestCase]
    public void UltimateBombardmentBypassesBlock() {
        var simulation = CreateSimulation(spawnDistance: 1, seed: 74);
        int tick = BasicStringTestDriver.LandChainedBasics(simulation, 0, 1);

        // The target holds Block for the entire ultimate; the ultimate-class
        // pulses ignore the shield entirely, so the full 80 damage lands and no
        // block charge is spent.
        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.Block));
        for (int step = 1; step <= 169; step++) {
            simulation.Advance(
                Frame(tick + step, 0, GameplayButtons.None),
                Frame(tick + step, 0, GameplayButtons.Block));
        }
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent blocked)).IsTrue();
        AssertThat(blocked.CurrentHP).IsEqual(220);
        AssertThat(blocked.BlockCharges).IsEqual(3);
    }

    [TestCase]
    public void UltimateLifecycleIsSnapshotAndRollbackSafe() {
        FighterLoadout leonardo = FighterLoadoutFactory.FromCharacterData(BuildUltimateLeonardo());
        FighterLoadout target = FighterLoadoutFactory.FromCharacterData(BuildTankTarget());
        var uninterrupted = new FighterSimulation(
            leonardo, target, seed: 75, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            leonardo, target, seed: 75, spawnDistance: 1, rules: FighterMatchRules.Disabled);

        uninterrupted.Advance(Frame(0, 0, GameplayButtons.BasicAttack), Frame(0, 0, GameplayButtons.None));
        uninterrupted.Advance(Frame(1, 0, GameplayButtons.Ultimate), Frame(1, 0, GameplayButtons.None));
        for (int tick = 2; tick < 40; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 127, GameplayButtons.None));
        }

        // Snapshot mid-bombardment with the Root hold and zone live.
        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // The replayed window spans the remaining ticks, the expiry explosion,
        // and the Root/status decay afterwards.
        for (int tick = 40; tick < 240; tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, 127, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, 127, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    private static FighterSimulation CreateSimulation(int spawnDistance, int seed) => new(
        FighterLoadoutFactory.FromCharacterData(BuildUltimateLeonardo()),
        FighterLoadoutFactory.FromCharacterData(BuildTankTarget()),
        seed: seed,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);

    private static CharacterData BuildUltimateLeonardo() => new() {
        CharacterID = "leonardo",
        MaxHP = 95,
        Weight = 0.9f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 7.5f,
        MaxJumpForce = 13f,
        // Test-only meter chargers: one landed hit fills the 100-point meter
        // (the first basic combo hit deals 0.8x, so 125 x 0.8 = 100).
        BasicAttackDamage = 125f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.Projectile,
            BaseDamage = 100f,
            ProjectileSpeed = 600f,
            ProjectileLifetime = 2f,
            CooldownDuration = 10f
        },
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        // Mirrors the authored resources/Abilities/leonardo/ultimate.tres.
        UltimateAttack = new AbilityData {
            ExecutionType = AbilityExecutionType.Cinematic,
            BaseDamage = 10f,
            IsMultiHit = true,
            HitCount = 8,
            DamageTickIntervalFrames = 18,
            Lifetime = 2.4f,
            AppliedStatus = StatusType.Root,
            StatusDuration = 0.4f,
            KnockbackForce = new Vector2(5, -3)
        }
    };

    private static CharacterData BuildTankTarget() => new() {
        CharacterID = "joan",
        MaxHP = 400,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 7f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 8f,
        BasicAttackKnockback = 2f,
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
