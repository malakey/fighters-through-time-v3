namespace FTT.Combat {

    /// <summary>
    /// Canonical 60 Hz timings and chain rules for the universal three-hit basic
    /// combo, shared by both modes. Story consumes these through
    /// <c>PlayerController</c>'s frame timelines (and the authored combat
    /// animation library mirrors the same numbers); Fighter Mode applies them in
    /// fixed-point authoritative state (<c>scripts/FighterSim/</c>). One source —
    /// the modes must not drift (design pillar: the same move must behave the
    /// same in Story and Fighter). This file must stay free of Godot types so the
    /// deterministic simulation can reference it.
    /// </summary>
    public static class BasicComboRules {
        public const int ComboHits = 3;

        // Grounded string: startup / active / recovery per hit (totals 27/30/45).
        public static readonly int[] GroundStartupFrames = { 6, 7, 15 };
        public static readonly int[] GroundActiveFrames = { 6, 7, 9 };
        public static readonly int[] GroundRecoveryFrames = { 15, 16, 21 };

        // Aerial string (totals 25/28/40).
        public static readonly int[] AerialStartupFrames = { 5, 6, 12 };
        public static readonly int[] AerialActiveFrames = { 7, 8, 10 };
        public static readonly int[] AerialRecoveryFrames = { 13, 14, 18 };

        /// <summary>
        /// Victim hitstun per hit (0.5 / 0.667 / 0.4 s at 60 Hz). Hits one and
        /// two cover the gap to the next chain hit (roughly 28 frames from hit
        /// one's connect to hit two's, 38 from hit two's to the finisher's, on
        /// the buffered grounded string); the finisher's shorter stun hands off
        /// to its launch knockback, which creates the separation that ends the
        /// exchange. A *grounded* victim is no longer held helpless through the
        /// string: holding Block exits hitstun into the block stance in both
        /// modes (gameplay-feel plan §2.4). Daze and airborne hitstun stay
        /// uncancelable.
        /// </summary>
        public static readonly int[] HitstunFrames = { 30, 40, 24 };

        /// <summary>
        /// Knockback multipliers per hit, applied to the character's base
        /// basic-attack knockback in both modes. The finisher is deliberately
        /// far above the first two: at 4.5x it launches even the lightest
        /// knockback kit (Cleopatra, base 2.0 into weight 0.7) past the 2-unit
        /// melee range with margin by the time the victim's hitstun ends, so
        /// the string always ends in separation rather than an unbroken loop
        /// (gameplay-feel plan §2.5; pinned by
        /// <c>FighterFinisherSeparationTests</c> across all nine kits).
        /// The effective impulse is additionally scaled by the victim's missing
        /// HP — see <see cref="LowHealthKnockbackScale"/>.
        /// </summary>
        public static readonly float[] KnockbackMultipliers = { 1f, 1.2f, 4.5f };

        /// <summary>
        /// Low-health knockback scaling (gameplay-feel plan §2.5), shared by
        /// every damage source in both modes: the impulse is multiplied by
        /// <c>1 + missingHPFraction</c> of the victim measured *after* the
        /// hit's damage is applied — a linear 1.0x at full HP to 2.0x at 0 HP.
        /// The Fighter sim applies the same ratio in fixed point inside
        /// <c>FighterDamageRules.ApplyFighterHit</c>, which is the single
        /// chokepoint for basics, specials, ultimates, projectiles, zones,
        /// constructs and hazards; Story applies it through
        /// <c>DamageCalculator.CalculateKnockback</c>'s victim-HP overload.
        /// </summary>
        public static float LowHealthKnockbackScale(float currentHP, float maxHP) {
            if (maxHP <= 0f) return 1f;
            float clamped = currentHP < 0f ? 0f : currentHP > maxHP ? maxHP : currentHP;
            return (2f * maxHP - clamped) / maxHP;
        }

        /// <summary>
        /// Story-enemy floor on post-<c>StunResistance</c> hitstun for the
        /// basic string's hits (the melee combo's <c>combo_N</c> hitboxes;
        /// <c>EnemyController.ApplyStun</c>): however high an enemy's authored
        /// resistance, a landed string hit keeps it stunned for at least this
        /// long, so no roster enemy can act between chain hits. Other
        /// Basic-class sources (constructs) keep their authored stuns, and the
        /// Fighter sim has no stun resistance and never consumes this.
        /// </summary>
        public const int EnemyBasicStunFloorFrames = 24;

        /// <summary>
        /// Post-recovery chain window (and the mid-swing input buffer length).
        /// Design 3080: the next basic pressed inside this window continues the
        /// string; jumping, rolling, or blocking inside the recovery
        /// or this window cancels the swing and resets the chain. Held
        /// horizontal movement steers the attacker but never cancels — letting
        /// it cancel allowed a moving attacker to restart hit one faster than
        /// the authored string pace.
        /// </summary>
        public const int ChainHoldFrames = 24;

        /// <summary>
        /// Frames between block-charge regenerations while not blocking. Story's
        /// <c>BlockSystem</c> divides this by 60; the Fighter sim counts it
        /// directly. (The design asks for 2.0 s; both modes currently ship the
        /// long-standing Story value — retune in one place when that is settled.)
        /// </summary>
        public const int BlockChargeRegenFrames = 180;
    }
}
