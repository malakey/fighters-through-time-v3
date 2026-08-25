using Godot;
using System;

namespace FTT.Core {

    public enum Difficulty {
        Easy,
        Normal,
        Hard
    }

    public enum MatchMode {
        Stock,
        TimeLimit,
        Hybrid
    }

    public enum ChronalOrbFrequency {
        Off,
        Low,
        Medium,
        High
    }

    public enum HazardTriggerFrequency {
        Off,
        Low,
        Medium,
        High
    }

    public enum FighterOpponentType {
        Cpu,
        LocalHuman,
        /// <summary>V7 initial release: direct-IP LAN 1v1 over the rollback
        /// session — the exact code path online will use.</summary>
        Lan
    }

    public enum CpuDifficulty {
        Easy,
        Normal,
        Hard
    }

    public struct MatchSettings {
        public MatchMode Mode;
        public int StockCount;
        public float TimeLimit;
        public bool ItemsEnabled;
        public ChronalOrbFrequency ItemSpawnRate;
        public bool StageHazardsEnabled;
        public HazardTriggerFrequency HazardRate;

        public static MatchSettings GetDefault() {
            return new MatchSettings {
                Mode = MatchMode.Stock,
                StockCount = 3,
                TimeLimit = 480.0f,
                ItemsEnabled = true,
                ItemSpawnRate = ChronalOrbFrequency.High,
                StageHazardsEnabled = true,
                HazardRate = HazardTriggerFrequency.High
            };
        }
    }

    public struct SessionData {
        public string SelectedCharacterID;
        public string OpponentCharacterID;
        public string SelectedStageID;
        public int ActiveSaveSlot;
        public Difficulty Difficulty;
        public MatchSettings MatchSettings;
        public FighterOpponentType FighterOpponentType;
        public CpuDifficulty CpuDifficulty;
        /// <summary>Set by the hub Holodeck so Fighter flows return to the Time-Ship instead of the main menu.</summary>
        public bool ReturnToHubAfterFighterMatch;
        /// <summary>Post-match "New Stage" (V7): re-enter CharacterSelect with both
        /// characters kept and jump straight to the stage phase. Consumed on read.</summary>
        public bool ResumeAtStageSelect;
    }

    public partial class GameManager : Node {
        public static GameManager Instance { get; private set; }

        public SessionData CurrentSession;

        private FTT.UI.LoadingScreen _loadingScreen;
        private string _pendingScenePath;
        private bool _isLoading;
        private double _loadingDisplayTimer;
        private ScenePoolCatalog _scenePoolCatalog;
        private const double MinLoadingDisplayTime = 2.0;

        public override void _Ready() {
            Instance = this;
            CurrentSession = new SessionData {
                // -1 = no story session. Booting at the struct default 0 let any
                // autosave path outside a story session fabricate a slot-0 save
                // (audit Low); menu flows set a real slot before campaign start.
                ActiveSaveSlot = -1,
                OpponentCharacterID = "joan",
                SelectedStageID = "florence_workshop",
                Difficulty = Difficulty.Normal,
                FighterOpponentType = FighterOpponentType.Cpu,
                CpuDifficulty = CpuDifficulty.Normal,
                MatchSettings = MatchSettings.GetDefault()
            };
            _scenePoolCatalog = ScenePoolCatalog.LoadDefault();
        }

        private bool _matchSettingsLoaded;

        /// <summary>
        /// V7 "Match Settings Persist": pre-loads the last-used Fighter
        /// MatchSettings from the global save the first time the Fighter flow
        /// asks — lazy so the SaveManager autoload order never matters.
        /// </summary>
        public void EnsureMatchSettingsLoaded() {
            if (_matchSettingsLoaded) return;
            _matchSettingsLoaded = true;
            SavedMatchSettings saved = SaveManager.Instance?.GlobalData?.LastMatchSettings;
            if (saved == null || !saved.Saved) return;
            var itemRate = (ChronalOrbFrequency)saved.ItemSpawnRate;
            var hazardRate = (HazardTriggerFrequency)saved.HazardRate;
            CurrentSession.MatchSettings = new MatchSettings {
                Mode = (MatchMode)saved.Mode,
                StockCount = Mathf.Max(1, saved.StockCount),
                TimeLimit = Mathf.Max(0f, saved.TimeLimit),
                ItemSpawnRate = itemRate,
                ItemsEnabled = itemRate != ChronalOrbFrequency.Off,
                HazardRate = hazardRate,
                StageHazardsEnabled = hazardRate != HazardTriggerFrequency.Off
            };
        }

        /// <summary>Writes the session's MatchSettings back to the global save.</summary>
        public void PersistMatchSettings() {
            SaveManager save = SaveManager.Instance;
            if (save?.GlobalData == null) return;
            MatchSettings settings = CurrentSession.MatchSettings;
            save.GlobalData.LastMatchSettings = new SavedMatchSettings {
                Saved = true,
                Mode = (int)settings.Mode,
                StockCount = settings.StockCount,
                TimeLimit = settings.TimeLimit,
                ItemSpawnRate = (int)settings.ItemSpawnRate,
                HazardRate = (int)settings.HazardRate
            };
            save.SaveGlobalData();
        }

