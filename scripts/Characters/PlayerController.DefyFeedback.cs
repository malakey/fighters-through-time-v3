namespace FTT.Characters {

	/// <summary>
	/// MM-FX (meter and Defy update 2026-10-31, plan section 2.11, P30): the Story hero's side
	/// of the Defy presentation, the two partial hooks MM-RULES declared in
	/// <c>PlayerController.Defy.cs</c> and MM-SDEFY calls. The world-side burst (ring, label,
	/// pulse, shake, cue, snapshot) is <c>CoreMechanicCuePresenter</c>'s, on
	/// <c>EventBus.OnDefyExecuted</c>. Presentation only.
	/// </summary>
	public partial class PlayerController {

		/// <summary>True while the post-Defy invulnerability aura is painted (frames 1-6). Test seam.</summary>
		public bool DefyGlowActive { get; private set; }

		/// <summary>
		/// A Defy began: the sprite holds the <c>ultimate</c> sheet's first key pose and the
		/// documented post-Defy aura (the spawn-invulnerability layer) shows for the
		/// invulnerable frames.
		/// </summary>
		partial void PresentDefyStart() {
			ShowAttackPose("ultimate", FTT.Combat.AttackPoseRules.WindUpPose);
			DefyGlowActive = true;
			_glow?.SetSpawnInvulnerability(true);
		}

		/// <summary>
		/// The blast fired (frame 6, the last invulnerable frame): the aura ends, and the sprite
		/// returns to <c>idle</c> for the recovery (P30). A post-rewind protection that is still
		/// running keeps its own aura.
		/// </summary>
		partial void PresentDefyBlast(in FTT.Core.DefyExecutedPayload payload) {
			DefyGlowActive = false;
			if (!IsPostRewindInvulnerable) _glow?.SetSpawnInvulnerability(false);
			PlayAnimation("idle");
		}
	}
}
