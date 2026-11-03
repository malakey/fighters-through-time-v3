using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>GR-INTEG: the session marker follows the save directory test override.</summary>
[TestSuite]
[RequireGodotRuntime]
public class SessionMarkerOverrideTests {
    [TestCase]
    public void AScopedRunNeverTouchesTheRealSessionMarker() {
        string real = ProjectSettings.GlobalizePath(SessionExitGuard.MarkerPath);
        bool existed = System.IO.File.Exists(real);
        string before = existed ? System.IO.File.ReadAllText(real) : null;
        string name = $"test-saves/{System.Guid.NewGuid():N}";
        string scratch = ProjectSettings.GlobalizePath($"user://{name}");
        string originalOverride = SaveManager.SaveDirectoryOverrideForTesting;
        try {
            if (!existed) {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(real));
                System.IO.File.WriteAllText(real, "2");
                before = "2";
            }
            SaveManager.SaveDirectoryOverrideForTesting = $"user://{name}/";
            SessionExitGuard.WriteMarker(1);
            AssertThat(SessionExitGuard.TryReadMarker(out int slot)).IsTrue();
            AssertThat(slot).IsEqual(1);
            SessionExitGuard.ClearMarker();
            AssertThat(SessionExitGuard.TryReadMarker(out _)).IsFalse();
            AssertThat(System.IO.File.Exists(real)).IsTrue();
            AssertThat(System.IO.File.ReadAllText(real)).IsEqual(before);
        } finally {
            SaveManager.SaveDirectoryOverrideForTesting = originalOverride;
            if (existed) System.IO.File.WriteAllText(real, before);
            else if (System.IO.File.Exists(real)) System.IO.File.Delete(real);
            try { if (System.IO.Directory.Exists(scratch)) System.IO.Directory.Delete(scratch, true); } catch (System.IO.IOException) { }
        }
    }
}
