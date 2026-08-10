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
            center.AddChild(panel);
            _menuPanel = panel;

            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            panel.AddChild(layout);

            var title = new Label {
                Name = "Title",
                Text = "menu_paused",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            title.AddThemeFontSizeOverride("font_size", UIPalette.TitleFontSize);
            layout.AddChild(title);

            layout.AddChild(MakeButton("ResumeButton", "menu_resume", () => SetPaused(false)));
            layout.AddChild(MakeButton("SettingsButton", "menu_settings", OpenSettings));
            _saveButton = MakeButton("SaveButton", "menu_save", SaveProgress);
            layout.AddChild(_saveButton);
            layout.AddChild(MakeButton("RestartButton", "menu_restart", ShowRestartConfirmation));
            layout.AddChild(MakeButton("QuitButton", "pause_quit_to_menu", ShowQuitConfirmation));
        }

        private static Button MakeButton(string name, string textKey, System.Action handler) {
            var button = new Button {
                Name = name,
                Text = textKey,
                CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, UIPalette.ButtonMinHeight)
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
            _restartConfirm.Confirmed += RestartFromCheckpoint;
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
                RestartFromCheckpoint();
                return;
            }
            _restartConfirm.Open();
        }

        /// <summary>
        /// Designed Restart Level option: back to the current level's
        /// last-reached checkpoint with 100% HP and a full rewind pool, no dust
        /// penalty. The save is stamped first, then the level scene reloads and
        /// its controller restores from <c>LastCheckpointID</c> exactly as a
        /// campaign resume does.
        /// </summary>
        private void RestartFromCheckpoint() {
            var story = FTT.Core.StoryManager.Instance;
            var saveManager = FTT.Core.SaveManager.Instance;
            var gameManager = FTT.Core.GameManager.Instance;
            SetPaused(false);
            if (story == null || saveManager == null || gameManager == null) return;

            FTT.Core.Difficulty difficulty = gameManager.CurrentSession.Difficulty;
            int maxRewinds = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
            story.SetRewinds(maxRewinds);

            int slot = gameManager.CurrentSession.ActiveSaveSlot;
            if (slot >= 0 && slot < saveManager.SaveSlots.Length && saveManager.SaveSlots[slot] != null) {
                FTT.Core.StorySaveData save = saveManager.SaveSlots[slot];
                save.CurrentLives = maxRewinds;
                save.LevelChronalDust = story.ChronalDustCollected;
                if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is FTT.Characters.PlayerController player) {
                    save.CurrentHP = player.MaximumHP;
                }
                saveManager.SaveStorySlot(slot);
            }

            story.LoadCurrentLevel();
        }

        // === Exit to main menu =============================================

        /// <summary>
        /// Designed Exit rule: the player retains 50% of undeposited Chronal
        /// Dust, rounded down (design-godot.md "Pause Screen Rules";
        /// docs/DUST_ECONOMY.md §2 models this penalty).
        /// </summary>
        public static int CalculateExitRetainedDust(int unbankedDust) =>
            Mathf.Max(0, unbankedDust) / 2;

        /// <summary>
        /// The wallet after the exit penalty: only dust earned in the current
        /// level is penalized, so any residue that predates the level (already
        /// counted in the wallet but not earned here) survives intact.
        /// </summary>
        public static int CalculateExitWalletAfterPenalty(int walletDust, int unbankedLevelDust) {
            int wallet = Mathf.Max(0, walletDust);
            int unbanked = Mathf.Clamp(unbankedLevelDust, 0, wallet);
            int forfeited = unbanked - CalculateExitRetainedDust(unbanked);
            return wallet - forfeited;
        }

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
            var story = FTT.Core.StoryManager.Instance;
            var saveManager = FTT.Core.SaveManager.Instance;
            var gameManager = FTT.Core.GameManager.Instance;
            if (story == null || saveManager == null || gameManager == null) return;

            int slot = gameManager.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= saveManager.SaveSlots.Length || saveManager.SaveSlots[slot] == null) return;

            // The level controller's tally is the "earned this level" figure; the
            // Tutorial and Florence predate StoryLevelControllerBase, so they fall
            // back to the whole wallet (equivalent in practice — the hub deposits
            // the wallet on every visit, so a level starts from zero).
            int wallet = story.ChronalDustCollected;
            int unbanked = GetTree()?.CurrentScene is FTT.Environment.StoryLevelControllerBase level
                ? level.DustEarnedThisLevel
                : wallet;

            saveManager.SaveSlots[slot].LevelChronalDust = CalculateExitWalletAfterPenalty(wallet, unbanked);
            saveManager.SaveStorySlot(slot);
        }

        private void RestoreMenuFocus() => FocusChainBuilder.GrabInitialFocus(_focusChain);

        /// <summary>
        /// A confirmation owns the player while it is open, so the pause button
        /// must not resume out from under it.
        /// </summary>
        protected override bool CanTogglePause() =>
            _quitConfirm?.IsOpen != true && _restartConfirm?.IsOpen != true;

        protected override void OnPauseStateChanged(bool paused) {
            if (_root != null) _root.Visible = paused;

            if (!paused) {
                _quitConfirm?.Close();
                _restartConfirm?.Close();
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
