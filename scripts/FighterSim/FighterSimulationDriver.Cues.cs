using FTT.Core;

namespace FTT.FighterSim {

    // GR-AUDIO (gameplay review ruling 13, 2026-11-03): the Fighter half of the small combat
    // sound set -- hit, block, perfect block and swing whiff (grab catch, KO stinger, Defy burst
    // and the Ultimate wind-up are raised by their existing sites and merely became audible).
    // Presentation only: edge detection over copied component state, a Play call on the cue
    // presenter, nothing written into the simulation and nothing read from the game back out.
    //
    // Every edge here is a FORWARD edge of the value it watches (HP falling, a block charge
    // falling, the perfect-block count rising by a short step through IsForwardCounterStep, an
    // attack phase passing Active -> Recovery), so a rollback correction that moves the value
    // back never plays a cue. The presenter's per-row throttle (CoreMechanicCueEntry
    // .MinIntervalSeconds) bounds what a corrected, re-added event can replay. Limit: HP, charges
    // and phases carry no wrapping counter, so a correction that removes a presented hit and later
    // adds it at another tick plays its thump twice, as the damage flash does (LAN is de-scoped).
    public partial class FighterSimulationDriver {

        private readonly int[] _cuePreviousAttackPhase = { 0, 0 };

        /// <summary>
        /// A swing's active window just ran out with nothing reached: the previous presented
        /// phase was Active, this one is the recovery (or the chain hold), and the swing never
        /// set <c>FlagHitResolved</c> -- the flag every resolved contact sets, a blocked or
        /// absorbed one included. A swing cancelled out of its active frames (hitstun, a
        /// Special, a landing) reads phase None and is no whiff. Pure.
        /// </summary>
        internal static bool SwingWhiffStarts(int previousPhase, int phase, int attackFlags) =>
            previousPhase == FighterBasicAttackRules.PhaseActive
            && (phase == FighterBasicAttackRules.PhaseRecovery || phase == FighterBasicAttackRules.PhaseChainHold)
            && (attackFlags & FighterBasicAttackRules.FlagHitResolved) == 0;

        private void SyncSwingWhiffCue(int playerID, in FighterRuntimeComponent runtime) {
            int previous = _cuePreviousAttackPhase[playerID];
            _cuePreviousAttackPhase[playerID] = runtime.AttackPhase;
            if (SwingWhiffStarts(previous, runtime.AttackPhase, runtime.AttackFlags)) {
                Cues?.Play(CoreMechanicCueIDs.SwingWhiff);
            }
        }

        /// <summary>The victim lost HP to something other than an active stage hazard.</summary>
        private static void PlayHitCue() => Cues?.Play(CoreMechanicCueIDs.HitBasic);

        /// <summary>An ordinary block or a guard break plays the ping; a perfect block the bright one.</summary>
        private static void PlayBlockCue(FTT.Combat.BlockSparkKind kind) =>
            Cues?.Play(kind == FTT.Combat.BlockSparkKind.Perfect
                ? CoreMechanicCueIDs.PerfectBlock
                : CoreMechanicCueIDs.Block);
    }
}
