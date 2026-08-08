using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A3: the pooled VFX emitter service. Before this, every pool config
/// warmed <c>combat_vfx</c> / <c>environment_vfx</c> / <c>fighter_vfx</c> /
/// <c>fighter_environment_vfx</c> and no code ever spawned from them, and
/// <c>AbilityData.CastVFXScene</c> / <c>ImpactVFXScene</c> had no consumer.
///
/// The emitter must stay pool-disciplined (reuse, never churn) and completely
/// null-safe, because B6 authors the actual VFX resources afterwards and gameplay
/// must never depend on an effect existing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class VfxEmitterTests {

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    private static (PoolManager manager, Node parent) CreateSubject() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node2D { Name = "VfxTestParent" };
        tree.Root.AddChild(parent);
        var manager = new PoolManager { Name = "VfxPoolManagerUnderTest" };
        tree.Root.AddChild(manager);
        return (manager, parent);
    }

    private static void Cleanup(PoolManager manager, Node parent) {
        manager.ClearAllPools();
        manager.Free();
        parent.Free();
        ParticleBudget.Shared.Clear();
    }

    [TestCase]
    public void CombatEffectsSpawnFromTheCombatPoolAndReturnToIt() {
        (PoolManager manager, Node parent) = CreateSubject();

        var first = VfxEmitter.EmitCombat(new Vector2(120f, -40f), parent) as PooledNode;
        AssertObject(first).IsNotNull();
        AssertThat(first.PoolID).IsEqual(VfxEmitter.CombatPoolID);
        AssertThat(manager.IsRegistered(VfxEmitter.CombatPoolID)).IsTrue();

        PoolStats warmed = manager.GetStats(VfxEmitter.CombatPoolID).Value;
        int totalInstances = warmed.Active + warmed.Inactive;

        // Pool discipline: a spawn/release cycle recycles rather than instantiating.
        for (int cycle = 0; cycle < 12; cycle++) {
            var effect = VfxEmitter.EmitCombat(new Vector2(cycle, 0f), parent) as PooledNode;
            AssertObject(effect).IsNotNull();
            effect.ReturnToPool();
        }
        PoolStats after = manager.GetStats(VfxEmitter.CombatPoolID).Value;
        AssertThat(after.Active + after.Inactive).IsEqual(totalInstances);
        AssertThat(after.Active).IsEqual(1);

        first.ReturnToPool();
        Cleanup(manager, parent);
    }

    [TestCase]
    public void EnvironmentEffectsUseTheirOwnPool() {
        (PoolManager manager, Node parent) = CreateSubject();

        var effect = VfxEmitter.EmitEnvironment(Vector2.Zero, parent) as PooledNode;
        AssertObject(effect).IsNotNull();
        AssertThat(effect.PoolID).IsEqual(VfxEmitter.EnvironmentPoolID);
        AssertThat(manager.IsRegistered(VfxEmitter.EnvironmentPoolID)).IsTrue();
        // The combat pool must not have been dragged in by an environment call.
        AssertThat(manager.IsRegistered(VfxEmitter.CombatPoolID)).IsFalse();

        Cleanup(manager, parent);
    }

    [TestCase]
    public void ADeathPresentationEventRoutesToTheEnvironmentPool() {
        (PoolManager manager, Node parent) = CreateSubject();

        var death = VfxEmitter.EmitForPresentationEvent(
            "chrono_slasher.death", EnemyPresentationPhase.Death, Vector2.Zero, parent) as PooledNode;
        AssertObject(death).IsNotNull();
        AssertThat(death.PoolID).IsEqual(VfxEmitter.EnvironmentPoolID);

        var telegraph = VfxEmitter.EmitForPresentationEvent(
            "chrono_slasher.telegraph", EnemyPresentationPhase.Telegraph, Vector2.Zero, parent) as PooledNode;
        AssertObject(telegraph).IsNotNull();
        AssertThat(telegraph.PoolID).IsEqual(VfxEmitter.CombatPoolID);

        Cleanup(manager, parent);
    }

    [TestCase]
    public void EveryEntryPointIsNullSafeWithoutAParentOrScene() {
        (PoolManager manager, Node parent) = CreateSubject();

        AssertObject(VfxEmitter.EmitCombat(Vector2.Zero, null)).IsNull();
        AssertObject(VfxEmitter.EmitScene(null, Vector2.Zero, parent)).IsNull();
        AssertObject(VfxEmitter.Emit("", Vector2.Zero, parent)).IsNull();
        AssertObject(VfxEmitter.EmitForPresentationEvent(
            "", EnemyPresentationPhase.Active, Vector2.Zero, null)).IsNull();

        Cleanup(manager, parent);
    }

    [TestCase]
    public void AnAuthoredVfxSceneGetsItsOwnPoolKeyedOnItsResourcePath() {
        (PoolManager manager, Node parent) = CreateSubject();
        PackedScene template = ResourceLoader.Load<PackedScene>(
            "res://scenes/templates/PooledVfxTemplate.tscn");
        AssertObject(template).IsNotNull();

        var first = VfxEmitter.EmitScene(template, Vector2.Zero, parent) as PooledNode;
        AssertObject(first).IsNotNull();
        AssertThat(first.PoolID).IsEqual(template.ResourcePath);

        first.ReturnToPool();
        var reused = VfxEmitter.EmitScene(template, new Vector2(5f, 5f), parent) as PooledNode;
        AssertThat(ReferenceEquals(first, reused)).IsTrue();

        Cleanup(manager, parent);
    }

    [TestCase]
    public void PooledEffectsReserveAndReleaseTheirParticleBudget() {
        (PoolManager manager, Node parent) = CreateSubject();
        ParticleBudget.Shared.Clear();

        var effect = VfxEmitter.EmitCombat(Vector2.Zero, parent) as PooledPlaceholder;
        AssertObject(effect).IsNotNull();
        AssertThat(effect.ParticleBudgetCost > 0).IsTrue();
        AssertThat(ParticleBudget.Shared.ActiveParticles).IsEqual(effect.ParticleBudgetCost);
        AssertThat(ParticleBudget.Shared.IsReserved(effect.GetInstanceId())).IsTrue();

        effect.ReturnToPool();
        AssertThat(ParticleBudget.Shared.ActiveParticles).IsEqual(0);
        AssertThat(ParticleBudget.Shared.IsReserved(effect.GetInstanceId())).IsFalse();

        Cleanup(manager, parent);
    }

    [TestCase]
    public void ThePooledVfxTemplatesCarryProceduralParticlesAndAVisibilitySuspender() {
        foreach (string path in new[] {
            "res://scenes/templates/PooledVfxTemplate.tscn",
            "res://scenes/templates/EnvironmentalVfxTemplate.tscn"
        }) {
            PackedScene template = ResourceLoader.Load<PackedScene>(path);
            AssertObject(template).IsNotNull();
            var instance = template.Instantiate() as PooledPlaceholder;
            AssertObject(instance).IsNotNull();
            AssertThat(instance.ParticleBudgetCost > 0).IsTrue();

            var particles = instance.GetNodeOrNull<CpuParticles2D>("Particles");
            AssertObject(particles).IsNotNull();
            // Warmed instances must sit inert until OnSpawn turns them on.
            AssertThat(particles.Emitting).IsFalse();
            AssertThat(particles.Amount).IsEqual(instance.ParticleBudgetCost);

            AssertObject(instance.GetNodeOrNull<PresentationVisibilitySuspender>(
                PresentationVisibilitySuspender.NodeName)).IsNotNull();
            instance.Free();
        }
    }
}
