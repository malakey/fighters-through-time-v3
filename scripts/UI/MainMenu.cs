using System;
using System.Collections.Generic;
using Godot;

namespace FTT.UI {

    /// <summary>Which authored screen of the main menu is showing.</summary>
    public enum MainMenuScreen {
        Root,
        SlotSelect,
        CharacterSelect,
        DifficultySelect,
        /// <summary>Developer level select (temporary, debug builds only).</summary>
        LevelSelect
    }

    /// <summary>
    /// Package 8 B4. The main menu, converted from a fully code-built shell to an
    /// authored, themed scene.
    ///
    /// <para>Every visible control now lives in <c>scenes/menus/MainMenu.tscn</c>
    /// and this script binds it by path. The four screens (root, story slot select,
    /// story character select, difficulty select) are authored siblings that are
    /// shown and hidden rather than instantiated and freed, which is what makes a
    /// uniform back stack possible: <c>ui_cancel</c> walks one screen back from
    /// wherever the player is, and the root screen is the floor.</para>
    ///
    /// <para>A fifth screen, the developer level select (2026-08-15), is a
    /// temporary debug-build affordance: root "Level Select (Dev)" → the shared
    /// character screen → a sixteen-level grid → the shared difficulty screen →
    /// <see cref="FTT.Core.StoryManager.StartLevelDirect"/>. It never creates or
    /// writes a save slot. Retire it with <see cref="ShowDeveloperLevelSelect"/>.</para>
    ///
    /// <para>Static copy is stored in the scene as raw translation keys and
    /// resolved by Godot's automatic control translation (the A1 convention).
    /// Only the strings that need runtime arguments — slot summaries, character
    /// names, difficulty descriptions — are assigned here.</para>
    /// </summary>
    public partial class MainMenu : Control {

        /// <summary>
        /// The roster, in authored manifest order — which is the grid order the
        /// character screen paints. Package 11 A6b: read from
        /// <see cref="FTT.Core.CharacterRoster"/> (the content manifest), never
        /// a literal cast list, per the design's standing "the roster will grow"
        /// mandate.
        /// </summary>
        private string[] RosterIDs => _rosterIDs ??= FTT.Core.CharacterRoster.ToArray();
        private string[] _rosterIDs;

        private const int StorySlotCount = 3;

        /// <summary>
        /// Developer level-select tile count. Seventeen as of V7.6 (Package 11 A12):
        /// the sixteen shared campaign slots plus <c>CampaignLevel.LegacyNexus</c>,
        /// the per-character Level 4A. Tiles are keyed by enum value, not by route
        /// position, which is why 4A is tile 16 and still labels itself "04A".
        /// </summary>
        private const int CampaignLevelCount = 17;

        /// <summary>
        /// Test seam for the developer level-select gate: null defers to
        /// <c>OS.IsDebugBuild()</c> (the <c>SaveManager.IsLegacyMigrationEnabled</c> idiom).
        /// </summary>
        internal static bool? DeveloperLevelSelectOverrideForTesting;

        /// <summary>
        /// Temporary development affordance (2026-08-15): the root-screen "Level
        /// Select (Dev)" button and its screen exist only in debug builds. Remove
        /// the button, the screen, and this gate together when it is retired.
        /// </summary>
        public static bool ShowDeveloperLevelSelect =>
            DeveloperLevelSelectOverrideForTesting ?? OS.IsDebugBuild();

        private SettingsMenu _settingsMenu;
        private ConfirmModal _confirmModal;
        private ConfirmModal _quitConfirm;

        private Control _rootScreen;
        private Control _slotScreen;
        private Control _characterScreen;
        private Control _difficultyScreen;
        private Control _levelSelectScreen;
        private TextureRect _temporalMainArt;
        private TextureRect _temporalSubmenuArt;

        private readonly Button[] _slotButtons = new Button[StorySlotCount];
        private readonly Button[] _deleteButtons = new Button[StorySlotCount];
        private readonly List<MainMenuScreen> _stack = new() { MainMenuScreen.Root };

        private string _pendingCharacterID = "";
        private int _pendingDeleteSlot = -1;

        /// <summary>
        /// True while the developer level-select flow is borrowing the character and
        /// difficulty screens; routes their confirmations to the direct level launch
        /// instead of the new-campaign path.
        /// </summary>
        private bool _developerLevelFlow;
        private FTT.Core.CampaignLevel _pendingLevel = FTT.Core.CampaignLevel.Tutorial;

