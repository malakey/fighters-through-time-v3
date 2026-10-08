using Godot;

namespace FTT.Environment {

    /// <summary>
    /// F8 (playtest pass 2026-10-04): the Story Mode follow camera, from
    /// design-godot.md "Story Mode Camera2D Configuration" — the single home of
    /// its numbers. Zones are fractions of the visible view (Cinemachine
    /// semantics: a 0.1 dead-zone width is 10% of the view wide, centred);
    /// damping is the time to close 99% of a gap (Cinemachine's damper).
    ///
    /// <para>Pure math, no engine calls; <see cref="StoryCameraConfiner"/> feeds
    /// it the view size, the follow point and the frame delta. Story-only — the
    /// Fighter camera is its own midpoint/zoom script.</para>
    /// </summary>
    public static class StoryCameraRules {
        // Units convert at the one canonical Story scale,
        // FTT.Combat.KitMotionRules.StoryPixelsPerUnit — never a local copy.

        /// <summary>Design "Follow Offset (0, 1)": one unit above the hero's feet (y-down, so negative).</summary>
        public static readonly Vector2 FollowOffsetPixels =
            new(0f, -1f * FTT.Combat.KitMotionRules.StoryPixelsPerUnit);

        public const float DeadZoneWidth = 0.1f;
        public const float DeadZoneHeight = 0.15f;
        public const float SoftZoneWidth = 0.6f;
        public const float SoftZoneHeight = 0.5f;
        public const float DampingX = 0.5f;
        public const float DampingY = 0.3f;

        /// <summary>Design "Lookahead Time 0.3 s": the camera leads by where the hero will be in 0.3 s.</summary>
        public const float LookAheadSeconds = 0.3f;

        /// <summary>
        /// Cap on the lead, so a launch or a dash cannot fling the view: three
        /// units, a little above a full run's 0.3 s travel (~2.4 units). Provisional.
        /// </summary>
        public const float MaxLookAheadPixels = 3f * FTT.Combat.KitMotionRules.StoryPixelsPerUnit;

        /// <summary>
        /// A follow point that jumps further than this in one step is a teleport
        /// (checkpoint restore, a scene's scripted relocation): the camera cuts
        /// instead of panning across the level. Smaller jumps (Echo Step, a warp)
        /// are followed with the ordinary damping.
        /// </summary>
        public const float SnapDistancePixels = 900f;

        /// <summary>Cinemachine's residual: after <c>damping</c> seconds 1% of the gap remains.</summary>
        private const float LogNegligibleResidual = 4.60517019f; // -ln(0.01)

        /// <summary>The fraction of a gap closed this frame for a damping time (1 = snap).</summary>
        public static float DampFactor(float damping, float delta) {
            if (damping <= 0f || delta <= 0f) return delta <= 0f ? 0f : 1f;
            return 1f - Mathf.Exp(-LogNegligibleResidual * delta / damping);
        }

        /// <summary>
        /// One axis of the follow: inside the dead zone the camera holds; outside
        /// it, it closes on the dead-zone edge with damping; and the follow point
        /// is never allowed past the soft zone's edge (a hard catch-up there).
        /// </summary>
        public static float FollowAxis(float center, float follow, float deadHalf, float softHalf,
            float damping, float delta) {
            float offset = follow - center;
            float desired = Mathf.Abs(offset) <= deadHalf ? center : follow - Mathf.Sign(offset) * deadHalf;
            float next = center + (desired - center) * DampFactor(damping, delta);
            if (softHalf > 0f) next = Mathf.Clamp(next, follow - softHalf, follow + softHalf);
            return next;
        }

        /// <summary>The lead target for a horizontal speed, capped.</summary>
        public static float LookAheadTarget(float velocityX) =>
            Mathf.Clamp(velocityX * LookAheadSeconds, -MaxLookAheadPixels, MaxLookAheadPixels);

        /// <summary>
        /// Clamps a view centre so a view of <paramref name="halfExtent"/> stays
        /// inside [<paramref name="min"/>, <paramref name="max"/>]; a span narrower
        /// than the view centres on it.
        /// </summary>
        public static float ConfineCenter(float center, float min, float max, float halfExtent) {
            if (max - min <= 2f * halfExtent) return (min + max) / 2f;
            return Mathf.Clamp(center, min + halfExtent, max - halfExtent);
        }
    }
}
