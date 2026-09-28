using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W2 — GAP-04, summon reward provenance
/// (docs/design-contracts/DUST_ECONOMY.md F05, docs/DESIGN_GAP_REVIEW_2026-09-16.md).
///
/// <para>The kill-drop system requests an award by enemy <i>type</i>, and the
/// reward directory hands out the next unissued finite source of that type. A
/// <c>SummonMinions</c> spawn of the same type used to draw from that queue, so
/// a summon that died first stole a mandatory encounter's entitlement — the
/// level total held, but the reward's location and ownership were wrong. A
/// summon now carries its provenance from spawn to death and issues nothing.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SummonRewardProvenanceTests {

    private const string EnemyID = "chrono_slasher";

    [TestCase]
    public void ASummonThatDiesFirstStealsNoMandatorySourceOfItsType() {
        StoryManager story = StoryManager.Instance;
        CampaignLevel originalLevel = story.CurrentLevel;
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "SummonProvenanceHost" };
        tree.Root.AddChild(host);
        try {
            // Orleans authors chrono_slasher as a REQUIRED source; its ledger is
            // the live one for this (slotless) session.
            story.PrepareDirectLevel(CampaignLevel.Orleans, "einstein", Difficulty.Normal);
            LevelRewardDirectory.ResetAttempt();
            LevelRewardLedger ledger = LevelRewardDirectory.EnsureCompiled();
            AssertObject(ledger).IsNotNull();
            AssertThat(ledger.EnemyQueues.TryGetValue(EnemyID, out IReadOnlyList<string> queue)).IsTrue();
            AssertThat(queue.Count > 0).IsTrue();

            var drops = new StoryDropSystem { Name = "StoryDropSystem", RandomSeed = 777UL };
            host.AddChild(drops);

            // Several summons of the mandatory type die before any authored one.
            for (int kill = 0; kill < queue.Count + 2; kill++) {
                EventBus.Instance.RaiseEnemyKilled(new EnemyKilledPayload {
                    EnemyID = EnemyID,
                    Position = new Vector2(100 + kill, 0),
                    ChronalDustDrop = 5,
                    IsSummoned = true
                });
            }
            AssertThat(DustPickupsUnder(host).Count)
                .OverrideFailureMessage("A summon issued a dust pickup; it is not a finite reward source.")
                .IsEqual(0);

            // Every authored chrono_slasher still collects its own source, in
            // queue order, for exactly the ledger's award.
            int expectedTotal = 0;
            foreach (string sourceID in queue) expectedTotal += ledger.Award(sourceID);
            for (int kill = 0; kill < queue.Count; kill++) {
                EventBus.Instance.RaiseEnemyKilled(new EnemyKilledPayload {
                    EnemyID = EnemyID,
                    Position = new Vector2(500 + kill, 0),
                    ChronalDustDrop = 5
                });
            }
            List<ChronalDustPickup> pickups = DustPickupsUnder(host);
            var issued = new List<string>();
            int paid = 0;
            foreach (ChronalDustPickup pickup in pickups) {
                issued.Add(pickup.SourceID);
                paid += pickup.DustAmount;
            }
            issued.Sort(System.StringComparer.Ordinal);
            var expectedSources = new List<string>(queue);
            expectedSources.Sort(System.StringComparer.Ordinal);
            AssertThat(string.Join(",", issued))
                .OverrideFailureMessage("Each authored kill must receive its own reserved source.")
                .IsEqual(string.Join(",", expectedSources));
            AssertThat(paid).IsEqual(expectedTotal);
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(host);
            host.GetParent()?.RemoveChild(host);
            host.Free();
            story.PrepareDirectLevel(originalLevel, string.IsNullOrEmpty(originalCharacter) ? "einstein" : originalCharacter,
                originalDifficulty);
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            LevelRewardDirectory.ResetAttempt();
        }
    }

    [TestCase]
    public void SummonProvenanceRidesTheKillPayloadAndNeverSurvivesAPoolCycle() {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "SummonPoolCycleHost" };
        tree.Root.AddChild(host);
        var kills = new List<EnemyKilledPayload>();
        void OnKilled(EnemyKilledPayload payload) => kills.Add(payload);
        EventBus.Instance.OnEnemyKilled += OnKilled;
        try {
            EnemyController summon = EnemyFactory.SpawnSummoned(EnemyID, host, new Vector2(200, 0));
            AssertObject(summon).IsNotNull();
            AssertThat(summon.IsSummoned).IsTrue();
            summon.TakeDamage(summon.CurrentHP + 1000);
            AssertThat(kills.Count).IsEqual(1);
            AssertThat(kills[0].IsSummoned)
                .OverrideFailureMessage("The death must carry the summon's provenance.")
                .IsTrue();

            // Recycle the body: an ordinary authored spawn must never inherit it.
            PoolManager.Instance?.ReleaseActiveUnder(host);
            EnemyController authored = EnemyFactory.Spawn(EnemyID, host, new Vector2(300, 0));
            AssertObject(authored).IsNotNull();
            AssertThat(authored.IsSummoned)
                .OverrideFailureMessage("A pooled body kept its previous occupant's summon provenance.")
                .IsFalse();
            authored.TakeDamage(authored.CurrentHP + 1000);
            AssertThat(kills.Count).IsEqual(2);
            AssertThat(kills[1].IsSummoned).IsFalse();
        } finally {
            EventBus.Instance.OnEnemyKilled -= OnKilled;
            PoolManager.Instance?.ReleaseActiveUnder(host);
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    private static List<ChronalDustPickup> DustPickupsUnder(Node root) {
        var found = new List<ChronalDustPickup>();
        Godot.Collections.Array<Node> children = root.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is ChronalDustPickup pickup && pickup.Visible) found.Add(pickup);
        }
        return found;
    }
}
