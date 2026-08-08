using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 4 Section 3.1: the shared telegraph -> active -> recovery driver used by
/// both elite abilities and boss attacks. Frame accounting is exact at 60 Hz.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EnemyAbilityExecutorTests {
    private const float Step = 1f / 60f;

    [TestCase]
    public void PhaseFrameAccountingMatchesAuthoredTelegraphActiveRecovery() {
        (Node2D owner, EnemyAbilityExecutor executor, Hitbox hitbox, AnimatedSprite2D sprite) = CreateSubject();
        var ability = MeleeAbility(telegraph: 10, active: 4, recovery: 6);

        AssertThat(ability.TotalFrames).IsEqual(20);
        executor.Begin(ability, new Vector2(200f, 0f), facingRight: true);
        AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Telegraph);
        AssertThat(hitbox.IsActive).IsFalse();

        for (int frame = 0; frame < 9; frame++) executor.Tick(Step);
        AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Telegraph);

        executor.Tick(Step);
        AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Active);
        AssertThat(hitbox.IsActive).IsTrue();

        for (int frame = 0; frame < 4; frame++) executor.Tick(Step);
        AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Recovery);
        AssertThat(hitbox.IsActive).IsFalse();

        for (int frame = 0; frame < 6; frame++) executor.Tick(Step);
        AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Idle);
        AssertThat(executor.IsBusy).IsFalse();

        AssertObject(sprite).IsNotNull();
        Cleanup(owner, ability);
    }

    [TestCase]
    public void TelegraphTintsTheSpriteAndRestoresItWhenTheHitboxGoesLive() {
        (Node2D owner, EnemyAbilityExecutor executor, Hitbox hitbox, AnimatedSprite2D sprite) = CreateSubject();
        executor.SetBaseModulate(Colors.White);
        var ability = MeleeAbility(telegraph: 3, active: 2, recovery: 1);
        ability.TelegraphTint = new Color(1f, 0f, 0f);

        executor.Begin(ability, new Vector2(120f, 0f), facingRight: true);
        AssertThat(sprite.Modulate).IsEqual(new Color(1f, 0f, 0f));
        for (int frame = 0; frame < 3; frame++) executor.Tick(Step);
        AssertThat(sprite.Modulate).IsEqual(Colors.White);
        AssertThat(hitbox.IsActive).IsTrue();

        Cleanup(owner, ability);
    }

    [TestCase]
    public void MeleeHitboxMirrorsWithFacingAndScalesDamageByTheDifficultyMultiplier() {
        (Node2D owner, EnemyAbilityExecutor executor, Hitbox hitbox, _) = CreateSubject();
        executor.DamageMultiplier = 1.5f;
        var ability = MeleeAbility(telegraph: 0, active: 4, recovery: 0);
        ability.Damage = 10f;
        ability.HitboxOffset = new Vector2(40f, -30f);

        executor.Begin(ability, new Vector2(-200f, 0f), facingRight: false);
        AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Active);
        AssertThat(hitbox.Position.X).IsEqualApprox(-40f, 0.001f);
        AssertThat(hitbox.Damage).IsEqualApprox(15f, 0.001f);
        AssertThat(hitbox.KnockbackForce.X < 0f).IsTrue();

        Cleanup(owner, ability);
    }

    [TestCase]
    public void NegativeAuthoredKnockbackPullsTheTargetBackTowardTheCaster() {
        (Node2D owner, EnemyAbilityExecutor executor, Hitbox hitbox, _) = CreateSubject();
        var ability = MeleeAbility(telegraph: 0, active: 4, recovery: 0);
        ability.KnockbackForce = new Vector2(-4f, -1f);
        AssertThat(ability.IsPullKnockback).IsTrue();

        executor.Begin(ability, new Vector2(300f, 0f), facingRight: true);

        // Caster faces right, so a pull must shove the target left (back inward).
        AssertThat(hitbox.KnockbackForce.X < 0f).IsTrue();
        Cleanup(owner, ability);
    }

    [TestCase]
    public void AreaPulseUsesARadialHitboxCenteredOnTheCaster() {
        (Node2D owner, EnemyAbilityExecutor executor, Hitbox hitbox, _) = CreateSubject();
        var ability = MeleeAbility(telegraph: 0, active: 3, recovery: 0);
        ability.AbilityID = "test.pulse";
        ability.Archetype = EnemyAbilityArchetype.AreaPulse;
        ability.PulseRadius = 100f;
        ability.HitboxOffset = new Vector2(40f, -30f);

        executor.Begin(ability, new Vector2(-500f, 0f), facingRight: false);

        AssertThat(hitbox.Position.X).IsEqualApprox(0f, 0.001f);
        var shape = (RectangleShape2D)hitbox.GetNode<CollisionShape2D>("CollisionShape2D").Shape;
        AssertThat(shape.Size).IsEqual(new Vector2(200f, 200f));
        Cleanup(owner, ability);
    }

    [TestCase]
    public void ProjectileArchetypeSpawnsPooledShotsInTheEnemyProjectileGroup() {
        (PoolManager pools, Node parent) = CreatePools();
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject(parent);
        var ability = MeleeAbility(telegraph: 0, active: 3, recovery: 0);
        ability.AbilityID = "test.volley";
        ability.Archetype = EnemyAbilityArchetype.Projectile;
        ability.ProjectileCount = 3;
        ability.ProjectileSpreadDegrees = 20f;
        ability.ProjectileSpeed = 300f;

        executor.Begin(ability, owner.GlobalPosition + new Vector2(400f, 0f), facingRight: true);

        IReadOnlyList<Node> active = pools.GetActiveNodes(EnemyAbilityExecutor.ProjectilePoolID);
        AssertThat(active.Count).IsEqual(3);
        foreach (Node node in active) {
            AssertThat(node.IsInGroup(EnemyProjectile.GroupName)).IsTrue();
            AssertThat(((EnemyProjectile)node).Velocity.X > 0f).IsTrue();
        }

        owner.Free();
        CleanupPools(pools, parent);
    }

    [TestCase]
    public void PooledProjectilesFullyResetAcrossRepeatedSpawnReleaseCycles() {
        (PoolManager pools, Node parent) = CreatePools();
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject(parent);
        var ability = MeleeAbility(telegraph: 0, active: 2, recovery: 0);
        ability.AbilityID = "test.single";
        ability.Archetype = EnemyAbilityArchetype.Projectile;
        ability.ProjectileCount = 1;

        for (int cycle = 0; cycle < 12; cycle++) {
            executor.Begin(ability, owner.GlobalPosition + new Vector2(200f, 0f), facingRight: true);
            IReadOnlyList<Node> active = pools.GetActiveNodes(EnemyAbilityExecutor.ProjectilePoolID);
            AssertThat(active.Count).IsEqual(1);
            var projectile = (EnemyProjectile)active[0];
            AssertThat(projectile.LifetimeRemaining > 0f).IsTrue();
            pools.Release(projectile);
            AssertThat(projectile.Velocity).IsEqual(Vector2.Zero);
            AssertThat(projectile.LifetimeRemaining).IsEqual(0f);
            AssertThat(projectile.SourceID).IsEqual("");
        }

        AssertThat(pools.GetStats(EnemyAbilityExecutor.ProjectilePoolID).Value.Active).IsEqual(0);
        owner.Free();
        CleanupPools(pools, parent);
    }

    [TestCase]
    public void SummonArchetypeSpawnsMinionsThroughTheSharedStandardEnemyPool() {
        (PoolManager pools, Node parent) = CreatePools();
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject(parent);
        var ability = MeleeAbility(telegraph: 0, active: 2, recovery: 0);
        ability.AbilityID = "test.summon";
        ability.Archetype = EnemyAbilityArchetype.SummonMinions;
        ability.SummonEnemyID = "chrono_slasher";
        ability.SummonCount = 2;

        executor.Begin(ability, owner.GlobalPosition, facingRight: true);

        AssertThat(pools.GetActiveNodes(EnemyFactory.StandardPoolID).Count).IsEqual(2);
        owner.Free();
        CleanupPools(pools, parent);
    }

    [TestCase]
    public void ShieldBubbleHoldsItsReductionForTheAuthoredDurationThenExpires() {
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject();
        var ability = MeleeAbility(telegraph: 0, active: 2, recovery: 0);
        ability.AbilityID = "test.shield";
        ability.Archetype = EnemyAbilityArchetype.ShieldBubble;
        ability.ShieldDuration = 0.1f;
        ability.ShieldDamageReduction = 0.5f;

        executor.Begin(ability, owner.GlobalPosition, facingRight: true);
        AssertThat(executor.HasActiveShield).IsTrue();
        AssertThat(executor.ShieldDamageReduction).IsEqualApprox(0.5f, 0.0001f);

        for (int frame = 0; frame < 8; frame++) executor.Tick(Step);
        AssertThat(executor.HasActiveShield).IsFalse();

        Cleanup(owner, ability);
    }

    [TestCase]
    public void ChargeDashPublishesADirectionalVelocityOnlyWhileActive() {
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject();
        var ability = MeleeAbility(telegraph: 2, active: 4, recovery: 2);
        ability.AbilityID = "test.dash";
        ability.Archetype = EnemyAbilityArchetype.ChargeDash;
        ability.DashSpeed = 600f;
        ability.DashDurationFrames = 5;

        executor.Begin(ability, owner.GlobalPosition + new Vector2(-300f, 0f), facingRight: false);
        AssertThat(executor.DashVelocity).IsEqual(Vector2.Zero);
        executor.Tick(Step);
        executor.Tick(Step);
        AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Active);
        AssertThat(executor.DashVelocity.X).IsEqualApprox(-600f, 0.001f);

        for (int frame = 0; frame < 5; frame++) executor.Tick(Step);
        AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Recovery);
        AssertThat(executor.DashVelocity).IsEqual(Vector2.Zero);

        Cleanup(owner, ability);
    }

    [TestCase]
    public void TeleportRepositionsPastTheTargetInsideTheAuthoredRange() {
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject();
        owner.GlobalPosition = new Vector2(0f, 0f);
        var ability = MeleeAbility(telegraph: 0, active: 2, recovery: 0);
        ability.AbilityID = "test.teleport";
        ability.Archetype = EnemyAbilityArchetype.Teleport;
        ability.TeleportRangeMin = 100f;
        ability.TeleportRangeMax = 200f;

        executor.Begin(ability, new Vector2(400f, 0f), facingRight: true);

        float offset = owner.GlobalPosition.X - 400f;
        AssertThat(offset >= 100f && offset <= 200f).IsTrue();
        Cleanup(owner, ability);
    }

    [TestCase]
    public void InterruptedTelegraphSkipsThePayloadAndKeepsTheRecoveryCost() {
        (Node2D owner, EnemyAbilityExecutor executor, Hitbox hitbox, _) = CreateSubject();
        var ability = MeleeAbility(telegraph: 20, active: 6, recovery: 8);

        executor.Begin(ability, new Vector2(150f, 0f), facingRight: true);
        executor.Tick(Step);
        executor.CancelIntoRecovery();

        AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Recovery);
        AssertThat(hitbox.IsActive).IsFalse();
        for (int frame = 0; frame < 8; frame++) executor.Tick(Step);
        AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Idle);

        Cleanup(owner, ability);
    }

    // === Helpers ===

    private static EnemyAbilityData MeleeAbility(int telegraph, int active, int recovery) => new() {
        AbilityID = "test.melee",
        Archetype = EnemyAbilityArchetype.MeleeStrike,
        RangeClass = EnemyAbilityRangeClass.Melee,
        TelegraphFrames = telegraph,
        ActiveFrames = active,
        RecoveryFrames = recovery,
        Damage = 10f,
        KnockbackForce = new Vector2(3f, -1.5f),
        HitboxSize = new Vector2(40f, 40f),
        HitboxOffset = new Vector2(30f, -25f)
    };

    private static (Node2D owner, EnemyAbilityExecutor executor, Hitbox hitbox, AnimatedSprite2D sprite)
        CreateSubject(Node parent = null) {
        var owner = new Node2D { Name = "AbilityOwner" };
        var sprite = new AnimatedSprite2D { Name = "AnimatedSprite2D" };
        owner.AddChild(sprite);
        var hitbox = new Hitbox { Name = "Hitbox", OwnerPlayerIndex = -1 };
        hitbox.AddChild(new CollisionShape2D {
            Name = "CollisionShape2D",
            Shape = new RectangleShape2D { Size = new Vector2(40f, 40f) }
        });
        owner.AddChild(hitbox);

        Node target = parent ?? ((SceneTree)Engine.GetMainLoop()).Root;
        target.AddChild(owner);

        var executor = new EnemyAbilityExecutor(owner) { SourceID = "test_source" };
        executor.Bind(sprite, hitbox, null);
        return (owner, executor, hitbox, sprite);
    }

    private static (PoolManager pools, Node parent) CreatePools() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node { Name = "ExecutorPoolParent" };
        tree.Root.AddChild(parent);
        var pools = new PoolManager { Name = "ExecutorPoolManager" };
        tree.Root.AddChild(pools);
        return (pools, parent);
    }

    private static void CleanupPools(PoolManager pools, Node parent) {
        pools.ClearAllPools();
        pools.Free();
        parent.Free();
    }

    /// <summary>
    /// Frees the owner node only. The EnemyAbilityData is RefCounted and Godot's
    /// reference counting owns it; Dispose()ing a Godot Resource double-disposes
    /// its C# script instance and corrupts the heap. See AGENTS.md.
    /// </summary>
    private static void Cleanup(Node2D owner, EnemyAbilityData ability) => owner.Free();
}
