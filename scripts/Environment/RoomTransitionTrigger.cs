using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    public partial class RoomTransitionTrigger : Area2D {
        [Signal] public delegate void RoomEnteredEventHandler(string roomID);

        [Export] public string RoomID = "";
        [Export] public Rect2 CameraBounds = new(0, 0, 1920, 1080);
        [Export] public NodePath CameraPath;
        [Export] public NodePath EncounterRootPath;
        [Export] public bool ActivateOnce = true;

        public bool HasActivated { get; private set; }

        public override void _Ready() {
            AddToGroup("room_transition");
            BodyEntered += OnBodyEntered;
            SetEncounterActive(false);
        }

        public override void _ExitTree() => BodyEntered -= OnBodyEntered;

        public bool ActivateRoom(PlayerController player) {
            if (player == null || (ActivateOnce && HasActivated)) return false;
            HasActivated = true;
            GetNodeOrNull<StoryCameraConfiner>(CameraPath)?.SetBounds(CameraBounds);
            SetEncounterActive(true);
            EventBus.Instance?.RaiseRoomTransitioned(new RoomTransitionPayload {
                RoomID = RoomID,
                CameraBounds = CameraBounds
            });
            EmitSignal(SignalName.RoomEntered, RoomID);
            return true;
        }

        private void OnBodyEntered(Node2D body) { if (body is PlayerController player) ActivateRoom(player); }

        private void SetEncounterActive(bool active) {
            Node encounter = GetNodeOrNull<Node>(EncounterRootPath);
            if (encounter == null) return;
            encounter.ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            if (encounter is CanvasItem item) item.Visible = active;
            foreach (Node child in encounter.GetChildren()) {
                if (child is CanvasItem childItem) childItem.Visible = active;
            }
        }
    }
}
