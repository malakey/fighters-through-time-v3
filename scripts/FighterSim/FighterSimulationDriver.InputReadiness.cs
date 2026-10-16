using FTT.Core;

namespace FTT.FighterSim {

    /// <summary>
    /// 9.2 (2026-10-16, CM-INPUT; ledger <c>USER-PAD-ULTIMATE-CHORD-LENIENCY</c>):
    /// after each local tick the driver tells <see cref="InputManager"/> whether
    /// each human-sampled fighter could cast its Ultimate, so the pad Ultimate
    /// chord's leniency window applies only while the chord could cast. A read
    /// of committed simulation state for the local input layer: nothing flows
    /// back into the simulation except the human's own frames, exactly as a
    /// player's timing would.
    /// </summary>
    public partial class FighterSimulationDriver {

        /// <summary>
        /// Reports slot 0 always and slot 1 when a local human plays it (not the
        /// CPU, which samples itself and never meets the window). A LAN match
        /// reports nothing, so its chord passes through.
        /// </summary>
        private void PushUltimateReadiness() {
            InputManager input = InputManager.Instance;
            if (input == null || Simulation == null) return;
            int humanSlots = _cpuController == null ? 2 : 1;
            for (int playerID = 0; playerID < humanSlots && playerID < input.MaxPlayers; playerID++) {
                input.ReportUltimateAffordable(playerID, IsUltimateAffordableForInput(playerID));
            }
        }

        /// <summary>
        /// Influence at the cap, and the fighter neither mid-activation (caster
        /// busy) nor held by the other's activation strike. The ability lockout is
        /// deliberately excluded: inside it the chord presses only the refused,
        /// flashing Ultimate.
        /// </summary>
        private bool IsUltimateAffordableForInput(int playerID) {
            if (!Simulation.TryGetFighter(playerID, out FighterStateComponent fighter)) return false;
            if (fighter.Influence < FighterMeterRules.Cap) return false;
            if (Simulation.TryGetFighterUltimateActivation(playerID, out FighterUltimateActivationComponent activation)
                && (FighterUltimateActivationRules.IsCasterBusy(in activation)
                    || FighterUltimateActivationRules.IsCaptured(in activation))) {
                return false;
            }
            return true;
        }
    }
}
