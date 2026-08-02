using Godot;

namespace FTT.Combat {

	[GlobalClass]
	public partial class AbilityData : Resource {
		[ExportGroup("Identity")]
		[Export] public string AbilityName = "";
		[Export] public Texture2D AbilityIcon;
		[Export(PropertyHint.MultilineText)] public string AbilityDescription = "";

		[ExportGroup("Damage")]
		[Export] public float BaseDamage = 10f;
		[Export] public bool IsMultiHit;
		[Export] public int HitCount = 1;

		[ExportGroup("Physics")]
		[Export] public Vector2 KnockbackForce = new(3f, -2f);
		[Export] public float ProjectileSpeed;
		[Export] public PackedScene ProjectileScene;
		[Export] public bool GrantsHyperArmor;

		[ExportGroup("Hitbox")]
		[Export] public Vector2 HitboxSize = new(40f, 40f);
		[Export] public Vector2 HitboxOffset = new(30f, 0f);

		[ExportGroup("Timing")]
		[Export] public float CooldownDuration = 10f;
		[Export] public string AnimationName = "";

		[ExportGroup("Status")]
		[Export] public FTT.Core.StatusType AppliedStatus = FTT.Core.StatusType.None;
		[Export] public float StatusDuration;
		[Export] public float StatusIntensity;

		[ExportGroup("VFX")]
		[Export] public PackedScene ImpactVFXScene;
		[Export] public float ScreenShakeIntensity = 0.2f;
		[Export] public float ScreenShakeDuration = 0.15f;
	}
}
