using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A3: roster bodies route their sprite tint through the shared glow
/// arbiter rather than writing <c>Modulate</c> from three different places. The
/// production frames use white as the base tint while placeholder-only data keeps
/// telegraph becomes a tint override, and a status effect becomes an outline —
/// three channels that cannot clobber one another.
///
/// <c>EnemyAbilityExecutorTests</c> still pins the arbiter-less path, where the
/// executor writes <c>Modulate</c> directly; both paths have to stay correct.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EnemyGlowRoutingTests {
    private const string StandardEnemyScenePath = "res://scenes/enemies/StandardEnemy.tscn";

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    private static (Node host, EnemyController enemy) CreateEnemy() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "EnemyGlowHost" };
        tree.Root.AddChild(host);

        // Scenes are streamed presentation, never AuthoredResources cache entries.
        var scene = ResourceLoader.Load<PackedScene>(StandardEnemyScenePath);
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = AuthoredResources.Load<EnemyData>(
            "res://resources/Enemies/chrono_slasher.tres");
        host.AddChild(enemy);
        return (host, enemy);
    }

    [TestCase]
    public void AuthoredProductionFramesUseWhiteAsTheArbitersBaseTint() {
        (Node host, EnemyController enemy) = CreateEnemy();
        var glow = enemy.GetNodeOrNull<GlowPresentationController>(GlowPresentationController.NodeName);

        AssertObject(glow).IsNotNull();
        AssertThat(glow.HasMaterial).IsTrue();
        AssertThat(glow.BaseTint).IsEqual(Colors.White);
        AssertThat(glow.Target.Modulate).IsEqual(Colors.White);
        AssertThat(glow.HasTintOverride).IsFalse();

        host.Free();
    }

    [TestCase]
    public void AStatusOutlineAndATelegraphTintCoexistWithoutErasingEachOther() {
        (Node host, EnemyController enemy) = CreateEnemy();
        var glow = enemy.GetNodeOrNull<GlowPresentationController>(GlowPresentationController.NodeName);
        Color baseTint = Colors.White;
        var telegraphTint = new Color(1f, 0.1f, 0.1f);

        enemy.ApplyStatusEffect(StatusType.RadiantBurn, 2f);
        AssertThat(glow.IsLayerActive(GlowLayer.Status)).IsTrue();
        AssertThat(glow.ResolvedState.OutlineColor).IsEqual(GlowPalette.RadiantBurnColor);
        // A status outline must not repaint the sprite body.
        AssertThat(glow.Target.Modulate).IsEqual(baseTint);

        glow.SetTintOverride(telegraphTint);
        AssertThat(glow.Target.Modulate).IsEqual(telegraphTint);
        AssertThat(glow.ResolvedState.OutlineColor).IsEqual(GlowPalette.RadiantBurnColor);

        // Run the status out: the outline clears, the telegraph tint survives.
        for (int frame = 0; frame < 130; frame++) enemy._PhysicsProcess(1.0 / 60.0);
        AssertThat(enemy.ActiveStatusType).IsEqual(StatusType.None);
        AssertThat(glow.IsLayerActive(GlowLayer.Status)).IsFalse();
        AssertThat(glow.Target.Modulate).IsEqual(telegraphTint);

        glow.ClearTintOverride();
        AssertThat(glow.Target.Modulate).IsEqual(baseTint);

        host.Free();
    }

    [TestCase]
    public void DamageFlashesTheSpriteAndDeathClearsEveryGlowState() {
        (Node host, EnemyController enemy) = CreateEnemy();
        var glow = enemy.GetNodeOrNull<GlowPresentationController>(GlowPresentationController.NodeName);
        Color baseTint = Colors.White;

        enemy.ApplyStatusEffect(StatusType.Venom, 5f);
        AssertThat(glow.IsGlowing).IsTrue();

        enemy.TakeDamage(1);
        AssertThat(glow.HasTintOverride).IsTrue();
        AssertThat(glow.Target.Modulate).IsNotEqual(baseTint);

        // The flash is timed, not latched: it always returns the base tint.
        glow._Process(0.2);
        AssertThat(glow.HasTintOverride).IsFalse();
        AssertThat(glow.Target.Modulate).IsEqual(baseTint);

        enemy.TakeDamage(enemy.ScaledMaxHP + 100);
        AssertThat(enemy.CurrentState).IsEqual(EnemyState.Dead);
        AssertThat(glow.IsGlowing).IsFalse();

        host.Free();
    }
}
