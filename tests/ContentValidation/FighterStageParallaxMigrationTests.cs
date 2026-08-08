using System.Collections.Generic;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 B7: the one-way gate on the <c>ParallaxBackground</c> →
/// <see cref="Parallax2D"/> migration.
///
/// <para>Package 6 §9 C1 deliberately kept the deprecated nodes for the placeholder
/// pass and recorded the migration as a single Package 8 sweep — "do not migrate one
/// stage in isolation". The per-stage suites cannot enforce that: each only inspects
/// its own scene, so nine green suites are equally consistent with nine migrated
/// stages and with one. This sweep is the repository-wide statement, and it is
/// written as an <i>absence</i> check as well as a presence one, because a stage that
/// regressed to the old node type would still satisfy every "has at least two layers"
/// assertion in its own file.</para>
///
/// <para>The deprecated classes are named as strings rather than C# types on purpose:
/// referencing <c>ParallaxBackground</c>/<c>ParallaxLayer</c> in C# raises CS0618
/// against a build gate that permits no new warnings — which is precisely why the old
/// per-stage assertions were untyped and why removing that workaround was part of
/// this workstream.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageParallaxMigrationTests {

    private const string DeprecatedContainer = "ParallaxBackground";
    private const string DeprecatedLayer = "ParallaxLayer";

    /// <summary>
    /// Every catalog stage scene is free of the deprecated pair, and the nine that
    /// carry a parallax carry it as <see cref="Parallax2D"/> with at least two layers
    /// at distinct scroll factors.
    /// </summary>
    [TestCase]
    public void EveryStageSceneUsesParallax2DAndNoDeprecatedParallaxNodeSurvives() {
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        AssertObject(catalog).IsNotNull();

        var issues = new List<string>();
        int stagesWithParallax = 0;
        int totalLayers = 0;

        foreach (FighterStageData stage in catalog.Stages) {
            Node root = ResourceLoader.Load<PackedScene>(stage.ScenePath).Instantiate();
            try {
                var layers = new List<Parallax2D>();
                var deprecated = new List<string>();
                Walk(root, root, layers, deprecated);

                foreach (string path in deprecated) {
                    issues.Add($"{stage.StageID}: '{path}' is still a deprecated parallax node");
                }
                if (layers.Count == 0) continue;

                stagesWithParallax++;
                totalLayers += layers.Count;

                if (layers.Count < 2) {
                    issues.Add($"{stage.StageID}: only {layers.Count} Parallax2D layer(s); " +
                               "a single plane is not a parallax");
                    continue;
                }
                for (int outer = 0; outer < layers.Count; outer++) {
                    // A zero scroll scale is a layer pinned to the camera, which is
                    // the one value that silently turns a parallax back into a decal.
                    if (layers[outer].ScrollScale == Vector2.Zero) {
                        issues.Add($"{stage.StageID}: layer '{layers[outer].Name}' has a zero scroll scale");
                    }
                    for (int inner = outer + 1; inner < layers.Count; inner++) {
                        if (Mathf.Abs(layers[outer].ScrollScale.X - layers[inner].ScrollScale.X) > 0.01f) {
                            continue;
                        }
                        issues.Add(
                            $"{stage.StageID}: layers '{layers[outer].Name}' and " +
                            $"'{layers[inner].Name}' share scroll scale " +
                            $"{layers[outer].ScrollScale.X}");
                    }
                }
            } finally {
                root.Free();
            }
        }

        // Nine of the ten stages ship a parallax; Florence predates Package 6 and
        // dresses its distance with a single static Sprite2D. Without this the sweep
        // would pass on an empty catalog walk.
        AssertThat(stagesWithParallax).OverrideFailureMessage(
            $"Only {stagesWithParallax} stages carry a Parallax2D.").IsGreaterEqual(9);
        AssertThat(totalLayers).OverrideFailureMessage(
            $"Only {totalLayers} Parallax2D layers found across the catalog.").IsGreaterEqual(18);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// <c>motion_mirroring</c> carried the seamless horizontal tile on five layers
    /// across three stages (Alexandria ×2, Chicago ×2, Nassau ×1);
    /// its <see cref="Parallax2D"/> equivalent is <c>repeat_size</c> plus a
    /// <c>repeat_times</c> above one. Setting the first and forgetting the second
    /// draws a single tile and leaves a hard edge mid-pan — a defect no headless gate
    /// can see, so the properties are pinned as a pair here instead.
    /// </summary>
    [TestCase]
    public void EveryRepeatingLayerCarriesBothRepeatSizeAndARepeatCount() {
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        var issues = new List<string>();
        int repeatingLayers = 0;

        foreach (FighterStageData stage in catalog.Stages) {
            Node root = ResourceLoader.Load<PackedScene>(stage.ScenePath).Instantiate();
            try {
                var layers = new List<Parallax2D>();
                Walk(root, root, layers, new List<string>());
                foreach (Parallax2D layer in layers) {
                    if (layer.RepeatSize == Vector2.Zero) continue;
                    repeatingLayers++;
                    if (layer.RepeatTimes < 2) {
                        issues.Add($"{stage.StageID}/{layer.Name}: repeat_size is set but " +
                                   $"repeat_times is {layer.RepeatTimes}");
                    }
                }
            } finally {
                root.Free();
            }
        }

        AssertThat(repeatingLayers).OverrideFailureMessage(
            $"Only {repeatingLayers} repeating parallax layers found; the five layers " +
            "that used motion_mirroring should have carried it across.").IsGreaterEqual(5);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    private static void Walk(Node node, Node root, List<Parallax2D> layers, List<string> deprecated) {
        if (node is Parallax2D layer) layers.Add(layer);
        string className = node.GetClass();
        if (className == DeprecatedContainer || className == DeprecatedLayer) {
            deprecated.Add(root.GetPathTo(node).ToString());
        }
        foreach (Node child in node.GetChildren()) Walk(child, root, layers, deprecated);
    }
}
