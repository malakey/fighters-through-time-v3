using Godot;

namespace FTT.Combat {

    public enum MovementType {
        Blink,
        Glide,
        Dash,
        Teleport,
        Warp,
        Float,
        /// <summary>Package 13 W7a (A08): a single strong gust burst along facing, then a normal fall (Prospero's Flight).</summary>
        Gust
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
