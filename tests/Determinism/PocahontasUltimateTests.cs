using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for Pocahontas's canonical Tidewater
/// Tempest ultimate (audit gap X7). Package 13 W6 (A02): her resource was
/// deliberately not re-authored (W5 replaces her), so her activation strike is
/// the AbilityData default — a straight six-unit shot — whose contact starts
/// the storm on the held victim. The press consumes the full meter, the spirit storm zone
/// (type 83) delivers the authored 8 x 10 multi-hit total at the authored
/// 21-frame cadence, only the final surge carries the outward knockback
/// impulse, the storm reaches beyond melee range, its ticks bypass block
/// charges, and the whole storm lifecycle is snapshot/rollback safe.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PocahontasUltimateTests {

    private const int TempestZoneTypeID = 83;
    private const int StormTotalDamage = 80;   // 8 ticks x 10 authored per-hit damage.
    private const int MeterChargeDamage = 138; // 33 + 42 + 63 basic combo, capped to 100 meter.

    /// <summary>
    /// The ultimate tests churn large deterministic-simulation allocations
    /// alongside throwaway Godot resource instances. Draining pending finalizers
    /// here keeps them from racing a later suite's ResourceLoader work, which
    /// can trip the engine's script-instance dispose FATAL mid-load.
    /// </summary>
    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [TestCase]
    public void TempestConsumesMeterAndDealsTheMultiHitTotalWithoutGenericDoubleHit() {
        FighterSimulation simulation = BuildSimulation(seed: 83);
        int ultimatePressTick = ChargeMeterWithBasics(simulation);

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.ToFloat()).IsEqual(100f);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent beforeStorm)).IsTrue();
        int hpBeforeStorm = beforeStorm.CurrentHP;

        // The press consumes the whole meter; the activation contact spawns
        // the storm, whose first tick lands on the contact frame.
        int contactNext = UltimateActivationTestKit.CastAndConnect(simulation, ultimatePressTick, out bool connected);
        AssertThat(connected).IsTrue();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent spent)).IsTrue();
        // V7.6 D03h (Package 11 A1b): Ultimate-origin damage awards its
        // caster ZERO damage-dealt meter, so the same-frame tick no longer
        // re-credits anything - the meter reads exactly 0 after the cast.
        AssertThat(spent.Influence.ToFloat()).IsEqual(0f);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent storm)).IsTrue();
        AssertThat(storm.ZoneTypeID).IsEqual(TempestZoneTypeID);
        AssertThat(storm.TickIntervalFrames).IsEqual(21);

        // Run past the full 168-frame storm: exactly 8 x 10 damage.
        UltimateActivationTestKit.Idle(simulation, contactNext, 170);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterStorm)).IsTrue();
        AssertThat(hpBeforeStorm - afterStorm.CurrentHP).IsEqual(StormTotalDamage);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent _)).IsFalse();
    }

    [TestCase]
    public void TempestFinalSurgeCarriesTheAuthoredOutwardKnockbackImpulse() {
        FighterSimulation simulation = BuildSimulation(seed: 84);
        int ultimatePressTick = ChargeMeterWithBasics(simulation);
        int contactNext = UltimateActivationTestKit.CastAndConnect(simulation, ultimatePressTick);

        // Ticks land at contact + 0, 21, ..., 147; every tick before the final
        // surge is impulse-free (no hitstun, no launch).
        UltimateActivationTestKit.Idle(simulation, contactNext, 146);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent beforeSurge)).IsTrue();
        AssertThat(beforeSurge.HitstunFrames).IsEqual(0);

        // The final surge throws the target outward from the storm center: the
        // target sits to the caster's right, so the launch is up and to the right.
        UltimateActivationTestKit.Idle(simulation, contactNext + 146, 1);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent surged)).IsTrue();
        AssertThat(surged.HitstunFrames > 0).IsTrue();
        AssertThat(surged.IsGrounded).IsEqual(0);
        AssertThat(surged.Velocity.x.ToFloat() > 0f).IsTrue();
        AssertThat(surged.Velocity.y.ToFloat() > 0f).IsTrue();
    }

    [TestCase]
    public void TempestReachesBeyondMeleeRange() {
        FighterSimulation simulation = BuildSimulation(seed: 85);
        int ultimatePressTick = ChargeMeterWithBasics(simulation);

        // The target retreats out of melee attack range but stays inside the
        // storm's 4-unit half-width; a generic melee ultimate would whiff from
        // here. The retreat runs until the gap clears 2.5 units (the charge
        // driver walks the fighters closer than the old fixed window assumed).
        int retreatTick = ultimatePressTick;
        for (int limit = ultimatePressTick + 90; retreatTick < limit; retreatTick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent chaser)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent runner)).IsTrue();
            if (runner.Position.x.ToFloat() - chaser.Position.x.ToFloat() > 2.5f) break;
            simulation.Advance(Frame(retreatTick, 0, GameplayButtons.None), Frame(retreatTick, 127, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent retreated)).IsTrue();
        float gap = retreated.Position.x.ToFloat() - caster.Position.x.ToFloat();
        AssertThat(gap > 2.5f).IsTrue();
        AssertThat(gap < 4.4f).IsTrue();
        int hpBeforeStorm = retreated.CurrentHP;

        int contactNext = UltimateActivationTestKit.CastAndConnect(simulation, retreatTick, out bool connected);
        AssertThat(connected).IsTrue();
        UltimateActivationTestKit.Idle(simulation, contactNext, 170);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterStorm)).IsTrue();
        AssertThat(hpBeforeStorm - afterStorm.CurrentHP).IsEqual(StormTotalDamage);
    }

    [TestCase]
    public void TempestBypassesBlockChargesLikeEveryUltimate() {
        FighterSimulation simulation = BuildSimulation(seed: 86);
        int ultimatePressTick = ChargeMeterWithBasics(simulation);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent beforeStorm)).IsTrue();
        int hpBeforeStorm = beforeStorm.CurrentHP;
        int chargesBeforeStorm = beforeStorm.BlockCharges;

        // The target holds Block for the whole storm; ultimate-class zone hits
        // bypass the shield, so the full total lands and no charge is spent.
        int contactNext = UltimateActivationTestKit.CastAndConnect(
            simulation, ultimatePressTick, out bool connected, victimHeld: GameplayButtons.Block);
        AssertThat(connected).IsTrue();
        UltimateActivationTestKit.Idle(simulation, contactNext, 170, GameplayButtons.Block);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterStorm)).IsTrue();
        AssertThat(hpBeforeStorm - afterStorm.CurrentHP).IsEqual(StormTotalDamage);
        AssertThat(afterStorm.BlockCharges).IsEqual(chargesBeforeStorm);
    }

    [TestCase]
    public void TempestLifecycleIsSnapshotAndRollbackSafe() {
        FighterSimulation uninterrupted = BuildSimulation(seed: 87);
        FighterSimulation restored = BuildSimulation(seed: 87);

        int ultimatePressTick = ChargeMeterWithBasics(uninterrupted);
        int contactNext = UltimateActivationTestKit.CastAndConnect(uninterrupted, ultimatePressTick);
        UltimateActivationTestKit.Idle(uninterrupted, contactNext, 59);

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // Resimulate through the remaining ticks, the final surge launch, and
        // the zone expiry; both simulations must stay hash-identical every tick.
        for (int tick = contactNext + 59; tick < contactNext + 259; tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    private static FighterSimulation BuildSimulation(int seed) => new(
        FighterLoadoutFactory.FromCharacterData(BuildTempestCharacter()),
        FighterLoadoutFactory.FromCharacterData(BuildTargetCharacter()),
        seed: seed,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    /// <summary>
    /// Lands the three-hit basic combo (0.8x / 1.0x / 1.5x of 42 = 138 damage
    /// dealt) to fill the Influence meter to its 100 cap, driving the real
    /// phased string, and returns the next free tick. The attacker's basics
    /// carry zero knockback so the adjacent target never leaves attack range.
    /// </summary>
    private static int ChargeMeterWithBasics(FighterSimulation simulation) =>
        BasicStringTestDriver.LandChainedBasics(simulation, 0, 3);

    /// <summary>
    /// Pocahontas with the authored Tidewater Tempest numbers from
    /// `resources/Abilities/pocahontas/ultimate.tres`: 10 per hit x 8 hits at a
    /// 21-frame cadence over a 2.8 s storm, final-surge knockback magnitude 5.
    /// </summary>
    private static CharacterData BuildTempestCharacter() => new() {
        CharacterID = "pocahontas",
        MaxHP = 90,
        Weight = 0.8f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 9f,
        MaxJumpForce = 15f,
        BasicAttackDamage = 42f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData {
            BaseDamage = 10f,
            IsMultiHit = true,
            HitCount = 8,
            DamageTickIntervalFrames = 21,
            Lifetime = 2.8f,
            KnockbackForce = new Godot.Vector2(5f, -3f)
        }
    };

    private static CharacterData BuildTargetCharacter() => new() {
        CharacterID = "joan",
        MaxHP = 400,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 9f,
        MaxJumpForce = 15f,
        BasicAttackDamage = 9f,
        BasicAttackKnockback = 2.5f,
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
