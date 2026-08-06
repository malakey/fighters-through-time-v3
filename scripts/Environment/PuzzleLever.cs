using Godot;
using FTT.Characters;
using FTT.Combat;

namespace FTT.Environment {

    public partial class PuzzleLever : Node2D, IInteractable, IStoryRewindable {
        [Signal] public delegate void ToggledEventHandler(bool isOn);

        [Export] public string LeverID = "";
        [Export] public string InteractionPromptKey = "interaction_use_lever";
        [Export] public NodePath PuzzleManagerPath;
        [Export] public string ConditionID = "lever_on";
        [Export] public Godot.Collections.Array<NodePath> RotationTargetPaths = new();
        [Export] public int RotationDirection = 1;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private Hurtbox _hurtbox;
        private bool _checkpointState;
        public bool IsOn { get; private set; }
        public string InteractionID => LeverID;
        public string PromptKey => InteractionPromptKey;

        public override void _Ready() {
            AddToGroup("puzzle_object");
            _hurtbox = GetNodeOrNull<Hurtbox>("Hurtbox");
            if (_hurtbox != null) _hurtbox.OnHit += OnHit;
            if (FTT.Core.EventBus.Instance != null) {
                FTT.Core.EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                FTT.Core.EventBus.Instance.OnRewindTriggered += OnRewind;
            }
            ApplyPresentation();
        }

        public override void _ExitTree() {
            if (_hurtbox != null) _hurtbox.OnHit -= OnHit;
            if (FTT.Core.EventBus.Instance != null) {
                FTT.Core.EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                FTT.Core.EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
        }

        public bool CanInteract(PlayerController player) => player != null;
        public void Interact(PlayerController player) => Toggle();

        public void Toggle() => SetState(!IsOn, rotateTargets: true);

        public void SetState(bool isOn, bool rotateTargets) {
            if (IsOn == isOn) return;
            IsOn = isOn;
            if (rotateTargets) {
                int direction = IsOn ? RotationDirection : -RotationDirection;
                foreach (NodePath path in RotationTargetPaths) {
                    if (GetNodeOrNull<Node>(path) is IQuarterTurnTarget target) target.RotateQuarterTurn(direction);
                }
            }
            GetNodeOrNull<PuzzleManager>(PuzzleManagerPath)?.SetCondition(ConditionID, IsOn);
            ApplyPresentation();
            EmitSignal(SignalName.Toggled, IsOn);
        }

        public void CaptureCheckpointState(string checkpointID) => _checkpointState = IsOn;

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            bool target = RewindPolicy == StoryRewindPolicy.ResetToInitialState ? false : _checkpointState;
            SetState(target, rotateTargets: false);
        }

        private float OnHit(HitPayload payload) { Toggle(); return 0f; }
        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();

        private void ApplyPresentation() {
            if (GetNodeOrNull<Node2D>("Visual") is Node2D visual) visual.RotationDegrees = IsOn ? 35f : -35f;
        }
    }
}