        /// <summary>Whether the developer level-select flow is active. Test surface.</summary>
        public bool IsDeveloperLevelFlow => _developerLevelFlow;

        /// <summary>The level chosen in the developer flow. Test surface.</summary>
        public FTT.Core.CampaignLevel PendingLevel => _pendingLevel;

        /// <summary>The screen currently showing. Test surface for the back walk.</summary>
        public MainMenuScreen CurrentScreen => _stack[^1];

        /// <summary>How deep the back stack is; 1 at the root screen.</summary>
        public int ScreenDepth => _stack.Count;

        /// <summary>The shared confirmation modal, created lazily on first use.</summary>
        public ConfirmModal Confirmation => _confirmModal;

        /// <summary>The quit confirmation modal, created lazily on first use. Test seam.</summary>
        public ConfirmModal QuitConfirmation => _quitConfirm;

        public override void _Ready() {
            UIPalette.ApplyTheme(this);

            _rootScreen = GetNode<Control>("RootScreen");
            _slotScreen = GetNode<Control>("SlotScreen");
            _characterScreen = GetNode<Control>("CharacterScreen");
            _difficultyScreen = GetNode<Control>("DifficultyScreen");
            _levelSelectScreen = GetNode<Control>("LevelSelectScreen");
            _temporalMainArt = GetNodeOrNull<TextureRect>("Background/TemporalMainArt");
            _temporalSubmenuArt = GetNodeOrNull<TextureRect>("Background/TemporalSubmenuArt");

            BindRootScreen();
            BindSlotScreen();
            BindCharacterScreen();
            BindDifficultyScreen();
            BindLevelSelectScreen();

            AddSaveLoadNotice(GetNode<Control>("RootScreen/Center/Panel/Layout/NoticeSlot"));
            ShowScreen(MainMenuScreen.Root, resetStack: true);
        }

        // ---- Binding ---------------------------------------------------------

        private void BindRootScreen() {
            const string layout = "RootScreen/Center/Panel/Layout/";
            GetNode<Button>(layout + "QuickPlayButton").Pressed += OnQuickPlayPressed;
            GetNode<Button>(layout + "StoryButton").Pressed += OnStoryModePressed;
            var levelSelect = GetNode<Button>(layout + "LevelSelectButton");
            levelSelect.Visible = ShowDeveloperLevelSelect;
            levelSelect.Pressed += OnLevelSelectPressed;
            var fighterButton = GetNode<Button>(layout + "FighterButton");
            fighterButton.Pressed += () => {
                // Entering the local flow clears any lingering LAN opponent type.
                var gameManager = FTT.Core.GameManager.Instance;
                if (gameManager != null
                    && gameManager.CurrentSession.FighterOpponentType == FTT.Core.FighterOpponentType.Lan) {
                    FTT.Core.SessionData session = gameManager.CurrentSession;
                    session.FighterOpponentType = FTT.Core.FighterOpponentType.Cpu;
                    gameManager.CurrentSession = session;
                }
                FTT.Core.GameManager.Instance?.LoadScene("res://scenes/menus/CharacterSelect.tscn");
            };
            // V7.3: LAN de-scoped to Package 7 — no LanButton on the root screen.
            // NetworkSelectScreen and scripts/Networking/ stay for the later package.
            GetNode<Button>(layout + "SettingsButton").Pressed += OpenSettings;
            GetNode<Button>(layout + "QuitButton").Pressed += ConfirmQuit;
        }

        /// <summary>
        /// M-30 (audit 2026-08-08). Design §"Main Menu Screen": Quit Game shows a
        /// confirmation modal; only Confirm terminates the process. Routed through
        /// the shared themed <see cref="ConfirmModal"/> (the slot-delete idiom), so
        /// it traps focus and restores it to the quit button on cancel.
        /// </summary>
        private void ConfirmQuit() {
            if (_quitConfirm == null || !IsInstanceValid(_quitConfirm)) {
                _quitConfirm = ConfirmModal.Create("menu_quit_confirm", "common_confirm", "common_cancel", "menu_quit");
                _quitConfirm.Name = "QuitConfirmModal";
                _quitConfirm.Confirmed += () => GetTree().Quit();
                AddChild(_quitConfirm);
            }
            _quitConfirm.Open();
        }

