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

            FocusChainBuilder.Apply(GetNode<Control>("List"));
        }

        private void BuildRows() {
            for (int index = 0; index < CalibrationDrillCatalog.Count; index++) {
                int captured = index;
                CalibrationDrillDefinition drill = CalibrationDrillCatalog.At(index);
                var button = new Button {
                    Name = $"DrillButton{index}",
                    // Numbered so the order the design fixes is visible, and the
                    // objective sits under the name as the one-line lesson.
                    Text = $"{index + 1}.  {Tr(drill.NameKey)}\n{Tr(drill.ObjectiveKey)}",
                    CustomMinimumSize = new Vector2(720f, 64f),
                    ThemeTypeVariation = "TemporalGlassButton",
                    Alignment = HorizontalAlignment.Left,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                button.Pressed += () => StartDrill(captured);
                _rows.AddChild(button);
                _drillButtons.Add(button);
            }
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
