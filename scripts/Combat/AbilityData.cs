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

		[ExportGroup("Hit (M08)")]
		/// <summary>
		/// M08 (Package 12 W3): hitstun an effective hit applies, in 60 Hz frames,
		/// before stagger/armor rules. The canonical value — the older seconds
		/// field <see cref="HitstunDuration"/> is now a read-only projection of it.
		/// </summary>
		[Export(PropertyHint.Range, "0,600,1")] public int HitstunFrames = 12;
		/// <summary>How the hit meets the ordinary block (Basic / Special full shatter / GuardCrush / Unblockable).</summary>
		[Export] public BlockClass BlockClass = BlockClass.Special;
		/// <summary>
		/// M05 launch flag: grants DI, tumble and landing tech. Data only in W3 —
		/// W3b implements the semantics; today every knockback hit still launches.
		/// </summary>
		[Export] public bool Launches;
		/// <summary>Delivery channel (DirectHit / Tick / Construct / Hazard): Rally reclaim, hitstop exemption, absorption.</summary>
		[Export] public HitDelivery Delivery = HitDelivery.DirectHit;
		/// <summary>Hit origin (Basic / Special / Ultimate / Throw / Environment): D03h meter rule, origin inheritance.</summary>
		[Export] public HitOrigin Origin = HitOrigin.Special;

		/// <summary>
		/// Deprecated reader kept for the kit scripts that build payloads in
		/// seconds. Derived from <see cref="HitstunFrames"/>; it is no longer
		/// exported, so it can never become a second canonical value.
		/// </summary>
		public float HitstunDuration => HitstunFrames / 60.0f;

		/// <summary>The runtime <see cref="AttackClass"/> this ability's hits carry (see <see cref="HitClassification.AttackClassFor"/>).</summary>
		public AttackClass ResolvedAttackClass => HitClassification.AttackClassFor(BlockClass, Origin);

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

		[ExportGroup("Ultimate (A02 / D15)")]
		/// <summary>
		/// D15: the finale hit that follows the <see cref="HitCount"/> regular
		/// hits of an Ultimate, read by both modes; 0 = no finale. The total is
		/// <c>HitCount × BaseDamage + FinaleDamage</c>
		/// (<see cref="UltimateActivationRules.ImpactTotal"/>).
		/// </summary>
		[Export] public float FinaleDamage;
		/// <summary>D15: whether the finale is an authored launcher (M05).</summary>
		[Export] public bool FinaleLaunches;
		/// <summary>A02: the Fighter-mode activation strike's shape. Story never reads it except for the Mirror Paradox clone.</summary>
		[Export] public UltimateActivationShape ActivationShape = UltimateActivationShape.Projectile;
		/// <summary>A02: how far the activation strike reaches, in pixels (60 px = 1 world unit).</summary>
		[Export] public float ActivationRange = 360f;
		/// <summary>A02: the activation strike's box, in pixels.</summary>
		[Export] public Vector2 ActivationHitboxSize = new(48f, 48f);
		/// <summary>A02 wind-up (hyper-armored; gravity 0 if started airborne). Provisional 20.</summary>
		[Export(PropertyHint.Range, "1,120,1")] public int ActivationWindupFrames = UltimateActivationRules.DefaultWindupFrames;
		/// <summary>A02 active frames (hyper-armored, unblockable). Provisional 10.</summary>
		[Export(PropertyHint.Range, "1,120,1")] public int ActivationActiveFrames = UltimateActivationRules.DefaultActiveFrames;
		/// <summary>A02 whiff recovery; Echo Step cannot undo it. Provisional 45.</summary>
		[Export(PropertyHint.Range, "0,240,1")] public int ActivationWhiffRecoveryFrames = UltimateActivationRules.DefaultWhiffRecoveryFrames;

		/// <summary>D15: regular hits plus the finale, if any.</summary>
		public int CinematicHitCount => UltimateActivationRules.CinematicHitCount(HitCount, FinaleDamage);

		/// <summary>D15: damage of the 1-based cinematic hit.</summary>
		public float CinematicHitDamage(int hitIndex) =>
			UltimateActivationRules.HitDamage(hitIndex, HitCount, BaseDamage, FinaleDamage);

		/// <summary>D15: true for the finale hit.</summary>
		public bool IsFinaleHit(int hitIndex) => UltimateActivationRules.IsFinaleHit(hitIndex, HitCount, FinaleDamage);

		/// <summary>D15: whether the 1-based hit carries <see cref="AppliedStatus"/>.</summary>
		public bool CinematicHitCarriesStatus(int hitIndex) =>
			UltimateActivationRules.HitCarriesStatus(hitIndex, HitCount, FinaleDamage, AppliedStatus);

		/// <summary>D15: <c>HitCount × BaseDamage + FinaleDamage</c> (status damage excluded).</summary>
		public float UltimateImpactTotal => UltimateActivationRules.ImpactTotal(HitCount, BaseDamage, FinaleDamage);

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
