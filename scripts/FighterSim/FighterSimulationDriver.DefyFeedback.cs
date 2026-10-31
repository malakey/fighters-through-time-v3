using FTT.Characters;
using FTT.Core;
using FTT.Combat;
using Godot;

namespace FTT.FighterSim {

    // MM-FX (meter and Defy update 2026-10-31, plan section 2.11; ledger
    // USER-DEFY-TIME-BREAK-PRESENTATION, PROVISIONAL-DEFY-PRESENTATION): the Fighter half of
    // the Defy burst. Presentation only - read-only over component 323's wrapping Defy count
    // (ActionFlags bits 12-15). The cache is cosmetic and edge-detected like the wall bounce
    // (-1 until the first read, so a rematch or a restored snapshot never replays an old Defy),
    // and only a short forward step of the 4-bit ring counts, so a rollback correction that
    // lowers the counter never replays the burst. The ring, label, pulse, shake and cue come
    // from the shared DefyPresentation.
    public partial class FighterSimulationDriver {

        private const int DefyCounterRing = 16; // 323 ActionFlags bits 12-15

        /// <summary>Pixels above the feet where the burst is centred (the hurtbox's middle).</summary>
        public const float DefyBurstRisePixels = 60f;

        private readonly int[] _presentedDefyCount = { -1, -1 };

        // Replay guard, by counter identity and lifecycle (never by time). A rollback correction can
        // lower the counter and the resimulation raise it again (0, 1, 0, 1) without a new Defy. The
        // last presented value is remembered per player, with whether the counter has gone backwards
        // since: only a forward step back onto that value after a backward step is a replay. Any other
        // value is a new Defy, even one straight after an interrupted first Defy. Cosmetic only.
        private readonly int[] _lastPresentedDefyValue = { -1, -1 };
        private readonly bool[] _defyWentBackSincePresented = new bool[2];
        private bool _defySuddenDeathSeen;

        /// <summary>
        /// Forgets a player's Defy presentation memory: a stock reset or Sudden Death zeroes component
        /// 323, which is a lifecycle reset and not a rollback correction. The next read re-baselines.
        /// </summary>
        public void ResetDefyPresentation(int playerID) {
            _presentedDefyCount[playerID] = -1;
            _lastPresentedDefyValue[playerID] = -1;
            _defyWentBackSincePresented[playerID] = false;
        }

        private void SyncDefyFeedback(PlayerController player, int playerID) {
            if (Simulation == null || !Simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent action)) return;
            // Lifecycle resets: the respawn-platform hold and the Sudden Death setup zero 323.
            if (!_defySuddenDeathSeen && Simulation.GetMatchState().SuddenDeathActive == 1) {
                _defySuddenDeathSeen = true;
                ResetDefyPresentation(0);
                ResetDefyPresentation(1);
            }
            if (Simulation.TryGetFighter(playerID, out FighterStateComponent fighter) && fighter.RespawnFramesRemaining > 0) {
                ResetDefyPresentation(playerID);
                return;
            }
            PresentDefyCounter(player, playerID, in action);
        }

        /// <summary>
        /// Edge-detects the Defy count. On a short forward step of the ring while a Defy runs
        /// it plays the burst once and returns true; an unchanged or lowered counter plays
        /// nothing. Public for headless tests, which step the counter directly.
        /// </summary>
        public bool PresentDefyCounter(PlayerController player, int playerID, in FighterActionStateComponent action) {
            int count = (action.ActionFlags & FighterActionStateComponent.DefyCounterMask)
                >> FighterActionStateComponent.DefyCounterShift;
            int previous = _presentedDefyCount[playerID];
            _presentedDefyCount[playerID] = count;
            if (previous < 0 || count == previous) return false;
            // A rollback correction lowers the counter; remember it, it is not a Defy.
            if (!IsForwardCounterStep(previous, count, DefyCounterRing)) {
                if (_lastPresentedDefyValue[playerID] >= 0) _defyWentBackSincePresented[playerID] = true;
                return false;
            }
            // A stock loss or respawn hold zeroes component 323; that is not a Defy.
            if (action.DefyFrames <= 0) return false;
            if (player == null) return false;
            // The same Defy replayed after a correction: back onto the value already presented.
            if (_defyWentBackSincePresented[playerID] && count == _lastPresentedDefyValue[playerID]) {
                _defyWentBackSincePresented[playerID] = false;
                return false;
            }
            _lastPresentedDefyValue[playerID] = count;
            _defyWentBackSincePresented[playerID] = false;

            Vector2 center = player.GlobalPosition + new Vector2(0f, -DefyBurstRisePixels);
            DefyPresentation.Play(center, GetParent(), DefyBurstEffect.FighterRadiusPixels);
            HapticFeedbackManager.Instance?.OnGuardImpact(playerID);
            return true;
        }

        /// <summary>
        /// True through the Defy's invulnerable frames (1 to <c>DefyInvulnerableFrames</c>).
        /// Pure; read-only.
        /// </summary>
        public static bool IsDefyInvulnerable(in FighterActionStateComponent action) =>
            action.DefyFrames >= 1 && action.DefyFrames <= BasicComboRules.DefyInvulnerableFrames;
    }
}
