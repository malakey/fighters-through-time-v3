using Godot;
using System;

namespace FTT.UI {
    public partial class MainMenu : Control {
		private SettingsMenu _settingsMenu;

        public override void _Ready() {
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0.06f, 0.06f, 0.12f, 1);
            AddChild(bg);

            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(center);

            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(400, 0);
            center.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 12);
            panel.AddChild(vbox);

            var title = new Label();
            title.Text = Tr("game_title");
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeColorOverride("font_color", new Color(0, 0.9f, 0.9f));
            title.AddThemeFontSizeOverride("font_size", 28);
            vbox.AddChild(title);

            var subtitle = new Label();
            subtitle.Text = Tr("menu_subtitle");
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            subtitle.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
            vbox.AddChild(subtitle);

            AddSaveLoadNotice(vbox);

            vbox.AddChild(new HSeparator());

            var quickPlayBtn = CreateButton(Tr("menu_quick_play"));
            quickPlayBtn.Pressed += () => {
                if (FTT.Core.GameManager.Instance == null) return;
                var session = FTT.Core.GameManager.Instance.CurrentSession;
                session.SelectedCharacterID = "einstein";
                FTT.Core.GameManager.Instance.CurrentSession = session;
                FTT.Core.GameManager.Instance.LoadScene("res://scenes/arenas/TestArena.tscn");
            };
            vbox.AddChild(quickPlayBtn);

            var storyBtn = CreateButton(Tr("menu_story_mode"));
            storyBtn.Pressed += OnStoryModePressed;
            vbox.AddChild(storyBtn);

            var fighterBtn = CreateButton(Tr("menu_fighter_mode"));
            fighterBtn.Pressed += () => FTT.Core.GameManager.Instance?.LoadScene("res://scenes/menus/CharacterSelect.tscn");
            vbox.AddChild(fighterBtn);

            var settingsBtn = CreateButton(Tr("menu_settings"));
            settingsBtn.Pressed += OpenSettings;
            vbox.AddChild(settingsBtn);

            vbox.AddChild(new HSeparator());

