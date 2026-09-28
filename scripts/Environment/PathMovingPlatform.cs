using Godot;
using System;
using FTT.Core;

namespace FTT.Environment {

    public enum PathMovingPlatformMode { PingPong, Loop }

    /// <summary>
    /// Platform that walks a list of authored waypoints at a constant speed
    /// (Level 7 boarding skiffs, Level 12 orbital lifts, Level 13 drifting shards).
    /// Waypoints are offsets from the platform's authored position, so a template
    /// instance can be dropped anywhere without rewriting them.
    /// </summary>
    public partial class PathMovingPlatform : AnimatableBody2D, IStoryRewindable, IRewindScrubbable, IStoryTimeFreezable {
        [Signal] public delegate void WaypointReachedEventHandler(int waypointIndex);

        [Export] public string PlatformID = "";
        /// <summary>Offsets from the authored position. Fewer than two entries means the platform stays put.</summary>
        [Export] public Vector2[] Waypoints = Array.Empty<Vector2>();
        [Export(PropertyHint.Range, "1,4000,1")] public float Speed = 120f;
        [Export(PropertyHint.Range, "0,60,0.05")] public float EndpointWaitSeconds = 0.5f;
        [Export] public PathMovingPlatformMode Mode = PathMovingPlatformMode.PingPong;
        [Export] public bool Enabled = true;

        /// <summary>Translation key for the template's placeholder sign; blank hides it.</summary>
        [Export] public string LabelKey = "toolkit_moving_platform";
        [Export] public NodePath LabelPath = ToolkitLabel.DefaultLabelPath;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private Vector2 _origin;
        private int _direction = 1;
        private float _waitTimer;
        private Vector2 _checkpointPosition;
        private int _checkpointTarget;
        private int _checkpointDirection = 1;

        // === V7.2 rewind scrub (the world-interaction exemplar) ===============
        // The platform records its own position history, as deep as the
        // player's rewind buffer, and scrubs back along it during a Chronal
        // Rewind.
        /// <summary>Scene group the rewind manager sweeps for scrubbables.</summary>
        public const string ScrubGroup = "path_moving_platform";
        private const int HistoryCapacity = ChronalRewindBuffer.DefaultCapacity;
        private readonly Vector2[] _history = new Vector2[HistoryCapacity];
        private int _historyNext;
        private int _historyCount;
        private bool _scrubbing;
        private bool _skipNextRewindRestore;

        public int TargetWaypointIndex { get; private set; }
        public int LastWaypointIndex { get; private set; }
        public Vector2 PlatformVelocity { get; private set; }
        public bool IsWaiting => _waitTimer > 0f;

        public override void _Ready() {
            AddToGroup("puzzle_object");
            AddToGroup(ScrubGroup);
            // Manual movement inside _PhysicsProcess: Godot estimates the body's
            // linear velocity from the transform delta, which is what carries a
            // CharacterBody2D standing on it. sync_to_physics is deliberately off so
            // Position writes land immediately (the RotatingPlatform convention).
            SyncToPhysics = false;
            _origin = Position;
            ToolkitLabel.Apply(this, LabelPath, LabelKey);
            if (Waypoints is { Length: > 0 }) Position = _origin + Waypoints[0];
            TargetWaypointIndex = Waypoints is { Length: > 1 } ? 1 : 0;
            _checkpointPosition = Position;
            _checkpointTarget = TargetWaypointIndex;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewind;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
        }

        public override void _PhysicsProcess(double delta) {
            // V7.6 Time Freeze: the platform stops exactly where it is — no
            // history recorded, no path advanced, no catch-up on thaw — while
            // its collision shape stays live, because the player may be
            // standing on it.
            if (_scrubbing || _timeFrozen) return;
            _history[_historyNext] = Position;
            _historyNext = (_historyNext + 1) % HistoryCapacity;
            if (_historyCount < HistoryCapacity) _historyCount++;
            AdvancePath((float)delta);
        }

        // === IRewindScrubbable ================================================

        public void BeginRewindScrub() {
            _scrubbing = true;
        }

