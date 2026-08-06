using Godot;
using FTT.Characters;

namespace FTT.Environment {

    public partial class LedgeGrabPoint : Area2D {
        [Export] public NodePath HangAnchorPath = "HangAnchor";
        [Export] public NodePath StandAnchorPath = "StandAnchor";
        [Export] public bool StageIsToRight = true;

        public PlayerController Occupant { get; private set; }
        public Vector2 HangPosition => GetNodeOrNull<Marker2D>(HangAnchorPath)?.GlobalPosition ?? GlobalPosition;
        public Vector2 StandPosition => GetNodeOrNull<Marker2D>(StandAnchorPath)?.GlobalPosition ?? GlobalPosition;

        public override void _Ready() {
            AddToGroup("Ledge");
        }

        public bool TryAcquire(PlayerController player) {
            if (player == null || (Occupant != null && IsInstanceValid(Occupant))) return false;
            Occupant = player;
            return true;
        }

        public void Release(PlayerController player) {
            if (Occupant == player) Occupant = null;
        }

        public override void _ExitTree() {
            Occupant = null;
        }
    }
}
