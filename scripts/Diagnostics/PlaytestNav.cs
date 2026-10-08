using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;

namespace FTT.Diagnostics {

    // =====================================================================
    // Playtest navigation: a platformer nav graph read from the live level.
    //
    // Every standable top edge of the level's collision (static bodies,
    // one-way platforms, moving-platform endpoints, closed trapdoors) is a
    // NavSegment, cut wherever a solid body occupies the hero's standing
    // space (a wall on the floor, a door, a breakable, a ceiling too low to
    // stand under). Segments are joined by edges — walk across a seam, walk
    // off an end, jump / double-jump, drop through a one-way, break the
    // breakable that split them, ride a moving platform — and every aerial
    // edge is proven by a frame-by-frame simulation of the hero's own Story
    // jump (the PlayerController gravity, jump, fall and air-control rules)
    // against the level's solid rectangles. A Dijkstra search over the
    // segments plans a route; NavAgent turns the route into pad input.
    //
    // Diagnostics only: nothing here writes gameplay state. The one piece of
    // reflection (PathMovingPlatform's private path origin) only reads.
    // =====================================================================

    public enum NavRectKind { Static, Moving, Trapdoor, Crumbling, Breakable, Rigid }

    /// <summary>One collision rectangle (oriented) read from the level.</summary>
    public sealed class NavRect {
        public CollisionObject2D Body;
        public Node2D Shape;
        public NavRectKind Kind;
        public bool OneWay;
        public bool Solid;
        public Vector2 Center;
        public Vector2 AxisX = Vector2.Right;
        public Vector2 AxisY = Vector2.Down;
        public Vector2 Half;
        public Rect2 Aabb;
        public bool AxisAligned = true;
        public DamageableEnvironmentObject Breakable;
        public Vector2 BodyPosition;

        public static NavRect FromRectangle(CollisionObject2D body, CollisionShape2D shape, RectangleShape2D rect) {
            Transform2D xf = shape.GlobalTransform;
            float sx = xf.X.Length(), sy = xf.Y.Length();
            var r = new NavRect {
                Body = body, Shape = shape,
                Center = xf.Origin,
                AxisX = sx > 0.0001f ? xf.X / sx : Vector2.Right,
                AxisY = sy > 0.0001f ? xf.Y / sy : Vector2.Down,
                Half = new Vector2(rect.Size.X * sx, rect.Size.Y * sy) / 2f,
                BodyPosition = body.GlobalPosition
            };
            r.AxisAligned = Mathf.Abs(r.AxisX.Y) < 0.001f && Mathf.Abs(r.AxisY.X) < 0.001f;
            r.ComputeAabb();
            return r;
        }

        public static NavRect FromCircle(CollisionObject2D body, CollisionShape2D shape, CircleShape2D circle) {
            float r = circle.Radius * shape.GlobalTransform.X.Length();
            var rect = new NavRect {
                Body = body, Shape = shape,
                Center = shape.GlobalPosition,
                Half = new Vector2(r, r),
                BodyPosition = body.GlobalPosition
            };
            rect.ComputeAabb();
            return rect;
        }

        public static NavRect FromPolygon(CollisionObject2D body, CollisionPolygon2D polygon) {
            Vector2[] points = polygon.Polygon;
            if (points == null || points.Length == 0) return null;
            Transform2D xf = polygon.GlobalTransform;
            Vector2 min = xf * points[0], max = min;
            foreach (Vector2 p in points) {
                Vector2 w = xf * p;
                min = new Vector2(Mathf.Min(min.X, w.X), Mathf.Min(min.Y, w.Y));
                max = new Vector2(Mathf.Max(max.X, w.X), Mathf.Max(max.Y, w.Y));
            }
            var r = new NavRect {
                Body = body, Shape = polygon,
                Center = (min + max) / 2f,
                Half = (max - min) / 2f,
                BodyPosition = body.GlobalPosition
            };
            r.ComputeAabb();
            return r;
        }

        public void RecomputeAabb() => ComputeAabb();

        private void ComputeAabb() {
            float ex = Mathf.Abs(AxisX.X) * Half.X + Mathf.Abs(AxisY.X) * Half.Y;
            float ey = Mathf.Abs(AxisX.Y) * Half.X + Mathf.Abs(AxisY.Y) * Half.Y;
            Aabb = new Rect2(Center.X - ex, Center.Y - ey, ex * 2f, ey * 2f);
        }

        /// <summary>Separating-axis overlap with an axis-aligned box.</summary>
        public bool Overlaps(Rect2 box) {
            if (!(box.Position.X < Aabb.End.X && box.End.X > Aabb.Position.X
                  && box.Position.Y < Aabb.End.Y && box.End.Y > Aabb.Position.Y)) return false;
            if (AxisAligned) return true;
            Vector2 c = box.GetCenter();
            Vector2 h = box.Size / 2f;
            Vector2 d = c - Center;
            // Rect axes.
            if (Mathf.Abs(d.Dot(AxisX)) >= Half.X + h.X * Mathf.Abs(AxisX.X) + h.Y * Mathf.Abs(AxisX.Y)) return false;
            if (Mathf.Abs(d.Dot(AxisY)) >= Half.Y + h.X * Mathf.Abs(AxisY.X) + h.Y * Mathf.Abs(AxisY.Y)) return false;
            return true;
        }

        /// <summary>The upward-facing edge (left endpoint first).</summary>
        public void TopEdge(out Vector2 left, out Vector2 right) {
            Vector2[] normals = { AxisX, -AxisX, AxisY, -AxisY };
            float[] halves = { Half.X, Half.X, Half.Y, Half.Y };
            int best = 0;
            for (int i = 1; i < 4; i++) if (normals[i].Y < normals[best].Y) best = i;
            Vector2 n = normals[best];
            Vector2 t = best < 2 ? AxisY : AxisX;
            float ht = best < 2 ? Half.Y : Half.X;
            Vector2 mid = Center + n * halves[best];
            Vector2 a = mid - t * ht, b = mid + t * ht;
            if (a.X <= b.X) { left = a; right = b; } else { left = b; right = a; }
        }

        public bool StillValid() {
            if (!GodotObject.IsInstanceValid(Body) || !GodotObject.IsInstanceValid(Shape)) return false;
            if (!Body.IsInsideTree()) return false;
            if (Shape is CollisionShape2D cs && cs.Disabled) return false;
            if (Shape is CollisionPolygon2D cp && cp.Disabled) return false;
            if (Kind == NavRectKind.Rigid && Body.GlobalPosition.DistanceTo(BodyPosition) > 12f) return false;
            return true;
        }
    }

    /// <summary>A standable top edge, cut to where the hero actually fits.</summary>
    public sealed class NavSegment {
        public int Id;
        public float Left, Right;
        public float YLeft, YRight;
        public bool OneWay;
        public NavRectKind Kind;
        public CollisionObject2D Body;
        public PathMovingPlatform Platform;
        /// <summary>Set on a rope pseudo-segment: the swinging anchor whose grab point is the "surface".</summary>
        public PendulumAnchor Rope;
        public int PlatformEndpoint = -1;
        public TrapdoorPlatform Trapdoor;
        /// <summary>Breakable obstacles that cut this segment at its left / right end.</summary>
        public DamageableEnvironmentObject LeftBreakable, RightBreakable;
        public bool Temporary;

        public float Width => Right - Left;
        public float Top => Mathf.Min(YLeft, YRight);
        public float TopAt(float x) {
            if (Right - Left < 1f) return YLeft;
            return Mathf.Lerp(YLeft, YRight, Mathf.Clamp((x - Left) / (Right - Left), 0f, 1f));
        }
        public bool Spans(float x, float margin = 0f) => x >= Left - margin && x <= Right + margin;
        public float Clamp(float x, float inset) {
            float lo = Left + inset, hi = Right - inset;
            if (lo > hi) return (Left + Right) / 2f;
            return Mathf.Clamp(x, lo, hi);
        }
        public bool IsDynamic => Kind is NavRectKind.Trapdoor or NavRectKind.Crumbling || Temporary;
        public bool IsRope => Rope != null;
        public override string ToString() =>
            Rope != null ? $"rope:{Rope.Name}" :
            $"{Mathf.RoundToInt(Left)}..{Mathf.RoundToInt(Right)}@{Mathf.RoundToInt(Top)}{(OneWay ? "ow" : "")}{(Platform != null ? $"P{PlatformEndpoint}" : "")}";
    }

    public enum NavEdgeKind { Walk, Fall, Jump, Drop, Break, Ride, Catch, Release }

    public sealed class NavEdge {
        public NavSegment From, To;
        public NavEdgeKind Kind;
        public float LaunchX, LandX;
        public int Jumps;
        public int Frames;
        /// <summary>Airborne frames to rise straight up before steering toward the landing (clears a ledge lip).</summary>
        public int SteerDelay;
        public DamageableEnvironmentObject Obstacle;
        public float BaseCost;
        public int Failures;
        public float Cost => BaseCost + Failures * 6f;
        public override string ToString() => $"{Kind}{(Kind == NavEdgeKind.Jump ? Jumps.ToString() : "")} {From}->{To} @{Mathf.RoundToInt(LaunchX)}->{Mathf.RoundToInt(LandX)}";
    }

    /// <summary>The hero's Story movement constants, read from the live PlayerController.</summary>
    public struct HeroPhysics {
        public float JumpSpeed;      // px/s
        public float GravityPerFrame; // px/s added per frame at multiplier 1 (before zone scale)
        public float RunSpeed;       // px/s
        public float AirControl;
        public int MaxJumps;

        // PlayerController constants (Story rules, mirrored for the planner).
        public const float FallMultiplier = 1.8f;
        public const float ShortHopMultiplier = 2.5f;
        public const float Terminal = 600f;
        public const float AirAccelRamp = 4f;
        public const float AirDecelRamp = 8f;
        public const float BodyHalfWidth = 20f;
        public const float BodyHeight = 64f;

        public static HeroPhysics From(PlayerController player) {
            CharacterData data = player.Data;
            float weight = data?.Weight ?? 1f;
            return new HeroPhysics {
                JumpSpeed = (data?.MaxJumpForce ?? 14f) * player.StoryJumpForceMultiplier * player.StatusJumpMultiplier * 54f,
                GravityPerFrame = 18f * (0.8f + 0.4f * weight),
                RunSpeed = (data?.MaxMoveSpeed ?? 8f) * player.StoryMoveSpeedMultiplier * player.StatusMovementMultiplier * 60f,
                AirControl = data?.AirControlMultiplier ?? 1f,
                MaxJumps = Math.Max(1, data?.MaxJumpCount ?? 1)
            };
        }

        public float SingleRise(float gravityScale) {
            float g = GravityPerFrame * 60f * Mathf.Max(0.05f, gravityScale);
            return JumpSpeed * JumpSpeed / (2f * g);
        }
    }

    public sealed class PlaytestNav {
        private readonly SceneTree _tree;
        public PlaytestNav(SceneTree tree) => _tree = tree;

        // --- collected geometry ---
        private readonly List<NavRect> _static = new();      // static solid + one-way (incl. breakables, rigid)
        private readonly List<NavRect> _dynamic = new();     // moving / trapdoor / crumbling
        private readonly List<(CollisionObject2D Body, Node2D Shape)> _disabledWatch = new();
        private readonly List<(Rect2 Area, float Scale)> _gravityZones = new();
        private readonly List<(Rect2 Area, float Multiplier)> _slowZones = new();
        private readonly List<Rect2> _spikes = new();
        private readonly List<PathMovingPlatform> _platforms = new();
        private readonly List<PendulumAnchor> _ropes = new();
        private readonly Dictionary<PathMovingPlatform, Vector2> _platformOrigins = new();

        // --- segments and edges ---
        private readonly List<NavSegment> _staticSegments = new();
        private readonly List<NavSegment> _dynamicSegments = new();
        private readonly Dictionary<NavSegment, List<NavEdge>> _edgeCache = new();
        private readonly List<NavRect> _obstacles = new(); // solid static rects used by the simulation
        private const float Cell = 256f;
        private readonly Dictionary<int, List<NavRect>> _obstacleGrid = new();
        private readonly Dictionary<int, List<NavSegment>> _segmentGrid = new();

        private ulong _sceneId;
        private int _builtFrame = -100000;
        private int _validatedFrame;
        private int _dynamicFrame = -1;
        private int _segmentIds;
        public int Frame;
        public int Rebuilds { get; private set; }
        public HeroPhysics Hero;

        public IReadOnlyList<NavSegment> StaticSegments => _staticSegments;
        public IReadOnlyList<NavRect> Obstacles => _obstacles;

        /// <summary>Call once per physics frame (cheap unless the geometry changed).</summary>
        public void Update(PlayerController player) {
            Frame++;
            if (player != null) Hero = HeroPhysics.From(player);
            Node scene = _tree.CurrentScene;
            if (scene == null) return;
            bool rebuild = scene.GetInstanceId() != _sceneId || Frame - _builtFrame > 1200;
            if (!rebuild && Frame - _validatedFrame >= 15) {
                _validatedFrame = Frame;
                foreach (NavRect rect in _static) {
                    if (!rect.StillValid()) { rebuild = true; break; }
                }
                if (!rebuild) {
                    foreach ((CollisionObject2D body, Node2D shape) in _disabledWatch) {
                        if (!GodotObject.IsInstanceValid(body) || !GodotObject.IsInstanceValid(shape)) continue;
                        bool disabled = shape is CollisionShape2D cs ? cs.Disabled : shape is CollisionPolygon2D cp && cp.Disabled;
                        if (!disabled && body.IsInsideTree()) { rebuild = true; break; }
                    }
                }
            }
            if (rebuild) Build(scene);
            if (_dynamicFrame != Frame) RefreshDynamic();
        }

