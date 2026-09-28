using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Godot;

namespace FTT.Core {

    /// <summary>
    /// Package 12 W6 (G15d). The build version string, read from the single
    /// canonical place it lives — <c>application/config/version</c> in
    /// <c>project.godot</c> — and never copied anywhere else. Shown in a main-menu
    /// corner, written to the log at boot, and stamped on every local crash report.
    /// </summary>
    public static class BuildInfo {
        public const string VersionSetting = "application/config/version";

        /// <summary>The version string, or "0.0.0" when the setting is missing.</summary>
        public static string Version {
            get {
                Variant value = ProjectSettings.GetSetting(VersionSetting, "0.0.0");
                string text = value.AsString();
                return string.IsNullOrWhiteSpace(text) ? "0.0.0" : text.Trim();
            }
        }

        /// <summary>The line written to the log at boot and on top of every crash report.</summary>
        public static string BootBanner {
            get {
                using Godot.Collections.Dictionary info = Engine.GetVersionInfo();
                string engine = info.TryGetValue("string", out Variant engineVersion) ? engineVersion.AsString() : "?";
                return $"Fighters Through Time {Version} (Godot {engine}, {OS.GetName()})";
            }
        }

        /// <summary>Writes the boot banner to the log (G15d: "written to every log").</summary>
        public static void LogBootBanner() => GD.Print(BootBanner);
    }

    /// <summary>
    /// Package 12 W6 (G09, adopted D9(a)). Crash detection by a session marker —
    /// the <see cref="SessionExitGuard"/> pattern — plus a <b>local</b> crash
    /// report and an "open the logs folder" affordance. <b>Nothing is uploaded</b>:
    /// there is no endpoint, no network call and no telemetry anywhere here.
    ///
    /// <para>Distinct from <see cref="SessionExitGuard"/>'s campaign marker, which
    /// is written only while a Story session is live and belongs to F10's attempt
    /// routing. This one covers the whole process: armed at boot, cleared by a
    /// clean shutdown. A marker still on disk at the next boot means the previous
    /// process never shut down cleanly.</para>
    /// </summary>
    public static class CrashReportService {
        public const string MarkerPath = "user://session_running.marker";
        public const string LogsDirectory = "user://logs";
        public const string ReportsDirectory = "user://crash_reports";

        /// <summary>Local reports kept on disk; older ones are pruned.</summary>
        public const int MaxKeptReports = 5;

        /// <summary>True when this boot found the previous session's marker still armed.</summary>
        public static bool PreviousSessionCrashed { get; private set; }

        /// <summary>What this boot decided to do about it (set by <see cref="OnBoot"/>).</summary>
        public static CrashReportAction BootAction { get; private set; }

        /// <summary>The local report this boot preserved, or "" when none.</summary>
        public static string LastReportPath { get; private set; } = "";

        /// <summary>True until the main menu has shown (or the player has dismissed) the Ask prompt.</summary>
        public static bool PromptPending { get; private set; }

        /// <summary>
        /// Boot hook: reads and re-arms the marker, applies the player's Ask /
        /// Always / Never choice, and keeps a local report when the choice says so.
        /// Idempotent within a process — only the first call reads the marker.
        /// </summary>
        public static void OnBoot(CrashReportMode mode) {
            if (_booted) return;
            _booted = true;
            PreviousSessionCrashed = MarkerExists();
            WriteMarker();
            BootAction = CrashReportPolicy.Resolve(PreviousSessionCrashed, mode);
            if (BootAction == CrashReportAction.None) return;
            LastReportPath = PreserveLatestLog();
            PromptPending = BootAction == CrashReportAction.KeepLocalReportAndPrompt;
        }

        private static bool _booted;

        /// <summary>Clean shutdown: the session is settled, the next boot is not a crash.</summary>
        public static void OnCleanExit() => ClearMarker();

        /// <summary>The prompt was shown (or answered); it never repeats this session.</summary>
        public static void ConsumePrompt() => PromptPending = false;

        /// <summary>Opens the local logs folder in the OS file browser. Local only.</summary>
        public static Error OpenLogsFolder() {
            string path = ProjectSettings.GlobalizePath(LogsDirectory);
            try { Directory.CreateDirectory(path); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            return OS.ShellOpen(path);
        }

        /// <summary>
        /// Copies the most recent rotated log (the crashed session's — Godot rotates
        /// <c>godot.log</c> at every launch) into the reports folder with the build
        /// banner on top, and prunes to <see cref="MaxKeptReports"/>. Returns the
        /// report path, or "" when there was nothing to keep.
        /// </summary>
        public static string PreserveLatestLog() {
            try {
                string logs = ProjectSettings.GlobalizePath(LogsDirectory);
                if (!Directory.Exists(logs)) return "";
                string current = Path.Combine(logs, "godot.log");
                FileInfo latest = new DirectoryInfo(logs)
                    .GetFiles("*.log")
                    .Where(file => !string.Equals(file.FullName, Path.GetFullPath(current), StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .FirstOrDefault();
                if (latest == null) return "";
                string reports = ProjectSettings.GlobalizePath(ReportsDirectory);
                Directory.CreateDirectory(reports);
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
                string target = Path.Combine(reports, $"crash_{stamp}.log");
                string header = $"{BuildInfo.BootBanner}\nPrevious session did not shut down cleanly. Local report only; nothing was uploaded.\n---\n";
                File.WriteAllText(target, header + File.ReadAllText(latest.FullName));
                Prune(reports);
                return target;
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                GD.PushWarning($"Crash report could not be preserved: {exception.Message}");
                return "";
            }
        }

        private static void Prune(string reports) {
            List<FileInfo> kept = new DirectoryInfo(reports)
                .GetFiles("crash_*.log")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();
            for (int index = MaxKeptReports; index < kept.Count; index++) {
                try { kept[index].Delete(); } catch (IOException) { }
            }
        }

        public static bool MarkerExists() {
            try {
                return File.Exists(ProjectSettings.GlobalizePath(MarkerPath));
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                return false;
            }
        }

        public static void WriteMarker() {
            try {
                string path = ProjectSettings.GlobalizePath(MarkerPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                GD.PushWarning($"Session crash marker could not be written: {exception.Message}");
            }
        }

        public static void ClearMarker() {
            try {
                string path = ProjectSettings.GlobalizePath(MarkerPath);
                if (File.Exists(path)) File.Delete(path);
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                GD.PushWarning($"Session crash marker could not be cleared: {exception.Message}");
            }
        }

        /// <summary>Test seam: forgets this process's boot decision so a suite can re-run it.</summary>
        internal static void ResetForTesting() {
            _booted = false;
            PreviousSessionCrashed = false;
            BootAction = CrashReportAction.None;
            LastReportPath = "";
            PromptPending = false;
        }
    }
}
