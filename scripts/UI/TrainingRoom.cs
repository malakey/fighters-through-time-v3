using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// GR-TRAIN (9c, ruling Q5): the Holodeck's training room, a third entry on the hub console. One
    /// <see cref="FighterSimulationDriver"/>-backed sandbox on the drills' Sealed stage, the campaign hero against a dummy
    /// that is driven through <c>InputManager.SetInputSource</c> (<see cref="TrainingDummySource"/>, the calibration drills'
    /// seam), and a readout slab. Both fighters are built with <c>applyStoryProgression: false</c>, so the room runs full
    /// normalized Fighter kits and cannot touch a campaign.
    ///
    /// <para><b>No simulation change.</b> The dummy is local input before quantization, and every readout is computed here
    /// from the driver's public read-only getters (<c>TryGetFighter</c>, <c>TryGetRuntime</c>, <c>TryGetVerb</c> and the
    /// simulation's <c>TryGetFighterActionState</c>). Reset rebuilds the sandbox outright, as a drill's Retry does, rather
    /// than writing deterministic state from outside; a dummy that is knocked out is reset the same way.</para>
    ///
    /// <para><b>Pause and input.</b> The pause button opens the settings panel (<see cref="TrainingRoomPanel"/>), which owns
    /// the tree pause while it is up; this node intercepts the press before the driver's own pause menu can take it, and the
    /// driver's menu stays reachable through a focus loss, where its Exit lands in the same place. <c>_ExitTree</c> clears
    /// player 1's input source, restores the opponent type and hands back any pause this node's panel raised.</para>
    /// </summary>
    public partial class TrainingRoom : Node2D {

        public const string ScenePath = "res://scenes/ui/TrainingRoom.tscn";

        /// <summary>The Sealed stage the sandbox runs on: the drills' (one stage, one set of geometry).</summary>
        public const string StageID = CalibrationDrillRunner.DrillStageID;
        public const string DummyCharacterID = CalibrationDrillRunner.DummyCharacterID;
        public const string DefaultStudentCharacterID = CalibrationDrillRunner.DefaultStudentCharacterID;

        /// <summary>A knocked-out dummy is put back after this many frames, once its KO has played.</summary>
        public const int KoResetDelayFrames = 150;

        private FighterSimulationDriver _driver;
        private PlayerController _student;
        private PlayerController _dummy;
        private TrainingDummySource _source;
        private TrainingRoomPanel _panel;
        private readonly TrainingReadoutTracker _tracker = new();
        private TrainingDummySettings _settings;

        private int _liveFrames;
        private int _startingDummyStocks;
        private int _koFrames;

        private Label _hitLine;
        private Label _framesLine;
        private int _shownTotal = -1;
        private string _shownHit = "";
        private string _shownFrames = "";

        private FighterOpponentType _restoreOpponentType;
        private bool _opponentTypeCaptured;

        /// <summary>The simulation driver backing the sandbox. Test surface.</summary>
        public FighterSimulationDriver Driver => _driver;
        /// <summary>The settings panel. Test surface.</summary>
        public TrainingRoomPanel Panel => _panel;
        /// <summary>The readout tracker. Test surface.</summary>
        public TrainingReadoutTracker Readouts => _tracker;
        /// <summary>The dummy's input source. Test surface.</summary>
        public TrainingDummySource DummySource => _source;
        /// <summary>The settings the dummy is running. Test surface.</summary>
        public TrainingDummySettings Settings => _settings;

        public override void _Ready() {
            // A training room is a two-local-fighter sandbox: the CPU controller would otherwise take player 1 and the
            // dummy would never be heard.
            if (GameManager.Instance != null) {
                SessionData session = GameManager.Instance.CurrentSession;
                _restoreOpponentType = session.FighterOpponentType;
                _opponentTypeCaptured = true;
                session.FighterOpponentType = FighterOpponentType.LocalHuman;
                GameManager.Instance.CurrentSession = session;
            }

            _hitLine = GetNodeOrNull<Label>("HudLayer/Overlay/HudPanel/Layout/HitLine");
            _framesLine = GetNodeOrNull<Label>("HudLayer/Overlay/HudPanel/Layout/FramesLine");
            if (GetNodeOrNull<PanelContainer>("HudLayer/Overlay/HudPanel") is { } hud) PulpScreenKit.DressPanel(hud);

            if (GetNodeOrNull<Control>("HudLayer/Overlay") is { } overlay) {
                _panel = new TrainingRoomPanel { Name = "TrainingRoomPanel", Settings = _settings };
                overlay.AddChild(_panel);
                _panel.Visible = false;
                _panel.SettingsChanged += settings => _settings = settings;
                _panel.ResetRequested += OnResetRequested;
                _panel.ExitRequested += ExitToShip;
            }

            StartSandbox();
        }

        public override void _ExitTree() {
            InputManager.Instance?.ClearInputSource(1);
            if (_opponentTypeCaptured && GameManager.Instance != null) {
                SessionData session = GameManager.Instance.CurrentSession;
                session.FighterOpponentType = _restoreOpponentType;
                GameManager.Instance.CurrentSession = session;
                _opponentTypeCaptured = false;
            }
            // The panel hands back a pause it raised in its own _ExitTree; this covers it leaving the tree first.
            if (_panel != null && GodotObject.IsInstanceValid(_panel) && _panel.HoldsPause) _panel.Close();
        }

        // ---- sandbox lifecycle ------------------------------------------------

        /// <summary>
        /// Tears the sandbox down and rebuilds it: both presentation fighters and the driver are freed and re-created, so HP,
        /// meter, shield, Momentum, status and position are all fresh by construction. The readouts start over with it.
        /// </summary>
        public void StartSandbox() {
            TeardownSandbox();
            BuildSandbox();
            _tracker.Reset();
            _liveFrames = 0;
            _koFrames = 0;
            RefreshReadouts(force: true);
        }

        private void TeardownSandbox() {
            InputManager.Instance?.ClearInputSource(1);
            FreeNode(_driver);
            FreeNode(_student);
            FreeNode(_dummy);
            _driver = null;
            _student = null;
            _dummy = null;
            _source = null;
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
            _student.Name = "TrainingStudent";
            _student.Position = spawnOne;
            AddChild(_student);

            _dummy = CharacterFactory.CreateCharacter(DummyCharacterID, 1, applyStoryProgression: false);
            _dummy.Name = "TrainingDummy";
            _dummy.Position = spawnTwo;
            _dummy.IsFacingRight = false;
            AddChild(_dummy);

            if (GetNodeOrNull<FighterCamera>("Camera2D") is FighterCamera camera) {
                camera.SetPlayers(_student, _dummy);
                camera.MakeCurrent();
            }

            _driver = new FighterSimulationDriver { Name = "FighterSimulationDriver" };
            AddChild(_driver);
            // The drills' sandbox rules, built rather than read from the session: the room must never inherit or overwrite
            // the player's saved Fighter match settings. Nine stocks, no items, no hazards.
            _driver.Initialize(_student, _dummy, CalibrationDrillRunner.DrillMatchSettings(), stageHazardTypeID: 0,
                stageID: StageID, matchSeed: CalibrationDrillRunner.DrillMatchSeed);

            _source = new TrainingDummySource();
            InputManager.Instance?.SetInputSource(1, _source);

            _startingDummyStocks = _driver.TryGetFighter(1, out FighterStateComponent dummy) ? dummy.Stocks : 0;
        }

        // ---- per-frame --------------------------------------------------------

        public override void _PhysicsProcess(double delta) {
            if (_driver == null || !IsInstanceValid(_driver) || _driver.Simulation == null) return;

            int matchState = _driver.Simulation.GetMatchState().MatchState;
            bool live = matchState == FighterMatchStates.InProgress;
            if (matchState == FighterMatchStates.Complete) {
                // A finished match would raise the results screen, which a training room never has: start over.
                StartSandbox();
                return;
            }
            if (live) _liveFrames++;

            TrainingReadoutSample sample = Sample(live);
            _tracker.Observe(in sample);
            PushDummyCommand(live);
            RefreshReadouts(force: false);

            if (live && _driver.TryGetFighter(1, out FighterStateComponent dummy) && dummy.Stocks < _startingDummyStocks) {
                if (++_koFrames >= KoResetDelayFrames) StartSandbox();
            }
        }

        private TrainingReadoutSample Sample(bool live) {
            var sample = new TrainingReadoutSample { Live = live };
            if (_driver.TryGetFighter(1, out FighterStateComponent dummy)) {
                sample.DummyHP = dummy.CurrentHP;
                sample.DummyHitstunFrames = dummy.HitstunFrames;
            }
            if (_driver.TryGetVerb(1, out FighterVerbComponent dummyVerb)) sample.DummyShieldStunFrames = dummyVerb.ShieldStunFrames;
            if (_driver.TryGetVerb(0, out FighterVerbComponent studentVerb)) sample.StudentFrozen = studentVerb.HitstopFrames > 0;
            sample.StudentPhase = StudentPhase();
            return sample;
        }

        /// <summary>
        /// The student's move phase on the one scale the readouts use (1 startup, 2 active, 3 recovery, 0 free): the basic
        /// string's phase machine, else a Special's cast phase on component 323. A chain hold and a delegated cast read free.
        /// </summary>
        private int StudentPhase() {
            if (_driver.TryGetRuntime(0, out FighterRuntimeComponent runtime) && runtime.AttackPhase is >= 1 and <= 3) {
                return runtime.AttackPhase;
            }
            if (_driver.Simulation.TryGetFighterActionState(0, out FighterActionStateComponent action)
                && action.SpecialPhase is >= 1 and <= 3) {
                return action.SpecialPhase;
            }
            return 0;
        }

        /// <summary>
        /// Builds the dummy's command for the next tick. InputManager captures player frames at the top of the physics
        /// frame, so the command written here is consumed on the following tick: a one-frame lag that is irrelevant to a
        /// bag and keeps the sampling order honest (the drills' note).
        /// </summary>
        private void PushDummyCommand(bool live) {
            if (_source == null) return;
            var context = new TrainingDummyContext { Frame = _liveFrames, FramesSinceHit = _tracker.FramesSinceHit, CentreDirection = 1f };
            if (_driver.TryGetFighter(1, out FighterStateComponent dummy)) {
                context.CanAct = live && dummy.Stocks > 0 && dummy.RespawnFramesRemaining == 0;
                context.CentreDirection = dummy.Position.x.ToFloat() > 0f ? -1f : 1f;
                context.InHitstun = dummy.HitstunFrames > 0 || dummy.DazeFrames > 0;
                if (_driver.TryGetVerb(1, out FighterVerbComponent verb) && verb.Tumble != 0) context.InHitstun = true;
            }
            TrainingDummyCommand command = TrainingDummyRules.Step(in _settings, in context);
            _source.SetCommand(in command);
        }

        // ---- readouts (presentation only) -------------------------------------

        private void RefreshReadouts(bool force) {
            if (_hitLine != null) {
                string hit = string.Format(Tr("training_readout_hit"), _tracker.LastHitDamage, _tracker.TotalDamage, _tracker.StunApplied);
                if (force || hit != _shownHit) { _shownHit = hit; _hitLine.Text = hit; }
            }
            if (_framesLine != null) {
                string advantage = _tracker.Advantage is { } value
                    ? (value > 0 ? "+" : "") + value
                    : "—";
                string frames = string.Format(Tr("training_readout_frames"),
                    _tracker.StartupFrames, _tracker.ActiveFrames, _tracker.RecoveryFrames, advantage);
                if (force || frames != _shownFrames) { _shownFrames = frames; _framesLine.Text = frames; }
            }
        }

        // ---- input and exits --------------------------------------------------

        public override void _Input(InputEvent @event) {
            if (@event == null || _panel == null) return;
            if (!@event.IsActionPressed(InputManager.Actions.Pause)) return;
            if (_panel.Visible) return; // the panel (ProcessMode Always) answers its own presses in its _Input.
            // The driver's pause menu would take this press; the room's settings panel takes it first. A pause menu that is
            // already open (a focus loss opened it) keeps it.
            if (PauseMenuBase.IsAnyOpen) return;
            GetViewport()?.SetInputAsHandled();
            _panel.Settings = _settings;
            _panel.Open();
        }

        private void OnResetRequested() {
            _panel?.Close();
            StartSandbox();
        }

        /// <summary>
        /// Resolves the exit through <see cref="FighterFlowRoutes"/> and commits the session the hub reads: it lands the hero
        /// in front of the Holodeck console and ends the Training origin. Split from <see cref="ExitToShip"/> so the route is
        /// testable without a scene change.
        /// </summary>
        public static string ApplyExitToSession() {
            GameManager manager = GameManager.Instance;
            if (manager == null) return null;
            SessionData session = manager.CurrentSession;
            string destination = FighterFlowRoutes.PauseExit(ref session);
            manager.CurrentSession = session;
            return destination;
        }

        /// <summary>Returns to the Time-Ship, in front of the Holodeck console.</summary>
        public void ExitToShip() {
            // The threaded scene load needs an unpaused tree: hand the panel's pause back before leaving.
            _panel?.Close(true);
            string destination = ApplyExitToSession();
            if (destination != null) GameManager.Instance.LoadScene(destination);
        }
    }
}
