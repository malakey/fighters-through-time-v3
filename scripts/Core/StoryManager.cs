using Godot;

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

        private static readonly string[] LevelScenePaths = {
            "res://scenes/campaign/Level_00_Tutorial.tscn",
            "res://scenes/campaign/Level_01_Florence.tscn",
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

            LoadCurrentLevel();
        }

        public void LoadCurrentLevel() {
            int levelIndex = (int)CurrentLevel;
            if (levelIndex < LevelScenePaths.Length) {
                GameManager.Instance.LoadScene(LevelScenePaths[levelIndex]);
            }
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
            int idx = (int)CurrentLevel;
            return idx < LevelScenePaths.Length ? LevelScenePaths[idx] : "";
        }

        public void CollectDust(int amount) {
            ChronalDustCollected += amount;
        }

        private void OnDustCollected(int amount) {
            CollectDust(amount);
        }

        private void OnLevelComplete(string levelID) {
            AdvanceToNextLevel();
        }
    }
}
