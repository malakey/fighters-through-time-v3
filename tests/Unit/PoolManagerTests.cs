using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

public partial class PoolProbeNode : PooledNode, IPoolable {
    public int SpawnCount;
    public int DespawnCount;
    public int MutableValue;

    public void OnSpawn() => SpawnCount++;

    public void OnDespawn() {
        DespawnCount++;
        MutableValue = 0;
    }
}

public partial class GroupedPoolProbeNode : PoolProbeNode {
    public override void _Ready() => AddToGroup("enemy_projectile");
}

[TestSuite]
[RequireGodotRuntime]
public class PoolManagerTests {
    [TestCase]
    public void RejectPolicyHonorsCapacity() {
        (PoolManager manager, Node parent, PackedScene template) = CreateSubject();
        manager.RegisterPool(template, 0, 1, PoolOverflowPolicy.Reject);

        Node first = manager.Spawn(template, Vector2.Zero, parent);
        Node rejected = manager.Spawn(template, Vector2.Zero, parent);

        AssertObject(first).IsNotNull();
        AssertObject(rejected).IsNull();
        Cleanup(manager, parent);
    }

    [TestCase]
    public void RecycleOldestResetsAndReusesTheNode() {
        (PoolManager manager, Node parent, PackedScene template) = CreateSubject();
        manager.RegisterPool(template, 0, 1, PoolOverflowPolicy.RecycleOldest);

        var first = (PoolProbeNode)manager.Spawn(template, Vector2.Zero, parent);
        first.MutableValue = 99;
        var recycled = (PoolProbeNode)manager.Spawn(template, Vector2.One, parent);

        AssertThat(ReferenceEquals(first, recycled)).IsTrue();
        AssertThat(recycled.MutableValue).IsEqual(0);
        AssertThat(recycled.SpawnCount).IsEqual(2);
        AssertThat(recycled.DespawnCount).IsEqual(1);
        Cleanup(manager, parent);
    }

    [TestCase]
    public void ReleasingAnInactiveNodeIsIdempotent() {
        (PoolManager manager, Node parent, PackedScene template) = CreateSubject();
        manager.RegisterPool(template, 1, 1, PoolOverflowPolicy.Reject);
        var node = (PoolProbeNode)manager.Spawn(template, Vector2.Zero, parent);

        manager.Release(node);
        manager.Release(node);

        AssertThat(node.DespawnCount).IsEqual(1);
        Cleanup(manager, parent);
    }

    [TestCase]
    public void GrowPolicyStopsAtConfiguredMaximum() {
        (PoolManager manager, Node parent, PackedScene template) = CreateSubject();
        manager.RegisterPool(template, 0, 2, PoolOverflowPolicy.Grow);

        Node first = manager.Spawn(template, Vector2.Zero, parent);
        Node second = manager.Spawn(template, Vector2.Zero, parent);
        Node rejected = manager.Spawn(template, Vector2.Zero, parent);

        AssertObject(first).IsNotNull();
        AssertObject(second).IsNotNull();
        AssertObject(rejected).IsNull();
        PoolStats stats = manager.GetStats(template).Value;
        AssertThat(stats.Active).IsEqual(2);
        AssertThat(stats.MaxCapacity).IsEqual(2);
        Cleanup(manager, parent);
    }

    [TestCase]
    public void InvalidSceneBudgetIsRejectedBeforeWarmup() {
        (PoolManager manager, Node parent, PackedScene template) = CreateSubject();
        var config = new ScenePoolConfig {
            ConfigID = "invalid",
            MaxWarmUpInstances = 1,
            PoolDefinitions = new[] {
                new PoolDefinition {
                    PoolID = "probe",
                    SceneTemplate = template,
                    WarmUpCount = 2,
                    MaxCapacity = 2
                }
            }
        };

        AssertThat(config.ValidateBudget().Count > 0).IsTrue();
        Cleanup(manager, parent);
        config.Dispose();
    }

