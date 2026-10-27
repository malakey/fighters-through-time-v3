using System.Collections.Generic;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// Story Mode pause menu: Resume / Settings / Save / Restart Level / Exit,
    /// per design-godot.md "Story Mode Pause Menu" (audit M-2). Save writes a
    /// checkpoint-style save without leaving the level; Restart Level restarts
    /// the whole level from its beginning behind a confirmation (V7.2: a fresh
    /// attempt, every checkpoint cleared, this level's dust forfeited); Exit
    /// quits to the main menu behind a confirmation and charges the voluntary
    /// 20% exit fee on undeposited Chronal Dust — the player keeps 80%, rounded
    /// down (<see cref="FTT.Core.SessionExitGuard.CalculateExitRetainedDust"/>;
    /// docs/DUST_ECONOMY.md). The 50% retention this menu shipped with on
    /// 2026-08-10 is superseded.
    ///
    /// Package 8 A1 moved this onto the authored, themed
    /// <c>res://scenes/ui/StoryPause.tscn</c> and onto <see cref="PauseMenuBase"/>.
    /// The code-built fallback below is kept deliberately: campaign scenes attach
    /// this through <see cref="FTT.Environment.StorySceneBootstrapper"/>, and a
    /// missing or broken scene resource must degrade to a working menu rather than
    /// leave a paused campaign with no way out.
    /// </summary>
    public partial class PauseMenu : PauseMenuBase {

        public const string SceneResourcePath = "res://scenes/ui/StoryPause.tscn";

        private Control _root;
        private Control _menuPanel;
        private Button _saveButton;
        private ConfirmModal _quitConfirm;
        private ConfirmModal _restartConfirm;
        private SettingsMenu _settingsMenu;
        private MoveListScreen _moveList;
        private List<Control> _focusChain = new();

        // Package 12 W6
        private DialogueLogScreen _logScreen;
        private Button _lowerDifficultyButton;
        private ConfirmModal _lowerDifficultyConfirm;
        private Label _controllerNotice;
        private Label _statusLine;
        private PausePanel _pausePanel;
        private bool _awaitingController;

        // TG-PAUSE: the dim, the prompt bar and the chevron rows the glass skin works on (both build paths).
        private CanvasItem _shade;
        private Control _promptBar;
        private readonly List<Button> _rows = new();

        /// <summary>The PAUSE-A rows in order, decorated on both build paths. Test surface.</summary>
        internal IReadOnlyList<Button> Rows => _rows;

        /// <summary>Test seam: forces the hub check instead of reading the current scene.</summary>
        internal bool? AtHubOverrideForTesting;

        /// <summary>True while the controller-loss notice is up (G11). Test surface.</summary>
        public bool IsAwaitingController => _awaitingController;

        /// <summary>The Dialogue Log overlay, once opened. Test surface.</summary>
        internal DialogueLogScreen LogScreen => _logScreen;

        /// <summary>The G14 confirmation, once created. Test surface.</summary>
        internal ConfirmModal LowerDifficultyConfirmation => _lowerDifficultyConfirm;

        /// <summary>
        /// Instantiates the authored scene, falling back to a code-built menu when
        /// it is unavailable. Mirrors <see cref="StoryHUD.CreateDefault"/>.
        /// </summary>
        public static PauseMenu CreateDefault() {
            if (ResourceLoader.Exists(SceneResourcePath)) {
                var packed = ResourceLoader.Load<PackedScene>(SceneResourcePath);
                if (packed?.Instantiate() is PauseMenu authored) {
                    // Both paths produce the same node name so campaign scene trees
                    // are identical whether or not the authored scene resolved.
                    authored.Name = "PauseMenu";
                    return authored;
                }
            }
            return new PauseMenu { Name = "PauseMenu" };
        }

        public override void _Ready() {
            Layer = 99;
            ProcessMode = ProcessModeEnum.Always;
            ResolveOrBuildUI();
            BuildConfirmations();
            if (_root != null) _root.Visible = false;
            // G11: controller loss pauses Story and the hub the same way it pauses
            // a local Fighter match. Keyboard players never raise this event.
            if (FTT.Core.InputManager.Instance != null) {
                FTT.Core.InputManager.Instance.PlayerDeviceDisconnected += OnPlayerDeviceDisconnected;
            }
        }

        public override void _ExitTree() {
            if (FTT.Core.InputManager.Instance != null) {
                FTT.Core.InputManager.Instance.PlayerDeviceDisconnected -= OnPlayerDeviceDisconnected;
            }
            // Releases a held pause (failure signature 4). Must run.
            base._ExitTree();
        }

        // === Package 12 W6 (G11): controller loss in Story and the hub ======

        private void OnPlayerDeviceDisconnected(int playerIndex) {
            // Story is single-player: only the campaign player's device matters.
            if (playerIndex == 0) ForceControllerLostPause();
        }

        /// <summary>
        /// Opens the ordinary pause menu with the reconnect notice. Public so the
        /// path is exercisable without a real controller. Any face button on a
        /// connected controller rebinds player 1 and resumes (design Section 11);
        /// Resume works as always for a player who switched to the keyboard.
        /// </summary>
        public void ForceControllerLostPause() {
            _awaitingController = true;
            if (_controllerNotice != null) _controllerNotice.Visible = true;
            if (IsPaused) {
                OnPauseStateChanged(true);
                return;
            }
            SetPaused(true);
        }

        /// <summary>
        /// A face button from a connected joypad while the notice is up: that
        /// controller becomes player 1's and the game resumes. Since 9.1
        /// (2026-10-16) a keyboard press does the same for the keyboard
        /// (<see cref="FTT.Core.InputManager.KeyboardDevice"/>): player 1 follows
        /// the last-used device, so the keyboard is as good a way back as a pad.
        /// Returns true when it resolved. Public so tests can drive it with a
        /// synthetic device.
        /// </summary>
        public bool ResolveControllerLost(int deviceId) {
            if (!_awaitingController) return false;
            var input = FTT.Core.InputManager.Instance;
            if (input != null && deviceId == FTT.Core.InputManager.KeyboardDevice) {
                input.AssignKeyboardToPlayer(0);
            } else if (input != null && deviceId >= 0) {
                try {
                    input.AssignJoypadToPlayer(deviceId, 0);
                } catch (System.ArgumentException) {
                    return false;
                }
            }
            _awaitingController = false;
            if (_controllerNotice != null) _controllerNotice.Visible = false;
            SetPaused(false);
            return true;
        }

        public override void _Input(InputEvent @event) {
            if (!_awaitingController || !IsPaused) return;
            if (@event is InputEventKey key && key.Pressed && !key.Echo) {
                if (ResolveControllerLost(FTT.Core.InputManager.KeyboardDevice)) GetViewport()?.SetInputAsHandled();
                return;
            }
            if (@event is not InputEventJoypadButton button || !button.Pressed) return;
            bool faceButton = button.ButtonIndex is JoyButton.A or JoyButton.B or JoyButton.X or JoyButton.Y;
            if (!faceButton) return;
            if (ResolveControllerLost(button.Device)) GetViewport()?.SetInputAsHandled();
        }

        // === Package 12 W6 (G14): lower difficulty from the hub =============

        private bool IsAtHub() =>
            AtHubOverrideForTesting ?? GetTree()?.CurrentScene is FTT.Environment.HubWorldController;

        /// <summary>True when the Lower Difficulty entry should be offered right now.</summary>
        public bool CanOfferLowerDifficulty() =>
            FTT.Core.CampaignDifficultyService.EvaluateActive(IsAtHub())
            == FTT.Core.CampaignDifficultyRules.Verdict.Allowed;

        private void RefreshLowerDifficultyEntry() {
            if (_lowerDifficultyButton == null) return;
            _lowerDifficultyButton.Visible = CanOfferLowerDifficulty();
        }

        private void ShowLowerDifficultyConfirmation() {
            if (!CanOfferLowerDifficulty()) return;
            var session = FTT.Core.GameManager.Instance?.CurrentSession;
            FTT.Core.Difficulty current = session?.Difficulty ?? FTT.Core.Difficulty.Normal;
            FTT.Core.Difficulty next = FTT.Core.CampaignDifficultyRules.OneStepLower(current);
            if (_lowerDifficultyConfirm == null || !IsInstanceValid(_lowerDifficultyConfirm)) {
                _lowerDifficultyConfirm = ConfirmModal.Create(
                    "pause_lower_difficulty_confirm", "common_confirm", "common_cancel", "pause_lower_difficulty");
                _lowerDifficultyConfirm.Name = "LowerDifficultyConfirmModal";
                _lowerDifficultyConfirm.UsesHostDim = true;
                _lowerDifficultyConfirm.Confirmed += ConfirmLowerDifficulty;
                _lowerDifficultyConfirm.Cancelled += RestoreMenuFocus;
                _root?.AddChild(_lowerDifficultyConfirm);
            }
            // Already-resolved copy (the ConfirmModal idiom for formatted prompts).
            _lowerDifficultyConfirm.SetPromptKey(string.Format(
                Tr("pause_lower_difficulty_confirm"),
                Tr(FTT.Core.CampaignDifficultyService.DifficultyNameKey(current)),
                Tr(FTT.Core.CampaignDifficultyService.DifficultyNameKey(next))));
            _lowerDifficultyConfirm.Open();
            HideMenuPanel();
        }

        private void ConfirmLowerDifficulty() {
            FTT.Core.CampaignDifficultyService.TryLowerActiveCampaign(IsAtHub());
            RefreshLowerDifficultyEntry();
            ShowMenuPanel();
            _focusChain = FocusChainBuilder.Build(_menuPanel);
            FocusChainBuilder.GrabInitialFocus(_focusChain);
        }

        // === Package 12 W6 (G10): the Dialogue Log from the pause menu =======

        private void OpenDialogueLog() {
            if (_logScreen == null || !IsInstanceValid(_logScreen)) {
                _logScreen = new DialogueLogScreen { Name = "DialogueLogScreen", UsesHostDim = true };
                AddChild(_logScreen);
                _logScreen.Closed += RestoreMenuFocus;
            }
            _logScreen.Open();
            HideMenuPanel();
        }

        private void ResolveOrBuildUI() {
            _root = GetNodeOrNull<Control>("Root");
            if (_root != null) {
                _menuPanel = _root.GetNodeOrNull<Control>("Center/Panel");
                WireButton("Root/Center/Panel/Layout/ResumeButton", () => SetPaused(false));
                WireButton("Root/Center/Panel/Layout/SettingsButton", OpenSettings);
                WireButton("Root/Center/Panel/Layout/MoveListButton", OpenMoveList);
                _saveButton = GetNodeOrNull<Button>("Root/Center/Panel/Layout/SaveButton");
                if (_saveButton != null) _saveButton.Pressed += SaveProgress;
                WireButton("Root/Center/Panel/Layout/RestartButton", ShowRestartConfirmation);
                WireButton("Root/Center/Panel/Layout/QuitButton", ShowQuitConfirmation);
                WireButton("Root/Center/Panel/Layout/LogButton", OpenDialogueLog);
                _lowerDifficultyButton = GetNodeOrNull<Button>("Root/Center/Panel/Layout/LowerDifficultyButton");
                if (_lowerDifficultyButton != null) _lowerDifficultyButton.Pressed += ShowLowerDifficultyConfirmation;
                _controllerNotice = GetNodeOrNull<Label>("Root/Center/Panel/Layout/ControllerNotice");
                _statusLine = GetNodeOrNull<Label>("Root/Center/Panel/Layout/StatusLine");
                _pausePanel = _menuPanel as PausePanel;
            } else {
                BuildFallbackUI();
            }
            DecorateForGlass();
        }

        /// <summary>The PAUSE-A rows in their authored order (both build paths name them the same).</summary>
        private static readonly string[] RowNames = {
            "ResumeButton", "SettingsButton", "MoveListButton", "LogButton", "SaveButton", "RestartButton",
            "LowerDifficultyButton", "QuitButton"
        };

        /// <summary>
        /// TG-PAUSE (UI_SPEC_GLASS 4.3): what both build paths share. Every row is a 64 px glass chevron (the diamond
        /// marker, the 14 px focus slide, the prompt badge), the stack shares the D6 width (the widest label decides: about
        /// 610 on a keyboard at 1.0, the panel then 20 + 610 + 20 + the 14 px stack inset), the panel keeps the 14 px inset
        /// for the focused row's slide and glow, the title's box is trimmed to the spec's, <see cref="MenuSafeAreaFit"/>
        /// keeps the slab on screen and clear of the prompt bar at UI scale 1.4, and the bottom-centre prompt bar is added.
        /// The authored scene and the fallback differ only in who made the nodes.
        /// </summary>
        private void DecorateForGlass() {
            if (_root == null) return;
            _rows.Clear();
            foreach (string name in RowNames) {
                if (_menuPanel?.GetNodeOrNull<Button>("Layout/" + name) is not { } button) continue;
                PulpScreenKit.StylePauseButton(button);
                _rows.Add(button);
            }
            PausePanel.ApplyRowWidths(_rows);
            PausePanel.TrimTitleBox(_menuPanel?.GetNodeOrNull<Label>("Layout/Title"));
            if (_menuPanel is PanelContainer panel) {
                PulpScreenKit.DressPanel(panel, sheen: true, buttonInset: true);
                MenuSafeAreaFit.AttachClearOfPromptBar(panel, _root);
            }
            if (_root.GetNodeOrNull(PulpScreenKit.PromptBarName) == null) PulpScreenKit.AddBottomPromptBar(_root, "menu_resume");
            _promptBar = _root.GetNodeOrNull<Control>(PulpScreenKit.PromptBarName);
            _shade = _root.GetNodeOrNull<CanvasItem>("Shade");
        }

        /// <summary>
        /// One slab over one dim (UI_SPEC_GLASS 9.2, the pause's child screens): while Settings, the Move List, the Dialogue
        /// Log or a confirmation is open over the pause, it sits on the pause's own dim and the pause panel and its prompt
        /// bar stand down; they come back, and focus with them, when the child closes.
        /// </summary>
        private void HideMenuPanel() {
            if (_menuPanel != null) _menuPanel.Visible = false;
            if (_promptBar != null) _promptBar.Visible = false;
        }

        private void ShowMenuPanel() {
            if (_menuPanel != null) _menuPanel.Visible = true;
            if (_promptBar != null) _promptBar.Visible = true;
            PausePanel.ApplyRowWidths(_rows);
        }

        /// <summary>The level-name tag text: the current level's title up to its first dash, or the ship at the hub.</summary>
        private string LevelTagText() {
            var story = FTT.Core.StoryManager.Instance;
            string key = IsAtHub()
                ? LoadingScreen.HubTitleKey
                : FTT.Environment.HubWorldController.CampaignLevelNameKey(story?.CurrentLevel ?? FTT.Core.CampaignLevel.Tutorial);
            return PulpScreenKit.ShortTitle(Tr(key));
        }

        /// <summary>The strip under the title: difficulty, the level clock and Timeline Integrity (the hub shows the difficulty alone).</summary>
        private string StatusText() {
            FTT.Core.Difficulty difficulty =
                FTT.Core.GameManager.Instance?.CurrentSession.Difficulty ?? FTT.Core.Difficulty.Normal;
            string name = Tr(FTT.Core.CampaignDifficultyService.DifficultyNameKey(difficulty));
            var story = FTT.Core.StoryManager.Instance;
            if (IsAtHub() || story == null) return name;
            return string.Format(Tr("pause_status_level"), name,
                PulpScreenKit.FormatClock(story.LevelElapsedSeconds), Mathf.RoundToInt(story.TimelineIntegrityPercent));
        }

        private void RefreshStatus() {
            if (_pausePanel != null) _pausePanel.TagText = LevelTagText();
            if (_statusLine != null) _statusLine.Text = StatusText();
        }

        private void WireButton(string path, System.Action handler) {
            var button = GetNodeOrNull<Button>(path);
            if (button != null) button.Pressed += handler;
        }

        private void BuildFallbackUI() {
            _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Stop };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(_root);
            AddChild(_root);

            _root.AddChild(PulpScreenKit.MakeScrim());

            var center = new CenterContainer { Name = "Center" };
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(center);

            var panel = new PausePanel { Name = "Panel" };
            center.AddChild(panel);
            _menuPanel = panel;
            _pausePanel = panel;

            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", 9);
            panel.AddChild(layout);

            var title = new Label {
                Name = "Title",
                Text = "menu_paused",
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.LabelRoles.PauseTitle
            };
            layout.AddChild(title);

            _statusLine = new Label {
                Name = "StatusLine",
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.LabelRoles.StatusLine,
                Uppercase = true
            };
            layout.AddChild(_statusLine);

            // 4.3: the list starts 24 px under the status strip (9 + this 6 + 9, the rows' own pitch being 64 + 9).
            layout.AddChild(new Control {
                Name = "ListGap", CustomMinimumSize = new Vector2(0f, 6f), MouseFilter = Control.MouseFilterEnum.Ignore
            });

            _controllerNotice = new Label {
                Name = "ControllerNotice",
                Text = "pause_controller_disconnected",
                Visible = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ThemeTypeVariation = UIPalette.SmallLabelVariation
            };
            _controllerNotice.AddThemeColorOverride("font_color", UIPalette.Warning);
            layout.AddChild(_controllerNotice);

            layout.AddChild(MakeButton("ResumeButton", "menu_resume", () => SetPaused(false)));
            layout.AddChild(MakeButton("SettingsButton", "menu_settings", OpenSettings));
            layout.AddChild(MakeButton("MoveListButton", "movelist_title", OpenMoveList));
            layout.AddChild(MakeButton("LogButton", "dialogue_log_title", OpenDialogueLog));
            _saveButton = MakeButton("SaveButton", "menu_save", SaveProgress);
            layout.AddChild(_saveButton);
            layout.AddChild(MakeButton("RestartButton", "menu_restart", ShowRestartConfirmation));
            _lowerDifficultyButton = MakeButton("LowerDifficultyButton", "pause_lower_difficulty", ShowLowerDifficultyConfirmation);
            _lowerDifficultyButton.Visible = false;
            layout.AddChild(_lowerDifficultyButton);
            layout.AddChild(MakeButton("QuitButton", "pause_quit_to_menu", ShowQuitConfirmation));
        }

        private static Button MakeButton(string name, string textKey, System.Action handler) {
            var button = new Button {
                Name = name,
                Text = textKey,
                CustomMinimumSize = new Vector2(PulpScreenKit.PauseButtonWidth, PulpScreenKit.PauseButtonHeight),
                ThemeTypeVariation = UIPalette.Variations.TemporalGlassButton
            };
            button.Pressed += handler;
            return button;
        }

        private void BuildConfirmations() {
            if (_root == null) return;
            _quitConfirm = ConfirmModal.Create("pause_quit_confirm");
            _quitConfirm.UsesHostDim = true;
            _quitConfirm.Confirmed += QuitToMainMenu;
            _quitConfirm.Cancelled += RestoreMenuFocus;
            _root.AddChild(_quitConfirm);

            _restartConfirm = ConfirmModal.Create("pause_restart_confirm");
            _restartConfirm.Name = "RestartConfirmModal";
            _restartConfirm.UsesHostDim = true;
            _restartConfirm.Confirmed += RestartLevelFromBeginning;
            _restartConfirm.Cancelled += RestoreMenuFocus;
            _root.AddChild(_restartConfirm);
        }

        // === Save ==========================================================

        /// <summary>
        /// Designed Save option: a background checkpoint-style save of the
        /// current run state through the existing SaveManager checkpoint path,
        /// re-stamping the checkpoint the player last reached. The menu stays
        /// open; the button flips to a "saved" label as feedback.
        /// </summary>
        private void SaveProgress() {
            // F03: an explicit Save during a live Time Freeze ends the freeze and
            // stores the full cooldown (a background autosave does not).
            FTT.Core.StoryManager.Instance?.EndActiveTimeFreezeForExplicitSave();
            var saveManager = FTT.Core.SaveManager.Instance;
            var gameManager = FTT.Core.GameManager.Instance;
            if (saveManager == null || gameManager == null) return;

            int slot = gameManager.CurrentSession.ActiveSaveSlot;
            string checkpointID = slot >= 0 && slot < saveManager.SaveSlots.Length
                ? saveManager.SaveSlots[slot]?.LastCheckpointID ?? ""
                : "";
            saveManager.SaveCheckpoint(checkpointID);
            if (_saveButton != null) _saveButton.Text = "pause_save_done";
        }

        // === Restart Level =================================================

        private void ShowRestartConfirmation() {
            if (_restartConfirm == null) {
                RestartLevelFromBeginning();
                return;
            }
            _restartConfirm.Open();
            HideMenuPanel();
        }

        /// <summary>
        /// The post-restart wallet (V7.2 rule): ALL dust earned this level is
        /// cleared — the attempt never happened — while residue that predates
        /// the level survives. (The V7 free checkpoint-restart let players farm
        /// full-strength boss attempts; checkpoint-resume now belongs
        /// exclusively to the priced rewind/Collapse path.)
        /// </summary>
        public static int CalculateRestartWallet(int walletDust, int earnedThisLevel) {
            int wallet = Mathf.Max(0, walletDust);
            return wallet - Mathf.Clamp(earnedThisLevel, 0, wallet);
        }

        /// <summary>
        /// V7.2 Restart Level — a true restart, not a free heal: the whole
        /// level restarts from its beginning (checkpoints cleared, every
        /// enemy/Extractor/pickup/Font/puzzle back to initial state via the
        /// scene reload), the player starts fresh at 100% HP and zero Ultimate
        /// meter with a full rewind pool, and every point of dust earned this
        /// level is cleared.
        /// </summary>
        private void RestartLevelFromBeginning() {
            var story = FTT.Core.StoryManager.Instance;
            var saveManager = FTT.Core.SaveManager.Instance;
            var gameManager = FTT.Core.GameManager.Instance;
            // CM-FIXB-PADUI: the restart leaves the scene, so the tree is released
            // even under a gameplay-pausing dialogue (the scene load needs it).
            ClosePauseForSceneExit();
            if (story == null || saveManager == null || gameManager == null) return;

            FTT.Core.Difficulty difficulty = gameManager.CurrentSession.Difficulty;
            int maxRewinds = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
            story.SetRewinds(maxRewinds);

            // Package 12 W2 (H02): the whole wallet belongs to the open attempt.
            // With no hub deposit, dust a Collapse or exit left behind is still
            // this level's undeposited dust even though this scene load never
            // tallied it, so a full Restart Level clears ALL of it (F02/F10) —
            // not just what DustEarnedThisLevel saw since the last load.
            int wallet = story.ChronalDustCollected;
            story.SetDust(CalculateRestartWallet(wallet, earnedThisLevel: wallet));
            // A true restart clears the whole per-attempt registry family:
            // Restoration Fonts refill, checkpoints de-stabilize (Mending
            // pays again), destroyed extractors and found secrets reset with
            // the scene reload (V7.3).
            story.ClearLevelAttemptState();

            int slot = gameManager.CurrentSession.ActiveSaveSlot;
            if (slot >= 0 && slot < saveManager.SaveSlots.Length && saveManager.SaveSlots[slot] != null) {
                FTT.Core.StorySaveData save = saveManager.SaveSlots[slot];
                save.CurrentLives = maxRewinds;
                save.LevelChronalDust = story.ChronalDustCollected;
                // A true restart: no checkpoint survives the attempt.
                save.LastCheckpointID = "";
                story.WriteAttemptStateToSave(save);
                if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is FTT.Characters.PlayerController player) {
                    save.CurrentHP = player.MaximumHP;
                }
                // F10: a full Restart Level is fresh-entry initialization, so the
                // Ultimate meter starts at zero too (the mint repeats it).
                save.CurrentUltimateMeter = 0f;
                saveManager.SaveStorySlot(slot);
            }

            story.LoadCurrentLevel();
        }

        // === Exit to main menu =============================================

        /// <summary>
        /// Unified exit rule (V7 "one rule, one number", enforced V7.2): any
        /// exit from an incomplete level — Timeline Collapse, quit-to-hub, or
        /// quit-to-menu — forfeits 20% of undeposited Chronal Dust; the player
        /// retains 80%, rounded down. V7.3 moved the math to the shared
        /// <see cref="FTT.Core.SessionExitGuard"/> (the abnormal-exit boot
        /// check lives in Core and must not reference UI); these wrappers keep
        /// the existing call sites and pins working.
        /// </summary>
        public static int CalculateExitRetainedDust(int unbankedDust) =>
            FTT.Core.SessionExitGuard.CalculateExitRetainedDust(unbankedDust);

        /// <summary>
        /// The wallet after the exit penalty: only dust earned in the current
        /// level is penalized, so any residue that predates the level (already
        /// counted in the wallet but not earned here) survives intact.
        /// </summary>
        public static int CalculateExitWalletAfterPenalty(int walletDust, int unbankedLevelDust) =>
            FTT.Core.SessionExitGuard.CalculateExitWalletAfterPenalty(walletDust, unbankedLevelDust);

        private void ShowQuitConfirmation() {
            if (_quitConfirm == null) {
                QuitToMainMenu();
                return;
            }
            _quitConfirm.Open();
            HideMenuPanel();
        }

        private void QuitToMainMenu() {
            ApplyExitDustPenalty();
            // CM-FIXB-PADUI: leaving the scene releases the tree outright.
            ClosePauseForSceneExit();
            var story = FTT.Core.StoryManager.Instance;
            if (story != null) story.ExitToMainMenu();
            else FTT.Core.GameManager.Instance?.LoadScene(FTT.Core.StoryManager.MainMenuScenePath);
        }

        /// <summary>
        /// Charges the exit fee and commits the orderly exit before the scene
        /// changes, through <see cref="FTT.Core.StoryManager.CommitOrderlyExit"/>:
        /// the post-penalty wallet replaces the in-memory wallet as well as the
        /// saved one, the live hero's latest HP, meter and resource timers are
        /// flushed (F10: "An orderly Save/exit flushes the latest state"), and
        /// the level run stops, so the per-second durable snapshot can no longer
        /// write the unpenalized wallet back over the fee under the loading
        /// screen or in the main menu.
        /// </summary>
        private void ApplyExitDustPenalty() {
            // F03: exiting during a live Time Freeze ends it and stores 45 s.
            FTT.Core.StoryManager.Instance?.EndActiveTimeFreezeForExplicitSave();
            var story = FTT.Core.StoryManager.Instance;
            if (story == null) return;
            var saveManager = FTT.Core.SaveManager.Instance;
            var gameManager = FTT.Core.GameManager.Instance;
            int slot = gameManager?.CurrentSession.ActiveSaveSlot ?? -1;
            bool hasSave = saveManager != null
                && slot >= 0 && slot < saveManager.SaveSlots.Length
                && saveManager.SaveSlots[slot] != null;

            // Package 12 W2 (H02): the whole wallet is the open attempt's
            // undeposited dust — only the level-completion transaction banks,
            // so anything a previous Collapse or exit left behind is still at
            // stake and the 20% rule applies to all of it. A slotless developer
            // launch has no save to charge, as before.
            int wallet = story.ChronalDustCollected;
            int unbanked = wallet;
            int retained = hasSave ? CalculateExitWalletAfterPenalty(wallet, unbanked) : wallet;

            story.CommitOrderlyExit(retained);
            if (!hasSave) return;
            // V7.3: the exit fee is paid — the session is settled, so the
            // abnormal-exit marker must not bill it a second time at boot.
            FTT.Core.SessionExitGuard.ClearMarker();
        }

        private void RestoreMenuFocus() {
            ShowMenuPanel();
            FocusChainBuilder.GrabInitialFocus(_focusChain);
        }

        /// <summary>
        /// A confirmation owns the player while it is open, so the pause button
        /// must not resume out from under it.
        /// </summary>
        protected override bool CanTogglePause() =>
            _quitConfirm?.IsOpen != true && _restartConfirm?.IsOpen != true
            && _moveList?.Visible != true
            && _lowerDifficultyConfirm?.IsOpen != true
            && (_logScreen == null || !IsInstanceValid(_logScreen) || !_logScreen.IsOpen);

        protected override void OnPauseStateChanged(bool paused) {
            if (_root != null) _root.Visible = paused;

            if (!paused) {
                _quitConfirm?.Close();
                _restartConfirm?.Close();
                _lowerDifficultyConfirm?.Close();
                if (_moveList != null && IsInstanceValid(_moveList)) _moveList.Close();
                if (_logScreen != null && IsInstanceValid(_logScreen)) _logScreen.Close();
                // Resuming by any route settles the controller notice.
                _awaitingController = false;
                if (_controllerNotice != null) _controllerNotice.Visible = false;
                // The resume is immediate (the game is running again): a half-played open ends on its resting look.
                _pausePanel?.StopAppear(_shade);
                return;
            }

            // A fresh open offers a fresh save; the "saved" feedback belongs to
            // the pause session that performed the save.
            if (_saveButton != null) _saveButton.Text = "menu_save";
            RefreshLowerDifficultyEntry();
            RefreshStatus();
            ShowMenuPanel();

            // Rebuilt on every open: the chain has to reflect what is actually
            // visible now, and the modal's buttons must not join the menu's chain.
            _focusChain = FocusChainBuilder.Build(_menuPanel);
            FocusChainBuilder.GrabInitialFocus(_focusChain);

            // MOT-A row 14: the dim fades in to .70 over 0.16 s while the panel fades and rises (row 4); both tweens run
            // while the tree is paused. Reduced Temporal Effects: fades only.
            _pausePanel?.PlayAppear(_shade);
        }

        /// <summary>
        /// V7.3 Fighter Onboarding: opens the Move List for the campaign's
        /// locked character as an overlay inside this pause CanvasLayer — the
        /// pause menu keeps ownership of SceneTree.Paused, and closing the list
        /// hands focus back to the menu's chain.
        /// </summary>
        private void OpenMoveList() {
            if (_moveList == null || !IsInstanceValid(_moveList)) {
                _moveList = new MoveListScreen { Name = "MoveListScreen", UsesHostDim = true };
                AddChild(_moveList);
                _moveList.Closed += RestoreMenuFocus;
            }
            string characterID =
                FTT.Core.GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "";
            // Package 11 A5: the STORY pause shows the gated list — a slot the
            // Legacy Unlock Schedule has not restored reads Dormant with no
            // frame data. The Fighter pause keeps the full kit.
            _moveList.Open(characterID, MoveListScreen.MoveListMode.Story);
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
    }
}
