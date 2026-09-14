using System;
using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using Godot;

namespace FTT.Combat {

    /// <summary>
    /// The shared Holodeck drill scene (F18 Option B). One
    /// <see cref="FighterSimulationDriver"/>-backed sandbox on a Sealed stage's
    /// geometry, a scripted sparring dummy injected through
    /// <c>InputManager.SetInputSource</c>, and one drill from
    /// <see cref="CalibrationDrillCatalog"/> running against it.
    ///
    /// <para><b>Isolation.</b> Both fighters are built with
    /// <c>applyStoryProgression: false</c>, so a drill runs full normalized Fighter
    /// kits with no Resonance stats, no Story perks, and no Legacy Unlock gate —
    /// the same seam the Mirror Paradox clone uses. Nothing here touches
    /// <c>SaveManager</c>, <c>StoryManager</c> or
    /// <c>GameManager.CurrentSession.ActiveSaveSlot</c>: a drill can neither write
    /// a campaign nor be resumed as one.</para>
    ///
    /// <para><b>Reset.</b> Retry rebuilds the sandbox outright — both presentation
    /// fighters and the driver are freed and re-created — rather than patching
    /// simulation state from outside. That is the only reset that is correct by
    /// construction for HP, meter, shield charges, cooldowns, status and position
    /// at once, and it keeps every write to deterministic state inside
    /// <c>scripts/FighterSim/</c>.</para>
    ///
    /// <para><b>Pause.</b> This node never writes <see cref="SceneTree.Paused"/>.
    /// The driver's <c>LocalFighterPause</c> owns it through
    /// <c>PauseMenuBase</c>; the result card suspends the sandbox by disabling the
    /// driver's process mode instead, so a card can never leak a pause.</para>
    /// </summary>
    public partial class CalibrationDrillRunner : Node2D {

        /// <summary>The Sealed stage whose fixed-point geometry the drill runs on.</summary>
        public const string DrillStageID = "florence_workshop";

        /// <summary>The sparring dummy's character. Fixed so every drill reads the same.</summary>
        public const string DummyCharacterID = "joan";

        /// <summary>Fallback student when the session carries no character.</summary>
        public const string DefaultStudentCharacterID = "einstein";

        public const string ScenePath = "res://scenes/ui/HolodeckDrill.tscn";
        public const string DrillListScenePath = "res://scenes/ui/CalibrationDrillList.tscn";

        private FighterSimulationDriver _driver;
        private PlayerController _student;
        private PlayerController _dummy;
        private CalibrationDrillDummySource _dummySource;
        private CalibrationDrillProgress _progress;
        private CalibrationDrillDefinition _definition;

        private int _drillIndex;
        private int _liveFrames;
        private int _studentStartHP;
        private bool _cardVisible;
        private int _lastStage = 1;

        private Label _nameLabel;
        private Label _objectiveLabel;
        private Label _progressLabel;
        private Control _resultCard;
        private Label _resultTitle;
        private Label _resultCoaching;
        private Button _retryButton;
        private Button _nextButton;
        private Button _listButton;
        private Button _exitButton;

        private FighterOpponentType _restoreOpponentType;
        private bool _opponentTypeCaptured;

        /// <summary>The drill currently loaded. Test surface.</summary>
        public CalibrationDrillDefinition CurrentDrill => _definition;

        /// <summary>Index of the drill currently loaded. Test surface.</summary>
        public int CurrentDrillIndex => _drillIndex;

        /// <summary>Attempt standing. Test surface.</summary>
        public CalibrationDrillOutcome Outcome =>
            _progress?.Outcome ?? CalibrationDrillOutcome.InProgress;

        /// <summary>True while the pass/fail card is showing. Test surface.</summary>
        public bool IsResultCardVisible => _cardVisible;

        /// <summary>The simulation driver backing the sandbox. Test surface.</summary>
        public FighterSimulationDriver Driver => _driver;

        public override void _Ready() {
            BindOverlay();
            SessionData session = GameManager.Instance?.CurrentSession ?? default;
            _drillIndex = CalibrationDrillCatalog.ClampIndex(session.CalibrationDrillIndex);
            // A drill is a two-local-fighter sandbox: the CPU controller would
            // otherwise take player 1 and the scripted dummy would never be heard.
            if (GameManager.Instance != null) {
                _restoreOpponentType = session.FighterOpponentType;
                _opponentTypeCaptured = true;
                SessionData drillSession = session;
                drillSession.FighterOpponentType = FighterOpponentType.LocalHuman;
                GameManager.Instance.CurrentSession = drillSession;
            }
            StartDrill(_drillIndex);
        }

