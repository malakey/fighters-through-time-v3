using System.Collections.Generic;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A10 — the F05 authored-budget allocator
/// (docs/design-contracts/DUST_ECONOMY.md §"Turning budgets into drops").
///
/// <para>Pure C#: the allocator takes an integer pool and a weighted source
/// list and never touches Godot, a resource or a random number. Every case here
/// is an arithmetic invariant of the contract, not a snapshot of shipped
/// content.</para>
/// </summary>
[TestSuite]
public class DustAllocatorTests {

    private static List<RewardSourceWeight> Standards(int count, string prefix = "s") {
        var sources = new List<RewardSourceWeight>();
        for (int index = 0; index < count; index++) {
            sources.Add(new RewardSourceWeight($"{prefix}{index:00}", RewardAllocator.StandardWeight));
        }
        return sources;
    }

    private static int Sum(Dictionary<string, int> allocation) {
        int total = 0;
        foreach (KeyValuePair<string, int> entry in allocation) total += entry.Value;
        return total;
    }

    [TestCase]
    public void TheWorkedExampleAllocatesExactlyFifteenAcrossTenStandardsAndTwoElites() {
        // The contract's own example: "allocating 15 encounter dust to ten
        // standard enemies and two elites gives quotas of 0.75 for each standard
        // and 3.75 for each elite. Integer allocation pays the exact 15 across
        // that authored list; it does not promise 1-2 per standard plus 20 per
        // elite."
        var sources = Standards(10);
        sources.Add(new RewardSourceWeight("e00", RewardAllocator.EliteWeight));
        sources.Add(new RewardSourceWeight("e01", RewardAllocator.EliteWeight));

        Dictionary<string, int> allocation = RewardAllocator.Allocate(15, sources);

        AssertThat(Sum(allocation))
            .OverrideFailureMessage("The allocation must equal the authored pool exactly.")
            .IsEqual(15);
        AssertThat(allocation.Count).IsEqual(12);
        // 5:1 weights: neither elite may be paid less than any standard.
        foreach (string standardID in new[] { "s00", "s05", "s09" }) {
            AssertThat(allocation["e00"] >= allocation[standardID]).IsTrue();
            AssertThat(allocation["e01"] >= allocation[standardID]).IsTrue();
        }
        // Equal weights are paid equally: the two elites tie.
        AssertThat(allocation["e00"]).IsEqual(allocation["e01"]);
    }

    [TestCase]
    public void LeftoverUnitsBreakTiesByStableSourceIDNotByListOrder() {
        // 5 across four identical sources: floor 1 each, one unit left over.
        // Every fractional remainder ties, so the stable source ID decides —
        // and the answer must not depend on the order the manifest listed them.
        var forward = new List<RewardSourceWeight> {
            new("alpha", 1), new("bravo", 1), new("charlie", 1), new("delta", 1)
        };
        var reversed = new List<RewardSourceWeight> {
            new("delta", 1), new("charlie", 1), new("bravo", 1), new("alpha", 1)
        };

        Dictionary<string, int> first = RewardAllocator.Allocate(5, forward);
        Dictionary<string, int> second = RewardAllocator.Allocate(5, reversed);

        AssertThat(Sum(first)).IsEqual(5);
        AssertThat(Sum(second)).IsEqual(5);
        AssertThat(first["alpha"])
            .OverrideFailureMessage("The lowest ordinal source ID must win a remainder tie.")
            .IsEqual(2);
        foreach (string id in new[] { "alpha", "bravo", "charlie", "delta" }) {
            AssertThat(first[id])
                .OverrideFailureMessage($"'{id}' allocated differently when the list order changed.")
                .IsEqual(second[id]);
        }
    }

    [TestCase]
    public void MoreSourcesChangeTheDistributionAndNeverTheTotal() {
        // "Each difficulty compiles its authored source list against the same
        // level pool. More enemies on Hard change the distribution, not the
        // total." Easy trims the list, Hard can extend it; the pool is fixed.
        Dictionary<string, int> easy = RewardAllocator.Allocate(15, Standards(5));
        Dictionary<string, int> normal = RewardAllocator.Allocate(15, Standards(8));
        Dictionary<string, int> hard = RewardAllocator.Allocate(15, Standards(11));

        AssertThat(Sum(easy)).IsEqual(15);
        AssertThat(Sum(normal)).IsEqual(15);
        AssertThat(Sum(hard)).IsEqual(15);
        AssertThat(easy["s00"] > hard["s00"])
            .OverrideFailureMessage("A longer source list must dilute each source's share.")
            .IsTrue();
    }

