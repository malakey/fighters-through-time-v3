using Godot;
using System;
using System.Collections.Generic;

namespace FTT.Characters {

	public enum CharacterState {
		Idle,
		Running,
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
		public int StoryMaxHPBonus { get; set; }
		public int StoryBlockChargeBonus { get; set; }
		public float StoryMoveSpeedMultiplier { get; set; } = 1f;
		public float StoryJumpForceMultiplier { get; set; } = 1f;
		public float StoryBasicDamageMultiplier { get; set; } = 1f;
		public float StorySpecialDamageMultiplier { get; set; } = 1f;
		public float StoryTemporaryDamageMultiplier { get; private set; } = 1f;
		public float StoryTemporarySpeedMultiplier { get; private set; } = 1f;
		public bool IsPostRewindInvulnerable => _postRewindInvulnerabilityFrames > 0;
		public int MaximumHP => (Data?.MaxHP ?? 100) + StoryMaxHPBonus;
		public int MaximumBlockCharges => (Data?.MaxBlockCharges ?? 3) + StoryBlockChargeBonus;
		private float EffectiveMoveSpeed => (Data?.MaxMoveSpeed ?? 8f) * StoryMoveSpeedMultiplier * StoryTemporarySpeedMultiplier;
		private float EffectiveJumpForce => (Data?.MaxJumpForce ?? 14f) * StoryJumpForceMultiplier;

		// Physics constants
		private const float BaseGravity = 18.0f;
		private const float FallGravityMultiplier = 1.8f;
		private const float ShortHopGravityMultiplier = 2.5f;
		private const float DirectionReversalPenalty = 0.7f;
		private const float TerminalVelocity = 600.0f;
		private const float GroundRampFrames = FTT.Core.UniversalMovementRules.RunAccelerationFrames;
		private const float AirAccelRampFrames = 4.0f;
		private const float AirDecelRampFrames = 8.0f;

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
		private CanvasItem _chronalArmorOverlay;
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
		private ColorRect _meleeHitVisual;
		private uint _rewindCollisionLayer;
		private uint _rewindCollisionMask;
		private bool _rewindSuspended;
		private int _dashFramesRemaining;
		private int _dashDirection;
		private int _rollFrame;
		private int _rollDirection;
		private bool _rollInvulnerable;
		private bool _hyperArmorPresentationActive;

		// Basic attack timing
		private int _attackFramesRemaining;
		private const int ComboBufferFrames = FTT.Combat.StoryCombatRules.ComboBufferFrames;
		private int _comboBufferFramesRemaining;
		private bool _comboBufferActive;
		private bool _nextAttackBuffered;
		private bool _attackHitActive;
		private bool _inRecoveryHold;
		private int _pendingSpecialSlot;
		private bool _attackStartedAerial;
		private bool _attackStartedCrouched;
		private bool _attackAnimationDriven;
		private bool _specialStartedAerial;
		private bool _ultimateStartedAerial;

		private static readonly FTT.Combat.CombatFrameTimeline[] ComboTimelines = {
			new(6, 6, 15),
			new(7, 7, 16),
			new(15, 9, 21)
		};
		private static readonly FTT.Combat.CombatFrameTimeline[] AerialComboTimelines = {
			new(5, 7, 13),
			new(6, 8, 14),
			new(12, 10, 18)
		};
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
			_chronalArmorOverlay = GetNodeOrNull<CanvasItem>("ChronalArmorOverlay");

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
			_meleeHitVisual = GetNodeOrNull<ColorRect>("MeleeHitVisual");

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
			bool wasLedgeHanging = CurrentState == CharacterState.LedgeHanging;

			if (CurrentState == CharacterState.Blocking && _blockSystem != null) {
				FTT.Combat.BlockResult blockResult = _blockSystem.ResolveHit(hit);
				if (blockResult != FTT.Combat.BlockResult.NotBlocked) {
					FTT.Core.CameraShake.Instance?.Shake(3f, 0.08f);
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
				Vector2 knockback = FTT.Combat.DamageCalculator.CalculateKnockback(
					hit.Knockback,
					Data?.Weight ?? 1f,
					hit.AttackerFacingRight);
				Velocity += knockback * 60f;

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
			SpawnDamageNumber(damageApplied, hit.HitOrigin);
			return damageApplied;
		}

		private void SpawnDamageNumber(int damage, Vector2 position) {
			FTT.UI.FloatingDamageNumber.Show(damage, position + new Vector2(-10, -30), GetParent());
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
				case CharacterState.Dashing:
					ProcessDashing(dt);
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
					bool bodyBlocked = _pushbox.ResolveStoryOverlaps(
						CurrentState == CharacterState.Rolling ? _rollDirection : 0);
					if (bodyBlocked && CurrentState == CharacterState.Dashing) {
						TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
					}
				}
			}

