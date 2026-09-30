using System;
using System.Collections.Generic;
using Godot;
using FTT.Core;

namespace FTT.Combat {

    /// <summary>
    /// Package 13 W7b — the Story half of the <b>ground wave</b> primitive
    /// (Joan's Righteous Smite J02, Lincoln's Emancipator LN03, Mozart's
    /// Fortissimo Wave M01). The sim half is <c>FTT.FighterSim.FighterGroundWave</c>.
    ///
    /// <para>A wave is a front that travels along the floor under its caster:
    /// it snaps to the surface below the cast point, advances at the authored
    /// speed for the authored distance (<c>ProjectileSpeed</c> ×
    /// <c>ProjectileLifetime</c>), and dissipates at a wall or where its surface
    /// ends. It strikes every target once, through the shared
    /// <see cref="Hurtbox"/> contract (so enemies, bosses, fighters and prop
    /// strike surfaces all answer it), and a <see cref="GroundedOnly"/> wave
    /// passes under anything not standing on the ground — a jump clears it.</para>
    ///
    /// <para><b>Not a projectile.</b> It is never a pooled
    /// <c>PlaceholderProjectile</c>, never in the <c>story_projectile</c> /
    /// <c>enemy_projectile</c> groups and never carries the <c>"projectile"</c>
    /// hitbox ID, so projectile immunity, Rail Breaker and the blink's
    /// pass-through do not apply to it.</para>
    ///
    /// <para>Plain C#, owned and ticked by the casting ability node (which lives
    /// under the hero for the fighter's whole life), exactly as the Emancipator's
    /// wave was before it became shared — no instantiate/free churn to pool.</para>
    /// </summary>
    public sealed class StoryGroundWave {
        /// <summary>How far below the cast point the wave looks for a floor.</summary>
        public const float MaxSnapDropPixels = 480f;
        /// <summary>How far below the surface line the continuation probe reaches.</summary>
        private const float ContinuationProbePixels = 12f;
        private const int MaxQueryResults = 16;

        public bool Active { get; private set; }
        public Vector2 Front { get; private set; }
        public bool MovingRight { get; private set; }
        public float Speed { get; private set; }
        public float DistanceRemaining { get; private set; }
        public Vector2 Size { get; private set; }
        public bool GroundedOnly { get; private set; }
        /// <summary>True when the start found a floor; only then does a missing surface end the wave.</summary>
        public bool SurfaceKnown { get; private set; }
        public float SurfaceY { get; private set; }

        /// <summary>
        /// Decides whether a hurtbox's owner stands on the ground. Defaults to
        /// <see cref="IsGroundedTarget"/>; a headless test with no floor may
        /// replace it.
        /// </summary>
        public Func<Hurtbox, bool> GroundedProbe { get; set; } = IsGroundedTarget;

        private readonly HashSet<ulong> _struck = new();

        /// <summary>
        /// Starts a wave whose front leaves <paramref name="origin"/> (a point at
        /// the caster's feet). The surface is found by a downward ray against
        /// Environment and one-way platforms; with none within
        /// <see cref="MaxSnapDropPixels"/> the wave rides the caster's feet line.
        /// </summary>
        public void Start(
            PhysicsDirectSpaceState2D space, Vector2 origin, bool movingRight,
            float speed, float distance, Vector2 size, bool groundedOnly) {
            MovingRight = movingRight;
            Speed = Mathf.Max(1f, speed);
            DistanceRemaining = Mathf.Max(0f, distance);
            Size = size == Vector2.Zero ? new Vector2(40f, 30f) : size;
            GroundedOnly = groundedOnly;
            SurfaceKnown = false;
            SurfaceY = origin.Y;
            if (space != null && TryRayDown(space, origin + new Vector2(0f, -4f), MaxSnapDropPixels, out float floorY)) {
                SurfaceKnown = true;
                SurfaceY = floorY;
            }
            Front = new Vector2(origin.X, SurfaceY - Size.Y * 0.5f);
            _struck.Clear();
            Active = DistanceRemaining > 0f;
        }

        public void Stop() {
            Active = false;
            _struck.Clear();
        }

        /// <summary>
        /// Advances the front one physics step and strikes what it overlaps.
        /// <paramref name="strike"/> builds and delivers the stamped payload for
        /// one hurtbox (the ability owns the numbers); each hurtbox is struck at
        /// most once per wave.
        /// </summary>
        public void Advance(
            float dt, PhysicsDirectSpaceState2D space, uint deliveryMask, int ownerIndex,
            Action<Hurtbox, Vector2> strike) {
            if (!Active) return;
            float step = Speed * dt;
            if (step > DistanceRemaining) step = DistanceRemaining;
            Vector2 next = Front + new Vector2(MovingRight ? step : -step, 0f);

            if (space != null) {
                // A wall ends the wave.
                var wallQuery = PhysicsRayQueryParameters2D.Create(Front, next, CollisionLayers.Environment);
                if (space.IntersectRay(wallQuery).Count > 0) {
                    Stop();
                    return;
                }
                // So does the end of its surface — a platform edge or a pit.
                if (SurfaceKnown && !TryRayDown(space,
                        new Vector2(next.X, SurfaceY - 4f), ContinuationProbePixels + 4f, out _)) {
                    Stop();
                    return;
                }
            }

            Front = next;
            DistanceRemaining -= step;
            if (space != null) StrikeFront(space, deliveryMask, ownerIndex, strike);
            // A sub-pixel float remainder is the end of the travel, not one more step.
            if (DistanceRemaining <= 0.01f) Stop();
        }

        private void StrikeFront(
            PhysicsDirectSpaceState2D space, uint deliveryMask, int ownerIndex, Action<Hurtbox, Vector2> strike) {
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new RectangleShape2D { Size = Size },
                Transform = new Transform2D(0f, Front),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = deliveryMask
            };
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, MaxQueryResults)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == ownerIndex) continue;
                TryStrike(hurtbox, strike);
            }
        }

        /// <summary>
        /// The per-target rule, split out so a test can drive it without a
        /// physics query: once per wave, and a grounded-only wave skips a target
        /// that is not on the ground (without marking it — it may land into the
        /// wave's path later).
        /// </summary>
        public bool TryStrike(Hurtbox hurtbox, Action<Hurtbox, Vector2> strike) {
            if (!Active || hurtbox == null) return false;
            if (GroundedOnly && !(GroundedProbe?.Invoke(hurtbox) ?? true)) return false;
            if (!_struck.Add(hurtbox.GetInstanceId())) return false;
            strike?.Invoke(hurtbox, Front);
            return true;
        }

        /// <summary>
        /// A hurtbox's owner is grounded when its body reports floor contact.
        /// Hurtboxes with no body above them (checkpoint fractures, extractors,
        /// constructs) sit on the ground and count as grounded.
        /// </summary>
        public static bool IsGroundedTarget(Hurtbox hurtbox) {
            Node current = hurtbox?.GetParent();
            while (current != null) {
                if (current is CharacterBody2D body) return body.IsOnFloor();
                current = current.GetParent();
            }
            return true;
        }

        private static bool TryRayDown(PhysicsDirectSpaceState2D space, Vector2 from, float length, out float hitY) {
            var query = PhysicsRayQueryParameters2D.Create(
                from, from + new Vector2(0f, length),
                CollisionLayers.Environment | CollisionLayers.OneWayPlatform);
            Godot.Collections.Dictionary hit = space.IntersectRay(query);
            if (hit.Count > 0 && hit.ContainsKey("position")) {
                hitY = ((Vector2)hit["position"]).Y;
                return true;
            }
            hitY = 0f;
            return false;
        }
    }
}
