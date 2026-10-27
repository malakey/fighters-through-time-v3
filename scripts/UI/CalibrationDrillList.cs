using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// The shared Calibration Drills list (F18 Option B). Both entry points — the
    /// standalone Main Menu route through
    /// <see cref="CalibrationDrillPicker"/> and the Story hub's Holodeck console —
    /// arrive here, and the content is identical: the six drills of
    /// <see cref="CalibrationDrillCatalog"/> run with the chosen character's
    /// <b>normalized Fighter kit</b>.
    ///
    /// <para>The rows are built here rather than authored one-per-drill because the
    /// catalog is the single source of drill identity; authoring six fixed buttons
    /// would put a second copy of the drill order in a scene file. The row template
    /// and the static copy are authored.</para>
    /// </summary>
    public partial class CalibrationDrillList : Control {

        private const string Root = "List/Root/";

        private Label _characterLabel;
        private VBoxContainer _rows;
        private readonly List<Button> _drillButtons = new();

        /// <summary>The drill buttons, in catalog order. Test surface.</summary>
        public IReadOnlyList<Button> DrillButtons => _drillButtons;

        public override void _Ready() {
            UIPalette.ApplyTheme(this);
            _characterLabel = GetNodeOrNull<Label>(Root + "CharacterLine");
            _rows = GetNode<VBoxContainer>(Root + "Rows");

            BuildRows();
            RefreshCharacterLine();

            GetNode<Button>(Root + "ButtonRow/BackButton").Pressed += Back;
            GetNode<Button>(Root + "ButtonRow/ExitButton").Pressed += ExitCalibration;

            DressGlass();
            // FU-MENU: the rows outgrow 1080 px at the 1.4 UI scale; shrink to fit (TG-SCREENS: and keep clear of the
            // screen heading above).
            MenuSafeAreaFit.Attach(GetNode<Control>(Root.TrimEnd('/')), GlassScreenDressing.ScaledHeadingReserve);
            FocusChainBuilder.Apply(GetNode<Control>("List"));
        }

        /// <summary>
        /// TG-SCREENS (UI_SPEC_GLASS 9.1, 9.2: the Calibration list): a full-screen menu. The sub-screen art, the heading
        /// <c>drill_list_title</c> between flourishes top-centre, the hint and the training line as scene text, a chevron
        /// column of the drills at its D6 width (min 560, cap 700, the prompt reserved) with the focused drill's objective
        /// under it (as the difficulty screen's description), the footer as compact chevrons with no badge and the derived
        /// row gap, the prompt bar bottom-right and the screen streak. A drill finished this session shows its last result
        /// at its row's right (<see cref="AddResultGlyph"/>).
        /// </summary>
        private void DressGlass() {
            GlassScreenDressing.AddMenuArt(this, index: 1);
            GlassScreenDressing.AddScreenHeading(this, "drill_list_title");
            _objectiveLabel = GetNodeOrNull<Label>(Root + "Objective");
            var rows = new List<Button>(_drillButtons.Count);
            foreach (Button row in _drillButtons) {
                FTT.UI.Pulp.PulpButtonDecor.Apply(row);
                rows.Add(row);
            }
            FTT.UI.Pulp.PulpButtonDecor.ApplyStackWidth(rows, 560f, GlassScreenDressing.ScaledStackCap(700f));
            var footer = GetNode<HBoxContainer>(Root + "ButtonRow");
            List<Button> buttons = PulpScreenKit.ButtonsOf(footer);
            foreach (Button button in buttons) FTT.UI.Pulp.PulpButtonDecor.Apply(button, compact: true).ShowPrompt = false;
            FTT.UI.Pulp.PulpButtonDecor.ApplyStackWidth(buttons, 290f, reservePrompt: false);
            PulpScreenKit.SeparateFocusRow(footer);
            PulpScreenKit.AddPromptBar(this, ("ui_accept", "ui_prompt_select"), ("ui_cancel", "common_back"));
            PulpScreenKit.AddScreenStreak(this);
            ShowObjective(0);
        }

        private Label _objectiveLabel;

        // ---- each drill's last result this session (UI_SPEC_GLASS 9.2: shown at the row's right) ------------------

        /// <summary>The result glyph's name, size and gap to the right of its row's chevron.</summary>
        public const string ResultGlyphName = "ResultGlyph";
        public const float ResultGlyphSize = 28f, ResultGlyphGap = 16f;

        private static CalibrationDrillOutcome[] _lastOutcomes = System.Array.Empty<CalibrationDrillOutcome>();

        /// <summary>
        /// Records drill <paramref name="drillIndex"/>'s result for the list (the runner calls it as a drill ends). Process
        /// memory for the glyph only, like the route's own session fields: never persisted and never read by gameplay.
        /// </summary>
        public static void NoteOutcome(int drillIndex, CalibrationDrillOutcome outcome) {
            if (drillIndex < 0 || drillIndex >= CalibrationDrillCatalog.Count) return;
            if (_lastOutcomes.Length != CalibrationDrillCatalog.Count) {
                System.Array.Resize(ref _lastOutcomes, CalibrationDrillCatalog.Count);
            }
            _lastOutcomes[drillIndex] = outcome;
        }

        /// <summary>Drill <paramref name="drillIndex"/>'s last result this session; <c>InProgress</c> when none.</summary>
        public static CalibrationDrillOutcome LastOutcome(int drillIndex) =>
            drillIndex >= 0 && drillIndex < _lastOutcomes.Length ? _lastOutcomes[drillIndex] : CalibrationDrillOutcome.InProgress;

        /// <summary>Test seam: forgets every recorded result.</summary>
        internal static void ForgetOutcomesForTest() => _lastOutcomes = System.Array.Empty<CalibrationDrillOutcome>();

        /// <summary>Shows the objective of drill <paramref name="index"/> under the column (the focused row's lesson).</summary>
        private void ShowObjective(int index) {
            if (_objectiveLabel == null || index < 0 || index >= CalibrationDrillCatalog.Count) return;
            _objectiveLabel.Text = Tr(CalibrationDrillCatalog.At(index).ObjectiveKey);
        }

        private void BuildRows() {
            for (int index = 0; index < CalibrationDrillCatalog.Count; index++) {
                int captured = index;
                CalibrationDrillDefinition drill = CalibrationDrillCatalog.At(index);
                var button = new Button {
                    Name = $"DrillButton{index}",
                    // Numbered so the order the design fixes is visible. TG-SCREENS: the objective, the one-line lesson,
                    // shows under the column for the focused row (a chevron row carries one line).
                    Text = $"{index + 1}.  {Tr(drill.NameKey)}",
                    CustomMinimumSize = new Vector2(0f, 68f),
                    ThemeTypeVariation = UIPalette.Variations.PulpButton,
                    Alignment = HorizontalAlignment.Left
                };
                button.Pressed += () => StartDrill(captured);
                button.FocusEntered += () => ShowObjective(captured);
                _rows.AddChild(button);
                _drillButtons.Add(button);
                AddResultGlyph(button, index);
            }
        }

        /// <summary>
        /// The row's last result this session at its right, outside the chevron so it never meets the focused row's
        /// prompt badge: <c>check</c> in hp for a pass, the <c>cross</c> in ink2 for a fail, named by its tooltip. A drill
        /// not finished this session shows nothing.
        /// </summary>
        private void AddResultGlyph(Button row, int index) {
            CalibrationDrillOutcome last = LastOutcome(index);
            if (last == CalibrationDrillOutcome.InProgress) return;
            bool passed = last == CalibrationDrillOutcome.Passed;
            FTT.UI.Pulp.PulpIcon glyph = FTT.UI.Pulp.PulpIcon.Create(
                passed ? "check" : "cross", passed ? UIPalette.Hp : UIPalette.Ink2, ResultGlyphSize);
            glyph.Name = ResultGlyphName;
            glyph.TooltipText = Tr(passed ? "drill_result_pass" : "drill_result_fail");
            row.AddChild(glyph);
            glyph.AnchorLeft = 1f;
            glyph.AnchorRight = 1f;
            glyph.AnchorTop = 0.5f;
            glyph.AnchorBottom = 0.5f;
            glyph.OffsetLeft = ResultGlyphGap;
            glyph.OffsetRight = ResultGlyphGap + ResultGlyphSize;
            glyph.OffsetTop = -ResultGlyphSize / 2f;
            glyph.OffsetBottom = ResultGlyphSize / 2f;
        }

        private void RefreshCharacterLine() {
            if (_characterLabel == null) return;
            string characterID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "";
            // Guarded: the list is reachable standalone (a headless smoke run, or a
            // session that has not picked a character yet), and asking for
            // "res://resources/Characters/_data.tres" is a hard resource error.
            string path = $"res://resources/Characters/{characterID}_data.tres";
            FTT.Characters.CharacterData data =
                string.IsNullOrWhiteSpace(characterID) || !ResourceLoader.Exists(path)
                    ? null
                    : AuthoredResources.Load<FTT.Characters.CharacterData>(path);
            string name = data == null || string.IsNullOrWhiteSpace(data.DisplayNameKey)
                ? Tr("common_unknown")
                : Tr(data.DisplayNameKey);
            _characterLabel.Text = string.Format(Tr("drill_list_character"), name);
        }

        /// <summary>Records the chosen drill in the session and opens the sandbox.</summary>
        public void StartDrill(int index) {
            string destination = ApplyDrillToSession(index);
            if (destination != null) GameManager.Instance.LoadScene(destination);
        }

        /// <summary>
        /// Records the chosen drill and returns the scene to open. Split from
        /// <see cref="StartDrill"/> so the session contract is testable without a
        /// real scene change.
        /// </summary>
        public string ApplyDrillToSession(int index) {
            if (GameManager.Instance == null) return null;
            SessionData session = GameManager.Instance.CurrentSession;
            session.CalibrationDrillIndex = CalibrationDrillCatalog.ClampIndex(index);
            // Package 12 W5 (M26): the drill borrows the Fighter match shell; its
            // exits are the calibration route's, never a Fighter lobby's.
            session.FighterMatchOrigin = FighterMatchOrigin.Drill;
            GameManager.Instance.CurrentSession = session;
            return CalibrationRoute.DrillScenePath;
        }

        /// <summary>
        /// Back: to the picker on the standalone route, to the originating hub on
        /// the console route (which has no picker — the campaign character is locked).
        /// </summary>
        public void Back() {
            string destination = ApplyBackToSession();
            if (destination != null) GameManager.Instance.LoadScene(destination);
        }

        /// <summary>Resolves Back, disarming the route only when Back leaves it.</summary>
        public string ApplyBackToSession() {
            if (GameManager.Instance == null) return null;
            string returnPath = GameManager.Instance.CurrentSession.CalibrationReturnScenePath;
            string destination = CalibrationRoute.DrillListBackDestination(returnPath);
            // Backing to the picker stays inside the route; backing to the hub leaves it.
            if (destination != CalibrationRoute.PickerScenePath) {
                GameManager.Instance.CurrentSession =
                    CalibrationRoute.Cleared(GameManager.Instance.CurrentSession);
            }
            return destination;
        }

        /// <summary>Exit Calibration: the Main Menu, or the originating hub.</summary>
        public void ExitCalibration() {
            string destination = ApplyExitToSession();
            if (destination != null) GameManager.Instance.LoadScene(destination);
        }

        /// <summary>Disarms the route and returns the Exit Calibration destination.</summary>
        public string ApplyExitToSession() {
            if (GameManager.Instance == null) return null;
            string destination = CalibrationRoute.ExitDestination(
                GameManager.Instance.CurrentSession.CalibrationReturnScenePath);
            GameManager.Instance.CurrentSession =
                CalibrationRoute.Cleared(GameManager.Instance.CurrentSession);
            return destination;
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (@event == null || !@event.IsActionPressed("ui_cancel")) return;
            GetViewport()?.SetInputAsHandled();
            Back();
        }
    }
}
