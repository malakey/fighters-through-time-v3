using Godot;
using System;
using System.Collections.Generic;

namespace FTT.Characters {

	public enum CharacterState {
		Idle,
		Running,
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

		[Export] public CharacterData Data;
		[Export] public int PlayerIndex = 0;

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

		// Physics constants
		private const float BaseGravity = 18.0f;
		private const float FallGravityMultiplier = 1.8f;
		private const float ShortHopGravityMultiplier = 2.5f;
		private const float DirectionReversalPenalty = 0.7f;
		private const float TerminalVelocity = 600.0f;
		private const float GroundRampFrames = 3.0f;
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
		private float _lastDownTapTime = -1.0f;
		private const float DoubleTapWindow = 0.3f;

		// Jump tracking
		private bool _jumpHeld;
		private bool _wasGrounded;

		// Crouching
		private float _normalHurtboxHeight;
		private const float CrouchHurtboxScale = 0.7f;

		// Drop-through
		private CharacterBody2D _dropThroughPlatform;

		// Node references
		private AnimatedSprite2D _animatedSprite;
		private CollisionShape2D _collisionShape;
		private Area2D _ledgeDetector;

		// Combat wiring
		private FTT.Combat.BaseSpecial _special1;
		private FTT.Combat.BaseSpecial _special2;
		private FTT.Combat.BaseSpecial _movementAbility;
		private FTT.Combat.BaseSpecial _ultimate;
		private FTT.Combat.UltimateMeter _ultimateMeter;
		private FTT.Combat.BlockSystem _blockSystem;
		private FTT.Combat.StatusController _statusController;
		private FTT.Combat.Hurtbox _hurtbox;
		private FTT.Combat.Hitbox _meleeHitbox;
		private ColorRect _meleeHitVisual;

		// Basic attack timing
		private float _attackTimer;
		private const float BasicAttackDuration = 0.4f;
		private const float BasicAttackWindup = 0.08f;
		private const float BasicAttackHitWindow = 0.15f;
		private bool _attackHitActive;
		private int _pendingSpecialSlot;

		public override void _Ready() {
			if (Data != null) {
				CurrentHP = Data.MaxHP;
				CurrentBlockCharges = Data.MaxBlockCharges;
				RemainingJumps = Data.MaxJumpCount;
			}

			_animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
			_collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			_ledgeDetector = GetNodeOrNull<Area2D>("LedgeDetector");

			if (_collisionShape?.Shape is RectangleShape2D rect) {
				_normalHurtboxHeight = rect.Size.Y;
			}

			if (_ledgeDetector != null) {
				_ledgeDetector.AreaEntered += OnLedgeAreaEntered;
			}

			MotionMode = MotionModeEnum.Grounded;
			UpDirection = Vector2.Up;
			FloorStopOnSlope = true;

			InitializeCombatNodes();
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
			_meleeHitbox = GetNodeOrNull<FTT.Combat.Hitbox>("MeleeHitbox");
			_meleeHitVisual = GetNodeOrNull<ColorRect>("MeleeHitVisual");

			if (_hurtbox != null) {
				_hurtbox.OnHit += OnHurtboxHit;
			}
		}

		private void OnHurtboxHit(float damage, Vector2 knockback, float hitstun, Vector2 hitPosition) {
			if (CurrentState == CharacterState.Dead || CurrentState == CharacterState.Respawning) return;

			if (CurrentState == CharacterState.Blocking && _blockSystem != null) {
				if (_blockSystem.AbsorbHit()) {
					FTT.Core.CameraShake.Instance?.Shake(3f, 0.08f);
					return;
				}
			}

			int dmg = (int)damage;
			ApplyDamage(dmg);

			float direction = hitPosition.X < GlobalPosition.X ? 1f : -1f;
			var vel = Velocity;
			vel += new Vector2(Mathf.Abs(knockback.X) * direction * 60f, knockback.Y * 60f);
			Velocity = vel;

			if (hitstun > 0 && CurrentState != CharacterState.Dead) {
				ApplyStun(hitstun);
			}

			_ultimateMeter?.AddFromDamageTaken(damage);
			FTT.Core.CameraShake.Instance?.Shake(damage * 0.5f, 0.1f);
			SpawnDamageNumber(dmg, hitPosition);
		}