        // =================================================================
        // Collection
        // =================================================================

        private void Build(Node scene) {
            _sceneId = scene.GetInstanceId();
            _builtFrame = Frame;
            _validatedFrame = Frame;
            Rebuilds++;
            _static.Clear();
            _dynamic.Clear();
            _disabledWatch.Clear();
            _gravityZones.Clear();
            _slowZones.Clear();
            _spikes.Clear();
            _platforms.Clear();
            _ropes.Clear();
            Collect(scene);
            BuildStaticSegments();
            _edgeCache.Clear();
            _dynamicFrame = -1;
        }

        private void Collect(Node node) {
            if (node is CharacterBody2D) return; // hero, enemies: never geometry
            if (node is Area2D spikes && spikes.Name.ToString().StartsWith("Spikes_", StringComparison.Ordinal)) {
                // Spike strips are not solid, but nobody should plan to stand on one.
                Rect2? area = AreaRect(spikes);
                if (area.HasValue) _spikes.Add(area.Value);
            } else if (node is GravityFieldZone gravity) {
                Rect2? area = AreaRect(gravity);
                if (area.HasValue) _gravityZones.Add((area.Value, gravity.GravityScale));
            } else if (node is MovementDampenerZone slow) {
                Rect2? area = AreaRect(slow);
                if (area.HasValue && slow.Enabled) _slowZones.Add((area.Value, slow.MoveMultiplier));
            } else if (node is PendulumAnchor rope) {
                _ropes.Add(rope);
            } else if (node is CollisionObject2D body && node is not Area2D) {
                uint layer = body.CollisionLayer;
                bool oneWay = (layer & CollisionLayers.OneWayPlatform) != 0;
                bool solid = (layer & CollisionLayers.Environment) != 0;
                if (oneWay || solid) {
                    NavRectKind kind = body switch {
                        PathMovingPlatform => NavRectKind.Moving,
                        TrapdoorPlatform => NavRectKind.Trapdoor,
                        CrumblingPlatform => NavRectKind.Crumbling,
                        DamageableEnvironmentObject => NavRectKind.Breakable,
                        RigidBody2D => NavRectKind.Rigid,
                        AnimatableBody2D => NavRectKind.Moving,
                        _ => NavRectKind.Static
                    };
                    if (body is PathMovingPlatform platform) RegisterPlatform(platform);
                    Godot.Collections.Array<Node> shapes = body.GetChildren();
                    using var shapesLifetime = shapes.AsDisposable();
                    foreach (Node child in shapes) {
                        NavRect rect = null;
                        bool disabled = false;
                        if (child is CollisionShape2D cs && cs.Shape is RectangleShape2D rs) {
                            disabled = cs.Disabled;
                            if (!disabled) rect = NavRect.FromRectangle(body, cs, rs);
                        } else if (child is CollisionShape2D cc && cc.Shape is CircleShape2D circle) {
                            // Boulders and other round props: a square obstacle, never a floor.
                            disabled = cc.Disabled;
                            if (!disabled) rect = NavRect.FromCircle(body, cc, circle);
                        } else if (child is CollisionPolygon2D cp) {
                            disabled = cp.Disabled;
                            if (!disabled) rect = NavRect.FromPolygon(body, cp);
                        } else {
                            continue;
                        }
                        if (disabled) {
                            if (kind is NavRectKind.Static or NavRectKind.Breakable or NavRectKind.Rigid) _disabledWatch.Add((body, (Node2D)child));
                            continue;
                        }
                        if (rect == null) continue;
                        rect.Kind = kind;
                        rect.OneWay = oneWay && !solid;
                        rect.Solid = solid;
                        rect.Breakable = body as DamageableEnvironmentObject;
                        if (kind is NavRectKind.Moving or NavRectKind.Trapdoor or NavRectKind.Crumbling) _dynamic.Add(rect);
                        else _static.Add(rect);
                    }
                }
            }
            Godot.Collections.Array<Node> children = node.GetChildren();
            using var childrenLifetime = children.AsDisposable();
            foreach (Node child in children) Collect(child);
        }

