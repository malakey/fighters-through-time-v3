using FTT.Characters;

namespace FTT.FighterSim {

    // CM-ULT (2026-10-16, decision 2.1; closes DEFER-ULT-TELEGRAPH): the A02
    // activation wind-up telegraph and its critical cue, Fighter side.
    // Presentation only — read-only over component 321, and nothing here writes
    // FighterSim state. The two caches are cosmetic diffs like the Package 8 A3
    // ones; the telegraph itself is re-derived from 321's phase every sync, so a
    // rollback correction or a rematch that leaves the wind-up hides it on the
    // next frame.
    public partial class FighterSimulationDriver {

        private readonly bool[] _presentedUltimateTelegraph = { false, false };
        private readonly bool[] _cueUltimateWindup = { false, false };

        /// <summary>True while <paramref name="playerID"/>'s activation strike is in its wind-up.</summary>
        private bool IsUltimateActivationWindup(int playerID) =>
            Simulation != null
            && Simulation.TryGetFighterUltimateActivation(playerID, out FighterUltimateActivationComponent activation)
            && activation.Phase == FighterUltimateActivationRules.PhaseWindup;

        /// <summary>
        /// The red tint plus the Unblockable X for exactly the wind-up: shown on
        /// every wind-up frame, hidden on the first active frame. Null-glow safe.
        /// </summary>
        private void SyncUltimateTelegraph(PlayerController player, int playerID,
            FTT.Combat.GlowPresentationController glow) {
            bool windup = IsUltimateActivationWindup(playerID);
            if (_presentedUltimateTelegraph[playerID] == windup) return;
            _presentedUltimateTelegraph[playerID] = windup;
            FTT.Combat.UltimateActivationTelegraph.Apply(player, glow, windup);
        }

        /// <summary>
        /// The wind-up's critical cue, once, on the wind-up's first frame (an
        /// edge over component 321's phase).
        /// </summary>
        private void SyncUltimateWindupCue(int playerID, FTT.Core.CoreMechanicCuePresenter cues) {
            bool windup = IsUltimateActivationWindup(playerID);
            if (windup && !_cueUltimateWindup[playerID]) {
                cues?.Play(FTT.Core.CoreMechanicCueIDs.UltimateActivationWindup);
            }
            _cueUltimateWindup[playerID] = windup;
        }
    }
}
