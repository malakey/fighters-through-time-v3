using Godot;

namespace FTT.Characters {

	/// <summary>
	/// CM-HIT (combat and movement update 2026-10-16, decision 3.2, finding
	/// PFC-04; ledger <c>USER-TUMBLE-AFTER-HITSTUN</c> /
	/// <c>PROVISIONAL-TUMBLE-EXIT-ACTIONS</c>): the Smash rule, Story half. A
	/// launched hero whose hitstun ends while airborne keeps tumbling: it returns
	/// to <see cref="CharacterState.Airborne"/> with full control (so Defy's
	/// actionable test starts on time, as the sim's does), and the first thing it
	/// does — an air jump, the air dodge, the air strike, a Special, the
	/// Ultimate, the movement ability, a ledge grab — leaves the tumble and lands
	/// plainly. A hero that does nothing lands in it: the owed Down-Air slam
	/// bounce first, then held Block's landing tech, otherwise the M05
	/// knockdown. Drift and fast-fall keep it. The Fighter sim's twin is
	/// component 310 <c>Tumble</c> value 2 (<c>FighterVerbRules.UpdateTumble</c>).
	/// Story mobs are out of scope (<c>VERIFY-M05-KNOCKDOWN-SCOPE</c>), and a
	/// Story throw still never techs (F23), so a thrown hero never arms it.
	///
	/// <para>The latch clears on any state change but Airborne and Stunned
	/// (<see cref="TransitionTo"/>), in <see cref="PerformJump"/>, in
	/// <see cref="ApplyStun"/> (a new hit: a launcher re-tumbles through the
	/// hitstun path, a non-launcher ends the tumble), on a Defy proc, on death and
	/// on a rewind restore.</para>
	/// </summary>
	public partial class PlayerController {

		/// <summary>3.2: the launch's hitstun ended in the air and the hero has not acted or landed yet.</summary>
		private bool _tumbleAfterHitstun;

		/// <summary>3.2 test and bot seam: tumbling after the launch's hitstun ended (Airborne, actionable).</summary>
		public bool IsTumblingAfterHitstun => _tumbleAfterHitstun;

		/// <summary>3.2: every exit from the post-hitstun tumble funnels here.</summary>
		private void ClearTumbleAfterHitstun() => _tumbleAfterHitstun = false;

		/// <summary>
		/// 3.2, the <see cref="ProcessAirborne"/> landing check, run before the
		/// buffered jump, the roll and every other input: a hero still tumbling
		/// on its ground-contact frame resolves the landing as a tumble — the owed
		/// slam bounce (forced, untechable), then held Block's tech, otherwise the
		/// knockdown. A buffered jump does not cancel it. Returns true when the
		/// landing was a tumble landing.
		/// </summary>
		private bool TryResolveTumbleLanding() {
			if (!_tumbleAfterHitstun || !IsOnFloor()) return false;
			_tumbleAfterHitstun = false;
			_jumpBufferTimer = 0f;
			// A12: the owed bounce survives the end of the slam's hitstun while the
			// hero still tumbles (the sim's FighterSlamRules.ClearIfStale reads
			// the tumble, not the hitstun). The bounce is a fresh launched stun.
			if (_slamBouncePending) {
				_stunTumble = true;
				if (TryResolveSlamBounce()) {
					TransitionTo(CharacterState.Stunned);
					RestartAnimation("hitstun");
					return true;
				}
				_stunTumble = false;
			}
			TransitionTo(CharacterState.Stunned);
			if (CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Block)) {
				BeginLandingTech();
			} else {
				BeginKnockdown();
			}
			return true;
		}

		/// <summary>
		/// V7.1 landing tech (ukemi), shared by the hitstun landing in
		/// <see cref="ProcessStunned"/> and the post-hitstun tumble landing: hitstun
		/// ends in place with the 12-frame invulnerable locked recovery (the
		/// <c>_techLockoutSeconds</c> gate in <c>_PhysicsProcess</c> holds the
		/// hero, then releases to Idle; the sim's <c>TechLockoutFrames</c>
		/// branch). The tech reads the raw Block input, not the stance (V7.3).
		/// </summary>
		private void BeginLandingTech() {
			_stunTimer = 0f;
			_stunTumble = false;
			_stunLeftTheGround = false;
			_hitstunBlockCancelBlocked = false;
			_hasPendingLaunch = false;
			Velocity = Vector2.Zero;
			_techInvulnerabilitySeconds =
				FTT.Combat.BasicComboRules.LandingTechRecoveryFrames / 60f;
			_techLockoutSeconds =
				FTT.Combat.BasicComboRules.LandingTechRecoveryFrames / 60f;
		}
	}
}
