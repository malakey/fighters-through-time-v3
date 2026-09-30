using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W7a (S01/S02) — The Tempest in Story is a windbox, not a hit: over
/// its 12 active frames it shoves a caught opponent ~3 units (180 px) outward,
/// with no damage, no stun and no meter for anyone, and it never moves a target
/// in a grab state, an Ultimate cinematic, or a knockback-immune boss.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryTempestWindboxTests {
    private const double Step = 1.0 / 60.0;
    private static readonly Vector2 Origin = new(-50000f, 600f);

    [TestCase]
    public void TheStormShovesTheEnemyThreeUnitsWithNoDamageStunOrMeter() {
        var host = new Node2D { Name = "TempestWindbox" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            PlayerController shakespeare = CharacterFactory.CreateCharacter("shakespeare", 0, applyStoryProgression: false);
            host.AddChild(shakespeare);
            shakespeare.GlobalPosition = Origin;
            EnemyController enemy = CreateEnemy("chrono_slasher", host, Origin + new Vector2(80f, 0f));
            int hpBefore = enemy.CurrentHP;
            EnemyState stateBefore = enemy.CurrentState;
            var meter = shakespeare.GetNode<UltimateMeter>("UltimateMeter");
            float meterBefore = meter.CurrentValue;

            var tempest = shakespeare.GetNode<ShakespeareTheTempest>("Special2");
            AssertThat(tempest.Data.StartupFrames).IsEqual(8);
            AssertThat(tempest.Data.ActiveFrames).IsEqual(KitMotionRules.TempestPushFrames);
            AssertThat(tempest.TryExecute()).IsTrue();
            for (int frame = 0; frame < 40 && tempest.IsExecuting; frame++) tempest._PhysicsProcess(Step);

            float pushed = enemy.GlobalPosition.X - (Origin.X + 80f);
            AssertThat(Mathf.Abs(pushed - 180f) < 1f)
                .OverrideFailureMessage($"Expected a ~180 px shove, got {pushed}.")
                .IsTrue();
            AssertThat(enemy.CurrentHP).IsEqual(hpBefore);
            AssertThat(enemy.CurrentState).IsEqual(stateBefore);
            AssertThat(meter.CurrentValue).IsEqual(meterBefore);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void GrabStatesUltimateCinematicsAndImmovableBossesAreNeverPushed() {
        var host = new Node2D { Name = "TempestExclusions" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            PlayerController target = CharacterFactory.CreateCharacter("joan", 1, applyStoryProgression: false);
            host.AddChild(target);
            AssertThat(ShakespeareTheTempest.CanBePushed(target)).IsTrue();
            target.TransitionTo(CharacterState.Grabbing);
            AssertThat(ShakespeareTheTempest.CanBePushed(target)).IsFalse();
            target.TransitionTo(CharacterState.UsingUltimate);
            AssertThat(ShakespeareTheTempest.CanBePushed(target)).IsFalse();

            // Never entered into the tree: the rule reads only the authored data.
            var boss = new BossController { Data = new BossData { IsKnockbackImmune = true } };
            try {
                AssertThat(ShakespeareTheTempest.CanBePushed(boss)).IsFalse();
                boss.Data = new BossData { IsKnockbackImmune = false };
                AssertThat(ShakespeareTheTempest.CanBePushed(boss)).IsTrue();
            } finally {
                boss.Free();
            }
        } finally {
            host.Free();
        }
    }

    private static EnemyController CreateEnemy(string enemyID, Node host, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        // Positioned before entering the tree, so the physics server registers
        // its hurtbox where the shape queries will look.
        enemy.Position = position;
        host.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        return enemy;
    }
}