        private void BindSlotScreen() {
            const string layout = "SlotScreen/Center/Panel/Layout/";
            for (int slot = 0; slot < StorySlotCount; slot++) {
                int captured = slot;
                _slotButtons[slot] = GetNode<Button>($"{layout}SlotRow{slot}/SlotButton");
                _deleteButtons[slot] = GetNode<Button>($"{layout}SlotRow{slot}/DeleteButton");
                _slotButtons[slot].Pressed += () => OnSlotPressed(captured);
                _deleteButtons[slot].Pressed += () => ConfirmDeleteStorySlot(captured);
            }
            GetNode<Button>(layout + "BackButton").Pressed += GoBack;
        }

        private void BindCharacterScreen() {
            const string layout = "CharacterScreen/Center/Panel/Layout/";
            var grid = GetNode<GridContainer>(layout + "Grid");
            // Package 11 A6b: the roster comes from the content manifest, never
            // from a literal cast list (the A9 mandate). The authored grid still
            // carries a fixed number of CharacterButton tiles, so a roster that
            // outgrows the scene binds the tiles that exist and leaves the rest
            // unbound rather than throwing out of _Ready — authoring the extra
            // tiles is scene work, not a code change. GetNodeOrNull is what
            // makes that degradation graceful.
            for (int index = 0; index < RosterIDs.Length; index++) {
                string characterID = RosterIDs[index];
                var button = grid.GetNodeOrNull<Button>($"CharacterButton{index}");
                if (button == null) continue;
                button.ThemeTypeVariation = "TemporalGlassButton";
                button.Text = CharacterName(characterID);
                button.Pressed += () => OnCharacterPressed(characterID);
            }
            GetNode<Button>(layout + "BackButton").Pressed += GoBack;
        }

        private void BindDifficultyScreen() {
            const string layout = "DifficultyScreen/Center/Panel/Layout/";
            BindDifficultyButton(
                GetNode<Button>(layout + "EasyButton"),
                FTT.Core.Difficulty.Easy, "difficulty_easy", "difficulty_easy_description");
            BindDifficultyButton(
                GetNode<Button>(layout + "NormalButton"),
                FTT.Core.Difficulty.Normal, "difficulty_normal", "difficulty_normal_description");
            BindDifficultyButton(
                GetNode<Button>(layout + "HardButton"),
                FTT.Core.Difficulty.Hard, "difficulty_hard", "difficulty_hard_description");
            GetNode<Button>(layout + "BackButton").Pressed += GoBack;
        }

        private void BindDifficultyButton(
            Button button,
            FTT.Core.Difficulty difficulty,
            string nameKey,
            string descriptionKey) {
            // Two lines with different emphasis cannot be one raw key, so this one
            // resolves here rather than through automatic control translation.
            button.Text = $"{Tr(nameKey)}\n{Tr(descriptionKey)}";
            button.Pressed += () => OnDifficultyPressed(difficulty);
        }

        /// <summary>
        /// Developer level select. Seventeen authored buttons, one per
        /// <see cref="FTT.Core.CampaignLevel"/>, labelled with the level's campaign
        /// number plus the hub's <c>campaign_level_*</c> mission name (the scene
        /// stores the raw key; the numbered prefix is why the text is composed here).
        /// Level 4A labels itself "04A" rather than "16": the enum value is an
        /// identity, and the slot's campaign number is what a developer looks for.
        /// </summary>
        private void BindLevelSelectScreen() {
            const string layout = "LevelSelectScreen/Center/Panel/Layout/";
            var grid = GetNode<GridContainer>(layout + "Grid");
            for (int index = 0; index < CampaignLevelCount; index++) {
                var level = (FTT.Core.CampaignLevel)index;
                var button = grid.GetNode<Button>($"LevelButton{index}");
                button.ThemeTypeVariation = "TemporalGlassButton";
                button.Text = $"{LevelSelectNumber(level)}  {Tr(FTT.Environment.HubWorldController.CampaignLevelNameKey(level))}";
                button.Pressed += () => OnLevelPressed(level);
            }
            GetNode<Button>(layout + "BackButton").Pressed += GoBack;
        }

        /// <summary>The campaign number shown on a developer tile ("04A" for Level 4A).</summary>
        internal static string LevelSelectNumber(FTT.Core.CampaignLevel level) =>
            level == FTT.Core.CampaignLevel.LegacyNexus ? "04A" : $"{(int)level:00}";

        // ---- Screen stack ----------------------------------------------------

