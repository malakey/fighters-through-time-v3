using Godot;

namespace FTT.Combat {

	public enum AbilityExecutionType {
		Melee,
		Projectile,
		Area,
		PersistentObject,
		Movement,
		Cinematic
	}

	[GlobalClass]
	public partial class AbilityData : Resource {
		[ExportGroup("Identity")]
		[Export] public int SchemaVersion = 1;
		[Export] public string AbilityID = "";
		[Export] public string CharacterID = "";
		[Export] public FTT.Core.AbilitySlot Slot = FTT.Core.AbilitySlot.Special1;
		[Export] public AbilityExecutionType ExecutionType = AbilityExecutionType.Melee;
		[Export] public string AbilityName = "";
		[Export] public string DisplayNameKey = "";
		[Export] public Texture2D AbilityIcon;
		[Export(PropertyHint.MultilineText)] public string AbilityDescription = "";
		[Export] public string DescriptionKey = "";

		[ExportGroup("Damage")]
		[Export] public float BaseDamage = 10f;
		[Export] public bool IsMultiHit;
		[Export(PropertyHint.Range, "1,64,1")] public int HitCount = 1;
		[Export(PropertyHint.Range, "0,600,1")] public int DamageTickIntervalFrames;
		[Export] public float HitstunDuration = 0.2f;

		[ExportGroup("Physics")]
		[Export] public Vector2 KnockbackForce = new(3f, -2f);
		[Export] public float ProjectileSpeed;
		[Export] public PackedScene ProjectileScene;
		[Export] public bool ProjectilePierces;
		[Export] public bool GrantsHyperArmor;

		[ExportGroup("Hitbox")]
		[Export] public Vector2 HitboxSize = new(40f, 40f);
		[Export] public Vector2 HitboxOffset = new(30f, 0f);

		[ExportGroup("Timing (60 Hz frames)")]
		[Export(PropertyHint.Range, "0,600,1")] public int StartupFrames = 12;
		[Export(PropertyHint.Range, "1,600,1")] public int ActiveFrames = 6;
		[Export(PropertyHint.Range, "0,600,1")] public int RecoveryFrames = 12;
		[Export] public float CooldownDuration = 10f;
		[Export] public float Lifetime;
		[Export] public float ProjectileLifetime = 5f;

		[ExportGroup("Persistent Object")]
		[Export] public string PersistentObjectID = "";
		[Export] public PackedScene PersistentObjectScene;
		[Export(PropertyHint.Range, "0,8,1")] public int MaxActiveObjects;

		[ExportGroup("Status")]
		[Export] public FTT.Core.StatusType AppliedStatus = FTT.Core.StatusType.None;
		[Export] public float StatusDuration;
		[Export] public float StatusIntensity = 1f;

		[ExportGroup("Presentation")]
		[Export] public string AnimationName = "";
		[Export] public AudioStream CastSFX;
		[Export] public AudioStream ImpactSFX;
		[Export] public PackedScene CastVFXScene;
		[Export] public PackedScene ImpactVFXScene;
		[Export(PropertyHint.Range, "0,1,0.01")] public float ScreenShakeIntensity = 0.2f;
		[Export] public float ScreenShakeDuration = 0.15f;

		public float StartupDuration => StartupFrames / 60.0f;
		public float ActiveDuration => ActiveFrames / 60.0f;
		public float RecoveryDuration => RecoveryFrames / 60.0f;

		public bool HasValidIdentity() => !string.IsNullOrWhiteSpace(AbilityID)
			&& !string.IsNullOrWhiteSpace(CharacterID)
			&& !string.IsNullOrWhiteSpace(DisplayNameKey)
			&& !string.IsNullOrWhiteSpace(DescriptionKey);
	}
}
