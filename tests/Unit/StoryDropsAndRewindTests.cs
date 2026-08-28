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
            AssertThat(pickup.ForceLargeTier).IsTrue();
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
                50, Vector2.Zero, null, DustAwardSource.Boss);
            AssertObject(pickup).IsNull();
            AssertThat(awards).IsEqual(1);
            AssertThat(attributions.Count).IsEqual(1);
            AssertThat(attributions[0].Amount).IsEqual(50);
            AssertThat(attributions[0].Source).IsEqual(DustAwardSource.Boss);
        } finally {
            EventBus.Instance.OnChronalDustCollected -= OnDust;
            EventBus.Instance.OnDustAwardCollected -= OnAttributed;
        }
    }

    [TestCase]
    public void TimelineCollapseRemovesExactlyTwentyPercentOfCarriedDust() {
        AssertThat(StoryManager.CalculateTimelineCollapseDust(100)).IsEqual(80);
        AssertThat(StoryManager.CalculateTimelineCollapseDust(27)).IsEqual(21);
        AssertThat(StoryManager.CalculateTimelineCollapseDust(-10)).IsEqual(0);
    }
}
