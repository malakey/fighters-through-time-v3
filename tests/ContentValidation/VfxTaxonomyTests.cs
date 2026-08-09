using System;
using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 B6: the six shared placeholder effect scenes under
/// <c>res://scenes/vfx/</c> and the presentation curve they animate.
///
/// <para>These scenes are the substrate for both the 36 ability assignments and the
/// 42 roster presentation hooks, so a broken one is a very wide failure. They must
/// stay pool-compatible (a <c>PooledPlaceholder</c> subclass with a bounded lifetime
/// and a declared particle cost), and their silhouettes must actually differ —
/// six identical scenes would pass every path check and carry no information.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class VfxTaxonomyTests {

    [After]
    public void DrainPendingFinalizers() {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [TestCase]
    public void EveryFamilyResolvesToAnAuthoredPoolCompatibleScene() {
        var issues = new List<string>();
        foreach (VfxEffectFamily family in Enum.GetValues<VfxEffectFamily>()) {
            string path = VfxLibrary.PathFor(family);
            if (!ResourceLoader.Exists(path)) { issues.Add($"{family}: missing {path}"); continue; }

            PackedScene scene = VfxLibrary.Load(family);
            if (scene == null) { issues.Add($"{family}: {path} failed to load"); continue; }

            Node instance = scene.Instantiate();
            if (instance is not VfxEffect effect) {
                issues.Add($"{family}: root is {instance.GetType().Name}, not VfxEffect");
                instance.Free();
                continue;
            }
            if (effect.Family != family) issues.Add($"{family}: scene declares Family={effect.Family}");
            // A zero lifetime never returns to the pool: a leak, one instance per cast.
            if (effect.LifetimeFrames <= 0) issues.Add($"{family}: LifetimeFrames={effect.LifetimeFrames}");
            if (effect.ParticleBudgetCost <= 0) issues.Add($"{family}: declares no particle cost");
            if (effect.GetNodeOrNull<Node2D>("Visual") == null) issues.Add($"{family}: no Visual child");
            if (effect.GetNodeOrNull<Node2D>("Particles") == null) issues.Add($"{family}: no Particles child");
            if (effect.GetNodeOrNull<PresentationVisibilitySuspender>(
                    PresentationVisibilitySuspender.NodeName) == null) {
                issues.Add($"{family}: no off-screen suspender");
            }
            instance.Free();
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheWholeTaxonomyFitsInsideTheSharedParticleBudget() {
        int total = 0;
        foreach (VfxEffectFamily family in Enum.GetValues<VfxEffectFamily>()) {
            PackedScene scene = VfxLibrary.Load(family);
            AssertObject(scene).IsNotNull();
            Node instance = scene.Instantiate();
            total += ((VfxEffect)instance).ParticleBudgetCost;
            instance.Free();
        }
        // One of every family at once is a fraction of the 500-particle budget; the
        // registry's steal-oldest policy handles the rest.
        AssertThat(total).IsLess(ParticleBudgetRegistry.DefaultMaxParticles / 4);
    }

    [TestCase]
    public void EveryFamilyFadesOutAndNoTwoShareASilhouette() {
        var signatures = new HashSet<string>();
        foreach (VfxEffectFamily family in Enum.GetValues<VfxEffectFamily>()) {
            AssertFloat(VfxEffect.AlphaAt(family, 1f)).IsEqualApprox(0f, 0.0001);
            AssertFloat(VfxEffect.AlphaAt(family, 0.5f)).IsGreater(0f);

            Vector2 start = VfxEffect.ScaleAt(family, 0f);
            Vector2 mid = VfxEffect.ScaleAt(family, 0.5f);
            Vector2 end = VfxEffect.ScaleAt(family, 1f);
            signatures.Add($"{start}|{mid}|{end}|{VfxEffect.RotationAt(family, 1f)}");
        }
        AssertThat(signatures.Count).IsEqual(Enum.GetValues<VfxEffectFamily>().Length);
    }

    [TestCase]
    public void TheCurveIsClampedOutsideItsNormalisedRange() {
        // ApplyCurve clamps, but the public helpers are called by tests and by any
        // later consumer; an unclamped lerp would invert the shape past the end.
        foreach (VfxEffectFamily family in Enum.GetValues<VfxEffectFamily>()) {
            AssertThat(VfxEffect.ScaleAt(family, -5f)).IsEqual(VfxEffect.ScaleAt(family, 0f));
            AssertThat(VfxEffect.ScaleAt(family, 5f)).IsEqual(VfxEffect.ScaleAt(family, 1f));
            AssertFloat(VfxEffect.AlphaAt(family, 5f)).IsEqualApprox(0f, 0.0001);
        }
    }

    [TestCase]
    public void OnlyTheSlashSweeps() {
        float sweep = Mathf.Abs(VfxEffect.RotationAt(VfxEffectFamily.Slash, 1f)
            - VfxEffect.RotationAt(VfxEffectFamily.Slash, 0f));
        AssertFloat(sweep).IsGreater(0.5f);
        foreach (VfxEffectFamily family in Enum.GetValues<VfxEffectFamily>()) {
            if (family == VfxEffectFamily.Slash) continue;
            AssertFloat(VfxEffect.RotationAt(family, 0.5f)).IsEqualApprox(0f, 0.0001);
        }
    }

    [TestCase]
    public void ASpawnedEffectRunsItsLifetimeAndReturnsToThePool() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node2D { Name = "VfxTaxonomyParent" };
        tree.Root.AddChild(parent);
        var manager = new PoolManager { Name = "VfxTaxonomyPoolManager" };
        tree.Root.AddChild(manager);

        try {
            PackedScene scene = VfxLibrary.Load(VfxEffectFamily.Impact);
            AssertObject(scene).IsNotNull();

            var effect = VfxEmitter.EmitScene(scene, new Vector2(64f, -32f), parent,
                new Color(1f, 0.5f, 0.25f, 1f)) as VfxEffect;
            AssertObject(effect).IsNotNull();
            // The caller's accent lands on the root, where the curve never writes.
            AssertFloat(effect.Modulate.R).IsEqualApprox(1f, 0.001);
            AssertFloat(effect.Modulate.G).IsEqualApprox(0.5f, 0.001);

            PoolStats before = manager.GetStats(scene.ResourcePath).Value;
            AssertThat(before.Active).IsEqual(1);

            for (int frame = 0; frame <= effect.LifetimeFrames; frame++) {
                effect._PhysicsProcess(1.0 / 60.0);
            }

            PoolStats after = manager.GetStats(scene.ResourcePath).Value;
            AssertThat(after.Active).IsEqual(0);
        } finally {
            manager.ClearAllPools();
            manager.Free();
            parent.Free();
            ParticleBudget.Shared.Clear();
        }
    }
}
