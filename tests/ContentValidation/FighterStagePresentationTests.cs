using System.Collections.Generic;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 6 C1, rewritten by Package 8 B7: the cross-stage presentation invariants
/// the nine per-stage suites each check only for themselves.
///
/// <para><b>The invisible-parallax trap.</b> The invariant is unchanged — every
/// stage's parallax content has to actually be visible, not sealed behind an opaque
/// full-screen rectangle drawn above it — but the ordering rule that decides it
/// changed shape with the node type.</para>
///
/// <para>Under the old <c>ParallaxBackground</c> the container was a
/// <c>CanvasLayer</c> at a negative <c>layer</c>, and a negative canvas layer draws
/// behind <i>everything</i> in layer 0 whatever its <c>z_index</c>. Four of the nine
/// Phase B scenes shipped an opaque layer-0 <c>BackdropTint</c> over one, and every
/// per-stage node-existence test stayed green. The Package 6 guard therefore had one
/// job: refuse an opaque <c>Presentation/BackdropTint</c> on any stage carrying a
/// <c>ParallaxBackground</c>.</para>
///
/// <para><see cref="Parallax2D"/> is an ordinary <see cref="Node2D"/>, so after the
/// migration the parallax lives in canvas layer 0 alongside the tint and the props,
/// and <b>z-order within the layer</b> is what decides visibility. A tint-alpha
/// check can no longer express the invariant: an opaque rect is now harmless if it
/// is authored *below* the parallax (which is exactly what every
/// <c>BackdropBase</c>/<c>SeaBase</c> in these scenes is), and a translucent one
/// stacked at the wrong depth is still wrong for a different reason. The guard is
/// therefore rebuilt around the real ordering: compute each canvas item's
/// <i>effective</i> (canvas layer, accumulated z_index) — respecting
/// <c>z_as_relative</c> — and fail if any opaque, full-bleed <see cref="ColorRect"/>
/// that is not itself parallax content sorts at or above the shallowest parallax
/// content in the scene.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStagePresentationTests {

    /// <summary>Reference canvas. A rect covering this is "full-bleed".</summary>
    private static readonly Rect2 ReferenceCanvas = new(0f, 0f, 1920f, 1080f);

    [TestCase]
    public void NoStageHidesItsParallaxBehindAnOpaqueFullBleedRect() {
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        AssertObject(catalog).IsNotNull();

        var offenders = new List<string>();
        int stagesWithParallax = 0;
        int opaqueFullBleedRectsSeen = 0;

        foreach (FighterStageData stage in catalog.Stages) {
            var scene = ResourceLoader.Load<PackedScene>(stage.ScenePath);
            AssertObject(scene).OverrideFailureMessage(
                $"{stage.StageID} scene '{stage.ScenePath}' did not load.").IsNotNull();
            Node root = scene.Instantiate();
            try {
                var parallaxDepths = new List<(int Layer, int Z)>();
                var occluders = new List<(string Path, int Layer, int Z)>();
                Collect(root, root, 0, 0, false, parallaxDepths, occluders);
                opaqueFullBleedRectsSeen += occluders.Count;

                if (parallaxDepths.Count == 0) continue;
                stagesWithParallax++;

                (int Layer, int Z) shallowest = parallaxDepths[0];
                foreach ((int Layer, int Z) depth in parallaxDepths) {
                    if (Compare(depth, shallowest) < 0) shallowest = depth;
                }

                foreach ((string Path, int Layer, int Z) occluder in occluders) {
                    if (Compare((occluder.Layer, occluder.Z), shallowest) < 0) continue;
                    offenders.Add(
                        $"{stage.StageID}: opaque full-bleed '{occluder.Path}' at " +
                        $"(layer {occluder.Layer}, z {occluder.Z}) covers parallax content " +
                        $"at (layer {shallowest.Layer}, z {shallowest.Z})");
                }
            } finally {
                root.Free();
            }
        }

        // Guards against a silently empty sweep passing vacuously: nine of the ten
        // stages ship a parallax (Florence, the pre-Package-6 stage, does not — it
        // dresses its distance with a single static Sprite2D backdrop instead).
        AssertThat(stagesWithParallax).OverrideFailureMessage(
            $"Only {stagesWithParallax} stages were found to carry a Parallax2D.")
            .IsGreaterEqual(9);
        // ...and against a blind detector. Six stages author an opaque full-bleed
        // base (Alexandria/Berlin/Chicago/Vesuvius BackdropBase, Nassau SeaBase,
        // Orléans' CanvasLayer tint) plus Florence's opaque BackdropTint; if the
        // full-bleed geometry test stopped matching, this check would pass on an
        // empty offender list and prove nothing.
        AssertThat(opaqueFullBleedRectsSeen).OverrideFailureMessage(
            $"Only {opaqueFullBleedRectsSeen} opaque full-bleed rects were detected " +
            "across the catalog; the full-bleed detector is not matching anything.")
            .IsGreaterEqual(6);
        AssertThat(offenders.Count).OverrideFailureMessage(
            "Stages whose parallax is hidden behind an opaque backdrop: " +
            string.Join(" | ", offenders))
            .IsEqual(0);
    }

    /// <summary>
    /// Walks the scene accumulating the draw order the engine will use: the nearest
    /// <see cref="CanvasLayer"/> ancestor's layer, and the z_index chain, which
    /// compounds through every parent whose <c>z_as_relative</c> is set (the
    /// default) and resets at one that is not.
    /// </summary>
    private static void Collect(
        Node node,
        Node root,
        int canvasLayer,
        int inheritedZ,
        bool insideParallax,
        List<(int Layer, int Z)> parallaxDepths,
        List<(string Path, int Layer, int Z)> occluders) {

        int layer = node is CanvasLayer canvas ? canvas.Layer : canvasLayer;
        int effectiveZ = inheritedZ;

        if (node is CanvasItem item) {
            effectiveZ = item.ZAsRelative ? inheritedZ + item.ZIndex : item.ZIndex;
        }
        if (node is CanvasLayer) effectiveZ = 0;

        bool nodeIsParallax = insideParallax || node is Parallax2D;
        if (nodeIsParallax && node is CanvasItem) parallaxDepths.Add((layer, effectiveZ));

        if (!nodeIsParallax && node is ColorRect rect && rect.Color.A >= 1f && IsFullBleed(rect)) {
            occluders.Add((root.GetPathTo(node).ToString(), layer, effectiveZ));
        }

        foreach (Node child in node.GetChildren()) {
            Collect(child, root, layer, effectiveZ, nodeIsParallax, parallaxDepths, occluders);
        }
    }

    /// <summary>Layer dominates z_index; both ascend towards the viewer.</summary>
    private static int Compare((int Layer, int Z) left, (int Layer, int Z) right) =>
        left.Layer != right.Layer ? left.Layer.CompareTo(right.Layer) : left.Z.CompareTo(right.Z);

    /// <summary>
    /// A <see cref="Control"/> resolves its rect from anchors against its parent, and
    /// these scenes are inspected without ever entering the tree, so fall back to the
    /// raw offsets (all ten stages author plain zero-anchor offsets) if the resolved
    /// rect came back degenerate.
    /// </summary>
    private static bool IsFullBleed(Control control) {
        Rect2 rect = control.GetRect();
        if (rect.Size.X <= 0f || rect.Size.Y <= 0f) {
            rect = new Rect2(
                control.OffsetLeft,
                control.OffsetTop,
                control.OffsetRight - control.OffsetLeft,
                control.OffsetBottom - control.OffsetTop);
        }
        return rect.Encloses(ReferenceCanvas);
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
