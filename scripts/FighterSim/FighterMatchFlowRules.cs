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

        /// <summary>Spawn invulnerability, counted from the drop (3.0 s).</summary>
        public const int RespawnInvulnerabilityFrames = 180;

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
