using Godot;
using FTT.Characters;

namespace FTT.Environment {

    /// <summary>
    /// Interactable rotating gear for gear-train puzzles (Florence print shop).
    /// Each interaction rotates this gear one quarter turn and drives every
    /// linked gear with it. When the gear reaches <see cref="TargetQuarterTurns"/>
    /// it satisfies its condition on the owning <see cref="PuzzleManager"/>.
    /// </summary>
    public partial class RotatingGear : Node2D, IInteractable, IQuarterTurnTarget, IStoryRewindable {
        [Signal] public delegate void RotationChangedEventHandler(int quarterTurns);

        [Export] public string GearID = "";
        [Export] public string InteractionPromptKey = "interaction_rotate_gear";
        [Export] public NodePath PuzzleManagerPath;
        [Export] public string ConditionID = "";
        [Export] public int TargetQuarterTurns;
        [Export] public Godot.Collections.Array<NodePath> LinkedGearPaths = new();
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private int _initialQuarterTurns;
        private int _checkpointQuarterTurns;
        public int QuarterTurns { get; private set; }

        public string InteractionID => GearID;
        public string PromptKey => InteractionPromptKey;

        public override void _Ready() {
            AddToGroup("puzzle_object");
            _initialQuarterTurns = NormalizeQuarterTurns(Mathf.RoundToInt(RotationDegrees / 90f));
            QuarterTurns = _initialQuarterTurns;
            _checkpointQuarterTurns = QuarterTurns;
            if (FTT.Core.EventBus.Instance != null) {
                FTT.Core.EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                FTT.Core.EventBus.Instance.OnRewindTriggered += OnRewind;
            }
            PublishCondition();
        }

        public override void _ExitTree() {
            if (FTT.Core.EventBus.Instance != null) {
                FTT.Core.EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                FTT.Core.EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
        }

        public bool CanInteract(PlayerController player) => player != null;

        public void Interact(PlayerController player) {
            RotateQuarterTurn(1);
            foreach (NodePath path in LinkedGearPaths) {
                if (GetNodeOrNull<Node>(path) is IQuarterTurnTarget linked) linked.RotateQuarterTurn(1);
            }
        }

        public void RotateQuarterTurn(int direction) =>
            SetQuarterTurns(QuarterTurns + (direction == 0 ? 1 : Mathf.Sign(direction)));

        public void SetQuarterTurns(int quarterTurns) {
            QuarterTurns = NormalizeQuarterTurns(quarterTurns);
            RotationDegrees = QuarterTurns * 90f;
            EmitSignal(SignalName.RotationChanged, QuarterTurns);
            PublishCondition();
        }

        public bool IsAligned => QuarterTurns == NormalizeQuarterTurns(TargetQuarterTurns);

        public void CaptureCheckpointState(string checkpointID) => _checkpointQuarterTurns = QuarterTurns;

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            SetQuarterTurns(RewindPolicy == StoryRewindPolicy.ResetToInitialState
                ? _initialQuarterTurns
                : _checkpointQuarterTurns);
        }

        private void PublishCondition() {
            if (string.IsNullOrWhiteSpace(ConditionID)) return;
            GetNodeOrNull<PuzzleManager>(PuzzleManagerPath)?.SetCondition(ConditionID, IsAligned);
        }

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();
        private static int NormalizeQuarterTurns(int turns) => ((turns % 4) + 4) % 4;
    }
}
