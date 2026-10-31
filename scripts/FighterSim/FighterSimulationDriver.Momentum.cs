using FTT.Combat;

namespace FTT.FighterSim {

    // Meter and Defy update 2026-10-31 (plan section 2.1): the driver's read seam for the
    // Fighter Momentum gauge, which the HUD (MM-HUD) polls. Presentation only: it reads
    // FighterVerbComponent.MomentumUnits and never writes the simulation.
    public partial class FighterSimulationDriver {

        /// <summary>
        /// The player's Momentum gauge in sim units (0 to <see cref="BasicComboRules.MomentumMaxUnits"/>).
        /// False when the simulation does not exist yet or the player is unknown.
        /// </summary>
        public bool TryGetMomentum(int playerID, out int units) {
            units = 0;
            if (Simulation == null || !Simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)) {
                return false;
            }
            units = verb.MomentumUnits;
            return true;
        }
    }
}
