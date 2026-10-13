using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using Godot;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// D13 (<c>PROVISIONAL-FONT-PLACEMENT</c>): a Restoration Font's channel must not
/// stand in a hazard's reach. Shared by <c>SecretCachePlacementTests</c> (Levels
/// 2–15) and <c>Level01ContentTests</c> (Florence).
///
/// <para>The channel runs while the hero's feet stay within
/// <see cref="RestorationFont.ChannelRangePixels"/> of the font, and damage
/// interrupts it. So the hero's body (the factory's 40 × 64 box, feet on the
/// origin) at any point of that circle, plus <see cref="Margin"/>, must miss the
/// shape of every damaging hazard: each <see cref="StoryCyclicHazard"/> (armed
/// <b>or dormant at load</b>, because a phase-armed lane such as Level 14's
/// Security Core grid fires during the fight and there is no boss-fight
/// refusal), each <see cref="ChronalRiftZone"/>, and each <c>Spikes_*</c> strip
/// (the base's and Florence's <c>BuildHazardSpikes</c>). Searchlights sweep and
/// escape fronts, floods and gravity pockets move or cover whole rooms, so those
/// stay authoring guidance.</para>
/// </summary>
public static class FontHazardClearance {
    /// <summary>The Story hero's body half-width (<c>CharacterFactory</c>: 40 × 64).</summary>
    public const float HeroHalfWidth = 20f;

    /// <summary>The Story hero's body height above the feet.</summary>
    public const float HeroHeight = 64f;

    /// <summary>Clearance kept beyond the channel circle.</summary>
    public const float Margin = 20f;

    /// <summary>One damaging hazard's shape, as a global axis-aligned box.</summary>
    public readonly record struct Hazard(string Label, Rect2 Shape);

    /// <summary>
    /// Adds <paramref name="node"/>'s shapes when it is a damaging hazard; returns
    /// false for anything else. Call it on every node of a walk.
    /// </summary>
    public static bool TryCollect(Node node, List<Hazard> hazards) {
        string label;
        switch (node) {
            case StoryCyclicHazard cyclic:
                label = $"cyclic hazard '{cyclic.HazardID}' ({cyclic.Name}{(cyclic.Enabled ? "" : ", dormant at load")})";
                break;
            case ChronalRiftZone rift:
                label = $"rift '{rift.Name}'";
                break;
            case Area2D spikes when spikes.Name.ToString().StartsWith("Spikes_", StringComparison.Ordinal):
                label = $"spikes '{spikes.Name}'";
                break;
            default:
                return false;
        }
        Godot.Collections.Array<Node> children = node.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is not CollisionShape2D { Shape: Shape2D shape } collision) continue;
            hazards.Add(new Hazard(label, GlobalBox(collision.GlobalTransform, shape.GetRect())));
        }
        return true;
    }

    /// <summary>Collects every damaging hazard under <paramref name="root"/>, the root included.</summary>
    public static List<Hazard> CollectUnder(Node root) {
        var hazards = new List<Hazard>();
        Walk(root, hazards);
        return hazards;
    }

    /// <summary>
    /// The distance from the font to the nearest feet position at which the
    /// hero's body would overlap <paramref name="hazard"/>.
    /// </summary>
    public static float Reach(Vector2 font, Hazard hazard) {
        Rect2 shape = hazard.Shape;
        // Feet positions whose 40 × 64 body overlaps the shape.
        float left = shape.Position.X - HeroHalfWidth;
        float right = shape.End.X + HeroHalfWidth;
        float top = shape.Position.Y;
        float bottom = shape.End.Y + HeroHeight;
        float dx = Math.Max(Math.Max(left - font.X, 0f), font.X - right);
        float dy = Math.Max(Math.Max(top - font.Y, 0f), font.Y - bottom);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Appends an issue for every hazard closer than the channel circle plus <see cref="Margin"/>.</summary>
    public static void Check(string label, Vector2 font, float channelRange, IEnumerable<Hazard> hazards,
        List<string> issues) {
        float required = channelRange + Margin;
        foreach (Hazard hazard in hazards) {
            float reach = Reach(font, hazard);
            if (reach < required) {
                issues.Add($"{label}: the hero channelling the font at {font} reaches the {hazard.Label} " +
                    $"(shape {hazard.Shape}) within {reach:0} px (needs {required:0})");
            }
        }
    }

    private static void Walk(Node node, List<Hazard> hazards) {
        TryCollect(node, hazards);
        Godot.Collections.Array<Node> children = node.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) Walk(child, hazards);
    }

    private static Rect2 GlobalBox(Transform2D transform, Rect2 local) {
        Vector2 a = transform * local.Position;
        Vector2 b = transform * new Vector2(local.End.X, local.Position.Y);
        Vector2 c = transform * new Vector2(local.Position.X, local.End.Y);
        Vector2 d = transform * local.End;
        float minX = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
        float minY = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
        float maxX = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
        float maxY = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }
}
