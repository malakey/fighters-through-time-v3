using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using LevelFact = FTT.Tests.ContentValidation.TraversalContentKit.LevelFact;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-PILOT (rulings 7a, 7b, 7c, 7c+; plan §1.14–§1.15): the rules every level's stretch
/// and optional route obey, read off the real scene. A level joins the sweep by adding
/// itself to <see cref="TraversalContentKit.LevelsWithTraversal"/>, and each case then fails
/// if that level authors no stretch or route. The per-hero half is
/// <c>OptionalRouteReachTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TraversalStretchContentTests {
    /// <summary>The reward stands over the route's last surface, within this band (a stand-in for the Extractor's height).</summary>
    private const float RewardStandBandPixels = 160f;

    [TestCase]
    public void EveryStretchStandsOutsideEveryLockWithItsOwnRoomAndCamera() {
        var issues = new List<string>();
        foreach (LevelFact level in Levels(issues)) {
            if (level.Stretches.Count == 0) issues.Add($"{level.Label}: no traversal stretch");
            var seenCameras = new HashSet<string>();
            foreach (TraversalContentKit.StretchFact stretch in level.Stretches) {
                string label = $"{level.Label}: '{stretch.ID}' {stretch.MinX:0}..{stretch.MaxX:0}";
                if (!stretch.ID.StartsWith($"level_{level.Index:00}.")) issues.Add($"{label} does not start with its level's prefix");
                foreach (Rect2 area in level.LockAreas) {
                    if (stretch.MinX < area.End.X && area.Position.X < stretch.MaxX) {
                        issues.Add($"{label} overlaps the lock {area.Position.X:0}..{area.End.X:0}");
                    }
                }
                if (!stretch.HasRoom) { issues.Add($"{label} has no room trigger"); continue; }
                Rect2 camera = stretch.CameraBounds;
                if (camera.Position.X > stretch.MinX || camera.End.X < stretch.MaxX) {
                    issues.Add($"{label}: its camera {camera} does not hold the stretch");
                }
                if (camera.Size.X < TraversalRules.MinCameraWidthPixels) issues.Add($"{label}: its camera {camera} is narrower than a view");
                if (!seenCameras.Add(stretch.ID)) issues.Add($"{label} repeats");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryStretchLineObeysTheHopRuleAndStandsOnRealGeometry() {
        var issues = new List<string>();
        foreach (LevelFact level in Levels(issues)) {
            foreach (TraversalContentKit.StretchFact stretch in level.Stretches) {
                string label = $"{level.Label}: '{stretch.ID}'";
                issues.AddRange(TraversalRules.ValidateLine(label, stretch.Line));
                if (stretch.Line.Count < 3) issues.Add($"{label} has no step between its decks");
                foreach (TraversalSurface surface in stretch.Line) {
                    if (!HasRealSurface(level, surface)) issues.Add($"{label}: nothing real stands at {surface}");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// VERIFY-STORY-PITS: nothing in a stretch kills. Under every column of the span there is
    /// floor no deeper than the trench rule allows, the ramps can be walked, and no lethal
    /// boundary reaches the span.
    /// </summary>
    [TestCase]
    public void EveryStretchIsFlooredWithWalkableRampsAndNoLethalBoundary() {
        var issues = new List<string>();
        foreach (LevelFact level in Levels(issues)) {
            foreach (TraversalContentKit.StretchFact stretch in level.Stretches) {
                string label = $"{level.Label}: '{stretch.ID}'";
                float deepest = stretch.DeckY + TraversalRules.MaxTrenchDepthPixels;
                for (float x = stretch.MinX; x <= stretch.MaxX; x += 20f) {
                    bool floored = level.Surfaces.Any(s => !s.OneWay && !s.Crumbling && s.Rect.Position.X <= x && s.Rect.End.X >= x
                        && s.Rect.Position.Y >= stretch.DeckY - 1f && s.Rect.Position.Y <= deepest + 1f);
                    if (!floored) { issues.Add($"{label} has no floor between {stretch.DeckY:0} and {deepest:0} at x {x:0}"); break; }
                }
                if (stretch.HasTrench) {
                    float depth = stretch.TrenchFloorY - stretch.DeckY;
                    if (!TraversalRules.TrenchIsAllowed(depth, stretch.RampRun)) {
                        issues.Add($"{label}: a trench {depth:0} deep with {stretch.RampRun:0} px ramps breaks the rule");
                    }
                }
                foreach (Rect2 boundary in level.KillBoundaries) {
                    if (boundary.Position.X < stretch.MaxX && stretch.MinX < boundary.End.X) issues.Add($"{label} holds a lethal boundary {boundary}");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>Rulings 7b and §1.14: no rope in a new stretch, and every route surface is real.</summary>
    [TestCase]
    public void NoStretchHoldsARopeAndEveryRouteSurfaceIsReal() {
        var issues = new List<string>();
        foreach (LevelFact level in Levels(issues)) {
            foreach (TraversalContentKit.StretchFact stretch in level.Stretches) {
                foreach (float rope in level.RopeXs) {
                    if (rope >= stretch.MinX && rope <= stretch.MaxX) issues.Add($"{level.Label}: '{stretch.ID}' holds a rope at x {rope:0}");
                }
            }
            foreach (TraversalContentKit.RouteFact route in level.Routes) {
                foreach (TraversalSurface surface in route.BaseLine) {
                    if (!HasRealSurface(level, surface)) issues.Add($"{level.Label}: '{route.ID}': nothing real stands at {surface}");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// F05 and 7c+: a route pays a source the manifest already has, that source really stands
    /// at the route's end, the ledger still sums to the level's pools on every difficulty, and
    /// the level offers both shortcut flavours (Level 1, with no movement ability yet, none).
    /// </summary>
    [TestCase]
    public void EveryRoutePaysAnExistingSourceAtItsEndAndOffersBothShortcuts() {
        var issues = new List<string>();
        foreach (LevelFact level in Levels(issues)) {
            if (level.Routes.Count != 1) issues.Add($"{level.Label}: {level.Routes.Count} optional routes, not 1");
            foreach (TraversalContentKit.RouteFact route in level.Routes) {
                string label = $"{level.Label}: '{route.ID}'";
                bool extractor = level.ManifestExtractors.Contains(route.RewardSourceID);
                if (!extractor && route.RewardSourceID != level.ManifestSecret) {
                    issues.Add($"{label} pays '{route.RewardSourceID}', which is not in the manifest");
                }
                bool placed = (extractor ? level.PlacedExtractors : level.PlacedSecrets)
                    .Any(p => p.ID == route.RewardSourceID && p.Position.DistanceTo(route.RewardPosition) < 1f);
                if (!placed) issues.Add($"{label}: '{route.RewardSourceID}' does not stand at {route.RewardPosition}");
                TraversalSurface last = route.BaseLine[^1];
                bool over = route.RewardPosition.X >= last.Left && route.RewardPosition.X <= last.Right
                    && route.RewardPosition.Y <= last.Top && route.RewardPosition.Y >= last.Top - RewardStandBandPixels;
                if (!over) issues.Add($"{label}: the reward at {route.RewardPosition} does not stand over the line's end {last}");
                if (!TraversalContentKit.LevelsWithoutShortcuts.Contains(level.Index)) {
                    if (!route.HasBurst) issues.Add($"{label} has no horizontal-burst shortcut");
                    if (!route.HasRise) issues.Add($"{label} has no rise-or-glide shortcut");
                }
            }
            LevelRewardManifest manifest = LevelRewardManifest.LoadFor(level.Index);
            foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
                LevelRewardLedger ledger = LevelRewardDirectory.Compile(manifest, difficulty);
                int paid = ledger.Awards.Values.Sum();
                int pools = manifest.RequiredEncounterPool + manifest.BossAward + manifest.OptionalPool;
                if (paid != pools) issues.Add($"{level.Label} on {difficulty}: the ledger pays {paid}, its pools hold {pools}");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    private static IEnumerable<LevelFact> Levels(List<string> issues) {
        List<LevelFact> levels = TraversalContentKit.Levels();
        AssertThat(levels.Count).IsEqual(TraversalContentKit.LevelsWithTraversal.Length);
        foreach (LevelFact level in levels) {
            if (level.LoadError != null) issues.Add($"{level.Label}: {level.LoadError}");
            else yield return level;
        }
    }

    private static bool HasRealSurface(LevelFact level, TraversalSurface surface) =>
        level.Surfaces.Any(real => Mathf.Abs(real.Rect.Position.Y - surface.Top) <= 1f
            && real.Rect.Position.X <= surface.Left + 1f && real.Rect.End.X >= surface.Right - 1f);
}