    [TestCase]
    public void StablePoolIDSupportsSpawnStatsAndRepeatedReleaseCycles() {
        (PoolManager manager, Node parent, PackedScene template) = CreateSubject();
        manager.RegisterPool("story_probe", template, 1, 2, PoolOverflowPolicy.Grow);

        for (int cycle = 0; cycle < 20; cycle++) {
            var node = (PoolProbeNode)manager.Spawn("story_probe", new Vector2(cycle, 0), parent);
            AssertObject(node).IsNotNull();
            node.MutableValue = cycle + 1;
            manager.Release(node);
            AssertThat(node.MutableValue).IsEqual(0);
        }

        AssertThat(manager.IsRegistered("story_probe")).IsTrue();
        PoolStats stats = manager.GetStats("story_probe").Value;
        AssertThat(stats.Active).IsEqual(0);
        AssertThat(stats.Inactive).IsEqual(1);
        Cleanup(manager, parent);
    }

    [TestCase]
    public void ActiveGroupReleaseClearsEnemyProjectilesOnly() {
        var groupedSource = new GroupedPoolProbeNode();
        var groupedTemplate = new PackedScene();
        AssertThat(groupedTemplate.Pack(groupedSource)).IsEqual(Error.Ok);
        groupedSource.Free();
        (PoolManager manager, Node parent, PackedScene regularTemplate) = CreateSubject();
        manager.RegisterPool("enemy_projectiles", groupedTemplate, 0, 2, PoolOverflowPolicy.Grow);
        manager.RegisterPool("regular", regularTemplate, 0, 2, PoolOverflowPolicy.Grow);
        manager.Spawn("enemy_projectiles", Vector2.Zero, parent);
        manager.Spawn("regular", Vector2.Zero, parent);

        AssertThat(manager.ReleaseActiveInGroup("enemy_projectile")).IsEqual(1);
        AssertThat(manager.GetStats("enemy_projectiles").Value.Active).IsEqual(0);
        AssertThat(manager.GetStats("regular").Value.Active).IsEqual(1);
        Cleanup(manager, parent);
    }

    [TestCase]
    public void StoryEnemyFactoryReusesAResetControllerInstance() {
        (PoolManager manager, Node parent, PackedScene template) = CreateSubject();
        EnemyController first = EnemyFactory.SpawnHologramDrone(parent, new Vector2(10f, 20f));
        EnemyFactory.SpawnHologramDrone(parent, new Vector2(12f, 20f));
        EnemyFactory.SpawnHologramDrone(parent, new Vector2(14f, 20f));
        EnemyFactory.SpawnHologramDrone(parent, new Vector2(16f, 20f));
        int maximumHP = first.Data.MaxHP;
        first.TakeDamage(maximumHP);
        EnemyController recycled = EnemyFactory.SpawnHologramDrone(parent, new Vector2(30f, 40f));

        AssertThat(ReferenceEquals(first, recycled)).IsTrue();
        AssertThat(recycled.CurrentHP).IsEqual(maximumHP);
        AssertThat(recycled.GlobalPosition).IsEqual(new Vector2(30f, 40f));
        AssertThat(manager.IsRegistered("story_enemy.hologram_drone")).IsTrue();
        Cleanup(manager, parent);
    }

    private static (PoolManager manager, Node parent, PackedScene template) CreateSubject() {
        var source = new PoolProbeNode();
        var template = new PackedScene();
        AssertThat(template.Pack(source)).IsEqual(Error.Ok);
        source.Free();

        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node { Name = "PoolTestParent" };
        tree.Root.AddChild(parent);
        var manager = new PoolManager { Name = "PoolManagerUnderTest" };
        tree.Root.AddChild(manager);
        return (manager, parent, template);
    }

    private static void Cleanup(PoolManager manager, Node parent) {
        manager.ClearAllPools();
        manager.Free();
        parent.Free();
    }
}
