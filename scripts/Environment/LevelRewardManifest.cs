using Godot;
using System;
using System.Collections.Generic;

namespace FTT.Environment {

    /// <summary>
    /// One authored level's F05 Chronal Dust reward inventory: the budget row
    /// from docs/design-contracts/DUST_ECONOMY.md plus the stable IDs of every
    /// finite reward source the level contains.
    ///
    /// <para><b>A source has exactly one budget category.</b> Required
    /// encounters draw from <see cref="RequiredEncounterPool"/>, the boss takes
    /// <see cref="BossAward"/> (repeated phases of one boss share that single
    /// reward), and Extractors plus the designated secret share
    /// <see cref="OptionalPool"/>.</para>
    ///
    /// <para><b>Wave and scripted-source lists are plain
    /// <c>PackedStringArray</c>s, deliberately.</b> An authored Script-typed
    /// array corrupts the Godot 4.7.1 .NET heap when it is empty, and a manifest
    /// with no Extractors or no secret is an ordinary case — so nothing here is
    /// a sub-resource. A wave row reads
    /// <c>"waveID:enemy_a,enemy_b,enemy_c"</c> in authored spawn order; a
    /// scripted row reads <c>"sourceID:amount"</c>.</para>
    /// </summary>
    [GlobalClass]
    public partial class LevelRewardManifest : Resource {
        public const string FolderPath = "res://resources/Content/reward_manifests/";

        /// <summary>The level's stable ID (matches <c>StoryLevelControllerBase.LevelID</c>).</summary>
        [Export] public string LevelID = "";

        /// <summary>The <c>CampaignLevel</c> ordinal this manifest serves (16 = Level 4A).</summary>
        [Export] public int CampaignLevelIndex = -1;

        /// <summary>Ledger column "Required encounters" — every mandatory non-boss reward.</summary>
        [Export] public int RequiredEncounterPool;

        /// <summary>Ledger column "Boss" — 25 for all sixteen bosses. 0 = no boss.</summary>
        [Export] public int BossAward = 25;

        /// <summary>Ledger column "Optional total" — the whole level, never per object.</summary>
        [Export] public int OptionalPool;

        /// <summary>Stable ID of the boss reward source. Empty = the level has no boss.</summary>
        [Export] public string BossSourceID = "";

        /// <summary>
        /// Authored mandatory waves, in spawn order, as
        /// <c>"waveID:enemy_a,enemy_b"</c>. Each entry expands to source IDs
        /// <c>waveID#0</c>, <c>waveID#1</c>… Easy compiles a prefix of each wave
        /// exactly as the level controllers do
        /// (<c>StoryDifficultyTuning.ScaleEncounterCount</c>, capped at the
        /// authored length), so a difficulty changes the distribution and never
        /// the total.
        /// </summary>
        [Export] public string[] RequiredWaves = Array.Empty<string>();

        /// <summary>
        /// Scripted mandatory rewards as <c>"sourceID:amount"</c>. These
        /// <b>replace</b> an equivalent weighted allocation rather than adding
        /// to it: the amount is carved out of <see cref="RequiredEncounterPool"/>
        /// before the remainder is distributed.
        /// </summary>
        [Export] public string[] ScriptedRequiredSources = Array.Empty<string>();

        /// <summary>Authored Chronal Extractor object IDs, in placement order.</summary>
        [Export] public string[] ExtractorSourceIDs = Array.Empty<string>();

        /// <summary>The designated secret/discovery reward's stable ID. Empty = none.</summary>
        [Export] public string SecretSourceID = "";

        /// <summary>
        /// Level 1's rule: the whole optional pool belongs to its Extractors and
        /// no secret is introduced before Level 2. Everywhere else the pool
        /// splits half to the Extractors and half to the discovery reward.
        /// </summary>
        [Export] public bool OptionalPoolAllToExtractors;

        /// <summary>Ledger check: required encounters + boss.</summary>
        public int RequiredTotal => Math.Max(0, RequiredEncounterPool) + Math.Max(0, BossAward);

        /// <summary><c>CampaignLevel.LegacyNexus</c>: the one campaign slot whose
        /// manifest depends on the locked character.</summary>
        public const int LegacyLevelIndex = 16;

        public static string PathFor(int campaignLevelIndex, string heroCharacterID = "") =>
            $"{FolderPath}{FileNameFor(campaignLevelIndex, heroCharacterID)}";

        /// <summary>
        /// Level 4A resolves <b>per hero</b>
        /// (<c>level_04a_&lt;hero&gt;_rewards.tres</c>), because each of the nine
        /// Legacy Levels authors its own approach inventory. The budget ROW is
        /// identical across all nine — 15 required / 25 boss / 10 optional
        /// "regardless of layout or enemy count" — but the source list is not.
        /// Every other level is <c>level_NN_rewards</c>.
        /// </summary>
        public static string FileNameFor(int campaignLevelIndex, string heroCharacterID = "") {
            if (campaignLevelIndex != LegacyLevelIndex) return $"level_{campaignLevelIndex:00}_rewards.tres";
            string hero = (heroCharacterID ?? "").Trim().ToLowerInvariant();
            return hero.Length == 0
                ? "level_04a_einstein_rewards.tres"
                : $"level_04a_{hero}_rewards.tres";
        }

        /// <summary>
        /// Parses <see cref="RequiredWaves"/> into (waveID, ordered enemy IDs).
        /// Malformed rows are skipped rather than throwing — a broken manifest
        /// must degrade to "this source pays nothing", never crash a level load.
        /// </summary>
        public List<(string WaveID, string[] EnemyIDs)> ParseWaves() {
            var waves = new List<(string, string[])>();
            foreach (string row in RequiredWaves ?? Array.Empty<string>()) {
                if (string.IsNullOrWhiteSpace(row)) continue;
                int split = row.IndexOf(':');
                if (split <= 0 || split >= row.Length - 1) continue;
                string waveID = row.Substring(0, split).Trim();
                string[] enemies = row.Substring(split + 1).Split(
                    ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (waveID.Length == 0 || enemies.Length == 0) continue;
                waves.Add((waveID, enemies));
            }
            return waves;
        }

        /// <summary>Parses <see cref="ScriptedRequiredSources"/> into (sourceID, amount).</summary>
        public List<(string SourceID, int Amount)> ParseScriptedSources() {
            var scripted = new List<(string, int)>();
            foreach (string row in ScriptedRequiredSources ?? Array.Empty<string>()) {
                if (string.IsNullOrWhiteSpace(row)) continue;
                int split = row.IndexOf(':');
                if (split <= 0 || split >= row.Length - 1) continue;
                string sourceID = row.Substring(0, split).Trim();
                if (sourceID.Length == 0) continue;
                if (!int.TryParse(row.Substring(split + 1).Trim(), out int amount) || amount <= 0) continue;
                scripted.Add((sourceID, amount));
            }
            return scripted;
        }

        /// <summary>Loads a level's manifest through the pinning cache. Null when unauthored.</summary>
        public static LevelRewardManifest LoadFor(int campaignLevelIndex, string heroCharacterID = "") {
            if (campaignLevelIndex < 0) return null;
            string path = PathFor(campaignLevelIndex, heroCharacterID);
            if (!ResourceLoader.Exists(path)) return null;
            return FTT.Core.AuthoredResources.Load<LevelRewardManifest>(path);
        }
    }
}
