using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 6 Phase B contract for the Gettysburg Ridge production Fighter stage
/// (<c>gettysburg_ridge</c>, hazard type 10).
///
/// <para>The scene is a pixel mirror of <see cref="FighterStageGeometry.Gettysburg"/>
/// — nothing in the engine enforces that, so
/// <see cref="FighterStageConformance"/> is the enforcement and this suite is
/// where it runs for this stage. The remaining cases pin the pieces the closeout
/// wires into the shared catalogs (pool config, audio set, preview art) so a
/// missing or misnamed file fails here rather than silently at integration.</para>
///
/// <para>One <c>[TestSuite]</c> per file: a second suite in the same file is
/// discovered but never executed (plan §9, INTEGRATION-A).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageGettysburgTests {
    private const string ScenePath = "res://scenes/fighter/FighterStage_Gettysburg.tscn";
    private const string PoolConfigPath =
        "res://resources/Pools/fighter_stage_configs/fighter_stage_gettysburg_pool_config.tres";
    private const string AudioSetPath = "res://resources/Audio/stage_gettysburg_audio.tres";
    private const string PreviewPath = "res://assets/placeholders/stages/gettysburg_preview.svg";
    private const string StageID = "gettysburg_ridge";
    private const string ContentID = "fighter_stage_gettysburg";

    [TestCase]
    public void TheStageSceneLoadsAndInstantiatesWithItsCatalogIdentity() {
        Node2D root = Instantiate();
        try {
            AssertThat(root.IsInGroup("fighter_stage")).IsTrue();
            var controller = root as FighterStageController;
            AssertObject(controller).IsNotNull();
            AssertThat(controller.StageID).IsEqual(StageID);

            // The controller must resolve the same authored geometry the scene mirrors.
            AssertThat(FighterStageGeometry.ForStage(controller.StageID))
                .IsSame(FighterStageGeometry.Gettysburg);
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void TheStageSceneSatisfiesTheFighterStageContract() {
        Node2D root = Instantiate();
        try {
            var marker = root.GetNodeOrNull<ContentTemplateMarker>("ContentContract");
            AssertObject(marker).IsNotNull();
            AssertThat(marker.ContentID).IsEqual(ContentID);
            AssertObject(marker.Contract).IsNotNull();
            AssertThat(marker.Contract.ContractID).IsEqual("fighter_stage_v1");

            IReadOnlyList<string> errors =
                ContentSceneContractValidator.Validate(root, marker.Contract);
            if (errors.Count > 0) AssertThat(string.Join(" | ", errors)).IsEqual("");
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The §2.10 marker↔geometry mirror. Note the platform bodies' *origins* are
    /// the surface points, not the top edge of their collision rects (plan §9, A1).
    /// </summary>
    [TestCase]
    public void TheStageSceneMirrorsItsAuthoredFixedPointGeometry() {
        Node2D root = Instantiate();
        try {
            List<string> issues =
                FighterStageConformance.Validate(root, FighterStageGeometry.Gettysburg);
            if (issues.Count > 0) {
                AssertThat("gettysburg conformance: " + string.Join(" | ", issues)).IsEqual("");
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The two broken rail fences are the stage's one-way platforms: layer 128,
    /// <c>one_way_collision</c>, and no collision mask, because the deterministic
    /// simulation — not Godot physics — owns landing and drop-through.
    /// </summary>
    [TestCase]
    public void TheRailFencesAreOneWayPlatformsAndTheRidgeIsSolid() {
        Node2D root = Instantiate();
        try {
            foreach (string path in new[] { "Geometry/RailFenceLeft", "Geometry/RailFenceRight" }) {
                var fence = root.GetNodeOrNull<StaticBody2D>(path);
                AssertObject(fence).IsNotNull();
                AssertThat(fence.CollisionLayer).IsEqual(FighterStageConformance.OneWayPlatformLayer);
                AssertThat(fence.CollisionMask).IsEqual(0u);
                var shape = fence.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
                AssertObject(shape).IsNotNull();
                AssertThat(shape.OneWayCollision).IsTrue();
            }

            foreach (string path in new[] { "Geometry/Ground", "Geometry/WallLeft", "Geometry/WallRight" }) {
                var body = root.GetNodeOrNull<StaticBody2D>(path);
                AssertObject(body).IsNotNull();
                AssertThat(body.CollisionLayer).IsEqual(FighterStageConformance.SolidCollisionLayer);
                AssertThat(body.CollisionMask).IsEqual(0u);
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// Presentation contract (plan §5 item 2): a backdrop tint plus a parallax
    /// background with at least two layers at distinct scroll factors, and the
    /// era dressing that makes the artillery band readable before it fires.
    ///
    /// <para>Typed against <see cref="Parallax2D"/> since the Package 8 B7
    /// migration. The old <c>ParallaxBackground</c>/<c>ParallaxLayer</c> pair is
    /// `[Obsolete]` in the Godot 4.7 bindings, which is why this assertion used to
    /// go through <see cref="Node.IsClass"/> and untyped property reads; the
    /// replacement class needs no such workaround.</para>
    /// </summary>
    [TestCase]
    public void ThePresentationLayerCarriesTheEraBackdropAndHazardDressing() {
        Node2D root = Instantiate();
        try {
            AssertObject(root.GetNodeOrNull<ColorRect>("Presentation/BackdropTint")).IsNotNull();

            var parallax = root.GetNodeOrNull<Node2D>("Presentation/Parallax");
            AssertObject(parallax).IsNotNull();

            var scrollFactors = new List<float>();
            foreach (Node child in parallax.GetChildren()) {
                if (child is not Parallax2D layer) continue;
                // Each layer must actually carry placeholder art, not be an empty node.
                AssertObject(layer.GetNodeOrNull<Sprite2D>("Sprite2D")?.Texture).IsNotNull();
                scrollFactors.Add(layer.ScrollScale.X);
            }
            AssertThat(scrollFactors.Count >= 2).IsTrue();
            // Distinct scroll factors, otherwise there is no parallax.
            for (int outer = 0; outer < scrollFactors.Count; outer++) {
                for (int inner = outer + 1; inner < scrollFactors.Count; inner++) {
                    AssertThat(Mathf.Abs(scrollFactors[outer] - scrollFactors[inner]) > 0.01f).IsTrue();
                }
            }

            // One scorched strike band per authored hazard anchor.
            foreach (string dressing in new[] {
                "Presentation/ScorchWest", "Presentation/ScorchCenter", "Presentation/ScorchEast",
                "Presentation/SightLineBand"
            }) {
                AssertObject(root.GetNodeOrNull<ColorRect>(dressing)).IsNotNull();
            }
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void TheStagePoolConfigIsUniquelyIdentifiedAndWithinItsWarmUpBudget() {
        var config = AuthoredResources.Load<ScenePoolConfig>(PoolConfigPath);
        AssertObject(config).IsNotNull();
        AssertThat(config.ConfigID).IsEqual("fighter_stage_gettysburg_pools");

        IReadOnlyList<string> errors = config.ValidateBudget();
        if (errors.Count > 0) AssertThat(string.Join(" | ", errors)).IsEqual("");

        // Mirrors the Test Arena pool set; a Fighter stage warms the same six pools.
        var poolIDs = new List<string>();
        foreach (PoolDefinition definition in config.PoolDefinitions) poolIDs.Add(definition.PoolID);
        foreach (string expected in new[] {
            "fighter_projectile", "fighter_vfx", "fighter_environment_vfx",
            "chronal_orb", "damage_numbers", "fighter_construct"
        }) {
            AssertThat(poolIDs.Contains(expected)).IsTrue();
        }
        AssertThat(config.GetWarmUpInstanceCount() <= config.MaxWarmUpInstances).IsTrue();
    }

    [TestCase]
    public void TheStageAudioSetMeetsTheSynchronizedStemContract() {
        var set = AuthoredResources.Load<StageAudioSet>(AudioSetPath);
        AssertObject(set).IsNotNull();

        List<string> issues =
            StageAudioSetTests.Validate(set, "audio_stage_gettysburg", StageID);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(set.StemsAreSynchronized).IsTrue();
    }

    [TestCase]
    public void TheStagePreviewImageIsAuthoredAndLoadable() {
        AssertThat(ResourceLoader.Exists(PreviewPath)).IsTrue();
        var preview = ResourceLoader.Load<Texture2D>(PreviewPath);
        AssertObject(preview).IsNotNull();
        AssertThat(preview.GetWidth() > 0).IsTrue();
        AssertThat(preview.GetHeight() > 0).IsTrue();
    }

    /// <summary>
    /// Two independent simulations on this stage's geometry, hazards and orbs at
    /// High, must agree on every frame hash across a full artillery cycle. The
    /// Gettysburg telegraph is 120 frames (the only 2 s warning in the catalog),
    /// not the standard 90, so the window below has to clear
    /// 1800 + 120 + 30 + 60 = 2010 to cover warning → strike → recovery → despawn.
    /// </summary>
    [TestCase]
    public void TheStageRunsIdenticallyAcrossTwoSimulationsThroughAFullHazardCycle() {
        FighterMatchRules rules = new(
            (int)MatchMode.Hybrid,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardFrequency: (int)HazardTriggerFrequency.High,
            stageHazardTypeID: FighterHazardTypeID.GettysburgArtillery);

        var first = new FighterSimulation(
            seed: 6210, rules: rules, stageGeometry: FighterStageGeometry.Gettysburg);
        var second = new FighterSimulation(
            seed: 6210, rules: rules, stageGeometry: FighterStageGeometry.Gettysburg);

        bool sawWarning = false;
        bool sawActive = false;
        bool sawRecovery = false;

        for (int tick = 0; tick < 2100; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            long firstHash = first.Advance(p1, p2);
            long secondHash = second.Advance(p1, p2);
            if (firstHash != secondHash) {
                AssertThat($"hash divergence on tick {tick}").IsEqual("");
            }
            if (first.TryGetFirstHazard(out FighterHazardComponent hazard)) {
                if (hazard.Phase == 0) sawWarning = true;
                else if (hazard.Phase == 1) sawActive = true;
                else sawRecovery = true;
            }
        }

        // A hash-identity run that never reached the hazard would prove nothing.
        AssertThat(sawWarning).IsTrue();
        AssertThat(sawActive).IsTrue();
        AssertThat(sawRecovery).IsTrue();
    }

    /// <summary>
    /// Steers both fighters with a repeating, deterministic pattern so the run
    /// exercises movement, platform landings and hazard overlap rather than two
    /// idle fighters standing on their spawns.
    /// </summary>
    private static PlayerInputFrame InputFor(int player, int tick) {
        int phase = tick + player * 37;
        GameplayButtons buttons = GameplayButtons.None;
        if (phase % 61 == 0) buttons |= GameplayButtons.Jump;
        if (phase % 97 == 0) buttons |= GameplayButtons.Block;
        if (phase % 131 == 0) buttons |= GameplayButtons.BasicAttack;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = (sbyte)((phase % 11 - 5) * 25),
            Held = buttons,
            Pressed = buttons
        };
    }

    /// <summary>
    /// Scenes are streamed content and must never go through
    /// <c>AuthoredResources</c>. The instantiated root is deliberately not added
    /// to the tree, so <c>FighterStageController._Ready</c> does not spawn
    /// fighters or start a simulation.
    /// </summary>
    private static Node2D Instantiate() {
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        var root = packed.Instantiate<Node2D>();
        AssertObject(root).IsNotNull();
        return root;
    }
}
