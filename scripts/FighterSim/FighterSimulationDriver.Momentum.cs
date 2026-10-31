using FTT.Combat;

namespace FTT.FighterSim {

    // Meter and Defy update 2026-10-31 (plan section 2.1): the driver's read seam for the
    // Fighter Momentum gauge, which the HUD (MM-HUD) polls. MM-RULES declares it inert: the
    // gauge is not in the sim yet, so every fighter reads the start fill. MM-SIM owns this
    // file and reads FighterVerbComponent.MomentumUnits here.
    public partial class FighterSimulationDriver {

        /// <summary>
        /// The player's Momentum gauge in sim units (0 to <see cref="BasicComboRules.MomentumMaxUnits"/>).
        /// Inert stub: always the start fill and always true.
        /// </summary>
        public bool TryGetMomentum(int playerID, out int units) {
            units = BasicComboRules.MomentumStartUnits;
            return true;
        }
    }
}