        public override void _ExitTree() {
            InputManager.Instance?.ClearInputSource(1);
            if (_opponentTypeCaptured && GameManager.Instance != null) {
                SessionData session = GameManager.Instance.CurrentSession;
                session.FighterOpponentType = _restoreOpponentType;
                GameManager.Instance.CurrentSession = session;
                _opponentTypeCaptured = false;
            }
        }

        // ---- sandbox lifecycle ------------------------------------------------

        /// <summary>
        /// Tears the sandbox down and rebuilds it for <paramref name="index"/>.
        /// This is both "start" and "retry": every resource the lesson needs is
        /// supplied by construction, so nothing can carry over from a prior attempt.
        /// </summary>
        public void StartDrill(int index) {
            _drillIndex = CalibrationDrillCatalog.ClampIndex(index);
            _definition = CalibrationDrillCatalog.At(_drillIndex);
            _progress = new CalibrationDrillProgress(_definition);
            _progress.Reset();
            _liveFrames = 0;
            _lastStage = 1;
            HideResultCard();

            if (GameManager.Instance != null) {
                SessionData session = GameManager.Instance.CurrentSession;
                session.CalibrationDrillIndex = _drillIndex;
                GameManager.Instance.CurrentSession = session;
            }

            TeardownSandbox();
            BuildSandbox();
            RefreshObjective();
        }

        private void TeardownSandbox() {
            InputManager.Instance?.ClearInputSource(1);
            FreeNode(_driver);
            FreeNode(_student);
            FreeNode(_dummy);
            _driver = null;
            _student = null;
            _dummy = null;
            _dummySource = null;
        }

        private static void FreeNode(Node node) {
            if (node == null || !IsInstanceValid(node)) return;
            node.GetParent()?.RemoveChild(node);
            node.QueueFree();
        }

        private void BuildSandbox() {
            string studentID = GameManager.Instance?.CurrentSession.SelectedCharacterID;
            if (string.IsNullOrWhiteSpace(studentID)) studentID = DefaultStudentCharacterID;

            Vector2 spawnOne = GetNodeOrNull<Marker2D>("Spawns/Player1")?.Position ?? new Vector2(750f, 700f);
            Vector2 spawnTwo = GetNodeOrNull<Marker2D>("Spawns/Player2")?.Position ?? new Vector2(1150f, 700f);

            _student = CharacterFactory.CreateCharacter(studentID, 0, applyStoryProgression: false);
            _student.Name = "DrillStudent";
            _student.Position = spawnOne;
            AddChild(_student);

            _dummy = CharacterFactory.CreateCharacter(DummyCharacterID, 1, applyStoryProgression: false);
            _dummy.Name = "DrillDummy";
            _dummy.Position = spawnTwo;
            _dummy.IsFacingRight = false;
            AddChild(_dummy);

            if (GetNodeOrNull<FighterCamera>("Camera2D") is FighterCamera camera) {
                camera.SetPlayers(_student, _dummy);
                camera.MakeCurrent();
            }

            _driver = new FighterSimulationDriver { Name = "FighterSimulationDriver" };
            AddChild(_driver);
            // A fixed seed: a drill is a lesson, not a match, and an identical
            // sandbox every attempt is what makes coaching reproducible.
            _driver.Initialize(_student, _dummy, DrillMatchSettings(), stageHazardTypeID: 0,
                stageID: DrillStageID, matchSeed: DrillMatchSeed);

            _dummySource = new CalibrationDrillDummySource();
            InputManager.Instance?.SetInputSource(1, _dummySource);

            _studentStartHP = _student.Data?.MaxHP ?? 100;
        }

        /// <summary>Reproducible sandbox seed; items and hazards are off regardless.</summary>
        public const int DrillMatchSeed = 110011;

        /// <summary>
        /// The drill sandbox's rules. Deliberately built here rather than read from
        /// the session: a drill must never inherit — or overwrite — the player's
        /// saved Fighter match settings. Items and hazards are off, and the stock
        /// and time budgets are large enough that no lesson can end the match.
        /// </summary>
        public static MatchSettings DrillMatchSettings() => new() {
            Mode = MatchMode.Stock,
            StockCount = 9,
            TimeLimit = 3600f,
            ItemsEnabled = false,
            ItemSpawnRate = ChronalOrbFrequency.Off,
            StageHazardsEnabled = false,
            HazardRate = HazardTriggerFrequency.Off
        };

