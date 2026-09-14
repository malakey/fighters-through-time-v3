using System;
using System.Collections.Generic;

namespace FTT.Environment {

    /// <summary>
    /// One finite reward source's claim on an authored budget pool.
    ///
    /// <para><see cref="Weight"/> is an <b>allocation ratio, never a flat drop
    /// amount</b> (F05: standard enemy = 1, elite/Eraser = 5).
    /// <see cref="FixedShare"/> is an authored amount carved out of the pool
    /// before the weighted remainder is distributed — the contract's "scripted
    /// mandatory encounter rewards must replace an equivalent allocation, not
    /// add to it".</para>
    /// </summary>
    public readonly struct RewardSourceWeight {
        public readonly string SourceID;
        public readonly int Weight;
        public readonly int FixedShare;

        public RewardSourceWeight(string sourceID, int weight, int fixedShare = 0) {
            SourceID = sourceID ?? "";
            Weight = Math.Max(0, weight);
            FixedShare = Math.Max(0, fixedShare);
        }
    }

    /// <summary>
    /// The F05 authored-budget allocator (docs/design-contracts/DUST_ECONOMY.md
    /// §"Turning budgets into drops", steps 2-3 and 6).
    ///
    /// <para>Pure C# — no Godot types, no resource loads, no randomness. Given
    /// an integer pool <c>P</c> and a weighted source list, every source gets
    /// <c>floor(P x weight[i] / sum(weights))</c> and the leftover units go to
    /// the <b>largest fractional remainders, ties broken by stable source
    /// ID</b>. The allocation therefore sums to <c>P</c> exactly, for every
    /// difficulty's compiled source list, with no float rounding anywhere: the
    /// quotas are computed in integer arithmetic as <c>(P x w) / sum</c> plus
    /// the exact modulus.</para>
    ///
    /// <para><b>Zero allocations are legal</b> and spawn no currency pickup.
    /// The pre-V7.6 "every enemy guarantees dust" rule is retired.</para>
    /// </summary>
    public static class RewardAllocator {

        /// <summary>F05 default relative weight for a standard enemy source.</summary>
        public const int StandardWeight = 1;

        /// <summary>F05 default relative weight for an elite / Eraser source.</summary>
        public const int EliteWeight = 5;

        /// <summary>
        /// Distributes <paramref name="pool"/> across <paramref name="sources"/>.
        /// Fixed shares are honoured first (clamped so they can never overdraw
        /// the pool); the weighted remainder is then allocated by
        /// floor-plus-largest-remainder. The returned amounts always sum to
        /// <paramref name="pool"/> whenever the pool is positive and at least
        /// one source can carry it.
        /// </summary>
        public static Dictionary<string, int> Allocate(
            int pool, IReadOnlyList<RewardSourceWeight> sources) {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (sources == null || sources.Count == 0) return result;
            foreach (RewardSourceWeight source in sources) {
                if (!string.IsNullOrEmpty(source.SourceID)) result[source.SourceID] = 0;
            }
            if (pool <= 0) return result;

            // 1. Authored fixed shares come out of the pool first, in list
            //    order, clamped to what is left. An over-authored fixed share
            //    is a content bug that must not inflate the level's total.
            int remaining = pool;
            foreach (RewardSourceWeight source in sources) {
                if (string.IsNullOrEmpty(source.SourceID) || source.FixedShare <= 0) continue;
                int share = Math.Min(source.FixedShare, remaining);
                result[source.SourceID] += share;
                remaining -= share;
                if (remaining <= 0) break;
            }
            if (remaining <= 0) return result;

            // 2. The weighted remainder. Sources with a fixed share do not also
            //    draw from it — F05 says a fixed share "distributes only the
            //    remainder" across the rest.
            var weighted = new List<RewardSourceWeight>();
            long weightSum = 0;
            foreach (RewardSourceWeight source in sources) {
                if (string.IsNullOrEmpty(source.SourceID)) continue;
                if (source.FixedShare > 0 || source.Weight <= 0) continue;
                weighted.Add(source);
                weightSum += source.Weight;
            }
            if (weighted.Count == 0 || weightSum <= 0) return result;

            // floor(P*w/sum) for each, exact integer modulus kept as the
            // fractional remainder for the leftover pass.
            var remainders = new List<(string SourceID, long Remainder, int Order)>(weighted.Count);
            int assigned = 0;
            for (int index = 0; index < weighted.Count; index++) {
                RewardSourceWeight source = weighted[index];
                long numerator = (long)remaining * source.Weight;
                int floorPart = (int)(numerator / weightSum);
                result[source.SourceID] += floorPart;
                assigned += floorPart;
                remainders.Add((source.SourceID, numerator % weightSum, index));
            }

            int leftover = remaining - assigned;
            if (leftover <= 0) return result;

            // Largest fractional remainder wins; ties break by STABLE SOURCE ID
            // (ordinal), never by list order, so the same authored inventory
            // always allocates identically.
            remainders.Sort((left, right) => {
                int byRemainder = right.Remainder.CompareTo(left.Remainder);
                if (byRemainder != 0) return byRemainder;
                return string.CompareOrdinal(left.SourceID, right.SourceID);
            });
            for (int index = 0; index < leftover && index < remainders.Count; index++) {
                result[remainders[index].SourceID] += 1;
            }
            return result;
        }

        /// <summary>
        /// The F05 "split evenly by the same integer/remainder rule" helper used
        /// for the Extractor half of an optional pool: every source carries
        /// weight 1, so 10 across three machines pays 4/3/3 and 5 across three
        /// pays 2/2/1.
        /// </summary>
        public static Dictionary<string, int> SplitEvenly(int pool, IReadOnlyList<string> sourceIDs) {
            var weights = new List<RewardSourceWeight>();
            if (sourceIDs != null) {
                foreach (string sourceID in sourceIDs) {
                    if (!string.IsNullOrEmpty(sourceID)) weights.Add(new RewardSourceWeight(sourceID, 1));
                }
            }
            return Allocate(pool, weights);
        }

        /// <summary>
        /// The Integrity tier bonus, applied exactly once at successful level
        /// completion: <c>floor(retainedBaseDust x tierRate)</c> with the
        /// authored 10 / 5 / 0 percent tier rates. A level with no Integrity
        /// clock (Level 1) passes <paramref name="levelIsTimed"/> false and is
        /// paid nothing — which is precisely why the all-Restored maxima are
        /// 787 / 1,094 rather than 792 / 1,100.
        /// </summary>
        public static int TierBonus(int retainedBaseDust, float integrityPercent, bool levelIsTimed) {
            if (!levelIsTimed || retainedBaseDust <= 0) return 0;
            int rate = FTT.Core.TimelineIntegrityRules.DustBonusPercent(integrityPercent);
            if (rate <= 0) return 0;
            return (int)((long)retainedBaseDust * rate / 100);
        }
    }
}
