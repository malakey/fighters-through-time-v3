using System;
using System.IO;
using FTT.Core;
using GdUnit4;
using Godot;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
[RequireGodotRuntime]
public class SaveEnvelopeTests {
    private static readonly byte[] TestKey = BuildSequentialBytes(32, 1);
    private static readonly byte[] TestIV = BuildSequentialBytes(16, 33);

    [TestCase]
    public void AuthenticatedEnvelopeRoundTripsMetadataAndPayload() {
        const string json = "{\"SaveVersion\":2,\"SelectedCharacterID\":\"einstein\"}";
        byte[] envelope = SaveEnvelopeCodec.Encode("story", 2, json, TestKey, 123456789L, TestIV);

        AssertThat(SaveEnvelopeCodec.TryDecode(envelope, TestKey, out DecodedSaveEnvelope decoded, out _)).IsTrue();
        AssertThat(decoded.PayloadType).IsEqual("story");
        AssertThat(decoded.SchemaVersion).IsEqual(2);
        AssertThat(decoded.UpdatedTimestamp).IsEqual(123456789L);
        AssertThat(decoded.Json).IsEqual(json);
    }

    [TestCase]
    public void AnyEnvelopeTamperingFailsAuthentication() {
        byte[] envelope = SaveEnvelopeCodec.Encode("global", 2, "{\"SaveVersion\":2}", TestKey, 5L, TestIV);
        envelope[envelope.Length / 2] ^= 0x40;

        AssertThat(SaveEnvelopeCodec.TryDecode(envelope, TestKey, out _, out string error)).IsFalse();
        AssertThat(string.IsNullOrWhiteSpace(error)).IsFalse();
    }

    [TestCase]
    public void LegacyStoryPayloadMigratesIntoCharacterScopedProgress() {
        string legacy = JsonConvert.SerializeObject(new {
            CharacterID = "joan",
            CurrentLevelIndex = 1,
            ChronalDust = 45,
            DepositedChronalDust = 120,
            UnlockedResonanceNodes = new[] { "joan_minor_01" },
            PlaytimeSeconds = 37.5f,
            LastSaveTimestamp = 1700000000L
        });

        StorySaveData migrated = SaveSchemaMigrator.DeserializeStory(legacy);
        AssertThat(migrated.SaveVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);
        AssertThat(migrated.SelectedCharacterID).IsEqual("joan");
        AssertThat(migrated.CurrentLevelID).IsEqual("res://scenes/campaign/Level_01_Florence.tscn");
        AssertThat(migrated.LevelChronalDust).IsEqual(45);
        AssertThat(migrated.DepositedChronalDust["joan"]).IsEqual(120);
        AssertThat(migrated.GridProgress["joan"][0]).IsEqual("joan_minor_01");
    }

    [TestCase]
    public void FutureSchemaIsRejectedWithoutMutation() {
        bool rejected = false;
        try {
            SaveSchemaMigrator.DeserializeStory("{\"SaveVersion\":999,\"SelectedCharacterID\":\"tesla\"}");
        } catch (SaveVersionException) {
            rejected = true;
        }
        AssertThat(rejected).IsTrue();
    }

    [TestCase]
    public void VersionTwoStorySaveMigratesPuzzleCompletionCollection() {
        StorySaveData migrated = SaveSchemaMigrator.DeserializeStory(
            "{\"SaveVersion\":2,\"SelectedCharacterID\":\"tesla\"}");

        // Package 8 A4 moved CurrentVersion to 4 for the global payload's binding
        // schema; the story chain has no v3 -> v4 step, so a v2 save still lands on
        // whatever the current version is with its puzzle collection created.
        AssertThat(migrated.SaveVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);
        AssertObject(migrated.CompletedPuzzleIDs).IsNotNull();
        AssertThat(migrated.CompletedPuzzleIDs.Count).IsEqual(0);
    }

    [TestCase]
    public void GlobalPayloadCarriesStructuredInputBindingsThroughTheEnvelope() {
        // Package 8 A4: binding data lives inside the encrypted global envelope and
        // nowhere else. This is the envelope-level proof for that schema.
        var data = new GlobalSaveData();
        data.InputBindings.Set("gameplay_jump", new[] {
            new InputBindingEvent(InputBindingKind.Key, (int)Key.Z),
            new InputBindingEvent(InputBindingKind.JoyButton, (int)JoyButton.X)
        });
        data.Normalize();

        byte[] envelope = SaveEnvelopeCodec.Encode(
            "global", data.SaveVersion, JsonConvert.SerializeObject(data), TestKey, 99L, TestIV);

        AssertThat(SaveEnvelopeCodec.TryDecode(envelope, TestKey, out DecodedSaveEnvelope decoded, out _)).IsTrue();
        AssertThat(decoded.PayloadType).IsEqual("global");
        AssertThat(decoded.SchemaVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);

        GlobalSaveData restored = SaveSchemaMigrator.DeserializeGlobal(decoded.Json);
        AssertThat(restored.InputBindings.For("gameplay_jump").Count).IsEqual(2);
        AssertThat(restored.InputBindings.For("gameplay_jump")[1].Kind).IsEqual(InputBindingKind.JoyButton);
        AssertThat(restored.InputBindings.For("gameplay_jump")[1].Code).IsEqual((int)JoyButton.X);
    }

