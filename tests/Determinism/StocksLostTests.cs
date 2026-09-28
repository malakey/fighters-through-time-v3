using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 11 A1c. F21's Time-mode decider: <b>fewest stocks lost</b>, unlimited
/// respawns, no HP tiebreak and no attacker credit
/// (<c>docs/design-contracts/FIGHTER_MATCH_RULES.md</c>).
///
/// <para>What was wrong: <c>FighterMatchComponent.PlayerOneKOs</c> counted the KOs
/// player one <em>scored</em> (<c>TrackKnockouts</c> credited the opponent), and the
/// Time-mode winner test picked <c>PlayerOneKOs &gt; PlayerTwoKOs ? 0 : 1</c> —
/// "most KOs scored wins", the opposite bookkeeping to the contract. The right
/// quantity already existed as the victim-side
/// <c>FighterRuntimeComponent.KnockoutsSuffered</c>; only the comparison and the
/// naming were wrong.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StocksLostTests {

    /// <summary>
    /// Every cause reaches the same victim-side chokepoint. A blast-zone fall and a
    /// hazard death are authored differently and count identically — no attacker
    /// credit, no ownership window, no self-KO penalty.
    /// </summary>
    [TestCase]
    public void EveryCauseIncrementsTheVictimsTotal() {
        // A self-inflicted pit fall: nobody attacked, and it still counts.
        var simulation = new FighterSimulation(
            stocks: 5, matchSeconds: 600,
            rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));
        DriveOffTheBottom(simulation, 1);

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.PlayerTwoStocksLost)
            .OverrideFailureMessage("A self-inflicted pit fall must raise the VICTIM's total.")
            .IsEqual(1);
        AssertThat(match.PlayerOneStocksLost)
            .OverrideFailureMessage("The opponent scored nothing; their total must stay 0.")
            .IsEqual(0);

        // A hazard death on the same rules: same chokepoint, same increment.
        var hazardous = new FighterSimulation(
            stocks: 5, matchSeconds: 600, seed: 90210,
            rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, true, 1800, 1));
        DriveOffTheBottom(hazardous, 0);
        AssertThat(hazardous.GetMatchState().PlayerOneStocksLost).IsEqual(1);
    }

    /// <summary>
    /// A single death is a single increment however it is caused, and the counter
    /// tracks the victim's own <c>KnockoutsSuffered</c> exactly — duplicate
    /// callbacks cannot repeat it, because there is only one place that raises it.
    /// </summary>
    [TestCase]
    public void RepeatedDeathsTrackTheVictimCounterExactly() {
        var simulation = new FighterSimulation(
            stocks: 5, matchSeconds: 600,
            rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));

        for (int fall = 1; fall <= 3; fall++) {
            DriveOffTheBottom(simulation, 1);
            // Ride the respawn platform out so the next fall is a clean one.
            for (int tick = 0; tick < 420; tick++) {
                int current = simulation.CurrentTick;
                simulation.Advance(Neutral(current), Neutral(current));
            }
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
            AssertThat(simulation.GetMatchState().PlayerTwoStocksLost)
                .OverrideFailureMessage($"After fall {fall} the match total left the victim counter behind.")
                .IsEqual(runtime.KnockoutsSuffered);
        }
        AssertThat(simulation.GetMatchState().PlayerTwoStocksLost).IsEqual(3);
    }

    /// <summary>
    /// Time mode has unlimited respawns: the stock pool never decrements, so the
    /// stocks-lost total is the only quantity that moves.
    /// </summary>
    [TestCase]
    public void TimeModeRespawnsAreUnlimitedAndTheStockPoolNeverMoves() {
        var simulation = new FighterSimulation(
            stocks: 1, matchSeconds: 600,
            rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        DriveOffTheBottom(simulation, 1);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.Stocks)
            .OverrideFailureMessage("Time mode must never decrement a finite life pool.")
            .IsEqual(before.Stocks);
        AssertThat(simulation.GetMatchState().MatchState).IsEqual(FighterMatchStates.InProgress);
        AssertThat(simulation.GetMatchState().PlayerTwoStocksLost).IsEqual(1);
    }

    /// <summary>
    /// The smaller total wins, irrespective of who or what caused the losses — and
    /// with no HP tiebreak. Player two falls once and is left on full HP; player one
    /// never falls and is chipped down. Fewest losses still takes it.
    /// </summary>
    [TestCase]
    public void FewestStocksLostWinsWithNoHPTiebreak() {
        var simulation = new FighterSimulation(
            stocks: 5, matchSeconds: 10, seed: 5150,
            rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));
        DriveOffTheBottom(simulation, 1);

        while (simulation.GetMatchState().MatchState == FighterMatchStates.InProgress
            && simulation.CurrentTick < 900) {
            int tick = simulation.CurrentTick;
            simulation.Advance(Neutral(tick), Neutral(tick));
        }

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.MatchState).IsEqual(FighterMatchStates.Complete);
        AssertThat(match.PlayerOneStocksLost).IsEqual(0);
        AssertThat(match.PlayerTwoStocksLost).IsEqual(1);
        AssertThat(match.WinnerPlayerID)
            .OverrideFailureMessage("Time mode must be decided by fewest stocks lost.")
            .IsEqual(0);
        AssertThat(match.IsTrueTie).IsEqual(0);
    }

    /// <summary>
    /// Equal totals — including 0-0, where nothing happened all match — enter
    /// Sudden Death rather than comparing HP. This is the case the retired
    /// "most KOs scored wins" comparison got backwards by construction.
    /// </summary>
    [TestCase]
    public void EqualTotalsIncludingZeroZeroEnterSuddenDeath() {
        var simulation = new FighterSimulation(
            stocks: 5, matchSeconds: 2,
            rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));
        for (int tick = 0; tick < 130; tick++) simulation.Advance(Neutral(tick), Neutral(tick));

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.PlayerOneStocksLost).IsEqual(0);
        AssertThat(match.PlayerTwoStocksLost).IsEqual(0);
        AssertThat(match.SuddenDeathActive)
            .OverrideFailureMessage("A 0-0 Time tie must enter Sudden Death, not record a result.")
            .IsEqual(1);
        AssertThat(match.WinnerPlayerID).IsEqual(-1);
    }

    /// <summary>
    /// Sudden Death never touches the regulation totals: they are frozen at entry
    /// and the decider's own death does not add to them.
    /// </summary>
    [TestCase]
    public void SuddenDeathFreezesTheRegulationTotals() {
        // The regulation window has to outlast BOTH falls plus the respawn platform
        // between them. Six seconds did not: the buzzer went while the second fall
        // was still in progress, Sudden Death entered, and its ready countdown
        // cleared the fall inputs — the fighter simply stood at spawn.
        var simulation = new FighterSimulation(
            stocks: 5, matchSeconds: 30, seed: 7,
            rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));
        DriveOffTheBottom(simulation, 0);
        RideOutTheRespawnPlatform(simulation, 0);
        DriveOffTheBottom(simulation, 1);
        RideOutTheRespawnPlatform(simulation, 1);

        while (simulation.GetMatchState().SuddenDeathActive == 0 && simulation.CurrentTick < 2400) {
            int tick = simulation.CurrentTick;
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
        // Ride out the phase's ready countdown so the decider is actually live.
        for (int step = 0; step < FighterMatchFlowRules.CountdownFrames + 2; step++) {
            int tick = simulation.CurrentTick;
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
        FighterSuddenDeathComponent phase = simulation.GetSuddenDeathState();
        AssertThat(phase.PhaseGeneration).IsEqual(1);
        AssertThat(phase.FrozenPlayerOneStocksLost).IsEqual(1);
        AssertThat(phase.FrozenPlayerTwoStocksLost).IsEqual(1);

        // A death inside the phase decides the match and changes neither total.
        DriveOffTheBottom(simulation, 1);
        FighterSuddenDeathComponent after = simulation.GetSuddenDeathState();
        AssertThat(after.FrozenPlayerOneStocksLost).IsEqual(1);
        AssertThat(after.FrozenPlayerTwoStocksLost).IsEqual(1);
        AssertThat(simulation.GetMatchState().PlayerTwoStocksLost)
            .OverrideFailureMessage("A Sudden Death death must not reopen the regulation counter.")
            .IsEqual(1);
    }

    // === Helpers ===

    /// <summary>
    /// Wait out the respawn platform so the next fall starts from solid ground.
    /// A fighter still held by the platform ignores input entirely.
    /// </summary>
    private static void RideOutTheRespawnPlatform(FighterSimulation simulation, int playerID) {
        for (int step = 0; step < 600; step++) {
            AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent state)).IsTrue();
            if (!FighterMatchFlowRules.IsOnRespawnPlatform(in state) && state.IsGrounded != 0) return;
            int tick = simulation.CurrentTick;
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
    }

    /// <summary>Drop through the arena floor and ride out the bottom blast zone.</summary>
    private static void DriveOffTheBottom(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent start)).IsTrue();
        int before = start.KnockoutsSuffered;
        for (int offset = 0; offset < 400; offset++) {
            int tick = simulation.CurrentTick;
            PlayerInputFrame falling = offset == 0
                ? Frame(tick, 0, GameplayButtons.Jump | GameplayButtons.Down)
                : Frame(tick, 0, GameplayButtons.Down);
            PlayerInputFrame one = playerID == 0 ? falling : Neutral(tick);
            PlayerInputFrame two = playerID == 1 ? falling : Neutral(tick);
            simulation.Advance(one, two);
            AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
            if (runtime.KnockoutsSuffered > before) return;
            if (simulation.GetMatchState().MatchState == FighterMatchStates.Complete) return;
        }
        AssertThat(false)
            .OverrideFailureMessage($"Harness: player {playerID}'s fall never reached the blast zone.")
            .IsTrue();
    }

    private static PlayerInputFrame Neutral(int tick) => Frame(tick, 0, GameplayButtons.None);

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };
}
