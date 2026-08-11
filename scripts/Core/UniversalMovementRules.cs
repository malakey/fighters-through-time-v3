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
        /// <summary>
        /// Frames a grounded fighter takes to ramp from a standstill to full
        /// <c>MaxMoveSpeed</c>. Raised 8 → 14 by the 2026-08-10 gameplay-feel
        /// batch (§2.1) so run-ups have weight. Air acceleration is unchanged
        /// (4 frames in both modes).
        /// </summary>
        public const int RunAccelerationFrames = 14;

        /// <summary>
        /// Frames a grounded fighter takes to bleed full <c>MaxMoveSpeed</c> back
        /// to a standstill. Applies whenever the target speed is zero or reverses
        /// sign against the current velocity — the block stance, idle/skid/crouch
        /// settling, roll startup and recovery, and released-stick running. Air
        /// deceleration keeps its own constant (8 frames, Story only).
        /// </summary>
        public const int RunDecelerationFrames = 12;

        /// <summary>
        /// Fast-fall floor in world units per second (2026-08-10 feel batch §2.9).
        /// Holding Down while airborne, outside hitstun/daze, pins vertical
        /// velocity to at least this much downward and cancels the Warp float
        /// window. Stateless — derived from held input every tick, never
        /// snapshotted. Story multiplies by 60 for its pixel-space equivalent,
        /// which deliberately bypasses the 600 px/s terminal clamp.
        /// </summary>
        public const float FastFallSpeed = 16f;

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
