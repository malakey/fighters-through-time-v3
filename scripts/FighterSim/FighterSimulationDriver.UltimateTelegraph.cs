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

        /// <summary>
        /// True while <paramref name="playerID"/>'s activation strike is in its
        /// wind-up and the caster is live and free.
        /// <para>CM-FIXA-PRES (2026-10-16): component 321 alone is one tick late
        /// on a knockout. <c>AdvanceUltimate</c> clears a fallen or held caster
        /// at the top of the combat system, but a KO landed later in the same
        /// tick (a basic swing, a projectile, a zone, a hazard) goes through
        /// <c>ApplyStockLoss</c>, which leaves 321 in its wind-up until the next
        /// tick's <c>AdvanceUltimate</c>. The KO tick is the frame the stock-loss
        /// freeze and the decisive-KO hit-freeze then hold, so the telegraph
        /// (and the cue) are derived with <c>AdvanceUltimate</c>'s own clear
        /// conditions: no stocks left, on the respawn platform, or held by a
        /// grab. Presentation only; no sim state is read differently.</para>
        /// </summary>
        private bool IsUltimateActivationWindup(int playerID) =>
            Simulation != null
            && Simulation.TryGetFighterUltimateActivation(playerID, out FighterUltimateActivationComponent activation)
            && activation.Phase == FighterUltimateActivationRules.PhaseWindup
            && Simulation.TryGetFighter(playerID, out FighterStateComponent caster)
            && !FighterStockLossRules.IsFallen(in caster)
            && !(Simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb) && verb.BeingHeld != 0);

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
