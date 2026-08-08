using System;
using System.Collections.Generic;
using FTT.FighterSim;
using Godot;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Reusable marker↔geometry conformance validator for production Fighter stage
/// scenes (Package 6 plan §2.10).
///
/// <para>The deterministic simulation owns every boundary, platform, spawn, orb
/// point and hazard anchor in fixed-point world units; the authored scene is a
/// pixel mirror of it through <c>pixel = (950 + x·62.5, 700 − y·62.5)</c>. Nothing
/// in the engine enforces that mirror — a scene whose platform is drawn 30 px too
/// high still plays, it just lies to the player about where the floor is. This
/// validator is the enforcement, and every per-stage test calls it.</para>
///
/// <para>Conventions the validator expects, matching
/// <c>scenes/fighter/FighterStage_Florence.tscn</c>:</para>
/// <list type="bullet">
/// <item><c>Spawns/Player1</c> and <c>Spawns/Player2</c> sit at ∓SpawnDistance on the floor.</item>
/// <item><c>OrbSpawnPoints</c> and <c>HazardAnchors</c> hold one <see cref="Marker2D"/> per authored anchor.</item>
/// <item>One-way platform bodies live under <c>Geometry</c> on collision layer 128;
/// the body's own origin is the platform surface point (CenterX, SurfaceY) and its
/// <see cref="RectangleShape2D"/> width is twice the authored half-width.</item>
/// <item><c>Geometry/Ground</c> is on layer 64; the top edge of its collision rect is y = 0
/// and its half-width matches the walls.</item>
/// <item><c>Geometry/WallLeft</c> and <c>Geometry/WallRight</c> sit on the authored wall planes.</item>
/// </list>
/// </summary>
public static class FighterStageConformance {
    public const float PixelsPerUnit = 62.5f;
    public static readonly Vector2 WorldOrigin = new(950f, 700f);

    /// <summary>Tolerance in pixels. One pixel is finer than any authoring rounding.</summary>
    public const float EpsilonPixels = 1.0f;

    public const uint SolidCollisionLayer = 64;
    public const uint OneWayPlatformLayer = 128;

    public static Vector2 ToPixels(FP64 x, FP64 y) => new(
        WorldOrigin.X + ToFloat(x) * PixelsPerUnit,
        WorldOrigin.Y - ToFloat(y) * PixelsPerUnit);

    public static float UnitsToPixels(FP64 units) => ToFloat(units) * PixelsPerUnit;

    /// <summary>
    /// Returns one human-readable message per mismatch. An empty list means the
    /// scene mirrors the geometry exactly.
    /// </summary>
    public static List<string> Validate(Node2D stageRoot, FighterStageGeometry geometry) {
        var issues = new List<string>();
        if (stageRoot == null) { issues.Add("stage root is null"); return issues; }
        if (geometry == null) { issues.Add("geometry is null"); return issues; }

        ValidateSpawns(stageRoot, geometry, issues);
        ValidateMarkerSet(
            stageRoot, "OrbSpawnPoints", issues, OrbAnchorPixels(geometry), "orb anchor");
        ValidateMarkerSet(
            stageRoot, "HazardAnchors", issues, HazardAnchorPixels(geometry), "hazard anchor", compareYAxis: false);
        ValidatePlatforms(stageRoot, geometry, issues);
        ValidateGroundAndWalls(stageRoot, geometry, issues);
        return issues;
    }

    private static void ValidateSpawns(Node2D root, FighterStageGeometry geometry, List<string> issues) {
        FP64 distance = FP64.FromInt(geometry.SpawnDistance);
        CheckMarker(root, "Spawns/Player1", ToPixels(FP64.Zero - distance, FP64.Zero), issues);
        CheckMarker(root, "Spawns/Player2", ToPixels(distance, FP64.Zero), issues);
    }

    private static void CheckMarker(Node2D root, string path, Vector2 expected, List<string> issues) {
        var marker = root.GetNodeOrNull<Marker2D>(path);
        if (marker == null) { issues.Add($"missing marker '{path}'"); return; }
        Vector2 actual = LocalToRoot(marker, root);
        if (actual.DistanceTo(expected) > EpsilonPixels) {
            issues.Add($"'{path}' is at {actual} but geometry requires {expected}");
        }
    }

