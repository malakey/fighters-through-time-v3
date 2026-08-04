using Godot;

namespace FTT.Combat {

    public enum MovementType {
        Blink,
        Glide,
        Dash,
        Teleport,
        Warp,
        Float
    }

    [GlobalClass]
    public partial class MovementAbilityData : AbilityData {
        [ExportGroup("Movement")]
        [Export] public MovementType MovementType = MovementType.Dash;
        [Export] public float MovementDuration = 0.2f;
        [Export] public float DistanceMoved;
        [Export] public float MovementSpeed;
        [Export] public bool ResetsDoubleJump;
    }
}
