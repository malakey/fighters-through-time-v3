using Godot;
using System;
using System.Collections.Generic;
using FTT.Core;

namespace FTT.Characters {

	public enum CharacterState {
		Idle,
		Running,
		/// <summary>
		/// Reserved. The universal dash mechanic was removed (2026-08-09 user
		/// directive); the member stays so serialized/int-cast state and the
		/// enum's ordinal layout are undisturbed. No code enters this state.
		/// </summary>
		Dashing,
		Rolling,
		Skidding,
		Crouching,
		Airborne,
		Attacking,
		UsingSpecial,
		UsingUltimate,
		Blocking,
		Stunned,
		Dazed,
		LedgeHanging,
		Dead,
		Respawning,
		UsingMovementAbility
	}

	public partial class PlayerController : CharacterBody2D {
		public const int StoryRewindInvulnerabilityFrames = 120;

		/// <summary>Story-only Resonance perk key (Joan): landing the final combo cleave heals 5% of missing HP.</summary>
		public const string ZealousVigorPerkKey = "zealous_vigor";
		/// <summary>Story-only Resonance perk key (Joan): blocked damage builds the Ultimate Meter 25% faster.</summary>
		public const string ShieldOfOrleansPerkKey = "shield_of_orleans";
		private const float ZealousVigorMissingHPFraction = 0.05f;
		private const float ShieldOfOrleansMeterMultiplier = 1.25f;

		[Export] public CharacterData Data;
		[Export] public int PlayerIndex = 0;
		public FTT.Core.PlayerInputFrame CurrentInputFrame { get; private set; }

		// Runtime state
		public CharacterState CurrentState { get; private set; } = CharacterState.Idle;
		public int CurrentHP;
		public int CurrentBlockCharges;
		public float CurrentUltimateMeter;
		public bool IsFacingRight = true;
		public int RemainingJumps;
		public float SpecialOneCooldownTimer;
		public float SpecialTwoCooldownTimer;
		public float MovementAbilityCooldownTimer;
		public List<Node2D> ActivePersistentObjects = new();
		public int ComboCounter;
		public int GroundComboCounter { get; private set; }
		public int AerialComboCounter { get; private set; }
		public float StatusMovementMultiplier { get; set; } = 1.0f;
		public float StatusJumpMultiplier { get; set; } = 1.0f;
		public float StatusAnimationMultiplier { get; set; } = 1.0f;
		public float StatusDamageTakenMultiplier { get; set; } = 1.0f;
		public bool IsMovementRooted { get; set; }
		/// <summary>
		/// Story-only environment gravity scale (low-gravity fields, Chronal Void
		/// shifts). 1.0 is normal gravity; multiplied into the Story gravity
		/// integration only. Owned by <c>FTT.Environment.EnvironmentPlayerModifiers</c>
		/// so overlapping zones stack instead of clobbering each other's restore.
		/// Fighter Mode never reads this — <c>scripts/FighterSim/</c> is authoritative
		/// there and has no equivalent field.
		/// </summary>
		public float EnvironmentGravityScale { get; set; } = 1.0f;
		/// <summary>
		/// Story-only environment movement multiplier (deep sand, submerged water).
		/// Stacks multiplicatively with — and never replaces — the status-effect
		/// <see cref="StatusMovementMultiplier"/>. Same ownership and isolation rules
		/// as <see cref="EnvironmentGravityScale"/>.
		/// </summary>
		public float EnvironmentMoveMultiplier { get; set; } = 1.0f;
		public int StoryMaxHPBonus { get; set; }
		public int StoryBlockChargeBonus { get; set; }
		public float StoryMoveSpeedMultiplier { get; set; } = 1f;
		public float StoryJumpForceMultiplier { get; set; } = 1f;
		public float StoryBasicDamageMultiplier { get; set; } = 1f;
		public float StorySpecialDamageMultiplier { get; set; } = 1f;
		// Story-only Resonance minor stat multipliers (all neutral at 1f; populated
		// by CharacterFactory from the resolved StoryStatProfile of the active
		// save's grid). Fighter Mode never sets these — the deterministic
		// simulation is authoritative there and its loadouts never read them.
		/// <summary>Cooldown time multiplier: 0.9 means cooldowns run 10% shorter.</summary>
		public float StoryCooldownMultiplier { get; set; } = 1f;
		public float StoryAttackRangeMultiplier { get; set; } = 1f;
		public float StoryComboSpeedMultiplier { get; set; } = 1f;
		public float StoryBlockRecoveryMultiplier { get; set; } = 1f;
		public float StoryKnockbackMultiplier { get; set; } = 1f;
		public float StoryProjectileSpeedMultiplier { get; set; } = 1f;
		public float StoryProjectileDamageMultiplier { get; set; } = 1f;
		public float StoryGlideSpeedMultiplier { get; set; } = 1f;
		/// <summary>Story-only "GlideDuration" minors (e.g. Pocahontas wr2) lengthen glide windows.</summary>
		public float StoryGlideDurationMultiplier { get; set; } = 1f;
		/// <summary>Story-only "ZoneRadius" minors (Einstein u2, Leonardo a2, Cleopatra dm1) widen authored ability zones.</summary>
		public float StoryZoneRadiusMultiplier { get; set; } = 1f;
		/// <summary>Story-only "ZoneDuration" minors (Cleopatra dm2) lengthen authored ability zones.</summary>
		public float StoryZoneDurationMultiplier { get; set; } = 1f;
		public float StoryPersistentDurationMultiplier { get; set; } = 1f;
		public float StoryPersistentRangeMultiplier { get; set; } = 1f;
		public float StoryPersistentHealthMultiplier { get; set; } = 1f;
		public float StoryStatusDurationMultiplier { get; set; } = 1f;
		/// <summary>Damaging-status (Venom/RadiantBurn) potency multiplier.</summary>
		public float StoryStatusIntensityMultiplier { get; set; } = 1f;
		public float StoryTemporaryDamageMultiplier { get; private set; } = 1f;
		public float StoryTemporarySpeedMultiplier { get; private set; } = 1f;
		/// <summary>
		/// Unlocked Resonance major-perk keys (Story-only). Populated by
		/// CharacterFactory from the active save; always empty in Fighter Mode.
		/// </summary>
		public HashSet<string> StoryAbilityPerks { get; } = new(StringComparer.Ordinal);
		public bool HasStoryPerk(string abilityModifierKey) =>
			!string.IsNullOrWhiteSpace(abilityModifierKey) && StoryAbilityPerks.Contains(abilityModifierKey);

		// Story-only perk shield (Wardenclyffe Shield, Royal Aegis, Leaf Barrier,
		// ...): absorbs damage before HP. Capacity is configured by the owning
		// perk's ability code; Fighter Mode never reads these fields because the
		// deterministic simulation is authoritative there.
		public float StoryShieldPoints { get; private set; }
		public float StoryShieldCapacity { get; private set; }

		public void ConfigureStoryShield(float capacity) {
			StoryShieldCapacity = MathF.Max(0f, capacity);
			StoryShieldPoints = MathF.Min(StoryShieldPoints, StoryShieldCapacity);
		}

		public void RechargeStoryShield(float amount) {
			if (StoryShieldCapacity <= 0f || amount <= 0f) return;
			StoryShieldPoints = MathF.Min(StoryShieldCapacity, StoryShieldPoints + amount);
		}

		// Story-only projectile immunity (Virtuoso Dash, Rest Shield, ...): while
		// active, hits whose HitboxID is "projectile" (the placeholder-projectile
		// hit path) are absorbed outright. Granting perks refresh the window each
		// frame; Fighter Mode never reads this because the deterministic
		// simulation is authoritative there.
		public bool HasStoryProjectileImmunity => _storyProjectileImmunityFrames > 0;

		public void GrantStoryProjectileImmunity(float durationSeconds) {
			_storyProjectileImmunityFrames = Math.Max(
				_storyProjectileImmunityFrames,
				Mathf.RoundToInt(durationSeconds * 60f));
		}

		private int _storyProjectileImmunityFrames;
		/// <summary>
		/// Story-only timed hyper-armor granted by perks (Homestead Bulwark, Joan's
		/// Unstoppable Crusade window). While active the player takes damage but
		/// ignores hitstun/knockback from non-ultimate hits, matching the
		/// phase-based ability hyper-armor rules. Fighter Mode never reads this.
		/// </summary>
		public bool StoryHyperArmorActive => _storyHyperArmorFrames > 0;

		public void ApplyStoryHyperArmor(float durationSeconds) {
			_storyHyperArmorFrames = Math.Max(
				_storyHyperArmorFrames,
				Mathf.RoundToInt(durationSeconds * 60f));
		}
		/// <summary>
		/// Remaining float-glide time granted by movement abilities (Einstein's
		/// Relativity Warp cancel). While positive, gravity is heavily reduced.
		/// </summary>
		public float StoryFloatTimer { get; set; }
		public bool IsPostRewindInvulnerable => _postRewindInvulnerabilityFrames > 0;
		/// <summary>
		/// Encounter-scoped HP pool that replaces the character baseline entirely.
		/// Set only by boss encounters that reuse a character body — the Level 13
		/// Mirror Paradox and its 1000-HP <c>BossData</c> pool. Zero (the default)
		/// keeps the normal <c>CharacterData</c> + Resonance baseline. This is not a
		/// Resonance bonus and never participates in Story stat resolution.
		/// </summary>
		public int EncounterMaxHPOverride { get; set; }
		/// <summary>
		/// Story-side opponent marker, set only by encounters that reuse a character
		/// body as a hostile (the Level 13 Mirror Paradox clone). Projectiles this
		/// controller fires join the <c>enemy_projectile</c> group so a Chronal
		/// Rewind's world clear removes them like any other hostile shot (audit
		/// H-8). Never set on the campaign avatar; Fighter Mode never reads it.
		/// </summary>
		public bool IsStoryHostile { get; set; }
		public int MaximumHP => EncounterMaxHPOverride > 0
			? EncounterMaxHPOverride
			: (Data?.MaxHP ?? 100) + StoryMaxHPBonus;
		public int MaximumBlockCharges => (Data?.MaxBlockCharges ?? 3) + StoryBlockChargeBonus;
		private float EffectiveMoveSpeed => (Data?.MaxMoveSpeed ?? 8f) * StoryMoveSpeedMultiplier
			* StoryTemporarySpeedMultiplier * EnvironmentMoveMultiplier;
		private float EffectiveJumpForce => (Data?.MaxJumpForce ?? 14f) * StoryJumpForceMultiplier;

