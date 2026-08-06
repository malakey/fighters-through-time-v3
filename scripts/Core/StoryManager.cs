using Godot;
using System.Collections.Generic;

namespace FTT.Core {

    public enum CampaignLevel {
        Tutorial = 0,
        Florence = 1,
        Orleans = 2,
        Chicago = 3,
        Paris = 4,
        Titanic = 5,
        Pompeii = 6,
        Nassau = 7,
        Egypt = 8,
        Berlin = 9,
        London = 10,
        Gettysburg = 11,
        Lunar = 12,
        ChronalVoid = 13,
        NeoEarth = 14,
        Alexandria = 15
    }

    public partial class StoryManager : Node {
        public static StoryManager Instance { get; private set; }

        public CampaignLevel CurrentLevel { get; private set; } = CampaignLevel.Tutorial;
        public int ChronalDustCollected { get; private set; }
        public int ChronalRewindsRemaining { get; set; } = 3;
        public bool TutorialComplete { get; set; }
        public bool HasPendingTimelineRestart { get; private set; }
        public CampaignLevel CollapsedLevel { get; private set; }
        public string CollapsedCheckpointID { get; private set; } = "";

        private static readonly string[] LevelScenePaths = {
            "res://scenes/campaign/Level_00_Tutorial.tscn",
            "res://scenes/campaign/Level_01_Florence.tscn",
            "res://scenes/campaign/Level_02_Orleans.tscn",
            "res://scenes/campaign/Level_03_Chicago.tscn",
            "res://scenes/campaign/Level_04_Paris.tscn",
            "res://scenes/campaign/Level_05_Titanic.tscn",
            "res://scenes/campaign/Level_06_Pompeii.tscn",
            "res://scenes/campaign/Level_07_Nassau.tscn",
            "res://scenes/campaign/Level_08_Egypt.tscn",
            "res://scenes/campaign/Level_09_Berlin.tscn",
            "res://scenes/campaign/Level_10_London.tscn",
            "res://scenes/campaign/Level_11_Gettysburg.tscn",
            "res://scenes/campaign/Level_12_Lunar.tscn",
            "res://scenes/campaign/Level_13_ChronalVoid.tscn",
            "res://scenes/campaign/Level_14_NeoEarth.tscn",
            "res://scenes/campaign/Level_15_Alexandria.tscn",
        };

        public override void _Ready() {
            Instance = this;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnChronalDustCollected += OnDustCollected;
                EventBus.Instance.OnLevelComplete += OnLevelComplete;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnChronalDustCollected -= OnDustCollected;
                EventBus.Instance.OnLevelComplete -= OnLevelComplete;
            }
        }

        public void StartCampaign(string characterID) {
            CurrentLevel = CampaignLevel.Tutorial;
            ChronalDustCollected = 0;
            ChronalRewindsRemaining = 3;
            TutorialComplete = false;

            var session = GameManager.Instance.CurrentSession;
            session.SelectedCharacterID = characterID;
            GameManager.Instance.CurrentSession = session;

            SaveManager.Instance?.CreateStorySlot(session.ActiveSaveSlot, characterID, session.Difficulty);

            LoadCurrentLevel();
        }

        public void ResumeCampaign(int slot, StorySaveData save) {
            if (save == null || GameManager.Instance == null) return;
            int levelIndex = 0;
            for (int index = 0; index < LevelScenePaths.Length; index++) {
                if (LevelScenePaths[index] == save.CurrentLevelID) {
                    levelIndex = index;
                    break;
                }
            }
            CurrentLevel = (CampaignLevel)levelIndex;
            ChronalDustCollected = Mathf.Max(0, save.LevelChronalDust);
            ChronalRewindsRemaining = Mathf.Max(0, save.CurrentLives);
            TutorialComplete = save.CompletedLevels.Contains("level_00_tutorial");
            SessionData session = GameManager.Instance.CurrentSession;
            session.ActiveSaveSlot = slot;
            session.SelectedCharacterID = save.SelectedCharacterID;
            session.Difficulty = save.Difficulty;
            GameManager.Instance.CurrentSession = session;
            ReturnToHub();
        }

        public void LoadCurrentLevel() {
            string path = GetCurrentLevelPath();
            if (!string.IsNullOrWhiteSpace(path) && ResourceLoader.Exists(path)) GameManager.Instance.LoadScene(path);
            else GD.PushError($"Campaign scene is not authored yet: {path}");
        }

