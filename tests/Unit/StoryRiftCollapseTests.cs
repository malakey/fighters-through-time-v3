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
/// Package 13 W7a — Einstein's Relativity Rift in Story: E03 placement (thrown
/// 5 units = 300 px ahead, stopping short of a wall, 1.5 units = 90 px radius)
/// and E01 Rift Collapse (an E=mc² burst inside his own open rift ends it, pulls
/// the caught enemy to its centre over 6 frames and launches it upward with a
/// 0-damage hit).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryRiftCollapseTests {
    private const double Step = 1.0 / 60.0;
    private static readonly Vector2 Origin = new(-46000f, 600f);

    [TestCase]
    public void TheRiftIsThrownFiveUnitsAheadAndStopsShortOfAWall() {
        Node2D host = CreateHost("RiftPlacement");
        PlayerController einstein = null;
        try {
            einstein = AddEinstein(host);
            EinsteinRelativityRift rift = OpenRift(einstein);
            AssertThat(rift.ActiveRiftRadiusPixels).IsEqual(90f);
            AssertThat(rift.ActiveRiftCenter.X - einstein.GlobalPosition.X).IsEqual(300f);
        } finally {
            host.Free();
        }

        Node2D walled = CreateHost("RiftPlacementWall");
        try {
            var wall = new StaticBody2D {
                Name = "Wall",
                CollisionLayer = CollisionLayers.Environment,
                Position = Origin + new Vector2(200f, 0f)
            };
            wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(40f, 400f) } });
            walled.AddChild(wall);
            PlayerController blocked = AddEinstein(walled);
            EinsteinRelativityRift rift = OpenRift(blocked);
            // The wall's near face is 180 px out; the rift lands just short of it.
            float thrown = rift.ActiveRiftCenter.X - blocked.GlobalPosition.X;
            AssertThat(thrown > 170f && thrown < 180f).IsTrue();
        } finally {
            walled.Free();
        }
    }

    [TestCase]
    public void AnEmcSquaredBurstInsideTheRiftPullsTheEnemyToTheCentreAndLaunchesIt() {
        Node2D host = CreateHost("RiftCollapse");
        EnemyController enemy = null;
        try {
            PlayerController einstein = AddEinstein(host);
            EinsteinRelativityRift rift = OpenRift(einstein);
            Vector2 center = rift.ActiveRiftCenter;
            enemy = CreateEnemy("chrono_slasher", host, center + new Vector2(40f, 0f));

            var emc = einstein.GetNode<EinsteinEmc2Blast>("Special1");
            AssertThat(emc.BurstRadius).IsEqual(72f);
            emc.DetonateForTest(center + new Vector2(20f, 0f));
            AssertThat(rift.RiftActive).OverrideFailureMessage("The collapse must end the rift at once.").IsFalse();
            AssertThat(rift.IsCollapsing).IsTrue();

            for (int frame = 0; frame < KitMotionRules.RiftCollapsePullFrames; frame++) rift._PhysicsProcess(Step);

            AssertThat(rift.IsCollapsing).IsFalse();
            AssertThat(enemy.GlobalPosition.DistanceTo(center) < 1f)
                .OverrideFailureMessage("The caught enemy was not pulled to the rift centre.")
                .IsTrue();
            AssertThat(enemy.Velocity.Y < 0f)
                .OverrideFailureMessage("The collapse must launch the enemy upward.")
                .IsTrue();
        } finally {
            host.Free();
        }
    }

    private static EinsteinRelativityRift OpenRift(PlayerController einstein) {
        var rift = einstein.GetNode<EinsteinRelativityRift>("Special2");
        AssertThat(rift.TryExecute()).IsTrue();
        for (int frame = 0; frame < 120 && !rift.RiftActive; frame++) rift._PhysicsProcess(Step);
        AssertThat(rift.RiftActive).IsTrue();
        return rift;
    }

    private static PlayerController AddEinstein(Node2D host) {
        PlayerController einstein = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        host.AddChild(einstein);
        einstein.GlobalPosition = Origin;
        return einstein;
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

    private static Node2D CreateHost(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }
}
