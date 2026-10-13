using System;

namespace FTT.Combat {

    /// <summary>
    /// The one canonical Influence (Ultimate) meter rulebook, read by both modes:
    /// Story's <see cref="UltimateMeter"/> node and the save/HUD clamps, and the
    /// Fighter simulation through its fixed-point adapter
    /// (<c>FTT.FighterSim.FighterMeterRules</c>). Design §"Influence Meter": the
    /// meter runs 0–100, damage dealt adds 1 point per HP, damage taken adds 0.25
    /// per HP, using the Ultimate resets it, and a Fighter stock loss retains 75%.
    ///
    /// <para>The rates are stored as integer rationals so the deterministic
    /// simulation can apply them in fixed point without a float ever reaching
    /// <c>scripts/FighterSim/</c>; the <c>float</c> views below are exact
    /// (100, 1, 0.25 and 0.75 are all representable) and exist for Story and the
    /// UI. This file must stay free of Godot types, like
    /// <see cref="BasicComboRules"/>, so the simulation and engine-free save
    /// payloads can reference it.</para>
    ///
    /// <para>Changing any value here is a design change: it moves every Fighter
    /// hash and needs a ledger row. Story-only gain modifiers (a Resonance lane
    /// such as Mozart's build rate) never live here — they are applied on the
    /// Story side only, so the competitive numbers stay normalized.</para>
    /// </summary>
    public static class InfluenceMeterRules {
        /// <summary>The full bar. An Ultimate needs it; Defy History needs it.</summary>
        public const int Cap = 100;

        /// <summary>Meter per HP of damage actually dealt: <c>1 / 1</c>.</summary>
        public const int DamageDealtNumerator = 1;
        public const int DamageDealtDenominator = 1;

        /// <summary>Meter per HP of damage actually taken: <c>1 / 4</c>.</summary>
        public const int DamageTakenNumerator = 1;
        public const int DamageTakenDenominator = 4;

        /// <summary>Share of the meter a Fighter keeps through a stock loss: <c>3 / 4</c>.</summary>
        public const int StockLossRetentionNumerator = 3;
        public const int StockLossRetentionDenominator = 4;

        // === Exact float views (Story, UI, engine-free save clamps) ===

        /// <summary><see cref="Cap"/> as a float.</summary>
        public const float MaxValue = Cap;

        /// <summary>1.0 meter per HP dealt.</summary>
        public const float PointsPerDamageDealt =
            (float)DamageDealtNumerator / DamageDealtDenominator;

        /// <summary>0.25 meter per HP taken.</summary>
        public const float PointsPerDamageTaken =
            (float)DamageTakenNumerator / DamageTakenDenominator;

        /// <summary>0.75 of the meter kept through a stock loss.</summary>
        public const float StockLossRetention =
            (float)StockLossRetentionNumerator / StockLossRetentionDenominator;

        // === Story gain (float) ===
        // The one statement of Story's two gain formulas. The Fighter sim never
        // calls these (it has no float and no gain factor); it applies the same
        // rationals in fixed point through FTT.FighterSim.FighterMeterRules.

        /// <summary>
        /// Story meter earned for <paramref name="damage"/> HP actually dealt,
        /// scaled by the hero's Story-only <paramref name="gainFactor"/> (1 when
        /// no lane applies). Negative inputs earn nothing. At a factor of 1 this
        /// is exactly the pre-rulebook <c>max(0, damage) × 1</c>.
        /// </summary>
        public static float GainFromDamageDealt(float damage, float gainFactor = 1f) =>
            MathF.Max(0f, damage) * PointsPerDamageDealt * MathF.Max(0f, gainFactor);

        /// <summary>
        /// Story meter earned for <paramref name="damage"/> HP actually taken —
        /// the permanent share at hit time, or Rally echo as it drains — scaled
        /// by the hero's Story-only <paramref name="gainFactor"/>. At a factor of
        /// 1 this is exactly the pre-rulebook <c>max(0, damage) × 0.25</c>.
        /// </summary>
        public static float GainFromDamageTaken(float damage, float gainFactor = 1f) =>
            MathF.Max(0f, damage) * PointsPerDamageTaken * MathF.Max(0f, gainFactor);
    }
}
