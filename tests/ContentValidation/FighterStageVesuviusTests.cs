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
/// Package 6 Phase B: the Vesuvius Caldera production Fighter stage
/// (<c>vesuvius_caldera</c>, hazard type 5 — volcanic rockfall).
///
/// <para>The authored scene is a pixel mirror of the code-authored
/// <see cref="FighterStageGeometry.Vesuvius"/> through
/// <c>pixel = (950 + x·62.5, 700 − y·62.5)</c>; the simulation is authoritative and
/// every collider carries <c>collision_mask = 0</c>. This suite pins the mirror
/// (via the shared A1 conformance validator), the content contract, the stage's own
/// pool budget and audio set, the preview asset, and a two-simulation hash-identity
/// run over a full rockfall cycle with hazards on High.</para>
///
/// <para>One <c>[TestSuite]</c> per file: a second suite in the same file is
/// discovered but silently not executed (Package 6 §9, A4/ORCHESTRATOR).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageVesuviusTests {
    private const string StageID = "vesuvius_caldera";
    private const string ScenePath = "res://scenes/fighter/FighterStage_Vesuvius.tscn";
    private const string ContractPath = "res://resources/Contracts/fighter_stage_contract.tres";
    private const string PoolConfigPath =
        "res://resources/Pools/fighter_stage_configs/fighter_stage_vesuvius_pool_config.tres";
    private const string AudioSetPath = "res://resources/Audio/stage_vesuvius_audio.tres";
    private const string PreviewPath = "res://assets/placeholders/stages/vesuvius_preview.svg";

    [TestCase]
    public void SceneLoadsAndInstantiatesWithTheCatalogStageIdentity() {
        Node2D root = Instantiate();
        try {
            AssertThat(root.IsInGroup("fighter_stage")).IsTrue();
            var controller = root as FighterStageController;
            AssertObject(controller).IsNotNull();
            AssertThat(controller.StageID).IsEqual(StageID);

            var marker = root.GetNodeOrNull<ContentTemplateMarker>("ContentContract");
            AssertObject(marker).IsNotNull();
            AssertThat(marker.ContentID).IsEqual("fighter_stage_vesuvius");
            AssertObject(marker.Contract).IsNotNull();

            var camera = root.GetNodeOrNull<Camera2D>("Camera2D");
            AssertObject(camera).IsNotNull();
            AssertThat(camera.LimitLeft).IsEqual(0);
            AssertThat(camera.LimitTop).IsEqual(0);
            AssertThat(camera.LimitRight).IsEqual(1920);
            AssertThat(camera.LimitBottom).IsEqual(1080);
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void SceneSatisfiesTheSharedFighterStageContract() {
        Node2D root = Instantiate();
        try {
            var contract = FTT.Core.AuthoredResources.Load<ContentSceneContract>(ContractPath);
            AssertObject(contract).IsNotNull();
            IReadOnlyList<string> errors = ContentSceneContractValidator.Validate(root, contract);
            if (errors.Count > 0) AssertThat(string.Join(" | ", errors)).IsEqual("");
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The A1 marker↔geometry validator. A one-way platform body's *origin* is the
    /// surface point (Package 6 §9, A1), so both ledges sit at their authored
    /// <c>SurfaceY</c> rather than at the top edge of their collision rect.
    /// </summary>
    [TestCase]
    public void SceneMirrorsItsAuthoredFixedPointGeometry() {
        Node2D root = Instantiate();
        try {
            List<string> issues = FighterStageConformance.Validate(root, FighterStageGeometry.Vesuvius);
            if (issues.Count > 0) {
                AssertThat("vesuvius conformance: " + string.Join(" | ", issues)).IsEqual("");
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The narrow caldera is the stage's identity: ±8 walls (the tightest arena),
    /// two asymmetric ledges at different heights, and four rockfall anchors. A
    /// scene edit that "tidied" these into a symmetric pair would still mirror the
    /// geometry, so the shape itself is pinned here too.
    /// </summary>
    [TestCase]
    public void CollidersAndMarkersMatchTheNarrowAsymmetricCalderaLayout() {
        Node2D root = Instantiate();
        try {
            foreach (string path in new[] {
                "Geometry/Ground", "Geometry/WallLeft", "Geometry/WallRight"
            }) {
                var body = root.GetNodeOrNull<StaticBody2D>(path);
                AssertObject(body).IsNotNull();
                AssertThat(body.CollisionLayer).IsEqual(64u);
                AssertThat(body.CollisionMask).IsEqual(0u);
            }

            var low = root.GetNodeOrNull<StaticBody2D>("Geometry/BasaltLedgeLow");
            var high = root.GetNodeOrNull<StaticBody2D>("Geometry/ObsidianShelfHigh");
            AssertObject(low).IsNotNull();
            AssertObject(high).IsNotNull();
            foreach (StaticBody2D ledge in new[] { low, high }) {
                AssertThat(ledge.CollisionLayer).IsEqual(128u);
                AssertThat(ledge.CollisionMask).IsEqual(0u);
                var shape = ledge.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
                AssertObject(shape).IsNotNull();
                AssertThat(shape.OneWayCollision).IsTrue();
            }
            // Asymmetric by design: the shelf is higher and further right.
            AssertThat(high.Position.Y < low.Position.Y).IsTrue();
            AssertThat(high.Position.X > low.Position.X).IsTrue();

            AssertThat(root.GetNode<Node2D>("HazardAnchors").GetChildCount()).IsEqual(4);
            AssertThat(root.GetNode<Node2D>("OrbSpawnPoints").GetChildCount()).IsEqual(3);
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// Presentation must be era-distinct rather than a recoloured template: an ash
    /// backdrop, a parallax background with at least two layers at different scroll
    /// factors, and visible rockfall dressing on all four hazard anchors.
    /// </summary>
    [TestCase]
    public void PresentationCarriesTheEruptingCalderaDressing() {
        Node2D root = Instantiate();
        try {
            var presentation = root.GetNodeOrNull<Node2D>("Presentation");
            AssertObject(presentation).IsNotNull();
            AssertObject(presentation.GetNodeOrNull<ColorRect>("BackdropTint")).IsNotNull();

            // Typed against Parallax2D since the Package 8 B7 migration; the
            // predecessors are [Obsolete] in Godot 4.7 and a typed reference to them
            // emitted CS0618, which is why this was inspected by class name before.
            var parallax = presentation.GetNodeOrNull<Node2D>("Parallax");
            AssertObject(parallax).IsNotNull();

            var scrollFactors = new List<Vector2>();
            foreach (Node child in parallax.GetChildren()) {
                if (child is not Parallax2D layer) continue;
                scrollFactors.Add(layer.ScrollScale);
                // A layer with no visual is a layer that does nothing.
                AssertThat(layer.GetChildCount() > 0).IsTrue();
            }
            AssertThat(scrollFactors.Count >= 2).IsTrue();
            AssertThat(scrollFactors[0]).IsNotEqual(scrollFactors[1]);

            // One scorch zone per rockfall anchor.
            AssertThat(presentation.GetNode<Node2D>("ImpactScorches").GetChildCount()).IsEqual(4);
            AssertThat(presentation.GetNode<Node2D>("RockfallChutes").GetChildCount()).IsEqual(4);

            // Package 11 A9: the slope's downhill end has collapsed. The painted
            // shelf must open downward exactly where the authored floor stops, and
            // the stage's one true ledge needs a readable edge cue.
            AssertThat(FighterStageGeometry.Vesuvius.IsOpenStage).IsTrue();
            var shelf = presentation.GetNodeOrNull<ColorRect>("CollapsedShelf");
            AssertObject(shelf).IsNotNull();
            float floorPlaneY = FighterStageConformance.ToPixels(FP64.Zero, FP64.Zero).Y;
            float gapStart = FighterStageConformance.ToPixels(
                FighterStageGeometry.Vesuvius.FloorSegments[0].EdgeX(1), FP64.Zero).X;
            AssertThat(Mathf.Abs(shelf.OffsetLeft - gapStart) <= FighterStageConformance.EpsilonPixels)
                .OverrideFailureMessage(
                    $"the painted shelf starts at {shelf.OffsetLeft} px but the floor stops at {gapStart} px")
                .IsTrue();
            AssertThat(shelf.OffsetTop >= floorPlaneY).IsTrue();
            AssertObject(root.GetNodeOrNull<ColorRect>("Geometry/Ground/LedgeCue")).IsNotNull();
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void PoolConfigIsAuthoredValidAndUniquelyIdentified() {
        var config = FTT.Core.AuthoredResources.Load<ScenePoolConfig>(PoolConfigPath);
        AssertObject(config).IsNotNull();
        AssertThat(config.ConfigID).IsEqual("fighter_stage_vesuvius_pools");

        IReadOnlyList<string> errors = config.ValidateBudget();
        if (errors.Count > 0) AssertThat(string.Join("; ", errors)).IsEqual("");
        AssertThat(config.GetWarmUpInstanceCount() <= config.MaxWarmUpInstances).IsTrue();
        AssertThat(config.GetMaxCapacityCount() >= config.GetWarmUpInstanceCount()).IsTrue();

        foreach (string poolID in new[] {
            "fighter_projectile", "fighter_vfx", "fighter_environment_vfx",
            "chronal_orb", "damage_numbers", "fighter_construct"
        }) {
            bool present = false;
            foreach (PoolDefinition definition in config.PoolDefinitions) {
                if (definition.PoolID != poolID) continue;
                present = true;
                AssertThat(definition.HasValidBudget()).IsTrue();
                AssertObject(definition.SceneTemplate).IsNotNull();
            }
            AssertThat(present).OverrideFailureMessage(
                $"{PoolConfigPath} does not warm the '{poolID}' pool.").IsTrue();
        }
    }

    [TestCase]
    public void AudioSetMeetsTheSynchronizedStemContract() {
        var set = FTT.Core.AuthoredResources.Load<StageAudioSet>(AudioSetPath);
        AssertObject(set).IsNotNull();
        List<string> issues = StageAudioSetTests.Validate(set, "audio_stage_vesuvius", StageID);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void PreviewImageIsAuthoredAtTheCatalogPath() {
        AssertThat(ResourceLoader.Exists(PreviewPath)).IsTrue();
        var preview = ResourceLoader.Load<Texture2D>(PreviewPath);
        AssertObject(preview).IsNotNull();
        AssertThat(preview.GetWidth() > 0 && preview.GetHeight() > 0).IsTrue();
    }

    /// <summary>
    /// Two simulations seeded identically must advance to the same hash on every
    /// tick. The window covers the first rockfall cycle end to end: spawn at frame
    /// 1800, 90-frame warning, the rock's fall from the ceiling, its impact, the
    /// 180-frame time-dilation pool, and the 60-frame recovery before despawn.
    /// </summary>
    [TestCase]
    public void VesuviusRunsIdenticallyAcrossTwoSimulationsThroughAFullRockfallCycle() {
        FighterMatchRules rules = new(
            (int)MatchMode.Stock,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardFrequency: (int)HazardTriggerFrequency.High,
            stageHazardTypeID: 5);

        // The first hazard spawns on the 1800-frame boundary and its cycle runs to
        // ~2190. `FighterHazardSystem` only advances while `MatchState == 1`, and the
        // scripted inputs below trade enough damage to burn the default three stocks
        // before then — which froze the hazard mid-active and made "saw recovery"
        // unreachable. A deep stock pool keeps the match live across the whole cycle.
        var first = new FighterSimulation(
            stocks: 20, seed: 605, rules: rules, stageGeometry: FighterStageGeometry.Vesuvius);
        var second = new FighterSimulation(
            stocks: 20, seed: 605, rules: rules, stageGeometry: FighterStageGeometry.Vesuvius);

        bool sawWarning = false;
        bool sawActive = false;
        bool sawRecovery = false;

        for (int tick = 0; tick < 2400; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            long left = first.Advance(p1, p2);
            long right = second.Advance(p1, p2);
            if (left != right) AssertThat($"hash diverged at tick {tick}").IsEqual("");

            if (tick >= 1800 && first.TryGetFirstHazard(out FighterHazardComponent hazard)) {
                AssertThat(hazard.HazardTypeID).IsEqual(5);
                if (hazard.Phase == 0) sawWarning = true;
                else if (hazard.Phase == 1) sawActive = true;
                else sawRecovery = true;
            }
        }

        // Guards the assertion above against a vacuous pass on a hazard-free run.
        AssertThat(sawWarning).IsTrue();
        AssertThat(sawActive).IsTrue();
        AssertThat(sawRecovery).IsTrue();
    }

    /// <summary>
    /// Scenes are streamed content and must never go through
    /// <c>AuthoredResources</c>. The root is deliberately never added to the tree,
    /// so <c>FighterStageController._Ready</c> does not spawn fighters.
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
        if (tick % 29 == 0) buttons |= GameplayButtons.BasicAttack;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = axis,
            Held = buttons,
            Pressed = buttons
        };
    }
}
