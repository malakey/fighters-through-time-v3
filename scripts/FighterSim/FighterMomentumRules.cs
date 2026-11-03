using FTT.Combat;

namespace FTT.FighterSim {

    /// <summary>
    /// The Fighter sim's integer view of the Momentum gauge region of
    /// <see cref="BasicComboRules"/> (meter and Defy update 2026-10-31, plan
    /// section 1.4). The gauge is counted in units, BasicComboRules.MomentumUnitsPerPip to the pip, so no float
    /// ever reaches the deterministic simulation. Pure functions over an int: the
    /// caller owns the storage (<c>FighterVerbComponent.MomentumUnits</c> once
    /// MM-SIM renames it), and nothing here is state.
    /// </summary>
    public static class FighterMomentumRules {
        /// <summary>Units in one pip.</summary>
        public const int UnitsPerPip = BasicComboRules.MomentumUnitsPerPip;
        /// <summary>The gauge's cap in units.</summary>
        public const int MaxUnits = BasicComboRules.MomentumMaxUnits;
        /// <summary>The cost of one Special in units.</summary>
        public const int SpecialCostUnits = BasicComboRules.MomentumSpecialCostUnits;
        /// <summary>Units regenerated per frame.</summary>
        public const int RegenUnitsPerFrame = BasicComboRules.MomentumRegenUnitsPerFrame;

        /// <summary>The fill at spawn (2 pips).</summary>
        public static int StartUnits => BasicComboRules.MomentumStartUnits;

        /// <summary>The fill a Sudden Death round starts at (the same 2 pips).</summary>
        public static int SuddenDeathUnits => BasicComboRules.MomentumStartUnits;

        /// <summary>Clamps a fill into [0, <see cref="MaxUnits"/>].</summary>
        public static int Clamp(int units) => units < 0 ? 0 : (units > MaxUnits ? MaxUnits : units);

        /// <summary>
        /// Clamps a widened fill, so <see cref="Spend"/>, <see cref="Gain"/> and
        /// <see cref="Regen"/> saturate instead of wrapping on extreme inputs.
        /// </summary>
        static int Clamp(long units) => units < 0 ? 0 : (units > MaxUnits ? MaxUnits : (int)units);

        /// <summary>True when the gauge holds at least one pip for a Special.</summary>
        public static bool CanAfford(int units) => units >= SpecialCostUnits;

        /// <summary>The fill after paying for one Special (never below 0).</summary>
        public static int Spend(int units) => Clamp((long)units - SpecialCostUnits);

        /// <summary>The fill after a landed swing adds <paramref name="gainUnits"/>.</summary>
        public static int Gain(int units, int gainUnits) => Clamp((long)units + gainUnits);

        /// <summary>The fill after one frame of passive regeneration.</summary>
        public static int Regen(int units) => Clamp((long)units + RegenUnitsPerFrame);
    }
}
