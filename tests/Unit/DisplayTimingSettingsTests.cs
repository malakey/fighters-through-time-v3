using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W6. The project.godot application settings W6 owns: G04 display
/// timing (a 60 FPS render cap with V-Sync on or off, a 60 Hz physics tick and no
/// physics interpolation), G09's rotating local logs (5 sessions), and G15d's
/// single canonical build version.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DisplayTimingSettingsTests {

    [TestCase]
    public void RenderingIsCappedAtSixtyWithAFixedSixtyHertzTickAndNoInterpolation() {
        AssertThat(ProjectSettings.GetSetting(DisplayTimingRules.MaxFpsSetting).AsInt32())
            .IsEqual(DisplayTimingRules.MaxFramesPerSecond);
        AssertThat(DisplayTimingRules.MaxFramesPerSecond).IsEqual(60);
        AssertThat(ProjectSettings.GetSetting(DisplayTimingRules.PhysicsTicksSetting).AsInt32()).IsEqual(60);
        AssertThat(ProjectSettings.GetSetting(DisplayTimingRules.PhysicsInterpolationSetting).AsBool()).IsFalse();
        AssertThat(Engine.PhysicsTicksPerSecond).IsEqual(60);
    }

    [TestCase]
    public void ApplyingDisplaySettingsKeepsTheCapWhetherVSyncIsOnOrOff() {
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        if (data == null) return;
        bool original = data.VSyncEnabled;
        int originalCap = Engine.MaxFps;
        try {
            data.VSyncEnabled = false;
            ViewportEnforcer.ApplyDisplaySettings(data);
            AssertThat(Engine.MaxFps).IsEqual(60);
            data.VSyncEnabled = true;
            ViewportEnforcer.ApplyDisplaySettings(data);
            AssertThat(Engine.MaxFps).IsEqual(60);
        } finally {
            data.VSyncEnabled = original;
            Engine.MaxFps = originalCap;
        }
    }

    [TestCase]
    public void LocalFileLoggingIsOnAndRotatesFiveSessions() {
        AssertThat(ProjectSettings.GetSetting("debug/file_logging/enable_file_logging").AsBool()).IsTrue();
        AssertThat(ProjectSettings.GetSetting("debug/file_logging/max_log_files").AsInt32()).IsEqual(5);
        AssertThat(CrashReportService.LogsDirectory).IsEqual("user://logs");
    }

    [TestCase]
    public void TheBuildVersionHasOneCanonicalHomeAndReachesTheBootBanner() {
        string authored = ProjectSettings.GetSetting(BuildInfo.VersionSetting).AsString();
        AssertThat(string.IsNullOrWhiteSpace(authored)).IsFalse();
        AssertThat(BuildInfo.Version).IsEqual(authored);
        AssertThat(BuildInfo.BootBanner.Contains(authored)).IsTrue();
    }
}
