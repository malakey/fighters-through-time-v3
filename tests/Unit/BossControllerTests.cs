using System;
using FTT.Core;
using FTT.Enemies;
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
            heavy.Dispose();
            light.Dispose();
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
            melee.Dispose();
            ranged.Dispose();
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
            meleeOnly.Dispose();
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
            opener.Dispose();
            finisher.Dispose();
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
            melee.Dispose();
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
            melee.Dispose();
        }
    }

    [TestCase]
    public void PhaseSpeedMultipliersAreResolvedPerPhaseWithANeutralDefault() {
        BossData data = BossResource();
        data.PhaseSpeedMultipliers = new[] { 1.0f, 1.35f };
        try {
            AssertThat(data.GetPhaseSpeedMultiplier(0)).IsEqualApprox(1.0f, 0.0001f);
            AssertThat(data.GetPhaseSpeedMultiplier(1)).IsEqualApprox(1.35f, 0.0001f);
            AssertThat(data.GetPhaseSpeedMultiplier(5)).IsEqualApprox(1.0f, 0.0001f);
        } finally {
            data.Dispose();
        }
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
            melee.Dispose();
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
            melee.Dispose();
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
        BossData boss = ResourceLoader.Load<BossData>("res://resources/Bosses/borgia_inquisitor.tres");
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
            melee.Dispose();
        }
    }

    // === Helpers ===

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

    private static void FreeBoss(BossController boss) {
        BossData data = boss.Data;
        boss.Free();
        data?.Dispose();
    }
}
