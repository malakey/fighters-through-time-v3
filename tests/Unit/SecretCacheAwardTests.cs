using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W8 — GAP-03: a <see cref="SecretCache"/> is the physical source of
/// its level's F05 discovery reward. Discovery issues the reserved
/// <c>{level}.secret</c> award once as a physical pickup; the wallet and the
/// results line are paid only at collection; a reload that finds the secret
/// found but uncollected respawns the one pickup; a collected claim never pays
/// again; and an unledgered context pays nothing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SecretCacheAwardTests {

    private const string SecretID = "level_02.secret";

    [TestCase]
    public void DiscoveryIssuesTheReservedAwardOnceAsAPhysicalPickup() {
        using var fixture = new SecretFixture();
        LevelRewardLedger ledger = LevelRewardDirectory.EnsureCompiled();
        AssertObject(ledger).IsNotNull();
        int award = ledger.Award(SecretID);
        AssertThat(award > 0).OverrideFailureMessage("Orleans reserves a secret share.").IsTrue();

        SecretCache cache = fixture.AddCache();
        int walletBefore = StoryManager.Instance.ChronalDustCollected;
        AssertThat(cache.Discover()).IsTrue();
        AssertThat(StoryManager.Instance.SecretFoundThisLevel).IsTrue();
        AssertThat(StoryManager.Instance.ChronalDustCollected)
            .OverrideFailureMessage("Discovery alone pays nothing; the wallet is paid at collection.")
            .IsEqual(walletBefore);

        List<ChronalDustPickup> pickups = fixture.Pickups();
        AssertThat(pickups.Count).IsEqual(1);
        AssertThat(pickups[0].DustAmount).IsEqual(award);
        AssertString(pickups[0].SourceID).IsEqual(SecretID);
        AssertThat(pickups[0].Source).IsEqual(DustAwardSource.Secret);

        AssertThat(cache.Discover()).OverrideFailureMessage("A secret is found once.").IsFalse();
        AssertThat(fixture.Pickups().Count).IsEqual(1);

        pickups[0].Collect();
        AssertThat(StoryManager.Instance.ChronalDustCollected).IsEqual(walletBefore + award);
        AssertThat(LevelRewardDirectory.IsClaimed(SecretID)).IsTrue();
    }

    [TestCase]
    public void AReloadRespawnsAnUncollectedAwardOnceAndNeverACollectedOne() {
        using var fixture = new SecretFixture();
        SecretCache first = fixture.AddCache();
        first.Discover();
        AssertThat(fixture.Pickups().Count).IsEqual(1);

        // The reload discards the world: issues are forgotten, claims survive.
        fixture.ReleasePickups();
        LevelRewardDirectory.RestoreClaims(LevelRewardDirectory.ClaimedSourceIDs());
        SecretCache reloaded = fixture.AddCache();
        reloaded.MarkAlreadyFound();
        AssertThat(reloaded.RestorePendingAward()).IsTrue();
        AssertThat(reloaded.RestorePendingAward())
            .OverrideFailureMessage("At most one pending pickup per load.")
            .IsFalse();
        List<ChronalDustPickup> pickups = fixture.Pickups();
        AssertThat(pickups.Count).IsEqual(1);
        pickups[0].Collect();

        fixture.ReleasePickups();
        LevelRewardDirectory.RestoreClaims(LevelRewardDirectory.ClaimedSourceIDs());
        SecretCache again = fixture.AddCache();
        again.MarkAlreadyFound();
        AssertThat(again.RestorePendingAward())
            .OverrideFailureMessage("A collected claim never pays again.")
            .IsFalse();
        AssertThat(fixture.Pickups().Count).IsEqual(0);
    }

    [TestCase]
    public void AnUnledgeredContextCountsTheSecretButPaysNothing() {
        using var fixture = new SecretFixture();
        LevelRewardDirectory.SuppressLedgerForTest = true;
        SecretCache cache = fixture.AddCache();
        AssertThat(cache.Discover()).IsTrue();
        AssertThat(StoryManager.Instance.SecretFoundThisLevel).IsTrue();
        AssertThat(fixture.Pickups().Count).IsEqual(0);
    }

    // ---- Harness -------------------------------------------------------------

    private sealed class SecretFixture : System.IDisposable {
        private readonly Node2D _host;
        private readonly CampaignLevel _originalLevel;
        private readonly Difficulty _originalDifficulty;
        private readonly string _originalCharacter;
        private readonly int _originalSlot;

        public SecretFixture() {
            StoryManager story = StoryManager.Instance;
            _originalLevel = story.CurrentLevel;
            _originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            story.PrepareDirectLevel(CampaignLevel.Orleans, "einstein", Difficulty.Normal);
            story.ClearLevelAttemptState();
            LevelRewardDirectory.ResetAttempt();
            _host = new Node2D { Name = "SecretCacheHost" };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(_host);
        }

        public SecretCache AddCache() {
            SecretCache cache = SecretCache.Create(SecretID, new Vector2(300f, -100f));
            _host.AddChild(cache);
            return cache;
        }

        public List<ChronalDustPickup> Pickups() {
            var list = new List<ChronalDustPickup>();
            Godot.Collections.Array<Node> children = _host.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is ChronalDustPickup pickup && pickup.Visible) list.Add(pickup);
            }
            return list;
        }

        public void ReleasePickups() => PoolManager.Instance?.ReleaseActiveUnder(_host);

        public void Dispose() {
            PoolManager.Instance?.ReleaseActiveUnder(_host);
            _host.GetParent()?.RemoveChild(_host);
            _host.Free();
            StoryManager story = StoryManager.Instance;
            story.PrepareDirectLevel(_originalLevel,
                string.IsNullOrEmpty(_originalCharacter) ? "einstein" : _originalCharacter, _originalDifficulty);
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            story.ClearLevelAttemptState();
            LevelRewardDirectory.ResetAttempt();
        }
    }
}
