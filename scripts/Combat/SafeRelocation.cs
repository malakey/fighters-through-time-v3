using Godot;
using FTT.Core;

namespace FTT.Combat {

    /// <summary>
    /// The engine side of <see cref="RelocationRules"/>: answers the rule's world
    /// questions — the body sweep with a <see cref="PhysicsDirectSpaceState2D.CastMotion"/>
    /// of the body's box against Environment (the moving body excluded), the
    /// guard rays and the floor probe with <see cref="EnvironmentProbe"/> rays —
    /// and measures a body from its own collision shape. Every Story direct
    /// position write that is not a <c>MoveAndSlide</c> goes through
    /// <see cref="Resolve"/> or <see cref="ResolveSummon"/> — the enemy/boss
    /// Teleport archetype, a SummonMinions spawn point and Einstein's Quantum
    /// Entanglement blink (2026-10-09).
    ///
    /// <para>Headless-safe in the same way as the probe: outside the tree, with no
    /// physics space, inside a physics in/out flush (where the space cannot be
    /// queried — <see cref="PhysicsCallbackGuard"/>), or for an owner with no
    /// collision shape, the requested point stands
    /// (<see cref="RelocationOutcome.Unverified"/>). A caller that runs inside a
    /// flush and must be validated defers its write off the flush; a direct call
    /// stays synchronous.</para>
    /// </summary>
    public static class SafeRelocation {

        /// <summary>
        /// Resolves a write of a body shaped <paramref name="body"/> from
        /// <paramref name="origin"/> to <paramref name="destination"/>, asking the
        /// physics space <paramref name="probeFrom"/> lives in. The sweep ignores
        /// <paramref name="mover"/>, the body being moved (default:
        /// <paramref name="probeFrom"/> when it is a collision object). A null
        /// body (a shapeless owner) is unverified.
        /// </summary>
        public static RelocationResult Resolve(Node2D probeFrom, Vector2 origin, Vector2 destination,
            RelocationBody? body, CollisionObject2D mover = null) {
            if (body is not RelocationBody shape) {
                return new RelocationResult(destination, RelocationOutcome.Unverified, false);
            }
            return RelocationRules.Resolve(new EnvironmentWorld(probeFrom, mover ?? probeFrom as CollisionObject2D),
                origin, destination, shape);
        }

        /// <summary>
        /// Resolves a summon (<see cref="RelocationRules.ResolveSummon"/>): a minion
        /// shaped <paramref name="minion"/> requested at <paramref name="requested"/>
        /// by a summoner whose feet are at <paramref name="summonerFeet"/> and whose
        /// body reaches <paramref name="summonerHalfWidth"/> either side of them.
        /// The sweep ignores <paramref name="minionBody"/>. A null minion shape is
        /// unverified.
        /// </summary>
        public static RelocationResult ResolveSummon(Node2D probeFrom, Vector2 summonerFeet,
            float summonerHalfWidth, Vector2 requested, RelocationBody? minion,
            CollisionObject2D minionBody = null) {
            if (minion is not RelocationBody shape) {
                return new RelocationResult(requested, RelocationOutcome.Unverified, false);
            }
            return RelocationRules.ResolveSummon(new EnvironmentWorld(probeFrom, minionBody),
                summonerFeet, summonerHalfWidth, requested, shape);
        }

