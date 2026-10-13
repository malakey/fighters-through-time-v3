using Godot;

namespace FTT.Environment {

    /// <summary>
    /// The Story Mode camera: room confinement (<see cref="SetBounds"/>, driven by
    /// <see cref="RoomTransitionTrigger"/>s and the base level's resume/rewind
    /// re-confinement) plus, once <see cref="EnableFollow"/> is called, the design's
    /// follow rig (F8, design-godot.md "Story Mode Camera2D Configuration"): a
    /// horizontal/vertical dead zone, a soft zone the hero never leaves, separate
    /// X/Y damping, a 0.3 s look-ahead in the movement direction and a one-unit
    /// follow offset. Numbers live in <see cref="StoryCameraRules"/>.
    ///
    /// <para>The rig drives the camera node's own position (the camera goes
    /// <c>TopLevel</c>, staying a child of the hero) and never touches
    /// <see cref="Camera2D.Offset"/>, which belongs to <c>CameraShake</c>. Built-in
    /// position smoothing and drag margins are off while following — the rig is
    /// the smoothing. Without <see cref="EnableFollow"/> this is the plain confiner
    /// it always was.</para>
    /// </summary>
    public partial class StoryCameraConfiner : Camera2D {
        [Signal] public delegate void BoundsChangedEventHandler(Rect2 bounds);

        [Export] public Rect2 ActiveBounds = new(0, 0, 1920, 1080);

        /// <summary>The body the rig follows; null when the rig is off.</summary>
        public Node2D FollowTarget { get; private set; }

        /// <summary>True while the follow rig drives this camera.</summary>
        public bool IsFollowing => FollowTarget != null && IsInstanceValid(FollowTarget);

        /// <summary>The rig's (unshaken) view centre in global coordinates.</summary>
        public Vector2 RigCenter { get; private set; }

        /// <summary>The current, smoothed horizontal lead in pixels.</summary>
        public float LookAheadX { get; private set; }

        private Vector2 _lastFollowPoint;
        private Vector2 _lastTargetPosition;
        private bool _hasLastTarget;

        public override void _Ready() => SetBounds(ActiveBounds);

        public void SetBounds(Rect2 bounds) {
            // GU-E: a room change while an encounter lock holds the camera only
            // updates the stored room rect (Levels 3 and 7 re-fire room
            // triggers on every crossing); the lock's release eases back to it.
            _roomBounds = bounds.Abs();
            _roomBoundsKnown = true;
            if (_hasLockBounds) return;
            _boundsEasing = false;
            ActiveBounds = bounds.Abs();
            LimitLeft = Mathf.RoundToInt(ActiveBounds.Position.X);
            LimitTop = Mathf.RoundToInt(ActiveBounds.Position.Y);
            LimitRight = Mathf.RoundToInt(ActiveBounds.End.X);
            LimitBottom = Mathf.RoundToInt(ActiveBounds.End.Y);
            // A room change confines at once (the built-in limits always snapped);
            // keeping the rig centre inside the new room stops a hidden drift
            // that would delay the first pan back.
            if (IsFollowing) {
                RigCenter = Confine(RigCenter);
                GlobalPosition = RigCenter;
            }
            EmitSignal(SignalName.BoundsChanged, ActiveBounds);
        }

        /// <summary>
        /// Turns the follow rig on for <paramref name="target"/> (normally the
        /// hero this camera is parented to) and cuts straight to it.
        /// </summary>
        public void EnableFollow(Node2D target) {
            FollowTarget = target;
            if (target == null) return;
            TopLevel = true;
            PositionSmoothingEnabled = false;
            DragHorizontalEnabled = false;
            DragVerticalEnabled = false;
            SnapToTarget();
        }

        /// <summary>Cuts to the target with no lead and no damping (teleports, loads).</summary>
        public void SnapToTarget() {
            if (!IsFollowing) return;
            LookAheadX = 0f;
            _lastTargetPosition = FollowTarget.GlobalPosition;
            _hasLastTarget = true;
            _lastFollowPoint = FollowTarget.GlobalPosition + StoryCameraRules.FollowOffsetPixels;
            RigCenter = Confine(_lastFollowPoint);
            GlobalPosition = RigCenter;
        }

        public override void _PhysicsProcess(double delta) {
            StepBoundsEase((float)delta); // GU-E: the lock-bounds ease, before the rig confines
            if (!IsFollowing) return;
            StepFollow((float)delta);
        }

        // === GU-E (2026-10-12): the encounter-lock bounds layer ===============
        //
        // An engaged EncounterLock overrides the room rect with its own camera
        // rect; a room SetBounds while it holds only updates the stored room
        // rect, so a repeatable room-trigger pair cannot overwrite the clamp, and
        // the base level's rewind and resume re-confinement defer to it for free.
        // The limits ease in and out (EncounterLockRules.CameraEaseSeconds) rather
        // than snapping. Zoom, the view size and boss framing are GU-B's.

        private Rect2 _roomBounds;
        private bool _roomBoundsKnown;
        private bool _hasLockBounds;
        private Rect2 _lockBounds;
        private bool _boundsEasing;
        private Rect2 _easedBounds;
        private Rect2 _easeTarget;

        /// <summary>True while an encounter lock's rect overrides the room rect.</summary>
        public bool HasLockBounds => _hasLockBounds;

        /// <summary>The encounter lock's rect, while one is set.</summary>
        public Rect2 LockBounds => _lockBounds;

        /// <summary>The last room rect a room trigger or the level set (the rect a lock release returns to).</summary>
        public Rect2 RoomBounds => _roomBoundsKnown ? _roomBounds : ActiveBounds;

