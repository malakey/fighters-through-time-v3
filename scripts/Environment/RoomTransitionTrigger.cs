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
            // V7.6 Time Freeze: a room revealed DURING an active freeze is frozen
            // before its actors can take a tick. This runs synchronously right
            // after the encounter root is enabled and before anything has
            // processed, which is what "suspend adjacent-room simulation so
            // off-screen attacks cannot enter" reduces to in practice.
            TimeFreezeController.FreezeActivatedRoom(GetTree());
            EventBus.Instance?.RaiseRoomTransitioned(new RoomTransitionPayload {
                RoomID = RoomID,
                CameraBounds = CameraBounds
            });
            EmitSignal(SignalName.RoomEntered, RoomID);
            return true;
        }

        private void OnBodyEntered(Node2D body) {
            if (body is not PlayerController player) return;
            // Room activation cascades into wave spawns and encounter
            // re-enabling, which the engine forbids during the in/out flush
            // this signal runs in; activate right after the flush. Direct
            // ActivateRoom calls (tests, resume restore) stay synchronous.
            Callable.From(() => {
                if (IsInstanceValid(player)) ActivateRoom(player);
            }).CallDeferred();
        }

        private void SetEncounterActive(bool active) {
            Node encounter = GetNodeOrNull<Node>(EncounterRootPath);
            if (encounter == null) return;
            encounter.ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            if (encounter is CanvasItem item) item.Visible = active;
            Godot.Collections.Array<Node> children = encounter.GetChildren();
            using var childrenLifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is CanvasItem childItem) childItem.Visible = active;
            }
        }
    }
}
