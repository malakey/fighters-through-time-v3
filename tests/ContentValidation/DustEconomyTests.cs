using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// The Chronal Dust economy ledger, re-derived from
/// <c>docs/design-contracts/DUST_ECONOMY.md</c> (F05 Option A) for Package 11
/// A10. The pre-V7.6 rate card — 1-2 per standard, 20 per elite, 50 per boss,
/// 25 (shipped 15) per Extractor, ~1,787 per campaign — is retired: rewards are
/// now <b>authored per-level budgets</b> allocated across stable source IDs.
///
/// <para>Every number here is a contract figure, not a measurement of shipped
/// scenes. <c>RewardManifestTests</c> proves the shipped inventory against these
/// rows; this suite proves the rows themselves, the resource values that carry
/// them, and the campaign totals they add up to.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DustEconomyTests {
    private static readonly string[] CharacterIDs = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "pocahontas"
    };

    // The contract's grid cost curve. Unchanged by F05 Option A, which was
    // chosen precisely to retain the 975-dust grid and its upgrade choices.
    private const int TierOneCost = 50;
    private const int TierTwoCost = 75;
    private const int MajorCost = 200;
    private const int FullGridCost = 3 * TierOneCost + 3 * TierTwoCost + 3 * MajorCost; // 975

    // The contract's §"Per-level budgets" table, in campaign-route order.
    // { CampaignLevel index, required encounters, boss, optional }
    internal static readonly int[][] LedgerRows = {
        new[] { 1, 25, 25, 10 },
        new[] { 2, 15, 25, 10 },
        new[] { 3, 15, 25, 10 },
        new[] { 4, 15, 25, 20 },
        new[] { 16, 15, 25, 10 },   // Level 4A — one hero's Legacy Level, not all nine
        new[] { 5, 15, 25, 20 },
        new[] { 6, 15, 25, 20 },
        new[] { 7, 15, 25, 20 },
        new[] { 8, 15, 25, 20 },
        new[] { 9, 15, 25, 20 },
        new[] { 10, 15, 25, 20 },
        new[] { 11, 15, 25, 20 },
        new[] { 12, 25, 25, 20 },
        new[] { 13, 35, 25, 20 },
        new[] { 14, 35, 25, 20 },
        new[] { 15, 35, 25, 20 }
    };

    private const int BossAward = 25;
    private const int RequiredEncounterTotal = 320;
    private const int BossTotal = 400;
    private const int OptionalTotal = 280;
    private const int RequiredBase = 720;
    private const int ThoroughBase = 1000;
    private const int RequiredAllRestoredMaximum = 787;
    private const int ThoroughAllRestoredMaximum = 1094;

    /// <summary>Level 1 is untimed and can never pay an Integrity tier bonus.</summary>
    private static bool IsTimed(int campaignLevelIndex) => campaignLevelIndex != 1;

    /// <summary>
    /// A run visits Levels 1-15 plus EXACTLY ONE character's 4A, so the ledger-row
    /// arithmetic below reads 4A once. With no hero this resolves to the
    /// representative variant; all nine carry the identical row, which
    /// <c>RewardManifestTests</c> proves per hero against each controller's own table.
    /// </summary>
    private static LevelRewardManifest Manifest(int campaignLevelIndex, string heroCharacterID = "") =>
        AuthoredResources.Load<LevelRewardManifest>(
            LevelRewardManifest.PathFor(campaignLevelIndex, heroCharacterID));

    [TestCase]
    public void AllNineGridsShareTheDocumentedCostCurve() {
        // 3x50 + 3x75 + 3x200 = 975 on every grid, competitively normalized —
        // character identity comes from effects, not price. F05 Option A was
        // selected to KEEP this curve rather than deflate the grid.
        foreach (string characterID in CharacterIDs) {
            ResonanceGridData grid = AuthoredResources.Load<ResonanceGridData>(
                $"res://resources/Resonance/{characterID}_grid.tres");
            AssertObject(grid).IsNotNull();

            int total = 0;
            var minorCosts = new List<int>();
            var majorCosts = new List<int>();
            foreach (ResonanceNodeData node in grid.Nodes) {
                AssertThat(node.UnlockCost > 0).IsTrue();
                total += node.UnlockCost;
                if (node.Type == ResonanceNodeType.Major) {
                    majorCosts.Add(node.UnlockCost);
                    AssertThat(node.UnlockCost).IsEqual(MajorCost);
                } else {
                    minorCosts.Add(node.UnlockCost);
                    // Package 11 A4: V7.6 breaks the old "Tier 1 has no
                    // prerequisites" inference in four grids, so the curve is
                    // checked against the explicit Tier field the node carries.
                    AssertThat(node.UnlockCost).IsEqual(
                        node.Tier == 1 ? TierOneCost : TierTwoCost);
                }
            }

            AssertThat(majorCosts.Count).IsEqual(3);
            AssertThat(minorCosts.Count).IsEqual(6);
            AssertThat(majorCosts.Min() > minorCosts.Max()).IsTrue();
            AssertThat(total).IsEqual(FullGridCost);
        }
    }

    [TestCase]
    public void EveryAuthoredManifestCarriesItsExactLedgerBudgetRow() {
        var issues = new List<string>();
        foreach (int[] row in LedgerRows) {
            LevelRewardManifest manifest = Manifest(row[0]);
            if (manifest == null) {
                issues.Add($"level {row[0]}: no manifest authored");
                continue;
            }
            if (manifest.CampaignLevelIndex != row[0]) {
                issues.Add($"level {row[0]}: manifest declares index {manifest.CampaignLevelIndex}");
            }
            if (manifest.RequiredEncounterPool != row[1]) {
                issues.Add($"level {row[0]}: required {manifest.RequiredEncounterPool} != {row[1]}");
            }
            if (manifest.BossAward != row[2]) {
                issues.Add($"level {row[0]}: boss {manifest.BossAward} != {row[2]}");
            }
            if (manifest.OptionalPool != row[3]) {
                issues.Add($"level {row[0]}: optional {manifest.OptionalPool} != {row[3]}");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheCampaignBudgetsTotalSevenHundredTwentyBaseAndOneThousandThorough() {
        // "Verified design arithmetic: 16 boss awards total 400; required
        // encounter budgets total 320; optional allocations total 280; base
        // totals are 720/1,000."
        int required = 0;
        int boss = 0;
        int optional = 0;
        foreach (int[] row in LedgerRows) {
            LevelRewardManifest manifest = Manifest(row[0]);
            AssertObject(manifest).IsNotNull();
            required += manifest.RequiredEncounterPool;
            boss += manifest.BossAward;
            optional += manifest.OptionalPool;
        }
        AssertThat(required).IsEqual(RequiredEncounterTotal);
        AssertThat(boss).IsEqual(BossTotal);
        AssertThat(optional).IsEqual(OptionalTotal);
        AssertThat(required + boss).IsEqual(RequiredBase);
        AssertThat(required + boss + optional).IsEqual(ThoroughBase);
        // A run is Levels 1-15 plus EXACTLY ONE character's 4A, not all nine.
        AssertThat(LedgerRows.Length).IsEqual(16);
    }

    [TestCase]
    public void AllSixteenBossesPayTwentyFiveAndOnlyTheBossForcesTheLargeIcon() {
        // "Boss rewards are 25 dust each (16 bosses = 400)... The Mirror Paradox
        // boss follows the same 25-dust physical-pickup rule."
        var issues = new List<string>();
        foreach (string path in TopLevelResources("res://resources/Bosses")) {
            BossData boss = AuthoredResources.Load<BossData>(path);
            if (boss == null) continue;
            if (boss.ChronalDustDrop != BossAward) {
                issues.Add($"{path} pays {boss.ChronalDustDrop}, not {BossAward}");
            }
        }
        // All nine Legacy bosses, not just one: a run visits exactly one of them,
        // so any variant paying a different number silently changes that run's row.
        foreach (string path in TopLevelResources("res://resources/Bosses/legacy")) {
            BossData legacyBoss = AuthoredResources.Load<BossData>(path);
            if (legacyBoss == null) continue;
            if (legacyBoss.ChronalDustDrop != BossAward) {
                issues.Add($"the Level 4A boss {path} pays {legacyBoss.ChronalDustDrop}, not {BossAward}");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

        // The BossData default must agree, so a new boss resource inherits 25.
        var fresh = new BossData();
        AssertThat(fresh.ChronalDustDrop).IsEqual(BossAward);

        // Every manifest reserves that same 25 as its single boss award. Level
        // 4A's manifest is per hero, so all nine are checked.
        foreach ((int levelIndex, string hero) in RewardManifestTests.LedgerManifests()) {
            AssertThat(Manifest(levelIndex, hero).BossAward).IsEqual(BossAward);
        }
    }

    [TestCase]
    public void RewardsLandInTheirQuantityVisualBandsWithNoForcedMilestonePlate() {
        // "Keep the existing quantity-based sprite thresholds: Small 1-5, Medium
        // 6-24, Large 25+. Every boss still produces a Large pickup at the arena
        // centre. An ordinary Extractor produces a Small or Medium icon
        // according to its actual award, not a forced Large icon."
        DustVisualTierSet tiers = AuthoredResources.Load<DustVisualTierSet>(
            "res://resources/Drops/dust_visual_tiers.tres");
        AssertObject(tiers).IsNotNull();
        AssertThat(tiers.MediumThreshold).IsEqual(6);
        AssertThat(tiers.LargeThreshold).IsEqual(25);

        // A 25-dust boss award is exactly the Large threshold.
        AssertThat(BossAward >= tiers.LargeThreshold).IsTrue();

        // No authored Extractor share on any level can reach the Large band:
        // the largest optional pool is 20 and half of it is split across the
        // machines, so a machine's icon is always its real quantity.
        var issues = new List<string>();
        foreach (int[] row in LedgerRows) {
            LevelRewardManifest manifest = Manifest(row[0]);
            LevelRewardLedger ledger = LevelRewardDirectory.Compile(manifest, Difficulty.Normal);
            foreach (string extractorID in manifest.ExtractorSourceIDs) {
                int award = ledger.Award(extractorID);
                if (award >= tiers.LargeThreshold) {
                    issues.Add($"{extractorID} pays {award}, which would force a milestone plate");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

        // The advisory fallback on the Extractor scene sits in the Small band
        // rather than the retired flat 15.
        PackedScene extractorScene = ResourceLoader.Load<PackedScene>(
            "res://scenes/templates/ChronalExtractorTemplate.tscn");
        var extractor = extractorScene.Instantiate<ChronalExtractor>();
        try {
            AssertThat(extractor.DustReward < tiers.MediumThreshold)
                .OverrideFailureMessage(
                    $"The advisory Extractor fallback is {extractor.DustReward}; the flat 15 is retired.")
                .IsTrue();
        } finally {
            extractor.Free();
        }
    }

    [TestCase]
    public void AllRestoredMaximaAreSevenEightySevenAndOneZeroNineFourBecauseLevelOneIsUntimed() {
        // "All-Restored maxima are 787 required and 1,094 thorough, not
        // 792/1,100: Level 1 receives no tier bonus."
        int requiredWithBonus = 0;
        int thoroughWithBonus = 0;
        foreach (int[] row in LedgerRows) {
            int requiredTotal = row[1] + row[2];
            int thoroughTotal = requiredTotal + row[3];
            requiredWithBonus += requiredTotal
                + RewardAllocator.TierBonus(requiredTotal, 100f, IsTimed(row[0]));
            thoroughWithBonus += thoroughTotal
                + RewardAllocator.TierBonus(thoroughTotal, 100f, IsTimed(row[0]));
        }
        AssertThat(requiredWithBonus).IsEqual(RequiredAllRestoredMaximum);
        AssertThat(thoroughWithBonus).IsEqual(ThoroughAllRestoredMaximum);

        // Naively paying Level 1 a bonus is exactly the 792/1,100 the contract
        // rejects — pinned so the exclusion cannot be "simplified" away.
        int naiveRequired = 0;
        foreach (int[] row in LedgerRows) {
            int requiredTotal = row[1] + row[2];
            naiveRequired += requiredTotal + RewardAllocator.TierBonus(requiredTotal, 100f, true);
        }
        AssertThat(naiveRequired).IsEqual(792);
    }

    [TestCase]
    public void LevelZeroAndTrainingAwardNoPersistentDust() {
        // "Level 0 and training award no persistent dust." The tutorial ships a
        // manifest with empty pools rather than no manifest at all, because a
        // missing manifest would fall back to the advisory resource values.
        LevelRewardManifest tutorial = Manifest(0);
        AssertObject(tutorial)
            .OverrideFailureMessage("Level 0 must author an explicitly empty reward manifest.")
            .IsNotNull();
        AssertThat(tutorial.RequiredEncounterPool).IsEqual(0);
        AssertThat(tutorial.BossAward).IsEqual(0);
        AssertThat(tutorial.OptionalPool).IsEqual(0);

        LevelRewardLedger ledger = LevelRewardDirectory.Compile(tutorial, Difficulty.Normal);
        AssertObject(ledger).IsNotNull();
        AssertThat(ledger.Awards.Count).IsEqual(0);
        AssertThat(ledger.EnemyQueues.Count).IsEqual(0);
        AssertThat(ledger.Award("hologram_drone")).IsEqual(0);
    }

    [TestCase]
    public void TheLedgerFundsTheContractsUpgradePacingAndNeverTheFullGridOnTheRequiredRoute() {
        // §"Upgrade pacing": the first minor (50) is affordable after Level 1 on
        // every route; a thorough all-Restored run reaches 1,006 after Level 14
        // and so completes the grid at Level 15's entry Beacon; and the required
        // route never funds the full grid, even all-Restored.
        AssertThat(LedgerRows[0][1] + LedgerRows[0][2] >= TierOneCost)
            .OverrideFailureMessage("Level 1 alone must fund the first 50-dust minor.")
            .IsTrue();

        int thoroughThroughFourteen = 0;
        foreach (int[] row in LedgerRows) {
            if (row[0] == 15) continue;                     // everything before the finale
            int thoroughTotal = row[1] + row[2] + row[3];
            thoroughThroughFourteen += thoroughTotal
                + RewardAllocator.TierBonus(thoroughTotal, 100f, IsTimed(row[0]));
        }
        AssertThat(thoroughThroughFourteen).IsEqual(1006);
        AssertThat(thoroughThroughFourteen >= FullGridCost).IsTrue();

        // The required route's ceiling is 787 — below the 975 grid, deliberately.
        AssertThat(RequiredAllRestoredMaximum < FullGridCost)
            .OverrideFailureMessage("The required route must never fund the full grid.")
            .IsTrue();
        // A thorough run without bonuses reaches 1,000 only after the finale, so
        // those rewards cannot improve the final fight.
        AssertThat(ThoroughBase >= FullGridCost).IsTrue();
        AssertThat(ThoroughBase - LedgerRows[^1].Skip(1).Sum() < FullGridCost).IsTrue();
    }

    /// <summary>Non-recursive: the boss data resources, excluding the Abilities and legacy subtrees.</summary>
    private static IEnumerable<string> TopLevelResources(string directory) {
        using DirAccess dir = DirAccess.Open(directory);
        if (dir == null) yield break;
        foreach (string file in dir.GetFiles()) {
            if (file.EndsWith(".tres")) yield return $"{directory}/{file}";
        }
    }
}