        /// <summary>
        /// Shows one authored screen and rebuilds its focus chain. Focus authoring
        /// has to be redone per transition because the chain is a property of the
        /// visible controls, and the slot screen's delete buttons come and go with
        /// the save data.
        /// </summary>
        private void ShowScreen(MainMenuScreen screen, bool resetStack = false) {
            if (resetStack) {
                _stack.Clear();
                _stack.Add(MainMenuScreen.Root);
            }

            _rootScreen.Visible = screen == MainMenuScreen.Root;
            _slotScreen.Visible = screen == MainMenuScreen.SlotSelect;
            _characterScreen.Visible = screen == MainMenuScreen.CharacterSelect;
            _difficultyScreen.Visible = screen == MainMenuScreen.DifficultySelect;
            _levelSelectScreen.Visible = screen == MainMenuScreen.LevelSelect;
            if (screen == MainMenuScreen.Root) _developerLevelFlow = false;
            if (_temporalMainArt != null) _temporalMainArt.Visible = screen == MainMenuScreen.Root;
            if (_temporalSubmenuArt != null) _temporalSubmenuArt.Visible = screen != MainMenuScreen.Root;

            if (screen == MainMenuScreen.SlotSelect) RefreshSlotRows();
            FocusChainBuilder.Apply(ScreenRoot(screen));
        }

        private Control ScreenRoot(MainMenuScreen screen) => screen switch {
            MainMenuScreen.SlotSelect => _slotScreen,
            MainMenuScreen.CharacterSelect => _characterScreen,
            MainMenuScreen.DifficultySelect => _difficultyScreen,
            MainMenuScreen.LevelSelect => _levelSelectScreen,
            _ => _rootScreen
        };

        private void PushScreen(MainMenuScreen screen) {
            _stack.Add(screen);
            ShowScreen(screen);
        }

        /// <summary>
        /// Walks one screen back. Public so the cancel path is exercisable without
        /// synthetic input. At the root screen it is deliberately a no-op — the main
        /// menu is the floor of the application, and cancel there must not quit.
        /// </summary>
        public void GoBack() {
            if (_quitConfirm is { IsOpen: true }) {
                _quitConfirm.Cancel();
                return;
            }
            if (_confirmModal is { IsOpen: true }) {
                _confirmModal.Cancel();
                return;
            }
            if (_stack.Count <= 1) return;
            _stack.RemoveAt(_stack.Count - 1);
            ShowScreen(CurrentScreen);
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (@event == null || !@event.IsActionPressed("ui_cancel")) return;
            if (_settingsMenu != null && IsInstanceValid(_settingsMenu) && _settingsMenu.Visible) return;
            if (_stack.Count <= 1 && _confirmModal is not { IsOpen: true }) return;
            GetViewport()?.SetInputAsHandled();
            GoBack();
        }

        // ---- Root screen actions --------------------------------------------

        private void OnQuickPlayPressed() {
            if (FTT.Core.GameManager.Instance == null) return;
            FTT.Core.SessionData session = FTT.Core.GameManager.Instance.CurrentSession;
            session.SelectedCharacterID = "einstein";
            FTT.Core.GameManager.Instance.CurrentSession = session;
            FTT.Core.GameManager.Instance.LoadScene("res://scenes/arenas/TestArena.tscn");
        }

        private void OnStoryModePressed() {
            if (FTT.Core.GameManager.Instance == null) return;
            _developerLevelFlow = false;
            PushScreen(MainMenuScreen.SlotSelect);
        }

        /// <summary>
        /// Developer level select: character → level → difficulty → direct launch.
        /// Borrows the authored character and difficulty screens; the flag decides
        /// where their confirmations go.
        /// </summary>
        private void OnLevelSelectPressed() {
            if (FTT.Core.GameManager.Instance == null || !ShowDeveloperLevelSelect) return;
            _developerLevelFlow = true;
            PushScreen(MainMenuScreen.CharacterSelect);
        }

        private void OpenSettings() {
            if (_settingsMenu == null || !IsInstanceValid(_settingsMenu)) {
                _settingsMenu = new SettingsMenu { Name = "SettingsMenu" };
                AddChild(_settingsMenu);
                _settingsMenu.Closed += () => FocusChainBuilder.Apply(ScreenRoot(CurrentScreen));
            }
            _settingsMenu.Show();
        }

