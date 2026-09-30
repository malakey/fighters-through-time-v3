namespace FTT.Combat {

    /// <summary>
    /// A02 (Package 13 W6): the shape of a Fighter-mode Ultimate activation
    /// strike. Serialized on <see cref="AbilityData.ActivationShape"/> and
    /// projected into the Fighter loadout as its ordinal — append only.
    /// </summary>
    public enum UltimateActivationShape {
        /// <summary>A box carried in front of the caster, who lunges <c>ActivationRange</c> forward (Joan's banner charge).</summary>
        Melee = 0,
        /// <summary>A straight shot that sweeps <c>ActivationRange</c> ahead over the active frames; hits grounded and airborne targets.</summary>
        Projectile = 1,
        /// <summary>A wave racing <c>ActivationRange</c> along the ground; grounded targets only, so a jump avoids it (Lincoln).</summary>
        GroundWave = 2
    }

    /// <summary>
    /// A02 / D15 (Package 13 W6) — the cross-mode Ultimate rulebook. Plain
    /// constants and pure helpers, no Godot types, so Story and
    /// <c>scripts/FighterSim/</c> read one copy (the <see cref="BasicComboRules"/>
    /// pattern). Everything per-character lives on the ability's
    /// <see cref="AbilityData"/>; only the values the design states once for
    /// every Ultimate live here.
    ///
    /// <para><b>Total damage (D15).</b> An Ultimate deals
    /// <c>HitCount × BaseDamage</c> regular hits followed by one
    /// <c>FinaleDamage</c> hit when that field is non-zero. A damage-over-time
    /// status rides the finale only (so Wrath of the Nile's Venom 2.0 × 3 s
    /// lands exactly once and counts its 12 toward the 78 total); a control
    /// hold rides the regular hits (<see cref="HitCarriesStatus"/>).</para>
    /// </summary>
    public static class UltimateActivationRules {

        /// <summary>A02 provisional wind-up, pending F09 validation.</summary>
        public const int DefaultWindupFrames = 20;
        /// <summary>A02 provisional active frames.</summary>
        public const int DefaultActiveFrames = 10;
        /// <summary>A02 provisional whiff recovery.</summary>
        public const int DefaultWhiffRecoveryFrames = 45;

        /// <summary>
        /// Hitstun of the finale hit in both modes. The finale is the launch
        /// that ends every cinematic; the retired per-type sim constants were
        /// 18–30 frames, and the generic melee Ultimate used 30.
        /// </summary>
        public const int FinaleHitstunFrames = 30;

        /// <summary><see cref="FinaleHitstunFrames"/> in seconds, for Story payloads.</summary>
        public const float FinaleHitstunSeconds = FinaleHitstunFrames / 60f;

        /// <summary>
        /// T03: each of Tesla's active coils detonated by Wardenclyffe
        /// Cataclysm deals this much (two coils take the 70 base to 80). Story's
        /// <c>TeslaCoilNode.Explode</c> and the sim's chain read this one value.
        /// </summary>
        public const int CoilDetonationDamage = 5;

        /// <summary>Regular hits plus the finale, if any.</summary>
        public static int CinematicHitCount(int hitCount, float finaleDamage) =>
            System.Math.Max(1, hitCount) + (finaleDamage > 0f ? 1 : 0);

        /// <summary>True when the 1-based hit <paramref name="hitIndex"/> is the finale.</summary>
        public static bool IsFinaleHit(int hitIndex, int hitCount, float finaleDamage) =>
            finaleDamage > 0f && hitIndex > System.Math.Max(1, hitCount);

        /// <summary>True when the 1-based hit is the last of the whole sequence (finale or last regular hit).</summary>
        public static bool IsLastHit(int hitIndex, int hitCount, float finaleDamage) =>
            hitIndex >= CinematicHitCount(hitCount, finaleDamage);

        /// <summary>Damage of the 1-based hit <paramref name="hitIndex"/>.</summary>
        public static float HitDamage(int hitIndex, int hitCount, float baseDamage, float finaleDamage) =>
            IsFinaleHit(hitIndex, hitCount, finaleDamage) ? finaleDamage : baseDamage;

        /// <summary>
        /// True when the Ultimate's authored status rides its finale alone: a
        /// finale exists and the status is a Damage-slot DoT (Venom, Radiant
        /// Burn), which has to land exactly once to count toward the total.
        /// </summary>
        public static bool StatusRidesFinale(FTT.Core.StatusType status, float finaleDamage) =>
            StatusRidesFinale(status, finaleDamage > 0f);

        /// <summary>
        /// Float-free form for the deterministic sim (Package 13 Phase C: the
        /// float overload tripped Klotho's <c>KLOTHO_DET002</c> analyzer from
        /// <c>scripts/FighterSim/</c>). Same rule; the caller states whether a
        /// finale is authored.
        /// </summary>
        public static bool StatusRidesFinale(FTT.Core.StatusType status, bool hasFinale) =>
            hasFinale && status != FTT.Core.StatusType.None
            && StatusRouting.SlotOf(status) == StatusSlot.Damage;

        /// <summary>
        /// Whether the 1-based hit carries the Ultimate's authored status. A
        /// Damage-slot status rides the finale only (C04: Wrath of the Nile's
        /// Venom 2.0 × 3 s = 12, once); a Control-slot hold (the Vitruvian
        /// Matrix's and Union Indestructible's Root) rides every regular hit
        /// and never the finale, so the launch is not held.
        /// </summary>
        public static bool HitCarriesStatus(int hitIndex, int hitCount, float finaleDamage, FTT.Core.StatusType status) =>
            StatusRidesFinale(status, finaleDamage)
                ? IsFinaleHit(hitIndex, hitCount, finaleDamage)
                : !IsFinaleHit(hitIndex, hitCount, finaleDamage);

        /// <summary>Impact total (excluding any damage-over-time status): <c>HitCount × BaseDamage + FinaleDamage</c>.</summary>
        public static float ImpactTotal(int hitCount, float baseDamage, float finaleDamage) =>
            System.Math.Max(1, hitCount) * baseDamage + (finaleDamage > 0f ? finaleDamage : 0f);

        /// <summary>
        /// Frames the Fighter cinematic holds its victim: through the finale
        /// (<c>N × interval</c> after contact) when one is authored, otherwise
        /// through the last regular hit (<c>(N − 1) × interval</c>), whose own
        /// knockback then releases them. Never less than one frame.
        /// </summary>
        public static int CinematicHoldFrames(int hitCount, int tickIntervalFrames, bool hasFinale) {
            int n = System.Math.Max(1, hitCount);
            int interval = System.Math.Max(1, tickIntervalFrames);
            int frames = hasFinale ? n * interval : (n - 1) * interval;
            return frames > 0 ? frames : 1;
        }
    }
}