		// Physics constants
		private const float BaseGravity = 18.0f;
		private const float FallGravityMultiplier = 1.8f;
		private const float ShortHopGravityMultiplier = 2.5f;
		private const float DirectionReversalPenalty = 0.7f;
		private const float TerminalVelocity = 600.0f;
		private const float GroundRampFrames = FTT.Core.UniversalMovementRules.RunAccelerationFrames;
		/// <summary>
		/// Grounded stop ramp (2026-08-10 feel batch §2.1). Every site that bleeds
		/// horizontal speed toward zero on the ground — idle/skid/crouch settling,
		/// the block stance, roll startup/recovery, and a released or reversed
		/// stick while running — uses this instead of the accel ramp.
		/// </summary>
		private const float GroundDecelRampFrames = FTT.Core.UniversalMovementRules.RunDecelerationFrames;
		private const float AirAccelRampFrames = 4.0f;
		private const float AirDecelRampFrames = 8.0f;
		/// <summary>Story pixel-space fast-fall floor: <c>FastFallSpeed</c> units/s × 60.</summary>
		private const float FastFallSpeedPixels = FTT.Core.UniversalMovementRules.FastFallSpeed * 60f;

		// Timers
		private float _coyoteTimer;
		private const float CoyoteTime = 0.1f;
		private float _jumpBufferTimer;
		private const float JumpBufferTime = 0.1f;
		private float _skidTimer;
		private const float SkidDuration = 0.05f;
		private float _ledgeHangTimer;
		private const float LedgeHangMaxTime = 5.0f;
		private float _stunTimer;
		private float _dazeTimer;
		private const float DazeDuration = 1.0f;
		private float _respawnTimer;
		private float _dropThroughTimer;
		private const float DropThroughDuration = 0.25f;
		private int _dropThroughFramesRemaining;
		private int _downTapFramesRemaining;
		private int _temporaryDamageBuffFrames;
		private int _temporarySpeedBuffFrames;
		private int _postRewindInvulnerabilityFrames;
		private int _storyHyperArmorFrames;

		// Jump tracking
		private bool _jumpHeld;
		private bool _wasGrounded;

		// Crouching
		private float _normalHurtboxHeight;
		private Vector2 _normalBodyShapePosition;
		private Vector2 _normalHurtboxSize;
		private Vector2 _normalHurtboxPosition;

		// Drop-through
		private PhysicsBody2D _dropThroughPlatform;

		// Node references
		private AnimatedSprite2D _animatedSprite;
		private CollisionShape2D _collisionShape;
		private Area2D _ledgeDetector;
		private AnimationPlayer _combatAnimationPlayer;
		private Marker2D _aerialHitboxMarker;
		private FTT.Combat.GlowPresentationController _glow;
		private FTT.Environment.LedgeGrabPoint _activeLedge;

		// Combat wiring
		private FTT.Combat.BaseSpecial _special1;
		private FTT.Combat.BaseSpecial _special2;
		private FTT.Combat.BaseSpecial _movementAbility;
		private FTT.Combat.BaseSpecial _ultimate;
		private FTT.Combat.UltimateMeter _ultimateMeter;
		private FTT.Combat.BlockSystem _blockSystem;
		private FTT.Combat.StatusController _statusController;
		private FTT.Combat.Hurtbox _hurtbox;
		private CollisionShape2D _hurtboxShape;
		private FTT.Combat.CombatantPushbox _pushbox;
		private FTT.Combat.Hitbox _meleeHitbox;
		private uint _rewindCollisionLayer;
		private uint _rewindCollisionMask;
		private bool _rewindSuspended;
		private int _rollFrame;
		private int _rollDirection;
		private bool _rollInvulnerable;
		private bool _hyperArmorPresentationActive;
		private bool _spawnInvulnerabilityPresentationActive;

		// === Package 8 B5: footsteps ===
		// Presentation only; nothing here is read by movement, combat, or rewind.

		/// <summary>
		/// Surface the character is standing on, used to pick a footstep sound.
		/// Levels set this from their geometry as production audio lands; until then
		/// every surface resolves to the single placeholder step through
		/// <c>AudioManager.PlayFootstep</c>'s fallback.
		/// </summary>
		public string FootstepSurfaceID = "";

		private readonly FTT.Core.FootstepCadence _footsteps = new();

		// Basic attack timing
		private int _attackFramesRemaining;
		private float _attackFrameProgress;
		private const int ComboBufferFrames = FTT.Combat.StoryCombatRules.ComboBufferFrames;
		private int _comboBufferFramesRemaining;
		private bool _comboBufferActive;
		private bool _nextAttackBuffered;
		private bool _attackHitActive;
		private bool _attackInRecovery;
		private bool _inRecoveryHold;
		private int _pendingSpecialSlot;
		private bool _attackStartedAerial;
		private bool _attackStartedCrouched;
		private bool _attackAnimationDriven;
		/// <summary>
		/// Which basic the running swing is (gameplay feel §2.8):
		/// <c>BasicComboRules.VariantChain</c>, <c>VariantUpAttack</c>, or
		/// <c>VariantDownAir</c>. The two directional strikes are single swings
		/// outside the chain — they never buffer, hold, or advance a counter.
		/// </summary>
		private int _attackVariant = FTT.Combat.BasicComboRules.VariantChain;
		private bool _specialStartedAerial;
		private bool _ultimateStartedAerial;

		// Built from FTT.Combat.BasicComboRules — the one shared rulebook with
		// the Fighter simulation. Do not author numbers here.
		private static readonly FTT.Combat.CombatFrameTimeline[] ComboTimelines = BuildComboTimelines(
			FTT.Combat.BasicComboRules.GroundStartupFrames,
			FTT.Combat.BasicComboRules.GroundActiveFrames,
			FTT.Combat.BasicComboRules.GroundRecoveryFrames);
		private static readonly FTT.Combat.CombatFrameTimeline[] AerialComboTimelines = BuildComboTimelines(
			FTT.Combat.BasicComboRules.AerialStartupFrames,
			FTT.Combat.BasicComboRules.AerialActiveFrames,
			FTT.Combat.BasicComboRules.AerialRecoveryFrames);

		private static FTT.Combat.CombatFrameTimeline[] BuildComboTimelines(
			int[] startup, int[] active, int[] recovery) {
			var timelines = new FTT.Combat.CombatFrameTimeline[startup.Length];
			for (int index = 0; index < startup.Length; index++) {
				timelines[index] = new(startup[index], active[index], recovery[index]);
			}
			return timelines;
		}
		private static readonly float[] ComboDamageMultipliers = { 0.8f, 1.0f, 1.5f };
		private static readonly Vector2[] ComboHitboxSizes = {
			new(72f, 60f),
			new(84f, 72f),
			new(108f, 84f)
		};
		private static readonly Vector2[] ComboHitboxOffsets = {
			new(60f, -32f),
			new(60f, -20f),
			new(72f, -32f)
		};
		private static readonly Vector2[] AerialComboHitboxSizes = {
			new(72f, 64f),
			new(82f, 82f),
			new(88f, 104f)
		};
		private static readonly Vector2[] AerialComboHitboxOffsets = {
			new(58f, -46f),
			new(42f, -50f),
			new(20f, -12f)
		};

		// Directional attacks (§2.8). Frames come from the shared rulebook; the
		// pixel boxes mirror the simulation's world-unit reaches at the
		// repository's 62.5 px/unit convention (up-attack |dx| <= 1.2 and 2.4
		// above the origin; down-air |dx| <= 1.0 and 2.0 below). Offsets are
		// facing-independent — these strikes are centred on the fighter.
		private static readonly FTT.Combat.CombatFrameTimeline UpAttackTimeline = new(
			FTT.Combat.BasicComboRules.UpAttackStartupFrames,
			FTT.Combat.BasicComboRules.UpAttackActiveFrames,
			FTT.Combat.BasicComboRules.UpAttackRecoveryFrames);
		private static readonly FTT.Combat.CombatFrameTimeline DownAirTimeline = new(
			FTT.Combat.BasicComboRules.DownAirStartupFrames,
			FTT.Combat.BasicComboRules.DownAirActiveFrames,
			FTT.Combat.BasicComboRules.DownAirRecoveryFrames);
		private static readonly Vector2 UpAttackHitboxSize = new(150f, 150f);
		private static readonly Vector2 UpAttackHitboxOffset = new(0f, -75f);
		private static readonly Vector2 DownAirHitboxSize = new(125f, 125f);
		private static readonly Vector2 DownAirHitboxOffset = new(0f, 62.5f);

		public override void _Ready() {
			AddToGroup("StoryPlayer");
			if (Data != null) {
				CurrentHP = MaximumHP;
				CurrentBlockCharges = MaximumBlockCharges;
				RemainingJumps = Data.MaxJumpCount;
			}

			_animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
			_collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			_ledgeDetector = GetNodeOrNull<Area2D>("LedgeDetector");
			_combatAnimationPlayer = GetNodeOrNull<AnimationPlayer>("CombatAnimationPlayer");
			_aerialHitboxMarker = GetNodeOrNull<Marker2D>("AerialHitboxMarker");
			_glow = GetNodeOrNull<FTT.Combat.GlowPresentationController>(
				FTT.Combat.GlowPresentationController.NodeName);

			if (_collisionShape?.Shape is RectangleShape2D rect) {
				_normalHurtboxHeight = rect.Size.Y;
				_normalBodyShapePosition = _collisionShape.Position;
			}

			if (_ledgeDetector != null) {
				_ledgeDetector.AreaEntered += OnLedgeAreaEntered;
			}
			if (_combatAnimationPlayer != null) {
				_combatAnimationPlayer.AnimationFinished += OnCombatAnimationFinished;
			}

			MotionMode = MotionModeEnum.Grounded;
			UpDirection = Vector2.Up;
			FloorStopOnSlope = true;

			InitializeCombatNodes();
		}

		public override void _ExitTree() {
			if (_ledgeDetector != null) _ledgeDetector.AreaEntered -= OnLedgeAreaEntered;
			if (_combatAnimationPlayer != null) _combatAnimationPlayer.AnimationFinished -= OnCombatAnimationFinished;
			if (_hurtbox != null) _hurtbox.OnHit -= OnHurtboxHit;
			if (_meleeHitbox != null) _meleeHitbox.HitConfirmed -= OnMeleeHitConfirmed;
			ReleaseActiveLedge();
			if (_dropThroughPlatform != null && IsInstanceValid(_dropThroughPlatform)) {
				RemoveCollisionExceptionWith(_dropThroughPlatform);
			}
		}

