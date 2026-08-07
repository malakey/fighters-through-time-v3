namespace FTT.FighterSim {

    /// <summary>
    /// Mode-neutral snapshot the Hard/Normal/Easy CPU utility decision table reads.
    /// The deterministic Fighter path fills it straight from
    /// <see cref="FighterStateComponent"/>/<see cref="FighterRuntimeComponent"/>, so
    /// its fixed-point values stay bit-exact. A Story-side adapter (the Level 13
    /// Mirror Paradox) fills the same fields from Godot state, which lets both modes
    /// share one decision engine without <c>scripts/FighterSim/</c> gaining any
    /// Godot or Story dependency.
    /// </summary>
    /// <remarks>
    /// Positions are raw <c>FP64</c> X values in world units — never pixels. Story
    /// callers must divide by their pixels-per-unit factor before converting.
    /// </remarks>
    public struct CpuDecisionObservation {
        public long SelfPositionXRaw;
        public long TargetPositionXRaw;
        public int Stocks;
        public int HitstunFrames;
        public int DazeFrames;
        public int IsGrounded;
        public long InfluenceRaw;
        public int SpecialOneCooldownFrames;
        public int SpecialTwoCooldownFrames;
        public int MovementCooldownFrames;
        /// <summary>Opponent buttons pressed this tick, as a <c>GameplayButtons</c> bit mask.</summary>
        public int TargetPressedButtons;
    }
}
