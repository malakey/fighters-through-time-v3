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
    public void TimerExpiryWithIdenticalStateIsATrueDrawAndNamesNoWinner() {
        var simulation = new FighterSimulation(
            matchSeconds: 1, rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));
        for (int tick = 0; tick < 60; tick++) simulation.Advance(Neutral(tick), Neutral(tick));

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.MatchState).IsEqual(FighterMatchStates.Complete);
        AssertThat(match.IsTrueTie).IsEqual(1);
        AssertThat(match.WinnerPlayerID).IsEqual(-1);
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
