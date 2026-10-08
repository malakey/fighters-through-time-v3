using System;
using System.Threading.Tasks;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The adversarial review of the 2026-10-04 ENEMY workstream
/// (<c>docs/PLAYTEST_FEEL_2026-10-04_PLAN.md</c> §2.2 S1/S3, F5/F6 victim half):
/// a stun opened by a hit that wrote no knockback stops the mob instead of S1's
/// friction sliding its chase or dash speed; Ultimate-sourced stuns charge no
/// S3 standard poise; a thrown mob's bowling knockdown keeps charging a bowled
/// elite's V7.4 budget; the mob and boss victim hitstop (and the kill freeze)
/// carry F6's launch bonus exactly as the attacker's freeze does; and the
/// grab/throw latches and the A12 slam bounce never survive a kill, a pool cycle
/// or a rewind restore. The engine-free halves are pinned in
/// <c>StoryEnemyCombatRulesTests</c>; the rest of the workstream's behaviour is
/// in <c>StoryEnemyCombatTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryEnemyCombatReviewTests {
    private const float Step = 1f / 60f;

    // Far from every other suite's bodies, so no leaked player aggroes these
    // mobs and no leaked enemy is swept by the bowling test.
    private const float WorldX = 70000f;

    // === E1: S1's stunned friction decays knockback, never chase or dash speed ===

    [TestCase]
    public async Task AStunWithoutKnockbackStopsAChargingMobInsteadOfSlidingIt() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor(centerX: WorldX, width: 4000f);
            EnemyController construct = CreateEnemy("chrono_slasher", new Vector2(WorldX - 600f, 0f));
            EnemyController special = CreateEnemy("chrono_slasher", new Vector2(WorldX + 600f, 0f));
            try {
                // An impulse-free construct bite (no victim hitstop) and a
                // zero-knockback direct Special (victim hitstop first): neither
                // writes knockback, so neither stun may carry the mob's speed.
                HitPayload bite = Hit(AttackClass.Basic, "turret_shot", 1f, hitstun: 0.5f);
                bite.Delivery = HitDelivery.Construct;
                HitPayload zap = Hit(AttackClass.Special, "special_test", 1f, hitstun: 0.5f);
                foreach ((EnemyController enemy, HitPayload hit) in new[] { (construct, bite), (special, zap) }) {
                    SettleOnFloor(enemy);
                    float startX = enemy.GlobalPosition.X;
                    // A ChargeDash / chase speed in flight when the hit lands.
                    enemy.Velocity = new Vector2(600f, enemy.Velocity.Y);
                    enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(hit);
                    AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
                    AssertThat(enemy.Velocity.X)
                        .OverrideFailureMessage("A stun opened without knockback must stop the mob; vx=" + enemy.Velocity.X)
                        .IsEqualApprox(0f, 0.0001f);
                    // Pre-fix, S1's 1800 px/s^2 friction slid this 600 px/s body
                    // 100 px over these frames (stopping inside 20 of them).
                    for (int frame = 0; frame < 24; frame++) enemy._PhysicsProcess(Step);
                    AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
                    float slid = Mathf.Abs(enemy.GlobalPosition.X - startX);
                    AssertThat(slid < 0.5f)
                        .OverrideFailureMessage("S1 friction slid a zero-knockback stun " + slid + " px")
                        .IsTrue();
                }
            } finally {
                construct.Free();
                special.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task AZeroKnockbackRefreshKeepsTheSlideAnEarlierHitsKnockbackWrote() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor(centerX: WorldX, width: 4000f);
            EnemyController enemy = CreateEnemy("chrono_slasher", new Vector2(WorldX, 0f));
            try {
                SettleOnFloor(enemy);
                var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
                hurtbox.TakeHit(Finisher(damage: 1f));
                int guard = 0;
                while (enemy.HitstopFramesRemaining > 0 && guard++ < 30) enemy._PhysicsProcess(Step);
                enemy._PhysicsProcess(Step);
                float slide = enemy.Velocity.X;
                AssertThat(slide > 0f).IsTrue();

                // A construct bite refreshes the running stun; the finisher's
                // knockback keeps decaying under S1 friction rather than stopping.
                HitPayload bite = Hit(AttackClass.Basic, "turret_shot", 1f, hitstun: 0.3f);
                bite.Delivery = HitDelivery.Construct;
                hurtbox.TakeHit(bite);
                AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
                AssertThat(enemy.Velocity.X)
                    .OverrideFailureMessage("A refresh must not cancel the knockback slide.")
                    .IsEqualApprox(slide, 0.0001f);
                enemy._PhysicsProcess(Step);
                AssertThat(enemy.Velocity.X > 0f && enemy.Velocity.X < slide).IsTrue();
            } finally {
                enemy.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public void ADirectStunThatOpensStopsTheMobHorizontallyAndKeepsAGroundMobsVerticalSpeed() {
        EnemyController enemy = CreateEnemy("chrono_slasher", new Vector2(WorldX, -2000f));
        try {
            // A direct ApplyStun (a standalone Static Charge, Kinetic Splitting's
            // bounce window) writes no knockback, so a stun it OPENS on an
            // unstunned, unarmored mob stops whatever horizontal speed the mob
            // carried; a ground mob keeps its vertical speed. Kinetic Splitting
            // only reaches this path when its bounce finds a mob with neither a
            // running stun nor getup armor (a fall outlasting the spike's stun
            // plus the 36 armor frames). A body that lands inside the spike's
            // stun has it REFRESHED, which keeps the horizontal motion (the next
            // case), and one that lands inside the getup armor has the window
            // refused.
            enemy.Velocity = new Vector2(250f, -300f);
            enemy.ApplyStun(0.2f);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            AssertThat(enemy.Velocity.X).IsEqualApprox(0f, 0.0001f);
            AssertThat(enemy.Velocity.Y).IsEqualApprox(-300f, 0.0001f);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void ADirectStunThatRefreshesARunningStunKeepsTheHorizontalMotion() {
        EnemyController enemy = CreateEnemy("chrono_slasher", new Vector2(WorldX, -2000f));
        try {
            // A knockback-carrying hit opens the stun and writes the slide.
            enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(Finisher(damage: 1f));
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            float slideX = enemy.Velocity.X;
            AssertThat(slideX > 0f).IsTrue();

            // Kinetic Splitting's refresh case: a spiked body that reaches the
            // floor inside the spike's stun (the authored 12 frames, scaled by
            // resistance), the bounce writes its upward speed, and the window's
            // direct ApplyStun REFRESHES the running stun.
            float bounceY = -FTT.Characters.Abilities.LincolnSplittingStrike.GroundBounceUpwardSpeed;
            enemy.Velocity = new Vector2(slideX, bounceY);
            enemy.ApplyStun(FTT.Characters.Abilities.LincolnSplittingStrike.GroundBounceWindowFrames / 60f);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            AssertThat(enemy.Velocity.X)
                .OverrideFailureMessage("A refresh must keep the slide the earlier knockback wrote; vx=" + enemy.Velocity.X)
                .IsEqualApprox(slideX, 0.0001f);
            AssertThat(enemy.Velocity.Y).IsEqualApprox(bounceY, 0.0001f);
        } finally {
            enemy.Free();
        }
    }

    // === E2: Ultimate-sourced stuns charge no standard poise ===

    [TestCase]
    public void AFiveSmashUltimateNeverTripsAStandardsPoiseWhileAnElitesBudgetStillCharges() {
        EnemyController standard = CreateEnemy("chrono_slasher", new Vector2(WorldX, -2000f));
        EnemyController elite = CreateEnemy("steam_automaton", new Vector2(WorldX + 400f, -2000f));
        try {
            const float smashStun = 0.5f; // Lincoln's Union Indestructible: 5 x 30 frames
            AssertThat(5 * smashStun > StoryEnemyCombatRules.StandardStaggerBudgetSeconds)
                .OverrideFailureMessage("The scenario must be one that would trip a charged budget.")
                .IsTrue();
            var hurtbox = standard.GetNode<Hurtbox>("Hurtbox");
            for (int smash = 0; smash < 5; smash++) {
                hurtbox.TakeHit(Hit(AttackClass.Ultimate, "ult_test", 1f, hitstun: smashStun,
                    origin: HitOrigin.Ultimate, attackID: "lincoln.ultimate"));
                AssertThat(standard.IsArmoredRecovery)
                    .OverrideFailureMessage("An Ultimate tripped the standard's poise at smash " + (smash + 1))
                    .IsFalse();
                AssertThat(standard.CurrentState).IsEqual(EnemyState.Stunned);
                AssertThat(standard.StaggerBudgetSeconds).IsEqualApprox(0f, 0.0001f);
                for (int frame = 0; frame < 12; frame++) standard._PhysicsProcess(Step);
            }

            // An Ultimate-origin sub-hit authored on the Special class is exempt too.
            hurtbox.TakeHit(Hit(AttackClass.Special, "ult_sub", 1f, hitstun: 0.3f,
                origin: HitOrigin.Ultimate, attackID: "test.ultimate_sub"));
            AssertThat(standard.StaggerBudgetSeconds).IsEqualApprox(0f, 0.0001f);
            // The budget itself is intact: a basic still charges it.
            hurtbox.TakeHit(StringHit(0, damage: 1f));
            AssertThat(standard.StaggerBudgetSeconds > 0f).IsTrue();

            // V7.4 is unchanged for elites: an Ultimate stun still charges the
            // applied (resisted) duration.
            elite.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(AttackClass.Ultimate, "ult_test", 1f, hitstun: smashStun,
                origin: HitOrigin.Ultimate, attackID: "lincoln.ultimate"));
            AssertThat(elite.StaggerBudgetSeconds)
                .IsEqualApprox(smashStun * (1f - elite.Data.StunResistance), 0.0001f);
        } finally {
            standard.Free();
            elite.Free();
        }
    }

    // === E3: bowling an elite keeps its V7.4 budget charge ===

    [TestCase]
    public async Task AThrownStandardBowlingAnEliteChargesTheEliteBudgetButNoStandardPoise() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            var origin = new Vector2(WorldX + 10000f, -3000f);
            EnemyController thrown = CreateEnemy("chrono_slasher", origin);
            // Both stand on the throw's far side, so the thrower is behind a
            // right-facing body and no frontal reduction applies.
            EnemyController elite = CreateEnemy("steam_automaton", origin + new Vector2(20f, 0f));
            EnemyController standard = CreateEnemy("chrono_slasher", origin + new Vector2(30f, 0f));
            try {
                thrown.LaunchThrown(Vector2.Zero, bowlingDamage: 1);
                AssertThat(thrown.IsThrownFlight).IsTrue();
                thrown._PhysicsProcess(Step);

                AssertThat(elite.CurrentState).IsEqual(EnemyState.Stunned);
                AssertThat(elite.StaggerBudgetSeconds)
                    .OverrideFailureMessage("A bowled elite must keep its V7.4 budget charge (the resisted stun).")
                    .IsEqualApprox(1.0f * (1f - elite.Data.StunResistance), 0.0001f);
                AssertThat(standard.CurrentState).IsEqual(EnemyState.Stunned);
                AssertThat(standard.StaggerBudgetSeconds)
                    .OverrideFailureMessage("A bowling knockdown spends no standard poise.")
                    .IsEqualApprox(0f, 0.0001f);
                // The bowling knockback is knockback: the knockdown keeps it.
                AssertThat(elite.Velocity.X > 0f).IsTrue();
                AssertThat(standard.Velocity.X > 0f).IsTrue();
            } finally {
                thrown.Free();
                elite.Free();
                standard.Free();
            }
        });
    }

    // === E4: the victim freeze carries the attacker's launch bonus ===

    [TestCase]
    public void ALaunchingHitFreezesAMobABossAndACorpseForTheAttackersLaunchAwareFrames() {
        EnemyController enemy = CreateEnemy("chrono_slasher", new Vector2(WorldX, -2000f));
        EnemyController corpse = CreateEnemy("chrono_slasher", new Vector2(WorldX + 400f, -2000f));
        BossController boss = CreateBoss(new Vector2(WorldX + 800f, -2000f));
        try {
            HitPayload finisher = Finisher(damage: 1f);
            AssertThat(finisher.Launches).IsTrue();

            int dealt = Mathf.RoundToInt(enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(finisher));
            AssertThat(dealt > 0).IsTrue();
            // PlayerController.OnMeleeHitConfirmed freezes the attacker for
            // HitstopFrames(dealt, payload.Launches): the victim must match it.
            AssertThat(enemy.HitstopFramesRemaining).IsEqual(BasicComboRules.HitstopFrames(dealt, launches: true));
            AssertThat(enemy.HitstopFramesRemaining > BasicComboRules.HitstopFrames(dealt, launches: false))
                .IsTrue();

            int bossDealt = Mathf.RoundToInt(boss.GetNode<Hurtbox>("Hurtbox").TakeHit(finisher));
            AssertThat(bossDealt > 0).IsTrue();
            AssertThat(boss.HitstopFramesRemaining).IsEqual(BasicComboRules.HitstopFrames(bossDealt, launches: true));

            int lethal = Mathf.RoundToInt(corpse.GetNode<Hurtbox>("Hurtbox").TakeHit(Finisher(damage: 1000f)));
            AssertThat(corpse.CurrentState).IsEqual(EnemyState.Dead);
            AssertThat(corpse.IsInKillFreeze).IsTrue();
            AssertThat(corpse.HitstopFramesRemaining).IsEqual(BasicComboRules.HitstopFrames(lethal, launches: true));
        } finally {
            enemy.Free();
            corpse.Free();
            boss.Free();
        }
    }

    // === E5: grab/throw latches and the slam bounce never outlive their body ===

    [TestCase]
    public void APoolCycleClearsAThrownFlightAHeldGrabAndAnOwedSlamBounce() {
        EnemyController enemy = CreateEnemy("chrono_slasher", new Vector2(WorldX, -2000f));
        try {
            enemy.LaunchThrown(new Vector2(200f, -300f), bowlingDamage: 3);
            AssertThat(enemy.IsThrownFlight).IsTrue();
            enemy.OnDespawn();
            enemy.OnSpawn();
            AssertThat(enemy.IsThrownFlight)
                .OverrideFailureMessage("A recycled body must not spawn mid-flight and bowl its neighbours.")
                .IsFalse();
            AssertThat(enemy.IsGrabbable).IsTrue();

            enemy.BeginHeld();
            AssertThat(enemy.IsHeldByPlayer).IsTrue();
            enemy.OnDespawn();
            enemy.OnSpawn();
            AssertThat(enemy.IsHeldByPlayer).IsFalse();

            enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(DownAirSlam());
            AssertThat(enemy.SlamBouncePending).IsTrue();
            enemy.OnDespawn();
            enemy.OnSpawn();
            AssertThat(enemy.SlamBouncePending)
                .OverrideFailureMessage("A recycled body must not bounce off its first floor contact.")
                .IsFalse();
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void AThrowWhoseDamageKilledTheMobLeavesNoFlightOnTheCorpse() {
        EnemyController enemy = CreateEnemy("chrono_slasher", new Vector2(WorldX, -2000f));
        try {
            // PlayerController resolves the throw's damage, then launches.
            enemy.TakeDamage(10000);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Dead);
            enemy.LaunchThrown(new Vector2(300f, -200f), bowlingDamage: 5);
            AssertThat(enemy.IsThrownFlight)
                .OverrideFailureMessage("A corpse must not latch a thrown flight it will never fly.")
                .IsFalse();
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void ARewindRestoreClearsTheGrabLatchesAndAnOwedSlamBounce() {
        EnemyController enemy = CreateEnemy("chrono_slasher", new Vector2(WorldX, -2000f));
        try {
            enemy.RewindPolicy = StoryRewindPolicy.ResetToInitialState;
            enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(DownAirSlam());
            AssertThat(enemy.SlamBouncePending).IsTrue();
            enemy.ApplyStoryRewind();
            AssertThat(enemy.SlamBouncePending)
                .OverrideFailureMessage("The restored body owes no bounce from the abandoned timeline.")
                .IsFalse();

            enemy.LaunchThrown(new Vector2(200f, -300f), bowlingDamage: 3);
            AssertThat(enemy.IsThrownFlight).IsTrue();
            enemy.ApplyStoryRewind();
            AssertThat(enemy.IsThrownFlight).IsFalse();
            AssertThat(enemy.IsGrabbable).IsTrue();
        } finally {
            enemy.Free();
        }
    }

    // === Helpers ===

    private static HitPayload Hit(
        AttackClass attackClass, string hitboxID, float damage, float hitstun,
        HitOrigin origin = HitOrigin.Basic, string attackID = "test.hit") => new() {
            AttackerIndex = 0,
            AttackID = attackID,
            HitboxID = hitboxID,
            AttackClass = attackClass,
            Origin = attackClass == AttackClass.Special && origin == HitOrigin.Basic ? HitOrigin.Special : origin,
            Delivery = HitDelivery.DirectHit,
            Damage = damage,
            HitstunDuration = hitstun,
            // Far behind a right-facing mob, so no frontal reduction can apply.
            HitOrigin = new Vector2(-100000f, 0f),
            AttackerFacingRight = true
        };

    private static HitPayload StringHit(int step, float damage) => new() {
        AttackerIndex = 0,
        AttackID = "einstein.basic",
        HitboxID = $"combo_{step + 1}",
        AttackClass = AttackClass.Basic,
        Origin = HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = damage,
        HitstunDuration = BasicComboRules.HitstunFrames[step] / 60f,
        Launches = BasicComboRules.StringHitLaunches[step],
        HitOrigin = new Vector2(-100000f, 0f),
        AttackerFacingRight = true
    };

    /// <summary>Joan's authored finisher shape: 3.5 base x 4.5 forward, -2 x 4.5 up.</summary>
    private static HitPayload Finisher(float damage) {
        HitPayload hit = StringHit(2, damage);
        hit.Knockback = new Vector2(3.5f * BasicComboRules.KnockbackMultipliers[2], -2f * BasicComboRules.KnockbackMultipliers[2]);
        return hit;
    }

    /// <summary>A Down-Air spike that drives the mob straight down (A12).</summary>
    private static HitPayload DownAirSlam() => new() {
        AttackerIndex = 0,
        AttackID = "einstein.basic",
        HitboxID = BasicComboRules.DownAirHitboxID,
        AttackClass = AttackClass.Basic,
        Origin = HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = 1f,
        HitstunDuration = 0.5f,
        Launches = true,
        Knockback = new Vector2(0f, 6f),
        HitOrigin = new Vector2(-100000f, 0f),
        AttackerFacingRight = true
    };

    private static void SettleOnFloor(EnemyController enemy) {
        enemy.Data.Behavior = DefaultBehavior.StandGuard;
        for (int frame = 0; frame < 8 && !enemy.IsOnFloor(); frame++) enemy._PhysicsProcess(Step);
        AssertThat(enemy.IsOnFloor()).IsTrue();
    }

    private static EnemyController CreateEnemy(string enemyID, Vector2 position) {
        EnemyData canonical = FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        return enemy;
    }

    private static BossController CreateBoss(Vector2 position) {
        var data = new BossData {
            BossID = "test_boss",
            DisplayNameKey = "boss_borgia_inquisitor_name",
            MaxHP = 500,
            MeleeRangeThreshold = 3f,
            RangedRangeThreshold = 8f,
            RestCooldown = 1f,
            AttackPattern = BossAttackPattern.DistanceBased,
            PhaseThresholds = Array.Empty<float>(),
            BossAbilities = Array.Empty<EnemyAbilityData>()
        };
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.SelectionSeed = 2026;
        boss.Data = data;
        boss.Position = position;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }

    /// <summary>An Environment floor whose top surface is y = 0.</summary>
    private static StaticBody2D CreateFloor(float centerX, float width) {
        var floor = new StaticBody2D {
            Name = "StoryEnemyCombatReviewFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(width, 40f) },
            Position = new Vector2(centerX, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }
}
