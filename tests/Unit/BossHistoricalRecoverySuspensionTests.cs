using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>A First-Unbound-shaped fixture boss, parented under a caller's host.</summary>
public static class BossSuspensionFixture {
    public static BossController CreateRecoveringBoss(Node host) {
        var data = new BossData {
            BossID = "first_unbound_suspension_under_test",
            DisplayNameKey = "boss_apex_eraser_name",
            MaxHP = 1000,
            MeleeRangeThreshold = 3f,
            RangedRangeThreshold = 8f,
            RestCooldown = 1f,
            AttackPattern = BossAttackPattern.DistanceBased,
            PhaseThresholds = new[] { 0.66f, 0.33f },
            PhaseTransitionInvincibilityDuration = 2f,
            HasHistoricalRecovery = true,
            BossAbilities = new[] {
                new EnemyAbilityData {
                    AbilityID = "boss.first_unbound_suspension_under_test.swing",
                    Archetype = EnemyAbilityArchetype.MeleeStrike,
                    RangeClass = EnemyAbilityRangeClass.Any,
                    SelectionWeight = 1f,
                    TelegraphFrames = 20,
                    ActiveFrames = 6,
                    RecoveryFrames = 10,
                    Damage = 10f
                }
            }
        };
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.SelectionSeed = 20260927;
        boss.Data = data;
        host.AddChild(boss);
        boss.ApplyData(data);
        boss.GlobalPosition = new Vector2(1400f, 850f);
        return boss;
    }
}

/// <summary>
/// Package 12 W1 — GAP-06. The First Unbound's T01b Historical Recovery beat is
/// a world-wide combat suspension (<c>TEMPORAL_STATE_CONTRACT.md</c>): the hero,
/// other actors, hazards and projectiles cannot act or deal damage; statuses,
/// lifetimes, cooldowns and gameplay clocks pause; the hero's position, velocity
/// and resources are preserved; nothing catches up at the end. Before this the
/// suspension was boss-side only — <c>OnBossHistoricalRecovery</c> had no
/// subscriber.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BossHistoricalRecoverySuspensionTests {
    private const float Step = 1f / 60f;

    [TestCase]
    public void TheBossRecoveryBeatSuspendsTheWholeWorldWithNoCatchUp() {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "BossSuspensionHost" };
        tree.Root.AddChild(host);
        StoryManager.Instance?.SetTimeFreezeCooldown(10f);
        try {
            PlayerController player = CharacterFactory.CreateCharacter("einstein", 0);
            host.AddChild(player);
            player.GlobalPosition = new Vector2(400f, 850f);
            var manager = new ChronalRewindManager { Name = "BossSuspensionManager" };
            host.AddChild(manager);
            var freeze = new TimeFreezeController { Name = "BossSuspensionTimeFreeze" };
            host.AddChild(freeze);
            freeze.SetPhysicsProcess(false);
            var hazard = new HoldFreezeProbe { Name = "SuspendedHazard" };
            host.AddChild(hazard);
            hazard.AddToGroup("story_hazard");
            var target = new Hurtbox { Name = "SuspendedTarget", OwnerPlayerIndex = -1 };
            host.AddChild(target);
            int delivered = 0;
            target.OnHit += _ => { delivered++; return 5f; };

            BossController boss = BossSuspensionFixture.CreateRecoveringBoss(host);
            for (int tick = 0; tick < 200; tick++) boss._PhysicsProcess(Step);
            var velocity = new Vector2(123f, -45f);
            player.Velocity = velocity;

            boss.TakeDamage(400);
            AssertThat(boss.IsHistoricalRecoverySuspended).IsTrue();
            AssertThat(manager.IsBossSuspensionActive)
                .OverrideFailureMessage("GAP-06: nothing answered OnBossHistoricalRecovery.")
                .IsTrue();
            AssertThat(player.IsWorldSuspended).IsTrue();
            AssertThat(hazard.IsFrozen).IsTrue();
            AssertThat(ChronalRewindManager.IsWorldHeld(tree)).IsTrue();
            AssertThat(target.TakeHit(new HitPayload { AttackerIndex = 0, Damage = 5f })).IsEqual(0f);
            AssertThat(delivered).IsEqual(0);

            // Gameplay clocks pause for the beat: the Time Freeze cooldown holds.
            for (int frame = 0; frame < 30; frame++) freeze._PhysicsProcess(Step);
            AssertThat(freeze.CooldownRemaining).IsEqual(10f);

            // The boss's own beat is the clock; the suspension ends with it.
            int guard = 0;
            while (boss.IsHistoricalRecoverySuspended && guard++ < 300) {
                boss._PhysicsProcess(Step);
                manager._PhysicsProcess(Step);
            }
            manager._PhysicsProcess(Step);
            AssertThat(boss.IsHistoricalRecoverySuspended).IsFalse();
            AssertThat(manager.IsBossSuspensionActive).IsFalse();
            AssertThat(player.IsWorldSuspended).IsFalse();
            AssertThat(hazard.IsFrozen).IsFalse();
            AssertThat(player.Velocity)
                .OverrideFailureMessage("The hero's velocity must survive the suspension untouched.")
                .IsEqual(velocity);
            AssertThat(target.TakeHit(new HitPayload { AttackerIndex = 0, Damage = 5f })).IsEqual(5f);
        } finally {
            host.Free();
            StoryManager.Instance?.SetTimeFreezeCooldown(0f);
        }
    }
}
