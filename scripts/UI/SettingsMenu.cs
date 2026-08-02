using Godot;
using FTT.Core;

namespace FTT.UI {

    public partial class SettingsMenu : CanvasLayer {

        private VBoxContainer _container;
        private TabContainer _tabs;

        // Audio
        private HSlider _masterSlider;
        private HSlider _musicSlider;
        private HSlider _sfxSlider;
        private HSlider _uiSlider;

        // Display
        private OptionButton _resolutionDropdown;
        private CheckButton _fullscreenToggle;
        private CheckButton _vsyncToggle;

        // Gameplay
        private HSlider _hapticSlider;
        private CheckButton _hapticToggle;
        private OptionButton _difficultyDropdown;

        public override void _Ready() {
            ProcessMode = ProcessModeEnum.Always;
            Visible = false;
            BuildUI();
            LoadSettings();
        }

        private void BuildUI() {
            var bg = new ColorRect();
            bg.Color = new Color(0, 0, 0, 0.85f);
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(bg);

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 200);
            margin.AddThemeConstantOverride("margin_right", 200);
            margin.AddThemeConstantOverride("margin_top", 100);
            margin.AddThemeConstantOverride("margin_bottom", 100);
            AddChild(margin);

            var panel = new PanelContainer();
            margin.AddChild(panel);

            _container = new VBoxContainer();
            panel.AddChild(_container);

            var title = new Label();
            title.Text = "SETTINGS";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 32);
            _container.AddChild(title);

            _tabs = new TabContainer();
            _tabs.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _container.AddChild(_tabs);

            BuildAudioTab();
            BuildDisplayTab();
            BuildGameplayTab();

            var closeBtn = new Button();
            closeBtn.Text = "Back";
            closeBtn.Pressed += OnClosePressed;
            _container.AddChild(closeBtn);
        }

        private void BuildAudioTab() {
            var vbox = new VBoxContainer();
            vbox.Name = "Audio";
            _tabs.AddChild(vbox);

            _masterSlider = CreateSlider(vbox, "Master Volume", 0, 1, 0.05f, 1.0f);
            _masterSlider.ValueChanged += v => AudioManager.Instance?.SetMasterVolume((float)v);

            _musicSlider = CreateSlider(vbox, "Music Volume", 0, 1, 0.05f, 0.8f);
            _musicSlider.ValueChanged += v => AudioManager.Instance?.SetMusicVolume((float)v);

            _sfxSlider = CreateSlider(vbox, "SFX Volume", 0, 1, 0.05f, 1.0f);
            _sfxSlider.ValueChanged += v => AudioManager.Instance?.SetSFXVolume((float)v);

            _uiSlider = CreateSlider(vbox, "UI Volume", 0, 1, 0.05f, 1.0f);
            _uiSlider.ValueChanged += v => AudioManager.Instance?.SetUIVolume((float)v);
        }

        private void BuildDisplayTab() {
            var vbox = new VBoxContainer();
            vbox.Name = "Display";
            _tabs.AddChild(vbox);

            var resLabel = new Label { Text = "Resolution" };
            vbox.AddChild(resLabel);
            _resolutionDropdown = new OptionButton();
            _resolutionDropdown.AddItem("1920x1080");
            _resolutionDropdown.AddItem("1600x900");
            _resolutionDropdown.AddItem("1280x720");
            _resolutionDropdown.AddItem("1024x576");
            _resolutionDropdown.ItemSelected += OnResolutionChanged;
            vbox.AddChild(_resolutionDropdown);

            _fullscreenToggle = new CheckButton();
            _fullscreenToggle.Text = "Fullscreen";
            _fullscreenToggle.Toggled += OnFullscreenToggled;
            vbox.AddChild(_fullscreenToggle);

            _vsyncToggle = new CheckButton();
            _vsyncToggle.Text = "VSync";
            _vsyncToggle.ButtonPressed = true;
            _vsyncToggle.Toggled += OnVsyncToggled;
            vbox.AddChild(_vsyncToggle);
        }

        private void BuildGameplayTab() {
            var vbox = new VBoxContainer();
            vbox.Name = "Gameplay";
            _tabs.AddChild(vbox);

            _hapticToggle = new CheckButton();
            _hapticToggle.Text = "Controller Haptics";
            _hapticToggle.ButtonPressed = true;
            _hapticToggle.Toggled += on => HapticFeedbackManager.Instance?.SetEnabled(on);
            vbox.AddChild(_hapticToggle);

            _hapticSlider = CreateSlider(vbox, "Haptic Intensity", 0, 1, 0.05f, 0.7f);
            _hapticSlider.ValueChanged += v => HapticFeedbackManager.Instance?.SetIntensity((float)v);

            var diffLabel = new Label { Text = "Default Difficulty" };
            vbox.AddChild(diffLabel);
            _difficultyDropdown = new OptionButton();
            _difficultyDropdown.AddItem("Easy");
            _difficultyDropdown.AddItem("Normal");
            _difficultyDropdown.AddItem("Hard");
            _difficultyDropdown.Selected = 1;
            vbox.AddChild(_difficultyDropdown);
        }

        private HSlider CreateSlider(Control parent, string label, float min, float max, float step, float defaultVal) {
            var lbl = new Label { Text = label };
            parent.AddChild(lbl);
            var slider = new HSlider();
            slider.MinValue = min;
            slider.MaxValue = max;
            slider.Step = step;
            slider.Value = defaultVal;
            slider.CustomMinimumSize = new Vector2(300, 20);
            parent.AddChild(slider);
            return slider;
        }

        private void OnResolutionChanged(long index) {
            Vector2I[] resolutions = {
                new(1920, 1080), new(1600, 900), new(1280, 720), new(1024, 576)
            };
            if (index >= 0 && index < resolutions.Length) {
                DisplayServer.WindowSetSize(resolutions[index]);
            }
        }

        private void OnFullscreenToggled(bool on) {
            DisplayServer.WindowSetMode(on ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        }

        private void OnVsyncToggled(bool on) {
            DisplayServer.WindowSetVsyncMode(on ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        }

        private void OnClosePressed() {
            SaveSettings();
            Visible = false;
            GetTree().Paused = false;
        }

        public new void Show() {
            Visible = true;
        }

        private void LoadSettings() {
            var data = SaveManager.Instance?.GlobalData;
            if (data == null) return;
            _masterSlider.Value = data.MasterVolume;
            _musicSlider.Value = data.MusicVolume;
            _sfxSlider.Value = data.SFXVolume;
            _uiSlider.Value = data.UIVolume;
            _hapticSlider.Value = data.HapticIntensity;
            _hapticToggle.ButtonPressed = data.HapticsEnabled;
        }

        private void SaveSettings() {
            var data = SaveManager.Instance?.GlobalData;
            if (data == null) return;
            data.MasterVolume = (float)_masterSlider.Value;
            data.MusicVolume = (float)_musicSlider.Value;
            data.SFXVolume = (float)_sfxSlider.Value;
            data.UIVolume = (float)_uiSlider.Value;
            data.HapticIntensity = (float)_hapticSlider.Value;
            data.HapticsEnabled = _hapticToggle.ButtonPressed;
            SaveManager.Instance?.SaveGlobalData();
        }
    }
}
