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
            storyBtn.Disabled = true;
            storyBtn.TooltipText = "Requires level scenes (coming soon)";
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
    }
}
