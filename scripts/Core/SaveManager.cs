using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FTT.Core {

    public class StorySaveData {
        public string CharacterID = "";
        public int CurrentLevelIndex;
        public int ChronalDust;
        public int DepositedChronalDust;
        public List<string> UnlockedResonanceNodes = new();
        public string LastCheckpointID = "";
        public Difficulty Difficulty = Difficulty.Normal;
        public float PlaytimeSeconds;
        public long LastSaveTimestamp;
    }

    public class GlobalSaveData {
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
    }

    public partial class SaveManager : Node {
        public static SaveManager Instance { get; private set; }

        private const string SaveDir = "user://saves/";
        private const string GlobalSavePath = "user://saves/global.sav";
        private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("FTT_2026_CHRONAL_KEY_32B_SECURE!");

        public StorySaveData[] SaveSlots = new StorySaveData[3];
        public GlobalSaveData GlobalData = new();

        public override void _Ready() {
            Instance = this;
            EnsureSaveDirectory();
            LoadGlobalData();
        }

        private void EnsureSaveDirectory() {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(SaveDir));
        }

        public void SaveStorySlot(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= 3 || SaveSlots[slotIndex] == null) return;
            SaveSlots[slotIndex].LastSaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(SaveSlots[slotIndex]);
            string encrypted = EncryptString(json);
            string path = $"{SaveDir}story_slot_{slotIndex}.sav";
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file?.StoreString(encrypted);
        }

        public void LoadStorySlot(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= 3) return;
            string path = $"{SaveDir}story_slot_{slotIndex}.sav";
            if (!FileAccess.FileExists(path)) { SaveSlots[slotIndex] = null; return; }
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            string encrypted = file?.GetAsText() ?? "";
            string json = DecryptString(encrypted);
            SaveSlots[slotIndex] = Newtonsoft.Json.JsonConvert.DeserializeObject<StorySaveData>(json);
        }

        public void SaveGlobalData() {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(GlobalData);
            using var file = FileAccess.Open(GlobalSavePath, FileAccess.ModeFlags.Write);
            file?.StoreString(json);
        }

        public void LoadGlobalData() {
            if (!FileAccess.FileExists(GlobalSavePath)) return;
            using var file = FileAccess.Open(GlobalSavePath, FileAccess.ModeFlags.Read);
            string json = file?.GetAsText() ?? "{}";
            GlobalData = Newtonsoft.Json.JsonConvert.DeserializeObject<GlobalSaveData>(json) ?? new GlobalSaveData();
        }

        public void SaveCheckpoint(string checkpointID) {
            var session = GameManager.Instance?.CurrentSession;
            if (session == null) return;
            int slot = session.Value.ActiveSaveSlot;
            if (SaveSlots[slot] == null) SaveSlots[slot] = new StorySaveData();
            SaveSlots[slot].LastCheckpointID = checkpointID;
            SaveStorySlot(slot);
        }

        private static string EncryptString(string plainText) {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        }

        private static string DecryptString(string cipherText) {
            return Encoding.UTF8.GetString(Convert.FromBase64String(cipherText));
        }
    }
}
