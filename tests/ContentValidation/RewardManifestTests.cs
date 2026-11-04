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

    /// <summary>
    /// Every visited level: Levels 1–15. Package 13 W2 (S27) retired Level 4A and
    /// its nine per-hero manifests; its row folded into Level 5.
    /// </summary>
    internal static readonly int[] LedgerLevels = {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
    };

    internal static readonly Difficulty[] Difficulties = {
        Difficulty.Easy, Difficulty.Normal, Difficulty.Hard
    };

    /// <summary>Every authored manifest's campaign level. No manifest depends on the hero.</summary>
    internal static IEnumerable<int> LedgerManifests() => LedgerLevels;

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
        foreach (int levelIndex in LedgerManifests()) {
            string label = $"level {levelIndex}";
            LevelRewardManifest manifest = Load(levelIndex);
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
        foreach (int levelIndex in LedgerManifests()) {
            string label = $"level {levelIndex}";
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
        foreach (int levelIndex in LedgerManifests()) {
            string label = $"level {levelIndex}";
            LevelRewardManifest manifest = Load(levelIndex);
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
    public void TheTitanicManifestCarriesTheFoldedRowAndItsControllersSpawnTableAndEraserDebut() {
        // Package 13 W2 (S27): the retired Level 4A row (15 / 25 / 10) folds into
        // Level 5 — 55 required / 25 boss / 30 optional. A manifest that drifts
        // from its controller's tables would allocate against enemies the level
        // never spawns and silently strand part of the pool.
        LevelRewardManifest manifest = Load((int)CampaignLevel.Titanic);
        var issues = new List<string>();
        if (manifest.RequiredEncounterPool != 55 || manifest.BossAward != 25 || manifest.OptionalPool != 30) {
            issues.Add($"row is {manifest.RequiredEncounterPool}/{manifest.BossAward}/{manifest.OptionalPool}, "
                + "the S27 Level 5 row is 55/25/30");
        }
        if (manifest.LevelID != Level05Controller.TitanicLevelID) {
            issues.Add($"manifest LevelID '{manifest.LevelID}' is not '{Level05Controller.TitanicLevelID}'");
        }

        var waves = new Dictionary<string, (string Head, string[] Enemies)>(System.StringComparer.Ordinal);
        foreach ((string head, string[] enemyIDs) in manifest.ParseWaves()) {
            waves[head.Split('@')[0]] = (head, enemyIDs);
        }
        // GU-L4 (2026-10-13): four waves, one per encounter lock (the old wave 3
        // split between the E-deck run and the boat deck); the level total is unchanged.
        for (int wave = 1; wave <= Level05Controller.WaveCount; wave++) {
            var authored = new List<string>();
            foreach ((string enemyID, int entryWave, Godot.Vector2 _) in Level05Controller.SpawnTable) {
                if (entryWave == wave) authored.Add(enemyID);
            }
            string id = Level05Controller.WaveRowID(wave);
            if (!waves.TryGetValue(id, out var row)) { issues.Add($"no '{id}' wave"); continue; }
            if (string.Join(",", row.Enemies) != string.Join(",", authored)) {
                issues.Add($"{id} is [{string.Join(",", row.Enemies)}], the controller spawns "
                    + $"[{string.Join(",", authored)}]");
            }
        }

        // The debut: one Eraser, never scaled by difficulty (@0), named for the
        // enemy the trigger ACTUALLY spawns (Package 12 W2 GAP-02 — the reward
        // lookup is keyed by the real enemy ID).
        string liveDebutEnemy = LiveEraserDebutEnemyID();
        if (!waves.TryGetValue("level_05.eraser_debut", out var debut)) {
            issues.Add("no 'level_05.eraser_debut' wave");
        } else {
            if (!debut.Head.EndsWith("@0", System.StringComparison.Ordinal)) {
                issues.Add($"the debut head '{debut.Head}' must be '@0' (never difficulty-scaled)");
            }
            if (debut.Enemies.Length != 1 || debut.Enemies[0] != liveDebutEnemy) {
                issues.Add($"debut wave is [{string.Join(",", debut.Enemies)}], but the trigger spawns "
                    + $"[{liveDebutEnemy}]");
            }
        }
        if (waves.Count != Level05Controller.WaveCount + 2) {
            issues.Add($"expected {Level05Controller.WaveCount} waves plus the debut and the swarm, found {waves.Count}");
        }

        // Optional: half to the four Extractors (15 → 4/4/4/3), half to the secret.
        LevelRewardLedger ledger = LevelRewardDirectory.Compile(manifest, Difficulty.Normal);
        int extractors = 0;
        foreach (string extractorID in manifest.ExtractorSourceIDs) extractors += ledger.Award(extractorID);
        if (manifest.ExtractorSourceIDs.Length != 4 || extractors != 15) {
            issues.Add($"{manifest.ExtractorSourceIDs.Length} extractors pay {extractors}, expected 4 paying 15");
        }
        if (ledger.Award(manifest.SecretSourceID) != 15) {
            issues.Add($"the secret pays {ledger.Award(manifest.SecretSourceID)}, expected 15");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// GU-L0 (2026-10-13): Florence's entry room became two encounter locks, so its
    /// <c>level_01.room1@5</c> row split into <c>room1a@3</c> (the streets) and
    /// <c>room1b@2</c> (the piazza), each the matching lock's table. The split keeps
    /// the room exactly: on every difficulty the two rows' spawnable sources are
    /// the old row's prefix as a multiset (Easy two slashers and a guard; Normal
    /// and Hard all five), the required pool still pays 25, and the old row is gone.
    /// </summary>
    [TestCase]
    public void FlorencesSplitEntryRowsKeepTheEntryRoomsCompositionOnEveryDifficulty() {
        LevelRewardManifest manifest = Load((int)CampaignLevel.Florence);
        string[] oldRoom = { "chrono_slasher", "chrono_slasher", "cyber_guard", "cyber_guard", "steam_automaton" };
        var issues = new List<string>();
        var rows = new Dictionary<string, string[]>(System.StringComparer.Ordinal);
        foreach ((string head, string[] enemyIDs) in manifest.ParseWaves()) rows[head] = enemyIDs;
        if (rows.ContainsKey("level_01.room1@5")) issues.Add("the old level_01.room1@5 row is still authored");
        if (!rows.TryGetValue("level_01.room1a@3", out string[] streets)) issues.Add("no level_01.room1a@3 row");
        if (!rows.TryGetValue("level_01.room1b@2", out string[] piazza)) issues.Add("no level_01.room1b@2 row");
        if (streets != null && string.Join(",", streets) != string.Join(",", Level01SpawnIDs(Level01Controller.Room1aSpawns))) {
            issues.Add($"room1a is [{string.Join(",", streets)}], the streets lock spawns [{string.Join(",", Level01SpawnIDs(Level01Controller.Room1aSpawns))}]");
        }
        if (piazza != null && string.Join(",", piazza) != string.Join(",", Level01SpawnIDs(Level01Controller.Room1bSpawns))) {
            issues.Add($"room1b is [{string.Join(",", piazza)}], the piazza lock spawns [{string.Join(",", Level01SpawnIDs(Level01Controller.Room1bSpawns))}]");
        }
        foreach (Difficulty difficulty in Difficulties) {
            LevelRewardLedger ledger = LevelRewardDirectory.Compile(manifest, difficulty);
            var split = new List<string>();
            int required = 0;
            foreach (KeyValuePair<string, IReadOnlyList<string>> queue in ledger.EnemyQueues) {
                foreach (string sourceID in queue.Value) {
                    if (sourceID.StartsWith("level_01.room1a#", System.StringComparison.Ordinal)
                        || sourceID.StartsWith("level_01.room1b#", System.StringComparison.Ordinal)) split.Add(queue.Key);
                    required += ledger.Award(sourceID);
                }
            }
            var expected = new List<string>(oldRoom[..System.Math.Min(oldRoom.Length,
                StoryDifficultyTuning.ScaleEncounterCount(oldRoom.Length, difficulty))]);
            split.Sort(System.StringComparer.Ordinal);
            expected.Sort(System.StringComparer.Ordinal);
            if (string.Join(",", split) != string.Join(",", expected)) {
                issues.Add($"{difficulty}: the split rows spawn [{string.Join(",", split)}], the entry room spawned [{string.Join(",", expected)}]");
            }
            if (required != 25) issues.Add($"{difficulty}: the required sources pay {required}, not 25");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// GU-L8 (2026-10-12): Hakata Bay's three encounter locks spawn exactly their
    /// manifest rows, and the Chrono-Warden the Lantern Watch has spawned since
    /// Package 11 A7a (it used to stand past the end of the <c>level_09.wave2</c> row,
    /// spawned on Normal and Hard but unledgered) is now that row's fifth entry. Kill
    /// and collect every source the locks plan on every difficulty — through the real
    /// issue/claim path — and the required pool pays exactly its 15, the Warden its
    /// share wherever it spawns.
    /// </summary>
    [TestCase]
    public void HakataBaysLockWavesAreItsRowsAndPayExactlyFifteenIncludingTheWarden() {
        StoryManager story = StoryManager.Instance;
        CampaignLevel originalLevel = story.CurrentLevel;
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        LevelRewardManifest manifest = Load((int)CampaignLevel.Berlin);
        var issues = new List<string>();
        var rows = new Dictionary<string, string[]>(System.StringComparer.Ordinal);
        foreach ((string head, string[] enemyIDs) in manifest.ParseWaves()) rows[head.Split('@')[0]] = enemyIDs;
        try {
            foreach (Difficulty difficulty in Difficulties) {
                var spawned = new List<string>();
                for (int wave = 1; wave <= 3; wave++) {
                    EncounterLockWave lockWave = Level09Controller.LockWaveFor(wave, EncounterWaveTrigger.OnBuild);
                    if (!rows.TryGetValue(lockWave.RowID, out string[] row)) { issues.Add($"no '{lockWave.RowID}' row"); continue; }
                    if (string.Join(",", row) != string.Join(",", lockWave.EnemyIDs)) {
                        issues.Add($"{lockWave.RowID} is [{string.Join(",", row)}], the lock spawns [{string.Join(",", lockWave.EnemyIDs)}]");
                    }
                    int count = EncounterLockRules.PlannedSpawnCount(lockWave.Spawns.Length, lockWave.AuthoredCount,
                        lockWave.Scaled, difficulty);
                    for (int index = 0; index < count; index++) spawned.Add(lockWave.EnemyIDs[index]);
                }

                story.PrepareDirectLevel(CampaignLevel.Berlin, "einstein", difficulty);
                LevelRewardDirectory.ResetAttempt();
                LevelRewardLedger ledger = LevelRewardDirectory.EnsureCompiled();
                if (ledger == null) { issues.Add($"{difficulty}: no ledger"); continue; }

                int paid = 0;
                foreach (string enemyID in spawned) {
                    if (!LevelRewardDirectory.TryIssueEnemyAward(enemyID, out string sourceID, out int amount)) {
                        issues.Add($"{difficulty}: killing '{enemyID}' issued no source");
                        continue;
                    }
                    LevelRewardDirectory.CommitClaim(sourceID);
                    paid += amount;
                    if (enemyID == Level09Controller.WardenEnemyID && amount <= 0) {
                        issues.Add($"{difficulty}: the Warden paid nothing");
                    }
                }
                if (paid != manifest.RequiredEncounterPool) {
                    issues.Add($"{difficulty}: collecting every lock member paid {paid}, expected {manifest.RequiredEncounterPool}");
                }
            }
        } finally {
            story.PrepareDirectLevel(originalLevel, originalCharacter ?? "einstein", originalDifficulty);
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            LevelRewardDirectory.ResetAttempt();
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    private static List<string> Level01SpawnIDs(
        (string EnemyID, Godot.Vector2 Position, Godot.Vector2? PatrolA, Godot.Vector2? PatrolB)[] table) {
        var ids = new List<string>();
        foreach ((string enemyID, Godot.Vector2 _, Godot.Vector2? _, Godot.Vector2? _) in table) ids.Add(enemyID);
        return ids;
    }

    /// <summary>
    /// The enemy the Level 5 Eraser debut really spawns: the default of a freshly
    /// constructed trigger, which is exactly what <c>Level05Controller</c> builds
    /// (it sets no <c>EnemyID</c> override).
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
    /// Package 12 W2 (GAP-02), moved to Level 5 by Package 13 W2. Kill and
    /// collect every required source the Titanic actually spawns on every
    /// difficulty — each wave's difficulty-scaled prefix, exactly as
    /// <c>Level05Controller.SpawnWave</c> spawns it, plus the unscaled Eraser
    /// debut — and the required pool pays exactly 55 through the real
    /// issue/claim path the kill-drop system uses.
    /// </summary>
    /// <summary>
    /// GU-L12 (gameplay update 2026-10-12): Levels 12–15's salted hunters — the
    /// Erasers and Level 14's Chrono-Warden — are encounter-lock members, so each
    /// is the tail of its wave's manifest row (F05: "elite/Eraser ambushes" are
    /// required encounters at elite weight) rather than an unledgered extra. On
    /// every difficulty, killing exactly what the lock waves plan (the per-row
    /// prefix of the controller's table) issues a source for every member and
    /// pays the level's whole required pool; Easy's prefix still fields no hunter,
    /// as the old spawn prefix never did, and Normal and Hard pay each one.
    /// </summary>
    [TestCase]
    public void TheLateLevelsPayEveryPlannedLockMemberTheirSaltedHuntersIncluded() {
        var levels = new (int Level, (string EnemyID, int Wave, Godot.Vector2 Position)[] Table)[] {
            (12, Level12Controller.SpawnTable),
            (13, Level13Controller.SpawnTable),
            (14, Level14Controller.SpawnTable),
            (15, Level15Controller.SpawnTable)
        };
        var issues = new List<string>();
        foreach ((int levelIndex, (string EnemyID, int Wave, Godot.Vector2 Position)[] table) in levels) {
            LevelRewardManifest manifest = Load(levelIndex);
            foreach (Difficulty difficulty in Difficulties) {
                string label = $"level {levelIndex} {difficulty}";
                LevelRewardLedger ledger = LevelRewardDirectory.Compile(manifest, difficulty);
                var queues = new Dictionary<string, Queue<string>>(System.StringComparer.Ordinal);
                foreach (KeyValuePair<string, IReadOnlyList<string>> entry in ledger.EnemyQueues) {
                    queues[entry.Key] = new Queue<string>(entry.Value);
                }
                var waves = new SortedDictionary<int, List<string>>();
                foreach ((string enemyID, int wave, Godot.Vector2 _) in table) {
                    if (!waves.TryGetValue(wave, out List<string> ids)) waves[wave] = ids = new List<string>();
                    ids.Add(enemyID);
                }
                int paid = 0;
                int hunters = 0;
                foreach (KeyValuePair<int, List<string>> wave in waves) {
                    int planned = EncounterLockRules.PlannedSpawnCount(wave.Value.Count, -1, true, difficulty);
                    for (int index = 0; index < planned; index++) {
                        string enemyID = wave.Value[index];
                        if (!queues.TryGetValue(enemyID, out Queue<string> queue) || queue.Count == 0) {
                            issues.Add($"{label}: killing wave {wave.Key}'s '{enemyID}' issues no source");
                            continue;
                        }
                        int amount = ledger.Award(queue.Dequeue());
                        paid += amount;
                        if (enemyID is "unbound_eraser" or "chrono_warden") {
                            hunters++;
                            if (amount <= 0) issues.Add($"{label}: the hunter '{enemyID}' pays nothing");
                        }
                    }
                }
                if (paid != manifest.RequiredEncounterPool) {
                    issues.Add($"{label}: the planned members pay {paid}, the pool is {manifest.RequiredEncounterPool}");
                }
                if (difficulty == Difficulty.Easy && hunters != 0) issues.Add($"{label}: Easy fields {hunters} hunters");
                if (difficulty != Difficulty.Easy && hunters == 0) issues.Add($"{label}: no hunter is planned");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheTitanicsRequiredPoolPaysExactlyFiftyFiveWhenEverySpawnedSourceIsCollected() {
        StoryManager story = StoryManager.Instance;
        CampaignLevel originalLevel = story.CurrentLevel;
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string debutEnemy = LiveEraserDebutEnemyID();
        var issues = new List<string>();
        try {
            foreach (Difficulty difficulty in Difficulties) {
                var spawned = new List<string>();
                for (int wave = 1; wave <= Level05Controller.WaveCount; wave++) {
                    var authored = new List<string>();
                    foreach ((string enemyID, int entryWave, Godot.Vector2 _) in Level05Controller.SpawnTable) {
                        if (entryWave == wave) authored.Add(enemyID);
                    }
                    int count = StoryDifficultyTuning.ScaleEncounterCount(authored.Count, difficulty);
                    for (int index = 0; index < count && index < authored.Count; index++) {
                        spawned.Add(authored[index]);
                    }
                }
                spawned.Add(debutEnemy);

                // Slotless, so nothing here can write a save.
                story.PrepareDirectLevel(CampaignLevel.Titanic, "einstein", difficulty);
                LevelRewardDirectory.ResetAttempt();
                LevelRewardLedger ledger = LevelRewardDirectory.EnsureCompiled();
                if (ledger == null) { issues.Add($"{difficulty}: no ledger"); continue; }

                int paid = 0;
                bool debutPaid = false;
                foreach (string enemyID in spawned) {
                    if (!LevelRewardDirectory.TryIssueEnemyAward(enemyID, out string sourceID, out int amount)) {
                        issues.Add($"{difficulty}: killing '{enemyID}' issued no source");
                        continue;
                    }
                    LevelRewardDirectory.CommitClaim(sourceID);
                    paid += amount;
                    if (sourceID.StartsWith("level_05.eraser_debut", System.StringComparison.Ordinal)) {
                        debutPaid = amount > 0;
                    }
                }
                if (paid != 55) {
                    issues.Add($"{difficulty}: collecting every required source paid {paid}, expected 55");
                }
                if (!debutPaid) issues.Add($"{difficulty}: the Eraser debut paid nothing");
            }
        } finally {
            story.PrepareDirectLevel(originalLevel, originalCharacter ?? "einstein", originalDifficulty);
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            LevelRewardDirectory.ResetAttempt();
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }
}