    private static void ValidateMarkerSet(
        Node2D root,
        string parentPath,
        List<string> issues,
        List<Vector2> expected,
        string label,
        bool compareYAxis = true) {
        var parent = root.GetNodeOrNull<Node2D>(parentPath);
        if (parent == null) { issues.Add($"missing '{parentPath}' node"); return; }

        var actual = new List<Vector2>();
        foreach (Node child in parent.GetChildren()) {
            if (child is Marker2D marker) actual.Add(LocalToRoot(marker, root));
        }
        if (actual.Count != expected.Count) {
            issues.Add($"'{parentPath}' has {actual.Count} markers but geometry authors {expected.Count} {label}s");
            return;
        }

        var unmatched = new List<Vector2>(expected);
        foreach (Vector2 point in actual) {
            int match = -1;
            for (int index = 0; index < unmatched.Count; index++) {
                float distance = compareYAxis
                    ? point.DistanceTo(unmatched[index])
                    : Mathf.Abs(point.X - unmatched[index].X);
                if (distance <= EpsilonPixels) { match = index; break; }
            }
            if (match < 0) issues.Add($"{label} marker at {point} matches no authored {label}");
            else unmatched.RemoveAt(match);
        }
    }

    private static void ValidatePlatforms(Node2D root, FighterStageGeometry geometry, List<string> issues) {
        var geometryNode = root.GetNodeOrNull<Node2D>("Geometry");
        if (geometryNode == null) { issues.Add("missing 'Geometry' node"); return; }

        var bodies = new List<StaticBody2D>();
        CollectBodies(geometryNode, OneWayPlatformLayer, bodies);
        if (bodies.Count != geometry.Platforms.Length) {
            issues.Add(
                $"scene has {bodies.Count} one-way platform bodies but geometry authors {geometry.Platforms.Length}");
            return;
        }

        var unmatched = new List<FighterStagePlatform>(geometry.Platforms);
        foreach (StaticBody2D body in bodies) {
            Vector2 origin = LocalToRoot(body, root);
            float halfWidthPixels = HalfWidthPixels(body);
            int match = -1;
            for (int index = 0; index < unmatched.Count; index++) {
                Vector2 expected = ToPixels(unmatched[index].CenterX, unmatched[index].SurfaceY);
                float expectedHalfWidth = UnitsToPixels(unmatched[index].HalfWidth);
                if (origin.DistanceTo(expected) <= EpsilonPixels
                    && Mathf.Abs(halfWidthPixels - expectedHalfWidth) <= EpsilonPixels) {
                    match = index;
                    break;
                }
            }
            if (match < 0) {
                issues.Add(
                    $"one-way platform '{body.Name}' at {origin} (half-width {halfWidthPixels} px) " +
                    "matches no authored platform");
            } else {
                unmatched.RemoveAt(match);
            }
            if (!HasOneWayCollision(body)) {
                issues.Add($"platform '{body.Name}' is not marked one_way_collision");
            }
            if (body.CollisionMask != 0) {
                issues.Add($"platform '{body.Name}' has a non-zero collision mask; the simulation is authoritative");
            }
        }
    }

    private static void ValidateGroundAndWalls(Node2D root, FighterStageGeometry geometry, List<string> issues) {
        var ground = root.GetNodeOrNull<StaticBody2D>("Geometry/Ground");
        if (ground == null) {
            issues.Add("missing 'Geometry/Ground' StaticBody2D");
        } else {
            if (ground.CollisionLayer != SolidCollisionLayer) {
                issues.Add($"ground is on collision layer {ground.CollisionLayer}, expected {SolidCollisionLayer}");
            }
            CollisionShape2D shape = FirstShape(ground);
            RectangleShape2D rect = shape?.Shape as RectangleShape2D;
            if (rect == null) {
                issues.Add("ground has no RectangleShape2D collider");
            } else {
                float topEdge = LocalToRoot(shape, root).Y - rect.Size.Y / 2f;
                float expectedTop = ToPixels(FP64.Zero, FP64.Zero).Y;
                if (Mathf.Abs(topEdge - expectedTop) > EpsilonPixels) {
                    issues.Add($"ground surface is at y {topEdge} px but the floor plane is {expectedTop} px");
                }
                float expectedWidth = UnitsToPixels(geometry.RightWall - geometry.LeftWall);
                if (Mathf.Abs(rect.Size.X - expectedWidth) > EpsilonPixels) {
                    issues.Add($"ground is {rect.Size.X} px wide but the walls are {expectedWidth} px apart");
                }
            }
        }

        CheckWall(root, "Geometry/WallLeft", geometry.LeftWall, issues);
        CheckWall(root, "Geometry/WallRight", geometry.RightWall, issues);
    }

