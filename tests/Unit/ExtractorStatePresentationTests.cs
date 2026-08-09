using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B6: the Chronal Extractor's damaged/destroyed dressing.
///
/// <para><c>ChronalExtractor</c> has tracked an <c>Idle/Damaged/Destroyed</c>
/// <c>VisualState</c> and published an <c>ExtractorStatePayload</c> since Package 5,
/// but the template only ever expressed one of the three — a red <c>DamagedVisual</c>
/// overlay — so a destroyed extractor looked exactly like an intact one. The state
/// machine was already tested; the presentation was not.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ExtractorStatePresentationTests {

    private const string TemplatePath = "res://scenes/templates/ChronalExtractorTemplate.tscn";

    private static ChronalExtractor Spawn(out Node2D host) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        host = new Node2D { Name = "ExtractorPresentationHost" };
        tree.Root.AddChild(host);
        PackedScene scene = ResourceLoader.Load<PackedScene>(TemplatePath);
        var extractor = scene.Instantiate<ChronalExtractor>();
        host.AddChild(extractor);
        return extractor;
    }

    [TestCase]
    public void TheTemplateCarriesAVisualForEveryState() {
        ChronalExtractor extractor = Spawn(out Node2D host);
        try {
            AssertObject(extractor.GetNodeOrNull<CanvasItem>("Visual")).IsNotNull();
            AssertObject(extractor.GetNodeOrNull<CanvasItem>("CoreGlow")).IsNotNull();
            AssertObject(extractor.GetNodeOrNull<CanvasItem>("DamagedVisual")).IsNotNull();
            AssertObject(extractor.GetNodeOrNull<CanvasItem>("DestroyedVisual")).IsNotNull();
            AssertObject(extractor.GetNodeOrNull<CpuParticles2D>("DamageSparks")).IsNotNull();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void DressingFollowsTheStateMachineThroughIdleDamagedAndDestroyed() {
        ChronalExtractor extractor = Spawn(out Node2D host);
        try {
            var damaged = extractor.GetNode<CanvasItem>("DamagedVisual");
            var destroyed = extractor.GetNode<CanvasItem>("DestroyedVisual");
            var sparks = extractor.GetNode<CpuParticles2D>("DamageSparks");
            var core = extractor.GetNode<CanvasItem>("CoreGlow");

            AssertThat(extractor.VisualState).IsEqual(ChronalExtractorVisualState.Idle);
            AssertThat(damaged.Visible).IsFalse();
            AssertThat(destroyed.Visible).IsFalse();
            AssertThat(sparks.Emitting).IsFalse();
            AssertThat(core.Visible).IsTrue();

            // Past the half-HP threshold: wounded, sparking, core running hot.
            extractor.TakeEnvironmentDamage(60f);
            AssertThat(extractor.VisualState).IsEqual(ChronalExtractorVisualState.Damaged);
            AssertThat(damaged.Visible).IsTrue();
            AssertThat(destroyed.Visible).IsFalse();
            AssertThat(sparks.Emitting).IsTrue();
            AssertThat(core.Modulate).IsNotEqual(Colors.White);

            extractor.TakeEnvironmentDamage(200f);
            AssertThat(extractor.VisualState).IsEqual(ChronalExtractorVisualState.Destroyed);
            AssertThat(destroyed.Visible).IsTrue();
            AssertThat(damaged.Visible).IsFalse();
            // A dead machine neither sparks nor glows.
            AssertThat(sparks.Emitting).IsFalse();
            AssertThat(core.Visible).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void DischargingSpawnsAPooledShockwaveAndADestroyedExtractorDoesNot() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var manager = new FTT.Core.PoolManager { Name = "ExtractorVfxPoolManager" };
        tree.Root.AddChild(manager);
        ChronalExtractor extractor = Spawn(out Node2D host);

        try {
            string poolID = FTT.Combat.VfxLibrary.PathFor(FTT.Combat.VfxEffectFamily.Shockwave);

            int before = extractor.DischargeCount;
            extractor.Discharge();
            AssertThat(extractor.DischargeCount).IsEqual(before + 1);

            FTT.Core.PoolStats? stats = manager.GetStats(poolID);
            AssertThat(stats.HasValue).IsTrue();
            AssertThat(stats.Value.Active).IsEqual(1);

            // Destroying it silences the hazard entirely: no count, no effect.
            extractor.TakeEnvironmentDamage(500f);
            int afterDeath = extractor.DischargeCount;
            extractor.Discharge();
            AssertThat(extractor.DischargeCount).IsEqual(afterDeath);
        } finally {
            host.Free();
            manager.ClearAllPools();
            manager.Free();
            FTT.Combat.ParticleBudget.Shared.Clear();
        }
    }
}
