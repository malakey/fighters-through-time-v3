using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W1 — A03 (Fighter half). Roll pressed while airborne is the air
/// dodge: an <c>AirDodge</c> sub-phase of Rolling, 4 startup / 8 invulnerable /
/// 10 recovery, no speed added (a held direction shifts 1.0 unit across the
/// invulnerable frames), once per airtime with the latch on component 320,
/// refreshed by landing, refused under Root. The Story half is
/// <c>tests/Unit/StoryAirDodgeTests.cs</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterAirDodgeTests {

    private const sbyte UpAxis = -127;

    [TestCase]
    public void AnAirborneRollIsAnAirDodgeWithExactlyEightInvulnerableFrames() {
        var simulation = NewSimulation(seed: 1301);
        int tick = JumpAndRise(simulation, 0);
        Advance(simulation, tick, GameplayButtons.Roll);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent started)).IsTrue();
        AssertThat(started.UniversalMovementState).IsEqual((int)UniversalMovementPhase.AirDodgeStartup);
        AssertThat(simulation.TryGetFighterKnockdown(0, out FighterKnockdownComponent latch)).IsTrue();
        AssertThat(latch.AirDodgeUsed).IsEqual(1);

        int invulnerableTicks = 0;
        int sawStates = 0;
        for (int step = 1; step < UniversalMovementRules.AirDodgeTotalFrames + 4; step++) {
            Advance(simulation, tick + step);
            simulation.TryGetFighter(0, out FighterStateComponent fighter);
            simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime);
            if (fighter.InvulnerabilityFrames > 0) invulnerableTicks++;
            if (runtime.UniversalMovementState == (int)UniversalMovementPhase.AirDodgeInvulnerable) sawStates |= 1;
            if (runtime.UniversalMovementState == (int)UniversalMovementPhase.AirDodgeRecovery) sawStates |= 2;
        }
        AssertThat(sawStates).IsEqual(3);
        AssertThat(invulnerableTicks)
            .OverrideFailureMessage("The air dodge grants exactly its 8 invulnerable frames.")
            .IsEqual(UniversalMovementRules.AirDodgeInvulnerableFrames);
        simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent done);
        AssertThat(done.UniversalMovementState).IsEqual((int)UniversalMovementPhase.None);
    }

    [TestCase]
    public void TheDodgeIsOncePerAirtimeAndLandingRefreshesIt() {
        var simulation = NewSimulation(seed: 1302);
        int tick = JumpAndRise(simulation, 0);
        Advance(simulation, tick++, GameplayButtons.Roll);
        for (int step = 0; step < UniversalMovementRules.AirDodgeTotalFrames; step++) Advance(simulation, tick++);
        simulation.TryGetFighter(0, out FighterStateComponent midair);
        if (midair.IsGrounded == 0) {
            Advance(simulation, tick++, GameplayButtons.Roll);
            simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent refused);
            AssertThat(refused.UniversalMovementState)
                .OverrideFailureMessage("A second air dodge in the same airtime must be refused.")
                .IsEqual((int)UniversalMovementPhase.None);
        }
        for (int step = 0; step < 180; step++) {
            simulation.TryGetFighter(0, out FighterStateComponent fighter);
            if (fighter.IsGrounded != 0) break;
            Advance(simulation, tick++);
        }
        simulation.TryGetFighter(0, out FighterStateComponent landed);
        AssertThat(landed.IsGrounded).IsEqual(1);
        simulation.TryGetFighterKnockdown(0, out FighterKnockdownComponent refreshed);
        AssertThat(refreshed.AirDodgeUsed)
            .OverrideFailureMessage("Landing refreshes the air dodge.")
            .IsEqual(0);
    }

    [TestCase]
    public void AHeldDirectionShiftsOneUnitAndAddsNoSpeed() {
        // Two identical jumps; one dodges holding Up, one dodges in neutral.
        // Up never touches air control, so the whole vertical difference after
        // the invulnerable frames is the authored 1.0-unit shift.
        var neutral = NewSimulation(seed: 1303);
        var shifted = NewSimulation(seed: 1303);
        int tick = JumpAndRise(neutral, 0);
        JumpAndRise(shifted, 0);
        Advance(neutral, tick, GameplayButtons.Roll);
        Advance(shifted, tick, GameplayButtons.Roll, moveY: UpAxis);
        int end = tick + UniversalMovementRules.AirDodgeStartupFrames + UniversalMovementRules.AirDodgeInvulnerableFrames;
        for (int step = tick + 1; step <= end; step++) {
            Advance(neutral, step);
            Advance(shifted, step);
        }
        neutral.TryGetFighter(0, out FighterStateComponent a);
        shifted.TryGetFighter(0, out FighterStateComponent b);
        FP64 dy = b.Position.y - a.Position.y;
        AssertThat(FP64.Abs(dy - FP64.One) < FP64.FromDouble(0.01))
            .OverrideFailureMessage($"The held Up must shift exactly 1.0 unit (got {dy}).")
            .IsTrue();
        AssertThat(b.Velocity.y.RawValue)
            .OverrideFailureMessage("The shift is positional: the dodge adds no speed.")
            .IsEqual(a.Velocity.y.RawValue);
    }

    [TestCase]
    public void RootAndAUsedLatchRefuseTheDodge() {
        FighterStateComponent fighter = new() { IsGrounded = 0, Stocks = 3 };
        FighterRuntimeComponent runtime = default;
        FighterKnockdownComponent knockdown = default;
        AssertThat(FighterAirDodgeRules.TryStart(ref fighter, ref runtime, ref knockdown, rooted: true))
            .OverrideFailureMessage("A05: Root refuses the air dodge.")
            .IsFalse();
        AssertThat(knockdown.AirDodgeUsed).IsEqual(0);
        knockdown.AirDodgeUsed = 1;
        AssertThat(FighterAirDodgeRules.TryStart(ref fighter, ref runtime, ref knockdown, rooted: false)).IsFalse();
        knockdown.AirDodgeUsed = 0;
        fighter.IsGrounded = 1;
        AssertThat(FighterAirDodgeRules.TryStart(ref fighter, ref runtime, ref knockdown, rooted: false))
            .OverrideFailureMessage("A grounded Roll is the roll, never the air dodge.")
            .IsFalse();
        fighter.IsGrounded = 0;
        AssertThat(FighterAirDodgeRules.TryStart(ref fighter, ref runtime, ref knockdown, rooted: false)).IsTrue();
        AssertThat(FighterUniversalMovementRules.IsCombatLocked(in runtime)).IsTrue();
    }

    /// <summary>Jumps and advances until the fighter is airborne and a few ticks up.</summary>
    private static int JumpAndRise(FighterSimulation simulation, int startTick) {
        Advance(simulation, startTick, GameplayButtons.Jump);
        int tick = startTick + 1;
        for (; tick < startTick + 6; tick++) Advance(simulation, tick);
        simulation.TryGetFighter(0, out FighterStateComponent fighter);
        AssertThat(fighter.IsGrounded).OverrideFailureMessage("setup: the jump must leave the ground.").IsEqual(0);
        return tick;
    }

    private static FighterSimulation NewSimulation(int seed) => new(
        FighterCharacterID.Tesla,
        FighterCharacterID.Joan,
        seed: seed,
        spawnDistance: 6,
        rules: FighterMatchRules.Disabled);

    private static void Advance(
        FighterSimulation simulation, int tick,
        GameplayButtons pressed = GameplayButtons.None, sbyte moveY = 0) =>
        simulation.Advance(
            new PlayerInputFrame { Tick = (uint)tick, MoveY = moveY, Held = pressed, Pressed = pressed },
            new PlayerInputFrame { Tick = (uint)tick });
}
