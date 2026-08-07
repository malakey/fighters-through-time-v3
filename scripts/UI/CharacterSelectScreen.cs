using Godot;
using FTT.Characters;
using FTT.Environment;
using System.Collections.Generic;

namespace FTT.UI {
    public partial class CharacterSelectScreen : Control {
        private int _selectedIndex;
        private int _opponentIndex = 1;

        private readonly string[] _characterIDs = {
            "einstein", "joan", "leonardo", "lincoln", "cleopatra",
            "tesla", "shakespeare", "mozart", "pocahontas"
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
        private Label _opponentLabel;
        private CheckButton _localHumanToggle;
        private OptionButton _cpuDifficulty;
        private SpinBox _stockCount;
        private SpinBox _timeLimit;
        private CheckButton _itemsToggle;
        private CheckButton _hazardsToggle;
        private OptionButton _matchMode;
        private OptionButton _stageSelect;
        private FighterStageCatalog _stageCatalog;
        private readonly List<string> _stageIDs = new();

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
            title.Text = Tr("fighter_select_title");
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

            BuildOpponentAndRules(root);

            var buttonRow = new HBoxContainer();
            buttonRow.Alignment = BoxContainer.AlignmentMode.Center;
            buttonRow.AddThemeConstantOverride("separation", 24);
            root.AddChild(buttonRow);

            var backBtn = CreateActionButton(Tr("common_back"));
            backBtn.Pressed += OnBack;
            buttonRow.AddChild(backBtn);

            var fightBtn = CreateActionButton(Tr("fighter_start_match"));
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
            label.Text = GetCharacterName(index);
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

            _selectedNameLabel.Text = string.Format(Tr("fighter_player_selection"), 1, GetCharacterName(index));
            UpdateStatsDisplay(_characterIDs[index]);
        }

