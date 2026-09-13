using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// V7.6 status architecture, deterministic half (Package 11 A1). The simulation
/// routes through the same <see cref="StatusRouting"/> table the three Story
/// controllers use, and within a slot applies STRONGER-WINS via an exact
/// <c>int64</c> product of the FP64 raw intensity and the integer frame count —
/// no FP64 division, bit-reproducible across a rollback. Suppression is refused
/// outright (Story-only), and the F07 Conductive mark lives on Klotho component
/// 318 as ordinary snapshot and hash state with a per-execution chain guard so a
/// restored multi-hit Pulse cannot duplicate its chains.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStatusSlotTests {

    [TestCase]
    public void FixedPointStrongerWinsGovernsBothSlotsInTheSimulation() {
        var state = new FighterStateComponent { HitstunFrames = 0 };
        var runtime = new FighterRuntimeComponent();

        // Control slot: a 120-frame Root at standard potency.
        FighterDamageRules.ApplyStatus(ref state, ref runtime, (int)StatusType.Root, 120, FP64.One);
        AssertThat(runtime.StatusType).IsEqual((int)StatusType.Root);
        AssertThat(runtime.StatusFrames).IsEqual(120);

        // A WEAKER same-type application is a complete no-op.
        FighterDamageRules.ApplyStatus(ref state, ref runtime, (int)StatusType.Root, 30, FP64.One);
        AssertThat(runtime.StatusFrames)
            .OverrideFailureMessage("A weaker same-type status must not replace or refresh.")
            .IsEqual(120);

        // Intensity participates in the product: a third of the duration at four
        // times the potency is stronger and wins.
        FighterDamageRules.ApplyStatus(
            ref state, ref runtime, (int)StatusType.Root, 40, FP64.FromInt(4));
        AssertThat(runtime.StatusFrames).IsEqual(40);
        AssertThat(runtime.StatusIntensity).IsEqual(FP64.FromInt(4));

        // EQUAL strength replaces (the rule is >=).
        FighterDamageRules.ApplyStatus(
            ref state, ref runtime, (int)StatusType.Root, 80, FP64.FromInt(2));
        AssertThat(runtime.StatusFrames).IsEqual(80);

        // A DIFFERENT type in the same slot always replaces, weaker or not.
        FighterDamageRules.ApplyStatus(
            ref state, ref runtime, (int)StatusType.TimeDilation, 5, FP64.One);
        AssertThat(runtime.StatusType).IsEqual((int)StatusType.TimeDilation);
        AssertThat(runtime.StatusFrames).IsEqual(5);

        // The damage slot is a separate competition and was never touched.
        AssertThat(runtime.DamageStatusType).IsEqual((int)StatusType.None);
        FighterDamageRules.ApplyStatus(ref state, ref runtime, (int)StatusType.Venom, 100, FP64.One);
        FighterDamageRules.ApplyStatus(ref state, ref runtime, (int)StatusType.Venom, 20, FP64.One);
        AssertThat(runtime.DamageStatusFrames)
            .OverrideFailureMessage("The damage slot obeys the same stronger-wins rule.")
            .IsEqual(100);
        AssertThat(runtime.StatusType)
            .OverrideFailureMessage("Damage-slot traffic must never touch the control slot.")
            .IsEqual((int)StatusType.TimeDilation);
    }

    [TestCase]
    public void TheRawProductComparisonIsExactIntegerArithmetic() {
        // The comparison the simulation runs: raw FP64 intensity x integer frames
        // as an int64. No division, no float, so a rollback replay reproduces the
        // same decision bit for bit.
        long one = FP64.One.RawValue;
        long half = FP64.FromDouble(0.5).RawValue;

        AssertThat(StatusRouting.ShouldReplaceRaw(
                (int)StatusType.Root, one, 60, (int)StatusType.Root, half, 120))
            .OverrideFailureMessage("0.5 x 120 equals 1.0 x 60 — equal strength replaces.")
            .IsTrue();
        AssertThat(StatusRouting.ShouldReplaceRaw(
                (int)StatusType.Root, one, 60, (int)StatusType.Root, half, 119))
            .OverrideFailureMessage("One frame short of equal must not replace.")
            .IsFalse();
        AssertThat(StatusRouting.ShouldReplaceRaw(
                (int)StatusType.Root, one, 60, (int)StatusType.Venom, half, 1))
            .OverrideFailureMessage("A different type always replaces.")
            .IsTrue();
        AssertThat(StatusRouting.ShouldReplaceRaw(
                (int)StatusType.None, 0, 0, (int)StatusType.Root, one, 10))
            .OverrideFailureMessage("An empty slot always accepts.")
            .IsTrue();
        AssertThat(StatusRouting.ShouldReplaceRaw(
                (int)StatusType.Root, one, 60, (int)StatusType.Root, one, 0))
            .OverrideFailureMessage("A zero-length application is refused outright.")
            .IsFalse();
        // The float and fixed-point forms must agree on the same question.
        AssertThat(StatusRouting.ShouldReplace(
                StatusType.Root, 1f, 1f, StatusType.Root, 0.5f, 2f))
            .IsEqual(StatusRouting.ShouldReplaceRaw(
                (int)StatusType.Root, one, 60, (int)StatusType.Root, half, 120));
    }

    [TestCase]
    public void ComponentThreeEighteenRoundTripsAndItsChainGuardSurvivesARollback() {
        var mark = new FighterConductiveComponent { SourcePlayerID = -1 };

        FighterConductiveRules.ApplyMark(ref mark, sourcePlayerID: 0, frames: 90);
        AssertThat(mark.FramesRemaining).IsEqual(90);
        AssertThat(mark.SourcePlayerID).IsEqual(0);
        AssertThat(FighterConductiveRules.HasMarkFrom(in mark, 0)).IsTrue();
        AssertThat(FighterConductiveRules.HasMarkFrom(in mark, 1)).IsFalse();

        // A shorter same-source application never shortens; a new source replaces.
        FighterConductiveRules.ApplyMark(ref mark, sourcePlayerID: 0, frames: 30);
        AssertThat(mark.FramesRemaining).IsEqual(90);
        FighterConductiveRules.ApplyMark(ref mark, sourcePlayerID: 1, frames: 30);
        AssertThat(mark.SourcePlayerID).IsEqual(1);
        AssertThat(mark.FramesRemaining).IsEqual(30);

        // A frozen (hitstop) fighter's mark holds exactly as their hitstun does.
        FighterConductiveRules.Tick(ref mark, frozen: true);
        AssertThat(mark.FramesRemaining)
            .OverrideFailureMessage("Hitstop must not silently shorten a mark.")
            .IsEqual(30);
        FighterConductiveRules.Tick(ref mark, frozen: false);
        AssertThat(mark.FramesRemaining).IsEqual(29);

        // The per-execution chain guard: a multi-hit Pulse chains ONCE, and a
        // rollback that replays the same execution ID reaches the same decision
        // rather than duplicating the chain.
        FighterConductiveComponent snapshot = mark;
        AssertThat(FighterConductiveRules.TryConsumeChain(ref mark, 1, executionID: 77))
            .OverrideFailureMessage("The first hit of an execution cashes the mark.")
            .IsTrue();
        AssertThat(FighterConductiveRules.TryConsumeChain(ref mark, 1, executionID: 77))
            .OverrideFailureMessage("A later hit of the SAME execution must not chain again.")
            .IsFalse();
        AssertThat(mark.ChainConsumedExecutionID).IsEqual(77);

        // Restore the pre-consumption snapshot (what a rollback does) and replay:
        // the decision is identical, and the guard lands in the same state.
        mark = snapshot;
        AssertThat(FighterConductiveRules.TryConsumeChain(ref mark, 1, executionID: 77)).IsTrue();
        AssertThat(FighterConductiveRules.TryConsumeChain(ref mark, 1, executionID: 77)).IsFalse();
        AssertThat(mark.ChainConsumedExecutionID).IsEqual(77);

        // A NEW execution chains again; a fresh mark resets the guard.
        AssertThat(FighterConductiveRules.TryConsumeChain(ref mark, 1, executionID: 78)).IsTrue();
        FighterConductiveRules.ApplyMark(ref mark, sourcePlayerID: 0, frames: 90);
        AssertThat(mark.ChainConsumedExecutionID)
            .OverrideFailureMessage("A fresh mark is a fresh chain opportunity.")
            .IsEqual(0);

        // Expiry and explicit clear both empty the component completely.
        FighterConductiveRules.Clear(ref mark);
        AssertThat(mark.FramesRemaining).IsEqual(0);
        AssertThat(mark.SourcePlayerID).IsEqual(-1);
        AssertThat(mark.ChainConsumedExecutionID).IsEqual(0);
    }

    [TestCase]
    public void FighterModeMarksAtTheBaselineAndNeverAtTheStoryUpgrade() {
        // Story/Fighter isolation: the tesla_conductive_hold Resonance node must
        // never reach the deterministic simulation, so the sim applies the
        // authored cross-mode profile value and nothing else.
        BasicStringProfile tesla = BasicComboRules.StringProfileFor("tesla");
        AssertThat(tesla.FinisherMarkFrames)
            .OverrideFailureMessage("The cross-mode table carries only the baseline.")
            .IsEqual(BasicComboRules.ConductiveMarkBaselineFrames);

        var mark = new FighterConductiveComponent { SourcePlayerID = -1 };
        FighterConductiveRules.ApplyMark(ref mark, sourcePlayerID: 0, frames: tesla.FinisherMarkFrames);
        AssertThat(mark.FramesRemaining)
            .OverrideFailureMessage("Fighter Mode marks at 90 frames, never 150.")
            .IsEqual(90);
        AssertThat(mark.FramesRemaining).IsNotEqual(BasicComboRules.ConductiveMarkUpgradedFrames);

        // Decay to zero releases the source identity so a stale owner cannot linger.
        for (int frame = 0; frame < 90; frame++) FighterConductiveRules.Tick(ref mark, frozen: false);
        AssertThat(mark.FramesRemaining).IsEqual(0);
        AssertThat(mark.SourcePlayerID).IsEqual(-1);
        AssertThat(FighterConductiveRules.HasMarkFrom(in mark, 0)).IsFalse();
    }
}
