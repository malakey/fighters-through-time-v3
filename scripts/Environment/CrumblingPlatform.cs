using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    public enum CrumblingPlatformState { Solid, Shaking, Collapsing, Disabled }

    public partial class CrumblingPlatform : AnimatableBody2D, IStoryRewindable {
        [Signal] public delegate void StateChangedEventHandler(CrumblingPlatformState state, float duration);

        [Export] public string PlatformID = "";
        [Export] public float ShakeDuration = 0.8f;
        [Export] public float CollapseDuration = 1.2f;
        [Export] public float RespawnDuration = 5f;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private CollisionShape2D _collisionShape;
        private CanvasItem _visual;
        private Vector2 _visualRestPosition;
        private float _timer;
        private CrumblingPlatformState _checkpointState;
        private float _checkpointTimer;
        public CrumblingPlatformState State { get; private set; } = CrumblingPlatformState.Solid;

        public override void _Ready() {
            AddToGroup("story_hazard");
            _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            _visual = GetNodeOrNull<CanvasItem>("Visual");
            if (_visual is Node2D visualNode) _visualRestPosition = visualNode.Position;
            if (GetNodeOrNull<Area2D>("LandingSensor") is Area2D sensor) sensor.BodyEntered += OnBodyEntered;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewind;
            }
            ApplyState();
        }

        public override void _ExitTree() {
            if (GetNodeOrNull<Area2D>("LandingSensor") is Area2D sensor) sensor.BodyEntered -= OnBodyEntered;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
        }

        public override void _PhysicsProcess(double delta) {
            if (State == CrumblingPlatformState.Solid) return;
            _timer -= (float)delta;
            if (State == CrumblingPlatformState.Shaking && _visual is Node2D shakingVisual) {
                int parity = Mathf.FloorToInt(_timer * 60f) & 1;
                shakingVisual.Position = _visualRestPosition + new Vector2(parity == 0 ? -3f : 3f, 0f);
            }
            if (_timer > 0f) return;

            switch (State) {
                case CrumblingPlatformState.Shaking:
                    SetState(CrumblingPlatformState.Collapsing, CollapseDuration);
                    break;
                case CrumblingPlatformState.Collapsing:
                    SetState(CrumblingPlatformState.Disabled, RespawnDuration);
                    break;
                case CrumblingPlatformState.Disabled:
                    SetState(CrumblingPlatformState.Solid, 0f);
                    break;
            }
        }

        public bool TriggerCollapse() {
            if (State != CrumblingPlatformState.Solid) return false;
            SetState(CrumblingPlatformState.Shaking, ShakeDuration);
            return true;
        }

        public void CaptureCheckpointState(string checkpointID) {
            _checkpointState = State;
            _checkpointTimer = _timer;
        }

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            if (RewindPolicy == StoryRewindPolicy.ResetToInitialState) SetState(CrumblingPlatformState.Solid, 0f);
            else SetState(_checkpointState, _checkpointTimer);
        }

        private void SetState(CrumblingPlatformState state, float duration) {
            State = state;
            _timer = Mathf.Max(0f, duration);
            ApplyState();
            EmitSignal(SignalName.StateChanged, (int)state, _timer);
            EventBus.Instance?.RaiseHazardStateChanged(new HazardStatePayload {
                HazardID = PlatformID,
                Phase = state == CrumblingPlatformState.Shaking ? HazardPhase.Warning
                    : state == CrumblingPlatformState.Collapsing ? HazardPhase.Active : HazardPhase.Cooldown,
                Duration = _timer
            });
        }

        private void ApplyState() {
            bool enabled = State != CrumblingPlatformState.Disabled;
            if (_collisionShape != null) _collisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, !enabled);
            if (_visual != null) _visual.Visible = enabled;
            if (_visual is Node2D visualNode && State != CrumblingPlatformState.Shaking) visualNode.Position = _visualRestPosition;
        }

        private void OnBodyEntered(Node2D body) { if (body is PlayerController) TriggerCollapse(); }
        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();
    }
}
