using System.Threading.Tasks;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using Godot;

namespace FTT.Tests.Unit;

/// <summary>The shared fixtures of the GR-HAZ suites (ruling 7d). Not a suite.</summary>
internal static class HazardMobTestKit {
    public const float OriginX = 73000f;
    public const string Slasher = "chrono_slasher";
    public const string Elite = "neural_linked_knight";

    public static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    public static async Task Frames(int count) {
        for (int frame = 0; frame < count; frame++) {
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
        }
    }

    /// <summary>A passive mob with an HP pool 30 cannot empty, unless <paramref name="maxHP"/> says otherwise.</summary>
    public static EnemyController CreateMob(string enemyID, Vector2 position, int maxHP = 400, string overrideEnemyID = null) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        data.AggroRadius = 0f;
        data.MaxHP = maxHP;
        if (overrideEnemyID != null) data.EnemyID = overrideEnemyID;
        if (data.Behavior != DefaultBehavior.Flying) data.Behavior = DefaultBehavior.StandGuard;
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        Tree.Root.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        return enemy;
    }

    /// <summary>An Environment floor around <see cref="OriginX"/> whose top is at y = 0.</summary>
    public static StaticBody2D CreateFloor() {
        var floor = new StaticBody2D {
            Name = "HazardMobFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(6000f, 40f) },
            Position = new Vector2(OriginX, 20f)
        });
        Tree.Root.AddChild(floor);
        return floor;
    }

    public static CollisionShape2D RectShape(Vector2 size, Vector2 offset) => new() {
        Shape = new RectangleShape2D { Size = size },
        Position = offset
    };

    /// <summary>Throws the mob straight up: a thrown flight, with no damage of its own.</summary>
    public static void Throw(EnemyController mob) => mob.LaunchThrown(new Vector2(0f, -360f), 0, 0);

    /// <summary>A string hit that names no hero, so it arms the wall-bounce latch only when it launches.</summary>
    public static HitPayload StringHit(string hitboxID, float damage, Vector2 knockback, bool launches) => new() {
        AttackerIndex = 0,
        AttackID = "test.basic",
        HitboxID = hitboxID,
        AttackClass = AttackClass.Basic,
        Origin = HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = damage,
        HitstunDuration = BasicComboRules.HitstunFrames[2] / 60f,
        Knockback = knockback,
        Launches = launches,
        // Behind a right-facing mob, so no frontal reduction can apply.
        HitOrigin = new Vector2(OriginX - 5000f, 0f),
        AttackerFacingRight = true
    };
}
