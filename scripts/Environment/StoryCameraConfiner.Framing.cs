using System.Collections.Generic;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// GU-B (gameplay update 2026-10-12): the Story camera's zoom and its
    /// framing requests. Every Story camera starts at
    /// <see cref="StoryCameraRules.ViewZoom"/> (the constructor sets it, so it is
    /// in place before <c>_Ready</c>'s first <see cref="SetBounds"/> and before
    /// <see cref="EnableFollow"/> cuts to the hero). A boss or Mirror encounter
    /// asks for its arena to be framed (<see cref="RequestFraming"/>): the zoom
    /// eases out to fit it and the horizontal follow slides onto it (the hero
    /// never leaving the soft zone), then both ease back once the request is
    /// released.
    ///
    /// <para>The rig reads the current zoom everywhere it reads the view
    /// (<c>ViewSize</c>, so the dead and soft zones and the room confinement),
    /// which keeps confinement exact while the zoom eases. Room bounds and the
    /// lock-bounds layer live in the main file; this part decides only the zoom
    /// and the framing's horizontal pull.</para>
    /// </summary>
    public partial class StoryCameraConfiner {
        /// <summary>A zoom this close to its target has arrived and is snapped onto it.</summary>
        private const float ZoomSettleEpsilon = 0.0005f;

        private readonly struct FramingRequest {
            public readonly object Owner;
            public readonly Rect2 Area;
            public readonly float FitZoom;

            public FramingRequest(object owner, Rect2 area, float fitZoom) {
                Owner = owner;
                Area = area;
                FitZoom = fitZoom;
            }
        }

        private readonly List<FramingRequest> _framingRequests = new();

        public StoryCameraConfiner() {
            Zoom = new Vector2(StoryCameraRules.ViewZoom, StoryCameraRules.ViewZoom);
        }

        /// <summary>
        /// Frames <paramref name="area"/> (global) for <paramref name="owner"/>
        /// until <see cref="ReleaseFraming"/>: the zoom eases toward
        /// <see cref="StoryCameraRules.FramingZoom"/> of the area's width, never
        /// wider than <paramref name="minZoom"/>. Repeating a request replaces the
        /// owner's earlier one. With several live owners the widest view wins
        /// (the smallest zoom), so no owner's area is cut off by another's and
        /// the order of requests does not matter.
        /// </summary>
        public void RequestFraming(object owner, Rect2 area, float minZoom) {
            if (owner == null) return;
            RemoveFramingFor(owner);
            Rect2 normalized = area.Abs();
            _framingRequests.Add(new FramingRequest(owner, normalized,
                StoryCameraRules.FramingZoom(normalized.Size.X, minZoom)));
        }

        /// <summary>Drops <paramref name="owner"/>'s framing; the zoom eases back once no request remains.</summary>
        public void ReleaseFraming(object owner) {
            if (owner == null) return;
            RemoveFramingFor(owner);
        }

        /// <summary>True while <paramref name="owner"/> holds a live framing request.</summary>
        public bool IsFramingFor(object owner) {
            if (owner == null) return false;
            foreach (FramingRequest request in _framingRequests) {
                if (ReferenceEquals(request.Owner, owner) && IsLiveFramingOwner(request.Owner)) return true;
            }
            return false;
        }

        /// <summary>The number of live framing requests.</summary>
        public int FramingRequestCount {
            get {
                int count = 0;
                foreach (FramingRequest request in _framingRequests) {
                    if (IsLiveFramingOwner(request.Owner)) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// The zoom the camera is easing toward: <see cref="StoryCameraRules.ViewZoom"/>
        /// with no live request, else the widest live request — floored at the zoom
        /// whose view still fits the current confinement limits, so a framing never
        /// shows the world outside the room it is confined to.
        /// </summary>
        public float TargetZoom {
            get {
                float target = StoryCameraRules.ViewZoom;
                foreach (FramingRequest request in _framingRequests) {
                    if (IsLiveFramingOwner(request.Owner)) target = Mathf.Min(target, request.FitZoom);
                }
                if (target >= StoryCameraRules.ViewZoom) return StoryCameraRules.ViewZoom;
                float roomFloor = StoryCameraRules.BoundsFitZoom(LimitRight - LimitLeft, LimitBottom - LimitTop);
                return Mathf.Min(StoryCameraRules.ViewZoom, Mathf.Max(target, roomFloor));
            }
        }

        /// <summary>The visible world size at the camera's current zoom.</summary>
        public Vector2 VisibleWorldSize => ViewSize();

        /// <summary>
        /// The visible world rect around the rig's unshaken centre (the camera's
        /// screen centre when the rig is off).
        /// </summary>
        public Rect2 VisibleWorldRect {
            get {
                Vector2 view = ViewSize();
                Vector2 centre = IsFollowing ? RigCenter : (IsInsideTree() ? GetScreenCenterPosition() : GlobalPosition);
                return new Rect2(centre - view / 2f, view);
            }
        }

        /// <summary>
        /// The Story camera parented to <paramref name="host"/> (the hero carries
        /// it as a direct child at every Story camera site), or null. Direct
        /// children only — callers cache the result rather than search per frame.
        /// </summary>
        public static StoryCameraConfiner FindOn(Node host) {
            if (host == null || !IsInstanceValid(host)) return null;
            int count = host.GetChildCount();
            for (int index = 0; index < count; index++) {
                if (host.GetChild(index) is StoryCameraConfiner camera) return camera;
            }
            return null;
        }

        /// <summary>
        /// The eased 0–1 strength of the framing's pull on the horizontal follow:
        /// it rises while a request is live and falls after the last release, on
        /// the zoom's own <see cref="StoryCameraRules.ZoomDampingSeconds"/>, so the
        /// view slides onto an arena and back rather than cutting.
        /// </summary>
        public float FramingWeight { get; private set; }

        private float _framingSpanStart;
        private float _framingSpanEnd;
        private bool _hasFramingSpan;

        /// <summary>
        /// The horizontal follow step (run by <see cref="StepFollow"/>). With no
        /// framing it is exactly the rig's ordinary follow. While a framing pulls,
        /// the follow point slides toward <see cref="StoryCameraRules.FramedCentre"/>
        /// of the framed span — a zoom that fits the area only frames it if the
        /// view is also over it — with the dead zone and X damping, no soft-zone
        /// catch-up toward that point, and the hero kept inside the soft zone.
        /// </summary>
        private float FramedFollowX(float followX, Vector2 view, float delta) {
            float deadHalf = StoryCameraRules.DeadZoneWidth * view.X / 2f;
            float softHalf = StoryCameraRules.SoftZoneWidth * view.X / 2f;
            bool live = TryGetLiveFramingSpan(out float spanStart, out float spanEnd);
            if (live) {
                _framingSpanStart = spanStart;
                _framingSpanEnd = spanEnd;
                _hasFramingSpan = true;
            }
            FramingWeight = EaseFramingWeight(FramingWeight, live ? 1f : 0f, delta);
            if (FramingWeight <= 0f || !_hasFramingSpan) {
                return StoryCameraRules.FollowAxis(RigCenter.X, followX, deadHalf, softHalf,
                    StoryCameraRules.DampingX, delta);
            }
            float framed = StoryCameraRules.FramedCentre(followX, _framingSpanStart, _framingSpanEnd,
                view.X / 2f, softHalf - deadHalf);
            float follow = Mathf.Lerp(followX, framed, FramingWeight);
            float x = StoryCameraRules.FollowAxis(RigCenter.X, follow, deadHalf, 0f, StoryCameraRules.DampingX, delta);
            return Mathf.Clamp(x, followX - softHalf, followX + softHalf);
        }

        private static float EaseFramingWeight(float weight, float target, float delta) {
            float next = weight + (target - weight) * StoryCameraRules.DampFactor(StoryCameraRules.ZoomDampingSeconds, delta);
            return Mathf.Abs(target - next) <= FramingWeightSettleEpsilon ? target : next;
        }

        /// <summary>A pull this close to its target has arrived.</summary>
        private const float FramingWeightSettleEpsilon = 0.001f;

        /// <summary>The horizontal extent covering every live request's area.</summary>
        private bool TryGetLiveFramingSpan(out float start, out float end) {
            start = 0f;
            end = 0f;
            bool any = false;
            foreach (FramingRequest request in _framingRequests) {
                if (!IsLiveFramingOwner(request.Owner) || request.Area.Size.X <= 0f) continue;
                start = any ? Mathf.Min(start, request.Area.Position.X) : request.Area.Position.X;
                end = any ? Mathf.Max(end, request.Area.End.X) : request.Area.End.X;
                any = true;
            }
            return any;
        }

        /// <summary>One damped zoom step toward <see cref="TargetZoom"/> (run by <see cref="StepFollow"/>).</summary>
        private void StepZoom(float delta) {
            PruneFramingRequests();
            float target = TargetZoom;
            Vector2 zoom = Zoom;
            float current = zoom.X;
            float next = Mathf.Abs(target - current) <= ZoomSettleEpsilon
                ? target
                : current + (target - current) * StoryCameraRules.DampFactor(StoryCameraRules.ZoomDampingSeconds, delta);
            if (Mathf.Abs(target - next) <= ZoomSettleEpsilon) next = target;
            if (next != current || zoom.Y != next) Zoom = new Vector2(next, next);
        }

        /// <summary>A cut (<see cref="SnapToTarget"/>) cuts the zoom too: no easing across a teleport.</summary>
        private void SnapZoomToTarget() {
            PruneFramingRequests();
            float target = TargetZoom;
            Vector2 zoom = Zoom;
            if (zoom.X != target || zoom.Y != target) Zoom = new Vector2(target, target);
        }

        private void RemoveFramingFor(object owner) {
            for (int index = _framingRequests.Count - 1; index >= 0; index--) {
                if (ReferenceEquals(_framingRequests[index].Owner, owner)) _framingRequests.RemoveAt(index);
            }
        }

        /// <summary>A freed owner (an encounter torn down without releasing) no longer frames anything.</summary>
        private void PruneFramingRequests() {
            for (int index = _framingRequests.Count - 1; index >= 0; index--) {
                if (!IsLiveFramingOwner(_framingRequests[index].Owner)) _framingRequests.RemoveAt(index);
            }
        }

        private static bool IsLiveFramingOwner(object owner) =>
            owner != null && (owner is not GodotObject godotObject || IsInstanceValid(godotObject));
    }
}
