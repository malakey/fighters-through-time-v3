using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 6 A2. Pins the deterministic match flow: the pre-match countdown, the
/// Chronal Respawn Platform FSM, all five ways a match can end, and the proof
/// that the driver's presentation clock cannot alter simulation state.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterMatchFlowTests {

    // === Countdown ===

    [TestCase]
    public void ProductionRulesStartTheMatchInTheThreeSecondCountdown() {
        var simulation = new FighterSimulation(rules: CountdownRules());

        FighterMatchComponent initial = simulation.GetMatchState();
        AssertThat(initial.MatchState).IsEqual(FighterMatchStates.Countdown);
        AssertThat(initial.CountdownFramesRemaining).IsEqual(FighterMatchFlowRules.CountdownFrames);

        for (int tick = 0; tick < FighterMatchFlowRules.CountdownFrames - 1; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
            AssertThat(simulation.GetMatchState().MatchState).IsEqual(FighterMatchStates.Countdown);
        }

        simulation.Advance(
            Neutral(FighterMatchFlowRules.CountdownFrames - 1),
            Neutral(FighterMatchFlowRules.CountdownFrames - 1));
        FighterMatchComponent live = simulation.GetMatchState();
        AssertThat(live.MatchState).IsEqual(FighterMatchStates.InProgress);
        AssertThat(live.CountdownFramesRemaining).IsEqual(0);
        AssertThat(live.GoBannerFramesRemaining).IsEqual(FighterMatchFlowRules.GoBannerFrames);
    }

    [TestCase]
    public void TheGoBannerWindowRunsForThirtyLiveFrames() {
        var simulation = new FighterSimulation(rules: CountdownRules());
        for (int tick = 0; tick < FighterMatchFlowRules.CountdownFrames; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
        AssertThat(simulation.GetMatchState().GoBannerFramesRemaining)
            .IsEqual(FighterMatchFlowRules.GoBannerFrames);

        for (int tick = 0; tick < FighterMatchFlowRules.GoBannerFrames; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
        AssertThat(simulation.GetMatchState().GoBannerFramesRemaining).IsEqual(0);
    }

    [TestCase]
    public void GameplayInputIsIgnoredUntilTheCountdownEnds() {
        var simulation = new FighterSimulation(spawnDistance: 8, rules: CountdownRules());
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent start)).IsTrue();

        // Full-tilt run plus attacks for the whole countdown: nothing may happen.
        for (int tick = 0; tick < FighterMatchFlowRules.CountdownFrames; tick++) {
            simulation.Advance(
                Frame(tick, 127, GameplayButtons.BasicAttack | GameplayButtons.Jump),
                Frame(tick, -127, GameplayButtons.BasicAttack));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent held)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent opponent)).IsTrue();
        AssertThat(held.Position.x.RawValue).IsEqual(start.Position.x.RawValue);
        AssertThat(held.Velocity.x.RawValue).IsEqual(0L);
        AssertThat(held.IsGrounded).IsEqual(1);
        AssertThat(held.CurrentHP).IsEqual(held.MaxHP);
        AssertThat(opponent.CurrentHP).IsEqual(opponent.MaxHP);

        // The very next frame is live and the same input moves the fighter.
        simulation.Advance(
            Frame(FighterMatchFlowRules.CountdownFrames, 127, GameplayButtons.None),
            Neutral(FighterMatchFlowRules.CountdownFrames));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent released)).IsTrue();
        AssertThat(released.Velocity.x > FP64.Zero).IsTrue();
    }

    [TestCase]
    public void TheMatchClockDoesNotRunDuringTheCountdown() {
        var simulation = new FighterSimulation(
            matchSeconds: 60,
            rules: new FighterMatchRules((int)MatchMode.Hybrid, false, 0, false, 0, 1,
                FighterMatchFlowRules.CountdownFrames));
        int startFrames = simulation.GetMatchState().RemainingFrames;

        // The clock is frozen for every input-locked countdown frame. The frame
        // the countdown resolves is already live, so it is also the clock's
        // first tick.
        for (int tick = 0; tick < FighterMatchFlowRules.CountdownFrames - 1; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
            AssertThat(simulation.GetMatchState().RemainingFrames).IsEqual(startFrames);
        }

        simulation.Advance(Neutral(0), Neutral(0));
        AssertThat(simulation.GetMatchState().MatchState).IsEqual(FighterMatchStates.InProgress);
        AssertThat(simulation.GetMatchState().RemainingFrames).IsEqual(startFrames - 1);
    }

    [TestCase]
    public void CountdownDigitsCountDownThreeTwoOneThenGo() {
        AssertThat(FighterMatchFlowRules.CountdownDigit(180)).IsEqual(3);
        AssertThat(FighterMatchFlowRules.CountdownDigit(121)).IsEqual(3);
        AssertThat(FighterMatchFlowRules.CountdownDigit(120)).IsEqual(2);
        AssertThat(FighterMatchFlowRules.CountdownDigit(61)).IsEqual(2);
        AssertThat(FighterMatchFlowRules.CountdownDigit(60)).IsEqual(1);
        AssertThat(FighterMatchFlowRules.CountdownDigit(1)).IsEqual(1);
        AssertThat(FighterMatchFlowRules.CountdownDigit(0)).IsEqual(0);
    }

    [TestCase]
    public void CountdownStateIsSnapshotAndRollbackSafe() {
        var uninterrupted = new FighterSimulation(seed: 51, rules: CountdownRules());
        for (int tick = 0; tick < 90; tick++) uninterrupted.Advance(Neutral(tick), Neutral(tick));
        byte[] snapshot = uninterrupted.CaptureFullState();
        AssertThat(uninterrupted.GetMatchState().MatchState).IsEqual(FighterMatchStates.Countdown);

        var restored = new FighterSimulation(seed: 51, rules: CountdownRules());
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);
        for (int tick = 90; tick < 300; tick++) {
            long expected = uninterrupted.Advance(Neutral(tick), Neutral(tick));
            AssertThat(restored.Advance(Neutral(tick), Neutral(tick))).IsEqual(expected);
        }
        AssertThat(restored.GetMatchState().MatchState).IsEqual(FighterMatchStates.InProgress);
    }

    // === Chronal Respawn Platform ===

    [TestCase]
    public void StockLossMaterialisesTheFighterOnTheRespawnPlatform() {
        var simulation = new FighterSimulation(rules: FighterMatchRules.Disabled);
        DriveOffTheBottom(simulation, 1);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent respawning)).IsTrue();
        AssertThat(respawning.Stocks).IsEqual(2);
        AssertThat(FighterMatchFlowRules.IsOnRespawnPlatform(in respawning)).IsTrue();
        AssertThat(respawning.Position.x.RawValue)
            .IsEqual(FighterMatchFlowRules.RespawnPlatformPosition.x.RawValue);
        AssertThat(respawning.Position.y.RawValue)
            .IsEqual(FighterMatchFlowRules.RespawnPlatformPosition.y.RawValue);
        AssertThat(respawning.Velocity.x.RawValue).IsEqual(0L);
        AssertThat(respawning.Velocity.y.RawValue).IsEqual(0L);
        AssertThat(respawning.CurrentHP).IsEqual(respawning.MaxHP);
    }

    [TestCase]
    public void AFighterCannotActWhileTheRespawnPlatformHoldsThem() {
        var simulation = new FighterSimulation(rules: FighterMatchRules.Disabled);
        DriveOffTheBottom(simulation, 1);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attackerStart)).IsTrue();
        int attackerHP = attackerStart.CurrentHP;

        // Hold everything for the whole grace window: no movement, no attack, no
        // jump, no roll, and the platform must still be holding them.
        for (int tick = 0; tick < FighterMatchFlowRules.RespawnPlatformGraceFrames - 1; tick++) {
            simulation.Advance(
                Neutral(tick),
                Frame(tick, 127,
                    GameplayButtons.BasicAttack | GameplayButtons.Jump
                    | GameplayButtons.Roll | GameplayButtons.Special1));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent frozen)).IsTrue();
            AssertThat(FighterMatchFlowRules.IsOnRespawnPlatform(in frozen)).IsTrue();
            AssertThat(frozen.Position.x.RawValue)
                .IsEqual(FighterMatchFlowRules.RespawnPlatformPosition.x.RawValue);
            AssertThat(frozen.Position.y.RawValue)
                .IsEqual(FighterMatchFlowRules.RespawnPlatformPosition.y.RawValue);
            AssertThat(frozen.Velocity.x.RawValue).IsEqual(0L);
        }

        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.UniversalMovementState).IsEqual((int)UniversalMovementPhase.None);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
        AssertThat(attacker.CurrentHP).IsEqual(attackerHP);
    }

    [TestCase]
    public void TheFighterIsInvulnerableForTheWholeDissolve() {
        var simulation = new FighterSimulation(spawnDistance: 1, rules: FighterMatchRules.Disabled);
        DriveOffTheBottom(simulation, 1);

        for (int tick = 0; tick < FighterMatchFlowRules.RespawnPlatformFrames - 1; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.BasicAttack), Neutral(tick));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent guarded)).IsTrue();
            AssertThat(guarded.CurrentHP).IsEqual(guarded.MaxHP);
            AssertThat(guarded.InvulnerabilityFrames
                >= FighterMatchFlowRules.RespawnInvulnerabilityFrames).IsTrue();
        }
    }

    [TestCase]
    public void AnyInputAfterTheGraceWindowDropsTheFighterAndArmsTheThreeSecondWindow() {
        var simulation = new FighterSimulation(rules: FighterMatchRules.Disabled);
        DriveOffTheBottom(simulation, 1);

        // Idle through the grace window with no input at all.
        for (int tick = 0; tick < FighterMatchFlowRules.RespawnPlatformGraceFrames; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent stillHeld)).IsTrue();
        AssertThat(FighterMatchFlowRules.IsOnRespawnPlatform(in stillHeld)).IsTrue();

        // One input now drops them, and the 3 s window starts at the drop with
        // its full length, not the leftover platform time.
        simulation.Advance(Neutral(0), Frame(0, 0, GameplayButtons.Jump));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent dropped)).IsTrue();
        AssertThat(FighterMatchFlowRules.IsOnRespawnPlatform(in dropped)).IsFalse();
        AssertThat(dropped.IsGrounded).IsEqual(0);
        AssertThat(dropped.InvulnerabilityFrames)
            .IsEqual(FighterMatchFlowRules.RespawnInvulnerabilityFrames);
    }

    [TestCase]
    public void AnExpiredPlatformDropsTheFighterAndArmsTheSameThreeSecondWindow() {
        var simulation = new FighterSimulation(rules: FighterMatchRules.Disabled);
        DriveOffTheBottom(simulation, 1);

        for (int tick = 0; tick < FighterMatchFlowRules.RespawnPlatformFrames; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent dropped)).IsTrue();
        AssertThat(FighterMatchFlowRules.IsOnRespawnPlatform(in dropped)).IsFalse();
        AssertThat(dropped.InvulnerabilityFrames)
            .IsEqual(FighterMatchFlowRules.RespawnInvulnerabilityFrames);

        // And it really does expire: three seconds of invulnerability, then vulnerable.
        for (int tick = 0; tick < FighterMatchFlowRules.RespawnInvulnerabilityFrames; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent vulnerable)).IsTrue();
        AssertThat(vulnerable.InvulnerabilityFrames).IsEqual(0);
    }

    [TestCase]
    public void RollInvulnerabilityStaysDistinctFromSpawnInvulnerability() {
        // The roll grant is short and must never overwrite a longer spawn window.
        AssertThat(UniversalMovementRules.RollInvulnerabilityFrames
            < FighterMatchFlowRules.RespawnInvulnerabilityFrames).IsTrue();

        var simulation = new FighterSimulation(rules: FighterMatchRules.Disabled);
        DriveOffTheBottom(simulation, 1);
        for (int tick = 0; tick < FighterMatchFlowRules.RespawnPlatformFrames; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
        // Land, then roll: the spawn window survives the roll's shorter grant.
        for (int tick = 0; tick < 40; tick++) simulation.Advance(Neutral(tick), Neutral(tick));
        simulation.Advance(Neutral(0), Frame(0, 127, GameplayButtons.Roll));
        for (int tick = 0; tick < UniversalMovementRules.RollStartupFrames + 1; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent rolling)).IsTrue();
        AssertThat(rolling.InvulnerabilityFrames
            > UniversalMovementRules.RollInvulnerabilityFrames).IsTrue();
    }

    [TestCase]
    public void RespawnPlatformStateIsSnapshotAndRollbackSafe() {
        var uninterrupted = new FighterSimulation(seed: 64, rules: FighterMatchRules.Disabled);
        DriveOffTheBottom(uninterrupted, 1);
        for (int tick = 0; tick < 40; tick++) uninterrupted.Advance(Neutral(tick), Neutral(tick));
        byte[] snapshot = uninterrupted.CaptureFullState();
        AssertThat(uninterrupted.TryGetFighter(1, out FighterStateComponent held)).IsTrue();
        AssertThat(FighterMatchFlowRules.IsOnRespawnPlatform(in held)).IsTrue();

        var restored = new FighterSimulation(seed: 64, rules: FighterMatchRules.Disabled);
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);
        for (int tick = 0; tick < 400; tick++) {
            long expected = uninterrupted.Advance(Neutral(tick), Neutral(tick));
            AssertThat(restored.Advance(Neutral(tick), Neutral(tick))).IsEqual(expected);
        }
        AssertThat(restored.TryGetFighter(1, out FighterStateComponent settled)).IsTrue();
        AssertThat(FighterMatchFlowRules.IsOnRespawnPlatform(in settled)).IsFalse();
    }

    // === The five match end conditions ===

    [TestCase]
    public void MatchEndsOnStockExhaustion() {
        var simulation = new FighterSimulation(
            stocks: 1, rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0));
        DriveOffTheBottom(simulation, 1);

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.MatchState).IsEqual(FighterMatchStates.Complete);
        AssertThat(match.WinnerPlayerID).IsEqual(0);
        AssertThat(match.IsTrueTie).IsEqual(0);
    }

    [TestCase]
    public void MatchEndsOnABottomBlastZoneFall() {
        var simulation = new FighterSimulation(
            stocks: 3, rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        AssertThat(before.Stocks).IsEqual(3);

        DriveOffTheBottom(simulation, 1);

        // The fall itself costs exactly one stock and the match continues.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.Stocks).IsEqual(2);
        AssertThat(simulation.GetMatchState().MatchState).IsEqual(FighterMatchStates.InProgress);
        AssertThat(simulation.GetMatchState().PlayerOneKOs).IsEqual(1);
    }

    // === Package 11 A9: the bottom blast zone on the three Open stages ===

    /// <summary>
    /// The V7 pillar behind audit finding H-11: on an <b>Open</b> stage the bottom
    /// blast zone is genuinely reachable through ordinary play. Every Open stage is
    /// walked off its own pit-facing floor edge with Down held — catch the ledge,
    /// let go, fast-fall — and the fall costs exactly one stock. Nothing is
    /// manufactured here: no drop-through input, no hand-placed position, no hazard.
    /// </summary>
    [TestCase]
    public void WalkingIntoAnOpenStagePitCostsAStock() {
        var issues = new List<string>();
        int stages = 0;
        foreach (FighterStageGeometry geometry in FighterStageGeometry.AllAuthored) {
            if (!geometry.IsOpenStage) continue;
            stages++;
            (int segmentIndex, int side) = FirstPitFacingLedge(geometry);
            var simulation = new FighterSimulation(
                stocks: 3,
                seed: 7300 + stages,
                rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0),
                stageGeometry: geometry);

            if (!WalkIntoThePit(simulation, geometry, segmentIndex, side, out int playerID, 600)) {
                issues.Add($"{geometry.StageID}: the walk never reached the blast zone");
                continue;
            }
            AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent fallen)).IsTrue();
            if (fallen.Stocks != 2) issues.Add($"{geometry.StageID}: stocks are {fallen.Stocks}, expected 2");
            if (!FighterMatchFlowRules.IsOnRespawnPlatform(in fallen)) {
                issues.Add($"{geometry.StageID}: the respawn platform did not take the fighter");
            }
            if (simulation.GetMatchState().MatchState != FighterMatchStates.InProgress) {
                issues.Add($"{geometry.StageID}: the match ended on a non-final stock");
            }
        }
        AssertThat(stages).IsEqual(3);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// The same fall on the last stock resolves the match, which is what makes a
    /// pit a real win condition rather than an expensive mistake.
    /// </summary>
    [TestCase]
    public void TheFinalStockLostToAPitEndsTheMatchOnAnOpenStage() {
        FighterStageGeometry paris = FighterStageGeometry.Paris;
        (int segmentIndex, int side) = FirstPitFacingLedge(paris);
        var simulation = new FighterSimulation(
            stocks: 1,
            seed: 7350,
            rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0),
            stageGeometry: paris);

        AssertThat(WalkIntoThePit(simulation, paris, segmentIndex, side, out int playerID, 600))
            .OverrideFailureMessage("The walk never reached the Paris courtyard's blast zone.")
            .IsTrue();

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.MatchState).IsEqual(FighterMatchStates.Complete);
        AssertThat(match.WinnerPlayerID).IsEqual(playerID == 0 ? 1 : 0);
        AssertThat(match.IsTrueTie).IsEqual(0);
    }

    /// <summary>
    /// The respawn anchor is per stage now: Paris's centre is a hole, so dropping a
    /// respawning fighter at the global stage-centre constant would cost them a
    /// second stock immediately. Every Open stage's platform must sit over solid
    /// support and its drop must reach a surface.
    /// </summary>
    [TestCase]
    public void TheRespawnPlatformUsesItsStageAnchorAndDropsOntoSolidGround() {
        var issues = new List<string>();
        int stages = 0;
        foreach (FighterStageGeometry geometry in FighterStageGeometry.AllAuthored) {
            if (!geometry.IsOpenStage) continue;
            stages++;
            (int segmentIndex, int side) = FirstPitFacingLedge(geometry);
            var simulation = new FighterSimulation(
                stocks: 3,
                seed: 7400 + stages,
                rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0),
                stageGeometry: geometry);
            if (!WalkIntoThePit(simulation, geometry, segmentIndex, side, out int playerID, 600)) {
                issues.Add($"{geometry.StageID}: the walk never reached the blast zone");
                continue;
            }

            AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent onPlatform)).IsTrue();
            if (onPlatform.Position.x.RawValue != geometry.RespawnPlatformPosition.x.RawValue
                || onPlatform.Position.y.RawValue != geometry.RespawnPlatformPosition.y.RawValue) {
                issues.Add(
                    $"{geometry.StageID}: respawned at ({onPlatform.Position.x.ToFloat()}, " +
                    $"{onPlatform.Position.y.ToFloat()}) instead of the authored anchor");
            }

            // Ride the platform out and land: the drop must find a surface and must
            // not cost a second stock.
            bool landed = false;
            bool lostAnother = false;
            for (int offset = 0; offset < 600 && !landed && !lostAnother; offset++) {
                int tick = simulation.CurrentTick;
                simulation.Advance(Neutral(tick), Neutral(tick));
                AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent dropping)).IsTrue();
                lostAnother = dropping.Stocks < 2;
                landed = !FighterMatchFlowRules.IsOnRespawnPlatform(in dropping)
                    && dropping.IsGrounded != 0;
            }
            if (lostAnother) issues.Add($"{geometry.StageID}: the respawn drop cost another stock");
            else if (!landed) issues.Add($"{geometry.StageID}: the respawn drop never reached a surface");
        }
        AssertThat(stages).IsEqual(3);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// The other half of the contract: the seven Sealed stages author no floor
    /// segments, so their floor still supports every x and a fighter walking the
    /// full width of one can never fall out of the world. This is what proves the
    /// segment lookup is a no-op wherever nothing is authored.
    /// </summary>
    [TestCase]
    public void SealedStagesKeepAnUnbrokenFloorAndNeverDropAFighter() {
        var issues = new List<string>();
        int stages = 0;
        foreach (FighterStageGeometry geometry in FighterStageGeometry.AllAuthored) {
            if (geometry.IsOpenStage) continue;
            stages++;
            for (int step = 0; step <= 40; step++) {
                FP64 x = geometry.LeftWall
                    + (geometry.RightWall - geometry.LeftWall) * FP64.FromInt(step) / FP64.FromInt(40);
                if (geometry.HasFloorSupport(x)) continue;
                issues.Add($"{geometry.StageID}: no floor support at x {x.ToFloat()}");
            }

            var simulation = new FighterSimulation(
                stocks: 3,
                seed: 7450 + stages,
                rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0),
                stageGeometry: geometry);
            for (int tick = 0; tick < 400; tick++) {
                // Both fighters walk hard into opposite walls holding Down the whole
                // way — the exact input that would find a hole if one existed.
                simulation.Advance(
                    Frame(tick, -127, GameplayButtons.Down),
                    Frame(tick, 127, GameplayButtons.Down));
            }
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent first)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent second)).IsTrue();
            if (first.Stocks != 3 || second.Stocks != 3) {
                issues.Add($"{geometry.StageID}: a sealed floor dropped a fighter");
            }
        }
        AssertThat(stages).IsEqual(7);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>The first authored floor-segment end that faces a pit.</summary>
    private static (int SegmentIndex, int Side) FirstPitFacingLedge(FighterStageGeometry geometry) {
        for (int index = 0; index < geometry.FloorSegments.Length; index++) {
            for (int side = 0; side < 2; side++) {
                FP64 edge = geometry.FloorSegments[index].EdgeX(side);
                if (edge > geometry.LeftWall && edge < geometry.RightWall) return (index, side);
            }
        }
        AssertThat($"{geometry.StageID} authors no pit-facing floor edge").IsEqual("");
        return (0, 0);
    }

    /// <summary>
    /// Walks the fighter that spawns on <paramref name="segmentIndex"/> off its
    /// named end with Down held: the ledge catch releases instantly, the fast-fall
    /// carries them past the blast zone, and the run stops at the stock loss.
    /// </summary>
    private static bool WalkIntoThePit(
        FighterSimulation simulation,
        FighterStageGeometry geometry,
        int segmentIndex,
        int side,
        out int playerID,
        int maxTicks) {
        FP64 edge = geometry.FloorSegments[segmentIndex].EdgeX(side);
        playerID = edge > FP64.Zero ? 1 : 0;
        sbyte intoTheGap = side == 1 ? (sbyte)127 : (sbyte)-127;
        int startingStocks = simulation.TryGetFighter(playerID, out FighterStateComponent start)
            ? start.Stocks
            : 0;

        for (int step = 0; step < maxTicks; step++) {
            int tick = simulation.CurrentTick;
            var falling = new PlayerInputFrame {
                Tick = (uint)tick,
                MoveX = intoTheGap,
                Held = GameplayButtons.Down,
                Pressed = step == 0 ? GameplayButtons.Down : GameplayButtons.None
            };
            var neutral = new PlayerInputFrame { Tick = (uint)tick };
            simulation.Advance(
                playerID == 0 ? falling : neutral,
                playerID == 0 ? neutral : falling);
            if (!simulation.TryGetFighter(playerID, out FighterStateComponent state)) return false;
            if (state.Stocks < startingStocks) return true;
            if (simulation.GetMatchState().MatchState == FighterMatchStates.Complete) return true;
        }
        return false;
    }

    [TestCase]
    public void MatchEndsOnAnHPKnockout() {
        var simulation = new FighterSimulation(
            FighterLoadout.Default(FighterCharacterID.Einstein),
            OneHitPointLoadout(),
            stocks: 1,
            seed: 5,
            spawnDistance: 1,
            rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0));

        bool ended = false;
        for (int tick = 0; tick < 60 && !ended; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.BasicAttack), Neutral(tick));
            ended = simulation.GetMatchState().MatchState == FighterMatchStates.Complete;
        }

        AssertThat(ended).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent knockedOut)).IsTrue();
        AssertThat(knockedOut.Stocks).IsEqual(0);
        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.WinnerPlayerID).IsEqual(0);
        AssertThat(match.IsTrueTie).IsEqual(0);
    }

    [TestCase]
    public void TimerExpiryDecidesOnStocksThenHpPercentage() {
        // Hybrid: equal stocks at the buzzer fall through to HP percentage.
        var simulation = new FighterSimulation(
            stocks: 3,
            matchSeconds: 2,
            seed: 11,
            spawnDistance: 1,
            rules: new FighterMatchRules((int)MatchMode.Hybrid, false, 0, false, 0));

        // Player one chips away for the whole two seconds; neither loses a stock,
        // so the decision has to come from the HP-percentage branch.
        for (int tick = 0; tick < 120; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.BasicAttack), Neutral(tick));
        }

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.MatchState).IsEqual(FighterMatchStates.Complete);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent first)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent second)).IsTrue();
        AssertThat(first.Stocks).IsEqual(second.Stocks);
        AssertThat(second.CurrentHP < first.CurrentHP).IsTrue();
        AssertThat(match.IsTrueTie).IsEqual(0);
        AssertThat(match.WinnerPlayerID).IsEqual(0);
    }

    [TestCase]
    public void TimerExpiryWithIdenticalStateEntersSuddenDeathNotADraw() {
        // V7.1: a true tie at the buzzer no longer records an immediate draw —
        // the match enters Sudden Death (still live, both fighters respawned
        // at 1 HP with no timer) and the first KO names the winner. The only
        // remaining path to a recorded Draw is a double-KO inside Sudden Death.
        var simulation = new FighterSimulation(
            matchSeconds: 1, rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));
        for (int tick = 0; tick < 60; tick++) simulation.Advance(Neutral(tick), Neutral(tick));

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.MatchState).IsEqual(FighterMatchStates.InProgress);
        AssertThat(match.SuddenDeathActive).IsEqual(1);
        AssertThat(match.TimerEnabled).IsEqual(0);
        AssertThat(match.WinnerPlayerID).IsEqual(-1);
        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent fighter)).IsTrue();
            AssertThat(fighter.CurrentHP).IsEqual(1);
        }
    }

    /// <summary>
    /// Audit M-10: the driver's per-knockout presentation beat keys off
    /// <c>FighterRuntimeComponent.KnockoutsSuffered</c>, because a TimeLimit match
    /// runs <c>UsesStocks = 0</c> and a stock diff never sees its knockouts. This
    /// pins the counter the driver observes: a TimeLimit fall increments it,
    /// scores the opponent's KO tally, leaves <c>Stocks</c> untouched, and the
    /// fighter still lands on the respawn platform for the visual beat.
    /// </summary>
    [TestCase]
    public void ATimeLimitKnockoutRaisesTheKnockoutCounterWithoutTouchingStocks() {
        var simulation = new FighterSimulation(
            stocks: 3,
            matchSeconds: 60,
            rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int startingStocks = before.Stocks;

        DriveOffTheBottom(simulation, 1);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.KnockoutsSuffered).IsEqual(1);
        AssertThat(after.Stocks).IsEqual(startingStocks);
        AssertThat(simulation.GetMatchState().PlayerOneKOs).IsEqual(1);
        AssertThat(simulation.GetMatchState().MatchState).IsEqual(FighterMatchStates.InProgress);
        AssertThat(FighterMatchFlowRules.IsOnRespawnPlatform(in after)).IsTrue();
    }

    /// <summary>
    /// And in a stock-bearing mode the same counter moves in lockstep with the
    /// stock, so keying the beat off knockouts loses nothing on the old path.
    /// </summary>
    [TestCase]
    public void AStockModeKnockoutMovesTheCounterAndTheStockTogether() {
        var simulation = new FighterSimulation(
            stocks: 3, rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0));
        DriveOffTheBottom(simulation, 1);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.KnockoutsSuffered).IsEqual(1);
        AssertThat(after.Stocks).IsEqual(2);
    }

    // === V7.3 timeout, Sudden Death, and dual-Defy rulings ===

    /// <summary>
    /// V7.3 ruling #8: the timer tie-break compares HP as a PERCENTAGE of max,
    /// never absolute HP. The heavier fighter here holds more absolute HP
    /// (150 vs 90) but a lower fraction (75% vs 90%) — the percentage rule
    /// must name the lighter fighter the winner.
    /// </summary>
    [TestCase]
    public void TimeoutComparesHPAsPercentageOfMax() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(TimeoutFighter(maxHP: 200, damage: 12.5f)),
            FighterLoadoutFactory.FromCharacterData(TimeoutFighter(maxHP: 100, damage: 62.5f)),
            stocks: 3,
            matchSeconds: 8,
            seed: 21,
            spawnDistance: 1,
            rules: new FighterMatchRules((int)MatchMode.Hybrid, false, 0, false, 0));

        // P1 chips P2 for 10 (of 100); much later — after the Rally pools have
        // fully drained, so no reclaim muddies the arithmetic — P2 answers
        // with a 50 (of 200) opener.
        int tick = 0;
        simulation.Advance(Frame(tick, 0, GameplayButtons.BasicAttack), Neutral(tick));
        for (tick = 1; tick <= 220; tick++) simulation.Advance(Neutral(tick), Neutral(tick));
        simulation.Advance(Neutral(tick), Frame(tick, 0, GameplayButtons.BasicAttack));
        for (tick++; tick <= 500; tick++) simulation.Advance(Neutral(tick), Neutral(tick));

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.MatchState).IsEqual(FighterMatchStates.Complete);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent first)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent second)).IsTrue();
        AssertThat(first.Stocks).IsEqual(second.Stocks);
        AssertThat(first.CurrentHP).IsEqual(150);
        AssertThat(second.CurrentHP).IsEqual(90);
        // Absolute HP favours player one; the percentage rule must not.
        AssertThat(match.WinnerPlayerID)
            .OverrideFailureMessage(
                "Timeout must compare HP as a percentage of max (75% vs 90%), not absolute HP (150 vs 90).")
            .IsEqual(1);
        AssertThat(match.IsTrueTie).IsEqual(0);
    }

    /// <summary>
    /// V7.3 ruling #8, second half: an un-reclaimed Echo Pool is not HP and
    /// must not count at the buzzer. The loser's live pool would flip the
    /// result if it were added to their HP.
    /// </summary>
    [TestCase]
    public void UnreclaimedEchoDoesNotCountAtTimeout() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(TimeoutFighter(maxHP: 100, damage: 37.5f)),
            FighterLoadoutFactory.FromCharacterData(TimeoutFighter(maxHP: 100, damage: 32f)),
            stocks: 3,
            matchSeconds: 6,
            seed: 22,
            spawnDistance: 1,
            rules: new FighterMatchRules((int)MatchMode.Hybrid, false, 0, false, 0));

        // P2 chips P1 for 25 early (its echo fully drains), then P1 lands a 30
        // just before the buzzer so the victim's pool is still live at expiry.
        int tick = 0;
        simulation.Advance(Neutral(tick), Frame(tick, 0, GameplayButtons.BasicAttack));
        for (tick = 1; tick <= 300; tick++) simulation.Advance(Neutral(tick), Neutral(tick));
        simulation.Advance(Frame(tick, 0, GameplayButtons.BasicAttack), Neutral(tick));
        for (tick++; tick <= 400; tick++) simulation.Advance(Neutral(tick), Neutral(tick));

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.MatchState).IsEqual(FighterMatchStates.Complete);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent first)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent second)).IsTrue();
        AssertThat(first.CurrentHP).IsEqual(75);
        AssertThat(second.CurrentHP).IsEqual(70);
        // The loser's echo really was live at the buzzer...
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
        AssertThat(verb.EchoPool > FP64.Zero)
            .OverrideFailureMessage("Harness: the victim's Echo Pool must still be draining at expiry.")
            .IsTrue();
        AssertThat(verb.EchoPool.ToFloat() > 5f)
            .OverrideFailureMessage("Harness: the live pool must be big enough to have flipped an HP+echo comparison.")
            .IsTrue();
        // ...and it counted for nothing: 75% beats 70% regardless of the pool.
        AssertThat(match.WinnerPlayerID)
            .OverrideFailureMessage("An un-reclaimed Echo Pool must not count at timeout.")
            .IsEqual(0);
    }

    /// <summary>
    /// V7.3 ruling #7: a simultaneous final-stock KO in regulation mirrors the
    /// timer tie — Sudden Death, not a recorded draw.
    /// </summary>
    [TestCase]
    public void SimultaneousFinalStockKOEntersSuddenDeath() {
        var simulation = new FighterSimulation(
            stocks: 1, rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0));

        // Both fighters drop through the floor and ride out the bottom blast
        // zone on the same tick.
        bool suddenDeath = false;
        for (int tick = 0; tick < 300 && !suddenDeath; tick++) {
            PlayerInputFrame falling = tick == 0
                ? Frame(tick, 0, GameplayButtons.Jump | GameplayButtons.Down)
                : Frame(tick, 0, GameplayButtons.Down);
            simulation.Advance(falling, falling);
            suddenDeath = simulation.GetMatchState().SuddenDeathActive == 1;
            AssertThat(simulation.GetMatchState().MatchState)
                .OverrideFailureMessage("A dual final-stock KO must never record an immediate result.")
                .IsEqual(FighterMatchStates.InProgress);
        }
        AssertThat(suddenDeath)
            .OverrideFailureMessage("The simultaneous final-stock KO must enter Sudden Death.")
            .IsTrue();

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.TimerEnabled).IsEqual(0);
        AssertThat(match.WinnerPlayerID).IsEqual(-1);
        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent fighter)).IsTrue();
            AssertThat(fighter.CurrentHP).IsEqual(1);
            AssertThat(fighter.Stocks).IsEqual(1);
            AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
            AssertThat(verb.DefyHistoryUsed)
                .OverrideFailureMessage("Sudden Death pre-marks Defy History used on both fighters.")
                .IsEqual(1);
        }
    }

    /// <summary>
    /// V7.3 ruling #7: a dual lethal trade with BOTH meters full fires BOTH
    /// Defy History procs. The combat system builds both attack intents from
    /// pre-hit state and applies them sequentially, so the first hit's Defy
    /// cannot suppress the second hit — both survivors stand at 1 HP with
    /// shattered meters, no echo, and no stock lost.
    /// </summary>
    [TestCase]
    public void ADualLethalTradeAtFullMeterFiresBothDefyProcs() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(DefyTradeFighter()),
            FighterLoadoutFactory.FromCharacterData(DefyTradeFighter()),
            stocks: 3,
            seed: 23,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        // Fill both meters with one 100-damage opener each, spaced so every
        // Rally pool fully drains (no reclaim muddies the HP arithmetic).
        int tick = 0;
        simulation.Advance(Frame(tick, 0, GameplayButtons.BasicAttack), Neutral(tick));
        for (tick = 1; tick <= 200; tick++) simulation.Advance(Neutral(tick), Neutral(tick));
        simulation.Advance(Neutral(tick), Frame(tick, 0, GameplayButtons.BasicAttack));
        for (tick++; tick <= 420; tick++) simulation.Advance(Neutral(tick), Neutral(tick));
        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent charged)).IsTrue();
            AssertThat(charged.Influence.RawValue)
                .OverrideFailureMessage($"Harness: player {playerID} must reach a full meter before the trade.")
                .IsEqual(FP64.FromInt(100).RawValue);
            AssertThat(charged.CurrentHP).IsEqual(300);
        }

        // The trade: both lethal melee specials on the same tick.
        simulation.Advance(
            Frame(tick, 0, GameplayButtons.Special1),
            Frame(tick, 0, GameplayButtons.Special1));
        for (tick++; tick <= 480; tick++) simulation.Advance(Neutral(tick), Neutral(tick));

        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent survivor)).IsTrue();
            AssertThat(survivor.Stocks)
                .OverrideFailureMessage($"Player {playerID}'s Defy proc must have refused the KO.")
                .IsEqual(3);
            AssertThat(survivor.CurrentHP).IsEqual(1);
            AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
            AssertThat(verb.DefyHistoryUsed).IsEqual(1);
            AssertThat(verb.EchoPool.RawValue)
                .OverrideFailureMessage("A defied hit generates no Rally echo (damage -> Defy -> no echo).")
                .IsEqual(0L);
        }
        // Meter asymmetry is the sequential ordering made visible: player one's
        // intent applies first (their pre-shatter meter was spent nowhere, and
        // the second hit shatters it to zero); player two's OWN hit applies
        // AFTER their shatter and legitimately re-earns meter from the ~300
        // damage it dealt. Only the defied-victim guarantees are the ruling.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent firstSurvivor)).IsTrue();
        AssertThat(firstSurvivor.Influence.RawValue)
            .OverrideFailureMessage("Player one's shattered meter earns nothing after its own earlier hit.")
            .IsEqual(0L);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent secondSurvivor)).IsTrue();
        AssertThat(secondSurvivor.Influence.RawValue)
            .OverrideFailureMessage(
                "Player two's post-shatter hit re-earns full meter from its damage-dealt credit.")
            .IsEqual(FP64.FromInt(100).RawValue);
    }

    /// <summary>
    /// V7.3 ruling #9: Chronal Orbs are OFF in Sudden Death. A live orb at the
    /// buzzer is cleared on entry, and no orb spawns or awards for the rest of
    /// the match (hazards stay forced on, unchanged).
    /// </summary>
    [TestCase]
    public void SuddenDeathSpawnsAndAwardsNoOrbs() {
        // Florence's authored orb anchors sit on the platforms, away from two
        // grounded neutral fighters — the pre-buzzer orb provably survives to
        // the transition instead of being camped.
        var simulation = new FighterSimulation(
            matchSeconds: 13,
            seed: 24,
            rules: new FighterMatchRules((int)MatchMode.TimeLimit, true, 3, false, 0),
            stageGeometry: FighterStageGeometry.Florence);

        // High frequency spawns the first orb at 660 ticks; the buzzer is 780.
        for (int tick = 0; tick < 700; tick++) simulation.Advance(Neutral(tick), Neutral(tick));
        AssertThat(simulation.OrbCount)
            .OverrideFailureMessage("Harness: an orb must be live before the buzzer.")
            .IsEqual(1);
        AssertThat(simulation.GetMatchState().SuddenDeathActive).IsEqual(0);

        for (int tick = 700; tick < 790; tick++) simulation.Advance(Neutral(tick), Neutral(tick));
        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.SuddenDeathActive).IsEqual(1);
        AssertThat(simulation.OrbCount)
            .OverrideFailureMessage("Entering Sudden Death must clear every live orb.")
            .IsEqual(0);

        // Two more full spawn intervals: nothing spawns and nothing awards.
        for (int tick = 790; tick < 2200; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
            if (tick % 100 == 0) {
                AssertThat(simulation.OrbCount)
                    .OverrideFailureMessage("No orb may spawn while Sudden Death runs.")
                    .IsEqual(0);
            }
        }
        AssertThat(simulation.OrbCount).IsEqual(0);
        AssertThat(simulation.GetMatchState().SuddenDeathActive).IsEqual(1);
    }

    // === Presentation cannot touch deterministic state ===

    [TestCase]
    public void PacingAndDeferringAdvanceCallsCannotChangeSimulationState() {
        // The driver's KO sequence freezes, then advances at a fractional rate.
        // Both only decide *when* a tick is consumed. A simulation fed the same
        // ticks through that schedule must land on the identical hash as one fed
        // them back to back.
        var paced = new FighterSimulation(seed: 77, rules: CountdownRules());
        var plain = new FighterSimulation(seed: 77, rules: CountdownRules());

        int consumed = 0;
        double slowMotionCredit = 0.0;
        for (int presentationFrame = 0; presentationFrame < 400; presentationFrame++) {
            bool hitFreeze = presentationFrame < 30;
            if (hitFreeze) continue;
            slowMotionCredit += 0.75;
            while (slowMotionCredit >= 1.0) {
                slowMotionCredit -= 1.0;
                paced.Advance(Neutral(consumed), Neutral(consumed));
                consumed++;
            }
        }
        AssertThat(consumed > 0).IsTrue();
        for (int tick = 0; tick < consumed; tick++) plain.Advance(Neutral(tick), Neutral(tick));

        AssertThat(paced.CurrentTick).IsEqual(plain.CurrentTick);
        AssertThat(paced.CurrentHash).IsEqual(plain.CurrentHash);
    }

    /// <summary>
    /// Audit M-10: TimeLimit knockouts now trigger the same driver-side
    /// presentation beat (hit-freeze deferral, paced slow motion), so the purity
    /// proof must hold on the TimeLimit path too — including through a real
    /// knockout, when the beat actually fires. One fighter is driven off the
    /// bottom mid-schedule; the paced run and the back-to-back run must still
    /// land on the identical hash.
    /// </summary>
    [TestCase]
    public void PacedAdvanceCallsArePureOnTheTimeLimitKnockoutPath() {
        FighterMatchRules timeLimitRules = new FighterMatchRules(
            (int)MatchMode.TimeLimit, false, 0, false, 0);
        var paced = new FighterSimulation(seed: 91, matchSeconds: 60, rules: timeLimitRules);
        var plain = new FighterSimulation(seed: 91, matchSeconds: 60, rules: timeLimitRules);

        // The input stream drops player two through the floor and holds Down so
        // they ride out the bottom blast zone — a TimeLimit KO with no stock loss.
        static PlayerInputFrame FallInput(int tick) => tick == 0
            ? Frame(tick, 0, GameplayButtons.Jump | GameplayButtons.Down)
            : Frame(tick, 0, GameplayButtons.Down);

        int consumed = 0;
        double slowMotionCredit = 0.0;
        for (int presentationFrame = 0; presentationFrame < 500; presentationFrame++) {
            // The driver's stock-loss freeze defers frames outright; slow motion
            // then consumes them at a fractional rate.
            bool hitFreeze = presentationFrame is >= 200 and < 212;
            if (hitFreeze) continue;
            slowMotionCredit += 0.75;
            while (slowMotionCredit >= 1.0) {
                slowMotionCredit -= 1.0;
                paced.Advance(Neutral(consumed), FallInput(consumed));
                consumed++;
            }
        }
        AssertThat(consumed > 0).IsTrue();
        for (int tick = 0; tick < consumed; tick++) plain.Advance(Neutral(tick), FallInput(tick));

        // The knockout really happened on this schedule...
        AssertThat(paced.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.KnockoutsSuffered >= 1).IsTrue();
        // ...and pacing the ticks changed nothing about their content.
        AssertThat(paced.CurrentTick).IsEqual(plain.CurrentTick);
        AssertThat(paced.CurrentHash).IsEqual(plain.CurrentHash);
    }

    [TestCase]
    public void MatchSettingsSurviveTheMappingIntoDeterministicRules() {
        var settings = new MatchSettings {
            Mode = MatchMode.Hybrid,
            StockCount = 5,
            TimeLimit = 120f,
            ItemsEnabled = true,
            ItemSpawnRate = ChronalOrbFrequency.Low,
            StageHazardsEnabled = true,
            HazardRate = HazardTriggerFrequency.Medium
        };

        FighterMatchRules rules = FighterSimulationDriver.RulesFor(settings, 7);
        AssertThat(rules.MatchMode).IsEqual((int)MatchMode.Hybrid);
        AssertThat(rules.ItemsEnabled).IsTrue();
        AssertThat(rules.ItemFrequency).IsEqual((int)ChronalOrbFrequency.Low);
        AssertThat(rules.HazardsEnabled).IsTrue();
        AssertThat(rules.HazardFrequency).IsEqual((int)HazardTriggerFrequency.Medium);
        AssertThat(rules.StageHazardTypeID).IsEqual(7);
        AssertThat(rules.PreMatchCountdownFrames).IsEqual(FighterMatchFlowRules.CountdownFrames);
        AssertThat(FighterSimulationDriver.MatchSeconds(settings)).IsEqual(120);

        // Off collapses to disabled on both axes rather than spawning at rate 0.
        settings.ItemSpawnRate = ChronalOrbFrequency.Off;
        settings.HazardRate = HazardTriggerFrequency.Off;
        FighterMatchRules off = FighterSimulationDriver.RulesFor(settings, 3);
        AssertThat(off.ItemsEnabled).IsFalse();
        AssertThat(off.HazardsEnabled).IsFalse();

        // And the whole ruleset reaches the simulation intact.
        var simulation = new FighterSimulation(
            FighterLoadout.Default(FighterCharacterID.Einstein),
            FighterLoadout.Default(FighterCharacterID.Joan),
            stocks: settings.StockCount,
            matchSeconds: FighterSimulationDriver.MatchSeconds(settings),
            rules: rules);
        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.MatchMode).IsEqual((int)MatchMode.Hybrid);
        AssertThat(match.ItemFrequency).IsEqual((int)ChronalOrbFrequency.Low);
        AssertThat(match.HazardFrequency).IsEqual((int)HazardTriggerFrequency.Medium);
        AssertThat(match.StageHazardTypeID).IsEqual(7);
        AssertThat(match.RemainingFrames).IsEqual(120 * FighterSimulation.TickRate);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
        AssertThat(fighter.Stocks).IsEqual(5);
    }

    // === Helpers ===

    /// <summary>Zero-knockback fighters for the timeout pins: hits chip HP
    /// without moving anyone out of range.</summary>
    private static CharacterData TimeoutFighter(int maxHP, float damage) => new() {
        CharacterID = "tesla",
        MaxHP = maxHP,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = damage,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    /// <summary>
    /// The dual-Defy trade fighter: a 125-damage basic (hit one deals 100 —
    /// one meter-filling opener) and a lethal 900-damage generic melee
    /// Special1 for the trade itself. Zero knockback keeps both in range.
    /// </summary>
    private static CharacterData DefyTradeFighter() => new() {
        CharacterID = "tesla",
        MaxHP = 400,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 125f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData { BaseDamage = 900f },
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static FighterMatchRules CountdownRules() =>
        FighterMatchRules.Disabled.WithCountdown(FighterMatchFlowRules.CountdownFrames);

    /// <summary>A 1 HP pool so a single basic attack ends the stock deterministically.</summary>
    private static FighterLoadout OneHitPointLoadout() => new(
        (int)FighterCharacterID.Joan,
        1, 3, 2, 10, 14, 12, 20, 600, 600,
        (int)StatusType.None, 0, (int)StatusType.None, 0, (int)StatusType.None, 0,
        FP64.One, FP64.FromInt(8), FP64.FromInt(13),
        FP64.FromInt(3), FP64.FromInt(4), FP64.FromInt(4), FP64.FromInt(5),
        FP64.One, FP64.One, FP64.One,
        FighterAbilityLoadout.Default);

    /// <summary>
    /// Taps Down+Jump once to drop through the legacy flat floor, then holds only
    /// Down so the fall is not interrupted by an air jump, and rides it past the
    /// bottom blast zone — the bottom-fall stock-loss path.
    /// </summary>
    private static void DriveOffTheBottom(FighterSimulation simulation, int playerID) {
        for (int tick = 0; tick < 200; tick++) {
            PlayerInputFrame falling = tick == 0
                ? Frame(tick, 0, GameplayButtons.Jump | GameplayButtons.Down)
                : Frame(tick, 0, GameplayButtons.Down);
            simulation.Advance(
                playerID == 0 ? falling : Neutral(tick),
                playerID == 1 ? falling : Neutral(tick));
            if (simulation.TryGetFighter(playerID, out FighterStateComponent state)
                && FighterMatchFlowRules.IsOnRespawnPlatform(in state)) return;
            if (simulation.GetMatchState().MatchState == FighterMatchStates.Complete) return;
        }
        AssertThat(false).OverrideFailureMessage("Fighter never fell through the bottom blast zone.").IsTrue();
    }

    private static PlayerInputFrame Neutral(int tick) => Frame(tick, 0, GameplayButtons.None);

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };
}
