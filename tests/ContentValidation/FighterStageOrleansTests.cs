using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Content contract for the Orléans Vanguard production Fighter stage
/// (Package 6 Phase B). The scene is a pixel mirror of
/// <see cref="FighterStageGeometry.Orleans"/> — siege battlements at Orléans, 1429,
/// with two flanking rampart platforms and a high keep — and nothing in the engine
/// enforces that mirror, so the A1 conformance validator is the load-bearing
/// assertion here.
///
/// <para>One <c>[TestSuite]</c> per file: a second suite in the same file is
/// discovered but silently not executed (plan §9, ORCHESTRATOR Phase A block).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageOrleansTests {
    private const string ScenePath = "res://scenes/fighter/FighterStage_Orleans.tscn";
    private const string PoolConfigPath =
        "res://resources/Pools/fighter_stage_configs/fighter_stage_orleans_pool_config.tres";
    private const string AudioSetPath = "res://resources/Audio/stage_orleans_audio.tres";
    private const string PreviewPath = "res://assets/placeholders/stages/orleans_preview.svg";
    private const string StageID = "orleans_vanguard";

    /// <summary>Orléans is hazard type 2, the rolling trebuchet debris.</summary>
    private const int OrleansHazardTypeID = 2;

    [TestCase]
    public void TheStageSceneLoadsInstantiatesAndDeclaresItsCatalogIdentity() {
        Node2D root = Instantiate();
        try {
            AssertThat(root.IsInGroup("fighter_stage")).IsTrue();
            var controller = root as FighterStageController;
            AssertObject(controller).IsNotNull();
            AssertThat(controller.StageID).IsEqual(StageID);

            // The camera contract the stage-select flow and FighterCamera rely on.
            var camera = root.GetNodeOrNull<FTT.Combat.FighterCamera>("Camera2D");
            AssertObject(camera).IsNotNull();
            AssertThat(camera.LimitLeft).IsEqual(0);
            AssertThat(camera.LimitTop).IsEqual(0);
            AssertThat(camera.LimitRight).IsEqual(1920);
            AssertThat(camera.LimitBottom).IsEqual(1080);
            AssertThat(camera.MinZoom).IsEqual(1.0f);
            AssertThat(camera.MaxZoom).IsEqual(1.4f);
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void TheStageSatisfiesTheSharedFighterStageSceneContract() {
        Node2D root = Instantiate();
        try {
            var marker = root.GetNodeOrNull<ContentTemplateMarker>("ContentContract");
            AssertObject(marker).IsNotNull();
            AssertThat(marker.ContentID).IsEqual("fighter_stage_orleans");
            AssertObject(marker.Contract).IsNotNull();

            IReadOnlyList<string> errors = marker.ValidateTemplate();
            if (errors.Count > 0) {
                AssertThat("orleans contract: " + string.Join(" | ", errors)).IsEqual("");
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The load-bearing test: every marker, platform body, wall and floor extent
    /// must agree with the fixed-point geometry within one pixel. A one-way
    /// platform body's *origin* is its surface point (plan §9, A1).
    /// </summary>
    [TestCase]
    public void TheSceneMirrorsItsAuthoredFixedPointGeometry() {
        Node2D root = Instantiate();
        try {
            List<string> issues =
                FighterStageConformance.Validate(root, FighterStageGeometry.Orleans);
            if (issues.Count > 0) {
                AssertThat("orleans conformance: " + string.Join(" | ", issues)).IsEqual("");
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The three stone platforms and the two trebuchet-impact hazard anchors are
    /// the stage's identity; a scene that quietly loses one still passes the
    /// generic contract, so the counts and layers are pinned explicitly.
    /// </summary>
    [TestCase]
    public void TheRampartsKeepAndBreachAnchorsAreAuthoredWithTheCorrectCollisionSetup() {
        Node2D root = Instantiate();
        try {
            AssertThat(root.GetNode<Node2D>("OrbSpawnPoints").GetChildCount()).IsEqual(3);
            AssertThat(root.GetNode<Node2D>("HazardAnchors").GetChildCount()).IsEqual(2);

            string[] platformPaths = {
                "Geometry/RampartLeft", "Geometry/RampartRight", "Geometry/KeepPlatform"
            };
            foreach (string path in platformPaths) {
                var body = root.GetNodeOrNull<StaticBody2D>(path);
                AssertObject(body).OverrideFailureMessage($"missing {path}").IsNotNull();
                AssertThat(body.CollisionLayer).IsEqual(128u);
                AssertThat(body.CollisionMask).IsEqual(0u);
            }

            string[] solidPaths = { "Geometry/Ground", "Geometry/WallLeft", "Geometry/WallRight" };
            foreach (string path in solidPaths) {
                var body = root.GetNodeOrNull<StaticBody2D>(path);
                AssertObject(body).OverrideFailureMessage($"missing {path}").IsNotNull();
                AssertThat(body.CollisionLayer).IsEqual(64u);
                AssertThat(body.CollisionMask).IsEqual(0u);
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// Placeholder presentation is still a deliverable: the backdrop tint, the
    /// two-plus parallax layers at distinct scroll factors, and the era props are
    /// what makes the stage readable as Orléans rather than a grey box.
    /// </summary>
    [TestCase]
    public void ThePresentationLayerCarriesTheBackdropParallaxAndEraProps() {
        Node2D root = Instantiate();
        try {
            var tint = root.GetNodeOrNull<ColorRect>("Presentation/Backdrop/BackdropTint");
            AssertObject(tint).IsNotNull();

            // Package 8 B7 migrated the deprecated ParallaxBackground/ParallaxLayer
            // pair to Parallax2D; the typed replacement carries no [Obsolete], so the
            // CS0618 suppression is gone with it. Orléans keeps its opaque backdrop in
            // a deeper CanvasLayer (-2), which still sits behind canvas layer 0.
            var parallax = root.GetNodeOrNull<Node2D>("Presentation/Parallax");
            AssertObject(parallax).IsNotNull();

            var scrollFactors = new List<float>();
            foreach (Node child in parallax.GetChildren()) {
                if (child is Parallax2D layer) scrollFactors.Add(layer.ScrollScale.X);
            }
            AssertThat(scrollFactors.Count >= 2).IsTrue();
            // Distinct scroll factors, or it is not parallax.
            for (int outer = 0; outer < scrollFactors.Count; outer++) {
                for (int inner = outer + 1; inner < scrollFactors.Count; inner++) {
                    AssertThat(Mathf.Abs(scrollFactors[outer] - scrollFactors[inner]) > 0.01f).IsTrue();
                }
            }

            var props = root.GetNodeOrNull<Node2D>("Presentation/RampartProps");
            AssertObject(props).IsNotNull();
            AssertThat(props.GetChildCount() >= 4).IsTrue();
            // The two authored hazard anchors are dressed as trebuchet impact zones.
            AssertObject(props.GetNodeOrNull<ColorRect>("BreachScorchWest")).IsNotNull();
            AssertObject(props.GetNodeOrNull<ColorRect>("BreachScorchEast")).IsNotNull();
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void ThePreviewImageIsAuthoredAtThePathTheCatalogWillPointAt() {
        AssertThat(ResourceLoader.Exists(PreviewPath)).IsTrue();
        var texture = ResourceLoader.Load<Texture2D>(PreviewPath);
        AssertObject(texture).IsNotNull();
        AssertThat(texture.GetWidth() > 0 && texture.GetHeight() > 0).IsTrue();
    }

    [TestCase]
    public void TheStagePoolConfigIsUniquelyIdentifiedValidAndWithinItsWarmUpBudget() {
        var config = AuthoredResources.Load<ScenePoolConfig>(PoolConfigPath);
        AssertObject(config).IsNotNull();
        AssertThat(config.ConfigID).IsEqual("fighter_stage_orleans_pools");

        IReadOnlyList<string> errors = config.ValidateBudget();
        AssertThat(errors.Count)
            .OverrideFailureMessage("pool budget errors: " + string.Join("; ", errors)).IsEqual(0);
        AssertThat(config.GetWarmUpInstanceCount() <= config.MaxWarmUpInstances).IsTrue();
        AssertThat(config.GetMaxCapacityCount() >= config.GetWarmUpInstanceCount()).IsTrue();

        // Mirrors the Test Arena pool set: a Fighter match on this stage spawns the
        // same object families, so a missing pool means runtime instantiation churn.
        var poolIDs = new HashSet<string>();
        foreach (PoolDefinition definition in config.PoolDefinitions) poolIDs.Add(definition.PoolID);
        string[] required = {
            "fighter_projectile", "fighter_vfx", "fighter_environment_vfx",
            "chronal_orb", "damage_numbers", "fighter_construct"
        };
        foreach (string poolID in required) {
            AssertThat(poolIDs.Contains(poolID)).OverrideFailureMessage($"missing pool '{poolID}'").IsTrue();
        }
    }

    [TestCase]
    public void TheStageAudioSetMeetsTheSynchronizedStemContract() {
        var set = AuthoredResources.Load<StageAudioSet>(AudioSetPath);
        AssertObject(set).IsNotNull();

        List<string> issues = StageAudioSetTests.Validate(set, "audio_stage_orleans", StageID);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(set.StemsAreSynchronized).IsTrue();
    }

    /// <summary>
    /// Two simulations seeded identically on the Orléans geometry, with hazards at
    /// High, must agree on every frame hash across a full trebuchet-debris cycle:
    /// the hazard spawns on the 1800-frame boundary, telegraphs for 90 frames,
    /// rolls the width of the ramparts, then runs its 60-frame recovery.
    /// </summary>
    [TestCase]
    public void TheStageRunsIdenticallyAcrossTwoSimulationsThroughAFullHazardCycle() {
        FighterMatchRules rules = new(
            (int)MatchMode.Stock,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardCadenceFrames: 1800,
            stageHazardTypeID: OrleansHazardTypeID);

        var first = new FighterSimulation(
            seed: 6202, rules: rules, stageGeometry: FighterStageGeometry.Orleans);
        var second = new FighterSimulation(
            seed: 6202, rules: rules, stageGeometry: FighterStageGeometry.Orleans);

        bool sawWarning = false;
        bool sawActiveDebrisTravel = false;
        FP64 spawnX = FP64.Zero;

        for (int tick = 0; tick < 2400; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            long hashOne = first.Advance(p1, p2);
            long hashTwo = second.Advance(p1, p2);
            if (hashOne != hashTwo) {
                AssertThat($"orleans hash divergence at tick {tick}").IsEqual("");
            }

            if (!first.TryGetFirstHazard(out FighterHazardComponent hazard)) continue;
            if (hazard.Phase == 0) {
                sawWarning = true;
                spawnX = hazard.Position.x;
            } else if (hazard.Phase == 1 && sawWarning) {
                // Rolling debris: the boulder leaves the wall anchor it was launched over.
                FP64 travelled = hazard.Position.x - spawnX;
                if (travelled > FP64.One || travelled < -FP64.One) sawActiveDebrisTravel = true;
            }
        }

        AssertThat(sawWarning).OverrideFailureMessage("no hazard warning phase observed").IsTrue();
        AssertThat(sawActiveDebrisTravel)
            .OverrideFailureMessage("the trebuchet debris never rolled across the ramparts").IsTrue();
    }

    /// <summary>
    /// Scenes are streamed content and must never go through
    /// <c>AuthoredResources</c>. The root is never added to the tree, so
    /// <c>FighterStageController._Ready</c> does not spawn fighters or a driver.
    /// </summary>
    private static Node2D Instantiate() {
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        var root = packed.Instantiate<Node2D>();
        AssertObject(root).IsNotNull();
        return root;
    }

    private static PlayerInputFrame InputFor(int playerID, int tick) {
        sbyte axis = (sbyte)(((tick + playerID * 13) % 7 - 3) * 40);
        GameplayButtons buttons = GameplayButtons.None;
        if (tick % 53 == 0) buttons |= GameplayButtons.Jump;
        if (tick % 37 == 0) buttons |= GameplayButtons.BasicAttack;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = axis,
            Held = buttons,
            Pressed = buttons
        };
    }
}
