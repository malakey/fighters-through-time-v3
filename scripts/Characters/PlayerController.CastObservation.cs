using FTT.Combat;

namespace FTT.Characters {

    /// <summary>
    /// CM-CANCEL (combat and movement update 2026-10-16, decision 9.3, plan
    /// §2.9.3): a read-only window onto the Special this body is casting, so the
    /// Level 13 Mirror's decision adapter can project the clone's cast phase into
    /// the shared CPU observation (<c>CpuDecisionObservation.SelfSpecialCastPhase</c>)
    /// and the CPU stops pressing into its own cast. Nothing here writes state.
    /// </summary>
    public partial class PlayerController {

        /// <summary>
        /// The Special 1 or Special 2 node this body is executing — while it is in
        /// <see cref="CharacterState.UsingSpecial"/> and the cast has not ended —
        /// or null. Read its <see cref="BaseSpecial.CurrentPhase"/> and
        /// <see cref="BaseSpecial.PhaseFramesRemaining"/>; never drive it.
        /// </summary>
        public BaseSpecial ExecutingSpecialCast {
            get {
                if (CurrentState != CharacterState.UsingSpecial) return null;
                BaseSpecial ability = _pendingSpecialSlot == 2 ? _special2 : _special1;
                return ability != null && ability.IsExecuting ? ability : null;
            }
        }
    }
}
