using System;
using System.Globalization;
using System.IO;
using Godot;

namespace FTT.Core {

    /// <summary>
    /// The campaign session marker. Written when a campaign session starts
    /// (StartCampaign/ResumeCampaign), refreshed on every checkpoint save, and
    /// cleared by a clean shutdown (SaveManager._ExitTree) or a paid
    /// pause-menu exit.
    ///
    /// <b>V7.6 ruling 2.B (Package 11 A3): crashes are free.</b> A marker
    /// surviving to the next boot no longer bills anything — the V7.3
    /// abnormal-exit fee and its notice are retired. The marker now exists
    /// purely so F10 can route an interrupted attempt's status (A3b).
    ///
    /// The VOLUNTARY 20% exit fee the pause menu charges is unchanged, and its
    /// math still lives here (Core) rather than in UI; PauseMenu delegates to
    /// these helpers.
    /// </summary>
    public static class SessionExitGuard {

        public const string MarkerPath = "user://saves/session.marker";

        /// <summary>Marks a live campaign session on <paramref name="slot"/>.</summary>
        public static void WriteMarker(int slot) {
            if (slot < 0) return;
            try {
                string path = ProjectSettings.GlobalizePath(MarkerPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, slot.ToString(CultureInfo.InvariantCulture));
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                GD.PushWarning($"Session exit marker could not be written: {exception.Message}");
            }
        }

        /// <summary>Clean shutdown or a paid exit fee: the session is settled.</summary>
        public static void ClearMarker() {
            try {
                string path = ProjectSettings.GlobalizePath(MarkerPath);
                if (File.Exists(path)) File.Delete(path);
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                GD.PushWarning($"Session exit marker could not be cleared: {exception.Message}");
            }
        }

        /// <summary>True when a marker from an unsettled session is on disk.</summary>
        public static bool TryReadMarker(out int slot) {
            slot = -1;
            try {
                string path = ProjectSettings.GlobalizePath(MarkerPath);
                if (!File.Exists(path)) return false;
                return int.TryParse(
                    File.ReadAllText(path).Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out slot) && slot >= 0;
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                GD.PushWarning($"Session exit marker could not be read: {exception.Message}");
                return false;
            }
        }

        // === Fee math (the V7 "one rule, one number": 20% of undeposited dust) ===

        /// <summary>The player retains 80% of undeposited dust, rounded down.</summary>
        public static int CalculateExitRetainedDust(int unbankedDust) =>
            Math.Max(0, unbankedDust) * 8 / 10;

        /// <summary>
        /// The wallet after the exit penalty: only dust earned in the current
        /// level is penalized, so any residue that predates the level (already
        /// counted in the wallet but not earned here) survives intact.
        /// </summary>
        public static int CalculateExitWalletAfterPenalty(int walletDust, int unbankedLevelDust) {
            int wallet = Math.Max(0, walletDust);
            int unbanked = Math.Clamp(unbankedLevelDust, 0, wallet);
            int forfeited = unbanked - CalculateExitRetainedDust(unbanked);
            return wallet - forfeited;
        }

        // V7.6 ruling 2.B (Package 11 A3): ApplyAbnormalExitFee is DELETED.
        // Crashes are free — a power cut is not a player decision, and billing
        // one taught players to fear the power switch rather than to press
        // Exit. The marker above survives as F10's attempt-status router
        // (A3b), and the VOLUNTARY 20% pause-menu exit fee — the two
        // Calculate* helpers — is unchanged.
    }
}
