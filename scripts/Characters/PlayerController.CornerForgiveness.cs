using Godot;
using FTT.Core;

namespace FTT.Characters {

	/// <summary>
	/// CM-MOVE (combat and movement update 2026-10-16; plan §2.5.1 and §2.6.2):
	/// the Story half of the movement update that runs once a tick, just before
	/// the controller's <c>MoveAndSlide</c> — the short hop's own-jump latch (the
	/// Q5 ruling) and corner forgiveness (decision 6.2: the head-clip nudge and
	/// the rising lip pop). Story only: nothing here reaches the Fighter sim,
	/// whose own halves are <c>FighterMovementSystem.Movement.cs</c>.
	/// </summary>
	public partial class PlayerController {
		/// <summary>
		/// The Q5 latch: the current rise began with this airtime's own Jump press
		/// (a ground, coyote, buffered or air jump, or a ledge or rope jump-off).
		/// Written with <see cref="JumpedThisAirtime"/> (so floor contact, a ledge or
		/// rope grab and the Tempest Apex refresh clear it too) and cleared by
		/// <see cref="PrepareMoveAndSlide"/> once the hero is launched, stunned,
		/// dazed, thrown, grabbing, in a movement ability or an Ultimate, hanging,
		/// dead or respawning, or lifted by the Tempest — so a post-hitstun rise, a
		/// kit lift or a movement ability's boost is never cut by the ×2.5 band.
		/// Never persisted.
		/// </summary>
		private bool _riseFromOwnJump;

		/// <summary>The Q5 latch (read-only test and diagnostics seam).</summary>
		public bool RiseFromOwnJump => _riseFromOwnJump;

		/// <summary>
		/// The latch as the gravity step reads it: false while a cast writes the
		/// hero's vertical velocity itself — Shakespeare's Tempest lift, the one
		/// Story Special that does (it rewrites the rise on every active tick, so
		/// the controller's gravity step before each write must not be the ×2.5
		/// band). The Fighter sim's counterpart is
		/// <c>FighterMovementSystem.CastLiftedTheCaster</c>.
		/// </summary>
		private bool ShortHopReadsOwnJump => _riseFromOwnJump && !SpecialCastLiftsOwner();

		/// <summary>The running Special is a kit lift: the Tempest in its active frames.</summary>
		private bool SpecialCastLiftsOwner() =>
			CurrentState == CharacterState.UsingSpecial
			&& (_pendingSpecialSlot == 2 ? _special2 : _special1)
				is Abilities.ShakespeareTheTempest { CurrentPhase: FTT.Combat.AbilityPhase.Active };

		/// <summary>Corner forgiveness corrections made this session (diagnostics seam): head-clip nudges.</summary>
		public int HeadClipNudges { get; private set; }

		/// <summary>Corner forgiveness corrections made this session (diagnostics seam): lip pops.</summary>
		public int LipPops { get; private set; }

		/// <summary><see cref="UniversalMovementRules.HeadClipNudgeUnits"/> in Story pixels (12).</summary>
		public const float HeadClipNudgePixels = UniversalMovementRules.HeadClipNudgeUnits * FTT.Combat.KitMotionRules.StoryPixelsPerUnit;

		/// <summary><see cref="UniversalMovementRules.LipPopUnits"/> in Story pixels (10).</summary>
		public const float LipPopPixels = UniversalMovementRules.LipPopUnits * FTT.Combat.KitMotionRules.StoryPixelsPerUnit;

		/// <summary>
		/// The one call before <c>MoveAndSlide</c> (every unfrozen tick that gets
		/// there, whatever the state): the Q5 latch's state clears, then corner
		/// forgiveness in a normal-control airborne state.
		/// </summary>
		private void PrepareMoveAndSlide(float dt) {
			if (CurrentState is CharacterState.Stunned or CharacterState.Thrown or CharacterState.Dazed
				or CharacterState.Grabbing or CharacterState.UsingMovementAbility or CharacterState.UsingUltimate
				or CharacterState.LedgeHanging or CharacterState.Dead or CharacterState.Respawning
				|| SpecialCastLiftsOwner()) {
				_riseFromOwnJump = false;
			}
			if (CurrentState is CharacterState.LedgeHanging or CharacterState.Dead) return;
			ApplyCornerForgiveness(dt);
		}