			_wasGrounded = IsOnFloor();
		}

		// === State Processors ===

		private void ProcessIdle(float dt) {
			ApplyGravity(dt);
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float step = maxSpeed / GroundRampFrames * dt * 60f;
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
			if (CheckDashInput()) return;
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
			if (CheckDashInput()) return;
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

		private void ProcessDashing(float dt) {
			ApplyGravity(dt);
			if (!IsOnFloor() || IsMovementRooted) {
				TransitionTo(CharacterState.Airborne);
				return;
			}

			int elapsed = FTT.Core.UniversalMovementRules.DashDurationFrames - _dashFramesRemaining;
			if (elapsed >= FTT.Core.UniversalMovementRules.DashCommitFrames) {
				if (CheckJumpInput()) return;
				if (CheckAttackInput()) return;
			}

			float dashSpeed = EffectiveMoveSpeed
				* StatusMovementMultiplier
				* 60f
				* FTT.Core.UniversalMovementRules.DashSpeedMultiplier;
			Velocity = new Vector2(_dashDirection * dashSpeed, Velocity.Y);
			_dashFramesRemaining--;
			if (_dashFramesRemaining <= 0 || IsOnWall()) {
				TransitionTo(CharacterState.Idle);
				return;
			}
			PlayAnimation("dash");
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
			float step = maxSpeed / GroundRampFrames * dt * 60f;
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, step), Velocity.Y);
		}

		private void ProcessSkidding(float dt) {
			ApplyGravity(dt);
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float step = maxSpeed / GroundRampFrames * dt * 60f;
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
			float step = maxSpeed / GroundRampFrames * dt * 60f;
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
			float rampFrames = isAccelerating ? GroundRampFrames : AirDecelRampFrames;
			float step = maxSpeed / rampFrames * dt * 60f;
			var vel = Velocity;
			vel.X = Mathf.MoveToward(vel.X, targetSpeed, step);
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
				float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
				float step = maxSpeed / GroundRampFrames * dt * 60f;
				var vel = Velocity;
				vel.X = Mathf.MoveToward(vel.X, 0, step);
				Velocity = vel;
			} else {
				ApplyAirControl(dt);
			}

			if (_inRecoveryHold) {
				ProcessRecoveryHold(dt);
				return;
			}

			CheckDropThrough();

			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.BasicAttack) && !_attackHitActive) {
				_nextAttackBuffered = true;
			}

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

			_attackFramesRemaining--;
			if (_attackFramesRemaining <= 0) {
				CompleteCurrentComboHit();
			}
		}

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

			if (CheckJumpInput()) {
				_inRecoveryHold = false;
				ResetActiveCombo();
				return;
			}

			if (CheckSpecialInput()) {
				_inRecoveryHold = false;
				ResetActiveCombo();
				return;
			}

			if (CheckBlockInput()) {
				_inRecoveryHold = false;
				ResetActiveCombo();
				return;
			}

			if (_comboBufferFramesRemaining <= 0) {
				_inRecoveryHold = false;
				ResetActiveCombo();
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
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
			float step = maxSpeed / GroundRampFrames * dt * 60f;
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
				case CharacterState.Dashing:
					_dashFramesRemaining = FTT.Core.UniversalMovementRules.DashDurationFrames;
					IsFacingRight = _dashDirection > 0;
					UpdateSpriteFlip();
					break;
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
			_stunTimer = duration;
			TransitionTo(CharacterState.Stunned);
		}

		public int ApplyDamage(int damage) => ApplyDamage(damage, ignoreRollInvulnerability: false);

		public int ApplyPersistentDamage(int damage) => ApplyDamage(damage, ignoreRollInvulnerability: true);

		private int ApplyDamage(int damage, bool ignoreRollInvulnerability) {
			if (CurrentState == CharacterState.Dead || CurrentState == CharacterState.Respawning) return 0;
			if (_postRewindInvulnerabilityFrames > 0) return 0;
			if (_rollInvulnerable && !ignoreRollInvulnerability) return 0;
			damage = Math.Max(0, (int)MathF.Round(damage * StatusDamageTakenMultiplier));
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
			if (_postRewindInvulnerabilityFrames > 0) _postRewindInvulnerabilityFrames--;
		}

		public void ApplyStockLossMeterRetention() {
			_ultimateMeter?.ApplyStockLossRetention();
			CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? CurrentUltimateMeter * 0.75f;
		}

		public bool HasActiveHyperArmor =>
			AbilityHasActiveHyperArmor(_special1) ||
			AbilityHasActiveHyperArmor(_special2) ||
			AbilityHasActiveHyperArmor(_movementAbility) ||
			AbilityHasActiveHyperArmor(_ultimate);

		public bool HasActiveHyperArmorAgainst(FTT.Combat.AttackClass attackClass) =>
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
			if (_animatedSprite.SpriteFrames?.HasAnimation(animationName) == true) _animatedSprite.Play(animationName);
		}

		public void SetRewindSuspended(bool suspended) {
			if (_rewindSuspended == suspended) return;
			_rewindSuspended = suspended;
			if (suspended) {
				_rewindCollisionLayer = CollisionLayer;
				_rewindCollisionMask = CollisionMask;
				CollisionLayer = 0;
				CollisionMask = 0;
				if (_hurtbox != null) {
					_hurtbox.Monitoring = false;
					_hurtbox.Monitorable = false;
				}
				_pushbox?.SetPushEnabled(false);
				Velocity = Vector2.Zero;
				ProcessMode = ProcessModeEnum.Disabled;
			} else {
				CollisionLayer = _rewindCollisionLayer;
				CollisionMask = _rewindCollisionMask;
				if (_hurtbox != null) {
					_hurtbox.Monitoring = true;
					_hurtbox.Monitorable = true;
				}
				_pushbox?.SetPushEnabled(true);
				ProcessMode = ProcessModeEnum.Inherit;
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
			if (IsOnFloor()) return;

			float effectiveGravity = BaseGravity * (0.8f + 0.4f * (Data?.Weight ?? 1.0f));
			float multiplier = 1.0f;

			if (Velocity.Y > 0) {
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
			float step = maxSpeed / GroundRampFrames * dt * 60f;
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
			float rampFrames = isAccelerating ? GroundRampFrames : AirDecelRampFrames;
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

		private bool CheckDashInput() {
			if (IsMovementRooted || !IsOnFloor()) return false;
			if (!CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Dash)) return false;
			float input = GetHorizontalInput();
			_dashDirection = Mathf.Abs(input) > 0.1f ? Math.Sign(input) : (IsFacingRight ? 1 : -1);
			TransitionTo(CharacterState.Dashing);
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
			_nextAttackBuffered = false;
			_inRecoveryHold = false;

			if (_meleeHitbox != null) {
				float baseDmg = (Data?.BasicAttackDamage ?? 10f) * StoryBasicDamageMultiplier;
				_meleeHitbox.Damage = baseDmg * ComboDamageMultipliers[comboIdx];
				_meleeHitbox.AttackID = $"{Data?.CharacterID ?? "fighter"}.basic";
				_meleeHitbox.HitboxID = $"combo_{comboIdx + 1}";
				_meleeHitbox.AttackClass = FTT.Combat.AttackClass.Basic;

				float baseKB = Data?.BasicAttackKnockback ?? 3f;
				if (comboIdx == 2) {
					_meleeHitbox.KnockbackForce = new Vector2(baseKB * 2f, -4f);
					_meleeHitbox.HitstunDuration = 0.3f;
				} else if (comboIdx == 1) {
					_meleeHitbox.KnockbackForce = new Vector2(baseKB * 1.2f, -1.5f);
					_meleeHitbox.HitstunDuration = 0.2f;
				} else {
					_meleeHitbox.KnockbackForce = new Vector2(baseKB, -1f);
					_meleeHitbox.HitstunDuration = 0.15f;
				}
			}

			if (_aerialHitboxMarker != null && _attackStartedAerial) {
				Vector2 offset = AerialComboHitboxOffsets[comboIdx];
				_aerialHitboxMarker.Position = new Vector2(IsFacingRight ? offset.X : -offset.X, offset.Y);
			}

			TransitionTo(CharacterState.Attacking);
			string animationName = $"basic_{(_attackStartedAerial ? "air" : "ground")}_{comboIdx + 1}";
			_attackAnimationDriven = _combatAnimationPlayer?.HasAnimation(animationName) == true;
			if (_attackAnimationDriven) _combatAnimationPlayer.Play(animationName);
		}

		public void OnAttackActiveStarted() {
			if (CurrentState != CharacterState.Attacking || _attackHitActive) return;
			_attackHitActive = true;
			int comboIdx = GetActiveComboIndex();
			Vector2 hitboxSize = _attackStartedAerial
				? AerialComboHitboxSizes[comboIdx]
				: ComboHitboxSizes[comboIdx];
			Vector2 hitboxOffset = _attackStartedAerial
				? (_aerialHitboxMarker?.Position ?? AerialComboHitboxOffsets[comboIdx])
				: ComboHitboxOffsets[comboIdx];
			float facingMul = IsFacingRight ? 1f : -1f;
			Vector2 resolvedOffset = _attackStartedAerial
				? hitboxOffset
				: new Vector2(hitboxOffset.X * facingMul, hitboxOffset.Y);

			if (_meleeHitbox?.GetChildCount() > 0 && _meleeHitbox.GetChild(0) is CollisionShape2D hitShape) {
				if (hitShape.Shape is RectangleShape2D rectShape) rectShape.Size = hitboxSize;
				hitShape.Position = resolvedOffset;
			}
			_meleeHitbox?.Activate();

			if (_meleeHitVisual != null) {
				_meleeHitVisual.Size = hitboxSize;
				_meleeHitVisual.Position = HitShapePosition(resolvedOffset, hitboxSize);
				_meleeHitVisual.Color = new Color(1, 1, 0.3f, 0.5f);
			}

			Vector2 HitShapePosition(Vector2 offset, Vector2 size) =>
				new(offset.X - size.X * 0.5f, offset.Y - size.Y * 0.5f);
		}

		public void OnAttackActiveEnded() {
			_attackHitActive = false;
			_meleeHitbox?.Deactivate();
			if (_meleeHitVisual != null) _meleeHitVisual.Color = new Color(1, 1, 0.3f, 0f);
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
			OnAttackActiveEnded();
		}

		private int GetActiveComboIndex() => Mathf.Clamp(
			_attackStartedAerial ? AerialComboCounter : GroundComboCounter,
			0,
			2);

		private FTT.Combat.CombatFrameTimeline GetActiveComboTimeline() =>
			(_attackStartedAerial ? AerialComboTimelines : ComboTimelines)[GetActiveComboIndex()];

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
					TransitionTo(CharacterState.UsingSpecial);
					return true;
				}
			}
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Special2) && SpecialTwoCooldownTimer <= 0) {
				if (_special2 != null && _special2.TryExecute()) {
					_pendingSpecialSlot = 2;
					_specialStartedAerial = !IsOnFloor();
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
					TransitionTo(CharacterState.UsingUltimate);
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

		private void UpdateHyperArmorPresentation() {
			bool isActive = HasActiveHyperArmor;
			if (_chronalArmorOverlay != null) _chronalArmorOverlay.Visible = isActive;
			if (_hyperArmorPresentationActive == isActive) return;

			_hyperArmorPresentationActive = isActive;
			FTT.Core.EventBus.Instance?.RaiseHyperArmorChanged(new FTT.Core.HyperArmorPayload {
				PlayerIndex = PlayerIndex,
				IsActive = isActive
			});
		}

		private void PlayAnimation(string animName) {
			if (_animatedSprite != null) _animatedSprite.SpeedScale = StatusAnimationMultiplier;
			if (_animatedSprite != null && _animatedSprite.Animation != animName) {
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
			if (SpecialOneCooldownTimer > 0) SpecialOneCooldownTimer -= dt;
			if (SpecialTwoCooldownTimer > 0) SpecialTwoCooldownTimer -= dt;
			if (MovementAbilityCooldownTimer > 0) MovementAbilityCooldownTimer -= dt;
		}

		// === Ledge Detection ===

		private void OnLedgeAreaEntered(Area2D area) {
			if (area is FTT.Environment.LedgeGrabPoint ledge) TryGrabLedge(ledge);
		}

		public bool TryGrabLedge(FTT.Environment.LedgeGrabPoint ledge) {
			if (ledge == null || CurrentState != CharacterState.Airborne || Velocity.Y < 0) return false;
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
