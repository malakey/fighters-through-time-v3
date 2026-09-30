using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for Einstein's canonical ultimate, The
/// Cosmological Constant (E06, Package 13 W6): a press only accepts the
/// Ultimate — the meter is spent and nothing lands until the chalk-orb
/// activation strike connects (A02) — and contact starts the singularity on the
/// held victim, which lands 6 × 10 pull hits and then the 18-damage launch
/// finale (78), works beyond melee range, bypasses block, and stays
/// snapshot/rollback safe.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EinsteinUltimateTests {

    private const int UltimateZoneTypeID = (int)FighterCharacterID.Einstein * 10 + 3;
    private const int PerTickDamage = 10;
    private const int HitCount = 6;
    private const int FinaleDamage = 18;
    private const int TotalUltimateDamage = PerTickDamage * HitCount + FinaleDamage;

    /// <summary>
    /// The kit tests churn large deterministic-simulation allocations alongside
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

    [TestCase]
    public void AcceptedUltimateSpendsTheMeterAndLandsNothingUntilContact() {
        var simulation = NewSimulation(seed: 61);
        int tick = FillMeterAndSettle(simulation);

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent readyAttacker)).IsTrue();
        AssertThat(readyAttacker.Influence).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(100));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent beforeTarget)).IsTrue();
        int hpBeforeCast = beforeTarget.CurrentHP;

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        tick++;

        // A02: the press spends the meter at once (D03h), starts the wind-up and
        // spawns nothing.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent castAttacker)).IsTrue();
        AssertThat(castAttacker.Influence).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.Zero);
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseWindup);
        AssertThat(simulation.ZoneCount).IsEqual(0);

        // Contact starts the singularity; its first pull pulse lands on the
        // contact tick and carries no hitstun.
        bool connected = false;
        for (int i = 0; i < 40 && !connected; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
            connected = UltimateActivationTestKit.IsCinematic(simulation, 0);
        }
        AssertThat(connected).IsTrue();
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual(UltimateZoneTypeID);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(hpBeforeCast - PerTickDamage);
        AssertThat(target.HitstunFrames).IsEqual(0);
    }

    [TestCase]
    public void MultiHitTotalAndFinaleLandOverTheCinematic() {
        var simulation = NewSimulation(seed: 62);
        int tick = FillMeterAndSettle(simulation);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hpBeforeCast = before.CurrentHP;

        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();
        UltimateActivationTestKit.Idle(simulation, tick, 140);

        // E06: 6 pull hits x 10, then the 18-damage launch = 78; the zone is gone.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.CurrentHP).IsEqual(hpBeforeCast - TotalUltimateDamage);
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseNone);
    }

    [TestCase]
    public void TheCinematicHoldsTheVictimOnTheSingularity() {
        var simulation = NewSimulation(seed: 63);
        int tick = FillMeterAndSettle(simulation);
        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();

        // The victim tries to walk and jump away; the hold keeps them on the
        // singularity (the zone is spawned on them) for the whole sequence.
        for (int i = 0; i < 60; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 127, GameplayButtons.Jump));
            tick++;
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent held)).IsTrue();
        AssertThat(held.Position.x).IsEqual(zone.Position.x);
        AssertThat(simulation.TryGetFighterUltimateActivation(1, out FighterUltimateActivationComponent capture)).IsTrue();
        AssertThat(FighterUltimateActivationRules.IsCaptured(in capture)).IsTrue();
    }

    [TestCase]
    public void FinalLaunchCarriesRealImpulse() {
        var simulation = NewSimulation(seed: 64);
        int tick = FillMeterAndSettle(simulation);
        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();

        // Step to the release (the finale tick), then verify the launch hit.
        bool launched = false;
        for (int i = 0; i < 140 && !launched; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
            if (!UltimateActivationTestKit.IsCinematic(simulation, 0)) {
                AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
                AssertThat(target.HitstunFrames > 0).IsTrue();
                AssertThat(target.IsGrounded).IsEqual(0);
                // UltimateKnockback 6 against weight 1 blasts the target up and
                // along Einstein's facing, toward the blast zone.
                AssertThat(target.Velocity.y > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();
                AssertThat(target.Velocity.x > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();
                launched = true;
            }
        }
        AssertThat(launched).IsTrue();
    }

    [TestCase]
    public void UltimateConnectsBeyondMeleeRangeAndBypassesBlock() {
        var simulation = NewSimulation(seed: 65);
        int tick = FillMeterAndSettle(simulation);
        // Retreat beyond the 2-unit generic attack range while staying inside
        // the orb's 6-unit activation reach.
        for (int i = 0; i < 30; i++) {
            simulation.Advance(Frame(tick, -127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        tick = UltimateActivationTestKit.Idle(simulation, tick, 20);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        var gap = xpTURN.Klotho.Deterministic.Math.FP64.Abs(target.Position.x - attacker.Position.x);
        AssertThat(gap > xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2)).IsTrue();
        AssertThat(gap < xpTURN.Klotho.Deterministic.Math.FP64.FromInt(6)).IsTrue();
        int hpBeforeCast = target.CurrentHP;

        // The attacker faces the opponent again (retreating turned it around).
        simulation.Advance(Frame(tick, 127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        tick++;
        tick = UltimateActivationTestKit.Idle(simulation, tick, 20);

        // The opponent holds Block for the whole Ultimate: the activation strike
        // is unblockable and every hit of the cinematic still lands.
        tick = UltimateActivationTestKit.CastAndConnect(
            simulation, tick, out bool connected, victimHeld: GameplayButtons.Block);
        AssertThat(connected).IsTrue();
        UltimateActivationTestKit.Idle(simulation, tick, 140, GameplayButtons.Block);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.CurrentHP).IsEqual(hpBeforeCast - TotalUltimateDamage);
    }

    [TestCase]
    public void UltimateLifecycleIsSnapshotAndRollbackSafe() {
        FighterLoadout einstein = FighterLoadoutFactory.FromCharacterData(BuildUltimateCharacter());
        FighterLoadout opponent = FighterLoadoutFactory.FromCharacterData(BuildTankOpponent());
        var uninterrupted = new FighterSimulation(
            einstein, opponent, seed: 66, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            einstein, opponent, seed: 66, spawnDistance: 1, rules: FighterMatchRules.Disabled);

        int tick = FillMeterAndSettle(uninterrupted);
        tick = UltimateActivationTestKit.CastAndConnect(uninterrupted, tick);
        // Snapshot mid-cinematic, with pulses and the hold in flight.
        tick = UltimateActivationTestKit.Idle(uninterrupted, tick, 30);

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // Convergence across the remaining pulses, the finale, and beyond.
        for (int i = 0; i < 200; i++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
            tick++;
        }
    }

    private static FighterSimulation NewSimulation(int seed) => new(
        FighterLoadoutFactory.FromCharacterData(BuildUltimateCharacter()),
        FighterLoadoutFactory.FromCharacterData(BuildTankOpponent()),
        seed: seed,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    /// <summary>
    /// One basic hit (125 x 0.8 = 100 damage) fills the Influence meter, then
    /// idle frames sized from the shared tables (opener startup plus its
    /// hitstun, with a small pad) let the fill hit's hitstun decay so later
    /// assertions see only the ultimate's effects. Returns the next input tick.
    /// </summary>
    private static int FillMeterAndSettle(FighterSimulation simulation) {
        int tick = 0;
        simulation.Advance(Frame(tick++, 0, GameplayButtons.BasicAttack), Frame(0, 0, GameplayButtons.None));
        int settleFrames = FTT.Combat.BasicComboRules.GroundStartupFrames[0]
            + FTT.Combat.BasicComboRules.HitstunFrames[0]
            // V7.1 hitstop: the 100-damage fill hit freezes the victim for the
            // max window before their hitstun starts counting down.
            + FTT.Combat.BasicComboRules.HitstopFrames(100)
            + 4;
        for (int i = 0; i < settleFrames; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        return tick;
    }

    private static CharacterData BuildUltimateCharacter() => new() {
        CharacterID = "einstein",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 125f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData {
            BaseDamage = 10f,
            IsMultiHit = true,
            HitCount = 6,
            DamageTickIntervalFrames = 18,
            FinaleDamage = 18f,
            FinaleLaunches = true,
            KnockbackForce = new Vector2(6, -4),
            Lifetime = 2.1f
        }
    };

    private static CharacterData BuildTankOpponent() => new() {
        CharacterID = "joan",
        MaxHP = 400,
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
