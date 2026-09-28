using Godot;
using System;

namespace FTT.Core {

    public enum Difficulty {
        Easy,
        Normal,
        Hard
    }

    /// <summary>
    /// F21 (Package 11 A1c): the selectable modes are <b>Stock</b> and <b>Time</b>.
    /// The ordinals are explicit because they serialize into the global save's
    /// <c>SavedMatchSettings.Mode</c> and into the deterministic
    /// <c>FighterMatchComponent.MatchMode</c>.
    /// </summary>
    public enum MatchMode {
        /// <summary>Limited stocks (default 3, range 1-5); the timer is optional, including Off.</summary>
        Stock = 0,
        /// <summary>
        /// Labelled <b>Time</b> in every menu; the identifier is retained for
        /// compatibility. Unlimited respawns, a positive timer required, decided by
        /// fewest stocks lost with no HP tiebreak.
        /// </summary>
        TimeLimit = 1,
        /// <summary>
        /// The retired Stock + Time combination. <b>Reserved, never reused, and
        /// never offered as a third playable mode</b> — it exists only so a legacy
        /// saved value decodes to something recognizable before
        /// <c>SavedMatchSettings.Normalize</c> rewrites it to timed Stock.
        /// </summary>
        [System.Obsolete("F21: retired. Legacy decoding only; SavedMatchSettings.Normalize maps it to timed Stock.")]
        Hybrid = 2
    }

    public enum ChronalOrbFrequency {
        Off,
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Package 12 W5 (M26): where a Fighter match was launched from, so every
    /// exit (pause Exit, the results buttons) returns to the surface that
    /// launched it. Session-only — <b>never persisted</b>: a relaunch always
    /// starts from the main menu.
    /// </summary>
    public enum FighterMatchOrigin {
        /// <summary>The two-human local select (and Remote Play, which reuses it).</summary>
        LocalVersus = 0,
        /// <summary>H05: the Fighter menu's Versus CPU entry (P1-only select + CPU config panel).</summary>
        VersusCpu = 1,
        /// <summary>The hub's Holodeck Arena Console.</summary>
        Holodeck = 2,
        /// <summary>F18: a Calibration Drill borrowing the Fighter match shell.</summary>
        Drill = 3
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
        /// <summary>
        /// Package 12 W5 (2026-09-26 design): the single hazard On/Off toggle. On
        /// runs each stage's fixed authored cadence (Overtime and Sudden Death
        /// double it); the retired <c>HazardTriggerFrequency</c> selector is gone.
        /// </summary>
        public bool StageHazardsEnabled;
        /// <summary>
        /// Package 12 W5 (M24, adopted D5(b)): the Items sub-toggle "Meter
        /// pickups". When On, Resonance Surge (+15 meter) joins the Chronal Orb
        /// draw. <b>Off by default</b>, so a default match's seeded orb schedule
        /// draws from exactly the four types it always did.
        /// </summary>
        public bool MeterPickupsEnabled;

