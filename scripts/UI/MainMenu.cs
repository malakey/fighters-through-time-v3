using Godot;

namespace FTT.UI {
    public partial class MainMenu : Control {

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
            title.Text = "FIGHTERS THROUGH TIME";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeColorOverride("font_color", new Color(0, 0.9f, 0.9f));
            title.AddThemeFontSizeOverride("font_size", 28);
            vbox.AddChild(title);

            var subtitle = new Label();
            subtitle.Text = "Godot V3 Build";
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            subtitle.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
            vbox.AddChild(subtitle);

            vbox.AddChild(new HSeparator());

            var quickPlayBtn = CreateButton("Quick Play (Einstein)");
            quickPlayBtn.Pressed += () => {
                if (FTT.Core.GameManager.Instance == null) return;
                var session = FTT.Core.GameManager.Instance.CurrentSession;
                session.SelectedCharacterID = "einstein";
                FTT.Core.GameManager.Instance.CurrentSession = session;
                FTT.Core.GameManager.Instance.LoadScene("res://scenes/arenas/TestArena.tscn");
            };
            vbox.AddChild(quickPlayBtn);

            var storyBtn = CreateButton("Story Mode");
            storyBtn.Pressed += OnStoryModePressed;
            vbox.AddChild(storyBtn);

            var fighterBtn = CreateButton("Fighter Mode");
            fighterBtn.Pressed += () => FTT.Core.GameManager.Instance?.LoadScene("res://scenes/menus/CharacterSelect.tscn");
            vbox.AddChild(fighterBtn);

            var settingsBtn = CreateButton("Settings");
            settingsBtn.Disabled = true;
            vbox.AddChild(settingsBtn);

            vbox.AddChild(new HSeparator());

            var quitBtn = CreateButton("Quit");
            quitBtn.Pressed += () => GetTree().Quit();
            vbox.AddChild(quitBtn);
        }

        private Button CreateButton(string text) {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(350, 50);
            return btn;
        }

        private void OnStoryModePressed() {
            if (FTT.Core.GameManager.Instance == null) return;
            ShowCharacterSelectForStory();
        }

        private void ShowCharacterSelectForStory() {
            foreach (var child in GetChildren()) {
                if (child is Control ctrl) ctrl.Visible = false;
            }

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
            titleLbl.Text = "STORY MODE - SELECT CHARACTER";
            titleLbl.HorizontalAlignment = HorizontalAlignment.Center;
            titleLbl.AddThemeColorOverride("font_color", new Color(0, 0.9f, 0.9f));
            titleLbl.AddThemeFontSizeOverride("font_size", 22);
            vbox.AddChild(titleLbl);

            vbox.AddChild(new HSeparator());

            string[] characters = { "einstein", "joan", "leonardo", "lincoln", "cleopatra", "tesla", "shakespeare", "mozart", "pocahontas" };
            string[] displayNames = { "Albert Einstein", "Joan of Arc", "Leonardo da Vinci", "Abraham Lincoln", "Cleopatra", "Nikola Tesla", "William Shakespeare", "Wolfgang Mozart", "Pocahontas" };

            var grid = new GridContainer();
            grid.Columns = 3;
            grid.AddThemeConstantOverride("h_separation", 10);
            grid.AddThemeConstantOverride("v_separation", 10);
            vbox.AddChild(grid);

            for (int i = 0; i < characters.Length; i++) {
                string charID = characters[i];
                var btn = new Button();
                btn.Text = displayNames[i];
                btn.CustomMinimumSize = new Vector2(200, 45);
                btn.Pressed += () => StartStoryWithCharacter(charID);
                grid.AddChild(btn);
            }

            vbox.AddChild(new HSeparator());

            var backBtn = new Button();
            backBtn.Text = "Back";
            backBtn.CustomMinimumSize = new Vector2(200, 40);
            backBtn.Pressed += () => {
                selectPanel.QueueFree();
                foreach (var child in GetChildren()) {
                    if (child is Control ctrl) ctrl.Visible = true;
                }
            };
            vbox.AddChild(backBtn);
        }

        private void StartStoryWithCharacter(string characterID) {
            var storyMgr = FTT.Core.StoryManager.Instance;
            if (storyMgr != null) {
                storyMgr.StartCampaign(characterID);
            } else {
                var session = FTT.Core.GameManager.Instance.CurrentSession;
                session.SelectedCharacterID = characterID;
                FTT.Core.GameManager.Instance.CurrentSession = session;
                FTT.Core.GameManager.Instance.LoadScene("res://scenes/campaign/HubWorld.tscn");
            }
        }
    }
}