		public void InitializeCombatNodes() {
			_special1 = GetNodeOrNull<FTT.Combat.BaseSpecial>("Special1");
			_special2 = GetNodeOrNull<FTT.Combat.BaseSpecial>("Special2");
			_movementAbility = GetNodeOrNull<FTT.Combat.BaseSpecial>("MovementAbility");
			_ultimate = GetNodeOrNull<FTT.Combat.BaseSpecial>("Ultimate");
			_ultimateMeter = GetNodeOrNull<FTT.Combat.UltimateMeter>("UltimateMeter");
			_blockSystem = GetNodeOrNull<FTT.Combat.BlockSystem>("BlockSystem");
			_statusController = GetNodeOrNull<FTT.Combat.StatusController>("StatusController");
			_hurtbox = GetNodeOrNull<FTT.Combat.Hurtbox>("Hurtbox");
			_pushbox = GetNodeOrNull<FTT.Combat.CombatantPushbox>("Pushbox");
			_meleeHitbox = GetNodeOrNull<FTT.Combat.Hitbox>("MeleeHitbox");

			if (_meleeHitbox != null) {
				// Guard against double subscription: InitializeCombatNodes can run
				// again after CharacterFactory adds combat children.
				_meleeHitbox.HitConfirmed -= OnMeleeHitConfirmed;
				_meleeHitbox.HitConfirmed += OnMeleeHitConfirmed;
			}

			if (_hurtbox != null) {
				_hurtbox.OnHit += OnHurtboxHit;
				_hurtboxShape = _hurtbox.GetNodeOrNull<CollisionShape2D>("CollisionShape2D")
					?? (_hurtbox.GetChildCount() > 0 ? _hurtbox.GetChild(0) as CollisionShape2D : null);
				if (_hurtboxShape?.Shape is RectangleShape2D hurtboxRect) {
					_normalHurtboxSize = hurtboxRect.Size;
					_normalHurtboxPosition = _hurtboxShape.Position;
				}
			}
		}

		private float OnHurtboxHit(FTT.Combat.HitPayload hit) {
			if (CurrentState == CharacterState.Dead || CurrentState == CharacterState.Respawning) return 0f;
			if (_rollInvulnerable) return 0f;
			if (HasStoryProjectileImmunity && hit.HitboxID == "projectile") return 0f;
			bool wasLedgeHanging = CurrentState == CharacterState.LedgeHanging;

			if (CurrentState == CharacterState.Blocking && _blockSystem != null) {
				FTT.Combat.BlockResult blockResult = _blockSystem.ResolveHit(hit);
				if (blockResult != FTT.Combat.BlockResult.NotBlocked) {
					// Shield of Orleans (Story-only, Joan): damage absorbed while
					// blocking still builds the Ultimate Meter, 25% faster than the
					// standard damage-taken rate.
					if (HasStoryPerk(ShieldOfOrleansPerkKey)) {
						_ultimateMeter?.AddFlat(Mathf.Max(0f, hit.Damage)
							* FTT.Combat.UltimateMeter.PointsPerDamageTaken
							* ShieldOfOrleansMeterMultiplier);
						CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? CurrentUltimateMeter;
					}
					FTT.Core.CameraShake.Instance?.Shake(3f, 0.08f);
					if (blockResult == FTT.Combat.BlockResult.Blocked) {
						// Guard Impact (design haptic table): a successful absorb.
						// A guard break buzzes its own heavier pattern through the
						// OnBlockBroken event instead.
						FTT.Core.HapticFeedbackManager.Instance?.OnGuardImpact(PlayerIndex);
					}
					return 0f;
				}
			}

			int damageApplied = ApplyDamage(Math.Max(0, (int)MathF.Round(hit.Damage)));
			if (damageApplied <= 0) return 0f;

			if (wasLedgeHanging && CurrentState != CharacterState.Dead) {
				ReleaseActiveLedge();
				TransitionTo(CharacterState.Airborne);
			}

			bool hasHyperArmor = HasActiveHyperArmorAgainst(hit.AttackClass);
			if (!hasHyperArmor) {
				// An impulse-free hit (construct arcs/bites carry zero
				// knockback) must not replace the velocity — a zero vector
				// would freeze the victim mid-motion.
				if (hit.Knockback != Vector2.Zero) {
					// Low-health knockback scaling (gameplay-feel plan §2.5): the
					// impulse scales with the victim's missing HP *after* this
					// hit's damage, exactly as the Fighter sim scales it.
					Vector2 knockback = FTT.Combat.DamageCalculator.CalculateKnockback(
						hit.Knockback,
						Data?.Weight ?? 1f,
						hit.AttackerFacingRight,
						CurrentHP,
						MaximumHP);
					// Knockback replaces velocity, as the Fighter sim resolves it —
					// a hit imparts the same impulse regardless of prior motion.
					Velocity = knockback * 60f;
				}

				if (hit.HitstunDuration > 0f && CurrentState != CharacterState.Dead) {
					ApplyStun(hit.HitstunDuration);
				}
			}

			if (hit.AppliedStatus != FTT.Core.StatusType.None && CurrentState != CharacterState.Dead) {
				_statusController?.ApplyStatus(hit.AppliedStatus, hit.StatusDuration, hit.StatusIntensity);
			}

			_ultimateMeter?.AddFromDamageTaken(damageApplied);
			CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? CurrentUltimateMeter;
			float shakeIntensity = hit.ScreenShakeIntensity > 0f
				? hit.ScreenShakeIntensity * 12f
				: damageApplied * 0.25f;
			FTT.Core.CameraShake.Instance?.Shake(shakeIntensity, hit.ScreenShakeDuration);
			_glow?.FlashHit();
			SpawnDamageNumber(damageApplied, hit.HitOrigin);
			return damageApplied;
		}

		private void SpawnDamageNumber(int damage, Vector2 position) {
			FTT.UI.FloatingDamageNumber.Show(damage, position + new Vector2(-10, -30), GetParent());
		}

		/// <summary>
		/// Zealous Vigor (Story-only, Joan): landing the final cleave of the basic
		/// three-hit string heals 5% of missing HP.
		/// </summary>
		private void OnMeleeHitConfirmed(FTT.Combat.HitPayload payload, float damageApplied) {
			if (damageApplied > 0f) {
				// Package 8 A3: the attacker-side feedback hook. Haptics and impact
				// VFX consume this; it carries no gameplay authority.
				FTT.Core.EventBus.Instance?.RaiseHitConfirm(new FTT.Core.HitConfirmPayload {
					PlayerIndex = PlayerIndex,
					AttackID = payload.AttackID ?? "",
					DamageApplied = damageApplied,
					IsHeavy = payload.AttackClass != FTT.Combat.AttackClass.Basic,
					Position = payload.HitOrigin
				});
			}
			if (damageApplied <= 0f || !HasStoryPerk(ZealousVigorPerkKey)) return;
			if (payload.HitboxID != "combo_3") return;
			int missingHP = MaximumHP - CurrentHP;
			int heal = (int)MathF.Round(missingHP * ZealousVigorMissingHPFraction);
			if (heal > 0) HealStory(heal);
		}

		public override void _PhysicsProcess(double delta) {
			float dt = (float)delta;
			UpdateStoryTemporaryEffects();
			CurrentInputFrame = FTT.Core.InputManager.Instance?.GetFrame(PlayerIndex) ?? default;

			UpdateCooldowns(dt);
			UpdateDropThrough(dt);
			UpdateHyperArmorPresentation();
			if (_downTapFramesRemaining > 0) _downTapFramesRemaining--;

			switch (CurrentState) {
				case CharacterState.Idle:
					ProcessIdle(dt);
					break;
				case CharacterState.Running:
					ProcessRunning(dt);
					break;
				case CharacterState.Rolling:
					ProcessRolling(dt);
					break;
				case CharacterState.Skidding:
					ProcessSkidding(dt);
					break;
				case CharacterState.Crouching:
					ProcessCrouching(dt);
					break;
				case CharacterState.Airborne:
					ProcessAirborne(dt);
					break;
				case CharacterState.Attacking:
					ProcessAttacking(dt);
					break;
				case CharacterState.UsingSpecial:
					ProcessUsingSpecial(dt);
					break;
				case CharacterState.UsingUltimate:
					ProcessUsingUltimate(dt);
					break;
				case CharacterState.Blocking:
					ProcessBlocking(dt);
					break;
				case CharacterState.Stunned:
					ProcessStunned(dt);
					break;
				case CharacterState.Dazed:
					ProcessDazed(dt);
					break;
				case CharacterState.LedgeHanging:
					ProcessLedgeHanging(dt);
					break;
				case CharacterState.Dead:
					break;
				case CharacterState.Respawning:
					ProcessRespawning(dt);
					break;
				case CharacterState.UsingMovementAbility:
					ProcessUsingMovementAbility(dt);
					break;
			}

			if (CurrentState != CharacterState.LedgeHanging &&
				CurrentState != CharacterState.Dead) {
				MoveAndSlide();
			}

			if (!_rewindSuspended && _pushbox != null) {
				if (CurrentState == CharacterState.Rolling && IsRollTravelFrame) {
					if (_pushbox.ResolveBlockingRollOverlaps(_rollDirection)) {
						EnterRollRecovery();
					}
				} else {
					_pushbox.ResolveStoryOverlaps(
						CurrentState == CharacterState.Rolling ? _rollDirection : 0);
				}
			}

			UpdateFootsteps(dt);
			UpdateLandingFeedback();

			_wasGrounded = IsOnFloor();
		}

		/// <summary>
		/// Fall distance, in pixels, above which a landing plays the Heavy Landing
		/// haptic — the design's "fall &gt; 3 units" at the 62.5 px/world-unit scale
		/// the Fighter presentation mapping uses.
		/// </summary>
		private const float HeavyLandingFallPixels =
			FTT.Core.HapticFeedbackManager.HeavyLandingFallUnits * 62.5f;

		private bool _fallTracking;
		private float _fallPeakY;

		/// <summary>
		/// Heavy Landing haptic (design haptic table; audit M-31). Tracks the
		/// airborne apex and buzzes on the grounded edge when the drop was tall
		/// enough. Purely additive presentation, runs after <c>MoveAndSlide</c> so
		/// <c>IsOnFloor</c> describes the frame that actually happened.
		/// </summary>
		private void UpdateLandingFeedback() {
			if (!IsOnFloor()) {
				// Y grows downward, so the apex is the smallest Y seen airborne.
				if (!_fallTracking) {
					_fallTracking = true;
					_fallPeakY = GlobalPosition.Y;
				} else if (GlobalPosition.Y < _fallPeakY) {
					_fallPeakY = GlobalPosition.Y;
				}
				return;
			}
			if (!_fallTracking) return;
			_fallTracking = false;
			if (_rewindSuspended || CurrentState == CharacterState.Dead) return;
			if (GlobalPosition.Y - _fallPeakY >= HeavyLandingFallPixels) {
				FTT.Core.HapticFeedbackManager.Instance?.OnHeavyLanding(PlayerIndex);
			}
		}

		/// <summary>
		/// Package 8 B5. Emits a footstep every stride of grounded travel. Runs after
		/// <c>MoveAndSlide</c> so <c>IsOnFloor</c> and <c>Velocity</c> describe the
		/// frame that actually happened; the cadence itself lives in
		/// <see cref="FTT.Core.FootstepCadence"/> so the rhythm is testable without a
		/// physics frame. Purely additive presentation — no gameplay state is read or
		/// written here.
		///
		/// <para>Rolling is deliberately silent: the character is off their feet, and
		/// the roll has its own feedback. Fighter Mode gets no footsteps from this
		/// path at all, because the driver disables native processing on its
		/// presentation bodies.</para>
		/// </summary>
		private void UpdateFootsteps(float dt) {
			bool eligible = IsOnFloor() && !_rewindSuspended && CurrentState switch {
				CharacterState.Rolling => false,
				CharacterState.Dead => false,
				CharacterState.Respawning => false,
				CharacterState.Stunned => false,
				CharacterState.Dazed => false,
				CharacterState.LedgeHanging => false,
				_ => true
			};
			if (_footsteps.Advance(eligible, Velocity.X, dt)) {
				FTT.Core.AudioManager.Instance?.PlayFootstep(FootstepSurfaceID);
			}
		}

