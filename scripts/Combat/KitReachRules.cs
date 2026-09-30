namespace FTT.Combat {

    /// <summary>
    /// Package 13 W7b — the cross-mode kit numbers for Joan, Mozart, Cleopatra
    /// and Lincoln that are not an <see cref="AbilityData"/> field (the character
    /// review J01–J03, M01–M04, C01–C03, LN02–LN03 and A08, 2026-09-29).
    ///
    /// <para><b>One value, two modes.</b> Story reads these in pixels through
    /// <see cref="KitMotionRules.StoryPixelsPerUnit"/>; the Fighter simulation
    /// converts them to FP64 once, at type initialization. Frame data, damage,
    /// travel speed and lifetime stay in the <c>.tres</c> (plan D9) — this class
    /// holds only the geometry the resource schema has no field for.</para>
    ///
    /// <para>Plain constants, no Godot types, so a pure-C# suite may read it.
    /// Every value is provisional under A07.</para>
    /// </summary>
    public static class KitReachRules {

        // --- Joan (J01, A08) ----------------------------------------------

        /// <summary>J01: Divine Piercing lunges this far forward across its thrust flurry (its active frames).</summary>
        public const double DivinePiercingLungeUnits = 3.0;

        /// <summary>
        /// A08: the Wing-Dive's steep forward descent, units per second. Held
        /// for up to the resource's <c>MovementDuration</c> (1 s). About 60°
        /// below horizontal.
        /// </summary>
        public const double WingDiveForwardUnitsPerSecond = 5.0;
        public const double WingDiveDescentUnitsPerSecond = 9.0;


        // --- Mozart (M02, M03, M04) ---------------------------------------

        /// <summary>M03: the Requiem Chord bursts into its pulses inside this radius.</summary>
        public const double RequiemBurstRadiusUnits = 1.2;

        /// <summary>M03: three pulses over 0.3 s — one every six frames.</summary>
        public const int RequiemPulseIntervalFrames = 6;

        /// <summary>M02: a hitting Requiem execution shortens Fortissimo Wave's remaining cooldown by this much, once.</summary>
        public const double RequiemFortissimoShaveSeconds = 2.0;
        public const int RequiemFortissimoShaveFrames = 120;

        /// <summary>M04: the staff platform is 2.0 units wide.</summary>
        public const double SonataPlatformWidthUnits = 2.0;

        /// <summary>M04: a staff landing refunds this share of the remaining cooldown, once per airtime.</summary>
        public const double SonataStaffRefundShare = 0.5;

        // --- Cleopatra (C01, C03) -----------------------------------------

        /// <summary>C01: the Serpent Nest is placed at her feet, 2.0 units wide.</summary>
        public const double SerpentNestWidthUnits = 2.0;

        /// <summary>C01: Sandstorm Vortex is thrown up to 5 units ahead, ground-snapped.</summary>
        public const double SandstormVortexMaxThrowUnits = 5.0;

        /// <summary>C01: the vortex radius.</summary>
        public const double SandstormVortexRadiusUnits = 1.8;

        /// <summary>C01: the vortex pulls toward its centre at 3 units per second.</summary>
        public const double SandstormVortexPullUnitsPerSecond = 3.0;

        // --- Lincoln (LN03) -----------------------------------------------

        /// <summary>LN03: Splitting Strike's overhead arc reaches 2.2 units in front of him.</summary>
        public const double SplittingStrikeReachUnits = 2.2;

        /// <summary>
        /// A <see cref="MovementType.Dash"/> charge (Rail Charge) stops dead on
        /// its first contact (LN02). The contact box sits this far ahead of the
        /// charger's centre, half as wide.
        /// </summary>
        public const double RailChargeContactReachUnits = 1.0;
    }
}
