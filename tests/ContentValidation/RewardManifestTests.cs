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

    /// <summary>
    /// The roster, from <c>content_manifest.csv</c> — never a literal list, so a
    /// tenth character demands a tenth 4A manifest on its own. Mirrors
    /// <c>Level04AVariantCoverageTests</c>.
    /// </summary>
    internal static IReadOnlyList<string> RosterHeroes() {
        ContentManifest contentManifest = ContentManifest.LoadDefault();
        AssertObject(contentManifest).IsNotNull();
        var heroes = new List<string>();
        foreach (ContentManifestEntry entry in contentManifest.ForCategory(ContentCategory.Character)) {
            if (!string.IsNullOrWhiteSpace(entry?.ContentID)) heroes.Add(entry.ContentID);
        }
        AssertThat(heroes.Count > 0)
            .OverrideFailureMessage("The manifest lists no Character rows; the ledger sweep would pass vacuously.")
            .IsTrue();
        return heroes;
    }

    /// <summary>
    /// Every authored manifest as (campaign level, hero). Level 4A expands to one
    /// entry per roster hero: the nine Legacy Levels share campaign index 16 and
    /// each authors its own approach inventory, so each ships its own manifest.
    /// </summary>
    internal static IEnumerable<(int Level, string Hero)> LedgerManifests() {
        foreach (int levelIndex in LedgerLevels) {
            if (levelIndex != LevelRewardManifest.LegacyLevelIndex) {
                yield return (levelIndex, "");
                continue;
            }
            foreach (string hero in RosterHeroes()) yield return (levelIndex, hero);
        }
    }

    internal static LevelRewardManifest Load(int levelIndex, string hero = "") {
        string path = LevelRewardManifest.PathFor(levelIndex, hero);
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
        foreach ((int levelIndex, string hero) in LedgerManifests()) {
            string label = hero.Length == 0 ? $"level {levelIndex}" : $"level 4A/{hero}";
            LevelRewardManifest manifest = Load(levelIndex, hero);
            foreach (Difficulty difficulty in Difficulties) {
                LevelRewardLedger ledger = LevelRewardDirectory.Compile(manifest, difficulty);
                if (ledger == null) {
                    issues.Add($"{label}: no ledger compiled");
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
                        $"{label} {difficulty}: required sources pay {required}, budget row is {manifest.RequiredEncounterPool}");
                }
                if (boss != manifest.BossAward) {
                    issues.Add($"{label} {difficulty}: boss pays {boss}, row is {manifest.BossAward}");
                }
                if (optional != manifest.OptionalPool) {
                    issues.Add(
                        $"{label} {difficulty}: optional sources pay {optional}, row is {manifest.OptionalPool}");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EverySourceIDIsStableUniqueAndCarriesExactlyOneBudgetCategory() {
        var issues = new List<string>();
        var seenAcrossCampaign = new HashSet<string>(System.StringComparer.Ordinal);
        foreach ((int levelIndex, string hero) in LedgerManifests()) {
            string label = hero.Length == 0 ? $"level {levelIndex}" : $"level 4A/{hero}";
            LevelRewardManifest manifest = Load(levelIndex, hero);
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
                        issues.Add($"{label}: '{sourceID}' is both {existing} and {category}");
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
                        issues.Add($"{label}: duplicate required source '{sourceID}'");
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
        foreach ((int levelIndex, string hero) in LedgerManifests()) {
            string label = hero.Length == 0 ? $"level {levelIndex}" : $"level 4A/{hero}";
            LevelRewardManifest manifest = Load(levelIndex, hero);
            foreach ((string _, string[] enemyIDs) in manifest.ParseWaves()) {
                foreach (string enemyID in enemyIDs) {
                    EnemyData data = null;
                    try {
                        data = EnemyFactory.LoadData(enemyID);
                    } catch (System.Exception exception) {
                        issues.Add($"{label}: '{enemyID}' -> {exception.GetType().Name}");
                    }
                    if (data == null) issues.Add($"{label}: '{enemyID}' resolved null");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryRosterHerosLevel4AManifestCarriesTheLockedRowAndItsOwnAuthoredApproachTable() {
        // Level 4A is the one campaign slot whose manifest is per hero. The
        // budget ROW is identical for all nine — 15 / 25 / 10 regardless of
        // layout or enemy count — but each variant authors its own approach
        // inventory, so each needs its own source list. A manifest that drifts
        // from its controller's table would allocate against enemies the level
        // never spawns and silently strand part of the pool.
        var issues = new List<string>();
        foreach (string hero in RosterHeroes()) {
            LevelRewardManifest manifest = Load(LevelRewardManifest.LegacyLevelIndex, hero);

            if (manifest.RequiredEncounterPool != LegacyLevelControllerBase.RequiredEncounterDust
                || manifest.BossAward != LegacyLevelControllerBase.BossDust
                || manifest.OptionalPool != LegacyLevelControllerBase.OptionalDust) {
                issues.Add($"{hero}: row is {manifest.RequiredEncounterPool}/{manifest.BossAward}/"
                    + $"{manifest.OptionalPool}, the locked 4A row is "
                    + $"{LegacyLevelControllerBase.RequiredEncounterDust}/"
                    + $"{LegacyLevelControllerBase.BossDust}/{LegacyLevelControllerBase.OptionalDust}");
            }
            if (manifest.LevelID != StoryManager.LegacyLevelID(hero)) {
                issues.Add($"{hero}: manifest LevelID '{manifest.LevelID}' is not "
                    + $"'{StoryManager.LegacyLevelID(hero)}'");
            }

            System.Type controller = typeof(LegacyLevelControllerBase).Assembly.GetType(
                $"FTT.Environment.Level04A{char.ToUpperInvariant(hero[0])}{hero.Substring(1)}Controller");
            if (controller == null) { issues.Add($"{hero}: no Level04A controller type"); continue; }
            System.Reflection.FieldInfo spawnField = controller.GetField(
                "ApproachSpawns",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (spawnField?.GetValue(null) is not System.Array spawns) {
                issues.Add($"{hero}: controller has no static ApproachSpawns table");
                continue;
            }
            var authored = new List<string>();
            foreach (object entry in spawns) {
                authored.Add((string)((System.Runtime.CompilerServices.ITuple)entry)[0]);
            }

            var waves = manifest.ParseWaves();
            string approachHead = $"{StoryManager.LegacyLevelID(hero)}.approach";
            string debutHead = $"{StoryManager.LegacyLevelID(hero)}.eraser_debut";
            string[] approach = null;
            string[] debut = null;
            foreach ((string head, string[] enemyIDs) in waves) {
                string id = head.Split('@')[0];
                if (id == approachHead) approach = enemyIDs;
                else if (id == debutHead) debut = enemyIDs;
            }

            if (approach == null) { issues.Add($"{hero}: no '{approachHead}' wave"); }
            else if (string.Join(",", approach) != string.Join(",", authored)) {
                issues.Add($"{hero}: approach wave is [{string.Join(",", approach)}], "
                    + $"the controller spawns [{string.Join(",", authored)}]");
            }

            // The debut is 4A's only elite. Package 12 W2 (GAP-02): the manifest
            // must name the enemy the trigger ACTUALLY spawns, read from a live
            // trigger rather than a constant — comparing against the retired
            // placeholder is exactly how nine manifests shipped bound to
            // chrono_guard_elite while the debut spawned unbound_eraser, so the
            // reward lookup (keyed by the real enemy ID) could never pay it.
            string liveDebutEnemy = LiveEraserDebutEnemyID();
            if (debut == null) { issues.Add($"{hero}: no '{debutHead}' wave"); }
            else if (debut.Length != 1 || debut[0] != liveDebutEnemy) {
                issues.Add($"{hero}: debut wave is [{string.Join(",", debut)}], but the trigger spawns "
                    + $"[{liveDebutEnemy}]");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// The enemy a Level 4A Eraser debut really spawns: the default of a freshly
    /// constructed trigger, which is exactly what <c>LegacyLevelControllerBase</c>
    /// builds (it sets no <c>EnemyID</c> override).
    /// </summary>
    internal static string LiveEraserDebutEnemyID() {
        var trigger = new EraserDebutTrigger();
        try {
            return trigger.EnemyID;
        } finally {
            trigger.Free();
        }
    }

    /// <summary>
    /// Package 12 W2 (GAP-02). The acceptance the gap names: kill and collect
    /// every required source in every Level 4A variant on every difficulty and
    /// the required pool pays exactly 15 — through the real issue/claim path the
    /// kill-drop system uses, keyed by the enemy IDs the level actually spawns
    /// (the controller's approach table plus the live debut enemy). No pool is
    /// enlarged to make it add up.
    /// </summary>
    [TestCase]
    public void EveryLevel4AVariantsRequiredPoolPaysExactlyFifteenWhenEverySpawnedSourceIsCollected() {
        StoryManager story = StoryManager.Instance;
        CampaignLevel originalLevel = story.CurrentLevel;
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string debutEnemy = LiveEraserDebutEnemyID();
        var issues = new List<string>();
        try {
            foreach (string hero in RosterHeroes()) {
                System.Type controller = typeof(LegacyLevelControllerBase).Assembly.GetType(
                    $"FTT.Environment.Level04A{char.ToUpperInvariant(hero[0])}{hero.Substring(1)}Controller");
                var spawned = new List<string>();
                if (controller?.GetField("ApproachSpawns",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                        ?.GetValue(null) is System.Array spawns) {
                    foreach (object entry in spawns) {
                        spawned.Add((string)((System.Runtime.CompilerServices.ITuple)entry)[0]);
                    }
                } else {
                    issues.Add($"{hero}: no static ApproachSpawns table");
                    continue;
                }
                spawned.Add(debutEnemy);

                foreach (Difficulty difficulty in Difficulties) {
                    // Slotless, so nothing here can write a save.
                    story.PrepareDirectLevel(CampaignLevel.LegacyNexus, hero, difficulty);
                    LevelRewardDirectory.ResetAttempt();
                    LevelRewardLedger ledger = LevelRewardDirectory.EnsureCompiled();
                    if (ledger == null) { issues.Add($"{hero} {difficulty}: no ledger"); continue; }

                    int paid = 0;
                    foreach (string enemyID in spawned) {
                        if (!LevelRewardDirectory.TryIssueEnemyAward(enemyID, out string sourceID, out int amount)) {
                            issues.Add($"{hero} {difficulty}: killing '{enemyID}' issued no source");
                            continue;
                        }
                        LevelRewardDirectory.CommitClaim(sourceID);
                        paid += amount;
                    }
                    if (paid != LegacyLevelControllerBase.RequiredEncounterDust) {
                        issues.Add($"{hero} {difficulty}: collecting every required source paid {paid}, "
                            + $"expected {LegacyLevelControllerBase.RequiredEncounterDust}");
                    }
                }
            }
        } finally {
            story.PrepareDirectLevel(originalLevel, originalCharacter ?? "einstein", originalDifficulty);
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            LevelRewardDirectory.ResetAttempt();
        }
        AssertThat(LegacyLevelControllerBase.RequiredEncounterDust).IsEqual(15);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }
}
