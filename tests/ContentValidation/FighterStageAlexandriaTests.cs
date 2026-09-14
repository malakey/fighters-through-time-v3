using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Content contract for the Alexandria Chambers production Fighter stage
/// (Package 6 Phase B): Cleopatra's palace, 30 BC — a sandy floor, two carved
/// sarcophagi as the lowest platforms in the catalog, and three shifting-sand
/// sinkhole anchors (<c>HazardTypeID</c> 7).
///
/// <para>The deterministic simulation owns every boundary in
/// <see cref="FighterStageGeometry.Alexandria"/>; the scene is only a pixel
/// mirror of it. That mirror is what
/// <see cref="FighterStageConformance"/> enforces here — nothing in the engine
/// would otherwise notice a sarcophagus drawn 40 px above the surface the
/// fighters actually land on.</para>
///
/// <para>One <c>[TestSuite]</c> per file: a second suite in the same file is
/// discovered but silently never executed (Package 6 plan §9, A4).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageAlexandriaTests {
    private const string ScenePath = "res://scenes/fighter/FighterStage_Alexandria.tscn";
    private const string PoolConfigPath =
        "res://resources/Pools/fighter_stage_configs/fighter_stage_alexandria_pool_config.tres";
    private const string AudioSetPath = "res://resources/Audio/stage_alexandria_audio.tres";
    private const string PreviewPath = "res://assets/placeholders/stages/alexandria_preview.svg";

    private const string StageID = "alexandria_chambers";
    private const int AlexandriaHazardTypeID = 7;

    [TestCase]
    public void TheStageSceneLoadsAndInstantiatesWithItsCatalogIdentity() {
        Node2D root = Instantiate();
        try {
            AssertThat(root.IsInGroup("fighter_stage")).IsTrue();
            var controller = root as FighterStageController;
            AssertObject(controller).IsNotNull();
            AssertThat(controller.StageID).IsEqual(StageID);

            // The camera contract the driver and FighterCamera both assume.
            var camera = root.GetNodeOrNull<Camera2D>("Camera2D");
            AssertObject(camera).IsNotNull();
            AssertThat(camera.LimitLeft).IsEqual(0);
            AssertThat(camera.LimitTop).IsEqual(0);
            AssertThat(camera.LimitRight).IsEqual(1920);
            AssertThat(camera.LimitBottom).IsEqual(1080);
            var fighterCamera = camera as FTT.Combat.FighterCamera;
            AssertObject(fighterCamera).IsNotNull();
            AssertThat(fighterCamera.MinZoom).IsEqual(1.0f);
            AssertThat(fighterCamera.MaxZoom).IsEqual(1.4f);
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
            AssertThat(marker.ContentID).IsEqual("fighter_stage_alexandria");
            AssertObject(marker.Contract).IsNotNull();
            AssertThat(marker.Contract.ContractID).IsEqual("fighter_stage_v1");

            IReadOnlyList<string> errors = ContentSceneContractValidator.Validate(root, marker.Contract);
            if (errors.Count > 0) {
                AssertThat("alexandria contract: " + string.Join(" | ", errors)).IsEqual("");
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The single highest-risk drift point for a hand-authored stage: markers and
    /// colliders must mirror the fixed-point geometry through
    /// <c>pixel = (950 + x·62.5, 700 − y·62.5)</c>. A one-way platform body's
    /// <em>origin</em> is the surface point, not the top edge of its rect
    /// (plan §9, A1).
    /// </summary>
    [TestCase]
    public void TheSceneMirrorsItsAuthoredFixedPointGeometry() {
        Node2D root = Instantiate();
        try {
            List<string> issues =
                FighterStageConformance.Validate(root, FighterStageGeometry.Alexandria);
            if (issues.Count > 0) {
                AssertThat("alexandria conformance: " + string.Join(" | ", issues)).IsEqual("");
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The presentation deliverables that make the stage readable as Alexandria:
    /// a backdrop tint, a parallax background with at least two independently
    /// scrolling layers, and hazard-anchor dressing for the sand sinkholes.
    /// </summary>
    [TestCase]
    public void ThePresentationCarriesABackdropAndAMultiLayerParallax() {
        Node2D root = Instantiate();
        try {
            var presentation = root.GetNodeOrNull<Node2D>("Presentation");
            AssertObject(presentation).IsNotNull();
            AssertObject(presentation.GetNodeOrNull<ColorRect>("BackdropTint")).IsNotNull();

            // Package 8 B7 migrated the deprecated ParallaxBackground/ParallaxLayer
            // pair to Parallax2D, so this reads the typed replacement class directly
            // and the CS0618 suppression the untyped workaround needed is gone. The
            // container is now a plain Node2D holding one Parallax2D per layer.
            var parallax = presentation.GetNodeOrNull<Node2D>("Parallax");
            AssertObject(parallax).IsNotNull();

            var scrollFactors = new List<Vector2>();
            foreach (Node child in parallax.GetChildren()) {
                if (child is Parallax2D layer) {
                    scrollFactors.Add(layer.ScrollScale);
                    // An empty layer scrolls nothing; each must carry artwork.
                    bool hasArt = false;
                    foreach (Node grandchild in layer.GetChildren()) {
                        if (grandchild is Sprite2D sprite && sprite.Texture != null) hasArt = true;
                    }
                    AssertThat(hasArt).OverrideFailureMessage(
                        $"parallax layer '{layer.Name}' has no textured Sprite2D").IsTrue();
                }
            }
            AssertThat(scrollFactors.Count >= 2).IsTrue();
            // Distinct scroll factors are the whole point of a parallax.
            AssertThat(scrollFactors[0].X).IsNotEqual(scrollFactors[1].X);

            // One piece of sand dressing per authored sinkhole anchor.
            int dressing = 0;
            foreach (Node child in presentation.GetChildren()) {
                if (child.Name.ToString().StartsWith("SinkholeDressing")) dressing++;
            }
            AssertThat(dressing).IsEqual(FighterStageGeometry.Alexandria.HazardAnchorXs.Length);
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void ThePreviewImageIsAuthored() {
        AssertObject(ResourceLoader.Load<Texture2D>(PreviewPath)).IsNotNull();
    }

    [TestCase]
    public void ThePoolConfigIsUniquelyIdentifiedValidAndWithinItsWarmUpBudget() {
        ScenePoolConfig config = AuthoredResources.Load<ScenePoolConfig>(PoolConfigPath);
        AssertObject(config).IsNotNull();
        AssertThat(config.ConfigID).IsEqual("fighter_stage_alexandria_pools");

        IReadOnlyList<string> errors = config.ValidateBudget();
        // GdUnit4's OverrideFailureMessage rejects an empty string, so the message
        // must stay non-empty even on the passing path.
        AssertThat(errors.Count).OverrideFailureMessage(
            "alexandria pool budget: " + string.Join("; ", errors)).IsEqual(0);
        AssertThat(config.GetWarmUpInstanceCount() <= config.MaxWarmUpInstances).IsTrue();
        AssertThat(config.GetMaxCapacityCount() >= config.GetWarmUpInstanceCount()).IsTrue();

        // Mirrors test_arena_pool_config.tres: a Fighter match spawns into all six.
        var poolIDs = new HashSet<string>();
        foreach (PoolDefinition definition in config.PoolDefinitions) {
            AssertThat(definition.HasValidBudget()).OverrideFailureMessage(
                $"pool '{definition.PoolID}' has an invalid budget").IsTrue();
            poolIDs.Add(definition.PoolID);
        }
        foreach (string poolID in new[] {
            "fighter_projectile", "fighter_vfx", "fighter_environment_vfx",
            "chronal_orb", "damage_numbers", "fighter_construct" }) {
            AssertThat(poolIDs.Contains(poolID)).OverrideFailureMessage(
                $"alexandria pool config is missing '{poolID}'").IsTrue();
        }
    }

    [TestCase]
    public void TheAudioSetMeetsTheSynchronizedStemContract() {
        StageAudioSet set = AuthoredResources.Load<StageAudioSet>(AudioSetPath);
        AssertObject(set).IsNotNull();

        List<string> issues = StageAudioSetTests.Validate(set, "audio_stage_alexandria", StageID);
        if (issues.Count > 0) {
            AssertThat("alexandria audio: " + string.Join(" | ", issues)).IsEqual("");
        }
        AssertThat(set.StemsAreSynchronized).IsTrue();
    }

    /// <summary>
    /// Determinism gate. Two simulations on Alexandria's geometry with orbs and
    /// hazards at High must agree on every frame hash across a full sinkhole
    /// cycle: the first hazard spawns on the 1800-frame High boundary, then runs
    /// 90 warning + 480 active + 60 recovery frames.
    /// </summary>
    [TestCase]
    public void AlexandriaRunsIdenticallyAcrossTwoSimulationsThroughAFullHazardCycle() {
        FighterMatchRules rules = new(
            (int)MatchMode.Stock,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardFrequency: (int)HazardTriggerFrequency.High,
            stageHazardTypeID: AlexandriaHazardTypeID);

        const int seed = 3007;
        var first = new FighterSimulation(
            seed: seed, rules: rules, stageGeometry: FighterStageGeometry.Alexandria);
        var second = new FighterSimulation(
            seed: seed, rules: rules, stageGeometry: FighterStageGeometry.Alexandria);

        // 1800 spawn + 90 warning + 480 active + 60 recovery = 2430; run past it.
        bool sawActiveSinkhole = false;
        for (int tick = 0; tick < 2500; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            AssertThat(first.Advance(p1, p2)).OverrideFailureMessage(
                $"alexandria hash divergence at tick {tick}").IsEqual(second.Advance(p1, p2));
            if (first.TryGetFirstHazard(out FighterHazardComponent hazard) && hazard.Phase == 1) {
                sawActiveSinkhole = true;
            }
        }
        AssertThat(sawActiveSinkhole).OverrideFailureMessage(
            "the run never reached an active sinkhole phase").IsTrue();
    }

    /// <summary>
    /// Scenes are streamed content and must never go through
    /// <see cref="AuthoredResources"/>. The root is never added to the tree, so
    /// the controller's <c>_Ready</c> does not spawn fighters or a driver.
    /// </summary>
    private static Node2D Instantiate() {
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        var root = packed.Instantiate<Node2D>();
        AssertObject(root).IsNotNull();
        return root;
    }

    private static PlayerInputFrame InputFor(int playerID, int tick) {
        sbyte axis = (sbyte)(((tick + playerID * 17) % 7 - 3) * 40);
        GameplayButtons buttons = GameplayButtons.None;
        if (tick % 47 == 0) buttons |= GameplayButtons.Jump;
        if (tick % 31 == 0) buttons |= GameplayButtons.BasicAttack;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = axis,
            Held = buttons,
            Pressed = buttons
        };
    }
}
