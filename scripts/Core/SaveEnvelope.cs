using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FTT.Core {

    public readonly struct DecodedSaveEnvelope {
        public readonly string PayloadType;
        public readonly int SchemaVersion;
        public readonly long UpdatedTimestamp;
        public readonly string Json;

        public DecodedSaveEnvelope(string payloadType, int schemaVersion, long updatedTimestamp, string json) {
            PayloadType = payloadType;
            SchemaVersion = schemaVersion;
            UpdatedTimestamp = updatedTimestamp;
            Json = json;
        }
    }

    public static class SaveEnvelopeCodec {
        public const int FormatVersion = 1;
        private static readonly byte[] Magic = { (byte)'F', (byte)'T', (byte)'T', (byte)'S' };
        private static readonly byte[] EncryptionContext = Encoding.UTF8.GetBytes("FTT.save.encryption.v1");
        private static readonly byte[] AuthenticationContext = Encoding.UTF8.GetBytes("FTT.save.authentication.v1");

        public static byte[] Encode(
            string payloadType,
            int schemaVersion,
            string json,
            byte[] masterKey,
            long updatedTimestamp,
            byte[] fixedIV = null) {
            ValidateMasterKey(masterKey);
            if (string.IsNullOrWhiteSpace(payloadType)) throw new ArgumentException("Payload type is required.", nameof(payloadType));
            if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));

            byte[] encryptionKey = DeriveKey(masterKey, EncryptionContext);
            byte[] authenticationKey = DeriveKey(masterKey, AuthenticationContext);
            byte[] iv = fixedIV == null ? RandomNumberGenerator.GetBytes(16) : (byte[])fixedIV.Clone();
            if (iv.Length != 16) throw new ArgumentException("AES-CBC requires a 16-byte IV.", nameof(fixedIV));
            byte[] plaintext = Encoding.UTF8.GetBytes(json ?? "{}");
            byte[] ciphertext;
            using (Aes aes = Aes.Create()) {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encryptionKey;
                aes.IV = iv;
                using ICryptoTransform encryptor = aes.CreateEncryptor();
                ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
            }

            byte[] authenticatedBytes;
            using (var stream = new MemoryStream()) {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) {
                    writer.Write(Magic);
                    writer.Write(FormatVersion);
                    writer.Write(schemaVersion);
                    writer.Write(updatedTimestamp);
                    writer.Write(payloadType);
                    writer.Write(iv.Length);
                    writer.Write(ciphertext.Length);
                    writer.Write(iv);
                    writer.Write(ciphertext);
                }
                authenticatedBytes = stream.ToArray();
            }

            byte[] signature;
            using (var hmac = new HMACSHA256(authenticationKey)) signature = hmac.ComputeHash(authenticatedBytes);
            byte[] envelope = new byte[authenticatedBytes.Length + signature.Length];
            Buffer.BlockCopy(authenticatedBytes, 0, envelope, 0, authenticatedBytes.Length);
            Buffer.BlockCopy(signature, 0, envelope, authenticatedBytes.Length, signature.Length);
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(authenticationKey);
            CryptographicOperations.ZeroMemory(plaintext);
            return envelope;
        }

        public static bool TryDecode(
            byte[] envelope,
            byte[] masterKey,
            out DecodedSaveEnvelope decoded,
            out string error) {
            decoded = default;
            error = "";
            try {
                ValidateMasterKey(masterKey);
                if (envelope == null || envelope.Length < 80) throw new InvalidDataException("Save envelope is truncated.");
                int authenticatedLength = envelope.Length - 32;
                byte[] authenticatedBytes = new byte[authenticatedLength];
                byte[] suppliedSignature = new byte[32];
                Buffer.BlockCopy(envelope, 0, authenticatedBytes, 0, authenticatedLength);
                Buffer.BlockCopy(envelope, authenticatedLength, suppliedSignature, 0, 32);

                byte[] authenticationKey = DeriveKey(masterKey, AuthenticationContext);
                byte[] expectedSignature;
                using (var hmac = new HMACSHA256(authenticationKey)) expectedSignature = hmac.ComputeHash(authenticatedBytes);
                CryptographicOperations.ZeroMemory(authenticationKey);
                if (!CryptographicOperations.FixedTimeEquals(expectedSignature, suppliedSignature)) {
                    throw new CryptographicException("Save authentication failed.");
                }

                using var stream = new MemoryStream(authenticatedBytes, false);
                using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                byte[] magic = reader.ReadBytes(4);
                if (!CryptographicOperations.FixedTimeEquals(magic, Magic)) throw new InvalidDataException("Unknown save format.");
                int formatVersion = reader.ReadInt32();
                if (formatVersion != FormatVersion) throw new InvalidDataException($"Unsupported save envelope version {formatVersion}.");
                int schemaVersion = reader.ReadInt32();
                long timestamp = reader.ReadInt64();
                string payloadType = reader.ReadString();
                int ivLength = reader.ReadInt32();
                int ciphertextLength = reader.ReadInt32();
                if (ivLength != 16 || ciphertextLength <= 0 || ciphertextLength > stream.Length) {
                    throw new InvalidDataException("Save envelope lengths are invalid.");
                }
                byte[] iv = reader.ReadBytes(ivLength);
                byte[] ciphertext = reader.ReadBytes(ciphertextLength);
                if (iv.Length != ivLength || ciphertext.Length != ciphertextLength || stream.Position != stream.Length) {
                    throw new InvalidDataException("Save envelope payload is truncated or has trailing data.");
                }

                byte[] encryptionKey = DeriveKey(masterKey, EncryptionContext);
                byte[] plaintext;
                using (Aes aes = Aes.Create()) {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = encryptionKey;
                    aes.IV = iv;
                    using ICryptoTransform decryptor = aes.CreateDecryptor();
                    plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
                }
                CryptographicOperations.ZeroMemory(encryptionKey);
                string json = Encoding.UTF8.GetString(plaintext);
                CryptographicOperations.ZeroMemory(plaintext);
                decoded = new DecodedSaveEnvelope(payloadType, schemaVersion, timestamp, json);
                return true;
            } catch (Exception exception) when (
                exception is CryptographicException
                || exception is InvalidDataException
                || exception is EndOfStreamException
                || exception is DecoderFallbackException
                || exception is ArgumentException) {
                error = exception.Message;
                return false;
            }
        }

        private static byte[] DeriveKey(byte[] masterKey, byte[] context) {
            byte[] material = new byte[masterKey.Length + context.Length];
            Buffer.BlockCopy(masterKey, 0, material, 0, masterKey.Length);
            Buffer.BlockCopy(context, 0, material, masterKey.Length, context.Length);
            byte[] result = SHA256.HashData(material);
            CryptographicOperations.ZeroMemory(material);
            return result;
        }

        private static void ValidateMasterKey(byte[] masterKey) {
            if (masterKey == null || masterKey.Length < 32) {
                throw new ArgumentException("A 256-bit save master key is required.", nameof(masterKey));
            }
        }
    }

    public interface ISaveKeyProvider {
        byte[] GetOrCreateKey();
    }

    public sealed class FileSaveKeyProvider : ISaveKeyProvider {
        private readonly string _keyPath;

        public FileSaveKeyProvider(string keyPath) {
            _keyPath = keyPath ?? throw new ArgumentNullException(nameof(keyPath));
        }

        public byte[] GetOrCreateKey() {
            if (File.Exists(_keyPath)) {
                byte[] existing = File.ReadAllBytes(_keyPath);
                if (existing.Length == 32) return existing;
                throw new InvalidDataException("The local save key is invalid. Existing saves cannot be opened safely.");
            }

            string directory = Path.GetDirectoryName(_keyPath) ?? throw new InvalidDataException("Save key path has no directory.");
            Directory.CreateDirectory(directory);
            byte[] key = RandomNumberGenerator.GetBytes(32);
            string temporaryPath = _keyPath + ".tmp";
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                stream.Write(key, 0, key.Length);
                stream.Flush(true);
            }
            File.Move(temporaryPath, _keyPath, true);
            return key;
        }
    }

    public static class AtomicSaveStore {
        public static void Write(string path, byte[] data, Func<byte[], bool> validator) {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is required.", nameof(path));
            if (data == null || data.Length == 0) throw new ArgumentException("Save data is empty.", nameof(data));
            string directory = Path.GetDirectoryName(path) ?? throw new InvalidDataException("Save path has no directory.");
            Directory.CreateDirectory(directory);
            string temporaryPath = path + ".tmp";
            string backupPath = path + ".bak";
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                stream.Write(data, 0, data.Length);
                stream.Flush(true);
            }
            byte[] written = File.ReadAllBytes(temporaryPath);
            if (validator != null && !validator(written)) {
                throw new InvalidDataException("Temporary save verification failed; the previous save was preserved.");
            }
            // M-21: rotate the backup only when the outgoing primary verifies.
            // Unconditional rotation copied an unverifiable primary over the
            // last-known-good backup — right after a backup-based recovery, one
            // more corruption in that window lost the slot. ADR 0004 promises
            // "last-known-good" rotation; verification is cheap at save cadence.
            if (File.Exists(path)) {
                bool primaryVerifies;
                try {
                    byte[] currentPrimary = File.ReadAllBytes(path);
                    primaryVerifies = validator == null || validator(currentPrimary);
                } catch (IOException) {
                    primaryVerifies = false;
                }
                if (primaryVerifies) File.Copy(path, backupPath, true);
            }
            File.Move(temporaryPath, path, true);
        }

        public static IEnumerable<(string Path, bool IsBackup)> ReadCandidates(string path) {
            if (File.Exists(path)) yield return (path, false);
            string backupPath = path + ".bak";
            if (File.Exists(backupPath)) yield return (backupPath, true);
        }

        public static void DeleteAllCandidates(string path) {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is required.", nameof(path));
            string[] candidates = {
                path,
                path + ".bak",
                path + ".tmp",
                path + ".corrupt",
                path + ".bak.corrupt"
            };
            foreach (string candidate in candidates) {
                if (File.Exists(candidate)) File.Delete(candidate);
            }
        }
    }

    public sealed class SaveVersionException : Exception {
        public SaveVersionException(string message) : base(message) { }
    }

    public static class SaveSchemaMigrator {
        /// <summary>
        /// Shared by both payloads. v4 (Package 8 A4) is the first schema step the
        /// <b>global</b> payload has ever needed: <c>InputBindings</c> changed from a
        /// dead <c>Dictionary&lt;string,string&gt;</c> to a structured
        /// <see cref="InputBindingSet"/>. Story saves have no v3→v4 work.
        /// v5 (V7.3) is purely additive on the story payload — per-attempt
        /// mid-level resume state (activated checkpoints, Restoration Font
        /// uses, destroyed extractors, found secrets, live Timeline Integrity)
        /// plus the viewed-dialogue and collapse-beat flags — so v4 payloads
        /// load with field-initializer defaults and need no migration step.
        /// P11 A10's <c>ClaimedRewardSourceIDs</c> joins that same additive
        /// per-attempt group (field initializer plus a <c>Normalize</c> null
        /// guard), so it needs no version step either.
        ///
        /// <para>v6 (Package 11, Phase C) is the package's single schema bump.
        /// Every workstream added its fields additively, so the bulk of the step
        /// is covered by field initializers; what v6 actually does is compose the
        /// handful of derivations the workstreams wrote —
        /// <see cref="MigrateStoryToV6"/> and <see cref="MigrateGlobalToV6"/>.
        /// <b>The version counter is shared by both payloads</b>, so a global-only
        /// change also bumps story payloads; splitting the two counters was
        /// considered and deliberately deferred (plan §6 item 1).</para>
        ///
        /// <para><b>Runtime note.</b> <see cref="DeserializeStory"/> reads authored
        /// Resonance grids through the A4 refund, so it needs the Godot runtime.
        /// Every caller is either an autoload or a <c>[RequireGodotRuntime]</c>
        /// suite; never call it from a pure-C# GdUnit suite (a Godot file read
        /// there is an uncatchable access violation — CLAUDE.md failure
        /// signature A6b).</para>
        ///
        /// <para>v7 (Package 12, Phase C) is again the package's single bump, and
        /// again composes only the derivations the workstreams declared —
        /// <see cref="MigrateStoryToV7"/> (W2's H02 completion reconciliation and
        /// W6's <c>LowestDifficultyUsed</c> seed) and <see cref="MigrateGlobalToV7"/>
        /// (W5's hazard toggle). Every other Package 12 field is additive with a
        /// field initializer equal to its legacy meaning. The counter is still
        /// shared (<c>DEFER-SAVE-VERSION-SPLIT</c>).</para>
        /// </summary>
        public const int CurrentVersion = 7;

        public static StorySaveData DeserializeStory(string json) => DeserializeStory(json, out _);

        /// <param name="loadedVersion">The schema version the payload was written at,
        /// before migration. Callers that need to run an out-of-band step (or persist
        /// the upgraded payload) read it from here rather than from the returned data,
        /// whose <c>SaveVersion</c> is always <see cref="CurrentVersion"/>.</param>
        public static StorySaveData DeserializeStory(string json, out int loadedVersion) {
            JObject root = JObject.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            int version = ReadVersion(root);
            loadedVersion = version;
            RejectFutureVersion(version);
            if (version < 2) MigrateLegacyStory(root);
            if (version < 3) MigratePuzzleState(root);
            if (version < 6) MigrateStoryCheckpointIntegrity(root);
            root[nameof(StorySaveData.SaveVersion)] = CurrentVersion;
            StorySaveData data = root.ToObject<StorySaveData>() ?? new StorySaveData();
            // The H02 reconciliation has to see the payload before the v6 step's
            // MigrateFromLegacyRoot mints an attempt ID — the unminted record is
            // exactly the shape it recognises (W2 handoff).
            if (version < 7) ReconcileStoryCompletionForV7(data);
            if (version < 6) MigrateStoryToV6(data);
            if (version < 7) MigrateStoryToV7(data);
            data.Normalize();
            return data;
        }

        public static GlobalSaveData DeserializeGlobal(string json) => DeserializeGlobal(json, out _);

        /// <param name="loadedVersion">See <see cref="DeserializeStory(string, out int)"/>.
        /// <see cref="SaveManager"/> uses it to decide whether to run
        /// <see cref="SeedGlobalSeenDialogue"/>, which cannot run here because it
        /// needs the story slots and the global payload loads first.</param>
        public static GlobalSaveData DeserializeGlobal(string json, out int loadedVersion) {
            JObject root = JObject.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            int version = ReadVersion(root);
            loadedVersion = version;
            RejectFutureVersion(version);
            if (version < 4) MigrateGlobalInputBindings(root);
            root[nameof(GlobalSaveData.SaveVersion)] = CurrentVersion;
            GlobalSaveData data = root.ToObject<GlobalSaveData>() ?? new GlobalSaveData();
            if (version < 6) MigrateGlobalToV6(data);
            if (version < 7) MigrateGlobalToV7(data);
            data.Normalize();
            return data;
        }

        private static int ReadVersion(JObject root) =>
            root.Value<int?>(nameof(StorySaveData.SaveVersion))
            ?? root.Value<int?>("saveVersion")
            ?? 0;

        private static void RejectFutureVersion(int version) {
            if (version > CurrentVersion) {
                throw new SaveVersionException($"This save was created with newer schema version {version}.");
            }
        }

        private static void MigrateLegacyStory(JObject root) {
            string characterID = root.Value<string>("CharacterID") ?? root.Value<string>("selectedCharacterID") ?? "";
            root[nameof(StorySaveData.SelectedCharacterID)] = characterID;
            int levelIndex = root.Value<int?>("CurrentLevelIndex") ?? 0;
            root[nameof(StorySaveData.CurrentLevelID)] = StoryManager.GetLevelScenePath((CampaignLevel)levelIndex);
            root[nameof(StorySaveData.LevelChronalDust)] = root.Value<int?>("ChronalDust") ?? 0;
            root[nameof(StorySaveData.PlayTimeSeconds)] = root.Value<float?>("PlaytimeSeconds") ?? 0f;
            long unixTimestamp = root.Value<long?>("LastSaveTimestamp") ?? 0L;
            root[nameof(StorySaveData.LastSavedTimestamp)] = unixTimestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime.ToString("O")
                : "";

            int deposited = root.Value<int?>("DepositedChronalDust") ?? 0;
            var depositedMap = new JObject();
            if (!string.IsNullOrWhiteSpace(characterID)) depositedMap[characterID] = deposited;
            root[nameof(StorySaveData.DepositedChronalDust)] = depositedMap;

            var progress = new JObject();
            JToken legacyNodes = root["UnlockedResonanceNodes"] ?? new JArray();
            if (!string.IsNullOrWhiteSpace(characterID)) progress[characterID] = legacyNodes.DeepClone();
            root[nameof(StorySaveData.GridProgress)] = progress;
        }

        private static void MigratePuzzleState(JObject root) {
            root[nameof(StorySaveData.CompletedPuzzleIDs)] ??= new JArray();
        }

        /// <summary>
        /// Global v3 → v4 (Package 8 A4). Pre-v4 payloads carry
        /// <c>InputBindings</c> as a flat action → string map. That field was never
        /// written by any code path and cannot express a multi-event binding, so the
        /// old shape is dropped rather than guessed at; the player keeps the project
        /// defaults and can rebind. A payload already carrying the structured shape
        /// (an <c>Actions</c> object) is left untouched.
        /// </summary>
        /// <summary>
        /// Story v5 → v6, JSON half. <c>CheckpointIntegrityPercent</c> (A3's F11
        /// paid-recovery allowance) is seeded from the live gauge the payload
        /// actually recorded — <b>with no invention</b>. A payload that carries
        /// neither field keeps the 100 field initializer, which is the entrance
        /// allowance F11 specifies.
        ///
        /// <para>It has to happen on the JObject rather than on the object,
        /// because after deserialization an absent key and an authored 100 are
        /// indistinguishable.</para>
        ///
        /// <para><b>Recorded caveat.</b> <c>IntegrityByLevel</c> (the per-completed-
        /// level record N05 averages) is re-scoped by F01 to mean the
        /// <i>PreBoss-locked</i> gauge, not the value at the moment of completion.
        /// A pre-v6 payload's rows were written under the old meaning and are
        /// carried across unchanged: re-deriving them is impossible from the save
        /// alone, and discarding them would silently shrink N05's denominator.
        /// Recorded in the deviation ledger rather than guessed at.</para>
        /// </summary>
        private static void MigrateStoryCheckpointIntegrity(JObject root) {
            const string allowance = nameof(StorySaveData.CheckpointIntegrityPercent);
            if (root[allowance] != null) return;
            float? live = root.Value<float?>(nameof(StorySaveData.LevelIntegrityPercent));
            if (live.HasValue) root[allowance] = live.Value;
        }

        /// <summary>
        /// Story v5 → v6, object half — the single step, composing the derivations
        /// the Package 11 workstreams wrote. Every other new story field is purely
        /// additive and loads on its field initializer.
        ///
        /// <list type="number">
        /// <item><b>A3b, F10.</b> <see cref="StoryAttemptState.MigrateFromLegacyRoot"/>
        /// derives the attempt record from the six loose V7.3 fields. A payload
        /// parked mid-level cannot have its anchors, Defy or reward claims
        /// reconstructed, so that function preserves it untouched and marks it
        /// <see cref="StoryAttemptStatus.LegacyRecoveryRequired"/>. F10's hard rule
        /// — <i>never default an active legacy attempt to full anchors / unused
        /// Defy / unused healing / unclaimed rewards</i> — is honoured there and
        /// re-honoured here: the root <c>AnchorCharges</c> mirror is copied from
        /// the record, never seeded from a difficulty cap.</item>
        /// <item><b>A5, Legacy Unlock Schedule.</b> Backfilled from
        /// <c>CompletedLevels</c> for the save's own character, and only when that
        /// character has no entry yet — a payload that already carries the map
        /// keeps it verbatim.</item>
        /// <item><b>A4, F08.</b> The grid refund drops any purchased node the V7.6
        /// topologies retired and refunds its recorded price into that character's
        /// deposited balance. Idempotent.</item>
        /// <item><b>A1b / A2 / A8 / A10</b> — <c>StoryDefyHistoryUsed</c> (false =
        /// an unused Defy, the conservative default), <c>TimeFreezeCooldownSeconds</c>
        /// (0 = Ready), <c>ClaimedRewardSourceIDs</c> — are additive and need no
        /// step.</item>
        /// </list>
        /// </summary>
        private static void MigrateStoryToV6(StorySaveData data) {
            if (data == null) return;

            StoryAttemptState.MigrateFromLegacyRoot(data);
            data.AttemptState ??= new StoryAttemptState();
            data.AnchorCharges = data.AttemptState.AnchorChargesRemaining;

            string characterID = data.SelectedCharacterID;
            data.UnlockedLegacyAbilities ??= new Dictionary<string, List<string>>();
            if (!string.IsNullOrWhiteSpace(characterID)
                && !data.UnlockedLegacyAbilities.ContainsKey(characterID)) {
                data.UnlockedLegacyAbilities[characterID] =
                    LegacyUnlockSchedule.UnlockedKeysFor(data.CompletedLevels ?? new List<string>());
            }

            SaveManager.MigrateResonanceGridsToV76(data);
        }

        /// <summary>
        /// Global v5 → v6. Two real derivations, two recorded no-ops.
        ///
        /// <list type="bullet">
        /// <item><b>A2.</b> A saved <c>gameplay_rewind</c> override moves onto
        /// <c>gameplay_time_freeze</c>; an explicit newer Time Freeze bind always
        /// wins, and the dead row is dropped either way.</item>
        /// <item><b>A1c, F21.</b> A saved Hybrid mode (the retired ordinal 2)
        /// normalizes to timed Stock with a valid positive timer.</item>
        /// <item><b>A8, C01a.</b> <c>ReducedTemporalEffects</c> defaults false —
        /// Off, never inferred from Screen Shake — on its field initializer.</item>
        /// <item><b>A8/A1c, C01c.</b> The per-device shortcut flags default
        /// <b>On</b> by construction: <see cref="InputBindingSet.IsShortcutEnabled"/>
        /// returns true for an absent key, so an empty dictionary already
        /// reproduces today's chords. <c>UnboundActions</c> likewise defaults
        /// empty, meaning "nothing explicitly unbound".</item>
        /// </list>
        ///
        /// <para><c>SeenDialogueIDs</c> is <b>not</b> seeded here — it is the union
        /// of every story slot's <c>ViewedDialogueIDs</c> and the global payload
        /// loads before the slots. <see cref="SeedGlobalSeenDialogue"/> runs it
        /// from <see cref="SaveManager"/> once the slots are in.</para>
        /// </summary>
        private static void MigrateGlobalToV6(GlobalSaveData data) {
            if (data == null) return;
            data.InputBindings ??= new InputBindingSet();
            data.InputBindings.MigrateLegacyRewindAction();
            data.LastMatchSettings ??= new SavedMatchSettings();
            data.LastMatchSettings.Normalize();
        }

        /// <summary>
        /// Story v6 → v7, first half (Package 12 W2, H02). A payload saved after a
        /// level completed but before the hub loaded still holds that level's
        /// earnings in <c>LevelChronalDust</c>: v6 relied on the hub's arrival
        /// deposit, which H02 deleted. <see cref="StoryAttemptState.ReconcileUndepositedCompletion"/>
        /// banks it exactly once. It runs <b>before</b> the v6 step so that a pre-v6
        /// payload is seen while its attempt record is still unminted. An open
        /// attempt's held dust (a Collapse or an exit parked in the hub) is never
        /// banked. Idempotent: the wallet is zeroed in the same step.
        /// </summary>
        private static void ReconcileStoryCompletionForV7(StorySaveData data) {
            if (data == null) return;
            StoryAttemptState.ReconcileUndepositedCompletion(data);
        }

        /// <summary>
        /// Story v6 → v7, second half (Package 12 W6, G14).
        /// <c>LowestDifficultyUsed</c> is seeded from <c>Difficulty</c>
        /// (<see cref="CampaignDifficultyRules.SeedLowestDifficultyUsed"/>), so a
        /// payload written before the field existed persists its own tier rather
        /// than the Hard initializer. Idempotent; never raises the value.
        /// </summary>
        private static void MigrateStoryToV7(StorySaveData data) {
            if (data == null) return;
            CampaignDifficultyRules.SeedLowestDifficultyUsed(data);
        }

        /// <summary>
        /// Global v6 → v7 (Package 12 W5). The retired hazard-frequency selector
        /// becomes the On/Off toggle: <c>HazardRate</c> Off → false, anything else →
        /// true (<see cref="SavedMatchSettings.DeriveStageHazardsEnabled"/>). An
        /// explicit toggle already on the payload is never overwritten. Every W6
        /// global field (comfort, input, dialogue, crash-report, mute, stick
        /// profiles) and W5's <c>PlayerSlotPalette</c> / <c>MeterPickupsEnabled</c>
        /// load on field initializers equal to their legacy meaning and need only
        /// <see cref="GlobalSaveData.Normalize"/>.
        /// </summary>
        private static void MigrateGlobalToV7(GlobalSaveData data) {
            if (data == null) return;
            data.LastMatchSettings ??= new SavedMatchSettings();
            data.LastMatchSettings.StageHazardsEnabled ??=
                SavedMatchSettings.DeriveStageHazardsEnabled(data.LastMatchSettings.HazardRate);
        }

        /// <summary>
        /// Global v5 → v6, deferred half (plan §6 item 1). Unions every story
        /// slot's per-slot <c>ViewedDialogueIDs</c> into the new global
        /// <c>SeenDialogueIDs</c>, so a player who has already watched a scene on
        /// one slot is not asked "skip this? it won't replay" again on another.
        ///
        /// <para>Idempotent and additive: it only ever adds IDs the player
        /// genuinely finished, and the global set is never cleared by a level
        /// restart or a slot change. Null slots are ignored.</para>
        /// </summary>
        /// <returns>The number of IDs actually added.</returns>
        public static int SeedGlobalSeenDialogue(GlobalSaveData global, IEnumerable<StorySaveData> slots) {
            if (global == null || slots == null) return 0;
            global.SeenDialogueIDs ??= new HashSet<string>(StringComparer.Ordinal);
            int added = 0;
            foreach (StorySaveData slot in slots) {
                if (slot?.ViewedDialogueIDs == null) continue;
                foreach (string sequenceID in slot.ViewedDialogueIDs) {
                    if (string.IsNullOrWhiteSpace(sequenceID)) continue;
                    if (global.SeenDialogueIDs.Add(sequenceID)) added++;
                }
            }
            return added;
        }

        private static void MigrateGlobalInputBindings(JObject root) {
            const string field = nameof(GlobalSaveData.InputBindings);
            JToken existing = root[field];
            if (existing is JObject structured
                && structured[nameof(InputBindingSet.Actions)] is JObject) {
                return;
            }
            root[field] = new JObject {
                [nameof(InputBindingSet.Actions)] = new JObject()
            };
        }
    }
}
