using System;

namespace FTT.Core {

    /// <summary>
    /// Timeline Integrity &amp; the Siphon Clock (V7.1, rebalanced V7.3;
    /// design Section 3): each level opens at 100%; every living, engaged
    /// Chronal Extractor drains it while it siphons — after a 10 s drain-free
    /// grace window, and never more than its 10% siphon share. Restoration is
    /// earned through play: +3% per destroyed Extractor, +2% per ordinary
    /// secret, +5% for the level's designated special secret, all capped at
    /// 100. The level-end tier feeds the exit beat and the Chronal Rating.
    /// The tier's dust bonus (+10%/+5%/0) is authored here but deliberately
    /// NOT applied to the wallet yet — the dust economy rebalance is deferred
    /// (V7.2 ruling), and the ledger must be retuned before bonuses land.
    /// </summary>
    public static class TimelineIntegrityRules {
        public const float StartPercent = 100f;

        /// <summary>V7.3: the hard cap on what one Extractor can ever steal —
        /// its 10% siphon share, counted in integrity points actually drained.</summary>
        public const float MaxSiphonSharePercent = 10f;

        /// <summary>V7.3: drain-free window after a siphon engages, so peeking
        /// into a room and retreating leaves no permanent scar.</summary>
        public const float SiphonGraceSeconds = 10f;

        /// <summary>V7.3 restoration paths (all capped at 100%).</summary>
        public const float ExtractorDestroyRestorePercent = 3f;
        public const float GenericSecretRestorePercent = 2f;
        public const float SpecialSecretRestorePercent = 5f;

        public const float RestoredThreshold = 90f;
        public const float StabilizedThreshold = 70f;

        /// <summary>
        /// The campaign-ending good-restoration threshold on the campaign-wide
        /// average Integrity — identical on every difficulty (V7.3 ruling).
        /// Authored here now; its consumer lands with the Level 15 ending work.
        /// </summary>
        public const float EndingThresholdPercent = 85f;

        /// <summary>Drain per second per engaged living Extractor (Hard doubles it).</summary>
        public static float DrainPerSecond(Difficulty difficulty) =>
            difficulty == Difficulty.Hard ? 0.2f : 0.1f;

        /// <summary>Level-end tier: 0 Restored, 1 Stabilized, 2 Fractured.</summary>
        public static int Tier(float percent) =>
            percent >= RestoredThreshold ? 0 : percent >= StabilizedThreshold ? 1 : 2;

        public static string TierKey(float percent) => Tier(percent) switch {
            0 => "integrity_tier_restored",
            1 => "integrity_tier_stabilized",
            _ => "integrity_tier_fractured"
        };

        /// <summary>The authored tier dust bonus — recorded, not yet applied
        /// (economy pass deferred).</summary>
        public static int DustBonusPercent(float percent) => Tier(percent) switch {
            0 => 10,
            1 => 5,
            _ => 0
        };
    }

    /// <summary>
    /// The Chronal Rating stamp (V7 results screen): S / A / B / C from time,
    /// rewinds used, secrets found, and Timeline Integrity. Default thresholds
    /// here; levels may author their own par time. It feeds nothing
    /// mechanically — it exists to make a second, better run feel seen.
    /// </summary>
    public static class ChronalRatingRules {
        /// <summary>Default par time when a level authors none.</summary>
        public const float DefaultParTimeSeconds = 600f;

        public static string Compute(
            float integrityPercent,
            int rewindsUsed,
            int secretsFound,
            int secretsTotal,
            float completionSeconds,
            float parTimeSeconds = DefaultParTimeSeconds) {
            int score = 0;
            if (integrityPercent >= TimelineIntegrityRules.RestoredThreshold) score += 2;
            else if (integrityPercent >= TimelineIntegrityRules.StabilizedThreshold) score += 1;
            if (rewindsUsed <= 0) score += 2;
            else if (rewindsUsed <= 2) score += 1;
            if (secretsTotal > 0 && secretsFound >= secretsTotal) score += 1;
            if (parTimeSeconds > 0f && completionSeconds > 0f && completionSeconds <= parTimeSeconds) score += 1;
            return score >= 5 ? "S" : score >= 4 ? "A" : score >= 2 ? "B" : "C";
        }
    }
}