        // ---- per-frame --------------------------------------------------------

        public override void _PhysicsProcess(double delta) {
            if (_driver == null || !IsInstanceValid(_driver) || _driver.Simulation == null) return;
            if (_cardVisible) return;

            bool live = _driver.Simulation.GetMatchState().MatchState == FighterMatchStates.InProgress;
            if (live) _liveFrames++;

            CalibrationDrillSample sample = Sample(live);
            CalibrationDrillOutcome outcome = _progress.Observe(in sample);
            PushDummyCommand(live);

            if (_progress.Stage != _lastStage) {
                _lastStage = _progress.Stage;
                RefreshObjective();
            }
            UpdateProgressLine(live);

            if (outcome != CalibrationDrillOutcome.InProgress) ShowResultCard(outcome);
        }

        private CalibrationDrillSample Sample(bool live) {
            var sample = new CalibrationDrillSample { Frame = _liveFrames, Live = live };
            if (_driver.TryGetFighter(0, out FighterStateComponent student)) {
                sample.StudentHP = student.CurrentHP;
                sample.StudentBlockCharges = student.BlockCharges;
                sample.StudentGrounded = student.IsGrounded != 0;
                sample.StudentMeter = (int)student.Influence.ToFloat();
            }
            if (_driver.TryGetVerb(0, out FighterVerbComponent verb)) {
                sample.StudentShieldStunFrames = verb.ShieldStunFrames;
                sample.StudentTumble = verb.Tumble;
                sample.StudentTechLockoutFrames = verb.TechLockoutFrames;
                sample.StudentPendingLaunchActive = verb.PendingLaunchActive;
                sample.StudentGrabPhase = verb.GrabPhase;
                sample.StudentEchoStepWindupFrames = verb.EchoStepWindupFrames;
                sample.StudentEchoPool = verb.EchoPool.ToFloat();
            }
            if (_driver.TryGetFighter(1, out FighterStateComponent dummy)) {
                sample.DummyHP = dummy.CurrentHP;
            }
            PlayerInputFrame studentInput = InputManager.Instance?.GetFrame(0) ?? default;
            sample.StudentDirectionHeld = Math.Abs(studentInput.MoveX) > 30 || Math.Abs(studentInput.MoveY) > 30;
            return sample;
        }

        /// <summary>
        /// Builds the dummy's command for the next tick. InputManager captures
        /// player frames at the top of the physics frame, so the command written
        /// here is consumed on the following tick — a one-frame lag that is
        /// irrelevant to a scripted bag and keeps the sampling order honest.
        /// </summary>
        private void PushDummyCommand(bool live) {
            if (_dummySource == null) return;
            var context = new CalibrationDrillDummyContext {
                Frame = _liveFrames,
                StudentStartHP = _studentStartHP,
                DummyCanAct = live
            };
            if (_driver.TryGetFighter(0, out FighterStateComponent student)
                && _driver.TryGetFighter(1, out FighterStateComponent dummy)) {
                context.SignedDistanceX = (student.Position.x - dummy.Position.x).ToFloat();
                context.StudentHP = student.CurrentHP;
                context.DummyCanAct = live
                    && dummy.HitstunFrames == 0 && dummy.DazeFrames == 0 && dummy.Stocks > 0;
            }
            CalibrationDrillDummyCommand command =
                CalibrationDrillDummyScript.Step(_definition.DummyRole, in context);
            _dummySource.SetCommand(in command);
        }

        // ---- overlay ----------------------------------------------------------

        private void BindOverlay() {
            const string overlay = "DrillLayer/Overlay/";
            _nameLabel = GetNodeOrNull<Label>(overlay + "TopPanel/Layout/DrillName");
            _objectiveLabel = GetNodeOrNull<Label>(overlay + "TopPanel/Layout/Objective");
            _progressLabel = GetNodeOrNull<Label>(overlay + "TopPanel/Layout/ProgressLine");
            _resultCard = GetNodeOrNull<Control>(overlay + "ResultCard");
            _resultTitle = GetNodeOrNull<Label>(overlay + "ResultCard/Layout/ResultTitle");
            _resultCoaching = GetNodeOrNull<Label>(overlay + "ResultCard/Layout/ResultCoaching");
            _retryButton = GetNodeOrNull<Button>(overlay + "ResultCard/Layout/Buttons/RetryButton");
            _nextButton = GetNodeOrNull<Button>(overlay + "ResultCard/Layout/Buttons/NextButton");
            _listButton = GetNodeOrNull<Button>(overlay + "ResultCard/Layout/Buttons/ListButton");
            _exitButton = GetNodeOrNull<Button>(overlay + "ResultCard/Layout/Buttons/ExitButton");

            if (_retryButton != null) _retryButton.Pressed += Retry;
            if (_nextButton != null) _nextButton.Pressed += NextDrill;
            if (_listButton != null) _listButton.Pressed += ReturnToDrillList;
            if (_exitButton != null) _exitButton.Pressed += ExitCalibration;
            if (_resultCard != null) _resultCard.Visible = false;
        }

