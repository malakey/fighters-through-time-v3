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
        /// M01 (D1(a), Package 12 W3): the Fighter terminal fall speed in world
        /// units per second. The simulation clamps every downward velocity to it
        /// — a stateless clamp, no snapshot field. Story's ORDINARY terminal is
        /// still its own 600 px/s (10 u/s) constant: the full H-7 gravity
        /// unification stays deferred, so only the fast-fall path reaches 20 in
        /// Story.
        /// </summary>
        public const float TerminalFallSpeed = 20f;

        /// <summary>
        /// Fast-fall speed in world units per second (2026-08-10 feel batch §2.9;
        /// M01 2026-09-26 raised it 16 → 20, because a normal fall already
        /// outpaced 16). Holding Down while airborne, outside hitstun/daze,
        /// SNAPS vertical velocity to exactly this much downward — equal to the
        /// terminal clamp, so it never exceeds terminal — and cancels the Warp
        /// float window. Stateless — derived from held input every tick, never
        /// snapshotted. Story multiplies by 60 for its pixel-space equivalent
        /// (1200 px/s), which deliberately sits above its ordinary 600 px/s
        /// terminal until H-7 lands.
        /// </summary>
        public const float FastFallSpeed = TerminalFallSpeed;

        public const int RollStartupFrames = 4;
        public const int RollTravelFrames = 12;
        public const int RollInvulnerabilityFrames = 8;
        public const int RollRecoveryFrames = 10;
        public const float RollSpeedMultiplier = 1.5f;

        public const int RollTotalFrames = RollStartupFrames + RollTravelFrames + RollRecoveryFrames;

        // === A03 air dodge (Package 13 W1, 2026-09-29; provisional under A07) ===
        // Roll pressed while airborne: an AirDodge sub-phase of Rolling in both
        // modes. No speed is added — gravity, momentum and normal air control
        // continue — and a held direction adds only a small positional shift
        // across the invulnerable frames. Once per airtime, refreshed by landing
        // or a ledge grab; landing does not cancel its recovery.

        public const int AirDodgeStartupFrames = 4;
        public const int AirDodgeInvulnerableFrames = 8;
        public const int AirDodgeRecoveryFrames = 10;
        public const int AirDodgeTotalFrames =
            AirDodgeStartupFrames + AirDodgeInvulnerableFrames + AirDodgeRecoveryFrames;

        /// <summary>
        /// The whole shift a held direction adds across the eight invulnerable
        /// frames, in world units (a diagonal is normalized to the same length).
        /// Neutral dodges in place.
        /// </summary>
        public const float AirDodgeShiftUnits = 1.0f;
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
        RollRecovery = 4,
        /// <summary>A03 (Package 13 W1): air dodge startup (4 f). Appended; kit phases own 16–20.</summary>
        AirDodgeStartup = 5,
        /// <summary>A03: the eight invulnerable, pushbox-free frames carrying the held shift.</summary>
        AirDodgeInvulnerable = 6,
        /// <summary>A03: recovery (10 f); a landing does not cancel it.</summary>
        AirDodgeRecovery = 7
    }
}
