using Godot;

namespace FTT.UI {

    /// <summary>
    /// Reusable Story Mode HUD: player HP and Influence bars, rewind and dust
    /// counters, objective text, boss bar, and checkpoint feedback toast.
    /// Instanced per campaign scene from StoryHUD.tscn; listens to EventBus.
    /// </summary>
    public partial class StoryHUD : CanvasLayer {
        public const string SceneResourcePath = "res://scenes/ui/StoryHUD.tscn";

        private Label _levelTitleLabel;
        private Label _objectiveLabel;
        private ProgressBar _hpBar;
        private Label _hpText;
        private ProgressBar _meterBar;
        private Label _rewindLabel;
        private Label _dustLabel;
        private Control _bossPanel;
        private Label _bossNameLabel;
        private ProgressBar _bossBar;
        private Label _checkpointToast;

        private float _checkpointToastTimer;
        private int _lastRewinds = int.MinValue;
        private int _lastDust = int.MinValue;

        /// <summary>Instantiates the authored StoryHUD scene, or a code-built fallback.</summary>
        public static StoryHUD CreateDefault() {
            if (ResourceLoader.Exists(SceneResourcePath)) {
                var packed = ResourceLoader.Load<PackedScene>(SceneResourcePath);
                if (packed?.Instantiate() is StoryHUD authored) return authored;
            }
            return new StoryHUD { Name = "StoryHUD" };
        }

        public override void _Ready() {
            Layer = 10;
            ResolveOrBuildUI();
            var bus = FTT.Core.EventBus.Instance;
            if (bus != null) {
                bus.OnPlayerHPChanged += OnPlayerHPChanged;
                bus.OnUltimateMeterChanged += OnUltimateMeterChanged;
                bus.OnCheckpointReached += OnCheckpointReached;
            }
            RefreshCounters(force: true);
        }

        public override void _ExitTree() {
            var bus = FTT.Core.EventBus.Instance;
            if (bus != null) {
                bus.OnPlayerHPChanged -= OnPlayerHPChanged;
                bus.OnUltimateMeterChanged -= OnUltimateMeterChanged;
                bus.OnCheckpointReached -= OnCheckpointReached;
            }
        }

        public override void _Process(double delta) {
            RefreshCounters(force: false);
            if (_checkpointToastTimer > 0f) {
                _checkpointToastTimer -= (float)delta;
                if (_checkpointToastTimer <= 0f && _checkpointToast != null) _checkpointToast.Visible = false;
            }
        }

        // === Public API used by level controllers ===

        public void SetLevelTitle(string translationKey) {
            if (_levelTitleLabel != null) _levelTitleLabel.Text = Tr(translationKey);
        }

        public void SetObjective(string translationKey, params object[] arguments) {
            if (_objectiveLabel == null) return;
            string text = Tr(translationKey);
            _objectiveLabel.Text = arguments != null && arguments.Length > 0
                ? string.Format(text, arguments)
                : text;
        }

        public void InitializePlayerVitals(int currentHP, int maximumHP, float ultimateMeter) {
            UpdateHP(currentHP, maximumHP);
            UpdateMeter(ultimateMeter);
        }

        public void ShowBossBar(string bossNameKey, int currentHP, int maximumHP) {
            if (_bossPanel == null) return;
            _bossPanel.Visible = true;
            if (_bossNameLabel != null) _bossNameLabel.Text = Tr(bossNameKey);
            if (_bossBar != null) {
                _bossBar.MaxValue = maximumHP;
                _bossBar.Value = currentHP;
            }
        }

        public void UpdateBossHP(int currentHP) {
            if (_bossBar != null) _bossBar.Value = currentHP;
        }

        public void HideBossBar() {
            if (_bossPanel != null) _bossPanel.Visible = false;
        }

        // === Event handlers ===

        private void OnPlayerHPChanged(FTT.Core.PlayerHPPayload payload) {
            if (payload.PlayerIndex != 0) return;
            UpdateHP(Mathf.RoundToInt(payload.CurrentHP), Mathf.RoundToInt(payload.MaxHP));
        }

        private void OnUltimateMeterChanged(FTT.Core.UltimateMeterPayload payload) {
            if (payload.PlayerIndex != 0) return;
            UpdateMeter(payload.CurrentValue);
        }

        private void OnCheckpointReached(string checkpointID) {
            if (_checkpointToast == null) return;
            _checkpointToast.Text = Tr("hud_checkpoint_reached");
            _checkpointToast.Visible = true;
            _checkpointToastTimer = 2.5f;
        }

        private void UpdateHP(int currentHP, int maximumHP) {
            if (_hpBar != null) {
                _hpBar.MaxValue = maximumHP;
                _hpBar.Value = currentHP;
            }
            if (_hpText != null) _hpText.Text = $"{Tr("hud_hp")} {currentHP}/{maximumHP}";
        }

        private void UpdateMeter(float value) {
            if (_meterBar != null) _meterBar.Value = Mathf.Clamp(value, 0f, 100f);
        }

