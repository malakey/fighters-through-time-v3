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

        /// <summary>
        /// V7.3 ledge trump: the edge is contested, not first-come-first-served.
        /// A second grabber forces the current occupant off through its normal
        /// release path (which arms the regrab lockout) and takes the ledge.
        /// The Fighter sim resolves the same rule in its end-of-update trump
        /// pass.
        /// </summary>
        public bool TryAcquire(PlayerController player) {
            if (player == null || Occupant == player) return false;
            if (Occupant != null && IsInstanceValid(Occupant)) {
                Occupant.ForceLedgeTrumpRelease();
            }
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
