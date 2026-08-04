using Godot;

namespace FTT.Characters {

	public enum CombatStyle {
		Melee,
		Ranged,
		Hybrid
	}

	public enum JumpType {
		SingleJump,
		DoubleJump
	}

	[GlobalClass]
	public partial class CharacterData : Resource {
		[ExportGroup("Identity")]
		[Export] public int SchemaVersion = 1;
		[Export] public string CharacterID = "";
		[Export] public string DisplayName = "";
		[Export] public string DisplayNameKey = "";
		[Export] public Texture2D CharacterPortrait;
		[Export] public CombatStyle Style = CombatStyle.Hybrid;

		[ExportGroup("Health & Weight")]
		[Export] public int MaxHP = 100;
		[Export] public float Weight = 1.0f;
		[Export] public int MaxBlockCharges = 3;

		[ExportGroup("Movement")]
		[Export] public float MaxMoveSpeed = 8.0f;
		[Export] public float Acceleration = 40.0f;
		[Export] public float GroundFriction = 20.0f;
		[Export] public float MaxJumpForce = 14.0f;
		[Export] public int MaxJumpCount = 2;
		[Export] public float AirControlMultiplier = 0.9f;

		[ExportGroup("Combat")]
		[Export] public float BasicAttackDamage = 10.0f;
		[Export] public float BasicAttackKnockback = 3.0f;

		[ExportGroup("Abilities")]
		[Export] public FTT.Combat.AbilityData SpecialAttackOne;
		[Export] public FTT.Combat.AbilityData SpecialAttackTwo;
		[Export] public FTT.Combat.MovementAbilityData MovementAbility;
		[Export] public FTT.Combat.AbilityData UltimateAttack;

		[ExportGroup("Animation")]
		[Export] public SpriteFrames SpriteFramesResource;
	}
}
