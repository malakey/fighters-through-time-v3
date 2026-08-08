using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace FTT.Core {

    public class StorySaveData {
        public int SaveVersion = SaveSchemaMigrator.CurrentVersion;
        public string SelectedCharacterID = "";
        public Difficulty Difficulty = Difficulty.Normal;
        public string CurrentLevelID = "res://scenes/campaign/Level_00_Tutorial.tscn";
        public string LastCheckpointID = "";
        public string LastViewedDialogueID = "";
        public int CurrentHP = 100;
        public int CurrentLives = 3;
        public float CurrentUltimateMeter;
        public List<string> CompletedLevels = new();
        public Dictionary<string, int> DepositedChronalDust = new();
        public int LevelChronalDust;
        public Dictionary<string, List<string>> GridProgress = new();
        public List<string> CompletedPuzzleIDs = new();
        public float PlayTimeSeconds;
        public bool IsCompleted;
        public string LastSavedTimestamp = "";

        public void Normalize() {
            SaveVersion = SaveSchemaMigrator.CurrentVersion;
            SelectedCharacterID ??= "";
            CurrentLevelID ??= "res://scenes/campaign/Level_00_Tutorial.tscn";
            LastCheckpointID ??= "";
            LastViewedDialogueID ??= "";
            CompletedLevels ??= new List<string>();
            DepositedChronalDust ??= new Dictionary<string, int>();
            GridProgress ??= new Dictionary<string, List<string>>();
            CompletedPuzzleIDs ??= new List<string>();
            LastSavedTimestamp ??= "";
            CurrentHP = Math.Max(0, CurrentHP);
            CurrentLives = Math.Max(0, CurrentLives);
            CurrentUltimateMeter = Math.Clamp(CurrentUltimateMeter, 0f, 100f);
            LevelChronalDust = Math.Max(0, LevelChronalDust);
        }
    }

    public class GlobalSaveData {
        public static readonly string[] InitialStageIDs = {
            "florence_workshop", "orleans_vanguard", "chicago_exposition", "paris_bastille",
            "vesuvius_caldera", "nassau_flagship", "alexandria_chambers", "berlin_wall",
            "globe_theatre", "gettysburg_ridge"
        };

        public int SaveVersion = SaveSchemaMigrator.CurrentVersion;
        public List<string> UnlockedCharacters = new() {
            "einstein", "joan", "leonardo", "lincoln", "cleopatra",
            "tesla", "shakespeare", "mozart", "pocahontas"
        };
        public List<string> UnlockedStages = new(InitialStageIDs);
        public int TotalPlayTime;
        public int TotalWins;
        public int TotalLosses;
        public Dictionary<string, int> CharacterWins = new();
        public Dictionary<string, int> CharacterLosses = new();
        public float MasterVolume = 1.0f;
        public float MusicVolume = 0.8f;
        public float SFXVolume = 1.0f;
        public float UIVolume = 1.0f;
        public float HapticIntensity = 0.7f;
        public bool HapticsEnabled = true;
        public bool DamageNumbersVisible = true;
        public float HudOpacity = 1f;
        public float ScreenShakeScale = 1f;
        public Dictionary<string, string> InputBindings = new();

        public void Normalize() {
            SaveVersion = SaveSchemaMigrator.CurrentVersion;
            UnlockedCharacters ??= new List<string>();
            UnlockedStages ??= new List<string>();
            foreach (string stageID in InitialStageIDs) {
                if (!UnlockedStages.Contains(stageID)) UnlockedStages.Add(stageID);
            }
            CharacterWins ??= new Dictionary<string, int>();
            CharacterLosses ??= new Dictionary<string, int>();
            InputBindings ??= new Dictionary<string, string>();
            MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
            MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
            SFXVolume = Math.Clamp(SFXVolume, 0f, 1f);
            UIVolume = Math.Clamp(UIVolume, 0f, 1f);
            HapticIntensity = Math.Clamp(HapticIntensity, 0f, 1f);
            HudOpacity = Math.Clamp(HudOpacity, 0.2f, 1f);
            ScreenShakeScale = Math.Clamp(ScreenShakeScale, 0f, 1f);
        }
    }

    public partial class SaveManager : Node {
        public static SaveManager Instance { get; private set; }

        private const string SaveDir = "user://saves/";
        private const string GlobalFileName = "global.sav";
        private ISaveKeyProvider _keyProvider;
        private byte[] _masterKey;

        public StorySaveData[] SaveSlots = new StorySaveData[3];
        public GlobalSaveData GlobalData = new();
        public string LastLoadNotice { get; private set; } = "";

        public override void _Ready() {
            Instance = this;
            string saveDirectory = ProjectSettings.GlobalizePath(SaveDir);
            Directory.CreateDirectory(saveDirectory);
            _keyProvider = new FileSaveKeyProvider(Path.Combine(saveDirectory, ".savekey"));
            _masterKey = _keyProvider.GetOrCreateKey();
            LoadGlobalData();
            for (int slot = 0; slot < SaveSlots.Length; slot++) LoadStorySlot(slot);
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += SaveCheckpoint;
                EventBus.Instance.OnLevelComplete += SaveLevelCompletion;
                EventBus.Instance.OnTalentNodeUnlocked += SaveTalentUnlock;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= SaveCheckpoint;
                EventBus.Instance.OnLevelComplete -= SaveLevelCompletion;
                EventBus.Instance.OnTalentNodeUnlocked -= SaveTalentUnlock;
            }
            if (_masterKey != null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(_masterKey);
            if (Instance == this) Instance = null;
        }

        public StorySaveData CreateStorySlot(int slotIndex, string characterID, Difficulty difficulty) {
            ValidateSlot(slotIndex);
            var data = new StorySaveData {
                SelectedCharacterID = characterID?.Trim().ToLowerInvariant() ?? "",
                Difficulty = difficulty,
                CurrentLives = difficulty == Difficulty.Easy ? 5 : difficulty == Difficulty.Hard ? 1 : 3,
                LastSavedTimestamp = DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
            };
            SaveSlots[slotIndex] = data;
            SaveStorySlot(slotIndex);
            return data;
        }

        public bool SaveStorySlot(int slotIndex) {
            ValidateSlot(slotIndex);
            StorySaveData data = SaveSlots[slotIndex];
            if (data == null) return false;
            data.Normalize();
            data.LastSavedTimestamp = DateTimeOffset.UtcNow.UtcDateTime.ToString("O");
            return WritePayload(GetStoryPath(slotIndex), "story", data.SaveVersion, JsonConvert.SerializeObject(data));
        }

        public bool DeleteStorySlot(int slotIndex) {
            ValidateSlot(slotIndex);
            try {
                AtomicSaveStore.DeleteAllCandidates(GetStoryPath(slotIndex));
                SaveSlots[slotIndex] = null;
                if (GameManager.Instance != null && GameManager.Instance.CurrentSession.ActiveSaveSlot == slotIndex) {
                    SessionData session = GameManager.Instance.CurrentSession;
                    session.ActiveSaveSlot = -1;
                    GameManager.Instance.CurrentSession = session;
                }
                LastLoadNotice = "";
                return true;
            } catch (IOException exception) {
                LastLoadNotice = $"Unable to delete story slot {slotIndex + 1}: {exception.Message}";
                GD.PushError(LastLoadNotice);
                return false;
            } catch (UnauthorizedAccessException exception) {
                LastLoadNotice = $"Unable to delete story slot {slotIndex + 1}: {exception.Message}";
                GD.PushError(LastLoadNotice);
                return false;
            }
        }

        public bool LoadStorySlot(int slotIndex) {
            ValidateSlot(slotIndex);
            string path = GetStoryPath(slotIndex);
            if (!File.Exists(path) && !File.Exists(path + ".bak")) {
                SaveSlots[slotIndex] = null;
                return false;
            }
            if (TryLoadPayload(path, "story", out string json, out bool backupUsed)) {
                try {
                    SaveSlots[slotIndex] = SaveSchemaMigrator.DeserializeStory(json);
                    if (backupUsed) LastLoadNotice = $"Story slot {slotIndex + 1} was recovered from backup.";
                    return true;
                } catch (SaveVersionException exception) {
                    LastLoadNotice = exception.Message;
                    return false;
                } catch (JsonException exception) {
                    LastLoadNotice = exception.Message;
                }
            }

            try {
                if (TryLoadLegacyStory(path, out StorySaveData migrated)) {
                    SaveSlots[slotIndex] = migrated;
                    SaveStorySlot(slotIndex);
                    LastLoadNotice = $"Story slot {slotIndex + 1} was migrated to the secure save format.";
                    return true;
                }
            } catch (SaveVersionException exception) {
                LastLoadNotice = exception.Message;
                return false;
            }
            SaveSlots[slotIndex] = null;
            PreserveCorruptCandidates(path);
            LastLoadNotice = $"Story slot {slotIndex + 1} is corrupted and could not be recovered.";
            return false;
        }

        public bool SaveGlobalData() {
            GlobalData ??= new GlobalSaveData();
            GlobalData.Normalize();
            return WritePayload(GetGlobalPath(), "global", GlobalData.SaveVersion, JsonConvert.SerializeObject(GlobalData));
        }

        public bool LoadGlobalData() {
            string path = GetGlobalPath();
            if (!File.Exists(path) && !File.Exists(path + ".bak")) {
                GlobalData = new GlobalSaveData();
                return false;
            }
            if (TryLoadPayload(path, "global", out string json, out bool backupUsed)) {
                try {
                    GlobalData = SaveSchemaMigrator.DeserializeGlobal(json);
                    if (backupUsed) LastLoadNotice = "Global settings were recovered from backup.";
                    return true;
                } catch (SaveVersionException exception) {
                    LastLoadNotice = exception.Message;
                    return false;
                } catch (JsonException exception) {
                    LastLoadNotice = exception.Message;
                }
            }

            try {
                if (TryLoadLegacyGlobal(path, out GlobalSaveData migrated)) {
                    GlobalData = migrated;
                    SaveGlobalData();
                    LastLoadNotice = "Global settings were migrated to the secure save format.";
                    return true;
                }
            } catch (SaveVersionException exception) {
                LastLoadNotice = exception.Message;
                return false;
            }
            GlobalData = new GlobalSaveData();
            PreserveCorruptCandidates(path);
            LastLoadNotice = "Global settings were corrupted and reset to defaults.";
            return false;
        }

        public void SaveCheckpoint(string checkpointID) {
            if (GameManager.Instance == null) return;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveSlots.Length) return;
            SaveSlots[slot] ??= new StorySaveData {
                SelectedCharacterID = GameManager.Instance.CurrentSession.SelectedCharacterID ?? "",
                Difficulty = GameManager.Instance.CurrentSession.Difficulty
            };
            SaveSlots[slot].LastCheckpointID = checkpointID ?? "";
            SaveSlots[slot].CurrentLevelID = StoryManager.Instance?.GetCurrentLevelPath()
                ?? SaveSlots[slot].CurrentLevelID;
            SaveSlots[slot].CurrentLives = StoryManager.Instance?.ChronalRewindsRemaining
                ?? SaveSlots[slot].CurrentLives;
            SaveSlots[slot].LevelChronalDust = StoryManager.Instance?.ChronalDustCollected
                ?? SaveSlots[slot].LevelChronalDust;
            if (GetTree().GetFirstNodeInGroup("StoryPlayer") is FTT.Characters.PlayerController player) {
                SaveSlots[slot].CurrentHP = player.CurrentHP;
                SaveSlots[slot].CurrentUltimateMeter = player.CurrentUltimateMeter;
            }
            SaveStorySlot(slot);
        }

        /// <summary>
        /// Designed autosave trigger: Resonance Grid unlocks persist to the
        /// active story slot immediately (AGENTS.md: "Autosave occurs at
        /// checkpoints, level completion, and unlock events").
        /// </summary>
        public void SaveTalentUnlock(string nodeID) {
            if (GameManager.Instance == null) return;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveSlots.Length || SaveSlots[slot] == null) return;
            SaveStorySlot(slot);
        }

        /// <summary>
        /// Campaign completion (Package 5 A1). Level 15's ending chain calls this
        /// once the restoration sequence and credits finish: it is the only writer
        /// of <see cref="StorySaveData.IsCompleted"/>, so UI code never pokes the
        /// field directly. Returns false when there is no active story slot.
        /// </summary>
        public bool MarkCampaignCompleted() {
            if (GameManager.Instance == null) return false;
            return MarkCampaignCompleted(GameManager.Instance.CurrentSession.ActiveSaveSlot);
        }

        /// <summary>Slot-explicit campaign completion; persists the slot immediately.</summary>
        public bool MarkCampaignCompleted(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= SaveSlots.Length) return false;
            StorySaveData save = SaveSlots[slotIndex];
            if (save == null) return false;
            save.IsCompleted = true;
            return SaveStorySlot(slotIndex);
        }

        /// <summary>True when the active story slot has finished the campaign.</summary>
        public bool IsActiveCampaignCompleted() => GetActiveStorySave()?.IsCompleted == true;

        public bool IsPuzzleCompleted(string puzzleID) {
            StorySaveData save = GetActiveStorySave();
            return save?.CompletedPuzzleIDs?.Contains(puzzleID) == true;
        }

        public void SetPuzzleCompleted(string puzzleID, bool completed) {
            if (string.IsNullOrWhiteSpace(puzzleID)) return;
            StorySaveData save = GetActiveStorySave(createIfMissing: true);
            if (save == null) return;
            if (completed) {
                if (!save.CompletedPuzzleIDs.Contains(puzzleID)) save.CompletedPuzzleIDs.Add(puzzleID);
            } else {
                save.CompletedPuzzleIDs.Remove(puzzleID);
            }
        }

        private StorySaveData GetActiveStorySave(bool createIfMissing = false) {
            if (GameManager.Instance == null) return null;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveSlots.Length) return null;
            if (SaveSlots[slot] == null && createIfMissing) {
                SaveSlots[slot] = new StorySaveData {
                    SelectedCharacterID = GameManager.Instance.CurrentSession.SelectedCharacterID ?? "",
                    Difficulty = GameManager.Instance.CurrentSession.Difficulty
                };
            }
            return SaveSlots[slot];
        }

        public void SaveLevelCompletion(string completedLevelID) {
            if (GameManager.Instance == null) return;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveSlots.Length || SaveSlots[slot] == null) return;
            if (!SaveSlots[slot].CompletedLevels.Contains(completedLevelID)) {
                SaveSlots[slot].CompletedLevels.Add(completedLevelID);
            }
            SaveSlots[slot].CurrentLevelID = StoryManager.Instance?.GetCurrentLevelPath()
                ?? SaveSlots[slot].CurrentLevelID;
            SaveSlots[slot].LevelChronalDust = StoryManager.Instance?.ChronalDustCollected
                ?? SaveSlots[slot].LevelChronalDust;
            SaveStorySlot(slot);
        }

        private bool WritePayload(string path, string payloadType, int schemaVersion, string json) {
            try {
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                byte[] encoded = SaveEnvelopeCodec.Encode(payloadType, schemaVersion, json, _masterKey, timestamp);
                AtomicSaveStore.Write(path, encoded, candidate =>
                    SaveEnvelopeCodec.TryDecode(candidate, _masterKey, out DecodedSaveEnvelope decoded, out _)
                    && decoded.PayloadType == payloadType);
                return true;
            } catch (Exception exception) {
                GD.PushError($"Unable to save {payloadType} data: {exception.Message}");
                return false;
            }
        }

        private bool TryLoadPayload(string path, string expectedPayloadType, out string json, out bool backupUsed) {
            json = "";
            backupUsed = false;
            foreach ((string candidatePath, bool isBackup) in AtomicSaveStore.ReadCandidates(path)) {
                try {
                    byte[] bytes = File.ReadAllBytes(candidatePath);
                    if (!SaveEnvelopeCodec.TryDecode(bytes, _masterKey, out DecodedSaveEnvelope decoded, out _)) continue;
                    if (decoded.PayloadType != expectedPayloadType) continue;
                    json = decoded.Json;
                    backupUsed = isBackup;
                    return true;
                } catch (IOException) { }
            }
            return false;
        }

        private static bool TryLoadLegacyStory(string path, out StorySaveData data) {
            data = null;
            try {
                if (!File.Exists(path)) return false;
                string encoded = File.ReadAllText(path);
                string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                data = SaveSchemaMigrator.DeserializeStory(json);
                return true;
            } catch (Exception exception) when (exception is FormatException || exception is JsonException || exception is IOException) {
                return false;
            }
        }

        private static bool TryLoadLegacyGlobal(string path, out GlobalSaveData data) {
            data = null;
            try {
                if (!File.Exists(path)) return false;
                data = SaveSchemaMigrator.DeserializeGlobal(File.ReadAllText(path));
                return true;
            } catch (Exception exception) when (exception is JsonException || exception is IOException) {
                return false;
            }
        }

        private static void PreserveCorruptCandidates(string path) {
            foreach ((string candidatePath, _) in AtomicSaveStore.ReadCandidates(path)) {
                try {
                    string corruptPath = candidatePath + ".corrupt";
                    if (!File.Exists(corruptPath)) File.Copy(candidatePath, corruptPath);
                } catch (IOException) { }
            }
        }

        private static string GetStoryPath(int slotIndex) =>
            Path.Combine(ProjectSettings.GlobalizePath(SaveDir), $"story_slot_{slotIndex}.sav");

        private static string GetGlobalPath() =>
            Path.Combine(ProjectSettings.GlobalizePath(SaveDir), GlobalFileName);

        private static void ValidateSlot(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= 3) throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }
    }
}
