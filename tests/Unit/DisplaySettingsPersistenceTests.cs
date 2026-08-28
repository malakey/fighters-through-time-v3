using FTT.Core;
using GdUnit4;
using Godot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A4. Display settings persistence (resolution / window mode / VSync
/// were applied to the DisplayServer but never stored and never restored at boot)
/// and the global payload's first schema step, v3 → v4.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DisplaySettingsPersistenceTests {

    [TestCase]
    public void DisplayFieldsRoundTripThroughTheGlobalPayload() {
        var data = new GlobalSaveData {
            ResolutionWidth = 1280,
            ResolutionHeight = 720,
            WindowMode = WindowModeSetting.BorderlessFullscreen,
            VSyncEnabled = false
        };
        data.Normalize();

        GlobalSaveData restored = SaveSchemaMigrator.DeserializeGlobal(JsonConvert.SerializeObject(data));

        AssertThat(restored.ResolutionWidth).IsEqual(1280);
        AssertThat(restored.ResolutionHeight).IsEqual(720);
        AssertThat(restored.WindowMode).IsEqual(WindowModeSetting.BorderlessFullscreen);
        AssertThat(restored.VSyncEnabled).IsFalse();
    }

    [TestCase]
    public void NormalizeSnapsUnsupportedSizesAndClampsTheWindowMode() {
        var data = new GlobalSaveData {
            ResolutionWidth = 0,
            ResolutionHeight = 0,
            WindowMode = (WindowModeSetting)42
        };
        data.Normalize();

        // 0x0 is nearest to the smallest supported size, never left as 0x0.
        AssertThat(data.ResolutionWidth).IsEqual(1024);
        AssertThat(data.ResolutionHeight).IsEqual(576);
        AssertThat(data.WindowMode).IsEqual(WindowModeSetting.Windowed);

        var oversized = new GlobalSaveData { ResolutionWidth = 4096, ResolutionHeight = 2160 };
        oversized.Normalize();
        AssertThat(oversized.ResolutionWidth).IsEqual(1920);
        AssertThat(oversized.ResolutionHeight).IsEqual(1080);

        // A near-miss snaps to its own row rather than to the first one.
        var nearMiss = new GlobalSaveData { ResolutionWidth = 1590, ResolutionHeight = 890 };
        nearMiss.Normalize();
        AssertThat(nearMiss.ResolutionWidth).IsEqual(1600);
        AssertThat(nearMiss.ResolutionHeight).IsEqual(900);
    }

    [TestCase]
    public void ResolutionIndexIsAlwaysInsideTheSupportedTable() {
        for (int index = 0; index < GlobalSaveData.SupportedResolutions.Length; index++) {
            (int width, int height) = GlobalSaveData.SupportedResolutions[index];
            AssertThat(GlobalSaveData.ResolutionIndex(width, height)).IsEqual(index);
        }
        AssertThat(GlobalSaveData.ResolutionIndex(-100, -100)).IsEqual(3);
        AssertThat(GlobalSaveData.ResolutionIndex(int.MaxValue / 4, int.MaxValue / 4)).IsEqual(0);
    }

    [TestCase]
    public void DefaultsAreTheReferenceCanvasWindowedWithVSyncOn() {
        var data = new GlobalSaveData();
        data.Normalize();
        AssertThat(data.ResolutionWidth).IsEqual(1920);
        AssertThat(data.ResolutionHeight).IsEqual(1080);
        AssertThat(data.WindowMode).IsEqual(WindowModeSetting.Windowed);
        AssertThat(data.VSyncEnabled).IsTrue();
    }

    [TestCase]
    public void GlobalVersionThreeMigratesTheDeadStringBindingMapToTheStructuredShape() {
        // The v3 shape: InputBindings as a flat action -> string map. It was never
        // written by any code path, so migration drops it rather than guessing.
        string legacy = JsonConvert.SerializeObject(new {
            SaveVersion = 3,
            MasterVolume = 0.5f,
            InputBindings = new { gameplay_jump = "Space", gameplay_block = "I" }
        });

        GlobalSaveData migrated = SaveSchemaMigrator.DeserializeGlobal(legacy);

        // CurrentVersion is shared by both payloads and moved to 5 with the
        // story-side V7.3 attempt-state fields; the v3 -> v4 binding step
        // still runs, and the payload lands on whatever is current.
        AssertThat(migrated.SaveVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);
        AssertObject(migrated.InputBindings).IsNotNull();
        AssertThat(migrated.InputBindings.Actions.Count).IsEqual(0);
        // Unrelated v3 fields survive the step.
        AssertThat(migrated.MasterVolume).IsEqual(0.5f);
    }

    [TestCase]
    public void GlobalVersionFourKeepsAnAlreadyStructuredBindingSet() {
        var data = new GlobalSaveData();
        data.InputBindings.Set("gameplay_roll", new[] {
            new InputBindingEvent(InputBindingKind.Key, 79)
        });
        data.Normalize();

        GlobalSaveData restored = SaveSchemaMigrator.DeserializeGlobal(JsonConvert.SerializeObject(data));

        AssertThat(restored.InputBindings.For("gameplay_roll").Count).IsEqual(1);
        AssertThat(restored.InputBindings.For("gameplay_roll")[0].Code).IsEqual(79);
    }

    [TestCase]
    public void AGlobalPayloadWithNoBindingFieldAtAllStillLoads() {
        JObject root = JObject.Parse("{\"SaveVersion\":3,\"UIVolume\":0.25}");
        GlobalSaveData migrated = SaveSchemaMigrator.DeserializeGlobal(root.ToString());

        AssertThat(migrated.SaveVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);
        AssertObject(migrated.InputBindings).IsNotNull();
        AssertThat(migrated.InputBindings.Actions.Count).IsEqual(0);
        AssertThat(migrated.UIVolume).IsEqual(0.25f);
    }

    [TestCase]
    public void WindowModeSettingsMapToTheirEngineModes() {
        // Audit Low ("window-mode swap"): Godot's DisplayServer.WindowMode.Fullscreen
        // is the BORDERLESS fullscreen window; ExclusiveFullscreen is true
        // fullscreen. The settings labels map accordingly — the old mapping had
        // the two crossed.
        AssertThat(ViewportEnforcer.ResolveWindowMode(WindowModeSetting.Windowed))
            .IsEqual(DisplayServer.WindowMode.Windowed);
        AssertThat(ViewportEnforcer.ResolveWindowMode(WindowModeSetting.Fullscreen))
            .IsEqual(DisplayServer.WindowMode.ExclusiveFullscreen);
        AssertThat(ViewportEnforcer.ResolveWindowMode(WindowModeSetting.BorderlessFullscreen))
            .IsEqual(DisplayServer.WindowMode.Fullscreen);
    }

    [TestCase]
    public void ApplyingDisplaySettingsIsSafeWithoutAWindow() {
        // Headless runs skip the DisplayServer calls; the guard is what keeps the
        // boot-time apply from erroring through every test session.
        ViewportEnforcer.ApplyDisplaySettings(null);
        var data = new GlobalSaveData { ResolutionWidth = 7, ResolutionHeight = 7 };
        ViewportEnforcer.ApplyDisplaySettings(data);
        // ApplyDisplaySettings normalizes before touching the server.
        AssertThat(data.ResolutionWidth).IsEqual(1024);
    }
}
