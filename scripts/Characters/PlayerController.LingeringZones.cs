using FTT.Core;

namespace FTT.Characters {

	public partial class PlayerController {

		/// <summary>
		/// Lingering-kit pass (Vortex, KL-SZONE): true while this player is in a roll's travel frames
		/// or an air dodge, the frames a positional zone pull does not take hold of. Read only by the
		/// Sandstorm Vortex's Story pull; the Fighter sim has its own phase test.
		/// </summary>
		public bool IsRollTravelOrAirDodge =>
			CurrentState == CharacterState.Rolling && (_airDodge || IsRollTravelFrame);
	}
}
