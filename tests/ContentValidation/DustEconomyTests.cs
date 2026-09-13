using System.Collections.Generic;
using System.Linq;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Economy invariants for the Package 3 Chronal Dust balance pass. Every number
/// asserted here is derived in docs/DUST_ECONOMY.md — retune the resources, the
/// document, and this suite together.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DustEconomyTests {
    private static readonly string[] CharacterIDs = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "pocahontas"
    };

    private static readonly string[] StandardEnemyIDs = { "chrono_slasher", "cyber_guard", "hologram_drone" };
    private static readonly string[] EliteEnemyIDs = { "steam_automaton", "tech_enforcer" };

    // docs/DUST_ECONOMY.md Section 1: authored grid cost curve, shared by all nine grids.
    private const int TierOneCost = 50;
    private const int TierTwoCost = 75;
    private const int MajorCost = 200;
    private const int FullGridCost = 3 * TierOneCost + 3 * TierTwoCost + 3 * MajorCost; // 975

    // docs/DUST_ECONOMY.md Section 1: authored reward tiers.
    private const int EliteDustReward = 10;
    private const int BossDustReward = 50;
    private const int ExtractorDustReward = 15;

    [TestCase]
    public void AllNineGridsShareTheDocumentedCostCurve() {
        // docs/DUST_ECONOMY.md Section 1: 3x50 + 3x75 + 3x200 = 975 on every grid,
        // competitively normalized — character identity comes from effects, not price.
        foreach (string characterID in CharacterIDs) {
            ResonanceGridData grid = FTT.Core.AuthoredResources.Load<ResonanceGridData>(
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
                    // prerequisites" inference in four grids (Joan, Leonardo,
                    // Tesla, Lincoln all author a Tier 1 node behind a
                    // prerequisite), so the curve is checked against the
                    // explicit Tier field the node now carries.
                    AssertThat(node.UnlockCost).IsEqual(
                        node.Tier == 1 ? TierOneCost : TierTwoCost);
                }
            }

            // Majors always cost more than every minor node.
            AssertThat(majorCosts.Count).IsEqual(3);
            AssertThat(minorCosts.Count).IsEqual(6);
            AssertThat(majorCosts.Min() > minorCosts.Max()).IsTrue();

            // docs/DUST_ECONOMY.md Section 1: full 9-node grid costs exactly 975.
            AssertThat(total).IsEqual(FullGridCost);
        }
    }

    [TestCase]
    public void EnemyBossAndExtractorRewardsMatchTheDocumentedTiers() {
        // docs/DUST_ECONOMY.md Section 1: standards 1-2, elites 10, boss 50, extractor 15.
        foreach (string enemyID in StandardEnemyIDs) {
            EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertThat(data.Tier).IsEqual(EnemyTier.Standard);
            AssertThat(data.ChronalDustDrop >= 1 && data.ChronalDustDrop <= 2).IsTrue();
        }
        foreach (string enemyID in EliteEnemyIDs) {
            EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertThat(data.Tier).IsEqual(EnemyTier.Elite);
            AssertThat(data.ChronalDustDrop).IsEqual(EliteDustReward);
        }

        BossData boss = FTT.Core.AuthoredResources.Load<BossData>("res://resources/Bosses/borgia_inquisitor.tres");
        AssertThat(boss.ChronalDustDrop).IsEqual(BossDustReward);

        PackedScene extractorScene = ResourceLoader.Load<PackedScene>(
            "res://scenes/templates/ChronalExtractorTemplate.tscn");
        var extractor = extractorScene.Instantiate<ChronalExtractor>();
        try {
            AssertThat(extractor.DustReward).IsEqual(ExtractorDustReward);
        } finally {
            extractor.Free();
        }
    }

    [TestCase]
    public void DustRewardsLandInTheirDesignedVisualSpriteTiers() {
        // docs/DUST_ECONOMY.md Section 5: standards Small (<6), elites and
        // extractors Medium (6-24), bosses Large (25+).
        DustVisualTierSet tiers = FTT.Core.AuthoredResources.Load<DustVisualTierSet>(
            "res://resources/Drops/dust_visual_tiers.tres");
        AssertObject(tiers).IsNotNull();

        foreach (string enemyID in StandardEnemyIDs) {
            EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertThat(data.ChronalDustDrop < tiers.MediumThreshold).IsTrue();
        }
        foreach (string enemyID in EliteEnemyIDs) {
            EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertThat(data.ChronalDustDrop >= tiers.MediumThreshold).IsTrue();
            AssertThat(data.ChronalDustDrop < tiers.LargeThreshold).IsTrue();
        }
        AssertThat(ExtractorDustReward >= tiers.MediumThreshold).IsTrue();
        AssertThat(ExtractorDustReward < tiers.LargeThreshold).IsTrue();

        BossData boss = FTT.Core.AuthoredResources.Load<BossData>("res://resources/Bosses/borgia_inquisitor.tres");
        AssertThat(boss.ChronalDustDrop >= tiers.LargeThreshold).IsTrue();
    }

    [TestCase]
    public void FlorenceAuthoredKillRewardsMatchTheDocumentedLevelOneBudget() {
        // docs/DUST_ECONOMY.md Section 2, level 1 row: the authored Florence waves
        // (7 chrono_slasher + 6 cyber_guard + 3 steam_automaton, Level01Controller)
        // plus the boss yield 106 dust; the remaining 45 of the 151 full-collection
        // budget is the three not-yet-authored extractors (flagged gap).
        EnemyData slasher = FTT.Core.AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_slasher.tres");
        EnemyData guard = FTT.Core.AuthoredResources.Load<EnemyData>("res://resources/Enemies/cyber_guard.tres");
        EnemyData automaton = FTT.Core.AuthoredResources.Load<EnemyData>("res://resources/Enemies/steam_automaton.tres");
        BossData boss = FTT.Core.AuthoredResources.Load<BossData>("res://resources/Bosses/borgia_inquisitor.tres");

        int killBudget = 7 * slasher.ChronalDustDrop
            + 6 * guard.ChronalDustDrop
            + 3 * automaton.ChronalDustDrop
            + boss.ChronalDustDrop;
        AssertThat(killBudget).IsEqual(106);
        AssertThat(killBudget + 3 * ExtractorDustReward).IsEqual(151);
    }

    [TestCase]
    public void CampaignModelFundsTheGridOnTheDocumentedSchedule() {
        // docs/DUST_ECONOMY.md Section 2: placeholder per-level counts
        // { standards, elites, bosses, extractors } for levels 0-15 (Normal).
        int[][] levels = {
            new[] { 4, 0, 0, 0 },   // 0 Tutorial
            new[] { 13, 3, 1, 3 },  // 1 Florence (mob counts authored in Level01Controller)
            new[] { 8, 0, 1, 3 },   // 2 Orleans
            new[] { 8, 0, 1, 3 },   // 3 Chicago
            new[] { 10, 0, 1, 3 },  // 4 Paris
            new[] { 12, 1, 1, 4 },  // 5 Titanic (Act I finale)
            new[] { 10, 0, 1, 3 },  // 6 Pompeii
            new[] { 10, 1, 1, 3 },  // 7 Nassau
            new[] { 10, 0, 1, 3 },  // 8 Alexandria 30 BC
            new[] { 12, 1, 1, 3 },  // 9 Berlin
            new[] { 10, 0, 1, 3 },  // 10 London
            new[] { 12, 1, 1, 3 },  // 11 Gettysburg
            new[] { 14, 2, 1, 4 },  // 12 Lunar (Act II finale)
            new[] { 8, 1, 0, 2 },   // 13 Chronal Void
            new[] { 14, 2, 1, 3 },  // 14 Neo-Earth
            new[] { 12, 2, 1, 3 },  // 15 Library of Alexandria
        };

        // Reward tiers come from the authored resources so a resource retune
        // re-derives the campaign totals instead of silently diverging.
        EnemyData elite = FTT.Core.AuthoredResources.Load<EnemyData>("res://resources/Enemies/steam_automaton.tres");
        BossData boss = FTT.Core.AuthoredResources.Load<BossData>("res://resources/Bosses/borgia_inquisitor.tres");
        const float standardAverage = 1.5f; // midpoint of the designed 1-2 range

        float fullTotal = 0f;
        float cumulative = 0f;
        float cumulativeThroughActOne = 0f;
        float cumulativeThroughLevelTen = 0f;
        for (int level = 0; level < levels.Length; level++) {
            int[] counts = levels[level];
            // Florence's authored standards drop exactly 26 (7x2 + 6x2), not 13x1.5.
            float standards = level == 1 ? 26f : counts[0] * standardAverage;
            cumulative += standards
                + counts[1] * elite.ChronalDustDrop
                + counts[2] * boss.ChronalDustDrop
                + counts[3] * ExtractorDustReward;
            if (level == 5) cumulativeThroughActOne = cumulative;
            if (level == 10) cumulativeThroughLevelTen = cumulative;
        }
        fullTotal = cumulative;

        // docs/DUST_ECONOMY.md Section 2: full-collection Normal total is 1,787,
        // inside the design's preliminary 1,200-1,800 estimate (+-2 rounding slack).
        AssertThat(Mathf.RoundToInt(fullTotal)).IsEqual(1787);
        AssertThat(fullTotal >= 1200f && fullTotal <= 1800f).IsTrue();

        // docs/DUST_ECONOMY.md Section 3: full collection funds the whole grid with
        // the documented surplus, and a branch major (325) is affordable by the Act I
        // finale while the full grid (975) is not yet affordable at level 10 under
        // expected collection (90% standards, 50% extractor discovery).
        AssertThat(fullTotal >= 975f).IsTrue();
        AssertThat(cumulativeThroughActOne >= 325f).IsTrue();
        float expectedThroughLevelTen = ExpectedCollection(levels, 10, elite, boss);
        AssertThat(expectedThroughLevelTen < 975f).IsTrue();
        float expectedCampaign = ExpectedCollection(levels, 15, elite, boss);
        AssertThat(expectedCampaign >= 975f).IsTrue();
        // docs/DUST_ECONOMY.md Section 2: expected Normal playthrough is ~1,416.
        AssertThat(Mathf.Abs(expectedCampaign - 1416f) <= 5f).IsTrue();
    }

    private static float ExpectedCollection(int[][] levels, int throughLevel, EnemyData elite, BossData boss) {
        // docs/DUST_ECONOMY.md Section 2 expected model: 90% standard clears,
        // all elites and bosses, 50% extractor discovery.
        float total = 0f;
        for (int level = 0; level <= throughLevel && level < levels.Length; level++) {
            int[] counts = levels[level];
            float standards = level == 1 ? 26f : counts[0] * 1.5f;
            total += 0.9f * standards
                + counts[1] * elite.ChronalDustDrop
                + counts[2] * boss.ChronalDustDrop
                + counts[3] * (0.5f * ExtractorDustReward);
        }
        return total;
    }
}
