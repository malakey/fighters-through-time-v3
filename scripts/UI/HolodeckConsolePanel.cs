using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// The Holodeck Arena Console's compact in-hub configuration panel (design
    /// Section 10.3 "In-Hub Configuration Surface": "the Holodeck console opens
    /// a compact configuration panel *in the hub* — CPU difficulty, CPU
    /// character, stage, and rules — then launches straight into the match").
    /// The Holodeck is the interim training mode; the previous behaviour —
    /// routing through the full three-screen Fighter select — is exactly what
    /// the design line forbids.
    ///
    /// The player's own character is not configurable here: a campaign save
    /// locks its character, and the hub restores it before this panel can open.
    /// Launch writes the session (opponent type Cpu, return-to-hub flag) and
    /// routes straight to the stage scene, skipping CharacterSelect entirely.
    /// </summary>
    public partial class HolodeckConsolePanel : Control {

        /// <summary>Raised when the panel closes without launching.</summary>
        public event Action Closed;

        /// <summary>
        /// The CPU opponent list, in manifest order. Package 11 A6b: read from
        /// <see cref="FTT.Core.CharacterRoster"/> rather than a literal
        /// nine-element array — the Holodeck builds its OptionButton from the
        /// roster at runtime, so a new manifest row is offered as a sparring
        /// partner with no code change.
        /// </summary>
        private readonly string[] RosterIDs = FTT.Core.CharacterRoster.ToArray();

        private readonly List<string> _stageIDs = new();
        private FighterStageCatalog _stageCatalog;

        private OptionButton _cpuDifficulty;
        private OptionButton _cpuCharacter;
        private OptionButton _stageSelect;
        private OptionButton _matchMode;
        private SpinBox _stockCount;
        private SpinBox _timeLimit;
        private OptionButton _itemFrequency;
        private OptionButton _hazardFrequency;

        // Test seams.
        internal OptionButton CpuDifficultySelect => _cpuDifficulty;
        internal OptionButton CpuCharacterSelect => _cpuCharacter;
        internal OptionButton StageSelect => _stageSelect;
        internal IReadOnlyList<string> StageIDs => _stageIDs;

        public override void _Ready() {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            UIPalette.ApplyTheme(this);

            var shade = new ColorRect {
                Name = "Shade",
                Color = UIPalette.Shade,
                MouseFilter = MouseFilterEnum.Stop
            };
            shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(shade);

            var panel = new PanelContainer { Name = "Panel", CustomMinimumSize = new Vector2(640f, 0f) };
            panel.SetAnchorsPreset(LayoutPreset.Center);
            AddChild(panel);

            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            panel.AddChild(layout);

            var title = new Label {
                Name = "Title",
                Text = Tr("holodeck_console_title"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.ThemeTypeVariation = UIPalette.HeadingLabelVariation;
            title.AddThemeColorOverride("font_color", UIPalette.TemporalViolet);
            layout.AddChild(title);

            GameManager.Instance?.EnsureMatchSettingsLoaded();
            MatchSettings settings = GameManager.Instance?.CurrentSession.MatchSettings
                ?? MatchSettings.GetDefault();
            SessionData session = GameManager.Instance?.CurrentSession ?? default;

            _cpuDifficulty = AddOptionRow(layout, "CpuDifficulty", "fighter_cpu_difficulty");
            _cpuDifficulty.AddItem(Tr("difficulty_easy"), (int)CpuDifficulty.Easy);
            _cpuDifficulty.AddItem(Tr("difficulty_normal"), (int)CpuDifficulty.Normal);
            _cpuDifficulty.AddItem(Tr("difficulty_hard"), (int)CpuDifficulty.Hard);
            _cpuDifficulty.Select((int)session.CpuDifficulty);

            _cpuCharacter = AddOptionRow(layout, "CpuCharacter", "holodeck_cpu_character");
            PopulateRoster(session.OpponentCharacterID);

            _stageSelect = AddOptionRow(layout, "StageSelect", "fighter_stage");
            PopulateStages(session.SelectedStageID);

            _matchMode = AddOptionRow(layout, "MatchMode", "holodeck_rules");
            _matchMode.AddItem(Tr("fighter_mode_stock"), (int)MatchMode.Stock);
            _matchMode.AddItem(Tr("fighter_mode_time"), (int)MatchMode.TimeLimit);
            _matchMode.AddItem(Tr("fighter_mode_hybrid"), (int)MatchMode.Hybrid);
            _matchMode.Select((int)settings.Mode);

            var rulesRow = new HBoxContainer { Name = "RulesRow" };
            rulesRow.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            layout.AddChild(rulesRow);

            rulesRow.AddChild(MakeRowLabel("StocksLabel", "hud_stocks"));
            _stockCount = new SpinBox {
                Name = "StockCount",
                MinValue = 1, MaxValue = 5, Value = settings.StockCount,
                CustomMinimumSize = new Vector2(80f, 36f)
            };
            rulesRow.AddChild(_stockCount);

            rulesRow.AddChild(MakeRowLabel("TimerLabel", "fighter_mode_time"));
            _timeLimit = new SpinBox {
                Name = "TimeLimit",
                MinValue = 60, MaxValue = 480, Step = 30, Value = settings.TimeLimit,
                CustomMinimumSize = new Vector2(110f, 36f)
            };
            rulesRow.AddChild(_timeLimit);

            _itemFrequency = AddOptionRow(layout, "ItemFrequency", "fighter_items");
            FillFrequencySelect(_itemFrequency, (int)settings.ItemSpawnRate);
            _hazardFrequency = AddOptionRow(layout, "HazardFrequency", "fighter_hazards");
            FillFrequencySelect(_hazardFrequency, (int)settings.HazardRate);

            var launch = new Button {
                Name = "LaunchButton",
                Text = Tr("holodeck_launch"),
                CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, UIPalette.ButtonMinHeight)
            };
            launch.Pressed += OnLaunchPressed;
            layout.AddChild(launch);

            var back = new Button {
                Name = "BackButton",
                Text = Tr("common_back"),
                CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, UIPalette.ButtonMinHeight)
            };
            back.Pressed += () => Closed?.Invoke();
            layout.AddChild(back);

            FocusChainBuilder.Apply(layout);
            launch.GrabFocus();
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (@event == null || !@event.IsActionPressed("ui_cancel")) return;
            GetViewport()?.SetInputAsHandled();
            Closed?.Invoke();
        }

        private static Label MakeRowLabel(string name, string textKey) => new() {
            Name = name,
            Text = TranslationServer.Translate(textKey),
            VerticalAlignment = VerticalAlignment.Center
        };

        private static OptionButton AddOptionRow(VBoxContainer layout, string name, string labelKey) {
            var row = new HBoxContainer { Name = name + "Row" };
            row.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            layout.AddChild(row);
            var label = MakeRowLabel(name + "Label", labelKey);
            label.CustomMinimumSize = new Vector2(200f, 0f);
            row.AddChild(label);
            var select = new OptionButton {
                Name = name,
                CustomMinimumSize = new Vector2(320f, 36f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            row.AddChild(select);
            return select;
        }

        private static void FillFrequencySelect(OptionButton select, int selectedID) {
            select.AddItem(TranslationServer.Translate("fighter_frequency_off"), 0);
            select.AddItem(TranslationServer.Translate("fighter_frequency_low"), 1);
            select.AddItem(TranslationServer.Translate("fighter_frequency_medium"), 2);
            select.AddItem(TranslationServer.Translate("fighter_frequency_high"), 3);
            select.Select(Mathf.Clamp(selectedID, 0, 3));
        }

        private void PopulateRoster(string preferredID) {
            for (int index = 0; index < RosterIDs.Length; index++) {
                var data = AuthoredResources.Load<CharacterData>(
                    $"res://resources/Characters/{RosterIDs[index]}_data.tres");
                string name = data == null || string.IsNullOrWhiteSpace(data.DisplayNameKey)
                    ? Tr("common_unknown")
                    : Tr(data.DisplayNameKey);
                _cpuCharacter.AddItem(name, index);
            }
            int preferred = Array.IndexOf(RosterIDs, preferredID);
            _cpuCharacter.Select(preferred >= 0 ? preferred : 0);
        }

        private void PopulateStages(string preferredID) {
            _stageCatalog = FighterStageCatalog.LoadDefault();
            if (_stageCatalog == null) { _stageSelect.Disabled = true; return; }
            List<string> unlocked = SaveManager.Instance?.GlobalData?.UnlockedStages
                ?? new List<string>(GlobalSaveData.InitialStageIDs);
            foreach (FighterStageData stage in _stageCatalog.Stages) {
                if (stage == null || !stage.IsPlayable || !unlocked.Contains(stage.StageID)) continue;
                int itemID = _stageIDs.Count;
                _stageIDs.Add(stage.StageID);
                // Same Open/Sealed badge the Fighter select shows: the Holodeck
                // routes straight into a match, so it must not hide whether the
                // floor has a pit in it.
                _stageSelect.AddItem(
                    $"{Tr(stage.DisplayNameKey)} - {Tr(FighterStageLayoutBadge.LabelKey(stage))}", itemID);
                _stageSelect.SetItemTooltip(
                    _stageSelect.ItemCount - 1, Tr(FighterStageLayoutBadge.TooltipKey(stage)));
            }
            _stageSelect.Disabled = _stageIDs.Count == 0;
            int preferred = _stageIDs.IndexOf(preferredID);
            if (preferred >= 0) _stageSelect.Select(preferred);
        }

        private void OnLaunchPressed() {
            FighterStageData stage = ApplyToSession();
            if (stage == null || !ResourceLoader.Exists(stage.ScenePath)) return;
            GameManager.Instance.LoadScene(stage.ScenePath);
        }

        /// <summary>
        /// Writes the console's choices into the session and returns the stage
        /// the practice bout routes to, or null when it cannot resolve. Split
        /// from <see cref="OnLaunchPressed"/> so tests can assert the session
        /// round-trip without a real scene change.
        /// </summary>
        public FighterStageData ApplyToSession() {
            if (GameManager.Instance == null) return null;
            int stageIndex = (int)_stageSelect.GetSelectedId();
            if (stageIndex < 0 || stageIndex >= _stageIDs.Count) return null;

            SessionData session = GameManager.Instance.CurrentSession;
            session.FighterOpponentType = FighterOpponentType.Cpu;
            session.ReturnToHubAfterFighterMatch = true;
            session.CpuDifficulty = (CpuDifficulty)_cpuDifficulty.GetSelectedId();
            int opponentIndex = Mathf.Clamp((int)_cpuCharacter.GetSelectedId(), 0, RosterIDs.Length - 1);
            session.OpponentCharacterID = RosterIDs[opponentIndex];
            session.SelectedStageID = _stageIDs[stageIndex];

            MatchSettings settings = session.MatchSettings;
            settings.Mode = (MatchMode)_matchMode.GetSelectedId();
            settings.StockCount = (int)_stockCount.Value;
            settings.TimeLimit = (float)_timeLimit.Value;
            var itemRate = (ChronalOrbFrequency)_itemFrequency.GetSelectedId();
            var hazardRate = (HazardTriggerFrequency)_hazardFrequency.GetSelectedId();
            settings.ItemSpawnRate = itemRate;
            settings.ItemsEnabled = itemRate != ChronalOrbFrequency.Off;
            settings.HazardRate = hazardRate;
            settings.StageHazardsEnabled = hazardRate != HazardTriggerFrequency.Off;
            session.MatchSettings = settings;

            GameManager.Instance.CurrentSession = session;
            GameManager.Instance.PersistMatchSettings();
            return _stageCatalog?.Find(session.SelectedStageID);
        }
    }
}
