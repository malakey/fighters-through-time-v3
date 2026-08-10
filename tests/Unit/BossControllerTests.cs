using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 4 Section 3.4: boss weighted selection, distance filtering with the
/// no-deadlock fallback, phase thresholds, interruption, and knockback immunity.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BossControllerTests {
    private const float Step = 1f / 60f;
    private const float MeleeDistance = 60f;   // 1 unit, inside a 3-unit melee threshold
    private const float RangedDistance = 360f; // 6 units, outside it

    [TestCase]
    public void SeededWeightedSelectionFollowsTheAuthoredWeightDistribution() {
        EnemyAbilityData heavy = Ability("a.heavy", EnemyAbilityRangeClass.Any, weight: 3f);
        EnemyAbilityData light = Ability("a.light", EnemyAbilityRangeClass.Any, weight: 1f);
        BossController boss = CreateBoss(BossResource(heavy, light), seed: 4242);
        try {
            int heavyCount = 0;
            const int rolls = 2000;
            for (int roll = 0; roll < rolls; roll++) {
                if (boss.SelectAbilityIndex(MeleeDistance) == 0) heavyCount++;
            }
            float share = heavyCount / (float)rolls;
            AssertThat(share > 0.68f && share < 0.82f).IsTrue();
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void DistanceFilteringPicksMeleeUpCloseAndRangedFarAway() {
        EnemyAbilityData melee = Ability("a.melee", EnemyAbilityRangeClass.Melee, weight: 1f);
        EnemyAbilityData ranged = Ability("a.ranged", EnemyAbilityRangeClass.Ranged, weight: 1f);
        BossController boss = CreateBoss(BossResource(melee, ranged), seed: 99);
        try {
            for (int roll = 0; roll < 40; roll++) {
                AssertThat(boss.SelectAbilityIndex(MeleeDistance)).IsEqual(0);
                AssertThat(boss.SelectAbilityIndex(RangedDistance)).IsEqual(1);
            }
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void AnEmptyDistanceFilterFallsBackToTheFullSetSoTheBossNeverDeadlocks() {
        EnemyAbilityData meleeOnly = Ability("a.only_melee", EnemyAbilityRangeClass.Melee, weight: 1f);
        BossController boss = CreateBoss(BossResource(meleeOnly), seed: 7);
        try {
            // Nothing is Ranged-class, yet a far-away player must still get an attack.
            AssertThat(boss.SelectAbilityIndex(RangedDistance)).IsEqual(0);
            AssertThat(boss.SelectAbilityIndex(MeleeDistance)).IsEqual(0);
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void NoAuthoredAbilitiesReportsNoSelectionInsteadOfThrowing() {
        BossController boss = CreateBoss(BossResource(), seed: 3);
        try {
            AssertThat(boss.SelectAbilityIndex(MeleeDistance)).IsEqual(-1);
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void PhaseGatedAbilitiesOnlyBecomeSelectableFromTheirUnlockPhase() {
        EnemyAbilityData opener = Ability("a.opener", EnemyAbilityRangeClass.Any, weight: 1f);
        EnemyAbilityData finisher = Ability("a.finisher", EnemyAbilityRangeClass.Any, weight: 50f);
        BossData data = BossResource(opener, finisher);
        data.AbilityMinPhase = new[] { 0, 1 };
        data.PhaseThresholds = new[] { 0.5f };
        BossController boss = CreateBoss(data, seed: 11);
        try {
            for (int roll = 0; roll < 50; roll++) {
                AssertThat(boss.SelectAbilityIndex(MeleeDistance)).IsEqual(0);
            }
            boss.CurrentPhase = 1;
            bool sawFinisher = false;
            for (int roll = 0; roll < 50; roll++) {
                if (boss.SelectAbilityIndex(MeleeDistance) == 1) sawFinisher = true;
            }
            AssertThat(sawFinisher).IsTrue();
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void CrossingOneThresholdAdvancesExactlyOnePhaseAndOpensTheInvincibilityWindow() {
        EnemyAbilityData melee = Ability("a.melee", EnemyAbilityRangeClass.Melee, weight: 1f);
        BossData data = BossResource(melee);
        data.MaxHP = 100;
        data.PhaseThresholds = new[] { 0.5f, 0.25f };
        data.PhaseTransitionInvincibilityDuration = 1f;
        BossController boss = CreateBoss(data, seed: 5);
        try {
            int maximum = boss.ScaledMaxHP;
            int firstHit = Mathf.RoundToInt(maximum * 0.6f);
            boss.TakeDamage(firstHit);
            AssertThat(boss.CurrentHP).IsEqual(maximum - firstHit);
            AssertThat(boss.CurrentPhase).IsEqual(1);
            AssertThat(boss.CurrentState).IsEqual(BossState.PhaseTransitioning);
            AssertThat(boss.IsPhaseInvincible).IsTrue();
            // Transition invincibility: further hits deal nothing until it ends.
            boss.TakeDamage(10);
            AssertThat(boss.CurrentHP).IsEqual(maximum - firstHit);
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void ASingleHitCrossingTwoThresholdsAdvancesBothPhasesWithoutSkippingOrDoubleFiring() {
        EnemyAbilityData melee = Ability("a.melee", EnemyAbilityRangeClass.Melee, weight: 1f);
        BossData data = BossResource(melee);
        data.MaxHP = 100;
        data.PhaseThresholds = new[] { 0.5f, 0.25f };
        BossController boss = CreateBoss(data, seed: 5);
        int phaseEvents = 0;
        void OnPhase(int phase) => phaseEvents++;
        EventBus.Instance.OnBossPhaseChanged += OnPhase;
        try {
            int maximum = boss.ScaledMaxHP;
            boss.TakeDamage(Mathf.RoundToInt(maximum * 0.8f));
            AssertThat(boss.CurrentPhase).IsEqual(2);
            AssertThat(phaseEvents).IsEqual(2);
            AssertThat(boss.CurrentState).IsEqual(BossState.PhaseTransitioning);
        } finally {
            EventBus.Instance.OnBossPhaseChanged -= OnPhase;
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void PhaseSpeedMultipliersAreResolvedPerPhaseWithANeutralDefault() {
        BossData data = BossResource();
        data.PhaseSpeedMultipliers = new[] { 1.0f, 1.35f };
        AssertThat(data.GetPhaseSpeedMultiplier(0)).IsEqualApprox(1.0f, 0.0001f);
        AssertThat(data.GetPhaseSpeedMultiplier(1)).IsEqualApprox(1.35f, 0.0001f);
        AssertThat(data.GetPhaseSpeedMultiplier(5)).IsEqualApprox(1.0f, 0.0001f);
    }

    [TestCase]
    public void KnockbackImmuneBossesStillTakeHealthDamageButAreNeverShoved() {
        BossData data = BossResource();
        data.IsKnockbackImmune = true;
        BossController boss = CreateBoss(data, seed: 1);
        try {
            int maximum = boss.ScaledMaxHP;
            boss.TakeDamage(50);
            AssertThat(boss.CurrentHP).IsEqual(maximum - 50);
            boss.ApplyKnockback(new Vector2(10f, -6f), attackerFacingRight: true);
            AssertThat(boss.Velocity).IsEqual(Vector2.Zero);
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void NonImmuneBossesAcceptKnockback() {
        BossData data = BossResource();
        data.IsKnockbackImmune = false;
        BossController boss = CreateBoss(data, seed: 1);
        try {
            boss.ApplyKnockback(new Vector2(10f, -6f), attackerFacingRight: true);
            AssertThat(boss.Velocity.X > 0f).IsTrue();
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void InterruptibleTelegraphsCancelIntoRecoveryOnceTheDamageThresholdIsMet() {
        EnemyAbilityData melee = Ability("a.slow", EnemyAbilityRangeClass.Melee, weight: 1f);
        melee.TelegraphFrames = 40;
        melee.RecoveryFrames = 10;
        BossData data = BossResource(melee);
        data.PhaseThresholds = Array.Empty<float>();
        data.InterruptibleDuringTelegraph = true;
        data.InterruptDamageThreshold = 25f;
        BossController boss = CreateBoss(data, seed: 2);
        try {
            boss.BeginAbility(melee, new Vector2(200f, 0f));
            AssertThat(boss.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);

            boss.TakeDamage(10);
            AssertThat(boss.LastTelegraphInterrupted).IsFalse();
            AssertThat(boss.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);

            boss.TakeDamage(30);
            AssertThat(boss.LastTelegraphInterrupted).IsTrue();
            AssertThat(boss.AbilityPhase).IsEqual(EnemyAbilityPhase.Recovery);
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void NonInterruptibleBossesRideOutTheirTelegraph() {
        EnemyAbilityData melee = Ability("a.slow", EnemyAbilityRangeClass.Melee, weight: 1f);
        melee.TelegraphFrames = 40;
        BossData data = BossResource(melee);
        data.PhaseThresholds = Array.Empty<float>();
        data.InterruptibleDuringTelegraph = false;
        BossController boss = CreateBoss(data, seed: 2);
        try {
            boss.BeginAbility(melee, new Vector2(200f, 0f));
            boss.TakeDamage(90);
            AssertThat(boss.LastTelegraphInterrupted).IsFalse();
            AssertThat(boss.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void BossesUseTheSameOneSlotStatusSemanticsAsEnemies() {
        BossData data = BossResource();
        BossController boss = CreateBoss(data, seed: 8);
        try {
            boss.ApplyStatusEffect(StatusType.TimeDilation, 2f, 1f);
            AssertThat(boss.ActiveStatusType).IsEqual(StatusType.TimeDilation);
            AssertThat(boss.StatusMoveMultiplier).IsEqualApprox(0.5f, 0.0001f);

            // Newest status completely replaces the previous one; Root still stops a
            // knockback-immune boss because movement denial is not HP damage.
            boss.ApplyStatusEffect(StatusType.Root, 2f, 1f);
            AssertThat(boss.ActiveStatusType).IsEqual(StatusType.Root);
            AssertThat(boss.StatusMoveMultiplier).IsEqualApprox(0f, 0.0001f);
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void DefeatDisablesCollisionAndRaisesTheDefeatPayloadOnce() {
        BossData data = BossResource();
        data.MaxHP = 80;
        data.ChronalDustDrop = 50;
        BossController boss = CreateBoss(data, seed: 6);
        int defeats = 0;
        BossDefeatedPayload captured = default;
        void OnDefeated(BossDefeatedPayload payload) { defeats++; captured = payload; }
        EventBus.Instance.OnBossDefeated += OnDefeated;
        try {
            boss.TakeDamage(boss.ScaledMaxHP + 50);
            AssertThat(boss.CurrentState).IsEqual(BossState.Dead);
            AssertThat(boss.CollisionLayer).IsEqual(0u);
            AssertThat(defeats).IsEqual(1);
            AssertThat(captured.ChronalDustDrop).IsEqual(50);
            boss.TakeDamage(50);
            AssertThat(defeats).IsEqual(1);
        } finally {
            EventBus.Instance.OnBossDefeated -= OnDefeated;
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void ReactionDelayIsScaledByStoryDifficultyAndNeverDropsBelowOneFrame() {
        AssertThat(StoryDifficultyTuning.ScaleReactionDelayFrames(20, Difficulty.Easy)).IsEqual(30);
        AssertThat(StoryDifficultyTuning.ScaleReactionDelayFrames(20, Difficulty.Normal)).IsEqual(20);
        AssertThat(StoryDifficultyTuning.ScaleReactionDelayFrames(20, Difficulty.Hard)).IsEqual(12);
        AssertThat(StoryDifficultyTuning.ScaleReactionDelayFrames(1, Difficulty.Hard)).IsEqual(1);
        AssertThat(StoryDifficultyTuning.ScaleReactionDelayFrames(0, Difficulty.Easy)).IsEqual(1);
    }

    [TestCase]
    public void FlorenceBossIsAuthoredWithMeleeAndRangedCoverageAndAPhaseTwoSpeedUp() {
        BossData boss = FTT.Core.AuthoredResources.Load<BossData>("res://resources/Bosses/borgia_inquisitor.tres");
        AssertObject(boss).IsNotNull();
        AssertThat(boss.BossAbilities.Length).IsEqual(2);
        AssertThat(boss.PhaseThresholds.Length).IsEqual(1);
        AssertThat(boss.GetPhaseSpeedMultiplier(1) > 1f).IsTrue();
        AssertThat(boss.AttackPattern).IsEqual(BossAttackPattern.DistanceBased);

        bool hasMelee = false;
        bool hasRanged = false;
        foreach (EnemyAbilityData ability in boss.BossAbilities) {
            AssertObject(ability).IsNotNull();
            AssertThat(ability.HasValidArchetypeFields()).IsTrue();
            if (ability.RangeClass is EnemyAbilityRangeClass.Melee or EnemyAbilityRangeClass.Any) hasMelee = true;
            if (ability.RangeClass is EnemyAbilityRangeClass.Ranged or EnemyAbilityRangeClass.Any) hasRanged = true;
        }
        AssertThat(hasMelee).IsTrue();
        AssertThat(hasRanged).IsTrue();
    }

    [TestCase]
    public void RestWindowFollowsAnExecutedAttackAndBypassesAfterAPhaseTransition() {
        EnemyAbilityData melee = Ability("a.quick", EnemyAbilityRangeClass.Any, weight: 1f);
        melee.TelegraphFrames = 1;
        melee.ActiveFrames = 1;
        melee.RecoveryFrames = 1;
        BossData data = BossResource(melee);
        data.MaxHP = 100;
        data.PhaseThresholds = new[] { 0.5f };
        data.PhaseTransitionInvincibilityDuration = 0.05f;
        BossController boss = CreateBoss(data, seed: 13);
        try {
            boss.TakeDamage(Mathf.RoundToInt(boss.ScaledMaxHP * 0.6f));
            AssertThat(boss.CurrentState).IsEqual(BossState.PhaseTransitioning);
            for (int frame = 0; frame < 6; frame++) boss._PhysicsProcess(Step);
            // The rest cooldown is bypassed: the boss never enters a rest window.
            AssertThat(boss.CurrentState).IsNotEqual(BossState.PhaseTransitioning);
            AssertThat(boss.CurrentState).IsNotEqual(BossState.RestWindow);
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void RestWindowTrackingHoldsAtTheMeleeBandStandOffAndNeverShovesThePlayer() {
        // Chase already stops short (it attacks from EngagementRangePixels, the full
        // authored band); the shove risk is rest-window tracking, which previously
        // crept into the player without a floor. The stand-off is keyed to the
        // boss's shortest-range attack band: MeleeRangeThreshold (3u -> 180 px).
        EnemyAbilityData swing = Ability("a.rest_standoff", EnemyAbilityRangeClass.Any, weight: 1f);
        BossController boss = CreateBoss(BossResource(swing), seed: 21);
        StaticBody2D floor = CreateStandOffFloor();
        PlayerController player = CreateTargetPlayer(new Vector2(100f, 0f));
        try {
            // 100 px is inside the 85% engage line (153 px): tracking must hold.
            bool sawRestWindow = false;
            for (int frame = 0; frame < 300; frame++) {
                boss._PhysicsProcess(Step);
                if (boss.CurrentState == BossState.RestWindow) {
                    sawRestWindow = true;
                    AssertThat(boss.Velocity.X).IsEqualApprox(0f, 0.001f);
                }
            }
            AssertThat(sawRestWindow).IsTrue();
            AssertThat(boss.GlobalPosition.X).IsEqualApprox(0f, 0.01f);
            AssertThat(player.GlobalPosition).IsEqual(new Vector2(100f, 0f));

            // Beyond the 110% release line (198 px) rest tracking still closes in,
            // but re-latches at the band instead of reaching pushbox contact.
            player.GlobalPosition = new Vector2(300f, 0f);
            for (int frame = 0; frame < 400; frame++) boss._PhysicsProcess(Step);
            AssertThat(boss.GlobalPosition.X > 50f).IsTrue();
            AssertThat(boss.GlobalPosition.DistanceTo(player.GlobalPosition) > 140f).IsTrue();
            AssertThat(player.GlobalPosition).IsEqual(new Vector2(300f, 0f));
        } finally {
            player.Free();
            floor.Free();
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void DeadBossesAreNotResurrectedByARewindRestore() {
        // Audit M-6: Die() zeroed collision/hurtbox/pushbox and already raised the
        // defeat payload with its dust; a rewind restore must skip the corpse
        // rather than stand up an invulnerable ghost whose re-kill double-pays.
        BossData data = BossResource();
        data.MaxHP = 80;
        BossController boss = CreateBoss(data, seed: 21);
        boss.RewindPolicy = StoryRewindPolicy.ResetToInitialState;
        try {
            boss.TakeDamage(boss.ScaledMaxHP);
            AssertThat(boss.CurrentState).IsEqual(BossState.Dead);

            boss.ApplyStoryRewind();

            AssertThat(boss.CurrentState).IsEqual(BossState.Dead);
            AssertThat(boss.CurrentHP).IsEqual(0);
            AssertThat(boss.CollisionLayer).IsEqual(0u);
        } finally {
            FreeBoss(boss);
        }
    }

    [TestCase]
    public void ChargeDashTravelObeysTheStatusMoveMultiplier() {
        // Audit Low: the controller wrote the raw executor DashVelocity, so a
        // Rooted boss still crossed the arena at full dash speed.
        EnemyAbilityData dash = Ability("a.status_dash", EnemyAbilityRangeClass.Any, weight: 1f);
        dash.Archetype = EnemyAbilityArchetype.ChargeDash;
        dash.TelegraphFrames = 1;
        dash.ActiveFrames = 30;
        dash.DashSpeed = 600f;
        dash.DashDurationFrames = 30;
        BossController boss = CreateBoss(BossResource(dash), seed: 9);
        try {
            AssertThat(boss.BeginAbility(dash, new Vector2(300f, 0f))).IsTrue();
            boss.TickAbility(Step); // telegraph -> active
            AssertThat(boss.AbilityPhase).IsEqual(EnemyAbilityPhase.Active);
            AssertThat(boss.StatusScaledDashVelocityX).IsEqualApprox(600f, 0.001f);

            boss.ApplyStatusEffect(StatusType.Root, 2f, 1f);
            AssertThat(boss.StatusScaledDashVelocityX).IsEqualApprox(0f, 0.0001f);

            boss.ApplyStatusEffect(StatusType.TimeDilation, 2f, 1f);
            AssertThat(boss.StatusScaledDashVelocityX).IsEqualApprox(300f, 0.001f);
        } finally {
            FreeBoss(boss);
        }
    }

    // === Helpers ===

    /// <summary>
    /// A bare PlayerController (no hurtbox, so boss attacks cannot knock it
    /// around) carrying the factory-shaped pushbox, registered in "Players" as
    /// the boss's target. It never ticks its own physics in a synchronous test,
    /// so any position change is displacement caused by the boss.
    /// </summary>
    private static PlayerController CreateTargetPlayer(Vector2 position) {
        var player = new PlayerController { Name = "BossStandOffTargetPlayer", Position = position };
        var pushbox = new CombatantPushbox {
            Name = "Pushbox",
            BoxSize = new Vector2(30f, 48f),
            Position = new Vector2(0f, -28f),
            CollisionLayer = CollisionLayers.Player,
            CollisionMask = CollisionLayers.Enemy,
            Monitoring = false,
            Monitorable = false
        };
        pushbox.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = pushbox.BoxSize } });
        player.AddChild(pushbox);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        player.AddToGroup("Players");
        return player;
    }

    /// <summary>Environment-layer floor with its top surface at y = 0 so the boss
    /// (feet on the node origin) stands still while a test drives frames.</summary>
    private static StaticBody2D CreateStandOffFloor() {
        var floor = new StaticBody2D {
            Name = "BossStandOffFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    private static EnemyAbilityData Ability(string id, EnemyAbilityRangeClass rangeClass, float weight) => new() {
        AbilityID = id,
        Archetype = EnemyAbilityArchetype.MeleeStrike,
        RangeClass = rangeClass,
        SelectionWeight = weight,
        TelegraphFrames = 6,
        ActiveFrames = 4,
        RecoveryFrames = 6,
        Damage = 10f,
        HitboxSize = new Vector2(60f, 60f),
        HitboxOffset = new Vector2(50f, -40f)
    };

    private static BossData BossResource(params EnemyAbilityData[] abilities) => new() {
        BossID = "test_boss",
        DisplayNameKey = "boss_borgia_inquisitor_name",
        MaxHP = 500,
        MeleeRangeThreshold = 3f,
        RangedRangeThreshold = 8f,
        RestCooldown = 1f,
        AttackPattern = BossAttackPattern.DistanceBased,
        PhaseThresholds = Array.Empty<float>(),
        BossAbilities = abilities
    };

    private static BossController CreateBoss(BossData data, ulong seed) {
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.SelectionSeed = seed;
        boss.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }

    /// <summary>
    /// Frees the boss node only. Its BossData and the EnemyAbilityData it owns are
    /// RefCounted: Godot's reference counting frees them, and calling Dispose() on
    /// them here would double-dispose the C# script instance and corrupt the heap.
    /// Never Dispose() a Godot Resource - see AGENTS.md.
    /// </summary>
    private static void FreeBoss(BossController boss) => boss.Free();
}
