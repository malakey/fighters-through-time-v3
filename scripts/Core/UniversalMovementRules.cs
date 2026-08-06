namespace FTT.Core {

    /// <summary>
    /// Canonical 60 Hz timings for universal movement. Story uses the floating-point
    /// multipliers at the Godot boundary; Fighter Mode applies the same values in
    /// fixed-point authoritative state.
    /// </summary>
    public static class UniversalMovementRules {
        public const int RunAccelerationFrames = 8;
        public const int DashDurationFrames = 12;
        public const int DashCommitFrames = 4;
        public const float DashSpeedMultiplier = 1.35f;

        public const int RollStartupFrames = 4;
        public const int RollTravelFrames = 12;
        public const int RollInvulnerabilityFrames = 8;
        public const int RollRecoveryFrames = 10;
        public const float RollSpeedMultiplier = 1.5f;

        public const int RollTotalFrames = RollStartupFrames + RollTravelFrames + RollRecoveryFrames;
    }

    public enum UniversalMovementPhase {
        None = 0,
        Dash = 1,
        RollStartup = 2,
        RollTravel = 3,
        RollRecovery = 4
    }
}
