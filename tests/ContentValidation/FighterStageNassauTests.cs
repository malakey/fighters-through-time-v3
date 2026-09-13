using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 6 Phase B — Nassau Flagship (`nassau_flagship`, hazard type 6).
///
/// <para>Pins the production-contract stage against the three things that can
/// silently drift apart: the authored scene, the code-authored fixed-point
/// geometry it is a pixel mirror of, and the placeholder content budget
/// (pools, audio, preview) the closeout wires into the catalog. The scene is
/// not routed at runtime until Phase C sets its catalog `ScenePath`, so this
/// suite loads it directly.</para>
///
/// <para>One <c>[TestSuite]</c> per file: a second suite in the same file is
/// discovered but silently not executed (plan §9, INTEGRATION-A).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageNassauTests {

    private const string ScenePath = "res://scenes/fighter/FighterStage_Nassau.tscn";
    private const string ContractPath = "res://resources/Contracts/fighter_stage_contract.tres";
    private const string PoolConfigPath =
        "res://resources/Pools/fighter_stage_configs/fighter_stage_nassau_pool_config.tres";
    private const string AudioSetPath = "res://resources/Audio/stage_nassau_audio.tres";
    private const string PreviewPath = "res://assets/placeholders/stages/nassau_preview.svg";

    private const string StageID = "nassau_flagship";
    private const string ContentID = "fighter_stage_nassau";
    private const string PoolConfigID = "fighter_stage_nassau_pools";
    private const string AudioSetID = "audio_stage_nassau";

    // === Scene ===

    [TestCase]
    public void TheSceneLoadsAndInstantiatesAsAFighterStageControllerForNassau() {
        AssertThat(ResourceLoader.Exists(ScenePath)).IsTrue();
        Node2D root = Instantiate();
        try {
            AssertThat(root is FighterStageController)
                .OverrideFailureMessage("FighterStage_Nassau.tscn must instantiate as FighterStageController.")
                .IsTrue();
            AssertString(((FighterStageController)root).StageID).IsEqual(StageID);
            AssertThat(root.IsInGroup("fighter_stage")).IsTrue();
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void TheSceneSatisfiesTheSharedFighterStageContentContract() {
        Node2D root = Instantiate();
        try {
            var contract = AuthoredResources.Load<ContentSceneContract>(ContractPath);
            AssertObject(contract).IsNotNull();

            IReadOnlyList<string> issues = ContentSceneContractValidator.Validate(root, contract);
            if (issues.Count > 0) {
                AssertThat("nassau contract: " + string.Join(" | ", issues)).IsEqual("");
            }

            var marker = root.GetNodeOrNull<ContentTemplateMarker>("ContentContract");
            AssertObject(marker).IsNotNull();
            AssertString(marker.ContentID).IsEqual(ContentID);
            AssertObject(marker.Contract).IsNotNull();
            AssertString(marker.Contract.ContractID).IsEqual(contract.ContractID);
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The one check nothing in the engine performs: every marker and collider is
    /// the pixel image of <see cref="FighterStageGeometry.Nassau"/> through
    /// <c>pixel = (950 + x·62.5, 700 − y·62.5)</c>. A drifted platform still plays,
    /// it just lies to the player about where the yard is.
    /// </summary>
    [TestCase]
    public void TheSceneMirrorsItsAuthoredFixedPointGeometry() {
        Node2D root = Instantiate();
        try {
            List<string> issues = FighterStageConformance.Validate(root, FighterStageGeometry.Nassau);
            if (issues.Count > 0) {
                AssertThat("nassau conformance: " + string.Join(" | ", issues)).IsEqual("");
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The simulation is authoritative, so every collider is presentation: solid
    /// bodies on Environment (64), the two yards one-way on OneWayPlatform (128),
    /// and no collision mask anywhere.
    /// </summary>
    [TestCase]
    public void EveryColliderIsPresentationOnlyWithTheAuthoredLayers() {
        Node2D root = Instantiate();
        try {
            foreach (string path in new[] { "Geometry/Ground", "Geometry/WallLeft", "Geometry/WallRight" }) {
                var body = root.GetNodeOrNull<StaticBody2D>(path);
                AssertObject(body).IsNotNull();
                AssertThat(body.CollisionLayer).IsEqual(64u);
                AssertThat(body.CollisionMask).IsEqual(0u);
            }
            foreach (string path in new[] { "Geometry/YardLeft", "Geometry/YardRight" }) {
                var body = root.GetNodeOrNull<StaticBody2D>(path);
                AssertObject(body).IsNotNull();
                AssertThat(body.CollisionLayer).IsEqual(128u);
                AssertThat(body.CollisionMask).IsEqual(0u);
                var shape = body.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
                AssertObject(shape).IsNotNull();
                AssertThat(shape.OneWayCollision).IsTrue();
            }
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void TheCameraIsAFighterCameraWithTheReferenceCanvasLimitsAndZoomBand() {
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

    /// <summary>
    /// Nassau is the burning deck of a pirate flagship: a layered backdrop, and
    /// visible targeting-grid dressing on each of the three mortar anchors so the
    /// hazard's identity reads before the first shell lands.
    /// </summary>
    [TestCase]
    public void ThePresentationLayersAParallaxBackdropAndDressesEveryMortarAnchor() {
        Node2D root = Instantiate();
        try {
            var presentation = root.GetNodeOrNull<Node2D>("Presentation");
            AssertObject(presentation).IsNotNull();
            AssertObject(presentation.GetNodeOrNull<ColorRect>("BackdropTint")).IsNotNull();

            // Typed against the successor class since the Package 8 B7 migration.
            // The old ParallaxBackground/ParallaxLayer bindings are [Obsolete] in
            // Godot 4.7, which is why this used to be checked structurally.
            var parallax = presentation.GetNodeOrNull<Node2D>("Parallax");
            AssertObject(parallax).IsNotNull();

            int layers = 0;
            var scrollFactors = new List<Vector2>();
            foreach (Node child in parallax.GetChildren()) {
                if (child is not Parallax2D layer) continue;
                layers++;
                scrollFactors.Add(layer.ScrollScale);
            }
            AssertThat(layers >= 2)
                .OverrideFailureMessage($"Nassau needs at least two parallax layers, found {layers}.")
                .IsTrue();
            // Layers that scroll together are one layer wearing two hats.
            for (int outer = 0; outer < scrollFactors.Count; outer++) {
                for (int inner = outer + 1; inner < scrollFactors.Count; inner++) {
                    AssertThat(scrollFactors[outer].IsEqualApprox(scrollFactors[inner]))
                        .OverrideFailureMessage("Two parallax layers share one motion scale.")
                        .IsFalse();
                }
            }

            // Package 11 A9: the listing stern ends over open water. The painted gap
            // must open downward exactly where the authored deck stops, and the
            // stage's one true ledge needs a readable edge cue.
            AssertThat(FighterStageGeometry.Nassau.IsOpenStage).IsTrue();
            var sternGap = presentation.GetNodeOrNull<ColorRect>("SternGap");
            AssertObject(sternGap).IsNotNull();
            float deckEnd = FighterStageConformance.ToPixels(
                FighterStageGeometry.Nassau.FloorSegments[0].EdgeX(1), FP64.Zero).X;
            AssertThat(Mathf.Abs(sternGap.OffsetLeft - deckEnd) <= FighterStageConformance.EpsilonPixels)
                .OverrideFailureMessage(
                    $"the painted stern gap starts at {sternGap.OffsetLeft} px but the deck ends at {deckEnd} px")
                .IsTrue();
            AssertThat(sternGap.OffsetTop
                >= FighterStageConformance.ToPixels(FP64.Zero, FP64.Zero).Y).IsTrue();
            AssertObject(root.GetNodeOrNull<ColorRect>("Geometry/Ground/LedgeCue")).IsNotNull();

            // One dressed grid per authored hazard anchor, on the anchor's x.
            var targets = presentation.GetNodeOrNull<Node2D>("MortarTargets");
            AssertObject(targets).IsNotNull();
            var dressedX = new List<float>();
            foreach (Node child in targets.GetChildren()) {
                if (child is Node2D grid) dressedX.Add(grid.Position.X);
            }
            AssertThat(dressedX.Count).IsEqual(FighterStageGeometry.Nassau.HazardAnchorXs.Length);
            foreach (Godot.Vector2 expected in AnchorPixels()) {
                bool matched = false;
                foreach (float actual in dressedX) {
                    if (Mathf.Abs(actual - expected.X) <= FighterStageConformance.EpsilonPixels) {
                        matched = true;
                        break;
                    }
                }
                AssertThat(matched)
                    .OverrideFailureMessage($"No mortar target dressing at x {expected.X}.")
                    .IsTrue();
            }
        } finally {
            root.Free();
        }
    }

    // === Placeholder content budget ===

    [TestCase]
    public void TheAuthoredPoolConfigIsValidAndMirrorsTheTestArenaFighterBudget() {
        var config = AuthoredResources.Load<ScenePoolConfig>(PoolConfigPath);
        AssertObject(config).IsNotNull();
        AssertString(config.ConfigID).IsEqual(PoolConfigID);

        IReadOnlyList<string> errors = config.ValidateBudget();
        if (errors.Count > 0) AssertThat(string.Join(" | ", errors)).IsEqual("");

        var reference = AuthoredResources.Load<ScenePoolConfig>("res://resources/Pools/test_arena_pool_config.tres");
        AssertObject(reference).IsNotNull();
        AssertThat(config.MaxWarmUpInstances).IsEqual(reference.MaxWarmUpInstances);
        AssertThat(config.GetWarmUpInstanceCount()).IsEqual(reference.GetWarmUpInstanceCount());
        AssertThat(config.GetMaxCapacityCount()).IsEqual(reference.GetMaxCapacityCount());

        var expected = new HashSet<string> {
            "fighter_projectile", "fighter_vfx", "fighter_environment_vfx",
            "chronal_orb", "damage_numbers", "fighter_construct"
        };
        var actual = new HashSet<string>();
        foreach (PoolDefinition definition in config.PoolDefinitions) actual.Add(definition.PoolID);
        AssertThat(actual.SetEquals(expected))
            .OverrideFailureMessage("Nassau must warm the same Fighter pool set as the Test Arena.")
            .IsTrue();
    }

    [TestCase]
    public void TheAuthoredAudioSetMeetsTheSharedStageStemContract() {
        var set = AuthoredResources.Load<StageAudioSet>(AudioSetPath);
        AssertObject(set).IsNotNull();

        List<string> issues = StageAudioSetTests.Validate(set, AudioSetID, StageID);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

        AssertThat(set.StemsAreSynchronized).IsTrue();
    }

    [TestCase]
    public void ThePlaceholderPreviewImageExistsForTheCloseoutToWireIntoTheCatalog() {
        AssertThat(ResourceLoader.Exists(PreviewPath))
            .OverrideFailureMessage($"Missing stage preview at {PreviewPath}.")
            .IsTrue();
        var preview = ResourceLoader.Load<Texture2D>(PreviewPath);
        AssertObject(preview).IsNotNull();
        AssertThat(preview.GetWidth() > 0 && preview.GetHeight() > 0).IsTrue();
    }

    // === Determinism ===

    /// <summary>
    /// Two simulations of Nassau's geometry with items and hazards on High must
    /// agree hash-for-hash across a window long enough to contain a whole mortar
    /// cycle: the first hazard spawns on the 1800-frame High boundary and then runs
    /// 90 warning → 30 explosion → 60 recovery. The run is asserted to have crossed
    /// every phase and the despawn, so a hazard that never fired cannot make this
    /// pass vacuously.
    ///
    /// <para>The stock count is raised well above the production 3 on purpose:
    /// two fighters trading basics for the thirty seconds it takes to reach the
    /// hazard boundary will burn through three stocks and end the match, and
    /// <c>FighterHazardSystem</c> stops spawning the moment the match is no longer
    /// in progress. Dropping the attacks instead would have cost the run its combat
    /// hash coverage.</para>
    /// </summary>
    [TestCase]
    public void NassauRunsIdenticallyAcrossTwoSimulationsThroughAFullMortarCycle() {
        FighterMatchRules rules = new(
            (int)MatchMode.Hybrid,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardFrequency: (int)HazardTriggerFrequency.High,
            stageHazardTypeID: FighterHazardTypeID.NassauMortar);

        var first = new FighterSimulation(
            stocks: 12, seed: 6106, rules: rules, stageGeometry: FighterStageGeometry.Nassau);
        var second = new FighterSimulation(
            stocks: 12, seed: 6106, rules: rules, stageGeometry: FighterStageGeometry.Nassau);

        bool sawWarning = false;
        bool sawExplosion = false;
        bool sawRecovery = false;
        bool sawDespawn = false;
        bool sawHazard = false;

        // 1800 (spawn) + 90 + 30 + 60 (full cycle) + slack for the despawn frame.
        for (int tick = 0; tick < 2050; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            AssertThat(first.Advance(p1, p2) == second.Advance(p1, p2))
                .OverrideFailureMessage($"nassau_flagship diverged at tick {tick}.")
                .IsTrue();

            if (first.TryGetFirstHazard(out FighterHazardComponent hazard)) {
                sawHazard = true;
                // Anchors are authored; a hazard off-anchor is a geometry bug.
                AssertThat(OnAuthoredAnchor(hazard))
                    .OverrideFailureMessage($"hazard at x {hazard.Position.x} is not on an authored anchor.")
                    .IsTrue();
                AssertThat(hazard.HazardTypeID).IsEqual(FighterHazardTypeID.NassauMortar);
                AssertThat(hazard.WarningFrames).IsEqual(90);
                AssertThat(hazard.ActiveFrames).IsEqual(30);
                AssertThat(hazard.CooldownFrames).IsEqual(60);
                AssertThat(hazard.Damage).IsEqual(10);
                // Phase encoding: 0 warning (target grid), 1 explosion, 2 recovery.
                if (hazard.Phase == 0) sawWarning = true;
                else if (hazard.Phase == 1) sawExplosion = true;
                else if (hazard.Phase == 2) sawRecovery = true;
            } else if (sawHazard) {
                sawDespawn = true;
            }
        }

        // Diagnosed first: a match that ended early silently stops hazard spawning,
        // and every assertion below would blame the mortar for it.
        AssertThat(first.GetMatchState().MatchState)
            .OverrideFailureMessage(
                "The match ended before the hazard window closed; raise the stock count.")
            .IsEqual(FighterMatchStates.InProgress);

        AssertThat(sawWarning)
            .OverrideFailureMessage("The mortar never telegraphed inside the run window.")
            .IsTrue();
        AssertThat(sawExplosion)
            .OverrideFailureMessage("The mortar never reached its explosion window.")
            .IsTrue();
        AssertThat(sawRecovery)
            .OverrideFailureMessage("The mortar never entered its recovery phase.")
            .IsTrue();
        AssertThat(sawDespawn)
            .OverrideFailureMessage("The mortar never completed its recovery and despawned.")
            .IsTrue();
    }

    // === Helpers ===

    /// <summary>
    /// Scenes are streamed content and must not go through
    /// <c>AuthoredResources</c>. The instantiated root is never added to the tree,
    /// so <c>FighterStageController._Ready</c> does not spawn fighters.
    /// </summary>
    private static Node2D Instantiate() {
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        var root = packed.Instantiate<Node2D>();
        AssertObject(root).IsNotNull();
        return root;
    }

    private static List<Vector2> AnchorPixels() {
        var points = new List<Vector2>();
        foreach (var anchorX in FighterStageGeometry.Nassau.HazardAnchorXs) {
            points.Add(FighterStageConformance.ToPixels(anchorX, xpTURN.Klotho.Deterministic.Math.FP64.Zero));
        }
        return points;
    }

    private static bool OnAuthoredAnchor(FighterHazardComponent hazard) {
        foreach (var anchorX in FighterStageGeometry.Nassau.HazardAnchorXs) {
            if (hazard.Position.x.RawValue == anchorX.RawValue) return true;
        }
        return false;
    }

    private static PlayerInputFrame InputFor(int playerID, int tick) {
        sbyte axis = (sbyte)(((tick + playerID * 13) % 7 - 3) * 40);
        GameplayButtons buttons = GameplayButtons.None;
        if (tick % 43 == 0) buttons |= GameplayButtons.Jump;
        if (tick % 29 == 0) buttons |= GameplayButtons.BasicAttack;
        if (tick % 89 == 0) buttons |= GameplayButtons.Block;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = axis,
            Held = buttons,
            Pressed = buttons
        };
    }
}
