using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Meter and Defy update 2026-10-31 (plan 2.12, O23, P24, P25; ledger <c>USER-DEFY-CPU-BY-BAND</c>,
/// <c>PROVISIONAL-CPU-DEFY-REFLEX</c>): the CPU's Defy, band by band. Easy and the Easy boss never; Medium
/// rolls once per hitstun instance once the string reaches 3 hits and only at Influence 50+; Hard takes no
/// roll and Defies on a string of 2+ hits, cornered, or against an A02 wind-up within 3.0 u, and
/// otherwise banks its meter. The scenarios are hand-built observations through the recorded-input path;
/// the last case runs a real <see cref="FighterSimulation"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CpuDefyPolicyTests {
    /// <summary>Influence above the 50 cost and below Hard's 70 reserve (GR-CPU, ruling 9d): where Hard still spends its meter on Defy.</summary>
    private const int BelowReserve = 60;

    [TestCase]
    public void EasyAndTheEasyBossNeverDefyAndDrawNothingFromTheRandomStream() {
        AssertThat(CpuBandTuning.Easy.DefyStringPercent).IsEqual(0);
        AssertThat(CpuBandTuning.EasyBoss.DefyStringPercent).IsEqual(0);
        AssertThat(CpuBandTuning.Easy.DefyCornerEscape || CpuBandTuning.Easy.DefyBeatsUltimateWindup).IsFalse();
        AssertThat(CpuBandTuning.EasyBoss.DefyCornerEscape || CpuBandTuning.EasyBoss.DefyBeatsUltimateWindup).IsFalse();

        foreach (CpuBandTuning band in new[] { CpuBandTuning.Easy, CpuBandTuning.EasyBoss }) {
            // A long string at full meter, and the same string with an empty meter: identical frames
            // prove no Defy and no extra draw (a shifted stream would change the later defense rolls).
            List<PlayerInputFrame> full = RunStrings(band with { HitstunDefensePercent = 50 }, 100, 6, 4, 2, out int fullDefies);
            List<PlayerInputFrame> empty = RunStrings(band with { HitstunDefensePercent = 50 }, 0, 6, 4, 2, out _);
            AssertThat(fullDefies).OverrideFailureMessage("Easy must never Defy.").IsEqual(0);
            AssertThat(SameFrames(full, empty))
                .OverrideFailureMessage("Easy drew from the random stream for a Defy it can never make.")
                .IsTrue();
        }
    }

    [TestCase]
    public void MediumRollsOncePerHitstunInstanceFromThreeHitsAndOnlyAtFiftyInfluence() {
        AssertThat(CpuBandTuning.Normal.DefyStringPercent).IsEqual(35);
        AssertThat(CpuBandTuning.Normal.DefyMinStringHits).IsEqual(3);
        AssertThat(CpuBandTuning.Normal.DefyCornerEscape || CpuBandTuning.Normal.DefyBeatsUltimateWindup).IsFalse();

        // Strings of four hits (a gap of 4 actionable frames between), 60 of them.
        CpuBandTuning medium = CpuBandTuning.Normal;
        List<PlayerInputFrame> frames = RunStrings(medium, 100, 60, 4, 4, out int defies, out int[] byIndex);
        AssertThat(byIndex[0]).OverrideFailureMessage("Hit 1 of a string is below the 3-hit gate.").IsEqual(0);
        AssertThat(byIndex[1]).OverrideFailureMessage("Hit 2 of a string is below the 3-hit gate.").IsEqual(0);
        AssertThat(byIndex[2] + byIndex[3]).OverrideFailureMessage("A 35% roll must fire sometimes in 120 chances.").IsGreater(0);
        AssertThat(defies).OverrideFailureMessage("and not on every instance").IsLess(100);
        AssertThat(frames.Count > 0).IsTrue();

        // Once per instance: a Defy never repeats inside one instance.
        AssertThat(MaxPressesInOneInstance(medium, 100)).IsLessEqual(1);

        // Below 50 Influence the gate fails before the roll: no Defy, and the stream matches a band
        // that has no Defy at all (an extra draw would shift the later hitstun-defense rolls).
        CpuBandTuning blind = medium with { HitstunDefensePercent = 50 };
        CpuBandTuning none = blind with { DefyStringPercent = 0 };
        List<PlayerInputFrame> under = RunStrings(blind, 49, 10, 4, 4, out int underDefies);
        List<PlayerInputFrame> never = RunStrings(none, 49, 10, 4, 4, out _);
        AssertThat(underDefies).OverrideFailureMessage("49 Influence cannot pay the Defy.").IsEqual(0);
        AssertThat(SameFrames(under, never)).OverrideFailureMessage("The draw happens only when the gate passes.").IsTrue();
        List<PlayerInputFrame> at = RunStrings(blind, 50, 10, 4, 4, out int atDefies);
        AssertThat(atDefies).OverrideFailureMessage("50 Influence pays the Defy.").IsGreater(0);
    }

    [TestCase]
    public void HardDefiesWithoutARollOnASecondHitWithinTheStringGapAndNotAfterIt() {
        AssertThat(CpuBandTuning.Hard.DefyStringPercent).IsEqual(100);
        AssertThat(CpuBandTuning.Hard.DefyMinStringHits).IsEqual(2);

        // Two hits 8 actionable frames apart are one string: the second Defies, the first does not.
        RunStrings(CpuBandTuning.Hard, BelowReserve, 1, 2, FighterCpuController.DefyStringGapFrames, out int joined, out int[] byIndex);
        AssertThat(byIndex[0]).OverrideFailureMessage("Hit 1 is below the 2-hit gate.").IsEqual(0);
        AssertThat(byIndex[1]).OverrideFailureMessage("Hit 2 of a string Defies with no roll.").IsEqual(1);
        AssertThat(joined).IsEqual(1);

        // Nine actionable frames end the string: hit 2 is a fresh hit 1.
        RunStrings(CpuBandTuning.Hard, BelowReserve, 1, 2, FighterCpuController.DefyStringGapFrames + 1, out int split, out _);
        AssertThat(split).OverrideFailureMessage("A gap of 9 frames starts a new string.").IsEqual(0);

        // No roll at all (below the 70 reserve, where Hard still spends its meter): every one of 40 strings Defies on its second hit, and 49 Influence never does.
        RunStrings(CpuBandTuning.Hard, BelowReserve, 40, 2, 3, out int every, out _);
        AssertThat(every).IsEqual(40);
        RunStrings(CpuBandTuning.Hard, 49, 40, 2, 3, out int broke, out _);
        AssertThat(broke).IsEqual(0);

        // No draw: Hard on single hits (below the gate) consumes nothing a no-Defy Hard would.
        CpuBandTuning noDefy = CpuBandTuning.Hard with {
            DefyStringPercent = 0, DefyCornerEscape = false, DefyBeatsUltimateWindup = false
        };
        List<PlayerInputFrame> a = RunStrings(CpuBandTuning.Hard, BelowReserve, 12, 1, 4, out _);
        List<PlayerInputFrame> b = RunStrings(noDefy, BelowReserve, 12, 1, 4, out _);
        AssertThat(SameFrames(a, b)).OverrideFailureMessage("Hard's Defy takes no roll and no draw.").IsTrue();
    }

    [TestCase]
    public void HardDefiesWhenCorneredWithTheTargetOnTheOpenSideAndNotOtherwise() {
        CpuDecisionObservation cornered = Neutral(selfX: -9.0, targetX: -7.0, influence: BelowReserve);
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, cornered, 90)).OverrideFailureMessage("cornered Hard Defies").IsGreater(0);

        // The target between the CPU and the wall is not the open side.
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, Neutral(-9.0, -9.9, BelowReserve), 90)).IsEqual(0);
        // Out of the 3.0 u range, mid-stage, a short meter, or a band without the corner flag.
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, Neutral(-9.0, -5.0, BelowReserve), 90)).IsEqual(0);
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, Neutral(0.0, 2.0, BelowReserve), 90)).IsEqual(0);
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, cornered with { InfluenceRaw = FP64.FromInt(49).RawValue }, 90)).IsEqual(0);
        AssertThat(CountDefyPresses(CpuBandTuning.Normal, cornered, 90)).IsEqual(0);
        AssertThat(CountDefyPresses(CpuBandTuning.Hard with { DefyCornerEscape = false }, cornered, 90)).IsEqual(0);
        // The right wall mirrors it.
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, Neutral(9.0, 7.0, BelowReserve), 90)).IsGreater(0);
    }

    [TestCase]
    public void ADefyScheduledBeforeADazeIsDeliveredDuringItBecauseTheSimAllowsDefyInDaze() {
        // Decided on tick 0 (cornered, target on the open side), delivered after the 4-8 tick reaction delay
        // while the CPU is dazed. FighterDefyRules.CanBegin permits Defy in daze, so the press must go out.
        CpuDecisionObservation cornered = Neutral(selfX: -9.0, targetX: -7.0, influence: BelowReserve);
        CpuDecisionObservation dazed = cornered;
        dazed.DazeFrames = 60;
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 4242, null, null, CpuBandTuning.Hard);
        PlayerInputFrame previous = default;
        int presses = 0;
        for (uint tick = 0; tick < 90; tick++) {
            CpuDecisionObservation observation = tick == 0 ? cornered : dazed;
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.Defy)) presses++;
        }
        AssertThat(presses).OverrideFailureMessage("A Defy scheduled before a daze is stripped by the CPU's own legality check.").IsGreater(0);
    }

    [TestCase]
    public void TheThreeUnitDefyTriggersMeasureDistanceInBothAxes() {
        // Same X as a cornered CPU's open-side target, 3+ units above: out of range.
        CpuDecisionObservation cornered = Neutral(-9.0, -7.0, BelowReserve);
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, cornered, 90)).IsGreater(0);
        CpuDecisionObservation above = cornered;
        above.TargetPositionYRaw = FP64.FromInt(3).RawValue;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, above, 90))
            .OverrideFailureMessage("A target 3 u above (3.6 u away) is not within 3.0 u.").IsEqual(0);
        CpuDecisionObservation slightlyAbove = cornered;
        slightlyAbove.TargetPositionYRaw = FP64.FromDouble(1.5).RawValue;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, slightlyAbove, 90)).IsGreater(0);

        CpuDecisionObservation windup = Neutral(0.0, 0.0, 100, bounds: false);
        windup.HasTargetVerbState = 1;
        windup.TargetUltimateActivationPhase = FighterUltimateActivationRules.PhaseWindup;
        windup.TargetUltimateActivationPhaseFrames = 18;
        windup.TargetPositionYRaw = FP64.FromDouble(2.5).RawValue;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, windup, 60)).IsGreater(0);
        windup.TargetPositionYRaw = FP64.FromInt(3).RawValue + 1;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, windup, 60))
            .OverrideFailureMessage("A wind-up 3+ u above at the same X is out of range.").IsEqual(0);
    }

    [TestCase]
    public void HardDefiesAnUltimateWindupWithinThreeUnitsAndOtherwiseBanksItsMeter() {
        CpuDecisionObservation windup = Neutral(0.0, 2.5, 100, bounds: false);
        windup.HasTargetVerbState = 1;
        windup.TargetUltimateActivationPhase = FighterUltimateActivationRules.PhaseWindup;
        windup.TargetUltimateActivationPhaseFrames = 18;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, windup, 60)).OverrideFailureMessage("wind-up within 3.0 u").IsGreater(0);

        CpuDecisionObservation far = windup;
        far.TargetPositionXRaw = FP64.FromDouble(3.5).RawValue;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, far, 60)).OverrideFailureMessage("beyond 3.0 u").IsEqual(0);
        CpuDecisionObservation late = windup;
        late.TargetUltimateActivationPhaseFrames = 5;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, late, 60))
            .OverrideFailureMessage("too late for the blast to land inside the wind-up").IsEqual(0);
        CpuDecisionObservation striking = windup;
        striking.TargetUltimateActivationPhase = FighterUltimateActivationRules.PhaseActive;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, striking, 60)).IsEqual(0);
        CpuDecisionObservation poor = windup;
        poor.InfluenceRaw = FP64.FromInt(30).RawValue;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, poor, 60)).IsEqual(0);
        AssertThat(CountDefyPresses(CpuBandTuning.Normal, windup, 60)).IsEqual(0);
        AssertThat(CountDefyPresses(CpuBandTuning.Hard with { DefyBeatsUltimateWindup = false }, windup, 60)).IsEqual(0);

        // Otherwise it banks: a full meter, no trigger, 900 ticks of neutral play, no Defy.
        CpuDecisionObservation calm = Neutral(0.0, 1.5, 100, bounds: false);
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, calm, 900)).OverrideFailureMessage("Hard banks its meter").IsEqual(0);
    }

    [TestCase]
    public void AHardCpusDefyCancelsAnUltimateWindupInARealSimulationAndWithoutItTheStrikeLands() {
        SimOutcome with = RunWindup(CpuBandTuning.Hard);
        AssertThat(with.CpuDefied).OverrideFailureMessage("The Hard CPU must press its Defy against the wind-up.").IsTrue();
        AssertThat(with.ReachedActive).OverrideFailureMessage("The blast cancels the wind-up before the strike (R3).").IsFalse();
        AssertThat(with.CpuHp).OverrideFailureMessage("and no strike ever landed on the Defier").IsEqual(with.StartHp);
        // The meter was spent at acceptance and the cancel refunds nothing: only a few points of passive
        // gain have crept back in by the tick the wind-up ends (the seeded 100 is gone).
        AssertThat(with.CasterInfluence >= 0 && with.CasterInfluence < FP64.FromInt(10).RawValue)
            .OverrideFailureMessage($"the caster's meter stays spent (read raw {with.CasterInfluence})")
            .IsTrue();

        SimOutcome without = RunWindup(CpuBandTuning.Hard with { DefyBeatsUltimateWindup = false });
        AssertThat(without.CpuDefied).IsFalse();
        AssertThat(without.ReachedActive || without.CpuHp < without.StartHp)
            .OverrideFailureMessage($"Without the Defy trigger the wind-up runs on into its strike. {without.Trace} hp {without.CpuHp}/{without.StartHp}")
            .IsTrue();
    }

    // === GR-CPU: the Hard meter reserve (ruling 9d) and the Mirror exemption (9d+) ===================

    [TestCase]
    public void HardKeepsASeventyInfluenceReserveWithA25PercentNearDeathExceptionAndNoOtherBandDoes() {
        AssertThat(CpuBandTuning.Hard.DefyReserveInfluence).IsEqual(70);
        AssertThat(CpuBandTuning.Hard.DefyNearDeathHPPercent).IsEqual(25);
        foreach (CpuBandTuning band in new[] { CpuBandTuning.Easy, CpuBandTuning.Normal, CpuBandTuning.EasyBoss, CpuBandTuning.HardBoss }) {
            AssertThat(band.DefyReserveInfluence).OverrideFailureMessage("only the practice Hard CPU holds a reserve").IsEqual(0);
        }
        // The exemption is a band of its own: Hard with the reserve off and nothing else changed, and the
        // Level 13 clone's override on Hard returns it (BossOverride(Hard) used to be Hard itself).
        AssertThat(CpuBandTuning.HardBoss with { DefyReserveInfluence = 70, DefyNearDeathHPPercent = 25 }).IsEqual(CpuBandTuning.Hard);
        AssertThat(CpuBandTuning.BossOverride(CpuDifficulty.Hard).Equals(CpuBandTuning.HardBoss)).IsTrue();
        AssertThat(CpuBandTuning.BossOverride(CpuDifficulty.Normal).Equals(CpuBandTuning.Normal)).IsTrue();
        AssertThat(CpuBandTuning.BossOverride(CpuDifficulty.Easy).Equals(CpuBandTuning.EasyBoss)).IsTrue();
    }

    [TestCase]
    public void HardBanksItsMeterAtTheReserveInsteadOfDefyingAStringAndSpendsItJustBelow() {
        // 69 Influence is under the reserve: the second hit of a string Defies. 70 is at it: no Defy.
        RunStrings(CpuBandTuning.Hard, 69, 20, 2, 3, out int under, out _);
        AssertThat(under).OverrideFailureMessage("69 Influence is under the reserve").IsEqual(20);
        RunStrings(CpuBandTuning.Hard, 70, 20, 2, 3, out int at, out _);
        AssertThat(at).OverrideFailureMessage("70 Influence is the reserve: the meter is kept for the Ultimate").IsEqual(0);
        RunStrings(CpuBandTuning.Hard, 100, 20, 4, 3, out int full, out _);
        AssertThat(full).OverrideFailureMessage("a long string at a full meter still does not spend it").IsEqual(0);

        // The gate sits before any draw: a reserved Hard string leaves the stream exactly as a Hard with no Defy.
        CpuBandTuning noDefy = CpuBandTuning.Hard with { DefyStringPercent = 0 };
        AssertThat(SameFrames(
                RunStrings(CpuBandTuning.Hard, 100, 12, 3, 4, out _),
                RunStrings(noDefy, 100, 12, 3, 4, out _)))
            .OverrideFailureMessage("the reserve blocks the reflex without drawing").IsTrue();
    }

    [TestCase]
    public void HardDefiesAtTheReserveOnlyWhenOwnHpIsAtOrBelowTwentyFivePercent() {
        // HitObservation carries 100/100 HP; near death is 25/100 and below.
        AssertThat(CountReservedStringDefies(hp: 25)).OverrideFailureMessage("25 % is near death").IsEqual(10);
        AssertThat(CountReservedStringDefies(hp: 1)).IsEqual(10);
        AssertThat(CountReservedStringDefies(hp: 26)).OverrideFailureMessage("26 % is not").IsEqual(0);
        AssertThat(CountReservedStringDefies(hp: 100)).IsEqual(0);

        // The same exception opens the corner branch.
        CpuDecisionObservation cornered = Neutral(selfX: -9.0, targetX: -7.0, influence: 100);
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, cornered, 90)).OverrideFailureMessage("full meter, full HP: banked").IsEqual(0);
        CpuDecisionObservation wounded = cornered;
        wounded.SelfCurrentHP = 25;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, wounded, 90)).OverrideFailureMessage("full meter, near death: Defy").IsGreater(0);
        wounded.SelfCurrentHP = 26;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, wounded, 90)).IsEqual(0);
    }

    [TestCase]
    public void TheReserveBlocksTheCornerDefyAtSeventyAndNeverTheUltimateWindupOrAnUnreservedBand() {
        CpuDecisionObservation cornered = Neutral(selfX: -9.0, targetX: -7.0, influence: 70);
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, cornered, 90)).OverrideFailureMessage("70 Influence is reserved").IsEqual(0);
        cornered.InfluenceRaw = FP64.FromInt(69).RawValue;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, cornered, 90)).OverrideFailureMessage("69 is not").IsGreater(0);

        // An A02 wind-up in range always passes, whatever the meter and HP: a reserved CPU still beats the strike.
        CpuDecisionObservation windup = Neutral(0.0, 2.5, 100, bounds: false);
        windup.HasTargetVerbState = 1;
        windup.TargetUltimateActivationPhase = FighterUltimateActivationRules.PhaseWindup;
        windup.TargetUltimateActivationPhaseFrames = 18;
        AssertThat(CountDefyPresses(CpuBandTuning.Hard, windup, 60)).OverrideFailureMessage("the wind-up always passes").IsGreater(0);

        // And in a string: the same wind-up in range lets a reserved CPU's reflex through; out of range it does not.
        AssertThat(CountReservedStringDefies(hp: 100, windupTarget: true)).IsEqual(10);
        AssertThat(CountReservedStringDefies(hp: 100, windupTarget: true, farTarget: true)).IsEqual(0);

        // Normal has no reserve at all: its 3-hit string roll is untouched at a full meter.
        RunStrings(CpuBandTuning.Normal, 100, 60, 4, 4, out int normalDefies);
        AssertThat(normalDefies).IsGreater(0);
    }

    [TestCase]
    public void TheMirrorCloneOnHardKeepsDefyingStringsAtAFullMeterBecauseItsBandHasNoReserve() {
        CpuBandTuning clone = CpuBandTuning.BossOverride(CpuDifficulty.Hard);
        RunStrings(clone, 100, 20, 2, 3, out int cloneDefies, out _);
        AssertThat(cloneDefies).OverrideFailureMessage("the Level 13 clone is exempt (9d+): unchanged behaviour").IsEqual(20);
        CpuDecisionObservation cornered = Neutral(selfX: -9.0, targetX: -7.0, influence: 100);
        AssertThat(CountDefyPresses(clone, cornered, 90)).IsGreater(0);
        // ... where the practice Hard CPU banks the same strings.
        RunStrings(CpuBandTuning.Hard, 100, 20, 2, 3, out int practiceDefies, out _);
        AssertThat(practiceDefies).IsEqual(0);
    }

    // === helpers ===============================================================

    private sealed record SimOutcome(bool CpuDefied, bool ReachedActive, int CpuHp, int StartHp, long CasterInfluence, string Trace);

    private static SimOutcome RunWindup(CpuBandTuning tuning) {
        var simulation = new FighterSimulation(
            FighterLoadout.Default(FighterCharacterID.Joan), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 929, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        AssertThat(simulation.SeedFighterInfluenceForTest(1, 50)).IsTrue();
        var cpu = new FighterCpuController(
            CpuDifficulty.Hard, 77, null, new FighterSimulationWorldObserver(simulation), tuning);
        simulation.TryGetFighter(1, out FighterStateComponent start);
        bool defied = false;
        bool reachedActive = false;
        bool sawWindup = false;
        long influenceAtEnd = -1;
        var trace = new System.Text.StringBuilder();
        int lastPhase = -1;
        PlayerInputFrame previous = default;
        for (int tick = 0; tick < 150; tick++) {
            simulation.TryGetFighter(0, out FighterStateComponent selfOne);
            simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtimeOne);
            simulation.TryGetFighter(1, out FighterStateComponent selfTwo);
            simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtimeTwo);
            PlayerInputFrame cpuFrame = cpu.Sample(
                (uint)simulation.CurrentTick, in selfTwo, in runtimeTwo, in selfOne, in runtimeOne, in previous);
            previous = cpuFrame;
            GameplayButtons press = tick == 0 ? GameplayButtons.Ultimate : GameplayButtons.None;
            simulation.Advance(
                new PlayerInputFrame { Tick = (uint)simulation.CurrentTick, Held = press, Pressed = press }, cpuFrame);
            simulation.TryGetFighterActionState(1, out FighterActionStateComponent action);
            if (action.DefyFrames > 0) defied = true;
            simulation.TryGetFighterUltimateActivation(0, out FighterUltimateActivationComponent activation);
            if (activation.Phase == FighterUltimateActivationRules.PhaseActive
                || activation.Phase == FighterUltimateActivationRules.PhaseCinematic) reachedActive = true;
            if (activation.Phase == FighterUltimateActivationRules.PhaseWindup) sawWindup = true;
            if (activation.Phase != lastPhase) { trace.Append($"[t{tick}:p{activation.Phase}]"); lastPhase = activation.Phase; }
            if (sawWindup && influenceAtEnd < 0 && activation.Phase != FighterUltimateActivationRules.PhaseWindup) {
                simulation.TryGetFighter(0, out FighterStateComponent atEnd);
                influenceAtEnd = atEnd.Influence.RawValue;
            }
        }
        simulation.TryGetFighter(1, out FighterStateComponent end);
        return new SimOutcome(defied, reachedActive, end.CurrentHP, start.CurrentHP, influenceAtEnd, trace.ToString());
    }

    /// <summary>
    /// Ten 2-hit strings of a Hard CPU holding 100 Influence (over the reserve) with <paramref name="hp"/> of 100 HP;
    /// the number of Defy presses. Optionally the opponent stands in an A02 wind-up, near or beyond 3.0 u.
    /// </summary>
    private static int CountReservedStringDefies(int hp, bool windupTarget = false, bool farTarget = false) {
        // With a wind-up target the neutral Defy triggers are off, so only the string reflex can press.
        CpuBandTuning band = windupTarget
            ? CpuBandTuning.Hard with { DefyBeatsUltimateWindup = false, DefyCornerEscape = false }
            : CpuBandTuning.Hard;
        var cpu = new FighterCpuController(CpuDifficulty.Normal, 31, null, null, band);
        PlayerInputFrame previous = default;
        uint tick = 1;
        int defies = 0;
        for (int stringIndex = 0; stringIndex < 10; stringIndex++) {
            for (int hit = 0; hit < 2; hit++) {
                for (int frame = 20; frame >= 1; frame--) {
                    CpuDecisionObservation observation = HitObservation(frame, 100);
                    observation.SelfCurrentHP = hp;
                    if (windupTarget) {
                        observation.HasTargetVerbState = 1;
                        observation.TargetUltimateActivationPhase = FighterUltimateActivationRules.PhaseWindup;
                        observation.TargetUltimateActivationPhaseFrames = 18;
                        observation.TargetPositionXRaw = FP64.FromDouble(farTarget ? 3.5 : 2.5).RawValue;
                    }
                    previous = cpu.Sample(tick++, in observation, in previous);
                    if (previous.IsPressed(GameplayButtons.Defy)) defies++;
                }
                int rest = hit == 1 ? 40 : 3;
                for (int frame = 0; frame < rest; frame++) {
                    CpuDecisionObservation observation = HitObservation(0, 100);
                    observation.SelfCurrentHP = hp;
                    previous = cpu.Sample(tick++, in observation, in previous);
                    if (previous.IsPressed(GameplayButtons.Defy)) defies++;
                }
            }
        }
        return defies;
    }


    private static CpuDecisionObservation HitObservation(int hitstun, int influence) => new() {
        Stocks = 3,
        SelfCurrentHP = 100,
        SelfMaxHP = 100,
        IsGrounded = 1,
        HitstunFrames = hitstun,
        InfluenceRaw = FP64.FromInt(influence).RawValue,
        TargetPositionXRaw = FP64.FromInt(4).RawValue
    };

    private static CpuDecisionObservation Neutral(double selfX, double targetX, int influence, bool bounds = true) {
        var observation = new CpuDecisionObservation {
            Stocks = 3,
            SelfCurrentHP = 100,
            SelfMaxHP = 100,
            IsGrounded = 1,
            InfluenceRaw = FP64.FromInt(influence).RawValue,
            SelfPositionXRaw = FP64.FromDouble(selfX).RawValue,
            TargetPositionXRaw = FP64.FromDouble(targetX).RawValue,
            TargetCurrentHP = 100,
            TargetMaxHP = 100
        };
        if (bounds) {
            observation.HasStageBounds = 1;
            observation.LeftWallRaw = FP64.FromInt(-10).RawValue;
            observation.RightWallRaw = FP64.FromInt(10).RawValue;
        }
        return observation;
    }

    private static int CountDefyPresses(CpuBandTuning tuning, CpuDecisionObservation observation, int ticks) {
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 4242, null, null, tuning);
        PlayerInputFrame previous = default;
        int presses = 0;
        for (uint tick = 0; tick < ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.Defy)) presses++;
        }
        return presses;
    }

    private static List<PlayerInputFrame> RunStrings(
        CpuBandTuning tuning, int influence, int strings, int hitsPerString, int gap, out int defies) =>
        RunStrings(tuning, influence, strings, hitsPerString, gap, out defies, out _);

    /// <summary>
    /// Plays <paramref name="strings"/> strings of <paramref name="hitsPerString"/> 20-frame hitstun
    /// instances, <paramref name="gap"/> actionable frames between hits and 40 between strings, and
    /// counts the Defy presses (overall, and by the hit's index inside its string).
    /// </summary>
    private static List<PlayerInputFrame> RunStrings(
        CpuBandTuning tuning, int influence, int strings, int hitsPerString, int gap,
        out int defies, out int[] perInstance) {
        var cpu = new FighterCpuController(CpuDifficulty.Normal, 31, null, null, tuning);
        var frames = new List<PlayerInputFrame>();
        perInstance = new int[hitsPerString];
        defies = 0;
        PlayerInputFrame previous = default;
        uint tick = 1;
        for (int s = 0; s < strings; s++) {
            for (int hit = 0; hit < hitsPerString; hit++) {
                for (int frame = 20; frame >= 1; frame--) {
                    CpuDecisionObservation observation = HitObservation(frame, influence);
                    previous = cpu.Sample(tick++, in observation, in previous);
                    frames.Add(previous);
                    if (previous.IsPressed(GameplayButtons.Defy)) { defies++; perInstance[hit]++; }
                }
                int rest = hit == hitsPerString - 1 ? 40 : gap;
                for (int frame = 0; frame < rest; frame++) {
                    CpuDecisionObservation observation = HitObservation(0, influence);
                    previous = cpu.Sample(tick++, in observation, in previous);
                    frames.Add(previous);
                    if (previous.IsPressed(GameplayButtons.Defy)) { defies++; }
                }
            }
        }
        return frames;
    }

    private static int MaxPressesInOneInstance(CpuBandTuning tuning, int influence) {
        var cpu = new FighterCpuController(CpuDifficulty.Normal, 31, null, null, tuning);
        PlayerInputFrame previous = default;
        uint tick = 1;
        int worst = 0;
        for (int instance = 0; instance < 200; instance++) {
            int presses = 0;
            for (int frame = 20; frame >= 1; frame--) {
                CpuDecisionObservation observation = HitObservation(frame, influence);
                previous = cpu.Sample(tick++, in observation, in previous);
                if (previous.IsPressed(GameplayButtons.Defy)) presses++;
            }
            if (presses > worst) worst = presses;
            for (int frame = 0; frame < 4; frame++) {
                CpuDecisionObservation observation = HitObservation(0, influence);
                previous = cpu.Sample(tick++, in observation, in previous);
            }
        }
        return worst;
    }

    private static bool SameFrames(List<PlayerInputFrame> a, List<PlayerInputFrame> b) {
        if (a.Count != b.Count) return false;
        for (int index = 0; index < a.Count; index++) {
            if (a[index].Held != b[index].Held || a[index].MoveX != b[index].MoveX || a[index].MoveY != b[index].MoveY) {
                return false;
            }
        }
        return true;
    }
}
