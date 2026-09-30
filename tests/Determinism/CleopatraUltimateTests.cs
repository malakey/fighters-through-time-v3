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
/// Nile ultimate (C04, Package 13 W6): the press spends the meter and sends the
/// spectral asp six units straight ahead (A02); its contact raises the storm
/// zone (type 43) on the held victim — six impulse-free 9-damage cobra strikes,
/// then the 12-damage sarcophagus finale carrying the heavy Venom (intensity
/// 2.0 for 3 s = 12 more), 78 in all. The Venom lands exactly once, on the
/// finale, in both modes. The storm bypasses block, reaches beyond melee range
/// and stays snapshot/rollback safe.
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
    private const int CobraDamage = 9;
    private const int CobraCount = 6;
    private const int IntervalFrames = 21;
    private const int SlamDamage = 12;
    private const int VenomTotal = 12;
    private const int ImpactTotal = CobraDamage * CobraCount + SlamDamage;
    private const int UltimateTotal = ImpactTotal + VenomTotal;

    [TestCase]
    public void TheAspContactRaisesTheStormWithTheVenomHeldForTheFinale() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 71, out int tick);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent beforeTarget)).IsTrue();
        int hpBeforeUltimate = beforeTarget.CurrentHP;

        UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();

        // D03h: spent on acceptance, no re-credit.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence.RawValue).IsEqual(
            xpTURN.Klotho.Deterministic.Math.FP64.Zero.RawValue);
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual(StormZoneTypeID);
        AssertThat(zone.Damage).IsEqual(CobraDamage);
        // C04: the Venom rides the finale, never the cobra pulses.
        AssertThat(zone.StatusType).IsEqual((int)StatusType.None);

        // The contact tick lands exactly one impulse-free cobra strike.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent contactFrame)).IsTrue();
        AssertThat(contactFrame.HitstunFrames).IsEqual(0);
        AssertThat(hpBeforeUltimate - contactFrame.CurrentHP).IsEqual(CobraDamage);
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent unpoisoned)).IsTrue();
        AssertThat(unpoisoned.DamageStatusType).IsEqual((int)StatusType.None);
    }

    [TestCase]
    public void StormLandsSeventyEightCountingTheFinaleVenom() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 72, out int tick);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hpBefore = before.CurrentHP;

        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick);
        // Through the cobras and the finale (6 x 21 frames after contact).
        tick = UltimateActivationTestKit.Idle(simulation, tick, CobraCount * IntervalFrames);
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent stormEnd)).IsTrue();
        AssertThat(hpBefore - stormEnd.CurrentHP).IsEqual(ImpactTotal);

        // The finale's heavy Venom (2.0 for 3 s) is attached once and ticks
        // three times for 4 each — exactly the 12 C04 counts in the total.
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.DamageStatusType).IsEqual((int)StatusType.Venom);
        AssertThat(runtime.DamageStatusIntensity.RawValue).IsEqual(
            xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2).RawValue);
        UltimateActivationTestKit.Idle(simulation, tick, 200);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent poisoned)).IsTrue();
        AssertThat(hpBefore - poisoned.CurrentHP).IsEqual(UltimateTotal);
    }

    [TestCase]
    public void TheAspReachesAnOpponentBeyondMeleeRange() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 73, out int tick);

        // Warp Cleopatra away so the opponent sits outside the 2-unit generic
        // melee range, then turn back toward them.
        simulation.Advance(Frame(tick, -127, GameplayButtons.MovementAbility), Frame(tick, 0, GameplayButtons.None));
        tick++;
        simulation.Advance(Frame(tick, 127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        tick = UltimateActivationTestKit.Idle(simulation, tick + 1, 20);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        var separation = xpTURN.Klotho.Deterministic.Math.FP64.Abs(target.Position.x - caster.Position.x);
        AssertThat(separation > xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2)).IsTrue();
        int hpBefore = target.CurrentHP;

        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();
        AssertThat(simulation.ZoneCount).IsEqual(1);
        UltimateActivationTestKit.Idle(simulation, tick, 60);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent hit)).IsTrue();
        AssertThat(hit.CurrentHP < hpBefore).IsTrue();
    }

    [TestCase]
    public void StormBypassesBlockWithoutSpendingBlockCharges() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 74, out int tick);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hpBefore = before.CurrentHP;

        tick = UltimateActivationTestKit.CastAndConnect(
            simulation, tick, out bool connected, victimHeld: GameplayButtons.Block);
        AssertThat(connected).IsTrue();
        UltimateActivationTestKit.Idle(simulation, tick, 220, GameplayButtons.Block);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent blocked)).IsTrue();
        AssertThat(hpBefore - blocked.CurrentHP >= ImpactTotal).IsTrue();
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

        int tick = ChargeMeter(uninterrupted, startTick: 0);
        tick = UltimateActivationTestKit.CastAndConnect(uninterrupted, tick);

        // Snapshot mid-storm and restore into the second simulation.
        for (int i = 0; i < 40; i++, tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -60, GameplayButtons.None));
        }
        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // Cover the remaining strikes, the finale, the Venom DoT and its expiry.
        for (int i = 0; i < 520; i++, tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -60, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -60, GameplayButtons.None));
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
    /// cleopatra/ultimate.tres (six 9-damage cobras at 21 frames, the 12-damage
    /// slam finale, heavy Venom 3 s at 2.0 intensity), zero-knockback basics so
    /// the meter charge never pushes the opponent out of range, and a
    /// three-unit teleport for the beyond-melee-range case.
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
            DistanceMoved = 180f,
            MovementSpeed = 600f,
            CooldownDuration = 5f
        },
        UltimateAttack = new AbilityData {
            ExecutionType = AbilityExecutionType.Cinematic,
            BaseDamage = CobraDamage,
            IsMultiHit = true,
            HitCount = CobraCount,
            DamageTickIntervalFrames = IntervalFrames,
            FinaleDamage = SlamDamage,
            FinaleLaunches = false,
            ActivationHitboxSize = new Vector2(60f, 36f),
            KnockbackForce = new Vector2(4f, -3f),
            Lifetime = 2.45f,
            AppliedStatus = StatusType.Venom,
            StatusDuration = 3f,
            StatusIntensity = 2f
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
