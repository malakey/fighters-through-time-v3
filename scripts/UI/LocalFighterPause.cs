using Godot;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// Local Fighter Mode pause menu (design-godot.md Section 11 "Pause Screen
    /// Rules" and "Local Multiplayer Input Disconnect"). Any local player may
    /// pause. A controller disconnect forces the pause open with a modal naming
    /// the affected player and cannot be dismissed until that slot has a device
    /// again.
    ///
    /// Pause discipline: whoever sets <see cref="SceneTree.Paused"/> hands it
    /// back, including on teardown — a leaked pause freezes the next scene in
    /// game and hangs the whole GdUnit4 session.
    /// </summary>
    public partial class LocalFighterPause : CanvasLayer {
        private Control _panel;
        private Control _disconnectModal;
        private Label _disconnectLabel;
        private Control _exitConfirm;
        private SettingsMenu _settingsMenu;
        private bool _isPaused;
        private int _disconnectedPlayer = -1;

        /// <summary>True while this menu holds the scene tree paused.</summary>
        public bool IsPaused => _isPaused;

        /// <summary>Player slot whose device dropped, or -1 when every slot is bound.</summary>
        public int DisconnectedPlayer => _disconnectedPlayer;

        public override void _Ready() {
            Layer = 95;
            ProcessMode = ProcessModeEnum.Always;
            BuildPanel();
            BuildDisconnectModal();

            if (InputManager.Instance != null) {
                InputManager.Instance.PlayerDeviceDisconnected += OnPlayerDeviceDisconnected;
                InputManager.Instance.DeviceAssigned += OnDeviceAssigned;
            }
        }

        public override void _ExitTree() {
            if (InputManager.Instance != null) {
                InputManager.Instance.PlayerDeviceDisconnected -= OnPlayerDeviceDisconnected;
                InputManager.Instance.DeviceAssigned -= OnDeviceAssigned;
            }
            if (!_isPaused) return;
            _isPaused = false;
            SceneTree tree = GetTree();
            if (tree != null) tree.Paused = false;
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (@event == null || !@event.IsActionPressed(InputManager.Actions.Pause)) return;
            // A forced disconnect pause cannot be dismissed with the pause button.
            if (_disconnectedPlayer >= 0) {
                GetViewport()?.SetInputAsHandled();
                return;
            }
            TogglePause();
            GetViewport()?.SetInputAsHandled();
        }

        public void TogglePause() => SetPaused(!_isPaused);

        public void SetPaused(bool paused) {
            if (_isPaused == paused) return;
            _isPaused = paused;
            if (_panel != null) _panel.Visible = paused && _disconnectedPlayer < 0;
            if (!paused) HideExitConfirm();
            SceneTree tree = GetTree();
            if (tree != null) tree.Paused = paused;
        }

        /// <summary>
        /// Forces the pause and raises the modal. Public so the disconnect path is
        /// exercisable without a real controller.
        /// </summary>
        public void ForceDisconnectPause(int playerIndex) {
            _disconnectedPlayer = playerIndex;
            if (_disconnectLabel != null) {
                _disconnectLabel.Text = string.Format(Tr("fighter_disconnect_modal"), playerIndex + 1);
            }
            if (_disconnectModal != null) _disconnectModal.Visible = true;
            if (_panel != null) _panel.Visible = false;
            SetPaused(true);
        }

        /// <summary>Clears the modal and resumes once the slot has a device again.</summary>
        public void ResolveDisconnect(int playerIndex) {
            if (_disconnectedPlayer != playerIndex) return;
            _disconnectedPlayer = -1;
            if (_disconnectModal != null) _disconnectModal.Visible = false;
            SetPaused(false);
        }

        private void OnPlayerDeviceDisconnected(int playerIndex) => ForceDisconnectPause(playerIndex);

        private void OnDeviceAssigned(int playerIndex, int deviceID) {
            if (deviceID == InputManager.UnassignedDevice) return;
            ResolveDisconnect(playerIndex);
        }

        private void BuildPanel() {
            _panel = new PanelContainer { Name = "PausePanel", Visible = false };
            _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
            AddChild(_panel);

            var layout = new VBoxContainer();
            layout.AddThemeConstantOverride("separation", 14);
            _panel.AddChild(layout);

            var title = new Label {
                Text = Tr("menu_paused"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 28);
            layout.AddChild(title);

            var resume = MakeButton(Tr("menu_resume"));
            resume.Pressed += () => SetPaused(false);
            layout.AddChild(resume);

            var settings = MakeButton(Tr("menu_settings"));
            settings.Pressed += OpenSettings;
            layout.AddChild(settings);

            var exit = MakeButton(Tr("fighter_pause_exit"));
            exit.Pressed += ShowExitConfirm;
            layout.AddChild(exit);

            BuildExitConfirm(layout);
        }

        private void BuildExitConfirm(Control parent) {
            var confirm = new VBoxContainer { Name = "ExitConfirm", Visible = false };
            confirm.AddThemeConstantOverride("separation", 8);
            parent.AddChild(confirm);
            _exitConfirm = confirm;

            var prompt = new Label {
                Text = Tr("fighter_pause_exit_confirm"),
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(360, 0)
            };
            confirm.AddChild(prompt);

            var yes = MakeButton(Tr("common_confirm"));
            yes.Pressed += ExitToLobby;
            confirm.AddChild(yes);

            var no = MakeButton(Tr("common_cancel"));
            no.Pressed += HideExitConfirm;
            confirm.AddChild(no);
        }

        private void BuildDisconnectModal() {
            var modal = new ColorRect {
                Name = "DisconnectModal",
                Color = new Color(0.05f, 0.02f, 0.03f, 0.9f),
                MouseFilter = Control.MouseFilterEnum.Stop,
                Visible = false
            };
            modal.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(modal);
            _disconnectModal = modal;

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            modal.AddChild(center);

            var layout = new VBoxContainer();
            layout.AddThemeConstantOverride("separation", 16);
            center.AddChild(layout);

            _disconnectLabel = new Label {
                Name = "DisconnectLabel",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(720, 0)
            };
            _disconnectLabel.AddThemeFontSizeOverride("font_size", 26);
            _disconnectLabel.AddThemeColorOverride("font_color", new Color(1f, 0.75f, 0.3f));
            layout.AddChild(_disconnectLabel);

            var rebind = MakeButton(Tr("fighter_disconnect_rebind"));
            rebind.Pressed += TryRebindDisconnectedPlayer;
            layout.AddChild(rebind);

            var forfeit = MakeButton(Tr("fighter_disconnect_forfeit"));
            forfeit.Pressed += ExitToLobby;
            layout.AddChild(forfeit);
        }

        /// <summary>Binds the first free connected joypad to the affected slot.</summary>
        private void TryRebindDisconnectedPlayer() {
            if (_disconnectedPlayer < 0 || InputManager.Instance == null) return;
            if (InputManager.Instance.TryAssignFirstFreeDevice(_disconnectedPlayer)) {
                ResolveDisconnect(_disconnectedPlayer);
            }
        }

        private void ShowExitConfirm() {
            if (_exitConfirm != null) _exitConfirm.Visible = true;
        }

        private void HideExitConfirm() {
            if (_exitConfirm != null) _exitConfirm.Visible = false;
        }

        /// <summary>Design: exit returns local players to the Fighter character select lobby.</summary>
        private void ExitToLobby() {
            SetPaused(false);
            bool holodeck = GameManager.Instance?.CurrentSession.ReturnToHubAfterFighterMatch == true;
            GameManager.Instance?.LoadScene(holodeck
                ? "res://scenes/campaign/HubWorld.tscn"
                : "res://scenes/menus/CharacterSelect.tscn");
        }

        private void OpenSettings() {
            if (_settingsMenu == null || !IsInstanceValid(_settingsMenu)) {
                _settingsMenu = new SettingsMenu { Name = "SettingsMenu" };
                AddChild(_settingsMenu);
            }
            _settingsMenu.Show();
        }

        private static Button MakeButton(string text) => new() {
            Text = text,
            CustomMinimumSize = new Vector2(320, 44)
        };
    }
}