        public void ApplyRewindScrub(int depthFrames) {
            if (_historyCount == 0) return;
            int offset = Mathf.Clamp(depthFrames, 1, _historyCount);
            int index = _historyNext - offset;
            if (index < 0) index += HistoryCapacity;
            Position = _history[index];
            PlatformVelocity = Vector2.Zero;
        }

        public void EndRewindScrub() {
            _scrubbing = false;
            _waitTimer = 0f;
            PlatformVelocity = Vector2.Zero;
            // The scrub already placed the platform where the rewound moment
            // had it — the checkpoint-state snap at rewind end must not undo it.
            _skipNextRewindRestore = true;
        }

        // === IStoryTimeFreezable ==============================================

        private bool _timeFrozen;

        /// <summary>True while Time Freeze holds the platform in place. Test seam.</summary>
        public bool IsTimeFrozen => _timeFrozen;

        /// <summary>
        /// Freeze in place. Deliberately mutates nothing else: the waypoint
        /// target, the endpoint wait timer and the recorded history all survive,
        /// so the platform resumes mid-leg exactly where it stopped. Contrast
        /// <see cref="ApplyRewindScrub"/>, which MOVES the platform.
        /// </summary>
        public void SetTimeFrozen(bool frozen) {
            _timeFrozen = frozen;
            if (frozen) PlatformVelocity = Vector2.Zero;
        }

        public void AdvancePath(float dt) {
            if (!Enabled || Waypoints == null || Waypoints.Length < 2) {
                PlatformVelocity = Vector2.Zero;
                return;
            }
            if (_waitTimer > 0f) {
                _waitTimer -= dt;
                PlatformVelocity = Vector2.Zero;
                return;
            }

            Vector2 target = _origin + Waypoints[TargetWaypointIndex];
            Vector2 toTarget = target - Position;
            float step = Speed * dt;
            if (toTarget.Length() <= step || step <= 0f) {
                Position = target;
                PlatformVelocity = Vector2.Zero;
                ReachWaypoint();
                return;
            }
            Vector2 move = toTarget.Normalized() * step;
            Position += move;
            PlatformVelocity = move / dt;
        }

        public Vector2 WaypointGlobalPosition(int index) {
            if (Waypoints == null || Waypoints.Length == 0) return GlobalPosition;
            int clamped = Mathf.Clamp(index, 0, Waypoints.Length - 1);
            return (GetParent() as Node2D)?.ToGlobal(_origin + Waypoints[clamped]) ?? (_origin + Waypoints[clamped]);
        }

        public void CaptureCheckpointState(string checkpointID) {
            _checkpointPosition = Position;
            _checkpointTarget = TargetWaypointIndex;
            _checkpointDirection = _direction;
        }

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            bool reset = RewindPolicy == StoryRewindPolicy.ResetToInitialState;
            Position = reset
                ? _origin + (Waypoints is { Length: > 0 } ? Waypoints[0] : Vector2.Zero)
                : _checkpointPosition;
            TargetWaypointIndex = reset ? (Waypoints is { Length: > 1 } ? 1 : 0) : _checkpointTarget;
            _direction = reset ? 1 : _checkpointDirection;
            _waitTimer = 0f;
            PlatformVelocity = Vector2.Zero;
        }

        private void ReachWaypoint() {
            LastWaypointIndex = TargetWaypointIndex;
            EmitSignal(SignalName.WaypointReached, LastWaypointIndex);
            _waitTimer = EndpointWaitSeconds;
            if (Mode == PathMovingPlatformMode.Loop) {
                TargetWaypointIndex = (TargetWaypointIndex + 1) % Waypoints.Length;
                return;
            }
            if (TargetWaypointIndex >= Waypoints.Length - 1) _direction = -1;
            else if (TargetWaypointIndex <= 0) _direction = 1;
            TargetWaypointIndex = Mathf.Clamp(TargetWaypointIndex + _direction, 0, Waypoints.Length - 1);
        }

        private void OnRewind(Vector2 targetPosition) {
            if (_skipNextRewindRestore) {
                _skipNextRewindRestore = false;
                return;
            }
            ApplyStoryRewind();
        }
    }
}
