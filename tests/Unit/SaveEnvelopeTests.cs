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
            // A current V7.6 Joan node. Package 11's v6 step runs A4's grid
            // refund on any pre-v6 payload, so a retired node ID here would be
            // dropped and refunded — correct behaviour, but it would stop this
            // case testing what it exists to test (the v1 character scoping).
            UnlockedResonanceNodes = new[] { "joan_zeal" },
            PlaytimeSeconds = 37.5f,
            LastSaveTimestamp = 1700000000L
        });

        StorySaveData migrated = SaveSchemaMigrator.DeserializeStory(legacy);
        AssertThat(migrated.SaveVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);
        AssertThat(migrated.SelectedCharacterID).IsEqual("joan");
        AssertThat(migrated.CurrentLevelID).IsEqual("res://scenes/campaign/Level_01_Florence.tscn");
        // Between levels with an undeposited wallet is the pre-H02 post-completion
        // shape, so Package 12's v7 step banks it once into Joan's own balance.
        AssertThat(migrated.LevelChronalDust).IsEqual(0);
        AssertThat(migrated.DepositedChronalDust["joan"]).IsEqual(165);
        AssertThat(migrated.GridProgress["joan"][0]).IsEqual("joan_zeal");
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

    /// <summary>
    /// Audit §4 Fighter Low (true-draw tally): <see cref="GlobalSaveData.TotalDraws"/>
    /// is additive on the existing v4 schema — no bump — so it must both survive a
    /// full envelope round trip and default to zero when an older payload written
    /// before the field existed is deserialized (Newtonsoft leaves a missing
    /// member at the C# default).
    /// </summary>
    [TestCase]
    public void DrawTallyRoundTripsAndDefaultsToZeroOnOlderPayloads() {
        var data = new GlobalSaveData { TotalWins = 3, TotalLosses = 1, TotalDraws = 2 };
        data.Normalize();

        byte[] envelope = SaveEnvelopeCodec.Encode(
            "global", data.SaveVersion, JsonConvert.SerializeObject(data), TestKey, 7L, TestIV);
        AssertThat(SaveEnvelopeCodec.TryDecode(envelope, TestKey, out DecodedSaveEnvelope decoded, out _)).IsTrue();
        GlobalSaveData restored = SaveSchemaMigrator.DeserializeGlobal(decoded.Json);
        AssertThat(restored.TotalDraws).IsEqual(2);
        AssertThat(restored.TotalWins).IsEqual(3);
        AssertThat(restored.TotalLosses).IsEqual(1);

        // A current-version payload from before the field existed: missing
        // member tolerated, tally starts at zero.
        GlobalSaveData legacy = SaveSchemaMigrator.DeserializeGlobal(
            $"{{\"SaveVersion\":{SaveSchemaMigrator.CurrentVersion},\"TotalWins\":5}}");
        AssertThat(legacy.TotalDraws).IsEqual(0);
        AssertThat(legacy.TotalWins).IsEqual(5);
    }

    /// <summary>
    /// V7.3 schema v5 is purely additive: a v4 story payload (written before
    /// the per-attempt fields existed) must load with the documented defaults
    /// — empty registries, Integrity 100, both flags false. Package 11's v6 step
    /// keeps that true: it derives, it never invents. Package 12's v7 step adds
    /// one derivation this fixture exercises: a between-levels payload with an
    /// undeposited wallet is the pre-H02 post-completion shape, so the wallet is
    /// banked once (W2 <c>ReconcileUndepositedCompletion</c>).
    /// </summary>
    [TestCase]
    public void VersionFourStoryPayloadLoadsWithVersionSixDefaults() {
        StorySaveData migrated = SaveSchemaMigrator.DeserializeStory(
            "{\"SaveVersion\":4,\"SelectedCharacterID\":\"joan\",\"LevelChronalDust\":42}");

        AssertThat(SaveSchemaMigrator.CurrentVersion).IsEqual(7);
        AssertThat(migrated.SaveVersion).IsEqual(7);
        AssertThat(migrated.LevelChronalDust).IsEqual(0);
        AssertThat(migrated.DepositedChronalDust["joan"]).IsEqual(42);
        AssertObject(migrated.ActivatedCheckpointIDs).IsNotNull();
        AssertThat(migrated.ActivatedCheckpointIDs.Count).IsEqual(0);
        AssertObject(migrated.FontUsesConsumed).IsNotNull();
        AssertThat(migrated.FontUsesConsumed.Count).IsEqual(0);
        AssertObject(migrated.DestroyedExtractorIDs).IsNotNull();
        AssertThat(migrated.DestroyedExtractorIDs.Count).IsEqual(0);
        AssertObject(migrated.FoundSecretIDs).IsNotNull();
        AssertThat(migrated.FoundSecretIDs.Count).IsEqual(0);
        AssertObject(migrated.ViewedDialogueIDs).IsNotNull();
        AssertThat(migrated.ViewedDialogueIDs.Count).IsEqual(0);
        AssertThat(migrated.LevelIntegrityPercent).IsEqual(100f);
        AssertThat(migrated.HasSeenCollapseBeat).IsFalse();

        // A null-armed v5 payload (hand-edited) is normalized, not crashed on.
        StorySaveData nulled = SaveSchemaMigrator.DeserializeStory(
            "{\"SaveVersion\":5,\"ActivatedCheckpointIDs\":null,\"FontUsesConsumed\":null,"
            + "\"DestroyedExtractorIDs\":null,\"FoundSecretIDs\":null,\"ViewedDialogueIDs\":null,"
            + "\"LevelIntegrityPercent\":250.0}");
        AssertObject(nulled.ActivatedCheckpointIDs).IsNotNull();
        AssertObject(nulled.FontUsesConsumed).IsNotNull();
        AssertObject(nulled.DestroyedExtractorIDs).IsNotNull();
        AssertObject(nulled.FoundSecretIDs).IsNotNull();
        AssertObject(nulled.ViewedDialogueIDs).IsNotNull();
        AssertThat(nulled.LevelIntegrityPercent).IsEqual(100f);
    }

    [TestCase]
    public void AttemptStateFieldsRoundTripAndTheFutureIsStillRejected() {
        var data = new StorySaveData {
            SelectedCharacterID = "einstein",
            ActivatedCheckpointIDs = new System.Collections.Generic.List<string> {
                "level_02_orleans_checkpoint_0", "level_02_orleans_checkpoint_1"
            },
            FontUsesConsumed = new System.Collections.Generic.Dictionary<string, int> {
                ["Orleans:font"] = 1
            },
            DestroyedExtractorIDs = new System.Collections.Generic.List<string> { "orleans_extractor_1" },
            FoundSecretIDs = new System.Collections.Generic.List<string> { "orleans_secret" },
            LevelIntegrityPercent = 87.5f,
            ViewedDialogueIDs = new System.Collections.Generic.List<string> { "level_02.entrance" },
            HasSeenCollapseBeat = true
        };
        data.Normalize();

        byte[] envelope = SaveEnvelopeCodec.Encode(
            "story", data.SaveVersion, JsonConvert.SerializeObject(data), TestKey, 11L, TestIV);
        AssertThat(SaveEnvelopeCodec.TryDecode(envelope, TestKey, out DecodedSaveEnvelope decoded, out _)).IsTrue();
        AssertThat(decoded.SchemaVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);

        StorySaveData restored = SaveSchemaMigrator.DeserializeStory(decoded.Json);
        AssertThat(restored.ActivatedCheckpointIDs.Count).IsEqual(2);
        AssertThat(restored.ActivatedCheckpointIDs[1]).IsEqual("level_02_orleans_checkpoint_1");
        AssertThat(restored.FontUsesConsumed["Orleans:font"]).IsEqual(1);
        AssertThat(restored.DestroyedExtractorIDs[0]).IsEqual("orleans_extractor_1");
        AssertThat(restored.FoundSecretIDs[0]).IsEqual("orleans_secret");
        AssertThat(restored.LevelIntegrityPercent).IsEqual(87.5f);
        AssertThat(restored.ViewedDialogueIDs[0]).IsEqual("level_02.entrance");
        AssertThat(restored.HasSeenCollapseBeat).IsTrue();

        // The version fence still holds: v8 is the future and stays rejected.
        bool rejected = false;
        try {
            SaveSchemaMigrator.DeserializeStory("{\"SaveVersion\":8}");
        } catch (SaveVersionException) {
            rejected = true;
        }
        AssertThat(rejected).IsTrue();
    }

    /// <summary>
    /// Package 11 Phase C — the v5 → v6 step. A v5 payload loads with v6
    /// defaults, and the derivations the workstreams wrote actually run:
    /// the F10 attempt record is built from the loose fields, the paid-recovery
    /// allowance is seeded from the live gauge rather than invented, and the
    /// Legacy Unlock Schedule is backfilled from the completed levels.
    ///
    /// <para>F10's hard rule is the load-bearing half: a payload parked
    /// mid-level cannot have its anchors, Defy or reward claims reconstructed,
    /// so it is preserved with <b>zero</b> anchors, a spent Defy and
    /// <c>LegacyRecoveryRequired</c> — never defaulted to a full attempt.</para>
    /// </summary>
    [TestCase]
    public void VersionFiveStoryPayloadLoadsWithVersionSixDefaultsAndDerivesItsAttempt() {
        // Between levels: nothing to lose, so the attempt migrates cleanly.
        StorySaveData clean = SaveSchemaMigrator.DeserializeStory(
            "{\"SaveVersion\":5,\"SelectedCharacterID\":\"joan\","
            + "\"CompletedLevels\":[\"level_00_tutorial\",\"level_01_florence\",\"level_02_orleans\"],"
            + "\"LevelIntegrityPercent\":72.5}");

        AssertThat(clean.SaveVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);
        AssertThat(clean.AttemptState.HasAttempt).IsTrue();
        AssertThat(clean.AttemptState.Status).IsEqual(StoryAttemptStatus.Active);
        AssertThat(clean.AttemptState.DefyHistoryUsed).IsFalse();
        AssertThat(clean.AnchorCharges).IsEqual(0);
        // Seeded from the live gauge, not invented as 100.
        AssertThat(clean.CheckpointIntegrityPercent).IsEqual(72.5f);
        // A5: Movement after L1 and Special 1 after L2 are restored; Special 2
        // and the Ultimate are not.
        AssertThat(clean.UnlockedLegacyAbilities.ContainsKey("joan")).IsTrue();
        AssertArray(clean.UnlockedLegacyAbilities["joan"])
            .Contains(LegacyUnlockSchedule.MovementKey, LegacyUnlockSchedule.SpecialOneKey);
        AssertThat(clean.UnlockedLegacyAbilities["joan"].Contains(LegacyUnlockSchedule.UltimateKey)).IsFalse();
        // A1b / A2 / A10: additive fields load on their conservative initializers.
        AssertThat(clean.StoryDefyHistoryUsed).IsFalse();
        AssertThat(clean.TimeFreezeCooldownSeconds).IsEqual(0f);
        AssertThat(clean.ClaimedRewardSourceIDs.Count).IsEqual(0);

        // Parked mid-level: preserved, marked, and never handed a fresh attempt.
        StorySaveData parked = SaveSchemaMigrator.DeserializeStory(
            "{\"SaveVersion\":5,\"SelectedCharacterID\":\"joan\","
            + "\"LastCheckpointID\":\"level_13_chronal_void_checkpoint_1\","
            + "\"LevelChronalDust\":180,\"LevelIntegrityPercent\":40.0}");

        AssertThat(parked.AttemptState.Status).IsEqual(StoryAttemptStatus.LegacyRecoveryRequired);
        AssertThat(parked.AttemptState.AnchorChargesRemaining).IsEqual(0);
        AssertThat(parked.AnchorCharges).IsEqual(0);
        AssertThat(parked.AttemptState.DefyHistoryUsed).IsTrue();
        AssertThat(parked.LevelChronalDust).IsEqual(180);
        AssertThat(parked.CheckpointIntegrityPercent).IsEqual(40f);

        // Global side: a v5 payload's retired Hybrid mode normalizes to timed
        // Stock, the dead rewind bind moves onto Time Freeze, and the C01a/C01c
        // defaults are Off / chords-enabled without being written anywhere.
        GlobalSaveData global = SaveSchemaMigrator.DeserializeGlobal(
            "{\"SaveVersion\":5,\"LastMatchSettings\":{\"Saved\":true,\"Mode\":2,\"TimeLimit\":0.0},"
            + "\"InputBindings\":{\"Actions\":{\"gameplay_rewind\":[{\"Kind\":0,\"Code\":82}]}}}");

        AssertThat(global.SaveVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);
        AssertThat(global.LastMatchSettings.Mode).IsEqual((int)MatchMode.Stock);
        AssertThat(global.LastMatchSettings.TimeLimit).IsEqual(SavedMatchSettings.DefaultTimeLimitSeconds);
        AssertThat(global.InputBindings.For(InputManager.Actions.LegacyRewind).Count).IsEqual(0);
        AssertThat(global.InputBindings.For(InputManager.Actions.TimeFreeze).Count).IsEqual(1);
        AssertThat(global.ReducedTemporalEffects).IsFalse();
        AssertThat(global.InputBindings.IsShortcutEnabled(
            InputManager.Actions.Grab, InputDeviceKind.Keyboard)).IsTrue();
        AssertThat(global.SeenDialogueIDs.Count).IsEqual(0);
    }

    /// <summary>
    /// Package 11 Phase C — a payload at the current schema (v7 since Package 12
    /// Phase C) round trips on both sides with no migration step re-running, and
    /// the deferred global half (the seen-dialogue union, which needs the story
    /// slots the global payload loads before) is idempotent and additive.
    /// </summary>
    [TestCase]
    public void CurrentVersionPayloadsRoundTripOnBothSides() {
        var story = new StorySaveData {
            SelectedCharacterID = "tesla",
            LastCheckpointID = "level_14_neo_earth_checkpoint_2",
            CheckpointIntegrityPercent = 63.25f,
            LevelIntegrityPercent = 55f,
            StoryDefyHistoryUsed = true,
            TimeFreezeCooldownSeconds = 45f,
            AnchorCharges = 2,
            ClaimedRewardSourceIDs = new System.Collections.Generic.List<string> { "level_14.wave_3" },
            ViewedDialogueIDs = new System.Collections.Generic.List<string> { "level_14.entrance" },
            UnlockedLegacyAbilities = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> {
                ["tesla"] = new() { LegacyUnlockSchedule.MovementKey, LegacyUnlockSchedule.SpecialOneKey }
            }
        };
        story.AttemptState = StoryAttemptState.CreateFresh("level_14_neo_earth", 2);
        story.AttemptState.DefyHistoryUsed = true;
        story.Normalize();
        AssertThat(story.SaveVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);

        byte[] envelope = SaveEnvelopeCodec.Encode(
            "story", story.SaveVersion, JsonConvert.SerializeObject(story), TestKey, 21L, TestIV);
        AssertThat(SaveEnvelopeCodec.TryDecode(envelope, TestKey, out DecodedSaveEnvelope decoded, out _)).IsTrue();
        AssertThat(decoded.SchemaVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);

        StorySaveData restored = SaveSchemaMigrator.DeserializeStory(decoded.Json, out int loadedVersion);
        AssertThat(loadedVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);
        AssertThat(restored.CheckpointIntegrityPercent).IsEqual(63.25f);
        AssertThat(restored.StoryDefyHistoryUsed).IsTrue();
        AssertThat(restored.TimeFreezeCooldownSeconds).IsEqual(45f);
        AssertThat(restored.AnchorCharges).IsEqual(2);
        AssertThat(restored.AttemptState.AttemptID).IsEqual(story.AttemptState.AttemptID);
        AssertThat(restored.AttemptState.AnchorChargesRemaining).IsEqual(2);
        AssertThat(restored.AttemptState.DefyHistoryUsed).IsTrue();
        AssertThat(restored.ClaimedRewardSourceIDs[0]).IsEqual("level_14.wave_3");

        // The deferred global seed: union from the slots, idempotent on a rerun.
        var global = new GlobalSaveData();
        int added = SaveSchemaMigrator.SeedGlobalSeenDialogue(
            global, new[] { restored, null, new StorySaveData {
                ViewedDialogueIDs = new System.Collections.Generic.List<string> { "level_02.entrance", "" }
            } });
        AssertThat(added).IsEqual(2);
        AssertThat(global.SeenDialogueIDs.Contains("level_14.entrance")).IsTrue();
        AssertThat(global.SeenDialogueIDs.Contains("level_02.entrance")).IsTrue();
        AssertThat(SaveSchemaMigrator.SeedGlobalSeenDialogue(global, new[] { restored })).IsEqual(0);
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