        public override void _ExitTree() {
            // Godot collection wrappers that reach the finalizer queue crash the
            // process if their finalizers run during engine teardown: the
            // finalizer thread faults inside godotsharp_array_destroy
            // (0xC0000005) once the native side is gone. As an autoload,
            // GameManager leaves the tree only at quit, after the current scene
            // has been torn down but while the engine is still alive — the last
            // safe moment to drain the queue. See GodotCollectionExtensions for
            // the deterministic-disposal half of this contract.
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
        }

        /// <summary>
        /// Raises the loading treatment matching <paramref name="scenePath"/> and
        /// starts the threaded load.
        ///
        /// Package 8 A1: the screen is built per transition rather than once at
        /// boot, because the variant depends on where the player is going — a
        /// Fighter destination gets the VS plate, a campaign destination the portal.
        /// It is parented to this autoload so it survives
        /// <see cref="SceneTree.ChangeSceneToPacked"/>, and freed on arrival.
        /// </summary>
        public void LoadScene(string scenePath) {
            if (_isLoading) return;

            _isLoading = true;
            _pendingScenePath = scenePath;
            _loadingDisplayTimer = 0.0;

            // The scene being left owns the LowHealth/Ultimate snapshot state; a
            // duck must not follow the player into the next scene (audit H-9).
            AudioManager.Instance?.OnSceneTransitionStarted();

            _loadingScreen = FTT.UI.LoadingScreen.CreateFor(scenePath);
            AddChild(_loadingScreen);
            _loadingScreen.Configure(scenePath, CurrentSession);

            Error requestError = ResourceLoader.LoadThreadedRequest(scenePath);
            if (requestError != Error.Ok) {
                AbortLoad($"Threaded scene load request failed for '{scenePath}': {requestError}.");
            }
        }

        private const string MainMenuScenePath = "res://scenes/menus/MainMenu.tscn";

        /// <summary>
        /// Failure path for a threaded scene load (audit M-23). Previously a
        /// <c>Failed</c>/<c>InvalidResource</c> status left <c>_isLoading</c> set
        /// forever: the loading screen never dismissed and every later
        /// <see cref="LoadScene"/> call was swallowed by the re-entrancy guard.
        /// Clears the loading state and, as a last resort, returns to the main
        /// menu so the player is never soft-locked on a dead screen.
        /// </summary>
        private void AbortLoad(string reason) {
            GD.PushError(reason);
            string failedPath = _pendingScenePath;
            DismissLoadingScreen();
            _isLoading = false;
            _pendingScenePath = null;
            string currentScenePath = GetTree().CurrentScene?.SceneFilePath ?? "";
            if (failedPath != MainMenuScenePath && currentScenePath != MainMenuScenePath) {
                Error recovery = GetTree().ChangeSceneToFile(MainMenuScenePath);
                if (recovery != Error.Ok) {
                    GD.PushError($"Main-menu recovery after a failed scene load also failed: {recovery}.");
                }
            }
        }

        private void DismissLoadingScreen() {
            if (_loadingScreen == null) return;
            if (IsInstanceValid(_loadingScreen)) {
                _loadingScreen.Visible = false;
                _loadingScreen.QueueFree();
            }
            _loadingScreen = null;
        }

        public override void _Process(double delta) {
            if (!_isLoading) return;

            _loadingDisplayTimer += delta;

            var status = ResourceLoader.LoadThreadedGetStatus(_pendingScenePath);
            if (status == ResourceLoader.ThreadLoadStatus.Failed
                || status == ResourceLoader.ThreadLoadStatus.InvalidResource) {
                AbortLoad($"Threaded scene load failed for '{_pendingScenePath}' ({status}).");
                return;
            }
            if (status == ResourceLoader.ThreadLoadStatus.Loaded && _loadingDisplayTimer >= MinLoadingDisplayTime) {
                var packedScene = ResourceLoader.LoadThreadedGet(_pendingScenePath) as PackedScene;
                if (packedScene == null) {
                    AbortLoad($"Threaded scene load for '{_pendingScenePath}' did not produce a PackedScene.");
                    return;
                }
                WarmPoolsForScene(_pendingScenePath);
                GetTree().ChangeSceneToPacked(packedScene);
                DismissLoadingScreen();
                _isLoading = false;
                _pendingScenePath = null;
            }
        }

        private void WarmPoolsForScene(string scenePath) {
            ScenePoolConfig config = _scenePoolCatalog?.Find(scenePath);
            if (config == null || PoolManager.Instance == null) return;

            try {
                PoolManager.Instance.WarmFromConfig(config);
            } catch (ArgumentException exception) {
                GD.PushError($"Pool warm-up rejected for '{scenePath}': {exception.Message}");
            }
        }
    }
}
