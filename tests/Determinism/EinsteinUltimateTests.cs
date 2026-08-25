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
/// Cosmological Constant: bespoke dispatch consumes the meter with no generic
/// double-hit, the singularity zone lands the full multi-hit total over its
/// lifetime, drags the opponent toward its center, fires the final explosive
/// launch on expiry, works beyond melee range, bypasses block, and stays
/// snapshot/rollback safe.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EinsteinUltimateTests {

    private const int UltimateZoneTypeID = (int)FighterCharacterID.Einstein * 10 + 3;
    private const int PerTickDamage = 15;
    private const int HitCount = 5;
    private const int TotalUltimateDamage = PerTickDamage * HitCount;

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
    public void UltimateCastConsumesMeterWithoutGenericDoubleHit() {
        var simulation = NewSimulation(seed: 61);
        int tick = FillMeterAndSettle(simulation);

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent readyAttacker)).IsTrue();
        AssertThat(readyAttacker.Influence).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(100));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent beforeTarget)).IsTrue();
        int hpBeforeCast = beforeTarget.CurrentHP;

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));

        // The bespoke dispatch consumed the meter and spawned the singularity.
        // The zone's first pulse lands later in the same frame and re-credits
        // exactly its 15 damage as fresh influence (the standard damage-dealt
        // rate), so the full 100 collapsed to the one-tick credit.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent castAttacker)).IsTrue();
        AssertThat(castAttacker.Influence).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(PerTickDamage));
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual(UltimateZoneTypeID);

        // Only the zone's first impulse-free pulse landed: the generic
        // melee-range ultimate (which carries 30 hitstun frames) did not fire
        // on the same press.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(hpBeforeCast - PerTickDamage);
        AssertThat(target.HitstunFrames).IsEqual(0);
    }

    [TestCase]
    public void MultiHitTotalLandsOverTheZoneLifetime() {
        var simulation = NewSimulation(seed: 62);
        int tick = FillMeterAndSettle(simulation);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hpBeforeCast = before.CurrentHP;

        simulation.Advance(Frame(tick++, 0, GameplayButtons.Ultimate), Frame(tick - 1, 0, GameplayButtons.None));
        for (int i = 0; i < 100; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }

        // 5 ticks x 15 damage over the 1.5 s lifetime; the expiry launch is
        // damage-free, and the zone is gone afterwards.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.CurrentHP).IsEqual(hpBeforeCast - TotalUltimateDamage);
        AssertThat(simulation.ZoneCount).IsEqual(0);
    }

    [TestCase]
    public void SingularityPullDragsTheOpponentTowardTheCenter() {
        var simulation = NewSimulation(seed: 63);
        int tick = FillMeterAndSettle(simulation);
        // Open real distance (beyond melee, still inside the 6-unit-wide
        // singularity): the attacker retreats while the opponent idles.
        for (int i = 0; i < 30; i++) {
            simulation.Advance(Frame(tick, -127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }

        simulation.Advance(Frame(tick++, 0, GameplayButtons.Ultimate), Frame(tick - 1, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        var gapBefore = xpTURN.Klotho.Deterministic.Math.FP64.Abs(before.Position.x - zone.Position.x);

        for (int i = 0; i < 30; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        var gapAfter = xpTURN.Klotho.Deterministic.Math.FP64.Abs(after.Position.x - zone.Position.x);
        AssertThat(gapAfter < gapBefore).IsTrue();
    }

    [TestCase]
    public void FinalLaunchCarriesRealImpulse() {
        var simulation = NewSimulation(seed: 64);
        int tick = FillMeterAndSettle(simulation);
        simulation.Advance(Frame(tick++, 0, GameplayButtons.Ultimate), Frame(tick - 1, 0, GameplayButtons.None));
        AssertThat(simulation.ZoneCount).IsEqual(1);

        // Step to the exact expiry frame, then verify the launch hit.
        bool launched = false;
        for (int i = 0; i < 120 && !launched; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
            if (simulation.ZoneCount == 0) {
                AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
                AssertThat(target.HitstunFrames > 0).IsTrue();
                AssertThat(target.IsGrounded).IsEqual(0);
                // UltimateKnockback 6 against weight 1 resolves to force 3 on
                // both axes, blasting the target up and away from the center.
                AssertThat(target.Velocity.y > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();
                launched = true;
            }
        }
        AssertThat(launched).IsTrue();
    }

    [TestCase]
    public void UltimateFiresBeyondMeleeRangeAndBypassesBlock() {
        var simulation = NewSimulation(seed: 65);
        int tick = FillMeterAndSettle(simulation);
        // Retreat beyond the 2-unit generic attack range while staying inside
        // the 6-unit-wide singularity footprint.
        for (int i = 0; i < 30; i++) {
            simulation.Advance(Frame(tick, -127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        var gap = xpTURN.Klotho.Deterministic.Math.FP64.Abs(target.Position.x - attacker.Position.x);
        AssertThat(gap > xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2)).IsTrue();
        int hpBeforeCast = target.CurrentHP;

        // The opponent holds Block for the whole ultimate; the meter is still
        // consumed, the zone still spawns, and every tick still lands.
        var blockHeld = new PlayerInputFrame {
            MoveX = 0, MoveY = 0, Held = GameplayButtons.Block, Pressed = GameplayButtons.None
        };
        simulation.Advance(Frame(tick++, 0, GameplayButtons.Ultimate), blockHeld);
        // The full meter collapsed to the first pulse's fresh 15-damage credit.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent castAttacker)).IsTrue();
        AssertThat(castAttacker.Influence).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(PerTickDamage));
        AssertThat(simulation.ZoneCount).IsEqual(1);
        for (int i = 0; i < 100; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), blockHeld);
            tick++;
        }

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
        uninterrupted.Advance(Frame(tick++, 0, GameplayButtons.Ultimate), Frame(tick - 1, 0, GameplayButtons.None));
        // Snapshot mid-singularity, with ticks and the pull in flight.
        for (int i = 0; i < 30; i++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // Convergence across the remaining ticks, the expiry launch, and beyond.
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
            BaseDamage = 15f,
            IsMultiHit = true,
            HitCount = 5,
            DamageTickIntervalFrames = 18,
            KnockbackForce = new Vector2(6, -4),
            Lifetime = 1.5f
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
