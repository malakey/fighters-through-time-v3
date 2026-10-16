namespace FTT.Characters {

	/// <summary>
	/// CM-STORY (combat and movement update 2026-10-16, decision 2.4 with the Q2
	/// ruling "startup only"; findings CMB-01, TWO-06; ledger
	/// <c>USER-STORY-ULTIMATE-ARMOR</c> / <c>PROVISIONAL-STORY-ULTIMATE-ARMOR</c>):
	/// a Story Ultimate is paid when it is accepted, and until now any mob or boss
	/// stun in its 30-frame startup threw the paid meter away. The hero's Ultimate
	/// is now armoured while its own cast clock is in Startup, through
	/// <see cref="FTT.Combat.StoryCombatRules.UltimateStartupArmorPreventsInterruption"/>
	/// (a Story rule; the nine <c>ultimate.tres</c> keep <c>GrantsHyperArmor</c>
	/// false, so the Fighter sim sees nothing). It follows the cast clock, so the
	/// caster's own hitstop and Time Freeze pause it with the cast. The Level 13
	/// Mirror clone's post-contact Story Ultimate runs through this controller, so
	/// its startup is armoured too (recorded beside <c>VERIFY-STORY-MIRROR-HOLD</c>).
	///
	/// <para>It also closes the StaticCharge leak for every armour window: a hit's
	/// StaticCharge was applied after the armour gate in
	/// <see cref="ResolveIncomingHit"/>, and its strategy stunned, which interrupted
	/// the armoured cast anyway (a Rail Charge too). While the hit is armoured
	/// against, the charge now enters its slot without its stun half.</para>
	/// </summary>
	public partial class PlayerController {

		/// <summary>
		/// True while <see cref="FTT.Combat.StaticChargeStrategy"/> must apply its slot without
		/// stunning: set only around the status application of a hit this body's
		/// armour shrugs off.
		/// </summary>
		internal bool ArmorAbsorbsStatusStun { get; private set; }

		/// <summary>2.4: the Ultimate's Startup armour against <paramref name="incoming"/>.</summary>
		private bool UltimateHasStartupArmor(FTT.Combat.AttackClass incoming) =>
			_ultimate != null && _ultimate.IsExecuting
			&& FTT.Combat.StoryCombatRules.UltimateStartupArmorPreventsInterruption(_ultimate.CurrentPhase, incoming);

		/// <summary>
		/// Applies a hit's attached status. <paramref name="armored"/> is the hit
		/// resolution's own armour read (<see cref="HasActiveHyperArmorAgainst"/>):
		/// an armoured body takes a StaticCharge into its control slot without the
		/// stun that would interrupt the armoured cast.
		/// </summary>
		private void ApplyHitStatus(in FTT.Combat.HitPayload hit, bool armored) {
			ArmorAbsorbsStatusStun = armored && hit.AppliedStatus == FTT.Core.StatusType.StaticCharge;
			try {
				_statusController?.ApplyStatus(hit.AppliedStatus, hit.StatusDuration, hit.StatusIntensity);
			} finally {
				ArmorAbsorbsStatusStun = false;
			}
		}
	}
}