		/// <summary>
		/// Decision 6.2 (Provisional, <c>PROVISIONAL-CORNER-FORGIVENESS-RULES</c>):
		/// two stateless current-frame corrections for a hero under normal-control
		/// air movement (Airborne, an airborne swing, the air dodge — never a launch
		/// state, a movement ability or a Special). Both only run on a tick whose
		/// first <c>TestMove</c> collides, so a clear tick costs one or two tests.
		/// <list type="bullet">
		/// <item><b>Head clip.</b> Rising into a ceiling (a collision normal pointing
		/// down, Y &gt; 0.7), the hero tries sideways shifts of 1 to
		/// <see cref="HeadClipNudgePixels"/> (12) px on the side away from the
		/// contact, nearest first, and takes the first whose shift and whose rise
		/// from there are both clear: a corner clipped by up to 12 px is slid past
		/// rather than bonked.</item>
		/// <item><b>Lip pop.</b> Rising while moving (or pushing) into a wall (|normal
		/// X| &gt; 0.7), the hero tries lifts of 1 to <see cref="LipPopPixels"/>
		/// (10) px and takes the first whose lift is clear and from which the
		/// horizontal step meets no wall: a ledge top up to 10 px above the feet is
		/// popped onto rather than missed.</item>
		/// </list>
		/// <c>TestMove</c> respects one-way shapes, so a one-way platform from below
		/// is never a ceiling, and full-height gate columns and lock barriers offer
		/// no lip to pop over.
		/// </summary>
		private void ApplyCornerForgiveness(float dt) {
			bool normalControlAir = CurrentState switch {
				CharacterState.Airborne => true,
				CharacterState.Attacking => !IsOnFloor(),
				CharacterState.Rolling => _airDodge,
				_ => false
			};
			if (!normalControlAir || UsesLaunchGravity || IsOnFloor()) return;
			if (Velocity.Y >= 0f) return;
			if (TryHeadClipNudge(dt)) return;
			TryLipPop(dt);
		}

		private bool TryHeadClipNudge(float dt) {
			var rise = new Vector2(0f, Velocity.Y * dt);
			var hit = new KinematicCollision2D();
			if (!TestMove(GlobalTransform, rise, hit)) return false;
			if (hit.GetNormal().Y <= 0.7f) return false;
			// The ceiling's corner overlaps the head on the side of the contact:
			// slide the other way. A contact dead centre prefers the travel.
			float contactX = hit.GetPosition().X;
			int side = contactX < GlobalPosition.X - 0.01f ? 1
				: contactX > GlobalPosition.X + 0.01f ? -1
				: Velocity.X < 0f ? -1 : Velocity.X > 0f ? 1 : IsFacingRight ? 1 : -1;
			int maxShift = Mathf.FloorToInt(HeadClipNudgePixels + 0.001f);
			for (int shift = 1; shift <= maxShift; shift++) {
				var offset = new Vector2(side * shift, 0f);
				if (TestMove(GlobalTransform, offset)) return false; // a wall beside: no room to nudge
				var after = new KinematicCollision2D();
				Transform2D shifted = GlobalTransform.Translated(offset);
				if (TestMove(shifted, rise, after) && after.GetNormal().Y > 0.7f) continue;
				GlobalPosition += offset;
				HeadClipNudges++;
				return true;
			}
			return false;
		}

		private bool TryLipPop(float dt) {
			float input = GetHorizontalInput();
			int direction = Velocity.X > 0.5f ? 1 : Velocity.X < -0.5f ? -1
				: input > 0.1f ? 1 : input < -0.1f ? -1 : 0;
			if (direction == 0) return false;
			var step = new Vector2(direction * Mathf.Max(Mathf.Abs(Velocity.X) * dt, 1f), 0f);
			var hit = new KinematicCollision2D();
			if (!TestMove(GlobalTransform, step, hit)) return false;
			if (Mathf.Abs(hit.GetNormal().X) <= 0.7f) return false;
			int maxLift = Mathf.FloorToInt(LipPopPixels + 0.001f);
			for (int lift = 1; lift <= maxLift; lift++) {
				var up = new Vector2(0f, -lift);
				if (TestMove(GlobalTransform, up)) return false; // a ceiling above: nothing to pop into
				var after = new KinematicCollision2D();
				Transform2D lifted = GlobalTransform.Translated(up);
				if (TestMove(lifted, step, after) && Mathf.Abs(after.GetNormal().X) > 0.7f) continue;
				GlobalPosition += up;
				LipPops++;
				return true;
			}
			return false;
		}
	}
}
