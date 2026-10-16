using System.Collections.Generic;
using FTT.FighterSim;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Combat and movement update 2026-10-16, decision 1.2 (plan §2.1.6): the pure
/// rules of the Fighter Special cast on component 323 —
/// <see cref="FighterSpecialCastRules"/>. The phase walk, a zero startup, the
/// 0 / 1 / 0 fallback for a contract with no authored frames, every interrupt,
/// the recovery frame index and Tubman's Delegated marker. Pure C#: integer
/// frame arithmetic on plain structs, no engine API.
/// </summary>
[TestSuite]
public class FighterSpecialCastRulesTests {

    [TestCase]
    public void ThePhaseWalkRunsStartupThenActiveThenRecoveryAndEnds() {
        FighterAbilityHitData contract = Contract(startup: 3, active: 2, recovery: 4);
        var cast = new FighterActionStateComponent();
        FighterSpecialCastRules.Begin(ref cast, 2, in contract, startedAerial: true);
        AssertThat(cast.SpecialSlot).IsEqual(2);
        AssertThat(FighterSpecialCastRules.HasFlag(in cast, FighterSpecialCastRules.FlagStartedAerial)).IsTrue();
        AssertThat(FighterSpecialCastRules.HasFlag(in cast, FighterSpecialCastRules.FlagFallbackTiming)).IsFalse();

        // The press tick T is the first startup tick; each later tick is one Advance.
        var phases = new List<int> { cast.SpecialPhase };
        var entries = new List<int>();
        for (int tick = 1; tick <= 10; tick++) {
            if (FighterSpecialCastRules.Advance(ref cast, in contract)) entries.Add(tick);
            phases.Add(cast.SpecialPhase);
        }
        int s = FighterSpecialCastRules.PhaseStartup, a = FighterSpecialCastRules.PhaseActive;
        int r = FighterSpecialCastRules.PhaseRecovery, n = FighterSpecialCastRules.PhaseNone;
        // T..T+2 startup, T+3..T+4 active, T+5..T+8 recovery, free on T+9.
        AssertThat(string.Join(",", phases)).IsEqual(string.Join(",", new[] { s, s, s, a, a, r, r, r, r, n, n }));
        AssertThat(entries.Count).IsEqual(1);
        AssertThat(entries[0])
            .OverrideFailureMessage("Advance reports the active entry on T+S.")
            .IsEqual(3);
        AssertThat(cast.SpecialSlot).IsEqual(0);
        AssertThat(cast.SpecialFlags).IsEqual(0);
    }

    [TestCase]
    public void AZeroStartupCastIsActiveOnThePressTickAndAZeroRecoveryEndsWithTheActiveWindow() {
        FighterAbilityHitData contract = Contract(startup: 0, active: 2, recovery: 1);
        var cast = new FighterActionStateComponent();
        FighterSpecialCastRules.Begin(ref cast, 1, in contract, startedAerial: false);
        AssertThat(FighterSpecialCastRules.IsActive(in cast)).IsTrue();
        AssertThat(cast.SpecialPhaseFrames).IsEqual(2);
        // No startup → active transition, so Advance never reports an entry: the
        // fire pass fires on the press tick itself.
        AssertThat(FighterSpecialCastRules.Advance(ref cast, in contract)).IsFalse();
        AssertThat(FighterSpecialCastRules.IsActive(in cast)).IsTrue();
        AssertThat(FighterSpecialCastRules.Advance(ref cast, in contract)).IsFalse();
        AssertThat(FighterSpecialCastRules.IsRecovery(in cast)).IsTrue();
        FighterSpecialCastRules.Advance(ref cast, in contract);
        AssertThat(FighterSpecialCastRules.IsBusy(in cast)).IsFalse();

        FighterAbilityHitData noRecovery = Contract(startup: 2, active: 1, recovery: 0);
        var brief = new FighterActionStateComponent();
        FighterSpecialCastRules.Begin(ref brief, 1, in noRecovery, startedAerial: false);
        FighterSpecialCastRules.Advance(ref brief, in noRecovery);
        AssertThat(FighterSpecialCastRules.Advance(ref brief, in noRecovery)).IsTrue();
        AssertThat(FighterSpecialCastRules.IsActive(in brief)).IsTrue();
        FighterSpecialCastRules.Advance(ref brief, in noRecovery);
        AssertThat(FighterSpecialCastRules.IsBusy(in brief))
            .OverrideFailureMessage("A zero-frame recovery ends the cast as its active window ends.")
            .IsFalse();
    }

