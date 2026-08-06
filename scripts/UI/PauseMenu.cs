using Godot;

namespace FTT.UI {
    public partial class PauseMenu : CanvasLayer {
        private bool _isPaused;
        private Control _panel;
        private SettingsMenu _settingsMenu;

        public override void _Ready() {
            Layer = 99;
            ProcessMode = ProcessModeEnum.Always;
            _panel = new PanelContainer();
            _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
            _panel.Visible = false;
            AddChild(_panel);

            var vbox = new VBoxContainer();
            _panel.AddChild(vbox);

            var title = new Label();
            title.Text = Tr("menu_paused");
            title.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(title);

            var resumeBtn = new Button { Text = Tr("menu_resume") };
            resumeBtn.Pressed += TogglePause;
            vbox.AddChild(resumeBtn);

            var settingsBtn = new Button { Text = Tr("menu_settings") };
            settingsBtn.Pressed += OpenSettings;
            vbox.AddChild(settingsBtn);

            var quitBtn = new Button { Text = Tr("pause_quit_to_menu") };
            quitBtn.Pressed += () => {
                GetTree().Paused = false;
                FTT.Core.GameManager.Instance?.LoadScene("res://scenes/menus/MainMenu.tscn");
            };
            vbox.AddChild(quitBtn);
        }

        private void OpenSettings() {
            if (_settingsMenu == null || !IsInstanceValid(_settingsMenu)) {
                _settingsMenu = new SettingsMenu { Name = "SettingsMenu" };
                AddChild(_settingsMenu);
            }
            _settingsMenu.Show();
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (@event.IsActionPressed(FTT.Core.InputManager.Actions.Pause)) {
                TogglePause();
                GetViewport().SetInputAsHandled();
            }
        }

        public void TogglePause() {
            _isPaused = !_isPaused;
            _panel.Visible = _isPaused;
            GetTree().Paused = _isPaused;
        }
    }
}
