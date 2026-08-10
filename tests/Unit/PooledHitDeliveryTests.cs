using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Audit C-1/H-3 regression pins: PoolManager reparents nodes on every
/// spawn/release cycle, and _ExitTree fires on every RemoveChild while _Ready
/// fires once per node lifetime. Hit delivery (Hitbox.AreaEntered) and construct
/// destructibility (Hurtbox.OnHit) therefore bind on the enter/exit pair, or a
/// warmed instance is dead on arrival and a fresh one dies after one cycle.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PooledHitDeliveryTests {

    [TestCase]
    public void PooledProjectileStillLandsAHitOnItsSecondPoolCycle() {
        (PoolManager manager, Node parent) = CreateSubject();
        try {
            var projectileSource = new PlaceholderProjectile();
            var template = new PackedScene();
            AssertThat(template.Pack(projectileSource)).IsEqual(Error.Ok);
            projectileSource.Free();
            manager.RegisterPool("hit_probe_projectile", template, 0, 4, PoolOverflowPolicy.Grow);

            (Hurtbox target, HitRecorder recorder) = CreateHitTarget(parent);

            var projectile = (PlaceholderProjectile)manager.Spawn("hit_probe_projectile", Vector2.Zero, parent);
            projectile.Setup(10f, Vector2.Zero, 0f, true, ownerIndex: 0, Colors.White);
            projectile.GetNode<Hitbox>("Hitbox").EmitSignal(Area2D.SignalName.AreaEntered, target);
            AssertThat(recorder.TotalDamage).IsEqualApprox(10f, 0.001f);

            manager.Release(projectile);

            // The release/reparent pair must leave delivery wired for cycle two
            // (audit C-1: it used to disconnect forever).
            var respawned = (PlaceholderProjectile)manager.Spawn("hit_probe_projectile", Vector2.Zero, parent);
            AssertThat(ReferenceEquals(projectile, respawned)).IsTrue();
            respawned.Setup(10f, Vector2.Zero, 0f, true, ownerIndex: 0, Colors.White);
            respawned.GetNode<Hitbox>("Hitbox").EmitSignal(Area2D.SignalName.AreaEntered, target);
            AssertThat(recorder.TotalDamage).IsEqualApprox(20f, 0.001f);
        } finally {
            Cleanup(manager, parent);
        }
    }

    [TestCase]
    public void WarmedInstanceLandsItsVeryFirstHitAfterTheSpawnReparent() {
        (PoolManager manager, Node parent) = CreateSubject();
        try {
            // The hitbox is authored into the template, so the warmed instance's
            // _Ready runs under the pool's InactiveContainer and the first spawn
            // reparents it (audit C-1: warmed instances were dead on arrival).
            manager.RegisterPool("warm_hit_probe", PackHitboxCarrier(), 1, 2, PoolOverflowPolicy.Grow);
            (Hurtbox target, HitRecorder recorder) = CreateHitTarget(parent);

            var carrier = (PooledNode)manager.Spawn("warm_hit_probe", Vector2.Zero, parent);
            Hitbox hitbox = carrier.GetNode<Hitbox>("Hitbox");
            hitbox.OwnerPlayerIndex = 0;
            hitbox.Damage = 7f;
            hitbox.Activate();
            hitbox.EmitSignal(Area2D.SignalName.AreaEntered, target);

            AssertThat(recorder.TotalDamage).IsEqualApprox(7f, 0.001f);
        } finally {
            Cleanup(manager, parent);
        }
    }

    [TestCase]
    public void WarmedTeslaCoilTakesDamageFromItsFirstIncomingHit() {
        (PoolManager manager, Node parent) = CreateSubject();
        try {
            // Authored construct scene; pooled scenes stay out of AuthoredResources.
            var coilScene = GD.Load<PackedScene>("res://scenes/constructs/TeslaCoil.tscn");
            manager.RegisterPool("warm_tesla_coil", coilScene, 1, 2, PoolOverflowPolicy.Grow);

            var coil = (TeslaCoilNode)manager.Spawn("warm_tesla_coil", Vector2.Zero, parent);
            coil.Initialize(null, null, resonantOverdrive: false);

            // Audit H-3: the warmed coil's hurtbox subscription must survive the
            // first spawn's reparent or the construct is indestructible.
            float applied = coil.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = 1,
                Damage = 10f
            });

            AssertThat(applied).IsEqualApprox(10f, 0.001f);
            AssertThat(coil.IsCoilDestroyed).IsFalse();
        } finally {
            Cleanup(manager, parent);
        }
    }

    private sealed class HitRecorder {
        public float TotalDamage;
    }

    private static (Hurtbox target, HitRecorder recorder) CreateHitTarget(Node parent) {
        var recorder = new HitRecorder();
        var target = new Hurtbox { Name = "TargetHurtbox", OwnerPlayerIndex = 1 };
        target.OnHit += payload => {
            recorder.TotalDamage += payload.Damage;
            return payload.Damage;
        };
        parent.AddChild(target);
        return (target, recorder);
    }

    private static PackedScene PackHitboxCarrier() {
        var root = new PooledNode { Name = "HitboxCarrier" };
        var hitbox = new Hitbox { Name = "Hitbox" };
        root.AddChild(hitbox);
        hitbox.Owner = root;
        var template = new PackedScene();
        AssertThat(template.Pack(root)).IsEqual(Error.Ok);
        root.Free();
        return template;
    }

    private static (PoolManager manager, Node parent) CreateSubject() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node { Name = "PooledHitDeliveryTestParent" };
        tree.Root.AddChild(parent);
        var manager = new PoolManager { Name = "PoolManagerUnderTest" };
        tree.Root.AddChild(manager);
        return (manager, parent);
    }

    private static void Cleanup(PoolManager manager, Node parent) {
        manager.ClearAllPools();
        manager.Free();
        parent.Free();
    }
}
