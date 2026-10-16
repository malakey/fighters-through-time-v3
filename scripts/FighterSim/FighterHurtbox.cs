using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// Combat and movement update 2026-10-16, decision 8.4 (plan §2.8.3): the
    /// ONE named fighter hurtbox in the deterministic sim. Every hit test against
    /// a fighter — basic swings, the activation strike, kit hits, projectiles,
    /// constructs and zones — reads <see cref="HalfExtents"/>. It replaced ten
    /// identical private copies of the same literal with no number change (the
    /// expression is raw-identical), so no hash moved.
    ///
    /// <para>The target size is still open (ledger <c>VERIFY-SIM-HURTBOX-WIDTH</c>:
    /// the design asks for 0.8 × 1.6 units, the sim tests 1.0 × 2.0). A resize
    /// changes this one value; <see cref="FighterBodyOverlap"/> is a separate
    /// name so the change can leave orb pickup and hazard reach alone.</para>
    /// </summary>
    public static class FighterHurtbox {
        /// <summary>The fighter hurtbox half extents in world units, about the fighter's position.</summary>
        public static readonly FPVector2 HalfExtents = new(FP64.FromDouble(0.5), FP64.One);
    }

    /// <summary>
    /// The fighter BODY box the orb pickup and the stage-hazard overlap test
    /// (plan §2.8.3). Today it is exactly the hurtbox, so it forwards to
    /// <see cref="FighterHurtbox.HalfExtents"/>; it is a different concept, and a
    /// later hurtbox resize can split it here instead of silently shrinking orb
    /// pickup and hazard reach.
    /// </summary>
    public static class FighterBodyOverlap {
        /// <summary>The body box half extents; forwards to (is initialized from) <see cref="FighterHurtbox.HalfExtents"/>.</summary>
        public static readonly FPVector2 HalfExtents = FighterHurtbox.HalfExtents;
    }
}
