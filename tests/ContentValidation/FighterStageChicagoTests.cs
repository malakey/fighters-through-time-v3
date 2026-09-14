using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 6 Phase B contract suite for the Chicago Exposition Fighter stage
/// (1893 World's Fair): the authored scene, its presentation dressing, its pool
/// budget, its music set, and a determinism run over its geometry.
///
/// <para>The stage is a wide flat metallic exposition floor flanked by two
/// towering conductor coils — the high side platforms at y = 3.2 — with a single
/// central hazard anchor where the Tesla induction grid (HazardTypeID 3)
/// discharges between the coils.</para>
///
/// <para>The scene is not routed by the catalog until the Package 6 closeout
/// wires <c>ScenePath</c>, so every test here loads it by path. One
/// <c>[TestSuite]</c> per file: a second suite in the same file is discovered and
/// then silently not executed (plan §9, A4).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageChicagoTests {
    private const string ScenePath = "res://scenes/fighter/FighterStage_Chicago.tscn";
    private const string PoolConfigPath =
        "res://resources/Pools/fighter_stage_configs/fighter_stage_chicago_pool_config.tres";
    private const string AudioSetPath = "res://resources/Audio/stage_chicago_audio.tres";
    private const string PreviewPath = "res://assets/placeholders/stages/chicago_preview.svg";
    private const string ContractPath = "res://resources/Contracts/fighter_stage_contract.tres";
    private const string StageID = "chicago_exposition";

    [TestCase]
    public void TheChicagoSceneLoadsAndInstantiatesWithItsCatalogIdentity() {
        Node2D root = Instantiate();
        try {
            AssertThat(root.IsInGroup("fighter_stage")).IsTrue();
            var controller = root as FighterStageController;
            AssertObject(controller).IsNotNull();
            AssertThat(controller.StageID).IsEqual(StageID);

            var marker = root.GetNodeOrNull<ContentTemplateMarker>("ContentContract");
            AssertObject(marker).IsNotNull();
            AssertThat(marker.ContentID).IsEqual("fighter_stage_chicago");

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
    public void TheChicagoSceneSatisfiesTheFighterStageContract() {
        Node2D root = Instantiate();
        try {
            var contract = FTT.Core.AuthoredResources.Load<ContentSceneContract>(ContractPath);
            AssertObject(contract).IsNotNull();
            IReadOnlyList<string> issues = ContentSceneContractValidator.Validate(root, contract);
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The simulation owns every boundary; the scene is a pixel mirror of it
    /// through <c>pixel = (950 + x·62.5, 700 − y·62.5)</c>. A drifted marker plays
    /// fine and lies to the player about where the floor is, so the shared A1
    /// validator is the only thing that catches it.
    /// </summary>
    [TestCase]
    public void TheChicagoSceneMirrorsItsAuthoredFixedPointGeometry() {
        Node2D root = Instantiate();
        try {
            List<string> issues =
                FighterStageConformance.Validate(root, FighterStageGeometry.Chicago);
            if (issues.Count > 0) {
                AssertThat("chicago conformance: " + string.Join(" | ", issues)).IsEqual("");
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// Collision authoring the conformance validator only checks for platforms:
    /// the solid bodies must be on the Environment layer with no mask at all, because
    /// the deterministic simulation — not Godot physics — resolves every collision.
    /// </summary>
    [TestCase]
    public void EverySolidBodyIsPresentationOnlyWithNoCollisionMask() {
        Node2D root = Instantiate();
        try {
            var geometry = root.GetNodeOrNull<Node2D>("Geometry");
            AssertObject(geometry).IsNotNull();
            int solid = 0;
            int oneWay = 0;
            foreach (Node child in geometry.GetChildren()) {
                var body = child as StaticBody2D;
                if (body == null) continue;
                AssertThat(body.CollisionMask == 0u).OverrideFailureMessage(
                    $"'{body.Name}' has a non-zero collision mask.").IsTrue();
                if (body.CollisionLayer == FighterStageConformance.SolidCollisionLayer) solid++;
                if (body.CollisionLayer == FighterStageConformance.OneWayPlatformLayer) oneWay++;
            }
            // Ground plus two walls, and one body per authored coil platform.
            AssertThat(solid).IsEqual(3);
            AssertThat(oneWay).IsEqual(FighterStageGeometry.Chicago.Platforms.Length);
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// Presentation contract: a backdrop tint in the catalog background colour, a
    /// parallax background with at least two layers at distinct scroll factors, and
    /// era dressing that reads as the World's Fair rather than as a grey box.
    /// </summary>
    [TestCase]
    public void TheChicagoPresentationIsDressedForTheWorldsFair() {
        Node2D root = Instantiate();
        try {
            var presentation = root.GetNodeOrNull<Node2D>("Presentation");
            AssertObject(presentation).IsNotNull();

            // Package 6 C1, still true after the Package 8 B7 Parallax2D migration:
            // the tint must be translucent. The parallax now lives in canvas layer 0
            // at a deep negative z_index, so an OPAQUE full-bleed ColorRect above it
            // hides it just as thoroughly as the old negative-CanvasLayer version did
            // — and a node-existence test still passes. Opaque screen coverage comes
            // from BackdropBase, which sits inside the parallax group beneath it.
            var tint = presentation.GetNodeOrNull<ColorRect>("BackdropTint");
            AssertObject(tint).IsNotNull();
            AssertThat(tint.Color.R).IsEqualApprox(0.04f, 0.001f);
            AssertThat(tint.Color.G).IsEqualApprox(0.09f, 0.001f);
            AssertThat(tint.Color.B).IsEqualApprox(0.13f, 0.001f);
            AssertThat(tint.Color.A < 1f).OverrideFailureMessage(
                $"BackdropTint is opaque (alpha {tint.Color.A}); it would hide the parallax.")
                .IsTrue();

            // Typed against Parallax2D since the Package 8 B7 migration; the
            // replacement class carries no [Obsolete], so the untyped GetClass()
            // workaround and its CS0618 rationale are both retired.
            var parallax = presentation.GetNodeOrNull<Node2D>("Parallax");
            AssertObject(parallax).IsNotNull();
            var scrollFactors = new List<float>();
            foreach (Node child in parallax.GetChildren()) {
                if (child is Parallax2D layer) scrollFactors.Add(layer.ScrollScale.X);
            }
            AssertThat(scrollFactors.Count >= 2).OverrideFailureMessage(
                $"expected at least two parallax layers, found {scrollFactors.Count}").IsTrue();
            // Distinct scroll factors: identical ones are one layer drawn twice.
            AssertThat(scrollFactors[0]).IsNotEqual(scrollFactors[1]);

            // The two conductor coils and the induction discharge span between them
            // are the stage's era identity and its hazard telegraph.
            AssertObject(presentation.GetNodeOrNull<Node2D>("CoilTowerLeft")).IsNotNull();
            AssertObject(presentation.GetNodeOrNull<Node2D>("CoilTowerRight")).IsNotNull();
            AssertObject(presentation.GetNodeOrNull<Node2D>("InductionSpan")).IsNotNull();
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void TheChicagoPreviewImageIsAuthored() {
        AssertThat(ResourceLoader.Exists(PreviewPath)).IsTrue();
        var preview = ResourceLoader.Load<Texture2D>(PreviewPath);
        AssertObject(preview).IsNotNull();
        AssertThat(preview.GetWidth() > 0 && preview.GetHeight() > 0).IsTrue();
    }

    [TestCase]
    public void TheChicagoPoolConfigIsValidAndBudgeted() {
        ScenePoolConfig config = FTT.Core.AuthoredResources.Load<ScenePoolConfig>(PoolConfigPath);
        AssertObject(config).IsNotNull();
        AssertThat(config.ConfigID).IsEqual("fighter_stage_chicago_pools");

        IReadOnlyList<string> errors = config.ValidateBudget();
        if (errors.Count > 0) AssertThat(string.Join("; ", errors)).IsEqual("");
        AssertThat(config.GetWarmUpInstanceCount() <= config.MaxWarmUpInstances).IsTrue();
        AssertThat(config.GetMaxCapacityCount() >= config.GetWarmUpInstanceCount()).IsTrue();

        // The Fighter pool set mirrors test_arena_pool_config.tres; a missing
        // definition means a cold first spawn mid-match.
        var poolIDs = new HashSet<string>();
        foreach (PoolDefinition definition in config.PoolDefinitions) {
            AssertThat(definition.HasValidBudget()).OverrideFailureMessage(
                $"pool '{definition.PoolID}' has an invalid budget.").IsTrue();
            poolIDs.Add(definition.PoolID);
        }
        foreach (string poolID in new[] {
            "fighter_projectile", "fighter_vfx", "fighter_environment_vfx",
            "chronal_orb", "damage_numbers", "fighter_construct"
        }) {
            AssertThat(poolIDs.Contains(poolID)).OverrideFailureMessage(
                $"chicago pool config does not warm '{poolID}'.").IsTrue();
        }
    }

    [TestCase]
    public void TheChicagoAudioSetSatisfiesTheStageStemContract() {
        var set = FTT.Core.AuthoredResources.Load<StageAudioSet>(AudioSetPath);
        AssertObject(set).IsNotNull();
        List<string> issues = StageAudioSetTests.Validate(set, "audio_stage_chicago", StageID);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// Determinism over Chicago's geometry with hazards and orbs at High, run long
    /// enough to cross a whole induction-grid cycle: the first hazard spawns on the
    /// 1,800-frame boundary, then warns 90, damages 360 and recovers 60 before it
    /// despawns at 2,310. The run asserts tick-by-tick hash equality across two
    /// simulations and that the cycle really happened, so it cannot pass vacuously
    /// by never spawning a hazard at all.
    /// </summary>
    [TestCase]
    public void ChicagoRunsIdenticallyAcrossTwoSimulationsThroughAFullHazardCycle() {
        FighterMatchRules rules = new(
            (int)MatchMode.Stock,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardFrequency: (int)HazardTriggerFrequency.High,
            stageHazardTypeID: 3);

        // Nine stocks, not the production three: the scripted inputs below trade
        // basic attacks for the whole run and exhaust three stocks well before frame
        // 2,310. A concluded match stops FighterHazardSystem dead (it returns unless
        // MatchState == 1), which would make the phase assertions pass vacuously.
        var first = new FighterSimulation(
            stocks: 9, seed: 3103, rules: rules, stageGeometry: FighterStageGeometry.Chicago);
        var second = new FighterSimulation(
            stocks: 9, seed: 3103, rules: rules, stageGeometry: FighterStageGeometry.Chicago);

        var phasesSeen = new HashSet<int>();
        bool hazardOnTheCentralAnchor = false;

        for (int tick = 0; tick < 2450; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            long firstHash = first.Advance(p1, p2);
            long secondHash = second.Advance(p1, p2);
            if (firstHash != secondHash) {
                AssertThat($"chicago diverged at tick {tick}").IsEqual("");
            }
            if (first.TryGetFirstHazard(out FighterHazardComponent hazard)) {
                phasesSeen.Add(hazard.Phase);
                if (hazard.Position.x.RawValue == FighterStageGeometry.Chicago.HazardAnchorXs[0].RawValue) {
                    hazardOnTheCentralAnchor = true;
                }
            }
        }

        // A match that ended early would silently stop the hazard system and make
        // the phase assertions below meaningless, so pin liveness first.
        AssertThat(first.GetMatchState().MatchState).OverrideFailureMessage(
            "the match ended before the hazard cycle completed").IsEqual(1);
        AssertThat(hazardOnTheCentralAnchor).OverrideFailureMessage(
            "no induction grid ever spawned on the authored central anchor").IsTrue();
        // Warning (0), active (1) and recovery (2) all observed, then despawned.
        AssertThat(phasesSeen.Count).IsEqual(3);
        AssertThat(first.TryGetFirstHazard(out FighterHazardComponent _)).IsFalse();
    }

    private static Node2D Instantiate() {
        // Scenes are streamed content and must not go through AuthoredResources.
        // The root is never added to the tree, so the controller's _Ready never runs.
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
        if (tick % 29 == 0) buttons |= GameplayButtons.BasicAttack;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = axis,
            Held = buttons,
            Pressed = buttons
        };
    }
}
