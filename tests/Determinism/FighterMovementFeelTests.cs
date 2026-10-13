using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The 2026-10-08 movement feel pass, Fighter-sim half (user direction, ledger
/// <c>USER-MOVEMENT-FEEL</c>): a 6-frame run ramp and a 4-frame stop that land
/// on their exact tick for every roster speed, the apex hang after a jump (and
/// none after a ledge drop), the heavier fall and its 12 u/s cap, the
/// fast-fall that ramps in under heavier gravity from any point in the air —
/// mid-rise included — to a 15 u/s cap and eases back down when released,
/// launch gravity that stays flat with its 20 u/s clamp, and rollback and hash
/// repeatability across a jump and fast-fall script.
/// </summary>
/// <remarks>
/// Every live arc is checked tick for tick against
/// <see cref="FighterAirPhysics.StepMovement"/> / <see cref="FighterAirPhysics.StepLaunch"/>
/// (proof that the movement system calls the shared rule with the right flags),
/// and the rule itself is pinned by its effect: the step sizes are rebuilt here
/// from the <see cref="UniversalMovementRules"/> constants and the sim's flat
/// −30 u/s², never read back out of the helper.
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class FighterMovementFeelTests {
    /// <summary>One base-gravity tick, rebuilt the way the sim builds it: −30 × 1/60.</summary>
    private static readonly FP64 BaseStep = FP64.FromInt(-30) * (FP64.One / FP64.FromInt(60));
    private static readonly FP64 HangStep = BaseStep * FP64.FromDouble(UniversalMovementRules.ApexHangGravityMultiplier);
    private static readonly FP64 FallStep = BaseStep * FP64.FromDouble(UniversalMovementRules.FallGravityMultiplier);
    private static readonly FP64 FastFallStep = BaseStep * FP64.FromDouble(UniversalMovementRules.FastFallGravityMultiplier);
    private static readonly FP64 Band = FP64.FromDouble(UniversalMovementRules.ApexHangSpeedBand);
    private static readonly FP64 Terminal = -FP64.FromInt(12);
    private static readonly FP64 FastFallCap = -FP64.FromInt(15);
    private static readonly FP64 LaunchClamp = -FP64.FromInt(20);

    [TestCase]
    public void TheSharedConstantsCarryTheUserRuling() {
        AssertThat(UniversalMovementRules.RunAccelerationFrames).IsEqual(6);
        AssertThat(UniversalMovementRules.RunDecelerationFrames).IsEqual(4);
        AssertThat(UniversalMovementRules.TerminalFallSpeed).IsEqual(12f);
        AssertThat(UniversalMovementRules.FastFallSpeed).IsEqual(15f);
        AssertThat(UniversalMovementRules.LaunchTerminalFallSpeed).IsEqual(20f);
        // The sim's caps are those constants, signed (Y up).
        AssertThat(FighterAirPhysics.TerminalFallVelocity.RawValue).IsEqual(Terminal.RawValue);
        AssertThat(FighterAirPhysics.FastFallVelocity.RawValue).IsEqual(FastFallCap.RawValue);
        AssertThat(FighterAirPhysics.LaunchTerminalFallVelocity.RawValue).IsEqual(LaunchClamp.RawValue);
        // Half gravity in the band, heavier while falling, heavier still fast-falling.
        AssertThat(FP64.Abs(HangStep * FP64.FromInt(2) - BaseStep).RawValue <= 1).IsTrue();
        AssertThat(FallStep < BaseStep).IsTrue();
        AssertThat(FastFallStep < FallStep).IsTrue();
    }

    /// <summary>
    /// The grounded ramps land on their exact tick: full speed on tick 6 and a
    /// standstill on tick 4 — not a tick late — for speeds whose division by
    /// six or four is inexact in fixed point (7.25, 7.75, 4.75, the default 7)
    /// as well as exact ones (6.0, 6.75).
    /// </summary>
    [TestCase]
    public void TheGroundedRunReachesFullSpeedOnTickSixAndStopsOnTickFourAtEveryRosterSpeed() {
        var loadouts = new List<(string Label, FighterLoadout Loadout)> {
            ("default", FighterLoadout.Default(FighterCharacterID.Einstein))
        };
        foreach (string characterID in new[] { "lincoln", "tesla", "einstein", "cleopatra", "joan" }) {
            loadouts.Add((characterID, LoadoutFor(characterID)));
        }

        foreach ((string label, FighterLoadout loadout) in loadouts) {
            var simulation = new FighterSimulation(
                loadout, FighterLoadout.Default(FighterCharacterID.Joan),
                spawnDistance: 8, rules: FighterMatchRules.Disabled);
            FP64 full = loadout.MoveSpeed;
            int tick = 0;
            for (int frame = 1; frame <= UniversalMovementRules.RunAccelerationFrames; frame++, tick++) {
                simulation.Advance(Frame(tick, 127), Frame(tick, 0));
                AssertThat(simulation.TryGetFighter(0, out FighterStateComponent running)).IsTrue();
                if (frame < UniversalMovementRules.RunAccelerationFrames) {
                    AssertThat(running.Velocity.x < full)
                        .OverrideFailureMessage($"{label}: the run ramp topped out early, on frame {frame}.")
                        .IsTrue();
                } else {
                    AssertThat(running.Velocity.x.RawValue)
                        .OverrideFailureMessage($"{label}: the run ramp missed full speed on frame {frame}.")
                        .IsEqual(full.RawValue);
                }
            }
            for (int frame = 1; frame <= UniversalMovementRules.RunDecelerationFrames; frame++, tick++) {
                simulation.Advance(Frame(tick, 0), Frame(tick, 0));
                AssertThat(simulation.TryGetFighter(0, out FighterStateComponent stopping)).IsTrue();
                if (frame < UniversalMovementRules.RunDecelerationFrames) {
                    AssertThat(stopping.Velocity.x > FP64.Zero)
                        .OverrideFailureMessage($"{label}: the stop ramp ended early, on frame {frame}.")
                        .IsTrue();
                } else {
                    AssertThat(stopping.Velocity.x.RawValue)
                        .OverrideFailureMessage($"{label}: the stop ramp was still moving on frame {frame}.")
                        .IsEqual(FP64.Zero.RawValue);
                }
            }
        }
    }

    /// <summary>
    /// A jump floats at its top: inside the ±2 u/s band gravity is halved, so
    /// the 4 u/s of velocity across it take at least twice the ticks full
    /// gravity would (14 rather than 7, give or take a fixed-point boundary
    /// tick). A ledge drop has not jumped this airtime and gets no hang: its
    /// first in-band ticks run at full and then fall gravity.
    /// </summary>
    [TestCase]
    public void AJumpHangsAtItsApexButALedgeDropDoesNot() {
        var simulation = new FighterSimulation(spawnDistance: 8, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, 0, GameplayButtons.Jump), Frame(0, 0));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent takeOff)).IsTrue();
        // The jump tick itself integrates one full-gravity step from the jump speed.
        AssertThat(takeOff.Velocity.y.RawValue).IsEqual((FP64.FromDouble(11.5) + BaseStep).RawValue);
        FP64 previous = takeOff.Velocity.y;
        int hangTicks = 0;
        for (int tick = 1; tick < 120; tick++) {
            simulation.Advance(Frame(tick, 0), Frame(tick, 0));
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
            if (fighter.IsGrounded != 0) break;
            FP64 expected = FighterAirPhysics.StepMovement(
                previous, fastFalling: false, floating: false, jumpedThisAirtime: true);
            AssertThat(fighter.Velocity.y.RawValue).IsEqual(expected.RawValue);
            if (FP64.Abs(previous) < Band) {
                hangTicks++;
                AssertThat((fighter.Velocity.y - previous).RawValue)
                    .OverrideFailureMessage($"tick {tick}: an in-band tick after a jump must run at half gravity.")
                    .IsEqual(HangStep.RawValue);
            }
            previous = fighter.Velocity.y;
        }
        AssertThat(hangTicks >= 13)
            .OverrideFailureMessage($"the apex hang lasted {hangTicks} ticks; half gravity across ±2 u/s is about 14.")
            .IsTrue();

        // The ledge drop: climb onto Florence's left gear platform, walk off its
        // right end (the ledge catches on the same tick), drop with one tick of
        // Down, then fall with no input.
        var florence = new FighterSimulation(
            seed: 4111, rules: FighterMatchRules.Disabled, stageGeometry: FighterStageGeometry.Florence);
        int at = RiseOntoPlatform(florence, FP64.FromDouble(2.4), 0);
        AssertThat(at >= 0).OverrideFailureMessage("never reached the Florence gear platform.").IsTrue();
        bool hung = false;
        for (at++; at < 400 && !hung; at++) {
            florence.Advance(Frame(at, 127), Frame(at, 0));
            florence.TryGetFighterRuntime(0, out FighterRuntimeComponent walking);
            hung = FighterLedgeRules.IsHanging(in walking);
        }
        AssertThat(hung).OverrideFailureMessage("walking off the platform end never caught its ledge.").IsTrue();
        florence.Advance(Frame(at, 0, GameplayButtons.Down), Frame(at, 0));
        at++;
        AssertThat(florence.TryGetFighter(0, out FighterStateComponent dropped)).IsTrue();
        AssertThat(florence.TryGetFighterRuntime(0, out FighterRuntimeComponent droppedRuntime)).IsTrue();
        AssertThat(FighterLedgeRules.IsHanging(in droppedRuntime)).IsFalse();
        AssertThat(dropped.RemainingJumps)
            .OverrideFailureMessage("a ledge drop leaves the refilled jump budget untouched — nothing was jumped.")
            .IsEqual(FighterLoadout.Default(FighterCharacterID.Einstein).MaxJumpCount);

        previous = dropped.Velocity.y;
        int inBandTicks = 0;
        for (; at < 500; at++) {
            florence.Advance(Frame(at, 0), Frame(at, 0));
            AssertThat(florence.TryGetFighter(0, out FighterStateComponent falling)).IsTrue();
            if (falling.IsGrounded != 0) break;
            FP64 expected = FighterAirPhysics.StepMovement(
                previous, fastFalling: false, floating: false, jumpedThisAirtime: false);
            AssertThat(falling.Velocity.y.RawValue).IsEqual(expected.RawValue);
            if (FP64.Abs(previous) < Band) {
                inBandTicks++;
                FP64 step = falling.Velocity.y - previous;
                FP64 unhung = previous < FP64.Zero ? FallStep : BaseStep;
                AssertThat(step.RawValue)
                    .OverrideFailureMessage($"tick {at}: a ledge drop must not hang (step {step}).")
                    .IsEqual(unhung.RawValue);
            }
            previous = falling.Velocity.y;
        }
        AssertThat(inBandTicks > 0).OverrideFailureMessage("the drop never crossed the hang band.").IsTrue();
    }

    /// <summary>
    /// A ledge climb jump is a jump: the grab refills the jumps, the climb spends
    /// the ground jump (<c>MaxJumpCount − 1</c>), and its arc hangs at the apex
    /// even when the ledge was caught from a walk-off with nothing jumped before
    /// it — the same rule Story's ledge and rope jump-offs follow.
    /// </summary>
    [TestCase]
    public void ALedgeClimbJumpHangsAtItsApexAfterAWalkOffCapture() {
        var florence = new FighterSimulation(
            seed: 4111, rules: FighterMatchRules.Disabled, stageGeometry: FighterStageGeometry.Florence);
        int maxJumps = FighterLoadout.Default(FighterCharacterID.Einstein).MaxJumpCount;
        int at = RiseOntoPlatform(florence, FP64.FromDouble(2.4), 0);
        AssertThat(at >= 0).OverrideFailureMessage("never reached the Florence gear platform.").IsTrue();
        bool hung = false;
        for (at++; at < 400 && !hung; at++) {
            florence.Advance(Frame(at, 127), Frame(at, 0));
            florence.TryGetFighterRuntime(0, out FighterRuntimeComponent walking);
            hung = FighterLedgeRules.IsHanging(in walking);
        }
        AssertThat(hung).OverrideFailureMessage("walking off the platform end never caught its ledge.").IsTrue();
        AssertThat(florence.TryGetFighter(0, out FighterStateComponent hanging)).IsTrue();
        AssertThat(hanging.RemainingJumps).IsEqual(maxJumps);

        florence.Advance(Frame(at, 0, GameplayButtons.Jump), Frame(at, 0));
        at++;
        AssertThat(florence.TryGetFighter(0, out FighterStateComponent climbing)).IsTrue();
        AssertThat(florence.TryGetFighterRuntime(0, out FighterRuntimeComponent climbingRuntime)).IsTrue();
        AssertThat(FighterLedgeRules.IsHanging(in climbingRuntime)).IsFalse();
        AssertThat(climbing.RemainingJumps)
            .OverrideFailureMessage("the climb jump spends the ground jump, as a jump off the floor does.")
            .IsEqual(maxJumps - 1);
        AssertThat(climbing.Velocity.y > FP64.Zero).OverrideFailureMessage("the climb jump is not rising.").IsTrue();

        FP64 previous = climbing.Velocity.y;
        int hangTicks = 0;
        for (; at < 500; at++) {
            florence.Advance(Frame(at, 0), Frame(at, 0));
            AssertThat(florence.TryGetFighter(0, out FighterStateComponent air)).IsTrue();
            AssertThat(florence.TryGetFighterRuntime(0, out FighterRuntimeComponent airRuntime)).IsTrue();
            if (air.IsGrounded != 0 || FighterLedgeRules.IsHanging(in airRuntime)) break;
            FP64 expected = FighterAirPhysics.StepMovement(
                previous, fastFalling: false, floating: false, jumpedThisAirtime: true);
            AssertThat(air.Velocity.y.RawValue).IsEqual(expected.RawValue);
            if (FP64.Abs(previous) < Band) {
                hangTicks++;
                AssertThat((air.Velocity.y - previous).RawValue)
                    .OverrideFailureMessage($"tick {at}: an in-band tick after a climb jump must run at half gravity.")
                    .IsEqual(HangStep.RawValue);
            }
            previous = air.Velocity.y;
        }
        AssertThat(hangTicks >= 13)
            .OverrideFailureMessage($"the climb jump's hang lasted {hangTicks} ticks; half gravity across ±2 u/s is about 14.")
            .IsTrue();
    }

    /// <summary>
    /// Below the band a fall runs at <see cref="UniversalMovementRules.FallGravityMultiplier"/>
    /// times gravity until it reaches the 12 u/s cap, then holds exactly there.
    /// A plain jump on the flat arena is high enough to get there before landing.
    /// </summary>
    [TestCase]
    public void AFallRunsAtTheFallMultiplierUntilTheTwelveUnitTerminal() {
        var simulation = new FighterSimulation(spawnDistance: 8, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, 0, GameplayButtons.Jump), Frame(0, 0));
        simulation.TryGetFighter(0, out FighterStateComponent takeOff);
        FP64 previous = takeOff.Velocity.y;
        int fallTicks = 0;
        int terminalTicks = 0;
        for (int tick = 1; tick < 120; tick++) {
            simulation.Advance(Frame(tick, 0), Frame(tick, 0));
            simulation.TryGetFighter(0, out FighterStateComponent fighter);
            if (fighter.IsGrounded != 0) break;
            FP64 velocity = fighter.Velocity.y;
            AssertThat(velocity >= Terminal)
                .OverrideFailureMessage($"tick {tick}: the fall passed the 12 u/s terminal ({velocity}).")
                .IsTrue();
            if (previous <= -Band && previous > Terminal) {
                fallTicks++;
                FP64 expected = previous + FallStep < Terminal ? Terminal : previous + FallStep;
                AssertThat(velocity.RawValue).IsEqual(expected.RawValue);
            }
            if (velocity.RawValue == Terminal.RawValue) terminalTicks++;
            previous = velocity;
        }
        AssertThat(fallTicks > 0).IsTrue();
        AssertThat(terminalTicks > 0)
            .OverrideFailureMessage("a full jump's descent never reached the 12 u/s terminal.")
            .IsTrue();
    }

    /// <summary>
    /// Fast-fall from mid-rise: Down held on the way up does not snap — the
    /// first tick takes one heavier-gravity step and the fighter is still
    /// rising. Held on, the descent builds to exactly 15 u/s and stays there.
    /// Released while faster than 12 u/s, the fall eases back down by one base
    /// gravity step a tick instead of snapping to the terminal.
    /// </summary>
    [TestCase]
    public void FastFallRampsInFromMidRiseCapsAtFifteenAndEasesBackOnRelease() {
        var simulation = new FighterSimulation(spawnDistance: 8, rules: FighterMatchRules.Disabled);
        int tick = 0;
        simulation.Advance(Frame(tick, 0, GameplayButtons.Jump), Frame(tick, 0));
        // Height first: the double jump at the apex gives the fall room to reach the cap.
        FighterStateComponent fighter;
        for (tick++; tick < 120; tick++) {
            simulation.TryGetFighter(0, out fighter);
            if (fighter.Velocity.y <= FP64.Zero) break;
            simulation.Advance(Frame(tick, 0), Frame(tick, 0));
        }
        simulation.Advance(Frame(tick, 0, GameplayButtons.Jump), Frame(tick, 0));
        for (int rise = 0; rise < 8; rise++) {
            tick++;
            simulation.Advance(Frame(tick, 0), Frame(tick, 0));
        }
        simulation.TryGetFighter(0, out fighter);
        AssertThat(fighter.RemainingJumps).IsEqual(0);
        FP64 previous = fighter.Velocity.y;
        AssertThat(previous > Band).OverrideFailureMessage("the second jump must still be rising.").IsTrue();

        tick++;
        simulation.Advance(Frame(tick, 0, GameplayButtons.Down), Frame(tick, 0));
        simulation.TryGetFighter(0, out fighter);
        AssertThat(fighter.Velocity.y.RawValue)
            .OverrideFailureMessage("the first fast-fall tick must take one heavier-gravity step, not snap.")
            .IsEqual((previous + FastFallStep).RawValue);
        AssertThat(fighter.Velocity.y > FP64.Zero)
            .OverrideFailureMessage("one tick of Down must not end the rise outright.")
            .IsTrue();

        int rampTicks = 1;
        previous = fighter.Velocity.y;
        while (previous > FastFallCap) {
            tick++;
            AssertThat(rampTicks < 60).OverrideFailureMessage("the fast-fall never reached its cap.").IsTrue();
            simulation.Advance(Frame(tick, 0, GameplayButtons.Down), Frame(tick, 0));
            simulation.TryGetFighter(0, out fighter);
            AssertThat(fighter.IsGrounded).OverrideFailureMessage("landed before the cap.").IsEqual(0);
            FP64 expected = previous + FastFallStep < FastFallCap ? FastFallCap : previous + FastFallStep;
            AssertThat(fighter.Velocity.y.RawValue).IsEqual(expected.RawValue);
            previous = fighter.Velocity.y;
            rampTicks++;
        }
        // About fifteen ticks from mid-rise to the cap, where the old rule snapped on one.
        AssertThat(rampTicks > 10).IsTrue();

        tick++;
        simulation.Advance(Frame(tick, 0, GameplayButtons.Down), Frame(tick, 0));
        simulation.TryGetFighter(0, out fighter);
        AssertThat(fighter.Velocity.y.RawValue)
            .OverrideFailureMessage("held Down must stay exactly on the 15 u/s cap.")
            .IsEqual(FastFallCap.RawValue);

        // Release: ease from 15 toward 12 by one base step a tick.
        previous = fighter.Velocity.y;
        int easeTicks = 0;
        while (previous < Terminal) {
            tick++;
            simulation.Advance(Frame(tick, 0), Frame(tick, 0));
            simulation.TryGetFighter(0, out fighter);
            AssertThat(fighter.IsGrounded).OverrideFailureMessage("landed before the ease finished.").IsEqual(0);
            FP64 eased = previous - BaseStep;
            FP64 expected = eased < Terminal ? eased : Terminal;
            AssertThat(fighter.Velocity.y.RawValue)
                .OverrideFailureMessage($"release tick {easeTicks + 1} must ease by one base step, not snap.")
                .IsEqual(expected.RawValue);
            previous = fighter.Velocity.y;
            easeTicks++;
        }
        // 3 u/s at half a unit per tick is six ticks; the fixed-point base step
        // sits a few raw units under half a unit, so the last one can spill
        // into a seventh. Either way the release never snaps.
        AssertThat(easeTicks >= 6 && easeTicks <= 7)
            .OverrideFailureMessage($"the release eased over {easeTicks} ticks.")
            .IsTrue();
    }

    /// <summary>
    /// Launch gravity is untouched: in hitstun every airborne tick takes the
    /// flat base step (no apex hang, no fall multiplier, no fast-fall even
    /// with Down held), and the launch clamp is the old 20 u/s.
    /// </summary>
    [TestCase]
    public void HitstunKeepsFlatGravityAndTheTwentyUnitLaunchClamp() {
        AssertThat(FighterAirPhysics.StepLaunch(-FP64.FromInt(35)).RawValue).IsEqual(LaunchClamp.RawValue);
        AssertThat(FighterAirPhysics.StepLaunch(-FP64.FromDouble(19.8)).RawValue).IsEqual(LaunchClamp.RawValue);
        AssertThat(FighterAirPhysics.StepLaunch(-FP64.FromInt(15)).RawValue)
            .IsEqual((-FP64.FromInt(15) + BaseStep).RawValue);
        AssertThat(FighterAirPhysics.StepLaunch(FP64.FromInt(1)).RawValue)
            .IsEqual((FP64.FromInt(1) + BaseStep).RawValue);

        var stunned = new FighterSimulation(spawnDistance: 1, rules: FighterMatchRules.Disabled);
        int flatTicks = 0;
        FighterStateComponent before = default;
        bool haveBefore = false;
        for (int tick = 0; tick < 160; tick++) {
            stunned.Advance(
                Frame(tick, 0, tick % 2 == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None),
                Frame(tick, 0, GameplayButtons.Down));
            AssertThat(stunned.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            // A tick that only advanced the stun clock (no hitstop, no new hit) and
            // stayed airborne on both sides is a pure gravity tick.
            if (haveBefore
                && before.IsGrounded == 0 && victim.IsGrounded == 0
                && before.HitstunFrames > 1 && victim.HitstunFrames == before.HitstunFrames - 1) {
                flatTicks++;
                AssertThat(victim.Velocity.y.RawValue)
                    .OverrideFailureMessage($"tick {tick}: hitstun must keep flat launch gravity.")
                    .IsEqual(FighterAirPhysics.StepLaunch(before.Velocity.y).RawValue);
                AssertThat(victim.Velocity.y.RawValue).IsEqual((before.Velocity.y + BaseStep).RawValue);
            }
            before = victim;
            haveBefore = true;
        }
        AssertThat(flatTicks > 0)
            .OverrideFailureMessage("the string never put the victim into airborne hitstun.")
            .IsTrue();
    }

    /// <summary>
    /// The air model adds no state: a jump, double jump, fast-fall and release
    /// script replays to identical hashes in two simulations, and a remote
    /// fighter's mispredicted Down press inside a rollback window resimulates
    /// back onto the authoritative hash.
    /// </summary>
    [TestCase]
    public void AJumpAndFastFallScriptRepeatsAndConvergesUnderRollback() {
        List<(PlayerInputFrame One, PlayerInputFrame Two)> script = AirScript();
        var authoritative = new FighterSimulation(seed: 1008, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        var replay = new FighterSimulation(seed: 1008, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        var predicted = new FighterSimulation(seed: 1008, spawnDistance: 6, rules: FighterMatchRules.Disabled);

        // The window opens on the remote fighter's first held Down mid-rise.
        const int predictFrom = 34;
        const int predictTo = predictFrom + 8;
        for (int tick = 0; tick < script.Count; tick++) {
            long hash = authoritative.Advance(script[tick].One, script[tick].Two);
            AssertThat(replay.Advance(script[tick].One, script[tick].Two)).IsEqual(hash);
            if (tick >= predictFrom && tick < predictTo) predicted.AdvanceWithPredictedPlayerTwo(script[tick].One);
            else predicted.Advance(script[tick].One, script[tick].Two);
            if (tick != predictTo) continue;
            for (int corrected = predictFrom; corrected < predictTo; corrected++) {
                AssertThat(predicted.CorrectPlayerTwoInput(corrected, script[corrected].Two)).IsTrue();
            }
            AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
        }
        AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
    }

    /// <summary>
    /// Player two jumps, double-jumps at the apex, holds Down from mid-rise of
    /// the second jump and lets go before landing; player one runs and hops.
    /// </summary>
    private static List<(PlayerInputFrame One, PlayerInputFrame Two)> AirScript() {
        var script = new List<(PlayerInputFrame, PlayerInputFrame)>();
        for (int tick = 0; tick < 150; tick++) {
            GameplayButtons two = tick switch {
                10 => GameplayButtons.Jump,
                30 => GameplayButtons.Jump,
                >= 34 and < 60 => GameplayButtons.Down,
                _ => GameplayButtons.None
            };
            GameplayButtons one = tick % 40 == 5 ? GameplayButtons.Jump : GameplayButtons.None;
            sbyte oneAxis = tick < 70 ? (sbyte)90 : (sbyte)-90;
            script.Add((Frame(tick, oneAxis, one), Frame(tick, 0, two)));
        }
        return script;
    }

    /// <summary>
    /// Climbs player one onto the surface directly overhead, spending the air
    /// jump the instant the first stalls, and returns the tick it came to rest
    /// there (−1 if it never did).
    /// </summary>
    private static int RiseOntoPlatform(FighterSimulation simulation, FP64 surfaceY, int startTick) {
        bool spentAirJump = false;
        for (int tick = startTick; tick < startTick + 200; tick++) {
            simulation.TryGetFighter(0, out FighterStateComponent fighter);
            if (fighter.IsGrounded != 0 && fighter.Position.y.RawValue == surfaceY.RawValue) return tick;
            GameplayButtons buttons = GameplayButtons.None;
            if (fighter.IsGrounded != 0) {
                spentAirJump = false;
                buttons = GameplayButtons.Jump;
            } else if (!spentAirJump && fighter.Velocity.y < FP64.Zero && fighter.RemainingJumps > 0) {
                buttons = GameplayButtons.Jump;
                spentAirJump = true;
            }
            simulation.Advance(Frame(tick, 0, buttons), Frame(tick, 0));
        }
        return -1;
    }

    private static FighterLoadout LoadoutFor(string characterID) {
        CharacterData data = AuthoredResources.Load<CharacterData>(
            $"res://resources/Characters/{characterID}_data.tres");
        return FighterLoadoutFactory.FromCharacterData(data);
    }

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons buttons = GameplayButtons.None) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = buttons,
        Pressed = buttons
    };
}
