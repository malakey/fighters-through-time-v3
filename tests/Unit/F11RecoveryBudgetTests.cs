using System;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the F11 timer-recovery budget arithmetic (Package 11 A3), against the
/// worked example in <c>docs/design-contracts/CHECKPOINT_RECOVERY.md</c>.
///
/// <code>
/// allAliveDrainPerSecond = 100 / (parSeconds * difficultyMultiplier)
/// requiredRecovery       = ceil(allAliveDrainPerSecond * remainingRouteSeconds * 1.20)
/// recoveryMinimum        = max(25, requiredRecovery)
/// timerRecoveryIntegrity = max(checkpointIntegrity, recoveryMinimum)
/// </code>
///
/// The pins are on the <b>arithmetic</b>, never on content: no measured
/// per-level route timings exist yet, and the provisional values the level
/// controllers author are recorded in the deviation ledger under
/// <c>VERIFY-PAR-SECONDS</c> rather than asserted here as if they were real.
/// </summary>
[TestSuite]
public class F11RecoveryBudgetTests {

    // The contract's worked example: par 600 s, 240 s of remaining route.
    private const float Par = 600f;
    private const float Route = 240f;

    [TestCase]
    public void TheArithmeticMatchesTheContractsWorkedExample() {
        AssertThat(TimelineIntegrityRules.AllAliveDrainPerSecond(Par, Difficulty.Easy))
            .IsEqualApprox(0.0833333f, 0.000001f);
        AssertThat(TimelineIntegrityRules.AllAliveDrainPerSecond(Par, Difficulty.Normal))
            .IsEqualApprox(0.1111111f, 0.000001f);
        AssertThat(TimelineIntegrityRules.AllAliveDrainPerSecond(Par, Difficulty.Hard))
            .IsEqualApprox(0.1388889f, 0.000001f);

        // 288 budgeted seconds after the margin -> 24 / 32 / 40 points.
        AssertThat(TimelineIntegrityRules.RequiredRecovery(Par, Difficulty.Easy, Route)).IsEqual(24);
        AssertThat(TimelineIntegrityRules.RequiredRecovery(Par, Difficulty.Normal, Route)).IsEqual(32);
        AssertThat(TimelineIntegrityRules.RequiredRecovery(Par, Difficulty.Hard, Route)).IsEqual(40);

        // ...and the minima are 25 / 32 / 40, the floor lifting only Easy.
        AssertThat(TimelineIntegrityRules.RecoveryMinimum(Par, Difficulty.Easy, Route)).IsEqual(25f);
        AssertThat(TimelineIntegrityRules.RecoveryMinimum(Par, Difficulty.Normal, Route)).IsEqual(32f);
        AssertThat(TimelineIntegrityRules.RecoveryMinimum(Par, Difficulty.Hard, Route)).IsEqual(40f);
    }

    [TestCase]
    public void TheTwentyFivePointFloorHoldsAndABetterCheckpointAlwaysWins() {
        AssertThat(TimelineIntegrityRules.RecoveryFloorPercent).IsEqual(25f);

        // A checkpoint that banked 10% still recovers to the authored minimum.
        AssertThat(TimelineIntegrityRules.TimerRecoveryIntegrity(10f, Par, Difficulty.Easy, Route))
            .IsEqual(25f);
        AssertThat(TimelineIntegrityRules.TimerRecoveryIntegrity(10f, Par, Difficulty.Hard, Route))
            .IsEqual(40f);

        // A checkpoint that banked 60% keeps 60% on every difficulty:
        // "retaining a saved checkpoint gauge above the minimum never lowers it".
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            AssertThat(TimerRecovery(60f, difficulty)).IsEqual(60f);
        }

        // A zero-length remaining route (the PreBoss anchor) is just the floor.
        AssertThat(TimelineIntegrityRules.RecoveryMinimum(Par, Difficulty.Hard, 0f)).IsEqual(25f);

        // And every grant starts at or above the 20% Tremor threshold, so a
        // recovery never drops the player straight back into the Tremor.
        AssertThat(TimelineIntegrityRules.RecoveryFloorPercent)
            .IsGreater(TimelineIntegrityRules.TremorStage1Threshold);
    }

    [TestCase]
    public void TheTwentyPercentMarginIsAppliedAndTheResultIsRoundedUp() {
        AssertThat(TimelineIntegrityRules.SafetyMarginFraction).IsEqualApprox(0.20f, 0.0001f);

        // Without the margin, Normal/600/240 would need 26.67 -> 27 points.
        // With it, 32. The margin is what separates those two numbers.
        double unmargined = Math.Ceiling(
            TimelineIntegrityRules.AllAliveDrainPerSecond(Par, Difficulty.Normal) * Route);
        AssertThat((int)unmargined).IsEqual(27);
        AssertThat(TimelineIntegrityRules.RequiredRecovery(Par, Difficulty.Normal, Route)).IsEqual(32);

        // Rounding is upward to a whole point, never to nearest: a route that
        // needs 30.01 points must be budgeted 31.
        // 100/(600*1.5) * r * 1.2 = 30.01  ->  r = 225.075
        AssertThat(TimelineIntegrityRules.RequiredRecovery(Par, Difficulty.Normal, 225.075f)).IsEqual(31);
    }

    [TestCase]
    public void ABudgetAboveOneHundredIsRejectedLoudlyAndNeverClampedToOneHundred() {
        // The contract's own counter-example: a 700 s Hard route at par 600
        // needs 117 points after the margin. That is an invalid CONTENT budget,
        // and silently clamping it to 100 would ship a recovery that provably
        // cannot finish the route.
        AssertThat(TimelineIntegrityRules.RequiredRecovery(Par, Difficulty.Hard, 700f)).IsEqual(117);
        AssertThrown(() => TimelineIntegrityRules.RecoveryMinimum(Par, Difficulty.Hard, 700f))
            .IsInstanceOf<InvalidOperationException>();
        AssertThrown(() => TimelineIntegrityRules.TimerRecoveryIntegrity(60f, Par, Difficulty.Hard, 700f))
            .IsInstanceOf<InvalidOperationException>();

        // Inputs are validated too: a non-positive par and a negative or
        // non-finite route are authoring errors, not silent zeroes.
        AssertThrown(() => TimelineIntegrityRules.RequiredRecovery(0f, Difficulty.Normal, Route))
            .IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThrown(() => TimelineIntegrityRules.RequiredRecovery(Par, Difficulty.Normal, -1f))
            .IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThrown(() => TimelineIntegrityRules.RequiredRecovery(Par, Difficulty.Normal, float.NaN))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    private static float TimerRecovery(float banked, Difficulty difficulty) =>
        TimelineIntegrityRules.TimerRecoveryIntegrity(banked, Par, difficulty, Route);
}
