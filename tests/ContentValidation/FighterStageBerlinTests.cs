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
/// balconies at −6.5 and (since the 2026-11-03 gameplay review, which opened the
/// stage with a one-sided gap [7, 10]) +5.5, both at 3.6, walls at ±10, and
/// three searchlight anchors at {−4, 0, 4}. Nothing in the engine enforces that mirror — the simulation is
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
            hazardCadenceFrames: 1800,
            stageHazardTypeID: BerlinHazardTypeID);

        // The jittering movement input drifts fighters east into the 2026-11-03 gap,
        // so the stock count is raised past what 2500 frames can burn (Paris's
        // precedent); the match-state assertion below still fails loudly if a run
        // ever ends the match before the searchlight's whole cycle.
        var first = new FighterSimulation(
            stocks: 25, seed: 8611, rules: rules, stageGeometry: FighterStageGeometry.Berlin);
        var second = new FighterSimulation(
            stocks: 25, seed: 8611, rules: rules, stageGeometry: FighterStageGeometry.Berlin);

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

    // === 2026-11-03 gameplay review (9a+/9a++): Hakata Seawall becomes an Open stage ===

    /// <summary>
    /// A one-sided 3.0-wide gap [7, 10] against the east wall, like Nassau's
    /// stern: a single floor segment [-10, 7], and the west half stays the
    /// unbroken baseline.
    /// </summary>
    [TestCase]
    public void HakataIsAnOpenStageWithAOneSidedThreeUnitGapAgainstTheEastWall() {
        FighterStageGeometry berlin = FighterStageGeometry.Berlin;
        AssertThat(berlin.IsOpenStage).IsTrue();
        AssertThat(berlin.FloorSegments.Length).IsEqual(1);
        AssertThat(berlin.FloorSegments[0].EdgeX(0).RawValue).IsEqual(berlin.LeftWall.RawValue);
        AssertThat(berlin.FloorSegments[0].EdgeX(1).RawValue).IsEqual(FP64.FromInt(7).RawValue);
        AssertThat((berlin.RightWall - berlin.FloorSegments[0].EdgeX(1)).RawValue)
            .IsEqual(FP64.FromInt(3).RawValue);

        foreach (double x in new[] { -10.0, -5.0, 0.0, 5.0, 6.9 }) {
            AssertThat(berlin.HasFloorSupport(FP64.FromDouble(x)))
                .OverrideFailureMessage($"no floor under x = {x}").IsTrue();
        }
        foreach (double x in new[] { 7.1, 8.0, 9.0, 9.9 }) {
            AssertThat(berlin.HasFloorSupport(FP64.FromDouble(x)))
                .OverrideFailureMessage($"floor under x = {x}, inside the gap").IsFalse();
        }
    }

    /// <summary>
    /// The east balcony and its orb moved 6.5 -> 5.5 so both stay over floor (the
    /// balcony spans 4.2..6.8, short of the gap at 7); the west balcony and the
    /// centre orb are untouched, and every orb sits half a unit over its surface.
    /// </summary>
    [TestCase]
    public void TheEastBalconyAndItsOrbMovedOverFloorAndTheWestSideIsUntouched() {
        FighterStageGeometry berlin = FighterStageGeometry.Berlin;
        AssertThat(berlin.Platforms.Length).IsEqual(2);
        AssertThat(berlin.Platforms[0].CenterX.RawValue).IsEqual(FP64.FromDouble(-6.5).RawValue);
        FighterStagePlatform east = berlin.Platforms[1];
        AssertThat(east.CenterX.RawValue).IsEqual(FP64.FromDouble(5.5).RawValue);
        AssertThat(east.SurfaceY.RawValue).IsEqual(FP64.FromDouble(3.6).RawValue);
        AssertThat(berlin.HasFloorSupport(east.EdgeX(0))).IsTrue();
        AssertThat(berlin.HasFloorSupport(east.EdgeX(1))).IsTrue();
        AssertThat(east.EdgeX(1) < FP64.FromInt(7)).IsTrue();

        AssertThat(berlin.OrbAnchors.Length).IsEqual(3);
        AssertThat(berlin.OrbAnchors[0].x.RawValue).IsEqual(FP64.FromDouble(-6.5).RawValue);
        AssertThat(berlin.OrbAnchors[1].x.RawValue).IsEqual(FP64.FromDouble(5.5).RawValue);
        AssertThat(berlin.OrbAnchors[1].y.RawValue).IsEqual(FP64.FromDouble(4.1).RawValue);
        AssertThat(berlin.OrbAnchors[2].x.RawValue).IsEqual(FP64.Zero.RawValue);
        AssertThat(berlin.OrbAnchors[2].y.RawValue).IsEqual(FP64.FromDouble(0.5).RawValue);
        AssertThat(berlin.HasFloorSupport(berlin.OrbAnchors[2].x)).IsTrue();
    }

    /// <summary>
    /// Both spawns, the three searchlight anchors and the respawn platform (the
    /// default stage-centre anchor, since the centre is solid floor) stand over
    /// floor, so nothing the gap changes can drop a fighter before the match.
    /// </summary>
    [TestCase]
    public void SpawnsSearchlightAnchorsAndTheRespawnPlatformStandOverFloor() {
        FighterStageGeometry berlin = FighterStageGeometry.Berlin;
        FP64 spawn = FP64.FromInt(berlin.SpawnDistance);
        AssertThat(berlin.HasFloorSupport(spawn)).IsTrue();
        AssertThat(berlin.HasFloorSupport(FP64.Zero - spawn)).IsTrue();
        foreach (FP64 anchorX in berlin.HazardAnchorXs) AssertThat(berlin.HasFloorSupport(anchorX)).IsTrue();
        AssertThat(berlin.RespawnPlatformPosition.x.RawValue)
            .IsEqual(FighterMatchFlowRules.RespawnPlatformPosition.x.RawValue);
        AssertThat(berlin.RespawnPlatformPosition.y.RawValue)
            .IsEqual(FighterMatchFlowRules.RespawnPlatformPosition.y.RawValue);
        AssertThat(berlin.HasFloorSupport(berlin.RespawnPlatformPosition.x)).IsTrue();
    }

    /// <summary>
    /// The east floor end is the one true ledge, encoded after the two
    /// platforms ((2 + 0) * 2 + 1 = 5); the segment end against the west wall is
    /// not a ledge.
    /// </summary>
    [TestCase]
    public void TheEastFloorEndIsTheOnlyLedge() {
        FighterStageGeometry berlin = FighterStageGeometry.Berlin;
        int gapEdge = (berlin.Platforms.Length + 0) * 2 + 1;
        AssertThat(gapEdge).IsEqual(5);

        FP64 justUnderTheFloor = FP64.FromDouble(-0.1);
        AssertThat(berlin.TryFindLedge(
            new FPVector2(FP64.FromInt(7), justUnderTheFloor), out int anchor)).IsTrue();
        AssertThat(anchor).IsEqual(gapEdge);

        AssertThat(berlin.TryGetHangPosition(gapEdge, out FPVector2 hang)).IsTrue();
        // The hang sits a quarter unit out over the gap.
        AssertThat(hang.x.RawValue)
            .IsEqual((FP64.FromInt(7) + FighterLedgeRules.HangOutwardOffset).RawValue);
        AssertThat(hang.y.RawValue).IsEqual((FP64.Zero - FighterLedgeRules.HangDepth).RawValue);

        AssertThat(berlin.TryGetHangPosition((berlin.Platforms.Length + 0) * 2 + 0, out FPVector2 _)).IsFalse();
        AssertThat(berlin.TryFindLedge(new FPVector2(berlin.LeftWall, justUnderTheFloor), out int _)).IsFalse();
        AssertThat(berlin.TryGetNearestFloorEdge(FP64.FromInt(4), 1, out FP64 edge)).IsTrue();
        AssertThat(edge.RawValue).IsEqual(FP64.FromInt(7).RawValue);
        AssertThat(berlin.TryGetNearestFloorEdge(FP64.FromInt(-4), -1, out edge)).IsFalse();
    }

    /// <summary>
    /// One solid floor body, shortened to the authored segment [-10, 7]: centre
    /// 856.25 px, 1062.5 px wide, on the floor plane, ending at 1387.5 px.
    /// </summary>
    [TestCase]
    public void TheSceneAuthorsOneFloorBodyThatEndsAtTheGap() {
        Node2D root = Instantiate();
        try {
            var ground = root.GetNodeOrNull<StaticBody2D>("Geometry/Ground");
            AssertObject(ground).IsNotNull();
            AssertThat(ground.CollisionLayer).IsEqual(64u);
            AssertThat(ground.CollisionMask).IsEqual(0u);
            var shape = ground.GetNode<CollisionShape2D>("CollisionShape2D");
            var rect = (RectangleShape2D)shape.Shape;
            AssertThat(rect.Size.X).IsEqual(1062.5f);
            AssertThat(ground.Position.X).IsEqual(856.25f);
            AssertThat(ground.Position.X + rect.Size.X / 2f).IsEqual(1387.5f);
            AssertThat(ground.Position.Y + shape.Position.Y - rect.Size.Y / 2f).IsEqual(700f);

            // The east guard tower and its orb marker moved with the geometry.
            AssertThat(root.GetNode<StaticBody2D>("Geometry/GuardTowerEast").Position.X).IsEqual(1293.75f);
            AssertThat(root.GetNode<Marker2D>("OrbSpawnPoints/EastBalcony").Position.X).IsEqual(1293.75f);
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The gap has a readable depth cue exactly over the authored span, opening
    /// downward from the floor plane, and the one true ledge carries an edge cue.
    /// </summary>
    [TestCase]
    public void ThePaintedGapSpansTheAuthoredGapAndTheLedgeCarriesACue() {
        Node2D root = Instantiate();
        try {
            var gap = root.GetNodeOrNull<ColorRect>("Presentation/SeawallGap");
            AssertObject(gap).IsNotNull();
            FighterStageGeometry berlin = FighterStageGeometry.Berlin;
            float gapLeft = FighterStageConformance.ToPixels(berlin.FloorSegments[0].EdgeX(1), FP64.Zero).X;
            float gapRight = FighterStageConformance.ToPixels(berlin.RightWall, FP64.Zero).X;
            AssertThat(Mathf.Abs(gap.OffsetLeft - gapLeft) <= FighterStageConformance.EpsilonPixels)
                .OverrideFailureMessage($"the painted gap starts at {gap.OffsetLeft} px, the gap at {gapLeft} px")
                .IsTrue();
            AssertThat(Mathf.Abs(gap.OffsetRight - gapRight) <= FighterStageConformance.EpsilonPixels)
                .OverrideFailureMessage($"the painted gap ends at {gap.OffsetRight} px, the wall at {gapRight} px")
                .IsTrue();
            AssertThat(gap.OffsetTop >= FighterStageConformance.ToPixels(FP64.Zero, FP64.Zero).Y).IsTrue();
            AssertObject(root.GetNodeOrNull<ColorRect>("Geometry/Ground/LedgeCue")).IsNotNull();
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// Player two (spawning at +4) walks east off the seawall holding Down: the
    /// fall costs exactly one stock, the respawn platform materialises at the
    /// stage-centre anchor and the drop lands on the floor.
    /// </summary>
    [TestCase]
    public void WalkingEastOffTheSeawallCostsAStockAndRespawnsAtStageCentre() {
        var simulation = new FighterSimulation(
            stocks: 3, seed: 86112, rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0),
            stageGeometry: FighterStageGeometry.Berlin);
        bool fell = false;
        for (int step = 0; step < 600 && !fell; step++) {
            int tick = simulation.CurrentTick;
            var falling = new PlayerInputFrame {
                Tick = (uint)tick, MoveX = 127, Held = GameplayButtons.Down,
                Pressed = step == 0 ? GameplayButtons.Down : GameplayButtons.None
            };
            simulation.Advance(new PlayerInputFrame { Tick = (uint)tick }, falling);
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent state)).IsTrue();
            fell = state.Stocks < 3;
        }
        AssertThat(fell).OverrideFailureMessage("player two never fell through the seawall gap").IsTrue();

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent onPlatform)).IsTrue();
        AssertThat(onPlatform.Stocks).IsEqual(2);
        AssertThat(FighterMatchFlowRules.IsOnRespawnPlatform(in onPlatform)).IsTrue();
        AssertThat(onPlatform.Position.x.RawValue)
            .IsEqual(FighterStageGeometry.Berlin.RespawnPlatformPosition.x.RawValue);

        bool landed = false;
        for (int offset = 0; offset < 600 && !landed; offset++) {
            int tick = simulation.CurrentTick;
            var neutral = new PlayerInputFrame { Tick = (uint)tick };
            simulation.Advance(neutral, neutral);
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent dropping)).IsTrue();
            AssertThat(dropping.Stocks).IsEqual(2);
            landed = !FighterMatchFlowRules.IsOnRespawnPlatform(in dropping) && dropping.IsGrounded != 0;
            if (landed) AssertThat(FighterStageGeometry.Berlin.HasFloorSupport(dropping.Position.x)).IsTrue();
        }
        AssertThat(landed).OverrideFailureMessage("the respawn drop never reached the floor").IsTrue();
    }

    /// <summary>
    /// Walking off the east end without Down is caught by the ledge, and a jump
    /// plus an inward hold climbs back onto the seawall.
    /// </summary>
    [TestCase]
    public void WalkingOffTheSeawallEndCatchesTheLedgeAndClimbsBack() {
        FighterStageGeometry berlin = FighterStageGeometry.Berlin;
        int gapAnchor = (berlin.Platforms.Length + 0) * 2 + 1;
        var simulation = new FighterSimulation(
            seed: 86113, rules: FighterMatchRules.Disabled, stageGeometry: berlin);
        bool hanging = false;
        for (int step = 0; step < 600 && !hanging; step++) {
            int tick = simulation.CurrentTick;
            simulation.Advance(
                new PlayerInputFrame { Tick = (uint)tick },
                new PlayerInputFrame { Tick = (uint)tick, MoveX = 127 });
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
            hanging = runtime.LedgeAnchor == gapAnchor;
        }
        AssertThat(hanging).OverrideFailureMessage("player two never caught the seawall ledge").IsTrue();

        bool backOnFloor = false;
        for (int offset = 0; offset < 180 && !backOnFloor; offset++) {
            int tick = simulation.CurrentTick;
            GameplayButtons held = offset == 0 ? GameplayButtons.Jump : GameplayButtons.None;
            simulation.Advance(
                new PlayerInputFrame { Tick = (uint)tick },
                new PlayerInputFrame { Tick = (uint)tick, MoveX = -127, Held = held, Pressed = held });
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent climbing)).IsTrue();
            backOnFloor = climbing.IsGrounded != 0
                && climbing.Position.y.RawValue == FP64.Zero.RawValue
                && berlin.HasFloorSupport(climbing.Position.x);
        }
        AssertThat(backOnFloor).OverrideFailureMessage("the climb never put the fighter back on the floor").IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent landed)).IsTrue();
        AssertThat(landed.Stocks).IsEqual(3);
    }

    /// <summary>
    /// The west half is the unbroken baseline: player one walks hard west with
    /// Down held for 400 ticks and never loses a stock.
    /// </summary>
    [TestCase]
    public void TheWestHalfStaysAnUnbrokenBaseline() {
        var simulation = new FighterSimulation(
            stocks: 3, seed: 86114, rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0),
            stageGeometry: FighterStageGeometry.Berlin);
        for (int tick = 0; tick < 400; tick++) {
            simulation.Advance(
                new PlayerInputFrame { Tick = (uint)tick, MoveX = -127, Held = GameplayButtons.Down },
                new PlayerInputFrame { Tick = (uint)tick });
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent west)).IsTrue();
        AssertThat(west.Stocks).IsEqual(3);
        AssertThat(west.IsGrounded).IsEqual(1);
        AssertThat(FighterStageGeometry.Berlin.HasFloorSupport(west.Position.x)).IsTrue();
    }

    /// <summary>
    /// Two simulations on the seawall's geometry stay hash-identical through a
    /// fall into the gap, the respawn and the drop, hazards and orbs on.
    /// </summary>
    [TestCase]
    public void TwoSimulationsStayHashIdenticalThroughAGapFallAndRespawn() {
        FighterMatchRules rules = new(
            (int)MatchMode.Stock,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardCadenceFrames: 1800,
            stageHazardTypeID: BerlinHazardTypeID);
        var first = new FighterSimulation(seed: 86115, rules: rules, stageGeometry: FighterStageGeometry.Berlin);
        var second = new FighterSimulation(seed: 86115, rules: rules, stageGeometry: FighterStageGeometry.Berlin);
        for (int tick = 0; tick < 900; tick++) {
            // Player two goes east into the gap for the first 90 frames, then stands still.
            PlayerInputFrame one = new() { Tick = (uint)tick };
            PlayerInputFrame two = tick < 90
                ? new PlayerInputFrame { Tick = (uint)tick, MoveX = 127, Held = GameplayButtons.Down }
                : new PlayerInputFrame { Tick = (uint)tick };
            AssertThat(first.Advance(one, two)).OverrideFailureMessage(
                $"berlin_wall diverged at tick {tick}.").IsEqual(second.Advance(one, two));
        }
        AssertThat(first.TryGetFighter(1, out FighterStateComponent fallen)).IsTrue();
        AssertThat(fallen.Stocks).OverrideFailureMessage("the scripted walk never lost a stock").IsEqual(2);
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
