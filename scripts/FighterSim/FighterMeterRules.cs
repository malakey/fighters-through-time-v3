using FTT.Combat;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// The Fighter simulation's fixed-point view of the one Influence meter
    /// rulebook, <see cref="InfluenceMeterRules"/>. Every sim, CPU and driver read
    /// of the cap, the two gain rates and the stock-loss retention goes through
    /// here, so the sim holds no meter literal of its own.
    ///
    /// <para><b>Bit-identical by construction.</b> The rulebook's integer
    /// rationals are converted once with <c>FP64.FromInt</c>, and each helper
    /// performs exactly the operations the sim used to inline: a 1/1 dealt rate
    /// is the bare <c>FromInt(damage)</c> (no multiply or divide by one), a
    /// 1/<i>n</i> taken rate is a single divide by <i>n</i>, and the retention is
    /// <c>meter × num / den</c> in that order. A rate whose numerator is not 1
    /// falls back to <c>× num / den</c>, which is a deliberate design change that
    /// moves every hash. No float is ever touched, and nothing here is state.</para>
    ///
    /// <para>Story progression never reaches this class: Story-only gain lanes
    /// (Mozart's build rate) are applied on the Story side alone.</para>
    /// </summary>
    public static class FighterMeterRules {
        /// <summary>The full bar as an integer, <see cref="InfluenceMeterRules.Cap"/>.</summary>
        public const int CapPoints = InfluenceMeterRules.Cap;

        /// <summary>The full bar, <see cref="InfluenceMeterRules.Cap"/>.</summary>
        public static readonly FP64 Cap = FP64.FromInt(CapPoints);

        private static readonly bool DealtIsUnitRate =
            InfluenceMeterRules.DamageDealtNumerator == 1
            && InfluenceMeterRules.DamageDealtDenominator == 1;
        private static readonly FP64 DealtNumerator = FP64.FromInt(InfluenceMeterRules.DamageDealtNumerator);
        private static readonly FP64 DealtDenominator = FP64.FromInt(InfluenceMeterRules.DamageDealtDenominator);

        private static readonly bool TakenHasUnitNumerator = InfluenceMeterRules.DamageTakenNumerator == 1;
        private static readonly FP64 TakenNumerator = FP64.FromInt(InfluenceMeterRules.DamageTakenNumerator);
        private static readonly FP64 TakenDenominator = FP64.FromInt(InfluenceMeterRules.DamageTakenDenominator);

        private static readonly FP64 RetentionNumerator = FP64.FromInt(InfluenceMeterRules.StockLossRetentionNumerator);
        private static readonly FP64 RetentionDenominator = FP64.FromInt(InfluenceMeterRules.StockLossRetentionDenominator);

        /// <summary>
        /// Meter earned for <paramref name="damage"/> HP actually dealt. At the
        /// rulebook's 1/1 this is exactly <c>FP64.FromInt(damage)</c>.
        /// </summary>
        public static FP64 FromDamageDealt(int damage) =>
            DealtIsUnitRate
                ? FP64.FromInt(damage)
                : FP64.FromInt(damage) * DealtNumerator / DealtDenominator;

        /// <summary>
        /// Meter earned for <paramref name="damage"/> HP actually taken (the
        /// permanent share at hit time, or Rally echo as it drains). At the
        /// rulebook's 1/4 this is exactly <c>damage / FP64.FromInt(4)</c>.
        /// </summary>
        public static FP64 FromDamageTaken(FP64 damage) =>
            TakenHasUnitNumerator
                ? damage / TakenDenominator
                : damage * TakenNumerator / TakenDenominator;

        /// <summary>
        /// The meter a fighter keeps through a stock loss: exactly
        /// <c>influence × FromInt(3) / FromInt(4)</c> at the rulebook's 3/4.
        /// </summary>
        public static FP64 RetainOnStockLoss(FP64 influence) =>
            influence * RetentionNumerator / RetentionDenominator;
    }
}
