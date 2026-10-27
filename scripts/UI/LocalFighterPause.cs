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

        // TG-PAUSE: the glass skin's parts (UI_SPEC_GLASS 9.2, the Fighter pause and the disconnect modal).
        private PausePanel _pausePanel;
        private PausePanel _disconnectPanel;
        private CanvasItem _shade;
        private Control _promptBar;
        private Label _statusLine;
        private readonly List<Button> _rows = new();
        private readonly List<Button> _disconnectRows = new();

        /// <summary>
        /// The Fighter pause's D6 cap at UI scale 1.0: its Move List rows carry a hero's full name ("Move List — William
        /// Shakespeare"), so it keeps the 760 px row the pulp build gave them; a shorter stack is narrower (the D6 width).
        /// </summary>
        public const float RowCapAtOne = 760f;

        /// <summary>The disconnect modal's slab width (UI_SPEC_GLASS 9.2: the confirm modal's 760).</summary>
        public const float DisconnectPanelWidth = ConfirmModal.ModalPanelWidth;

        /// <summary>The PAUSE-A rows in order. Test surface.</summary>
        internal IReadOnlyList<Button> Rows => _rows;

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

        /// <summary>
        /// The match's results screen, wired by <c>FighterSimulationDriver</c>.
        /// While it is showing, a window focus loss must not open the pause on
        /// top of it (Package 12 Phase C, the W5/W6 seam): the match is over and
        /// the results panel already owns focus.
        /// </summary>
        public MatchResults ResultsScreen { get; set; }

        /// <summary>True while the wired results screen is on screen.</summary>
        public bool ResultsShowing =>
            ResultsScreen != null && IsInstanceValid(ResultsScreen) && ResultsScreen.IsShowing;

        /// <summary>G11 focus-loss auto-pause, narrowed: never over the results screen.</summary>
        protected override bool CanAutoPause() => base.CanAutoPause() && !ResultsShowing;

        /// <summary>A forced disconnect pause cannot be dismissed with the pause button.</summary>
        protected override bool CanTogglePause() =>
            _disconnectedPlayer < 0 && _exitConfirm?.IsOpen != true
            && _moveList?.Visible != true;

        protected override void OnPauseStateChanged(bool paused) {
            if (_pauseBackdrop != null) _pauseBackdrop.Visible = paused && _disconnectedPlayer < 0;
            if (_panel != null) _panel.Visible = paused && _disconnectedPlayer < 0;

            if (!paused) {
                _exitConfirm?.Close();
                if (_moveList != null && IsInstanceValid(_moveList)) _moveList.Close();
                // The resume is immediate: a half-played open ends on its resting look.
                _pausePanel?.StopAppear(_shade);
                _disconnectPanel?.StopAppear();
                return;
            }

            if (_disconnectedPlayer >= 0) {
                // The blocking modal carries its own dim (the DisconnectModal scrim): the panel fades and rises over it.
                _disconnectPanel?.PlayAppear();
                // 9.1 item 4 / 9.2 (as the confirm modal: "the focused one carries the prompt"): its first row takes focus,
                // so a player whose controller is still connected can answer it. Until now nothing in the modal had focus.
                if (_disconnectPanel != null) FocusChainBuilder.GrabInitialFocus(FocusChainBuilder.Build(_disconnectPanel));
                return;
            }
            RefreshTagAndStatus();
            ShowMenuPanel();
            _focusChain = FocusChainBuilder.Build(_panel);
            FocusChainBuilder.GrabInitialFocus(_focusChain);
            // MOT-A row 14: the dim fades in to .70 while the panel fades and rises; both run while the tree is paused.
            _pausePanel?.PlayAppear(_shade);
        }

        /// <summary>
        /// UI_SPEC_GLASS 9.2 (agent choice): the tag names the stage being fought on and the status strip the match mode.
        /// A session with no stage (a hosted test, a drill with none recorded) keeps the Fighter Mode tag.
        /// </summary>
        private void RefreshTagAndStatus() {
            SessionData session = GameManager.Instance?.CurrentSession ?? default;
            string stageKey = "";
            if (!string.IsNullOrWhiteSpace(session.SelectedStageID)) {
                stageKey = FTT.Environment.FighterStageCatalog.LoadDefault()?.Find(session.SelectedStageID)?.DisplayNameKey ?? "";
            }
            if (_pausePanel != null) {
                _pausePanel.TagText = Tr(string.IsNullOrWhiteSpace(stageKey) ? "menu_fighter_mode" : stageKey);
            }
            if (_statusLine != null) _statusLine.Text = Tr(FighterHudModel.ModeLabelKey((int)session.MatchSettings.Mode));
        }

        /// <summary>
        /// One slab over one dim (UI_SPEC_GLASS 9.2): while Settings, a Move List or the exit confirmation is open over the
        /// pause it sits on the pause's dim, and the pause panel and its prompt bar stand down until it closes.
        /// </summary>
        private void HideMenuPanel() {
            if (_panel != null) _panel.Visible = false;
            if (_promptBar != null) _promptBar.Visible = false;
        }

        private void ShowMenuPanel() {
            if (_panel != null) _panel.Visible = IsPaused && _disconnectedPlayer < 0;
            if (_promptBar != null) _promptBar.Visible = true;
            PausePanel.ApplyRowWidths(_rows, RowCapAtOne);
        }

        private void RestoreMenuFocus() {
            ShowMenuPanel();
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
            // One dim (UI_SPEC_GLASS 9.2): the blocking modal is its own scrim, so the pause's dim and its prompt bar (whose
            // B would not resume here) stand down while it is up.
            if (_pauseBackdrop != null) _pauseBackdrop.Visible = false;
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

        /// <summary>
        /// 9.1 (2026-10-16, CM-INPUT): outside a local join (Versus CPU, the
        /// Holodeck, a drill) player 1 follows the last-used device, so a lost
        /// player-1 controller resolves the way the Story pause's does — a key
        /// press hands slot 0 the keyboard, a face button on a connected pad hands
        /// it that pad, and the assignment clears the modal. In a local join the
        /// per-slot modal stays as it was: Rebind or Forfeit.
        /// </summary>
        public override void _Input(InputEvent @event) {
            if (_disconnectedPlayer != 0 || !IsPaused) return;
            InputManager input = InputManager.Instance;
            if (input == null || input.JoinMode != LocalJoinMode.FollowLastUsed) return;
            if (@event is InputEventKey key && key.Pressed && !key.Echo) {
                input.AssignKeyboardToPlayer(0);
                GetViewport()?.SetInputAsHandled();
                return;
            }
            if (@event is not InputEventJoypadButton button || !button.Pressed) return;
            if (button.ButtonIndex is not (JoyButton.A or JoyButton.B or JoyButton.X or JoyButton.Y)) return;
            try {
                input.AssignJoypadToPlayer(button.Device, 0);
            } catch (System.ArgumentException) {
                return;
            }
            GetViewport()?.SetInputAsHandled();
        }

        private void OnDeviceAssigned(int playerIndex, int deviceID) {
            if (deviceID == InputManager.UnassignedDevice) return;
            ResolveDisconnect(playerIndex);
        }

        private void BuildPanel() {
            // TG-PAUSE (UI_SPEC_GLASS 9.2, 4.3): the PAUSE-A panel over the 0.70 dim: the glass slab with the stage-name tag,
            // the PAUSED title, the match-mode status strip, 64 px glass chevron rows at the D6 width and the bottom-centre
            // prompt bar. The pause backdrop owns the dim and the prompt bar so they show and hide with the pause.
            var backdrop = new Control { Name = "Backdrop", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(backdrop);
            AddChild(backdrop);
            PanelContainer shade = PulpScreenKit.MakeScrim();
            backdrop.AddChild(shade);
            _shade = shade;
            _pauseBackdrop = backdrop;
            _promptBar = PulpScreenKit.AddBottomPromptBar(backdrop, "menu_resume");

            _pausePanel = new PausePanel { Name = "PausePanel", Visible = false, TagText = Tr("menu_fighter_mode") };
            _panel = _pausePanel;
            UIPalette.ApplyTheme(_panel);
            var panelCenter = new CenterContainer { Name = "PanelCenter", MouseFilter = Control.MouseFilterEnum.Ignore };
            panelCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AddChild(panelCenter);
            panelCenter.AddChild(_panel);
            MenuSafeAreaFit.Attach(_panel, GlassScreenDressing.OverlayPromptClearance);
            PulpScreenKit.DressPanel(_pausePanel, sheen: true, buttonInset: true);

            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", 9);
            _panel.AddChild(layout);

            var title = new Label {
                Name = "Title",
                Text = "menu_paused",
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.LabelRoles.PauseTitle
            };
            layout.AddChild(title);
            PausePanel.TrimTitleBox(title);

            _statusLine = new Label {
                Name = "StatusLine",
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.LabelRoles.StatusLine,
                Uppercase = true
            };
            layout.AddChild(_statusLine);
            layout.AddChild(new Control {
                Name = "ListGap", CustomMinimumSize = new Vector2(0f, 6f), MouseFilter = Control.MouseFilterEnum.Ignore
            });

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

            foreach (Button row in new[] { resume, settings, moveListP1, moveListP2, exit }) _rows.Add(row);
            PausePanel.ApplyRowWidths(_rows, RowCapAtOne);
            RefreshTagAndStatus();

            _exitConfirm = ConfirmModal.Create("fighter_pause_exit_confirm");
            _exitConfirm.UsesHostDim = true;
            _exitConfirm.Confirmed += ExitToLobby;
            _exitConfirm.Cancelled += RestoreMenuFocus;
            AddChild(_exitConfirm);
        }

        private void BuildDisconnectModal() {
            // TG-PAUSE (UI_SPEC_GLASS 9.2: "the disconnect modal as the confirm modal"): the blocking "rebind or forfeit"
            // state is a 760 px glass slab tagged CONTROLLER over its own 0.70 dim, the message in the modal's body copy and
            // two stacked chevron rows sharing the D6 width. It cannot be backed out of; its rows say what to do.
            PanelContainer modal = PulpScreenKit.MakeScrim("DisconnectModal");
            modal.Visible = false;
            UIPalette.ApplyTheme(modal);
            AddChild(modal);
            _disconnectModal = modal;

            var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            modal.AddChild(center);

            _disconnectPanel = new PausePanel {
                Name = "DisconnectPanel", TagText = Tr("pause_tag_controller"),
                ThemeTypeVariation = UIPalette.Variations.InkPanel,
                CustomMinimumSize = new Vector2(DisconnectPanelWidth, 0f)
            };
            center.AddChild(_disconnectPanel);
            MenuSafeAreaFit.Attach(_disconnectPanel);
            PulpScreenKit.DressPanel(_disconnectPanel, sheen: true, buttonInset: true);

            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", ConfirmModal.LayoutSeparation);
            _disconnectPanel.AddChild(layout);

            _disconnectLabel = new Label {
                Name = "DisconnectLabel",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(ConfirmModal.MessageWidth, 0),
                ThemeTypeVariation = UIPalette.LabelRoles.BodyLabel
            };
            layout.AddChild(_disconnectLabel);

            var rebind = MakeButton("RebindButton", "fighter_disconnect_rebind");
            rebind.Pressed += TryRebindDisconnectedPlayer;
            rebind.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            layout.AddChild(rebind);

            var forfeit = MakeButton("ForfeitButton", "fighter_disconnect_forfeit");
            forfeit.Pressed += ExitToLobby;
            forfeit.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            layout.AddChild(forfeit);

            _disconnectRows.Add(rebind);
            _disconnectRows.Add(forfeit);
            PausePanel.ApplyRowWidths(_disconnectRows, RowCapAtOne);
        }

        /// <summary>Binds the first free connected joypad to the affected slot.</summary>
        private void TryRebindDisconnectedPlayer() {
            if (_disconnectedPlayer < 0 || InputManager.Instance == null) return;
            if (InputManager.Instance.TryAssignFirstFreeDevice(_disconnectedPlayer)) {
                ResolveDisconnect(_disconnectedPlayer);
            }
        }

        private void ShowExitConfirm() {
            if (_exitConfirm == null) return;
            _exitConfirm.Open();
            HideMenuPanel();
        }

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
            string exit = ApplyPauseExitToSession();
            if (exit != null) GameManager.Instance.LoadScene(exit);
        }

        /// <summary>
        /// M26 (Package 12 W5): pause Exit by match origin — the character select
        /// lobby for Local Versus, the Fighter menu for Versus CPU, and the hub in
        /// front of the Holodeck console for a Holodeck bout. Commits the session
        /// the destination reads and returns the scene; split out so a test can
        /// assert the route without a scene change.
        /// </summary>
        public static string ApplyPauseExitToSession() {
            GameManager manager = GameManager.Instance;
            if (manager == null) return null;
            SessionData session = manager.CurrentSession;
            string destination = FighterFlowRoutes.PauseExit(ref session);
            manager.CurrentSession = session;
            return destination;
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
                _moveList = new MoveListScreen { Name = "MoveListScreen", UsesHostDim = true };
                AddChild(_moveList);
                _moveList.Closed += RestoreMenuFocus;
            }
            _moveList.Open(characterID);
            HideMenuPanel();
        }

        private void OpenSettings() {
            if (_settingsMenu == null || !IsInstanceValid(_settingsMenu)) {
                _settingsMenu = new SettingsMenu { Name = "SettingsMenu", UsesHostDim = true };
                AddChild(_settingsMenu);
                // H-10: Settings holds focus while open; hand it back to this
                // menu's chain on close (mirrors MainMenu's restore).
                _settingsMenu.Closed += RestoreMenuFocus;
            }
            _settingsMenu.Show();
            HideMenuPanel();
        }

        /// <summary>A 64 px glass chevron row; the stack's D6 width replaces the 560 floor once the rows are built.</summary>
        private static Button MakeButton(string name, string textKey) =>
            PulpScreenKit.StylePauseButton(new Button { Name = name, Text = textKey });
    }
}
