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

        /// <summary>Victim hitstun per hit (0.15 / 0.2 / 0.3 s at 60 Hz).</summary>
        public static readonly int[] HitstunFrames = { 9, 12, 18 };

        /// <summary>
        /// Post-recovery chain window (and the mid-swing input buffer length).
        /// Design 3080: the next basic pressed inside this window continues the
        /// string; movement, jumping, dashing, rolling, or blocking inside the
        /// recovery or this window cancels the swing and resets the chain.
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
