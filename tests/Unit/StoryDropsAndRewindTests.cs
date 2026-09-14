using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
[RequireGodotRuntime]
public class StoryDropsAndRewindTests {
    [TestCase]
    public void DefaultDropProfilesMatchDifficultyContract() {
        StoryDropTable table = StoryDropTable.LoadDefault();
        StoryDropProfile easy = table.GetProfile(Difficulty.Easy);
        StoryDropProfile normal = table.GetProfile(Difficulty.Normal);
        StoryDropProfile hard = table.GetProfile(Difficulty.Hard);

        AssertThat(easy.RandomItemChance).IsEqualApprox(0.30f, 0.0001f);
        AssertThat(easy.HealingAmount).IsEqual(50);
        AssertThat(easy.BuffMultiplier).IsEqualApprox(1.50f, 0.0001f);
        AssertThat(easy.BuffDurationSeconds).IsEqualApprox(15f, 0.0001f);
        AssertThat(normal.RandomItemChance).IsEqualApprox(0.15f, 0.0001f);
        AssertThat(normal.HealingAmount).IsEqual(25);
        AssertThat(normal.BuffMultiplier).IsEqualApprox(1.25f, 0.0001f);
        AssertThat(normal.BuffDurationSeconds).IsEqualApprox(10f, 0.0001f);
        AssertThat(hard.RandomItemChance).IsEqualApprox(0.05f, 0.0001f);
        AssertThat(hard.HealingAmount).IsEqual(10);
        AssertThat(hard.AllowsBuffs).IsFalse();
    }

    [TestCase]
    public void HardDropProfileCanOnlyChooseHealing() {
        StoryDropProfile hard = StoryDropTable.LoadDefault().GetProfile(Difficulty.Hard);
        AssertThat(hard.ChooseItem(0f)).IsEqual(StoryPickupKind.Healing);
        AssertThat(hard.ChooseItem(0.5f)).IsEqual(StoryPickupKind.Healing);
        AssertThat(hard.ChooseItem(0.99f)).IsEqual(StoryPickupKind.Healing);
        AssertThat(hard.ShouldDrop(0.049f)).IsTrue();
        AssertThat(hard.ShouldDrop(0.05f)).IsFalse();
    }

    [TestCase]
    public void DustVisualTiersUseSmallMediumAndLargeThresholds() {
        DustVisualTierSet tiers = FTT.Core.AuthoredResources.Load<DustVisualTierSet>("res://resources/Drops/dust_visual_tiers.tres");
        AssertThat(ReferenceEquals(tiers.GetTexture(1), tiers.SmallTexture)).IsTrue();
        AssertThat(ReferenceEquals(tiers.GetTexture(6), tiers.MediumTexture)).IsTrue();
        AssertThat(ReferenceEquals(tiers.GetTexture(25), tiers.LargeTexture)).IsTrue();
    }

    [TestCase]
    public void HealingAndTemporaryDamageBuffsApplyWithoutChangingCanonicalStats() {
        var player = new PlayerController { CurrentHP = 20 };
        AssertThat(player.HealStory(50)).IsEqual(50);
        AssertThat(player.CurrentHP).IsEqual(70);
        player.ApplyStoryDamageBuff(1.5f, 15f);
        var hitbox = new Hitbox { Damage = 10f, SourcePlayer = player };

        AssertThat(hitbox.CreatePayload(-1).Damage).IsEqualApprox(15f, 0.0001f);
        AssertThat(player.MaximumHP).IsEqual(100);
        hitbox.Free();
        player.Free();
    }

    [TestCase]
    public void StoryRewindLandingGrantsExplicitTwoSecondInvulnerability() {
        var player = new PlayerController { CurrentHP = 0 };
        player.CompleteStoryRewind(new Vector2(10f, 20f), 50);

        AssertThat(player.CurrentHP).IsEqual(50);
        AssertThat(player.IsPostRewindInvulnerable).IsTrue();
        AssertThat(player.ApplyPersistentDamage(25)).IsEqual(0);
        AssertThat(PlayerController.StoryRewindInvulnerabilityFrames).IsEqual(120);
        player.Free();
    }

