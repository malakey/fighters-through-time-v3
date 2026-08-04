using Godot;
using FTT.Characters;

namespace FTT.UI {
    public partial class CharacterSelectScreen : Control {
        private int _selectedIndex;

        private readonly string[] _characterIDs = {
            "einstein", "joan", "leonardo", "lincoln", "cleopatra",
            "tesla", "shakespeare", "mozart", "pocahontas"
        };

        private readonly string[] _characterNames = {
            "Einstein", "Joan of Arc", "Da Vinci", "Lincoln", "Cleopatra",
            "Tesla", "Shakespeare", "Mozart", "Pocahontas"
        };

        private readonly Color[] _characterColors = {
            new(0.2f, 0.5f, 0.9f),
            new(0.85f, 0.75f, 0.2f),
            new(0.3f, 0.7f, 0.3f),
            new(0.15f, 0.15f, 0.35f),
            new(0.6f, 0.2f, 0.8f),
            new(0.1f, 0.8f, 0.9f),
            new(0.7f, 0.15f, 0.2f),
            new(0.9f, 0.85f, 0.8f),
            new(0.55f, 0.35f, 0.2f),
        };

        private PanelContainer[] _characterPanels;
        private Label _statsLabel;
        private Label _selectedNameLabel;

        public override void _Ready() {
            var bg = new ColorRect();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0.06f, 0.06f, 0.12f, 1);
            AddChild(bg);

            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(center);

            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 16);
            center.AddChild(root);

            var title = new Label();
            title.Text = "SELECT YOUR FIGHTER";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeColorOverride("font_color", new Color(0, 0.9f, 0.9f));
            title.AddThemeFontSizeOverride("font_size", 32);
            root.AddChild(title);

            _grid = new GridContainer();
            _grid.Columns = 3;
            _grid.AddThemeConstantOverride("h_separation", 12);
            _grid.AddThemeConstantOverride("v_separation", 12);
            root.AddChild(_grid);

            _characterPanels = new PanelContainer[_characterIDs.Length];
            for (int i = 0; i < _characterIDs.Length; i++) {
                int idx = i;
                var panel = CreateCharacterPanel(i);
                panel.GuiInput += (InputEvent @event) => {
                    if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) {
                        SelectCharacter(idx);
                    }
                };
                _grid.AddChild(panel);
                _characterPanels[i] = panel;
            }

            _selectedNameLabel = new Label();
            _selectedNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _selectedNameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.95f));
            _selectedNameLabel.AddThemeFontSizeOverride("font_size", 22);
            root.AddChild(_selectedNameLabel);

            _statsLabel = new Label();
            _statsLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _statsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _statsLabel.CustomMinimumSize = new Vector2(640, 0);
            _statsLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.85f));
            root.AddChild(_statsLabel);

            var buttonRow = new HBoxContainer();
            buttonRow.Alignment = BoxContainer.AlignmentMode.Center;
            buttonRow.AddThemeConstantOverride("separation", 24);
            root.AddChild(buttonRow);

            var backBtn = CreateActionButton("Back");
            backBtn.Pressed += OnBack;
            buttonRow.AddChild(backBtn);

            var fightBtn = CreateActionButton("FIGHT!");
            fightBtn.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.2f));
            fightBtn.Pressed += OnFight;
            buttonRow.AddChild(fightBtn);

            SelectCharacter(0);
        }

        private GridContainer _grid;

        private PanelContainer CreateCharacterPanel(int index) {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(180, 120);
            panel.MouseFilter = MouseFilterEnum.Stop;

            var style = new StyleBoxFlat();
            style.BgColor = _characterColors[index];
            style.BorderWidthLeft = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthRight = 2;
            style.BorderWidthBottom = 2;
            style.BorderColor = new Color(0.2f, 0.2f, 0.25f);
            style.CornerRadiusTopLeft = 6;
            style.CornerRadiusTopRight = 6;
            style.CornerRadiusBottomLeft = 6;
            style.CornerRadiusBottomRight = 6;
            panel.AddThemeStyleboxOverride("panel", style);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_bottom", 8);
            panel.AddChild(margin);

            var label = new Label();
            label.Text = _characterNames[index];
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(160, 100);

            var c = _characterColors[index];
            float luminance = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
            var textColor = luminance > 0.55f
                ? new Color(0.1f, 0.1f, 0.15f)
                : new Color(0.95f, 0.95f, 0.98f);
            label.AddThemeColorOverride("font_color", textColor);
            label.AddThemeFontSizeOverride("font_size", 16);
            margin.AddChild(label);

            return panel;
        }

        private static Button CreateActionButton(string text) {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(160, 48);
            return btn;
        }

        private void SelectCharacter(int index) {
            _selectedIndex = index;

            for (int i = 0; i < _characterPanels.Length; i++) {
                var style = _characterPanels[i].GetThemeStylebox("panel") as StyleBoxFlat;
                if (style == null) continue;

                bool selected = i == index;
                style.BorderWidthLeft = selected ? 5 : 2;
                style.BorderWidthTop = selected ? 5 : 2;
                style.BorderWidthRight = selected ? 5 : 2;
                style.BorderWidthBottom = selected ? 5 : 2;
                style.BorderColor = selected
                    ? new Color(0, 0.95f, 0.95f)
                    : new Color(0.2f, 0.2f, 0.25f);
                style.ShadowSize = selected ? 8 : 0;
                style.ShadowColor = selected
                    ? new Color(0, 0.9f, 0.9f, 0.5f)
                    : Colors.Transparent;
            }

            _selectedNameLabel.Text = _characterNames[index];
            UpdateStatsDisplay(_characterIDs[index]);
        }

        private void UpdateStatsDisplay(string characterID) {
            var data = GD.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres");
            if (data == null) {
                _statsLabel.Text = "Stats unavailable.";
                return;
            }

            _statsLabel.Text =
                $"Max HP: {data.MaxHP}    Speed: {data.MaxMoveSpeed:0.#}    Attack Damage: {data.BasicAttackDamage:0.#}\n" +
                $"Knockback: {data.BasicAttackKnockback:0.#}    Weight: {data.Weight:0.#}    Block Charges: {data.MaxBlockCharges}\n" +
                $"Style: {data.Style}    Jump Force: {data.MaxJumpForce:0.#}    Jumps: {data.MaxJumpCount}";
        }

        private void OnFight() {
            if (FTT.Core.GameManager.Instance == null) return;

            var session = FTT.Core.GameManager.Instance.CurrentSession;
            session.SelectedCharacterID = _characterIDs[_selectedIndex];
            if (string.IsNullOrEmpty(session.OpponentCharacterID)) session.OpponentCharacterID = "joan";
            FTT.Core.GameManager.Instance.CurrentSession = session;
            FTT.Core.GameManager.Instance.LoadScene("res://scenes/arenas/TestArena.tscn");
        }

        private void OnBack() {
            FTT.Core.GameManager.Instance?.LoadScene("res://scenes/menus/MainMenu.tscn");
        }
    }
}
