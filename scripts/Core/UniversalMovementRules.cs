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
        /// <c>MaxMoveSpeed</c>. Cut 14 → 6 by user direction 2026-10-08 (the run
        /// felt sluggish; ledger <c>USER-MOVEMENT-FEEL</c>), after the 2026-08-10
        /// gameplay-feel batch had raised it 8 → 14. Air acceleration is
        /// unchanged (4 frames in both modes).
        /// </summary>
        public const int RunAccelerationFrames = 6;

        /// <summary>
        /// Frames a grounded fighter takes to bleed full <c>MaxMoveSpeed</c> back
        /// to a standstill. Applies whenever the target speed is zero or reverses
        /// sign against the current velocity — the block stance, idle/skid/crouch
        /// settling, roll startup and recovery, and released-stick running. Cut
        /// 12 → 4 by user direction 2026-10-08. Air deceleration keeps its own
        /// constant (8 frames, Story only).
        /// </summary>
        public const int RunDecelerationFrames = 4;

        // === Jump arc (user direction 2026-10-08, ledger USER-MOVEMENT-FEEL; every
        // number provisional) ===
        // These shape CONTROLLED airborne movement in both modes: jumps, walk-offs,
        // drops, aerial swings and the air dodge. Hitstun, tumble, daze and throws
        // keep their launch gravity untouched (the Fighter sim's flat -30 with
        // LaunchTerminalFallSpeed; Story's own launch constants), so combat launch
        // arcs, DI windows and tech timing do not move. Each mode keeps its own base
        // gravity (Story weight-coupled, the sim flat: intended per mode by the
        // 2026-10-09 ruling, no longer part of H-7); these values are
        // multipliers on it and speeds in world units, shared by both.

        /// <summary>
        /// The apex hang band in world units per second. While a fighter that has
        /// jumped this airtime moves slower than this vertically (either side of
        /// the apex), gravity is scaled by <see cref="ApexHangGravityMultiplier"/>.
        /// Walk-offs, drop-throughs and ledge drops have not jumped and get no
        /// hang; a ledge (or Story swinging-rope) jump-off is a jump and gets it.
        /// </summary>
        public const float ApexHangSpeedBand = 2f;

        /// <summary>Gravity scale inside the apex hang band: half gravity, so the jump floats at its top.</summary>
        public const float ApexHangGravityMultiplier = 0.5f;

        /// <summary>
        /// Gravity scale through a Special cast that started airborne, during its
        /// startup and active frames (design :1097): the normal step at half
        /// strength, then the normal step in recovery. The single canonical
        /// value for both modes (combat and movement update 2026-10-16, plan
        /// §2.1.6): Story's <c>PlayerController.ProcessUsingSpecial</c> forwards
        /// to it (it was a literal 0.5 there), and the Fighter sim's cast step
        /// reads it through <c>FighterAirPhysics</c>.
        /// </summary>
        public const float AerialSpecialGravityMultiplier = 0.5f;

        /// <summary>
        /// Gravity scale while falling (moving down, outside the apex band) under
        /// normal control, so a descent gathers speed quickly. Story's was 1.8; the
        /// Fighter sim had none (1.0).
        /// </summary>
        public const float FallGravityMultiplier = 2.2f;

        /// <summary>
        /// The terminal fall speed under normal control, in world units per second
        /// (Story 720 px/s). Raised from Story's 10 u/s and lowered from the sim's
        /// 20 u/s by user direction 2026-10-08, so both modes now share it. A fall
        /// already faster than this (a fast-fall just released, or a launch that
        /// has just ended) eases down to it by one base-gravity step per tick
        /// instead of snapping.
        /// </summary>
        public const float TerminalFallSpeed = 12f;

        /// <summary>
        /// The launch terminal in world units per second: the Fighter sim clamps
        /// hitstun and daze falls here (M01's old 20, unchanged). Story's launch
        /// falls keep Story's own 600 px/s terminal.
        /// </summary>
        public const float LaunchTerminalFallSpeed = 20f;

        /// <summary>
        /// The fast-fall cap in world units per second (Story 900 px/s). Lowered
        /// 20 → 15 by user direction 2026-10-08. Holding Down while airborne under
        /// normal control no longer snaps the velocity: it scales gravity by
        /// <see cref="FastFallGravityMultiplier"/> (cutting a rise short quickly,
        /// not instantly) until the descent reaches this cap. It cancels the Warp
        /// float window and the apex hang. Stateless, derived from held input
        /// every tick, never snapshotted.
        /// </summary>
        public const float FastFallSpeed = 15f;

        /// <summary>Gravity scale while fast-falling, toward <see cref="FastFallSpeed"/>.</summary>
        public const float FastFallGravityMultiplier = 3f;

        // === CM-MOVE (combat and movement update 2026-10-16, decisions 5.1, 6.1,
        // 6.2 and 6.3; plan §2.5.1, §2.6.1–§2.6.3; every number provisional
        // unless the user stated it) ===

        /// <summary>
        /// Decision 5.1 (user number): the short hop — gravity scale while a fighter
        /// rises from its own Jump press with Jump released. One value for both
        /// modes: Story's <c>StoryAirPhysics.ShortHopGravityMultiplier</c> forwards
        /// to it (it was Story-only until 2026-10-16) and the Fighter sim reads it
        /// through <c>FighterAirPhysics</c>. The Q5 ruling ("own jump only, both
        /// modes") limits it to a rise that began with this airtime's own Jump
        /// press — a ground jump, an air jump or a ledge climb jump (Story's ledge
        /// and rope jump-offs) — so a post-hitstun rise, a kit lift and an airborne
        /// non-launching hit are never cut. Decision 6.1: the apex hang applies
        /// only while Jump is held, so a released hop drops through its peak.
        /// </summary>
        public const float ShortHopGravityMultiplier = 2.5f;

        /// <summary>
        /// Decision 6.3 (design value, 0.3 s): the double-tap Down window that
        /// drops through a one-way platform, in both modes (Down+Jump drops too).
        /// Story's <c>StoryCombatRules.DownDoubleTapFrames</c> forwards to it; each
        /// mode keeps its own drop-through duration.
        /// </summary>
        public const int DropThroughDoubleTapFrames = 18;

        /// <summary>
        /// Decision 6.2 (Provisional, <c>PROVISIONAL-CORNER-FORGIVENESS-RULES</c>):
        /// the Story head-clip nudge — a rising hero whose head meets a ceiling
        /// corner slides sideways by up to this much (0.2 u = 12 px) and keeps
        /// rising. The Fighter sim has no partial ceiling to nudge around (a
        /// geometry guard pins that premise), so the sim half is a no-op.
        /// </summary>
        public const float HeadClipNudgeUnits = 0.2f;

        /// <summary>
        /// Decision 6.2 (Provisional): the Story lip pop — a hero rising into a
        /// wall whose top is at most this far above the feet (10 px) is lifted over
        /// the lip. Stateless, current frame only.
        /// </summary>
        public const float LipPopUnits = 10f / 60f;

        /// <summary>
        /// Decision 6.2 with the Q6 ruling (Provisional, <c>PROVISIONAL-SIM-LIP-POP</c>):
        /// the Fighter sim's one-way lip pop — a normal-control airborne fighter at
        /// its apex or falling, with its feet at most this far below a one-way
        /// platform's top and over the platform, is placed on the top and lands.
        /// It resolves before the ledge capture, so a near-edge capture inside the
        /// band becomes a landing. Converted once to FP64 by <c>FighterAirPhysics</c>.
        /// </summary>
        public const float SimOneWayLipPopUnits = 0.2f;

        public const int RollStartupFrames = 4;
        public const int RollTravelFrames = 12;
        public const int RollInvulnerabilityFrames = 8;
        public const int RollRecoveryFrames = 10;

        /// <summary>
        /// Roll travel speed (and the knockdown roll get-up's) as a multiple of the
        /// fighter's run speed, in both modes. Raised 1.5 → 1.875 (= 1.5 / 0.8) by
        /// the 2026-10-12 gameplay update (GU-C, provisional) in the same change
        /// that cut every <c>MaxMoveSpeed</c> by 20% (user request "reduce the
        /// movement speed by about 20%"), so the roll and the get-up keep the
        /// distance they had before the cut: Lincoln keeps his Story roll-through
        /// of a standard mob and his Fighter cross-up.
        /// </summary>
        public const float RollSpeedMultiplier = 1.875f;

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
        AirDodgeRecovery = 7,
        /// <summary>
        /// CM-MOVE (decision 5.2, 2026-10-16): the Fighter neutral ledge climb —
        /// a fresh toward-stage or Up tilt from the hang. Values 8–15 were free
        /// (plan §1.5); the anchor is packed in the slot's direction field.
        /// </summary>
        LedgeClimb = 8,
        /// <summary>CM-MOVE: the ledge roll's intangible climb (then the ordinary grounded roll).</summary>
        LedgeRoll = 9,
        /// <summary>CM-MOVE: the ledge attack's intangible climb (then the ledge strike).</summary>
        LedgeAttackClimb = 10
    }
}