    [TestCase]
    public void AZeroWeightOrUnlistedSourceDrawsNothing() {
        // "Reinforcements/summons that can repeat indefinitely have zero dust."
        // A zero-weight source is a legal authored entry that pays nothing, and
        // a source the manifest never lists is simply absent — neither one is
        // ever topped up from the pool, and neither reduces it.
        var sources = Standards(3);
        sources.Add(new RewardSourceWeight("summon_wave", 0));

        Dictionary<string, int> allocation = RewardAllocator.Allocate(9, sources);

        AssertThat(Sum(allocation)).IsEqual(9);
        AssertThat(allocation["summon_wave"])
            .OverrideFailureMessage("A repeatable spawn must draw zero.")
            .IsEqual(0);
        AssertThat(allocation.ContainsKey("never_authored")).IsFalse();
        foreach (string id in new[] { "s00", "s01", "s02" }) AssertThat(allocation[id]).IsEqual(3);
    }

    [TestCase]
    public void AFixedAuthoredShareIsCarvedOutAndOnlyTheRemainderIsDistributed() {
        // "Scripted mandatory encounter rewards must REPLACE an equivalent
        // allocation, not add to it" — and "if an author wants a particular
        // elite to carry more of the pool, author a fixed share and distribute
        // only the remainder; validate the unchanged total."
        var sources = new List<RewardSourceWeight> {
            new("scripted_ambush", 0, 6)
        };
        sources.AddRange(Standards(3));

        Dictionary<string, int> allocation = RewardAllocator.Allocate(15, sources);

        AssertThat(Sum(allocation))
            .OverrideFailureMessage("A fixed share must not inflate the level's total.")
            .IsEqual(15);
        AssertThat(allocation["scripted_ambush"]).IsEqual(6);
        foreach (string id in new[] { "s00", "s01", "s02" }) AssertThat(allocation[id]).IsEqual(3);

        // An over-authored fixed share is a content bug that must be clamped,
        // never allowed to overdraw the budget row.
        Dictionary<string, int> overdrawn = RewardAllocator.Allocate(
            4, new List<RewardSourceWeight> { new("greedy", 0, 99) });
        AssertThat(Sum(overdrawn)).IsEqual(4);
    }

    [TestCase]
    public void TheEvenSplitMatchesTheContractsExtractorExamples() {
        // "With a 20-dust optional pool, two Extractors pay 5 each or three pay
        // 4/3/3... With a 10-dust optional pool, two pay 3/2 or three pay
        // 2/2/1." (Each figure is half the stated optional pool, the Extractor
        // share; the discovery reward takes the other half.)
        var two = new List<string> { "x0", "x1" };
        var three = new List<string> { "x0", "x1", "x2" };

        Dictionary<string, int> twoOfTen = RewardAllocator.SplitEvenly(10, two);
        AssertThat(twoOfTen["x0"]).IsEqual(5);
        AssertThat(twoOfTen["x1"]).IsEqual(5);

        Dictionary<string, int> threeOfTen = RewardAllocator.SplitEvenly(10, three);
        AssertThat(threeOfTen["x0"]).IsEqual(4);
        AssertThat(threeOfTen["x1"]).IsEqual(3);
        AssertThat(threeOfTen["x2"]).IsEqual(3);

        Dictionary<string, int> twoOfFive = RewardAllocator.SplitEvenly(5, two);
        AssertThat(twoOfFive["x0"]).IsEqual(3);
        AssertThat(twoOfFive["x1"]).IsEqual(2);

        Dictionary<string, int> threeOfFive = RewardAllocator.SplitEvenly(5, three);
        AssertThat(threeOfFive["x0"]).IsEqual(2);
        AssertThat(threeOfFive["x1"]).IsEqual(2);
        AssertThat(threeOfFive["x2"]).IsEqual(1);

        AssertThat(Sum(RewardAllocator.SplitEvenly(0, three))).IsEqual(0);
    }
}