        public void ReturnToHub() {
            GameManager.Instance.LoadScene("res://scenes/campaign/HubWorld.tscn");
        }

        public void AdvanceToNextLevel() {
            if ((int)CurrentLevel < 15) {
                CurrentLevel = (CampaignLevel)((int)CurrentLevel + 1);
            }
        }

        public string GetCurrentLevelPath() {
            return GetLevelScenePath(CurrentLevel);
        }

        public static string GetLevelScenePath(CampaignLevel level) {
            int idx = (int)level;
            return idx >= 0 && idx < LevelScenePaths.Length ? LevelScenePaths[idx] : "";
        }

        public void CollectDust(int amount) {
            ChronalDustCollected += Mathf.Max(0, amount);
        }

        public void SetRewinds(int remaining) {
            ChronalRewindsRemaining = Mathf.Max(0, remaining);
        }

        public void ApplyTimelineCollapseDustPenalty() {
            ChronalDustCollected = CalculateTimelineCollapseDust(ChronalDustCollected);
        }

        public static int CalculateTimelineCollapseDust(int carriedDust) =>
            Mathf.FloorToInt(Mathf.Max(0, carriedDust) * 0.8f);

        public void BeginTimelineCollapse(string checkpointID) {
            CollapsedLevel = CurrentLevel;
            CollapsedCheckpointID = checkpointID ?? "";
            HasPendingTimelineRestart = true;
            ApplyTimelineCollapseDustPenalty();

            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            ChronalRewindsRemaining = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
            StorySaveData save = GetActiveSave();
            if (save != null) {
                save.LevelChronalDust = ChronalDustCollected;
                save.CurrentLives = ChronalRewindsRemaining;
                save.CurrentHP = GetSelectedCharacterMaximumHP();
                if (!string.IsNullOrWhiteSpace(CollapsedCheckpointID)) save.LastCheckpointID = CollapsedCheckpointID;
                SaveManager.Instance.SaveStorySlot(GameManager.Instance.CurrentSession.ActiveSaveSlot);
            }
            ReturnToHub();
        }

        public bool RestartCollapsedLevel(bool resumeFromTimelineAnchor) {
            if (!HasPendingTimelineRestart) return false;
            CurrentLevel = CollapsedLevel;
            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            ChronalRewindsRemaining = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
            StorySaveData save = GetActiveSave();
            if (save != null) {
                save.CurrentLevelID = GetCurrentLevelPath();
                save.LastCheckpointID = resumeFromTimelineAnchor ? CollapsedCheckpointID : "";
                save.CurrentHP = GetSelectedCharacterMaximumHP();
                save.CurrentLives = ChronalRewindsRemaining;
                save.LevelChronalDust = ChronalDustCollected;
                SaveManager.Instance.SaveStorySlot(GameManager.Instance.CurrentSession.ActiveSaveSlot);
            }
            HasPendingTimelineRestart = false;
            LoadCurrentLevel();
            return true;
        }

        private StorySaveData GetActiveSave() {
            if (SaveManager.Instance == null || GameManager.Instance == null) return null;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            return slot >= 0 && slot < SaveManager.Instance.SaveSlots.Length
                ? SaveManager.Instance.SaveSlots[slot]
                : null;
        }

        private int GetSelectedCharacterMaximumHP() {
            string characterID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "";
            FTT.Characters.CharacterData data = GD.Load<FTT.Characters.CharacterData>($"res://resources/Characters/{characterID}_data.tres");
            return data?.MaxHP ?? 100;
        }

        public int DepositDustToActiveSave() {
            if (SaveManager.Instance == null || GameManager.Instance == null || ChronalDustCollected <= 0) return 0;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveManager.Instance.SaveSlots.Length) return 0;
            StorySaveData save = SaveManager.Instance.SaveSlots[slot];
            string characterID = GameManager.Instance.CurrentSession.SelectedCharacterID ?? "";
            int deposited = FTT.Environment.ResonanceProgression.DepositActiveDust(save, characterID, ChronalDustCollected);
            if (deposited <= 0) return 0;
            ChronalDustCollected -= deposited;
            EventBus.Instance?.RaiseChronalDustDeposited(
                save.DepositedChronalDust.GetValueOrDefault(characterID));
            SaveManager.Instance.SaveStorySlot(slot);
            return deposited;
        }

        private void OnDustCollected(int amount) {
            CollectDust(amount);
        }

        private void OnLevelComplete(string levelID) {
            AdvanceToNextLevel();
        }
    }
}