    [TestCase]
    public void AContractWithNoAuthoredFramesRunsAsZeroOneZero() {
        // A hand-built FighterAbilityLoadout.Default slot projects no ability:
        // ActiveFrames 0 is the fallback marker, whatever else it carries.
        FighterAbilityHitData unprojected = new() { StartupFrames = 12, RecoveryFrames = 20 };
        AssertThat(FighterSpecialCastRules.IsFallback(in unprojected)).IsTrue();
        FighterSpecialCastRules.Frames(in unprojected, out int startup, out int active, out int recovery);
        AssertThat(startup).IsEqual(0);
        AssertThat(active).IsEqual(1);
        AssertThat(recovery).IsEqual(0);

        var cast = new FighterActionStateComponent();
        FighterSpecialCastRules.Begin(ref cast, 1, in unprojected, startedAerial: false);
        AssertThat(FighterSpecialCastRules.IsActive(in cast))
            .OverrideFailureMessage("The fallback cast is active on the press tick (today's press-tick timing).")
            .IsTrue();
        AssertThat(FighterSpecialCastRules.HasFlag(in cast, FighterSpecialCastRules.FlagFallbackTiming)).IsTrue();
        AssertThat(FighterSpecialCastRules.Advance(ref cast, in unprojected)).IsFalse();
        AssertThat(FighterSpecialCastRules.IsBusy(in cast))
            .OverrideFailureMessage("The fallback cast is free on the tick after the press.")
            .IsFalse();

        // An authored contract with a zero startup is not the fallback.
        FighterAbilityHitData authored = Contract(startup: 0, active: 6, recovery: 12);
        AssertThat(FighterSpecialCastRules.IsFallback(in authored)).IsFalse();
    }

    [TestCase]
    public void EveryInterruptClearsTheCastAndLeavesTheOtherFieldsAlone() {
        FighterAbilityHitData contract = Contract(startup: 12, active: 6, recovery: 20);
        var interrupts = new (string Name, FighterStateComponent Fighter, FighterVerbComponent Verb, bool Captured, bool KnockedDown)[] {
            ("hitstun", Fighter(f => f.HitstunFrames = 1), default, false, false),
            ("daze", Fighter(f => f.DazeFrames = 1), default, false, false),
            ("grabbing", Fighter(null), new FighterVerbComponent { GrabPhase = 1 }, false, false),
            ("held", Fighter(null), new FighterVerbComponent { BeingHeld = 1 }, false, false),
            ("captured", Fighter(null), default, true, false),
            // Fix round A (CM-FIXA-SIM): the M05 knockdown and its get-up.
            ("knocked down", Fighter(null), default, false, true),
            ("out of stocks", Fighter(f => f.Stocks = 0), default, false, false),
            ("the respawn platform", Fighter(f => f.RespawnFramesRemaining = 1), default, false, false)
        };
        foreach ((string name, FighterStateComponent fighter, FighterVerbComponent verb, bool captured, bool knockedDown) in interrupts) {
            var cast = new FighterActionStateComponent {
                SpecialCancelBuffer = 5, BlockTiming = 7, DownTapFrames = 3, ActionFlags = 2
            };
            FighterSpecialCastRules.Begin(ref cast, 1, in contract, startedAerial: false);
            AssertThat(FighterSpecialCastRules.ClearIfInterrupted(ref cast, in fighter, in verb, captured, knockedDown))
                .OverrideFailureMessage($"{name} must end the cast.")
                .IsTrue();
            AssertThat(FighterSpecialCastRules.IsBusy(in cast)).IsFalse();
            AssertThat(cast.SpecialSlot).IsEqual(0);
            AssertThat(cast.SpecialFlags).IsEqual(0);
            AssertThat(cast.SpecialCancelBuffer).IsEqual(5);
            AssertThat(cast.BlockTiming).IsEqual(7);
            AssertThat(cast.DownTapFrames).IsEqual(3);
            AssertThat(cast.ActionFlags).IsEqual(2);
        }

        // No interrupt: the cast runs on. An idle cast reports nothing to clear.
        FighterStateComponent standing = Fighter(null);
        var running = new FighterActionStateComponent();
        FighterSpecialCastRules.Begin(ref running, 2, in contract, startedAerial: false);
        AssertThat(FighterSpecialCastRules.ClearIfInterrupted(ref running, in standing, default, false, false)).IsFalse();
        AssertThat(FighterSpecialCastRules.IsStartup(in running)).IsTrue();
        var idle = new FighterActionStateComponent();
        AssertThat(FighterSpecialCastRules.ClearIfInterrupted(ref idle, Fighter(f => f.HitstunFrames = 5), default, false, false)).IsFalse();
    }