    [TestCase]
    public void AtomicWriteKeepsLastKnownGoodBackup() {
        string directory = ProjectSettings.GlobalizePath($"user://test-saves/{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "story.sav");
        try {
            byte[] first = SaveEnvelopeCodec.Encode("story", 2, "{\"value\":1}", TestKey, 1L, TestIV);
            byte[] second = SaveEnvelopeCodec.Encode("story", 2, "{\"value\":2}", TestKey, 2L, TestIV);
            AtomicSaveStore.Write(path, first, IsValidStoryEnvelope);
            AtomicSaveStore.Write(path, second, IsValidStoryEnvelope);

            AssertThat(File.Exists(path)).IsTrue();
            AssertThat(File.Exists(path + ".bak")).IsTrue();
            AssertThat(SaveEnvelopeCodec.TryDecode(File.ReadAllBytes(path + ".bak"), TestKey, out DecodedSaveEnvelope backup, out _)).IsTrue();
            AssertThat(backup.Json).IsEqual("{\"value\":1}");
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [TestCase]
    public void BackupRotationRefusesToClobberTheGoodBackupWithACorruptPrimary() {
        // Audit M-21: rotation used to copy the current primary over the backup
        // unconditionally. After a backup-based recovery (corrupt primary still on
        // disk), the next save then destroyed the last-known-good copy.
        string directory = ProjectSettings.GlobalizePath($"user://test-saves/{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "story.sav");
        try {
            byte[] first = SaveEnvelopeCodec.Encode("story", 2, "{\"value\":1}", TestKey, 1L, TestIV);
            byte[] second = SaveEnvelopeCodec.Encode("story", 2, "{\"value\":2}", TestKey, 2L, TestIV);
            AtomicSaveStore.Write(path, first, IsValidStoryEnvelope);
            AtomicSaveStore.Write(path, second, IsValidStoryEnvelope);
            // The backup now holds the good {"value":1}; corrupt the primary in place.
            byte[] corrupted = File.ReadAllBytes(path);
            corrupted[corrupted.Length / 2] ^= 0x40;
            File.WriteAllBytes(path, corrupted);

            byte[] third = SaveEnvelopeCodec.Encode("story", 2, "{\"value\":3}", TestKey, 3L, TestIV);
            AtomicSaveStore.Write(path, third, IsValidStoryEnvelope);

            // The unverifiable primary was not rotated: the backup is still good.
            AssertThat(SaveEnvelopeCodec.TryDecode(File.ReadAllBytes(path + ".bak"), TestKey, out DecodedSaveEnvelope backup, out _)).IsTrue();
            AssertThat(backup.Json).IsEqual("{\"value\":1}");
            // And the new primary landed normally.
            AssertThat(SaveEnvelopeCodec.TryDecode(File.ReadAllBytes(path), TestKey, out DecodedSaveEnvelope primary, out _)).IsTrue();
            AssertThat(primary.Json).IsEqual("{\"value\":3}");
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [TestCase]
    public void DeleteAllCandidatesRemovesPrimaryRecoveryAndTemporaryFiles() {
        string directory = ProjectSettings.GlobalizePath($"user://test-saves/{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "story.sav");
        Directory.CreateDirectory(directory);
        string[] candidates = {
            path,
            path + ".bak",
            path + ".tmp",
            path + ".corrupt",
            path + ".bak.corrupt"
        };
        try {
            foreach (string candidate in candidates) File.WriteAllText(candidate, "test");

            AtomicSaveStore.DeleteAllCandidates(path);

            foreach (string candidate in candidates) AssertThat(File.Exists(candidate)).IsFalse();
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static bool IsValidStoryEnvelope(byte[] bytes) =>
        SaveEnvelopeCodec.TryDecode(bytes, TestKey, out DecodedSaveEnvelope decoded, out _)
        && decoded.PayloadType == "story";

    private static byte[] BuildSequentialBytes(int count, int start) {
        byte[] bytes = new byte[count];
        for (int index = 0; index < count; index++) bytes[index] = (byte)(start + index);
        return bytes;
    }
}