        private static Rect2? AreaRect(Area2D area) {
            Godot.Collections.Array<Node> children = area.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is CollisionShape2D cs && cs.Shape is RectangleShape2D rs) {
                    Transform2D xf = cs.GlobalTransform;
                    Vector2 size = rs.Size * new Vector2(xf.X.Length(), xf.Y.Length());
                    return new Rect2(xf.Origin - size / 2f, size);
                }
            }
            return null;
        }

        private static readonly FieldInfo PlatformOriginField =
            typeof(PathMovingPlatform).GetField("_origin", BindingFlags.NonPublic | BindingFlags.Instance);

        private void RegisterPlatform(PathMovingPlatform platform) {
            _platforms.Add(platform);
            if (!_platformOrigins.ContainsKey(platform) && PlatformOriginField?.GetValue(platform) is Vector2 origin) {
                _platformOrigins[platform] = origin;
            }
        }

        /// <summary>World position of a moving platform's waypoint (its body origin when there).</summary>
        public bool PlatformEndpoint(PathMovingPlatform platform, int index, out Vector2 world) {
            world = Vector2.Zero;
            if (platform?.Waypoints == null || index < 0 || index >= platform.Waypoints.Length) return false;
            if (!_platformOrigins.TryGetValue(platform, out Vector2 origin)) return false;
            Vector2 local = origin + platform.Waypoints[index];
            Node2D parent = platform.GetParent() as Node2D;
            world = parent != null ? parent.GlobalTransform * local : local;
            return true;
        }

        // =================================================================
        // Segments
        // =================================================================

        private struct RawSurface {
            public float Left, Right, YLeft, YRight;
            public bool OneWay;
            public NavRect Source;
        }

        private void BuildStaticSegments() {
            _staticSegments.Clear();
            _obstacles.Clear();
            _obstacleGrid.Clear();
            _segmentIds = 0;
            foreach (NavRect rect in _static) {
                if (!rect.Solid) continue;
                _obstacles.Add(rect);
                int c0 = Mathf.FloorToInt(rect.Aabb.Position.X / Cell), c1 = Mathf.FloorToInt(rect.Aabb.End.X / Cell);
                for (int c = c0; c <= c1; c++) {
                    if (!_obstacleGrid.TryGetValue(c, out List<NavRect> list)) _obstacleGrid[c] = list = new List<NavRect>();
                    list.Add(rect);
                }
            }

            var raws = new List<RawSurface>();
            foreach (NavRect rect in _static) {
                if (rect.Kind is NavRectKind.Breakable or NavRectKind.Rigid) continue;
                if (!TryTop(rect, out RawSurface raw)) continue;
                raws.Add(raw);
            }
            // Merge collinear neighbours (adjacent floors authored room by room).
            raws.Sort((a, b) => a.Left.CompareTo(b.Left));
            var merged = new List<RawSurface>();
            foreach (RawSurface raw in raws) {
                bool absorbed = false;
                for (int i = 0; i < merged.Count; i++) {
                    RawSurface m = merged[i];
                    bool flat = Mathf.Abs(m.YLeft - m.YRight) < 0.5f && Mathf.Abs(raw.YLeft - raw.YRight) < 0.5f;
                    if (flat && m.OneWay == raw.OneWay && Mathf.Abs(m.YRight - raw.YLeft) <= 3f
                        && raw.Left <= m.Right + 6f && raw.Right >= m.Left - 6f) {
                        m.Left = Mathf.Min(m.Left, raw.Left);
                        m.Right = Mathf.Max(m.Right, raw.Right);
                        merged[i] = m;
                        absorbed = true;
                        break;
                    }
                }
                if (!absorbed) merged.Add(raw);
            }

            foreach (RawSurface surface in merged) {
                foreach (NavSegment piece in CutByObstacles(surface)) {
                    piece.Id = _segmentIds++;
                    _staticSegments.Add(piece);
                }
            }
            // Moving-platform endpoints are fixed places: static segments too.
            foreach (PathMovingPlatform platform in _platforms) {
                if (!GodotObject.IsInstanceValid(platform) || platform.Waypoints == null) continue;
                NavRect current = null;
                foreach (NavRect rect in _dynamic) if (rect.Body == platform) { current = rect; break; }
                if (current == null || !TryTop(current, out RawSurface top)) continue;
                for (int k = 0; k < platform.Waypoints.Length; k++) {
                    if (!PlatformEndpoint(platform, k, out Vector2 endpoint)) continue;
                    Vector2 shift = endpoint - platform.GlobalPosition;
                    _staticSegments.Add(new NavSegment {
                        Id = _segmentIds++,
                        Left = top.Left + shift.X, Right = top.Right + shift.X,
                        YLeft = top.YLeft + shift.Y, YRight = top.YRight + shift.Y,
                        Kind = NavRectKind.Moving, Body = platform,
                        Platform = platform, PlatformEndpoint = k
                    });
                }
            }
            _segmentGrid.Clear();
            foreach (NavSegment segment in _staticSegments) AddToGrid(segment);
            // Swinging ropes: pseudo-segments spanning the grab point's arc
            // (never surfaces, so never in the landing grid).
            foreach (PendulumAnchor rope in _ropes) {
                if (!GodotObject.IsInstanceValid(rope) || !rope.Enabled || rope.Ledge == null) continue;
                RopeArc(rope, out float left, out float right, out float lowY, out float _);
                _staticSegments.Add(new NavSegment {
                    Id = _segmentIds++, Left = left, Right = right, YLeft = lowY, YRight = lowY,
                    Kind = NavRectKind.Moving, Body = rope, Rope = rope
                });
            }
        }

        private void AddToGrid(NavSegment segment) {
            int c0 = Mathf.FloorToInt(segment.Left / Cell), c1 = Mathf.FloorToInt(segment.Right / Cell);
            for (int c = c0; c <= c1; c++) {
                if (!_segmentGrid.TryGetValue(c, out List<NavSegment> list)) _segmentGrid[c] = list = new List<NavSegment>();
                list.Add(segment);
            }
        }

        private static bool TryTop(NavRect rect, out RawSurface raw) {
            raw = default;
            rect.TopEdge(out Vector2 a, out Vector2 b);
            float width = b.X - a.X;
            if (width < 48f) return false;
            if (Mathf.Abs(b.Y - a.Y) > width * 0.6f) return false;
            raw = new RawSurface { Left = a.X, Right = b.X, YLeft = a.Y, YRight = b.Y, OneWay = rect.OneWay, Source = rect };
            return true;
        }

        /// <summary>Clearance the hero needs above a standing surface.</summary>
        public const float StandClearance = 66f;

        private IEnumerable<NavSegment> CutByObstacles(RawSurface surface) {
            // Collect blocked x-intervals: any solid body (other than the
            // surface itself) occupying the hero's standing space.
            var blocked = new List<(float A, float B, DamageableEnvironmentObject Breakable)>();
            int c0 = Mathf.FloorToInt(surface.Left / Cell), c1 = Mathf.FloorToInt(surface.Right / Cell);
            var seen = new HashSet<NavRect>();
            for (int c = c0; c <= c1; c++) {
                if (!_obstacleGrid.TryGetValue(c, out List<NavRect> list)) continue;
                foreach (NavRect o in list) {
                    if (!seen.Add(o) || o == surface.Source) continue;
                    if (o.Aabb.End.X <= surface.Left || o.Aabb.Position.X >= surface.Right) continue;
                    float topHere = Mathf.Min(surface.YLeft, surface.YRight);
                    float bandTop = Mathf.Min(surface.YLeft, surface.YRight) - StandClearance;
                    float bandBottom = Mathf.Max(surface.YLeft, surface.YRight) - 2f;
                    if (o.Aabb.End.Y <= bandTop || o.Aabb.Position.Y >= bandBottom) continue;
                    // Refine on the exact shape: sample the obstacle's x-range.
                    float a = Mathf.Max(surface.Left, o.Aabb.Position.X), b = Mathf.Min(surface.Right, o.Aabb.End.X);
                    float first = float.NaN, last = float.NaN;
                    for (float x = a; x <= b + 0.01f; x += Mathf.Max(4f, (b - a) / 24f)) {
                        float top = Lerp(surface, x);
                        var probe = new Rect2(x - 1f, top - StandClearance, 2f, StandClearance - 2f);
                        if (o.Overlaps(probe)) {
                            if (float.IsNaN(first)) first = x;
                            last = x;
                        }
                    }
                    _ = topHere;
                    if (float.IsNaN(first)) continue;
                    // The hero's body is 40 wide: it cannot stand within a half-width of the obstacle.
                    blocked.Add((first - HeroPhysics.BodyHalfWidth, last + HeroPhysics.BodyHalfWidth, o.Breakable));
                }
            }
            foreach (Rect2 spike in _spikes) {
                if (spike.End.X <= surface.Left || spike.Position.X >= surface.Right) continue;
                float top = Lerp(surface, Mathf.Clamp(spike.GetCenter().X, surface.Left, surface.Right));
                if (spike.End.Y <= top - StandClearance || spike.Position.Y >= top + 2f) continue;
                blocked.Add((spike.Position.X - HeroPhysics.BodyHalfWidth, spike.End.X + HeroPhysics.BodyHalfWidth, null));
            }
            blocked.Sort((x, y) => x.A.CompareTo(y.A));
            float cursor = surface.Left;
            DamageableEnvironmentObject leftBreakable = null;
            bool firstCut = true;
            foreach ((float a, float b, DamageableEnvironmentObject breakable) in blocked) {
                if (a > cursor + 24f || (breakable != null && a > cursor + 1f)) {
                    yield return MakeSegment(surface, cursor, a, leftBreakable, breakable);
                } else if (firstCut && breakable != null && a <= cursor + 1f && a > surface.Left - PerchOverhang) {
                    // A breakable flush against the surface's end leaves no
                    // room inside the edge, but the hero can still stand with
                    // half its body over the drop: a perch to strike it from.
                    yield return MakeSegment(surface, Mathf.Max(surface.Left - PerchOverhang, a - 8f), a, null, breakable);
                }
                firstCut = false;
                if (b > cursor) {
                    cursor = b;
                    leftBreakable = breakable;
                }
            }
            if (surface.Right > cursor + 24f || (leftBreakable != null && surface.Right > cursor + 1f)) {
                yield return MakeSegment(surface, cursor, surface.Right, leftBreakable, null);
            } else if (leftBreakable != null && cursor >= surface.Right - 1f && cursor < surface.Right + PerchOverhang) {
                yield return MakeSegment(surface, cursor, Mathf.Min(surface.Right + PerchOverhang, cursor + 8f), leftBreakable, null);
            }
        }

        /// <summary>How far the hero's centre can stand past a surface's end (half its body over the drop).</summary>
        private const float PerchOverhang = 14f;

        private static float Lerp(RawSurface s, float x) =>
            s.Right - s.Left < 1f ? s.YLeft : Mathf.Lerp(s.YLeft, s.YRight, Mathf.Clamp((x - s.Left) / (s.Right - s.Left), 0f, 1f));

        private static NavSegment MakeSegment(RawSurface s, float left, float right,
            DamageableEnvironmentObject leftBreakable, DamageableEnvironmentObject rightBreakable) {
            return new NavSegment {
                Left = left, Right = right,
                YLeft = Lerp(s, left), YRight = Lerp(s, right),
                OneWay = s.OneWay,
                Kind = s.Source.Kind,
                Body = s.Source.Body,
                LeftBreakable = leftBreakable,
                RightBreakable = rightBreakable
            };
        }

        private void RefreshDynamic() {
            _dynamicFrame = Frame;
            _dynamicSegments.Clear();
            foreach (NavRect rect in _dynamic) {
                if (rect.Kind == NavRectKind.Moving) continue; // endpoints are static segments
                if (!rect.StillValid()) continue;
                if (rect.Body is TrapdoorPlatform trapdoor && trapdoor.State != TrapdoorState.Closed) continue;
                if (rect.Body is CrumblingPlatform crumbling && crumbling.State != CrumblingPlatformState.Solid) continue;
                Node2D shape = rect.Shape;
                if (shape is CollisionShape2D cs && cs.Shape is RectangleShape2D rs) {
                    NavRect fresh = NavRect.FromRectangle(rect.Body, cs, rs);
                    fresh.Kind = rect.Kind;
                    fresh.OneWay = rect.OneWay;
                    if (!TryTop(fresh, out RawSurface raw)) continue;
                    _dynamicSegments.Add(new NavSegment {
                        Id = 100000 + _dynamicSegments.Count,
                        Left = raw.Left, Right = raw.Right, YLeft = raw.YLeft, YRight = raw.YRight,
                        OneWay = raw.OneWay, Kind = rect.Kind, Body = rect.Body,
                        Trapdoor = rect.Body as TrapdoorPlatform
                    });
                }
            }
        }

        public IEnumerable<NavSegment> AllSegments() {
            foreach (NavSegment s in _staticSegments) yield return s;
            foreach (NavSegment s in _dynamicSegments) yield return s;
        }

        /// <summary>Lowest standable top in the level (for out-of-world checks).</summary>
        public float LowestTop {
            get {
                float lowest = float.NegativeInfinity;
                foreach (NavSegment s in _staticSegments) if (!s.IsRope) lowest = Mathf.Max(lowest, Mathf.Max(s.YLeft, s.YRight));
                return lowest;
            }
        }

        public float GravityScaleAt(float x, float y) {
            foreach ((Rect2 area, float scale) in _gravityZones) {
                if (area.HasPoint(new Vector2(x, y))) return scale;
            }
            return 1f;
        }

        public float SpeedMultiplierAt(float x, float y) {
            foreach ((Rect2 area, float multiplier) in _slowZones) {
                if (area.HasPoint(new Vector2(x, y))) return multiplier;
            }
            return 1f;
        }

        // =================================================================
        // Locating the hero
        // =================================================================

        /// <summary>The segment the hero stands on, or a temporary one if none matches.</summary>
        public NavSegment Locate(PlayerController player) {
            if (player == null || !player.IsOnFloor()) return null;
            Vector2 feet = player.GlobalPosition;
            CollisionObject2D floorBody = null;
            int count = player.GetSlideCollisionCount();
            for (int i = 0; i < count; i++) {
                KinematicCollision2D collision = player.GetSlideCollision(i);
                if (collision != null && collision.GetNormal().Y < -0.6f) {
                    floorBody = collision.GetCollider() as CollisionObject2D;
                    break;
                }
            }
            if (floorBody is PathMovingPlatform platform) {
                // Riding: plan from the endpoint the platform is heading to
                // (or waiting at).
                int endpoint = platform.IsWaiting ? platform.LastWaypointIndex : platform.TargetWaypointIndex;
                foreach (NavSegment s in _staticSegments) {
                    if (s.Platform == platform && s.PlatformEndpoint == endpoint) return s;
                }
            }
            NavSegment best = null;
            float bestScore = float.MaxValue;
            foreach (NavSegment s in AllSegments()) {
                if (s.Platform != null || s.IsRope) continue;
                if (!s.Spans(feet.X, 22f)) continue;
                float dy = Mathf.Abs(s.TopAt(feet.X) - feet.Y);
                if (dy > 14f) continue;
                float score = dy + (floorBody != null && s.Body == floorBody ? 0f : 6f)
                    + Mathf.Max(0f, Mathf.Max(s.Left - feet.X, feet.X - s.Right));
                if (score < bestScore) {
                    bestScore = score;
                    best = s;
                }
            }
            if (best != null) return best;
            // Standing on something the graph does not list (a breakable, an
            // enemy, a pit polygon): a small temporary segment under the feet.
            return new NavSegment {
                Id = -1, Left = feet.X - 24f, Right = feet.X + 24f, YLeft = feet.Y, YRight = feet.Y,
                Kind = NavRectKind.Static, Temporary = true, Body = floorBody
            };
        }

        /// <summary>The standable segment under a world point (within <paramref name="maxBelow"/>).</summary>
        public NavSegment SegmentUnder(Vector2 point, float maxBelow = 260f, float minAbove = -40f, float xMargin = 0f) {
            NavSegment best = null;
            float bestDy = float.MaxValue;
            foreach (NavSegment s in AllSegments()) {
                if (s.Platform != null || s.IsRope) continue;
                if (!s.Spans(point.X, xMargin)) continue;
                float dy = s.TopAt(Mathf.Clamp(point.X, s.Left, s.Right)) - point.Y;
                if (dy < minAbove || dy > maxBelow) continue;
                if (dy < bestDy) {
                    bestDy = dy;
                    best = s;
                }
            }
            return best;
        }

        // =================================================================
        // Edges
        // =================================================================

        public List<NavEdge> EdgesFrom(NavSegment a) {
            if (a.IsRope) return RopeReleaseEdges(a);
            if (!a.IsDynamic && _edgeCache.TryGetValue(a, out List<NavEdge> cached)) {
                // Dynamic targets change frame to frame: append fresh edges to them.
                if (_dynamicSegments.Count == 0) return cached;
                var withDynamic = new List<NavEdge>(cached);
                foreach (NavSegment b in _dynamicSegments) AddEdgesTo(a, b, withDynamic);
                return withDynamic;
            }
            var edges = new List<NavEdge>();
            foreach (NavSegment b in _staticSegments) {
                if (b == a) continue;
                if (b.IsRope) {
                    AddCatchEdge(a, b, edges);
                    continue;
                }
                AddEdgesTo(a, b, edges);
            }
            // Ride edges between a platform's endpoints.
            if (a.Platform != null) {
                foreach (NavSegment b in _staticSegments) {
                    if (b == a || b.Platform != a.Platform) continue;
                    float distance = Mathf.Abs(b.Left - a.Left) + Mathf.Abs(b.Top - a.Top);
                    edges.Add(new NavEdge {
                        From = a, To = b, Kind = NavEdgeKind.Ride,
                        LaunchX = (a.Left + a.Right) / 2f, LandX = (b.Left + b.Right) / 2f,
                        BaseCost = distance / Mathf.Max(20f, a.Platform.Speed) + a.Platform.EndpointWaitSeconds * 2f + 1f
                    });
                }
            }
            if (!a.IsDynamic) {
                _edgeCache[a] = edges;
                if (_dynamicSegments.Count > 0) {
                    var withDynamic = new List<NavEdge>(edges);
                    foreach (NavSegment b in _dynamicSegments) AddEdgesTo(a, b, withDynamic);
                    return withDynamic;
                }
            } else {
                foreach (NavSegment b in _dynamicSegments) {
                    if (b != a) AddEdgesTo(a, b, edges);
                }
            }
            return edges;
        }

        private float MaxRise(float gravityScale) {
            float single = Hero.SingleRise(gravityScale);
            return Hero.MaxJumps >= 2 ? single * 1.95f : single;
        }

        private void AddEdgesTo(NavSegment a, NavSegment b, List<NavEdge> edges) {
            if (Hero.JumpSpeed <= 0f) return;
            float gap = Mathf.Max(0f, Mathf.Max(b.Left - a.Right, a.Left - b.Right));
            float gScale = Mathf.Min(GravityScaleAt((a.Left + a.Right) / 2f, a.Top - 10f), GravityScaleAt((b.Left + b.Right) / 2f, b.Top - 10f));
            float rise = MaxRise(gScale);
            float dyTop = b.Top - a.Top; // positive: b lower
            if (-dyTop > rise + 8f) return;
            float airSeconds = 2f * Hero.JumpSpeed / (Hero.GravityPerFrame * 60f * Mathf.Max(0.05f, gScale)) * 1.6f
                + Mathf.Sqrt(Mathf.Max(0f, dyTop + rise) * 2f / (Hero.GravityPerFrame * 60f * HeroPhysics.FallMultiplier * Mathf.Max(0.05f, gScale)));
            float reach = Hero.RunSpeed * airSeconds + 60f;
            if (gap > reach) return;
            if (dyTop > 1600f) return;

            // Breakable seam: the two pieces of one surface either side of a breakable.
            if (a.RightBreakable != null && a.RightBreakable == b.LeftBreakable && b.Left > a.Right - 1f && Mathf.Abs(dyTop) < 12f) {
                edges.Add(new NavEdge {
                    From = a, To = b, Kind = NavEdgeKind.Break, Obstacle = a.RightBreakable,
                    LaunchX = a.Right - 6f, LandX = b.Left + 30f, BaseCost = 4f
                });
            }
            if (a.LeftBreakable != null && a.LeftBreakable == b.RightBreakable && b.Right < a.Left + 1f && Mathf.Abs(dyTop) < 12f) {
                edges.Add(new NavEdge {
                    From = a, To = b, Kind = NavEdgeKind.Break, Obstacle = a.LeftBreakable,
                    LaunchX = a.Left + 6f, LandX = b.Right - 30f, BaseCost = 4f
                });
            }

            // Walk across a seam (adjacent pieces at nearly the same height).
            if (gap <= 26f && Mathf.Abs(b.TopAt(a.Right) - a.TopAt(a.Right)) <= 12f && b.Left >= a.Right - 30f) {
                if (SeamClear(a.Right, b.Left, a.TopAt(a.Right), b.TopAt(b.Left))) {
                    edges.Add(new NavEdge { From = a, To = b, Kind = NavEdgeKind.Walk, LaunchX = a.Right - 4f, LandX = b.Left + 30f, BaseCost = 0.1f });
                    return;
                }
            }
            if (gap <= 26f && Mathf.Abs(b.TopAt(a.Left) - a.TopAt(a.Left)) <= 12f && b.Right <= a.Left + 30f) {
                if (SeamClear(b.Right, a.Left, b.TopAt(b.Right), a.TopAt(a.Left))) {
                    edges.Add(new NavEdge { From = a, To = b, Kind = NavEdgeKind.Walk, LaunchX = a.Left + 4f, LandX = b.Right - 30f, BaseCost = 0.1f });
                    return;
                }
            }

            NavEdge best = null;
            // Drop through a one-way onto something underneath.
            if (a.OneWay && dyTop > 30f && b.Right > a.Left + 20f && b.Left < a.Right - 20f) {
                float lo = Mathf.Max(a.Left, b.Left) + 24f, hi = Mathf.Min(a.Right, b.Right) - 24f;
                if (hi >= lo) {
                    foreach (float x in Candidates(lo, hi, (a.Left + a.Right) / 2f)) {
                        SimResult r = Simulate(new Vector2(x, a.TopAt(x) + 2f), 0f, 0, true, a, b, b.Clamp(x, 24f));
                        if (r.Success) Keep(ref best, a, b, NavEdgeKind.Drop, x, r, 0.6f);
                        if (best != null) break;
                    }
                }
            }
            // Walk off an end.
            if (dyTop > 24f) {
                if (b.Right > a.Right - 10f) {
                    float x0 = a.Right + HeroPhysics.BodyHalfWidth + 2f;
                    foreach (float land in LandCandidates(b, x0)) {
                        SimResult r = Simulate(new Vector2(x0, a.TopAt(a.Right)), Hero.RunSpeed * 0.8f, 0, false, null, b, land);
                        if (r.Success) { Keep(ref best, a, b, NavEdgeKind.Fall, a.Right - 2f, r, 0.2f, land); break; }
                    }
                }
                if (b.Left < a.Left + 10f) {
                    float x0 = a.Left - HeroPhysics.BodyHalfWidth - 2f;
                    foreach (float land in LandCandidates(b, x0)) {
                        SimResult r = Simulate(new Vector2(x0, a.TopAt(a.Left)), -Hero.RunSpeed * 0.8f, 0, false, null, b, land);
                        if (r.Success) { Keep(ref best, a, b, NavEdgeKind.Fall, a.Left + 2f, r, 0.2f, land); break; }
                    }
                }
            }
            // Jumps (single, then double).
            int maxJumps = Mathf.Clamp(Hero.MaxJumps, 1, 2);
            for (int jumps = 1; jumps <= maxJumps; jumps++) {
                if (best != null && best.Kind != NavEdgeKind.Fall && jumps == 2) break;
                bool found = false;
                foreach (int delay in SteerDelays) {
                    foreach (float x in LaunchCandidates(a, b)) {
                        foreach (float land in LandCandidates(b, x)) {
                            SimResult r = Simulate(new Vector2(x, a.TopAt(x)), 0f, jumps, false, a, b, land, 240, delay);
                            if (r.Success) {
                                Keep(ref best, a, b, NavEdgeKind.Jump, x, r, 0.25f * jumps + delay / 120f, land, jumps, delay);
                                found = true;
                                break;
                            }
                        }
                        if (found) break;
                    }
                    if (found) break;
                }
            }
            if (best != null) edges.Add(best);
        }

        private bool SeamClear(float x0, float x1, float y0, float y1) {
            float lo = Mathf.Min(x0, x1) - 2f, hi = Mathf.Max(x0, x1) + 2f;
            float top = Mathf.Min(y0, y1);
            var box = new Rect2(lo, top - StandClearance + 4f, Mathf.Max(1f, hi - lo), StandClearance - 8f);
            foreach (NavRect o in ObstaclesNear(lo, hi)) {
                if (o.Overlaps(box)) return false;
            }
            return true;
        }

        private static IEnumerable<float> Candidates(float lo, float hi, float preferred) {
            yield return Mathf.Clamp(preferred, lo, hi);
            yield return lo;
            yield return hi;
        }

        private static IEnumerable<float> LaunchCandidates(NavSegment a, NavSegment b) {
            const float inset = 22f;
            var list = new List<float>(6);
            void Add(float x) {
                x = a.Clamp(x, inset);
                foreach (float existing in list) if (Mathf.Abs(existing - x) < 20f) return;
                list.Add(x);
            }
            float bCenter = (b.Left + b.Right) / 2f;
            if (b.Left >= a.Right - 40f) {          // b to the right
                Add(a.Right); Add(b.Left - 120f); Add(b.Left - 240f);
            } else if (b.Right <= a.Left + 40f) {   // b to the left
                Add(a.Left); Add(b.Right + 120f); Add(b.Right + 240f);
            } else {                                // overlapping
                Add(bCenter);
                Add(b.Left - 60f); Add(b.Right + 60f);
                Add(b.Left - 130f); Add(b.Right + 130f);
                Add(b.Left + 30f); Add(b.Right - 30f);
            }
            return list;
        }

        private static IEnumerable<float> LandCandidates(NavSegment b, float fromX) {
            const float inset = 26f;
            var list = new List<float>(4);
            void Add(float x) {
                x = b.Clamp(x, inset);
                foreach (float existing in list) if (Mathf.Abs(existing - x) < 16f) return;
                list.Add(x);
            }
            Add(fromX);
            Add(fromX < b.Left ? b.Left + 40f : b.Right - 40f);
            Add((b.Left + b.Right) / 2f);
            return list;
        }

        /// <summary>Airborne frames of straight rise before steering (0 = steer from take-off).</summary>
        private static readonly int[] SteerDelays = { 0, 16, 32 };

        private void Keep(ref NavEdge best, NavSegment a, NavSegment b, NavEdgeKind kind, float launchX, SimResult r,
            float penalty, float landX = float.NaN, int jumps = 0, int steerDelay = 0) {
            float cost = r.Frames / 60f + penalty;
            if (best != null && best.BaseCost <= cost) return;
            best = new NavEdge {
                From = a, To = b, Kind = kind, LaunchX = launchX,
                LandX = float.IsNaN(landX) ? r.LandX : landX,
                Jumps = jumps, Frames = r.Frames, BaseCost = cost, SteerDelay = steerDelay
            };
        }

        // =================================================================
        // Jump simulation (mirrors PlayerController's Story air rules)
        // =================================================================

        public struct SimResult {
            public bool Success;
            public int Frames;
            public float LandX;
            public NavSegment LandedOn;
            public bool Crashed;
            public Vector2 EndPos;
        }

        private readonly List<NavRect> _scratchObstacles = new();

        private List<NavRect> ObstaclesNear(float lo, float hi) {
            _scratchObstacles.Clear();
            int c0 = Mathf.FloorToInt(lo / Cell), c1 = Mathf.FloorToInt(hi / Cell);
            for (int c = c0; c <= c1; c++) {
                if (!_obstacleGrid.TryGetValue(c, out List<NavRect> list)) continue;
                foreach (NavRect o in list) if (!_scratchObstacles.Contains(o)) _scratchObstacles.Add(o);
            }
            return _scratchObstacles;
        }

        /// <summary>One simulated airborne hero (the PlayerController Story air rules).</summary>
        public struct Flight {
            public Vector2 Pos, Vel;
            public bool Holding;          // jump held (no short-hop gravity while rising)
            public int AirJumps;          // jumps left to spend
            public bool UseAirJumps;      // press the next jump right after each apex
            public int Frame;
            public int SteerDelay;        // frames of no horizontal input
            public float TargetX;
            public NavSegment IgnoreOneWay;
            public float DropStartY;
        }

        public enum FlightStep { Flying, Landed, Crashed }

        /// <summary>
        /// Advances a flight one frame: steering (full speed toward TargetX,
        /// braking to stop over it), jump hold / air jumps, gravity with the
        /// fall and short-hop multipliers, landing on any surface top while
        /// descending, head bumps on undersides, and a crash on any wall.
        /// </summary>
        public FlightStep StepFlight(ref Flight f, List<NavRect> obstacles, out NavSegment landedOn) {
            landedOn = null;
            f.Frame++;
            float maxSpeed = Hero.RunSpeed * SpeedMultiplierAt(f.Pos.X, f.Pos.Y - 10f);
            float desired = f.Frame <= f.SteerDelay ? 0f : SteerToward(f.Pos.X, f.Vel.X, f.TargetX, maxSpeed, Hero.AirControl);
            bool accelerating = Mathf.Abs(desired) >= Mathf.Abs(f.Vel.X) || (desired > 0 && f.Vel.X < 0) || (desired < 0 && f.Vel.X > 0);
            float ramp = accelerating ? HeroPhysics.AirAccelRamp : HeroPhysics.AirDecelRamp;
            f.Vel.X = Mathf.MoveToward(f.Vel.X, desired, maxSpeed / ramp * Hero.AirControl);

            if (f.Holding && f.Vel.Y >= 0f) f.Holding = false;
            else if (!f.Holding && f.UseAirJumps && f.AirJumps > 0 && f.Vel.Y > -40f) {
                f.Vel.Y = -Hero.JumpSpeed;
                f.AirJumps--;
                f.Holding = true;
            }
            float g = Hero.GravityPerFrame * GravityScaleAt(f.Pos.X, f.Pos.Y - 30f);
            float mult = f.Vel.Y > 0f ? HeroPhysics.FallMultiplier : (!f.Holding && f.Vel.Y < 0f ? HeroPhysics.ShortHopMultiplier : 1f);
            f.Vel.Y = Mathf.Min(f.Vel.Y + g * mult, HeroPhysics.Terminal);

            Vector2 next = f.Pos + f.Vel / 60f;
            if (f.Vel.Y > 0f) {
                NavSegment landed = LandingSurface(f.Pos, next, f.IgnoreOneWay);
                if (landed != null) {
                    landedOn = landed;
                    f.Pos = next;
                    return FlightStep.Landed;
                }
            }
            var box = new Rect2(next.X - HeroPhysics.BodyHalfWidth + 1f, next.Y - HeroPhysics.BodyHeight + 1f,
                HeroPhysics.BodyHalfWidth * 2f - 2f, HeroPhysics.BodyHeight - 2f);
            foreach (NavRect o in obstacles) {
                if (!o.Overlaps(box)) continue;
                // Rising into the underside of something: the head bumps, the
                // rise ends, the flight goes on (the engine slides).
                float headBefore = f.Pos.Y - HeroPhysics.BodyHeight;
                if (f.Vel.Y < 0f && o.AxisAligned && o.Aabb.End.Y <= headBefore + 2f) {
                    next.Y = o.Aabb.End.Y + HeroPhysics.BodyHeight + 0.5f;
                    f.Vel.Y = 0f;
                    f.Holding = false;
                    box = new Rect2(next.X - HeroPhysics.BodyHalfWidth + 1f, next.Y - HeroPhysics.BodyHeight + 1f,
                        HeroPhysics.BodyHalfWidth * 2f - 2f, HeroPhysics.BodyHeight - 2f);
                    continue;
                }
                return FlightStep.Crashed;
            }
            foreach (NavRect o in obstacles) {
                if (o.Overlaps(box)) return FlightStep.Crashed;
            }
            if (f.IgnoreOneWay != null && next.Y > f.DropStartY + 40f) f.IgnoreOneWay = null;
            f.Pos = next;
            return FlightStep.Flying;
        }

        /// <summary>Solid rectangles around a horizontal span (a copy, safe to keep).</summary>
        public List<NavRect> ObstaclesBetween(float x0, float x1) =>
            new(ObstaclesNear(Mathf.Min(x0, x1) - 400f, Mathf.Max(x0, x1) + 400f));

        /// <summary>
        /// Simulates one airborne maneuver frame by frame. The steering policy
        /// is the one <see cref="NavAgent"/> executes: full speed toward the
        /// landing x, braking so it stops over it; jump held to the apex; the
        /// second jump pressed right after the first apex.
        /// </summary>
        public SimResult Simulate(Vector2 start, float vx0, int jumps, bool dropThrough, NavSegment from, NavSegment target,
            float targetX, int maxFrames = 240, int steerDelay = 0) {
            var flight = new Flight {
                Pos = start, Vel = new Vector2(vx0, 0f), TargetX = targetX, SteerDelay = steerDelay,
                UseAirJumps = jumps >= 2, AirJumps = Mathf.Max(0, jumps - 1),
                IgnoreOneWay = dropThrough ? from : null, DropStartY = start.Y
            };
            if (jumps > 0) {
                flight.Vel.Y = -Hero.JumpSpeed;
                flight.Holding = true;
            } else if (dropThrough) {
                flight.Vel.Y = 90f;
            }
            return Fly(ref flight, target, maxFrames, ObstaclesBetween(start.X, targetX));
        }

        /// <summary>Runs a prepared flight until it lands, crashes or times out.</summary>
        public SimResult Fly(ref Flight flight, NavSegment target, int maxFrames, List<NavRect> obstacles) {
            var result = new SimResult();
            while (flight.Frame < maxFrames) {
                FlightStep step = StepFlight(ref flight, obstacles, out NavSegment landed);
                result.EndPos = flight.Pos;
                if (step == FlightStep.Crashed) {
                    result.Crashed = true;
                    return result;
                }
                if (step == FlightStep.Landed) {
                    result.LandedOn = landed;
                    if (landed == target || (target != null && landed.Platform != null && landed.Platform == target.Platform
                        && landed.PlatformEndpoint == target.PlatformEndpoint)) {
                        result.Success = true;
                        result.Frames = flight.Frame;
                        result.LandX = flight.Pos.X;
                    }
                    return result;
                }
                if (target != null && flight.Pos.Y > target.Top + 900f) return result;
            }
            return result;
        }

        /// <summary>Same steering rule the agent uses in the air.</summary>
        public static float SteerToward(float x, float vx, float targetX, float maxSpeed, float airControl) {
            float dx = targetX - x;
            if (Mathf.Abs(dx) < 6f && Mathf.Abs(vx) < 60f) return 0f;
            float dir = Mathf.Sign(dx);
            float decel = maxSpeed / HeroPhysics.AirDecelRamp * Mathf.Max(0.05f, airControl) * 60f; // px/s^2
            float stopping = vx * vx / (2f * decel);
            if (Mathf.Sign(vx) == dir && stopping >= Mathf.Abs(dx) - 4f) return 0f;
            return dir * maxSpeed;
        }

        private NavSegment LandingSurface(Vector2 from, Vector2 to, NavSegment ignore) {
            int c0 = Mathf.FloorToInt((to.X - 30f) / Cell), c1 = Mathf.FloorToInt((to.X + 30f) / Cell);
            NavSegment best = null;
            float bestTop = float.MaxValue;
            for (int c = c0; c <= c1; c++) {
                if (_segmentGrid.TryGetValue(c, out List<NavSegment> list)) {
                    foreach (NavSegment s in list) Consider(s);
                }
            }
            foreach (NavSegment s in _dynamicSegments) Consider(s);
            return best;

            void Consider(NavSegment s) {
                if (s == ignore) return;
                if (!s.Spans(to.X, HeroPhysics.BodyHalfWidth - 6f)) return;
                float top = s.TopAt(to.X);
                if (from.Y <= top + 1f && to.Y >= top - 0.5f && top < bestTop) {
                    best = s;
                    bestTop = top;
                }
            }
        }

        // =================================================================
        // Planning
        // =================================================================

        public sealed class Plan {
            public readonly List<NavEdge> Edges = new();
            public NavSegment Goal;
            public float GoalX;
            public bool ReachesGoal;
            public float Cost;
        }

        /// <summary>
        /// Dijkstra from <paramref name="start"/> to any segment that can stand
        /// under <paramref name="goal"/>. When none is reachable the plan leads to
        /// the reached segment closest to the goal (horizontal distance plus
        /// three times the vertical), flagged <see cref="Plan.ReachesGoal"/> false.
        /// </summary>
        public Plan FindPath(NavSegment start, float startX, Vector2 goal, float maxBelow = 260f, ISet<NavEdge> banned = null,
            Func<NavSegment, bool> isGoal = null) {
            var plan = new Plan();
            if (start == null) return plan;
            var goals = new HashSet<NavSegment>();
            if (isGoal != null) {
                foreach (NavSegment s in AllSegments()) if (isGoal(s)) goals.Add(s);
            } else {
                foreach (NavSegment s in AllSegments()) {
                    if (s.Platform != null || s.IsRope) continue;
                    if (!s.Spans(goal.X, 10f)) continue;
                    float dy = s.TopAt(Mathf.Clamp(goal.X, s.Left, s.Right)) - goal.Y;
                    if (dy >= -48f && dy <= maxBelow) goals.Add(s);
                }
                // Keep only the closest standing surface(s) under the point.
                float bestDy = float.MaxValue;
                foreach (NavSegment s in goals) bestDy = Mathf.Min(bestDy, s.TopAt(goal.X) - goal.Y);
                goals.RemoveWhere(s => s.TopAt(goal.X) - goal.Y > bestDy + 40f);
            }

            var dist = new Dictionary<NavSegment, float> { [start] = 0f };
            var entryX = new Dictionary<NavSegment, float> { [start] = startX };
            var via = new Dictionary<NavSegment, NavEdge>();
            var open = new PriorityQueue<NavSegment, float>();
            open.Enqueue(start, 0f);
            var closed = new HashSet<NavSegment>();
            NavSegment reachedGoal = null;
            float reachedCost = float.MaxValue;
            NavSegment closest = start;
            float closestScore = ApproachScore(start, goal);
            float speed = Mathf.Max(60f, Hero.RunSpeed);
            int expansions = 0;
            while (open.Count > 0 && expansions < 400) {
                NavSegment s = open.Dequeue();
                if (!closed.Add(s)) continue;
                expansions++;
                float d = dist[s];
                if (goals.Contains(s)) {
                    float total = d + Mathf.Abs(Mathf.Clamp(goal.X, s.Left, s.Right) - entryX[s]) / speed;
                    if (total < reachedCost) {
                        reachedCost = total;
                        reachedGoal = s;
                    }
                }
                float score = ApproachScore(s, goal);
                if (score < closestScore) {
                    closestScore = score;
                    closest = s;
                }
                if (d > reachedCost) break;
                foreach (NavEdge e in EdgesFrom(s)) {
                    if (banned != null && banned.Contains(e)) continue;
                    if (e.Kind == NavEdgeKind.Break && (e.Obstacle == null || !GodotObject.IsInstanceValid(e.Obstacle) || e.Obstacle.IsDestroyed)) continue;
                    float walk = Mathf.Abs(e.LaunchX - entryX[s]) / speed;
                    float nd = d + walk + e.Cost;
                    if (!dist.TryGetValue(e.To, out float old) || nd < old - 0.001f) {
                        dist[e.To] = nd;
                        entryX[e.To] = e.LandX;
                        via[e.To] = e;
                        open.Enqueue(e.To, nd);
                    }
                }
            }
            NavSegment end = reachedGoal ?? closest;
            plan.Goal = end;
            plan.ReachesGoal = reachedGoal != null;
            plan.GoalX = end.Clamp(goal.X, 14f);
            plan.Cost = dist.TryGetValue(end, out float c) ? c : 0f;
            var chain = new List<NavEdge>();
            NavSegment cursor = end;
            int guard = 0;
            while (cursor != start && via.TryGetValue(cursor, out NavEdge edge) && guard++ < 200) {
                chain.Add(edge);
                cursor = edge.From;
            }
            chain.Reverse();
            plan.Edges.AddRange(chain);
            return plan;
        }

        // =================================================================
        // Ropes (PendulumAnchor): catch and release, both predicted frame by
        // frame against the rope's deterministic swing.
        // =================================================================

        /// <summary>The ledge-grab point's world position <paramref name="frames"/> physics frames from now.</summary>
        public static Vector2 RopeGrabPoint(PendulumAnchor rope, int frames, out Vector2 hang) {
            LedgeGrabPoint ledge = rope.Ledge;
            Vector2 local = ledge?.Position ?? new Vector2(0f, 320f);
            Vector2 hangLocal = local + (ledge?.GetNodeOrNull<Marker2D>(ledge.HangAnchorPath)?.Position ?? new Vector2(0f, 40f));
            float phase = rope.SwingPhase + (rope.Enabled ? frames / 60f / Mathf.Max(0.01f, rope.PeriodSeconds) : 0f);
            float angle = Mathf.DegToRad(rope.AmplitudeDegrees * Mathf.Sin((phase + rope.PhaseOffset) * Mathf.Tau));
            Vector2 pivot = rope.GlobalPosition;
            hang = pivot + hangLocal.Rotated(angle);
            return pivot + local.Rotated(angle);
        }

        private static void RopeArc(PendulumAnchor rope, out float left, out float right, out float lowY, out float highY) {
            float length = rope.Ledge?.Position.Length() ?? 320f;
            float amp = Mathf.DegToRad(rope.AmplitudeDegrees);
            Vector2 pivot = rope.GlobalPosition;
            left = pivot.X - length * Mathf.Sin(amp);
            right = pivot.X + length * Mathf.Sin(amp);
            lowY = pivot.Y + length;
            highY = pivot.Y + length * Mathf.Cos(amp);
        }

        /// <summary>True when the hero's ledge detector (56x36 at feet-52) touches the rope's 56x56 grab box.</summary>
        private static bool DetectorTouches(Vector2 feet, Vector2 grab) =>
            Mathf.Abs(feet.X - grab.X) < 56f && feet.Y - 34f > grab.Y - 28f && feet.Y - 70f < grab.Y + 28f;

        /// <summary>Frames until a flight starting now catches <paramref name="rope"/> (-1 when it does not).</summary>
        public int PredictCatch(Flight flight, PendulumAnchor rope, int maxFrames = 150, PendulumAnchor leaving = null) {
            List<NavRect> obstacles = ObstaclesBetween(flight.Pos.X, rope.GlobalPosition.X);
            int start = flight.Frame;
            while (flight.Frame - start < maxFrames) {
                FlightStep step = StepFlight(ref flight, obstacles, out _);
                if (step != FlightStep.Flying) return -1;
                int t = flight.Frame - start;
                Vector2 grab = RopeGrabPoint(rope, t, out _);
                if (flight.Vel.Y >= -140f && DetectorTouches(flight.Pos, grab)) return t;
                // Every bar is solid: meeting one above its grab box (the target's
                // included) bounces the hero off or lands it on the bar.
                if (!RopeBarsStep(ref flight, t, leaving, out _)) return -1;
            }
            return -1;
        }

        /// <summary>
        /// The ropes are solid swinging bars (16 x 320, Environment layer): a
        /// flight that meets one lands on it or bounces off. Pose predicted
        /// <paramref name="frames"/> ahead.
        /// </summary>
        /// <summary>Debug: why a catch flight fails (first bar contact or crash).</summary>
        public string ExplainCatch(Flight flight, PendulumAnchor rope, int maxFrames, PendulumAnchor leaving) {
            List<NavRect> obstacles = ObstaclesBetween(flight.Pos.X, rope.GlobalPosition.X);
            int start = flight.Frame;
            Vector2 v0 = flight.Vel;
            float closest = float.MaxValue;
            while (flight.Frame - start < maxFrames) {
                FlightStep step = StepFlight(ref flight, obstacles, out _);
                int t = flight.Frame - start;
                if (step != FlightStep.Flying) return $"v0={v0} {step} t={t} at {flight.Pos}";
                Vector2 grab = RopeGrabPoint(rope, t, out _);
                closest = Mathf.Min(closest, grab.DistanceTo(flight.Pos + new Vector2(0f, -52f)));
                if (flight.Vel.Y >= -140f && DetectorTouches(flight.Pos, grab)) return $"v0={v0} catch t={t}";
                if (!RopeBarsStep(ref flight, t, leaving, out string hit))
                    return $"v0={v0} bar {hit} t={t} at {flight.Pos} v={flight.Vel} grab={grab}";
            }
            return $"v0={v0} miss closest={closest:0}";
        }

        /// <summary>
        /// One flight frame against the swinging bars. A bar the hero runs into
        /// is a crash (it bounces off or lands on it) — except the bar just let
        /// go of sweeping into the hero from behind, which pushes it along.
        /// Returns false on a crash.
        /// </summary>
        private bool RopeBarsStep(ref Flight flight, int frames, PendulumAnchor leaving, out string hit) {
            hit = null;
            foreach (PendulumAnchor rope in _ropes) {
                if (!GodotObject.IsInstanceValid(rope) || Mathf.Abs(rope.GlobalPosition.X - flight.Pos.X) > 420f) continue;
                // A hanging hero's head overlaps the bottom of the bar it lets go
                // of, and the engine pushes it clear (observed: a release never
                // bumps on the bar's lower end), so only the upper part of that
                // bar counts.
                if (rope == leaving && frames < 8) continue;
                if (!TouchesRopeBar(rope, flight.Pos, frames, rope == leaving && frames < 16 ? 100f : 28f)) continue;
                if (rope == leaving && PushedByBar(rope, ref flight, frames)) continue;
                hit = rope.Name;
                return false;
            }
            return true;
        }

        /// <summary>The bar sweeping into the hero from behind carries it: true when that is the contact.</summary>
        private static bool PushedByBar(PendulumAnchor rope, ref Flight flight, int frames) {
            Vector2 pivot = rope.GlobalPosition;
            Vector2 grab = RopeGrabPoint(rope, frames, out _);
            Vector2 next = RopeGrabPoint(rope, frames + 1, out _);
            float length = Mathf.Max(1f, (grab - pivot).Length());
            Vector2 axis = (grab - pivot) / length;
            var normal = new Vector2(-axis.Y, axis.X);
            Vector2 center = flight.Pos + new Vector2(0f, -HeroPhysics.BodyHeight / 2f);
            float r = Mathf.Clamp((center - pivot).Dot(axis), 0f, length);
            float sweep = (next - grab).Dot(normal) * 60f * (r / length); // the bar's speed across itself at the hero
            float side = (center - pivot).Dot(normal);
            if (Mathf.Abs(sweep) < 20f || Mathf.Sign(side) != Mathf.Sign(sweep)) return false;
            // Swept up into from below is a landing on the bar, not a push.
            if ((normal * Mathf.Sign(side)).Y < -0.45f) return false;
            float heroAcross = flight.Vel.Dot(normal);
            if (Mathf.Abs(heroAcross) < Mathf.Abs(sweep)) flight.Vel += normal * (sweep - heroAcross);
            float support = HeroPhysics.BodyHalfWidth * Mathf.Abs(normal.X) + HeroPhysics.BodyHeight / 2f * Mathf.Abs(normal.Y);
            float clear = Mathf.Sign(sweep) * (8f + support + 1f);
            flight.Pos += normal * (clear - side);
            return true;
        }

        private static bool TouchesRopeBar(PendulumAnchor rope, Vector2 feet, int frames, float trimBottom = 28f) {
            var box = new Rect2(feet.X - HeroPhysics.BodyHalfWidth + 1f, feet.Y - HeroPhysics.BodyHeight + 1f,
                HeroPhysics.BodyHalfWidth * 2f - 2f, HeroPhysics.BodyHeight - 2f);
            {
                Vector2 grab = RopeGrabPoint(rope, frames, out _);
                Vector2 axis = (grab - rope.GlobalPosition).Normalized();
                // The solid bar runs from the pivot to the grab point; the bottom
                // of it sits inside the grab box, where a touch is a catch.
                float length = Mathf.Max(20f, (grab - rope.GlobalPosition).Length() - trimBottom);
                var bar = new NavRect {
                    Center = rope.GlobalPosition + axis * (length / 2f),
                    AxisY = axis, AxisX = new Vector2(-axis.Y, axis.X),
                    Half = new Vector2(8f, length / 2f),
                    AxisAligned = false
                };
                bar.RecomputeAabb();
                return bar.Overlaps(box);
            }
        }

        /// <summary>A release / jump flight that also treats the swinging bars as obstacles.</summary>
        public SimResult FlyAmongRopes(ref Flight flight, NavSegment target, int maxFrames, List<NavRect> obstacles, PendulumAnchor leaving = null) {
            var result = new SimResult();
            int start = flight.Frame;
            while (flight.Frame - start < maxFrames) {
                FlightStep step = StepFlight(ref flight, obstacles, out NavSegment landed);
                result.EndPos = flight.Pos;
                if (step == FlightStep.Crashed || !RopeBarsStep(ref flight, flight.Frame - start, leaving, out _)) {
                    result.Crashed = true;
                    return result;
                }
                if (step == FlightStep.Landed) {
                    result.LandedOn = landed;
                    if (landed == target) {
                        result.Success = true;
                        result.Frames = flight.Frame - start;
                        result.LandX = flight.Pos.X;
                    }
                    return result;
                }
                if (target != null && flight.Pos.Y > target.Top + 900f) return result;
            }
            return result;
        }

        /// <summary>The flight a jump-release from the rope right now would start (hang point, jump + swing launch).</summary>
        public Flight ReleaseFlight(PendulumAnchor rope, PlayerController player, float targetX) {
            Vector2 launch = rope.AnchorVelocity * rope.ReleaseLaunchAssist;
            if (launch.Length() > rope.MaxLaunchSpeed) launch = launch.Normalized() * rope.MaxLaunchSpeed;
            float jump = (player.Data?.MaxJumpForce ?? 14f) * player.StoryJumpForceMultiplier * player.StatusJumpMultiplier * 45f;
            return new Flight {
                Pos = player.GlobalPosition,
                Vel = new Vector2(launch.X, -jump + launch.Y),
                Holding = true,
                AirJumps = Mathf.Max(0, player.RemainingJumps),
                UseAirJumps = true,
                TargetX = targetX
            };
        }

        private void AddCatchEdge(NavSegment a, NavSegment rope, List<NavEdge> edges) {
            if (a.IsDynamic || a.Platform != null) return;
            RopeArc(rope.Rope, out float left, out float right, out float lowY, out float highY);
            float gap = Mathf.Max(0f, Mathf.Max(left - a.Right, a.Left - right));
            if (gap > 260f) return;
            float rise = a.Top - (highY + 52f);           // feet must reach the grab box
            if (rise > MaxRise(1f) + 20f || a.Top < lowY - 260f) return;
            float launch = left >= a.Right - 10f ? a.Right - 22f : right <= a.Left + 10f ? a.Left + 22f : a.Clamp(rope.Rope.GlobalPosition.X, 22f);
            edges.Add(new NavEdge {
                From = a, To = rope, Kind = NavEdgeKind.Catch, LaunchX = launch, LandX = rope.Rope.GlobalPosition.X,
                Jumps = 1, BaseCost = 2.5f
            });
        }

        private List<NavEdge> RopeReleaseEdges(NavSegment rope) {
            if (_edgeCache.TryGetValue(rope, out List<NavEdge> cached)) return cached;
            var edges = new List<NavEdge>();
            RopeArc(rope.Rope, out float left, out float right, out float lowY, out float highY);
            foreach (NavSegment b in _staticSegments) {
                if (b == rope || b.IsDynamic) continue;
                if (b.IsRope) {
                    float pivotGap = Mathf.Abs(b.Rope.GlobalPosition.X - rope.Rope.GlobalPosition.X);
                    if (pivotGap > 40f && pivotGap <= 900f && Mathf.Abs(b.Rope.GlobalPosition.Y - rope.Rope.GlobalPosition.Y) < 260f) {
                        edges.Add(new NavEdge {
                            From = rope, To = b, Kind = NavEdgeKind.Release, LaunchX = rope.Rope.GlobalPosition.X,
                            LandX = b.Rope.GlobalPosition.X, BaseCost = 2.5f
                        });
                    }
                    continue;
                }
                float gap = Mathf.Max(0f, Mathf.Max(b.Left - right, left - b.Right));
                if (gap > 700f || b.Top < highY - 220f || b.Top > lowY + 700f) continue;
                float land = b.Clamp(rope.Rope.GlobalPosition.X < b.Left ? b.Left + 60f : b.Right - 60f, 26f);
                edges.Add(new NavEdge {
                    From = rope, To = b, Kind = NavEdgeKind.Release, LaunchX = rope.Rope.GlobalPosition.X,
                    LandX = land, BaseCost = 2f + gap / 600f
                });
            }
            _edgeCache[rope] = edges;
            return edges;
        }

        /// <summary>
        /// True when a jump launched now along <paramref name="edge"/> lands (or
        /// catches a rope on the way) without running into a swinging rope's
        /// solid bar. Ropes swing through the gaps between galleries, so such a
        /// jump has to be timed.
        /// </summary>
        public bool JumpClearsRopes(Vector2 start, float vx0, NavEdge edge) => JumpClearsRopes(start, vx0, edge, out _);

        public bool JumpClearsRopes(Vector2 start, float vx0, NavEdge edge, out PendulumAnchor blocking) {
            blocking = null;
            if (_ropes.Count == 0 || edge.Jumps <= 0) return true;
            float lo = Mathf.Min(start.X, edge.LandX) - 80f;
            float hi = Mathf.Max(start.X, edge.LandX) + 80f;
            var near = new List<PendulumAnchor>();
            foreach (PendulumAnchor rope in _ropes) {
                if (!GodotObject.IsInstanceValid(rope)) continue;
                RopeArc(rope, out float left, out float right, out float _, out float _);
                if (right >= lo && left <= hi) near.Add(rope);
            }
            if (near.Count == 0) return true;
            var flight = new Flight {
                Pos = start, Vel = new Vector2(vx0, -Hero.JumpSpeed), Holding = true, TargetX = edge.LandX,
                SteerDelay = edge.SteerDelay, UseAirJumps = edge.Jumps >= 2, AirJumps = Mathf.Max(0, edge.Jumps - 1),
                DropStartY = start.Y
            };
            List<NavRect> obstacles = ObstaclesBetween(start.X, edge.LandX);
            while (flight.Frame < 240) {
                FlightStep step = StepFlight(ref flight, obstacles, out _);
                if (step != FlightStep.Flying) return true;
                // Only the solid bar spoils the jump: a grab box met on the way
                // is a catch, and a hang is handled like any other rope.
                foreach (PendulumAnchor rope in near) {
                    Vector2 grab = RopeGrabPoint(rope, flight.Frame, out _);
                    if (flight.Vel.Y >= -150f && DetectorTouches(flight.Pos, grab)) return true;
                    if (TouchesRopeBar(rope, flight.Pos, flight.Frame)) {
                        blocking = rope;
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>The catch edge from <paramref name="from"/> onto <paramref name="rope"/>, if the graph has one.</summary>
        public NavEdge CatchEdge(NavSegment from, PendulumAnchor rope) {
            if (from == null || rope == null) return null;
            foreach (NavEdge e in EdgesFrom(from)) if (e.Kind == NavEdgeKind.Catch && e.To.Rope == rope) return e;
            return null;
        }

        /// <summary>The swinging rope bar the hero is standing on, if any.</summary>
        public PendulumAnchor StandingOnRope(PlayerController player) {
            int count = player.GetSlideCollisionCount();
            for (int i = 0; i < count; i++) {
                KinematicCollision2D collision = player.GetSlideCollision(i);
                if (collision != null && collision.GetNormal().Y < -0.5f && collision.GetCollider() is PendulumAnchor rope) return rope;
            }
            // The slide report can miss a frame: also accept feet resting on a bar.
            if (!player.IsOnFloor()) return null;
            Vector2 feet = player.GlobalPosition;
            foreach (PendulumAnchor rope in _ropes) {
                if (!GodotObject.IsInstanceValid(rope) || Mathf.Abs(rope.GlobalPosition.X - feet.X) > 340f) continue;
                Vector2 pivot = rope.GlobalPosition;
                Vector2 grab = RopeGrabPoint(rope, 0, out _);
                Vector2 closest = Geometry2D.GetClosestPointToSegment(feet, pivot, grab);
                if (closest.DistanceTo(feet) < 34f && closest.Y >= feet.Y - 4f) return rope;
            }
            return null;
        }

        /// <summary>The nearest other rope between <paramref name="from"/> and the goal side (null if none within reach).</summary>
        public PendulumAnchor NextRopeToward(PendulumAnchor from, Vector2 goal) {
            PendulumAnchor best = null;
            float bestDistance = 1000f;
            float dir = Mathf.Sign(goal.X - from.GlobalPosition.X);
            foreach (PendulumAnchor rope in _ropes) {
                if (rope == from || !GodotObject.IsInstanceValid(rope)) continue;
                float dx = rope.GlobalPosition.X - from.GlobalPosition.X;
                if (Mathf.Sign(dx) != dir) continue;
                if (Mathf.Abs(dx) < bestDistance) {
                    bestDistance = Mathf.Abs(dx);
                    best = rope;
                }
            }
            return best;
        }

        /// <summary>The rope the hero is hanging on, if any.</summary>
        public PendulumAnchor HangingRope(PlayerController player) {
            foreach (PendulumAnchor rope in _ropes) {
                if (GodotObject.IsInstanceValid(rope) && rope.Occupant == player) return rope;
            }
            return null;
        }

        /// <summary>The rope pseudo-segment for a rope (null when the rope is not in the graph).</summary>
        public NavSegment RopeSegment(PendulumAnchor rope) {
            foreach (NavSegment s in _staticSegments) if (s.Rope == rope) return s;
            return null;
        }

        /// <summary>Human-readable dump of every segment and its edges (debugging aid, --nav-dump).</summary>
        public string Dump() {
            var text = new System.Text.StringBuilder();
            text.AppendLine($"hero jump={Hero.JumpSpeed:0} g/frame={Hero.GravityPerFrame:0.00} run={Hero.RunSpeed:0} air={Hero.AirControl:0.00} jumps={Hero.MaxJumps} rise1={Hero.SingleRise(1f):0}");
            text.AppendLine($"obstacles={_obstacles.Count} static segments={_staticSegments.Count} dynamic={_dynamicSegments.Count} spikes={_spikes.Count}");
            foreach (NavRect o in _obstacles) {
                text.AppendLine($"  obstacle {o.Body?.Name} {o.Kind} aabb=({o.Aabb.Position.X:0},{o.Aabb.Position.Y:0})..({o.Aabb.End.X:0},{o.Aabb.End.Y:0})");
            }
            foreach (NavSegment s in AllSegments()) {
                text.AppendLine($"segment #{s.Id} {s} body={s.Body?.Name} kind={s.Kind}{(s.LeftBreakable != null ? " L:" + s.LeftBreakable.Name : "")}{(s.RightBreakable != null ? " R:" + s.RightBreakable.Name : "")}");
                foreach (NavEdge e in EdgesFrom(s)) text.AppendLine($"    {e} cost={e.Cost:0.00} frames={e.Frames}");
            }
            return text.ToString();
        }

        private static float ApproachScore(NavSegment s, Vector2 goal) {
            float x = Mathf.Clamp(goal.X, s.Left, s.Right);
            return Mathf.Abs(goal.X - x) + 3f * Mathf.Abs(s.TopAt(x) - goal.Y);
        }
    }

    /// <summary>
    /// Turns a nav plan into pad input: walk to each edge's launch point,
    /// perform the edge (jump, double jump, walk off, drop through, break,
    /// ride), steer to the landing point, and replan whenever the hero ends
    /// up somewhere the plan did not expect.
    /// </summary>
    public sealed class NavAgent {
        private readonly PlaytestNav _nav;
        public NavAgent(PlaytestNav nav) => _nav = nav;

        private PlaytestNav.Plan _plan;
        private int _index;
        private Vector2 _goal;
        private int _planFrame = -1000;
        private NavEdge _air;          // aerial edge being executed (set at launch)
        private bool _airborneSeen;    // the launch actually left the ground
        private int _launchFrame;
        private int _airFrames;
        private int _jumpsPressed;
        private bool _jumpHeld;
        private int _dropFrame = -1;
        private int _stuckFrame;
        private float _stuckX;
        private int _pushFrames;
        private float _lastMoveX;

        /// <summary>The horizontal input the bot finally sent last frame (pushing detection).</summary>
        public void NoteSentMoveX(float moveX) => _lastMoveX = moveX;
        private readonly HashSet<NavEdge> _banned = new();
        private int _bannedClearFrame;
        public NavSegment Current { get; private set; }
        public string Description { get; private set; } = "";
        public bool GoalReachable => _plan?.ReachesGoal ?? false;
        public NavEdge NextEdge => _plan != null && _index < _plan.Edges.Count ? _plan.Edges[_index] : null;
        /// <summary>The obstacle the agent is breaking this frame (Break edge), for the bot to attack.</summary>
        public DamageableEnvironmentObject BreakTarget { get; private set; }
        public int Replans { get; private set; }

        /// <summary>Notable maneuver decisions (rope releases), drained into the report by the runner.</summary>
        public readonly List<string> Notes = new();

        /// <summary>Forget the plan (goal kinds changed, a door opened, ...).</summary>
        public void Reset() {
            _plan = null;
            _air = null;
            _airborneSeen = false;
            _dropFrame = -1;
        }

        /// <summary>
        /// Plans (or reuses a plan) to <paramref name="goal"/> and drives toward
        /// it. Returns true while still travelling, false once standing within
        /// <paramref name="arriveRadius"/> of the goal on its surface.
        /// </summary>
        public bool Drive(PlayerController player, Vector2 goal, ref BotIntent intent, float arriveRadius = 40f,
            float maxBelow = 260f, Func<NavSegment, bool> isGoal = null) {
            BreakTarget = null;
            Vector2 feet = player.GlobalPosition;
            bool grounded = player.IsOnFloor();
            int frame = _nav.Frame;
            if (frame - _bannedClearFrame > 1800) {
                _banned.Clear();
                _bannedClearFrame = frame;
            }

            if (player.CurrentState == CharacterState.LedgeHanging) {
                _goal = goal;
                _maxBelow = maxBelow;
                if (HandleHang(player, ref intent)) return true;
                // A static ledge: let go.
                Description = "ledge";
                if ((frame & 15) == 0) intent.Tap |= GameplayButtons.Jump;
                return true;
            }

            // --- airborne: execute the active aerial edge ---
            if (!grounded) {
                Current = null;
                if (_dropFrame >= 0 && frame - _dropFrame < 30) {
                    Description = "drop";
                    if (_air != null) intent.MoveX = SteerInput(player, _air.LandX);
                    return true;
                }
                if (_air != null) {
                    _airborneSeen = true;
                    _airFrames++;
                    Description = $"air {_air}";
                    intent.MoveX = _airFrames <= _air.SteerDelay ? 0f : SteerInput(player, _air.LandX);
                    // Jump hold to the apex, then the second jump.
                    if (_air.Kind is NavEdgeKind.Jump or NavEdgeKind.Catch or NavEdgeKind.Release) {
                        if (_jumpHeld && player.Velocity.Y >= 0f) _jumpHeld = false;
                        else if (!_jumpHeld && _jumpsPressed < _flightJumps && player.Velocity.Y > -40f && _airFrames > 2) {
                            _jumpHeld = true;
                            _jumpsPressed++;
                        }
                        if (_jumpHeld) intent.Held |= GameplayButtons.Jump;
                    }
                    // Falling well past the target: use a spare jump to recover.
                    if (_air.Kind is NavEdgeKind.Fall or NavEdgeKind.Drop && player.Velocity.Y > 120f && feet.Y > _air.To.Top + 40f
                        && player.RemainingJumps > 0 && _air.To.Top < feet.Y) {
                        intent.Tap |= GameplayButtons.Jump;
                    }
                    if (_airFrames > 400) _air = null;
                    return true;
                }
                // Airborne with no plan (knocked back, fell): steer to the goal.
                Description = "air";
                intent.MoveX = SteerInput(player, goal.X);
                return true;
            }

            // --- grounded ---
            // Standing on a swinging rope's bar (a release that met the bar
            // again): jump on toward the next rope when the jump catches it.
            PendulumAnchor bar = _nav.StandingOnRope(player);
            if (bar != null) {
                if (_barRope != bar) {
                    _barRope = bar;
                    _barStart = frame;
                }
                PendulumAnchor next = _nav.NextRopeToward(bar, goal);
                Description = $"bar {bar.Name}->{next?.Name}";
                intent.MoveX = 0f;
                if (next != null) {
                    for (int jumps = 1; jumps <= 2; jumps++) {
                        var flight = new PlaytestNav.Flight {
                            Pos = feet, Vel = new Vector2(player.Velocity.X, -_nav.Hero.JumpSpeed), Holding = true,
                            AirJumps = jumps - 1, UseAirJumps = jumps > 1, TargetX = next.GlobalPosition.X
                        };
                        if (_nav.PredictCatch(flight, next, 150, bar) >= 0) {
                            NavSegment target = _nav.RopeSegment(next);
                            var hop = new NavEdge {
                                From = null, To = target, Kind = NavEdgeKind.Catch, LaunchX = feet.X,
                                LandX = next.GlobalPosition.X, Jumps = jumps
                            };
                            StartJump(hop, ref intent, jumps);
                            Notes.Add($"bar-jump {bar.Name}->{next.Name} jumps={jumps}");
                            return true;
                        }
                    }
                }
                // No jump on: walk off the bar's free end and fall past its own
                // grab, which catches (the rope then carries on as a hang).
                Vector2 grabEnd = PlaytestNav.RopeGrabPoint(bar, 0, out _);
                intent.MoveX = Mathf.Abs(grabEnd.X - feet.X) > 6f ? Mathf.Sign(grabEnd.X - feet.X) : Mathf.Sign(grabEnd.X - bar.GlobalPosition.X);
                if (intent.MoveX == 0f) intent.MoveX = Mathf.Sign(goal.X - feet.X);
                if (frame - _barStart > 150) {
                    intent.Held |= GameplayButtons.Jump;
                    intent.MoveX = Mathf.Sign(goal.X - feet.X);
                }
                return true;
            }
            _barRope = null;
            NavSegment here = _nav.Locate(player);
            Current = here;
            if (_air != null && !_airborneSeen && frame - _launchFrame > 20 && _dropFrame < 0) {
                _air = null; // the launch never left the ground (busy state); just retry
            }
            if (_air != null && _airborneSeen) {
                // Landed: did we land where the edge said?
                NavEdge finished = _air;
                _airborneSeen = false;
                _air = null;
                _dropFrame = -1;
                bool onTarget = here != null && (here == finished.To || here == _expectedLanding
                    || (finished.To.Platform != null && here.Platform == finished.To.Platform)
                    || (here.Body != null && here.Body == finished.To.Body && Mathf.Abs(here.Top - finished.To.Top) < 4f));
                if (onTarget) {
                    if (_plan != null && _index < _plan.Edges.Count && _plan.Edges[_index] == finished) _index++;
                } else {
                    finished.Failures++;
                    if (finished.Failures >= 3) _banned.Add(finished);
                    _plan = null;
                }
            }

            bool goalMoved = _plan == null || goal.DistanceTo(_goal) > 64f;
            bool offPlan = _plan != null && _index < _plan.Edges.Count && here != null
                && _plan.Edges[_index].From != here
                && !(here.Platform != null && _plan.Edges[_index].From.Platform == here.Platform);
            _maxBelow = maxBelow;
            if (here != null && (goalMoved || offPlan || frame - _planFrame > 45)) {
                _goal = goal;
                _plan = _nav.FindPath(here, feet.X, goal, maxBelow, _banned, isGoal);
                _index = 0;
                _planFrame = frame;
                Replans++;
            }
            if (_plan == null || here == null) {
                Description = "noplan";
                intent.MoveX = Mathf.Abs(goal.X - feet.X) > arriveRadius ? Mathf.Sign(goal.X - feet.X) : 0f;
                return true;
            }

            // Pushing into something the graph does not know (a prop, an
            // enemy body): hop after a third of a second.
            if (Mathf.Abs(player.Velocity.X) < 20f && Mathf.Abs(_lastMoveX) > 0.5f && grounded) {
                if (++_pushFrames > 20) {
                    _pushFrames = 0;
                    intent.Held |= GameplayButtons.Jump;
                    intent.MoveX = _lastMoveX;
                    Description = "hop";
                    return true;
                }
            } else {
                _pushFrames = 0;
            }
            // Stuck watchdog: no horizontal progress for 3 s while trying to move.
            if (Mathf.Abs(feet.X - _stuckX) > 30f) {
                _stuckX = feet.X;
                _stuckFrame = frame;
            } else if (frame - _stuckFrame > 180) {
                _stuckFrame = frame;
                if (_index < _plan.Edges.Count) {
                    NavEdge blocked = _plan.Edges[_index];
                    blocked.Failures += 2;
                    if (blocked.Failures >= 4) _banned.Add(blocked);
                }
                _plan = null;
                intent.Tap |= GameplayButtons.Jump;
                Description = "unstick";
                return true;
            }

            if (_index >= _plan.Edges.Count) {
                // On the final segment: walk to the goal x.
                float targetX = _plan.Goal == here ? here.Clamp(goal.X, 14f) : goal.X;
                float dx = targetX - feet.X;
                Description = _plan.ReachesGoal ? "final" : "closest";
                if (Mathf.Abs(dx) <= arriveRadius) {
                    _stuckFrame = frame;
                    return !_plan.ReachesGoal ? false : false;
                }
                intent.MoveX = GroundInput(player, targetX);
                return true;
            }

            NavEdge edge = _plan.Edges[_index];
            Description = edge.ToString();
            // Riding: stand on the platform until it reaches the next endpoint.
            if (edge.Kind == NavEdgeKind.Ride) {
                PathMovingPlatform platform = edge.From.Platform;
                if (_nav.PlatformEndpoint(platform, edge.To.PlatformEndpoint, out Vector2 dest)
                    && platform.GlobalPosition.DistanceTo(dest) < 10f) {
                    _index++;
                    return true;
                }
                intent.MoveX = GroundInput(player, platform.GlobalPosition.X, 10f);
                _stuckFrame = frame;
                return true;
            }
            // Leaving a platform endpoint: wait until the platform is there.
            if (edge.From.Platform != null) {
                if (!_nav.PlatformEndpoint(edge.From.Platform, edge.From.PlatformEndpoint, out Vector2 at)
                    || edge.From.Platform.GlobalPosition.DistanceTo(at) > 12f) {
                    intent.MoveX = GroundInput(player, edge.From.Platform.GlobalPosition.X, 10f);
                    _stuckFrame = frame;
                    Description = "wait-platform";
                    return true;
                }
            }
            // Boarding a platform endpoint: wait at the launch until it is there.
            if (edge.To.Platform != null) {
                bool there = _nav.PlatformEndpoint(edge.To.Platform, edge.To.PlatformEndpoint, out Vector2 at)
                    && edge.To.Platform.GlobalPosition.DistanceTo(at) < 24f;
                if (!there) {
                    // Never wait under a lift's landing footprint: a lift that
                    // descends onto the hero can crush it through the floor.
                    float waitX = edge.LaunchX;
                    NavSegment from = edge.From;
                    bool under = from != null && from.Left < edge.To.Right && from.Right > edge.To.Left && from.Top >= edge.To.Top - 4f;
                    if (under && waitX > edge.To.Left - 30f && waitX < edge.To.Right + 30f) {
                        float left = edge.To.Left - 34f, right = edge.To.Right + 34f;
                        bool leftOk = left >= from.Left + 16f, rightOk = right <= from.Right - 16f;
                        if (leftOk && (!rightOk || feet.X < (edge.To.Left + edge.To.Right) / 2f)) waitX = left;
                        else if (rightOk) waitX = right;
                    }
                    intent.MoveX = GroundInput(player, waitX, 8f);
                    _stuckFrame = frame;
                    Description = "wait-board";
                    return true;
                }
            }

            switch (edge.Kind) {
                case NavEdgeKind.Walk:
                    intent.MoveX = Mathf.Sign(edge.LandX - feet.X);
                    if (intent.MoveX == 0f) intent.MoveX = 1f;
                    return true;
                case NavEdgeKind.Break:
                    if (edge.Obstacle == null || !GodotObject.IsInstanceValid(edge.Obstacle) || edge.Obstacle.IsDestroyed) {
                        _plan = null;
                        return true;
                    }
                    if (Mathf.Abs(edge.LaunchX - feet.X) > 60f) {
                        intent.MoveX = GroundInput(player, edge.LaunchX);
                        return true;
                    }
                    BreakTarget = edge.Obstacle;
                    _stuckFrame = frame;
                    return true;
                case NavEdgeKind.Fall:
                    intent.MoveX = Mathf.Sign(edge.LaunchX - (edge.From.Left + edge.From.Right) / 2f);
                    if (Mathf.Abs(feet.X - edge.LaunchX) > 40f && Mathf.Sign(edge.LaunchX - feet.X) != intent.MoveX) {
                        intent.MoveX = Mathf.Sign(edge.LaunchX - feet.X);
                    }
                    if (_air != edge) {
                        _air = edge;
                        _airborneSeen = false;
                        _launchFrame = frame + 100000; // walking to the edge: never times out
                        _airFrames = 0;
                        _jumpsPressed = 0;
                        _jumpHeld = false;
                    }
                    return true;
                case NavEdgeKind.Drop:
                    if (Mathf.Abs(edge.LaunchX - feet.X) > 14f && _dropFrame < 0) {
                        intent.MoveX = GroundInput(player, edge.LaunchX, 8f);
                        return true;
                    }
                    return DriveDrop(player, edge, ref intent, frame);
                case NavEdgeKind.Catch: {
                    if (Mathf.Abs(edge.LaunchX - feet.X) > 12f) {
                        intent.MoveX = GroundInput(player, edge.LaunchX, 6f);
                        return true;
                    }
                    _stuckFrame = frame; // waiting for the swing is not being stuck
                    Description = "wait-rope " + edge;
                    for (int jumps = 1; jumps <= Mathf.Clamp(_nav.Hero.MaxJumps, 1, 2); jumps++) {
                        var flight = new PlaytestNav.Flight {
                            Pos = feet, Vel = new Vector2(player.Velocity.X, -_nav.Hero.JumpSpeed), Holding = true,
                            AirJumps = jumps - 1, UseAirJumps = jumps > 1, TargetX = edge.LandX
                        };
                        if (_nav.PredictCatch(flight, edge.To.Rope) >= 0) {
                            StartJump(edge, ref intent, jumps);
                            return true;
                        }
                    }
                    if (frame - _catchWaitStart > 600 && _catchWaitEdge == edge) {
                        edge.Failures += 3;
                        _banned.Add(edge);
                        _plan = null;
                    }
                    if (_catchWaitEdge != edge) {
                        _catchWaitEdge = edge;
                        _catchWaitStart = frame;
                    }
                    return true;
                }
                case NavEdgeKind.Jump:
                default:
                    float distance = Mathf.Abs(edge.LaunchX - feet.X);
                    if (distance > 12f) {
                        // Take a running jump if the simulation says it still lands.
                        if (distance < 200f && Mathf.Abs(player.Velocity.X) > 60f && edge.SteerDelay == 0
                            && Mathf.Sign(player.Velocity.X) == Mathf.Sign(edge.LandX - feet.X)) {
                            PlaytestNav.SimResult r = _nav.Simulate(feet, player.Velocity.X, edge.Jumps, false, edge.From, edge.To, edge.LandX);
                            if (r.Success && _nav.JumpClearsRopes(feet, player.Velocity.X, edge)) {
                                StartJump(edge, ref intent);
                                return true;
                            }
                        }
                        intent.MoveX = GroundInput(player, edge.LaunchX, 6f);
                        return true;
                    }
                    if (Mathf.Abs(player.Velocity.X) > 90f && Mathf.Sign(player.Velocity.X) != Mathf.Sign(edge.LandX - feet.X)) {
                        intent.MoveX = 0f; // settle before a precise jump
                        return true;
                    }
                    // A swinging rope crosses the jump: wait for the gap (a few
                    // swings at most, then let the planner look elsewhere).
                    if (!_nav.JumpClearsRopes(feet, player.Velocity.X, edge, out PendulumAnchor blocking)) {
                        if (_ropeWaitEdge != edge) {
                            _ropeWaitEdge = edge;
                            _ropeWaitStart = frame;
                        }
                        if (frame - _ropeWaitStart < 150) {
                            _stuckFrame = frame;
                            Description = "wait-swing " + edge;
                            intent.MoveX = 0f;
                            return true;
                        }
                        _ropeWaitEdge = null;
                        // No clean window in a swing and a half: the rope in the
                        // way is the way across - catch it, and let the hang pick
                        // the best release. Without a catch edge, jump anyway.
                        NavEdge viaRope = _nav.CatchEdge(edge.From, blocking);
                        if (viaRope != null && _plan != null && _index < _plan.Edges.Count && _plan.Edges[_index] == edge) {
                            Notes.Add($"no rope gap for {edge} after 150f; catching {blocking.Name} instead");
                            _plan.Edges[_index] = viaRope;
                            edge.Failures += 1;
                            return true;
                        }
                        Notes.Add($"no rope gap for {edge} after 150f; jumping");
                    }
                    StartJump(edge, ref intent);
                    return true;
            }
        }

        private void StartJump(NavEdge edge, ref BotIntent intent, int flightJumps = -1) {
            _air = edge;
            _expectedLanding = null;
            _airborneSeen = false;
            _launchFrame = _nav.Frame;
            _airFrames = 0;
            _flightJumps = flightJumps > 0 ? flightJumps : edge.Jumps;
            _jumpsPressed = 1;
            _jumpHeld = true;
            intent.Held |= GameplayButtons.Jump;
            intent.MoveX = edge.SteerDelay > 0 ? 0f : Mathf.Sign(edge.LandX - edge.LaunchX);
        }

        /// <summary>Debug: note why each planned rope-to-rope release is refused.</summary>
        public static bool DebugRopes;
        private int _flightJumps = 1;
        private int _releaseJumps = 1;
        private NavEdge _ropeWaitEdge;
        private readonly Dictionary<NavSegment, float> _remainingCost = new();
        private NavSegment _expectedLanding;
        private int _costHangStart = -1;
        private int _ropeWaitStart;
        private float _maxBelow = 260f;
        private PendulumAnchor _hangRope;
        private PendulumAnchor _barRope;
        private int _barStart;
        private int _hangStart;
        private NavEdge _catchWaitEdge;
        private int _catchWaitStart;

        /// <summary>
        /// Hanging on a swinging rope: plan from the rope and let go on the
        /// frame the predicted flight (jump + the swing's launch) lands on the
        /// next surface or catches the next rope. A rope grabbed by accident
        /// mid-jump is handled the same way. Returns false for a static ledge.
        /// </summary>
        public bool HandleHang(PlayerController player, ref BotIntent intent) {
            PendulumAnchor rope = _nav.HangingRope(player);
            if (rope == null) return false;
            int frame = _nav.Frame;
            if (_hangRope != rope) {
                _hangRope = rope;
                _hangStart = frame;
            }
            NavSegment node = _nav.RopeSegment(rope);
            Current = node;
            if (_air != null) {
                if (_air.To == node && _plan != null && _index < _plan.Edges.Count && _plan.Edges[_index] == _air) _index++;
                _air = null;
                _airborneSeen = false;
            }
            intent.MoveX = 0f;
            int hung = frame - _hangStart;
            if (node == null) {
                if ((frame & 1) == 0) intent.Tap |= GameplayButtons.Jump;
                return true;
            }
            if (_plan == null || _index >= _plan.Edges.Count || _plan.Edges[_index].From != node || frame - _planFrame > 30) {
                _plan = _nav.FindPath(node, player.GlobalPosition.X, _goal, _maxBelow, _banned);
                _index = 0;
                _planFrame = frame;
                Replans++;
            }
            NavEdge edge = _plan != null && _index < _plan.Edges.Count ? _plan.Edges[_index] : null;
            Description = "hang " + (edge?.ToString() ?? "(no plan)");
            bool release = false;
            NavEdge chosen = null;
            if (_costHangStart != _hangStart) {
                _remainingCost.Clear();
                _costHangStart = _hangStart;
            }
            // Every release from this rope is predicted; each is scored by the
            // cheapest remaining route from wherever it actually lands. Let go
            // when one is as good as the plan, or (after a swing) when one makes
            // progress, or (before the 5 s auto-drop) when anything lands at all.
            var candidates = new List<NavEdge>();
            if (edge != null && edge.From == node) candidates.Add(edge);
            foreach (NavEdge other in _nav.EdgesFrom(node)) if (other != edge && !_banned.Contains(other)) candidates.Add(other);
            float plannedScore = edge != null && edge.From == node ? RemainingCost(edge.To) : float.MaxValue;
            float hereScore = RemainingCost(node);
            if (DebugRopes && edge != null && hung % 6 == 0 && hung < 160) {
                PlaytestNav.Flight debugFlight = _nav.ReleaseFlight(rope, player, edge.LandX);
                if (edge.To.IsRope) {
                    Notes.Add($"why h={hung} {rope.Name}@{player.GlobalPosition} -> {_nav.ExplainCatch(debugFlight, edge.To.Rope, 150, rope)}");
                } else {
                    debugFlight.AirJumps = Mathf.Max(0, player.RemainingJumps);
                    debugFlight.UseAirJumps = true;
                    PlaytestNav.SimResult r = _nav.FlyAmongRopes(ref debugFlight, edge.To, 240,
                        _nav.ObstaclesBetween(player.GlobalPosition.X, edge.LandX), rope);
                    Notes.Add($"why h={hung} {rope.Name}@{player.GlobalPosition} -> {edge.To} ok={r.Success} crash={r.Crashed} landed={r.LandedOn} end={r.EndPos} v={debugFlight.Vel}");
                }
            }
            float bestScore = float.MaxValue;
            int bestFrames = -1, bestJumps = 1;
            NavSegment bestLanding = null;
            foreach (NavEdge candidate in candidates) {
                int frames = PredictRelease(player, rope, candidate, out NavSegment landed, out int jumps);
                if (frames < 0 || landed == null) continue;
                float score = RemainingCost(landed);
                if (score < bestScore) {
                    bestScore = score;
                    bestFrames = frames;
                    bestJumps = jumps;
                    bestLanding = landed;
                    chosen = candidate;
                }
            }
            if (chosen != null) {
                release = bestScore <= plannedScore + 1f
                    || (hung > 120 && bestScore < hereScore - 0.5f)
                    || hung > 230;
            }
            if (release) {
                _releaseJumps = bestJumps;
                _expectedLanding = bestLanding;
                if (chosen != edge || bestLanding != chosen.To) _plan = null; // re-plan from the landing
                Notes.Add($"release {rope.Name}->{bestLanding} via {chosen.To} predicted={bestFrames}f jumps={bestJumps}/{player.RemainingJumps} hung={hung}f score={bestScore:0.0} plan={(plannedScore < 1e6f ? plannedScore.ToString("0.0") : "-")} here={hereScore:0.0}");
            } else {
                chosen = null;
            }
            if (!release && hung > 270) {
                release = true; // the hang auto-drops at 5 s anyway
                Notes.Add($"release {rope.Name} forced hung={hung}f");
            }
            if (release) {
                edge = chosen;
                intent.Tap |= GameplayButtons.Jump;
                _air = edge;
                _airborneSeen = false;
                _launchFrame = frame;
                _airFrames = 0;
                _jumpHeld = true;
                _jumpsPressed = 1;
                _flightJumps = chosen != null ? _releaseJumps : 1 + Mathf.Max(0, player.RemainingJumps);
                _hangRope = null;
            }
            return true;
        }

        /// <summary>Cheapest remaining route cost from a segment to the goal (cached per hang).</summary>
        private float RemainingCost(NavSegment segment) {
            if (segment == null) return float.MaxValue;
            if (_remainingCost.TryGetValue(segment, out float cached)) return cached;
            float x = segment.IsRope ? segment.Rope.GlobalPosition.X : (segment.Left + segment.Right) / 2f;
            PlaytestNav.Plan rest = _nav.FindPath(segment, x, _goal, _maxBelow, _banned);
            float cost = rest.ReachesGoal ? rest.Cost
                : 10000f + Mathf.Abs(x - _goal.X) + 3f * Mathf.Abs(segment.Top - _goal.Y);
            _remainingCost[segment] = cost;
            return cost;
        }

        /// <summary>
        /// Frames until a jump-release now lands (-1 when it crashes or lands
        /// nowhere useful), with the surface it lands on - the edge's target, or
        /// any other solid surface on the way - and the jumps it uses.
        /// </summary>
        private int PredictRelease(PlayerController player, PendulumAnchor rope, NavEdge edge, out NavSegment landedOn, out int jumpsUsed) {
            landedOn = null;
            jumpsUsed = 1;
            PlaytestNav.Flight flight = _nav.ReleaseFlight(rope, player, edge.LandX);
            // The rope is a solid swinging bar: only let go while it carries the
            // hero toward the target, so the hero flies out ahead of it.
            float toward = Mathf.Sign(edge.LandX - player.GlobalPosition.X);
            bool forward = Mathf.Abs(edge.LandX - player.GlobalPosition.X) < 40f
                || (Mathf.Sign(flight.Vel.X) == toward && Mathf.Abs(flight.Vel.X) > 120f);
            if (!forward) return -1;
            // The single jump first (a double jump usually sails over the next
            // grab), then the double; the better landing wins.
            int airJumps = Mathf.Max(0, player.RemainingJumps);
            int bestFrames = -1;
            float bestScore = float.MaxValue;
            for (int variant = 0; variant < 2; variant++) {
                if (variant == 1 && airJumps == 0) break;
                PlaytestNav.Flight attempt = flight;
                attempt.AirJumps = variant == 0 ? 0 : airJumps;
                attempt.UseAirJumps = variant == 1;
                int frames;
                NavSegment landed;
                if (edge.To.IsRope) {
                    frames = _nav.PredictCatch(attempt, edge.To.Rope, 150, rope);
                    landed = frames >= 0 ? edge.To : null;
                } else {
                    int startFrame = attempt.Frame;
                    PlaytestNav.SimResult r = _nav.FlyAmongRopes(ref attempt, edge.To, 240,
                        _nav.ObstaclesBetween(player.GlobalPosition.X, edge.LandX), rope);
                    if (r.Success) {
                        frames = r.Frames;
                        landed = edge.To;
                    } else if (!r.Crashed && r.LandedOn != null && !r.LandedOn.IsDynamic && !r.LandedOn.IsRope) {
                        frames = Mathf.Max(1, attempt.Frame - startFrame);
                        landed = r.LandedOn;
                    } else {
                        frames = -1;
                        landed = null;
                    }
                }
                if (frames < 0) continue;
                float score = RemainingCost(landed);
                if (score < bestScore) {
                    bestScore = score;
                    bestFrames = frames;
                    landedOn = landed;
                    jumpsUsed = 1 + (variant == 1 ? airJumps : 0);
                }
            }
            return bestFrames;
        }

        private bool DriveDrop(PlayerController player, NavEdge edge, ref BotIntent intent, int frame) {
            if (_dropFrame < 0) _dropFrame = frame;
            int step = frame - _dropFrame;
            _air = edge;
            _airborneSeen = false;
            _launchFrame = frame;
            _airFrames = 0;
            _jumpsPressed = 0;
            Description = "drop";
            // Story drop-through is a double-tap of Down inside a state that
            // checks it: the block stance does (Idle/Running turn the first
            // Down into a crouch first). Hold Block, tap Down twice.
            bool canBlock = player.CurrentBlockCharges > 0;
            if (canBlock) intent.Held |= GameplayButtons.Block;
            else if (step == 1) intent.Tap |= GameplayButtons.BasicAttack;
            if (step == 3 || step == 6) {
                intent.Held |= GameplayButtons.Down;
                intent.MoveY = 1f;
            }
            if (step > 24) {
                // Did not drop (no one-way under us after all): give up on this edge.
                edge.Failures += 2;
                if (edge.Failures >= 4) _banned.Add(edge);
                _dropFrame = -1;
                _air = null;
                _plan = null;
            }
            return true;
        }

        private float SteerInput(PlayerController player, float targetX) {
            float max = Mathf.Max(60f, _nav.Hero.RunSpeed * _nav.SpeedMultiplierAt(player.GlobalPosition.X, player.GlobalPosition.Y - 10f));
            float desired = PlaytestNav.SteerToward(player.GlobalPosition.X, player.Velocity.X, targetX, max, _nav.Hero.AirControl);
            return Mathf.Clamp(desired / max, -1f, 1f);
        }

        /// <summary>Ground steering with a braking zone so the hero stops on the point.</summary>
        private static float GroundInput(PlayerController player, float targetX, float tolerance = 10f) {
            float dx = targetX - player.GlobalPosition.X;
            if (Mathf.Abs(dx) <= tolerance) return 0f;
            float vx = player.Velocity.X;
            // ~12-frame decel ramp at run speed: stopping distance ~ v^2 / (2 * v / 0.2 s).
            float stopping = Mathf.Abs(vx) * 0.11f;
            if (Mathf.Sign(vx) == Mathf.Sign(dx) && stopping >= Mathf.Abs(dx)) return 0f;
            return Mathf.Sign(dx);
        }
    }
}
