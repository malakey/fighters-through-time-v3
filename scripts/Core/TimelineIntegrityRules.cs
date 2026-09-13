using System;

namespace FTT.Core {

    /// <summary>
    /// Timeline Integrity — the level timer (V7.6 F01; design Section 3).
    ///
    /// Every timed level opens at 100% and drains continuously from the moment
    /// the level loads, seen or not: there is no engagement check, no room
    /// entry, no grace window and no per-machine share. The rate is
    /// <b>normalized against the level's authored par</b> so the gauge reads
    /// the same way on every level and every difficulty:
    ///
    /// <code>
    /// drainPerSecond = 100 / (parSeconds * difficultyMultiplier)
    ///                  * currentDrainFactor / initialDrainFactor
    /// </code>
    ///
    /// where the drain factor is <c>1.0 + 0.2 * livingExtractors - 0.1 * (secretFound ? 1 : 0)</c>
    /// floored at <see cref="MinimumRawFactor"/>, and the <b>denominator is
    /// fixed at level load</b> from the authored starting Extractor population
    /// — never recalculated from survivors, including on a checkpoint resume.
    /// Breaking a machine or finding the secret therefore only ever slows the
    /// FUTURE rate; neither ever moves the current gauge by a single point.
    /// Nothing adds time back: rewinds do not refund, Time Freeze does not
    /// pause the clock, and there is no restoration path at all.
    ///
    /// At par with no detours (every machine alive, no secret) the gauge ends
    /// at 50% on Easy, 33.33% on Normal and 16.67% on Hard.
    ///
    /// The V7.1/V7.3 siphon model this replaces — the 10% share, the 10 s
    /// grace, the 600 px engagement, the +3/+2/+5 restoration paths, the
    /// 90/70 tier lines, the Hard-doubles flat rate and the Chronal Rating
    /// stamp — is retired outright (Package 11 A3; V7.6 rulings F01 and 2.A).
    /// </summary>
    public static class TimelineIntegrityRules {
        public const float StartPercent = 100f;

        // === Tier lines (V7.6: 90/70 -> 50/20) =============================

        /// <summary>Restored: a final gauge at or above 50%.</summary>
        public const float RestoredThreshold = 50f;

        /// <summary>Stabilized: at or above 20%. Below that, Fractured.</summary>
        public const float StabilizedThreshold = 20f;

        /// <summary>
        /// The campaign-ending good-restoration threshold on the campaign-wide
        /// average Integrity — identical on every difficulty. V7.6 moves it
        /// from 85 to 50 in step with the Restored line, because the F01 clock
        /// makes a par run end near a third rather than near full.
        /// </summary>
        public const float EndingThresholdPercent = 50f;

        // === F01 drain model ===============================================

        /// <summary>Each living Chronal Extractor adds this much to the raw drain factor.</summary>
        public const float ExtractorDrainWeight = 0.2f;

        /// <summary>A found secret subtracts this much from the raw drain factor, permanently.</summary>
        public const float SecretDrainReduction = 0.1f;

        /// <summary>
        /// The floor on the raw factor, so a detoured route on a machine-light
        /// level can never drive the numerator below nine tenths of the
        /// all-alive rate.
        /// </summary>
        public const float MinimumRawFactor = 0.9f;

        /// <summary>Par is multiplied by this per difficulty: Easy 2.0, Normal 1.5, Hard 1.2.</summary>
        public static float DifficultyTimeMultiplier(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 2.0f,
            Difficulty.Hard => 1.2f,
            _ => 1.5f
        };

        /// <summary>
        /// The denominator, fixed at level load from the authored starting
        /// Extractor population. Never recalculated from survivors.
        /// </summary>
        public static float InitialDrainFactor(int startingExtractors) =>
            1.0f + ExtractorDrainWeight * Math.Max(0, startingExtractors);

        /// <summary>The live numerator: living machines up, a found secret down, floored.</summary>
        public static float CurrentDrainFactor(int living, bool secretFound) =>
            Math.Max(
                MinimumRawFactor,
                1.0f + ExtractorDrainWeight * Math.Max(0, living) - (secretFound ? SecretDrainReduction : 0f));

        /// <summary>
        /// Integrity points lost per second of live play. Returns 0 for a
        /// non-positive par (an untimed level authors <c>0</c>).
        /// </summary>
        public static float DrainPerSecond(
            float parSeconds, Difficulty difficulty, int living, int starting, bool secretFound) {
            if (parSeconds <= 0f) return 0f;
            float initial = InitialDrainFactor(starting);
            if (initial <= 0f) return 0f;
            return StartPercent / (parSeconds * DifficultyTimeMultiplier(difficulty))
                * CurrentDrainFactor(living, secretFound) / initial;
        }

