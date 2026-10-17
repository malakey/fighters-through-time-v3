using Godot;

namespace FTT.Characters {

	/// <summary>
	/// CM-STORY (combat and movement update 2026-10-16, decision 1.1(1), finding
	/// MOV-13): Story movement abilities fall under Story gravity.
	///
	/// <para>Before this, <see cref="ProcessUsingMovementAbility"/> never called
	/// <c>ApplyGravity</c>, and every kit wrote <c>Owner.Velocity</c> after the
	/// controller's <c>MoveAndSlide</c>. So whatever vertical speed a cast started
	/// or ended with carried for the whole cast: Leonardo's ornithopter boost rose
	/// for the full three-second glide (about 1,250 px), and Tesla's, Tubman's,
	/// Cleopatra's and Mozart's recoveries hovered.</para>
	///
	/// <para>Now gravity runs every tick of the cast unless the ability says it
	/// holds its owner off it (<see cref="FTT.Combat.BaseSpecial.SuspendsOwnerGravityNow"/>,
	/// true exactly where the Fighter sim suspends gravity, or where the kit
	/// rewrites the owner's velocity every tick of a phase), with the short-hop
	/// band off so a boost is never cut by it, and the apex hang reading Jump live
	/// (CM-FIXB-STORY, finding B11, decision 6.1: it used to read Jump as held,
	/// which hung a recovery with Jump released), then capped by the ability's
	/// <see cref="FTT.Combat.BaseSpecial.OwnerFallSpeedCapNow"/> (Leonardo's glide
	/// sink). Story only: nothing in the Fighter sim reads either member.</para>
	/// </summary>
	public partial class PlayerController {

		/// <summary>
		/// One tick of Story gravity for the executing movement ability, before
		/// the controller's move. Called from <see cref="ProcessUsingMovementAbility"/>.
		/// </summary>
		private void ApplyMovementAbilityGravity(float dt) {
			if (_movementAbility == null || _movementAbility.SuspendsOwnerGravityNow) return;
			ApplyGravity(dt, ignoreShortHop: true);
			float cap = _movementAbility.OwnerFallSpeedCapNow;
			if (Velocity.Y > cap) Velocity = new Vector2(Velocity.X, cap);
		}
	}
}
