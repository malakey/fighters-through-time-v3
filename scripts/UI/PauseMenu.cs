using System.Collections.Generic;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// Story Mode pause menu: Resume / Settings / Save / Restart Level / Exit,
    /// per design-godot.md "Story Mode Pause Menu" (audit M-2). Save writes a
    /// checkpoint-style save without leaving the level; Restart returns to the
    /// last-reached checkpoint behind a confirmation; Exit quits to the main
    /// menu behind a confirmation and applies the designed 50% retention on
    /// undeposited Chronal Dust (docs/DUST_ECONOMY.md §2).
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
                return;
            }
            BuildFallbackUI();
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

            var shade = new ColorRect { Name = "Shade", Color = UIPalette.Shade };
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(shade);

            var center = new CenterContainer { Name = "Center" };
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(center);

            var panel = new PanelContainer { Name = "Panel" };
            panel.CustomMinimumSize = new Vector2(420, 0);
            panel.ThemeTypeVariation = "DialogueGlassPanel";
            center.AddChild(panel);
            _menuPanel = panel;

            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            panel.AddChild(layout);

            var title = new Label {
                Name = "Title",
                Text = "menu_paused",
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.TitleLabelVariation
            };
            title.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            layout.AddChild(title);

            layout.AddChild(MakeButton("ResumeButton", "menu_resume", () => SetPaused(false)));
            layout.AddChild(MakeButton("SettingsButton", "menu_settings", OpenSettings));
            layout.AddChild(MakeButton("MoveListButton", "movelist_title", OpenMoveList));
            _saveButton = MakeButton("SaveButton", "menu_save", SaveProgress);
            layout.AddChild(_saveButton);
            layout.AddChild(MakeButton("RestartButton", "menu_restart", ShowRestartConfirmation));
            layout.AddChild(MakeButton("QuitButton", "pause_quit_to_menu", ShowQuitConfirmation));
        }

        private static Button MakeButton(string name, string textKey, System.Action handler) {
            var button = new Button {
                Name = name,
                Text = textKey,
                CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, UIPalette.ButtonMinHeight),
                ThemeTypeVariation = "TemporalGlassButton"
            };
            button.Pressed += handler;
            return button;
        }

        private void BuildConfirmations() {
            if (_root == null) return;
            _quitConfirm = ConfirmModal.Create("pause_quit_confirm");
            _quitConfirm.Confirmed += QuitToMainMenu;
            _quitConfirm.Cancelled += RestoreMenuFocus;
            _root.AddChild(_quitConfirm);

            _restartConfirm = ConfirmModal.Create("pause_restart_confirm");
            _restartConfirm.Name = "RestartConfirmModal";
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
        /// scene reload), the player starts fresh at 100% HP with a full
        /// rewind pool, and every point of dust earned this level is cleared.
        /// </summary>
        private void RestartLevelFromBeginning() {
            var story = FTT.Core.StoryManager.Instance;
            var saveManager = FTT.Core.SaveManager.Instance;
            var gameManager = FTT.Core.GameManager.Instance;
            SetPaused(false);
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
        }

        private void QuitToMainMenu() {
            ApplyExitDustPenalty();
            SetPaused(false);
            FTT.Core.GameManager.Instance?.LoadScene("res://scenes/menus/MainMenu.tscn");
        }

        /// <summary>
        /// Writes the post-penalty dust into the active story save before the
        /// scene changes. Only the save is written: every campaign re-entry from
        /// the main menu passes through <c>StartCampaign</c>/<c>ResumeCampaign</c>,
        /// which reload <c>StoryManager.ChronalDustCollected</c> from the save,
        /// so the in-memory wallet cannot leak the forfeited dust back.
        /// </summary>
        private void ApplyExitDustPenalty() {
            // F03: exiting during a live Time Freeze ends it and stores 45 s.
            FTT.Core.StoryManager.Instance?.EndActiveTimeFreezeForExplicitSave();
            var story = FTT.Core.StoryManager.Instance;
            var saveManager = FTT.Core.SaveManager.Instance;
            var gameManager = FTT.Core.GameManager.Instance;
            if (story == null || saveManager == null || gameManager == null) return;

            int slot = gameManager.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= saveManager.SaveSlots.Length || saveManager.SaveSlots[slot] == null) return;

            // Package 12 W2 (H02): the whole wallet is the open attempt's
            // undeposited dust — only the level-completion transaction banks,
            // so anything a previous Collapse or exit left behind is still at
            // stake and the 20% rule applies to all of it.
            int wallet = story.ChronalDustCollected;
            int unbanked = wallet;

            saveManager.SaveSlots[slot].LevelChronalDust = CalculateExitWalletAfterPenalty(wallet, unbanked);
            saveManager.SaveStorySlot(slot);
            // V7.3: the exit fee is paid — the session is settled, so the
            // abnormal-exit marker must not bill it a second time at boot.
            FTT.Core.SessionExitGuard.ClearMarker();
        }

        private void RestoreMenuFocus() => FocusChainBuilder.GrabInitialFocus(_focusChain);

        /// <summary>
        /// A confirmation owns the player while it is open, so the pause button
        /// must not resume out from under it.
        /// </summary>
        protected override bool CanTogglePause() =>
            _quitConfirm?.IsOpen != true && _restartConfirm?.IsOpen != true
            && _moveList?.Visible != true;

        protected override void OnPauseStateChanged(bool paused) {
            if (_root != null) _root.Visible = paused;

            if (!paused) {
                _quitConfirm?.Close();
                _restartConfirm?.Close();
                if (_moveList != null && IsInstanceValid(_moveList)) _moveList.Close();
                return;
            }

            // A fresh open offers a fresh save; the "saved" feedback belongs to
            // the pause session that performed the save.
            if (_saveButton != null) _saveButton.Text = "menu_save";

            // Rebuilt on every open: the chain has to reflect what is actually
            // visible now, and the modal's buttons must not join the menu's chain.
            _focusChain = FocusChainBuilder.Build(_menuPanel);
            FocusChainBuilder.GrabInitialFocus(_focusChain);
        }

        /// <summary>
        /// V7.3 Fighter Onboarding: opens the Move List for the campaign's
        /// locked character as an overlay inside this pause CanvasLayer — the
        /// pause menu keeps ownership of SceneTree.Paused, and closing the list
        /// hands focus back to the menu's chain.
        /// </summary>
        private void OpenMoveList() {
            if (_moveList == null || !IsInstanceValid(_moveList)) {
                _moveList = new MoveListScreen { Name = "MoveListScreen" };
                AddChild(_moveList);
                _moveList.Closed += RestoreMenuFocus;
            }
            string characterID =
                FTT.Core.GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "";
            // Package 11 A5: the STORY pause shows the gated list — a slot the
            // Legacy Unlock Schedule has not restored reads Dormant with no
            // frame data. The Fighter pause keeps the full kit.
            _moveList.Open(characterID, MoveListScreen.MoveListMode.Story);
        }

        private void OpenSettings() {
            if (_settingsMenu == null || !IsInstanceValid(_settingsMenu)) {
                _settingsMenu = new SettingsMenu { Name = "SettingsMenu" };
                AddChild(_settingsMenu);
                // H-10: Settings holds focus while open; hand it back to this
                // menu's chain on close (mirrors MainMenu's restore).
                _settingsMenu.Closed += RestoreMenuFocus;
            }
            _settingsMenu.Show();
        }
    }
}
