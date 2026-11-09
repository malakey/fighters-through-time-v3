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
    ///
    /// <para>The zoom (<see cref="StoryCameraRules.ViewZoom"/>, eased out for a
    /// boss or Mirror arena) lives in <c>StoryCameraConfiner.Framing.cs</c>.</para>
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
            // triggers on every crossing); the lock's release heads back to it.
            _roomBounds = bounds.Abs();
            _roomBoundsKnown = true;
            if (_hasLockBounds) return;
            _ratcheting = false;
            ActiveBounds = bounds.Abs();
            WriteLimits(ActiveBounds);
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
            CaptureOpeningOrigin();
            SnapZoomToTarget();
            LookAheadX = 0f;
            _lastTargetPosition = FollowTarget.GlobalPosition;
            _hasLastTarget = true;
            _lastFollowPoint = FollowTarget.GlobalPosition + StoryCameraRules.FollowOffsetPixels;
            if (_ratcheting) {
                // FU-LOCK: a cut folds the view it lands on into the working limits.
                Vector2 snapHalf = ViewSize() / 2f;
                _working = _working.Merge(new Rect2(_lastFollowPoint - snapHalf, snapHalf * 2f));
                _ratcheting = _working != _ratchetTarget;
                WriteLimits(_working);
            }
            RigCenter = Confine(OpeningCenter(_lastFollowPoint));
            GlobalPosition = RigCenter;
        }

        public override void _PhysicsProcess(double delta) {
            if (!IsFollowing) return;
            StepFollow((float)delta);
        }

        // === GU-E (2026-10-12) / FU-LOCK (2026-10-25): the encounter-lock bounds layer
        //
        // An engaged EncounterLock overrides the room rect with its own camera
        // rect; a room SetBounds while it holds only updates the stored room
        // rect, so a repeatable room-trigger pair cannot overwrite the clamp, and
        // the base level's rewind and resume re-confinement defer to it for free.
        //
        // FU-LOCK: the limits never pan the camera. A change of target rect T (a
        // lock engaging, or its release back to the room rect) opens the working
        // limits W to the union of the view as it stands and T, then W ratchets
        // toward T one side at a time, only as the follow rig's own desired view
        // passes that side, and never back. The camera therefore moves only when
        // the hero does, and the view tightens onto the area as the hero walks in.

        private Rect2 _roomBounds;
        private bool _roomBoundsKnown;
        private bool _hasLockBounds;
        private Rect2 _lockBounds;
        private bool _ratcheting;
        private Rect2 _working;
        private Rect2 _ratchetTarget;

        /// <summary>True while an encounter lock's rect overrides the room rect.</summary>
        public bool HasLockBounds => _hasLockBounds;

        /// <summary>The encounter lock's rect, while one is set.</summary>
        public Rect2 LockBounds => _lockBounds;

        /// <summary>The last room rect a room trigger or the level set (the rect a lock release returns to).</summary>
        public Rect2 RoomBounds => _roomBoundsKnown ? _roomBounds : ActiveBounds;

        /// <summary>True while the working limits are still wider than the target rect and tightening toward it.</summary>
        public bool IsRatchetingBounds => _ratcheting;

        /// <summary>
        /// The limits the camera currently confines to, as a rect. Equal to
        /// <see cref="ActiveBounds"/> once the ratchet has settled. Test seam.
        /// </summary>
        public Rect2 CurrentLimits => new(LimitLeft, LimitTop, LimitRight - LimitLeft, LimitBottom - LimitTop);

        /// <summary>
        /// An engaged encounter lock takes the camera: its rect becomes the target
        /// of the limits. <see cref="ActiveBounds"/> reads the lock rect at once.
        /// </summary>
        public void SetLockBounds(Rect2 bounds) {
            if (!_roomBoundsKnown) {
                _roomBounds = ActiveBounds;
                _roomBoundsKnown = true;
            }
            _hasLockBounds = true;
            _lockBounds = bounds.Abs();
            BeginRatchet(_lockBounds);
        }

        /// <summary>The lock released the camera: the limits head back to the stored room rect without a pan.</summary>
        public void ClearLockBounds() {
            if (!_hasLockBounds) return;
            _hasLockBounds = false;
            BeginRatchet(RoomBounds);
        }

        private void BeginRatchet(Rect2 target) {
            ActiveBounds = target;
            _ratchetTarget = target;
            if (!IsFollowing || !IsInsideTree()) {
                // No rig to pan: the built-in limits apply at once.
                _ratcheting = false;
                WriteLimits(target);
            } else {
                _working = target.Merge(VisibleWorldRect);
                _ratcheting = _working != target;
                WriteLimits(_working);
            }
            EmitSignal(SignalName.BoundsChanged, ActiveBounds);
        }

        /// <summary>
        /// Tightens the working limits toward the target on each side the
        /// <paramref name="desiredCentre"/> view has moved past, never back.
        /// Public (pure on the camera's state) so tests can drive it.
        /// </summary>
        public void StepRatchet(Vector2 desiredCentre) {
            if (!_ratcheting) return;
            Vector2 half = ViewSize() / 2f;
            float left = _working.Position.X, top = _working.Position.Y;
            float right = _working.End.X, bottom = _working.End.Y;
            Rect2 t = _ratchetTarget;
            if (t.Position.X > left) left = Mathf.Max(left, Mathf.Min(t.Position.X, desiredCentre.X - half.X));
            if (t.Position.Y > top) top = Mathf.Max(top, Mathf.Min(t.Position.Y, desiredCentre.Y - half.Y));
            if (t.End.X < right) right = Mathf.Min(right, Mathf.Max(t.End.X, desiredCentre.X + half.X));
            if (t.End.Y < bottom) bottom = Mathf.Min(bottom, Mathf.Max(t.End.Y, desiredCentre.Y + half.Y));
            _working = new Rect2(left, top, right - left, bottom - top);
            if (_working == _ratchetTarget) _ratcheting = false;
            WriteLimits(_working);
        }

        private void WriteLimits(Rect2 bounds) {
            if (_openingWeight > 0f) {
                bounds = bounds.Merge(_openingFrame);
                LimitLeft = Mathf.FloorToInt(bounds.Position.X);
                LimitTop = Mathf.FloorToInt(bounds.Position.Y);
                LimitRight = Mathf.CeilToInt(bounds.End.X);
                LimitBottom = Mathf.CeilToInt(bounds.End.Y);
                return;
            }
            LimitLeft = Mathf.RoundToInt(bounds.Position.X);
            LimitTop = Mathf.RoundToInt(bounds.Position.Y);
            LimitRight = Mathf.RoundToInt(bounds.End.X);
            LimitBottom = Mathf.RoundToInt(bounds.End.Y);
        }

        // === end GU-E region ==================================================

        /// <summary>One rig step. Public so tests can drive it frame by frame.</summary>
        public void StepFollow(float delta) {
            if (!IsFollowing) return;
            StepOpening();
            // GU-B: the zoom eases first, so this step's view, zones and
            // confinement all read the zoom the frame is drawn at.
            StepZoom(delta);
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
            // GU-B: the ordinary horizontal follow, pulled onto a framed arena
            // while a framing request is live (StoryCameraConfiner.Framing.cs).
            float x = FramedFollowX(follow.X, view, delta);
            float y = StoryCameraRules.FollowAxis(RigCenter.Y, follow.Y,
                StoryCameraRules.DeadZoneHeight * view.Y / 2f, StoryCameraRules.SoftZoneHeight * view.Y / 2f,
                StoryCameraRules.DampingY, delta);
            Vector2 desired = OpeningCenter(new Vector2(x, y));
            StepRatchet(desired); // FU-LOCK: tighten the lock limits before the rig confines
            RigCenter = Confine(desired);
            GlobalPosition = RigCenter;
        }

        /// <summary>
        /// The visible world size: the viewport over the current zoom (eased
        /// toward <see cref="TargetZoom"/>, so confinement follows the ease).
        /// </summary>
        private Vector2 ViewSize() {
            var reference = new Vector2(StoryCameraRules.ReferenceViewportWidth, StoryCameraRules.ReferenceViewportHeight);
            Vector2 viewport = IsInsideTree() ? GetViewportRect().Size : reference;
            if (viewport.X <= 0f || viewport.Y <= 0f) viewport = reference;
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