    [TestCase]
    public void PhaseElapsedIndexesTheRecoveryFrameFromZero() {
        FighterAbilityHitData contract = Contract(startup: 2, active: 3, recovery: 4);
        var cast = new FighterActionStateComponent();
        FighterSpecialCastRules.Begin(ref cast, 1, in contract, startedAerial: false);
        var recoveryFrames = new List<int>();
        var elapsedAtEntry = new List<int>();
        int previousPhase = cast.SpecialPhase;
        AssertThat(FighterSpecialCastRules.RecoveryFrame(in cast)).IsEqual(-1);
        for (int tick = 1; tick <= 9; tick++) {
            FighterSpecialCastRules.Advance(ref cast, in contract);
            if (cast.SpecialPhase != previousPhase && FighterSpecialCastRules.IsBusy(in cast)) {
                elapsedAtEntry.Add(cast.SpecialPhaseElapsed);
            }
            previousPhase = cast.SpecialPhase;
            int frame = FighterSpecialCastRules.RecoveryFrame(in cast);
            if (frame >= 0) recoveryFrames.Add(frame);
            else if (FighterSpecialCastRules.IsBusy(in cast)) {
                AssertThat(FighterSpecialCastRules.IsRecovery(in cast)).IsFalse();
            }
        }
        AssertThat(string.Join(",", recoveryFrames)).IsEqual("0,1,2,3");
        AssertThat(string.Join(",", elapsedAtEntry))
            .OverrideFailureMessage("Each phase starts its elapsed count at 0.")
            .IsEqual("0,0");
        AssertThat(FighterSpecialCastRules.RecoveryFrame(in cast)).IsEqual(-1);
    }

    [TestCase]
    public void ADelegatedCastIsBusyButNotClocked() {
        FighterAbilityHitData contract = Contract(startup: 4, active: 20, recovery: 24);
        var cast = new FighterActionStateComponent();
        FighterSpecialCastRules.BeginDelegated(ref cast, 2, startedAerial: false);
        AssertThat(FighterSpecialCastRules.IsDelegated(in cast)).IsTrue();
        AssertThat(FighterSpecialCastRules.IsBusy(in cast))
            .OverrideFailureMessage("Foresight's Delegated marker holds the cast lock.")
            .IsTrue();
        AssertThat(FighterSpecialCastRules.IsCasting(in cast))
            .OverrideFailureMessage("Delegated is not a clocked cast — component 322 owns its timeline.")
            .IsFalse();
        for (int tick = 0; tick < 60; tick++) {
            AssertThat(FighterSpecialCastRules.Advance(ref cast, in contract)).IsFalse();
        }
        AssertThat(FighterSpecialCastRules.IsDelegated(in cast)).IsTrue();
        AssertThat(cast.SpecialPhaseElapsed).IsEqual(0);
        AssertThat(FighterSpecialCastRules.RecoveryFrame(in cast)).IsEqual(-1);
        AssertThat(FighterSpecialCastRules.ClearIfInterrupted(ref cast, Fighter(f => f.HitstunFrames = 1), default, false, false)).IsTrue();
        AssertThat(FighterSpecialCastRules.IsBusy(in cast)).IsFalse();
    }

    private static FighterAbilityHitData Contract(int startup, int active, int recovery) => new() {
        StartupFrames = startup,
        ActiveFrames = active,
        RecoveryFrames = recovery,
        TotalFrames = startup + active + recovery
    };

    /// <summary>A grounded, living fighter with stocks, edited by <paramref name="edit"/>.</summary>
    private static FighterStateComponent Fighter(System.Action<FighterStateBox> edit) {
        var box = new FighterStateBox { Value = new FighterStateComponent { Stocks = 3, IsGrounded = 1 } };
        edit?.Invoke(box);
        return box.Value;
    }

    /// <summary>A mutable holder so a lambda can edit the struct.</summary>
    private sealed class FighterStateBox {
        public FighterStateComponent Value;
        public int HitstunFrames { set => Value.HitstunFrames = value; }
        public int DazeFrames { set => Value.DazeFrames = value; }
        public int Stocks { set => Value.Stocks = value; }
        public int RespawnFramesRemaining { set => Value.RespawnFramesRemaining = value; }
    }
}
