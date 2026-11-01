using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.Characters {

	/// <summary>
	/// The Story active Defy History (meter and Defy update 2026-10-31, ledger
	/// <c>USER-DEFY-ACTIVE-BURST</c>, plan sections 2.4 to 2.7). A button, 50 Influence,
	/// six invulnerable frames, a blast on frame 6 (<c>PlayerController.DefyBlast.cs</c>)
	/// and a vulnerable recovery to frame 30. The passive lethal save is gone.
	/// MM-FX implements the two presentation hooks in <c>PlayerController.DefyFeedback.cs</c>.
	/// </summary>
	public partial class PlayerController {

		/// <summary>The current Defy frame, 1 to 30 (0 when not Defying). Frame 1 is the accepting tick.</summary>
		private int _defyFrame;
		// O10: a press made on the hero's own hitstop tick, held one deep and begun on the thaw tick.
		private bool _defyBuffered;
		private float _defyMeterBefore;

		/// <summary>True while the hero is in a Defy (<see cref="CharacterState.Defying"/>).</summary>
		public bool IsDefying => CurrentState == CharacterState.Defying;

		/// <summary>The current Defy frame, 1 to 30; 0 when not Defying. Test seam / presentation.</summary>
		public int DefyFrame => IsDefying ? _defyFrame : 0;

		/// <summary>Defies this controller has executed (a presentation counter).</summary>
		public int DefyExecutionCount { get; private set; }

		/// <summary>
		/// Set by a scripted sequence that must not offer Defy (Level 0's unlosable hold fight,
		/// P21). Never persisted.
		/// </summary>
		public bool DefyDisabledByScript { get; set; }

		/// <summary>
		/// True when a Defy press would begin now (P21's refusals plus 50 Influence). Side-effect
		/// free. It ignores the ability lockout, the Special lockout, Suppression and the Legacy
		/// Unlock Schedule (never gated).
		/// </summary>
		public bool CanDefyNow => DefyRefusal() == null;

		/// <summary>Why a Defy would be refused now, or null when it would begin (P21).</summary>
		private string DefyRefusal() {
			if (CurrentState is CharacterState.Dead or CharacterState.Respawning or CharacterState.Defying) return "state";
			if (CurrentHP <= 0) return "dead";
			if (CurrentState == CharacterState.UsingUltimate) return "own-ultimate";
			if (TimeFrozen) return "time-freeze";
			if (_rewindSuspended) return "death-rewind";
			if (IsRecoveryWorldHeld || _worldSuspended) return "world-held";
			if (DefyDisabledByScript) return "scripted";
			if (IsInsideTree() && FTT.Environment.ChronalRewindManager.IsWorldHeld(GetTree())) return "world-held";
			if (CurrentUltimateMeter < BasicComboRules.DefyInfluenceCost) return "meter";
			return null;
		}

		/// <summary>
		/// Called once a tick after the cooldown counters and before the tech-lock return and the
		/// state switch. A press inside the hero's own hitstop is held by
		/// <see cref="BufferDefyPress"/> and begins here on the thaw tick (O10).
		/// </summary>
		private void TryBeginDefy() {
			bool pressed = CurrentInputFrame.IsPressed(GameplayButtons.Defy);
			bool buffered = _defyBuffered;
			_defyBuffered = false;
			if (!pressed && !buffered) return;
			if (DefyRefusal() != null) return;
			BeginDefy();
		}

		/// <summary>The hitstop branch's call: hold one Defy press for the thaw tick.</summary>
		private void BufferDefyPress() {
			if (CurrentInputFrame.IsPressed(GameplayButtons.Defy) && DefyRefusal() == null) _defyBuffered = true;
		}

		private void BeginDefy() {
			_defyMeterBefore = CurrentUltimateMeter;
			// Cost: 50 Influence, a spend, never scaled (O15). The ability lockout, the Special
			// lockout and the swing latch are neither read nor armed (5.4).
			DrainUltimateMeter(BasicComboRules.DefyInfluenceCost);

			// Release from everything O9 makes legal: the swing, the cast, the grab, hitstun,
			// tumble, knockdown, the tech lock, the block stance, the daze, the movement ability.
			// TransitionTo cleans up the roll, the air dodge and the ledge hang.
			if (CurrentState == CharacterState.Attacking) CancelActiveAttack();
			ResetComboChain();
			InterruptActiveAbilities();
			ReleaseGrabState();
			_blockSystem?.EndBlock();
			_stunTimer = 0f;
			_stunTumble = false;
			_stunLeftTheGround = false;
			_hitstunBlockCancelBlocked = false;
			_hasPendingLaunch = false;
			_dazeTimer = 0f;
			_techLockoutSeconds = 0f;
			_techInvulnerabilitySeconds = 0f;
			ClearTumbleAfterHitstun();
			ClearKnockdown();
			Velocity = Vector2.Zero;
			_defyFrame = 0;
			TransitionTo(CharacterState.Defying);
			PresentDefyStart();
		}

		/// <summary>One Defying tick: frames 1 to 30 (P11).</summary>
		private void ProcessDefying(float dt) {
			_defyFrame++;
			if (_defyFrame > BasicComboRules.DefyTotalFrames) {
				// Frame 31: free. The hero acts on this tick.
				_defyFrame = 0;
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
				if (CurrentState == CharacterState.Idle) ProcessIdle(dt); else ProcessAirborne(dt);
				return;
			}

			if (_defyFrame <= BasicComboRules.DefyBlastFrame) {
				// Frames 1 to 6: no gravity, no motion; the Ultimate's first key pose.
				Velocity = Vector2.Zero;
				ShowAttackPose("ultimate", 0);
			} else {
				ApplyGravity(dt);
				Velocity = new Vector2(0f, Velocity.Y);
				PlayAnimation("idle");
			}

			if (_defyFrame == BasicComboRules.DefyBlastFrame) ExecuteDefyBlast();
		}

		/// <summary>Test seam: puts the hero at a Defy frame without paying or blasting.</summary>
		internal void SeedDefyFrameForTest(int frame) {
			if (frame <= 0) {
				_defyFrame = 0;
				if (CurrentState == CharacterState.Defying) TransitionTo(CharacterState.Idle);
				return;
			}
			_defyFrame = frame;
			TransitionTo(CharacterState.Defying);
		}

		/// <summary>
		/// Cashes the Rally echo pool in as a heal (P10): <c>min(missingHP, max(round(pool),
		/// ceil(MaxHP / 10)))</c>, HP only, no meter. The pool, its drain and the damage-taken
		/// meter it had not yet credited are then forfeited. An empty pool still heals the 10 %
		/// floor. Returns the HP healed.
		/// </summary>
		public int CashInEchoPool() {
			if (CurrentState == CharacterState.Dead) return 0;
			int pool = Mathf.RoundToInt(_echoPool);
			int heal = BasicComboRules.DefyHealPoints(MaximumHP - CurrentHP, pool, MaximumHP);
			int healed = HealStory(heal, isDefyHeal: true);
			_echoPool = 0f;
			_echoDrainPerFrame = 0f;
			return healed;
		}

		/// <summary>Presentation hook: a Defy began (the invulnerable startup). Implemented by MM-FX.</summary>
		partial void PresentDefyStart();

		/// <summary>Presentation hook: the Defy's blast fired. Implemented by MM-FX.</summary>
		partial void PresentDefyBlast(in DefyExecutedPayload payload);
	}
}
