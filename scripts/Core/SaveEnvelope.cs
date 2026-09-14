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
        /// </summary>
        public const int CurrentVersion = 5;

        public static StorySaveData DeserializeStory(string json) {
            JObject root = JObject.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            int version = ReadVersion(root);
            RejectFutureVersion(version);
            if (version < 2) MigrateLegacyStory(root);
            if (version < 3) MigratePuzzleState(root);
            root[nameof(StorySaveData.SaveVersion)] = CurrentVersion;
            StorySaveData data = root.ToObject<StorySaveData>() ?? new StorySaveData();
            data.Normalize();
            return data;
        }

        public static GlobalSaveData DeserializeGlobal(string json) {
            JObject root = JObject.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            int version = ReadVersion(root);
            RejectFutureVersion(version);
            if (version < 4) MigrateGlobalInputBindings(root);
            root[nameof(GlobalSaveData.SaveVersion)] = CurrentVersion;
            GlobalSaveData data = root.ToObject<GlobalSaveData>() ?? new GlobalSaveData();
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
