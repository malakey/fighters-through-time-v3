namespace FTT.Characters {

	// CM-ULT (2026-10-16, decision 2.1; closes DEFER-ULT-TELEGRAPH): the A02
	// activation wind-up telegraph on the Level 13 Mirror clone — the same red
	// tint and Unblockable X the Fighter driver draws, through the shared
	// FTT.Combat.UltimateActivationTelegraph. Gated on
	// UsesFighterUltimateActivation, so the hero's own Story Ultimate (which casts
	// at once) never draws it. Presentation only.
	public partial class PlayerController {

		private bool _ultimateTelegraphShown;

		/// <summary>True while this controller shows the activation wind-up telegraph. Test/presentation read.</summary>
		public bool IsShowingUltimateTelegraph => _ultimateTelegraphShown;

		/// <summary>
		/// Re-derives the telegraph from the activation phase: shown for exactly
		/// the wind-up, hidden from the first active frame. Called at every phase
		/// edge of the activation strike and when the controller leaves
		/// <see cref="CharacterState.UsingUltimate"/>. A hide never creates the glyph.
		/// </summary>
		private void SyncUltimateTelegraph() {
			bool windup = UsesFighterUltimateActivation
				&& CurrentState == CharacterState.UsingUltimate
				&& _ultimateActivationPhase == UltimateActivationWindup;
			if (windup == _ultimateTelegraphShown) return;
			_ultimateTelegraphShown = windup;
			FTT.Combat.UltimateActivationTelegraph.Apply(this, _glow, windup);
		}

		/// <summary>The wind-up's critical cue, once, on the wind-up's first frame (A02 acceptance).</summary>
		private void PlayUltimateWindupCue() {
			if (!UsesFighterUltimateActivation) return;
			FTT.Core.AudioManager.Instance?.CoreCues?.Play(FTT.Core.CoreMechanicCueIDs.UltimateActivationWindup);
		}

		/// <summary>
		/// The <see cref="TransitionTo"/> exit hook: leaving
		/// <see cref="CharacterState.UsingUltimate"/> by any edge — a stun, a death,
		/// a grab, the whiff recovery's natural end — ends the activation strike's
		/// presentation and resets its phase, so a stunned or killed clone never
		/// keeps a stale wind-up (the latent A02 defect the plan names).
		/// </summary>
		private void OnLeaveUltimateState() {
			_ultimateActivationPhase = UltimateActivationNone;
			_ultimateActivationFrames = 0;
			SyncUltimateTelegraph();
		}
	}
}
