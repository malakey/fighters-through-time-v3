namespace FTT.FighterSim {

    /// <summary>
    /// CM-HIT (combat and movement update 2026-10-16, Phase B): the movement
    /// system's per-tick hit-reaction and protection bookkeeping. One call,
    /// <see cref="ApplyHitReaction"/>, at the top of every unfrozen movement
    /// tick — after the hitstop gate, before the respawn-platform hold of
    /// component 323 and before any branch runs <c>TickCounters</c>. Every rule
    /// here is stateless: it reads the fighter's components and writes only
    /// values the plan reserves for CM-HIT (310 <c>Tumble</c> value 2, 320
    /// <c>AirDodgeUsed</c>, 323 <c>ActionFlags</c> bit 0) or the shared
    /// invulnerability counter.
    /// </summary>
    public sealed partial class FighterMovementSystem {

        /// <summary>
        /// <list type="bullet">
        /// <item><b>3.2, tumble outlives hitstun</b> — a launched fighter whose
        /// hitstun ran out in the air keeps tumbling (value 2) until it acts or
        /// lands; a non-launching hit on such a fighter ends the tumble
        /// (<see cref="FighterVerbRules.UpdateTumble"/>).</item>
        /// <item><b>5.3, the air dodge refreshes when hit</b> — any applied
        /// hitstun (a throw's and a tick's included) refreshes it; blocked,
        /// absorbed, armoured and Defy-zeroed hits apply none, so they do not.</item>
        /// <item><b>4.2, the respawn-protection record</b> — 323 bit 0 is dropped
        /// once the shared counter has run out.</item>
        /// <item><b>The throw animation's grabber grant ends with the throw</b>
        /// (the fix-round A side note, CM_FIXA_SIM.md §5) — the grab-busy
        /// branch skips the normal decrement, so the 12-frame grant used to sit
        /// frozen through the animation and outlive the throw by about 12
        /// ticks. It now counts down through the animation (the landing tech's
        /// pattern): invulnerable to every pass from the grant tick until the
        /// throw resolves, and hittable on the tick the throw does.</item>
        /// </list>
        /// </summary>
        private static void ApplyHitReaction(
            ref FighterStateComponent fighter,
            ref FighterVerbComponent verb,
            ref FighterKnockdownComponent knockdown,
            ref FighterActionStateComponent action) {
            FighterVerbRules.UpdateTumble(in fighter, ref verb);
            if (fighter.HitstunFrames > 0) FighterAirDodgeRules.Refresh(ref knockdown);
            FighterMatchFlowRules.ExpireRespawnProtection(in fighter, ref action);
            if (verb.GrabPhase == FighterGrabRules.PhaseThrowAnimation && fighter.InvulnerabilityFrames > 0) {
                fighter.InvulnerabilityFrames--;
            }
        }
    }
}
