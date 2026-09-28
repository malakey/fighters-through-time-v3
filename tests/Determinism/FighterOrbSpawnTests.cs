using System.Collections.Generic;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Random;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 12 W5 — M23 and M24 (design §10 "Chronal Orb Pickups"). Orbs spawn at
/// a seeded pick among the stage's authored anchors, skip an anchor already
/// holding an orb, spawn at a seeded time inside the frequency window with a
/// strict 600-frame minimum gap, live 15 s, and — only behind the Items
/// sub-toggle "Meter pickups" (adopted D5(b)) — include Resonance Surge (+15
/// meter) as effect type 4. Every draw threads the match's single RandomState.
/// </summary>
[TestSuite]
public class FighterOrbSpawnTests {

    private const int RunTicks = 7200;

    [TestCase]
    public void EverySpawnGapDrawStaysInsideItsWindowAndAboveTheTenSecondFloor() {
        var random = new DeterministicRandom(77);
        for (int frequency = 1; frequency <= 3; frequency++) {
            (int min, int max) = FighterOrbSpawnRules.Window(frequency);
            int lowest = int.MaxValue, highest = int.MinValue;
            for (int draw = 0; draw < 2000; draw++) {
                int gap = FighterOrbSpawnRules.DrawSpawnGap(ref random, frequency);
                AssertThat(gap >= FighterOrbSpawnRules.MinimumGapFrames && gap >= min && gap <= max)
                    .OverrideFailureMessage($"frequency {frequency} drew {gap}, outside [{min}, {max}]")
                    .IsTrue();
                if (gap < lowest) lowest = gap;
                if (gap > highest) highest = gap;
            }
            // Timing is genuinely drawn, not a metronome.
            AssertThat(highest > lowest)
                .OverrideFailureMessage($"frequency {frequency} never varied its gap").IsTrue();
        }
        // Off (and unknown) spawn nothing.
        AssertThat(FighterOrbSpawnRules.DrawSpawnGap(ref random, 0)).IsEqual(0);
        AssertThat(FighterOrbSpawnRules.LifetimeFrames).IsEqual(900);
    }

    [TestCase]
    public void OrbsSpawnAtFreeAuthoredAnchorsWithAtLeastTenSecondsBetweenThemAndExpireAtFifteen() {
        FighterSimulation simulation = NewSimulation(seed: 4101, meterPickups: false);
        FighterStageGeometry geometry = FighterStageGeometry.Florence;
        var firstSeen = new Dictionary<int, int>();
        var lastSeen = new Dictionary<int, int>();
        var orbs = new List<FighterOrbComponent>();
        for (int tick = 0; tick < RunTicks; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
            orbs.Clear();
            simulation.CopyOrbsTo(orbs);
            var occupied = new HashSet<long>();
            foreach (FighterOrbComponent orb in orbs) {
                if (!firstSeen.ContainsKey(orb.EntityID)) firstSeen[orb.EntityID] = tick;
                lastSeen[orb.EntityID] = tick;
                AssertThat(IsAuthoredAnchor(geometry, orb.Position))
                    .OverrideFailureMessage($"orb {orb.EntityID} spawned off the authored anchors").IsTrue();
                long key = orb.Position.x.RawValue * 31 + orb.Position.y.RawValue;
                AssertThat(occupied.Add(key))
                    .OverrideFailureMessage($"two live orbs share an anchor at tick {tick}").IsTrue();
                AssertThat(orb.EffectType is >= 0 and <= 3)
                    .OverrideFailureMessage("Resonance Surge must stay out of the default draw").IsTrue();
            }
        }

        AssertThat(firstSeen.Count >= 3)
            .OverrideFailureMessage($"only {firstSeen.Count} orbs spawned in {RunTicks} ticks").IsTrue();
        var spawnTicks = new List<int>(firstSeen.Values);
        spawnTicks.Sort();
        for (int index = 1; index < spawnTicks.Count; index++) {
            int gap = spawnTicks[index] - spawnTicks[index - 1];
            AssertThat(gap >= FighterOrbSpawnRules.MinimumGapFrames)
                .OverrideFailureMessage($"consecutive spawns only {gap} frames apart").IsTrue();
        }
        foreach (KeyValuePair<int, int> entry in firstSeen) {
            int life = lastSeen[entry.Key] - entry.Value + 1;
            AssertThat(life <= FighterOrbSpawnRules.LifetimeFrames)
                .OverrideFailureMessage($"orb {entry.Key} lived {life} frames").IsTrue();
        }
    }