        /// <summary>
        /// The relocation profile of a collision body: its first enabled
        /// <see cref="CollisionShape2D"/> child's extents, relative to the body's
        /// origin. Null for anything that is not a <see cref="CollisionObject2D"/>
        /// with a shape — it has no body to put through a wall or over a gap.
        /// </summary>
        public static RelocationBody? BodyOf(Node owner, bool needsFloor, bool passesWalls) {
            if (owner is not CollisionObject2D || !GodotObject.IsInstanceValid(owner)) return null;
            Godot.Collections.Array<Node> children = owner.GetChildren();
            using var childrenLifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is not CollisionShape2D { Disabled: false, Shape: not null } shapeNode) continue;
                Rect2 local = shapeNode.Shape.GetRect();
                Vector2 scale = shapeNode.Scale.Abs();
                return new RelocationBody(
                    local.Size.X * 0.5f * scale.X,
                    shapeNode.Position.Y + local.GetCenter().Y * scale.Y,
                    local.Size.Y * 0.5f * scale.Y,
                    needsFloor,
                    passesWalls);
            }
            return null;
        }

        /// <summary>The live physics space, asked through a shape cast and <see cref="EnvironmentProbe"/>.</summary>
        private sealed class EnvironmentWorld : IRelocationWorld {
            private readonly Node2D _from;
            private readonly CollisionObject2D _mover;
            // RefCounted: held for this resolution, never disposed.
            private PhysicsRayQueryParameters2D _query;
            private PhysicsShapeQueryParameters2D _sweep;
            private RectangleShape2D _sweepShape;

            public EnvironmentWorld(Node2D from, CollisionObject2D mover) {
                _from = from;
                _mover = mover;
            }

            public bool CanQuery =>
                _from != null && GodotObject.IsInstanceValid(_from) && _from.IsInsideTree()
                && !PhysicsCallbackGuard.IsInPhysicsCallback
                && _from.GetWorld2D()?.DirectSpaceState != null;

            private PhysicsRayQueryParameters2D Query => _query ??= EnvironmentProbe.CreateReusableQuery();

            /// <summary>
            /// The box cast along the motion against Environment, the mover
            /// excluded. The engine's cast bisects the motion in eight steps that
            /// are not always even halves, so one cast brackets the contact only
            /// to about 1/171 of the travel (about 2.4 px on a 400 px teleport);
            /// a second cast across the bracketing interval brings it to about
            /// 0.014 px. The contact is always the last safe fraction, so the
            /// landing never touches the wall even before the clearance.
            /// </summary>
            public float SweepContactTravel(Rect2 box, Vector2 motion) {
                float length = motion.Length();
                if (length <= 0f || box.Size.X <= 0f || box.Size.Y <= 0f) return -1f;
                if (!CanQuery) return -1f;
                PhysicsDirectSpaceState2D space = _from.GetWorld2D().DirectSpaceState;
                if (_sweep == null) {
                    _sweepShape = new RectangleShape2D();
                    _sweep = new PhysicsShapeQueryParameters2D {
                        CollideWithAreas = false,
                        CollideWithBodies = true,
                        CollisionMask = CollisionLayers.Environment
                    };
                    if (_mover != null && GodotObject.IsInstanceValid(_mover)) {
                        // Copied into the query on assignment; the wrapper is disposed here.
                        var exclude = new Godot.Collections.Array<Rid> { _mover.GetRid() };
                        using Godot.Collections.Array excludeLifetime = exclude.AsDisposable();
                        _sweep.Exclude = exclude;
                    }
                }
                _sweepShape.Size = box.Size;
                _sweep.Shape = _sweepShape;
                Vector2 start = box.GetCenter();
                _sweep.Transform = new Transform2D(0f, start);
                _sweep.Motion = motion;
                float[] coarse = space.CastMotion(_sweep);
                if (coarse == null || coarse.Length < 2) return -1f;
                float safe = Mathf.Clamp(coarse[0], 0f, 1f);
                float unsafeFraction = Mathf.Clamp(coarse[1], safe, 1f);
                if (safe >= 1f) return -1f;
                if (unsafeFraction > safe) {
                    _sweep.Transform = new Transform2D(0f, start + motion * safe);
                    _sweep.Motion = motion * (unsafeFraction - safe);
                    float[] fine = space.CastMotion(_sweep);
                    // A refinement that meets nothing (it cannot, short of float
                    // noise) keeps the coarse safe fraction rather than the
                    // touching one.
                    if (fine != null && fine.Length > 0 && fine[0] < 1f) {
                        safe += Mathf.Clamp(fine[0], 0f, 1f) * (unsafeFraction - safe);
                    }
                }
                return length * safe;
            }

            public float WallHitDistance(Vector2 from, Vector2 to) =>
                EnvironmentProbe.FirstHitDistance(_from, Query, from, to, CollisionLayers.Environment);

            public bool HasFloorOnSegment(Vector2 from, Vector2 to) =>
                EnvironmentProbe.FirstHitDistance(_from, Query, from, to,
                    CollisionLayers.Environment | CollisionLayers.OneWayPlatform,
                    hitFromInside: true) >= 0f;
        }
    }
}
