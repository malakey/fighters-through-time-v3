using System.Collections.Generic;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Gameplay-feel plan §2.11 — deterministic ledge grab, the sim's first slice of
/// audit M-16. Two halves are pinned here: the pure rulebook
/// (<c>FighterLedgeRules</c> plus the geometry capture box) and the wiring inside
/// <c>FighterMovementSystem</c>, driven through a real simulation on authored
/// Florence geometry (platforms centred at ±4, surface 2.4, half-width 1.6, so
/// their ends sit at ∓5.6 and ±2.4).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterLedgeTests {
    private static readonly FighterStageGeometry Florence = FighterStageGeometry.Florence;
    private const int ScriptSeed = 4500;

    // === The rulebook: capture conditions ===

    [TestCase]
    public void AFallingFighterInsideTheCaptureBoxMayGrab() {
        FighterStateComponent fighter = AirborneAt(EdgeOf(0, 0), FP64.FromDouble(2.0));
        fighter.Velocity.y = FP64.FromInt(-8);
        FighterRuntimeComponent runtime = FreshRuntime();
        FighterVerbComponent verb = new();

        AssertThat(FighterLedgeRules.CanGrab(in fighter, in runtime, in verb)).IsTrue();
        AssertThat(Florence.TryFindLedge(in fighter.Position, out int anchor)).IsTrue();
        AssertThat(anchor).IsEqual(0);
    }

    /// <summary>
    /// The "jumping up to it" case: still rising, but slowly enough to be near the
    /// apex. Two units per second is the locked ceiling — at three the fighter is
    /// clearing the edge, not catching it.
    /// </summary>
    [TestCase]
    public void RisingSlowlyGrabsButRisingFastDoesNot() {
        FighterStateComponent slow = AirborneAt(EdgeOf(0, 0), FP64.FromDouble(2.0));
        slow.Velocity.y = FighterLedgeRules.MaxGrabVerticalSpeed;
        FighterRuntimeComponent runtime = FreshRuntime();
        FighterVerbComponent verb = new();
        AssertThat(FighterLedgeRules.CanGrab(in slow, in runtime, in verb)).IsTrue();

        FighterStateComponent fast = slow;
        fast.Velocity.y = FighterLedgeRules.MaxGrabVerticalSpeed + FP64.One;
        AssertThat(FighterLedgeRules.CanGrab(in fast, in runtime, in verb)).IsFalse();
    }

    [TestCase]
    public void HitstunDazeDropThroughAndTheRegrabLockoutAllRefuseTheGrab() {
        FighterStateComponent baseline = AirborneAt(EdgeOf(0, 0), FP64.FromDouble(2.0));
        baseline.Velocity.y = FP64.FromInt(-8);
        FighterRuntimeComponent freshRuntime = FreshRuntime();
        FighterVerbComponent freshVerb = new();
        AssertThat(FighterLedgeRules.CanGrab(in baseline, in freshRuntime, in freshVerb)).IsTrue();

        FighterStateComponent stunned = baseline;
        stunned.HitstunFrames = 1;
        AssertThat(FighterLedgeRules.CanGrab(in stunned, in freshRuntime, in freshVerb)).IsFalse();

        FighterStateComponent dazed = baseline;
        dazed.DazeFrames = 1;
        AssertThat(FighterLedgeRules.CanGrab(in dazed, in freshRuntime, in freshVerb)).IsFalse();

        FighterStateComponent dropping = baseline;
        dropping.DropThroughFrames = 1;
        AssertThat(FighterLedgeRules.CanGrab(in dropping, in freshRuntime, in freshVerb)).IsFalse();

        FighterStateComponent grounded = baseline;
        grounded.IsGrounded = 1;
        AssertThat(FighterLedgeRules.CanGrab(in grounded, in freshRuntime, in freshVerb)).IsFalse();

        FighterRuntimeComponent lockedOut = freshRuntime;
        lockedOut.LedgeRegrabLockoutFrames = 1;
        AssertThat(FighterLedgeRules.CanGrab(in baseline, in lockedOut, in freshVerb)).IsFalse();

        FighterRuntimeComponent alreadyHanging = freshRuntime;
        alreadyHanging.LedgeAnchor = 0;
        AssertThat(FighterLedgeRules.CanGrab(in baseline, in alreadyHanging, in freshVerb)).IsFalse();
    }

    /// <summary>
    /// V7.3 regrab cap: the per-airtime budget is
    /// <c>BasicComboRules.LedgeRegrabsPerAirtime</c> (3) — the fourth grab in
    /// one airtime is refused, and every latched grab spends one.
    /// </summary>
    [TestCase]
    public void TheFourthGrabInOneAirtimeIsRefused() {
        FighterStateComponent fighter = AirborneAt(EdgeOf(0, 0), FP64.FromDouble(2.0));
        fighter.Velocity.y = FP64.FromInt(-8);
        FighterRuntimeComponent runtime = FreshRuntime();
        FighterTuningComponent tuning = Tuning();
        AssertThat(Florence.TryGetHangPosition(0, out FPVector2 hang)).IsTrue();

        FighterVerbComponent verb = new();
        for (int grab = 1; grab <= FTT.Combat.BasicComboRules.LedgeRegrabsPerAirtime; grab++) {
            AssertThat(FighterLedgeRules.CanGrab(in fighter, in runtime, in verb))
                .OverrideFailureMessage($"Grab {grab} of the airtime budget must be allowed.")
                .IsTrue();
            FighterLedgeRules.Grab(ref fighter, ref runtime, ref verb, in tuning, 0, in hang);
            AssertThat(verb.LedgeGrabsThisAirtime).IsEqual(grab);
            // Simulate the release between grabs without grounding.
            FighterLedgeRules.Release(ref fighter, ref runtime);
            runtime.LedgeRegrabLockoutFrames = 0;
            fighter.Velocity.y = FP64.FromInt(-8);
        }

        AssertThat(FighterLedgeRules.CanGrab(in fighter, in runtime, in verb))
            .OverrideFailureMessage("The fourth grab in one airtime must be refused.")
            .IsFalse();
    }

    [TestCase]
    public void TheCaptureBoxIsHalfAUnitWideAndReachesOnePointTwoBelowTheSurface() {
        FighterStagePlatform platform = Florence.Platforms[0];
        FP64 edge = platform.EdgeX(0);
        FP64 justUnder = platform.SurfaceY - FP64.FromDouble(0.1);

        AssertThat(platform.IsInCaptureBox(
            new FPVector2(edge + FighterLedgeRules.CaptureHalfWidth, justUnder), 0)).IsTrue();
        AssertThat(platform.IsInCaptureBox(
            new FPVector2(edge + FighterLedgeRules.CaptureHalfWidth + FP64.FromDouble(0.01), justUnder), 0))
            .IsFalse();

        AssertThat(platform.IsInCaptureBox(
            new FPVector2(edge, platform.SurfaceY - FighterLedgeRules.CaptureDepth), 0)).IsTrue();
        AssertThat(platform.IsInCaptureBox(
            new FPVector2(edge, platform.SurfaceY - FighterLedgeRules.CaptureDepth - FP64.FromDouble(0.01)), 0))
            .IsFalse();
        // Above the surface is a landing, never a grab.
        AssertThat(platform.IsInCaptureBox(
            new FPVector2(edge, platform.SurfaceY + FP64.FromDouble(0.01)), 0)).IsFalse();
    }

    // === The rulebook: hang, climb, release ===

    [TestCase]
    public void GrabbingPinsTheAnchorPositionZeroesVelocityAndRefillsJumps() {
        FighterStateComponent fighter = AirborneAt(EdgeOf(0, 0), FP64.FromDouble(2.0));
        fighter.Velocity = new FPVector2(FP64.FromInt(-3), FP64.FromInt(-9));
        fighter.RemainingJumps = 0;
        FighterRuntimeComponent runtime = FreshRuntime();
        FighterTuningComponent tuning = Tuning();

        AssertThat(Florence.TryFindLedge(in fighter.Position, out int anchor)).IsTrue();
        AssertThat(Florence.TryGetHangPosition(anchor, out FPVector2 hang)).IsTrue();
        FighterVerbComponent verb = new();
        FighterLedgeRules.Grab(ref fighter, ref runtime, ref verb, in tuning, anchor, in hang);

        AssertThat(FighterLedgeRules.IsHanging(in runtime)).IsTrue();
        AssertThat(verb.LedgeGrabsThisAirtime).IsEqual(1);
        // Side 0 is the left edge, so the hang sits a quarter unit further left
        // and a full unit below the surface.
        AssertThat(fighter.Position.x.RawValue).IsEqual(
            (Florence.Platforms[0].EdgeX(0) - FighterLedgeRules.HangOutwardOffset).RawValue);
        AssertThat(fighter.Position.y.RawValue).IsEqual(
            (Florence.Platforms[0].SurfaceY - FighterLedgeRules.HangDepth).RawValue);
        AssertThat(fighter.Velocity.x.RawValue).IsEqual(0);
        AssertThat(fighter.Velocity.y.RawValue).IsEqual(0);
        AssertThat(fighter.RemainingJumps).IsEqual(tuning.MaxJumpCount);
    }

    [TestCase]
    public void JumpClimbsWithNineTenthsOfAGroundJumpAndSpendsNoAirJump() {
        FighterTuningComponent tuning = Tuning();
        Hang(out FighterStateComponent fighter, out FighterRuntimeComponent runtime, in tuning, anchor: 0);

        runtime.PressedButtons = (int)GameplayButtons.Jump;
        runtime.HeldButtons = (int)GameplayButtons.Jump;
        AssertThat(Florence.TryGetHangPosition(0, out FPVector2 hang)).IsTrue();
        bool stillHanging = FighterLedgeRules.Process(ref fighter, ref runtime, in tuning, in hang);

        AssertThat(stillHanging).IsFalse();
        AssertThat(FighterLedgeRules.IsHanging(in runtime)).IsFalse();
        AssertThat(fighter.Velocity.y.RawValue).IsEqual(
            (tuning.JumpSpeed * FighterLedgeRules.ClimbJumpMultiplier).RawValue);
        // The grab refilled the budget and the climb is the ground-jump
        // equivalent, so the fighter leaves with exactly one jump spent.
        AssertThat(fighter.RemainingJumps).IsEqual(tuning.MaxJumpCount - 1);
        AssertThat(runtime.LedgeRegrabLockoutFrames).IsEqual(0);
    }

    [TestCase]
    public void DownReleasesIntoAFallWithTheThirtyFrameRegrabLockout() {
        FighterTuningComponent tuning = Tuning();
        Hang(out FighterStateComponent fighter, out FighterRuntimeComponent runtime, in tuning, anchor: 0);

        runtime.HeldButtons = (int)GameplayButtons.Down;
        AssertThat(Florence.TryGetHangPosition(0, out FPVector2 hang)).IsTrue();
        bool stillHanging = FighterLedgeRules.Process(ref fighter, ref runtime, in tuning, in hang);

        AssertThat(stillHanging).IsFalse();
        AssertThat(FighterLedgeRules.IsHanging(in runtime)).IsFalse();
        AssertThat(runtime.LedgeRegrabLockoutFrames).IsEqual(FighterLedgeRules.RegrabLockoutFrames);
        AssertThat(fighter.IsGrounded).IsEqual(0);
    }

    [TestCase]
    public void TheHangAutoReleasesAtThreeHundredFramesWithTheSameLockout() {
        FighterTuningComponent tuning = Tuning();
        Hang(out FighterStateComponent fighter, out FighterRuntimeComponent runtime, in tuning, anchor: 0);
        AssertThat(Florence.TryGetHangPosition(0, out FPVector2 hang)).IsTrue();

        for (int frame = 1; frame < FighterLedgeRules.AutoReleaseFrames; frame++) {
            AssertThat(FighterLedgeRules.Process(ref fighter, ref runtime, in tuning, in hang)).IsTrue();
        }
        AssertThat(FighterLedgeRules.Process(ref fighter, ref runtime, in tuning, in hang)).IsFalse();
        AssertThat(FighterLedgeRules.IsHanging(in runtime)).IsFalse();
        AssertThat(runtime.LedgeRegrabLockoutFrames).IsEqual(FighterLedgeRules.RegrabLockoutFrames);
    }

    [TestCase]
    public void TheLegacyFlatArenaHasNoPlatformsAndThereforeNoLedges() {
        FighterStageGeometry flat = FighterStageGeometry.Default;
        AssertThat(flat.Platforms.Length).IsEqual(0);
        for (int x = -10; x <= 10; x++) {
            for (int y = -5; y <= 9; y++) {
                AssertThat(flat.TryFindLedge(
                    new FPVector2(FP64.FromInt(x), FP64.FromInt(y)), out int anchor)).IsFalse();
                AssertThat(anchor).IsEqual(FighterLedgeRules.NoAnchor);
            }
        }
        AssertThat(flat.TryGetHangPosition(0, out FPVector2 _)).IsFalse();
    }

    // === Wiring: a real simulation on authored geometry ===

    [TestCase]
    public void WalkingOffAPlatformEndCatchesTheLedgeInTheSimulation() {
        var simulation = new FighterSimulation(
            seed: 4100, rules: FighterMatchRules.Disabled, stageGeometry: Florence);
        AssertThat(SeekLedge(simulation, playerID: 0, platformIndex: 0, side: 0, maxTicks: 600)).IsTrue();

        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
        AssertThat(runtime.LedgeAnchor).IsEqual(0);
        AssertThat(Florence.TryGetHangPosition(0, out FPVector2 hang)).IsTrue();
        AssertThat(fighter.Position.x.RawValue).IsEqual(hang.x.RawValue);
        AssertThat(fighter.Position.y.RawValue).IsEqual(hang.y.RawValue);
        AssertThat(fighter.IsGrounded).IsEqual(0);
    }

    /// <summary>
    /// Gravity, movement and the attack phase machine are all short-circuited by
    /// the hang exactly as hitstun short-circuits them, so a hanging fighter
    /// mashing every button stays exactly where it is.
    /// </summary>
    [TestCase]
    public void HangingSuppressesAttacksSpecialsAndBlockAndIgnoresRollAndMovement() {
        var simulation = new FighterSimulation(
            seed: 4200, rules: FighterMatchRules.Disabled, stageGeometry: Florence);
        AssertThat(SeekLedge(simulation, playerID: 0, platformIndex: 0, side: 0, maxTicks: 600)).IsTrue();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();

        const GameplayButtons mash = GameplayButtons.BasicAttack | GameplayButtons.Special1
            | GameplayButtons.Special2 | GameplayButtons.Ultimate | GameplayButtons.Block
            | GameplayButtons.Roll | GameplayButtons.MovementAbility;
        int startTick = simulation.CurrentTick;
        for (int offset = 0; offset < 60; offset++) {
            int tick = startTick + offset;
            simulation.Advance(
                new PlayerInputFrame {
                    Tick = (uint)tick,
                    MoveX = (sbyte)(offset % 2 == 0 ? 127 : -127),
                    Held = mash,
                    // Re-pressed every other tick so no edge is missed.
                    Pressed = offset % 2 == 0 ? mash : GameplayButtons.None
                },
                new PlayerInputFrame { Tick = (uint)tick });
        }

        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
        AssertThat(FighterLedgeRules.IsHanging(in runtime)).IsTrue();
        AssertThat(runtime.AttackPhase).IsEqual(0);
        AssertThat(runtime.UniversalMovementState).IsEqual((int)UniversalMovementPhase.None);
        AssertThat(runtime.SpecialOneCooldownFrames).IsEqual(0);
        AssertThat(runtime.SpecialTwoCooldownFrames).IsEqual(0);
        AssertThat(runtime.MovementCooldownFrames).IsEqual(0);
        AssertThat(simulation.ProjectileCount + simulation.PersistentObjectCount + simulation.ZoneCount)
            .IsEqual(0);
        AssertThat(after.Position.x.RawValue).IsEqual(before.Position.x.RawValue);
        AssertThat(after.Position.y.RawValue).IsEqual(before.Position.y.RawValue);
    }

    [TestCase]
    public void ReleasingWithDownLocksOutRegrabForThirtyFramesAndAllowsItAfterwards() {
        var simulation = new FighterSimulation(
            seed: 4300, rules: FighterMatchRules.Disabled, stageGeometry: Florence);
        AssertThat(SeekLedge(simulation, playerID: 0, platformIndex: 0, side: 0, maxTicks: 600)).IsTrue();

        // One tick of Down releases; the fighter is left standing inside the
        // capture box it just left, so only the lockout keeps it falling.
        int tick = simulation.CurrentTick;
        simulation.Advance(
            new PlayerInputFrame {
                Tick = (uint)tick, Held = GameplayButtons.Down, Pressed = GameplayButtons.Down
            },
            new PlayerInputFrame { Tick = (uint)tick });
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent released)).IsTrue();
        AssertThat(FighterLedgeRules.IsHanging(in released)).IsFalse();
        AssertThat(released.LedgeRegrabLockoutFrames).IsEqual(FighterLedgeRules.RegrabLockoutFrames);

        // Neutral from here: nothing re-grabs while the lockout runs, and by the
        // time it expires the fighter has fallen clear of the capture box.
        for (int offset = 1; offset <= FighterLedgeRules.RegrabLockoutFrames; offset++) {
            tick = simulation.CurrentTick;
            simulation.Advance(
                new PlayerInputFrame { Tick = (uint)tick }, new PlayerInputFrame { Tick = (uint)tick });
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent during)).IsTrue();
            AssertThat(FighterLedgeRules.IsHanging(in during)).IsFalse();
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent expired)).IsTrue();
        AssertThat(expired.LedgeRegrabLockoutFrames).IsEqual(0);

        // With the lockout gone the same edge is grabbable again.
        AssertThat(SeekLedge(simulation, playerID: 0, platformIndex: 0, side: 0, maxTicks: 600)).IsTrue();
    }

    /// <summary>
    /// V7.3 ledge trump: the anchor is contested, not shared. When a second
    /// fighter grabs an edge the other already hangs, the EARLIER hanger is
    /// forced off through the normal release path — 30-frame regrab lockout
    /// armed — and the newcomer keeps the ledge.
    /// </summary>
    [TestCase]
    public void ASecondGrabberTrumpsTheHanger() {
        var simulation = new FighterSimulation(
            seed: 4400, rules: FighterMatchRules.Disabled, stageGeometry: Florence);
        // Player one starts under platform 1 and takes its left end first; player
        // zero then crosses the stage and takes the same one.
        AssertThat(SeekLedge(simulation, playerID: 1, platformIndex: 1, side: 0, maxTicks: 900)).IsTrue();
        AssertThat(SeekLedge(simulation, playerID: 0, platformIndex: 1, side: 0, maxTicks: 900)).IsTrue();

        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent newcomer)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent trumped)).IsTrue();
        AssertThat(newcomer.LedgeAnchor)
            .OverrideFailureMessage("The newcomer must keep the contested anchor.")
            .IsEqual(2);
        AssertThat(FighterLedgeRules.IsHanging(in trumped))
            .OverrideFailureMessage("The earlier hanger must be forced off by the trump.")
            .IsFalse();
        AssertThat(trumped.LedgeRegrabLockoutFrames)
            .OverrideFailureMessage("The forced release arms the standard regrab lockout.")
            .IsEqual(FighterLedgeRules.RegrabLockoutFrames);
    }

    /// <summary>
    /// V7.3 regrab cap, the reset half: touching the ground zeroes
    /// <c>LedgeGrabsThisAirtime</c>, so the next airtime starts with the full
    /// budget. Driven through the real simulation: grab (budget 1), Down-drop,
    /// fast-fall to the solid base floor (budget 0).
    /// </summary>
    [TestCase]
    public void LandingResetsTheRegrabBudget() {
        var simulation = new FighterSimulation(
            seed: 4450, rules: FighterMatchRules.Disabled, stageGeometry: Florence);
        AssertThat(SeekLedge(simulation, playerID: 0, platformIndex: 0, side: 0, maxTicks: 600)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent hanging)).IsTrue();
        AssertThat(hanging.LedgeGrabsThisAirtime).IsEqual(1);

        // Down drops off the hang (arming the regrab lockout) and then
        // fast-falls the fighter onto the solid base floor.
        bool landed = false;
        for (int offset = 0; offset < 120 && !landed; offset++) {
            int tick = simulation.CurrentTick;
            simulation.Advance(
                new PlayerInputFrame {
                    Tick = (uint)tick,
                    Held = GameplayButtons.Down,
                    Pressed = offset == 0 ? GameplayButtons.Down : GameplayButtons.None
                },
                new PlayerInputFrame { Tick = (uint)tick });
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
            landed = fighter.IsGrounded != 0;
        }
        AssertThat(landed)
            .OverrideFailureMessage("The dropped fighter never touched down within two seconds.")
            .IsTrue();
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent grounded)).IsTrue();
        AssertThat(grounded.LedgeGrabsThisAirtime)
            .OverrideFailureMessage("Grounding must reset the per-airtime regrab budget.")
            .IsEqual(0);
    }

    /// <summary>
    /// The three ledge ints live in <c>FighterRuntimeComponent</c>, so rollback
    /// covers them for free — this proves it over a scripted hang → climb →
    /// re-hang → release run rather than trusting the component contract.
    /// </summary>
    [TestCase]
    public void HangClimbAndReleaseConvergeAcrossTwoSimulationsUnderRollback() {
        List<(PlayerInputFrame One, PlayerInputFrame Two)> script = RecordHangScript(out int hangTick);

        var authoritative = new FighterSimulation(
            seed: ScriptSeed, rules: FighterMatchRules.Disabled, stageGeometry: Florence);
        var predicted = new FighterSimulation(
            seed: ScriptSeed, rules: FighterMatchRules.Disabled, stageGeometry: Florence);

        // The prediction window straddles the climb jump, so the resimulation has
        // to reproduce a hang, its exit, and the regrab that follows.
        int predictFrom = hangTick + 25;
        int predictTo = predictFrom + 10;
        for (int tick = 0; tick < script.Count; tick++) {
            authoritative.Advance(script[tick].One, script[tick].Two);
            if (tick >= predictFrom && tick < predictTo) predicted.AdvanceWithPredictedPlayerTwo(script[tick].One);
            else predicted.Advance(script[tick].One, script[tick].Two);
            // Corrections arrive as soon as the window closes: the 120-tick
            // history would otherwise have rolled past them by the script's end.
            if (tick != predictTo) continue;
            for (int corrected = predictFrom; corrected < predictTo; corrected++) {
                AssertThat(predicted.CorrectPlayerTwoInput(corrected, script[corrected].Two)).IsTrue();
            }
            AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
        }

        AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
    }

    /// <summary>
    /// Full-state capture and restore also has to carry the three ledge ints — a
    /// restored frame re-derives the pinned position from the anchor alone.
    /// </summary>
    [TestCase]
    public void ASnapshotTakenMidHangResumesTheSameHashSequence() {
        List<(PlayerInputFrame One, PlayerInputFrame Two)> script = RecordHangScript(out int hangTick);
        var uninterrupted = new FighterSimulation(
            seed: ScriptSeed, rules: FighterMatchRules.Disabled, stageGeometry: Florence);
        int captureAt = hangTick + 10;
        for (int tick = 0; tick < captureAt; tick++) uninterrupted.Advance(script[tick].One, script[tick].Two);

        AssertThat(uninterrupted.TryGetFighterRuntime(0, out FighterRuntimeComponent midHang)).IsTrue();
        AssertThat(FighterLedgeRules.IsHanging(in midHang)).IsTrue();
        byte[] snapshot = uninterrupted.CaptureFullState();

        var restored = new FighterSimulation(
            seed: ScriptSeed, rules: FighterMatchRules.Disabled, stageGeometry: Florence);
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);
        for (int tick = captureAt; tick < script.Count; tick++) {
            AssertThat(restored.Advance(script[tick].One, script[tick].Two))
                .IsEqual(uninterrupted.Advance(script[tick].One, script[tick].Two));
        }
    }

    [TestCase]
    public void NoFighterEverGrabsOnTheLegacyFlatArena() {
        var simulation = new FighterSimulation(seed: 4600, rules: FighterMatchRules.Disabled);
        for (int tick = 0; tick < 600; tick++) {
            simulation.Advance(LedgeScriptInput(tick, 0), LedgeScriptInput(tick, 1));
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent first)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent second)).IsTrue();
            AssertThat(first.LedgeAnchor).IsEqual(FighterLedgeRules.NoAnchor);
            AssertThat(second.LedgeAnchor).IsEqual(FighterLedgeRules.NoAnchor);
        }
    }

    /// <summary>
    /// A hanging CPU has no attack, no special and no ability — the climb jump is
    /// its only move, and the decision table emits it ahead of everything else.
    /// </summary>
    [TestCase]
    public void AHangingCpuLeavesTheLedgeWithinTwoSeconds() {
        var simulation = new FighterSimulation(
            seed: 4700, rules: FighterMatchRules.Disabled, stageGeometry: Florence);
        AssertThat(SeekLedge(simulation, playerID: 0, platformIndex: 0, side: 0, maxTicks: 600)).IsTrue();

        var cpu = new FighterCpuController(CpuDifficulty.Hard, seed: 4700, Florence, null);
        var previous = new PlayerInputFrame();
        bool escaped = false;
        for (int offset = 0; offset < 120 && !escaped; offset++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent self)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent selfRuntime)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent targetRuntime)).IsTrue();

            int tick = simulation.CurrentTick;
            PlayerInputFrame decision = cpu.Sample(
                (uint)tick, in self, in selfRuntime, in target, in targetRuntime, in previous);
            previous = decision;
            simulation.Advance(decision, new PlayerInputFrame { Tick = (uint)tick });

            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent after)).IsTrue();
            escaped = !FighterLedgeRules.IsHanging(in after);
        }

        AssertThat(escaped).IsTrue();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent final)).IsTrue();
        // Off the ledge means either climbing (upward velocity) or standing on a
        // surface — never still pinned at the anchor.
        AssertThat(final.IsGrounded != 0 || final.Velocity.y != FP64.Zero).IsTrue();
    }

    /// <summary>The observation the CPU reads carries the hang, and only the hang.</summary>
    [TestCase]
    public void TheCpuObservationReportsTheHangAndTheTableAnswersWithTheClimbJump() {
        FighterTuningComponent tuning = Tuning();
        Hang(out FighterStateComponent fighter, out FighterRuntimeComponent runtime, in tuning, anchor: 0);
        FighterStateComponent target = AirborneAt(FP64.Zero, FP64.Zero);
        target.IsGrounded = 1;
        FighterRuntimeComponent targetRuntime = FreshRuntime();

        CpuDecisionObservation hanging = FighterCpuController.Observe(
            in fighter, in runtime, in target, in targetRuntime, Florence, null);
        AssertThat(hanging.IsLedgeHanging).IsEqual(1);

        var cpu = new FighterCpuController(CpuDifficulty.Hard, seed: 11);
        AssertThat(cpu.Decide(in hanging, out sbyte _, out sbyte _)).IsEqual(GameplayButtons.Jump);

        FighterRuntimeComponent free = FreshRuntime();
        CpuDecisionObservation grounded = FighterCpuController.Observe(
            in fighter, in free, in target, in targetRuntime, Florence, null);
        AssertThat(grounded.IsLedgeHanging).IsEqual(0);
    }

    // === Helpers ===

    private static FP64 EdgeOf(int platformIndex, int side) =>
        Florence.Platforms[platformIndex].EdgeX(side);

    private static FighterStateComponent AirborneAt(FP64 x, FP64 y) => new() {
        Stocks = 3,
        CurrentHP = 100,
        MaxHP = 100,
        IsGrounded = 0,
        Position = new FPVector2(x, y)
    };

    private static FighterRuntimeComponent FreshRuntime() => new() {
        StatusIntensity = FP64.One,
        LedgeAnchor = FighterLedgeRules.NoAnchor
    };

    private static FighterTuningComponent Tuning() => new() {
        MaxJumpCount = 2,
        MaxBlockCharges = 3,
        MoveSpeed = FP64.FromDouble(5.5),
        JumpSpeed = FP64.FromDouble(11.5)
    };

    private static void Hang(
        out FighterStateComponent fighter,
        out FighterRuntimeComponent runtime,
        in FighterTuningComponent tuning,
        int anchor) {
        int platformIndex = anchor / 2;
        fighter = AirborneAt(EdgeOf(platformIndex, anchor % 2), FP64.FromDouble(2.0));
        runtime = FreshRuntime();
        FighterVerbComponent verb = new();
        AssertThat(Florence.TryGetHangPosition(anchor, out FPVector2 hang)).IsTrue();
        FighterLedgeRules.Grab(ref fighter, ref runtime, ref verb, in tuning, anchor, in hang);
    }

    /// <summary>
    /// Advances the simulation, steering <paramref name="playerID"/> onto the
    /// named platform and then off the named end, until it hangs. The other
    /// fighter is left neutral so a hang it already holds is not disturbed.
    /// </summary>
    private static bool SeekLedge(
        FighterSimulation simulation, int playerID, int platformIndex, int side, int maxTicks) {
        FighterStagePlatform platform = Florence.Platforms[platformIndex];
        int expectedAnchor = platformIndex * 2 + side;
        for (int step = 0; step < maxTicks; step++) {
            if (!simulation.TryGetFighter(playerID, out FighterStateComponent fighter)
                || !simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)) return false;
            if (runtime.LedgeAnchor == expectedAnchor) return true;

            int tick = simulation.CurrentTick;
            PlayerInputFrame seeking = SeekInput(tick, in fighter, in platform, side);
            PlayerInputFrame neutral = new() { Tick = (uint)tick };
            simulation.Advance(
                playerID == 0 ? seeking : neutral,
                playerID == 0 ? neutral : seeking);
        }
        return false;
    }

    private static PlayerInputFrame SeekInput(
        int tick, in FighterStateComponent fighter, in FighterStagePlatform platform, int side) {
        bool onPlatform = fighter.IsGrounded != 0
            && fighter.Position.y.RawValue == platform.SurfaceY.RawValue
            && platform.Supports(fighter.Position.x);
        if (onPlatform) {
            // Standing on it: walk off the requested end.
            return new PlayerInputFrame { Tick = (uint)tick, MoveX = side == 0 ? (sbyte)-127 : (sbyte)127 };
        }

        FP64 stopBand = FP64.FromDouble(0.15);
        FP64 dx = platform.CenterX - fighter.Position.x;
        sbyte moveX = dx > stopBand ? (sbyte)127 : dx < -stopBand ? (sbyte)-127 : (sbyte)0;
        GameplayButtons buttons = GameplayButtons.None;
        bool below = fighter.Position.y < platform.SurfaceY;
        if (below && platform.Supports(fighter.Position.x)) {
            bool canGroundJump = fighter.IsGrounded != 0;
            bool canAirJump = fighter.IsGrounded == 0
                && fighter.Velocity.y < FP64.Zero
                && fighter.RemainingJumps > 0;
            if (canGroundJump || canAirJump) buttons = GameplayButtons.Jump;
        }
        return new PlayerInputFrame { Tick = (uint)tick, MoveX = moveX, Held = buttons, Pressed = buttons };
    }

    /// <summary>
    /// Records a replayable input script that provably reaches a hang: the seek
    /// drive up to the grab, then a fixed tail that holds, climbs, falls back,
    /// re-grabs, and finally drops off with Down. Recording it rather than writing
    /// it by hand is what makes the rollback runs meaningful — a hand-written
    /// script that quietly stops reaching the ledge would prove nothing.
    /// </summary>
    private static List<(PlayerInputFrame One, PlayerInputFrame Two)> RecordHangScript(out int hangTick) {
        var script = new List<(PlayerInputFrame, PlayerInputFrame)>();
        var scratch = new FighterSimulation(
            seed: ScriptSeed, rules: FighterMatchRules.Disabled, stageGeometry: Florence);
        FighterStagePlatform platform = Florence.Platforms[0];

        hangTick = -1;
        for (int step = 0; step < 600 && hangTick < 0; step++) {
            AssertThat(scratch.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
            AssertThat(scratch.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            if (runtime.LedgeAnchor == 0) { hangTick = script.Count; break; }
            int tick = scratch.CurrentTick;
            var pair = (SeekInput(tick, in fighter, in platform, side: 0),
                new PlayerInputFrame { Tick = (uint)tick });
            script.Add(pair);
            scratch.Advance(pair.Item1, pair.Item2);
        }
        AssertThat(hangTick >= 0).IsTrue();

        // Fixed tail: hold the hang, climb at +30, let the fall re-grab, drop at
        // +90, then settle. Every phase of §2.11 is inside the replay.
        for (int offset = 0; offset < 150; offset++) {
            int tick = hangTick + offset;
            GameplayButtons held = offset switch {
                30 => GameplayButtons.Jump,
                >= 90 and < 95 => GameplayButtons.Down,
                _ => GameplayButtons.None
            };
            GameplayButtons pressed = offset is 30 or 90 ? held : GameplayButtons.None;
            script.Add((
                new PlayerInputFrame { Tick = (uint)tick, Held = held, Pressed = pressed },
                new PlayerInputFrame { Tick = (uint)tick }));
        }
        return script;
    }

    /// <summary>
    /// A fixed input script that walks, jumps and drops around the flat arena.
    /// Deterministic in the tick alone; used only where no ledge should ever be
    /// found, so it deliberately does not aim at any platform end.
    /// </summary>
    private static PlayerInputFrame LedgeScriptInput(int tick, int playerID) {
        int phase = (tick + playerID * 37) % 120;
        sbyte moveX = phase < 40
            ? (playerID == 0 ? (sbyte)-127 : (sbyte)127)
            : phase < 80 ? (sbyte)0 : (playerID == 0 ? (sbyte)127 : (sbyte)-127);
        GameplayButtons held = GameplayButtons.None;
        if (phase % 20 == 0) held |= GameplayButtons.Jump;
        if (phase >= 100) held |= GameplayButtons.Down;
        GameplayButtons pressed = phase % 20 == 0 || phase == 100 ? held : GameplayButtons.None;
        return new PlayerInputFrame { Tick = (uint)tick, MoveX = moveX, Held = held, Pressed = pressed };
    }
}