        /// <summary>
        /// The all-alive normalized rate — the F11 recovery-budget input and
        /// an upper bound on the live rate, since broken machines and a found
        /// secret can only reduce it.
        /// </summary>
        public static float AllAliveDrainPerSecond(float parSeconds, Difficulty difficulty) =>
            parSeconds <= 0f ? 0f : StartPercent / (parSeconds * DifficultyTimeMultiplier(difficulty));

        // === F11 recovery minima ===========================================

        /// <summary>The provisional safety margin on a measured remaining route (F11).</summary>
        public const float SafetyMarginFraction = 0.20f;

        /// <summary>The lower bound on any granted timer recovery.</summary>
        public const float RecoveryFloorPercent = 25f;

        /// <summary>
        /// The authored recovery requirement for one anchor: the all-alive
        /// rate over the remaining mandatory route plus the 20% margin,
        /// rounded UP to a whole point.
        /// </summary>
        public static int RequiredRecovery(float parSeconds, Difficulty difficulty, float remainingRouteSeconds) {
            if (parSeconds <= 0f) {
                throw new ArgumentOutOfRangeException(
                    nameof(parSeconds), "F11 requires a positive authored par.");
            }
            if (remainingRouteSeconds < 0f || float.IsNaN(remainingRouteSeconds)
                || float.IsInfinity(remainingRouteSeconds)) {
                throw new ArgumentOutOfRangeException(
                    nameof(remainingRouteSeconds), "F11 requires a finite non-negative remaining route.");
            }
            // Computed in double throughout: the single-precision rate carries
            // enough error that an exactly-24-point budget rounds up to 25, and
            // a recovery minimum that drifts by a point between releases is not
            // a budget anyone can validate. The epsilon absorbs the remaining
            // binary-representation noise so a whole-number requirement stays
            // that whole number; anything genuinely above it still rounds up.
            double rate = StartPercent / ((double)parSeconds * DifficultyTimeMultiplier(difficulty));
            double requirement = rate * remainingRouteSeconds * (1.0 + SafetyMarginFraction);
            return (int)Math.Ceiling(requirement - RoundingEpsilon);
        }

        /// <summary>Binary-noise tolerance on the upward rounding (see <see cref="RequiredRecovery"/>).</summary>
        private const double RoundingEpsilon = 1e-6;

        /// <summary>
        /// The recovery minimum for one anchor: the 25-point floor or the
        /// authored requirement, whichever is larger.
        /// <b>A requirement above 100 is an invalid content budget</b> — it
        /// throws, never clamps, because clamping would silently ship a
        /// recovery that cannot finish the route.
        /// </summary>
        public static float RecoveryMinimum(float parSeconds, Difficulty difficulty, float remainingRouteSeconds) {
            int required = RequiredRecovery(parSeconds, difficulty, remainingRouteSeconds);
            if (required > StartPercent) {
                throw new InvalidOperationException(
                    $"F11 recovery budget is invalid: a remaining route of {remainingRouteSeconds:0.##} s "
                    + $"at par {parSeconds:0.##} s ({difficulty}) requires {required} integrity points, "
                    + "which is above 100. Fix the level's par/route budget — never clamp this.");
            }
            return Math.Max(RecoveryFloorPercent, required);
        }

        /// <summary>
        /// The Integrity a timer-caused Collapse grants at
        /// <paramref name="checkpointIntegrity"/>: never below the anchor's
        /// authored minimum, never below what the checkpoint already banked.
        /// </summary>
        public static float TimerRecoveryIntegrity(
            float checkpointIntegrity, float parSeconds, Difficulty difficulty, float remainingRouteSeconds) =>
            Math.Max(
                Math.Clamp(checkpointIntegrity, 0f, StartPercent),
                RecoveryMinimum(parSeconds, difficulty, remainingRouteSeconds));

        // === Level-end tier ================================================

        /// <summary>Level-end tier: 0 Restored, 1 Stabilized, 2 Fractured.</summary>
        public static int Tier(float percent) =>
            percent >= RestoredThreshold ? 0 : percent >= StabilizedThreshold ? 1 : 2;

        public static string TierKey(float percent) => Tier(percent) switch {
            0 => "integrity_tier_restored",
            1 => "integrity_tier_stabilized",
            _ => "integrity_tier_fractured"
        };

        /// <summary>The authored tier dust bonus. A10 applies it to the wallet.</summary>
        public static int DustBonusPercent(float percent) => Tier(percent) switch {
            0 => 10,
            1 => 5,
            _ => 0
        };

        // === Collapse Tremor thresholds ====================================

        /// <summary>Stage 1 Tremor begins below this gauge value.</summary>
        public const float TremorStage1Threshold = 20f;

        /// <summary>Stage 2 Tremor begins below this gauge value.</summary>
        public const float TremorStage2Threshold = 10f;

        /// <summary>0 = calm, 1 = below 20%, 2 = below 10%.</summary>
        public static int TremorStage(float percent) =>
            percent < TremorStage2Threshold ? 2 : percent < TremorStage1Threshold ? 1 : 0;
    }
}
