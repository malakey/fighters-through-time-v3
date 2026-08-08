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
/// Production contract for the Globe Theatre Fighter stage (Package 6 plan §4 row
/// <c>globe_theatre</c>, hazard identity 9).
///
/// <para>The scene is a pixel mirror of <see cref="FighterStageGeometry.Globe"/>
/// through <c>pixel = (950 + x·62.5, 700 − y·62.5)</c>: an open wooden stage with
/// two tiered audience galleries (the ±5 one-way platforms) and the balcony above
/// the tiring-house (the high centre platform at 4.4). The three hazard anchors are
/// the gallery boxes the hecklers pelt from.</para>
///
/// <para>The determinism case matters more here than on a static-hazard stage:
/// Audience Heckle fires off per-fighter <i>idle</i> counters carried in the hazard
/// component, so the run below deliberately drives one fighter that never stops
/// pacing and one that never moves, and crosses the full warning → active →
/// recovery → despawn cycle in lockstep across two simulations.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageGlobeTests {
    private const string ScenePath = "res://scenes/fighter/FighterStage_Globe.tscn";
    private const string PoolConfigPath =
        "res://resources/Pools/fighter_stage_configs/fighter_stage_globe_pool_config.tres";
    private const string AudioSetPath = "res://resources/Audio/stage_globe_audio.tres";
    private const string PreviewPath = "res://assets/placeholders/stages/globe_preview.svg";

    private const string StageID = "globe_theatre";
    private const int HazardTypeID = 9;

    [TestCase]
    public void GlobeSceneLoadsInstantiatesAndCarriesItsCatalogIdentity() {
        Node2D root = Instantiate();
        try {
            AssertThat(root.IsInGroup("fighter_stage")).IsTrue();
            var controller = root as FighterStageController;
            AssertObject(controller).IsNotNull();
            AssertThat(controller.StageID).IsEqual(StageID);
            AssertThat(FighterStageGeometry.Globe.StageID).IsEqual(StageID);
            AssertThat(FighterStageGeometry.ForStage(StageID).StageID).IsEqual(StageID);
            AssertThat(FighterStageGeometry.ForStage(StageID).HazardAnchorXs.Length).IsEqual(3);
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void GlobeSceneSatisfiesTheFighterStageSceneContract() {
        Node2D root = Instantiate();
        try {
            var marker = root.GetNodeOrNull<ContentTemplateMarker>("ContentContract");
            AssertObject(marker).IsNotNull();
            AssertThat(marker.ContentID).IsEqual("fighter_stage_globe");
            AssertObject(marker.Contract).IsNotNull();
            AssertThat(marker.Contract.ContractID).IsEqual("fighter_stage_v1");

            IReadOnlyList<string> errors = marker.ValidateTemplate();
            if (errors.Count > 0) AssertThat(string.Join(" | ", errors)).IsEqual("");

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

    /// <summary>
    /// The single highest-risk drift point for an authored stage: every marker and
    /// collider must agree with the fixed-point geometry the simulation actually
    /// uses. Note the A1 rule that a one-way platform body's <i>origin</i> is the
    /// surface point, not the top edge of its collision rect.
    /// </summary>
    [TestCase]
    public void GlobeSceneMirrorsItsAuthoredFixedPointGeometry() {
        Node2D root = Instantiate();
        try {
            List<string> issues = FighterStageConformance.Validate(root, FighterStageGeometry.Globe);
            if (issues.Count > 0) {
                AssertThat("globe conformance: " + string.Join(" | ", issues)).IsEqual("");
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// Collision authoring: the simulation is authoritative, so every body masks
    /// nothing, solids sit on layer 64 and the three galleries on the one-way
    /// layer 128.
    /// </summary>
    [TestCase]
    public void GlobeCollidersAreSimulationMirrorsAndNeverQueryTheEngine() {
        Node2D root = Instantiate();
        try {
            var geometry = root.GetNode<Node2D>("Geometry");
            var solids = new List<StaticBody2D>();
            var oneWay = new List<StaticBody2D>();
            foreach (Node child in geometry.GetChildren()) {
                if (child is not StaticBody2D body) continue;
                AssertThat((int)body.CollisionMask).IsEqual(0);
                if (body.CollisionLayer == 64) solids.Add(body);
                else if (body.CollisionLayer == 128) oneWay.Add(body);
                else AssertThat($"{body.Name} on layer {body.CollisionLayer}").IsEqual("");
            }
            // Floor plus the two solid walls.
            AssertThat(solids.Count).IsEqual(3);
            // Two galleries and the tiring-house balcony.
            AssertThat(oneWay.Count).IsEqual(3);
            AssertThat(oneWay.Count).IsEqual(FighterStageGeometry.Globe.Platforms.Length);
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// Presentation deliverable: a backdrop tint plus a parallax background with at
    /// least two layers at distinct scroll factors, and the gallery-box dressing
    /// that makes the heckle hazard legible.
    /// </summary>
    [TestCase]
    public void GlobePresentationCarriesATintedParallaxBackdropAndGalleryDressing() {
        Node2D root = Instantiate();
        try {
            var presentation = root.GetNode<Node2D>("Presentation");
            AssertObject(presentation.GetNodeOrNull<ColorRect>("BackdropTint")).IsNotNull();

            // Reached through the untyped API on purpose: the C# ParallaxBackground /
            // ParallaxLayer bindings carry [Obsolete] in Godot 4.7 (superseded by
            // Parallax2D) and referencing them would add CS0618 to a clean build,
            // while the plan's §5 deliverable names ParallaxBackground explicitly.
            var parallax = presentation.GetNodeOrNull<CanvasLayer>("ParallaxBackground");
            AssertObject(parallax).IsNotNull();
            AssertThat(parallax.IsClass("ParallaxBackground")).IsTrue();

            var scrollFactors = new List<float>();
            foreach (Node child in parallax.GetChildren()) {
                if (child.IsClass("ParallaxLayer")) {
                    scrollFactors.Add(child.Get("motion_scale").AsVector2().X);
                }
            }
            AssertThat(scrollFactors.Count >= 2).IsTrue();
            // Distinct scroll factors, or it is one flat backdrop wearing two nodes.
            AssertThat(Mathf.Abs(scrollFactors[0] - scrollFactors[1]) > 0.01f).IsTrue();

            // Silhouetted patrons occupy the gallery boxes the crowd throws from.
            AssertObject(root.GetNodeOrNull<Polygon2D>("Geometry/GalleryLeft/PatronA")).IsNotNull();
            AssertObject(root.GetNodeOrNull<Polygon2D>("Geometry/GalleryRight/PatronA")).IsNotNull();
            AssertObject(root.GetNodeOrNull<Node2D>("Presentation/TiringHouse")).IsNotNull();
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void GlobePreviewArtIsAuthoredAndLoadable() {
        AssertThat(ResourceLoader.Exists(PreviewPath)).IsTrue();
        var preview = ResourceLoader.Load<Texture2D>(PreviewPath);
        AssertObject(preview).IsNotNull();
        AssertThat(preview.GetWidth() > 0 && preview.GetHeight() > 0).IsTrue();
    }

    [TestCase]
    public void GlobePoolConfigIsValidUniquelyIdentifiedAndBudgeted() {
        ScenePoolConfig config = AuthoredResources.Load<ScenePoolConfig>(PoolConfigPath);
        AssertObject(config).IsNotNull();
        AssertThat(config.ConfigID).IsEqual("fighter_stage_globe_pools");

        IReadOnlyList<string> errors = config.ValidateBudget();
        if (errors.Count > 0) AssertThat(string.Join("; ", errors)).IsEqual("");
        AssertThat(config.GetWarmUpInstanceCount() <= config.MaxWarmUpInstances).IsTrue();
        AssertThat(config.GetMaxCapacityCount() >= config.GetWarmUpInstanceCount()).IsTrue();

        // Mirrors the Test Arena Fighter pool set exactly; a missing pool is a
        // gameplay-time instantiate spike, not a cosmetic difference.
        var poolIDs = new HashSet<string>();
        foreach (PoolDefinition definition in config.PoolDefinitions) {
            AssertThat(definition.HasValidBudget()).IsTrue();
            poolIDs.Add(definition.PoolID);
        }
        foreach (string expected in new[] {
                     "fighter_projectile", "fighter_vfx", "fighter_environment_vfx",
                     "chronal_orb", "damage_numbers", "fighter_construct" }) {
            AssertThat(poolIDs.Contains(expected)).OverrideFailureMessage(
                $"globe pool config is missing '{expected}'.").IsTrue();
        }

        ScenePoolConfig testArena =
            AuthoredResources.Load<ScenePoolConfig>("res://resources/Pools/test_arena_pool_config.tres");
        AssertThat(config.GetWarmUpInstanceCount()).IsEqual(testArena.GetWarmUpInstanceCount());
        AssertThat(config.GetMaxCapacityCount()).IsEqual(testArena.GetMaxCapacityCount());
    }

    [TestCase]
    public void GlobeAudioSetSatisfiesTheStageStemContract() {
        StageAudioSet set = AuthoredResources.Load<StageAudioSet>(AudioSetPath);
        AssertObject(set).IsNotNull();
        List<string> issues = StageAudioSetTests.Validate(set, "audio_stage_globe", StageID);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(set.StemsAreSynchronized).IsTrue();
    }

    /// <summary>
    /// Two simulations, identical inputs, hash-compared every tick across a full
    /// hazard cycle. Player one paces so the crowd never has a target; player two
    /// stands perfectly still and is pelted from the nearest gallery once every
    /// <c>HeckleIdleFrames</c>. Both facts are asserted, so a run that silently
    /// stopped short of the active window cannot pass as determinism evidence.
    /// </summary>
    [TestCase]
    public void GlobeRunsIdenticallyAcrossTwoSimulationsThroughAFullHeckleCycle() {
        FighterMatchRules rules = new(
            (int)MatchMode.Hybrid,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardFrequency: (int)HazardTriggerFrequency.High,
            stageHazardTypeID: HazardTypeID);

        const int seed = 60109;
        var first = new FighterSimulation(seed: seed, rules: rules, stageGeometry: FighterStageGeometry.Globe);
        var second = new FighterSimulation(seed: seed, rules: rules, stageGeometry: FighterStageGeometry.Globe);

        var phasesSeen = new HashSet<int>();
        bool hazardExisted = false;
        bool hazardDespawned = false;
        bool anchorMatched = false;
        int spawnedActiveFrames = -1;
        int spawnedDamage = -1;

        for (int tick = 0; tick < 2650; tick++) {
            PlayerInputFrame pacing = PacingInput(tick);
            PlayerInputFrame still = new() { Tick = (uint)tick };
            long firstHash = first.Advance(pacing, still);
            long secondHash = second.Advance(pacing, still);
            AssertThat(firstHash).OverrideFailureMessage(
                $"globe_theatre diverged at tick {tick}.").IsEqual(secondHash);

            if (first.TryGetFirstHazard(out FighterHazardComponent hazard)) {
                if (!hazardExisted) {
                    hazardExisted = true;
                    spawnedActiveFrames = hazard.ActiveFrames;
                    spawnedDamage = hazard.Damage;
                    foreach (FP64 anchorX in FighterStageGeometry.Globe.HazardAnchorXs) {
                        if (hazard.Position.x.RawValue == anchorX.RawValue) anchorMatched = true;
                    }
                }
                phasesSeen.Add(hazard.Phase);
            } else if (hazardExisted) {
                hazardDespawned = true;
            }
        }

        // The Globe crowd watches for a full ten seconds after a 1.5 s murmur, then
        // settles: warning, active and recovery all observed, then the entity is gone.
        AssertThat(hazardExisted).IsTrue();
        AssertThat(anchorMatched).IsTrue();
        AssertThat(spawnedActiveFrames).IsEqual(600);
        AssertThat(spawnedDamage).IsEqual(5);
        AssertThat(phasesSeen.Contains(FighterHazardSystem.WarningPhase)).IsTrue();
        AssertThat(phasesSeen.Contains(FighterHazardSystem.ActivePhase)).IsTrue();
        AssertThat(phasesSeen.Contains(FighterHazardSystem.RecoveryPhase)).IsTrue();
        AssertThat(hazardDespawned).IsTrue();

        AssertThat(first.TryGetFighter(0, out FighterStateComponent pacer)).IsTrue();
        AssertThat(first.TryGetFighter(1, out FighterStateComponent camper)).IsTrue();
        AssertThat(second.TryGetFighter(1, out FighterStateComponent mirrorCamper)).IsTrue();

        // A moving fighter is never hit; a camping one is pelted for 5 a throw.
        AssertThat(pacer.CurrentHP).IsEqual(100);
        AssertThat(camper.CurrentHP <= 95).OverrideFailureMessage(
            $"the camping fighter was never heckled (HP {camper.CurrentHP}).").IsTrue();
        AssertThat((100 - camper.CurrentHP) % 5).IsEqual(0);
        AssertThat(mirrorCamper.CurrentHP).IsEqual(camper.CurrentHP);
    }

    /// <summary>
    /// Player one reverses every 40 frames and hops occasionally, so its idle
    /// counter can never reach the 120 consecutive frames the crowd needs.
    /// </summary>
    private static PlayerInputFrame PacingInput(int tick) {
        sbyte axis = tick / 40 % 2 == 0 ? (sbyte)127 : (sbyte)-127;
        GameplayButtons buttons = tick % 97 == 0 ? GameplayButtons.Jump : GameplayButtons.None;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = axis,
            Held = buttons,
            Pressed = buttons
        };
    }

    /// <summary>
    /// Scenes are streamed content and must never go through
    /// <c>AuthoredResources</c>. The root is deliberately kept out of the tree so
    /// <c>FighterStageController._Ready</c> does not build fighters or a driver.
    /// </summary>
    private static Node2D Instantiate() {
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        var root = packed.Instantiate<Node2D>();
        AssertObject(root).IsNotNull();
        return root;
    }
}
