using System.Collections.Generic;
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
    /// Package 8 A1 moved the pause bookkeeping onto <see cref="PauseMenuBase"/>
    /// and the exit confirmation onto the shared <see cref="ConfirmModal"/>. The
    /// disconnect modal stays bespoke on purpose: it is not a confirm/cancel
    /// question but a blocking "rebind or forfeit" state that the player cannot
    /// simply back out of.
    ///
    /// Pause discipline lives in the base class: whoever sets
    /// <see cref="SceneTree.Paused"/> hands it back, including on teardown.
    /// </summary>
    public partial class LocalFighterPause : PauseMenuBase {
        private Control _panel;
        private Control _pauseBackdrop;
        private Control _disconnectModal;
        private Label _disconnectLabel;
        private ConfirmModal _exitConfirm;
        private SettingsMenu _settingsMenu;
        private MoveListScreen _moveList;
        private List<Control> _focusChain = new();
        private int _disconnectedPlayer = -1;

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
            // Releases a held pause. Must run.
            base._ExitTree();
        }

        /// <summary>A forced disconnect pause cannot be dismissed with the pause button.</summary>
        protected override bool CanTogglePause() =>
            _disconnectedPlayer < 0 && _exitConfirm?.IsOpen != true
            && _moveList?.Visible != true;

        protected override void OnPauseStateChanged(bool paused) {
            if (_pauseBackdrop != null) _pauseBackdrop.Visible = paused;
            if (_panel != null) _panel.Visible = paused && _disconnectedPlayer < 0;

            if (!paused) {
                _exitConfirm?.Close();
                if (_moveList != null && IsInstanceValid(_moveList)) _moveList.Close();
                return;
            }

            if (_disconnectedPlayer >= 0) return;
            _focusChain = FocusChainBuilder.Build(_panel);
            FocusChainBuilder.GrabInitialFocus(_focusChain);
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
            var shade = new ColorRect {
                Name = "Shade",
                Color = UIPalette.Shade,
                MouseFilter = Control.MouseFilterEnum.Stop,
                Visible = false
            };
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(shade);
            AddChild(shade);
            _pauseBackdrop = shade;

            const string artPath =
                "res://assets/backgrounds/menu/temporal_glass_submenu_background.png";
            if (ResourceLoader.Exists(artPath)) {
                var art = new TextureRect {
                    Name = "TemporalGlassArt",
                    Texture = ResourceLoader.Load<Texture2D>(artPath),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Modulate = new Color(1f, 1f, 1f, 0.24f)
                };
                art.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                shade.AddChild(art);
            }

            _panel = new PanelContainer { Name = "PausePanel", Visible = false };
            _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
            _panel.ThemeTypeVariation = "DialogueGlassPanel";
            UIPalette.ApplyTheme(_panel);
            AddChild(_panel);

            var layout = new VBoxContainer();
            layout.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            _panel.AddChild(layout);

            var title = new Label {
                Text = "menu_paused",
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.TitleLabelVariation
            };
            title.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            layout.AddChild(title);

            var resume = MakeButton("ResumeButton", "menu_resume");
            resume.Pressed += () => SetPaused(false);
            layout.AddChild(resume);

            var settings = MakeButton("SettingsButton", "menu_settings");
            settings.Pressed += OpenSettings;
            layout.AddChild(settings);

            // V7.3 Fighter Onboarding: the Fighter pause offers the Move List
            // for either active fighter. Raw format key + name key so a locale
            // change re-resolves; names read from the session at build time.
            SessionData session = GameManager.Instance?.CurrentSession ?? default;
            var moveListP1 = MakeButton("MoveListP1Button", "movelist_title");
            moveListP1.Text = FormatMoveListLabel(session.SelectedCharacterID);
            moveListP1.Pressed += () => OpenMoveList(session.SelectedCharacterID);
            layout.AddChild(moveListP1);

            var moveListP2 = MakeButton("MoveListP2Button", "movelist_title");
            moveListP2.Text = FormatMoveListLabel(session.OpponentCharacterID);
            moveListP2.Pressed += () => OpenMoveList(session.OpponentCharacterID);
            layout.AddChild(moveListP2);

            var exit = MakeButton("ExitButton", "fighter_pause_exit");
            exit.Pressed += ShowExitConfirm;
            layout.AddChild(exit);

            _exitConfirm = ConfirmModal.Create("fighter_pause_exit_confirm");
            _exitConfirm.Confirmed += ExitToLobby;
            _exitConfirm.Cancelled += () => FocusChainBuilder.GrabInitialFocus(_focusChain);
            AddChild(_exitConfirm);
        }

        private void BuildDisconnectModal() {
            var modal = new ColorRect {
                Name = "DisconnectModal",
                Color = new Color(0.05f, 0.02f, 0.03f, 0.9f),
                MouseFilter = Control.MouseFilterEnum.Stop,
                Visible = false
            };
            modal.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(modal);
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
                CustomMinimumSize = new Vector2(720, 0),
                ThemeTypeVariation = UIPalette.EmphasisLabelVariation
            };
            _disconnectLabel.AddThemeColorOverride("font_color", UIPalette.Warning);
            layout.AddChild(_disconnectLabel);

            var rebind = MakeButton("RebindButton", "fighter_disconnect_rebind");
            rebind.Pressed += TryRebindDisconnectedPlayer;
            layout.AddChild(rebind);

            var forfeit = MakeButton("ForfeitButton", "fighter_disconnect_forfeit");
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

        private void ShowExitConfirm() => _exitConfirm?.Open();

        /// <summary>Design: exit returns local players to the Fighter character select lobby.</summary>
        private void ExitToLobby() {
            SetPaused(false);
            // Package 11 A11 (F18): a Calibration Drill borrows this pause menu, and
            // its exit is "Exit Calibration" — the Main Menu on the standalone
            // route, the originating hub on the console route. Never the Fighter
            // lobby, which a drill player never passed through to get here.
            string calibration = GameManager.Instance?.CurrentSession.CalibrationReturnScenePath;
            if (!string.IsNullOrWhiteSpace(calibration)) {
                string destination = FTT.Combat.CalibrationRoute.ExitDestination(calibration);
                GameManager.Instance.CurrentSession =
                    FTT.Combat.CalibrationRoute.Cleared(GameManager.Instance.CurrentSession);
                GameManager.Instance.LoadScene(destination);
                return;
            }
            bool holodeck = GameManager.Instance?.CurrentSession.ReturnToHubAfterFighterMatch == true;
            GameManager.Instance?.LoadScene(holodeck
                ? "res://scenes/campaign/HubWorld.tscn"
                : "res://scenes/menus/CharacterSelect.tscn");
        }

        /// <summary>"Move List — {name}", degrading to the plain label when the
        /// slot has no resolvable character (menu-hosted tests, empty session).</summary>
        private string FormatMoveListLabel(string characterID) {
            string path = $"res://resources/Characters/{characterID}_data.tres";
            if (string.IsNullOrWhiteSpace(characterID) || !ResourceLoader.Exists(path)) {
                return Tr("movelist_title");
            }
            var data = FTT.Core.AuthoredResources.Load<FTT.Characters.CharacterData>(path);
            return data == null || string.IsNullOrWhiteSpace(data.DisplayNameKey)
                ? Tr("movelist_title")
                : string.Format(Tr("movelist_title_for"), Tr(data.DisplayNameKey));
        }

        /// <summary>
        /// V7.3 Fighter Onboarding: the Move List opens as an overlay inside
        /// this pause CanvasLayer — pause ownership stays here, and closing the
        /// list hands focus back to the pause chain.
        /// </summary>
        private void OpenMoveList(string characterID) {
            if (_moveList == null || !IsInstanceValid(_moveList)) {
                _moveList = new MoveListScreen { Name = "MoveListScreen" };
                AddChild(_moveList);
                _moveList.Closed += () => FocusChainBuilder.GrabInitialFocus(_focusChain);
            }
            _moveList.Open(characterID);
        }

        private void OpenSettings() {
            if (_settingsMenu == null || !IsInstanceValid(_settingsMenu)) {
                _settingsMenu = new SettingsMenu { Name = "SettingsMenu" };
                AddChild(_settingsMenu);
                // H-10: Settings holds focus while open; hand it back to this
                // menu's chain on close (mirrors MainMenu's restore).
                _settingsMenu.Closed += () => FocusChainBuilder.GrabInitialFocus(_focusChain);
            }
            _settingsMenu.Show();
        }

        private static Button MakeButton(string name, string textKey) => new() {
            Name = name,
            Text = textKey,
            CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, UIPalette.ButtonMinHeight),
            ThemeTypeVariation = "TemporalGlassButton"
        };
    }
}
