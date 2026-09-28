using System;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 12 W5 — the hazard On/Off toggle (2026-09-26 design). The
/// <c>HazardTriggerFrequency</c> selector is retired: with hazards On, each stage
/// runs its own authored cadence (seeded from the retired Medium interval, so a
/// default match behaves exactly as before), and Overtime / Sudden Death double it.
/// </summary>
[TestSuite]
public class FighterHazardCadenceTests {

    [TestCase]
    public void EveryStageHasAnAuthoredCadenceSeededFromTheRetiredMediumInterval() {
        for (int type = 1; type <= FighterHazardTypeID.Count; type++) {
            AssertThat(FighterHazardCadence.AuthoredFrames(type))
                .OverrideFailureMessage($"hazard type {type} has no authored cadence")
                .IsEqual(2700);
        }
        AssertThat(FighterHazardCadence.AuthoredFrames(0)).IsEqual(FighterHazardCadence.DefaultFrames);
    }

    [TestCase]
    public void RulesResolveTheStageCadenceAndRefuseAStaleFrequencyOrdinal() {
        var rules = new FighterMatchRules((int)MatchMode.Stock, false, 0, true, 0, FighterHazardTypeID.NassauMortar);
        AssertThat(rules.HazardCadenceFrames)
            .IsEqual(FighterHazardCadence.AuthoredFrames(FighterHazardTypeID.NassauMortar));
        // A test override is still honoured...
        AssertThat(new FighterMatchRules((int)MatchMode.Stock, false, 0, true, 1800, 1).HazardCadenceFrames)
            .IsEqual(1800);
        // ...but a leftover 1-3 frequency ordinal cannot silently become a cadence.
        foreach (int stale in new[] { 1, 2, 3, -5 }) {
            bool threw = false;
            try {
                _ = new FighterMatchRules((int)MatchMode.Stock, false, 0, true, stale, 1);
            } catch (ArgumentOutOfRangeException) {
                threw = true;
            }
            AssertThat(threw).OverrideFailureMessage($"cadence {stale} was accepted").IsTrue();
        }
    }

    [TestCase]
    public void HazardsOnFireAtTheAuthoredCadenceAndOffNeverFire() {
        var on = new FighterSimulation(seed: 31, rules: new FighterMatchRules(
            (int)MatchMode.Stock, false, 0, true, 0, 1), stageGeometry: FighterStageGeometry.Florence);
        int cadence = FighterHazardCadence.AuthoredFrames(1);
        AssertThat(on.GetMatchState().HazardCadenceFrames).IsEqual(cadence);
        int firstHazardTick = -1;
        for (int tick = 0; tick < cadence + 5 && firstHazardTick < 0; tick++) {
            on.Advance(Neutral(tick), Neutral(tick));
            if (on.HazardCount > 0) firstHazardTick = tick;
        }
        AssertThat(Math.Abs(firstHazardTick - (cadence - 1)) <= 1)
            .OverrideFailureMessage($"the first hazard landed at tick {firstHazardTick}, not on the {cadence}-frame boundary")
            .IsTrue();

        var off = new FighterSimulation(seed: 31, rules: new FighterMatchRules(
            (int)MatchMode.Stock, false, 0, false, 0, 1), stageGeometry: FighterStageGeometry.Florence);
        for (int tick = 0; tick < cadence * 2; tick++) {
            off.Advance(Neutral(tick), Neutral(tick));
            AssertThat(off.HazardCount).IsEqual(0);
        }
    }

    private static PlayerInputFrame Neutral(int tick) => new() { Tick = (uint)tick };
}
