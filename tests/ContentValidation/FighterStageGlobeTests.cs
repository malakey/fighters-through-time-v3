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
/// the tiring-house (the high centre platform, lowered 4.4 → 4.0 by the 2026-08-10
/// feel batch §2.2 so it stays inside the reduced double-jump envelope). The three
/// hazard anchors are
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
    private static readonly FighterStageVisualProfile Profile = FighterStageVisualProfile.Find(StageID);
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
            // Since the 2026-11-03 centre pit (9a+): one solid floor body per
            // FloorSegments entry plus the two solid walls.
            AssertThat(solids.Count).IsEqual(FighterStageGeometry.Globe.FloorSegments.Length + 2);
            AssertThat(solids.Count).IsEqual(4);
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

            // Typed since the Package 8 B7 migration: the old ParallaxBackground /
            // ParallaxLayer bindings carried [Obsolete] in Godot 4.7 and had to be
            // reached untyped to keep the build warning-free; Parallax2D does not.
            var parallax = presentation.GetNodeOrNull<Node2D>("Parallax");
            AssertObject(parallax).IsNotNull();

            var scrollFactors = new List<float>();
            foreach (Node child in parallax.GetChildren()) {
                if (child is Parallax2D layer) scrollFactors.Add(layer.ScrollScale.X);
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
            (int)MatchMode.Stock,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardCadenceFrames: 1800,
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

    // === 2026-11-03 gameplay review (9a+/9a++): the Globe becomes an Open stage ===

    /// <summary>
    /// A 3.0-wide centred pit: two floor segments [-9, -1.5] and [1.5, 9], so the
    /// floor supports every x outside the gap and nothing inside it.
    /// </summary>
    [TestCase]
    public void GlobeIsAnOpenStageWithTheReferenceCentredPit() {
        FighterStageGeometry globe = FighterStageGeometry.Globe;
        AssertThat(globe.IsOpenStage).IsTrue();
        AssertThat(globe.FloorSegments.Length).IsEqual(2);
        AssertThat(globe.FloorSegments[0].EdgeX(0).RawValue).IsEqual(globe.LeftWall.RawValue);
        AssertThat(globe.FloorSegments[0].EdgeX(1).RawValue).IsEqual(FP64.FromDouble(Profile.WorldX(Profile.FloorSpans[0].Right)).RawValue);
        AssertThat(globe.FloorSegments[1].EdgeX(0).RawValue).IsEqual(FP64.FromDouble(Profile.WorldX(Profile.FloorSpans[1].Left)).RawValue);
        AssertThat(globe.FloorSegments[1].EdgeX(1).RawValue).IsEqual(globe.RightWall.RawValue);
        AssertThat((globe.FloorSegments[1].EdgeX(0) - globe.FloorSegments[0].EdgeX(1)).RawValue)
            .IsEqual((FP64.FromDouble(Profile.WorldX(Profile.FloorSpans[1].Left)) - FP64.FromDouble(Profile.WorldX(Profile.FloorSpans[0].Right))).RawValue);

        foreach (double x in new[] { -7.6, -4.0, -1.9, 1.9, 4.0, 7.6 }) {
            AssertThat(globe.HasFloorSupport(FP64.FromDouble(x)))
                .OverrideFailureMessage($"no floor under x = {x}").IsTrue();
        }
        foreach (double x in new[] { -1.8, -0.5, 0.0, 0.5, 1.8 }) {
            AssertThat(globe.HasFloorSupport(FP64.FromDouble(x)))
                .OverrideFailureMessage($"floor under x = {x}, inside the pit").IsFalse();
        }
    }

    /// <summary>
    /// Both spawns, both gallery platforms and the respawn platform stand over
    /// floor: a spawn over the pit would cost a stock before the countdown ended.
    /// The respawn platform moves to (-4, 3) because stage centre is a hole.
    /// </summary>
    [TestCase]
    public void GlobeSpawnsGalleriesAndTheRespawnPlatformStandOverFloor() {
        FighterStageGeometry globe = FighterStageGeometry.Globe;
        FP64 spawn = FP64.FromInt(globe.SpawnDistance);
        AssertThat(globe.HasFloorSupport(spawn)).IsTrue();
        AssertThat(globe.HasFloorSupport(FP64.Zero - spawn)).IsTrue();

        // The two galleries (the +-5 platforms) sit wholly over floor.
        for (int index = 0; index < 2; index++) {
            FighterStagePlatform gallery = globe.Platforms[index];
            AssertThat(globe.HasFloorSupport(gallery.EdgeX(0))).IsTrue();
            AssertThat(globe.HasFloorSupport(gallery.EdgeX(1))).IsTrue();
        }

        AssertThat(globe.RespawnPlatformPosition.x.RawValue).IsEqual(FP64.FromInt(-4).RawValue);
        AssertThat(globe.RespawnPlatformPosition.y.RawValue).IsEqual(FP64.FromInt(3).RawValue);
        AssertThat(globe.HasFloorSupport(globe.RespawnPlatformPosition.x)).IsTrue();
    }

    /// <summary>
    /// The two pit-facing segment ends are the only floor ledges, encoded after
    /// the three platforms ((3 + segment) * 2 + side); the segment ends against
    /// the walls are not ledges.
    /// </summary>
    [TestCase]
    public void GlobePitFacingFloorEndsAreTheOnlyTwoLedges() {
        FighterStageGeometry globe = FighterStageGeometry.Globe;
        int leftPitEdge = (globe.Platforms.Length + 0) * 2 + 1;
        int rightPitEdge = (globe.Platforms.Length + 1) * 2 + 0;
        AssertThat(leftPitEdge).IsEqual(7);
        AssertThat(rightPitEdge).IsEqual(8);

        FP64 justUnderTheFloor = FP64.FromDouble(-0.1);
        AssertThat(globe.TryFindLedge(
            new FPVector2(FP64.FromDouble(Profile.WorldX(Profile.FloorSpans[0].Right)), justUnderTheFloor), out int anchor)).IsTrue();
        AssertThat(anchor).IsEqual(leftPitEdge);
        AssertThat(globe.TryFindLedge(
            new FPVector2(FP64.FromDouble(Profile.WorldX(Profile.FloorSpans[1].Left)), justUnderTheFloor), out anchor)).IsTrue();
        AssertThat(anchor).IsEqual(rightPitEdge);

        AssertThat(globe.TryGetHangPosition(leftPitEdge, out FPVector2 hang)).IsTrue();
        AssertThat(hang.x.RawValue)
            .IsEqual((FP64.FromDouble(Profile.WorldX(Profile.FloorSpans[0].Right)) + FighterLedgeRules.HangOutwardOffset).RawValue);
        AssertThat(globe.TryGetHangPosition(rightPitEdge, out FPVector2 mirrored)).IsTrue();
        AssertThat(mirrored.x.RawValue)
            .IsEqual((FP64.FromDouble(Profile.WorldX(Profile.FloorSpans[1].Left)) - FighterLedgeRules.HangOutwardOffset).RawValue);

        // The wall-side segment ends and the middle of the pit are not ledges.
        AssertThat(globe.TryGetHangPosition((globe.Platforms.Length + 0) * 2 + 0, out FPVector2 _)).IsFalse();
        AssertThat(globe.TryGetHangPosition((globe.Platforms.Length + 1) * 2 + 1, out FPVector2 _)).IsFalse();
        AssertThat(globe.TryFindLedge(new FPVector2(FP64.Zero, justUnderTheFloor), out int _)).IsFalse();

        // The nearest floor edge walking toward the pit from either spawn.
        AssertThat(globe.TryGetNearestFloorEdge(FP64.FromInt(-4), 1, out FP64 edge)).IsTrue();
        AssertThat(edge.RawValue).IsEqual(FP64.FromDouble(Profile.WorldX(Profile.FloorSpans[0].Right)).RawValue);
        AssertThat(globe.TryGetNearestFloorEdge(FP64.FromInt(4), -1, out edge)).IsTrue();
        AssertThat(edge.RawValue).IsEqual(FP64.FromDouble(Profile.WorldX(Profile.FloorSpans[1].Left)).RawValue);
    }

    /// <summary>
    /// The scene authors one solid body per floor segment (the shared validator
    /// matches them by centre and half-width), split either side of the pit.
    /// </summary>
    [TestCase]
    public void GlobeSceneAuthorsOneFloorBodyPerSegmentOnTheFloorPlane() {
        Node2D root = Instantiate();
        try {
            var left = root.GetNodeOrNull<StaticBody2D>("Geometry/GroundLeft");
            var right = root.GetNodeOrNull<StaticBody2D>("Geometry/GroundRight");
            AssertObject(left).IsNotNull();
            AssertObject(right).IsNotNull();
            AssertObject(root.GetNodeOrNull<Node>("Geometry/Ground")).IsNull();
            foreach (StaticBody2D body in new[] { left, right }) {
                AssertThat(body.CollisionLayer).IsEqual(64u);
                AssertThat(body.CollisionMask).IsEqual(0u);
                var shape = body.GetNode<CollisionShape2D>("CollisionShape2D");
                var rect = (RectangleShape2D)shape.Shape;
                AssertThat(Mathf.Abs(rect.Size.X - FighterStageConformance.UnitsToPixels(FighterStageGeometry.Globe.FloorSegments[0].HalfWidth) * 2)).IsLess(0.001f);
                AssertThat(body.Position.Y + shape.Position.Y - rect.Size.Y / 2f).IsEqual(700f);
            }
            AssertThat(Mathf.Abs(left.Position.X - FighterStageConformance.ToPixels(FighterStageGeometry.Globe.FloorSegments[0].CenterX, FP64.Zero).X)).IsLess(0.001f);
            AssertThat(Mathf.Abs(right.Position.X - FighterStageConformance.ToPixels(FighterStageGeometry.Globe.FloorSegments[1].CenterX, FP64.Zero).X)).IsLess(0.001f);
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The pit has a readable depth cue exactly over the authored gap, opening
    /// downward from the floor plane, and both true ledges carry an edge cue.
    /// </summary>
    [TestCase]
    public void GlobePaintedPitSpansTheAuthoredGapAndBothLedgesCarryACue() {
        Node2D root = Instantiate();
        try {
            var pit = root.GetNodeOrNull<ColorRect>("Presentation/CentrePit");
            AssertObject(pit).IsNotNull();
            FighterStageGeometry globe = FighterStageGeometry.Globe;
            float gapLeft = FighterStageConformance.ToPixels(globe.FloorSegments[0].EdgeX(1), FP64.Zero).X;
            float gapRight = FighterStageConformance.ToPixels(globe.FloorSegments[1].EdgeX(0), FP64.Zero).X;
            AssertThat(Mathf.Abs(pit.OffsetLeft - gapLeft) <= FighterStageConformance.EpsilonPixels)
                .OverrideFailureMessage($"the painted pit starts at {pit.OffsetLeft} px, the gap at {gapLeft} px")
                .IsTrue();
            AssertThat(Mathf.Abs(pit.OffsetRight - gapRight) <= FighterStageConformance.EpsilonPixels)
                .OverrideFailureMessage($"the painted pit ends at {pit.OffsetRight} px, the gap at {gapRight} px")
                .IsTrue();
            AssertThat(pit.OffsetTop >= FighterStageConformance.ToPixels(FP64.Zero, FP64.Zero).Y).IsTrue();
            AssertObject(root.GetNodeOrNull<ColorRect>("Geometry/GroundLeft/LedgeCue")).IsNotNull();
            AssertObject(root.GetNodeOrNull<ColorRect>("Geometry/GroundRight/LedgeCue")).IsNotNull();
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The canopy platform and its orb now hang over the hole: no floor under
    /// them, the platform carries the orb, and the heckler anchor at x = 0 is kept
    /// (conformance compares hazard anchors on X only; the crowd has no damaging
    /// region, so nothing lingers over the pit).
    /// </summary>
    [TestCase]
    public void GlobeCanopyPlatformAndOrbHangOverThePitAndTheCentreHecklerAnchorIsKept() {
        FighterStageGeometry globe = FighterStageGeometry.Globe;
        FighterStagePlatform canopy = globe.Platforms[2];
        AssertThat(canopy.CenterX.RawValue).IsEqual(FP64.FromDouble((Profile.WorldX(Profile.PlatformSpans[2].Left) + Profile.WorldX(Profile.PlatformSpans[2].Right)) / 2).RawValue);
        AssertThat(globe.HasFloorSupport(canopy.CenterX)).IsFalse();

        bool orbOnCanopy = false;
        foreach (FPVector2 orb in globe.OrbAnchors) {
            if (orb.x.RawValue != canopy.CenterX.RawValue) continue;
            orbOnCanopy = orb.y.RawValue == (canopy.SurfaceY + FP64.FromDouble(0.5)).RawValue;
        }
        AssertThat(orbOnCanopy).IsTrue();

        bool hecklerOverPit = false;
        foreach (FP64 anchorX in globe.HazardAnchorXs) {
            if (anchorX.RawValue == FP64.Zero.RawValue) hecklerOverPit = true;
        }
        AssertThat(hecklerOverPit).IsTrue();
    }

    /// <summary>
    /// Player one walks east off the left segment holding Down (the ledge catch
    /// releases at once): the fall costs exactly one stock, the respawn platform
    /// materialises at (-4, 3), and the drop lands on the left segment.
    /// </summary>
    [TestCase]
    public void WalkingIntoTheGlobePitCostsAStockAndRespawnsOverTheLeftFloor() {
        var simulation = new FighterSimulation(
            stocks: 3, seed: 61110, rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0),
            stageGeometry: FighterStageGeometry.Globe);
        bool fell = false;
        for (int step = 0; step < 600 && !fell; step++) {
            int tick = simulation.CurrentTick;
            var falling = new PlayerInputFrame {
                Tick = (uint)tick, MoveX = 127, Held = GameplayButtons.Down,
                Pressed = step == 0 ? GameplayButtons.Down : GameplayButtons.None
            };
            simulation.Advance(falling, new PlayerInputFrame { Tick = (uint)tick });
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent state)).IsTrue();
            fell = state.Stocks < 3;
        }
        AssertThat(fell).OverrideFailureMessage("player one never fell through the Globe pit").IsTrue();

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent onPlatform)).IsTrue();
        AssertThat(onPlatform.Stocks).IsEqual(2);
        AssertThat(FighterMatchFlowRules.IsOnRespawnPlatform(in onPlatform)).IsTrue();
        AssertThat(onPlatform.Position.x.RawValue).IsEqual(FP64.FromInt(-4).RawValue);
        AssertThat(onPlatform.Position.y.RawValue).IsEqual(FP64.FromInt(3).RawValue);

        bool landed = false;
        for (int offset = 0; offset < 600 && !landed; offset++) {
            int tick = simulation.CurrentTick;
            var neutral = new PlayerInputFrame { Tick = (uint)tick };
            simulation.Advance(neutral, neutral);
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent dropping)).IsTrue();
            AssertThat(dropping.Stocks).IsEqual(2);
            landed = !FighterMatchFlowRules.IsOnRespawnPlatform(in dropping) && dropping.IsGrounded != 0;
            if (landed) {
                AssertThat(FighterStageGeometry.Globe.HasFloorSupport(dropping.Position.x)).IsTrue();
                AssertThat(dropping.Position.x < FP64.Zero).IsTrue();
            }
        }
        AssertThat(landed).OverrideFailureMessage("the respawn drop never reached the floor").IsTrue();
    }

    /// <summary>
    /// A fighter that walks off the pit-facing end without Down is caught by the
    /// ledge, and a jump plus an inward hold climbs back onto the left segment.
    /// </summary>
    [TestCase]
    public void WalkingOffTheGlobeFloorEndCatchesTheLedgeAndClimbsBack() {
        FighterStageGeometry globe = FighterStageGeometry.Globe;
        int pitAnchor = (globe.Platforms.Length + 0) * 2 + 1;
        var simulation = new FighterSimulation(
            seed: 61111, rules: FighterMatchRules.Disabled, stageGeometry: globe);
        bool hanging = false;
        for (int step = 0; step < 600 && !hanging; step++) {
            int tick = simulation.CurrentTick;
            simulation.Advance(
                new PlayerInputFrame { Tick = (uint)tick, MoveX = 127 },
                new PlayerInputFrame { Tick = (uint)tick });
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            hanging = runtime.LedgeAnchor == pitAnchor;
        }
        AssertThat(hanging).OverrideFailureMessage("player one never caught the Globe pit ledge").IsTrue();

        bool backOnFloor = false;
        for (int offset = 0; offset < 180 && !backOnFloor; offset++) {
            int tick = simulation.CurrentTick;
            GameplayButtons held = offset == 0 ? GameplayButtons.Jump : GameplayButtons.None;
            simulation.Advance(
                new PlayerInputFrame { Tick = (uint)tick, MoveX = -127, Held = held, Pressed = held },
                new PlayerInputFrame { Tick = (uint)tick });
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent climbing)).IsTrue();
            backOnFloor = climbing.IsGrounded != 0
                && climbing.Position.y.RawValue == FP64.Zero.RawValue
                && globe.HasFloorSupport(climbing.Position.x);
        }
        AssertThat(backOnFloor).OverrideFailureMessage("the climb never put the fighter back on the floor").IsTrue();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent landed)).IsTrue();
        AssertThat(landed.Stocks).IsEqual(3);
    }

    /// <summary>
    /// Two simulations on the Globe's geometry stay hash-identical through a
    /// pit fall, the respawn and the drop, hazards and orbs on.
    /// </summary>
    [TestCase]
    public void TwoSimulationsStayHashIdenticalThroughAGlobePitFallAndRespawn() {
        FighterMatchRules rules = new(
            (int)MatchMode.Stock,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardCadenceFrames: 1800,
            stageHazardTypeID: HazardTypeID);
        var first = new FighterSimulation(seed: 61112, rules: rules, stageGeometry: FighterStageGeometry.Globe);
        var second = new FighterSimulation(seed: 61112, rules: rules, stageGeometry: FighterStageGeometry.Globe);
        for (int tick = 0; tick < 900; tick++) {
            // East into the pit for the first 90 frames, then stand still.
            PlayerInputFrame one = tick < 90
                ? new PlayerInputFrame { Tick = (uint)tick, MoveX = 127, Held = GameplayButtons.Down }
                : new PlayerInputFrame { Tick = (uint)tick };
            PlayerInputFrame two = new() { Tick = (uint)tick };
            AssertThat(first.Advance(one, two)).OverrideFailureMessage(
                $"globe_theatre diverged at tick {tick}.").IsEqual(second.Advance(one, two));
        }
        AssertThat(first.TryGetFighter(0, out FighterStateComponent fallen)).IsTrue();
        AssertThat(fallen.Stocks).OverrideFailureMessage("the scripted walk never lost a stock").IsEqual(2);
    }

    /// <summary>
    /// Player one reverses every 40 frames and hops occasionally, so its idle
    /// counter can never reach the 120 consecutive frames the crowd needs.
    /// </summary>
    private static PlayerInputFrame PacingInput(int tick) {
        // Away from the stage centre first: player one spawns at x = -4 and the
        // 2026-11-03 centre pit starts 2.5 units east of that, so a 40-frame run
        // east would end in it. Pacing west-then-east never reaches the pit.
        sbyte axis = tick / 40 % 2 == 0 ? (sbyte)-127 : (sbyte)127;
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
