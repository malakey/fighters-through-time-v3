using System.Collections.Generic;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 11 A9 — the negative case for the floor-segment half of
/// <see cref="FighterStageConformance"/>.
///
/// <para>The V7 "Floor Segments, Pits &amp; Ledges" decision only means something if
/// the authored scene agrees with the simulation about where the holes are. A scene
/// that colliders solid floor across an authored pit still plays — it just lies to
/// the player, who sees ground under their feet and falls through it. The shipped
/// per-stage suites only ever call the validator on scenes that are expected to
/// pass, so without this the segment check could be silently vacuous.</para>
///
/// <para>One <c>[TestSuite]</c> per file (plan §2.12): a second suite sharing a file
/// with a <c>[RequireGodotRuntime]</c> suite is discovered and then silently never
/// run.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageConformanceNegativeTests {
    private const string ParisScenePath = "res://scenes/fighter/FighterStage_Paris.tscn";
    private const string FlorenceScenePath = "res://scenes/fighter/FighterStage_Florence.tscn";

    /// <summary>
    /// Three lies the segment validator has to catch, against two shipped scenes
    /// that must pass untouched first: a floor body widened across Paris's authored
    /// courtyard pit, one of Paris's two segments deleted outright, and — the
    /// Sealed path, which must keep behaving exactly as it did before segments
    /// existed — Florence's single wall-to-wall floor cut in half.
    /// </summary>
    [TestCase]
    public void TheValidatorRejectsAFloorThatDisagreesWithTheAuthoredSegments() {
        // --- Paris passes as shipped, then fails with a floor across the pit ---
        Node2D paris = Instantiate(ParisScenePath);
        try {
            List<string> clean = FighterStageConformance.Validate(paris, FighterStageGeometry.Paris);
            if (clean.Count > 0) {
                AssertThat("paris baseline conformance: " + string.Join(" | ", clean)).IsEqual("");
            }

            // A fresh shape, because both segments share one authored sub-resource.
            var shape = paris.GetNode<CollisionShape2D>("Geometry/GroundLeft/CollisionShape2D");
            var rect = shape.Shape as RectangleShape2D;
            AssertObject(rect).IsNotNull();
            shape.Shape = new RectangleShape2D {
                Size = new Vector2(
                    FighterStageConformance.UnitsToPixels(
                        FighterStageGeometry.Paris.RightWall - FighterStageGeometry.Paris.LeftWall),
                    rect.Size.Y)
            };

            List<string> spanning = FighterStageConformance.Validate(paris, FighterStageGeometry.Paris);
            AssertThat(spanning.Count > 0)
                .OverrideFailureMessage(
                    "A floor body stretched across the authored courtyard pit must fail conformance.")
                .IsTrue();
        } finally {
            paris.Free();
        }

        // --- A missing segment is the same lie from the other direction ---
        Node2D truncated = Instantiate(ParisScenePath);
        try {
            var geometryNode = truncated.GetNode<Node>("Geometry");
            Node removed = geometryNode.GetNode<Node>("GroundRight");
            geometryNode.RemoveChild(removed);
            removed.Free();

            List<string> issues = FighterStageConformance.Validate(truncated, FighterStageGeometry.Paris);
            AssertThat(issues.Count > 0)
                .OverrideFailureMessage("A scene missing one of its authored floor segments must fail.")
                .IsTrue();
        } finally {
            truncated.Free();
        }

        // --- The Sealed path is unchanged: one floor, wall to wall ---
        Node2D florence = Instantiate(FlorenceScenePath);
        try {
            List<string> clean = FighterStageConformance.Validate(florence, FighterStageGeometry.Florence);
            if (clean.Count > 0) {
                AssertThat("florence baseline conformance: " + string.Join(" | ", clean)).IsEqual("");
            }

            var shape = florence.GetNode<CollisionShape2D>("Geometry/Ground/CollisionShape2D");
            var rect = shape.Shape as RectangleShape2D;
            AssertObject(rect).IsNotNull();
            shape.Shape = new RectangleShape2D { Size = new Vector2(rect.Size.X / 2f, rect.Size.Y) };

            List<string> shrunk = FighterStageConformance.Validate(florence, FighterStageGeometry.Florence);
            AssertThat(shrunk.Count > 0)
                .OverrideFailureMessage("A Sealed stage whose floor stops short of the walls must fail.")
                .IsTrue();
        } finally {
            florence.Free();
        }
    }

    /// <summary>
    /// Scenes are streamed content and must never go through
    /// <c>FTT.Core.AuthoredResources</c>; the instantiated root is never added to
    /// the tree, so the stage controller's <c>_Ready</c> does not spawn fighters.
    /// </summary>
    private static Node2D Instantiate(string scenePath) {
        var packed = ResourceLoader.Load<PackedScene>(scenePath);
        AssertObject(packed).IsNotNull();
        var root = packed.Instantiate<Node2D>();
        AssertObject(root).IsNotNull();
        return root;
    }
}