    private static void CheckWall(Node2D root, string path, FP64 wallX, List<string> issues) {
        var wall = root.GetNodeOrNull<StaticBody2D>(path);
        if (wall == null) { issues.Add($"missing '{path}' StaticBody2D"); return; }
        if (wall.CollisionLayer != SolidCollisionLayer) {
            issues.Add($"'{path}' is on collision layer {wall.CollisionLayer}, expected {SolidCollisionLayer}");
        }
        float expectedX = ToPixels(wallX, FP64.Zero).X;
        float actualX = LocalToRoot(wall, root).X;
        if (Mathf.Abs(actualX - expectedX) > EpsilonPixels) {
            issues.Add($"'{path}' is at x {actualX} px but the wall plane is {expectedX} px");
        }
    }

    private static void CollectBodies(Node node, uint layer, List<StaticBody2D> found) {
        foreach (Node child in node.GetChildren()) {
            if (child is StaticBody2D body && body.CollisionLayer == layer) found.Add(body);
            CollectBodies(child, layer, found);
        }
    }

    private static CollisionShape2D FirstShape(Node body) {
        foreach (Node child in body.GetChildren()) {
            if (child is CollisionShape2D shape) return shape;
        }
        return null;
    }

    private static bool HasOneWayCollision(Node body) {
        CollisionShape2D shape = FirstShape(body);
        return shape != null && shape.OneWayCollision;
    }

    private static float HalfWidthPixels(Node body) {
        CollisionShape2D shape = FirstShape(body);
        return shape?.Shape is RectangleShape2D rect ? rect.Size.X / 2f : float.NaN;
    }

    private static List<Vector2> OrbAnchorPixels(FighterStageGeometry geometry) {
        var points = new List<Vector2>(geometry.OrbAnchors.Length);
        foreach (FPVector2 anchor in geometry.OrbAnchors) points.Add(ToPixels(anchor.x, anchor.y));
        return points;
    }

    private static List<Vector2> HazardAnchorPixels(FighterStageGeometry geometry) {
        var points = new List<Vector2>(geometry.HazardAnchorXs.Length);
        foreach (FP64 anchorX in geometry.HazardAnchorXs) points.Add(ToPixels(anchorX, FP64.Zero));
        return points;
    }

    /// <summary>
    /// Accumulates a node's transform up to (and including) the stage root without
    /// requiring the scene to be inside the tree, so a per-stage test can validate
    /// a freshly instantiated <see cref="PackedScene"/> without running its
    /// controller's <c>_Ready</c>.
    /// </summary>
    private static Vector2 LocalToRoot(Node2D node, Node2D root) {
        Transform2D accumulated = node.Transform;
        Node current = node.GetParent();
        while (current != null && current != root) {
            if (current is Node2D node2D) accumulated = node2D.Transform * accumulated;
            current = current.GetParent();
        }
        if (current == root) accumulated = root.Transform * accumulated;
        return accumulated.Origin;
    }

    private static float ToFloat(FP64 value) =>
        (float)((double)value.RawValue / FP64.One.RawValue);

    /// <summary>Convenience for tests: throws with every mismatch listed.</summary>
    public static void AssertConformant(Node2D stageRoot, FighterStageGeometry geometry) {
        List<string> issues = Validate(stageRoot, geometry);
        if (issues.Count > 0) {
            throw new InvalidOperationException(
                $"{geometry.StageID} scene does not mirror its geometry:\n - " + string.Join("\n - ", issues));
        }
    }
}
