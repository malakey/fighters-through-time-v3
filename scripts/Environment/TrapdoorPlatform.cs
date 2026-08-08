using Godot;
using FTT.Core;

namespace FTT.Environment {

    public enum TrapdoorState { Closed, Warning, Open }

    /// <summary>
    /// Stage-machinery trapdoor (Level 10 Globe). Solid while closed; shakes for
    /// <see cref="WarningShakeSeconds"/>, then drops its collision and its visual.
    /// Either cycles on a timer (<see cref="AutoCycle"/>) or is driven externally
    /// through <see cref="BeginWarning"/> / <see cref="Open"/> / <see cref="Close"/>.
    /// </summary>
    public partial class TrapdoorPlatform : AnimatableBody2D, IStoryRewindable {
        [Signal] public delegate void StateChangedEventHandler(int state);

        [Export] public string TrapdoorID = "";
        [Export] public bool AutoCycle;
        [Export(PropertyHint.Range, "0.05,600,0.05")] public float ClosedDurationSeconds = 3f;
        [Export(PropertyHint.Range, "0,30,0.05")] public float WarningShakeSeconds = 0.75f;
        [Export(PropertyHint.Range, "0.05,600,0.05")] public float OpenDurationSeconds = 1.5f;
        [Export(PropertyHint.Range, "0,64,0.5")] public float ShakeAmplitude = 4f;
        [Export(PropertyHint.Range, "0,10,0.5")] public float ShakeFrequency = 22f;
        [Export(PropertyHint.Range, "0,2000,1")] public float VisualDropDistance = 64f;
        [Export] public NodePath CollisionShapePath = "CollisionShape2D";
        [Export] public NodePath VisualPath = "Visual";
        [Export] public bool Enabled = true;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private Vector2 _visualHomePosition;
        private float _timer;
        private float _shakeClock;
        private TrapdoorState _checkpointState = TrapdoorState.Closed;

        public TrapdoorState State { get; private set; } = TrapdoorState.Closed;
        public bool IsSolid => State != TrapdoorState.Open;

        public override void _Ready() {
            AddToGroup("puzzle_object");
            SyncToPhysics = false;
            if (GetNodeOrNull<Node2D>(VisualPath) is Node2D visual) _visualHomePosition = visual.Position;
            _timer = ClosedDurationSeconds;
            ApplyState(TrapdoorState.Closed, notify: false);
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
            float dt = (float)delta;
            if (State == TrapdoorState.Warning) {
                _shakeClock += dt;
                ApplyShake();
            }
            if (!Enabled || !AutoCycle) return;
            _timer -= dt;
            if (_timer > 0f) return;
            switch (State) {
                case TrapdoorState.Closed: BeginWarning(); break;
                case TrapdoorState.Warning: Open(); break;
                case TrapdoorState.Open: Close(); break;
            }
        }

        public void BeginWarning() {
            _shakeClock = 0f;
            _timer = WarningShakeSeconds;
            ApplyState(TrapdoorState.Warning);
        }

        public void Open() {
            _timer = OpenDurationSeconds;
            ApplyState(TrapdoorState.Open);
        }

        public void Close() {
            _timer = ClosedDurationSeconds;
            ApplyState(TrapdoorState.Closed);
        }

        public void CaptureCheckpointState(string checkpointID) => _checkpointState = State;

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            TrapdoorState restored = RewindPolicy == StoryRewindPolicy.ResetToInitialState
                ? TrapdoorState.Closed
                : _checkpointState;
            _timer = restored switch {
                TrapdoorState.Warning => WarningShakeSeconds,
                TrapdoorState.Open => OpenDurationSeconds,
                _ => ClosedDurationSeconds
            };
            _shakeClock = 0f;
            ApplyState(restored);
        }

        private void ApplyState(TrapdoorState state, bool notify = true) {
            State = state;
            if (GetNodeOrNull<CollisionShape2D>(CollisionShapePath) is CollisionShape2D shape) {
                shape.Disabled = state == TrapdoorState.Open;
            }
            if (GetNodeOrNull<Node2D>(VisualPath) is Node2D visual) {
                visual.Position = state == TrapdoorState.Open
                    ? _visualHomePosition + new Vector2(0f, VisualDropDistance)
                    : _visualHomePosition;
                visual.Modulate = state == TrapdoorState.Open
                    ? new Color(1f, 1f, 1f, 0.35f)
                    : Colors.White;
            }
            if (!notify) return;
            EmitSignal(SignalName.StateChanged, (int)state);
            EventBus.Instance?.RaiseHazardStateChanged(new HazardStatePayload {
                HazardID = TrapdoorID,
                Phase = state switch {
                    TrapdoorState.Warning => HazardPhase.Warning,
                    TrapdoorState.Open => HazardPhase.Active,
                    _ => HazardPhase.Cooldown
                },
                Duration = _timer
            });
        }

        private void ApplyShake() {
            if (GetNodeOrNull<Node2D>(VisualPath) is not Node2D visual) return;
            float offset = Mathf.Sin(_shakeClock * ShakeFrequency * Mathf.Tau) * ShakeAmplitude;
            visual.Position = _visualHomePosition + new Vector2(offset, 0f);
        }

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();
    }
}
