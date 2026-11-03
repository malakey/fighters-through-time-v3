using Godot;

namespace FTT.Characters {

	public partial class PlayerController {
		// The running attack (ruling 9b, 2026-11-03; GR-RUN). Every number lives in
		// the "running attack" region of BasicComboRules, the table the Fighter
		// simulation reads. Do not author numbers here.

		private static readonly FTT.Combat.CombatFrameTimeline RunAttackTemplateTimeline = new(
			FTT.Combat.BasicComboRules.RunAttackStartupFrames,
			FTT.Combat.BasicComboRules.RunAttackActiveFrames,
			FTT.Combat.BasicComboRules.RunAttackRecoveryFrames);

		/// <summary>
		/// Ruling 15's lead definition of "running at press time": the Running state,
		/// moving along the facing at the shared fraction of the current maximum run
		/// speed or more, so the first ramp frames of a push still open the string.
		/// The sim approximates it (<c>FighterBasicAttackRules.IsRunningForAttack</c>).
		/// </summary>
		private bool IsRunningForAttack => FTT.Combat.BasicComboRules.IsRunningForAttack(
			CurrentState == CharacterState.Running,
			IsFacingRight ? Velocity.X : -Velocity.X,
			EffectiveMoveSpeed * StatusMovementMultiplier * 60f);

		/// <summary>
		/// The running attack's active and recovery frames, where it no longer steers,
		/// read from the swing's own clock at the top of the tick.
		/// </summary>
		private bool RunAttackCommitted =>
			_attackVariant == FTT.Combat.BasicComboRules.VariantRunAttack
			&& _runAttackTimeline.GetPhase(_runAttackTimeline.TotalFrames - _attackFramesRemaining)
				!= FTT.Combat.CombatFramePhase.Startup;

		/// <summary>
		/// A committed running attack on the floor: the active frames travel the
		/// lunge along the facing (less under a slow, none rooted) whatever the stick
		/// says, and the recovery brakes on the stop ramp. Hitstop never reaches here,
		/// so a landing pauses the lunge.
		/// </summary>
		private void ApplyRunAttackMotion(float dt) {
			int elapsed = _runAttackTimeline.TotalFrames - _attackFramesRemaining;
			// Rooted (the sim's ApplyRunAttackLunge): no forced lunge, the stop ramp only.
			if (IsMovementRooted
				|| _runAttackTimeline.GetPhase(elapsed) != FTT.Combat.CombatFramePhase.Active) {
				ApplyHorizontalMovement(0f, dt);
				return;
			}
			float speed = (float)(FTT.Combat.BasicComboRules.RunAttackLungeSpeedUnits
				* FTT.Combat.KitMotionRules.StoryPixelsPerUnit) * StatusMovementMultiplier;
			Vector2 velocity = Velocity;
			velocity.X = IsFacingRight ? speed : -speed;
			Velocity = velocity;
		}

		/// <summary>
		/// The running attack's box in Story pixels, for a hero facing right, relative
		/// to the feet pivot: string hit 1's grounded box with its far edge moved out
		/// by <see cref="FTT.Combat.BasicComboRules.RunAttackReachBonusUnits"/> and its
		/// near edge where it was. The bonus is flat in the profile's width scale, as
		/// the sim's is, and the Story-only AttackRange minor scales it with the rest,
		/// so a minor still moves the far edge by its full percentage.
		/// </summary>
		internal static void ResolveRunAttackBoxPixels(
			FTT.Combat.BasicStringProfile profile,
			float reachMultiplier,
			out Vector2 size,
			out Vector2 offset) {
			ResolveGroundStringBoxPixels(
				FTT.Combat.BasicComboRules.RunAttackStringStep,
				profile,
				reachMultiplier,
				out size,
				out offset);
			float bonus = (float)(FTT.Combat.BasicComboRules.RunAttackReachBonusUnits
				* FTT.Combat.KitMotionRules.StoryPixelsPerUnit) * reachMultiplier;
			size.X += bonus;
			offset.X += bonus * 0.5f;
		}

		/// <summary>
		/// Arms the running attack: one strike outside the three-hit chain, a flat
		/// 1.2x basic, 30-frame hitstun, 1.5x knockback, never a launch, posed on
		/// <c>basic_attack_2</c>. The box is placed at active start
		/// (<see cref="ResolveRunAttackBoxPixels"/>).
		/// </summary>
		private void StartRunAttack() {
			if (_meleeHitbox != null) {
				float baseDamage = (Data?.BasicAttackDamage ?? 10f) * StoryBasicDamageMultiplier;
				_meleeHitbox.Damage = baseDamage * FTT.Combat.BasicComboRules.RunAttackDamageMultiplier;
				_meleeHitbox.AttackID = $"{Data?.CharacterID ?? "fighter"}.basic";
				_meleeHitbox.HitboxID = FTT.Combat.BasicComboRules.RunAttackHitboxID;
				_meleeHitbox.AttackClass = FTT.Combat.AttackClass.Basic;
				_meleeHitbox.Launches = FTT.Combat.BasicComboRules.RunAttackLaunches;
				_meleeHitbox.Delivery = FTT.Combat.HitDelivery.DirectHit;
				_meleeHitbox.Origin = FTT.Combat.HitOrigin.Basic;
				_meleeHitbox.HitstunDuration = FTT.Combat.BasicComboRules.RunAttackHitstunFrames / 60f;
				// Hit 1's shape with the larger multiplier: the same vertical
				// component, so the shove stays a shove.
				float baseKnockback = Data?.BasicAttackKnockback ?? 3f;
				_meleeHitbox.KnockbackForce = new Vector2(
					baseKnockback * FTT.Combat.BasicComboRules.RunAttackKnockbackMultiplier,
					-1f);
				// No rider: clear the shared hitbox so a finisher's authored status
				// or combo mark cannot leak in.
				_meleeHitbox.AppliedStatus = FTT.Core.StatusType.None;
				_meleeHitbox.StatusDuration = 0f;
				_meleeHitbox.StatusIntensity = 1f;
				_meleeHitbox.ComboMark = FTT.Combat.ComboMarkType.None;
				_meleeHitbox.ComboMarkFrames = 0;
			}

			TransitionTo(CharacterState.Attacking);
			// No new art: the running attack is posed on hit 2's sheet (F1).
			_attackPoseAnimation = $"basic_attack_{FTT.Combat.BasicComboRules.RunAttackPoseStringStep + 1}";
			ShowAttackPose(_attackPoseAnimation, FTT.Combat.AttackPoseRules.WindUpPose);
			_attackFrameProgress = 0f;
		}
	}
}