    [TestCase]
    public void TheOrbScheduleIsSeededIdenticalAcrossPeersAndDiffersAcrossSeeds() {
        List<int> first = SpawnTicks(NewSimulation(seed: 900, meterPickups: false));
        List<int> again = SpawnTicks(NewSimulation(seed: 900, meterPickups: false));
        AssertThat(string.Join(",", again)).IsEqual(string.Join(",", first));

        bool anyDifferent = false;
        for (int seed = 901; seed < 906 && !anyDifferent; seed++) {
            List<int> other = SpawnTicks(NewSimulation(seed: seed, meterPickups: false));
            anyDifferent = string.Join(",", other) != string.Join(",", first);
        }
        AssertThat(anyDifferent)
            .OverrideFailureMessage("M23: spawn timing must come from the match seed, not a fixed interval")
            .IsTrue();
    }

    [TestCase]
    public void MeterPickupsAddResonanceSurgeToTheDrawOnlyWhenEnabled() {
        FighterSimulation simulation = NewSimulation(seed: 5150, meterPickups: true);
        AssertThat(simulation.GetMatchState().MeterPickupsEnabled).IsEqual(1);
        AssertThat(FighterOrbSpawnRules.EffectTypeCount(false)).IsEqual(4);
        AssertThat(FighterOrbSpawnRules.EffectTypeCount(true)).IsEqual(5);

        bool sawSurge = false;
        var orbs = new List<FighterOrbComponent>();
        for (int seed = 5150; seed < 5170 && !sawSurge; seed++) {
            FighterSimulation run = NewSimulation(seed, meterPickups: true);
            for (int tick = 0; tick < RunTicks && !sawSurge; tick++) {
                run.Advance(Neutral(tick), Neutral(tick));
                orbs.Clear();
                run.CopyOrbsTo(orbs);
                foreach (FighterOrbComponent orb in orbs) {
                    if (orb.EffectType == FighterOrbSystem.ResonanceSurgeEffectType) sawSurge = true;
                }
            }
        }
        AssertThat(sawSurge)
            .OverrideFailureMessage("With Meter pickups on, Resonance Surge never entered the draw").IsTrue();
    }

    [TestCase]
    public void ResonanceSurgeGrantsFifteenMeterClampedAtAFullBar() {
        var fighter = new FighterStateComponent { Influence = FP64.FromInt(40), MaxHP = 100, CurrentHP = 100 };
        var runtime = new FighterRuntimeComponent();
        var defense = new FighterDefenseComponent();
        FighterOrbSystem.ApplyEffect(ref fighter, ref runtime, ref defense, FighterOrbSystem.ResonanceSurgeEffectType);
        AssertThat(fighter.Influence.RawValue).IsEqual(FP64.FromInt(55).RawValue);
        AssertThat(fighter.CurrentHP).IsEqual(100);
        AssertThat(defense.AegisActive).IsEqual(0);

        fighter.Influence = FP64.FromInt(95);
        FighterOrbSystem.ApplyEffect(ref fighter, ref runtime, ref defense, FighterOrbSystem.ResonanceSurgeEffectType);
        AssertThat(fighter.Influence.RawValue).IsEqual(FP64.FromInt(100).RawValue);
    }

    // ---- helpers ------------------------------------------------------------

    private static FighterSimulation NewSimulation(int seed, bool meterPickups) => new(
        seed: seed,
        rules: new FighterMatchRules(
            (int)MatchMode.Stock, itemsEnabled: true, itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: false, hazardCadenceFrames: 0, stageHazardTypeID: 1,
            preMatchCountdownFrames: 0, meterPickupsEnabled: meterPickups),
        stageGeometry: FighterStageGeometry.Florence);

    private static List<int> SpawnTicks(FighterSimulation simulation) {
        var seen = new HashSet<int>();
        var ticks = new List<int>();
        var orbs = new List<FighterOrbComponent>();
        for (int tick = 0; tick < 4000; tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
            orbs.Clear();
            simulation.CopyOrbsTo(orbs);
            foreach (FighterOrbComponent orb in orbs) {
                if (seen.Add(orb.EntityID)) ticks.Add(tick);
            }
        }
        return ticks;
    }

    private static bool IsAuthoredAnchor(FighterStageGeometry geometry, FPVector2 position) {
        foreach (FPVector2 anchor in geometry.OrbAnchors) {
            if (anchor.x.RawValue == position.x.RawValue && anchor.y.RawValue == position.y.RawValue) return true;
        }
        return false;
    }

    private static PlayerInputFrame Neutral(int tick) => new() { Tick = (uint)tick };
}
