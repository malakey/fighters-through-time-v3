using Godot;

namespace FTT.UI {

    /// <summary>
    /// Reusable Story Mode HUD: player HP and Influence bars, ability-readiness and
    /// status indicators, rewind and dust counters, objective text, the boss bar
    /// with its authored phase notches, and the checkpoint feedback toast.
    /// Instanced per campaign scene from StoryHUD.tscn; listens to EventBus.
    /// </summary>
    public partial class StoryHUD : CanvasLayer {
        public const string SceneResourcePath = "res://scenes/ui/StoryHUD.tscn";

        /// <summary>Slots the ability indicator row draws, left to right.</summary>
        private static readonly FTT.Core.AbilitySlot[] IndicatorSlots = {
            FTT.Core.AbilitySlot.Special1,
            FTT.Core.AbilitySlot.Special2,
            FTT.Core.AbilitySlot.MovementAbility,
            FTT.Core.AbilitySlot.Ultimate
        };

        private Control _root;
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
        private BossPhaseNotchOverlay _bossNotches;
        private Label _checkpointToast;
        private Label _statusIndicator;
        private readonly ProgressBar[] _slotBars = new ProgressBar[IndicatorSlots.Length];
        private readonly Label[] _slotLabels = new Label[IndicatorSlots.Length];

        private readonly HudAbilityIndicatorModel _indicators = new();
        private readonly HudOpacityBinder _opacity = new();

        private float _checkpointToastTimer;
        private int _lastRewinds = int.MinValue;
        private int _lastDust = int.MinValue;

        /// <summary>The ability/status readiness model this HUD renders.</summary>
        public HudAbilityIndicatorModel Indicators => _indicators;

        /// <summary>The HudOpacity setting most recently applied to the HUD root.</summary>
        public float AppliedHudOpacity => _opacity.AppliedOpacity;

        /// <summary>The boss bar's phase-notch overlay, or null before it resolves.</summary>
        public BossPhaseNotchOverlay BossNotches => _bossNotches;

        /// <summary>
        /// Instantiates the authored StoryHUD scene. The scene is a committed,
        /// tested asset (Package 8 B1 removed the duplicated code-built fallback
        /// that used to shadow it — two layouts drifting apart silently was worse
        /// than one loud failure), so a missing scene is a packaging error: it is
        /// reported and a bare, harmless instance is returned rather than null,
        /// because every level controller calls into this unconditionally.
        /// </summary>
        public static StoryHUD CreateDefault() {
            if (ResourceLoader.Exists(SceneResourcePath)) {
                var packed = ResourceLoader.Load<PackedScene>(SceneResourcePath);
                if (packed?.Instantiate() is StoryHUD authored) return authored;
            }
            GD.PushError($"StoryHUD scene missing at {SceneResourcePath}; the HUD will render nothing.");
            return new StoryHUD { Name = "StoryHUD" };
        }

        public override void _Ready() {
            Layer = 10;
            ResolveUI();
            var bus = FTT.Core.EventBus.Instance;
            if (bus != null) {
                bus.OnPlayerHPChanged += OnPlayerHPChanged;
                bus.OnUltimateMeterChanged += OnUltimateMeterChanged;
                bus.OnCheckpointReached += OnCheckpointReached;
                bus.OnCooldownStarted += OnCooldownStarted;
                bus.OnCooldownComplete += OnCooldownComplete;
                bus.OnStatusEffectApplied += OnStatusEffectApplied;
                bus.OnStatusEffectCleared += OnStatusEffectCleared;
                bus.OnBossPhaseChanged += OnBossPhaseChanged;
            }
            RefreshCounters(force: true);
            RefreshIndicators();
            _opacity.Apply(_root, force: true);
        }

        public override void _ExitTree() {
            var bus = FTT.Core.EventBus.Instance;
            if (bus != null) {
                bus.OnPlayerHPChanged -= OnPlayerHPChanged;
                bus.OnUltimateMeterChanged -= OnUltimateMeterChanged;
                bus.OnCheckpointReached -= OnCheckpointReached;
                bus.OnCooldownStarted -= OnCooldownStarted;
                bus.OnCooldownComplete -= OnCooldownComplete;
                bus.OnStatusEffectApplied -= OnStatusEffectApplied;
                bus.OnStatusEffectCleared -= OnStatusEffectCleared;
                bus.OnBossPhaseChanged -= OnBossPhaseChanged;
            }
        }