        private void BuildOpponentAndRules(VBoxContainer root) {
            var opponentRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            opponentRow.AddThemeConstantOverride("separation", 12);
            root.AddChild(opponentRow);
            var previous = CreateActionButton("<");
            previous.CustomMinimumSize = new Vector2(48, 40);
            previous.Pressed += () => CycleOpponent(-1);
            opponentRow.AddChild(previous);
            _opponentLabel = new Label {
                CustomMinimumSize = new Vector2(300, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            opponentRow.AddChild(_opponentLabel);
            var next = CreateActionButton(">");
            next.CustomMinimumSize = new Vector2(48, 40);
            next.Pressed += () => CycleOpponent(1);
            opponentRow.AddChild(next);

            var modeRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            modeRow.AddThemeConstantOverride("separation", 18);
            root.AddChild(modeRow);
            _localHumanToggle = new CheckButton { Text = Tr("fighter_local_human") };
            _localHumanToggle.Toggled += enabled => _cpuDifficulty.Disabled = enabled;
            modeRow.AddChild(_localHumanToggle);
            _cpuDifficulty = new OptionButton();
            _cpuDifficulty.AddItem(Tr("difficulty_easy"), (int)FTT.Core.CpuDifficulty.Easy);
            _cpuDifficulty.AddItem(Tr("difficulty_normal"), (int)FTT.Core.CpuDifficulty.Normal);
            _cpuDifficulty.AddItem(Tr("difficulty_hard"), (int)FTT.Core.CpuDifficulty.Hard);
            _cpuDifficulty.Select(1);
            modeRow.AddChild(_cpuDifficulty);

            var stageRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            stageRow.AddThemeConstantOverride("separation", 12);
            root.AddChild(stageRow);
            stageRow.AddChild(new Label { Text = Tr("fighter_stage") });
            _stageSelect = new OptionButton { CustomMinimumSize = new Vector2(420, 38) };
            stageRow.AddChild(_stageSelect);
            PopulateStages();

            var rulesRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            rulesRow.AddThemeConstantOverride("separation", 14);
            root.AddChild(rulesRow);
            _matchMode = new OptionButton { CustomMinimumSize = new Vector2(145, 36) };
            _matchMode.AddItem(Tr("fighter_mode_stock"), (int)FTT.Core.MatchMode.Stock);
            _matchMode.AddItem(Tr("fighter_mode_time"), (int)FTT.Core.MatchMode.TimeLimit);
            _matchMode.AddItem(Tr("fighter_mode_hybrid"), (int)FTT.Core.MatchMode.Hybrid);
            rulesRow.AddChild(_matchMode);
            rulesRow.AddChild(new Label { Text = Tr("hud_stocks") });
            _stockCount = new SpinBox { MinValue = 1, MaxValue = 5, Step = 1, Value = 3, CustomMinimumSize = new Vector2(80, 36) };
            rulesRow.AddChild(_stockCount);
            rulesRow.AddChild(new Label { Text = Tr("hud_timer") });
            _timeLimit = new SpinBox { MinValue = 60, MaxValue = 480, Step = 30, Value = 480, CustomMinimumSize = new Vector2(100, 36) };
            rulesRow.AddChild(_timeLimit);
            _itemsToggle = new CheckButton { Text = Tr("fighter_items"), ButtonPressed = true };
            rulesRow.AddChild(_itemsToggle);
            _hazardsToggle = new CheckButton { Text = Tr("fighter_hazards"), ButtonPressed = true };
            rulesRow.AddChild(_hazardsToggle);
            UpdateOpponentLabel();
        }

        private void PopulateStages() {
            _stageCatalog = FighterStageCatalog.LoadDefault();
            _stageIDs.Clear();
            _stageSelect.Clear();
            if (_stageCatalog == null) return;
            List<string> unlocked = FTT.Core.SaveManager.Instance?.GlobalData?.UnlockedStages
                ?? new List<string>(FTT.Core.GlobalSaveData.InitialStageIDs);
            foreach (FighterStageData stage in _stageCatalog.Stages) {
                if (stage == null || !stage.IsPlayable || !unlocked.Contains(stage.StageID)) continue;
                int itemID = _stageIDs.Count;
                _stageIDs.Add(stage.StageID);
                _stageSelect.AddItem(Tr(stage.DisplayNameKey), itemID);
                int itemIndex = _stageSelect.ItemCount - 1;
                _stageSelect.SetItemTooltip(
                    itemIndex,
                    $"{Tr(stage.LayoutDescriptionKey)}\n{Tr(stage.HazardNameKey)}: {Tr(stage.HazardDescriptionKey)}\n{Tr("fighter_stage_prototype")}");
            }
            _stageSelect.Disabled = _stageIDs.Count == 0;
        }

        private void CycleOpponent(int direction) {
            _opponentIndex = (_opponentIndex + direction + _characterIDs.Length) % _characterIDs.Length;
            UpdateOpponentLabel();
        }

        private void UpdateOpponentLabel() {
            if (_opponentLabel != null) {
                _opponentLabel.Text = string.Format(Tr("fighter_player_selection"), 2, GetCharacterName(_opponentIndex));
            }
        }

        private string GetCharacterName(int index) {
            CharacterData data = GD.Load<CharacterData>($"res://resources/Characters/{_characterIDs[index]}_data.tres");
            return data == null || string.IsNullOrWhiteSpace(data.DisplayNameKey)
                ? Tr("common_unknown")
                : Tr(data.DisplayNameKey);
        }

        private void UpdateStatsDisplay(string characterID) {
            var data = GD.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres");
            if (data == null) {
                _statsLabel.Text = Tr("fighter_stats_unavailable");
                return;
            }

            _statsLabel.Text = string.Format(
                Tr("fighter_stats_summary"),
                data.MaxHP, data.MaxMoveSpeed, data.BasicAttackDamage, data.BasicAttackKnockback,
                data.Weight, data.MaxBlockCharges, data.Style, data.MaxJumpForce, data.MaxJumpCount);
        }

        private void OnFight() {
            if (FTT.Core.GameManager.Instance == null) return;

            var session = FTT.Core.GameManager.Instance.CurrentSession;
            session.SelectedCharacterID = _characterIDs[_selectedIndex];
            session.OpponentCharacterID = _characterIDs[_opponentIndex];
            int selectedStageIndex = (int)_stageSelect.GetSelectedId();
            if (selectedStageIndex < 0 || selectedStageIndex >= _stageIDs.Count) return;
            session.SelectedStageID = _stageIDs[selectedStageIndex];
            session.FighterOpponentType = _localHumanToggle.ButtonPressed
                ? FTT.Core.FighterOpponentType.LocalHuman
                : FTT.Core.FighterOpponentType.Cpu;
            session.CpuDifficulty = (FTT.Core.CpuDifficulty)_cpuDifficulty.GetSelectedId();
            FTT.Core.MatchSettings settings = session.MatchSettings;
            settings.Mode = (FTT.Core.MatchMode)_matchMode.GetSelectedId();
            settings.StockCount = (int)_stockCount.Value;
            settings.TimeLimit = (float)_timeLimit.Value;
            settings.ItemsEnabled = _itemsToggle.ButtonPressed;
            settings.ItemSpawnRate = _itemsToggle.ButtonPressed ? FTT.Core.ChronalOrbFrequency.High : FTT.Core.ChronalOrbFrequency.Off;
            settings.StageHazardsEnabled = _hazardsToggle.ButtonPressed;
            settings.HazardRate = _hazardsToggle.ButtonPressed ? FTT.Core.HazardTriggerFrequency.High : FTT.Core.HazardTriggerFrequency.Off;
            session.MatchSettings = settings;
            FTT.Core.GameManager.Instance.CurrentSession = session;
            FighterStageData stage = _stageCatalog?.Find(session.SelectedStageID);
            if (stage == null || !ResourceLoader.Exists(stage.ScenePath)) return;
            FTT.Core.GameManager.Instance.LoadScene(stage.ScenePath);
        }

        private void OnBack() {
            bool holodeck = FTT.Core.GameManager.Instance?.CurrentSession.ReturnToHubAfterFighterMatch == true;
            FTT.Core.GameManager.Instance?.LoadScene(holodeck
                ? "res://scenes/campaign/HubWorld.tscn"
                : "res://scenes/menus/MainMenu.tscn");
        }
    }
}
