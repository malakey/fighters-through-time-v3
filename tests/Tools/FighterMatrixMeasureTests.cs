using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Tools;

/// <summary>
/// The Fighter CPU matrix measurement tool (ruling 8b, kept as a tool; GR-CPU). Plays CPU-vs-CPU matches
/// across all 81 hero pairs on one stage and writes one CSV row per match, which
/// <c>tools/fighter_matrix/analyse.py</c> turns into win matrices and per-hero tables. It measures; it
/// asserts no balance.
///
/// <b>A no-op unless the environment variable <c>FTT_MATRIX</c> names a config file</b>, so it costs one
/// passing case in the normal suite, keeps the build honest against API drift, and can never run into the
/// 300 s session timeout. A real run takes tens of minutes: see <c>tools/fighter_matrix/README.md</c> for the
/// command, the longer-timeout runsettings and the GdUnit lock rule.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterMatrixMeasureTests {
    /// <summary>The environment variable holding the path of the key=value config file (see config.example.txt).</summary>
    public const string ConfigVariable = "FTT_MATRIX";
    private static readonly string[] Heroes = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra", "tesla", "shakespeare", "mozart", "tubman"
    };
    private const int HardCap = 70000;

    [After]
    public void Drain() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }

    private sealed class Side {
        public int KoHp, KoPit, S1, S2, Ult, Defy, Grab, Blocks, Dmg;
        public bool Open; public int Slot, Phase;
        public string Csv() => $"{KoHp},{KoPit},{S1},{S2},{Ult},{Defy},{Grab},{Blocks},{Dmg}";
    }

    [TestCase(Timeout = 1700000)]
    public void Measure() {
        string configPath = System.Environment.GetEnvironmentVariable(ConfigVariable);
        if (string.IsNullOrWhiteSpace(configPath)) return; // the gate: nothing to measure unless asked
        AssertThat(File.Exists(configPath)).OverrideFailureMessage($"{ConfigVariable} names a missing config file: {configPath}").IsTrue();
        string configDir = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".";

        var cfg = new Dictionary<string, string>();
        foreach (string line in File.ReadAllLines(configPath)) {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            string[] kv = trimmed.Split('=');
            if (kv.Length == 2) cfg[kv[0].Trim()] = kv[1].Trim();
        }
        string stage = cfg["stage"];
        CpuDifficulty diff = Enum.Parse<CpuDifficulty>(cfg["diff"]);
        int seedFrom = int.Parse(cfg["seedfrom"]), seedCount = int.Parse(cfg["seeds"]);
        int pairFrom = int.Parse(cfg["pairfrom"]), pairCount = int.Parse(cfg["pairs"]);
        // Output paths are relative to the config file's folder; the progress log sits beside the CSV.
        string outFile = Path.Combine(configDir, cfg["out"]);
        string progressFile = Path.Combine(Path.GetDirectoryName(outFile) ?? configDir, "progress.log");
        FighterStageGeometry geometry = stage switch {
            "Florence" => FighterStageGeometry.Florence,
            "Paris" => FighterStageGeometry.Paris,
            "Nassau" => FighterStageGeometry.Nassau,
            "Vesuvius" => FighterStageGeometry.Vesuvius,
            "Globe" => FighterStageGeometry.Globe,
            "Hakata" or "Berlin" => FighterStageGeometry.Berlin,
            _ => throw new ArgumentException(stage)
        };
        bool header = !File.Exists(outFile);
        using var writer = new StreamWriter(outFile, append: true);
        if (header) {
            string h = "stage,difficulty,hero1,hero2,seed,winner,ticks,end,sd,stocks1,stocks2,lost1,lost2";
            foreach (string p in new[] { "p1", "p2" })
                h += $",{p}_ko_hp,{p}_ko_pit,{p}_s1,{p}_s2,{p}_ult,{p}_defy,{p}_grab,{p}_blocks,{p}_dmg";
            writer.WriteLine(h); writer.Flush();
        }
        var failures = new List<string>();
        long totalTicks = 0; var clock = Stopwatch.StartNew(); int matches = 0;
        for (int pair = pairFrom; pair < Math.Min(81, pairFrom + pairCount); pair++) {
            string a = Heroes[pair / 9], b = Heroes[pair % 9];
            for (int k = 0; k < seedCount; k++) {
                int seed = 10000 + pair * 100 + seedFrom + k;
                try {
                    string row = Play(geometry, diff, a, b, seed, out int ticks);
                    writer.WriteLine($"{stage},{diff},{a},{b},{seed},{row}"); writer.Flush();
                    totalTicks += ticks; matches++;
                } catch (Exception e) {
                    failures.Add($"{a} v {b} {seed}: {e.GetType().Name}: {e.Message}");
                }
            }
        }
        File.AppendAllText(progressFile,
            $"{DateTime.Now:HH:mm:ss} {stage} {diff} pairs {pairFrom}+{pairCount} seeds {seedFrom}+{seedCount}: {matches} matches, {totalTicks} ticks, {clock.Elapsed.TotalSeconds:F1}s, {totalTicks / Math.Max(0.001, clock.Elapsed.TotalSeconds):F0} ticks/s, failures {failures.Count}\n");
        if (failures.Count > 0) AssertThat(string.Join(" | ", failures)).IsEqual("");
    }

    private static string Play(FighterStageGeometry geometry, CpuDifficulty diff, string heroOne, string heroTwo, int seed, out int tickCount) {
        FighterLoadout one = Authored(heroOne);
        FighterLoadout two = Authored(heroTwo);
        var rules = new FighterMatchRules((int)MatchMode.Stock, true, 2, true, 0, HazardTypeFor(geometry));
        var sim = new FighterSimulation(one, two, stocks: 3, matchSeconds: 480, seed: seed, rules: rules, stageGeometry: geometry);
        var observer = new FighterSimulationWorldObserver(sim);
        var cpus = new[] {
            new FighterCpuController(diff, unchecked(seed * 397), geometry, observer, tuningOverride: null, recoveryProfile: CpuRecoveryProfile.FromLoadout(one)),
            new FighterCpuController(diff, unchecked(seed * 397 + 1), geometry, observer, tuningOverride: null, recoveryProfile: CpuRecoveryProfile.FromLoadout(two))
        };
        var sides = new[] { new Side(), new Side() };
        var previous = new PlayerInputFrame[2];
        bool sd = false; int tick = 0;
        for (; tick < HardCap; tick++) {
            if (sim.GetMatchState().MatchState == 2) break;
            var fb = new FighterStateComponent[2]; var rb = new FighterRuntimeComponent[2];
            var vb = new FighterVerbComponent[2]; var ab = new FighterActionStateComponent[2];
            var ub = new FighterUltimateActivationComponent[2];
            for (int p = 0; p < 2; p++) {
                sim.TryGetFighter(p, out fb[p]); sim.TryGetFighterRuntime(p, out rb[p]);
                sim.TryGetFighterVerb(p, out vb[p]); sim.TryGetFighterActionState(p, out ab[p]);
                sim.TryGetFighterUltimateActivation(p, out ub[p]);
            }
            var inputs = new PlayerInputFrame[2];
            for (int p = 0; p < 2; p++) {
                inputs[p] = cpus[p].Sample(unchecked((uint)sim.CurrentTick), in fb[p], in rb[p], in fb[1 - p], in rb[1 - p], in previous[p]);
                previous[p] = inputs[p];
            }
            sim.Advance(inputs[0], inputs[1]);
            if (sim.GetMatchState().SuddenDeathActive == 1) sd = true;
            for (int p = 0; p < 2; p++) {
                sim.TryGetFighter(p, out var fa); sim.TryGetFighterRuntime(p, out var ra);
                sim.TryGetFighterVerb(p, out var va); sim.TryGetFighterActionState(p, out var aa);
                sim.TryGetFighterUltimateActivation(p, out var ua);
                Side s = sides[p], o = sides[1 - p];
                bool lost = fa.Stocks < fb[p].Stocks;
                if (lost) {
                    bool pit = fb[p].Position.y < geometry.BottomBlastZone + FP64.FromDouble(0.6) && fb[p].Velocity.y < FP64.Zero;
                    if (pit) s.KoPit++; else { s.KoHp++; o.Dmg += fb[p].CurrentHP; }
                } else if (fa.CurrentHP < fb[p].CurrentHP) {
                    o.Dmg += fb[p].CurrentHP - fa.CurrentHP;
                }
                if (aa.DefyFrames > 0 && ab[p].DefyFrames == 0) s.Defy++;
                if (ua.Phase != 0 && ub[p].Phase == 0) s.Ult++;
                if (va.GrabPhase != 0 && vb[p].GrabPhase == 0) s.Grab++;
                bool chargeSpent = fa.BlockCharges < fb[p].BlockCharges;
                if (chargeSpent || va.ShieldStunFrames > vb[p].ShieldStunFrames) s.Blocks++;
                bool busy = FighterSpecialCastRules.IsBusy(in aa);
                bool fresh = busy && (!s.Open || aa.SpecialSlot != s.Slot
                    || (FighterSpecialCastRules.IsCasting(in aa) && aa.SpecialPhase < s.Phase));
                if (fresh) { if (aa.SpecialSlot == 1) s.S1++; else s.S2++; }
                if (busy) { s.Open = true; s.Slot = aa.SpecialSlot; if (FighterSpecialCastRules.IsCasting(in aa)) s.Phase = aa.SpecialPhase; }
                else { s.Open = false; s.Phase = 0; }
            }
        }
        tickCount = tick;
        FighterMatchComponent m = sim.GetMatchState();
        sim.TryGetFighter(0, out var f0); sim.TryGetFighter(1, out var f1);
        string end = m.MatchState != 2 ? "capped" : sd ? "sudden_death" : m.RemainingFrames <= 0 ? "time" : "stocks";
        string winner = m.MatchState != 2 ? "none" : m.WinnerPlayerID < 0 ? "draw" : m.WinnerPlayerID.ToString();
        return $"{winner},{tick},{end},{(sd ? 1 : 0)},{f0.Stocks},{f1.Stocks},{m.PlayerOneStocksLost},{m.PlayerTwoStocksLost},{sides[0].Csv()},{sides[1].Csv()}";
    }

    private static int HazardTypeFor(FighterStageGeometry geometry) {
        int acc = 0; string id = geometry.StageID ?? "";
        for (int i = 0; i < id.Length; i++) acc = acc * 31 + id[i];
        return Math.Abs(acc % 10) + 1;
    }

    private static FighterLoadout Authored(string hero) => FighterLoadoutFactory.FromCharacterData(
        AuthoredResources.Load<CharacterData>($"res://resources/Characters/{hero}_data.tres"));
}
