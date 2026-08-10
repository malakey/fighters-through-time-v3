using System;
using System.IO;
using System.Text;
using FTT.Core;
using GdUnit4;
using Godot;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Audit 2026-08-08 H-2 and M-22.
/// <para>
/// H-2: the legacy Base64/plain-JSON fallback loads unauthenticated data and
/// re-signs it under the real key — ADR 0004 frames it as a one-time development
/// measure, so it is gated to debug builds. The suite runs in a debug build, so
/// the fallback path itself stays covered; the gate decision is exercised through
/// the injectable override rather than by faking a release build.
/// </para>
/// <para>
/// M-22: a malformed <c>.savekey</c> used to throw out of <c>SaveManager._Ready</c>,
/// silently bricking every load and save for the session. Provisioning failure now
/// enters an explicit no-save state with a localized notice, and never deletes or
/// overwrites the key file or any save file.
/// </para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SaveSecurityHardeningTests {

    [TestCase]
    public void LegacyFallbackExecutesInDebugBuilds() {
        // The test host is a debug build, so the default gate is open and the
        // existing migration behavior is preserved for development.
        AssertThat(SaveManager.IsLegacyMigrationEnabled).IsTrue();

        string directory = ProjectSettings.GlobalizePath($"user://test-saves/{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string storyPath = Path.Combine(directory, "legacy_story.sav");
        string globalPath = Path.Combine(directory, "legacy_global.sav");
        try {
            string storyJson = JsonConvert.SerializeObject(new { SaveVersion = 3, SelectedCharacterID = "einstein" });
            File.WriteAllText(storyPath, Convert.ToBase64String(Encoding.UTF8.GetBytes(storyJson)));
            File.WriteAllText(globalPath, JsonConvert.SerializeObject(new { SaveVersion = 3, MasterVolume = 0.5f }));

            AssertThat(SaveManager.TryLoadLegacyStory(storyPath, out StorySaveData story)).IsTrue();
            AssertThat(story.SelectedCharacterID).IsEqual("einstein");
            AssertThat(SaveManager.TryLoadLegacyGlobal(globalPath, out GlobalSaveData global)).IsTrue();
            AssertThat(global.MasterVolume).IsEqual(0.5f);
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [TestCase]
    public void LegacyFallbackIsRefusedWhenTheGateIsClosed() {
        string directory = ProjectSettings.GlobalizePath($"user://test-saves/{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string storyPath = Path.Combine(directory, "legacy_story.sav");
        string globalPath = Path.Combine(directory, "legacy_global.sav");
        try {
            string storyJson = JsonConvert.SerializeObject(new { SaveVersion = 3, SelectedCharacterID = "einstein" });
            File.WriteAllText(storyPath, Convert.ToBase64String(Encoding.UTF8.GetBytes(storyJson)));
            File.WriteAllText(globalPath, JsonConvert.SerializeObject(new { SaveVersion = 3, MasterVolume = 0.5f }));

            // The release-build decision, via the injectable override: perfectly
            // valid legacy files must NOT load once the gate is closed — the
            // caller falls through to the tamper/corrupt notice path instead.
            SaveManager.LegacyMigrationOverrideForTesting = false;
            AssertThat(SaveManager.IsLegacyMigrationEnabled).IsFalse();
            AssertThat(SaveManager.TryLoadLegacyStory(storyPath, out StorySaveData story)).IsFalse();
            AssertObject(story).IsNull();
            AssertThat(SaveManager.TryLoadLegacyGlobal(globalPath, out GlobalSaveData global)).IsFalse();
            AssertObject(global).IsNull();
        } finally {
            SaveManager.LegacyMigrationOverrideForTesting = null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [TestCase]
    public void MalformedSaveKeyEntersAnExplicitNoSaveStateWithoutTouchingFiles() {
        string directory = ProjectSettings.GlobalizePath($"user://test-saves/{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string keyPath = Path.Combine(directory, ".savekey");
        byte[] malformed = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        File.WriteAllBytes(keyPath, malformed);

        // A fresh node, never added to the tree: _Ready does not run, so the
        // autoload's Instance and the developer's real saves are untouched.
        var manager = new SaveManager();
        try {
            AssertThat(manager.TryProvisionMasterKey(new FileSaveKeyProvider(keyPath))).IsFalse();

            AssertThat(manager.IsSaveSystemAvailable).IsFalse();
            // The main menu surfaces LastLoadNotice whenever the key is set.
            AssertThat(manager.LastLoadNoticeKey).IsEqual("save_notice_key_error");

            // Saves refuse (with a warning) instead of failing silently...
            AssertThat(manager.SaveGlobalData()).IsFalse();
            // ...loads report empty instead of corrupt-flagging files we cannot
            // decode for lack of a key...
            AssertThat(manager.LoadGlobalData()).IsFalse();
            AssertThat(manager.LoadStorySlot(0)).IsFalse();

            // ...and the bad key file is preserved exactly as it was — never
            // deleted or regenerated, or intact saves become undecryptable.
            AssertThat(Convert.ToBase64String(File.ReadAllBytes(keyPath)))
                .IsEqual(Convert.ToBase64String(malformed));
        } finally {
            manager.Free();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
