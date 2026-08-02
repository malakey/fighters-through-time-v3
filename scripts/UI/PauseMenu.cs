using Godot;

namespace FTT.UI {
    public partial class PauseMenu : CanvasLayer {
        private bool _isPaused;
        private Control _panel;

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
            title.Text = "PAUSED";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(title);

            var resumeBtn = new Button { Text = "Resume" };
            resumeBtn.Pressed += TogglePause;
            vbox.AddChild(resumeBtn);

            var settingsBtn = new Button { Text = "Settings" };
            vbox.AddChild(settingsBtn);

            var quitBtn = new Button { Text = "Quit to Main Menu" };
            quitBtn.Pressed += () => {
                GetTree().Paused = false;
                FTT.Core.GameManager.Instance?.LoadScene("res://scenes/menus/MainMenu.tscn");
            };
            vbox.AddChild(quitBtn);
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
