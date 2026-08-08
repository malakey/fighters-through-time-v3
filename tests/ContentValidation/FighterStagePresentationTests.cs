using System.Collections.Generic;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 6 C1: the cross-stage presentation invariants the nine per-stage suites
/// each check only for themselves.
///
/// <para><b>The invisible-parallax trap.</b> A <c>ParallaxBackground</c> is a
/// <c>CanvasLayer</c> on a negative layer, so it draws behind <i>everything</i> in
/// canvas layer 0 — including a full-bleed opaque <c>ColorRect</c>, whatever its
/// <c>z_index</c>. Florence has no parallax, so its opaque <c>BackdropTint</c> costs
/// nothing, and copying that layout into a stage that does have one silently ships a
/// parallax nobody can see. Four of the nine Phase B scenes did exactly that, and
/// every per-stage node-existence test still passed. This suite is the guard: a
/// stage may pair the two nodes only if the tint is translucent.</para>
///
/// <para>Deliberately reached untyped (<c>GetClass()</c> rather than
/// <c>is ParallaxBackground</c>): the C# bindings carry <c>[Obsolete]</c> in Godot
/// 4.7 in favour of <c>Parallax2D</c>, and naming them would add CS0618 to a build
/// gate that allows no new warnings. Package 6 keeps the deprecated node for the
/// placeholder pass; migration belongs with the Package 8 presentation work.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStagePresentationTests {

    [TestCase]
    public void NoStagePairsAnOpaqueLayerZeroBackdropWithAParallaxBackground() {
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        AssertObject(catalog).IsNotNull();

        var offenders = new List<string>();
        int stagesWithParallax = 0;

        foreach (FighterStageData stage in catalog.Stages) {
            var scene = ResourceLoader.Load<PackedScene>(stage.ScenePath);
            AssertObject(scene).OverrideFailureMessage(
                $"{stage.StageID} scene '{stage.ScenePath}' did not load.").IsNotNull();
            Node root = scene.Instantiate();
            try {
                var presentation = root.GetNodeOrNull<Node2D>("Presentation");
                if (presentation == null) continue;

                bool hasParallax = false;
                foreach (Node child in presentation.GetChildren()) {
                    if (child.GetClass() == "ParallaxBackground") hasParallax = true;
                    // Orleans nests its tint one CanvasLayer deeper, below the
                    // parallax — a different but equally valid solution.
                    if (child is CanvasLayer nested && child.GetClass() == "CanvasLayer") {
                        foreach (Node inner in nested.GetChildren()) {
                            if (inner.GetClass() == "ParallaxBackground") hasParallax = true;
                        }
                    }
                }
                if (!hasParallax) continue;
                stagesWithParallax++;

                var tint = presentation.GetNodeOrNull<ColorRect>("BackdropTint");
                if (tint != null && tint.Color.A >= 1f) {
                    offenders.Add($"{stage.StageID}: Presentation/BackdropTint is opaque " +
                                  "and sits in canvas layer 0 over a ParallaxBackground");
                }
            } finally {
                root.Free();
            }
        }

        // Guards against a silently empty sweep passing vacuously: nine of the ten
        // stages ship a parallax (Florence, the pre-Package-6 stage, does not).
        AssertThat(stagesWithParallax).OverrideFailureMessage(
            $"Only {stagesWithParallax} stages were found to carry a ParallaxBackground.")
            .IsGreaterEqual(9);
        AssertThat(offenders.Count).OverrideFailureMessage(
            "Stages whose parallax is hidden behind an opaque backdrop: " +
            string.Join(" | ", offenders))
            .IsEqual(0);
    }

    /// <summary>
    /// Every catalog stage routes to its own scene, and that scene declares the
    /// catalog's StageID. A copy-pasted scene that kept a sibling's id would resolve
    /// to the wrong deterministic geometry at runtime while every per-stage suite
    /// (which loads its scene by path) stayed green.
    /// </summary>
    [TestCase]
    public void EveryCatalogScenePathResolvesToASceneDeclaringThatStageID() {
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        var seenPaths = new HashSet<string>();
        var issues = new List<string>();

        foreach (FighterStageData stage in catalog.Stages) {
            if (!seenPaths.Add(stage.ScenePath)) {
                issues.Add($"{stage.StageID} reuses scene path '{stage.ScenePath}'");
            }
            if (stage.ScenePath == "res://scenes/arenas/TestArena.tscn") {
                issues.Add($"{stage.StageID} still aliases the Test Arena");
            }
            if (!ResourceLoader.Exists(stage.ScenePath)) {
                issues.Add($"{stage.StageID} scene '{stage.ScenePath}' does not exist");
                continue;
            }
            if (!stage.ProductionReady) {
                issues.Add($"{stage.StageID} is not marked ProductionReady");
            }
            if (string.IsNullOrWhiteSpace(stage.PreviewTexturePath) ||
                !ResourceLoader.Exists(stage.PreviewTexturePath)) {
                issues.Add($"{stage.StageID} has no importable preview " +
                           $"('{stage.PreviewTexturePath}')");
            }

            Node root = ResourceLoader.Load<PackedScene>(stage.ScenePath).Instantiate();
            try {
                var declared = root.Get("StageID").AsString();
                if (declared != stage.StageID) {
                    issues.Add($"{stage.ScenePath} declares StageID '{declared}', " +
                               $"catalog says '{stage.StageID}'");
                }
            } finally {
                root.Free();
            }
        }

        AssertThat(issues.Count).OverrideFailureMessage(
            "Stage routing problems: " + string.Join(" | ", issues)).IsEqual(0);
    }
}
