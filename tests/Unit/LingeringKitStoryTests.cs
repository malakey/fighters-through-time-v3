using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The lingering-kit pass's Story half of KL-RULES: Venom's 3 HP a second on the hero, a mob and a
/// boss; the healing block (every heal but Defy's, with the Rally pool kept); the boss slow floor;
/// and the x0.4 hero slow from the three <c>.tres</c> files.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LingeringKitStoryTests {
    private const double Step = 1.0 / 60.0;

    // === Venom damage =========================================================

    [TestCase]
    public void VenomDealsThreeHPASecondToTheHero() {
        PlayerController player = Hero();
        try {
            int before = player.CurrentHP;
            player.ApplyStatusEffect(StatusType.Venom, 4f, 1f);
            for (int frame = 0; frame < 61; frame++) player.GetNode<StatusController>("StatusController")._PhysicsProcess(Step);
            AssertThat(before - player.CurrentHP).IsEqual(3);
        } finally {
            Free(player);
        }
    }

    [TestCase]
    public void VenomDealsThreeHPASecondToAMobAndABoss() {
        EnemyData canonical = AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_slasher.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        BossController boss = null;
        try {
            enemy.CurrentHP = 500;
            enemy.ApplyStatusEffect(StatusType.Venom, 4f, 1f);
            for (int frame = 0; frame < 61; frame++) enemy._PhysicsProcess(Step);
            AssertThat(500 - enemy.CurrentHP)
                .OverrideFailureMessage("A mob's Venom tick is 3 HP at intensity 1.")
                .IsEqual(3);

            boss = Boss();
            int bossBefore = boss.CurrentHP;
            boss.ApplyStatusEffect(StatusType.Venom, 4f, 1f);
            for (int frame = 0; frame < 61; frame++) boss._PhysicsProcess(Step);
            AssertThat(bossBefore - boss.CurrentHP)
                .OverrideFailureMessage("A boss's Venom tick is 3 HP at intensity 1.")
                .IsEqual(3);
        } finally {
            enemy.Free();
            boss?.Free();
        }
    }

    // === the healing block ====================================================

    [TestCase]
    public void VenomBlocksEveryStoryHealButDefys() {
        PlayerController player = Hero();
        try {
            player.CurrentHP = 40;
            AssertThat(player.HealStory(10)).OverrideFailureMessage("Control: an unpoisoned heal lands.").IsEqual(10);

            player.ApplyStatusEffect(StatusType.Venom, 30f, 1f);
            AssertThat(player.IsHealingBlocked).IsTrue();
            int hp = player.CurrentHP;
            AssertThat(player.HealStory(10)).IsEqual(0);
            AssertThat(player.CurrentHP).IsEqual(hp);

            // A healing orb routes through HealStory.
            var orb = new ChronalOrbItem { Data = new ChronalOrbData { Effect = OrbEffect.HPRestore, Value = 25f } };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(orb);
            try {
                orb.OnPickedUp(player);
                AssertThat(player.CurrentHP).OverrideFailureMessage("Venom must block the healing orb.").IsEqual(hp);
            } finally {
                orb.Free();
            }

            // Defy's heal is the one exception.
            AssertThat(player.HealStory(10, isDefyHeal: true)).IsEqual(10);
            AssertThat(player.CurrentHP).IsEqual(hp + 10);
        } finally {
            Free(player);
        }
    }

    [TestCase]
    public void ABlockedRallyReclaimBurnsNoPoolAndDefyStillCashesItIn() {
        PlayerController player = Hero();
        try {
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = 1,
                TargetIndex = 0,
                AttackID = "test.hit",
                HitboxID = "primary",
                AttackClass = AttackClass.Basic,
                Damage = 40f,
                HitOrigin = new Vector2(-20f, 0f),
                AttackerFacingRight = true
            });
            float pool = player.EchoPool;
            AssertThat(pool > 0f).IsTrue();
            player.ApplyStatusEffect(StatusType.Venom, 30f, 1f);
            int hp = player.CurrentHP;

            player.AddInfluenceFromDamageDealt(4f);
            AssertThat(player.CurrentHP).OverrideFailureMessage("Venom must block the Rally reclaim.").IsEqual(hp);
            AssertThat(player.EchoPool)
                .OverrideFailureMessage("A blocked reclaim burns no pool.")
                .IsEqual(pool);

            // Zealous Vigor's fraction reclaim is gated before its debit as well.
            player.ReclaimRallyEchoFraction(0.25f);
            AssertThat(player.EchoPool).IsEqual(pool);
            AssertThat(player.CurrentHP).IsEqual(hp);

            // Defy cashes the pool in and still heals.
            int healed = player.CashInEchoPool();
            AssertThat(healed > 0).OverrideFailureMessage("Defy still heals under Venom.").IsTrue();
            AssertThat(player.CurrentHP).IsEqual(hp + healed);
        } finally {
            Free(player);
        }
    }

    // === the slow =============================================================

    [TestCase]
    public void AHeroSlowFromTheResourcesIsFortyPercentMoveAndJump() {
        string[] paths = {
            "res://resources/Abilities/einstein/special_2.tres",
            "res://resources/Abilities/cleopatra/special_2.tres",
            "res://resources/Abilities/shakespeare/special_1.tres"
        };
        foreach (string path in paths) {
            AbilityData data = AuthoredResources.Load<AbilityData>(path);
            AssertThat(data.AppliedStatus).OverrideFailureMessage(path).IsEqual(StatusType.TimeDilation);
            AssertThat(data.StatusIntensity).OverrideFailureMessage(path).IsEqual(1.2f);
            AssertThat(data.StatusDuration).OverrideFailureMessage(path).IsEqual(1.75f);

            PlayerController player = Hero();
            try {
                player.ApplyStatusEffect(data.AppliedStatus, data.StatusDuration, data.StatusIntensity);
                AssertThat(player.StatusMovementMultiplier).OverrideFailureMessage(path).IsEqualApprox(0.4f, 0.0001f);
                AssertThat(player.StatusJumpMultiplier).OverrideFailureMessage(path).IsEqualApprox(0.4f, 0.0001f);
            } finally {
                Free(player);
            }
        }
    }

    [TestCase]
    public void ABossSlowNeverDropsBelowSeventyPercent() {
        BossController boss = Boss();
        try {
            boss.ApplyStatusEffect(StatusType.TimeDilation, 1.75f, 1.2f);
            AssertThat(boss.StatusMoveMultiplier).IsEqualApprox(0.7f, 0.0001f);
            boss.ClearAllStatusEffects();
            boss.ApplyStatusEffect(StatusType.TimeDilation, 1.75f, 1f);
            AssertThat(boss.StatusMoveMultiplier).IsEqualApprox(0.7f, 0.0001f);
            // A hero is not floored: the same intensity gives x0.4 there.
        } finally {
            boss.Free();
        }
    }

    // === helpers ==============================================================

    private static PlayerController Hero() {
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        return player;
    }

    private static void Free(PlayerController player) {
        InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        player.Free();
    }

    private static BossController Boss() {
        var data = new BossData {
            BossID = "lingering_scratch_boss",
            DisplayNameKey = "boss_borgia_inquisitor_name",
            MaxHP = 500,
            MoveSpeed = 4f,
            MeleeRangeThreshold = 3f,
            RangedRangeThreshold = 8f,
            RestCooldown = 1f,
            AttackPattern = BossAttackPattern.DistanceBased,
            PhaseThresholds = System.Array.Empty<float>(),
            BossAbilities = System.Array.Empty<EnemyAbilityData>()
        };
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.SelectionSeed = 4242;
        boss.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }
}
