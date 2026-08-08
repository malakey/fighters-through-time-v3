using System.Collections.Generic;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Exercises the shared marker↔geometry conformance validator and applies it to
/// its first consumer, the shipped Florence Workshop scene. Package 6 Phase B
/// per-stage suites call <see cref="FighterStageConformance.Validate"/> the same
/// way, so a bug here would silently pass nine drifted scenes: the negative cases
/// below prove the validator actually fails when a scene lies.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageConformanceTests {
    private const string FlorenceScenePath = "res://scenes/fighter/FighterStage_Florence.tscn";

    [TestCase]
    public void PixelConversionMatchesTheDriverContract() {
        // pixel = (950 + x * 62.5, 700 - y * 62.5)
        AssertThat(FighterStageConformance.ToPixels(FP64.Zero, FP64.Zero))
            .IsEqual(new Vector2(950f, 700f));
        AssertThat(FighterStageConformance.ToPixels(FP64.FromInt(-4), FP64.Zero))
            .IsEqual(new Vector2(700f, 700f));
        AssertThat(FighterStageConformance.ToPixels(FP64.FromInt(4), FP64.FromDouble(2.4)))
            .IsEqual(new Vector2(1200f, 550f));
        AssertThat(FighterStageConformance.UnitsToPixels(FP64.FromDouble(1.6))).IsEqual(100f);
    }

    [TestCase]
    public void FlorenceSceneMirrorsItsAuthoredFixedPointGeometry() {
        Node2D root = InstantiateFlorence();
        try {
            List<string> issues = FighterStageConformance.Validate(root, FighterStageGeometry.Florence);
            if (issues.Count > 0) {
                AssertThat("florence conformance: " + string.Join(" | ", issues)).IsEqual("");
            }
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// A scene that no longer mirrors its geometry must fail. Without this, an
    /// always-passing validator would wave nine Phase B stages through.
    /// </summary>
    [TestCase]
    public void ValidatorRejectsAStageWhoseMarkersDriftFromTheGeometry() {
        Node2D root = InstantiateFlorence();
        try {
            // A platform nudged 40 px up, one orb marker moved, one spawn shifted.
            root.GetNode<Node2D>("Geometry/GearPlatformLeft").Position += new Vector2(0f, -40f);
            root.GetNode<Node2D>("OrbSpawnPoints/Center").Position += new Vector2(25f, 0f);
            root.GetNode<Node2D>("Spawns/Player2").Position += new Vector2(60f, 0f);

            List<string> issues = FighterStageConformance.Validate(root, FighterStageGeometry.Florence);
            AssertThat(issues.Count >= 3).IsTrue();
        } finally {
            root.Free();
        }
    }

    /// <summary>Checking Florence against another stage's geometry must not pass.</summary>
    [TestCase]
    public void ValidatorRejectsAStageCheckedAgainstTheWrongGeometry() {
        Node2D root = InstantiateFlorence();
        try {
            List<string> issues = FighterStageConformance.Validate(root, FighterStageGeometry.Berlin);
            AssertThat(issues.Count > 0).IsTrue();
        } finally {
            root.Free();
        }
    }

    /// <summary>
    /// Scenes are streamed content and must not go through
    /// <c>FTT.Core.AuthoredResources</c>; the instantiated root is never added to
    /// the tree, so the stage controller's <c>_Ready</c> does not spawn fighters.
    /// </summary>
    private static Node2D InstantiateFlorence() {
        var packed = ResourceLoader.Load<PackedScene>(FlorenceScenePath);
        AssertObject(packed).IsNotNull();
        var root = packed.Instantiate<Node2D>();
        AssertObject(root).IsNotNull();
        return root;
    }
}
