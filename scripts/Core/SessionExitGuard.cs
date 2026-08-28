using System;
using System.Globalization;
using System.IO;
using Godot;

namespace FTT.Core {

    /// <summary>
    /// V7.3 quit-fee session marker — one rule for the Exit button, Alt-F4,
    /// and the power switch. A marker file is written when a campaign session
    /// starts (StartCampaign/ResumeCampaign) and refreshed on every
    /// checkpoint save; a clean shutdown (SaveManager._ExitTree) or a paid
    /// pause-menu exit fee clears it. If the marker is still present at the
    /// next boot, the session ended abnormally and the identical 20%
    /// undeposited-dust fee applies to the marked slot, with a one-line
    /// notice. A marker left while parked at the hub costs nothing, because
    /// the hub auto-deposit zeroes the at-risk wallet.
    ///
    /// The fee math lives here (Core), moved from PauseMenu, so the boot
    /// check never references UI; PauseMenu delegates to these helpers.
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

        /// <summary>
        /// Applies the abnormal-exit fee to a save's undeposited wallet.
        /// A crash cannot know what portion of the wallet was earned in the
        /// interrupted level, so the whole undeposited wallet is treated as
        /// at-risk — which matches reality, because the hub auto-deposit
        /// zeroes it between levels. Returns the dust forfeited.
        /// </summary>
        public static int ApplyAbnormalExitFee(StorySaveData save) {
            if (save == null) return 0;
            int wallet = Math.Max(0, save.LevelChronalDust);
            int after = CalculateExitWalletAfterPenalty(wallet, wallet);
            save.LevelChronalDust = after;
            return wallet - after;
        }
    }
}
