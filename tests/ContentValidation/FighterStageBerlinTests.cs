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
/// Content contract for the Berlin Wall production Fighter stage
/// (Package 6 Phase B, `berlin_wall`, hazard type 8).
///
/// <para>The scene is a pixel mirror of <see cref="FighterStageGeometry.Berlin"/>
/// through <c>pixel = (950 + x·62.5, 700 − y·62.5)</c>: two high guard-tower
/// balconies at ±6.5 / 3.6, walls at ±10, and three searchlight anchors at
/// {−4, 0, 4}. Nothing in the engine enforces that mirror — the simulation is
/// authoritative and every collider here has <c>collision_mask = 0</c> — so the
/// shared conformance validator is the only thing keeping the drawn stage honest
/// about where the floor and balconies are.</para>
///
/// <para>One <c>[TestSuite]</c> per file: a second suite in the same file is
/// discovered but silently never executed (Package 6 §9, A4).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageBerlinTests {
    private const string ScenePath = "res://scenes/fighter/FighterStage_Berlin.tscn";
    private const string PoolConfigPath =
        "res://resources/Pools/fighter_stage_configs/fighter_stage_berlin_pool_config.tres";
    private const string AudioSetPath = "res://resources/Audio/stage_berlin_audio.tres";
    private const string PreviewPath = "res://assets/placeholders/stages/berlin_preview.svg";

    private const string StageID = "berlin_wall";
    private const int BerlinHazardTypeID = 8;

    [TestCase]
    public void SceneLoadsAndCarriesTheCatalogStageIdentity() {
        Node2D root = Instantiate();
        try {
            AssertThat(root.IsInGroup("fighter_stage")).IsTrue();
            var controller = root as FighterStageController;
            AssertObject(controller).IsNotNull();
            AssertThat(controller.StageID).IsEqual(StageID);

            var marker = root.GetNodeOrNull<ContentTemplateMarker>("ContentContract");
            AssertObject(marker).IsNotNull();
            AssertThat(marker.ContentID).IsEqual("fighter_stage_berlin");
            AssertObject(marker.Contract).IsNotNull();
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void SceneSatisfiesTheSharedFighterStageContract() {
        Node2D root = Instantiate();
        try {
            var marker = root.GetNode<ContentTemplateMarker>("ContentContract");
            AssertThat(marker.Contract.ContractID).IsEqual("fighter_stage_v1");
            IReadOnlyList<string> errors =
                ContentSceneContractValidator.Validate(root, marker.Contract);
            if (errors.Count > 0) AssertThat(string.Join(" | ", errors)).IsEqual("");
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The Package 6 §2.10 marker↔geometry check: spawns, orb points, hazard
    /// anchors, both balconies, the floor plane and both walls must land on the
    /// authored fixed-point values within a pixel.
    /// </summary>
    [TestCase]
    public void SceneMirrorsItsAuthoredFixedPointGeometry() {
        Node2D root = Instantiate();
        try {
            List<string> issues =
                FighterStageConformance.Validate(root, FighterStageGeometry.Berlin);
            if (issues.Count > 0) {
                AssertThat("berlin conformance: " + string.Join(" | ", issues)).IsEqual("");
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// Presentation is free, but the collision contract is not: solid bodies on
    /// layer 64, one-way balconies on 128, and a zero mask everywhere so no scene
    /// collider can ever push a fighter the simulation did not move.
    /// </summary>
    [TestCase]
    public void GeometryBodiesUseTheAuthoritativeSimulationCollisionContract() {
        Node2D root = Instantiate();
        try {
            var bodies = new List<StaticBody2D>();
            Collect(root.GetNode<Node2D>("Geometry"), bodies);
            AssertThat(bodies.Count).IsEqual(5);

            int solid = 0;
            int oneWay = 0;
            foreach (StaticBody2D body in bodies) {
                AssertThat(body.CollisionMask).IsEqual(0u);
                if (body.CollisionLayer == 64) solid++;
                else if (body.CollisionLayer == 128) oneWay++;
                else AssertThat($"{body.Name} layer {body.CollisionLayer}").IsEqual("");
            }
            AssertThat(solid).IsEqual(2 + 1);   // two walls plus the snowy street
            AssertThat(oneWay).IsEqual(2);      // the two guard-tower balconies
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void CameraUsesTheReferenceCanvasLimitsAndZoomBand() {
        Node2D root = Instantiate();
        try {
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
    public void PoolConfigIsBudgetedAndOwnsItsOwnConfigID() {
        var config = AuthoredResources.Load<ScenePoolConfig>(PoolConfigPath);
        AssertObject(config).IsNotNull();
        AssertThat(config.ConfigID).IsEqual("fighter_stage_berlin_pools");

        IReadOnlyList<string> errors = config.ValidateBudget();
        if (errors.Count > 0) AssertThat(string.Join(" | ", errors)).IsEqual("");

        var poolIDs = new HashSet<string>();
        foreach (PoolDefinition definition in config.PoolDefinitions) poolIDs.Add(definition.PoolID);
        string[] expected = {
            "fighter_projectile", "fighter_vfx", "fighter_environment_vfx",
            "chronal_orb", "damage_numbers", "fighter_construct"
        };
        foreach (string poolID in expected) {
            if (!poolIDs.Contains(poolID)) AssertThat($"missing pool '{poolID}'").IsEqual("");
        }
        AssertThat(poolIDs.Count).IsEqual(expected.Length);
        AssertThat(config.GetWarmUpInstanceCount() <= config.MaxWarmUpInstances).IsTrue();
    }

    [TestCase]
    public void AudioSetMeetsTheSynchronizedStemContract() {
        var set = AuthoredResources.Load<StageAudioSet>(AudioSetPath);
        AssertObject(set).IsNotNull();
        List<string> issues = StageAudioSetTests.Validate(set, "audio_stage_berlin", StageID);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(set.StemsAreSynchronized).IsTrue();
    }

    /// <summary>
    /// The placeholder preview the closeout wires into the catalog's
    /// <c>PreviewTexturePath</c>. It must be a real imported texture, not just a
    /// file on disk — an un-imported SVG loads as null and the stage select would
    /// show an empty tile.
    /// </summary>
    [TestCase]
    public void PreviewImageIsAnImportedTexture() {
        AssertThat(ResourceLoader.Exists(PreviewPath)).IsTrue();
        var preview = ResourceLoader.Load<Texture2D>(PreviewPath);
        AssertObject(preview).IsNotNull();
        AssertThat(preview.GetWidth() > 0 && preview.GetHeight() > 0).IsTrue();
    }

    /// <summary>
    /// Two simulations on Berlin's geometry, hazards High, run past the first
    /// searchlight's entire life: spawn at frame 1800, 90-frame warning, 480-frame
    /// active window (the dwell hazard's whole sweep) and the 60-frame recovery,
    /// despawning at 2430. Any float, unordered iteration or wall-clock read in the
    /// hazard path diverges the two hashes inside that window.
    /// </summary>
    [TestCase]
    public void BerlinRunsIdenticallyAcrossTwoSimulationsThroughAFullSearchlightCycle() {
        FighterMatchRules rules = new(
            (int)MatchMode.Stock,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardFrequency: (int)HazardTriggerFrequency.High,
            stageHazardTypeID: BerlinHazardTypeID);

        var first = new FighterSimulation(
            seed: 8611, rules: rules, stageGeometry: FighterStageGeometry.Berlin);
        var second = new FighterSimulation(
            seed: 8611, rules: rules, stageGeometry: FighterStageGeometry.Berlin);

        bool sawWarning = false;
        bool sawActive = false;
        bool sawRecovery = false;
        bool sawDespawn = false;

        for (int tick = 0; tick < 2500; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            long hashOne = first.Advance(p1, p2);
            long hashTwo = second.Advance(p1, p2);
            if (hashOne != hashTwo) AssertThat($"desync at tick {tick}").IsEqual("");

            if (tick < 1800) continue;
            if (first.TryGetFirstHazard(out FighterHazardComponent hazard)) {
                if (hazard.Phase == 0) sawWarning = true;
                else if (hazard.Phase == 1) sawActive = true;
                else if (hazard.Phase == 2) sawRecovery = true;
                // The searchlight is a tall column standing on one of the three
                // authored anchors; it never moves off them.
                bool onAnchor = false;
                foreach (FP64 anchorX in FighterStageGeometry.Berlin.HazardAnchorXs) {
                    if (hazard.Position.x.RawValue == anchorX.RawValue) onAnchor = true;
                }
                AssertThat(onAnchor).IsTrue();
            } else if (sawRecovery) {
                sawDespawn = true;
            }
        }

        // A finished match stops the hazard system, which would make the phase
        // assertions below vacuous rather than failing loudly.
        AssertThat(first.GetMatchState().MatchState).IsEqual(1);
        AssertThat(sawWarning).IsTrue();
        AssertThat(sawActive).IsTrue();
        AssertThat(sawRecovery).IsTrue();
        AssertThat(sawDespawn).IsTrue();
    }

    /// <summary>
    /// Scenes are streamed content and must not go through
    /// <c>AuthoredResources</c>. The root is never added to the tree, so the
    /// controller's <c>_Ready</c> does not build fighters or a simulation driver.
    /// </summary>
    private static Node2D Instantiate() {
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        var root = packed.Instantiate<Node2D>();
        AssertObject(root).IsNotNull();
        return root;
    }

    private static void Collect(Node node, List<StaticBody2D> found) {
        foreach (Node child in node.GetChildren()) {
            if (child is StaticBody2D body) found.Add(body);
            Collect(child, found);
        }
    }

    /// <summary>
    /// Movement only, deliberately: 2500 frames of random attacking KOs a fighter
    /// and ends the match, and a finished match stops the hazard system dead —
    /// the searchlight would freeze mid-active and never reach recovery. Walking,
    /// jumping and rolling keeps both fighters alive, keeps them crossing the
    /// three beam anchors, and still drives every deterministic system this run
    /// is meant to compare.
    /// </summary>
    private static PlayerInputFrame InputFor(int playerID, int tick) {
        sbyte axis = (sbyte)(((tick + playerID * 13) % 7 - 3) * 40);
        GameplayButtons buttons = GameplayButtons.None;
        if (tick % 43 == 0) buttons |= GameplayButtons.Jump;
        if (tick % 137 == 0) buttons |= GameplayButtons.Roll;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = axis,
            Held = buttons,
            Pressed = buttons
        };
    }
}