        public override void _Process(double delta) {
            RefreshCounters(force: false);
            // Polled rather than event-driven: there is no settings-changed event,
            // and SettingsMenu belongs to another workstream this package.
            _opacity.Apply(_root);
            _indicators.Tick((float)delta);
            RefreshIndicators();
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

        public void ShowBossBar(string bossNameKey, int currentHP, int maximumHP) =>
            ShowBossBar(bossNameKey, currentHP, maximumHP, null);

        /// <summary>
        /// Reveals the boss bar. <paramref name="phaseThresholds"/> is the boss's
        /// authored <c>BossData.PhaseThresholds</c> array; passing it here is what
        /// puts the phase notches on the bar. A null or empty array draws a plain
        /// bar, which is correct for a single-phase boss.
        /// </summary>
        public void ShowBossBar(string bossNameKey, int currentHP, int maximumHP, float[] phaseThresholds) {
            if (_bossPanel == null) return;
            _bossPanel.Visible = true;
            if (_bossNameLabel != null) _bossNameLabel.Text = Tr(bossNameKey);
            if (_bossBar != null) {
                _bossBar.MaxValue = maximumHP;
                _bossBar.Value = currentHP;
            }
            _bossNotches?.SetThresholds(phaseThresholds);
        }

        public void UpdateBossHP(int currentHP) {
            if (_bossBar != null) _bossBar.Value = currentHP;
        }

        public void HideBossBar() {
            if (_bossPanel != null) _bossPanel.Visible = false;
            _bossNotches?.Clear();
        }

        // === Event handlers ===

        private void OnPlayerHPChanged(FTT.Core.PlayerHPPayload payload) {
            if (payload.PlayerIndex != 0) return;
            UpdateHP(Mathf.RoundToInt(payload.CurrentHP), Mathf.RoundToInt(payload.MaxHP));
        }

        private void OnUltimateMeterChanged(FTT.Core.UltimateMeterPayload payload) {
            if (payload.PlayerIndex != 0) return;
            UpdateMeter(payload.CurrentValue);
            _indicators.SetUltimateMeter(payload.CurrentValue);
        }

        private void OnCooldownStarted(FTT.Core.CooldownPayload payload) {
            if (payload.PlayerIndex != 0) return;
            _indicators.StartCooldown(payload.Slot, payload.Duration);
        }

        private void OnCooldownComplete(FTT.Core.AbilitySlot slot) =>
            _indicators.StartCooldown(slot, 0f);

        private void OnStatusEffectApplied(FTT.Core.StatusEffectPayload payload) {
            if (payload.TargetIndex != 0) return;
            _indicators.ApplyStatus(payload.Type, payload.Duration);
        }

        private void OnStatusEffectCleared(FTT.Core.StatusEffectPayload payload) {
            if (payload.TargetIndex != 0) return;
            _indicators.ClearStatus();
        }

        private void OnBossPhaseChanged(int phaseIndex) => _bossNotches?.NotifyPhaseChanged(phaseIndex);

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

        /// <summary>
        /// Repaints the four readiness bars and the status pip from the model.
        /// Cheap enough to run unconditionally: four Range writes and a colour.
        /// </summary>
        private void RefreshIndicators() {
            for (int index = 0; index < IndicatorSlots.Length; index++) {
                FTT.Core.AbilitySlot slot = IndicatorSlots[index];
                ProgressBar bar = _slotBars[index];
                if (bar != null) bar.Value = _indicators.ReadinessFraction(slot) * 100f;
                Label label = _slotLabels[index];
                if (label == null) continue;
                label.AddThemeColorOverride(
                    "font_color",
                    _indicators.IsReady(slot) ? UIPalette.TextAccent : UIPalette.TextDisabled);
            }

            if (_statusIndicator == null) return;
            FTT.Core.StatusType status = _indicators.ActiveStatus;
            if (status == FTT.Core.StatusType.None) {
                _statusIndicator.Visible = false;
                return;
            }
            _statusIndicator.Visible = true;
            _statusIndicator.Text = Tr(HudAbilityIndicatorModel.StatusLabelKey(status));
            // The authored status colours live with the glow arbiter (A3); reusing
            // them keeps the HUD pip and the character's outline the same colour
            // rather than creating a second canonical palette for statuses.
            _statusIndicator.AddThemeColorOverride(
                "font_color", FTT.Combat.GlowPalette.Status(status).OutlineColor);
        }

        // === UI resolution ===

        private void ResolveUI() {
            _root = GetNodeOrNull<Control>("Root");
            if (_root == null) return;
            UIPalette.ApplyTheme(_root);
            _levelTitleLabel = GetNodeOrNull<Label>("Root/TopLeft/LevelTitle");
            _objectiveLabel = GetNodeOrNull<Label>("Root/TopLeft/Objective");
            _hpBar = GetNodeOrNull<ProgressBar>("Root/Vitals/HPBar");
            _hpText = GetNodeOrNull<Label>("Root/Vitals/HPText");
            _meterBar = GetNodeOrNull<ProgressBar>("Root/Vitals/MeterBar");
            _rewindLabel = GetNodeOrNull<Label>("Root/Vitals/RewindLabel");
            _dustLabel = GetNodeOrNull<Label>("Root/Vitals/DustLabel");
            _statusIndicator = GetNodeOrNull<Label>("Root/Vitals/StatusIndicator");
            if (_statusIndicator != null) _statusIndicator.Visible = false;
            for (int index = 0; index < IndicatorSlots.Length; index++) {
                string slotName = SlotNodeName(IndicatorSlots[index]);
                _slotLabels[index] = GetNodeOrNull<Label>($"Root/Vitals/Abilities/{slotName}/SlotName");
                _slotBars[index] = GetNodeOrNull<ProgressBar>($"Root/Vitals/Abilities/{slotName}/SlotBar");
                if (_slotLabels[index] != null) {
                    _slotLabels[index].Text = Tr(HudAbilityIndicatorModel.SlotLabelKey(IndicatorSlots[index]));
                }
            }
            _bossPanel = GetNodeOrNull<Control>("Root/BossPanel");
            _bossNameLabel = GetNodeOrNull<Label>("Root/BossPanel/BossName");
            _bossBar = GetNodeOrNull<ProgressBar>("Root/BossPanel/BossBar");
            _bossNotches = GetNodeOrNull<BossPhaseNotchOverlay>("Root/BossPanel/BossBar/PhaseNotches");
            _checkpointToast = GetNodeOrNull<Label>("Root/CheckpointToast");
        }

        private static string SlotNodeName(FTT.Core.AbilitySlot slot) => slot switch {
            FTT.Core.AbilitySlot.Special1 => "Special1",
            FTT.Core.AbilitySlot.Special2 => "Special2",
            FTT.Core.AbilitySlot.MovementAbility => "Movement",
            _ => "Ultimate"
        };
    }
}
