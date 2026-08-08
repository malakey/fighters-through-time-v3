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
        LocalHuman
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
                OpponentCharacterID = "joan",
                SelectedStageID = "florence_workshop",
                Difficulty = Difficulty.Normal,
                FighterOpponentType = FighterOpponentType.Cpu,
                CpuDifficulty = CpuDifficulty.Normal,
                MatchSettings = MatchSettings.GetDefault()
            };
            _scenePoolCatalog = ScenePoolCatalog.LoadDefault();
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

            _loadingScreen = FTT.UI.LoadingScreen.CreateFor(scenePath);
            AddChild(_loadingScreen);
            _loadingScreen.Configure(scenePath, CurrentSession);

            ResourceLoader.LoadThreadedRequest(scenePath);
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
            if (status == ResourceLoader.ThreadLoadStatus.Loaded && _loadingDisplayTimer >= MinLoadingDisplayTime) {
                var packedScene = ResourceLoader.LoadThreadedGet(_pendingScenePath) as PackedScene;
                if (packedScene != null) {
                    WarmPoolsForScene(_pendingScenePath);
                    GetTree().ChangeSceneToPacked(packedScene);
                }
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
