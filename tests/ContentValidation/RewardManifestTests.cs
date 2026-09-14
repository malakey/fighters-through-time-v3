using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 11 A10 — the authored F05 reward manifests
/// (resources/Content/reward_manifests/). These prove the shipped inventory
/// against docs/design-contracts/DUST_ECONOMY.md: every level's finite sources
/// sum exactly to its budget row, on every difficulty; every source ID is stable,
/// unique and in exactly one budget category; and every authored enemy ID
/// resolves to a real roster resource so its allocation weight is real.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RewardManifestTests {

    /// <summary>Every visited level: the sixteen campaign rows plus Level 4A (index 16).</summary>
    internal static readonly int[] LedgerLevels = {
        1, 2, 3, 4, 16, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
    };

    internal static readonly Difficulty[] Difficulties = {
        Difficulty.Easy, Difficulty.Normal, Difficulty.Hard
    };

    internal static LevelRewardManifest Load(int levelIndex) {
        string path = LevelRewardManifest.PathFor(levelIndex);
        AssertThat(ResourceLoader.Exists(path))
            .OverrideFailureMessage($"Missing reward manifest '{path}'.")
            .IsTrue();
        LevelRewardManifest manifest = AuthoredResources.Load<LevelRewardManifest>(path);
        AssertObject(manifest).IsNotNull();
        return manifest;
    }

    [TestCase]
    public void EveryLevelsAuthoredSourcesSumExactlyToItsBudgetRowOnAllThreeDifficulties() {
        var issues = new List<string>();
        foreach (int levelIndex in LedgerLevels) {
            LevelRewardManifest manifest = Load(levelIndex);
            foreach (Difficulty difficulty in Difficulties) {
                LevelRewardLedger ledger = LevelRewardDirectory.Compile(manifest, difficulty);
                if (ledger == null) {
                    issues.Add($"level {levelIndex}: no ledger compiled");
                    continue;
                }

                int required = 0;
                int boss = 0;
                int optional = 0;
                var extractorIDs = new HashSet<string>(
                    manifest.ExtractorSourceIDs ?? System.Array.Empty<string>(), System.StringComparer.Ordinal);
                string secretID = manifest.SecretSourceID ?? "";
                foreach (KeyValuePair<string, int> entry in ledger.Awards) {
                    if (entry.Key == manifest.BossSourceID) boss += entry.Value;
                    else if (extractorIDs.Contains(entry.Key) || entry.Key == secretID) optional += entry.Value;
                    else required += entry.Value;
                }

                if (required != manifest.RequiredEncounterPool) {
                    issues.Add(
                        $"level {levelIndex} {difficulty}: required sources pay {required}, budget row is {manifest.RequiredEncounterPool}");
                }
                if (boss != manifest.BossAward) {
                    issues.Add($"level {levelIndex} {difficulty}: boss pays {boss}, row is {manifest.BossAward}");
                }
                if (optional != manifest.OptionalPool) {
                    issues.Add(
                        $"level {levelIndex} {difficulty}: optional sources pay {optional}, row is {manifest.OptionalPool}");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EverySourceIDIsStableUniqueAndCarriesExactlyOneBudgetCategory() {
        var issues = new List<string>();
        var seenAcrossCampaign = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (int levelIndex in LedgerLevels) {
            LevelRewardManifest manifest = Load(levelIndex);
            var categories = new Dictionary<string, string>(System.StringComparer.Ordinal);

            void Claim(string sourceID, string category) {
                if (string.IsNullOrWhiteSpace(sourceID)) return;
                if (categories.TryGetValue(sourceID, out string existing) && existing != category) {
                    // F05 §7 allows exactly one overlap: an Extractor that is
                    // ALSO the designated secret owns the sum of both shares,
                    // paid once as one pickup.
                    bool allowedOverlap =
                        (existing == "extractor" && category == "secret")
                        || (existing == "secret" && category == "extractor");
                    if (!allowedOverlap) {
                        issues.Add($"level {levelIndex}: '{sourceID}' is both {existing} and {category}");
                    }
                    return;
                }
                categories[sourceID] = category;
            }

            foreach ((string waveHead, string[] enemyIDs) in manifest.ParseWaves()) {
                string waveID = waveHead.Split('@')[0];
                for (int index = 0; index < enemyIDs.Length; index++) {
                    string sourceID = $"{waveID}#{index}";
                    if (!categories.TryAdd(sourceID, "required")) {
                        issues.Add($"level {levelIndex}: duplicate required source '{sourceID}'");
                    }
                }
            }
            foreach ((string sourceID, int _) in manifest.ParseScriptedSources()) Claim(sourceID, "required");
            Claim(manifest.BossSourceID, "boss");
            foreach (string sourceID in manifest.ExtractorSourceIDs ?? System.Array.Empty<string>()) {
                Claim(sourceID, "extractor");
            }
            Claim(manifest.SecretSourceID, "secret");

            foreach (string sourceID in categories.Keys) {
                if (!seenAcrossCampaign.Add(sourceID)) {
                    issues.Add($"source ID '{sourceID}' is reused by more than one level");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryAuthoredEnemyIDResolvesToARosterResourceSoItsWeightIsReal() {
        // Allocation weights come from EnemyData.Tier, never from a second
        // authored number. An enemy ID that does not resolve would silently
        // allocate as a standard and quietly skew the level's distribution.
        var issues = new List<string>();
        foreach (int levelIndex in LedgerLevels) {
            LevelRewardManifest manifest = Load(levelIndex);
            foreach ((string _, string[] enemyIDs) in manifest.ParseWaves()) {
                foreach (string enemyID in enemyIDs) {
                    EnemyData data = null;
                    try {
                        data = EnemyFactory.LoadData(enemyID);
                    } catch (System.Exception exception) {
                        issues.Add($"level {levelIndex}: '{enemyID}' -> {exception.GetType().Name}");
                    }
                    if (data == null) issues.Add($"level {levelIndex}: '{enemyID}' resolved null");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }
}
