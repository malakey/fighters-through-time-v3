using System;
using System.Collections.Generic;

namespace FTT.Environment {

    /// <summary>
    /// One standable surface of a traversal stretch or an optional route, in level
    /// pixels. Plain data: <see cref="Top"/> is the y a hero's feet stand at.
    /// </summary>
    public readonly record struct TraversalSurface(float Left, float Right, float Top) {
        public override string ToString() => $"[{Left:0}..{Right:0} top {Top:0}]";
    }

    /// <summary>
    /// The traversal rulebook (rulings 7a, 7b, 7c, 7c+; every number provisional,
    /// <c>PROVISIONAL-TRAVERSAL-RULES</c>). Pure: no Godot types, so a pure suite and
    /// the level helpers read one statement of each number. A stretch is built with
    /// <c>StoryLevelControllerBase</c>'s stretch helpers; the guide is
    /// <c>docs/handoffs/GR_PILOT.md</c>.
    /// </summary>
    public static class TraversalRules {
        /// <summary>The widest edge-to-edge gap of one hop on a stretch's line (§1.14).</summary>
        public const float MaxHopGapPixels = 280f;
        /// <summary>The highest rise of one hop on a stretch's line (§1.14).</summary>
        public const float MaxHopRisePixels = 240f;

        /// <summary>A trench floor lies at least this far under the deck.</summary>
        public const float MinTrenchDepthPixels = 100f;
        /// <summary>A trench floor lies at most this far under the deck.</summary>
        public const float MaxTrenchDepthPixels = 150f;
        /// <summary>
        /// The steepest walkable ramp, rise over run: 40.6°, the Tribunal pit's slope,
        /// under the 45° floor limit.
        /// </summary>
        public const float MaxRampRisePerRun = 0.857f;

        /// <summary>A stretch's camera rect is at least one visible view wide (the 1.5 zoom's 1280 px).</summary>
        public const float MinCameraWidthPixels = EncounterLockRules.ViewWorldWidth;

        /// <summary>What a stretch adds to par before the measured pass (§1.14: minimum +30 per stretch).</summary>
        public const float ParSecondsPerStretch = 30f;
        /// <summary>Par stays a multiple of this, as the seeded pars are.</summary>
        public const float ParGranularitySeconds = 30f;

        /// <summary>The par an author applies: the seeded par plus +30 a stretch, rounded up to 30 s.</summary>
        public static float ParWithStretches(float seededParSeconds, int stretches) {
            float raw = seededParSeconds + ParSecondsPerStretch * Math.Max(0, stretches);
            return MathF.Ceiling(raw / ParGranularitySeconds) * ParGranularitySeconds;
        }

        /// <summary>The edge-to-edge horizontal gap between two surfaces; 0 when their spans overlap.</summary>
        public static float HopGap(TraversalSurface from, TraversalSurface to) =>
            Math.Max(0f, Math.Max(to.Left - from.Right, from.Left - to.Right));

        /// <summary>How far up a hop goes (y is down); negative for a drop.</summary>
        public static float HopRise(TraversalSurface from, TraversalSurface to) => from.Top - to.Top;

        /// <summary>True when a ramp of this run and rise can be walked.</summary>
        public static bool RampIsWalkable(float run, float rise) =>
            run > 0f && Math.Abs(rise) / run <= MaxRampRisePerRun;

        /// <summary>True when a trench of this depth with ramps of this run is allowed.</summary>
        public static bool TrenchIsAllowed(float depth, float rampRun) =>
            depth >= MinTrenchDepthPixels && depth <= MaxTrenchDepthPixels && RampIsWalkable(rampRun, depth);

        /// <summary>Every hop of a line that breaks the gap or rise rule; empty when the line holds.</summary>
        public static List<string> ValidateLine(string label, IReadOnlyList<TraversalSurface> line) {
            var issues = new List<string>();
            if (line == null || line.Count < 2) {
                issues.Add($"{label}: a line needs at least its two ends");
                return issues;
            }
            for (int index = 1; index < line.Count; index++) {
                float gap = HopGap(line[index - 1], line[index]);
                float rise = HopRise(line[index - 1], line[index]);
                if (gap > MaxHopGapPixels) issues.Add($"{label}: {line[index - 1]} to {line[index]} is a {gap:0} px gap, over {MaxHopGapPixels:0}");
                if (rise > MaxHopRisePixels) issues.Add($"{label}: {line[index - 1]} to {line[index]} rises {rise:0} px, over {MaxHopRisePixels:0}");
            }
            return issues;
        }
    }
}
