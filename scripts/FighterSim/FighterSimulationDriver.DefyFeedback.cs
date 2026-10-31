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

        private void SyncDefyFeedback(PlayerController player, int playerID) {
            if (Simulation == null || !Simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent action)) return;
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
            // A stock loss or respawn hold zeroes component 323; that is not a Defy.
            if (action.DefyFrames <= 0) return false;
            // A rollback correction can lower the counter; that is not a Defy either.
            if (!IsForwardCounterStep(previous, count, DefyCounterRing)) return false;
            if (player == null) return false;

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
