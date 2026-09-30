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
/// Matrix ultimate (L04, Package 13 W6): the press spends the meter and throws
/// the geometric sphere (A02: a straight seven-unit activation strike); its
/// contact starts the trap zone (type 23) on the held victim — five 10-damage
/// bombardment ticks carrying the refreshing Root hold, then the 24-damage
/// explosion finale (74) — bypassing block, reaching well beyond melee range,
/// and snapshot/rollback safe.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LeonardoUltimateTests {

    private const int ChargedHP = 300;
    private const int UltimateTotal = 5 * 10 + 24;

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
    public void UltimateSpendsTheMeterAndTheSphereContactStartsTheMatrix() {
        var simulation = CreateSimulation(spawnDistance: 1, seed: 71);

        // One landed basic (125 x 0.8 = 100) fills the meter exactly; the
        // driver walks the real phased swing and settles back to idle.
        int tick = BasicStringTestDriver.LandChainedBasics(simulation, 0, 1);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.ToFloat()).IsEqual(100f);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterCharge)).IsTrue();
        AssertThat(afterCharge.CurrentHP).IsEqual(ChargedHP);

        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual((int)FighterCharacterID.Leonardo * 10 + 3);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        // V7.6 D03h: spent on acceptance, and Ultimate-origin damage re-credits nothing.
        AssertThat(caster.Influence.ToFloat()).IsEqual(0f);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterContact)).IsTrue();
        AssertThat(afterContact.CurrentHP).IsEqual(ChargedHP - 10);
    }

    [TestCase]
    public void TrapRootHoldsTheTargetInsideTheMatrix() {
        var simulation = CreateSimulation(spawnDistance: 1, seed: 72);
        int tick = BasicStringTestDriver.LandChainedBasics(simulation, 0, 1);
        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick);

        // The first bombardment tick applies the authored Root hold (a control
        // hold rides the regular hits, never the finale).
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent trapped)).IsTrue();
        AssertThat(trapped.StatusType).IsEqual((int)StatusType.Root);
        AssertThat(trapped.StatusFrames > 0).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent beforeHold)).IsTrue();
        var heldX = beforeHold.Position.x;

        // The target mashes run-away input through the bombardment but stays caged.
        for (int step = 0; step < 80; step++) {
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
    public void TheSevenUnitSphereLandsTheFullTotalBeyondMeleeRange() {
        // Fighters spawn at +/-2 (4 units apart), beyond the 2-unit generic
        // melee range; the meter is charged with a 100-damage projectile special.
        var simulation = CreateSimulation(spawnDistance: 2, seed: 73);
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick <= 30; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.ToFloat()).IsEqual(100f);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterCharge)).IsTrue();
        AssertThat(afterCharge.CurrentHP).IsEqual(ChargedHP);
        var xBeforeFinale = afterCharge.Position.x;

        int next = UltimateActivationTestKit.CastAndConnect(simulation, 31, out bool connected);
        AssertThat(connected).IsTrue();
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual((int)FighterCharacterID.Leonardo * 10 + 3);

        // Five ticks of 10 and the 24-damage explosion (74 total), which
        // launches the target away with real knockback.
        UltimateActivationTestKit.Idle(simulation, next, 150);
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent bombarded)).IsTrue();
        AssertThat(bombarded.CurrentHP).IsEqual(ChargedHP - UltimateTotal);
        AssertThat(bombarded.Position.x > xBeforeFinale).IsTrue();
    }

    [TestCase]
    public void UltimateBombardmentBypassesBlock() {
        var simulation = CreateSimulation(spawnDistance: 1, seed: 74);
        int tick = BasicStringTestDriver.LandChainedBasics(simulation, 0, 1);

        // The target holds Block for the entire ultimate; the activation strike
        // and the ultimate-class pulses ignore the shield, so the full total
        // lands and no block charge is spent.
        tick = UltimateActivationTestKit.CastAndConnect(
            simulation, tick, out bool connected, victimHeld: GameplayButtons.Block);
        AssertThat(connected).IsTrue();
        UltimateActivationTestKit.Idle(simulation, tick, 150, GameplayButtons.Block);
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent blocked)).IsTrue();
        AssertThat(blocked.CurrentHP).IsEqual(ChargedHP - UltimateTotal);
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

        int tick = BasicStringTestDriver.LandChainedBasics(uninterrupted, 0, 1);
        tick = UltimateActivationTestKit.CastAndConnect(uninterrupted, tick);
        for (int i = 0; i < 38; i++, tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 127, GameplayButtons.None));
        }

        // Snapshot mid-bombardment with the Root hold and zone live.
        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // The replayed window spans the remaining ticks, the explosion finale,
        // and the Root/status decay afterwards.
        for (int i = 0; i < 200; i++, tick++) {
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
            HitCount = 5,
            DamageTickIntervalFrames = 18,
            FinaleDamage = 24f,
            FinaleLaunches = true,
            ActivationShape = UltimateActivationShape.Projectile,
            ActivationRange = 420f,
            ActivationHitboxSize = new Vector2(60f, 60f),
            Lifetime = 1.8f,
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