		private void SpawnDamageNumber(int damage, Vector2 position) {
			var num = new Label();
			num.Text = damage.ToString();
			num.GlobalPosition = position + new Vector2(-10, -30);
			num.AddThemeColorOverride("font_color", damage >= 15 ? new Color(1, 0.3f, 0.1f) : new Color(1, 0.9f, 0.3f));
			num.AddThemeFontSizeOverride("font_size", damage >= 15 ? 22 : 16);
			GetParent()?.AddChild(num);

			var tween = num.CreateTween();
			tween.TweenProperty(num, "position:y", num.Position.Y - 50, 0.7f);
			tween.Parallel().TweenProperty(num, "modulate:a", 0f, 0.7f);
			tween.TweenCallback(Callable.From(() => num.QueueFree()));
		}

		public override void _PhysicsProcess(double delta) {
			float dt = (float)delta;

			UpdateCooldowns(dt);
			UpdateDropThrough(dt);

			switch (CurrentState) {
				case CharacterState.Idle:
					ProcessIdle(dt);
					break;
				case CharacterState.Running:
					ProcessRunning(dt);
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

			_wasGrounded = IsOnFloor();
		}

		// === State Processors ===

		private void ProcessIdle(float dt) {
			ApplyGravity(dt);
			float maxSpeed = (Data?.MaxMoveSpeed ?? 8f) * 60f;
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

			if (Input.IsActionPressed(FTT.Core.InputManager.Actions.Down)) {
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

			if (CheckJumpInput()) return;
			if (CheckAttackInput()) return;
			if (CheckBlockInput()) return;
			if (CheckMovementAbilityInput()) return;

			float hAxis = GetHorizontalInput();
			if (Mathf.Abs(hAxis) < 0.1f) {
				TransitionTo(CharacterState.Idle);
				return;
			}

			if (Input.IsActionPressed(FTT.Core.InputManager.Actions.Down)) {
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

		private void ProcessSkidding(float dt) {
			ApplyGravity(dt);
			float maxSpeed = (Data?.MaxMoveSpeed ?? 8f) * 60f;
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
			float maxSpeed = (Data?.MaxMoveSpeed ?? 8f) * 60f;
			float step = maxSpeed / GroundRampFrames * dt * 60f;
			var crouchVel = Velocity;
			crouchVel.X = Mathf.MoveToward(crouchVel.X, 0, step);
			Velocity = crouchVel;

			if (!Input.IsActionPressed(FTT.Core.InputManager.Actions.Down)) {
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
			float maxSpeed = (Data?.MaxMoveSpeed ?? 8f) * 60f;
			float airFactor = Data?.AirControlMultiplier ?? 0.9f;
			float targetSpeed = hAxis * maxSpeed * airFactor;
			bool isAccelerating = Mathf.Abs(targetSpeed) >= Mathf.Abs(Velocity.X)
				|| (targetSpeed > 0 && Velocity.X < 0) || (targetSpeed < 0 && Velocity.X > 0);
			float rampFrames = isAccelerating ? AirAccelRampFrames : AirDecelRampFrames;
			float step = maxSpeed / rampFrames * dt * 60f;
			var vel = Velocity;
			vel.X = Mathf.MoveToward(vel.X, targetSpeed, step);
			Velocity = vel;

			UpdateFacing(hAxis);

			if (IsOnFloor()) {
				if (_jumpBufferTimer > 0) {
					PerformJump();
				} else {
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

			PlayAnimation(Velocity.Y < 0 ? "jump_rise" : "jump_fall");
		}

		private void ProcessAttacking(float dt) {
			ApplyGravity(dt);
			if (IsOnFloor()) {
				float hAxis = GetHorizontalInput();
				if (Mathf.Abs(hAxis) > 0.1f) {
					ApplyHorizontalMovement(hAxis, dt);
				} else {
					float maxSpeed = (Data?.MaxMoveSpeed ?? 8f) * 60f;
					float step = maxSpeed / GroundRampFrames * dt * 60f;
					var vel = Velocity;
					vel.X = Mathf.MoveToward(vel.X, 0, step);
					Velocity = vel;
				}
			} else {
				ApplyAirControl(dt);
			}

			_attackTimer -= dt;

			if (_attackTimer <= BasicAttackDuration - BasicAttackWindup && !_attackHitActive) {
				_attackHitActive = true;
				if (_meleeHitbox != null) {
					float facingMul = IsFacingRight ? 1f : -1f;
					if (_meleeHitbox.GetChildCount() > 0 && _meleeHitbox.GetChild(0) is CollisionShape2D hitShape)
						hitShape.Position = new Vector2(30 * facingMul, -32);
					_meleeHitbox.Activate();
				}
				if (_meleeHitVisual != null) {
					float vx = IsFacingRight ? 5 : -55;
					_meleeHitVisual.Position = new Vector2(vx, -55);
					_meleeHitVisual.Color = new Color(1, 1, 0.3f, 0.5f);
				}
			}

			if (_attackHitActive && _attackTimer <= BasicAttackDuration - BasicAttackWindup - BasicAttackHitWindow) {
				_attackHitActive = false;
				_meleeHitbox?.Deactivate();
				if (_meleeHitVisual != null) _meleeHitVisual.Color = new Color(1, 1, 0.3f, 0f);
			}

			if (_attackTimer <= 0) {
				_meleeHitbox?.Deactivate();
				if (_meleeHitVisual != null) _meleeHitVisual.Color = new Color(1, 1, 0.3f, 0f);
				_attackHitActive = false;
				ComboCounter = (ComboCounter + 1) % 3;
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		private void ProcessUsingSpecial(float dt) {
			ApplyGravity(dt);
			var ability = _pendingSpecialSlot == 2 ? _special2 : _special1;
			if (ability == null || !ability.IsExecuting) {
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		private void ProcessUsingUltimate(float dt) {
			if (_ultimate == null || !_ultimate.IsExecuting) {
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		private void ProcessBlocking(float dt) {
			ApplyGravity(dt);
			float maxSpeed = (Data?.MaxMoveSpeed ?? 8f) * 60f;
			float step = maxSpeed / GroundRampFrames * dt * 60f;
			var blockVel = Velocity;
			blockVel.X = Mathf.MoveToward(blockVel.X, 0, step);
			Velocity = blockVel;

			_blockSystem?.StartBlock();

			if (!Input.IsActionPressed(FTT.Core.InputManager.Actions.Block)) {
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
				TransitionTo(CharacterState.Airborne);
				return;
			}

			if (Input.IsActionJustPressed(FTT.Core.InputManager.Actions.Jump)) {
				TransitionTo(CharacterState.Airborne);
				var vel = Velocity;
				vel.Y = -(Data?.MaxJumpForce ?? 14f) * 45f;
				Velocity = vel;
				return;
			}

			if (Input.IsActionJustPressed(FTT.Core.InputManager.Actions.Down)) {
				TransitionTo(CharacterState.Airborne);
				return;
			}

			float hAxis = GetHorizontalInput();
			bool towardStage = (IsFacingRight && hAxis > 0.1f) || (!IsFacingRight && hAxis < -0.1f);
			if (Input.IsActionJustPressed(FTT.Core.InputManager.Actions.Jump) || towardStage) {
				PlayAnimation("ledge_pull_up");
				TransitionTo(CharacterState.Idle);
				return;
			}

			PlayAnimation("ledge_hang");
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
			CurrentState = newState;

			switch (newState) {
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
					if (oldState != CharacterState.Airborne) {
						ComboCounter = 0;
					}
					break;
				case CharacterState.Idle:
					if (oldState == CharacterState.Airborne || oldState == CharacterState.Attacking) {
						ComboCounter = 0;
					}
					if (oldState == CharacterState.Crouching) {
						RestoreHurtboxHeight();
					}
					break;
			}
		}

		public void ApplyStun(float duration) {
			if (CurrentState == CharacterState.Dead || CurrentState == CharacterState.Respawning) return;
			_stunTimer = duration;
			TransitionTo(CharacterState.Stunned);
		}

		public void ApplyDamage(int damage) {
			CurrentHP -= damage;
			FTT.Core.EventBus.Instance?.RaisePlayerHPChanged(new FTT.Core.PlayerHPPayload {
				PlayerIndex = PlayerIndex,
				CurrentHP = CurrentHP,
				MaxHP = Data?.MaxHP ?? 100,
				DamageAmount = damage
			});

			if (CurrentHP <= 0) {
				CurrentHP = 0;
				TransitionTo(CharacterState.Dead);
				FTT.Core.EventBus.Instance?.RaisePlayerDied(PlayerIndex);
			}
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
			float maxSpeed = (Data?.MaxMoveSpeed ?? 8f) * 60f;
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
			float maxSpeed = (Data?.MaxMoveSpeed ?? 8f) * 60f;
			float airFactor = Data?.AirControlMultiplier ?? 0.9f;
			float targetSpeed = hAxis * maxSpeed * airFactor;
			bool isAccelerating = Mathf.Abs(targetSpeed) >= Mathf.Abs(Velocity.X)
				|| (targetSpeed > 0 && Velocity.X < 0) || (targetSpeed < 0 && Velocity.X > 0);
			float rampFrames = isAccelerating ? AirAccelRampFrames : AirDecelRampFrames;
			float step = maxSpeed / rampFrames * dt * 60f;
			var vel = Velocity;
			vel.X = Mathf.MoveToward(vel.X, targetSpeed, step);
			Velocity = vel;
		}

		// === Input Helpers ===

		private float GetHorizontalInput() {
			float axis = 0f;
			if (Input.IsActionPressed(FTT.Core.InputManager.Actions.MoveRight)) axis += 1f;
			if (Input.IsActionPressed(FTT.Core.InputManager.Actions.MoveLeft)) axis -= 1f;
			return axis;
		}

		private bool CheckJumpInput() {
			_jumpHeld = Input.IsActionPressed(FTT.Core.InputManager.Actions.Jump);

			if (Input.IsActionJustPressed(FTT.Core.InputManager.Actions.Jump)) {
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
			vel.Y = -(Data?.MaxJumpForce ?? 14f) * 45f;
			Velocity = vel;
			RemainingJumps--;
			_coyoteTimer = 0;
			TransitionTo(CharacterState.Airborne);
		}

		private bool CheckAttackInput() {
			if (Input.IsActionJustPressed(FTT.Core.InputManager.Actions.BasicAttack)) {
				_attackTimer = BasicAttackDuration;
				_attackHitActive = false;
				if (_meleeHitbox != null) {
					float dmg = Data?.BasicAttackDamage ?? 10f;
					if (ComboCounter == 2) dmg = FTT.Combat.DamageCalculator.CalculateComboFinisherDamage(dmg);
					_meleeHitbox.Damage = dmg;
					_meleeHitbox.KnockbackForce = ComboCounter == 2
						? new Vector2(5f, -3f)
						: new Vector2(Data?.BasicAttackKnockback ?? 3f, -1.5f);
				}
				TransitionTo(CharacterState.Attacking);
				_ultimateMeter?.AddFromDamageDealt(Data?.BasicAttackDamage ?? 10f);
				return true;
			}
			return false;
		}

		private bool CheckSpecialInput() {
			if (Input.IsActionJustPressed(FTT.Core.InputManager.Actions.Special1) && SpecialOneCooldownTimer <= 0) {
				if (_special1 != null && _special1.TryExecute()) {
					_pendingSpecialSlot = 1;
					TransitionTo(CharacterState.UsingSpecial);
					return true;
				}
			}
			if (Input.IsActionJustPressed(FTT.Core.InputManager.Actions.Special2) && SpecialTwoCooldownTimer <= 0) {
				if (_special2 != null && _special2.TryExecute()) {
					_pendingSpecialSlot = 2;
					TransitionTo(CharacterState.UsingSpecial);
					return true;
				}
			}
			return false;
		}

		private bool CheckUltimateInput() {
			if (Input.IsActionJustPressed(FTT.Core.InputManager.Actions.Ultimate)) {
				bool meterReady = _ultimateMeter != null ? _ultimateMeter.IsFull : CurrentUltimateMeter >= 100f;
				if (meterReady && _ultimate != null && _ultimate.TryExecute()) {
					TransitionTo(CharacterState.UsingUltimate);
					return true;
				}
			}
			return false;
		}

		private bool CheckBlockInput() {
			if (Input.IsActionPressed(FTT.Core.InputManager.Actions.Block)) {
				TransitionTo(CharacterState.Blocking);
				return true;
			}
			return false;
		}

		private bool CheckMovementAbilityInput() {
			if (Input.IsActionJustPressed(FTT.Core.InputManager.Actions.MovementAbility) && MovementAbilityCooldownTimer <= 0) {
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
			if (Input.IsActionJustPressed(FTT.Core.InputManager.Actions.Down)) {
				double now = Time.GetTicksMsec() / 1000.0;
				if (now - _lastDownTapTime <= DoubleTapWindow) {
					TryDropThrough();
					_lastDownTapTime = -1.0f;
				} else {
					_lastDownTapTime = (float)now;
				}
			}
		}

		private void TryDropThrough() {
			if (CurrentState == CharacterState.Stunned ||
				CurrentState == CharacterState.Dazed ||
				CurrentState == CharacterState.Dead) return;

			// Find one-way platform below
			if (IsOnFloor()) {
				var collision = GetLastSlideCollision();
				if (collision != null) {
					var collider = collision.GetCollider();
					if (collider is CharacterBody2D platform) {
						_dropThroughPlatform = platform;
						AddCollisionExceptionWith(platform);
						_dropThroughTimer = DropThroughDuration;
					} else if (collider is StaticBody2D staticPlatform) {
						AddCollisionExceptionWith(staticPlatform);
						_dropThroughTimer = DropThroughDuration;
					}
				}
			}
		}

		private void UpdateDropThrough(float dt) {
			if (_dropThroughTimer > 0) {
				_dropThroughTimer -= dt;
				if (_dropThroughTimer <= 0 && _dropThroughPlatform != null) {
					RemoveCollisionExceptionWith(_dropThroughPlatform);
					_dropThroughPlatform = null;
				}
			}
		}

		// === Hurtbox ===

		private void SetCrouchHurtbox() {
			if (_collisionShape?.Shape is RectangleShape2D rect) {
				rect.Size = new Vector2(rect.Size.X, _normalHurtboxHeight * CrouchHurtboxScale);
			}
		}

		private void RestoreHurtboxHeight() {
			if (_collisionShape?.Shape is RectangleShape2D rect) {
				rect.Size = new Vector2(rect.Size.X, _normalHurtboxHeight);
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

		private void PlayAnimation(string animName) {
			if (_animatedSprite != null && _animatedSprite.Animation != animName) {
				_animatedSprite.Play(animName);
			}
		}

		// === Cooldowns ===

		private void UpdateCooldowns(float dt) {
			if (SpecialOneCooldownTimer > 0) SpecialOneCooldownTimer -= dt;
			if (SpecialTwoCooldownTimer > 0) SpecialTwoCooldownTimer -= dt;
			if (MovementAbilityCooldownTimer > 0) MovementAbilityCooldownTimer -= dt;
		}

		// === Ledge Detection ===

		private void OnLedgeAreaEntered(Area2D area) {
			if (CurrentState != CharacterState.Airborne) return;
			if (Velocity.Y < 0) return; // Must be falling
			if (!area.IsInGroup("Ledge")) return;

			TransitionTo(CharacterState.LedgeHanging);
		}
	}
}
