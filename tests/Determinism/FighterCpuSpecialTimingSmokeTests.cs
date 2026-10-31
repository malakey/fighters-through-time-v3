using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Combat and movement update 2026-10-16, decision 9.3 (plan §2.9.3 and §4.3
/// item 4, CM-CANCEL): the package's CPU smoke. A seeded Hard-vs-Hard CPU match
/// on each of the ten stages, production loadouts from the authored
/// <c>*_data.tres</c>, items and hazards on, for 1,800 ticks. It asserts that
/// each fighter casts at least one Special through its Active window, that no
/// clocked cast runs more unfrozen ticks than its authored startup + active +
/// recovery, that nothing throws, and that a second run from the same seed
/// replays every tick's hash. It also RECORDS, without asserting, per stage:
/// perfect blocks (a block contact that spent no charge and set no shieldstun —
/// zero until CM-BLOCK's perfect block is merged), the Specials the opponent
/// answered during their startup with a block or an evade, Lincoln's Splitting
/// Strikes and their whiffs, and the Special and Ultimate presses the CPU made
/// into its own cast. The counts are printed to the Godot log
/// (<c>[CM-CANCEL smoke]</c>) for the handoff and the pad session (plan §5 rows
/// 9, 11 and 25). This is a functional smoke, not a measurement batch (9.4).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterCpuSpecialTimingSmokeTests {
    private const int Ticks = 1800;

    private static readonly string[] Heroes = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra", "tesla", "shakespeare", "mozart", "tubman"
    };

    [After]
    public void DrainPendingFinalizers() {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [TestCase]
    public void HardCpusCastSpecialsThroughTheirPhasesOnEveryStageAndTheMatchReplays() {
        var failures = new List<string>();
        FighterStageGeometry[] stages = {
            FighterStageGeometry.Florence, FighterStageGeometry.Orleans, FighterStageGeometry.Chicago,
            FighterStageGeometry.Paris, FighterStageGeometry.Vesuvius, FighterStageGeometry.Nassau,
            FighterStageGeometry.Alexandria, FighterStageGeometry.Berlin, FighterStageGeometry.Globe,
            FighterStageGeometry.Gettysburg
        };
        for (int index = 0; index < stages.Length; index++) {
            FighterStageGeometry geometry = stages[index];
            string one = Heroes[index % Heroes.Length];
            string two = Heroes[(index + 4) % Heroes.Length];
            int seed = 9300 + index;
            string label = $"{geometry.StageID} ({one} vs {two})";
            try {
                SmokeRun first = Run(geometry, one, two, seed);
                SmokeRun replay = Run(geometry, one, two, seed);
                if (first.Hashes.Count != replay.Hashes.Count) {
                    failures.Add($"{label}: the replay ran {replay.Hashes.Count} ticks, not {first.Hashes.Count}");
                } else {
                    for (int tick = 0; tick < first.Hashes.Count; tick++) {
                        if (first.Hashes[tick] == replay.Hashes[tick]) continue;
                        failures.Add($"{label}: the replay diverged on tick {tick}");
                        break;
                    }
                }
                for (int player = 0; player < 2; player++) {
                    PlayerRecord record = first.Players[player];
                    if (record.ActiveCasts == 0) {
                        failures.Add($"{label}: {record.Hero} never cast a Special through Active ({record.Casts} casts)");
                    }
                    failures.AddRange(record.Overlong);
                }
                GD.Print($"[CM-CANCEL smoke] {label} ticks={first.Hashes.Count} "
                    + $"{first.Players[0].Summary()} | {first.Players[1].Summary()}");
            } catch (Exception exception) {
                failures.Add($"{label}: {exception.GetType().Name}: {exception.Message}");
            }
        }
        if (failures.Count > 0) AssertThat(string.Join(" | ", failures)).IsEqual("");
    }

    // === the run ===================================================================

    private sealed class SmokeRun {
        public readonly List<long> Hashes = new(Ticks);
        public readonly PlayerRecord[] Players = new PlayerRecord[2];
    }

    private sealed class PlayerRecord {
        public string Hero;
        public FighterAbilityLoadout Modes;
        public int Casts;
        public int ActiveCasts;
        public int StartupBlocked;
        public int StartupEvaded;
        public int Blocks;
        public int PerfectBlocks;
        public int PressesIntoOwnCast;
        public int Defies;
        public int SpecialPresses;
        public int SplittingCasts;
        public int SplittingWhiffs;
        public int SplittingInterrupted;
        public readonly List<string> Overlong = new();

        // The open cast.
        public bool Open;
        public int Slot;
        public bool Delegated;
        public int Phase;
        public int UnfrozenTicks;
        public bool ReachedActive;
        public bool MeleeResolved;
        public bool AnsweredBlock;
        public bool AnsweredEvade;

        public string Summary() =>
            $"{Hero}: casts={Casts} active={ActiveCasts} answeredInStartup(block={StartupBlocked} evade={StartupEvaded}) "
            + $"blocks={Blocks} perfectBlocks={PerfectBlocks} pressesIntoOwnCast={PressesIntoOwnCast} "
            + $"specialPresses={SpecialPresses} defies={Defies}"
            + (Hero == "lincoln"
                ? $" splitting={SplittingCasts} whiffs={SplittingWhiffs} interrupted={SplittingInterrupted}"
                : "");
    }

    private readonly struct Snapshot {
        public readonly FighterStateComponent Fighter;
        public readonly FighterRuntimeComponent Runtime;
        public readonly FighterVerbComponent Verb;
        public readonly FighterActionStateComponent Cast;
        public readonly FighterCounterComponent Counter;

        public Snapshot(FighterSimulation simulation, int playerID) {
            simulation.TryGetFighter(playerID, out Fighter);
            simulation.TryGetFighterRuntime(playerID, out Runtime);
            simulation.TryGetFighterVerb(playerID, out Verb);
            simulation.TryGetFighterActionState(playerID, out Cast);
            simulation.TryGetFighterCounter(playerID, out Counter);
        }

        public bool BlockStance => FighterBasicAttackRules.IsBlockStance(in Fighter, in Runtime, in Verb);

        public bool Evading =>
            Runtime.UniversalMovementState == (int)UniversalMovementPhase.RollStartup
            || Runtime.UniversalMovementState == (int)UniversalMovementPhase.AirDodgeStartup;
    }

    private static SmokeRun Run(FighterStageGeometry geometry, string heroOne, string heroTwo, int seed) {
        FighterLoadout one = Authored(heroOne);
        FighterLoadout two = Authored(heroTwo);
        var rules = new FighterMatchRules((int)MatchMode.Stock, true, 2, true, 0, HazardTypeFor(geometry));
        var simulation = new FighterSimulation(
            one, two, stocks: 3, matchSeconds: 480, seed: seed, rules: rules, stageGeometry: geometry);
        var observer = new FighterSimulationWorldObserver(simulation);
        var cpus = new[] {
            new FighterCpuController(CpuDifficulty.Hard, unchecked(seed * 397), geometry, observer,
                tuningOverride: null, recoveryProfile: CpuRecoveryProfile.FromLoadout(one)),
            new FighterCpuController(CpuDifficulty.Hard, unchecked(seed * 397 + 1), geometry, observer,
                tuningOverride: null, recoveryProfile: CpuRecoveryProfile.FromLoadout(two))
        };
        var run = new SmokeRun();
        run.Players[0] = new PlayerRecord { Hero = heroOne, Modes = one.AbilityModes };
        run.Players[1] = new PlayerRecord { Hero = heroTwo, Modes = two.AbilityModes };
        var previous = new PlayerInputFrame[2];
        for (int tick = 0; tick < Ticks; tick++) {
            if (tick > 0 && simulation.GetMatchState().MatchState != 1) break;
            var before = new[] { new Snapshot(simulation, 0), new Snapshot(simulation, 1) };
            var inputs = new PlayerInputFrame[2];
            for (int player = 0; player < 2; player++) {
                Snapshot self = before[player];
                Snapshot other = before[1 - player];
                inputs[player] = cpus[player].Sample(
                    unchecked((uint)simulation.CurrentTick),
                    in self.Fighter, in self.Runtime, in other.Fighter, in other.Runtime, in previous[player]);
                previous[player] = inputs[player];
                // A press is read again on the tick the cast's last recovery
                // frame runs out (that tick's movement pass ends the cast first).
                if (inputs[player].IsPressed(GameplayButtons.Special1) || inputs[player].IsPressed(GameplayButtons.Special2)) {
                    run.Players[player].SpecialPresses++;
                }
                bool castHoldsTheTick = FighterSpecialCastRules.IsBusy(in self.Cast)
                    && !(FighterSpecialCastRules.IsRecovery(in self.Cast) && self.Cast.SpecialPhaseFrames <= 1
                        && self.Verb.HitstopFrames == 0);
                if (castHoldsTheTick
                    && (inputs[player].IsPressed(GameplayButtons.Special1)
                        || inputs[player].IsPressed(GameplayButtons.Special2)
                        || inputs[player].IsPressed(GameplayButtons.Ultimate))) {
                    run.Players[player].PressesIntoOwnCast++;
                }
            }
            run.Hashes.Add(simulation.Advance(inputs[0], inputs[1]));
            var after = new[] { new Snapshot(simulation, 0), new Snapshot(simulation, 1) };
            for (int player = 0; player < 2; player++) {
                Track(run.Players[player], before[player], after[player], before[1 - player], after[1 - player], tick);
            }
        }
        return run;
    }

    private static void Track(
        PlayerRecord record, in Snapshot before, in Snapshot after, in Snapshot otherBefore, in Snapshot otherAfter, int tick) {
        // Meter and Defy update (plan 2.12): the smoke also records the Defies the Hard CPU makes.
        if (after.Cast.DefyFrames > 0 && before.Cast.DefyFrames == 0) record.Defies++;
        // Block contacts this tick, on the defending side (this record's fighter).
        bool chargeSpent = after.Fighter.BlockCharges < before.Fighter.BlockCharges;
        bool shieldstunned = after.Verb.ShieldStunFrames > before.Verb.ShieldStunFrames;
        bool frozenInStance = before.BlockStance
            && after.Verb.HitstopFrames > before.Verb.HitstopFrames
            && after.Fighter.CurrentHP == before.Fighter.CurrentHP;
        if (chargeSpent || shieldstunned || frozenInStance) {
            record.Blocks++;
            if (!chargeSpent && !shieldstunned) record.PerfectBlocks++;
        }

        FighterActionStateComponent cast = after.Cast;
        bool busy = FighterSpecialCastRules.IsBusy(in cast);
        bool newExecution = busy && (!record.Open
            || cast.SpecialSlot != record.Slot
            || (FighterSpecialCastRules.IsCasting(in cast) && cast.SpecialPhase < record.Phase));
        if (record.Open && (!busy || newExecution)) Close(record, in after, tick);
        if (busy && newExecution) {
            record.Open = true;
            record.Casts++;
            record.Slot = cast.SpecialSlot;
            record.Delegated = FighterSpecialCastRules.IsDelegated(in cast);
            record.UnfrozenTicks = 0;
            record.ReachedActive = false;
            record.MeleeResolved = false;
            record.AnsweredBlock = false;
            record.AnsweredEvade = false;
        }
        if (!busy) return;

        record.Phase = FighterSpecialCastRules.IsCasting(in cast) ? cast.SpecialPhase : record.Phase;
        if (before.Verb.HitstopFrames == 0) record.UnfrozenTicks++;
        if (FighterSpecialCastRules.IsActive(in cast)
            || (record.Delegated && after.Counter.Phase == FighterForesightRules.PhaseWindow)) {
            record.ReachedActive = true;
        }
        if (FighterSpecialCastRules.HasFlag(in cast, FighterSpecialCastRules.FlagMeleeResolved)) record.MeleeResolved = true;
        bool inStartup = FighterSpecialCastRules.IsStartup(in cast)
            || (record.Delegated && after.Counter.Phase == FighterForesightRules.PhaseStartup);
        if (inStartup) {
            if (!otherBefore.BlockStance && otherAfter.BlockStance) record.AnsweredBlock = true;
            if (!otherBefore.Evading && otherAfter.Evading) record.AnsweredEvade = true;
        }
    }

    private static void Close(PlayerRecord record, in Snapshot after, int tick) {
        record.Open = false;
        if (record.ReachedActive) record.ActiveCasts++;
        if (record.AnsweredBlock) record.StartupBlocked++;
        if (record.AnsweredEvade) record.StartupEvaded++;
        if (!record.Delegated) {
            FighterAbilityHitData contract = record.Slot == 1 ? record.Modes.SpecialOneHit : record.Modes.SpecialTwoHit;
            FighterSpecialCastRules.Frames(in contract, out int startup, out int active, out int recovery);
            if (record.UnfrozenTicks > startup + active + recovery) {
                record.Overlong.Add(
                    $"{record.Hero} S{record.Slot} ran {record.UnfrozenTicks} unfrozen ticks, past {startup}+{active}+{recovery} (closed on tick {tick})");
            }
        }
        if (record.Hero == "lincoln" && record.Slot == 2) {
            record.SplittingCasts++;
            if (!record.MeleeResolved) {
                bool interrupted = after.Fighter.HitstunFrames > 0 || after.Fighter.DazeFrames > 0;
                if (interrupted) record.SplittingInterrupted++;
                else record.SplittingWhiffs++;
            }
        }
        record.Phase = 0;
    }

    /// <summary>A distinct hazard identity per stage without the catalog (RollbackReadinessTests' rule).</summary>
    private static int HazardTypeFor(FighterStageGeometry geometry) {
        int accumulator = 0;
        string stageID = geometry.StageID ?? "";
        for (int index = 0; index < stageID.Length; index++) accumulator = accumulator * 31 + stageID[index];
        return Math.Abs(accumulator % 10) + 1;
    }

    private static FighterLoadout Authored(string hero) => FighterLoadoutFactory.FromCharacterData(
        AuthoredResources.Load<CharacterData>($"res://resources/Characters/{hero}_data.tres"));
}
