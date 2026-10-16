namespace FTT.Characters {

	/// <summary>
	/// 9.2 (2026-10-16, CM-INPUT; ledger <c>USER-PAD-ULTIMATE-CHORD-LENIENCY</c>):
	/// the Story hero tells <see cref="FTT.Core.InputManager"/> whether its
	/// Ultimate is affordable, so the pad Ultimate chord's leniency window
	/// (<see cref="FTT.Core.UltimateChordWindow"/>) holds a lone LB or RB back only
	/// while the chord could actually cast. Input-side only: nothing here touches
	/// a cast, a cooldown or the meter.
	/// </summary>
	public partial class PlayerController {

		/// <summary>
		/// Whether this hero could cast its Ultimate right now, for the input layer:
		/// the meter is full, the Ultimate slot is restored (the Legacy Unlock
		/// Schedule), and the cast is neither Suppressed nor refused by Time Freeze.
		/// The ability lockout is deliberately not part of it — inside the lockout
		/// the chord should press only the refused, flashing Ultimate, never its
		/// components (the case that leaked the movement ability before 9.2).
		/// </summary>
		public bool IsUltimateAffordableForInput {
			get {
				if (TimeFrozen || _abilityCastSuppressed || _ultimate == null) return false;
				if (!IsAbilityUnlocked(FTT.Core.AbilitySlot.Ultimate)) return false;
				return _ultimateMeter != null
					? _ultimateMeter.IsFull
					: CurrentUltimateMeter >= FTT.Combat.InfluenceMeterRules.MaxValue;
			}
		}

		/// <summary>
		/// Called once per physics tick, at the top of <c>_PhysicsProcess</c> so no
		/// early return (hitstop, the tech lock) skips it: the state the previous
		/// tick and its physics callbacks left is what the next tick's input sample
		/// reads. A Story-hostile body (the Level 13 Mirror clone) never reports —
		/// its frames come from the CPU, which bypasses the window.
		/// </summary>
		private void ReportUltimateReadiness() {
			if (IsStoryHostile) return;
			FTT.Core.InputManager.Instance?.ReportUltimateAffordable(PlayerIndex, IsUltimateAffordableForInput);
		}
	}
}