        /// <summary>True while the limits are still easing toward a lock rect or back to the room.</summary>
        public bool IsEasingBounds => _boundsEasing;

        /// <summary>
        /// The limits the camera currently confines to (eased), as a rect. Equal to
        /// <see cref="ActiveBounds"/> once an ease has settled. Test seam.
        /// </summary>
        public Rect2 CurrentLimits => new(LimitLeft, LimitTop, LimitRight - LimitLeft, LimitBottom - LimitTop);

        /// <summary>
        /// An engaged encounter lock takes the camera: its rect overrides the room
        /// rect, eased in. <see cref="ActiveBounds"/> reads the lock rect at once.
        /// </summary>
        public void SetLockBounds(Rect2 bounds) {
            if (!_roomBoundsKnown) {
                _roomBounds = ActiveBounds;
                _roomBoundsKnown = true;
            }
            _hasLockBounds = true;
            _lockBounds = bounds.Abs();
            BeginBoundsEase(_lockBounds);
        }

        /// <summary>The lock released the camera: ease back to the stored room rect.</summary>
        public void ClearLockBounds() {
            if (!_hasLockBounds) return;
            _hasLockBounds = false;
            BeginBoundsEase(RoomBounds);
        }

        private void BeginBoundsEase(Rect2 target) {
            ActiveBounds = target;
            if (!IsFollowing || !IsInsideTree()) {
                // No rig to pan smoothly: the built-in limits apply at once.
                _boundsEasing = false;
                WriteLimits(target);
            } else {
                _easedBounds = _boundsEasing ? _easedBounds : CurrentLimits;
                _easeTarget = target;
                _boundsEasing = true;
            }
            EmitSignal(SignalName.BoundsChanged, ActiveBounds);
        }

        /// <summary>
        /// One step of the limit ease. Public so tests can drive it frame by
        /// frame; the camera's own physics step runs it.
        /// </summary>
        public void StepBoundsEase(float delta) {
            if (!_boundsEasing) return;
            float factor = StoryCameraRules.DampFactor(EncounterLockRules.CameraEaseSeconds, delta);
            Vector2 start = _easedBounds.Position.Lerp(_easeTarget.Position, factor);
            Vector2 end = _easedBounds.End.Lerp(_easeTarget.End, factor);
            if (start.DistanceTo(_easeTarget.Position) < 0.5f && end.DistanceTo(_easeTarget.End) < 0.5f) {
                start = _easeTarget.Position;
                end = _easeTarget.End;
                _boundsEasing = false;
            }
            _easedBounds = new Rect2(start, end - start);
            WriteLimits(_easedBounds);
        }

        private void WriteLimits(Rect2 bounds) {
            LimitLeft = Mathf.RoundToInt(bounds.Position.X);
            LimitTop = Mathf.RoundToInt(bounds.Position.Y);
            LimitRight = Mathf.RoundToInt(bounds.End.X);
            LimitBottom = Mathf.RoundToInt(bounds.End.Y);
        }

        // === end GU-E region ==================================================

        /// <summary>One rig step. Public so tests can drive it frame by frame.</summary>
        public void StepFollow(float delta) {
            if (!IsFollowing) return;
            Vector2 targetPosition = FollowTarget.GlobalPosition;
            Vector2 velocity = FollowTarget is CharacterBody2D body
                ? body.Velocity
                : (_hasLastTarget && delta > 0f ? (targetPosition - _lastTargetPosition) / delta : Vector2.Zero);
            _lastTargetPosition = targetPosition;
            _hasLastTarget = true;

            Vector2 basePoint = targetPosition + StoryCameraRules.FollowOffsetPixels;
            if (basePoint.DistanceTo(_lastFollowPoint) > StoryCameraRules.SnapDistancePixels) {
                SnapToTarget();
                return;
            }

            LookAheadX = Mathf.Lerp(LookAheadX, StoryCameraRules.LookAheadTarget(velocity.X),
                StoryCameraRules.DampFactor(StoryCameraRules.DampingX, delta));
            Vector2 follow = basePoint + new Vector2(LookAheadX, 0f);
            _lastFollowPoint = basePoint;

            Vector2 view = ViewSize();
            float x = StoryCameraRules.FollowAxis(RigCenter.X, follow.X,
                StoryCameraRules.DeadZoneWidth * view.X / 2f, StoryCameraRules.SoftZoneWidth * view.X / 2f,
                StoryCameraRules.DampingX, delta);
            float y = StoryCameraRules.FollowAxis(RigCenter.Y, follow.Y,
                StoryCameraRules.DeadZoneHeight * view.Y / 2f, StoryCameraRules.SoftZoneHeight * view.Y / 2f,
                StoryCameraRules.DampingY, delta);
            RigCenter = Confine(new Vector2(x, y));
            GlobalPosition = RigCenter;
        }

        /// <summary>The visible world size: the viewport over the zoom.</summary>
        private Vector2 ViewSize() {
            Vector2 viewport = IsInsideTree() ? GetViewportRect().Size : new Vector2(1920f, 1080f);
            if (viewport.X <= 0f || viewport.Y <= 0f) viewport = new Vector2(1920f, 1080f);
            Vector2 zoom = Zoom;
            return new Vector2(viewport.X / Mathf.Max(0.01f, zoom.X), viewport.Y / Mathf.Max(0.01f, zoom.Y));
        }

        private Vector2 Confine(Vector2 center) {
            Vector2 half = ViewSize() / 2f;
            return new Vector2(
                StoryCameraRules.ConfineCenter(center.X, LimitLeft, LimitRight, half.X),
                StoryCameraRules.ConfineCenter(center.Y, LimitTop, LimitBottom, half.Y));
        }
    }
}
