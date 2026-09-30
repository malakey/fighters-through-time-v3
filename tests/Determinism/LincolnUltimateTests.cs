using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using FP64 = xpTURN.Klotho.Deterministic.Math.FP64;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for Lincoln's canonical Union
/// Indestructible ultimate (LN05, Package 13 W6): the press spends the meter
/// and slams the rail; the fence-line ground wave (A02: five units along the
/// ground, grounded targets only) must connect before the pen rises on the held
/// victim. The pen roots them, the five 14-damage rail smashes land (70 — no
/// finale), the final smash carries the heavy finisher impulse, the cast
/// connects beyond the generic melee range, the smashes bypass an active block,
/// a jump clears the wave, and the whole lifecycle is snapshot/rollback safe.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LincolnUltimateTests {

    private const int UnionZoneTypeID = (int)FighterCharacterID.Lincoln * 10 + 3;

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
    public void TheGroundWaveContactRaisesThePenAndSpendsTheMeter() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 71, out int nextTick);

        int tick = UltimateActivationTestKit.CastAndConnect(simulation, nextTick, out bool connected);
        AssertThat(connected).IsTrue();

        // The contact tick lands only the first impulse-free pen smash (14, no
        // hitstun). V7.6 D03h: the meter was spent on acceptance and
        // Ultimate-origin damage re-credits nothing.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent lincoln)).IsTrue();
        AssertThat(lincoln.Influence.RawValue).IsEqual(FP64.Zero.RawValue);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(286);
        AssertThat(target.HitstunFrames).IsEqual(0);

        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual(UnionZoneTypeID);
    }

    [TestCase]
    public void FencePenRootsTheTrappedOpponentForTheWholeWindow() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 72, out int nextTick);

        int tick = UltimateActivationTestKit.CastAndConnect(simulation, nextTick);
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent penned)).IsTrue();
        AssertThat(penned.StatusType).IsEqual((int)StatusType.Root);
        AssertThat(penned.StatusFrames).IsEqual(36);

        // The opponent mashes away for 100 frames; the hold and the refreshed
        // 0.6 s Root keep them in the pen.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        for (int step = 0; step < 100; step++) {
            simulation.Advance(
                Frame(tick + step, 0, GameplayButtons.None),
                Frame(tick + step, 127, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent stillPenned)).IsTrue();
        AssertThat(stillPenned.StatusType).IsEqual((int)StatusType.Root);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.Position.x.RawValue).IsEqual(before.Position.x.RawValue);
    }

    [TestCase]
    public void SmashSequenceDealsTheFullMultiHitTotalThenExpires() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 73, out int nextTick);

        int tick = UltimateActivationTestKit.CastAndConnect(simulation, nextTick);
        UltimateActivationTestKit.Idle(simulation, tick, 160);

        // 5 smashes x 14 = 70 (LN05; no finale) on top of the 100-damage
        // meter-charging basic, and the 2.5 s zone is gone.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(230);
        AssertThat(simulation.ZoneCount).IsEqual(0);
    }

    [TestCase]
    public void FinalSmashCarriesTheHeavyFinisherImpulse() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 74, out int nextTick);

        // The contact tick is the first smash; smashes 2-5 follow every 30 frames.
        int tick = UltimateActivationTestKit.CastAndConnect(simulation, nextTick);
        tick = UltimateActivationTestKit.Idle(simulation, tick, 119);
        // Smashes 1-4 stayed impulse-free so the pen kept holding.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent penned)).IsTrue();
        AssertThat(penned.HitstunFrames).IsEqual(0);

        // The fifth and final smash: the fence shatters, the hold releases and
        // the massive authored knockback (12) launches the target.
        UltimateActivationTestKit.Idle(simulation, tick, 1);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent launched)).IsTrue();
        AssertThat(launched.CurrentHP).IsEqual(230);
        AssertThat(launched.HitstunFrames).IsEqual(30);
        AssertThat(launched.IsGrounded).IsEqual(0);
        AssertThat(launched.Velocity.y > FP64.Zero).IsTrue();
        AssertThat(simulation.TryGetFighterUltimateActivation(1, out FighterUltimateActivationComponent capture)).IsTrue();
        AssertThat(FighterUltimateActivationRules.IsCaptured(in capture)).IsFalse();
    }

    [TestCase]
    public void TheWaveConnectsBeyondTheGenericMeleeRange() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 75, out int nextTick);

        // Lincoln retreats for 30 frames, then turns back toward the opponent:
        // the gap is now beyond the generic 2-unit melee attack range but
        // inside the wave's five units.
        for (int step = 0; step < 30; step++) {
            simulation.Advance(
                Frame(nextTick + step, -127, GameplayButtons.None),
                Frame(nextTick + step, 0, GameplayButtons.None));
        }
        simulation.Advance(Frame(nextTick + 30, 127, GameplayButtons.None), Frame(nextTick + 30, 0, GameplayButtons.None));
        int tick = UltimateActivationTestKit.Idle(simulation, nextTick + 31, 20);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent lincoln)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(FP64.Abs(target.Position.x - lincoln.Position.x) > FP64.FromInt(2)).IsTrue();

        UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent smashed)).IsTrue();
        AssertThat(smashed.CurrentHP).IsEqual(286);
    }

    [TestCase]
    public void AJumpClearsTheGroundWave() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 78, out int nextTick);

        // LN05: grounded targets only. The victim jumps as the rail slams; the
        // wave races under them, nothing is dealt, and the meter stays spent.
        simulation.Advance(Frame(nextTick, 0, GameplayButtons.Ultimate), Frame(nextTick, 0, GameplayButtons.None));
        simulation.Advance(Frame(nextTick + 1, 0, GameplayButtons.None), Frame(nextTick + 1, 0, GameplayButtons.Jump));
        UltimateActivationTestKit.Idle(simulation, nextTick + 2, 40);

        AssertThat(UltimateActivationTestKit.Phase(simulation, 0))
            .IsEqual(FighterUltimateActivationRules.PhaseWhiffRecovery);
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent jumper)).IsTrue();
        AssertThat(jumper.CurrentHP).IsEqual(300);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent lincoln)).IsTrue();
        AssertThat(lincoln.Influence.RawValue).IsEqual(FP64.Zero.RawValue);
    }

    [TestCase]
    public void SmashesAndFinisherBypassAnActiveBlock() {
        FighterSimulation simulation = BuildChargedSimulation(seed: 76, out int nextTick);

        // The opponent blocks through the whole ultimate. The wave is
        // unblockable, the smashes are ultimate-class, and every hit lands with
        // no charge consumed.
        int tick = UltimateActivationTestKit.CastAndConnect(
            simulation, nextTick, out bool connected, victimHeld: GameplayButtons.Block);
        AssertThat(connected).IsTrue();
        UltimateActivationTestKit.Idle(simulation, tick, 120, GameplayButtons.Block);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent blocked)).IsTrue();
        AssertThat(blocked.CurrentHP).IsEqual(230);
        AssertThat(blocked.BlockCharges).IsEqual(3);
        AssertThat(blocked.HitstunFrames).IsEqual(30);
    }

    [TestCase]
    public void UltimateLifecycleIsSnapshotAndRollbackSafe() {
        int seed = 77;
        FighterSimulation uninterrupted = BuildChargedSimulation(seed, out int nextTick);
        FighterSimulation restored = BuildChargedSimulation(seed, out int _);

        // Cast, then snapshot mid-pen with the zone alive and the Root active so
        // the whole ultimate state participates in the rollback.
        int tick = UltimateActivationTestKit.CastAndConnect(uninterrupted, nextTick);
        for (int i = 0; i < 20; i++, tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
        }

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        for (int i = 0; i < 375; i++, tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 64, GameplayButtons.None),
                Frame(tick, -127, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 64, GameplayButtons.None),
                Frame(tick, -127, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    /// <summary>
    /// Builds a simulation with Lincoln's meter fully charged: after walking
    /// inside his authored 1.7-unit reach (V7.1 string profile), the oversized
    /// zero-knockback opener (143 x 0.7 = 100 damage) fills the meter in one
    /// hit against the 400 HP opponent, then idle frames sized from his
    /// profile (opener startup plus its hitstun, with a small pad) let the
    /// hitstun lapse. Returns the next free input tick.
    /// </summary>
    private static FighterSimulation BuildChargedSimulation(int seed, out int nextTick) {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildUltimateLincoln()),
            FighterLoadoutFactory.FromCharacterData(BuildTankyOpponent()),
            seed: seed,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        int approachTick = 0;
        for (int limit = 120; approachTick < limit; approachTick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent walker)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent standing)).IsTrue();
            if (FP64.Abs(standing.Position.x - walker.Position.x) <= FP64.FromDouble(1.4)) break;
            simulation.Advance(
                Frame(approachTick, 64, GameplayButtons.None),
                Frame(approachTick, 0, GameplayButtons.None));
        }

        simulation.Advance(
            Frame(approachTick, 0, GameplayButtons.BasicAttack),
            Frame(approachTick, 0, GameplayButtons.None));
        int settleTicks = approachTick
            + FTT.Combat.BasicComboRules.StringProfileFor("lincoln").GroundStartupFrames[0]
            + FTT.Combat.BasicComboRules.HitstunFrames[0]
            // V7.1 hitstop: the 100-damage opener freezes the victim for the
            // max window before their hitstun starts counting down.
            + FTT.Combat.BasicComboRules.HitstopFrames(100)
            + 4;
        for (int tick = approachTick + 1; tick <= settleTicks; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent lincoln)).IsTrue();
        AssertThat(lincoln.Influence.RawValue).IsEqual(FP64.FromInt(100).RawValue);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(300);
        AssertThat(target.HitstunFrames).IsEqual(0);
        nextTick = settleTicks + 1;
        return simulation;
    }

    private static CharacterData BuildUltimateLincoln() => new() {
        CharacterID = "lincoln",
        MaxHP = 130,
        Weight = 1.6f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 5.5f,
        MaxJumpForce = 11f,
        // 143 x Lincoln's 0.7 opener shape (V7.1) = 100 = one full meter.
        BasicAttackDamage = 143f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData { BaseDamage = 20f },
        SpecialAttackTwo = new AbilityData { BaseDamage = 18f },
        MovementAbility = new MovementAbilityData(),
        // Mirrors the authored lincoln/ultimate.tres numbers.
        UltimateAttack = new AbilityData {
            BaseDamage = 14f,
            IsMultiHit = true,
            HitCount = 5,
            DamageTickIntervalFrames = 30,
            HitstunFrames = 30,
            ActivationShape = UltimateActivationShape.GroundWave,
            ActivationRange = 300f,
            ActivationHitboxSize = new Vector2(60f, 40f),
            KnockbackForce = new Vector2(12f, -8f),
            AppliedStatus = StatusType.Root,
            StatusDuration = 0.6f,
            StatusIntensity = 1f,
            Lifetime = 2.5f
        }
    };

    private static CharacterData BuildTankyOpponent() => new() {
        CharacterID = "joan",
        MaxHP = 400,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 6f,
        MaxJumpForce = 11f,
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
