using Godot;

namespace FTT.Environment {

    public partial class Counterweight : AnimatableBody2D, IStoryRewindable {
        [Export] public Vector2 UnloadedPosition;
        [Export] public Vector2 LoadedPosition = new(0f, 200f);
        [Export(PropertyHint.Range, "0.1,100,0.1")] public float FullLoadWeight = 5f;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private float _checkpointLoad;
        public float CurrentLoad { get; private set; }

        public override void _Ready() {
            AddToGroup("puzzle_object");
            if (UnloadedPosition == Vector2.Zero) UnloadedPosition = Position;
            if (FTT.Core.EventBus.Instance != null) {
                FTT.Core.EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                FTT.Core.EventBus.Instance.OnRewindTriggered += OnRewind;
            }
            SetLoad(CurrentLoad);
        }

        public override void _ExitTree() {
            if (FTT.Core.EventBus.Instance != null) {
                FTT.Core.EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                FTT.Core.EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
        }

        public void SetLoad(float weight) {
            CurrentLoad = Mathf.Max(0f, weight);
            float ratio = Mathf.Clamp(CurrentLoad / Mathf.Max(0.1f, FullLoadWeight), 0f, 1f);
            Position = UnloadedPosition.Lerp(LoadedPosition, ratio);
        }

        public void CaptureCheckpointState(string checkpointID) => _checkpointLoad = CurrentLoad;

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            SetLoad(RewindPolicy == StoryRewindPolicy.ResetToInitialState ? 0f : _checkpointLoad);
        }

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();
    }
}
