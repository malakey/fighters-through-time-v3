using System.Collections.Generic;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>All ten approved A plates must load and register to the same
/// world-space surfaces as their simulation and scene colliders.</summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStagePresentationTests {

    [TestCase]
    public void EveryStageHasALoadableWorldRegisteredReferencePlate() {
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        AssertThat(catalog.Stages.Length).IsEqual(10);
        AssertThat(FTT.FighterSim.FighterStageVisualProfile.All.Count).IsEqual(10);
        var paths = new HashSet<string>();
        int openStages = 0;
        foreach (FighterStageData stage in catalog.Stages) {
            var profile = FTT.FighterSim.FighterStageVisualProfile.Find(stage.StageID);
            AssertObject(profile).IsNotNull();
            AssertThat(paths.Add(profile.PlatePath)).IsTrue();
            Texture2D plate = ResourceLoader.Load<Texture2D>(profile.PlatePath);
            AssertObject(plate).OverrideFailureMessage(stage.StageID + " reference plate missing").IsNotNull();
            AssertThat(plate.GetWidth() > 0 && plate.GetHeight() > 0).IsTrue();
            Texture2D backdrop = ResourceLoader.Load<Texture2D>(profile.BackdropPath);
            AssertObject(backdrop).OverrideFailureMessage(stage.StageID + " distant scenery backdrop missing").IsNotNull();
            AssertThat(backdrop.GetWidth() > 0 && backdrop.GetHeight() > 0).IsTrue();
            Texture2D foreground = ResourceLoader.Load<Texture2D>(profile.ForegroundPath);
            AssertObject(foreground).OverrideFailureMessage(stage.StageID + " transparent foreground missing").IsNotNull();
            AssertThat(foreground.GetWidth() > 0 && foreground.GetHeight() > 0).IsTrue();
            Image foregroundImage = foreground.GetImage();
            AssertThat(foregroundImage.DetectAlpha() != Image.AlphaMode.None).IsTrue();
            AssertThat(foregroundImage.GetUsedRect().Size.Y > 0).IsTrue();
            var geometry = FTT.FighterSim.FighterStageGeometry.ForStage(stage.StageID);
            Node2D scene = ResourceLoader.Load<PackedScene>(stage.ScenePath).Instantiate<Node2D>();
            try {
                AssertThat(scene is FighterStageController).IsTrue();
                var issues = FighterStageConformance.Validate(scene, geometry);
                AssertThat(string.Join(" | ", issues)).IsEqual("");
                // Both views cover exactly the same painted x range; camera motion
                // cannot introduce an independent transform between art and physics.
                AssertThat(Mathf.Abs((float)(profile.WorldX(1672) - profile.WorldX(0)) * 62.5f - 960)).IsLess(0.001f);
                AssertThat((float)profile.WorldY(profile.FloorRow)).IsEqual(0f);
                if (geometry.IsOpenStage) openStages++;
                AssertThat(geometry.HasFloorSupport(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(-geometry.SpawnDistance))).IsTrue();
                AssertThat(geometry.HasFloorSupport(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(geometry.SpawnDistance))).IsTrue();
                for (int i = 0; i < geometry.Platforms.Length; i++) {
                    var span = profile.PlatformSpans[i];
                    Vector2 surface = FighterStageConformance.ToPixels(geometry.Platforms[i].CenterX, geometry.Platforms[i].SurfaceY);
                    float paintedY = 700 + (float)((span.Top - profile.FloorRow) * FTT.FighterSim.FighterStageVisualProfile.PlateScale);
                    AssertThat(Mathf.Abs(surface.Y - paintedY)).IsLess(0.001f);
                }
                for (int i = 0; i < geometry.FloorSegments.Length; i++) {
                    var span = profile.FloorSpans[i];
                    float left = FighterStageConformance.UnitsToPixels(geometry.FloorSegments[i].EdgeX(0)) + 950;
                    float paintedLeft = 950 + (float)((span.Left - 836) * FTT.FighterSim.FighterStageVisualProfile.PlateScale);
                    AssertThat(Mathf.Abs(left - paintedLeft)).IsLess(0.001f);
                }
            } finally { scene.Free(); }
        }
        AssertThat(openStages).IsEqual(5);
    }

    [TestCase]
    public void ReferenceFrameIsStableAtSpawnAndExpandsForElevatedPlay() {
        foreach (var profile in FTT.FighterSim.FighterStageVisualProfile.All) {
            Vector2 center = new(950, (float)profile.FrameCenterY);
            Vector2 left = new(950 - profile.SpawnDistance * 62.5f, 700);
            Vector2 right = new(950 + profile.SpawnDistance * 62.5f, 700);
            var idle = FTT.Combat.FighterCamera.ReferenceFrameForPlayers(center, 2, new Vector2(1920, 1080), left, right);
            AssertThat(idle.Center).IsEqual(center);
            AssertThat(idle.Zoom).IsEqual(2f);
            var airborne = FTT.Combat.FighterCamera.ReferenceFrameForPlayers(center, 2, new Vector2(1920, 1080), left - new Vector2(0, 450), right);
            AssertThat(airborne.Zoom).IsLess(2f);
            AssertThat(airborne.Center.Y).IsLess(center.Y);
            Vector2 half = new Vector2(1920, 1080) / (2 * airborne.Zoom);
            float silhouette = 1080 * FTT.FighterSim.FighterStageVisualProfile.FighterHeightFraction / 2;
            AssertThat(airborne.Center.Y - half.Y <= left.Y - 450 - silhouette - 30 + 0.001f).IsTrue();
            AssertThat(airborne.Center.Y + half.Y >= right.Y + 30 - 0.001f).IsTrue();
            float headScreenY = (left.Y - 450 - silhouette - airborne.Center.Y) * airborne.Zoom + 540;
            float feetScreenY = (right.Y - airborne.Center.Y) * airborne.Zoom + 540;
            AssertThat(headScreenY >= 280 - 0.001f).OverrideFailureMessage(
                profile.StageID + " elevated silhouette overlaps the Fighter HUD").IsTrue();
            AssertThat(feetScreenY <= 1080 - 30 + 0.001f).OverrideFailureMessage(
                profile.StageID + " grounded fighter leaves the bottom safe area").IsTrue();
        }
        AssertObject(FTT.FighterSim.FighterStageVisualProfile.Find("unknown")).IsNull();
    }

    [TestCase]
    public void ACameraStepCannotLagBehindAnElevatedFighterIntoTheHud() {
        Vector2 viewport = new(1920, 1080);
        Vector2 oldCenter = new(950, 500);
        Vector2 highFeet = new(650, 250);
        Vector2 lowFeet = new(1250, 700);
        var target = FTT.Combat.FighterCamera.ReferenceFrameForPlayers(oldCenter, 2, viewport, highFeet, lowFeet);
        Vector2 smoothCenter = oldCenter.Lerp(target.Center, 5f / 60);
        float smoothZoom = Mathf.Lerp(2, target.Zoom, 2f / 60);
        var safe = FTT.Combat.FighterCamera.ConstrainReferenceFrame(smoothCenter, smoothZoom, 2,
            viewport, highFeet, lowFeet);
        float silhouette = 1080 * FTT.FighterSim.FighterStageVisualProfile.FighterHeightFraction / 2;
        float headScreenY = (highFeet.Y - silhouette - safe.Center.Y) * safe.Zoom + 540;
        float feetScreenY = (lowFeet.Y - safe.Center.Y) * safe.Zoom + 540;
        AssertThat(headScreenY >= 280 - 0.001f).IsTrue();
        AssertThat(feetScreenY <= 1050 + 0.001f).IsTrue();
        AssertThat(safe.Zoom <= smoothZoom).IsTrue();
    }

    [TestCase]
    public void EveryPlatformCameraFrameAndSmoothedStepRemainFiniteAndHudSafe() {
        Vector2 viewport = new(1920, 1080);
        foreach (var geometry in FTT.FighterSim.FighterStageGeometry.AllAuthored) {
            var profile = FTT.FighterSim.FighterStageVisualProfile.Find(geometry.StageID);
            Vector2 referenceCenter = new(950, (float)profile.FrameCenterY);
            Vector2 opponentFeet = new(950 + geometry.SpawnDistance * 62.5f, 700);
            foreach (var platform in geometry.Platforms) {
                Vector2 elevatedFeet = FighterStageConformance.ToPixels(platform.CenterX, platform.SurfaceY);
                var target = FTT.Combat.FighterCamera.ReferenceFrameForPlayers(referenceCenter, 2,
                    viewport, elevatedFeet, opponentFeet);
                AssertFiniteHudSafeFrame(target.Center, target.Zoom, elevatedFeet, opponentFeet);
                Vector2 center = referenceCenter;
                float zoom = 2;
                for (int step = 0; step < 180; step++) {
                    var frame = FTT.Combat.FighterCamera.ConstrainReferenceFrame(
                        center.Lerp(target.Center, 5f / 60), Mathf.Lerp(zoom, target.Zoom, 2f / 60),
                        2, viewport, elevatedFeet, opponentFeet);
                    AssertFiniteHudSafeFrame(frame.Center, frame.Zoom, elevatedFeet, opponentFeet);
                    center = frame.Center;
                    zoom = frame.Zoom;
                }
            }
        }
        // Exact world positions from the failing Nassau platform-zero capture.
        var nassau = FTT.Combat.FighterCamera.ReferenceFrameForPlayers(new Vector2(950, 533.7799f),
            2, viewport, new Vector2(724.067f, 435.88513f), new Vector2(1200, 700));
        AssertFiniteHudSafeFrame(nassau.Center, nassau.Zoom,
            new Vector2(724.067f, 435.88513f), new Vector2(1200, 700));
    }

    private static void AssertFiniteHudSafeFrame(Vector2 center, float zoom, Vector2 firstFeet, Vector2 secondFeet) {
        AssertThat(float.IsFinite(center.X) && float.IsFinite(center.Y) && float.IsFinite(zoom)).IsTrue();
        float silhouette = 1080 * FTT.FighterSim.FighterStageVisualProfile.FighterHeightFraction / 2;
        float headScreenY = (Mathf.Min(firstFeet.Y, secondFeet.Y) - silhouette - center.Y) * zoom + 540;
        float feetScreenY = (Mathf.Max(firstFeet.Y, secondFeet.Y) - center.Y) * zoom + 540;
        AssertThat(headScreenY >= 280 - 0.02f).IsTrue();
        AssertThat(feetScreenY <= 1050 + 0.02f).IsTrue();
    }

    [TestCase]
    public void ASpanTooLargeForMinimumZoomUsesAFiniteBalancedFrame() {
        Vector2 highFeet = new(-3000, -2000);
        Vector2 lowFeet = new(4000, 3000);
        var frame = FTT.Combat.FighterCamera.ConstrainReferenceFrame(new Vector2(950, 500),
            2, 2, new Vector2(1920, 1080), highFeet, lowFeet);
        AssertThat(frame.Zoom).IsEqual(0.55f);
        AssertThat(float.IsFinite(frame.Center.X) && float.IsFinite(frame.Center.Y)).IsTrue();
        // Containment is impossible: centre the oversized bounds in the available
        // safe area rather than throwing or pushing all the overflow to one edge.
        float silhouette = 1080 * FTT.FighterSim.FighterStageVisualProfile.FighterHeightFraction / 2;
        float paddedTopScreen = (highFeet.Y - silhouette - 30 - frame.Center.Y) * frame.Zoom + 540;
        float paddedBottomScreen = (lowFeet.Y + 30 - frame.Center.Y) * frame.Zoom + 540;
        AssertThat(Mathf.Abs((280 - paddedTopScreen) - (paddedBottomScreen - 1050))).IsLess(0.02f);
    }

    [TestCase]
    public void ExpandedReferenceFrameBlendsOnlyAfterLeavingTheAuthoredZoom() {
        AssertThat(FighterStageController.ReferenceEdgeBlend(2)).IsEqual(0f);
        AssertThat(FighterStageController.ReferenceEdgeBlend(2.5f)).IsEqual(0f);
        AssertThat(FighterStageController.ReferenceEdgeBlend(1.8f)).IsGreater(0f);
        AssertThat(FighterStageController.ReferenceEdgeBlend(1.65f)).IsEqual(1f);
        AssertObject(ResourceLoader.Load<Shader>("res://assets/shaders/fighter_reference_edge.gdshader")).IsNotNull();
    }

    [TestCase]
    public void ReferenceLayersCompleteTheirTimedTransitionAtIntermediateZoomAndReturnOnlyAtTheAuthoredFrame() {
        Vector2 reference = new(950, 500);
        var state = (Expanded: false, Blend: 0f);
        state = FighterStageController.AdvanceReferencePresentation(state.Expanded, state.Blend,
            reference, 2, reference, 1);
        AssertThat(state.Expanded).IsFalse();
        AssertThat(state.Blend).IsEqual(0f);
        state = FighterStageController.AdvanceReferencePresentation(state.Expanded, state.Blend,
            reference, 1.9f, reference, 0.075f);
        AssertThat(Mathf.Abs(state.Blend - 0.5f)).IsLess(0.001f);
        for (int frame = 0; frame < 60; frame++) {
            state = FighterStageController.AdvanceReferencePresentation(state.Expanded, state.Blend,
                reference, 1.9f, reference, 1f / 60);
        }
        AssertThat(state.Expanded).IsTrue();
        AssertThat(state.Blend).IsEqual(1f);
        // Zoom alone does not restore the full plate while the camera is translated.
        state = FighterStageController.AdvanceReferencePresentation(state.Expanded, state.Blend,
            reference + new Vector2(2, 0), 2, reference, 1);
        AssertThat(state.Blend).IsEqual(1f);
        // The hysteresis band retains the chosen layer rather than flickering.
        state = FighterStageController.AdvanceReferencePresentation(state.Expanded, state.Blend,
            reference + Vector2.One, 2, reference, 1);
        AssertThat(state.Expanded).IsTrue();
        state = FighterStageController.AdvanceReferencePresentation(state.Expanded, state.Blend,
            reference, 2, reference, 0.075f);
        AssertThat(state.Expanded).IsFalse();
        AssertThat(Mathf.Abs(state.Blend - 0.5f)).IsLess(0.001f);
        for (int frame = 0; frame < 60; frame++) {
            state = FighterStageController.AdvanceReferencePresentation(state.Expanded, state.Blend,
                reference, 2, reference, 1f / 60);
        }
        AssertThat(state.Expanded).IsFalse();
        AssertThat(state.Blend).IsEqual(0f);
        // A translation at unchanged zoom also triggers the timed scene separation.
        state = FighterStageController.AdvanceReferencePresentation(state.Expanded, state.Blend,
            reference + new Vector2(2, 0), 2, reference, 1);
        AssertThat(state.Expanded).IsTrue();
        AssertThat(state.Blend).IsEqual(1f);
    }

    [TestCase]
    public void ForegroundFeatherIsLimitedToTwelveWorldPixelsOnEveryCanvasEdge() {
        Vector2 width = FighterStageController.ReferenceForegroundEdgeWidth;
        AssertThat(Mathf.Abs(width.X * 960 - 12)).IsLess(0.001f);
        float canvasHeight = (float)(FTT.FighterSim.FighterStageVisualProfile.ReferenceHeight
            * FTT.FighterSim.FighterStageVisualProfile.PlateScale);
        AssertThat(Mathf.Abs(width.Y * canvasHeight - 12)).IsLess(0.001f);
        AssertThat(width.X < 0.05f && width.Y < 0.05f).IsTrue();
    }

    [TestCase]
    public void DedicatedBackdropAndFallbackCropCoverEveryExpandedFighterView() {
        foreach (float cropHeight in new[] { 1f, 0.65f }) {
            Vector2 cropSize = new(1672, 941 * cropHeight);
            foreach (float zoom in new[] { 2f, 1.65f, 1f, 0.55f }) {
                Vector2 view = new Vector2(1920, 1080) / zoom;
                Vector2 covered = ReferenceSceneBackdrop.CoverSize(cropSize, view);
                AssertThat(covered.X + 0.001f >= view.X).IsTrue();
                AssertThat(covered.Y + 0.001f >= view.Y).IsTrue();
                AssertThat(Mathf.Abs(covered.X / covered.Y - cropSize.X / cropSize.Y)).IsLess(0.001f);
            }
        }
    }

    [TestCase]
    public void KnockoutFocusKeepsItsRelativeTighteningInAReferenceFrame() {
        var camera = new FTT.Combat.FighterCamera { MinZoom = 1, MaxZoom = 1.4f };
        try {
            camera.FocusOn(new Vector2(950, 700), 1.8f);
            AssertThat(camera.FocusTargetZoom).IsEqual(1.8f);
            camera.ReleaseFocus();
            camera.ConfigureReferenceFrame(new Vector2(950, 500), 2);
            camera.FocusOn(new Vector2(950, 700), 1.8f);
            AssertThat(camera.IsFocused).IsTrue();
            AssertThat(camera.FocusTargetZoom).IsGreater(2f);
            AssertThat(Mathf.Abs(camera.FocusTargetZoom - 1.8f * 2 / 1.4f)).IsLess(0.001f);
            camera.ReleaseFocus();
            AssertThat(camera.IsFocused).IsFalse();
        } finally { camera.Free(); }
    }

    /// <summary>
    /// Every catalog stage routes to its own scene, and that scene declares the
    /// catalog's StageID. A copy-pasted scene that kept a sibling's id would resolve
    /// to the wrong deterministic geometry at runtime while every per-stage suite
    /// (which loads its scene by path) stayed green.
    /// </summary>
    [TestCase]
    public void EveryCatalogScenePathResolvesToASceneDeclaringThatStageID() {
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        var seenPaths = new HashSet<string>();
        var issues = new List<string>();

        foreach (FighterStageData stage in catalog.Stages) {
            if (!seenPaths.Add(stage.ScenePath)) {
                issues.Add($"{stage.StageID} reuses scene path '{stage.ScenePath}'");
            }
            if (stage.ScenePath == "res://scenes/arenas/TestArena.tscn") {
                issues.Add($"{stage.StageID} still aliases the Test Arena");
            }
            if (!ResourceLoader.Exists(stage.ScenePath)) {
                issues.Add($"{stage.StageID} scene '{stage.ScenePath}' does not exist");
                continue;
            }
            if (!stage.ProductionReady) {
                issues.Add($"{stage.StageID} is not marked ProductionReady");
            }
            if (string.IsNullOrWhiteSpace(stage.PreviewTexturePath) ||
                !ResourceLoader.Exists(stage.PreviewTexturePath)) {
                issues.Add($"{stage.StageID} has no importable preview " +
                           $"('{stage.PreviewTexturePath}')");
            }

            Node root = ResourceLoader.Load<PackedScene>(stage.ScenePath).Instantiate();
            try {
                var declared = root.Get("StageID").AsString();
                if (declared != stage.StageID) {
                    issues.Add($"{stage.ScenePath} declares StageID '{declared}', " +
                               $"catalog says '{stage.StageID}'");
                }
            } finally {
                root.Free();
            }
        }

        AssertThat(issues.Count).OverrideFailureMessage(
            "Stage routing problems: " + string.Join(" | ", issues)).IsEqual(0);
    }
}