        private void RefreshObjective() {
            if (_nameLabel != null) _nameLabel.Text = Tr(_definition.NameKey);
            if (_objectiveLabel == null) return;
            string key = _progress.Stage >= 2 && _definition.ObjectiveStageTwoKey != null
                ? _definition.ObjectiveStageTwoKey
                : _definition.ObjectiveKey;
            _objectiveLabel.Text = Tr(key);
        }

        private void UpdateProgressLine(bool live) {
            if (_progressLabel == null) return;
            _progressLabel.Text = string.Format(
                Tr("drill_progress"), _drillIndex + 1, CalibrationDrillCatalog.Count);
            _progressLabel.Visible = live;
        }

        private void ShowResultCard(CalibrationDrillOutcome outcome) {
            _cardVisible = true;
            // Suspends the sandbox without touching SceneTree.Paused: the card
            // lives on its own CanvasLayer and keeps taking input.
            if (_driver != null && IsInstanceValid(_driver)) _driver.ProcessMode = ProcessModeEnum.Disabled;
            bool passed = outcome == CalibrationDrillOutcome.Passed;
            if (_resultTitle != null) _resultTitle.Text = Tr(passed ? "drill_result_pass" : "drill_result_fail");
            if (_resultTitle != null) {
                _resultTitle.AddThemeColorOverride(
                    "font_color", passed ? FTT.UI.UIPalette.Cyan : FTT.UI.UIPalette.Warning);
            }
            if (_resultCoaching != null) {
                _resultCoaching.Text = Tr(passed ? _definition.PassCoachKey : _definition.FailCoachKey);
            }
            // F18: the final drill offers the list or the exit only.
            if (_nextButton != null) {
                _nextButton.Visible = passed && CalibrationDrillCatalog.HasNext(_drillIndex);
            }
            if (_resultCard != null) {
                _resultCard.Visible = true;
                FTT.UI.FocusChainBuilder.Apply(_resultCard);
            }
        }

        private void HideResultCard() {
            _cardVisible = false;
            if (_resultCard != null) _resultCard.Visible = false;
            if (_driver != null && IsInstanceValid(_driver)) _driver.ProcessMode = ProcessModeEnum.Inherit;
        }

        // ---- result-card actions ---------------------------------------------

        /// <summary>Restarts the same drill from a freshly built sandbox.</summary>
        public void Retry() => StartDrill(_drillIndex);

        /// <summary>Advances to the next drill in place; a no-op on the last one.</summary>
        public void NextDrill() {
            if (!CalibrationDrillCatalog.HasNext(_drillIndex)) return;
            StartDrill(_drillIndex + 1);
        }

        public void ReturnToDrillList() => GameManager.Instance?.LoadScene(DrillListScenePath);

        /// <summary>
        /// Disarms the route and returns the Exit Calibration destination, without
        /// changing scene. Split from <see cref="ExitCalibration"/> so the routing
        /// contract is testable (the Holodeck console idiom).
        /// </summary>
        public string ApplyExitToSession() {
            if (GameManager.Instance == null) return null;
            string destination = CalibrationRoute.ExitDestination(
                GameManager.Instance.CurrentSession.CalibrationReturnScenePath);
            GameManager.Instance.CurrentSession =
                CalibrationRoute.Cleared(GameManager.Instance.CurrentSession);
            return destination;
        }

        /// <summary>
        /// Exit Calibration. Routes to the session's recorded calibration return
        /// destination — the Main Menu for the standalone route, the originating
        /// hub for the Holodeck console route — and leaves campaign state alone.
        /// </summary>
        public void ExitCalibration() {
            string destination = ApplyExitToSession();
            if (destination != null) GameManager.Instance.LoadScene(destination);
        }
    }
}