        public static MatchSettings GetDefault() {
            // V7.3 ruling #18: items default to Medium; hazards default On
            // (2026-09-26: each stage's authored cadence, seeded from the
            // retired Medium interval).
            return new MatchSettings {
                // F21: Stock is 0 explicitly now, and Hybrid is not offered.
                Mode = MatchMode.Stock,
                StockCount = 3,
                TimeLimit = 480.0f,
                ItemsEnabled = true,
                ItemSpawnRate = ChronalOrbFrequency.Medium,
                StageHazardsEnabled = true,
                MeterPickupsEnabled = false
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
        /// <summary>
        /// Package 12 W5 (M26): the surface that launched the current Fighter
        /// match. Never persisted. <c>FTT.UI.FighterFlowRoutes</c> owns every
        /// destination that reads it.
        /// </summary>
        public FighterMatchOrigin FighterMatchOrigin;
        /// <summary>
        /// Set by the hub Holodeck so Fighter flows return to the Time-Ship instead
        /// of the main menu. Package 12 W5: a view over
        /// <see cref="FighterMatchOrigin"/> rather than a second flag — true exactly
        /// when the origin is <see cref="FighterMatchOrigin.Holodeck"/>. Setting it
        /// true selects the Holodeck origin; setting it false leaves a non-Holodeck
        /// origin alone and demotes a Holodeck one to Local Versus.
        /// </summary>
        public bool ReturnToHubAfterFighterMatch {
            readonly get => FighterMatchOrigin == FighterMatchOrigin.Holodeck;
            set {
                if (value) FighterMatchOrigin = FighterMatchOrigin.Holodeck;
                else if (FighterMatchOrigin == FighterMatchOrigin.Holodeck) {
                    FighterMatchOrigin = FighterMatchOrigin.LocalVersus;
                }
            }
        }
        /// <summary>
        /// Package 12 W5 (M26): the Holodeck's post-match <b>Reconfigure</b> —
        /// the hub reopens the console panel on arrival. Consumed on read.
        /// </summary>
        public bool ReopenHolodeckConsole;
        /// <summary>
        /// Package 12 W5 (M26): the hub spawns the player in front of the Holodeck
        /// console instead of at the Calibration Bay anchor. Consumed on read.
        /// </summary>
        public bool ArriveAtHolodeckConsole;
        /// <summary>
        /// Package 12 W5 (G15a): a per-match seed rolled at the select screen so a
        /// Random character / stage tile resolves from the same seed the match
        /// then runs on. <see cref="HasPendingMatchSeed"/> gates it; the driver
        /// consumes it once, so a Rematch rolls a fresh seed.
        /// </summary>
        public int PendingMatchSeed;
        /// <inheritdoc cref="PendingMatchSeed"/>
        public bool HasPendingMatchSeed;
        /// <summary>Post-match "New Stage" (V7): re-enter CharacterSelect with both
        /// characters kept and jump straight to the stage phase. Consumed on read.</summary>
        public bool ResumeAtStageSelect;
        /// <summary>
        /// Package 11 A11 (F18 Calibration Drills): where "Exit Calibration" and a
        /// Back out of the drill route land — the Main Menu for the standalone
        /// route, the originating hub for the Holodeck console route. Empty outside
        /// the route; <c>FTT.Combat.CalibrationRoute</c> owns the routing rules.
        /// Deliberately a scene path rather than a campaign field: the drill route
        /// never creates, loads or resumes a Story attempt.
        /// </summary>
        public string CalibrationReturnScenePath;
        /// <summary>
        /// Package 11 A11: which drill of <c>CalibrationDrillCatalog</c> the shared
        /// Holodeck drill scene should load. Never persisted — quitting or
        /// relaunching can therefore never resume a drill.
        /// </summary>
        public int CalibrationDrillIndex;
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
                CalibrationReturnScenePath = "",
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
            // F21 migration, run before the loaded settings are ever displayed or a
            // match is created from them: a recognized legacy Hybrid becomes timed
            // Stock with its timer and stock count preserved and repaired. The
            // normalization is idempotent and never touches a running match.
            saved.Normalize();
            var itemRate = (ChronalOrbFrequency)saved.ItemSpawnRate;
            CurrentSession.MatchSettings = new MatchSettings {
                Mode = (MatchMode)saved.Mode,
                StockCount = Mathf.Clamp(saved.StockCount, SavedMatchSettings.MinStockCount, SavedMatchSettings.MaxStockCount),
                TimeLimit = Mathf.Max(0f, saved.TimeLimit),
                ItemSpawnRate = itemRate,
                ItemsEnabled = itemRate != ChronalOrbFrequency.Off,
                // Normalize has already resolved a legacy payload's retired hazard
                // rate into the toggle (SavedMatchSettings.DeriveStageHazardsEnabled).
                StageHazardsEnabled = saved.StageHazardsEnabled ?? true,
                MeterPickupsEnabled = saved.MeterPickupsEnabled
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
                StageHazardsEnabled = settings.StageHazardsEnabled,
                MeterPickupsEnabled = settings.MeterPickupsEnabled
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
