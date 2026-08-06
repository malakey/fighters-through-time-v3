using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    public partial class TreasureChest : StaticBody2D, IInteractable, IStoryRewindable {
        [Signal] public delegate void OpenedEventHandler(string rewardID, int quantity);

        [Export] public string ChestID = "";
        [Export] public string InteractionPromptKey = "interaction_open_chest";
        [Export] public string RewardID = "chronal_dust";
        [Export(PropertyHint.Range, "1,100,1")] public int RewardQuantity = 10;
        [Export] public NodePath PuzzleManagerPath;
        [Export] public string ConditionID = "chest_opened";
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private bool _checkpointOpened;
        public bool IsOpened { get; private set; }
        public string InteractionID => ChestID;
        public string PromptKey => InteractionPromptKey;

        public override void _Ready() {
            AddToGroup("puzzle_object");
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewind;
            }
            ApplyPresentation();
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
        }

        public bool CanInteract(PlayerController player) => player != null && !IsOpened;
        public void Interact(PlayerController player) => Open();

        public bool Open() {
            if (IsOpened) return false;
            IsOpened = true;
            if (RewardID == "chronal_dust") EventBus.Instance?.RaiseChronalDustCollected(RewardQuantity);
            GetNodeOrNull<PuzzleManager>(PuzzleManagerPath)?.SetCondition(ConditionID, true);
            EmitSignal(SignalName.Opened, RewardID, RewardQuantity);
            ApplyPresentation();
            return true;
        }

        public void CaptureCheckpointState(string checkpointID) => _checkpointOpened = IsOpened;

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            IsOpened = RewindPolicy == StoryRewindPolicy.RestoreCheckpointState && _checkpointOpened;
            ApplyPresentation();
        }

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();
        private void ApplyPresentation() {
            if (GetNodeOrNull<Node2D>("Lid") is Node2D lid) lid.RotationDegrees = IsOpened ? -70f : 0f;
        }
    }
}
