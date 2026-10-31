using FTT.Core;

namespace FTT.Characters {

	/// <summary>
	/// The Story active Defy History (meter and Defy update 2026-10-31, ledger
	/// <c>USER-DEFY-ACTIVE-BURST</c>, plan section 2.4). <b>MM-RULES declares the interface
	/// inert</b>: nothing enters <see cref="CharacterState.Defying"/> yet. MM-SDEFY owns this
	/// file and implements the action; MM-FX implements the two presentation hooks in
	/// <c>PlayerController.DefyFeedback.cs</c>.
	/// </summary>
	public partial class PlayerController {

		/// <summary>True while the hero is in a Defy (<see cref="CharacterState.Defying"/>).</summary>
		public bool IsDefying => false;

		/// <summary>Defies this controller has executed (a presentation counter; MM-SDEFY).</summary>
		public int DefyExecutionCount { get; private set; }

		/// <summary>True when a Defy press would begin now (P21's refusals plus 50 meter; MM-SDEFY).</summary>
		public bool CanDefyNow => false;

		/// <summary>Presentation hook: a Defy began (the invulnerable startup). Implemented by MM-FX.</summary>
		partial void PresentDefyStart();

		/// <summary>Presentation hook: the Defy's blast fired. Implemented by MM-FX.</summary>
		partial void PresentDefyBlast(in DefyExecutedPayload payload);
	}
}