        /// <summary>
        /// Package 8 A4. <c>SaveManager.LastLoadNotice</c> recorded backup
        /// recoveries, schema migrations, and corruption resets but had no consumer,
        /// so a player whose save was recovered or reset was never told. The notice
        /// is now a translation key plus arguments; this surfaces it once, localized,
        /// on the first screen after boot.
        /// </summary>
        private static void AddSaveLoadNotice(Control parent) {
            FTT.Core.SaveManager manager = FTT.Core.SaveManager.Instance;
            if (parent == null || manager == null || string.IsNullOrEmpty(manager.LastLoadNoticeKey)) return;
            var notice = new Label {
                Name = "SaveLoadNotice",
                Text = manager.LastLoadNotice,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(360, 0)
            };
            notice.AddThemeColorOverride("font_color", UIPalette.Gold);
            parent.AddChild(notice);
        }

        // ---- Story slot select ----------------------------------------------

        /// <summary>
        /// Repaints the three authored slot rows from the current save data. A slot
        /// with no save hides its delete button rather than disabling it, so the
        /// focus chain does not stop on a control that can never do anything.
        /// </summary>
        private void RefreshSlotRows() {
            for (int slot = 0; slot < StorySlotCount; slot++) {
                FTT.Core.StorySaveData save = FTT.Core.SaveManager.Instance?.SaveSlots[slot];
                _slotButtons[slot].Text = save == null
                    ? string.Format(Tr("save_slot_empty"), slot + 1)
                    : SaveSlotSummary(slot, save);
                _deleteButtons[slot].Visible = save != null;
            }
        }

        private void OnSlotPressed(int slot) {
            FTT.Core.StorySaveData save = FTT.Core.SaveManager.Instance?.SaveSlots[slot];
            if (save == null) {
                if (FTT.Core.GameManager.Instance == null) return;
                FTT.Core.SessionData session = FTT.Core.GameManager.Instance.CurrentSession;
                session.ActiveSaveSlot = slot;
                FTT.Core.GameManager.Instance.CurrentSession = session;
                PushScreen(MainMenuScreen.CharacterSelect);
                return;
            }
            FTT.Core.StoryManager.Instance?.ResumeCampaign(slot, save);
        }

        /// <summary>
        /// Slot deletion is destructive, so it goes through the shared themed
        /// <see cref="ConfirmModal"/> rather than the native
        /// <c>ConfirmationDialog</c> it used to use — that one opened an OS-level
        /// window, ignored the theme, and trapped no focus, so a controller player
        /// could tab off it onto the delete button behind.
        /// </summary>
        private void ConfirmDeleteStorySlot(int slotIndex) {
            FTT.Core.StorySaveData save = FTT.Core.SaveManager.Instance?.SaveSlots[slotIndex];
            if (save == null) return;

            _pendingDeleteSlot = slotIndex;
            EnsureConfirmModal();
            // Already-formatted copy, not a raw key: automatic control translation
            // leaves an unknown string untouched, so a resolved sentence is safe here.
            _confirmModal.SetPromptKey(string.Format(
                Tr("save_confirm_delete"),
                slotIndex + 1,
                CharacterName(save.SelectedCharacterID)));
            _confirmModal.Open();
        }

        private void EnsureConfirmModal() {
            if (_confirmModal != null && IsInstanceValid(_confirmModal)) return;
            _confirmModal = ConfirmModal.Create(
                "save_confirm_delete",
                "common_confirm",
                "common_cancel",
                "save_delete_title");
            _confirmModal.Confirmed += OnDeleteConfirmed;
            _confirmModal.Cancelled += () => _pendingDeleteSlot = -1;
            AddChild(_confirmModal);
        }

        private void OnDeleteConfirmed() {
            int slot = _pendingDeleteSlot;
            _pendingDeleteSlot = -1;
            if (slot < 0) return;
            FTT.Core.SaveManager.Instance?.DeleteStorySlot(slot);
            RefreshSlotRows();
            FocusChainBuilder.Apply(_slotScreen);
        }

        private string SaveSlotSummary(int slotIndex, FTT.Core.StorySaveData save) {
            string difficultyKey = save.Difficulty switch {
                FTT.Core.Difficulty.Easy => "difficulty_easy",
                FTT.Core.Difficulty.Hard => "difficulty_hard",
                _ => "difficulty_normal"
            };
            string progress = save.IsCompleted
                ? Tr("save_completed")
                : LocationText(save.CurrentLevelID);
            string timestamp = Tr("save_timestamp_unknown");
            if (DateTimeOffset.TryParse(save.LastSavedTimestamp, out DateTimeOffset savedAt)) {
                timestamp = savedAt.ToLocalTime().ToString("g");
            }
            return string.Format(
                Tr("save_slot_summary"),
                slotIndex + 1,
                CharacterName(save.SelectedCharacterID),
                progress,
                Tr(difficultyKey),
                timestamp);
        }