        private void RefreshCounters(bool force) {
            var storyManager = FTT.Core.StoryManager.Instance;
            if (storyManager == null) return;
            int rewinds = storyManager.ChronalRewindsRemaining;
            if ((force || rewinds != _lastRewinds) && _rewindLabel != null) {
                _rewindLabel.Text = string.Format(Tr("hud_rewinds"), rewinds);
                _lastRewinds = rewinds;
            }
            int dust = storyManager.ChronalDustCollected;
            if ((force || dust != _lastDust) && _dustLabel != null) {
                _dustLabel.Text = string.Format(Tr("hub_carried_dust"), dust);
                _lastDust = dust;
            }
        }

        // === UI construction ===

        private void ResolveOrBuildUI() {
            var root = GetNodeOrNull<Control>("Root");
            if (root == null) {
                BuildFallbackUI();
                return;
            }
            _levelTitleLabel = GetNodeOrNull<Label>("Root/TopLeft/LevelTitle");
            _objectiveLabel = GetNodeOrNull<Label>("Root/TopLeft/Objective");
            _hpBar = GetNodeOrNull<ProgressBar>("Root/Vitals/HPBar");
            _hpText = GetNodeOrNull<Label>("Root/Vitals/HPText");
            _meterBar = GetNodeOrNull<ProgressBar>("Root/Vitals/MeterBar");
            _rewindLabel = GetNodeOrNull<Label>("Root/Vitals/RewindLabel");
            _dustLabel = GetNodeOrNull<Label>("Root/Vitals/DustLabel");
            _bossPanel = GetNodeOrNull<Control>("Root/BossPanel");
            _bossNameLabel = GetNodeOrNull<Label>("Root/BossPanel/BossName");
            _bossBar = GetNodeOrNull<ProgressBar>("Root/BossPanel/BossBar");
            _checkpointToast = GetNodeOrNull<Label>("Root/CheckpointToast");
        }

        private void BuildFallbackUI() {
            var root = new Control { Name = "Root" };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(root);

            var topLeft = new VBoxContainer { Name = "TopLeft", Position = new Vector2(20, 12) };
            root.AddChild(topLeft);
            _levelTitleLabel = new Label { Name = "LevelTitle" };
            _levelTitleLabel.AddThemeFontSizeOverride("font_size", 16);
            _levelTitleLabel.AddThemeColorOverride("font_color", new Color(0f, 0.9f, 0.9f));
            topLeft.AddChild(_levelTitleLabel);
            _objectiveLabel = new Label { Name = "Objective" };
            _objectiveLabel.AddThemeFontSizeOverride("font_size", 13);
            _objectiveLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.2f));
            topLeft.AddChild(_objectiveLabel);

            var vitals = new VBoxContainer { Name = "Vitals", Position = new Vector2(20, 80), CustomMinimumSize = new Vector2(280, 0) };
            root.AddChild(vitals);
            _hpBar = new ProgressBar { Name = "HPBar", CustomMinimumSize = new Vector2(280, 16), MaxValue = 100, Value = 100, ShowPercentage = false };
            vitals.AddChild(_hpBar);
            _hpText = new Label { Name = "HPText" };
            _hpText.AddThemeFontSizeOverride("font_size", 11);
            vitals.AddChild(_hpText);
            _meterBar = new ProgressBar { Name = "MeterBar", CustomMinimumSize = new Vector2(280, 8), MaxValue = 100, Value = 0, ShowPercentage = false };
            _meterBar.Modulate = new Color(0.4f, 0.8f, 1f);
            vitals.AddChild(_meterBar);
            _rewindLabel = new Label { Name = "RewindLabel" };
            _rewindLabel.AddThemeFontSizeOverride("font_size", 12);
            _rewindLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 1f));
            vitals.AddChild(_rewindLabel);
            _dustLabel = new Label { Name = "DustLabel" };
            _dustLabel.AddThemeFontSizeOverride("font_size", 12);
            _dustLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.8f, 0.3f));
            vitals.AddChild(_dustLabel);

            _bossPanel = new VBoxContainer { Name = "BossPanel", Visible = false };
            _bossPanel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            _bossPanel.Position = new Vector2(660, 14);
            _bossPanel.CustomMinimumSize = new Vector2(600, 0);
            root.AddChild(_bossPanel);
            _bossNameLabel = new Label { Name = "BossName", HorizontalAlignment = HorizontalAlignment.Center };
            _bossNameLabel.AddThemeFontSizeOverride("font_size", 15);
            _bossNameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.25f, 0.25f));
            _bossPanel.AddChild(_bossNameLabel);
            _bossBar = new ProgressBar { Name = "BossBar", CustomMinimumSize = new Vector2(600, 14), ShowPercentage = false };
            _bossBar.Modulate = new Color(1f, 0.3f, 0.3f);
            _bossPanel.AddChild(_bossBar);

            _checkpointToast = new Label { Name = "CheckpointToast", Visible = false, HorizontalAlignment = HorizontalAlignment.Center };
            _checkpointToast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            _checkpointToast.Position = new Vector2(810, 140);
            _checkpointToast.CustomMinimumSize = new Vector2(300, 30);
            _checkpointToast.AddThemeFontSizeOverride("font_size", 20);
            _checkpointToast.AddThemeColorOverride("font_color", new Color(0f, 0.9f, 0.9f));
            root.AddChild(_checkpointToast);
        }
    }
}
