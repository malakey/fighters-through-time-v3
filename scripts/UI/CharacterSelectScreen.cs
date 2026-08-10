using System.Collections.Generic;
using FTT.Characters;
using FTT.Environment;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 8 B4. The Fighter Mode character/stage/rules screen, converted from
    /// a fully code-built shell to an authored, themed scene.
    ///
    /// <para>The nine character tiles used to be <see cref="PanelContainer"/>s with
    /// a <c>GuiInput</c> mouse handler — not focusable, so a controller or keyboard
    /// player could not select a fighter at all. They are now real
    /// <see cref="Button"/>s in the authored grid, chained by
    /// <see cref="FocusChainBuilder"/> and drawing the theme's focus ring. The
    /// character colour identity survives as a per-tile stylebox override on
    /// normal/hover/pressed only; <c>focus</c> is deliberately left to the theme so
    /// the ring is never painted over.</para>
    ///
    /// <para>All session writes, stage routing through the catalog, and the four
    /// frequency bands are unchanged from the pre-conversion behaviour.</para>
    /// </summary>
    public partial class CharacterSelectScreen : Control {

        private int _selectedIndex;
        private int _opponentIndex = 1;

        private readonly string[] _characterIDs = {
            "einstein", "joan", "leonardo", "lincoln", "cleopatra",
            "tesla", "shakespeare", "mozart", "pocahontas"
        };

        private Button[] _characterButtons;
        private Label _statsLabel;
        private Label _selectedNameLabel;
        private Label _opponentLabel;
        private CheckButton _localHumanToggle;
        private OptionButton _cpuDifficulty;
        private SpinBox _stockCount;
        private SpinBox _timeLimit;
        private OptionButton _itemFrequency;
        private OptionButton _hazardFrequency;
        private OptionButton _matchMode;
        private OptionButton _stageSelect;
        private TextureRect _stagePreview;
        private FighterStageCatalog _stageCatalog;
        private readonly List<string> _stageIDs = new();

        /// <summary>The tile index currently chosen for player one.</summary>
        public int SelectedIndex => _selectedIndex;

        /// <summary>The tile index currently chosen for player two.</summary>
        public int OpponentIndex => _opponentIndex;

        public override void _Ready() {
            UIPalette.ApplyTheme(this);

            const string root = "Center/Root/";
            _selectedNameLabel = GetNode<Label>(root + "SelectedName");
            _statsLabel = GetNode<Label>(root + "Stats");
            _opponentLabel = GetNode<Label>(root + "OpponentRow/OpponentLabel");
            _localHumanToggle = GetNode<CheckButton>(root + "ModeRow/LocalHumanToggle");
            _cpuDifficulty = GetNode<OptionButton>(root + "ModeRow/CpuDifficulty");
            _stageSelect = GetNode<OptionButton>(root + "StageRow/StageSelect");
            _stagePreview = GetNode<TextureRect>(root + "StageRow/StagePreview");
            _matchMode = GetNode<OptionButton>(root + "RulesRow/MatchMode");
            _stockCount = GetNode<SpinBox>(root + "RulesRow/StockCount");
            _timeLimit = GetNode<SpinBox>(root + "RulesRow/TimeLimit");
            _itemFrequency = GetNode<OptionButton>(root + "RulesRow/ItemFrequency");
            _hazardFrequency = GetNode<OptionButton>(root + "RulesRow/HazardFrequency");

            BindCharacterGrid();
            BindOpponentAndRules();

            GetNode<Button>(root + "ButtonRow/BackButton").Pressed += OnBack;
            GetNode<Button>(root + "ButtonRow/FightButton").Pressed += OnFight;

            SelectCharacter(0);
            UpdateOpponentLabel();

            BuildFocusChain();
        }

        /// <summary>
        /// Chains every interactive control in authored reading order.
        ///
        /// <para>The chain is assembled explicitly rather than through
        /// <c>FocusChainBuilder.Apply</c> because <see cref="SpinBox"/> keeps its
        /// editable <see cref="LineEdit"/> as an *internal* child: the collector
        /// walks <c>GetChild</c> and therefore cannot see it, which would leave the
        /// stock count and time limit unreachable by keyboard or controller — the
        /// exact class of gap this workstream exists to close.</para>
        ///
        /// <para>Focus starts on the roster, so the first thing a controller player
        /// touches is the choice the screen exists to make.</para>
        /// </summary>
        private void BuildFocusChain() {
            FocusChainBuilder.Chain(FocusChain);
            _characterButtons[0]?.GrabFocus();
        }

        /// <summary>The authored focus order, exposed so a test can walk it.</summary>
        public IReadOnlyList<Control> FocusChain => new List<Control>(_characterButtons) {
            GetNode<Button>("Center/Root/OpponentRow/PreviousButton"),
            GetNode<Button>("Center/Root/OpponentRow/NextButton"),
            _localHumanToggle,
            _cpuDifficulty,
            _stageSelect,
            _matchMode,
            _stockCount.GetLineEdit(),
            _timeLimit.GetLineEdit(),
            _itemFrequency,
            _hazardFrequency,
            GetNode<Button>("Center/Root/ButtonRow/BackButton"),
            GetNode<Button>("Center/Root/ButtonRow/FightButton")
        };

        private void BindCharacterGrid() {
            var grid = GetNode<GridContainer>("Center/Root/Grid");
            _characterButtons = new Button[_characterIDs.Length];
            for (int index = 0; index < _characterIDs.Length; index++) {
                int captured = index;
                var button = grid.GetNode<Button>($"CharacterButton{index}");
                button.Text = GetCharacterName(index);
                button.Icon = GetCharacterPortrait(index);
                button.Pressed += () => SelectCharacter(captured);
                _characterButtons[index] = button;
            }
        }

        /// <summary>
        /// Paints one tile. Selection is a thick cyan border over the character's
        /// colour; the theme's focus ring is layered on top by the <c>focus</c>
        /// stylebox, which is never overridden here.
        /// </summary>
        private void StyleTile(int index, bool selected) {
            // The canonical nine-colour identity table lives in CharacterFactory;
            // a duplicated copy here was audit Low "select-screen colour dedupe".
            Color color = CharacterFactory.GetCharacterColor(_characterIDs[index]);
            var style = new StyleBoxFlat {
                BgColor = color,
                BorderColor = selected ? UIPalette.Cyan : new Color(0.2f, 0.2f, 0.25f),
                ShadowSize = selected ? 8 : 0,
                ShadowColor = selected ? new Color(UIPalette.Cyan, 0.5f) : Colors.Transparent
            };
            style.SetBorderWidthAll(selected ? 5 : 2);
            style.SetCornerRadiusAll(6);
            style.SetContentMarginAll(8);

            Button button = _characterButtons[index];
            button.AddThemeStyleboxOverride("normal", style);
            button.AddThemeStyleboxOverride("hover", style);
            button.AddThemeStyleboxOverride("pressed", style);

            // Readable text on top of an arbitrary character colour.
            float luminance = 0.299f * color.R + 0.587f * color.G + 0.114f * color.B;
            Color textColor = luminance > 0.55f
                ? new Color(0.1f, 0.1f, 0.15f)
                : new Color(0.95f, 0.95f, 0.98f);
            button.AddThemeColorOverride("font_color", textColor);
            button.AddThemeColorOverride("font_hover_color", textColor);
            button.AddThemeColorOverride("font_pressed_color", textColor);
            button.AddThemeColorOverride("font_focus_color", textColor);
        }

        private void SelectCharacter(int index) {
            _selectedIndex = index;
            for (int i = 0; i < _characterButtons.Length; i++) StyleTile(i, i == index);
            _selectedNameLabel.Text = string.Format(Tr("fighter_player_selection"), 1, GetCharacterName(index));
            UpdateStatsDisplay(_characterIDs[index]);
        }

        private void BindOpponentAndRules() {
            const string root = "Center/Root/";
            GetNode<Button>(root + "OpponentRow/PreviousButton").Pressed += () => CycleOpponent(-1);
            GetNode<Button>(root + "OpponentRow/NextButton").Pressed += () => CycleOpponent(1);

            _localHumanToggle.Toggled += enabled => _cpuDifficulty.Disabled = enabled;
            _cpuDifficulty.AddItem(Tr("difficulty_easy"), (int)FTT.Core.CpuDifficulty.Easy);
            _cpuDifficulty.AddItem(Tr("difficulty_normal"), (int)FTT.Core.CpuDifficulty.Normal);
            _cpuDifficulty.AddItem(Tr("difficulty_hard"), (int)FTT.Core.CpuDifficulty.Hard);
            _cpuDifficulty.Select(1);

            _stageSelect.ItemSelected += _ => UpdateStagePreview();
            PopulateStages();
            UpdateStagePreview();

            _matchMode.AddItem(Tr("fighter_mode_stock"), (int)FTT.Core.MatchMode.Stock);
            _matchMode.AddItem(Tr("fighter_mode_time"), (int)FTT.Core.MatchMode.TimeLimit);
            _matchMode.AddItem(Tr("fighter_mode_hybrid"), (int)FTT.Core.MatchMode.Hybrid);

            // Off/Low/Medium/High, matching the deterministic spawn-interval bands
            // the simulation actually consumes rather than a binary on/off.
            FillFrequencySelect(_itemFrequency, (int)FTT.Core.ChronalOrbFrequency.High);
            FillFrequencySelect(_hazardFrequency, (int)FTT.Core.HazardTriggerFrequency.High);
        }

        private static void FillFrequencySelect(OptionButton select, int selectedID) {
            select.Clear();
            select.AddItem(TranslationServer.Translate("fighter_frequency_off"), 0);
            select.AddItem(TranslationServer.Translate("fighter_frequency_low"), 1);
            select.AddItem(TranslationServer.Translate("fighter_frequency_medium"), 2);
            select.AddItem(TranslationServer.Translate("fighter_frequency_high"), 3);
            select.Select(selectedID);
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
                // A production-contract stage is no longer a prototype. The line
                // used to be appended unconditionally, which branded every stage —
                // including the shipped Florence Workshop — as placeholder routing.
                string tooltip =
                    $"{Tr(stage.LayoutDescriptionKey)}\n{Tr(stage.HazardNameKey)}: {Tr(stage.HazardDescriptionKey)}";
                if (!stage.ProductionReady) tooltip += $"\n{Tr("fighter_stage_prototype")}";
                _stageSelect.SetItemTooltip(itemIndex, tooltip);
            }
            _stageSelect.Disabled = _stageIDs.Count == 0;
        }

        /// <summary>
        /// Shows the selected stage's placeholder preview plate. Production art
        /// replaces the SVG behind <see cref="FighterStageData.PreviewTexturePath"/>
        /// with no change here.
        /// </summary>
        private void UpdateStagePreview() {
            if (_stagePreview == null) return;
            _stagePreview.Texture = null;
            _stagePreview.Visible = false;

            int index = (int)_stageSelect.GetSelectedId();
            if (index < 0 || index >= _stageIDs.Count) return;

            FighterStageData stage = _stageCatalog?.Find(_stageIDs[index]);
            if (stage == null || string.IsNullOrWhiteSpace(stage.PreviewTexturePath)) return;
            if (!ResourceLoader.Exists(stage.PreviewTexturePath)) return;

            // Preview plates are streamable art, not authored tuning data, so they
            // deliberately do NOT go through AuthoredResources' pinning cache.
            var texture = ResourceLoader.Load<Texture2D>(stage.PreviewTexturePath);
            if (texture == null) return;
            _stagePreview.Texture = texture;
            _stagePreview.TooltipText = Tr(stage.DisplayNameKey);
            _stagePreview.Visible = true;
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

        private Texture2D GetCharacterPortrait(int index) {
            CharacterData data = FTT.Core.AuthoredResources.Load<CharacterData>($"res://resources/Characters/{_characterIDs[index]}_data.tres");
            return data?.CharacterPortrait;
        }

        private string GetCharacterName(int index) {
            CharacterData data = FTT.Core.AuthoredResources.Load<CharacterData>($"res://resources/Characters/{_characterIDs[index]}_data.tres");
            return data == null || string.IsNullOrWhiteSpace(data.DisplayNameKey)
                ? Tr("common_unknown")
                : Tr(data.DisplayNameKey);
        }

        private void UpdateStatsDisplay(string characterID) {
            var data = FTT.Core.AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres");
            if (data == null) {
                _statsLabel.Text = Tr("fighter_stats_unavailable");
                return;
            }

            _statsLabel.Text = string.Format(
                Tr("fighter_stats_summary"),
                data.MaxHP, data.MaxMoveSpeed, data.BasicAttackDamage, data.BasicAttackKnockback,
                data.Weight, data.MaxBlockCharges, data.Style, data.MaxJumpForce, data.MaxJumpCount);
        }

        /// <summary>
        /// Uniform cancel: from this screen, back means leave it, exactly as the
        /// Back button does. The screen has no sub-states to unwind.
        /// </summary>
        public override void _UnhandledInput(InputEvent @event) {
            if (@event == null || !@event.IsActionPressed("ui_cancel")) return;
            GetViewport()?.SetInputAsHandled();
            OnBack();
        }

        private void OnFight() {
            FighterStageData stage = ApplySelectionToSession();
            if (stage == null || !ResourceLoader.Exists(stage.ScenePath)) return;
            FTT.Core.GameManager.Instance.LoadScene(stage.ScenePath);
        }

        /// <summary>
        /// Writes every control's value into the session and returns the stage the
        /// match should route to, or null when the selection cannot resolve.
        ///
        /// <para>Split out of <c>OnFight</c> so the session round-trip is testable
        /// without triggering a real scene change: a test that pressed Fight would
        /// tear the GdUnit runner's own scene out from under it two seconds
        /// later.</para>
        /// </summary>
        public FighterStageData ApplySelectionToSession() {
            if (FTT.Core.GameManager.Instance == null) return null;

            var session = FTT.Core.GameManager.Instance.CurrentSession;
            session.SelectedCharacterID = _characterIDs[_selectedIndex];
            session.OpponentCharacterID = _characterIDs[_opponentIndex];
            int selectedStageIndex = (int)_stageSelect.GetSelectedId();
            if (selectedStageIndex < 0 || selectedStageIndex >= _stageIDs.Count) return null;
            session.SelectedStageID = _stageIDs[selectedStageIndex];
            session.FighterOpponentType = _localHumanToggle.ButtonPressed
                ? FTT.Core.FighterOpponentType.LocalHuman
                : FTT.Core.FighterOpponentType.Cpu;
            session.CpuDifficulty = (FTT.Core.CpuDifficulty)_cpuDifficulty.GetSelectedId();
            FTT.Core.MatchSettings settings = session.MatchSettings;
            settings.Mode = (FTT.Core.MatchMode)_matchMode.GetSelectedId();
            settings.StockCount = (int)_stockCount.Value;
            settings.TimeLimit = (float)_timeLimit.Value;
            var itemRate = (FTT.Core.ChronalOrbFrequency)_itemFrequency.GetSelectedId();
            var hazardRate = (FTT.Core.HazardTriggerFrequency)_hazardFrequency.GetSelectedId();
            settings.ItemSpawnRate = itemRate;
            settings.ItemsEnabled = itemRate != FTT.Core.ChronalOrbFrequency.Off;
            settings.HazardRate = hazardRate;
            settings.StageHazardsEnabled = hazardRate != FTT.Core.HazardTriggerFrequency.Off;
            session.MatchSettings = settings;
            FTT.Core.GameManager.Instance.CurrentSession = session;
            return _stageCatalog?.Find(session.SelectedStageID);
        }

        private void OnBack() {
            bool holodeck = FTT.Core.GameManager.Instance?.CurrentSession.ReturnToHubAfterFighterMatch == true;
            FTT.Core.GameManager.Instance?.LoadScene(holodeck
                ? "res://scenes/campaign/HubWorld.tscn"
                : "res://scenes/menus/MainMenu.tscn");
        }
    }
}
