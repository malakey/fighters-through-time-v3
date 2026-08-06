using Godot;
using System.Collections.Generic;
using FTT.Characters;

namespace FTT.Environment {

    public partial class ConductiveCoil : PowerRoutingNode, IInteractable, IQuarterTurnTarget, IStoryRewindable {
        [Export] public string CoilID = "";
        [Export] public string InteractionPromptKey = "interaction_rotate_coil";
        [Export] public Godot.Collections.Array<NodePath> EastOutputPaths = new();
        [Export] public Godot.Collections.Array<NodePath> SouthOutputPaths = new();
        [Export] public Godot.Collections.Array<NodePath> WestOutputPaths = new();
        [Export] public Godot.Collections.Array<NodePath> NorthOutputPaths = new();
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private int _checkpointQuarterTurns;
        private int _initialQuarterTurns;
        public int QuarterTurns { get; private set; }
        public string InteractionID => CoilID;
        public string PromptKey => InteractionPromptKey;

        public override void _Ready() {
            QuarterTurns = Normalize(Mathf.RoundToInt(RotationDegrees / 90f));
            _initialQuarterTurns = QuarterTurns;
            _checkpointQuarterTurns = QuarterTurns;
            base._Ready();
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
            base._ExitTree();
        }

        public bool CanInteract(PlayerController player) => player != null;
        public void Interact(PlayerController player) => RotateQuarterTurn(1);

        public void RotateQuarterTurn(int direction) => SetQuarterTurns(QuarterTurns + (direction < 0 ? -1 : 1));

        public void SetQuarterTurns(int turns) {
            QuarterTurns = Normalize(turns);
            RotationDegrees = QuarterTurns * 90f;
            RefreshPowerRouting();
        }

        public void CaptureCheckpointState(string checkpointID) => _checkpointQuarterTurns = QuarterTurns;

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            SetQuarterTurns(RewindPolicy == StoryRewindPolicy.ResetToInitialState
                ? _initialQuarterTurns
                : _checkpointQuarterTurns);
        }

        protected override IEnumerable<NodePath> GetActiveOutputPaths() => QuarterTurns switch {
            0 => EastOutputPaths,
            1 => SouthOutputPaths,
            2 => WestOutputPaths,
            _ => NorthOutputPaths
        };

        protected override void OnPowerStateChanged(bool powered) {
            if (GetNodeOrNull<CanvasItem>("PoweredVisual") is CanvasItem visual) visual.Visible = powered;
        }

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();
        private static int Normalize(int turns) => ((turns % 4) + 4) % 4;
    }
}
