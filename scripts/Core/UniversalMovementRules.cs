namespace FTT.Core {

    /// <summary>
    /// Canonical 60 Hz timings for universal movement. Story uses the floating-point
    /// multipliers at the Godot boundary; Fighter Mode applies the same values in
    /// fixed-point authoritative state.
    ///
    /// <para>The universal dash was removed on 2026-08-09 by user directive
    /// (superseding design-godot.md's universal dash): its constants are gone,
    /// while <see cref="UniversalMovementPhase.Dash"/> stays reserved because
    /// snapshots store the phase as an int.</para>
    /// </summary>
    public static class UniversalMovementRules {
        public const int RunAccelerationFrames = 8;

        public const int RollStartupFrames = 4;
        public const int RollTravelFrames = 12;
        public const int RollInvulnerabilityFrames = 8;
        public const int RollRecoveryFrames = 10;
        public const float RollSpeedMultiplier = 1.5f;

        public const int RollTotalFrames = RollStartupFrames + RollTravelFrames + RollRecoveryFrames;
    }

    public enum UniversalMovementPhase {
        None = 0,
        /// <summary>
        /// Reserved. The universal dash mechanic was removed (2026-08-09 user
        /// directive); snapshots serialize this enum as an int, so the value must
        /// never be reused for a new phase.
        /// </summary>
        Dash = 1,
        RollStartup = 2,
        RollTravel = 3,
        RollRecovery = 4
    }
}
