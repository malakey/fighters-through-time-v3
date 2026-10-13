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
    ///
    /// <para>Since the 2026-10-12 gameplay update the Story view is zoomed in
    /// (<see cref="ViewZoom"/>). The dead and soft zones stay view fractions, so
    /// they read the same on screen at any zoom; the look-ahead cap, the follow
    /// offset and the snap distance stay in world pixels.</para>
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

        // === Zoom and framing (GU-B, gameplay update 2026-10-12) ===============

        /// <summary>
        /// The reference canvas every Story camera renders to (project.godot and
        /// <c>ViewportEnforcer</c> pin the content scale to 1920 x 1080), so a
        /// view's world size is this over the zoom.
        /// </summary>
        public const float ReferenceViewportWidth = 1920f;
        public const float ReferenceViewportHeight = 1080f;

        /// <summary>
        /// The Story camera's zoom (Godot 4: above 1 is closer). The user asked
        /// for "a closer view on the player" (2026-10-12); 1.5 shows 1280 x 720
        /// world pixels (21.3 x 12 units) and puts the idle hero at about 14% of
        /// the screen's height, the Fighter camera's closest framing. Applied by
        /// <see cref="StoryCameraConfiner"/> itself, so every Story camera (hub,
        /// Level 0, Florence, Levels 2–15) has it and the Fighter camera, the
        /// Test Arena and the Holodeck drills cannot. Provisional.
        /// </summary>
        public const float ViewZoom = 1.5f;

        /// <summary>The world size of the Story view at <see cref="ViewZoom"/> (1280 x 720), for tests and content rules.</summary>
        public static readonly Vector2 ViewWorldSize =
            new(ReferenceViewportWidth / ViewZoom, ReferenceViewportHeight / ViewZoom);

        /// <summary>
        /// World pixels kept clear on each side of a framed area: a framing
        /// request fits the area's width plus twice this into the view, so a
        /// boss at its arena's edge is not drawn against the screen's edge.
        /// Provisional.
        /// </summary>
        public const float FramingMarginPixels = 120f;

        /// <summary>
        /// Time for the zoom to close 99% of a gap to its target (the same
        /// Cinemachine damper as the follow), so a framing request eases the view
        /// out and its release eases it back in. Provisional.
        /// </summary>
        public const float ZoomDampingSeconds = 0.6f;

        /// <summary>
        /// The widest a boss or Mirror arena framing may pull the view: 1.0, the
        /// pre-2026-10-12 Story view (1920 x 1080 world pixels). Provisional.
        /// </summary>
        public const float BossFramingMinZoom = 1.0f;

        /// <summary>
        /// The zoom that frames an area <paramref name="areaWidth"/> wide with
        /// <see cref="FramingMarginPixels"/> on each side:
        /// <c>clamp(1920 / (width + 2 x margin), minZoom, ViewZoom)</c>. A framing
        /// never zooms closer than <see cref="ViewZoom"/>, and a degenerate area
        /// asks for no change.
        /// </summary>
        public static float FramingZoom(float areaWidth, float minZoom) {
            if (areaWidth <= 0f) return ViewZoom;
            float floor = Mathf.Min(minZoom, ViewZoom);
            float fit = ReferenceViewportWidth / (areaWidth + 2f * FramingMarginPixels);
            return Mathf.Clamp(fit, floor, ViewZoom);
        }

        /// <summary>
        /// Where a framing pulls the view's centre: the centre that keeps the
        /// framed span (plus <see cref="FramingMarginPixels"/> a side) in view —
        /// the span's middle when the view is at least as wide, else the hero's
        /// point confined inside it — leashed to within
        /// <paramref name="heroLeash"/> of the hero's follow point, so a hero who
        /// strays from the area is never framed out.
        /// </summary>
        public static float FramedCentre(float followX, float spanStart, float spanEnd, float halfView, float heroLeash) {
            float framed = ConfineCenter(followX, spanStart - FramingMarginPixels, spanEnd + FramingMarginPixels, halfView);
            return Mathf.Clamp(framed, followX - heroLeash, followX + heroLeash);
        }

        /// <summary>
        /// The smallest zoom whose view still fits inside confinement bounds of
        /// this size (0 when the bounds are degenerate or unset). A framing never
        /// widens the view past the room it is confined to, which keeps the rig's
        /// centring and Godot's built-in limits in agreement.
        /// </summary>
        public static float BoundsFitZoom(float boundsWidth, float boundsHeight) {
            if (boundsWidth <= 0f || boundsHeight <= 0f) return 0f;
            return Mathf.Max(ReferenceViewportWidth / boundsWidth, ReferenceViewportHeight / boundsHeight);
        }

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
