using System;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// CM-MOVE (combat and movement update 2026-10-16, decision 6.2 with the Q6
/// ruling; plan §2.6.2; ledger <c>PROVISIONAL-SIM-LIP-POP</c>): the Fighter
/// sim's one-way lip pop. A normal-control airborne fighter at its apex or
/// falling, whose feet sit at most 0.2 u under a one-way top and over it, is
/// placed on the top and lands; it resolves before the ledge capture, so a
/// near-edge capture inside the band becomes a landing; a fighter dropping
/// through is never popped back up; and it adds no state, so a pop script
/// converges under rollback.
/// </summary>
/// <remarks>
/// Pure C#: the default loadouts, hand-built and <see cref="PlayerInputFrame.Create"/>
/// frames, and <see cref="FighterStageGeometry.ForTest"/> geometry whose
/// platform top is placed relative to a measured apex. Each case first flies
/// the same input on a geometry whose platform is far out of reach, records
/// the tick the vertical velocity turns (the apex tick) and the position
/// there, then puts the one-way top a known distance above it.
/// </remarks>
[TestSuite]
public class FighterLipPopTests {
    private static readonly FP64 Band = FP64.FromDouble(UniversalMovementRules.SimOneWayLipPopUnits);

    /// <summary>
    /// The band edge, with both input styles: a top 0.19 u above the apex feet
    /// is popped onto on the apex tick (grounded, feet on the top, jumps
    /// refilled); a top 0.21 u above is not, and the fighter falls back to the
    /// floor without ever standing on it.
    /// </summary>
    [TestCase]
    public void AFighterPointNineteenUnderAOneWayTopAtItsApexLandsOnItAndPointTwentyOneDoesNot() {
        AssertThat(Band.RawValue).IsEqual(FP64.FromDouble(0.2).RawValue);
        foreach (bool createBuilt in new[] { false, true }) {
            Func<int, PlayerInputFrame> jumper = tick => JumpInput(tick, moveX: 0, createBuilt);
            Turn turn = MeasureTurn(playerID: 0, platformCenterX: FP64.FromInt(-4), jumper);
            string label = createBuilt ? "Create-built" : "hand-built";

            FighterStageGeometry popGeometry = Geometry(FP64.FromInt(-4), turn.Y + FP64.FromDouble(0.19), FP64.One);
            var pop = new FighterSimulation(seed: 77, rules: FighterMatchRules.Disabled, stageGeometry: popGeometry);
            FighterStateComponent atTurn = AdvanceTo(pop, turn.Tick, jumper);
            AssertThat(atTurn.IsGrounded)
                .OverrideFailureMessage($"{label}: 0.19 u under the top at the apex tick must land on it.")
                .IsEqual(1);
            AssertThat(atTurn.Position.y.RawValue).IsEqual(popGeometry.Platforms[0].SurfaceY.RawValue);
            AssertThat(atTurn.Velocity.y.RawValue).IsEqual(0);
            AssertThat(atTurn.RemainingJumps).IsEqual(FighterLoadout.Default(FighterCharacterID.Einstein).MaxJumpCount);
            // One tick earlier it was still rising inside the band: no pop.
            var pre = new FighterSimulation(seed: 77, rules: FighterMatchRules.Disabled, stageGeometry: popGeometry);
            AssertThat(AdvanceTo(pre, turn.Tick - 1, jumper).IsGrounded)
                .OverrideFailureMessage($"{label}: a rising fighter inside the band is never popped.")
                .IsEqual(0);

            FighterStageGeometry missGeometry = Geometry(FP64.FromInt(-4), turn.Y + FP64.FromDouble(0.21), FP64.One);
            var miss = new FighterSimulation(seed: 77, rules: FighterMatchRules.Disabled, stageGeometry: missGeometry);
            FP64 surface = missGeometry.Platforms[0].SurfaceY;
            bool stoodOnTop = false;
            bool landedOnFloor = false;
            for (int tick = 0; tick < 200 && !landedOnFloor; tick++) {
                miss.Advance(jumper(tick), Idle(tick));
                AssertThat(miss.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
                if (fighter.IsGrounded != 0 && fighter.Position.y.RawValue == surface.RawValue) stoodOnTop = true;
                if (tick > 2 && fighter.IsGrounded != 0 && fighter.Position.y.RawValue == FP64.Zero.RawValue) landedOnFloor = true;
            }
            AssertThat(stoodOnTop).OverrideFailureMessage($"{label}: 0.21 u under the top must not pop.").IsFalse();
            AssertThat(landedOnFloor).OverrideFailureMessage($"{label}: the missed apex never fell back to the floor.").IsTrue();
        }
    }

    /// <summary>
    /// The order before the ledge capture: a fighter drifting toward a
    /// platform's right end reaches its apex 0.45 u inside the end (inside the
    /// ledge's capture box, outside it the tick before). Inside the band it
    /// lands on the top; with the top 0.21 u up, outside the band, the same
    /// apex catches the ledge instead.
    /// </summary>
    [TestCase]
    public void AFighterInsideTheBandAtANearEdgeApexLandsRatherThanGrabbingTheLedge() {
        Func<int, PlayerInputFrame> drifter = tick => JumpInput(tick, moveX: 127, createBuilt: true);
        Turn turn = MeasureTurn(playerID: 0, platformCenterX: FP64.FromInt(-4), drifter);
        FP64 edge = turn.X + FP64.FromDouble(0.45);
        AssertThat(turn.PreviousX < edge - FighterLedgeRules.CaptureHalfWidth)
            .OverrideFailureMessage("the drift must enter the capture box on the apex tick, not before.")
            .IsTrue();
        FP64 halfWidth = FP64.FromDouble(1.5);
        FP64 center = edge - halfWidth;

        FighterStageGeometry popGeometry = Geometry(center, turn.Y + FP64.FromDouble(0.15), halfWidth);
        var pop = new FighterSimulation(seed: 78, rules: FighterMatchRules.Disabled, stageGeometry: popGeometry);
        FighterStateComponent landed = AdvanceTo(pop, turn.Tick, drifter);
        AssertThat(pop.TryGetFighterRuntime(0, out FighterRuntimeComponent landedRuntime)).IsTrue();
        AssertThat(landed.IsGrounded)
            .OverrideFailureMessage("inside the band, the near-edge apex must land on the top.")
            .IsEqual(1);
        AssertThat(FighterLedgeRules.IsHanging(in landedRuntime))
            .OverrideFailureMessage("the landing must win over the capture.")
            .IsFalse();

        FighterStageGeometry grabGeometry = Geometry(center, turn.Y + FP64.FromDouble(0.21), halfWidth);
        var grab = new FighterSimulation(seed: 78, rules: FighterMatchRules.Disabled, stageGeometry: grabGeometry);
        FighterStateComponent caught = AdvanceTo(grab, turn.Tick, drifter);
        AssertThat(grab.TryGetFighterRuntime(0, out FighterRuntimeComponent caughtRuntime)).IsTrue();
        AssertThat(caught.IsGrounded).IsEqual(0);
        AssertThat(caughtRuntime.LedgeAnchor)
            .OverrideFailureMessage("outside the band the same apex must catch the right end's ledge.")
            .IsEqual(1);
    }

    /// <summary>
    /// A fighter that drops through a one-way platform starts its fall inside
    /// the band under the top, falling, and is never popped back up (the
    /// drop-through window exempts it); and a script of a lip-pop jump and a
    /// drop-through, played by player two, replays to identical hashes and
    /// converges when a rollback window straddles the pop.
    /// </summary>
    [TestCase]
    public void ADroppingFighterIsNeverPoppedBackAndAPopScriptConvergesUnderRollback() {
        // (a) Standing on a low top, Down + Jump drops through it.
        FighterStageGeometry low = Geometry(FP64.FromInt(-4), FP64.FromDouble(1.5), FP64.FromDouble(1.5));
        var drop = new FighterSimulation(seed: 79, rules: FighterMatchRules.Disabled, stageGeometry: low);
        FP64 surface = low.Platforms[0].SurfaceY;
        int tick = 0;
        bool onTop = false;
        for (; tick < 200 && !onTop; tick++) {
            drop.Advance(Hand(tick, 0, tick == 0 ? GameplayButtons.Jump : GameplayButtons.None,
                tick < 30 ? GameplayButtons.Jump : GameplayButtons.None), Idle(tick));
            drop.TryGetFighter(0, out FighterStateComponent fighter);
            onTop = tick > 2 && fighter.IsGrounded != 0 && fighter.Position.y.RawValue == surface.RawValue;
        }
        AssertThat(onTop).OverrideFailureMessage("the jump never came down onto the low top.").IsTrue();
        drop.Advance(Hand(tick, 0, GameplayButtons.Jump | GameplayButtons.Down, GameplayButtons.Jump | GameplayButtons.Down), Idle(tick));
        tick++;
        drop.TryGetFighter(0, out FighterStateComponent dropping);
        AssertThat(dropping.IsGrounded).IsEqual(0);
        AssertThat(dropping.DropThroughFrames > 0).IsTrue();
        AssertThat(surface - dropping.Position.y <= Band)
            .OverrideFailureMessage("the drop must start inside the band, where a pop would put it back.")
            .IsTrue();
        bool backOnTop = false;
        bool onFloor = false;
        for (int step = 0; step < 90 && !onFloor; step++, tick++) {
            drop.Advance(Idle(tick), Idle(tick));
            drop.TryGetFighter(0, out FighterStateComponent falling);
            if (falling.IsGrounded != 0 && falling.Position.y.RawValue == surface.RawValue) backOnTop = true;
            onFloor = falling.IsGrounded != 0 && falling.Position.y.RawValue == FP64.Zero.RawValue;
        }
        AssertThat(backOnTop).OverrideFailureMessage("a dropping fighter must never be popped back onto the top.").IsFalse();
        AssertThat(onFloor).OverrideFailureMessage("the drop never reached the floor.").IsTrue();

        // (b) Player two: a lip-pop jump onto a top over its spawn, then a drop.
        Func<int, PlayerInputFrame> jumper = t => JumpInput(t, moveX: 0, createBuilt: true);
        Turn turn = MeasureTurn(playerID: 1, platformCenterX: FP64.FromInt(4), jumper);
        FighterStageGeometry popGeometry = Geometry(FP64.FromInt(4), turn.Y + FP64.FromDouble(0.1), FP64.One);
        int dropAt = turn.Tick + 20;
        PlayerInputFrame Two(int t) => t == dropAt
            ? PlayerInputFrame.Create((uint)t, 0f, 1f, GameplayButtons.Jump | GameplayButtons.Down)
            : t > dropAt ? Idle(t) : jumper(t);

        var authoritative = new FighterSimulation(seed: 80, rules: FighterMatchRules.Disabled, stageGeometry: popGeometry);
        var replay = new FighterSimulation(seed: 80, rules: FighterMatchRules.Disabled, stageGeometry: popGeometry);
        var predicted = new FighterSimulation(seed: 80, rules: FighterMatchRules.Disabled, stageGeometry: popGeometry);
        int predictFrom = turn.Tick - 4;
        int predictTo = turn.Tick + 6;
        bool popped = false;
        for (int t = 0; t < dropAt + 60; t++) {
            long hash = authoritative.Advance(Idle(t), Two(t));
            AssertThat(replay.Advance(Idle(t), Two(t))).IsEqual(hash);
            if (t >= predictFrom && t < predictTo) predicted.AdvanceWithPredictedPlayerTwo(Idle(t));
            else predicted.Advance(Idle(t), Two(t));
            if (t == predictTo) {
                for (int corrected = predictFrom; corrected < predictTo; corrected++) {
                    AssertThat(predicted.CorrectPlayerTwoInput(corrected, Two(corrected))).IsTrue();
                }
                AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
            }
            authoritative.TryGetFighter(1, out FighterStateComponent two);
            if (t == turn.Tick) popped = two.IsGrounded != 0;
        }
        AssertThat(popped).OverrideFailureMessage("player two's scripted apex never popped onto the top.").IsTrue();
        AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
    }

    // === helpers ===

    private readonly struct Turn {
        public readonly int Tick;
        public readonly FP64 X;
        public readonly FP64 Y;
        public readonly FP64 PreviousX;

        public Turn(int tick, FP64 x, FP64 y, FP64 previousX) {
            Tick = tick;
            X = x;
            Y = y;
            PreviousX = previousX;
        }
    }

    /// <summary>
    /// Flies <paramref name="input"/> on a geometry whose only platform sits far
    /// out of reach over the same x and returns the first post-jump tick whose
    /// vertical velocity is no longer rising, with the feet there and the x a
    /// tick earlier.
    /// </summary>
    private static Turn MeasureTurn(int playerID, FP64 platformCenterX, Func<int, PlayerInputFrame> input) {
        FighterStageGeometry high = Geometry(platformCenterX, FP64.FromInt(8), FP64.One);
        var dry = new FighterSimulation(seed: 70, rules: FighterMatchRules.Disabled, stageGeometry: high);
        FP64 previousX = FP64.Zero;
        for (int tick = 0; tick < 200; tick++) {
            PlayerInputFrame frame = input(tick);
            if (playerID == 0) dry.Advance(frame, Idle(tick));
            else dry.Advance(Idle(tick), frame);
            AssertThat(dry.TryGetFighter(playerID, out FighterStateComponent fighter)).IsTrue();
            if (tick > 0 && fighter.IsGrounded == 0 && fighter.Velocity.y <= FP64.Zero) {
                return new Turn(tick, fighter.Position.x, fighter.Position.y, previousX);
            }
            previousX = fighter.Position.x;
        }
        AssertThat(false).OverrideFailureMessage("the jump never turned.").IsTrue();
        return default;
    }

    private static FighterStateComponent AdvanceTo(FighterSimulation simulation, int lastTick, Func<int, PlayerInputFrame> input) {
        for (int tick = 0; tick <= lastTick; tick++) simulation.Advance(input(tick), Idle(tick));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
        return fighter;
    }

    private static FighterStageGeometry Geometry(FP64 centerX, FP64 surfaceY, FP64 halfWidth) =>
        FighterStageGeometry.ForTest("cm_move_lip_pop", new[] { new FighterStagePlatform(centerX, surfaceY, halfWidth) });

    /// <summary>A held jump from tick 0: hand-built (Held only) or Create-built (with edges).</summary>
    private static PlayerInputFrame JumpInput(int tick, sbyte moveX, bool createBuilt) {
        if (createBuilt) {
            return PlayerInputFrame.Create((uint)tick, moveX / 127f, 0f, GameplayButtons.Jump,
                tick == 0 ? GameplayButtons.None : GameplayButtons.Jump);
        }
        return Hand(tick, moveX, tick == 0 ? GameplayButtons.Jump : GameplayButtons.None, GameplayButtons.Jump);
    }

    private static PlayerInputFrame Hand(int tick, sbyte moveX, GameplayButtons pressed, GameplayButtons held) => new() {
        Tick = (uint)tick, MoveX = moveX, Held = held, Pressed = pressed
    };

    private static PlayerInputFrame Idle(int tick) => new() { Tick = (uint)tick };
}
