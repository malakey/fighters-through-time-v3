using Godot;
using FTT.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FTT.UI {

    /// <summary>
    /// The one settings screen (Package 8 A4). Backed by the authored
    /// <c>res://scenes/ui/Settings.tscn</c>; MainMenu, the Story pause menu, and
    /// the local Fighter pause menu all embed this same overlay.
    ///
    /// <para><b>Why the scene root is a plain Control and the script stays a
    /// CanvasLayer.</b> All three openers construct this class directly
    /// (<c>new SettingsMenu()</c>). Putting the script on the scene root would make
    /// those constructions produce an empty screen, and changing the call sites
    /// would collide with A1 (pause menus) and B4 (main menu) in parallel
    /// worktrees. So the scene is a script-less authored Control tree that this
    /// CanvasLayer instantiates in <see cref="_Ready"/>: one authored scene, three
    /// untouched openers.</para>
    ///
    /// <para><b>Pause discipline (plan §2.6).</b> This screen never writes
    /// <see cref="SceneTree.Paused"/>. Closing it returns control to whoever opened
    /// it — the pause menus still own their own pause — and raises
    /// <see cref="Closed"/>. The old unconditional <c>GetTree().Paused = false</c>
    /// on close silently unpaused the game behind an open pause menu.</para>
    /// </summary>
    public partial class SettingsMenu : CanvasLayer {
        public const string ScenePath = "res://scenes/ui/Settings.tscn";
        public const string ThemePath = "res://resources/UI/ftt_theme.tres";

        /// <summary>Raised after the screen hides, so the opener can restore focus.</summary>
        public event Action Closed;

        private Control _root;

        // TG-PAUSE: the glass skin's parts.
        private PausePanel _panel;
        private Control _shade;
        private Control _promptBar;
        private readonly Dictionary<Godot.Range, Label> _valueLabels = new();
        private Button _listeningSlot;

        /// <summary>
        /// One dim (UI_SPEC_GLASS 9.2): opened from a pause menu the screen sits on the pause's dim and draws none of its own;
        /// opened from the main menu or the hub it brings its 0.70 dim. Default false.
        /// </summary>
        public bool UsesHostDim { get; set; }

        /// <summary>The screen's glass slab. Test surface.</summary>
        internal PausePanel Panel => _panel;

        // Audio
        private HSlider _masterSlider;
        private HSlider _musicSlider;
        private HSlider _sfxSlider;
        private HSlider _uiSlider;
        // Package 12 W6: M27 per-category mutes and G11 Mute When Unfocused.
        private CheckButton _masterMute;
        private CheckButton _musicMute;
        private CheckButton _sfxMute;
        private CheckButton _uiMute;
        private CheckButton _muteWhenUnfocused;

        // Package 12 W6: G10 Text Speed and G09 About & Privacy (Gameplay tab).
        private OptionButton _textSpeedDropdown;
        private OptionButton _crashReportsDropdown;
        private Label _versionLabel;

        // Package 12 W6: G13 Block Mode, stick profiles; M27 Vibration moved here (Controls tab).
        private OptionButton _blockModeDropdown;
        private OptionButton _stickDeviceDropdown;
        private HSlider _deadzoneSlider;
        private HSlider _downThresholdSlider;
        private readonly List<string> _stickDeviceKeys = new();
        private Dictionary<string, StickProfile> _workingStickProfiles = new(StringComparer.Ordinal);
        private bool _suppressStickWrites;

        private static readonly TextSpeed[] TextSpeedOrder = {
            TextSpeed.Slow, TextSpeed.Normal, TextSpeed.Fast, TextSpeed.Instant
        };

        private static readonly CrashReportMode[] CrashReportOrder = {
            CrashReportMode.Ask, CrashReportMode.Always, CrashReportMode.Never
        };

        private static readonly BlockMode[] BlockModeOrder = { BlockMode.Hold, BlockMode.Toggle };

        // Display
        private OptionButton _resolutionDropdown;
        private OptionButton _windowModeDropdown;
        private CheckButton _vsyncToggle;

        // Gameplay
        private CheckButton _hapticToggle;
        private HSlider _hapticSlider;
        private CheckButton _damageNumbersToggle;
        private HSlider _hudOpacitySlider;
        private HSlider _screenShakeSlider;
        private HSlider _uiScaleSlider;
        private CheckButton _reducedEffectsToggle;
        private Label _reducedEffectsHelp;

        // Controls
        private TabContainer _tabs;
        private VBoxContainer _actionRows;
        private GridContainer _shortcutRows;
        private Label _controlsStatus;
        private readonly Dictionary<string, Button> _unbindButtons = new();
        private readonly Dictionary<string, Button> _keyboardSlots = new();
        private readonly Dictionary<string, Button> _joypadSlots = new();
        private InputBindingSet _workingBindings = new();
        private string _listeningAction = "";
        private InputDeviceKind _listeningDeviceKind = InputDeviceKind.Keyboard;
        private List<Control> _focusChain = new();

        private static readonly string[] TabTitleKeys = {
            "settings_tab_audio", "settings_tab_display", "settings_tab_gameplay", "settings_tab_controls"
        };

        private static readonly string[] WindowModeKeys = {
            "settings_window_windowed", "settings_window_fullscreen", "settings_window_borderless"
        };

        /// <summary>True while a rebind row is waiting for a physical input.</summary>
        public bool IsListening => _listeningAction.Length > 0;

        /// <summary>The working (unsaved) binding map the Controls tab edits.</summary>
        public InputBindingSet WorkingBindings => _workingBindings;

        /// <summary>The instantiated authored scene root. Null only if the scene failed to load.</summary>
        internal Control Root => _root;

        /// <summary>The tab strip, for contract tests.</summary>
        internal TabContainer Tabs => _tabs;

        /// <summary>The focus chain built for the currently visible controls. Test seam.</summary>
        internal IReadOnlyList<Control> FocusChain => _focusChain;

        public override void _Ready() {
            Layer = 100;
            ProcessMode = ProcessModeEnum.Always;
            Visible = false;
            BuildFromScene();
            LoadSettings();
        }

        public override void _ExitTree() {
            // Deliberately does not touch SceneTree.Paused: this screen never set it.
            _listeningAction = "";
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        private void BuildFromScene() {
            var packed = ResourceLoader.Load<PackedScene>(ScenePath);
            _root = packed?.Instantiate<Control>();
            if (_root == null) {
                GD.PushError($"SettingsMenu could not instantiate {ScenePath}.");
                return;
            }
            AddChild(_root);
            ApplyTheme();
            BindNodes();
            LocalizeStaticText();
            PopulateDropdowns();
            WireSignals();
            BuildControlsTab();
            ApplyGlassWidgets();
        }

        /// <summary>
        /// TG-PAUSE (UI_SPEC_GLASS 3.3, 9.2): a centred glass slab (the screen's own 0.70 dim unless it sits on a host's)
        /// with the sheen, kept on screen at UI scale 1.4; every switch a glass toggle whose row label is drawn upper-case
        /// in the label column; every slider with its value column and the glass field's focus look; the Back and Reset All
        /// buttons compact chevrons; the tab strip upper-case; the bottom-centre prompt bar.
        /// </summary>
        private void ApplyGlassWidgets() {
            if (_root == null) return;
            _shade = _root.GetNodeOrNull<Control>("Shade");
            _panel = _root.GetNodeOrNull<PausePanel>("Margin/Panel");
            if (_panel != null) {
                PulpScreenKit.DressPanel(_panel, sheen: true, buttonInset: false);
                MenuSafeAreaFit.AttachClearOfPromptBar(_panel, _root);
            }
            var found = _root.FindChildren("*", "CheckButton", true, false);
            using (found.AsDisposable()) {
                foreach (Node node in found) if (node is CheckButton toggle) SkinToggle(toggle);
            }
            if (_root.GetNodeOrNull<Button>("Margin/Panel/Body/Footer/BackButton") is { } back) {
                Pulp.PulpButtonDecor.Apply(back, compact: true).ShowPrompt = false;
                Pulp.PulpButtonDecor.ApplyStackWidth(new[] { back }, CompactButtonMinWidth, reservePrompt: false);
            }
            if (_tabs.GetNodeOrNull<Button>("Controls/Scroll/Content/ResetAllButton") is { } resetAll) {
                Pulp.PulpButtonDecor.Apply(resetAll, compact: true).ShowPrompt = false;
                Pulp.PulpButtonDecor.ApplyStackWidth(new[] { resetAll }, CompactButtonMinWidth, reservePrompt: false);
            }
            QuietTabPanel();
            RefreshValueLabels();
            _promptBar = PulpScreenKit.AddBottomPromptBar(_root, "common_back");
        }

        /// <summary>The D6 floor of a compact chevron (3.2: 290).</summary>
        public const float CompactButtonMinWidth = 290f;

        /// <summary>A shortcut toggle on the Controls tab: its label column, the gap and the 132 px track.</summary>
        private const float ShortcutToggleWidth = 440f;

        /// <summary>A toggle row's label: the <c>RowLabel</c> child drawn in the label column (UI_SPEC_GLASS 3.3, T52).</summary>
        public const string ToggleRowLabelName = Pulp.PulpToggleDecor.RowLabelName;

        /// <summary>
        /// A settings switch (3.3): the glass toggle track at the button's right end, and the row's label drawn upper-case in
        /// the label column by a <c>RowLabel</c> child (T52) while the button's own text, which tests and the focus chain
        /// read, stays as it is and draws nothing. A row inside a <see cref="SettingsRowLayout"/> takes the layout's width;
        /// any other row (the shortcut grid) takes <paramref name="width"/>.
        /// </summary>
        internal static void SkinToggle(CheckButton toggle, float width = 0f) {
            if (width > 0f) toggle.CustomMinimumSize = new Vector2(width, toggle.CustomMinimumSize.Y);
            // The toggle decor draws the row label (T52, upper case) and hides the button's own text (TG-CLOSE folded the
            // RowLabel this method used to build into PulpToggleDecor, so every toggle in the game has it).
            Pulp.PulpToggleDecor.Apply(toggle);
        }

        /// <summary>
        /// The TabContainer's inner panel stands down to a quiet frame with a tighter padding, so the rows sit close under
        /// the tab strip (a scene-local copy of the theme's own box; the shared theme is untouched).
        /// </summary>
        private void QuietTabPanel() {
            if (_tabs?.GetThemeStylebox("panel") is not StyleBox box) return;
            if (box.Duplicate() is not StyleBox own) return;
            own.ContentMarginLeft = TabPanelPadding;
            own.ContentMarginRight = TabPanelPadding;
            own.ContentMarginTop = TabPanelPaddingTop;
            own.ContentMarginBottom = TabPanelPaddingTop;
            _tabs.AddThemeStyleboxOverride("panel", own);
        }

        private const float TabPanelPadding = 24f, TabPanelPaddingTop = 14f;

        /// <summary>
        /// Applies the shared UI theme if it is present. A1 authors it in a parallel
        /// worktree, so the lookup is deliberately fallback-safe: without the file
        /// the screen still renders with engine defaults.
        /// </summary>
        private void ApplyTheme() {
            if (_root == null || !ResourceLoader.Exists(ThemePath)) return;
            var theme = ResourceLoader.Load<Theme>(ThemePath);
            if (theme != null) _root.Theme = theme;
        }

        private void BindNodes() {
            _tabs = _root.GetNode<TabContainer>("Margin/Panel/Body/Tabs");
            _masterSlider = Slider("Audio/MasterSlider");
            _musicSlider = Slider("Audio/MusicSlider");
            _sfxSlider = Slider("Audio/SfxSlider");
            _uiSlider = Slider("Audio/UiSlider");
            _masterMute = _tabs.GetNode<CheckButton>("Audio/MasterMuteToggle");
            _musicMute = _tabs.GetNode<CheckButton>("Audio/MusicMuteToggle");
            _sfxMute = _tabs.GetNode<CheckButton>("Audio/SfxMuteToggle");
            _uiMute = _tabs.GetNode<CheckButton>("Audio/UiMuteToggle");
            _muteWhenUnfocused = _tabs.GetNode<CheckButton>("Audio/MuteWhenUnfocusedToggle");

            _resolutionDropdown = _tabs.GetNode<OptionButton>("Display/ResolutionDropdown");
            _windowModeDropdown = _tabs.GetNode<OptionButton>("Display/WindowModeDropdown");
            _vsyncToggle = _tabs.GetNode<CheckButton>("Display/VSyncToggle");

            // M27: Vibration moved from Gameplay to Controls (design Section 12).
            _hapticToggle = _tabs.GetNode<CheckButton>("Controls/Options/HapticToggle");
            _hapticSlider = Slider("Controls/Options/HapticSlider");
            _blockModeDropdown = _tabs.GetNode<OptionButton>("Controls/Options/BlockModeDropdown");
            _stickDeviceDropdown = _tabs.GetNode<OptionButton>("Controls/Options/StickDeviceDropdown");
            _deadzoneSlider = Slider("Controls/Options/DeadzoneSlider");
            _downThresholdSlider = Slider("Controls/Options/DownThresholdSlider");

            _textSpeedDropdown = _tabs.GetNode<OptionButton>("Gameplay/TextSpeedRow/TextSpeedDropdown");
            _crashReportsDropdown = _tabs.GetNode<OptionButton>("Gameplay/CrashReportsRow/CrashReportsDropdown");
            _versionLabel = _tabs.GetNode<Label>("Gameplay/VersionLabel");
            _damageNumbersToggle = _tabs.GetNode<CheckButton>("Gameplay/DamageNumbersToggle");
            _hudOpacitySlider = Slider("Gameplay/HudOpacitySlider");
            _screenShakeSlider = Slider("Gameplay/ScreenShakeSlider");
            _uiScaleSlider = Slider("Gameplay/UiScaleSlider");
            _reducedEffectsToggle = _tabs.GetNode<CheckButton>("Gameplay/ReducedEffectsToggle");
            _reducedEffectsHelp = _tabs.GetNode<Label>("Gameplay/ReducedEffectsHelp");

            _actionRows = _tabs.GetNode<VBoxContainer>("Controls/Scroll/Content/ActionRows");
            _shortcutRows = _tabs.GetNode<GridContainer>("Controls/Scroll/Content/ShortcutRows");
            _controlsStatus = _tabs.GetNode<Label>("Controls/Status");

            // TG-PAUSE (3.3): each slider's value column, beside it in the scene ("<Name>Slider" then "<Name>Value").
            _valueLabels.Clear();
            foreach (HSlider slider in new[] {
                         _masterSlider, _musicSlider, _sfxSlider, _uiSlider, _hudOpacitySlider, _screenShakeSlider,
                         _uiScaleSlider, _deadzoneSlider, _downThresholdSlider, _hapticSlider }) {
                if (slider == null) continue;
                string valueName = slider.Name.ToString().Replace("Slider", "Value");
                if (slider.GetParent()?.GetNodeOrNull<Label>(valueName) is { } value) _valueLabels[slider] = value;
            }
        }

        /// <summary>The value column's text for a slider (3.3, T53): its value as a whole percentage (hud_percent).</summary>
        public static string FormatSliderValue(double value) =>
            string.Format(TranslationServer.Translate("hud_percent"), Mathf.RoundToInt((float)value * 100f));

        private void RefreshValueLabels() {
            foreach (KeyValuePair<Godot.Range, Label> pair in _valueLabels) {
                if (IsInstanceValid(pair.Value)) pair.Value.Text = FormatSliderValue(pair.Key.Value);
            }
        }

        private HSlider Slider(string path) => _tabs.GetNode<HSlider>(path);

        /// <summary>A tab's title: its translation, upper-cased for the current locale (T55).</summary>
        public static string TabTitle(string key) => Pulp.PulpButtonDecor.ToDrawnText(TranslationServer.Translate(key));

        private void LocalizeStaticText() {
            _root.GetNode<Label>("Margin/Panel/Body/Title").Text = Tr("menu_settings").ToUpperInvariant();
            _root.GetNode<Button>("Margin/Panel/Body/Footer/BackButton").Text = Tr("common_back");

            // TabContainer renders the child node names unless the titles are set
            // explicitly, which is why the tabs used to read as raw English.
            // TG-PAUSE (3.3, T55): tab labels are upper case (a tab title is resolved text, set here, not a raw key).
            for (int index = 0; index < TabTitleKeys.Length && index < _tabs.GetTabCount(); index++) {
                _tabs.SetTabTitle(index, TabTitle(TabTitleKeys[index]));
            }

            _tabs.GetNode<Label>("Audio/MasterLabel").Text = Tr("settings_master_volume");
            _tabs.GetNode<Label>("Audio/MusicLabel").Text = Tr("settings_music_volume");
            _tabs.GetNode<Label>("Audio/SfxLabel").Text = Tr("settings_sfx_volume");
            _tabs.GetNode<Label>("Audio/UiLabel").Text = Tr("settings_ui_volume");

            _tabs.GetNode<Label>("Display/ResolutionLabel").Text = Tr("settings_resolution");
            _tabs.GetNode<Label>("Display/WindowModeLabel").Text = Tr("settings_window_mode");
            _vsyncToggle.Text = Tr("settings_vsync");

            _hapticToggle.Text = Tr("settings_haptics");
            _tabs.GetNode<Label>("Controls/Options/HapticLabel").Text = Tr("settings_haptic_intensity");
            // G15d: the build version, from the one canonical setting.
            _versionLabel.Text = string.Format(Tr("settings_build_version"), BuildInfo.Version);
            _damageNumbersToggle.Text = Tr("settings_damage_numbers");
            _tabs.GetNode<Label>("Gameplay/HudOpacityLabel").Text = Tr("settings_hud_opacity");
            _tabs.GetNode<Label>("Gameplay/ScreenShakeLabel").Text = Tr("settings_screen_shake");
            _tabs.GetNode<Label>("Gameplay/UiScaleLabel").Text = Tr("settings_ui_scale");
            _reducedEffectsToggle.Text = Tr("settings_reduced_temporal_effects");
            _reducedEffectsHelp.Text = Tr("settings_reduced_temporal_effects_help");

            _tabs.GetNode<Label>("Controls/Hint").Text = Tr("controls_hint");
            _tabs.GetNode<Label>("Controls/ReadOnlyInfo").Text = Tr("controls_ultimate_readonly");
            _tabs.GetNode<Label>("Controls/Scroll/Content/ShortcutsHeading").Text = Tr("controls_shortcuts_heading");
            _tabs.GetNode<Button>("Controls/Scroll/Content/ResetAllButton").Text = Tr("controls_reset_all");
            _controlsStatus.Text = "";
        }

        private void PopulateDropdowns() {
            _resolutionDropdown.Clear();
            foreach ((int width, int height) in GlobalSaveData.SupportedResolutions) {
                _resolutionDropdown.AddItem($"{width}x{height}");
            }
            _windowModeDropdown.Clear();
            foreach (string key in WindowModeKeys) _windowModeDropdown.AddItem(Tr(key));
            _textSpeedDropdown.Clear();
            foreach (TextSpeed speed in TextSpeedOrder) _textSpeedDropdown.AddItem(Tr(TextSpeedRules.LabelKey(speed)));
            _crashReportsDropdown.Clear();
            foreach (CrashReportMode mode in CrashReportOrder) _crashReportsDropdown.AddItem(Tr(CrashReportPolicy.LabelKey(mode)));
            _blockModeDropdown.Clear();
            _blockModeDropdown.AddItem(Tr("settings_block_mode_hold"));
            _blockModeDropdown.AddItem(Tr("settings_block_mode_toggle"));
            PopulateStickDevices();
        }

        /// <summary>
        /// G13 "per-device": the shared default profile plus one entry per
        /// connected controller, keyed by the joypad GUID the input layer resolves.
        /// </summary>
        private void PopulateStickDevices() {
            _stickDeviceDropdown.Clear();
            _stickDeviceKeys.Clear();
            _stickDeviceDropdown.AddItem(Tr("settings_stick_device_all"));
            _stickDeviceKeys.Add(StickProfiles.DefaultKey);
            Godot.Collections.Array<int> pads = Input.GetConnectedJoypads();
            using (pads.AsDisposable()) {
                foreach (int device in pads) {
                    string guid = InputManager.Instance?.JoypadGuid(device) ?? Input.GetJoyGuid(device);
                    if (string.IsNullOrWhiteSpace(guid) || _stickDeviceKeys.Contains(guid)) continue;
                    _stickDeviceDropdown.AddItem(Input.GetJoyName(device));
                    _stickDeviceKeys.Add(guid);
                }
            }
            _stickDeviceDropdown.Selected = 0;
        }

        /// <summary>The profile key the stick sliders currently edit.</summary>
        private string SelectedStickKey =>
            _stickDeviceKeys.Count == 0
                ? StickProfiles.DefaultKey
                : _stickDeviceKeys[Math.Clamp(_stickDeviceDropdown.Selected, 0, _stickDeviceKeys.Count - 1)];

        private void ShowStickProfile() {
            StickProfile profile = StickProfiles.Resolve(_workingStickProfiles, SelectedStickKey);
            _suppressStickWrites = true;
            _deadzoneSlider.Value = profile.Deadzone;
            _downThresholdSlider.Value = profile.DownThreshold;
            _suppressStickWrites = false;
        }

        /// <summary>Writes the sliders into the selected device's own profile entry.</summary>
        private void OnStickSliderChanged() {
            if (_suppressStickWrites) return;
            string key = SelectedStickKey;
            if (!_workingStickProfiles.TryGetValue(key, out StickProfile profile) || profile == null) {
                profile = StickProfiles.Resolve(_workingStickProfiles, key).Clone();
                _workingStickProfiles[key] = profile;
            }
            profile.Deadzone = (float)_deadzoneSlider.Value;
            profile.DownThreshold = (float)_downThresholdSlider.Value;
            profile.Normalize();
            // Live: the input layer reads the saved dictionary, so the working copy
            // is pushed straight in — the next sampled frame uses the new stick.
            GlobalSaveData data = SaveManager.Instance?.GlobalData;
            if (data != null) data.StickProfiles = CloneProfiles(_workingStickProfiles);
        }

        private static Dictionary<string, StickProfile> CloneProfiles(Dictionary<string, StickProfile> source) {
            var copy = new Dictionary<string, StickProfile>(StringComparer.Ordinal);
            if (source == null) return copy;
            foreach (KeyValuePair<string, StickProfile> pair in source) {
                if (pair.Value != null) copy[pair.Key] = pair.Value.Clone();
            }
            return copy;
        }

        /// <summary>The stick profiles the Controls tab is editing. Test seam.</summary>
        internal IReadOnlyDictionary<string, StickProfile> WorkingStickProfiles => _workingStickProfiles;

        private void WireSignals() {
            _masterSlider.ValueChanged += value => AudioManager.Instance?.SetMasterVolume((float)value);
            _musicSlider.ValueChanged += value => AudioManager.Instance?.SetMusicVolume((float)value);
            _sfxSlider.ValueChanged += value => AudioManager.Instance?.SetSFXVolume((float)value);
            _uiSlider.ValueChanged += value => AudioManager.Instance?.SetUIVolume((float)value);
            // M27 mutes apply live, keep the slider value, and touch only the
            // four user category buses (never the C01b critical-cue path).
            _masterMute.Toggled += on => AudioManager.Instance?.SetCategoryMuted(AudioBuses.Master, on);
            _musicMute.Toggled += on => AudioManager.Instance?.SetCategoryMuted(AudioBuses.Music, on);
            _sfxMute.Toggled += on => AudioManager.Instance?.SetCategoryMuted(AudioBuses.SFX, on);
            _uiMute.Toggled += on => AudioManager.Instance?.SetCategoryMuted(AudioBuses.UI, on);
            _muteWhenUnfocused.Toggled += on => AudioManager.Instance?.SetMuteWhenUnfocused(on);

            _stickDeviceDropdown.ItemSelected += _ => ShowStickProfile();
            _deadzoneSlider.ValueChanged += _ => OnStickSliderChanged();
            _downThresholdSlider.ValueChanged += _ => OnStickSliderChanged();
            _blockModeDropdown.ItemSelected += index => {
                // A mode switch never leaves a stale latch behind.
                InputManager.Instance?.ClearBlockLatches();
                GlobalSaveData data = SaveManager.Instance?.GlobalData;
                if (data != null) data.BlockInputMode = BlockModeOrder[Math.Clamp((int)index, 0, BlockModeOrder.Length - 1)];
            };
            _tabs.GetNode<Button>("Gameplay/CrashReportsRow/OpenLogsButton").Pressed +=
                () => CrashReportService.OpenLogsFolder();

            _resolutionDropdown.ItemSelected += OnDisplayChanged;
            _windowModeDropdown.ItemSelected += OnDisplayChanged;
            _vsyncToggle.Toggled += _ => OnDisplayChanged(0);

            _hapticToggle.Toggled += on => HapticFeedbackManager.Instance?.SetEnabled(on);
            _hapticSlider.ValueChanged += value => HapticFeedbackManager.Instance?.SetIntensity((float)value);
            _screenShakeSlider.ValueChanged += value => CameraShake.Instance?.SetIntensityScale((float)value);
            // Live preview: rescaling the shared theme repaints every open
            // screen (this one included) as the slider moves.
            _uiScaleSlider.ValueChanged += value => UIPalette.ApplyUiScale((float)value);
            // C01a applies LIVE: the preset reaches every presentation surface the
            // moment it is toggled, without restarting a level, replaying a proc or
            // resetting a gameplay timer. It is pushed here as well as on save so a
            // player who backs out with Escape still sees what they chose.
            _reducedEffectsToggle.Toggled += on => ComfortSettings.Apply(on);

            _root.GetNode<Button>("Margin/Panel/Body/Footer/BackButton").Pressed += Close;
            _tabs.GetNode<Button>("Controls/Scroll/Content/ResetAllButton").Pressed += OnResetAllBindings;

            // H-10: the chain is a property of the visible controls, so switching
            // tabs swaps the whole middle of it and it must be rebuilt.
            _tabs.TabChanged += _ => {
                if (Visible) RebuildFocusChain();
            };

            // TG-PAUSE (3.3): a slider's value column follows it live.
            foreach (KeyValuePair<Godot.Range, Label> pair in _valueLabels) {
                Label value = pair.Value;
                pair.Key.ValueChanged += v => value.Text = FormatSliderValue(v);
            }
        }

        /// <summary>
        /// H-10 (audit 2026-08-08). Authors focus for whatever is visible right now
        /// and takes initial focus. Before this existed the Settings overlay had no
        /// focus authoring at all: a controller or keyboard player could not reach a
        /// single control, and navigation input kept driving the opener's chain
        /// underneath the overlay. Grabbing focus into this chain is also what stops
        /// the surface beneath from acting on the same presses.
        /// </summary>
        private void RebuildFocusChain() {
            if (_root == null) return;
            _focusChain = FocusChainBuilder.Apply(_root);
        }

        // ------------------------------------------------------------------
        // Controls tab
        // ------------------------------------------------------------------

        private void BuildControlsTab() {
            if (_actionRows == null) return;
            // Detach before freeing: QueueFree alone leaves the old rows parented
            // until the end of the frame, and Show() rebuilds this list every time.
            // Detach and free immediately. QueueFree here would leave the previous
            // ~60 row nodes alive until the end of the frame, which reads as an
            // orphan-node leak in the test harness and is pure churn at runtime.
            Godot.Collections.Array<Node> existing = _actionRows.GetChildren();
            using (existing.AsDisposable()) {
                foreach (Node child in existing) {
                    _actionRows.RemoveChild(child);
                    child.Free();
                }
            }
            _keyboardSlots.Clear();
            _joypadSlots.Clear();
            _unbindButtons.Clear();
            // C01c: the live InputMap cannot express "explicitly Unbound" or a
            // shortcut flag, so the persisted set is handed in and the working set
            // carries those choices forward.
            _workingBindings = InputBindingService.CaptureEffective(
                SaveManager.Instance?.GlobalData?.InputBindings);

            // C01c lists EVERY gameplay action, including the ones whose direct
            // slot ships Unbound and the Story-only Time Freeze. Actions A1c and A2
            // have not landed yet are simply absent from BindableActions rather
            // than rendering a broken row.
            foreach (string action in InputBindingService.BindableActions()) {
                string capturedAction = action;
                // TG-PAUSE (9.2, 3.3): a 62 px settings row: the action label (T52), a keyboard and a gamepad binding cell
                // (44 px glass fields), and the Unbind and Reset compact chevrons, the columns kept by ApplyColumnWidths.
                var row = new HBoxContainer { Name = $"Row_{action}", CustomMinimumSize = new Vector2(0f, ControlsRowHeight) };
                row.AddThemeConstantOverride("separation", ControlsColumnGap);

                var label = new Label {
                    Text = ActionLabel(action),
                    CustomMinimumSize = new Vector2(LabelColumnWidth, 0),
                    Uppercase = true,
                    VerticalAlignment = VerticalAlignment.Center,
                    ThemeTypeVariation = UIPalette.LabelRoles.SettingsRowLabel,
                    ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
                };
                row.AddChild(label);

                var keyboardSlot = NewSlotButton(KeyboardColumnWidth);
                keyboardSlot.Pressed += () => BeginListening(capturedAction, InputDeviceKind.Keyboard);
                row.AddChild(keyboardSlot);
                _keyboardSlots[action] = keyboardSlot;

                var joypadSlot = NewSlotButton(PadColumnWidth);
                joypadSlot.Pressed += () => BeginListening(capturedAction, InputDeviceKind.Joypad);
                row.AddChild(joypadSlot);
                _joypadSlots[action] = joypadSlot;

                // C01c: an explicit Unbound is a real choice, distinct from "no
                // override, inherit the default". Without a control for it the
                // player could never reach the state the save format now stores.
                var unbind = NewRowChevron(Tr("controls_unbind"), UnbindColumnWidth);
                unbind.Pressed += () => UnbindSlot(capturedAction, InputDeviceKind.Keyboard);
                row.AddChild(unbind);
                _unbindButtons[action] = unbind;

                var reset = NewRowChevron(Tr("controls_reset_action"), ResetColumnWidth);
                reset.Pressed += () => ResetSingleAction(capturedAction);
                row.AddChild(reset);

                // A focused chevron slides 14 px and glows: the row keeps the derived focus gap between its buttons.
                PulpScreenKit.SeparateFocusRow(row);
                _actionRows.AddChild(row);
            }

            BuildShortcutRows();

            RefreshBindingLabels();
        }

        // The Controls table's column widths: fixed, so a long binding ("Pad Axis 0-, Pad Button 13") clips with an
        // ellipsis (its tooltip carries the whole text) instead of pushing its row's other columns out of line.
        internal const float LabelColumnWidth = 300f, KeyboardColumnWidth = 260f, PadColumnWidth = 340f, UnbindColumnWidth = 200f, ResetColumnWidth = 200f;

        /// <summary>A remap row (UI_SPEC_GLASS 3.3: 62 tall), its cells 12 apart; a binding cell is a 44 px glass field.</summary>
        internal const float ControlsRowHeight = 62f, BindingCellHeight = 44f;
        internal const int ControlsColumnGap = 12;

        private static Button NewSlotButton(float width) => new Button {
            CustomMinimumSize = new Vector2(width, BindingCellHeight), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };

        /// <summary>The Unbind and Reset buttons of a remap row: compact chevrons with no badge (3.3, the binding cell).</summary>
        private static Button NewRowChevron(string text, float width) {
            var button = new Button {
                Text = text,
                CustomMinimumSize = new Vector2(width, ConfirmModal.ModalButtonHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                ThemeTypeVariation = UIPalette.Variations.PulpButtonCompact
            };
            Pulp.PulpButtonDecor.Apply(button, compact: true).ShowPrompt = false;
            return button;
        }

        /// <summary>
        /// C01c's preset shortcuts. Their action recipes are FIXED — there is no
        /// chord-capture editor — so each row is one On/Off toggle plus a label
        /// naming the recipe's CURRENT component bindings. Remapping a component
        /// updates the label; it never redefines the recipe.
        /// </summary>
        private void BuildShortcutRows() {
            if (_shortcutRows == null) return;
            Godot.Collections.Array<Node> existing = _shortcutRows.GetChildren();
            using (existing.AsDisposable()) {
                foreach (Node child in existing) {
                    _shortcutRows.RemoveChild(child);
                    child.Free();
                }
            }
            foreach (InputShortcuts.Recipe recipe in InputShortcuts.Recipes) {
                foreach (InputDeviceKind deviceKind in DeviceKinds) {
                    if (!InputShortcuts.AppliesTo(recipe, deviceKind)) continue;
                    InputShortcuts.Recipe capturedRecipe = recipe;
                    InputDeviceKind capturedKind = deviceKind;
                    var row = new HBoxContainer { Name = $"Shortcut_{recipe.Action}_{(int)deviceKind}" };
                    row.AddThemeConstantOverride("separation", 8);

                    var toggle = new CheckButton {
                        Text = Tr(recipe.LabelKey),
                        ButtonPressed = _workingBindings.IsShortcutEnabled(recipe.Action, deviceKind)
                    };
                    toggle.Toggled += on => {
                        _workingBindings.SetShortcutEnabled(capturedRecipe.Action, capturedKind, on);
                        RefreshBindingLabels();
                        SetStatus(ReachabilityWarning() ?? "");
                    };
                    SkinToggle(toggle, ShortcutToggleWidth);
                    row.AddChild(toggle);

                    // The chord's CURRENT buttons, not a hard-coded "LB+RB": C01c
                    // is explicit that such a label describes defaults, and a
                    // remapped component must be reflected here.
                    row.AddChild(new Label {
                        Text = DescribeRecipe(recipe, deviceKind),
                        VerticalAlignment = VerticalAlignment.Center,
                        ThemeTypeVariation = UIPalette.LabelRoles.BodySmallLabel
                    });
                    row.CustomMinimumSize = new Vector2(0f, ControlsRowHeight);
                    _shortcutRows.AddChild(row);
                }
            }
        }

        private static readonly InputDeviceKind[] DeviceKinds =
            { InputDeviceKind.Keyboard, InputDeviceKind.Joypad };

        /// <summary>Localized action name, covering C01c's four direct actions.</summary>
        private static string ActionLabel(string action) =>
            TranslationServer.Translate(InputShortcuts.ActionLabelKey(action));

        /// <summary>Renders a recipe as its components' live bindings, joined.</summary>
        private string DescribeRecipe(InputShortcuts.Recipe recipe, InputDeviceKind deviceKind) {
            var parts = new List<string>();
            foreach (string component in recipe.Components) {
                parts.Add(InputBindingService.DescribeAll(_workingBindings.EventsFor(component, deviceKind)));
            }
            return string.Join(" + ", parts);
        }

        /// <summary>
        /// C01c's explicit Unbound. It clears the slot rather than deleting the
        /// row, and the reachability check runs immediately so the player is told
        /// at once that they just made Ultimate or Grab unreachable —
        /// rather than discovering it in a match.
        /// </summary>
        private void UnbindSlot(string action, InputDeviceKind deviceKind) {
            CancelListening();
            _workingBindings.SetUnbound(action, deviceKind, unbound: true);
            RefreshBindingLabels();
            SetStatus(ReachabilityWarning() ?? Tr("controls_unbound_done"));
        }

        /// <summary>
        /// C01c: "Do not silently leave Ultimate/Grab unreachable when
        /// their shortcut is disabled. Explain the missing route." Returns the
        /// localized explanation, or null when every required verb keeps a route.
        /// </summary>
        internal string ReachabilityWarning() {
            if (InputShortcuts.ProfileIsReachable(_workingBindings, out List<string> unreachable)) return null;
            var names = new List<string>();
            foreach (string action in unreachable) names.Add(ActionLabel(action));
            return string.Format(Tr("controls_unreachable_action"), string.Join(", ", names));
        }

        private void RefreshBindingLabels() {
            if (!IsListening) SetListeningCell(null);
            foreach (string action in InputBindingService.BindableActions()) {
                IReadOnlyList<InputBindingEvent> events = _workingBindings.For(action);
                if (_keyboardSlots.TryGetValue(action, out Button keyboardSlot) && IsInstanceValid(keyboardSlot)) {
                    keyboardSlot.Text = DescribeSlot(events, InputDeviceKind.Keyboard);
                    keyboardSlot.TooltipText = keyboardSlot.Text;
                }
                if (_joypadSlots.TryGetValue(action, out Button joypadSlot) && IsInstanceValid(joypadSlot)) {
                    joypadSlot.Text = DescribeSlot(events, InputDeviceKind.Joypad);
                    joypadSlot.TooltipText = joypadSlot.Text;
                }
            }
            ApplyColumnWidths();
        }

        private float _columnScale = 1f;

        /// <summary>Re-fits the columns when the UI scale changes while Settings is open.</summary>
        public override void _Process(double delta) {
            if (_actionRows == null || Mathf.IsEqualApprox(_columnScale, UIPalette.AppliedUiScale)) return;
            ApplyColumnWidths();
        }

        /// <summary>
        /// One width per column, the same for every row so the table reads as columns: the 1.0 widths, grown to the widest
        /// text in that column at the current font size (the default pad bindings at 1.4 need more than 340), and capped
        /// so the table keeps fitting the tab. A binding longer than the cap still clips with an ellipsis and its tooltip.
        /// </summary>
        private void ApplyColumnWidths() {
            if (_actionRows == null) return;
            _columnScale = UIPalette.AppliedUiScale;
            float[] width = { LabelColumnWidth, KeyboardColumnWidth, PadColumnWidth, UnbindColumnWidth, ResetColumnWidth };
            // TG-PAUSE: the caps grow with the UI scale (the labels are T52 capitals and the last two columns chevrons now,
            // both wider than the pulp cells); MenuSafeAreaFit keeps the whole slab on screen at 1.4.
            float capScale = Mathf.Max(1f, _columnScale);
            float[] cap = { 440f * capScale, 360f * capScale, 520f * capScale, 260f * capScale, 260f * capScale };
            int rows = _actionRows.GetChildCount();
            for (int r = 0; r < rows; r++) {
                if (_actionRows.GetChild(r) is not HBoxContainer row) continue;
                for (int c = 0; c < 5 && c < row.GetChildCount(); c++) {
                    if (row.GetChild(c) is not Control cell) continue;
                    width[c] = Mathf.Max(width[c], Mathf.Min(MeasureCell(cell), cap[c]));
                }
            }
            for (int r = 0; r < rows; r++) {
                if (_actionRows.GetChild(r) is not HBoxContainer row) continue;
                for (int c = 0; c < 5 && c < row.GetChildCount(); c++) {
                    if (row.GetChild(c) is Control cell) cell.CustomMinimumSize = new Vector2(width[c], cell.CustomMinimumSize.Y);
                }
            }
        }

        /// <summary>
        /// The width a Controls cell needs for its text whole: a decorated chevron its decor's own required width (the
        /// upper-case label at the UI scale, no badge); a label its text as drawn (upper-cased when the label draws capitals);
        /// a field its text and plate, plus the cell's 28 px air.
        /// </summary>
        private static float MeasureCell(Control cell) {
            if (cell is Button decorated && decorated.GetNodeOrNull<Pulp.PulpButtonDecor>("PulpButtonDecor") != null) {
                return Pulp.PulpButtonDecor.RequiredWidth(decorated, reservePrompt: false);
            }
            string text = cell is Label l ? (l.Uppercase ? Pulp.PulpButtonDecor.ToDrawnText(l.Text) : l.Text)
                : cell is Button b ? b.Text : "";
            Font font = cell.GetThemeFont("font");
            float chrome = (cell.GetThemeStylebox("normal")?.GetMinimumSize().X ?? 0f) + 28f;
            return font.GetStringSize(text, HorizontalAlignment.Left, -1f, cell.GetThemeFontSize("font_size")).X + chrome;
        }

        /// <summary>
        /// Test seam: drives the listen-capture path without a physical device.
        /// Returns the action that blocked the capture, or "" when it applied.
        /// </summary>
        internal string CaptureForTesting(string action, InputDeviceKind deviceKind, InputBindingEvent captured) {
            BeginListening(action, deviceKind);
            string conflict = InputBindingConflicts.FindConflictingAction(_workingBindings, action, captured);
            CommitCapture(captured);
            return conflict;
        }

        private static string DescribeSlot(IReadOnlyList<InputBindingEvent> events, InputDeviceKind deviceKind) {
            List<InputBindingEvent> matching = events.Where(candidate => candidate.DeviceKind == deviceKind).ToList();
            return InputBindingService.DescribeAll(matching);
        }

        private void BeginListening(string action, InputDeviceKind deviceKind) {
            _listeningAction = action;
            _listeningDeviceKind = deviceKind;
            Button slot = deviceKind == InputDeviceKind.Keyboard ? _keyboardSlots[action] : _joypadSlots[action];
            slot.Text = Tr("controls_listening");
            // UI_SPEC_GLASS 3.3 (the binding cell): while listening the cell keeps its focus look and reads in res2, steady.
            SetListeningCell(slot);
            SetStatus(Tr(deviceKind == InputDeviceKind.Keyboard
                ? "controls_listening_keyboard"
                : "controls_listening_joypad"), UIPalette.Res2);
        }

        private void CancelListening() {
            if (!IsListening) return;
            _listeningAction = "";
            if (_controlsStatus != null) SetStatus("");
            RefreshBindingLabels();
        }

        private static readonly string[] CellTextColorSlots = {
            "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color"
        };

        /// <summary>Marks <paramref name="slot"/> as the listening cell (its text in res2); null clears the mark.</summary>
        private void SetListeningCell(Button slot) {
            if (_listeningSlot != null && IsInstanceValid(_listeningSlot)) {
                foreach (string name in CellTextColorSlots) _listeningSlot.RemoveThemeColorOverride(name);
            }
            _listeningSlot = slot;
            if (slot == null) return;
            foreach (string name in CellTextColorSlots) slot.AddThemeColorOverride(name, UIPalette.Res2);
        }

        /// <summary>
        /// The Controls status line (3.3): listening in res2, a refused conflict in danger, anything else in the role's ink2.
        /// </summary>
        private void SetStatus(string text, Color? color = null) {
            if (_controlsStatus == null) return;
            _controlsStatus.Text = text ?? "";
            if (color.HasValue) _controlsStatus.AddThemeColorOverride("font_color", color.Value);
            else _controlsStatus.RemoveThemeColorOverride("font_color");
        }

        public override void _Input(InputEvent @event) {
            if (!Visible || !IsListening || @event == null) return;
            if (@event is InputEventKey escape && escape.Pressed && escape.PhysicalKeycode == Key.Escape) {
                CancelListening();
                GetViewport()?.SetInputAsHandled();
                return;
            }
            InputBindingEvent captured = TryCapture(@event);
            if (captured == null) {
                // CM-PADUI (2026-10-16): pad B (ui_cancel) while a keyboard slot
                // listens cancels the listen, as Escape does for a pad slot. A pad
                // slot captures B as a binding above, so B stays bindable.
                if (!@event.IsActionPressed(PadMenuInput.CancelAction)) return;
                CancelListening();
                GetViewport()?.SetInputAsHandled();
                return;
            }
            GetViewport()?.SetInputAsHandled();
            CommitCapture(captured);
        }

        /// <summary>
        /// Converts a live event into a binding, honouring the listening slot's
        /// device kind. Analog axes need a real deflection so a resting stick does
        /// not bind itself the moment a joypad slot is armed.
        /// </summary>
        private InputBindingEvent TryCapture(InputEvent @event) {
            bool wantsKeyboard = _listeningDeviceKind == InputDeviceKind.Keyboard;
            switch (@event) {
                case InputEventKey key when wantsKeyboard && key.Pressed && !key.Echo:
                    return InputBindingService.FromInputEvent(key);
                case InputEventMouseButton mouse when wantsKeyboard && mouse.Pressed:
                    return InputBindingService.FromInputEvent(mouse);
                case InputEventJoypadButton button when !wantsKeyboard && button.Pressed:
                    return InputBindingService.FromInputEvent(button);
                case InputEventJoypadMotion motion when !wantsKeyboard && Mathf.Abs(motion.AxisValue) >= 0.7f:
                    return InputBindingService.FromInputEvent(motion);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Conflict policy: block. One physical event drives at most one action in
        /// the local action space, and a refused capture explains which action
        /// already owns the input rather than silently moving that binding.
        /// </summary>
        private void CommitCapture(InputBindingEvent captured) {
            string action = _listeningAction;
            string conflict = InputBindingConflicts.FindConflictingAction(_workingBindings, action, captured);
            if (conflict.Length > 0) {
                _listeningAction = "";
                RefreshBindingLabels();
                SetStatus(string.Format(
                    Tr("controls_conflict"), Tr(InputManager.ActionLabelKey(conflict))), UIPalette.Danger);
                return;
            }

            // Replace only this device kind's events; the other kind's slot keeps
            // whatever it already had.
            List<InputBindingEvent> next = _workingBindings.For(action)
                .Where(existing => existing.DeviceKind != _listeningDeviceKind)
                .Select(existing => existing.Clone())
                .ToList();
            next.Add(captured);
            _workingBindings.Set(action, next);
            // A fresh capture is the opposite of an explicit Unbound: lift it, or
            // Normalize would strip the binding straight back out on save.
            _workingBindings.SetUnbound(action, _listeningDeviceKind, unbound: false);

            _listeningAction = "";
            RefreshBindingLabels();
            SetStatus(ReachabilityWarning() ?? Tr("controls_rebound"));
        }

        /// <summary>
        /// C01c's per-action reset: "restores that action's direct default AND its
        /// preset flags for the selected device". Clearing the explicit Unbound is
        /// the half that is easy to forget — without it the row would show the
        /// default while the payload still said the slot was cleared on purpose.
        /// </summary>
        private void ResetSingleAction(string action) {
            CancelListening();
            IReadOnlyList<InputBindingEvent> defaults = InputBindingService.ProjectDefaults.For(action);
            _workingBindings.Set(action, defaults);
            foreach (InputDeviceKind deviceKind in DeviceKinds) _workingBindings.ResetSlot(action, deviceKind);
            RefreshBindingLabels();
            BuildShortcutRows();
            SetStatus(Tr("controls_reset_action_done"));
        }

        private void OnResetAllBindings() {
            CancelListening();
            SaveManager.Instance?.ResetInputBindingsToDefault();
            // Global reset restores the WHOLE control profile, so the C01c maps go
            // with it: no persisted set is handed in here on purpose.
            _workingBindings = InputBindingService.CaptureEffective();
            RefreshBindingLabels();
            BuildShortcutRows();
            SetStatus(Tr("controls_reset_all_done"));
        }

        // ------------------------------------------------------------------
        // Display
        // ------------------------------------------------------------------

        private void OnDisplayChanged(long _) {
            GlobalSaveData data = SaveManager.Instance?.GlobalData;
            if (data == null) return;
            ReadDisplayInto(data);
            ViewportEnforcer.ApplyDisplaySettings(data);
        }

        private void ReadDisplayInto(GlobalSaveData data) {
            int index = Math.Clamp(_resolutionDropdown.Selected, 0, GlobalSaveData.SupportedResolutions.Length - 1);
            (int width, int height) = GlobalSaveData.SupportedResolutions[index];
            data.ResolutionWidth = width;
            data.ResolutionHeight = height;
            data.WindowMode = (WindowModeSetting)Math.Clamp(_windowModeDropdown.Selected, 0, WindowModeKeys.Length - 1);
            data.VSyncEnabled = _vsyncToggle.ButtonPressed;
        }

        // ------------------------------------------------------------------
        // Open / close / persistence
        // ------------------------------------------------------------------

        public new void Show() {
            LoadSettings();
            BuildControlsTab();
            Visible = true;
            if (_shade != null) _shade.Visible = !UsesHostDim;
            RebuildFocusChain();
            // MOT-A row 4: the slab fades and rises (its own dim fading in with it unless it sits on a host's).
            _panel?.PlayAppear(UsesHostDim ? null : _shade);
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (!Visible || @event == null) return;
            // CM-PADUI (2026-10-16): ui_cancel (pad B, Escape) closes Settings as
            // the pause key does, so a pad backs out with B and the press never
            // falls through to the pause menu underneath.
            if (!@event.IsActionPressed(InputManager.Actions.Pause)
                && !@event.IsActionPressed(PadMenuInput.CancelAction)) return;
            Close();
            GetViewport()?.SetInputAsHandled();
        }

        /// <summary>
        /// Saves, hides, and hands control back to the opener. It must never write
        /// <see cref="SceneTree.Paused"/> — the pause menu that opened it still owns
        /// the pause (plan §2.6).
        /// </summary>
        public void Close() {
            CancelListening();
            SaveSettings();
            Visible = false;
            _panel?.StopAppear(UsesHostDim ? null : _shade);
            Closed?.Invoke();
        }

        private void LoadSettings() {
            GlobalSaveData data = SaveManager.Instance?.GlobalData;
            if (data == null || _root == null) return;
            data.Normalize();
            _masterSlider.Value = data.MasterVolume;
            _musicSlider.Value = data.MusicVolume;
            _sfxSlider.Value = data.SFXVolume;
            _uiSlider.Value = data.UIVolume;
            _hapticSlider.Value = data.HapticIntensity;
            _hapticToggle.ButtonPressed = data.HapticsEnabled;
            _damageNumbersToggle.ButtonPressed = data.DamageNumbersVisible;
            _hudOpacitySlider.Value = data.HudOpacity;
            _screenShakeSlider.Value = data.ScreenShakeScale;
            _uiScaleSlider.Value = data.UiScale;
            // C01a: the preset is independent of Screen Shake and is never
            // inferred from it — a player who turned shake off has said nothing
            // about distortion or after-images.
            _reducedEffectsToggle.ButtonPressed = data.ReducedTemporalEffects;
            _resolutionDropdown.Selected =
                GlobalSaveData.ResolutionIndex(data.ResolutionWidth, data.ResolutionHeight);
            _windowModeDropdown.Selected = (int)data.WindowMode;
            _vsyncToggle.ButtonPressed = data.VSyncEnabled;

            // Package 12 W6
            _masterMute.ButtonPressed = data.MasterMuted;
            _musicMute.ButtonPressed = data.MusicMuted;
            _sfxMute.ButtonPressed = data.SFXMuted;
            _uiMute.ButtonPressed = data.UIMuted;
            _muteWhenUnfocused.ButtonPressed = data.MuteWhenUnfocused;
            _textSpeedDropdown.Selected = Math.Max(0, Array.IndexOf(TextSpeedOrder, data.DialogueTextSpeed));
            _crashReportsDropdown.Selected = Math.Max(0, Array.IndexOf(CrashReportOrder, data.CrashReports));
            _blockModeDropdown.Selected = Math.Max(0, Array.IndexOf(BlockModeOrder, data.BlockInputMode));
            _workingStickProfiles = CloneProfiles(data.StickProfiles);
            PopulateStickDevices();
            ShowStickProfile();
            RefreshValueLabels();
        }

        private void SaveSettings() {
            SaveManager manager = SaveManager.Instance;
            GlobalSaveData data = manager?.GlobalData;
            if (data == null || _root == null) return;
            data.MasterVolume = (float)_masterSlider.Value;
            data.MusicVolume = (float)_musicSlider.Value;
            data.SFXVolume = (float)_sfxSlider.Value;
            data.UIVolume = (float)_uiSlider.Value;
            data.HapticIntensity = (float)_hapticSlider.Value;
            data.HapticsEnabled = _hapticToggle.ButtonPressed;
            data.DamageNumbersVisible = _damageNumbersToggle.ButtonPressed;
            data.HudOpacity = (float)_hudOpacitySlider.Value;
            data.ScreenShakeScale = (float)_screenShakeSlider.Value;
            data.UiScale = (float)_uiScaleSlider.Value;
            data.ReducedTemporalEffects = _reducedEffectsToggle.ButtonPressed;
            data.ApplyComfortSettings();
            // Package 12 W6
            data.MasterMuted = _masterMute.ButtonPressed;
            data.MusicMuted = _musicMute.ButtonPressed;
            data.SFXMuted = _sfxMute.ButtonPressed;
            data.UIMuted = _uiMute.ButtonPressed;
            data.MuteWhenUnfocused = _muteWhenUnfocused.ButtonPressed;
            data.DialogueTextSpeed = TextSpeedOrder[Math.Clamp(_textSpeedDropdown.Selected, 0, TextSpeedOrder.Length - 1)];
            data.CrashReports = CrashReportOrder[Math.Clamp(_crashReportsDropdown.Selected, 0, CrashReportOrder.Length - 1)];
            data.BlockInputMode = BlockModeOrder[Math.Clamp(_blockModeDropdown.Selected, 0, BlockModeOrder.Length - 1)];
            data.StickProfiles = CloneProfiles(_workingStickProfiles);
            ReadDisplayInto(data);
            // PersistInputBindings writes the global payload itself.
            manager.PersistInputBindings(_workingBindings);
        }
    }
}
