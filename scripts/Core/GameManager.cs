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
        public string SelectedStageID;
        public int ActiveSaveSlot;
        public Difficulty Difficulty;
        public MatchSettings MatchSettings;
    }

    public partial class GameManager : Node {
        public static GameManager Instance { get; private set; }

        public SessionData CurrentSession;

        private CanvasLayer _loadingScreen;
        private ColorRect _loadingBackground;
        private Label _loadingLabel;
        private string _pendingScenePath;
        private bool _isLoading;
        private double _loadingDisplayTimer;
        private const double MinLoadingDisplayTime = 2.0;

        public override void _Ready() {
            Instance = this;
            CurrentSession = new SessionData {
                Difficulty = Difficulty.Normal,
                MatchSettings = MatchSettings.GetDefault()
            };
            SetupLoadingScreen();
        }

        private void SetupLoadingScreen() {
            _loadingScreen = new CanvasLayer();
            _loadingScreen.Layer = 100;
            _loadingScreen.Visible = false;
            AddChild(_loadingScreen);

            _loadingBackground = new ColorRect();
            _loadingBackground.Color = new Color(0.05f, 0.05f, 0.1f, 1.0f);
            _loadingBackground.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _loadingScreen.AddChild(_loadingBackground);

            _loadingLabel = new Label();
            _loadingLabel.Text = "LOADING...";
            _loadingLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _loadingLabel.VerticalAlignment = VerticalAlignment.Center;
            _loadingLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _loadingLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.9f, 0.9f));
            _loadingScreen.AddChild(_loadingLabel);
        }

        public void LoadScene(string scenePath) {
            if (_isLoading) return;

            _isLoading = true;
            _pendingScenePath = scenePath;
            _loadingDisplayTimer = 0.0;
            _loadingScreen.Visible = true;

            ResourceLoader.LoadThreadedRequest(scenePath);
        }

        public override void _Process(double delta) {
            if (!_isLoading) return;

            _loadingDisplayTimer += delta;

            var status = ResourceLoader.LoadThreadedGetStatus(_pendingScenePath);
            if (status == ResourceLoader.ThreadLoadStatus.Loaded && _loadingDisplayTimer >= MinLoadingDisplayTime) {
                var packedScene = ResourceLoader.LoadThreadedGet(_pendingScenePath) as PackedScene;
                if (packedScene != null) {
                    GetTree().ChangeSceneToPacked(packedScene);
                }
                _loadingScreen.Visible = false;
                _isLoading = false;
                _pendingScenePath = null;
            }
        }
    }
}
