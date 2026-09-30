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
/// Package 13 W7a (L03) — the Story Clockwork Turret fires a real straight bolt
/// (a construct-class projectile at the authored 12 units/s) only at a target in
/// line of sight: a wall between it and the only enemy leaves it waiting with
/// its ammunition untouched.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryTurretLineOfSightTests {
    private static readonly Vector2 Origin = new(-62000f, 600f);

    [TestCase]
    public void TheTurretFiresABoltOnlyWithTheTargetInLineOfSight() {
        AbilityData data = AuthoredResources.Load<AbilityData>("res://resources/Abilities/leonardo/special_2.tres");
        AssertThat(data.ProjectileSpeed).IsEqual(KitMotionRules.TurretBoltSpeedUnits * KitMotionRules.StoryPixelsPerUnit);

        // Clear line: one commanded bolt leaves as a projectile and spends one of four.
        var open = new Node2D { Name = "TurretClearShot" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(open);
        try {
            PlayerController leonardo = AddLeonardo(open);
            LeonardoTurretNode turret = AddTurret(open, data, leonardo);
            CreateEnemy("chrono_slasher", open, Origin + new Vector2(300f, -20f));
            AssertThat(turret.TryCommandBolt()).IsTrue();
            AssertThat(turret.BoltsFired).IsEqual(1);
            AssertThat(turret.BoltsRemaining).IsEqual(3);
        } finally {
            open.Free();
        }

        // A wall between them: no line of sight, so it cannot fire and spends nothing.
        var walled = new Node2D { Name = "TurretBlockedShot" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(walled);
        try {
            var wall = new StaticBody2D {
                Name = "Wall",
                CollisionLayer = CollisionLayers.Environment,
                Position = Origin + new Vector2(150f, 0f)
            };
            wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(40f, 600f) } });
            walled.AddChild(wall);
            PlayerController leonardo = AddLeonardo(walled);
            LeonardoTurretNode turret = AddTurret(walled, data, leonardo);
            CreateEnemy("chrono_slasher", walled, Origin + new Vector2(300f, -20f));
            AssertThat(turret.TryCommandBolt()).IsFalse();
            AssertThat(turret.BoltsFired).IsEqual(0);
            AssertThat(turret.BoltsRemaining).IsEqual(4);
        } finally {
            walled.Free();
        }
    }

    private static PlayerController AddLeonardo(Node2D host) {
        PlayerController leonardo = CharacterFactory.CreateCharacter("leonardo", 0, applyStoryProgression: false);
        host.AddChild(leonardo);
        leonardo.GlobalPosition = Origin + new Vector2(-100f, 0f);
        return leonardo;
    }

    private static LeonardoTurretNode AddTurret(Node2D host, AbilityData data, PlayerController owner) {
        var turret = ResourceLoader.Load<PackedScene>("res://scenes/constructs/ClockworkTurret.tscn")
            .Instantiate<LeonardoTurretNode>();
        host.AddChild(turret);
        turret.GlobalPosition = Origin;
        turret.Initialize(data, owner, clockworkOverdrive: false);
        return turret;
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
