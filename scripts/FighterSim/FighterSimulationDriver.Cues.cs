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

        // GR-INTEG: a per-cue, per-player WATERMARK of the highest sim tick whose edge already played. An edge
        // at tick T plays only if T is above the watermark, so it guards DECREASING ticks only. It does NOT
        // dedupe an event a real rollback removes and reintroduces: CorrectRemoteInput() replays to the current
        // tick before SyncPresentation(), so the observations (HP 90, 100, 90) arrive at increasing ticks and
        // the replayed hit sounds again, bounded by the row's throttle window. Rollback exists only on LAN and
        // online, which this build does not enable; stable sim event identities are the fix (Package 7,
        // VERIFY-PRESENTATION-DEDUPE-WEAKNESS). A genuine event at or below the watermark is suppressed once.
        // Presentation only.
        internal const int CueKindHit = 0;
        internal const int CueKindBlock = 1;
        internal const int CueKindPerfectBlock = 2;
        internal const int CueKindSwingWhiff = 3;
        private const int CueKindCount = 4;

        private readonly int[] _cueWatermark = NewCueWatermarks();

        /// <summary>Test seam: replaces the simulation tick the cue edges are stamped with.</summary>
        internal int? CueTickOverrideForTesting { get; set; }

        private static int[] NewCueWatermarks() {
            var marks = new int[CueKindCount * 2];
            System.Array.Fill(marks, int.MinValue);
            return marks;
        }

        private void ResetCueWatermarks() => System.Array.Fill(_cueWatermark, int.MinValue);

        /// <summary>True (and the watermark advances) when this cue's edge at the current tick has not played yet.</summary>
        internal bool ClaimCue(int cueKind, int playerID) {
            int tick = CueTickOverrideForTesting ?? CurrentTick;
            int slot = cueKind * 2 + playerID;
            if (tick <= _cueWatermark[slot]) return false;
            _cueWatermark[slot] = tick;
            return true;
        }

        private void SyncSwingWhiffCue(int playerID, in FighterRuntimeComponent runtime) {
            int previous = _cuePreviousAttackPhase[playerID];
            _cuePreviousAttackPhase[playerID] = runtime.AttackPhase;
            if (SwingWhiffStarts(previous, runtime.AttackPhase, runtime.AttackFlags) && ClaimCue(CueKindSwingWhiff, playerID)) {
                Cues?.Play(CoreMechanicCueIDs.SwingWhiff);
            }
        }

        /// <summary>The victim lost HP to something other than an active stage hazard.</summary>
        internal void PlayHitCue(int playerID) {
            if (ClaimCue(CueKindHit, playerID)) Cues?.Play(CoreMechanicCueIDs.HitBasic);
        }

        /// <summary>An ordinary block or a guard break plays the ping; a perfect block the bright one.</summary>
        internal void PlayBlockCue(FTT.Combat.BlockSparkKind kind, int playerID) {
            bool perfect = kind == FTT.Combat.BlockSparkKind.Perfect;
            if (!ClaimCue(perfect ? CueKindPerfectBlock : CueKindBlock, playerID)) return;
            Cues?.Play(perfect ? CoreMechanicCueIDs.PerfectBlock : CoreMechanicCueIDs.Block);
        }
    }
}