    [TestCase]
    public void RewindPresentationContractCoversVisualAndAudioHooks() {
        RewindPresentationPayload playback = ChronalRewindManager.CreatePresentationPayload(
            RewindPresentationPhase.Playback,
            new Vector2(12f, 34f),
            true);
        RewindPresentationPayload landed = ChronalRewindManager.CreatePresentationPayload(
            RewindPresentationPhase.Landed,
            Vector2.Zero,
            false);

        AssertThat(playback.GhostTrailEnabled).IsTrue();
        AssertThat(playback.ScreenTintEnabled).IsTrue();
        AssertThat(playback.ScanlinesEnabled).IsTrue();
        AssertThat(playback.MusicDuckDecibels).IsEqualApprox(-12f, 0.0001f);
        AssertThat(playback.ReverseSweepEnabled).IsTrue();
        AssertThat(playback.ClockTickEnabled).IsTrue();
        AssertThat(landed.GhostTrailEnabled).IsFalse();
        AssertThat(landed.MusicDuckDecibels).IsEqualApprox(0f, 0.0001f);
    }

    [TestCase]
    public void EnemyRewindPolicyFreezesWithoutMovingOrResettingTheEnemy() {
        var enemy = new EnemyController();
        AssertThat(enemy.RewindPolicy).IsEqual(StoryRewindPolicy.PreserveCurrentState);
        enemy.SetStoryRewindFrozen(true);
        AssertThat(enemy.IsStoryRewindFrozen).IsTrue();
        enemy.ApplyStoryRewind();
        enemy.SetStoryRewindFrozen(false);
        AssertThat(enemy.IsStoryRewindFrozen).IsFalse();
        enemy.Free();
    }

    // === V7.3 Single Icon Rule: boss/extractor dust is a physical pickup ===

    [TestCase]
    public void ASourcedDustAwardSpawnsANonExpiringLargeTierPickupPaidOnlyAtCollection() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node2D { Name = "DustAwardParent" };
        tree.Root.AddChild(parent);
        var pools = new PoolManager { Name = "DustAwardPools" };
        tree.Root.AddChild(pools);

