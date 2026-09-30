using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FTT.Core;
using FTT.FighterSim;
using FTT.Networking;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 6 A4 — the rollback-readiness gate for Fighter Mode.
///
/// Everything here is deliberately Godot-free: the deterministic simulation, the
/// rollback session, and the in-memory transport are all pure C#, so the whole
/// gate runs in the .NET test host without an engine child process.
///
/// Coverage:
///  * delayed-input convergence for every kit shape on every authored stage
///    geometry, with items and hazards at High frequency;
///  * bounded rollback history (rejection past <see cref="FighterSimulation.RollbackHistoryTicks"/>,
///    depth-8 rejection with the too-late event, depth-7 acceptance);
///  * desync reporting on an induced divergence;
///  * the corrected-tick ring staying bounded over a long session;
///  * worst-case depth-7 resimulation cost against the 8 ms rollback budget and
///    the full-state snapshot size (recorded in docs/PERFORMANCE_BASELINE.md).
///
/// The authored stage geometries are enumerated by reflection rather than listed,
/// so stages added later are covered with no edit to this file.
/// </summary>
[TestSuite]
public class RollbackReadinessTests {

    // ---------------------------------------------------------------- harness

    /// <summary>
    /// Every authored <see cref="FighterStageGeometry"/> exposed as a public
    /// static member, ordered by name so the sweep is reproducible. Reflection is
    /// deliberate: the Package 6 stage workstream adds geometries as new static
    /// properties and this gate must pick them up without being edited.
    /// </summary>
    private static IReadOnlyList<FighterStageGeometry> AuthoredGeometries() {
        var found = new List<(string Name, FighterStageGeometry Geometry)>();
        Type type = typeof(FighterStageGeometry);
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Static)) {
            if (property.PropertyType != type || property.GetIndexParameters().Length > 0) continue;
            if (property.GetValue(null) is FighterStageGeometry geometry) found.Add((property.Name, geometry));
        }
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static)) {
            if (field.FieldType != type) continue;
            if (field.GetValue(null) is FighterStageGeometry geometry) found.Add((field.Name, geometry));
        }
        return found
            .GroupBy(entry => entry.Geometry)
            .Select(group => group.First())
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .Select(entry => entry.Geometry)
            .ToList();
    }

    /// <summary>Items and hazards at High frequency, Stock rules, per-stage hazard identity.</summary>
    private static FighterMatchRules HighFrequencyRules(FighterStageGeometry geometry) =>
        new(2, true, 3, true, 1800, HazardTypeFor(geometry));

    /// <summary>
    /// Distinct hazard identity per stage without depending on the catalog: the
    /// hazard type is clamped to 1..10 by <see cref="FighterMatchRules"/>, so a
    /// stable hash of the stage ID exercises a different type per stage.
    /// </summary>
    private static int HazardTypeFor(FighterStageGeometry geometry) {
        int accumulator = 0;
        string stageID = geometry.StageID ?? "";
        for (int index = 0; index < stageID.Length; index++) accumulator = accumulator * 31 + stageID[index];
        return Math.Abs(accumulator % 10) + 1;
    }

    /// <summary>
    /// Deterministic scripted input. Not idle: the generator walks, jumps,
    /// crouches, blocks, throws basics, both specials, the movement ability, and
    /// the ultimate, on per-player prime cycles so the two fighters desynchronize
    /// their action phases and cover overlapping entity lifetimes.
    /// </summary>
    private sealed class ScriptedInputs {
        private readonly int _seed;
        private readonly int _playerID;
        private readonly bool _spamSpecials;
        private GameplayButtons _previousHeld;

        /// <param name="spamSpecials">
        /// Presses both specials and the movement ability on their shortest legal
        /// cadence, for the worst-case entity-load harness.
        /// </param>
        public ScriptedInputs(int seed, int playerID, bool spamSpecials = false) {
            _seed = seed;
            _playerID = playerID;
            _spamSpecials = spamSpecials;
        }

        public PlayerInputFrame Next(int tick) {
            int noise = Noise(tick);
            float horizontal = ((tick / 37 + _playerID) & 1) == 0 ? 1f : -1f;
            if (noise % 11 == 0) horizontal = 0f;
            float vertical = noise % 23 == 0 ? -1f : noise % 29 == 0 ? 1f : 0f;

            int specialOnePeriod = _spamSpecials ? 31 : 91;
            int specialTwoPeriod = _spamSpecials ? 47 : 127;
            int movementPeriod = _spamSpecials ? 61 : 173;
            GameplayButtons held = GameplayButtons.None;
            if (noise % 13 == _playerID) held |= GameplayButtons.Jump;
            if (noise % 7 == _playerID) held |= GameplayButtons.BasicAttack;
            if (tick % specialOnePeriod == 5 + _playerID * 7) held |= GameplayButtons.Special1;
            if (tick % specialTwoPeriod == 11 + _playerID * 9) held |= GameplayButtons.Special2;
            if (tick % movementPeriod == 21 + _playerID * 13) held |= GameplayButtons.MovementAbility;
            if (noise % 17 == 3) held |= GameplayButtons.Block;
            if (tick % 401 == 90 + _playerID * 31) held |= GameplayButtons.Ultimate;
            if (vertical < 0f) held |= GameplayButtons.Down;

            PlayerInputFrame frame = PlayerInputFrame.Create((uint)tick, horizontal, vertical, held, _previousHeld);
            _previousHeld = held;
            return frame;
        }

        /// <summary>Deterministic integer noise; no Random, no float math.</summary>
        private int Noise(int tick) {
            unchecked {
                int value = tick * 1103515245 + _seed * 12345 + _playerID * 2654435;
                value ^= value >> 13;
                value *= 1274126177;
                value ^= value >> 16;
                return value & 0x7FFFFFFF;
            }
        }
    }

    private static FighterSimulation BuildSimulation(
        FighterCharacterID playerOne,
        FighterCharacterID playerTwo,
        FighterStageGeometry geometry,
        int seed,
        int stocks) =>
        new(
            RollbackHarnessKits.KitShape(playerOne),
            RollbackHarnessKits.KitShape(playerTwo),
            stocks: stocks,
            matchSeconds: 480,
            seed: seed,
            rules: HighFrequencyRules(geometry),
            stageGeometry: geometry);

    /// <summary>
    /// Runs one delayed-input rollback match between two peers and asserts the
    /// two independent simulations agree on every confirmed tick. Returns the
    /// peak observed hazard/orb counts so callers can prove the run crossed the
    /// spawn cycles it claims to cross.
    /// </summary>
    private static (int PeakHazards, int PeakOrbs) RunConvergence(
        FighterCharacterID playerOne,
        FighterCharacterID playerTwo,
        FighterStageGeometry geometry,
        int seed,
        int ticks,
        int latencyPolls,
        int stocks) {
        (InMemoryRollbackTransport firstTransport, InMemoryRollbackTransport secondTransport) =
            InMemoryRollbackTransport.CreatePair(latencyPolls);
        FighterSimulation firstSimulation = BuildSimulation(playerOne, playerTwo, geometry, seed, stocks);
        FighterSimulation secondSimulation = BuildSimulation(playerOne, playerTwo, geometry, seed, stocks);
        using var first = new OnlineRollbackSession(firstSimulation, firstTransport, 4400, 0);
        using var second = new OnlineRollbackSession(secondSimulation, secondTransport, 4400, 1);

        string context = $"{playerOne} vs {playerTwo} on '{geometry.StageID}'";
        int lateInputs = 0;
        int corrections = 0;
        first.InputArrivedTooLate += _ => lateInputs++;
        second.InputArrivedTooLate += _ => lateInputs++;
        first.RollbackCorrectionMeasured += (_, _) => corrections++;
        second.RollbackCorrectionMeasured += (_, _) => corrections++;

        var playerOneInputs = new ScriptedInputs(seed, 0);
        var playerTwoInputs = new ScriptedInputs(seed, 1);
        int peakHazards = 0;
        int peakOrbs = 0;

        for (int tick = 0; tick < ticks; tick++) {
            first.Advance(playerOneInputs.Next(tick));
            second.Advance(playerTwoInputs.Next(tick));
            peakHazards = Math.Max(peakHazards, firstSimulation.HazardCount);
            peakOrbs = Math.Max(peakOrbs, firstSimulation.OrbCount);
        }
        // Flush the in-flight window so both peers have applied every correction.
        for (int flush = 0; flush <= latencyPolls * 2 + 2; flush++) {
            first.PumpNetwork();
            second.PumpNetwork();
        }

        AssertThat(lateInputs)
            .OverrideFailureMessage($"{context}: {lateInputs} remote inputs arrived past the 7-frame rollback window.")
            .IsEqual(0);
        // Convergence is only meaningful if rollback actually ran; latency of
        // three polls must produce a correction on essentially every tick.
        AssertThat(corrections)
            .OverrideFailureMessage($"{context}: the run applied only {corrections} rollback corrections.")
            .IsGreater(ticks);
        AssertThat(firstSimulation.CurrentTick)
            .OverrideFailureMessage($"{context}: peers ended on different ticks.")
            .IsEqual(secondSimulation.CurrentTick);
        AssertThat(firstSimulation.CurrentHash)
            .OverrideFailureMessage($"{context}: peer state hashes diverged after rollback convergence.")
            .IsEqual(secondSimulation.CurrentHash);

        // Per-tick equality across the whole confirmed history still inside the
        // 120-tick window: a final-hash match alone could hide a transient split.
        int confirmedFrom = Math.Max(0, firstSimulation.CurrentTick - FighterSimulation.RollbackHistoryTicks + 1);
        for (int tick = confirmedFrom; tick < firstSimulation.CurrentTick; tick++) {
            AssertThat(firstSimulation.GetRecordedHash(tick))
                .OverrideFailureMessage($"{context}: recorded hashes differ at tick {tick}.")
                .IsEqual(secondSimulation.GetRecordedHash(tick));
        }
        return (peakHazards, peakOrbs);
    }

    // ------------------------------------------------- convergence sweep (A4.1)

    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    [TestCase(4, 4)]
    [TestCase(5, 5)]
    [TestCase(6, 6)]
    [TestCase(7, 7)]
    [TestCase(9, 9)]
    [TestCase(0, 5)]
    [TestCase(2, 4)]
    [TestCase(9, 3)]
    [TestCase(6, 7)]
    [TestCase(1, 2)]
    [TestCase(7, 0)]
    public void DelayedInputsConvergeForEveryKitOnEveryAuthoredStage(int playerOne, int playerTwo) {
        IReadOnlyList<FighterStageGeometry> geometries = AuthoredGeometries();
        AssertThat(geometries.Count).IsGreater(1);
        int seed = 6100 + playerOne * 13 + playerTwo;
        foreach (FighterStageGeometry geometry in geometries) {
            RunConvergence(
                (FighterCharacterID)playerOne,
                (FighterCharacterID)playerTwo,
                geometry,
                seed,
                ticks: 420,
                latencyPolls: 3,
                stocks: 9);
            seed++;
        }
    }

    /// <summary>
    /// The sweep above is short enough to keep the gate cheap per matchup; this
    /// run is long enough to cross a full High-frequency orb cycle (660 frames)
    /// and a full hazard cycle (1800 frames plus its 90-frame warning) on every
    /// authored stage, with headroom for a pre-match countdown phase.
    /// </summary>
    [TestCase]
    public void LongRunConvergenceCrossesHazardAndOrbSpawnCycles() {
        IReadOnlyList<FighterStageGeometry> geometries = AuthoredGeometries();
        int seed = 6900;
        foreach (FighterStageGeometry geometry in geometries) {
            (int peakHazards, int peakOrbs) = RunConvergence(
                FighterCharacterID.Tesla,
                FighterCharacterID.Leonardo,
                geometry,
                seed++,
                ticks: 2200,
                latencyPolls: 3,
                stocks: 30);
            AssertThat(peakHazards)
                .OverrideFailureMessage($"'{geometry.StageID}': no stage hazard spawned inside 2200 ticks.")
                .IsGreater(0);
            AssertThat(peakOrbs)
                .OverrideFailureMessage($"'{geometry.StageID}': no Chronal Orb spawned inside 2200 ticks.")
                .IsGreater(0);
        }
    }

    // --------------------------------------------- bounded-history proofs (A4.2)

    [TestCase]
    public void CorrectRemoteInputRejectsTicksOutsideTheHistoryWindow() {
        FighterSimulation simulation = BuildSimulation(
            FighterCharacterID.Einstein, FighterCharacterID.Joan, FighterStageGeometry.Florence, 4211, 3);
        var inputs = new ScriptedInputs(4211, 0);
        int ticks = FighterSimulation.RollbackHistoryTicks + 40;
        for (int tick = 0; tick < ticks; tick++) {
            simulation.Advance(inputs.Next(tick), new PlayerInputFrame { Tick = (uint)tick });
        }

        int current = simulation.CurrentTick;
        var correction = new PlayerInputFrame { MoveX = 100, Held = GameplayButtons.BasicAttack };

        // Inside the window: accepted and resimulated.
        AssertThat(simulation.CorrectRemoteInput(1, current - 10, correction)).IsTrue();
        // Exactly one tick past the retained history: rejected.
        AssertThat(simulation.CorrectRemoteInput(1, current - FighterSimulation.RollbackHistoryTicks - 1, correction))
            .IsFalse();
        // Far past the retained history, negative ticks, and the future: rejected.
        AssertThat(simulation.CorrectRemoteInput(1, 0, correction)).IsFalse();
        AssertThat(simulation.CorrectRemoteInput(1, -1, correction)).IsFalse();
        AssertThat(simulation.CorrectRemoteInput(1, current, correction)).IsFalse();
        AssertThat(simulation.CorrectRemoteInput(1, current + 5, correction)).IsFalse();
        // Unknown player slot: rejected without touching state.
        AssertThat(simulation.CorrectRemoteInput(2, current - 3, correction)).IsFalse();
    }

    [TestCase]
    public void DepthEightPacketIsReportedTooLateWhileDepthSevenIsApplied() {
        (InMemoryRollbackTransport localTransport, InMemoryRollbackTransport peerTransport) =
            InMemoryRollbackTransport.CreatePair();
        FighterSimulation simulation = BuildSimulation(
            FighterCharacterID.Einstein, FighterCharacterID.Joan, FighterStageGeometry.Florence, 4212, 3);
        using var session = new OnlineRollbackSession(simulation, localTransport, 5150, 0);
        var lateTicks = new List<int>();
        session.InputArrivedTooLate += tick => lateTicks.Add(tick);

        var inputs = new ScriptedInputs(4212, 0);
        for (int tick = 0; tick < 60; tick++) {
            session.Advance(inputs.Next(tick));
            // Drain the peer side so its queues do not accumulate.
            peerTransport.Poll();
            while (peerTransport.TryReceive(out _)) { }
        }

        int current = simulation.CurrentTick;
        long hashBeforeDepthSeven = simulation.CurrentHash;

        // The correction must be an input that provably perturbs state under the
        // shared combat rules. The default recipe's odd-tick variant is a Block
        // hold, which the grounded block stance now resolves as a legitimate
        // no-op for an idle fighter (movement locks to zero, nothing persists) —
        // a jump diverges the arc no matter what state the fighter is in.
        SendRemotePacket(peerTransport, 5150, current - OnlineRollbackSession.MaximumRollbackFrames,
            new PlayerInputFrame {
                MoveX = 100,
                Held = GameplayButtons.Jump,
                Pressed = GameplayButtons.Jump
            });
        session.PumpNetwork();
        AssertThat(lateTicks.Count)
            .OverrideFailureMessage("A depth-7 packet is inside the window and must not be reported too late.")
            .IsEqual(0);
        AssertThat(simulation.CurrentHash)
            .OverrideFailureMessage("A depth-7 correction must resimulate and change the confirmed state.")
            .IsNotEqual(hashBeforeDepthSeven);
        AssertThat(simulation.CurrentTick).IsEqual(current);

        long hashBeforeDepthEight = simulation.CurrentHash;
        SendRemotePacket(peerTransport, 5150, current - OnlineRollbackSession.MaximumRollbackFrames - 1);
        session.PumpNetwork();
        AssertThat(lateTicks.Count)
            .OverrideFailureMessage("A depth-8 packet exceeds MaximumRollbackFrames and must be reported too late.")
            .IsEqual(1);
        AssertThat(lateTicks[0]).IsEqual(current - OnlineRollbackSession.MaximumRollbackFrames - 1);
        AssertThat(simulation.CurrentHash)
            .OverrideFailureMessage("A too-late packet must not be applied.")
            .IsEqual(hashBeforeDepthEight);
    }

    [TestCase]
    public void DesyncDetectedFiresWhenConfirmedPeerHashesDiverge() {
        (InMemoryRollbackTransport firstTransport, InMemoryRollbackTransport secondTransport) =
            InMemoryRollbackTransport.CreatePair();
        // Same seed and stage, deliberately different loadouts: the peers cannot
        // agree on any confirmed hash, which is exactly what the exchange detects.
        var firstSimulation = new FighterSimulation(
            RollbackHarnessKits.KitShape(FighterCharacterID.Tesla),
            RollbackHarnessKits.KitShape(FighterCharacterID.Joan),
            seed: 4213, rules: FighterMatchRules.Disabled, stageGeometry: FighterStageGeometry.Florence);
        var secondSimulation = new FighterSimulation(
            RollbackHarnessKits.KitShape(FighterCharacterID.Tesla),
            RollbackHarnessKits.KitShape(FighterCharacterID.Lincoln),
            seed: 4213, rules: FighterMatchRules.Disabled, stageGeometry: FighterStageGeometry.Florence);
        using var first = new OnlineRollbackSession(firstSimulation, firstTransport, 5151, 0);
        using var second = new OnlineRollbackSession(secondSimulation, secondTransport, 5151, 1);

        var desyncs = new List<(int Tick, long Local, long Remote)>();
        first.DesyncDetected += (tick, local, remote) => desyncs.Add((tick, local, remote));

        var playerOneInputs = new ScriptedInputs(4213, 0);
        var playerTwoInputs = new ScriptedInputs(4213, 1);
        for (int tick = 0; tick < 80; tick++) {
            first.Advance(playerOneInputs.Next(tick));
            second.Advance(playerTwoInputs.Next(tick));
        }

        AssertThat(desyncs.Count)
            .OverrideFailureMessage("Divergent peers must raise DesyncDetected on the confirmed-hash exchange.")
            .IsGreater(0);
        AssertThat(desyncs[0].Local).IsNotEqual(desyncs[0].Remote);
        AssertThat(desyncs[0].Tick).IsGreaterEqual(0);

        // The production consumer's message names the tick and both hashes.
        string message = RollbackDiagnostics.FormatDesync(desyncs[0].Tick, desyncs[0].Local, desyncs[0].Remote);
        AssertThat(message).Contains(desyncs[0].Tick.ToString());
        AssertThat(message).Contains(desyncs[0].Local.ToString());
        AssertThat(message).Contains(desyncs[0].Remote.ToString());
    }

    // ---------------------------------------- corrected-tick ring bounds (A4.3)

    [TestCase]
    public void CorrectedTickTrackingStaysBoundedAndStillDeduplicates() {
        (InMemoryRollbackTransport localTransport, InMemoryRollbackTransport peerTransport) =
            InMemoryRollbackTransport.CreatePair();
        FighterSimulation simulation = BuildSimulation(
            FighterCharacterID.Einstein, FighterCharacterID.Joan, FighterStageGeometry.Florence, 4214, 9);
        using var session = new OnlineRollbackSession(simulation, localTransport, 5152, 0);

        var inputs = new ScriptedInputs(4214, 0);
        for (int tick = 0; tick < 3000; tick++) {
            session.Advance(inputs.Next(tick));
            peerTransport.Poll();
            while (peerTransport.TryReceive(out _)) { }
            int current = simulation.CurrentTick;
            if (current <= OnlineRollbackSession.MaximumRollbackFrames) continue;
            SendRemotePacket(peerTransport, 5152, current - OnlineRollbackSession.MaximumRollbackFrames);
            session.PumpNetwork();
            AssertThat(session.RetainedCorrectedTickCount)
                .OverrideFailureMessage($"Corrected-tick tracking grew past its bound at tick {current}.")
                .IsLessEqual(OnlineRollbackSession.CorrectedTickCapacity);
        }

        AssertThat(session.RetainedCorrectedTickCount).IsLessEqual(OnlineRollbackSession.CorrectedTickCapacity);

        // De-duplication inside the live window is preserved: a repeat packet for
        // an already-corrected tick must not resimulate again.
        int finalTick = simulation.CurrentTick;
        int repeatTick = finalTick - OnlineRollbackSession.MaximumRollbackFrames + 1;
        SendRemotePacket(peerTransport, 5152, repeatTick);
        session.PumpNetwork();
        long afterFirst = simulation.CurrentHash;
        int measuredCorrections = 0;
        session.RollbackCorrectionMeasured += (_, _) => measuredCorrections++;
        SendRemotePacket(peerTransport, 5152, repeatTick);
        session.PumpNetwork();
        AssertThat(measuredCorrections)
            .OverrideFailureMessage("A duplicate packet for an already-corrected tick must be ignored.")
            .IsEqual(0);
        AssertThat(simulation.CurrentHash).IsEqual(afterFirst);
    }

    // ----------------------------------- worst-case resimulation timing (A4.4)

    /// <summary>
    /// Forces a depth-7 correction on every frame while the simulation is
    /// carrying its worst-case entity load — two construct kits with live
    /// persistent objects, projectiles, an execution zone, an active stage
    /// hazard, and Chronal Orbs — and measures the resimulation cost through the
    /// session's own stopwatch. The assertion carries generous CI headroom; the
    /// recorded numbers are what matter and are transcribed into
    /// docs/PERFORMANCE_BASELINE.md.
    ///
    /// <para>Package 6 C1: the sweep covers <b>every</b> authored stage geometry
    /// rather than Florence alone. Each stage carries a different hazard identity
    /// (a moving debris body, a sweeping beam, a falling rock plus its ground pool,
    /// per-fighter dwell and idle counters), so "worst case" is not the same shape
    /// on every stage and a Florence-only measurement cannot stand in for the
    /// Package 7 entry criterion. Stages are enumerated from
    /// <c>FighterStageGeometry.AllAuthored</c>, so a stage added later is measured
    /// with no edit here. The test count is unchanged — one case, ten
    /// measurements.</para>
    /// </summary>
    [TestCase]
    public void WorstCaseDepthSevenResimulationFitsTheRollbackBudget() {
        double worstMedian = 0;
        double worstP95 = 0;
        int largestSnapshot = 0;
        string worstStage = "";

        foreach (FighterStageGeometry stage in FighterStageGeometry.AllAuthored) {
            (double median, double p95, int snapshotBytes) = MeasureWorstCaseDepthSeven(stage);
            if (median > worstMedian) { worstMedian = median; worstStage = stage.StageID; }
            worstP95 = Math.Max(worstP95, p95);
            largestSnapshot = Math.Max(largestSnapshot, snapshotBytes);
        }

        Console.WriteLine(
            "[A4 rollback readiness] worst case across "
            + $"{FighterStageGeometry.AllAuthored.Length} authored stages: "
            + $"median {worstMedian:F3} ms on '{worstStage}', p95 {worstP95:F3} ms, "
            + $"largest snapshot {largestSnapshot} bytes, budget "
            + $"{OnlineRollbackSession.RollbackBudgetMilliseconds:F1} ms.");

        AssertThat(worstStage.Length)
            .OverrideFailureMessage("The sweep measured no stage at all.").IsGreater(0);
        AssertThat(largestSnapshot)
            .OverrideFailureMessage("A full-state snapshot must be non-empty to be shippable over the wire.")
            .IsGreater(0);
    }

    /// <summary>
    /// One stage's depth-7 measurement. Returns (median ms, p95 ms, snapshot bytes)
    /// and asserts the per-stage budget on the way through.
    /// </summary>
    private static (double Median, double P95, int SnapshotBytes) MeasureWorstCaseDepthSeven(
        FighterStageGeometry geometry) {
        (InMemoryRollbackTransport localTransport, InMemoryRollbackTransport peerTransport) =
            InMemoryRollbackTransport.CreatePair();
        // Tesla contributes two persistent coils plus an execution zone; Mozart
        // contributes two projectile specials plus a Float platform construct.
        // With hazards and orbs at High that covers every entity family the
        // simulation can carry at once.
        FighterSimulation simulation = BuildSimulation(
            FighterCharacterID.Tesla, FighterCharacterID.Mozart, geometry, 4215, 30);
        using var session = new OnlineRollbackSession(simulation, localTransport, 5153, 0);

        var samples = new List<double>();
        var budgetBreaches = new List<double>();
        session.RollbackCorrectionMeasured += (depth, milliseconds) => {
            AssertThat(depth).IsEqual(OnlineRollbackSession.MaximumRollbackFrames);
            samples.Add(milliseconds);
        };
        session.RollbackBudgetExceeded += (_, milliseconds) => budgetBreaches.Add(milliseconds);

        // Warm up past the first High-frequency hazard spawn (1800 frames) and its
        // 90-frame warning so the measured window runs with a live hazard, live
        // orbs, and both kits' constructs deployed.
        const int WarmupTicks = 1900;
        const int MeasuredTicks = 300;
        var localInputs = new ScriptedInputs(4215, 0, spamSpecials: true);
        var remoteScript = new ScriptedInputs(4215, 1, spamSpecials: true);
        var remoteFrames = new PlayerInputFrame[WarmupTicks + MeasuredTicks];
        for (int tick = 0; tick < remoteFrames.Length; tick++) remoteFrames[tick] = remoteScript.Next(tick);

        for (int tick = 0; tick < WarmupTicks; tick++) {
            session.Advance(localInputs.Next(tick));
            DrainPeer(peerTransport);
            int warmupTick = simulation.CurrentTick;
            if (warmupTick <= OnlineRollbackSession.MaximumRollbackFrames) continue;
            // Feed the remote kit through the same seven-frame-late path so it
            // deploys its own constructs, zones, and projectiles during warm-up.
            int correctedTick = warmupTick - OnlineRollbackSession.MaximumRollbackFrames;
            SendRemotePacket(peerTransport, 5153, correctedTick, remoteFrames[correctedTick]);
            session.PumpNetwork();
        }
        samples.Clear();
        budgetBreaches.Clear();

        int peakEntities = 0;
        int peakProjectiles = 0;
        int peakConstructs = 0;
        int peakZones = 0;
        int peakHazards = 0;
        int peakOrbs = 0;
        for (int tick = WarmupTicks; tick < WarmupTicks + MeasuredTicks; tick++) {
            session.Advance(localInputs.Next(tick));
            DrainPeer(peerTransport);
            int current = simulation.CurrentTick;
            int correctedTick = current - OnlineRollbackSession.MaximumRollbackFrames;
            SendRemotePacket(peerTransport, 5153, correctedTick, remoteFrames[correctedTick]);
            session.PumpNetwork();
            peakProjectiles = Math.Max(peakProjectiles, simulation.ProjectileCount);
            peakConstructs = Math.Max(peakConstructs, simulation.PersistentObjectCount);
            peakZones = Math.Max(peakZones, simulation.ZoneCount);
            peakHazards = Math.Max(peakHazards, simulation.HazardCount);
            peakOrbs = Math.Max(peakOrbs, simulation.OrbCount);
            peakEntities = Math.Max(
                peakEntities,
                simulation.ProjectileCount + simulation.PersistentObjectCount
                    + simulation.ZoneCount + simulation.HazardCount + simulation.OrbCount);
        }

        AssertThat(samples.Count)
            .OverrideFailureMessage("The timing harness applied no depth-7 corrections.")
            .IsGreaterEqual(MeasuredTicks - 5);
        AssertThat(peakEntities)
            .OverrideFailureMessage("The worst-case window carried no simulated entities.")
            .IsGreater(0);

        samples.Sort();
        double median = samples[samples.Count / 2];
        double p95 = samples[(int)(samples.Count * 0.95)];
        double max = samples[^1];
        double total = 0;
        foreach (double sample in samples) total += sample;
        double mean = total / samples.Count;
        byte[] snapshot = simulation.CaptureFullState();

        Console.WriteLine(
            "[A4 rollback readiness] depth-7 resimulation over "
            + $"{samples.Count} corrections on '{geometry.StageID}': "
            + $"median {median:F3} ms, mean {mean:F3} ms, p95 {p95:F3} ms, max {max:F3} ms; "
            + $"budget {OnlineRollbackSession.RollbackBudgetMilliseconds:F1} ms; "
            + $"breaches {budgetBreaches.Count}; snapshot {snapshot.Length} bytes; "
            + $"peak entities {peakEntities} (projectiles {peakProjectiles}, constructs {peakConstructs}, "
            + $"zones {peakZones}, hazards {peakHazards}, orbs {peakOrbs}).");

        // Fail only well outside the budget so ordinary CI variance cannot flap
        // the gate; the recorded medians are the number that matters.
        AssertThat(p95 <= OnlineRollbackSession.RollbackBudgetMilliseconds * 2.0)
            .OverrideFailureMessage(
                $"Depth-7 resimulation p95 on '{geometry.StageID}' was {p95:F3} ms, beyond twice the "
                + $"{OnlineRollbackSession.RollbackBudgetMilliseconds:F1} ms rollback budget "
                + $"(median {median:F3} ms, max {max:F3} ms).")
            .IsTrue();
        AssertThat(budgetBreaches.Count)
            .OverrideFailureMessage(
                $"'{geometry.StageID}' breached the rollback budget {budgetBreaches.Count} times.")
            .IsEqual(0);
        AssertThat(snapshot.Length)
            .OverrideFailureMessage("A full-state snapshot must be non-empty to be shippable over the wire.")
            .IsGreater(0);
        return (median, p95, snapshot.Length);
    }

    [TestCase]
    public void FullStateSnapshotRoundTripsUnderWorstCaseLoad() {
        FighterStageGeometry geometry = FighterStageGeometry.Florence;
        FighterSimulation source = BuildSimulation(
            FighterCharacterID.Tesla, FighterCharacterID.Mozart, geometry, 4216, 30);
        FighterSimulation restored = BuildSimulation(
            FighterCharacterID.Tesla, FighterCharacterID.Mozart, geometry, 4216, 30);

        const int SnapshotTick = 1950;
        const int ReplayTicks = 250;
        var playerOneInputs = new ScriptedInputs(4216, 0, spamSpecials: true);
        var playerTwoInputs = new ScriptedInputs(4216, 1, spamSpecials: true);
        var scriptOne = new PlayerInputFrame[SnapshotTick + ReplayTicks];
        var scriptTwo = new PlayerInputFrame[SnapshotTick + ReplayTicks];
        for (int tick = 0; tick < scriptOne.Length; tick++) {
            scriptOne[tick] = playerOneInputs.Next(tick);
            scriptTwo[tick] = playerTwoInputs.Next(tick);
        }

        for (int tick = 0; tick < SnapshotTick; tick++) {
            source.Advance(scriptOne[tick], scriptTwo[tick]);
        }

        byte[] snapshot = source.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(source.CurrentHash);

        // A restored peer must stay bit-identical: this is the state-transfer
        // path a Package 7 resync would depend on.
        for (int tick = SnapshotTick; tick < scriptOne.Length; tick++) {
            long expected = source.Advance(scriptOne[tick], scriptTwo[tick]);
            long actual = restored.Advance(scriptOne[tick], scriptTwo[tick]);
            AssertThat(actual)
                .OverrideFailureMessage($"Restored simulation diverged at tick {tick}.")
                .IsEqual(expected);
        }
    }

    // ------------------------------------------------------------------ helper

    private static void SendRemotePacket(InMemoryRollbackTransport peerTransport, uint sessionID, int tick) {
        SendRemotePacket(peerTransport, sessionID, tick, new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = (sbyte)((tick % 5) * 20 - 40),
            Held = (tick & 1) == 0 ? GameplayButtons.BasicAttack : GameplayButtons.Block,
            Pressed = (tick & 1) == 0 ? GameplayButtons.BasicAttack : GameplayButtons.Block
        });
    }

    private static void SendRemotePacket(
        InMemoryRollbackTransport peerTransport, uint sessionID, int tick, PlayerInputFrame input) {
        input.Tick = (uint)tick;
        // HashTick -1 keeps the confirmed-hash exchange out of these synthetic
        // packets; this helper only exercises the rollback path.
        var packet = new RollbackInputPacket(sessionID, (uint)(tick + 1), 0, 1, tick, input, -1, 0L);
        peerTransport.Send(packet.Serialize());
    }

    private static void DrainPeer(InMemoryRollbackTransport peerTransport) {
        peerTransport.Poll();
        while (peerTransport.TryReceive(out _)) { }
    }
}

