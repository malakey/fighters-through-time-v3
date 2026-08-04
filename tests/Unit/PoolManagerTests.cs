using FTT.Core;
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
