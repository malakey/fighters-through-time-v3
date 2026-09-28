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
    public void EnemyShotsUseTheCentralProjectileMaskAndDespawnOnTerrain() {
        // Audit Low: the hand-rolled mask dropped Environment, contradicting
        // CollisionLayers.ProjectileMask, and no enemy shot despawned on terrain —
        // ranged mobs hit through walls in room-based levels.
        (PoolManager pools, Node parent) = CreatePools();
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject(parent);
        var ability = MeleeAbility(telegraph: 0, active: 3, recovery: 0);
        ability.AbilityID = "test.wall_shot";
        ability.Archetype = EnemyAbilityArchetype.Projectile;
        ability.ProjectileCount = 1;
        ability.ProjectileSpeed = 300f;

        executor.Begin(ability, owner.GlobalPosition + new Vector2(400f, 0f), facingRight: true);
        var projectile = (EnemyProjectile)pools.GetActiveNodes(EnemyAbilityExecutor.ProjectilePoolID)[0];
        var hitbox = projectile.GetNode<Hitbox>("Hitbox");
        AssertThat(hitbox.CollisionMask).IsEqual(CollisionLayers.ProjectileMask);
        AssertThat((hitbox.CollisionMask & CollisionLayers.Environment) != 0u).IsTrue();

        // A body on a non-Environment layer is ignored (a construct, say)...
        var construct = new StaticBody2D { CollisionLayer = CollisionLayers.PersistentObject };
        parent.AddChild(construct);
        projectile.HandleBodyContact(construct);
        AssertThat(pools.GetStats(EnemyAbilityExecutor.ProjectilePoolID).Value.Active).IsEqual(1);

        // ...while terrain contact returns the shot to its pool.
        var wall = new StaticBody2D { CollisionLayer = CollisionLayers.Environment };
        parent.AddChild(wall);
        projectile.HandleBodyContact(wall);
        AssertThat(pools.GetStats(EnemyAbilityExecutor.ProjectilePoolID).Value.Active).IsEqual(0);

        owner.Free();
        CleanupPools(pools, parent);
    }

    [TestCase]
    public void EvenPiercingShotsStopAtAWall() {
        // Piercing is about targets, not terrain: a shot that passes through
        // fighters still cannot leave the room.
        (PoolManager pools, Node parent) = CreatePools();
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject(parent);
        var ability = MeleeAbility(telegraph: 0, active: 3, recovery: 0);
        ability.AbilityID = "test.pierce_wall_shot";
        ability.Archetype = EnemyAbilityArchetype.Projectile;
        ability.ProjectileCount = 1;
        ability.PiercesTargets = true;

        executor.Begin(ability, owner.GlobalPosition + new Vector2(400f, 0f), facingRight: true);
        var projectile = (EnemyProjectile)pools.GetActiveNodes(EnemyAbilityExecutor.ProjectilePoolID)[0];
        AssertThat(projectile.PiercesTargets).IsTrue();

        var wall = new StaticBody2D { CollisionLayer = CollisionLayers.Environment };
        parent.AddChild(wall);
        projectile.HandleBodyContact(wall);
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
        // Package 12 W2 (GAP-04): every minion carries summon provenance, so its
        // death can never draw an authored encounter's finite reward.
        foreach (Node minion in pools.GetActiveNodes(EnemyFactory.StandardPoolID)) {
            AssertThat(minion is EnemyController { IsSummoned: true })
                .OverrideFailureMessage("A SummonMinions spawn must be marked IsSummoned.")
                .IsTrue();
        }
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

    // === V7.3 Chrono-Warden rework: PersistentFieldAtTarget ===

    [TestCase]
    public void PersistentFieldSpawnsAtTheCapturedTargetPositionAndLivesTheAuthoredSeconds() {
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject();
        var ability = MeleeAbility(telegraph: 0, active: 2, recovery: 0);
        ability.AbilityID = "test.field";
        ability.Archetype = EnemyAbilityArchetype.PersistentFieldAtTarget;
        ability.PulseRadius = 170f;
        ability.FieldDurationSeconds = 4f;
        ability.AppliedStatus = StatusType.TimeDilation;
        ability.StatusDuration = 2.5f;

        var target = new Vector2(555f, -40f);
        executor.Begin(ability, target, facingRight: true);

        var field = owner.GetParent().GetNodeOrNull<DilationFieldZone>("DilationFieldZone");
        try {
            AssertObject(field)
                .OverrideFailureMessage("The archetype must spawn a DilationFieldZone.")
                .IsNotNull();
            // The CAPTURED cast position, not the caster or the live target.
            AssertThat(field.GlobalPosition).IsEqual(target);
            AssertThat(field.RadiusPixels).IsEqualApprox(170f, 0.001f);
            AssertThat(field.LifeSeconds).IsEqualApprox(4f, 0.001f);
            AssertThat(field.IsInGroup("persistent_construct")).IsTrue();

            // The rewind/scrub world freeze pauses the life timer.
            field.SetStoryRewindFrozen(true);
            for (int frame = 0; frame < 30; frame++) field._PhysicsProcess(Step);
            AssertThat(field.LifeSeconds).IsEqualApprox(4f, 0.001f);
            field.SetStoryRewindFrozen(false);

            // 4 s of life, then the zone removes itself.
            for (int frame = 0; frame < 239; frame++) field._PhysicsProcess(Step);
            AssertThat(field.IsQueuedForDeletion()).IsFalse();
            for (int frame = 0; frame < 3; frame++) field._PhysicsProcess(Step);
            AssertThat(field.IsQueuedForDeletion()).IsTrue();
        } finally {
            if (field != null && GodotObject.IsInstanceValid(field)) field.Free();
            owner.Free();
        }
    }

    [TestCase]
    public void TheFieldRefreshesTheSlowWhileThePlayerStaysInsideAndStopsWhenTheyLeave() {
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject();
        var ability = MeleeAbility(telegraph: 0, active: 2, recovery: 0);
        ability.AbilityID = "test.field_refresh";
        ability.Archetype = EnemyAbilityArchetype.PersistentFieldAtTarget;
        ability.PulseRadius = 170f;
        ability.FieldDurationSeconds = 30f;
        ability.AppliedStatus = StatusType.TimeDilation;
        ability.StatusDuration = 0.5f;

        var player = FTT.Characters.CharacterFactory.CreateCharacter("einstein");
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        var status = player.GetNode<StatusController>("StatusController");
        executor.Begin(ability, player.GlobalPosition, facingRight: true);
        var field = owner.GetParent().GetNodeOrNull<DilationFieldZone>("DilationFieldZone");
        try {
            AssertObject(field).IsNotNull();

            // 1.0 s inside a field applying a 0.5 s status: only the per-frame
            // refresh keeps it alive past its own duration.
            for (int frame = 0; frame < 60; frame++) {
                field._PhysicsProcess(Step);
                status._PhysicsProcess(Step);
            }
            AssertThat(status.HasStatus(StatusType.TimeDilation))
                .OverrideFailureMessage("The Slow must be refreshed while the player stands inside.")
                .IsTrue();

            // Walking out ends the refresh; the last application runs out.
            player.GlobalPosition = field.GlobalPosition + new Vector2(400f, 0f);
            for (int frame = 0; frame < 45; frame++) {
                field._PhysicsProcess(Step);
                status._PhysicsProcess(Step);
            }
            AssertThat(status.HasStatus(StatusType.TimeDilation))
                .OverrideFailureMessage("Outside the radius the Slow must expire on its own timer.")
                .IsFalse();
        } finally {
            if (field != null && GodotObject.IsInstanceValid(field)) field.Free();
            player.Free();
            owner.Free();
        }
    }

    // === V7.3 dual-channel telegraph: the class glyph ===

    [TestCase]
    public void TheTelegraphGlyphShowsTheClassShapeAndHidesWhenTheTelegraphEnds() {
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject();
        var ability = MeleeAbility(telegraph: 6, active: 2, recovery: 2);
        try {
            // Basic: open circle, coloured like the tint channel.
            executor.Begin(ability, new Vector2(100f, 0f), facingRight: true);
            AssertObject(executor.Glyph).IsNotNull();
            AssertThat(executor.Glyph.Visible).IsTrue();
            AssertThat(executor.Glyph.Shape).IsEqual(TelegraphGlyphShape.Basic);
            AssertThat(executor.Glyph.GlyphColor).IsEqual(ability.TelegraphTint);

            // Telegraph ends -> the glyph hides with the tint restore.
            for (int frame = 0; frame < 6; frame++) executor.Tick(Step);
            AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Active);
            AssertThat(executor.Glyph.Visible).IsFalse();
            for (int frame = 0; frame < 4; frame++) executor.Tick(Step);

            // Guard-Crush: diamond. Unblockable: X. Same flags as the tint, so
            // the two channels cannot disagree.
            executor.Begin(ability, new Vector2(100f, 0f), facingRight: true, guardCrush: true);
            AssertThat(executor.Glyph.Shape).IsEqual(TelegraphGlyphShape.GuardCrush);
            executor.Cancel();
            executor.Begin(ability, new Vector2(100f, 0f), facingRight: true,
                guardCrush: true, unblockable: true);
            AssertThat(executor.Glyph.Shape).IsEqual(TelegraphGlyphShape.Unblockable);
            AssertThat(executor.Glyph.Visible).IsTrue();
            executor.Cancel();
            AssertThat(executor.Glyph.Visible)
                .OverrideFailureMessage("A hard cancel must hide the glyph with the tint.")
                .IsFalse();

            AssertThat(EnemyAbilityExecutor.ResolveGlyphShape(false, false))
                .IsEqual(TelegraphGlyphShape.Basic);
            AssertThat(EnemyAbilityExecutor.ResolveGlyphShape(true, false))
                .IsEqual(TelegraphGlyphShape.GuardCrush);
            AssertThat(EnemyAbilityExecutor.ResolveGlyphShape(true, true))
                .IsEqual(TelegraphGlyphShape.Unblockable);
        } finally {
            Cleanup(owner, ability);
        }
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

    // === V7.6 F14 (Package 11 A7a): the Null Lance's Basic-class override ===

    [TestCase]
    public void TheNullLanceCarriesSuppressionOntoItsProjectileAndStaysBasicClass() {
        (PoolManager pools, Node parent) = CreatePools();
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject(parent);
        var lance = FTT.Core.AuthoredResources.Load<EnemyAbilityData>(
            "res://resources/Enemies/Abilities/unbound_eraser/null_lance.tres");
        try {
            // Basic-class means the caller passes guardCrush: false even though
            // this is an elite signature ability - EnemyController.ResolveGuardCrush
            // is what makes that decision, and it is pinned in EnemyControllerTests.
            executor.Begin(lance, owner.GlobalPosition + new Vector2(200f, 0f),
                facingRight: true, guardCrush: false, unblockable: false);
            for (int frame = 0; frame < lance.TelegraphFrames; frame++) executor.Tick(1f / 60f);
            AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Active);

            EnemyProjectile bolt = null;
            Godot.Collections.Array<Node> children = parent.GetChildren();
            using var childrenLifetime = children.AsDisposable();
            foreach (Node child in children) if (child is EnemyProjectile found) { bolt = found; break; }
            AssertObject(bolt)
                .OverrideFailureMessage("The Null Lance must put a real pooled bolt on the field.")
                .IsNotNull();

            var hitbox = bolt.GetNode<FTT.Combat.Hitbox>("Hitbox");
            AssertThat(hitbox.AppliedStatus)
                .OverrideFailureMessage("The lance's whole point is the Suppression rider.")
                .IsEqual(StatusType.Suppression);
            AssertFloat(hitbox.StatusDuration).IsEqual(2f);
            // Basic class costs ONE charge: BlockChargeCost 0 defers to the class
            // default, which BlockRules.ChargeCost resolves to 1 for Basic.
            AssertThat(hitbox.BlockChargeCost)
                .OverrideFailureMessage("A Guard-Crush lance would author 2 here; Basic defers to the class.")
                .IsEqual(0);
            AssertThat(FTT.Combat.BlockRules.ChargeCost(hitbox.AttackClass, 3)).IsEqual(1);
            AssertThat(hitbox.Unblockable).IsFalse();
        } finally {
            owner.Free();
            CleanupPools(pools, parent);
        }
    }

    [TestCase]
    public void ABasicClassTelegraphDrawsTheWhiteYellowCircleNotTheGuardCrushDiamond() {
        (Node2D owner, EnemyAbilityExecutor executor, _, AnimatedSprite2D sprite) = CreateSubject();
        var lance = FTT.Core.AuthoredResources.Load<EnemyAbilityData>(
            "res://resources/Enemies/Abilities/unbound_eraser/null_lance.tres");
        try {
            executor.SetBaseModulate(Colors.White);
            executor.Begin(lance, owner.GlobalPosition + new Vector2(200f, 0f),
                facingRight: true, guardCrush: false, unblockable: false);

            // Shape channel: the promise reads without colour vision.
            AssertObject(executor.Glyph).IsNotNull();
            AssertThat(executor.Glyph.Shape).IsEqual(TelegraphGlyphShape.Basic);
            AssertThat(executor.Glyph.Accent)
                .OverrideFailureMessage("Only the Siphon Snare carries the tether accent.")
                .IsEqual(TelegraphGlyphAccent.None);

            // Colour channel: the authored cold-white tint, inside the Basic
            // white/yellow family rather than the Guard-Crush orange.
            AssertThat(sprite.Modulate).IsEqual(lance.TelegraphTint);
            AssertThat(sprite.Modulate.B >= 0.8f && sprite.Modulate.R >= 0.8f)
                .OverrideFailureMessage($"The lance tip is cold-white; {sprite.Modulate} is not.")
                .IsTrue();
            AssertThat(EnemyAbilityExecutor.ResolveGlyphShape(guardCrush: true, unblockable: false))
                .IsEqual(TelegraphGlyphShape.GuardCrush);
        } finally {
            owner.Free();
        }
    }

    [TestCase]
    public void TheSiphonTelegraphAddsATetherAccentAndDrawsItsThreeUnitBoundary() {
        (Node2D owner, EnemyAbilityExecutor executor, _, _) = CreateSubject();
        var snare = FTT.Core.AuthoredResources.Load<EnemyAbilityData>(
            "res://resources/Enemies/Abilities/unbound_eraser/siphon_snare.tres");
        try {
            executor.Begin(snare, owner.GlobalPosition, facingRight: true);

            // The accent is ADDITIVE: the Basic circle stays, because a shield
            // still answers the cast for one charge.
            AssertObject(executor.Glyph).IsNotNull();
            AssertThat(executor.Glyph.Shape).IsEqual(TelegraphGlyphShape.Basic);
            AssertThat(executor.Glyph.Accent).IsEqual(TelegraphGlyphAccent.Tether);
            AssertFloat(executor.Glyph.BoundaryRadiusPixels)
                .OverrideFailureMessage("The telegraph must draw the real 3-unit attachment circle.")
                .IsEqual(snare.PulseRadius);

            // And it clears with the telegraph rather than lingering.
            executor.Cancel();
            AssertThat(executor.Glyph.Visible).IsFalse();
        } finally {
            owner.Free();
        }
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