/// <summary>
/// Construct-, projectile-, and zone-heavy deterministic loadouts, one per roster
/// slot, built purely in C# so the rollback gate never needs a Godot runtime.
///
/// The *shape* of each slot — special execution types, persistent construct IDs,
/// movement type — mirrors that character's authored kit, and
/// <c>RollbackReadinessKitShapeTests</c> pins the mirror against the `.tres`
/// resources. The tuning numbers here are harness values chosen to maximize
/// entity load; the authored resources remain the only canonical source of
/// balance numbers and nothing in this table feeds production.
/// </summary>
internal static class RollbackHarnessKits {

    public static FighterLoadout KitShape(FighterCharacterID characterID) {
        (int SpecialOne, int SpecialTwo, int Movement, int PersistentOne, int PersistentTwo, int MovementPersistent) shape =
            characterID switch {
                // (special 1 exec, special 2 exec, movement type, s1 construct, s2 construct, movement construct)
                FighterCharacterID.Einstein => (1, 2, 4, 0, 0, 0),
                FighterCharacterID.Joan => (1, 0, 10, 0, 0, 0),
                FighterCharacterID.Leonardo => (2, 3, 1, 0, 2, 0),
                FighterCharacterID.Lincoln => (1, 0, 2, 0, 0, 0),
                FighterCharacterID.Cleopatra => (3, 2, 11, 3, 0, 0),
                FighterCharacterID.Tesla => (3, 2, 0, 1, 0, 0),
                // Package 13 W7a (A08): Prospero's Flight is a gust burst (MovementType 6).
                FighterCharacterID.Shakespeare => (1, 2, 6, 0, 0, 0),
                FighterCharacterID.Mozart => (1, 1, 5, 0, 0, 5),
                // Package 13 W5: Tubman (Conductor's Call projectile, Foresight
                // melee stance, North Star Leap authored as Blink); ordinal 8 is
                // the reserved Pocahontas slot and falls to the default arm.
                FighterCharacterID.Tubman => (1, 0, 0, 0, 0, 0),
                _ => (0, 0, 2, 0, 0, 0)
            };

        var abilities = new FighterAbilityLoadout {
            SpecialOneExecutionType = shape.SpecialOne,
            SpecialTwoExecutionType = shape.SpecialTwo,
            SpecialOneProjectileLifetimeFrames = 180,
            SpecialTwoProjectileLifetimeFrames = 180,
            SpecialOnePersistentTypeID = shape.PersistentOne,
            SpecialOneMaxActiveObjects = shape.PersistentOne == 0 ? 0 : 2,
            SpecialOnePersistentLifetimeFrames = 600,
            SpecialTwoPersistentTypeID = shape.PersistentTwo,
            SpecialTwoMaxActiveObjects = shape.PersistentTwo == 0 ? 0 : 2,
            SpecialTwoPersistentLifetimeFrames = 600,
            SpecialOneTickIntervalFrames = shape.SpecialOne is 2 or 3 ? 30 : 0,
            SpecialTwoTickIntervalFrames = shape.SpecialTwo is 2 or 3 ? 30 : 0,
            MovementType = shape.Movement,
            MovementCooldownFrames = 120,
            MovementDurationFrames = 18,
            MovementResetsJump = 1,
            MovementGrantsHyperArmor = 0,
            MovementPersistentTypeID = shape.MovementPersistent,
            MovementMaxActiveObjects = shape.MovementPersistent == 0 ? 0 : 1,
            MovementPersistentLifetimeFrames = 300,
            SpecialOneProjectileSpeed = FP64.FromDouble(0.25),
            SpecialTwoProjectileSpeed = FP64.FromDouble(0.2),
            MovementDistance = FP64.FromDouble(2.5),
            MovementSpeed = FP64.FromInt(10),
            // Package 13 W7a: E=mc² and Yorick's Lament are two-stage bursting
            // projectiles, so the gate resimulates the burst primitive too.
            SpecialOneHit = characterID is FighterCharacterID.Einstein or FighterCharacterID.Shakespeare
                ? FighterAbilityHitData.DefaultSpecial with {
                    BurstRadius = FP64.FromDouble(1.2),
                    ContactDamage = 5,
                    BurstsOnTerrain = true
                }
                : default
        };

        return new FighterLoadout(
            (int)characterID,
            100, 3, 2,
            10, 12, 11, 20,
            60, 90,
            (int)StatusType.None, 0,
            (int)StatusType.None, 0,
            (int)StatusType.None, 0,
            FP64.One,
            FP64.FromInt(8),
            FP64.FromInt(13),
            FP64.FromInt(3),
            FP64.FromInt(4),
            FP64.FromInt(4),
            FP64.FromInt(5),
            FP64.One, FP64.One, FP64.One,
            abilities);
    }
}
