using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>Canonical <see cref="FighterMatchComponent.MatchState"/> values.</summary>
    public static class FighterMatchStates {
        /// <summary>Pre-match countdown. Gameplay input is ignored; the match clock does not run.</summary>
        public const int Countdown = 0;
        /// <summary>Live match.</summary>
        public const int InProgress = 1;
        /// <summary>Decided. No further match state changes.</summary>
        public const int Complete = 2;
    }

    /// <summary>
    /// Deterministic match-flow timings shared by the simulation and its
    /// presentation driver. These are simulation constants, not tuning
    /// resources: they are identical for every stage, character, and mode, and
    /// they participate in every snapshot and state hash.
    /// design-godot.md ~1565-1571 (respawn platform) and Section 11 (match flow).
    /// </summary>
    public static class FighterMatchFlowRules {
        /// <summary>3-2-1 pre-match countdown at 60 Hz (three seconds).</summary>
        public const int CountdownFrames = 180;

        /// <summary>Frames per displayed countdown digit.</summary>
        public const int CountdownFramesPerDigit = 60;

        /// <summary>"GO!" banner window that runs on the first live frames.</summary>
        public const int GoBannerFrames = 30;

        /// <summary>Chronal Respawn Platform dissolve window (5.0 s).</summary>
        public const int RespawnPlatformFrames = 300;

        /// <summary>
        /// Input is ignored for this many frames after materialising so the input
        /// that scored the knockout (or a buffered press) cannot instantly drop
        /// the respawning fighter off the platform.
        /// </summary>
        public const int RespawnPlatformGraceFrames = 30;

        // === CM-HIT (combat and movement update 2026-10-16, decision 4.2;
        // Fighter only, the Q8 ruling): respawn protection is 120 frames
        // shrinking toward 60 the longer the fighter waits on the platform, and
        // it ends early on the fighter's first offensive action (a swing, a
        // Special, the Ultimate or a grab; FighterActionRules.OnActionAccepted).
        // It replaces the flat 180-frame (3.0 s) spawn invulnerability. The
        // curve's shape is provisional (PROVISIONAL-RESPAWN-PROTECTION-CURVE).

        /// <summary>Respawn protection granted by a drop at the first legal tick (2.0 s).</summary>
        public const int RespawnInvulnerabilityMaxFrames = 120;

        /// <summary>The floor the protection shrinks to: the 300-frame auto-drop gets exactly this (1.0 s).</summary>
        public const int RespawnInvulnerabilityMinFrames = 60;

        /// <summary>
        /// The frames over which the protection shrinks, counted from the end of
        /// the input grace: the first legal drop (waited 31) gets the maximum and
        /// the auto-drop (waited 300) the minimum.
        /// </summary>
        public const int RespawnInvulnerabilityShrinkFrames = RespawnPlatformFrames - RespawnPlatformGraceFrames;

        /// <summary>
        /// The respawn protection granted at the platform drop, from the frames
        /// the fighter waited on the platform:
        /// <c>max(60, 120 − 60 × clamp(waited − 30, 0, 270) / 270)</c>, integer
        /// arithmetic. <paramref name="waited"/> is
        /// <c>RespawnPlatformFrames − RespawnFramesRemaining</c> read after the
        /// drop tick's decrement: 31 at the first legal input drop (120), 300 at
        /// the auto-drop (exactly 60, by the explicit floor).
        /// </summary>
        public static int RespawnInvulnerabilityFor(int waited) {
            int shrink = waited - RespawnPlatformGraceFrames;
            if (shrink < 0) shrink = 0;
            if (shrink > RespawnInvulnerabilityShrinkFrames) shrink = RespawnInvulnerabilityShrinkFrames;
            int frames = RespawnInvulnerabilityMaxFrames
                - (RespawnInvulnerabilityMaxFrames - RespawnInvulnerabilityMinFrames) * shrink
                    / RespawnInvulnerabilityShrinkFrames;
            return frames > RespawnInvulnerabilityMinFrames ? frames : RespawnInvulnerabilityMinFrames;
        }

        /// <summary>
        /// The record of WHY the fighter is invulnerable (plan §1.5: component
        /// 323 <c>ActionFlags</c> bit 0): true while the respawn grant still
        /// runs. The shared <c>InvulnerabilityFrames</c> counter carries every
        /// other grant too (roll, air dodge, tech, knockdown, throw, Foresight),
        /// so the driver's "Respawning" label and spawn glow read this instead.
        /// </summary>
        public static bool IsRespawnProtected(
            in FighterStateComponent fighter, in FighterActionStateComponent action) =>
            (action.ActionFlags & FighterActionStateComponent.ActionFlagRespawnProtection) != 0
            && fighter.InvulnerabilityFrames > 0;

        /// <summary>
        /// The platform drop (CM-HIT, 4.2): grants the shrinking protection and
        /// records it on 323 bit 0. Called by the movement system's respawn
        /// branch after the platform hold zeroed 323 earlier the same tick.
        /// </summary>
        public static void GrantRespawnProtection(
            ref FighterStateComponent fighter, ref FighterActionStateComponent action, int waited) {
            fighter.InvulnerabilityFrames = RespawnInvulnerabilityFor(waited);
            action.ActionFlags |= FighterActionStateComponent.ActionFlagRespawnProtection;
        }

        /// <summary>
        /// Drops the 323 record once the shared counter has run out (or another
        /// rule zeroed it), so a later, unrelated grant is never read as respawn
        /// protection. Stateless; run once per unfrozen movement tick.
        /// </summary>
        public static void ExpireRespawnProtection(
            in FighterStateComponent fighter, ref FighterActionStateComponent action) {
            if (fighter.InvulnerabilityFrames <= 0) {
                action.ActionFlags &= ~FighterActionStateComponent.ActionFlagRespawnProtection;
            }
        }

        /// <summary>
        /// 4.2's early end: the fighter's first offensive action (a swing, a
        /// Special, the Ultimate or a grab) while the respawn protection runs
        /// zeroes the shared counter and clears the record. Safe because every
        /// other grant on the counter is action-locked while it runs, so none can
        /// be live when an offensive action is accepted (plan §2.4.2); a ledge
        /// get-up's own climb grant is max-assigned AFTER its chokepoint call.
        /// </summary>
        public static void EndRespawnProtectionOnOffence(
            ref FighterStateComponent fighter, ref FighterActionStateComponent action) {
            if ((action.ActionFlags & FighterActionStateComponent.ActionFlagRespawnProtection) == 0) return;
            action.ActionFlags &= ~FighterActionStateComponent.ActionFlagRespawnProtection;
            fighter.InvulnerabilityFrames = 0;
        }

        /// <summary>Stage-centre platform offset: (0.0, +3.0) world units.</summary>
        public static readonly FPVector2 RespawnPlatformPosition =
            new(FP64.Zero, FP64.FromInt(3));

        /// <summary>Countdown digit currently displayed; 0 means the GO window.</summary>
        public static int CountdownDigit(int countdownFramesRemaining) {
            if (countdownFramesRemaining <= 0) return 0;
            return (countdownFramesRemaining - 1) / CountdownFramesPerDigit + 1;
        }

        /// <summary>True while the Chronal Respawn Platform holds this fighter frozen.</summary>
        public static bool IsOnRespawnPlatform(in FighterStateComponent fighter) =>
            fighter.RespawnFramesRemaining > 0;
    }
}
