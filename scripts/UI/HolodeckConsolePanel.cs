using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// The Holodeck Arena Console's compact configuration panel (design Section
    /// 10.3 "In-Hub Configuration Surface": "the Holodeck console opens a compact
    /// configuration panel *in the hub* — CPU difficulty, CPU character, stage,
    /// and rules — then launches straight into the match"). The Holodeck is the
    /// interim training mode; routing it through the full three-screen Fighter
    /// select is exactly what the design line forbids.
    ///
    /// <para><b>Two hosts (Package 12 W5, H05 per adopted D4(a)).</b> In the hub
    /// it is the Holodeck console (<see cref="FrontEnd"/> false): the player's
    /// own character is the campaign's locked one, the match origin is
    /// <see cref="FighterMatchOrigin.Holodeck"/>, and a Calibration Drills entry
    /// sits beside Launch. The same panel is the front-end <b>Versus CPU</b>
    /// configuration screen (<see cref="FrontEnd"/> true), opened by the P1-only
    /// character select: no drills entry, origin
    /// <see cref="FighterMatchOrigin.VersusCpu"/>, and no Story save involved.</para>
    ///
    /// <para>Rules: the retired hazard frequency selector is a single On/Off
    /// toggle, and Items carries the M24 "Meter pickups" sub-toggle. Random CPU
    /// character and Random stage entries (G15a) resolve from the per-match seed.</para>
    /// </summary>
    public partial class HolodeckConsolePanel : Control {

        /// <summary>OptionButton id of the Random entry in the CPU character and stage lists.</summary>
        public const int RandomItemID = 1000;

        /// <summary>Raised when the panel closes without launching.</summary>
        public event Action Closed;

        /// <summary>
        /// True when the panel is the front-end Versus CPU configuration screen
        /// rather than the hub console. Set before the panel enters the tree.
        /// </summary>
        public bool FrontEnd { get; set; }

        /// <summary>
        /// Test seam: a fixed per-match seed instead of a freshly rolled one, so a
        /// Random entry's resolution is reproducible.
        /// </summary>
        internal int? SeedOverride { get; set; }

        /// <summary>
        /// The CPU opponent list, in manifest order. Package 11 A6b: read from
        /// <see cref="FTT.Core.CharacterRoster"/> rather than a literal
        /// nine-element array — the console builds its OptionButton from the
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
        private CheckButton _meterPickups;
        private CheckButton _stageHazards;

        // Test seams.
        internal OptionButton CpuDifficultySelect => _cpuDifficulty;
        internal OptionButton CpuCharacterSelect => _cpuCharacter;
        internal OptionButton StageSelect => _stageSelect;
        internal CheckButton StageHazardsToggle => _stageHazards;
        internal CheckButton MeterPickupsToggle => _meterPickups;
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
                Text = Tr(FrontEnd ? "fighter_versus_cpu_title" : "holodeck_console_title"),
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
            CpuDifficultyLabels.Fill(_cpuDifficulty, session.CpuDifficulty);

            _cpuCharacter = AddOptionRow(layout, "CpuCharacter", "holodeck_cpu_character");
            PopulateRoster(session.OpponentCharacterID);

            _stageSelect = AddOptionRow(layout, "StageSelect", "fighter_stage");
            PopulateStages(session.SelectedStageID);

            _matchMode = AddOptionRow(layout, "MatchMode", "holodeck_rules");
            // F21: two modes. The retired Stock + Time combination is gone from
            // both selectors; a legacy saved Hybrid has already normalized to
            // timed Stock by the time these settings are read.
            _matchMode.AddItem(Tr("fighter_mode_stock"), (int)MatchMode.Stock);
            _matchMode.AddItem(Tr("fighter_mode_time"), (int)MatchMode.TimeLimit);
            _matchMode.Select(SelectableModeIndex(settings.Mode));

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

            var togglesRow = new HBoxContainer { Name = "TogglesRow" };
            togglesRow.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            layout.AddChild(togglesRow);
            _meterPickups = new CheckButton {
                Name = "MeterPickups",
                Text = Tr("fighter_meter_pickups"),
                ButtonPressed = settings.MeterPickupsEnabled
            };
            togglesRow.AddChild(_meterPickups);
            _stageHazards = new CheckButton {
                Name = "StageHazards",
                Text = Tr("fighter_hazards"),
                ButtonPressed = settings.StageHazardsEnabled
            };
            togglesRow.AddChild(_stageHazards);

            var launch = new Button {
                Name = "LaunchButton",
                Text = Tr("holodeck_launch"),
                CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, UIPalette.ButtonMinHeight)
            };
            launch.Pressed += OnLaunchPressed;
            layout.AddChild(launch);

            // Package 11 A11 (F18): the hub console keeps the CPU practice bout and
            // gains a second entry alongside it. Same drill list and content as the
            // standalone Main Menu route, run with the campaign character's
            // *normalized* Fighter kit; Exit Calibration returns to this hub. The
            // front-end Versus CPU screen has no drills entry — the main menu owns
            // the standalone route.
            if (!FrontEnd) {
                var drills = new Button {
                    Name = "CalibrationDrillsButton",
                    Text = Tr("holodeck_calibration_drills"),
                    CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, UIPalette.ButtonMinHeight)
                };
                drills.Pressed += OnCalibrationDrillsPressed;
                layout.AddChild(drills);
            }

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
            // G15a: a Random opponent, resolved from the per-match seed at launch.
            _cpuCharacter.AddItem(Tr("fighter_random_character"), RandomItemID);
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
            if (_stageIDs.Count == 0) return;
            // G15a: a Random stage, resolved from the per-match seed at launch and
            // revealed on the loading screen.
            _stageSelect.AddItem(Tr("fighter_random_stage"), RandomItemID);
            int preferred = _stageIDs.IndexOf(preferredID);
            if (preferred >= 0) _stageSelect.Select(preferred);
        }

        private void OnLaunchPressed() {
            FighterStageData stage = ApplyToSession();
            if (stage == null || !ResourceLoader.Exists(stage.ScenePath)) return;
            GameManager.Instance.LoadScene(stage.ScenePath);
        }

        private void OnCalibrationDrillsPressed() {
            if (GameManager.Instance == null) return;
            GameManager.Instance.LoadScene(ApplyCalibrationRouteToSession());
        }

        /// <summary>
        /// Arms the hub's calibration route and returns the scene to open. The hub
        /// route skips the character picker — a campaign save locks its character —
        /// and records the hub as the Exit Calibration destination. Nothing else in
        /// the session moves: the active save slot, difficulty, campaign level and
        /// the player's saved match settings are all left exactly as they are, so
        /// the attempt and its resources survive the detour and no exit fee applies.
        /// Split from the button handler so the session contract is testable without
        /// a scene change.
        /// </summary>
        public static string ApplyCalibrationRouteToSession() {
            SessionData session = GameManager.Instance.CurrentSession;
            session.CalibrationReturnScenePath = FTT.Combat.CalibrationRoute.HubScenePath;
            session.CalibrationDrillIndex = 0;
            GameManager.Instance.CurrentSession = session;
            return FTT.Combat.CalibrationRoute.DrillListScenePath;
        }

        /// <summary>
        /// Writes the panel's choices into the session and returns the stage the
        /// bout routes to, or null when it cannot resolve. Split from
        /// <see cref="OnLaunchPressed"/> so tests can assert the session round-trip
        /// without a real scene change.
        /// </summary>
        public FighterStageData ApplyToSession() {
            if (GameManager.Instance == null) return null;
            int stageItem = (int)_stageSelect.GetSelectedId();
            if (_stageIDs.Count == 0) return null;
            if (stageItem != RandomItemID && (stageItem < 0 || stageItem >= _stageIDs.Count)) return null;

            SessionData session = GameManager.Instance.CurrentSession;
            // G15a: the per-match seed. The Versus CPU select may already have
            // rolled it to resolve P1's Random tile; otherwise roll it here.
            if (!session.HasPendingMatchSeed) {
                session.PendingMatchSeed = SeedOverride ?? unchecked((int)GD.Randi());
                session.HasPendingMatchSeed = true;
            }
            int seed = session.PendingMatchSeed;

            session.FighterOpponentType = FighterOpponentType.Cpu;
            session.FighterMatchOrigin = FrontEnd ? FighterMatchOrigin.VersusCpu : FighterMatchOrigin.Holodeck;
            session.CpuDifficulty = (CpuDifficulty)_cpuDifficulty.GetSelectedId();
            int opponentItem = (int)_cpuCharacter.GetSelectedId();
            int opponentIndex = opponentItem == RandomItemID
                ? FighterRandomPick.Index(seed, FighterRandomPick.PlayerTwoCharacterSalt, RosterIDs.Length)
                : Mathf.Clamp(opponentItem, 0, RosterIDs.Length - 1);
            session.OpponentCharacterID = RosterIDs[opponentIndex];
            int stageIndex = stageItem == RandomItemID
                ? FighterRandomPick.Index(seed, FighterRandomPick.StageSalt, _stageIDs.Count)
                : stageItem;
            session.SelectedStageID = _stageIDs[stageIndex];

            MatchSettings settings = session.MatchSettings;
            settings.Mode = (MatchMode)_matchMode.GetSelectedId();
            settings.StockCount = (int)_stockCount.Value;
            settings.TimeLimit = ResolveTimeLimitForMode(settings.Mode, (float)_timeLimit.Value);
            var itemRate = (ChronalOrbFrequency)_itemFrequency.GetSelectedId();
            settings.ItemSpawnRate = itemRate;
            settings.ItemsEnabled = itemRate != ChronalOrbFrequency.Off;
            settings.MeterPickupsEnabled = _meterPickups.ButtonPressed;
            settings.StageHazardsEnabled = _stageHazards.ButtonPressed;
            session.MatchSettings = settings;

            GameManager.Instance.CurrentSession = session;
            GameManager.Instance.PersistMatchSettings();
            return _stageCatalog?.Find(session.SelectedStageID);
        }

        /// <summary>
        /// F21: the option list carries only the two selectable modes, so the index
        /// a saved mode maps to is its ordinal — clamped, because an out-of-range
        /// stored value (the retired Hybrid, or a corrupt payload that reached the
        /// menu before <c>SavedMatchSettings.Normalize</c> could run) would
        /// otherwise index past the list.
        /// </summary>
        private static int SelectableModeIndex(MatchMode mode) =>
            mode == MatchMode.TimeLimit ? 1 : 0;

        /// <summary>
        /// F21: <b>Time requires a positive timer and Off is unavailable there</b>,
        /// so switching from untimed Stock to Time selects the default 480 s. The
        /// spin box is written before launch, which is what "shows it before launch"
        /// asks for — the player sees the repaired value in the rules row. Stock
        /// keeps its own timer, Off included, and characters, stage, items and
        /// hazards are untouched by the switch.
        /// </summary>
        private float ResolveTimeLimitForMode(MatchMode mode, float requested) {
            if (mode != MatchMode.TimeLimit) return requested;
            if (requested > 0f && !float.IsNaN(requested) && !float.IsInfinity(requested)) return requested;
            float repaired = SavedMatchSettings.DefaultTimeLimitSeconds;
            if (_timeLimit != null) _timeLimit.Value = repaired;
            return repaired;
        }

    }
}
