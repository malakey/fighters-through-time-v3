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
        Gust = 6,
        // Package 13 W7b (A08, C03). Explicit ordinals: the enum serializes as
        // an int into every movement .tres and into the sim loadout, and W7a
        // appends in parallel — 10+ keeps the two waves from colliding.
        /// <summary>Joan's Ascendant Wings: a rising slash-leap, then the held Wing-Dive (A08).</summary>
        WingDive = 10,
        /// <summary>Cleopatra's Desert Mirage: an 8-direction sand rush that passes through opponents (C03).</summary>
        SandRush = 11
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
