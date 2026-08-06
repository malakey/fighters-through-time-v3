using Godot;

namespace FTT.Environment {

    public partial class RotatingPlatform : AnimatableBody2D, IQuarterTurnTarget, IStoryRewindable {
        [Signal] public delegate void RotationChangedEventHandler(int quarterTurns);

        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;
        [Export(PropertyHint.Range, "-1,1,2")] public int DefaultDirection = 1;

        private int _initialQuarterTurns;
        private int _checkpointQuarterTurns;
        public int QuarterTurns { get; private set; }

        public override void _Ready() {
            AddToGroup("puzzle_object");
            SyncToPhysics = false;
            _initialQuarterTurns = NormalizeQuarterTurns(Mathf.RoundToInt(RotationDegrees / 90f));
            QuarterTurns = _initialQuarterTurns;
            _checkpointQuarterTurns = QuarterTurns;
            if (FTT.Core.EventBus.Instance != null) {
                FTT.Core.EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                FTT.Core.EventBus.Instance.OnRewindTriggered += OnRewind;
            }
        }

        public override void _ExitTree() {
            if (FTT.Core.EventBus.Instance != null) {
                FTT.Core.EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                FTT.Core.EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
        }

        public void RotateQuarterTurn(int direction) {
            int resolvedDirection = direction == 0 ? DefaultDirection : Mathf.Sign(direction);
            SetQuarterTurns(QuarterTurns + resolvedDirection);
        }

        public void SetQuarterTurns(int quarterTurns) {
            QuarterTurns = NormalizeQuarterTurns(quarterTurns);
            RotationDegrees = QuarterTurns * 90f;
            EmitSignal(SignalName.RotationChanged, QuarterTurns);
        }

        public void CaptureCheckpointState(string checkpointID) => _checkpointQuarterTurns = QuarterTurns;

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            SetQuarterTurns(RewindPolicy == StoryRewindPolicy.ResetToInitialState
                ? _initialQuarterTurns
                : _checkpointQuarterTurns);
        }

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();
        private static int NormalizeQuarterTurns(int turns) => ((turns % 4) + 4) % 4;
    }
}