        /// <summary>
        /// Audit Low (UI): the slot row used to render the raw scene basename
        /// ("Level_02_Orleans") as the save's location — an unlocalized machine ID.
        /// The scene path maps onto the authored per-level <c>*_level_title</c> key
        /// family through <see cref="LoadingScreen.LevelTitleKeyForScene"/> (the
        /// existing path-to-title mapping); an unknown path falls back to the old
        /// basename so a forward-compatible save still shows something.
        /// </summary>
        private string LocationText(string currentLevelID) {
            string fallback = currentLevelID.GetFile().GetBaseName();
            string titleKey = LoadingScreen.LevelTitleKeyForScene(currentLevelID);
            if (string.IsNullOrEmpty(titleKey)) return fallback;
            string resolved = Tr(titleKey);
            // Tr returns the key itself when the table has no entry for it.
            return resolved == titleKey ? fallback : resolved;
        }

        // ---- Story character / difficulty select -----------------------------

        private void OnCharacterPressed(string characterID) {
            _pendingCharacterID = characterID;
            PushScreen(_developerLevelFlow ? MainMenuScreen.LevelSelect : MainMenuScreen.DifficultySelect);
        }

        private void OnLevelPressed(FTT.Core.CampaignLevel level) {
            _pendingLevel = level;
            PushScreen(MainMenuScreen.DifficultySelect);
        }

        private void OnDifficultyPressed(FTT.Core.Difficulty difficulty) {
            if (_developerLevelFlow) {
                BeginDirectLevel(_pendingCharacterID, _pendingLevel, difficulty);
            } else {
                BeginNewStory(_pendingCharacterID, difficulty);
            }
        }

        /// <summary>
        /// Developer level select launch. No save slot is created or touched
        /// (<see cref="FTT.Core.StoryManager.PrepareDirectLevel"/> clears the
        /// session slot to <c>-1</c>), so a dev launch can never overwrite a real
        /// campaign; autosave inside the level silently no-ops.
        /// </summary>
        private void BeginDirectLevel(
            string characterID,
            FTT.Core.CampaignLevel level,
            FTT.Core.Difficulty difficulty) {
            if (string.IsNullOrEmpty(characterID) || FTT.Core.GameManager.Instance == null) return;
            FTT.Core.StoryManager storyMgr = FTT.Core.StoryManager.Instance;
            if (storyMgr == null) return;
            storyMgr.StartLevelDirect(level, characterID, difficulty);
        }

        private void BeginNewStory(string characterID, FTT.Core.Difficulty difficulty) {
            if (string.IsNullOrEmpty(characterID) || FTT.Core.GameManager.Instance == null) return;
            FTT.Core.SessionData session = FTT.Core.GameManager.Instance.CurrentSession;
            session.Difficulty = difficulty;
            FTT.Core.GameManager.Instance.CurrentSession = session;
            FTT.Core.StoryManager storyMgr = FTT.Core.StoryManager.Instance;
            if (storyMgr != null) {
                storyMgr.StartCampaign(characterID);
            } else {
                FTT.Core.SessionData fallbackSession = FTT.Core.GameManager.Instance.CurrentSession;
                fallbackSession.SelectedCharacterID = characterID;
                FTT.Core.GameManager.Instance.CurrentSession = fallbackSession;
                FTT.Core.GameManager.Instance.LoadScene("res://scenes/campaign/HubWorld.tscn");
            }
        }

        /// <summary>
        /// Resolves a character's localized display name. Goes through
        /// <see cref="FTT.Core.AuthoredResources"/> rather than <c>GD.Load</c>:
        /// a bare load drops the pinned instance and rebuilds the whole scripted
        /// resource graph on the next call, which races the .NET finalizer thread
        /// (CLAUDE.md failure signature 2).
        /// </summary>
        private string CharacterName(string characterID) {
            if (string.IsNullOrWhiteSpace(characterID)) return Tr("common_unknown");
            FTT.Characters.CharacterData data = FTT.Core.AuthoredResources.Load<FTT.Characters.CharacterData>(
                $"res://resources/Characters/{characterID}_data.tres");
            return data == null || string.IsNullOrWhiteSpace(data.DisplayNameKey)
                ? Tr("common_unknown")
                : Tr(data.DisplayNameKey);
        }
    }
}
