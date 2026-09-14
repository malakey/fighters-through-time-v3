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
/// Content contract for the Paris Bastille production Fighter stage
/// (Package 6 Phase B). The scene is a pixel mirror of
/// <see cref="FighterStageGeometry.Paris"/> — two drawbridge walkways over the
/// sunken courtyard, three searchlight emitters on the floor — and nothing in the
/// engine enforces that mirror, so the shared conformance validator does.
///
/// <para>One <c>[TestSuite]</c> per file: a second suite in the same file is
/// discovered but silently not executed (plan §9, ORCHESTRATOR Phase A block).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageParisTests {
    private const string ScenePath = "res://scenes/fighter/FighterStage_Paris.tscn";
    private const string PoolConfigPath =
        "res://resources/Pools/fighter_stage_configs/fighter_stage_paris_pool_config.tres";
    private const string AudioSetPath = "res://resources/Audio/stage_paris_audio.tres";
    private const string PreviewPath = "res://assets/placeholders/stages/paris_preview.svg";

    private const string StageID = "paris_bastille";
    private const string ContentID = "fighter_stage_paris";

    /// <summary>
    /// Hazards spawn on the 1800-frame High-frequency boundary; warning 90 +
    /// active 360 + recovery 60 is a full cycle, so this clears one end to end.
    /// </summary>
    private const int FullHazardCycleTicks = 1800 + 90 + 360 + 60 + 30;

    [TestCase]
    public void SceneLoadsAndInstantiatesWithItsCatalogStageIdentity() {
        Node2D root = Instantiate();
        try {
            AssertThat(root.IsInGroup("fighter_stage")).IsTrue();
            var controller = root as FighterStageController;
            AssertObject(controller).IsNotNull();
            AssertThat(controller.StageID).IsEqual(StageID);

            var marker = root.GetNodeOrNull<ContentTemplateMarker>("ContentContract");
            AssertObject(marker).IsNotNull();
            AssertThat(marker.ContentID).IsEqual(ContentID);
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void SceneSatisfiesTheSharedFighterStageContract() {
        Node2D root = Instantiate();
        try {
            var marker = root.GetNodeOrNull<ContentTemplateMarker>("ContentContract");
            AssertObject(marker).IsNotNull();
            AssertObject(marker.Contract).IsNotNull();

            IReadOnlyList<string> issues = ContentSceneContractValidator.Validate(root, marker.Contract);
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// The authored markers, platform bodies, ground and walls must agree with the
    /// merged fixed-point geometry through <c>pixel = (950 + x·62.5, 700 − y·62.5)</c>.
    /// A one-way platform body's ORIGIN is the surface point, not its rect top edge
    /// (plan §9, A1 conformance note).
    /// </summary>
    [TestCase]
    public void SceneMirrorsItsAuthoredFixedPointGeometry() {
        Node2D root = Instantiate();
        try {
            List<string> issues = FighterStageConformance.Validate(root, FighterStageGeometry.Paris);
            if (issues.Count > 0) {
                AssertThat("paris conformance: " + string.Join(" | ", issues)).IsEqual("");
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// Collision hygiene the shared validator does not cover: the simulation is
    /// authoritative, so no stage collider may mask anything, and the solid bodies
    /// must sit on the Environment layer while the drawbridges sit on OneWayPlatform.
    /// </summary>
    [TestCase]
    public void EveryStageColliderIsPresentationOnlyOnItsAuthoredLayer() {
        Node2D root = Instantiate();
        try {
            var geometry = root.GetNode<Node2D>("Geometry");
            int solid = 0;
            int oneWay = 0;
            foreach (Node child in geometry.GetChildren()) {
                var body = child as StaticBody2D;
                AssertObject(body).OverrideFailureMessage(
                    $"Geometry child '{child.Name}' is not a StaticBody2D.").IsNotNull();
                AssertThat(body.CollisionMask).OverrideFailureMessage(
                    $"'{body.Name}' has a non-zero collision mask.").IsEqual(0u);
                if (body.CollisionLayer == FighterStageConformance.SolidCollisionLayer) solid++;
                else if (body.CollisionLayer == FighterStageConformance.OneWayPlatformLayer) oneWay++;
                else AssertThat($"'{body.Name}' is on layer {body.CollisionLayer}").IsEqual("");
            }
            // Package 11 A9: Paris is an Open stage, so its main floor is authored
            // as one solid body per FloorSegments entry — two of them, either side
            // of the courtyard pit — plus the two walls. The drawbridge walkways
            // stay one-way.
            AssertThat(FighterStageGeometry.Paris.IsOpenStage).IsTrue();
            AssertThat(solid).IsEqual(FighterStageGeometry.Paris.FloorSegments.Length + 2);
            AssertThat(solid).IsEqual(4);
            AssertThat(oneWay).IsEqual(FighterStageGeometry.Paris.Platforms.Length);
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// Presentation contract: a tint plus a parallax backdrop of at least two
    /// layers at distinct scroll factors, and the three searchlight emitters that
    /// dress the hazard anchors.
    /// </summary>
    [TestCase]
    public void PresentationCarriesATintedMultiLayerParallaxBackdrop() {
        Node2D root = Instantiate();
        try {
            var presentation = root.GetNode<Node2D>("Presentation");
            AssertObject(presentation.GetNodeOrNull<ColorRect>("BackdropTint")).IsNotNull();

            // Typed against Parallax2D since the Package 8 B7 migration. The
            // predecessors were reached by class name because both are [Obsolete] in
            // the Godot 4.7 bindings and a typed reference raised CS0618 against the
            // no-new-warnings gate; the replacement carries no such attribute.
            var parallax = presentation.GetNodeOrNull<Node2D>("Parallax");
            AssertObject(parallax).IsNotNull();

            var scrollFactors = new List<float>();
            foreach (Node child in parallax.GetChildren()) {
                if (child is not Parallax2D layer) continue;
                bool hasArtwork = false;
                foreach (Node grandChild in layer.GetChildren()) {
                    if (grandChild is Sprite2D sprite && sprite.Texture != null) hasArtwork = true;
                }
                AssertThat(hasArtwork).OverrideFailureMessage(
                    $"parallax layer '{layer.Name}' carries no textured Sprite2D.").IsTrue();
                scrollFactors.Add(layer.ScrollScale.X);
            }
            AssertThat(scrollFactors.Count >= 2).IsTrue();
            for (int outer = 0; outer < scrollFactors.Count; outer++) {
                for (int inner = outer + 1; inner < scrollFactors.Count; inner++) {
                    AssertThat(Mathf.Abs(scrollFactors[outer] - scrollFactors[inner]) > 0.01f).IsTrue();
                }
            }

            // One beam column per hazard anchor, dressing each emitter.
            int beamColumns = 0;
            foreach (Node child in presentation.GetChildren()) {
                if (child.Name.ToString().StartsWith("BeamColumn")) beamColumns++;
            }
            AssertThat(beamColumns).IsEqual(FighterStageGeometry.Paris.HazardAnchorXs.Length);

            // Package 11 A9: the courtyard pit is real geometry now, and it needs a
            // readable depth cue over the authored gap rather than the old painted
            // trench that spanned both spawn points over solid floor.
            var pit = presentation.GetNodeOrNull<ColorRect>("CourtyardPit");
            AssertObject(pit).IsNotNull();
            FighterStagePlatform leftSegment = FighterStageGeometry.Paris.FloorSegments[0];
            FighterStagePlatform rightSegment = FighterStageGeometry.Paris.FloorSegments[1];
            float pitLeft = FighterStageConformance.ToPixels(leftSegment.EdgeX(1), FP64.Zero).X;
            float pitRight = FighterStageConformance.ToPixels(rightSegment.EdgeX(0), FP64.Zero).X;
            AssertThat(Mathf.Abs(pit.OffsetLeft - pitLeft) <= FighterStageConformance.EpsilonPixels)
                .OverrideFailureMessage(
                    $"the painted pit starts at {pit.OffsetLeft} px but the authored gap starts at {pitLeft} px")
                .IsTrue();
            AssertThat(Mathf.Abs(pit.OffsetRight - pitRight) <= FighterStageConformance.EpsilonPixels)
                .OverrideFailureMessage(
                    $"the painted pit ends at {pit.OffsetRight} px but the authored gap ends at {pitRight} px")
                .IsTrue();
            // It has to open DOWNWARD from the floor plane, not sit on top of it.
            AssertThat(pit.OffsetTop >= FighterStageConformance.ToPixels(FP64.Zero, FP64.Zero).Y).IsTrue();
            // Both true ledges carry an edge cue.
            AssertObject(root.GetNodeOrNull<ColorRect>("Geometry/GroundLeft/LedgeCue")).IsNotNull();
            AssertObject(root.GetNodeOrNull<ColorRect>("Geometry/GroundRight/LedgeCue")).IsNotNull();
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void PreviewImageIsAuthoredAtThePathTheCatalogWillPointAt() {
        AssertThat(ResourceLoader.Exists(PreviewPath)).IsTrue();
        var texture = ResourceLoader.Load<Texture2D>(PreviewPath);
        AssertObject(texture).IsNotNull();
        AssertThat(texture.GetWidth() > 0 && texture.GetHeight() > 0).IsTrue();
    }

    [TestCase]
    public void PoolConfigIsUniquelyIdentifiedValidAndInsideItsWarmUpBudget() {
        ScenePoolConfig config = AuthoredResources.Load<ScenePoolConfig>(PoolConfigPath);
        AssertObject(config).IsNotNull();
        AssertThat(config.ConfigID).IsEqual("fighter_stage_paris_pools");

        IReadOnlyList<string> errors = config.ValidateBudget();
        if (errors.Count > 0) AssertThat(string.Join("; ", errors)).IsEqual("");
        AssertThat(config.GetWarmUpInstanceCount() <= config.MaxWarmUpInstances).IsTrue();
        AssertThat(config.GetMaxCapacityCount() >= config.GetWarmUpInstanceCount()).IsTrue();

        // Mirrors the Test Arena's Fighter pool set exactly (plan §5 item 3).
        var poolIDs = new HashSet<string>();
        foreach (PoolDefinition definition in config.PoolDefinitions) poolIDs.Add(definition.PoolID);
        AssertThat(poolIDs.Contains("fighter_projectile")).IsTrue();
        AssertThat(poolIDs.Contains("fighter_vfx")).IsTrue();
        AssertThat(poolIDs.Contains("fighter_environment_vfx")).IsTrue();
        AssertThat(poolIDs.Contains("chronal_orb")).IsTrue();
        AssertThat(poolIDs.Contains("damage_numbers")).IsTrue();
        AssertThat(poolIDs.Contains("fighter_construct")).IsTrue();

        ScenePoolConfig testArena =
            AuthoredResources.Load<ScenePoolConfig>("res://resources/Pools/test_arena_pool_config.tres");
        AssertObject(testArena).IsNotNull();
        AssertThat(config.GetWarmUpInstanceCount()).IsEqual(testArena.GetWarmUpInstanceCount());
        AssertThat(config.GetMaxCapacityCount()).IsEqual(testArena.GetMaxCapacityCount());
    }

    [TestCase]
    public void AudioSetMeetsTheStageStemContract() {
        StageAudioSet set = AuthoredResources.Load<StageAudioSet>(AudioSetPath);
        AssertObject(set).IsNotNull();

        List<string> issues = StageAudioSetTests.Validate(set, "audio_stage_paris", StageID);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(set.StemsAreSynchronized).IsTrue();
    }

    /// <summary>
    /// The Paris geometry must remain bit-identical across two independent
    /// simulations driven by the same inputs, all the way through a full
    /// neural-dampening-beam cycle (spawn, sweep, recovery, despawn) with hazards
    /// and orbs at High.
    /// </summary>
    [TestCase]
    public void TwoSimulationsOnTheParisGeometryStayHashIdenticalThroughAFullHazardCycle() {
        FighterMatchRules rules = new(
            (int)MatchMode.Stock,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardFrequency: (int)HazardTriggerFrequency.High,
            stageHazardTypeID: FighterHazardTypeID.ParisDampeningBeam);

        // The first hazard spawns on the 1800-frame boundary, and the hazard system
        // stops advancing phases the moment the match completes. Three stocks do not
        // survive 1800 frames of scripted attacking, so the stock count is raised
        // well past what this run can burn through; the match-state assertion below
        // fails loudly if that ever stops being true instead of silently skipping
        // the beam.
        var first = new FighterSimulation(
            stocks: 25, seed: 6204, rules: rules, stageGeometry: FighterStageGeometry.Paris);
        var second = new FighterSimulation(
            stocks: 25, seed: 6204, rules: rules, stageGeometry: FighterStageGeometry.Paris);

        bool sawActiveBeam = false;
        int hazardFrames = 0;
        int maxPhase = -1;
        for (int tick = 0; tick < FullHazardCycleTicks; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            long left = first.Advance(p1, p2);
            long right = second.Advance(p1, p2);
            if (left != right) AssertThat($"paris desync at tick {tick}").IsEqual("");
            if (first.TryGetFirstHazard(out FighterHazardComponent hazard)) {
                hazardFrames++;
                if (hazard.Phase > maxPhase) maxPhase = hazard.Phase;
                if (hazard.Phase == FighterHazardSystem.ActivePhase) sawActiveBeam = true;
            }
        }
        FighterMatchComponent finalMatch = first.GetMatchState();
        AssertThat(finalMatch.MatchState).OverrideFailureMessage(
            "the match ended before the hazard cycle completed; raise the stock count")
            .IsEqual(FighterMatchStates.InProgress);
        AssertThat(sawActiveBeam).OverrideFailureMessage(
            $"no active beam in {FullHazardCycleTicks} ticks: hazardFrames={hazardFrames}, " +
            $"maxPhase={maxPhase}, matchState={finalMatch.MatchState}, " +
            $"remainingFrames={finalMatch.RemainingFrames}, " +
            $"nextHazardSpawn={finalMatch.NextHazardSpawnFrames}").IsTrue();
    }

    private static Node2D Instantiate() {
        // Scenes are streamed content and must never go through AuthoredResources.
        // The root is never added to the tree, so the controller's _Ready does not
        // run and no fighters are spawned.
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(packed).IsNotNull();
        var root = packed.Instantiate<Node2D>();
        AssertObject(root).IsNotNull();
        return root;
    }

    private static PlayerInputFrame InputFor(int playerID, int tick) {
        sbyte axis = (sbyte)(((tick + playerID * 13) % 7 - 3) * 40);
        GameplayButtons buttons = GameplayButtons.None;
        if (tick % 43 == 0) buttons |= GameplayButtons.Jump;
        if (tick % 29 == 0) buttons |= GameplayButtons.BasicAttack;
        if (tick % 61 == 0) buttons |= GameplayButtons.Block;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = axis,
            Held = buttons,
            Pressed = buttons
        };
    }
}