		// === State Processors ===

		private void ProcessIdle(float dt) {
			ApplyGravity(dt);
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float step = maxSpeed / GroundDecelRampFrames * dt * 60f;
			var idleVel = Velocity;
			idleVel.X = Mathf.MoveToward(idleVel.X, 0, step);
			Velocity = idleVel;

			if (!IsOnFloor()) {
				_coyoteTimer -= dt;
				if (_coyoteTimer <= 0) {
					TransitionTo(CharacterState.Airborne);
					return;
				}
			} else {
				_coyoteTimer = CoyoteTime;
				RemainingJumps = Data?.MaxJumpCount ?? 1;
			}

			if (CheckRollInput()) return;
			if (CheckJumpInput()) return;
			if (CheckAttackInput()) return;
			if (CheckSpecialInput()) return;
			if (CheckUltimateInput()) return;
			if (CheckBlockInput()) return;
			if (CheckMovementAbilityInput()) return;
			if (CheckInteractInput()) return;

			float hAxis = GetHorizontalInput();
			if (Mathf.Abs(hAxis) > 0.1f) {
				TransitionTo(CharacterState.Running);
				return;
			}

			if (CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Down)) {
				TransitionTo(CharacterState.Crouching);
				return;
			}

			CheckDropThrough();
			PlayAnimation("idle");
		}

		private void ProcessRunning(float dt) {
			ApplyGravity(dt);

			if (!IsOnFloor()) {
				_coyoteTimer -= dt;
				if (_coyoteTimer <= 0) {
					TransitionTo(CharacterState.Airborne);
					return;
				}
			} else {
				_coyoteTimer = CoyoteTime;
				RemainingJumps = Data?.MaxJumpCount ?? 1;
			}

			if (CheckRollInput()) return;
			if (CheckJumpInput()) return;
			if (CheckAttackInput()) return;
			if (CheckBlockInput()) return;
			if (CheckMovementAbilityInput()) return;

			float hAxis = GetHorizontalInput();
			if (Mathf.Abs(hAxis) < 0.1f) {
				TransitionTo(CharacterState.Idle);
				return;
			}

			if (CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Down)) {
				TransitionTo(CharacterState.Crouching);
				return;
			}

			bool wantsRight = hAxis > 0;
			if (wantsRight != IsFacingRight && IsOnFloor()) {
				TransitionTo(CharacterState.Skidding);
				return;
			}

