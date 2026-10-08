namespace FTT.Combat {

    /// <summary>
    /// The retro three-frame sprite contract (design-godot.md, "Frame Count
    /// Targets": each animation's three key poses are stretched across the
    /// authored frame windows — wind-up pose through startup, strike pose at
    /// active, follow-through through recovery). 2026-10-04 feel pass, F1.
    ///
    /// <para>Presentation only, and Godot-free so both the Story controller and
    /// the Fighter presentation driver can read it: the sprite frame is CHOSEN
    /// from the attack's phase instead of playing at a fixed sheet fps, so a
    /// hitstop freeze or a slow finisher holds the right pose rather than the
    /// sheet running out early or late. Nothing here ever feeds gameplay or the
    /// deterministic simulation — the timing numbers stay in
    /// <see cref="BasicComboRules"/> and <c>AbilityData</c>.</para>
    /// </summary>
    public static class AttackPoseRules {
        /// <summary>Sheet frame shown through startup.</summary>
        public const int WindUpPose = 0;
        /// <summary>Sheet frame shown from the first active frame through active.</summary>
        public const int StrikePose = 1;
        /// <summary>Sheet frame shown through recovery (and any freeze inside it).</summary>
        public const int FollowThroughPose = 2;

        /// <summary>The pose for a basic swing's frame-clock phase.</summary>
        public static int PoseFor(CombatFramePhase phase) => phase switch {
            CombatFramePhase.Startup => WindUpPose,
            CombatFramePhase.Active => StrikePose,
            _ => FollowThroughPose
        };

        /// <summary>The pose for an ability's executing phase (Story's <c>BaseSpecial</c>).</summary>
        public static int PoseFor(AbilityPhase phase) => phase switch {
            AbilityPhase.Startup => WindUpPose,
            AbilityPhase.Active => StrikePose,
            AbilityPhase.Recovery or AbilityPhase.Cleanup => FollowThroughPose,
            _ => WindUpPose
        };

        /// <summary>
        /// The pose for a presentation hold with no authored phases (the Fighter
        /// sim's Specials still resolve on press — <c>DEFER-SIM-SPECIAL-PHASES</c>):
        /// the hold is split into equal thirds. <paramref name="elapsedFrames"/>
        /// counts from 0 on the first presented frame.
        /// </summary>
        public static int PoseForHold(int elapsedFrames, int totalFrames) {
            if (totalFrames <= 0 || elapsedFrames <= 0) return WindUpPose;
            if (elapsedFrames >= totalFrames) return FollowThroughPose;
            int third = elapsedFrames * 3 / totalFrames;
            return third <= 0 ? WindUpPose : third == 1 ? StrikePose : FollowThroughPose;
        }

        /// <summary>Clamps a pose to a sheet's frame count (an authored sheet may hold fewer than three).</summary>
        public static int ClampToFrameCount(int pose, int frameCount) {
            if (frameCount <= 0) return 0;
            if (pose < 0) return 0;
            return pose >= frameCount ? frameCount - 1 : pose;
        }
    }
}
