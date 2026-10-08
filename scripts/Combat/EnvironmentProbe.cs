using Godot;
using FTT.Core;

namespace FTT.Combat {

    /// <summary>
    /// Playtest pass 2026-10-04 (P2): a single ray probe against solid level
    /// geometry, shared by anything that has to ask "is there a wall between
    /// here and there". The Siphon Snare's line-of-sight test
    /// (<c>SiphonTetherChannel.DefaultLineOfSight</c>) is the same shape with a
    /// wider mask and can move onto this.
    ///
    /// <para>Headless-safe in every direction: no tree, no world, no space state
    /// or a physics in/out flush (where the space is locked) all read as "clear",
    /// so a probe never throws inside a test that never built a physics world and
    /// never touches a locked space.</para>
    ///
    /// <para>A3 (2026-10-04): a caller that probes every frame (an enemy charge
    /// re-clipping its hitbox) passes its own <see cref="PhysicsRayQueryParameters2D"/>
    /// through the <c>query</c> overload, which rewrites its endpoints and mask in
    /// place instead of allocating a new query per ray. The result
    /// <see cref="Godot.Collections.Dictionary"/> is the engine's own return value
    /// and is disposed deterministically.</para>
    /// </summary>
    public static class EnvironmentProbe {

        /// <summary>
        /// How many bodies containing the origin <c>ignoreBodiesAtOrigin</c>
        /// collects; more than this many solid bodies stacked on one point is not
        /// level geometry.
        /// </summary>
        private const int MaxBodiesAtOrigin = 8;

        /// <summary>
        /// Distance from <paramref name="origin"/> to the first body on
        /// <paramref name="mask"/> along the segment to
        /// <paramref name="destination"/>, or -1 when the segment is clear (or the
        /// world cannot be asked). Allocates a fresh query; a per-frame caller
        /// should hold one and use the overload that takes it.
        ///
        /// <para><paramref name="ignoreBodiesAtOrigin"/> (2026-10-04 fix pass, M1)
        /// also skips every body that CONTAINS <paramref name="origin"/>. A ray
        /// already ignores the one convex shape it starts inside; this extends that
        /// to the whole body, so a start inside a multi-shape body (two adjacent
        /// rects, a decomposed <c>CollisionPolygon2D</c>) or a concave shape — which
        /// a ray never treats as "started inside" — is not stopped by the rest of
        /// that same body. One point query, only when asked.</para>
        /// </summary>
        public static float FirstHitDistance(Node2D from, Vector2 origin, Vector2 destination,
            uint mask = CollisionLayers.Environment, bool ignoreBodiesAtOrigin = false) =>
            FirstHitDistance(from, null, origin, destination, mask, ignoreBodiesAtOrigin);

        /// <summary>
        /// A query configured the way every probe asks (bodies only, no areas),
        /// for a caller to hold and pass to the reusing overload. RefCounted: the
        /// holder keeps it referenced and never disposes it.
        /// </summary>
        public static PhysicsRayQueryParameters2D CreateReusableQuery() {
            var query = new PhysicsRayQueryParameters2D {
                CollideWithAreas = false,
                CollideWithBodies = true
            };
            return query;
        }

        /// <summary>
        /// <see cref="FirstHitDistance(Node2D, Vector2, Vector2, uint, bool)"/>
        /// through a caller-owned <paramref name="query"/> from
        /// <see cref="CreateReusableQuery"/>: only its <c>From</c>, <c>To</c> and
        /// <c>CollisionMask</c> are rewritten (and, for
        /// <paramref name="ignoreBodiesAtOrigin"/>, its <c>Exclude</c> list for the
        /// duration of this call — it is emptied again before returning, so a
        /// reused query never carries a stale exclusion). A null query allocates
        /// one for this call.
        /// </summary>
        public static float FirstHitDistance(Node2D from, PhysicsRayQueryParameters2D query,
            Vector2 origin, Vector2 destination, uint mask = CollisionLayers.Environment,
            bool ignoreBodiesAtOrigin = false) {
            if (from == null || !GodotObject.IsInstanceValid(from) || !from.IsInsideTree()) return -1f;
            if (PhysicsCallbackGuard.IsInPhysicsCallback) return -1f;
            PhysicsDirectSpaceState2D space = from.GetWorld2D()?.DirectSpaceState;
            if (space == null) return -1f;
            query ??= CreateReusableQuery();
            query.From = origin;
            query.To = destination;
            query.CollisionMask = mask;
            // The exclusion lists are Godot arrays (disposed here, after the query
            // that copied them has run); the queries are RefCounted and are left to
            // reference counting (never Dispose a RefCounted).
            Godot.Collections.Array<Rid> startBodies = ignoreBodiesAtOrigin
                ? BodiesContaining(space, origin, mask)
                : null;
            using Godot.Collections.Array startBodiesLifetime = startBodies?.AsDisposable();
            bool excluded = startBodies != null && startBodies.Count > 0;
            if (excluded) query.Exclude = startBodies;
            float distance = -1f;
            // The result Dictionary is IDisposable.
            using (Godot.Collections.Dictionary result = space.IntersectRay(query)) {
                if (result != null && result.Count > 0 && result.ContainsKey("position")) {
                    distance = origin.DistanceTo(result["position"].AsVector2());
                }
            }
            if (excluded) {
                var cleared = new Godot.Collections.Array<Rid>();
                using Godot.Collections.Array clearedLifetime = cleared.AsDisposable();
                query.Exclude = cleared;
            }
            return distance;
        }

        /// <summary>
        /// True when nothing on <paramref name="mask"/> lies between the two points
        /// (see <see cref="FirstHitDistance(Node2D, Vector2, Vector2, uint, bool)"/>
        /// for <paramref name="ignoreBodiesAtOrigin"/>).
        /// </summary>
        public static bool IsClear(Node2D from, Vector2 origin, Vector2 destination,
            uint mask = CollisionLayers.Environment, bool ignoreBodiesAtOrigin = false) =>
            FirstHitDistance(from, origin, destination, mask, ignoreBodiesAtOrigin) < 0f;

        /// <summary>The RIDs of the bodies on <paramref name="mask"/> whose shapes contain <paramref name="point"/>.</summary>
        private static Godot.Collections.Array<Rid> BodiesContaining(
            PhysicsDirectSpaceState2D space, Vector2 point, uint mask) {
            var bodies = new Godot.Collections.Array<Rid>();
            var pointQuery = new PhysicsPointQueryParameters2D {
                Position = point,
                CollisionMask = mask,
                CollideWithAreas = false,
                CollideWithBodies = true
            };
            Godot.Collections.Array<Godot.Collections.Dictionary> hits =
                space.IntersectPoint(pointQuery, MaxBodiesAtOrigin);
            using Godot.Collections.Array hitsLifetime = hits.AsDisposable();
            foreach (Godot.Collections.Dictionary hit in hits) {
                using (hit) {
                    if (hit.ContainsKey("rid")) bodies.Add(hit["rid"].AsRid());
                }
            }
            return bodies;
        }
    }
}