			ApplyHorizontalMovement(hAxis, dt);
			UpdateFacing(hAxis);
			CheckDropThrough();
			PlayAnimation("run");
		}

		private void ProcessRolling(float dt) {
			ApplyGravity(dt);
			_rollInvulnerable = false;

			if (_rollFrame < FTT.Core.UniversalMovementRules.RollStartupFrames) {
				_pushbox?.SetPushEnabled(true);
				DecelerateHorizontal(dt);
				PlayAnimation("roll_startup");
			} else if (IsRollTravelFrame) {
				int travelFrame = _rollFrame - FTT.Core.UniversalMovementRules.RollStartupFrames;
				_rollInvulnerable = travelFrame < FTT.Core.UniversalMovementRules.RollInvulnerabilityFrames;
				_pushbox?.SetPushEnabled(false);
				float rollSpeed = EffectiveMoveSpeed
					* StatusMovementMultiplier
					* 60f
					* FTT.Core.UniversalMovementRules.RollSpeedMultiplier;
				Velocity = new Vector2(_rollDirection * rollSpeed, Velocity.Y);
				PlayAnimation("roll");
			} else {
				_pushbox?.SetPushEnabled(true);
				DecelerateHorizontal(dt);
				PlayAnimation("roll_recovery");
			}

			_rollFrame++;
			if (_rollFrame >= FTT.Core.UniversalMovementRules.RollTotalFrames) {
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		private bool IsRollTravelFrame =>
			_rollFrame >= FTT.Core.UniversalMovementRules.RollStartupFrames
			&& _rollFrame < FTT.Core.UniversalMovementRules.RollStartupFrames
				+ FTT.Core.UniversalMovementRules.RollTravelFrames;

		private void EnterRollRecovery() {
			_rollFrame = FTT.Core.UniversalMovementRules.RollStartupFrames
				+ FTT.Core.UniversalMovementRules.RollTravelFrames;
			_rollInvulnerable = false;
			_pushbox?.SetPushEnabled(true);
			Velocity = new Vector2(0f, Velocity.Y);
		}

		private void DecelerateHorizontal(float dt) {
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float step = maxSpeed / GroundDecelRampFrames * dt * 60f;
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, step), Velocity.Y);
		}

		private void ProcessSkidding(float dt) {
			ApplyGravity(dt);
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float step = maxSpeed / GroundDecelRampFrames * dt * 60f;
			var skidVel = Velocity;
			skidVel.X = Mathf.MoveToward(skidVel.X, 0, step);
			Velocity = skidVel;
			_skidTimer -= dt;

			if (_skidTimer <= 0) {
				IsFacingRight = !IsFacingRight;
				UpdateSpriteFlip();
				TransitionTo(CharacterState.Running);
			}

			PlayAnimation("skid");
		}

		private void ProcessCrouching(float dt) {
			ApplyGravity(dt);
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float step = maxSpeed / GroundDecelRampFrames * dt * 60f;
			var crouchVel = Velocity;
			crouchVel.X = Mathf.MoveToward(crouchVel.X, 0, step);
			Velocity = crouchVel;

			if (!CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Down)) {
				RestoreHurtboxHeight();
				TransitionTo(CharacterState.Idle);
				return;
			}

			if (CheckAttackInput()) return;
			CheckDropThrough();
			PlayAnimation("crouch");
		}

		private void ProcessAirborne(float dt) {
			ApplyGravity(dt);

			float hAxis = GetHorizontalInput();
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float targetSpeed = hAxis * maxSpeed;
			bool isAccelerating = Mathf.Abs(targetSpeed) >= Mathf.Abs(Velocity.X)
				|| (targetSpeed > 0 && Velocity.X < 0) || (targetSpeed < 0 && Velocity.X > 0);
			// The 4-frame air ramp is the designed constant the Fighter sim uses;
			// the 8-frame ground ramp was accidental here (audit M-17).
			float rampFrames = isAccelerating ? AirAccelRampFrames : AirDecelRampFrames;
			float step = maxSpeed / rampFrames * dt * 60f;
			var vel = Velocity;
			vel.X = Mathf.MoveToward(vel.X, targetSpeed, step);

			// Fast-fall (§2.9, 2026-08-10): stateless — held Down while airborne
			// pins the descent to at least FastFallSpeedPixels and cancels the
			// Warp float window. Applied after ApplyGravity so it deliberately
			// overrides that method's 600 px/s terminal clamp for this case.
			// ProcessStunned/ProcessDazed own hitstun and never route here.
			if (CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Down)) {
				StoryFloatTimer = 0f;
				vel.Y = Mathf.Max(vel.Y, FastFallSpeedPixels);
			}
			Velocity = vel;

			UpdateFacing(hAxis);

			if (IsOnFloor()) {
				if (_jumpBufferTimer > 0) {
					PerformJump();
				} else {
					AerialComboCounter = 0;
					ComboCounter = 0;
					TransitionTo(CharacterState.Idle);
				}
				return;
			}

			if (CheckJumpInput()) return;
			if (CheckAttackInput()) return;
			if (CheckSpecialInput()) return;
			if (CheckUltimateInput()) return;
			if (CheckMovementAbilityInput()) return;

			PlayAnimation(Velocity.Y < 0 ? "jump" : "fall");
		}

		private void ProcessAttacking(float dt) {
			ApplyGravity(dt);
			if (_attackStartedAerial && IsOnFloor()) {
				CancelActiveAttack();
				AerialComboCounter = 0;
				ComboCounter = 0;
				TransitionTo(CharacterState.Idle);
				return;
			}
			if (IsOnFloor()) {
				// Swings never stop the attacker: full horizontal steering at
				// the normal run ramp through startup/active/recovery/hold
				// (mirrors the Fighter sim's unlocked swing movement).
				ApplyHorizontalMovement(GetHorizontalInput(), dt);
			} else {
				ApplyAirControl(dt);
			}
			// Gameplay-feel plan §2.12 — facing follows movement during a swing,
			// superseding the 2026-08-09 "committed for the whole string" rule.
			// Hitbox placement still reads facing when the active window opens
			// (OnAttackActiveStarted), so nothing migrates mid-active.
			UpdateFacing(GetHorizontalInput());

			// Design 1051 — a special (or the ultimate) cancels a basic at any
			// point in the swing and resets the chain; shared rule with the
			// Fighter sim.
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Special1)
				|| CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Special2)
				|| CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Ultimate)) {
				if (CheckSpecialInput() || CheckUltimateInput()) {
					CancelActiveAttack();
					ResetComboChain();
					return;
				}
			}

			if (_inRecoveryHold) {
				ProcessRecoveryHold(dt);
				return;
			}

			CheckDropThrough();

			// Directional attacks are single strikes: they never buffer into the
			// three-hit string (§2.8).
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.BasicAttack)
				&& !_attackHitActive
				&& _attackVariant == FTT.Combat.BasicComboRules.VariantChain) {
				_nextAttackBuffered = true;
			}

			// Design 752/3080 — during recovery frames, jumping, rolling, or
			// blocking cancels the swing and resets the chain. Held movement
			// steers the attacker but never cancels: the authored string pace
			// is the only pace.
			if (_attackInRecovery && TryRecoveryCancel()) return;

			if (_attackAnimationDriven) return;

			FTT.Combat.CombatFrameTimeline timeline = GetActiveComboTimeline();
			int elapsedFrame = timeline.TotalFrames - _attackFramesRemaining;
			bool shouldBeActive = timeline.IsActive(elapsedFrame);

			if (shouldBeActive && !_attackHitActive) {
				OnAttackActiveStarted();
			}

			if (_attackHitActive && !shouldBeActive) {
				OnAttackActiveEnded();
			}

			// Story-only ComboSpeed minors advance the authored attack clock
			// faster than real time (a 1f multiplier steps exactly one frame).
			_attackFrameProgress += StoryComboSpeedMultiplier;
			while (_attackFrameProgress >= 1f && _attackFramesRemaining > 0) {
				_attackFrameProgress -= 1f;
				_attackFramesRemaining--;
			}
			if (_attackFramesRemaining <= 0) {
				CompleteCurrentComboHit();
			}
		}

		/// <summary>
		/// The post-recovery chain window. Reached only from
		/// <see cref="ProcessAttacking"/>, which has already applied this tick's
		/// movement and §2.12 facing update before dispatching here.
		/// </summary>
		private void ProcessRecoveryHold(float dt) {
			_comboBufferFramesRemaining--;

			if (_attackStartedAerial && IsOnFloor()) {
				CancelActiveAttack();
				AerialComboCounter = 0;
				ComboCounter = 0;
				TransitionTo(CharacterState.Idle);
				return;
			}

			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.BasicAttack)) {
				_inRecoveryHold = false;
				AdvanceActiveCombo();
				StartComboHit();
				return;
			}

			// Same cancel set as the recovery frames: jump, roll, or block
			// ends the chain window (specials are handled upstream in
			// ProcessAttacking before the hold dispatch). Held movement walks
			// the hold window without ending it.
			if (TryRecoveryCancel()) {
				_inRecoveryHold = false;
				return;
			}

			if (_comboBufferFramesRemaining <= 0) {
				_inRecoveryHold = false;
				ResetActiveCombo();
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		/// <summary>
		/// Design 752/3080: during a basic's recovery frames or the chain-hold
		/// window, jumping, rolling, or blocking cancels the swing and
		/// resets the chain. Held horizontal movement deliberately does not —
		/// it steers the attacker while the string keeps its authored pace
		/// (movement-cancel let a moving attacker restart hit one early). The
		/// Fighter sim applies the identical rule in its phase machine.
		/// </summary>
		private bool TryRecoveryCancel() {
			if (CheckJumpInput() || CheckRollInput() || CheckBlockInput()) {
				CancelActiveAttack();
				ResetComboChain();
				return true;
			}
			return false;
		}

		/// <summary>Resets both surface counters — the whole chain, not one string.</summary>
		private void ResetComboChain() {
			GroundComboCounter = 0;
			AerialComboCounter = 0;
			ComboCounter = 0;
			_attackVariant = FTT.Combat.BasicComboRules.VariantChain;
		}

		private void ProcessUsingSpecial(float dt) {
			var ability = _pendingSpecialSlot == 2 ? _special2 : _special1;
			float gravityMultiplier = _specialStartedAerial && ability?.CurrentPhase is
				FTT.Combat.AbilityPhase.Startup or FTT.Combat.AbilityPhase.Active
				? 0.5f
				: 1f;
			ApplyGravity(dt * gravityMultiplier);
			if (ability == null || !ability.IsExecuting) {
				_specialStartedAerial = false;
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		private void ProcessUsingUltimate(float dt) {
			if (_ultimateStartedAerial && _ultimate?.IsExecuting == true) {
				Velocity = new Vector2(Velocity.X, 0f);
			}
			if (_ultimate == null || !_ultimate.IsExecuting) {
				_ultimateStartedAerial = false;
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		private void ProcessBlocking(float dt) {
			ApplyGravity(dt);
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float step = maxSpeed / GroundDecelRampFrames * dt * 60f;
			var blockVel = Velocity;
			blockVel.X = Mathf.MoveToward(blockVel.X, 0, step);
			Velocity = blockVel;

			_blockSystem?.StartBlock();
			if (CheckRollInput()) {
				_blockSystem?.EndBlock();
				return;
			}

			CheckDropThrough();
			if (CurrentState != CharacterState.Blocking) {
				_blockSystem?.EndBlock();
				return;
			}

			if (!CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Block)) {
				_blockSystem?.EndBlock();
				TransitionTo(CharacterState.Idle);
				return;
			}

			PlayAnimation("block");
		}

		private void ProcessStunned(float dt) {
			ApplyGravity(dt);
			// Gameplay-feel plan §2.4 — Block cancels hitstun. A grounded victim
			// holding Block leaves hitstun straight into the block stance; an
			// airborne one cannot (the stance is grounded-only), and Dazed is a
			// separate state so the guard-break punish window is untouched. The
			// Fighter sim clears HitstunFrames on the same condition.
			if (IsOnFloor() && CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Block)) {
				_stunTimer = 0f;
				TransitionTo(CharacterState.Blocking);
				return;
			}
			_stunTimer -= dt;
			if (_stunTimer <= 0) {
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		private void ProcessDazed(float dt) {
			ApplyGravity(dt);
			_dazeTimer -= dt;
			if (_dazeTimer <= 0) {
				TransitionTo(CharacterState.Idle);
			}
		}

		private void ProcessLedgeHanging(float dt) {
			_ledgeHangTimer -= dt;

			if (_ledgeHangTimer <= 0) {
				DropFromLedge();
				return;
			}

			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Jump)) {
				ReleaseActiveLedge();
				TransitionTo(CharacterState.Airborne);
				var vel = Velocity;
				vel.Y = -EffectiveJumpForce * StatusJumpMultiplier * 45f;
				Velocity = vel;
				return;
			}

			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Down)) {
				DropFromLedge();
				return;
			}

			float hAxis = GetHorizontalInput();
			bool towardStage = (IsFacingRight && hAxis > 0.1f) || (!IsFacingRight && hAxis < -0.1f);
			if (towardStage) {
				PlayAnimation("ledge_pull_up");
				if (_activeLedge != null) GlobalPosition = _activeLedge.StandPosition;
				ReleaseActiveLedge();
				TransitionTo(CharacterState.Idle);
				return;
			}

			PlayAnimation("ledge_hang");
		}

		private void DropFromLedge() {
			ReleaseActiveLedge();
			Velocity = new Vector2(Velocity.X, Mathf.Max(90f, Velocity.Y));
			TransitionTo(CharacterState.Airborne);
		}

		private void ReleaseActiveLedge() {
			_activeLedge?.Release(this);
			_activeLedge = null;
		}

		private void ProcessRespawning(float dt) {
			_respawnTimer -= dt;
			if (_respawnTimer <= 0) {
				TransitionTo(CharacterState.Idle);
			}
		}

		private void ProcessUsingMovementAbility(float dt) {
			if (_movementAbility == null || !_movementAbility.IsExecuting) {
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		// === State Transitions ===

		public void TransitionTo(CharacterState newState) {
			if (CurrentState == CharacterState.Dead && newState != CharacterState.Respawning) return;

			var oldState = CurrentState;
			if (oldState == CharacterState.Rolling && newState != CharacterState.Rolling) CleanupRoll();
			if (oldState == CharacterState.LedgeHanging && newState != CharacterState.LedgeHanging) ReleaseActiveLedge();
			if (oldState == CharacterState.Crouching && newState != CharacterState.Crouching &&
				!(newState == CharacterState.Attacking && _attackStartedCrouched)) {
				RestoreHurtboxHeight();
			}
			if (oldState == CharacterState.Attacking && newState != CharacterState.Attacking && _attackStartedCrouched) {
				RestoreHurtboxHeight();
				_attackStartedCrouched = false;
			}
			CurrentState = newState;

			switch (newState) {
				case CharacterState.Rolling:
					_rollFrame = 0;
					_rollInvulnerable = false;
					_pushbox?.SetPushEnabled(true);
					break;
				case CharacterState.Skidding:
					_skidTimer = SkidDuration;
					break;
				case CharacterState.Crouching:
					SetCrouchHurtbox();
					break;
				case CharacterState.LedgeHanging:
					_ledgeHangTimer = LedgeHangMaxTime;
					Velocity = Vector2.Zero;
					break;
				case CharacterState.Dazed:
					_dazeTimer = DazeDuration;
					break;
				case CharacterState.Respawning:
					_respawnTimer = 2.0f;
					break;
				case CharacterState.Airborne:
					GroundComboCounter = 0;
					if (oldState != CharacterState.Attacking) ComboCounter = AerialComboCounter;
					break;
				case CharacterState.Idle:
					if (oldState == CharacterState.Airborne || _attackStartedAerial) AerialComboCounter = 0;
					if (oldState == CharacterState.Attacking) GroundComboCounter = 0;
					ComboCounter = GroundComboCounter;
					break;
			}
		}

		public void ApplyStun(float duration) {
			if (CurrentState == CharacterState.Dead || CurrentState == CharacterState.Respawning) return;
			// Being hit cancels the swing and resets the chain (design 752) —
			// and cleans up an active hitbox the state switch alone would leave
			// live. The Fighter sim applies the identical rule on hitstun.
			if (CurrentState == CharacterState.Attacking) CancelActiveAttack();
			ResetComboChain();
			// H-4: a landed stun also cancels an executing special/ultimate
			// outright — no further ticks, steering, or phase advancement.
			// Hyper-armor gating lives at the hit-resolution site
			// (OnHurtboxHit): while an armor window covers the incoming attack
			// class, this method is never reached and the cast completes.
			InterruptActiveAbilities();
			_stunTimer = duration;
			TransitionTo(CharacterState.Stunned);
		}

		/// <summary>
		/// H-4: interrupts whichever ability slot is mid-cast. Safe to call
		/// unconditionally — <see cref="FTT.Combat.BaseSpecial.Interrupt"/>
		/// no-ops on an inactive ability.
		/// </summary>
		private void InterruptActiveAbilities() {
			_special1?.Interrupt();
			_special2?.Interrupt();
			_movementAbility?.Interrupt();
			_ultimate?.Interrupt();
		}

		public int ApplyDamage(int damage) => ApplyDamage(damage, ignoreRollInvulnerability: false);

		public int ApplyPersistentDamage(int damage) => ApplyDamage(damage, ignoreRollInvulnerability: true);

		private int ApplyDamage(int damage, bool ignoreRollInvulnerability) {
			if (CurrentState == CharacterState.Dead || CurrentState == CharacterState.Respawning) return 0;
			if (_postRewindInvulnerabilityFrames > 0) return 0;
			if (_rollInvulnerable && !ignoreRollInvulnerability) return 0;
			damage = Math.Max(0, (int)MathF.Round(damage * StatusDamageTakenMultiplier));
			if (StoryShieldPoints > 0f && damage > 0) {
				int absorbed = Math.Min(damage, (int)MathF.Floor(StoryShieldPoints));
				if (absorbed > 0) {
					StoryShieldPoints -= absorbed;
					damage -= absorbed;
				}
			}
			int previousHP = CurrentHP;
			CurrentHP = Math.Max(0, CurrentHP - damage);
			int damageApplied = previousHP - CurrentHP;
			FTT.Core.EventBus.Instance?.RaisePlayerHPChanged(new FTT.Core.PlayerHPPayload {
				PlayerIndex = PlayerIndex,
				CurrentHP = CurrentHP,
				MaxHP = MaximumHP,
				DamageAmount = damageApplied
			});

			if (CurrentHP <= 0) {
				// H-4: death cancels the basic swing and any executing
				// special/ultimate unconditionally — hyper-armor prevents
				// hitstun, never death — so no hitbox, steering, or
				// multi-hit sequence survives into the Dead state.
				if (CurrentState == CharacterState.Attacking) CancelActiveAttack();
				InterruptActiveAbilities();
				TransitionTo(CharacterState.Dead);
				FTT.Core.EventBus.Instance?.RaisePlayerDied(PlayerIndex);
			}
			return damageApplied;
		}

		public void AddInfluenceFromDamageDealt(float damageApplied) {
			_ultimateMeter?.AddFromDamageDealt(damageApplied);
			CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? CurrentUltimateMeter;
		}

		public void DrainUltimateMeter(float points) {
			if (_ultimateMeter != null) {
				_ultimateMeter.SetValue(_ultimateMeter.CurrentValue - Mathf.Max(0f, points));
				CurrentUltimateMeter = _ultimateMeter.CurrentValue;
			} else {
				CurrentUltimateMeter = Mathf.Max(0f, CurrentUltimateMeter - Mathf.Max(0f, points));
			}
		}

		public int HealStory(int amount) {
			if (CurrentState == CharacterState.Dead || amount <= 0) return 0;
			int previousHP = CurrentHP;
			CurrentHP = Math.Min(MaximumHP, CurrentHP + amount);
			int healed = CurrentHP - previousHP;
			if (healed > 0) {
				FTT.Core.EventBus.Instance?.RaisePlayerHPChanged(new FTT.Core.PlayerHPPayload {
					PlayerIndex = PlayerIndex,
					CurrentHP = CurrentHP,
					MaxHP = MaximumHP,
					DamageAmount = -healed
				});
			}
			return healed;
		}

		public void ApplyStoryDamageBuff(float multiplier, float durationSeconds) {
			StoryTemporaryDamageMultiplier = Math.Max(1f, multiplier);
			_temporaryDamageBuffFrames = Math.Max(0, Mathf.RoundToInt(durationSeconds * 60f));
			if (_temporaryDamageBuffFrames == 0) StoryTemporaryDamageMultiplier = 1f;
		}

		public void ApplyStorySpeedBuff(float multiplier, float durationSeconds) {
			StoryTemporarySpeedMultiplier = Math.Max(1f, multiplier);
			_temporarySpeedBuffFrames = Math.Max(0, Mathf.RoundToInt(durationSeconds * 60f));
			if (_temporarySpeedBuffFrames == 0) StoryTemporarySpeedMultiplier = 1f;
		}

		private void UpdateStoryTemporaryEffects() {
			if (_temporaryDamageBuffFrames > 0 && --_temporaryDamageBuffFrames == 0) {
				StoryTemporaryDamageMultiplier = 1f;
			}
			if (_temporarySpeedBuffFrames > 0 && --_temporarySpeedBuffFrames == 0) {
				StoryTemporarySpeedMultiplier = 1f;
			}
			if (_storyProjectileImmunityFrames > 0) _storyProjectileImmunityFrames--;
			if (_postRewindInvulnerabilityFrames > 0) _postRewindInvulnerabilityFrames--;
			if (_storyHyperArmorFrames > 0) _storyHyperArmorFrames--;
		}

		public void ApplyStockLossMeterRetention() {
			_ultimateMeter?.ApplyStockLossRetention();
			CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? CurrentUltimateMeter * 0.75f;
		}

		public bool HasActiveHyperArmor =>
			StoryHyperArmorActive ||
			AbilityHasActiveHyperArmor(_special1) ||
			AbilityHasActiveHyperArmor(_special2) ||
			AbilityHasActiveHyperArmor(_movementAbility) ||
			AbilityHasActiveHyperArmor(_ultimate);

		public bool HasActiveHyperArmorAgainst(FTT.Combat.AttackClass attackClass) =>
			(StoryHyperArmorActive && attackClass != FTT.Combat.AttackClass.Ultimate) ||
			AbilityHasActiveHyperArmor(_special1, attackClass) ||
			AbilityHasActiveHyperArmor(_special2, attackClass) ||
			AbilityHasActiveHyperArmor(_movementAbility, attackClass) ||
			AbilityHasActiveHyperArmor(_ultimate, attackClass);

		private static bool AbilityHasActiveHyperArmor(FTT.Combat.BaseSpecial ability) =>
			AbilityHasActiveHyperArmor(ability, FTT.Combat.AttackClass.Basic);

		private static bool AbilityHasActiveHyperArmor(
			FTT.Combat.BaseSpecial ability,
			FTT.Combat.AttackClass incomingAttackClass) =>
			ability != null && FTT.Combat.StoryCombatRules.HyperArmorPreventsInterruption(
				ability.Data?.GrantsHyperArmor == true,
				ability.CurrentPhase,
				incomingAttackClass);

		public string ActiveAnimationName => _animatedSprite?.Animation ?? "idle";

		public void PlayPresentationAnimation(string animationName) {
			if (_animatedSprite == null || string.IsNullOrWhiteSpace(animationName)) return;
			// Same-name guard so per-frame presentation drivers do not restart
			// the animation at frame zero every tick.
			if (_animatedSprite.Animation == animationName) return;
			if (_animatedSprite.SpriteFrames?.HasAnimation(animationName) == true) _animatedSprite.Play(animationName);
		}

		/// <summary>
		/// Applies <see cref="IsFacingRight"/> to the sprite. Fighter-mode
		/// presentation entry point: the driver writes the field from sim state
		/// and native processing (which normally flips the sprite) is disabled.
		/// </summary>
		public void SyncPresentationFacing() => UpdateSpriteFlip();

		public void SetRewindSuspended(bool suspended) {
			if (_rewindSuspended == suspended) return;
			_rewindSuspended = suspended;
			if (suspended) {
				_rewindCollisionLayer = CollisionLayer;
				_rewindCollisionMask = CollisionMask;
				CollisionLayer = 0;
				CollisionMask = 0;
				if (_hurtbox != null) {
					// Safe setters: a lethal-hit rewind suspends the player from
					// inside the hit signal's physics flush, where the direct
					// writes are engine-blocked.
					_hurtbox.SetMonitoringSafe(false);
					_hurtbox.SetMonitorableSafe(false);
				}
				_pushbox?.SetPushEnabled(false);
				Velocity = Vector2.Zero;
				this.SetProcessModeSafe(ProcessModeEnum.Disabled);
			} else {
				CollisionLayer = _rewindCollisionLayer;
				CollisionMask = _rewindCollisionMask;
				if (_hurtbox != null) {
					_hurtbox.SetMonitoringSafe(true);
					_hurtbox.SetMonitorableSafe(true);
				}
				_pushbox?.SetPushEnabled(true);
				this.SetProcessModeSafe(ProcessModeEnum.Inherit);
			}
		}

		private void CleanupRoll() {
			_rollInvulnerable = false;
			_pushbox?.SetPushEnabled(true);
			_pushbox?.ResolveStoryOverlaps(_rollDirection);
		}

		public void CompleteStoryRewind(Vector2 landingPosition, int restoredHP) {
			GlobalPosition = landingPosition;
			Velocity = Vector2.Zero;
			CurrentHP = Math.Clamp(restoredHP, 1, MaximumHP);
			SetRewindSuspended(false);
			_postRewindInvulnerabilityFrames = StoryRewindInvulnerabilityFrames;
			TransitionTo(CharacterState.Respawning);
			FTT.Core.EventBus.Instance?.RaisePlayerHPChanged(new FTT.Core.PlayerHPPayload {
				PlayerIndex = PlayerIndex,
				CurrentHP = CurrentHP,
				MaxHP = MaximumHP,
				DamageAmount = 0
			});
		}

		public void RestoreStoryCheckpoint(Vector2 position, int hp, float ultimateMeter) {
			GlobalPosition = position;
			Velocity = Vector2.Zero;
			CurrentHP = Math.Clamp(hp, 1, MaximumHP);
			_ultimateMeter?.SetValue(ultimateMeter);
			CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? Mathf.Clamp(ultimateMeter, 0f, 100f);
			FTT.Core.EventBus.Instance?.RaisePlayerHPChanged(new FTT.Core.PlayerHPPayload {
				PlayerIndex = PlayerIndex,
				CurrentHP = CurrentHP,
				MaxHP = MaximumHP,
				DamageAmount = 0
			});
		}

		// === Movement Helpers ===

		private void ApplyGravity(float dt) {
			if (IsOnFloor()) {
				StoryFloatTimer = 0f;
				return;
			}

			float effectiveGravity = BaseGravity * (0.8f + 0.4f * (Data?.Weight ?? 1.0f)) * EnvironmentGravityScale;
			float multiplier = 1.0f;

			if (StoryFloatTimer > 0f) {
				StoryFloatTimer = Mathf.Max(0f, StoryFloatTimer - dt);
				multiplier = 0.15f;
				if (Velocity.Y > 0) {
					Velocity = new Vector2(Velocity.X, Mathf.Min(Velocity.Y, 60f));
				}
			} else if (Velocity.Y > 0) {
				multiplier = FallGravityMultiplier;
			} else if (!_jumpHeld && Velocity.Y < 0) {
				multiplier = ShortHopGravityMultiplier;
			}

			var vel = Velocity;
			vel.Y += effectiveGravity * multiplier * dt * 60f;
			vel.Y = Mathf.Min(vel.Y, TerminalVelocity);
			Velocity = vel;
		}

		private void ApplyHorizontalMovement(float hAxis, float dt) {
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float targetSpeed = hAxis * maxSpeed;
			// Grounded movement runs two ramps (§2.1): the 14-frame accel ramp
			// when building speed toward a same-signed target, the 12-frame
			// decel ramp when the target is zero or reverses against current
			// velocity. Mirrors the Fighter sim's ApplyNormalMovement selection.
			bool decelerating = Mathf.IsZeroApprox(targetSpeed)
				|| (targetSpeed > 0f && Velocity.X < 0f)
				|| (targetSpeed < 0f && Velocity.X > 0f);
			float rampFrames = decelerating ? GroundDecelRampFrames : GroundRampFrames;
			float step = maxSpeed / rampFrames * dt * 60f;
			var vel = Velocity;
			vel.X = Mathf.MoveToward(vel.X, targetSpeed, step);
			Velocity = vel;
		}

		private void ApplyFriction(float dt) {
			float friction = (Data?.GroundFriction ?? 20f) * dt * 60f;
			var vel = Velocity;
			vel.X = Mathf.MoveToward(vel.X, 0, friction);
			Velocity = vel;
		}

		private void ApplyAirControl(float dt) {
			float hAxis = GetHorizontalInput();
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float targetSpeed = hAxis * maxSpeed;
			bool isAccelerating = Mathf.Abs(targetSpeed) >= Mathf.Abs(Velocity.X)
				|| (targetSpeed > 0 && Velocity.X < 0) || (targetSpeed < 0 && Velocity.X > 0);
			// The 4-frame air ramp is the designed constant the Fighter sim uses;
			// the 8-frame ground ramp was accidental here (audit M-17).
			float rampFrames = isAccelerating ? AirAccelRampFrames : AirDecelRampFrames;
			float step = maxSpeed / rampFrames * dt * 60f;
			var vel = Velocity;
			vel.X = Mathf.MoveToward(vel.X, targetSpeed, step);
			Velocity = vel;
		}

			// === Input Helpers ===

		private float GetHorizontalInput() {
			return IsMovementRooted ? 0.0f : CurrentInputFrame.Horizontal;
		}

		private bool CheckRollInput() {
			if (IsMovementRooted || !IsOnFloor()) return false;
			if (!CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Roll)) return false;
			float input = GetHorizontalInput();
			_rollDirection = Mathf.Abs(input) > 0.1f ? Math.Sign(input) : (IsFacingRight ? 1 : -1);
			TransitionTo(CharacterState.Rolling);
			return true;
		}

		private bool CheckJumpInput() {
			if (IsMovementRooted) return false;
			_jumpHeld = CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Jump);

			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Jump)) {
				_jumpBufferTimer = JumpBufferTime;
			}
			_jumpBufferTimer -= (float)GetPhysicsProcessDeltaTime();

			if (_jumpBufferTimer > 0) {
				if (IsOnFloor() || _coyoteTimer > 0 || RemainingJumps > 0) {
					PerformJump();
					_jumpBufferTimer = 0;
					return true;
				}
			}
			return false;
		}

		private void PerformJump() {
			var vel = Velocity;
			vel.Y = -EffectiveJumpForce * StatusJumpMultiplier * 54f;
			Velocity = vel;
			RemainingJumps--;
			_coyoteTimer = 0;
			TransitionTo(CharacterState.Airborne);
		}

		private bool CheckAttackInput() {
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.BasicAttack)) {
				_attackStartedAerial = !IsOnFloor() || CurrentState == CharacterState.Airborne;
				_attackStartedCrouched = CurrentState == CharacterState.Crouching;
				// Gameplay feel §2.8 — one shared selection rule with the Fighter
				// simulation. Up wins; airborne Down is the down-air; grounded
				// Down (including the Crouching state) is the normal string.
				_attackVariant = FTT.Combat.BasicComboRules.SelectAttackVariant(
					upHeld: CurrentInputFrame.Vertical < FTT.Combat.BasicComboRules.StoryUpInputThreshold,
					downHeld: CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Down),
					airborne: _attackStartedAerial);
				if (_attackStartedAerial) {
					AerialComboCounter = 0;
					GroundComboCounter = 0;
				} else {
					GroundComboCounter = 0;
				}
				ComboCounter = 0;
				StartComboHit();
				return true;
			}
			return false;
		}

		private void StartComboHit() {
			int comboIdx = GetActiveComboIndex();
			ComboCounter = comboIdx;
			_attackFramesRemaining = GetActiveComboTimeline().TotalFrames;
			_attackHitActive = false;
			_attackInRecovery = false;
			_nextAttackBuffered = false;
			_inRecoveryHold = false;

			if (_attackVariant != FTT.Combat.BasicComboRules.VariantChain) {
				StartDirectionalAttack();
				return;
			}

			if (_meleeHitbox != null) {
				float baseDmg = (Data?.BasicAttackDamage ?? 10f) * StoryBasicDamageMultiplier;
				_meleeHitbox.Damage = baseDmg * ComboDamageMultipliers[comboIdx];
				_meleeHitbox.AttackID = $"{Data?.CharacterID ?? "fighter"}.basic";
				_meleeHitbox.HitboxID = $"combo_{comboIdx + 1}";
				// Kinetic Splitting (Story-only Resonance perk): Lincoln's third-hit
				// downward crush shatters shields instantly, which is exactly the
				// special-class block interaction.
				_meleeHitbox.AttackClass = comboIdx == 2
					&& HasStoryPerk(Abilities.LincolnSplittingStrike.KineticSplittingPerkKey)
					? FTT.Combat.AttackClass.Special
					: FTT.Combat.AttackClass.Basic;

				float baseKB = Data?.BasicAttackKnockback ?? 3f;
				// Hitstun and the horizontal knockback multiplier both come from
				// the shared rulebook, the same tables the Fighter sim applies.
				// The stun holds the victim through the chain gap to the next
				// hit; the 3x finisher launches them away.
				_meleeHitbox.HitstunDuration = FTT.Combat.BasicComboRules.HitstunFrames[comboIdx] / 60f;
				float kbMultiplier = FTT.Combat.BasicComboRules.KnockbackMultipliers[comboIdx];
				if (comboIdx == 2) {
					// Gameplay-feel plan §2.5 raised the shared finisher
					// multiplier from 3x to 4.5x; the launch's vertical
					// component is scaled by the same factor (-6 -> -9) so the
					// Story finisher keeps its launch angle and gains the same
					// separation the Fighter sim now produces.
					_meleeHitbox.KnockbackForce = new Vector2(baseKB * kbMultiplier, -9f);
				} else if (comboIdx == 1) {
					_meleeHitbox.KnockbackForce = new Vector2(baseKB * kbMultiplier, -1.5f);
				} else {
					_meleeHitbox.KnockbackForce = new Vector2(baseKB * kbMultiplier, -1f);
				}
			}

			if (_aerialHitboxMarker != null && _attackStartedAerial) {
				Vector2 offset = AerialComboHitboxOffsets[comboIdx];
				_aerialHitboxMarker.Position = new Vector2(IsFacingRight ? offset.X : -offset.X, offset.Y);
			}

			TransitionTo(CharacterState.Attacking);
			PlayAnimation($"basic_attack_{comboIdx + 1}");
			string animationName = $"basic_{(_attackStartedAerial ? "air" : "ground")}_{comboIdx + 1}";
			_attackAnimationDriven = _combatAnimationPlayer?.HasAnimation(animationName) == true;
			if (_attackAnimationDriven) {
				// Story-only ComboSpeed minors run the basic string faster; the
				// animation clock carries the hit-activation callbacks with it.
				_combatAnimationPlayer.SpeedScale = StoryComboSpeedMultiplier;
				_combatAnimationPlayer.Play(animationName);
			}
			_attackFrameProgress = 0f;
		}

		/// <summary>
		/// Arms the up-attack or down-air (§2.8): a single strike outside the
		/// three-hit chain. Damage, hitstun and both knockback components come
		/// from <see cref="FTT.Combat.BasicComboRules"/>, the same table the
		/// Fighter simulation reads. Both variants have dedicated retro sprite
		/// animations; gameplay timing remains on the frame clock.
		/// </summary>
		private void StartDirectionalAttack() {
			bool upAttack = _attackVariant == FTT.Combat.BasicComboRules.VariantUpAttack;
			if (_meleeHitbox != null) {
				float baseDmg = (Data?.BasicAttackDamage ?? 10f) * StoryBasicDamageMultiplier;
				_meleeHitbox.Damage = baseDmg * FTT.Combat.BasicComboRules.DirectionalAttackDamageMultiplier;
				_meleeHitbox.AttackID = $"{Data?.CharacterID ?? "fighter"}.basic";
				_meleeHitbox.HitboxID = upAttack
					? FTT.Combat.BasicComboRules.UpAttackHitboxID
					: FTT.Combat.BasicComboRules.DownAirHitboxID;
				_meleeHitbox.AttackClass = FTT.Combat.AttackClass.Basic;
				_meleeHitbox.HitstunDuration =
					FTT.Combat.BasicComboRules.DirectionalAttackHitstunFrames / 60f;
				// Both strikes launch: a small horizontal nudge and a strong
				// upward component (Godot 2D Y is down, so upward is negative).
				float baseKB = Data?.BasicAttackKnockback ?? 3f;
				_meleeHitbox.KnockbackForce = new Vector2(
					baseKB * FTT.Combat.BasicComboRules.DirectionalAttackHorizontalKnockback,
					-baseKB * FTT.Combat.BasicComboRules.DirectionalAttackVerticalKnockback);
			}

			TransitionTo(CharacterState.Attacking);
			PlayAnimation(upAttack ? "up_attack" : "down_attack");
			// Directional gameplay timing is fixed by BasicComboRules rather than
			// animation callbacks, so only presentation uses the authored sheet.
			_attackAnimationDriven = false;
			_attackFrameProgress = 0f;
		}

		public void OnAttackActiveStarted() {
			if (CurrentState != CharacterState.Attacking || _attackHitActive) return;
			_attackHitActive = true;
			int comboIdx = GetActiveComboIndex();
			bool directional = _attackVariant != FTT.Combat.BasicComboRules.VariantChain;
			bool upAttack = _attackVariant == FTT.Combat.BasicComboRules.VariantUpAttack;
			// Story-only AttackRange minors extend basic-attack melee reach.
			Vector2 hitboxSize = (directional
				? (upAttack ? UpAttackHitboxSize : DownAirHitboxSize)
				: _attackStartedAerial
					? AerialComboHitboxSizes[comboIdx]
					: ComboHitboxSizes[comboIdx]) * StoryAttackRangeMultiplier;
			Vector2 hitboxOffset = directional
				? (upAttack ? UpAttackHitboxOffset : DownAirHitboxOffset)
				: _attackStartedAerial
					? (_aerialHitboxMarker?.Position ?? AerialComboHitboxOffsets[comboIdx])
					: ComboHitboxOffsets[comboIdx];
			float facingMul = IsFacingRight ? 1f : -1f;
			// The directional boxes are centred on the fighter, so facing does
			// not mirror them (their X offset is zero by construction).
			Vector2 resolvedOffset = directional || _attackStartedAerial
				? hitboxOffset
				: new Vector2(hitboxOffset.X * facingMul, hitboxOffset.Y);

			if (_meleeHitbox?.GetChildCount() > 0 && _meleeHitbox.GetChild(0) is CollisionShape2D hitShape) {
				if (hitShape.Shape is RectangleShape2D rectShape) rectShape.Size = hitboxSize;
				hitShape.Position = resolvedOffset;
			}
			_meleeHitbox?.Activate();

		}

		public void OnAttackActiveEnded() {
			// Reaching the end of the active window mid-swing means the recovery
			// frames are running — the cancellable part of the swing.
			if (_attackHitActive && CurrentState == CharacterState.Attacking) _attackInRecovery = true;
			_attackHitActive = false;
			_meleeHitbox?.Deactivate();
		}

		private void OnCombatAnimationFinished(StringName animationName) {
			if (CurrentState != CharacterState.Attacking || !_attackAnimationDriven) return;
			if (!animationName.ToString().StartsWith("basic_", StringComparison.Ordinal)) return;
			CompleteCurrentComboHit();
		}

		private void CompleteCurrentComboHit() {
			OnAttackActiveEnded();
			_attackAnimationDriven = false;
			int comboIndex = GetActiveComboIndex();

			// A directional attack exits straight out: no buffered continuation,
			// no chain-hold window, chain reset (§2.8).
			if (_attackVariant != FTT.Combat.BasicComboRules.VariantChain) {
				_attackVariant = FTT.Combat.BasicComboRules.VariantChain;
				ResetActiveCombo();
				_nextAttackBuffered = false;
				_inRecoveryHold = false;
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
				return;
			}

			if (comboIndex < 2 && _nextAttackBuffered) {
				AdvanceActiveCombo();
				StartComboHit();
			} else if (comboIndex >= 2) {
				ResetActiveCombo();
				_nextAttackBuffered = false;
				_inRecoveryHold = false;
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			} else {
				_inRecoveryHold = true;
				_comboBufferFramesRemaining = ComboBufferFrames;
				_nextAttackBuffered = false;
			}
		}

		private void CancelActiveAttack() {
			_combatAnimationPlayer?.Stop();
			_attackAnimationDriven = false;
			_inRecoveryHold = false;
			_nextAttackBuffered = false;
			_attackVariant = FTT.Combat.BasicComboRules.VariantChain;
			OnAttackActiveEnded();
			_attackInRecovery = false;
		}

		private int GetActiveComboIndex() => Mathf.Clamp(
			_attackStartedAerial ? AerialComboCounter : GroundComboCounter,
			0,
			2);

		private FTT.Combat.CombatFrameTimeline GetActiveComboTimeline() =>
			_attackVariant == FTT.Combat.BasicComboRules.VariantUpAttack ? UpAttackTimeline
			: _attackVariant == FTT.Combat.BasicComboRules.VariantDownAir ? DownAirTimeline
			: (_attackStartedAerial ? AerialComboTimelines : ComboTimelines)[GetActiveComboIndex()];

		private void AdvanceActiveCombo() {
			if (_attackStartedAerial) AerialComboCounter = Mathf.Min(2, AerialComboCounter + 1);
			else GroundComboCounter = Mathf.Min(2, GroundComboCounter + 1);
			ComboCounter = GetActiveComboIndex();
		}

		private void ResetActiveCombo() {
			if (_attackStartedAerial) AerialComboCounter = 0;
			else GroundComboCounter = 0;
			ComboCounter = 0;
		}

		private bool CheckSpecialInput() {
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Special1) && SpecialOneCooldownTimer <= 0) {
				if (_special1 != null && _special1.TryExecute()) {
					_pendingSpecialSlot = 1;
					_specialStartedAerial = !IsOnFloor();
					PlayAnimation("special_1");
					TransitionTo(CharacterState.UsingSpecial);
					return true;
				}
			}
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Special2) && SpecialTwoCooldownTimer <= 0) {
				if (_special2 != null && _special2.TryExecute()) {
					_pendingSpecialSlot = 2;
					_specialStartedAerial = !IsOnFloor();
					PlayAnimation("special_2");
					TransitionTo(CharacterState.UsingSpecial);
					return true;
				}
			}
			return false;
		}

		private bool CheckUltimateInput() {
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Ultimate)) {
				bool meterReady = _ultimateMeter != null ? _ultimateMeter.IsFull : CurrentUltimateMeter >= 100f;
				if (meterReady && _ultimate != null && _ultimate.TryExecute()) {
					_ultimateStartedAerial = !IsOnFloor();
					PlayAnimation("ultimate");
					TransitionTo(CharacterState.UsingUltimate);
					FTT.Core.EventBus.Instance?.RaiseUltimateActivation(new FTT.Core.UltimateActivationPayload {
						PlayerIndex = PlayerIndex,
						AbilityID = _ultimate.Data?.AbilityID ?? "",
						Position = GlobalPosition
					});
					return true;
				}
			}
			return false;
		}

		private bool CheckBlockInput() {
			if (CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Block)) {
				TransitionTo(CharacterState.Blocking);
				return true;
			}
			return false;
		}

		private bool CheckMovementAbilityInput() {
			if (IsMovementRooted) return false;
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.MovementAbility) && MovementAbilityCooldownTimer <= 0) {
				if (_movementAbility != null && _movementAbility.TryExecute()) {
					PlayAnimation("movement_ability");
					TransitionTo(CharacterState.UsingMovementAbility);
					return true;
				}
			}
			return false;
		}

		private bool CheckInteractInput() {
			return false; // Interaction handled by interaction system
		}

		private void CheckDropThrough() {
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Down)) {
				if (_downTapFramesRemaining > 0) {
					TryDropThrough();
					_downTapFramesRemaining = 0;
				} else {
					_downTapFramesRemaining = FTT.Combat.StoryCombatRules.DownDoubleTapFrames;
				}
			}
		}

		private void TryDropThrough() {
			if (!FTT.Combat.StoryCombatRules.IsDropThroughAllowed(CurrentState)) return;

			// Find one-way platform below
			if (IsOnFloor()) {
				var collision = GetLastSlideCollision();
				if (collision != null) {
					var collider = collision.GetCollider() as PhysicsBody2D;
					if (collider == null || !collider.IsInGroup("OneWayPlatform")) return;

					_dropThroughPlatform = collider;
					AddCollisionExceptionWith(collider);
					_dropThroughFramesRemaining = FTT.Combat.StoryCombatRules.DropThroughFrames;
					_dropThroughTimer = DropThroughDuration;
					Velocity = new Vector2(Velocity.X, Mathf.Max(90f, Velocity.Y));
					if (CurrentState != CharacterState.Attacking) TransitionTo(CharacterState.Airborne);
				}
			}
		}

		private void UpdateDropThrough(float dt) {
			if (_dropThroughFramesRemaining > 0) {
				_dropThroughFramesRemaining--;
				_dropThroughTimer = Mathf.Max(0f, _dropThroughTimer - dt);
				if (_dropThroughFramesRemaining <= 0 && _dropThroughPlatform != null) {
					RemoveCollisionExceptionWith(_dropThroughPlatform);
					_dropThroughPlatform = null;
				}
			}
		}

		// === Hurtbox ===

		private void SetCrouchHurtbox() {
			if (_collisionShape?.Shape is RectangleShape2D rect) {
				float crouchedHeight = _normalHurtboxHeight * FTT.Combat.StoryCombatRules.CrouchHurtboxScale;
				rect.Size = new Vector2(rect.Size.X, crouchedHeight);
				_collisionShape.Position = new Vector2(
					_normalBodyShapePosition.X,
					_normalBodyShapePosition.Y + (_normalHurtboxHeight - crouchedHeight) * 0.5f);
			}
			if (_hurtboxShape?.Shape is RectangleShape2D hurtboxRect && _normalHurtboxSize.Y > 0f) {
				float crouchedHeight = _normalHurtboxSize.Y * FTT.Combat.StoryCombatRules.CrouchHurtboxScale;
				hurtboxRect.Size = new Vector2(_normalHurtboxSize.X, crouchedHeight);
				_hurtboxShape.Position = new Vector2(
					_normalHurtboxPosition.X,
					_normalHurtboxPosition.Y + (_normalHurtboxSize.Y - crouchedHeight) * 0.5f);
			}
		}

		private void RestoreHurtboxHeight() {
			if (_collisionShape?.Shape is RectangleShape2D rect) {
				rect.Size = new Vector2(rect.Size.X, _normalHurtboxHeight);
				_collisionShape.Position = _normalBodyShapePosition;
			}
			if (_hurtboxShape?.Shape is RectangleShape2D hurtboxRect && _normalHurtboxSize.Y > 0f) {
				hurtboxRect.Size = _normalHurtboxSize;
				_hurtboxShape.Position = _normalHurtboxPosition;
			}
		}

		// === Visual ===

		private void UpdateFacing(float hAxis) {
			if (Mathf.Abs(hAxis) > 0.1f) {
				IsFacingRight = hAxis > 0;
				UpdateSpriteFlip();
			}
		}

		private void UpdateSpriteFlip() {
			if (_animatedSprite != null) {
				_animatedSprite.FlipH = !IsFacingRight;
			}
		}

		/// <summary>
		/// The shared outline/glow arbiter for this body, or null before it is
		/// attached. Presentation only; nothing gameplay-side may read it.
		/// </summary>
		public FTT.Combat.GlowPresentationController Glow => _glow;

		private void UpdateHyperArmorPresentation() {
			bool isActive = HasActiveHyperArmor;
			if (_hyperArmorPresentationActive != isActive) {
				_hyperArmorPresentationActive = isActive;
				// The arbiter also subscribes to this event, but a direct push keeps
				// the shell correct when the bus autoload is absent (tests, tools).
				_glow?.SetHyperArmor(isActive);
				FTT.Core.EventBus.Instance?.RaiseHyperArmorChanged(new FTT.Core.HyperArmorPayload {
					PlayerIndex = PlayerIndex,
					IsActive = isActive
				});
			}

			bool spawnInvulnerable = IsPostRewindInvulnerable;
			if (_spawnInvulnerabilityPresentationActive == spawnInvulnerable) return;
			_spawnInvulnerabilityPresentationActive = spawnInvulnerable;
			_glow?.SetSpawnInvulnerability(spawnInvulnerable);
		}

		private void PlayAnimation(string animName) {
			if (_animatedSprite == null) return;
			_animatedSprite.SpeedScale = StatusAnimationMultiplier;
			if (_animatedSprite.Animation != animName
				&& _animatedSprite.SpriteFrames?.HasAnimation(animName) == true) {
				_animatedSprite.Play(animName);
			}
		}

		public void ResetStatusModifiers() {
			StatusMovementMultiplier = 1.0f;
			StatusJumpMultiplier = 1.0f;
			StatusAnimationMultiplier = 1.0f;
			StatusDamageTakenMultiplier = 1.0f;
			IsMovementRooted = false;
			if (_animatedSprite != null) _animatedSprite.SpeedScale = 1.0f;
		}

		// === Cooldowns ===

		private void UpdateCooldowns(float dt) {
			// Story-only CooldownReduction minors shorten every ability cooldown by
			// ticking the shared timers faster; abilities keep assigning the
			// authored CooldownDuration so the .tres numbers stay canonical.
			float cooldownDt = dt / Mathf.Max(0.05f, StoryCooldownMultiplier);
			if (SpecialOneCooldownTimer > 0) SpecialOneCooldownTimer -= cooldownDt;
			if (SpecialTwoCooldownTimer > 0) SpecialTwoCooldownTimer -= cooldownDt;
			if (MovementAbilityCooldownTimer > 0) MovementAbilityCooldownTimer -= cooldownDt;
		}

		// === Ledge Detection ===

		private void OnLedgeAreaEntered(Area2D area) {
			if (area is FTT.Environment.LedgeGrabPoint ledge) TryGrabLedge(ledge);
		}

		/// <summary>
		/// Story ledge capture. Gameplay-feel plan §2.11 widened the vertical gate
		/// from "falling only" to "falling, or rising slowly", so jumping up to a
		/// ledge catches it the way the Fighter simulation now does. Godot screen
		/// space is +Y down, so -150 px/s is the rising side of the band.
		/// </summary>
		public const float LedgeGrabMaximumRiseSpeed = -150f;

		public bool TryGrabLedge(FTT.Environment.LedgeGrabPoint ledge) {
			if (ledge == null
				|| CurrentState != CharacterState.Airborne
				|| Velocity.Y < LedgeGrabMaximumRiseSpeed) return false;
			if (!ledge.IsInGroup("Ledge") || !ledge.TryAcquire(this)) return false;

			_activeLedge = ledge;
			GlobalPosition = ledge.HangPosition;
			IsFacingRight = ledge.StageIsToRight;
			UpdateSpriteFlip();
			TransitionTo(CharacterState.LedgeHanging);
			return true;
		}
	}
}
