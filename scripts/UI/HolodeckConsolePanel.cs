using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using FTT.UI.Pulp;
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

        /// <summary>
        /// The settings rows (UI_SPEC_GLASS 3.3): a 360 px label column, 22 px, a 44 px tall control; rows 62 apart. The
        /// control column is 380 wide, not 3.3's 300: a stage with its layout ("Chicago Exposition — Sealed") or a full hero
        /// name has to read whole in the field.
        /// </summary>
        public const float RowLabelWidth = 360f, RowGap = 22f, RowControlWidth = 380f, RowControlHeight = 44f, RowPitch = 62f;
        /// <summary>The console's action rows: the pause panel's chevrons (D6, min 560, cap 680).</summary>
        public const float ActionRowMinWidth = 560f, ActionRowCap = 680f;

        public override void _Ready() {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            UIPalette.ApplyTheme(this);

            // TG-SELECT (UI_SPEC_GLASS 9.1, 9.2): an overlay over the hub or the select, the build dim rgba(3,5,14,.70).
            var shade = new ColorRect {
                Name = "Shade",
                Color = UIPalette.PauseDim,
                MouseFilter = MouseFilterEnum.Stop
            };
            shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(shade);

            // FU-MENU: a full-rect CenterContainer keeps the panel on the screen centre
            // however tall it grows; SetAnchorsPreset(Center) alone left its top-left
            // corner there and pushed the bottom off screen.
            var center = new CenterContainer { Name = "Center", MouseFilter = MouseFilterEnum.Ignore };
            center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(center);

            // TG-SELECT: one glass slab (the sheen, the 14 px stack inset), its title inside (9.2: 36 px), no tag. The
            // tagged host stays so the console's node paths do not move.
            var tagged = new PulpTaggedPanel { Name = "Tagged", TagKey = "" };
            center.AddChild(tagged);
            var panel = new PanelContainer {
                Name = "Panel", ThemeTypeVariation = UIPalette.Variations.InkPanel
            };
            tagged.AddChild(panel);
            PulpScreenKit.DressPanel(panel, null, sheen: true, buttonInset: true);
            // The slab clears the overlay prompt bar at the foot of the screen (as the other glass overlays do).
            MenuSafeAreaFit.AttachClearOfPromptBar(tagged, this);

            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", (int)(RowPitch - RowControlHeight));
            panel.AddChild(layout);
            layout.AddChild(new Label {
                Name = "Title", Text = FrontEnd ? "fighter_versus_cpu_title" : "holodeck_console_title", Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.TitleLabel, HorizontalAlignment = HorizontalAlignment.Center
            });

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

            // Stocks and the timer are two settings rows; the stock count keeps its RulesRow path.
            var rulesRow = MakeSettingsRow(layout, "RulesRow");
            rulesRow.AddChild(MakeRowLabel("StocksLabel", "hud_stocks"));
            _stockCount = new SpinBox {
                Name = "StockCount",
                MinValue = 1, MaxValue = 5, Value = settings.StockCount,
                CustomMinimumSize = new Vector2(RowControlWidth, RowControlHeight),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            rulesRow.AddChild(_stockCount);

            var timerRow = MakeSettingsRow(layout, "TimerRow");
            timerRow.AddChild(MakeRowLabel("TimerLabel", "fighter_mode_time"));
            _timeLimit = new SpinBox {
                Name = "TimeLimit",
                MinValue = 60, MaxValue = 480, Step = 30, Value = settings.TimeLimit,
                CustomMinimumSize = new Vector2(RowControlWidth, RowControlHeight),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            timerRow.AddChild(_timeLimit);

            _itemFrequency = AddOptionRow(layout, "ItemFrequency", "fighter_items");
            FillFrequencySelect(_itemFrequency, (int)settings.ItemSpawnRate);

            // The two toggles as settings rows: the switch's own text in the label column, the track at the row's end.
            var togglesRow = new VBoxContainer { Name = "TogglesRow" };
            togglesRow.AddThemeConstantOverride("separation", (int)(RowPitch - RowControlHeight - 8f));
            layout.AddChild(togglesRow);
            _meterPickups = new CheckButton {
                Name = "MeterPickups",
                Text = Tr("fighter_meter_pickups"),
                ButtonPressed = settings.MeterPickupsEnabled
            };
            PulpToggleDecor.Apply(_meterPickups);
            _meterPickups.CustomMinimumSize = new Vector2(RowLabelWidth + RowGap + RowControlWidth, 52f);
            togglesRow.AddChild(_meterPickups);
            _stageHazards = new CheckButton {
                Name = "StageHazards",
                Text = Tr("fighter_hazards"),
                ButtonPressed = settings.StageHazardsEnabled
            };
            PulpToggleDecor.Apply(_stageHazards);
            _stageHazards.CustomMinimumSize = new Vector2(RowLabelWidth + RowGap + RowControlWidth, 52f);
            togglesRow.AddChild(_stageHazards);

            // The actions: the pause panel's chevron rows (64 px, D6 width, the focused row's prompt), Launch first.
            var actions = new List<Button>();
            var launch = new Button { Name = "LaunchButton", Text = Tr("holodeck_launch") };
            PulpScreenKit.StylePauseButton(launch, ActionRowMinWidth);
            launch.Pressed += OnLaunchPressed;
            layout.AddChild(launch);
            actions.Add(launch);

            // Package 11 A11 (F18): the hub console keeps the CPU practice bout and
            // gains a second entry alongside it. Same drill list and content as the
            // standalone Main Menu route, run with the campaign character's
            // *normalized* Fighter kit; Exit Calibration returns to this hub. The
            // front-end Versus CPU screen has no drills entry — the main menu owns
            // the standalone route.
            if (!FrontEnd) {
                var drills = new Button { Name = "CalibrationDrillsButton", Text = Tr("holodeck_calibration_drills") };
                PulpScreenKit.StylePauseButton(drills, ActionRowMinWidth);
                drills.Pressed += OnCalibrationDrillsPressed;
                layout.AddChild(drills);
                actions.Add(drills);

                // GR-TRAIN (9c, ruling Q5): the third entry, hub console only. A dummy sandbox for tuning by hand.
                var training = new Button { Name = "TrainingRoomButton", Text = Tr("holodeck_training_room") };
                PulpScreenKit.StylePauseButton(training, ActionRowMinWidth);
                training.Pressed += OnTrainingRoomPressed;
                layout.AddChild(training);
                actions.Add(training);
            }

            var back = new Button { Name = "BackButton", Text = Tr("common_back") };
            PulpScreenKit.StylePauseButton(back, ActionRowMinWidth);
            back.Pressed += () => Closed?.Invoke();
            layout.AddChild(back);
            actions.Add(back);
            PulpButtonDecor.ApplyStackWidth(actions, ActionRowMinWidth, ActionRowCap);

            PulpScreenKit.AddOverlayPromptBar(this, ("ui_accept", "ui_prompt_select"), ("ui_cancel", "common_back"));
            PulpScreenKit.FadeIn(tagged);

            FocusChainBuilder.Apply(layout);
            launch.GrabFocus();
        }

        internal static HBoxContainer MakeSettingsRow(VBoxContainer layout, string name) {
            var row = new HBoxContainer { Name = name };
            row.AddThemeConstantOverride("separation", (int)RowGap);
            layout.AddChild(row);
            return row;
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (@event == null || !@event.IsActionPressed("ui_cancel")) return;
            GetViewport()?.SetInputAsHandled();
            Closed?.Invoke();
        }

        /// <summary>A settings row's label (UI_SPEC_GLASS 3.3, T52 <c>SettingsRowLabel</c>, upper-case) in the 360 px column.</summary>
        internal static Label MakeRowLabel(string name, string textKey) => new() {
            Name = name,
            Text = TranslationServer.Translate(textKey),
            Uppercase = true,
            ThemeTypeVariation = UIPalette.LabelRoles.SettingsRowLabel,
            CustomMinimumSize = new Vector2(RowLabelWidth, RowControlHeight),
            VerticalAlignment = VerticalAlignment.Center
        };

        internal static OptionButton AddOptionRow(VBoxContainer layout, string name, string labelKey) {
            HBoxContainer row = MakeSettingsRow(layout, name + "Row");
            row.AddChild(MakeRowLabel(name + "Label", labelKey));
            // The glass field (3.3, P8): the theme's default OptionButton. Every control fills the row past the label column,
            // so the fields keep one width when a large UI scale widens the longest one past 380.
            var select = new OptionButton {
                Name = name,
                CustomMinimumSize = new Vector2(RowControlWidth, RowControlHeight),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
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
                // floor has a pit in it. TG-CLOSE polish: one format key joins the
                // two with the spaced em dash ("Florence Workshop — Sealed").
                _stageSelect.AddItem(string.Format(Tr("fighter_stage_with_layout"),
                    Tr(stage.DisplayNameKey), Tr(FighterStageLayoutBadge.LabelKey(stage))), itemID);
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

        private void OnTrainingRoomPressed() {
            if (GameManager.Instance == null) return;
            GameManager.Instance.LoadScene(ApplyTrainingRouteToSession());
        }

        /// <summary>
        /// GR-TRAIN: marks the session as a training-room visit and returns the scene to open. Nothing else in the session
        /// moves (the save slot, difficulty, campaign level and the player's saved match settings are untouched, as for the
        /// drills), and a stale calibration route is cleared so the pause menu's exit cannot take it. Split from the button
        /// handler so the session contract is testable without a scene change.
        /// </summary>
        public static string ApplyTrainingRouteToSession() {
            SessionData session = GameManager.Instance.CurrentSession;
            session.FighterMatchOrigin = FighterMatchOrigin.Training;
            session.CalibrationReturnScenePath = "";
            session.ResumeAtStageSelect = false;
            GameManager.Instance.CurrentSession = session;
            return TrainingRoom.ScenePath;
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
