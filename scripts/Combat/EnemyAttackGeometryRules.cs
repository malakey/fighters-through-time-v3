using Godot;

namespace FTT.Combat {

    /// <summary>
    /// Playtest pass 2026-10-04 (P2 + P3): the one statement of how a Story enemy
    /// or boss attack meets the world — melee/charge hitboxes stop at walls, and
    /// shots aim where a moving target will be. Story-only: nothing in
    /// <c>scripts/FighterSim/</c> may consume it (the sim has its own fixed-point
    /// projectiles and no enemy roster).
    ///
    /// <para>Pure math on <see cref="Vector2"/>/<c>float</c> with no engine
    /// calls, so it can be pinned in isolation; the executor supplies the world
    /// samples (a ray distance, a target body's position and velocity).</para>
    /// </summary>
    public static class EnemyAttackGeometryRules {

        // === P2 — wall clipping ==============================================

        /// <summary>
        /// The clipped hitbox stops this far short of the wall face, so its edge
        /// never sits exactly on the far side of a 20 px wall through float noise.
        /// </summary>
        public const float WallClearancePixels = 2f;

        /// <summary>
        /// Rays cast per clip check, spread evenly over the hitbox's height: a
        /// wall that blocks only the top or only the bottom of a swing does not
        /// clip the swing (the open part still reaches), but one that blocks every
        /// sampled height does. Three covers the centre and both quarter lines.
        /// </summary>
        public const int WallProbeRays = 3;

        /// <summary>
        /// Clips a forward hitbox span against the nearest wall. The span is
        /// measured along the facing direction from the owner's centre:
        /// <paramref name="near"/> and <paramref name="far"/> are the hitbox's two
        /// edges (near may be negative when the box straddles the owner).
        /// <paramref name="wallDistance"/> is the distance to the wall face along
        /// the same axis, or a negative value when nothing was hit.
        /// Returns false when the whole forward reach is behind the wall — the swing
        /// lands nothing — else the (possibly shortened) span.
        /// </summary>
        public static bool ClipForwardSpan(float near, float far, float wallDistance,
            out float clippedNear, out float clippedFar) {
            clippedNear = near;
            clippedFar = far;
            if (wallDistance < 0f || far <= wallDistance) return true;
            float limit = wallDistance - WallClearancePixels;
            // Everything in front of the owner is past the wall: no forward reach.
            if (limit <= Mathf.Max(near, 0f)) return false;
            clippedFar = limit;
            return true;
        }

        // === P3 — projectile lead ============================================

        /// <summary>
        /// Shots aim at the body, not the feet: the Story player's origin is its
        /// feet and its body is 64 px tall, so the aim point is lifted half a body.
        /// Aiming at the feet sent every flat shot into the floor in front of a
        /// player standing on a step.
        /// </summary>
        public const float BodyAimHeightPixels = 32f;

        /// <summary>
        /// The longest flight the lead extrapolates over. Beyond it a shot aims at
        /// where the target would be after this long — a running player still has
        /// to change something to dodge a slow, distant volley.
        /// </summary>
        public const float MaxLeadSeconds = 0.75f;

        /// <summary>
        /// At fire time the shot re-acquires the living player nearest the point
        /// the telegraph promised, within this radius; farther than that the
        /// telegraphed point stands (nobody is there to lead).
        /// </summary>
        public const float ResampleRadiusPixels = 480f;

        /// <summary>
        /// First-order intercept, iterated twice: flight time from the origin to
        /// the aim point, then to the predicted point, each clamped to
        /// <see cref="MaxLeadSeconds"/>. Horizontal only — a jumping target's
        /// vertical velocity is mostly gravity's to undo, so extrapolating it
        /// linearly aims shots into the sky. Deterministic: no randomness.
        /// </summary>
        public static Vector2 LeadPoint(Vector2 origin, Vector2 aim, Vector2 targetVelocity, float projectileSpeed) {
            if (projectileSpeed <= 0f || Mathf.IsZeroApprox(targetVelocity.X)) return aim;
            Vector2 predicted = aim;
            for (int iteration = 0; iteration < 2; iteration++) {
                float flight = Mathf.Min(MaxLeadSeconds, origin.DistanceTo(predicted) / projectileSpeed);
                predicted = new Vector2(aim.X + targetVelocity.X * flight, aim.Y);
            }
            return predicted;
        }
    }
}
