using Godot;
using System;
using System.Collections.Generic;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// One level's compiled F05 reward ledger: every finite source's stable ID
    /// mapped to the dust it pays, for one difficulty. Immutable once built.
    /// </summary>
    public sealed class LevelRewardLedger {
        public string LevelID { get; init; } = "";
        public int CampaignLevelIndex { get; init; } = -1;
        public Difficulty Difficulty { get; init; }

        /// <summary>Stable source ID -> authored dust. Zero entries are legal and pay nothing.</summary>
        public IReadOnlyDictionary<string, int> Awards { get; init; } =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Enemy ID -> the ordered required-encounter source IDs that spawn on this difficulty.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> EnemyQueues { get; init; } =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        public string BossSourceID { get; init; } = "";

        public int RequiredEncounterTotal { get; init; }
        public int BossTotal { get; init; }
        public int OptionalTotal { get; init; }

        public int Award(string sourceID) =>
            !string.IsNullOrEmpty(sourceID) && Awards.TryGetValue(sourceID, out int amount) ? amount : 0;
    }

    /// <summary>
    /// The F05 reward runtime: compiles the active level's
    /// <see cref="LevelRewardManifest"/> into a per-difficulty ledger, hands out
    /// each finite source's award exactly once, and remembers which sources a
    /// player has actually <b>collected</b>.
    ///
    /// <para><b>Three source states</b>, per
    /// docs/design-contracts/DUST_ECONOMY.md §"Retries and persistence":
    /// <i>unissued</i>, <i>spawned/uncollected</i> and <i>collected</i>.
    /// Issuing moves a source to spawned; <see cref="CommitClaim"/> (called from
    /// <see cref="ChronalDustPickup.Collect"/>) moves it to collected and pays
    /// the wallet in the same step.</para>
    ///
    /// <para><b>Claims persist; issues do not.</b> The collected set rides the
    /// per-attempt registries into the save, so a death rewind, checkpoint
    /// resume, Anchor Snap, quit/resume or crash recovery never pays a source
    /// twice — a restored enemy whose reward was collected may fight again and
    /// awards zero. The issued set is per level load, so a reload that discards
    /// the world's uncollected pickups also lets their sources be re-issued,
    /// which is what keeps the restore from either duplicating a pickup or
    /// silently swallowing one.</para>
    ///
    /// <para>Static rather than an autoload deliberately: it holds no node state
    /// and must be readable from pooled pickups, enemy controllers and the
    /// completion transaction alike without a scene-tree lookup.</para>
    /// </summary>
    public static class LevelRewardDirectory {

        private static LevelRewardLedger _ledger;
        private static int _ledgerLevelIndex = -1;
        private static string _ledgerHeroID = "";
        private static Difficulty _ledgerDifficulty = Difficulty.Normal;

        private static readonly HashSet<string> Issued = new(StringComparer.Ordinal);
        private static readonly HashSet<string> Claimed = new(StringComparer.Ordinal);

        /// <summary>Undeposited base dust this attempt lost to a Collapse or exit fee.</summary>
        public static int DustLostThisAttempt { get; private set; }

        /// <summary>The tier bonus paid by the last successful completion (results itemisation).</summary>
        public static int LastTierBonusDust { get; internal set; }

        /// <summary>The compiled ledger for the live level, or null when the level has no manifest.</summary>
        public static LevelRewardLedger Current => _ledger;

        // === Compilation ====================================================

        /// <summary>
        /// Compiles (or reuses) the ledger for the level and difficulty the
        /// session is currently on. Safe to call from anywhere and on every
        /// award — it only rebuilds when the level or difficulty changes.
        /// </summary>
        public static LevelRewardLedger EnsureCompiled() {
            StoryManager story = StoryManager.Instance;
            if (story == null) return _ledger;
            int levelIndex = (int)story.CurrentLevel;
            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            if (_ledger != null && _ledgerLevelIndex == levelIndex && _ledgerDifficulty == difficulty) {
                return _ledger;
            }
            _ledgerLevelIndex = levelIndex;
            _ledgerDifficulty = difficulty;
            _ledger = Compile(LevelRewardManifest.LoadFor(levelIndex), difficulty);
            return _ledger;
        }

        /// <summary>
        /// Test seam and the pure half of <see cref="EnsureCompiled"/>: turns an
        /// authored manifest into a ledger for one difficulty. Enemy weights are
        /// resolved from each source's <see cref="FTT.Enemies.EnemyData.Tier"/>
        /// (standard = 1, elite/Eraser = 5), never authored twice.
        /// </summary>
        public static LevelRewardLedger Compile(LevelRewardManifest manifest, Difficulty difficulty) {
            if (manifest == null) return null;

            var awards = new Dictionary<string, int>(StringComparer.Ordinal);
            var queues = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var weights = new List<RewardSourceWeight>();

            // --- Required encounters -----------------------------------------
            // Each difficulty compiles its OWN spawnable source list against the
            // SAME pool, exactly as the level controllers spawn: a prefix of the
            // authored table sized by ScaleEncounterCount and capped at the
            // table's length. More enemies change the distribution, never the
            // total.
            foreach ((string waveID, string[] enemyIDs) in manifest.ParseWaves()) {
                (string id, int authoredCount) = SplitWaveHead(waveID, enemyIDs.Length);
                int spawnCount = authoredCount <= 0
                    ? enemyIDs.Length
                    : Math.Min(
                        StoryDifficultyTuning.ScaleEncounterCount(authoredCount, difficulty),
                        enemyIDs.Length);
                for (int index = 0; index < spawnCount; index++) {
                    string enemyID = enemyIDs[index];
                    string sourceID = $"{id}#{index}";
                    weights.Add(new RewardSourceWeight(sourceID, WeightForEnemy(enemyID)));
                    if (!queues.TryGetValue(enemyID, out List<string> queue)) {
                        queue = new List<string>();
                        queues[enemyID] = queue;
                    }
                    queue.Add(sourceID);
                }
            }
            foreach ((string sourceID, int amount) in manifest.ParseScriptedSources()) {
                weights.Add(new RewardSourceWeight(sourceID, 0, amount));
            }
            foreach (KeyValuePair<string, int> entry
                     in RewardAllocator.Allocate(Math.Max(0, manifest.RequiredEncounterPool), weights)) {
                awards[entry.Key] = entry.Value;
            }

            // --- Boss --------------------------------------------------------
            // Repeated phases of one boss share this single reward.
            if (!string.IsNullOrWhiteSpace(manifest.BossSourceID) && manifest.BossAward > 0) {
                awards[manifest.BossSourceID] = manifest.BossAward;
            }

            // --- Optional ----------------------------------------------------
            // Half to all Extractors combined, half to the designated secret. A
            // level with no secret gives the whole pool to its machines (Level
            // 1); a level with no machines gives the whole pool to its secret
            // (Level 4A).
            var extractorIDs = new List<string>();
            foreach (string id in manifest.ExtractorSourceIDs ?? Array.Empty<string>()) {
                if (!string.IsNullOrWhiteSpace(id)) extractorIDs.Add(id);
            }
            string secretID = (manifest.SecretSourceID ?? "").Trim();
            int optional = Math.Max(0, manifest.OptionalPool);
            bool hasSecret = secretID.Length > 0 && !manifest.OptionalPoolAllToExtractors;
            int extractorPool;
            int secretPool;
            if (extractorIDs.Count == 0) {
                extractorPool = 0;
                secretPool = hasSecret ? optional : 0;
            } else if (!hasSecret) {
                extractorPool = optional;
                secretPool = 0;
            } else {
                extractorPool = optional / 2;
                secretPool = optional - extractorPool;
            }
            foreach (KeyValuePair<string, int> entry in RewardAllocator.SplitEvenly(extractorPool, extractorIDs)) {
                awards[entry.Key] = entry.Value;
            }
            // F05 §7: an Extractor that is ALSO the designated secret owns the
            // sum of its machine share and the discovery share, paid once as one
            // pickup — the discovery flag must not trigger a second payout.
            if (secretPool > 0) {
                awards[secretID] = (awards.TryGetValue(secretID, out int existing) ? existing : 0) + secretPool;
            }

            var readOnlyQueues = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<string>> entry in queues) readOnlyQueues[entry.Key] = entry.Value;

            return new LevelRewardLedger {
                LevelID = manifest.LevelID ?? "",
                CampaignLevelIndex = manifest.CampaignLevelIndex,
                Difficulty = difficulty,
                Awards = awards,
                EnemyQueues = readOnlyQueues,
                BossSourceID = manifest.BossSourceID ?? "",
                RequiredEncounterTotal = Math.Max(0, manifest.RequiredEncounterPool),
                BossTotal = Math.Max(0, manifest.BossAward),
                OptionalTotal = optional
            };
        }

        private static (string ID, int AuthoredCount) SplitWaveHead(string head, int enemyCount) {
            int at = head.IndexOf('@');
            if (at <= 0) return (head, enemyCount);
            string id = head.Substring(0, at);
            // "@0" means the level spawns the whole table unconditionally, with
            // no difficulty scaling (Level 4A's Legacy approach).
            return int.TryParse(head.Substring(at + 1), out int authored)
                ? (id, authored)
                : (id, enemyCount);
        }

        private static int WeightForEnemy(string enemyID) {
            try {
                FTT.Enemies.EnemyData data = FTT.Enemies.EnemyFactory.LoadData(enemyID);
                return data != null && data.Tier != FTT.Enemies.EnemyTier.Standard
                    ? RewardAllocator.EliteWeight
                    : RewardAllocator.StandardWeight;
            } catch (Exception) {
                // An unresolvable enemy ID is an authoring error in the
                // manifest, not a reason to fail a level load: it allocates as
                // a standard and the content test names the file.
                return RewardAllocator.StandardWeight;
            }
        }

        // === Issuing ========================================================

        /// <summary>
        /// Issues the next unissued, unclaimed required-encounter source for
        /// this enemy ID. Returns false (and zero) when the level has no
        /// manifest, when the enemy is not an authored finite source, or when
        /// every source of its kind is already spent — which is exactly how a
        /// repeatable reinforcement or summon draws nothing.
        /// </summary>
        public static bool TryIssueEnemyAward(string enemyID, out string sourceID, out int amount) {
            sourceID = "";
            amount = 0;
            LevelRewardLedger ledger = EnsureCompiled();
            if (ledger == null || string.IsNullOrWhiteSpace(enemyID)) return false;
            if (!ledger.EnemyQueues.TryGetValue(enemyID, out IReadOnlyList<string> queue)) return false;
            foreach (string candidate in queue) {
                if (Issued.Contains(candidate) || Claimed.Contains(candidate)) continue;
                Issued.Add(candidate);
                sourceID = candidate;
                amount = ledger.Award(candidate);
                return true;
            }
            return false;
        }

        /// <summary>Issues the level's single boss reward. Repeated phases share it.</summary>
        public static bool TryIssueBossAward(out string sourceID, out int amount) {
            sourceID = "";
            amount = 0;
            LevelRewardLedger ledger = EnsureCompiled();
            if (ledger == null || string.IsNullOrEmpty(ledger.BossSourceID)) return false;
            return TryIssueSourceAward(ledger.BossSourceID, out sourceID, out amount);
        }

        /// <summary>
        /// Issues a directly-keyed source (an Extractor's object ID, the
        /// designated secret's ID). Returns false when the ledger does not
        /// author it — an unauthored source pays nothing, it does not fall back
        /// to a flat value.
        /// </summary>
        public static bool TryIssueSourceAward(string sourceID, out string issuedID, out int amount) {
            issuedID = "";
            amount = 0;
            LevelRewardLedger ledger = EnsureCompiled();
            if (ledger == null || string.IsNullOrWhiteSpace(sourceID)) return false;
            if (!ledger.Awards.ContainsKey(sourceID)) return false;
            if (Issued.Contains(sourceID) || Claimed.Contains(sourceID)) return false;
            Issued.Add(sourceID);
            issuedID = sourceID;
            amount = ledger.Award(sourceID);
            return true;
        }

        /// <summary>True once this source's pickup has actually been collected.</summary>
        public static bool IsClaimed(string sourceID) =>
            !string.IsNullOrEmpty(sourceID) && Claimed.Contains(sourceID);

        /// <summary>
        /// Commits a source claim. Called from the collection site so the claim
        /// and the wallet increment land together — a pickup that is never
        /// touched leaves its source spawned-but-uncollected, and a reload can
        /// legitimately re-issue it.
        /// </summary>
        public static void CommitClaim(string sourceID) {
            if (string.IsNullOrEmpty(sourceID)) return;
            Claimed.Add(sourceID);
            Issued.Remove(sourceID);
        }

        // === Attempt lifecycle ==============================================

        /// <summary>Fresh level entry / Restart Level: nothing survives.</summary>
        public static void ResetAttempt() {
            Issued.Clear();
            Claimed.Clear();
            DustLostThisAttempt = 0;
            LastTierBonusDust = 0;
            _ledger = null;
            _ledgerLevelIndex = -1;
        }

        /// <summary>Mid-level resume: collected claims come back, issues do not.</summary>
        public static void RestoreClaims(IEnumerable<string> claimedSourceIDs) {
            Issued.Clear();
            if (claimedSourceIDs == null) return;
            foreach (string id in claimedSourceIDs) {
                if (!string.IsNullOrWhiteSpace(id)) Claimed.Add(id);
            }
        }

        /// <summary>The collected set, for the per-attempt save record.</summary>
        public static List<string> ClaimedSourceIDs() => new(Claimed);

        /// <summary>
        /// Records undeposited base dust removed by a Collapse or exit fee, for
        /// the results overlay's losses line. Never unclaims a collected source
        /// — lost dust cannot be recovered by killing the same source again.
        /// </summary>
        public static void RecordDustLoss(int amount) {
            if (amount > 0) DustLostThisAttempt += amount;
        }
    }
}