        int awards = 0;
        int awarded = 0;
        var attributions = new System.Collections.Generic.List<DustAwardCollectedPayload>();
        void OnDust(int amount) { awards++; awarded += amount; }
        void OnAttributed(DustAwardCollectedPayload payload) => attributions.Add(payload);
        EventBus.Instance.OnChronalDustCollected += OnDust;
        EventBus.Instance.OnDustAwardCollected += OnAttributed;
        try {
            ChronalDustPickup pickup = StoryDropSystem.SpawnDustAward(
                15, new Vector2(400f, 100f), parent, DustAwardSource.Extractor);

            AssertObject(pickup)
                .OverrideFailureMessage("The award must spawn a physical pooled pickup.")
                .IsNotNull();
            AssertThat(pickup.DustAmount).IsEqual(15);
            AssertThat(pickup.Source).IsEqual(DustAwardSource.Extractor);
            AssertThat(pickup.NeverExpires).IsTrue();
            // Package 11 A10 (F05): only the boss forces the Large plate now.
            AssertThat(pickup.ForceLargeTier)
                .OverrideFailureMessage("An Extractor must use its actual quantity icon.")
                .IsFalse();
            AssertThat(awards)
                .OverrideFailureMessage("The wallet must stay unpaid until collection.")
                .IsEqual(0);

            // Far past the ordinary 10 s kill-drop expiry: a milestone award
            // never times out (no player is in magnet range at x=400).
            for (int frame = 0; frame < 700; frame++) pickup._PhysicsProcess(1.0 / 60.0);
            AssertThat(pickup.IsInsideTree())
                .OverrideFailureMessage("A boss/extractor award must never expire.")
                .IsTrue();
            AssertThat(awards).IsEqual(0);

            pickup.Collect();
            AssertThat(awards).IsEqual(1);
            AssertThat(awarded).IsEqual(15);
            AssertThat(attributions.Count).IsEqual(1);
            AssertThat(attributions[0].Amount).IsEqual(15);
            AssertThat(attributions[0].Source).IsEqual(DustAwardSource.Extractor);
        } finally {
            EventBus.Instance.OnChronalDustCollected -= OnDust;
            EventBus.Instance.OnDustAwardCollected -= OnAttributed;
            pools.ClearAllPools();
            pools.Free();
            parent.Free();
        }
    }

    [TestCase]
    public void AParentlessAwardFallsBackToADirectWalletPaymentWithAttribution() {
        // The headless/no-pool fallback: the award may not be losable, so with
        // nowhere to spawn the pickup the wallet is paid directly — once — with
        // the same attribution payload the collection site would raise.
        int awards = 0;
        var attributions = new System.Collections.Generic.List<DustAwardCollectedPayload>();
        void OnDust(int amount) => awards++;
        void OnAttributed(DustAwardCollectedPayload payload) => attributions.Add(payload);
        EventBus.Instance.OnChronalDustCollected += OnDust;
        EventBus.Instance.OnDustAwardCollected += OnAttributed;
        try {
            ChronalDustPickup pickup = StoryDropSystem.SpawnDustAward(
                25, Vector2.Zero, null, DustAwardSource.Boss);
            AssertObject(pickup).IsNull();
            AssertThat(awards).IsEqual(1);
            AssertThat(attributions.Count).IsEqual(1);
            AssertThat(attributions[0].Amount).IsEqual(25);
            AssertThat(attributions[0].Source).IsEqual(DustAwardSource.Boss);
        } finally {
            EventBus.Instance.OnChronalDustCollected -= OnDust;
            EventBus.Instance.OnDustAwardCollected -= OnAttributed;
        }
    }

    // === Package 11 A10 — the F05 authored-budget reward ledger =============

    [TestCase]
    public void ABossAwardStillForcesTheLargeMilestonePlateAtTheArenaCentre() {
        // F05 §9: "Every boss still produces a Large pickup at the arena centre."
        // The 25-dust award is exactly the Large threshold, so the plate is both
        // forced AND quantity-correct — the two rules agree for a boss and only
        // for a boss.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node2D { Name = "BossAwardParent" };
        tree.Root.AddChild(parent);
        var pools = new PoolManager { Name = "BossAwardPools" };
        tree.Root.AddChild(pools);
        try {
            var arenaCentre = new Vector2(1840f, 620f);
            ChronalDustPickup pickup = StoryDropSystem.SpawnDustAward(
                25, arenaCentre, parent, DustAwardSource.Boss, "level_02.boss");

            AssertObject(pickup).IsNotNull();
            AssertThat(pickup.DustAmount).IsEqual(25);
            AssertThat(pickup.Source).IsEqual(DustAwardSource.Boss);
            AssertThat(pickup.ForceLargeTier).IsTrue();
            AssertThat(pickup.NeverExpires).IsTrue();
            AssertThat(pickup.GlobalPosition.IsEqualApprox(arenaCentre)).IsTrue();
            AssertString(pickup.SourceID).IsEqual("level_02.boss");
        } finally {
            pools.ClearAllPools();
            pools.Free();
            parent.Free();
        }
    }

    [TestCase]
    public void CollectingAPickupCommitsItsSourceClaimSoTheSourceCanNeverPayTwice() {
        // "Collection commits the source claim and the wallet increment
        // together... A restored enemy whose reward was collected may fight
        // again but awards zero additional dust."
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node2D { Name = "ClaimParent" };
        tree.Root.AddChild(parent);
        var pools = new PoolManager { Name = "ClaimPools" };
        tree.Root.AddChild(pools);
        LevelRewardDirectory.ResetAttempt();
        try {
            const string sourceID = "level_02.extractor_courtyard";
            AssertThat(LevelRewardDirectory.IsClaimed(sourceID)).IsFalse();

            ChronalDustPickup pickup = StoryDropSystem.SpawnDustAward(
                2, new Vector2(200f, 80f), parent, DustAwardSource.Extractor, sourceID);
            AssertObject(pickup).IsNotNull();
            // Spawned but uncollected: the claim is NOT committed yet, which is
            // what lets a reload restore an untouched pickup without duplicating it.
            AssertThat(LevelRewardDirectory.IsClaimed(sourceID)).IsFalse();

            pickup.Collect();
            AssertThat(LevelRewardDirectory.IsClaimed(sourceID))
                .OverrideFailureMessage("Collection must commit the source claim.")
                .IsTrue();

            // The claim rides the attempt into the save and back.
            var claimed = LevelRewardDirectory.ClaimedSourceIDs();
            AssertThat(claimed.Contains(sourceID)).IsTrue();
            LevelRewardDirectory.RestoreClaims(claimed);
            AssertThat(LevelRewardDirectory.IsClaimed(sourceID)).IsTrue();

            // A full Restart Level discards it along with the whole attempt.
            LevelRewardDirectory.ResetAttempt();
            AssertThat(LevelRewardDirectory.IsClaimed(sourceID)).IsFalse();
        } finally {
            LevelRewardDirectory.ResetAttempt();
            pools.ClearAllPools();
            pools.Free();
            parent.Free();
        }
    }

    [TestCase]
    public void TheDirectoryIssuesEachFiniteSourceOnceAndPaysRepeatableSpawnsNothing() {
        // The authored Orleans inventory: eight standard sources sharing a
        // 15-dust pool, one 25-dust boss, three machines and a discovery reward
        // sharing a 10-dust optional pool. Nothing outside that list draws.
        LevelRewardManifest manifest = FTT.Core.AuthoredResources.Load<LevelRewardManifest>(
            LevelRewardManifest.PathFor(2));
        AssertObject(manifest).IsNotNull();

        LevelRewardLedger ledger = LevelRewardDirectory.Compile(manifest, Difficulty.Normal);
        AssertObject(ledger).IsNotNull();

        int requiredPaid = 0;
        foreach (var queue in ledger.EnemyQueues) {
            foreach (string sourceID in queue.Value) requiredPaid += ledger.Award(sourceID);
        }
        AssertThat(requiredPaid)
            .OverrideFailureMessage("Orleans' authored enemy sources must pay exactly its 15-dust pool.")
            .IsEqual(15);
        AssertThat(ledger.Award(ledger.BossSourceID)).IsEqual(25);

        int optionalPaid = ledger.Award("level_02.secret");
        foreach (string extractorID in manifest.ExtractorSourceIDs) optionalPaid += ledger.Award(extractorID);
        AssertThat(optionalPaid).IsEqual(10);

        // A boss summon or any other repeatable spawn is simply not an authored
        // finite source: it resolves to nothing, and takes nothing from the pool.
        AssertThat(ledger.EnemyQueues.ContainsKey("hologram_drone")).IsFalse();
        AssertThat(ledger.Award("level_02.room1#99")).IsEqual(0);
    }

    [TestCase]
    public void TimelineCollapseRemovesExactlyTwentyPercentOfCarriedDust() {
        AssertThat(StoryManager.CalculateTimelineCollapseDust(100)).IsEqual(80);
        AssertThat(StoryManager.CalculateTimelineCollapseDust(27)).IsEqual(21);
        AssertThat(StoryManager.CalculateTimelineCollapseDust(-10)).IsEqual(0);
    }
}