            var quitBtn = CreateButton(Tr("menu_quit"));
            quitBtn.Pressed += () => GetTree().Quit();
            vbox.AddChild(quitBtn);
        }

        /// <summary>
        /// Package 8 A4. <c>SaveManager.LastLoadNotice</c> recorded backup
        /// recoveries, schema migrations, and corruption resets but had no consumer,
        /// so a player whose save was recovered or reset was never told. The notice
        /// is now a translation key plus arguments; this surfaces it once, localized,
        /// on the first screen after boot. Additive by design — B4's authored menu
        /// carries this line over.
        /// </summary>
        private static void AddSaveLoadNotice(Control parent) {
            FTT.Core.SaveManager manager = FTT.Core.SaveManager.Instance;
            if (manager == null || string.IsNullOrEmpty(manager.LastLoadNoticeKey)) return;
            var notice = new Label {
                Name = "SaveLoadNotice",
                Text = manager.LastLoadNotice,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(360, 0)
            };
            notice.AddThemeColorOverride("font_color", new Color(0.95f, 0.8f, 0.3f));
            parent.AddChild(notice);
        }

		private void OpenSettings() {
			if (_settingsMenu == null || !IsInstanceValid(_settingsMenu)) {
				_settingsMenu = new SettingsMenu { Name = "SettingsMenu" };
				AddChild(_settingsMenu);
			}
			_settingsMenu.Show();
		}

        private Button CreateButton(string text) {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(350, 50);
            return btn;
        }

        private void OnStoryModePressed() {
            if (FTT.Core.GameManager.Instance == null) return;
            ShowStorySlotSelect();
        }

        private void ShowStorySlotSelect() {
            HideMenuControls();
            var selectPanel = new PanelContainer { Name = "StorySlotSelect" };
            selectPanel.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(selectPanel);
            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            selectPanel.AddChild(center);
            var layout = new VBoxContainer();
            layout.AddThemeConstantOverride("separation", 14);
            center.AddChild(layout);
            var title = new Label {
                Text = Tr("save_select_title"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 28);
            layout.AddChild(title);
            for (int slot = 0; slot < 3; slot++) {
                int capturedSlot = slot;
                FTT.Core.StorySaveData save = FTT.Core.SaveManager.Instance?.SaveSlots[slot];
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 10);
                var button = new Button {
                    Text = save == null
                        ? string.Format(Tr("save_slot_empty"), slot + 1)
                        : SaveSlotSummary(slot, save),
                    CustomMinimumSize = new Vector2(save == null ? 600 : 500, save == null ? 64 : 84)
                };
                button.Pressed += () => {
                    if (save == null) {
                        FTT.Core.SessionData session = FTT.Core.GameManager.Instance.CurrentSession;
                        session.ActiveSaveSlot = capturedSlot;
                        FTT.Core.GameManager.Instance.CurrentSession = session;
                        ShowCharacterSelectForStory();
                    } else {
                        FTT.Core.StoryManager.Instance?.ResumeCampaign(capturedSlot, save);
                    }
                };
                row.AddChild(button);
                if (save != null) {
                    var delete = new Button {
                        Text = Tr("save_delete"),
                        CustomMinimumSize = new Vector2(90, 84)
                    };
                    delete.Pressed += () => ConfirmDeleteStorySlot(capturedSlot, save);
                    row.AddChild(delete);
                }
                layout.AddChild(row);
            }
            var back = new Button { Text = Tr("common_back"), CustomMinimumSize = new Vector2(240, 44) };
            back.Pressed += () => {
                selectPanel.QueueFree();
                ShowMenuControls();
            };
            layout.AddChild(back);
        }

        private void ConfirmDeleteStorySlot(int slotIndex, FTT.Core.StorySaveData save) {
            var confirmation = new ConfirmationDialog {
                Title = Tr("save_delete_title"),
                DialogText = string.Format(
                    Tr("save_confirm_delete"),
                    slotIndex + 1,
                    CharacterName(save.SelectedCharacterID))
            };
            confirmation.Confirmed += () => {
                if (FTT.Core.SaveManager.Instance?.DeleteStorySlot(slotIndex) == true) {
                    GetNodeOrNull<Control>("StorySlotSelect")?.QueueFree();
                    Callable.From(ShowStorySlotSelect).CallDeferred();
                }
                confirmation.QueueFree();
            };
            confirmation.Canceled += confirmation.QueueFree;
            AddChild(confirmation);
            confirmation.PopupCentered();
        }

        private string SaveSlotSummary(int slotIndex, FTT.Core.StorySaveData save) {
            string difficultyKey = save.Difficulty switch {
                FTT.Core.Difficulty.Easy => "difficulty_easy",
                FTT.Core.Difficulty.Hard => "difficulty_hard",
                _ => "difficulty_normal"
            };
            string progress = save.IsCompleted
                ? Tr("save_completed")
                : save.CurrentLevelID.GetFile().GetBaseName();
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

        private void ShowCharacterSelectForStory() {
            HideMenuControls();

            var selectPanel = new PanelContainer();
            selectPanel.Name = "StoryCharSelect";
            selectPanel.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(selectPanel);

            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            selectPanel.AddChild(center);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);
            center.AddChild(vbox);

            var titleLbl = new Label();
            titleLbl.Text = Tr("story_select_character");
            titleLbl.HorizontalAlignment = HorizontalAlignment.Center;
            titleLbl.AddThemeColorOverride("font_color", new Color(0, 0.9f, 0.9f));
            titleLbl.AddThemeFontSizeOverride("font_size", 22);
            vbox.AddChild(titleLbl);

            vbox.AddChild(new HSeparator());

            string[] characters = { "einstein", "joan", "leonardo", "lincoln", "cleopatra", "tesla", "shakespeare", "mozart", "pocahontas" };
            var grid = new GridContainer();
            grid.Columns = 3;
            grid.AddThemeConstantOverride("h_separation", 10);
            grid.AddThemeConstantOverride("v_separation", 10);
            vbox.AddChild(grid);

            for (int i = 0; i < characters.Length; i++) {
                string charID = characters[i];
                var btn = new Button();
                btn.Text = CharacterName(charID);
                btn.CustomMinimumSize = new Vector2(200, 45);
                btn.Pressed += () => StartStoryWithCharacter(charID);
                grid.AddChild(btn);
            }

            vbox.AddChild(new HSeparator());

            var backBtn = new Button();
            backBtn.Text = Tr("common_back");
            backBtn.CustomMinimumSize = new Vector2(200, 40);
            backBtn.Pressed += () => {
                selectPanel.QueueFree();
                Control slotPanel = GetNodeOrNull<Control>("StorySlotSelect");
                if (slotPanel != null) slotPanel.Visible = true;
                else ShowMenuControls();
            };
            vbox.AddChild(backBtn);
        }

        private void StartStoryWithCharacter(string characterID) {
            ShowDifficultySelect(characterID);
        }

        private void ShowDifficultySelect(string characterID) {
            Control characterPanel = GetNodeOrNull<Control>("StoryCharSelect");
            if (characterPanel != null) characterPanel.Visible = false;
            var panel = new PanelContainer { Name = "StoryDifficultySelect" };
            panel.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(panel);
            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            panel.AddChild(center);
            var layout = new VBoxContainer();
            layout.AddThemeConstantOverride("separation", 16);
            center.AddChild(layout);
            var title = new Label {
                Text = Tr("story_select_difficulty"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 26);
            layout.AddChild(title);
            AddDifficultyButton(layout, characterID, FTT.Core.Difficulty.Easy, "difficulty_easy", "difficulty_easy_description");
            AddDifficultyButton(layout, characterID, FTT.Core.Difficulty.Normal, "difficulty_normal", "difficulty_normal_description");
            AddDifficultyButton(layout, characterID, FTT.Core.Difficulty.Hard, "difficulty_hard", "difficulty_hard_description");
            var back = new Button { Text = Tr("common_back"), CustomMinimumSize = new Vector2(400, 44) };
            back.Pressed += () => {
                panel.QueueFree();
                if (characterPanel != null) characterPanel.Visible = true;
            };
            layout.AddChild(back);
        }

        private void AddDifficultyButton(
            VBoxContainer parent,
            string characterID,
            FTT.Core.Difficulty difficulty,
            string nameKey,
            string descriptionKey) {
            var button = new Button {
                Text = $"{Tr(nameKey)}\n{Tr(descriptionKey)}",
                CustomMinimumSize = new Vector2(620, 76)
            };
            button.Pressed += () => BeginNewStory(characterID, difficulty);
            parent.AddChild(button);
        }

        private void BeginNewStory(string characterID, FTT.Core.Difficulty difficulty) {
            FTT.Core.SessionData session = FTT.Core.GameManager.Instance.CurrentSession;
            session.Difficulty = difficulty;
            FTT.Core.GameManager.Instance.CurrentSession = session;
            var storyMgr = FTT.Core.StoryManager.Instance;
            if (storyMgr != null) {
                storyMgr.StartCampaign(characterID);
            } else {
                var fallbackSession = FTT.Core.GameManager.Instance.CurrentSession;
                fallbackSession.SelectedCharacterID = characterID;
                FTT.Core.GameManager.Instance.CurrentSession = fallbackSession;
                FTT.Core.GameManager.Instance.LoadScene("res://scenes/campaign/HubWorld.tscn");
            }
        }

        private string CharacterName(string characterID) {
            FTT.Characters.CharacterData data = GD.Load<FTT.Characters.CharacterData>(
                $"res://resources/Characters/{characterID}_data.tres");
            return data == null || string.IsNullOrWhiteSpace(data.DisplayNameKey)
                ? Tr("common_unknown")
                : Tr(data.DisplayNameKey);
        }

        private void HideMenuControls() {
            foreach (Node child in GetChildren()) {
                if (child is Control control) control.Visible = false;
            }
        }

        private void ShowMenuControls() {
            foreach (Node child in GetChildren()) {
                if (child is Control control && !control.IsQueuedForDeletion()) control.Visible = true;
            }
        }
    }
}
