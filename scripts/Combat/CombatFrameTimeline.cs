namespace FTT.Combat {

    public enum CombatFramePhase {
        Startup,
        Active,
        Recovery,
        Complete
    }

    /// <summary>
    /// Integer 60 Hz attack window used by gameplay and deterministic Fighter
    /// adapters. Frame zero is the first startup frame.
    /// </summary>
    public readonly struct CombatFrameTimeline {
        public readonly int StartupFrames;
        public readonly int ActiveFrames;
        public readonly int RecoveryFrames;

        public CombatFrameTimeline(int startupFrames, int activeFrames, int recoveryFrames) {
            StartupFrames = System.Math.Max(0, startupFrames);
            ActiveFrames = System.Math.Max(1, activeFrames);
            RecoveryFrames = System.Math.Max(0, recoveryFrames);
        }

        public int TotalFrames => StartupFrames + ActiveFrames + RecoveryFrames;

        public CombatFramePhase GetPhase(int elapsedFrame) {
            if (elapsedFrame < StartupFrames) return CombatFramePhase.Startup;
            if (elapsedFrame < StartupFrames + ActiveFrames) return CombatFramePhase.Active;
            if (elapsedFrame < TotalFrames) return CombatFramePhase.Recovery;
            return CombatFramePhase.Complete;
        }

        public bool IsActive(int elapsedFrame) => GetPhase(elapsedFrame) == CombatFramePhase.Active;
    }
}
