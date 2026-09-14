using System.Collections.Generic;
using System.IO;
using System.Linq;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using FileAccess = Godot.FileAccess;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 4 workstream C1: the repo-wide roster sweep.
/// <para>
/// The B1-B6 suites each pin their own subset's authored numbers. This suite
/// deliberately makes no per-resource tuning assertions; it validates the
/// invariants that only hold across the <em>whole</em> roster once every
/// workstream has merged — manifest/resource agreement, cross-workstream summon
/// references, repo-wide dust conformance, translation coverage, and the
/// no-deadlock distance-band rule for every scripted boss.
/// </para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EnemyRosterContentTests {
    private const string EnemyDirectory = "res://resources/Enemies";
    private const string BossDirectory = "res://resources/Bosses";

    // docs/DUST_ECONOMY.md Section 1 reward tiers, locked by DustEconomyTests.
    private const int StandardDustCeiling = 2;
    private const int EliteDustReward = 10;
    private const int BossDustReward = 25;

    /// <summary>Design Section 6 / plan Section 3.5: the only boss without BossData attacks.</summary>
    private const string CpuDrivenBossID = "mirror_paradox";

    [TestCase]
    public void EveryManifestEnemyRowResolvesToAResourceCarryingTheSameID() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        var rows = manifest.ForCategory(ContentCategory.Enemy).ToArray();
        AssertThat(rows.Length >= 26).IsTrue();

        foreach (ContentManifestEntry entry in rows) {
            AssertThat(entry.ImplementationState).IsEqual(ContentImplementationState.Implemented);
            AssertThat(entry.AssetStatus).IsEqual(ContentAssetStatus.ReadyForReplacement);
            AssertThat(entry.ValidationState).IsEqual(ContentValidationState.Valid);
            AssertThat(entry.ResourcePath).IsEqual($"res://resources/Enemies/{entry.ContentID}.tres");

            EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>(entry.ResourcePath);
            AssertObject(data).OverrideFailureMessage(
                $"Manifest Enemy row '{entry.ContentID}' has no loadable resource at {entry.ResourcePath}.")
                .IsNotNull();
            AssertThat(data.EnemyID).OverrideFailureMessage(
                $"Manifest Enemy row '{entry.ContentID}' points at a resource whose EnemyID is '{data.EnemyID}'.")
                .IsEqual(entry.ContentID);
        }
    }

    [TestCase]
    public void EveryManifestBossRowResolvesToAResourceCarryingTheSameID() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        var rows = manifest.ForCategory(ContentCategory.Boss).ToArray();
        AssertThat(rows.Length).IsEqual(15);

        foreach (ContentManifestEntry entry in rows) {
            AssertThat(entry.ImplementationState).IsEqual(ContentImplementationState.Implemented);
            AssertThat(entry.AssetStatus).IsEqual(ContentAssetStatus.ReadyForReplacement);
            AssertThat(entry.ValidationState).IsEqual(ContentValidationState.Valid);
            AssertThat(entry.ResourcePath).IsEqual($"res://resources/Bosses/{entry.ContentID}.tres");

            BossData data = FTT.Core.AuthoredResources.Load<BossData>(entry.ResourcePath);
            AssertObject(data).OverrideFailureMessage(
                $"Manifest Boss row '{entry.ContentID}' has no loadable resource at {entry.ResourcePath}.")
                .IsNotNull();
            AssertThat(data.BossID).OverrideFailureMessage(
                $"Manifest Boss row '{entry.ContentID}' points at a resource whose BossID is '{data.BossID}'.")
                .IsEqual(entry.ContentID);
        }

        // Plan Section 3.5: the mirror runs on the deterministic Fighter CPU engine,
        // not BossController's weighted selection, and the manifest records that.
        ContentManifestEntry mirror = rows.Single(entry => entry.ContentID == CpuDrivenBossID);
        AssertThat(mirror.OwnerSystem).IsEqual("FighterCpuController");
    }

    [TestCase]
    public void EveryRosterResourceOnDiskIsRegisteredInTheManifest() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        var enemyIDs = manifest.ForCategory(ContentCategory.Enemy).Select(e => e.ContentID).ToHashSet();
        var bossIDs = manifest.ForCategory(ContentCategory.Boss).Select(e => e.ContentID).ToHashSet();

        foreach (string path in TopLevelResources(EnemyDirectory)) {
            string id = System.IO.Path.GetFileNameWithoutExtension(path);
            AssertThat(enemyIDs.Contains(id)).OverrideFailureMessage(
                $"{path} is on disk but has no Enemy row in the content manifest.").IsTrue();
        }
        foreach (string path in TopLevelResources(BossDirectory)) {
            string id = System.IO.Path.GetFileNameWithoutExtension(path);
            AssertThat(bossIDs.Contains(id)).OverrideFailureMessage(
                $"{path} is on disk but has no Boss row in the content manifest.").IsTrue();
        }
    }

    [TestCase]
    public void EveryRosterDisplayNameKeyIsAuthoredAndResolvesInEnglish() {
        HashSet<string> tableKeys = EnglishTranslationKeys();
        TranslationServer.SetLocale("en");

        int checkedKeys = 0;
        foreach ((string path, Resource resource) in AllRosterResources()) {
            string key = DisplayNameKeyOf(resource);
            if (key == null) continue; // not one of the three roster resource types
            checkedKeys++;

            AssertThat(string.IsNullOrWhiteSpace(key)).OverrideFailureMessage(
                $"{path} has an empty DisplayNameKey.").IsFalse();
            AssertThat(tableKeys.Contains(key)).OverrideFailureMessage(
                $"{path} uses DisplayNameKey '{key}', which is not in localization/en.csv.").IsTrue();
            // The compiled en.en.translation must be in step with the CSV, otherwise
            // Node.Tr() shows the raw key at runtime (see AGENTS.md localization notes).
            AssertThat(TranslationServer.Translate(key).ToString()).OverrideFailureMessage(
                $"{path} uses DisplayNameKey '{key}', which does not resolve through " +
                "localization/en.en.translation. Re-import localization/en.csv.")
                .IsNotEqual(key);
        }

        // 28 enemies + 30 enemy abilities + 15 bosses + 49 boss abilities. V7.6
        // (A7a) added the Eraser and its three kits, so the floor rises by four.
        AssertThat(checkedKeys).OverrideFailureMessage(
            $"Only {checkedKeys} roster resources were reached; the directory walk is broken.")
            .IsGreaterEqual(122);
    }

    [TestCase]
    public void EverySummonAbilityNamesAnEnemyResourceThatExists() {
        // Closes the deferred B4/B5 cross-reference checks: those suites could only
        // assert the ID strings because the summoned enemies lived in other worktrees.
        int summonAbilities = 0;
        foreach ((string path, Resource resource) in AllRosterResources()) {
            if (resource is not EnemyAbilityData ability) continue;
            if (ability.Archetype != EnemyAbilityArchetype.SummonMinions) continue;

            summonAbilities++;
            AssertThat(string.IsNullOrWhiteSpace(ability.SummonEnemyID)).OverrideFailureMessage(
                $"{path} is a SummonMinions ability with no SummonEnemyID.").IsFalse();
            AssertThat(ability.SummonCount >= 1).IsTrue();

            string summonPath = $"res://resources/Enemies/{ability.SummonEnemyID}.tres";
            AssertThat(FileAccess.FileExists(summonPath)).OverrideFailureMessage(
                $"{path} summons '{ability.SummonEnemyID}' but {summonPath} does not exist.").IsTrue();

            EnemyData summoned = FTT.Core.AuthoredResources.Load<EnemyData>(summonPath);
            AssertObject(summoned).IsNotNull();
            AssertThat(summoned.EnemyID).IsEqual(ability.SummonEnemyID);
            // Boss summons spawn through the standard/elite enemy pools; a summoned
            // boss would have no pool and no controller.
            AssertThat(summoned.Tier != EnemyTier.Boss).IsTrue();
        }

        // revolutionary_tribunal x2, tragedy_king, archive_prime, apex_eraser.
        AssertThat(summonAbilities).IsEqual(5);
    }

    [TestCase]
    public void TierDustRewardsConformToTheEconomyAcrossTheWholeRoster() {
        // docs/DUST_ECONOMY.md Section 1: standards 1-2, elites exactly 10, bosses 50.
        int standards = 0, elites = 0;
        foreach (string path in TopLevelResources(EnemyDirectory)) {
            EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>(path);
            AssertObject(data).IsNotNull();
            switch (data.Tier) {
                case EnemyTier.Standard:
                    standards++;
                    AssertThat(data.ChronalDustDrop >= 1 && data.ChronalDustDrop <= StandardDustCeiling)
                        .OverrideFailureMessage($"{path} drops {data.ChronalDustDrop} dust; standards are 1-{StandardDustCeiling}.")
                        .IsTrue();
                    break;
                case EnemyTier.Elite:
                    elites++;
                    AssertThat(data.ChronalDustDrop)
                        .OverrideFailureMessage($"{path} drops {data.ChronalDustDrop} dust; elites are exactly {EliteDustReward}.")
                        .IsEqual(EliteDustReward);
                    AssertThat(data.HasEliteAbilities).OverrideFailureMessage(
                        $"{path} is Elite but authors no EliteAbilities.").IsTrue();
                    AssertThat(data.StunResistance > 0f).IsTrue();
                    break;
                default:
                    AssertThat(false).OverrideFailureMessage(
                        $"{path} uses EnemyTier.Boss; bosses live in resources/Bosses as BossData.").IsTrue();
                    break;
            }
        }

        int bosses = 0;
        foreach (string path in TopLevelResources(BossDirectory)) {
            BossData data = FTT.Core.AuthoredResources.Load<BossData>(path);
            AssertObject(data).IsNotNull();
            bosses++;
            AssertThat(data.ChronalDustDrop)
                .OverrideFailureMessage($"{path} drops {data.ChronalDustDrop} dust; bosses are exactly {BossDustReward}.")
                .IsEqual(BossDustReward);
        }

        // 27 originals, plus the V7.1 Chrono-Warden elite, plus the V7.6 Eraser.
        AssertThat(standards + elites).IsEqual(29);
        AssertThat(bosses).IsEqual(15);
    }

    [TestCase]
    public void EveryScriptedBossCoversBothDistanceBands() {
        // Plan Section 3.4 no-deadlock rule: BossController filters BossAbilities by
        // RangeClass against the player distance, so a kit missing a band relies on
        // the fallback every single time it crosses that threshold.
        int scripted = 0;
        foreach (string path in TopLevelResources(BossDirectory)) {
            BossData data = FTT.Core.AuthoredResources.Load<BossData>(path);
            if (data.BossID == CpuDrivenBossID) {
                AssertThat(data.BossAbilities == null || data.BossAbilities.Length == 0).OverrideFailureMessage(
                    "mirror_paradox must stay on the Fighter CPU engine with no BossData attacks.").IsTrue();
                continue;
            }

            scripted++;
            AssertThat(data.BossAbilities is { Length: > 0 }).OverrideFailureMessage(
                $"{path} authors no BossAbilities.").IsTrue();

            bool melee = false, ranged = false;
            foreach (EnemyAbilityData ability in data.BossAbilities) {
                AssertObject(ability).IsNotNull();
                AssertThat(ability.HasValidArchetypeFields()).OverrideFailureMessage(
                    $"{path} -> '{ability.AbilityID}' fails archetype field validation.").IsTrue();
                melee |= ability.RangeClass is EnemyAbilityRangeClass.Melee;
                ranged |= ability.RangeClass is EnemyAbilityRangeClass.Ranged;
            }

            AssertThat(melee).OverrideFailureMessage(
                $"{path} has no Melee-class ability; the close band would always hit the fallback.").IsTrue();
            AssertThat(ranged).OverrideFailureMessage(
                $"{path} has no Ranged-class ability; the far band would always hit the fallback.").IsTrue();
        }

        AssertThat(scripted).IsEqual(14);
    }

    [TestCase]
    public void EnglishTranslationTableHasNoDuplicateKeys() {
        // A duplicated key silently shadows one of the two entries after import.
        var seen = new HashSet<string>();
        var duplicates = new List<string>();
        int rows = 0;
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            if (line.Length == 0) continue;
            int comma = line.IndexOf(',');
            if (comma <= 0) continue;
            string key = line[..comma];
            rows++;
            if (!seen.Add(key)) duplicates.Add(key);
        }

        AssertThat(rows).OverrideFailureMessage(
            $"Only {rows} rows were read from localization/en.csv.").IsGreater(500);
        AssertThat(duplicates.Count).OverrideFailureMessage(
            "localization/en.csv has duplicate keys: " + string.Join(", ", duplicates)).IsEqual(0);
    }

    private static HashSet<string> EnglishTranslationKeys() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        return keys;
    }

    private static string DisplayNameKeyOf(Resource resource) => resource switch {
        EnemyData enemy => enemy.DisplayNameKey,
        BossData boss => boss.DisplayNameKey,
        EnemyAbilityData ability => ability.DisplayNameKey,
        _ => null
    };

    /// <summary>Non-recursive: the roster data resources, excluding the Abilities subtree.</summary>
    private static IEnumerable<string> TopLevelResources(string directory) {
        using DirAccess dir = DirAccess.Open(directory);
        if (dir == null) yield break;
        foreach (string file in dir.GetFiles()) {
            if (file.EndsWith(".tres")) yield return $"{directory}/{file}";
        }
    }

    /// <summary>Every enemy/boss data and ability resource, recursively.</summary>
    private static IEnumerable<(string Path, Resource Resource)> AllRosterResources() {
        foreach (string path in Walk(EnemyDirectory)) yield return (path, ResourceLoader.Load<Resource>(path));
        foreach (string path in Walk(BossDirectory)) yield return (path, ResourceLoader.Load<Resource>(path));
    }

    private static IEnumerable<string> Walk(string directory) {
        using DirAccess dir = DirAccess.Open(directory);
        if (dir == null) yield break;
        foreach (string subdirectory in dir.GetDirectories()) {
            foreach (string nested in Walk($"{directory}/{subdirectory}")) yield return nested;
        }
        foreach (string file in dir.GetFiles()) {
            if (file.EndsWith(".tres")) yield return $"{directory}/{file}";
        }
    }
}
