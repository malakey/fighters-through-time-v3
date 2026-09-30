namespace FTT.Combat {

    /// <summary>
    /// Package 13 W5 (roster swap, D3) — Harriet Tubman's cross-mode kit
    /// rulebook: the frame counts and distances whose Fighter half cannot read an
    /// <c>AbilityData</c> field from inside the deterministic tick (the loadout
    /// carries damage, cooldown and distance, not per-phase frames — see
    /// <c>DEFER-SIM-SPECIAL-PHASES</c>). Plain constants, no Godot types, so Story
    /// and <c>scripts/FighterSim/</c> consume exactly one copy, the way they
    /// consume <see cref="KitMotionRules"/>.
    ///
    /// <para>Where a number is also authored on a <c>resources/Abilities/tubman/*.tres</c>
    /// (Story's phase timer reads the resource), <c>TubmanContentTests</c> pins
    /// the resource EQUAL to the constant, so the two can never drift apart.
    /// Numbers are ABILITY_DATA's (design 2026-09-29); the ones marked
    /// PROVISIONAL are this workstream's placeholders where the design is silent.</para>
    /// </summary>
    public static class TubmanKitRules {

        // --- Foresight (Special 2): a counter stance ---------------------------

        /// <summary>Startup before the counter window opens; the resource's <c>StartupFrames</c>.</summary>
        public const int ForesightStartupFrames = 4;
        /// <summary>The counter window; the resource's <c>ActiveFrames</c>. Story's Minor Foresight Window lengthens it; the sim never does.</summary>
        public const int ForesightWindowFrames = 20;
        /// <summary>Recovery after a window that caught nothing; the resource's <c>RecoveryFrames</c>.</summary>
        public const int ForesightWhiffRecoveryFrames = 24;
        /// <summary>The counter strike answers an attacker within this many units.</summary>
        public const float ForesightAnswerRangeUnits = 2.5f;
        /// <summary>
        /// PROVISIONAL (design gives no number): the sidestep after a trigger —
        /// frames Tubman is invulnerable and action-locked while the answer
        /// lands. Shorter than the whiff recovery, so a read is always rewarded.
        /// </summary>
        public const int ForesightSidestepFrames = 12;

        // --- North Star Leap (movement): a guided 8-way leap --------------------

        /// <summary>The leap's travel; equal to the resource's <c>ActiveFrames</c> and <c>MovementDuration × 60</c>.</summary>
        public const int NorthStarLeapTravelFrames = 18;
        /// <summary>Leap distance; <c>DistanceMoved = 240</c> px on the resource.</summary>
        public const float NorthStarLeapDistanceUnits = 4.0f;
        /// <summary>
        /// "Snaps to ledges from 0.5 units farther than normal": added to the
        /// ledge capture box's half-width while the leap is in flight and on the
        /// frame it ends.
        /// </summary>
        public const float NorthStarLeapLedgeSnapBonusUnits = 0.5f;
    }
}
