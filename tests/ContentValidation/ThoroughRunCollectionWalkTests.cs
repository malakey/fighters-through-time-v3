using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 12 W8 — GAP-03 acceptance: "verify physical collection … and
/// complete-route totals rather than only allocator sums". This walk loads every
/// campaign level a run visits (Levels 1–15; S27 retired the per-hero Level 4A),
/// finds the optional sources that are <b>physically placed</b> in the built
/// scene — every Chronal Extractor / Resonance Hold node and the secret cache —
/// resolves each through its real completion path, and collects the pickup it
/// spawns. Required encounters and the boss are issued and claimed through the
/// ledger exactly as a kill or a boss defeat would. A thorough run must pay the
/// F05 base <b>1,000</b> (720 required-route + 280 optional).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ThoroughRunCollectionWalkTests {

    private const int ThoroughBase = 1000;
    private const int OptionalBase = 280;

    [TestCase]
    public void AThoroughRunCollectsTheFullOneThousandBaseDust() {
        StoryManager story = StoryManager.Instance;
        CampaignLevel originalLevel = story.CurrentLevel;
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        bool originalPaused = ((SceneTree)Engine.GetMainLoop()).Paused;
        var issues = new List<string>();
        try {
            // Package 13 W2 (S27): Levels 1-15 are the whole run; no level
            // depends on the hero since the per-hero Level 4A was retired.
            int total = 0;
            int optionalTotal = 0;
            for (int index = 1; index <= 15; index++) {
                (int levelTotal, int optional) = WalkLevel((CampaignLevel)index, "einstein", issues);
                total += levelTotal;
                optionalTotal += optional;
            }
            if (total != ThoroughBase) {
                issues.Add($"a thorough run collects {total}, expected {ThoroughBase}");
            }
            if (optionalTotal != OptionalBase) {
                issues.Add($"physically placed optional sources pay {optionalTotal}, expected {OptionalBase}");
            }
        } finally {
            ((SceneTree)Engine.GetMainLoop()).Paused = originalPaused;
            story.PrepareDirectLevel(originalLevel,
                string.IsNullOrEmpty(originalCharacter) ? "einstein" : originalCharacter, originalDifficulty);
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            LevelRewardDirectory.ResetAttempt();
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// Loads one level slotless, collects every placed optional source through
    /// its real path, claims the required and boss sources through the ledger,
    /// and returns (level total, optional share).
    /// </summary>
    private static (int Total, int Optional) WalkLevel(CampaignLevel level, string hero, List<string> issues) {
        StoryManager story = StoryManager.Instance;
        story.PrepareDirectLevel(level, hero, Difficulty.Normal);
        story.ClearLevelAttemptState();
        LevelRewardDirectory.ResetAttempt();
        LevelRewardLedger ledger = LevelRewardDirectory.EnsureCompiled();
        string label = level.ToString();
        if (ledger == null) {
            issues.Add($"{label}: no reward ledger");
            return (0, 0);
        }

        string scenePath = StoryManager.GetLevelScenePath(level);
        var packed = ResourceLoader.Load<PackedScene>(scenePath);
        if (packed == null) {
            issues.Add($"{label}: scene '{scenePath}' did not load");
            return (0, 0);
        }
        Node root = packed.Instantiate();
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(root);
        int optional = 0;
        try {
            var placedOptional = new HashSet<string>(StringComparer.Ordinal);
            var extractors = new List<ChronalExtractor>();
            var caches = new List<SecretCache>();
            Collect(root, extractors, caches);
            foreach (ChronalExtractor extractor in extractors) {
                placedOptional.Add(extractor.ObjectID);
                if (extractor is ResonanceHoldNode hold) hold.CompleteChannelForTest();
                else extractor.TakeEnvironmentDamage(extractor.MaxHP * 10f);
            }
            foreach (SecretCache cache in caches) {
                placedOptional.Add(cache.SecretID);
                cache.Discover();
            }
            foreach (ChronalDustPickup pickup in Pickups(root)) {
                if (pickup.Source is DustAwardSource.Extractor or DustAwardSource.Secret) {
                    optional += pickup.DustAmount;
                    pickup.Collect();
                }
            }
            if (optional != ledger.OptionalTotal) {
                issues.Add($"{label}: placed optional sources [{string.Join(",", placedOptional)}] pay {optional}, "
                    + $"the ledger's optional pool is {ledger.OptionalTotal}");
            }
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(root);
            root.GetParent()?.RemoveChild(root);
            root.Free();
            tree.Paused = false;
        }

        int required = 0;
        foreach (KeyValuePair<string, IReadOnlyList<string>> queue in ledger.EnemyQueues) {
            foreach (string _ in queue.Value) {
                if (!LevelRewardDirectory.TryIssueEnemyAward(queue.Key, out string sourceID, out int amount)) continue;
                LevelRewardDirectory.CommitClaim(sourceID);
                required += amount;
            }
        }
        // Scripted required sources are keyed directly.
        foreach (KeyValuePair<string, int> award in ledger.Awards) {
            if (LevelRewardDirectory.IsClaimed(award.Key) || award.Key == ledger.BossSourceID) continue;
            if (award.Key.Contains(".secret") || award.Key.Contains("extractor")) continue;
            if (LevelRewardDirectory.TryIssueSourceAward(award.Key, out string id, out int amount)) {
                LevelRewardDirectory.CommitClaim(id);
                required += amount;
            }
        }
        if (required != ledger.RequiredEncounterTotal) {
            issues.Add($"{label}: required sources pay {required}, expected {ledger.RequiredEncounterTotal}");
        }
        int boss = 0;
        if (LevelRewardDirectory.TryIssueBossAward(out string bossID, out int bossAmount)) {
            LevelRewardDirectory.CommitClaim(bossID);
            boss = bossAmount;
        }
        return (required + boss + optional, optional);
    }

    private static void Collect(Node node, List<ChronalExtractor> extractors, List<SecretCache> caches) {
        if (node is ChronalExtractor extractor) extractors.Add(extractor);
        if (node is SecretCache cache) caches.Add(cache);
        Godot.Collections.Array<Node> children = node.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) Collect(child, extractors, caches);
    }

    private static List<ChronalDustPickup> Pickups(Node root) {
        var list = new List<ChronalDustPickup>();
        void Walk(Node node) {
            if (node is ChronalDustPickup pickup && pickup.Visible) list.Add(pickup);
            Godot.Collections.Array<Node> children = node.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) Walk(child);
        }
        Walk(root);
        return list;
    }
}
