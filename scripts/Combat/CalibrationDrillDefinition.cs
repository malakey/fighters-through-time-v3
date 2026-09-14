using System;
using System.Collections.Generic;
using FTT.Core;

namespace FTT.Combat {

    /// <summary>Where a drill attempt stands after the latest observed frame.</summary>
    public enum CalibrationDrillOutcome {
        InProgress,
        Passed,
        Failed
    }

    /// <summary>
    /// One 60 Hz observation of a drill attempt, projected out of the deterministic
    /// simulation by <c>CalibrationDrillRunner</c>. Deliberately a flat struct of
    /// primitives: the evaluation below is pure C# with no Godot and no
    /// <c>FTT.FighterSim</c> types, so every pass/fail rule is unit-testable
    /// without an engine.
    /// </summary>
    public struct CalibrationDrillSample {
        /// <summary>Frames elapsed since the attempt went live (post-countdown).</summary>
        public int Frame;
        /// <summary>False while the match countdown is still running.</summary>
        public bool Live;

        // --- the student (player 0) -----------------------------------------
        public int StudentHP;
        public int StudentBlockCharges;
        public int StudentShieldStunFrames;
        public int StudentTumble;
        public int StudentTechLockoutFrames;
        public int StudentPendingLaunchActive;
        public int StudentGrabPhase;
        public int StudentEchoStepWindupFrames;
        public int StudentMeter;
        public float StudentEchoPool;
        public bool StudentGrounded;
        /// <summary>True when the student is holding a non-neutral stick this frame.</summary>
        public bool StudentDirectionHeld;

        // --- the dummy (player 1) -------------------------------------------
        public int DummyHP;
    }

    /// <summary>
    /// One Holodeck Calibration Drill: its identity, its localized copy, and the
    /// attempt budget. The pass/fail rule itself lives in
    /// <see cref="CalibrationDrillProgress"/> so the definition stays plain data.
    ///
    /// <para>F18 (design-godot.md §7 "Holodeck Guided Drills"): six drills, each
    /// pass/fail with one line of coaching. The catalog order is the design's
    /// order and is also the "Next Drill" order.</para>
    /// </summary>
    public sealed class CalibrationDrillDefinition {
        /// <summary>Stable identity. Never localized, never renumbered.</summary>
        public string DrillID { get; }
        public string NameKey { get; }
        public string ObjectiveKey { get; }
        /// <summary>
        /// Second-stage objective for a two-beat drill (Echo Step's "charge the
        /// meter, then fold the recovery"), or null when the drill has one beat.
        /// </summary>
        public string ObjectiveStageTwoKey { get; }
        public string PassCoachKey { get; }
        public string FailCoachKey { get; }
        /// <summary>Attempt budget in 60 Hz frames; exceeding it fails the attempt.</summary>
        public int TimeLimitFrames { get; }
        /// <summary>Which scripted dummy behaviour this drill runs.</summary>
        public CalibrationDrillDummyRole DummyRole { get; }

        internal CalibrationDrillDefinition(
            string drillID,
            string nameKey,
            string objectiveKey,
            string passCoachKey,
            string failCoachKey,
            int timeLimitFrames,
            CalibrationDrillDummyRole dummyRole,
            string objectiveStageTwoKey = null) {
            DrillID = drillID;
            NameKey = nameKey;
            ObjectiveKey = objectiveKey;
            PassCoachKey = passCoachKey;
            FailCoachKey = failCoachKey;
            TimeLimitFrames = timeLimitFrames;
            DummyRole = dummyRole;
            ObjectiveStageTwoKey = objectiveStageTwoKey;
        }
    }

    /// <summary>The scripted behaviours the sparring dummy runs, one per drill.</summary>
    public enum CalibrationDrillDummyRole {
        /// <summary>Walks in and throws the full three-hit string on a loop.</summary>
        StringThrower,
        /// <summary>Stands still holding Block: the grab/throw lesson.</summary>
        Blocker,
        /// <summary>Stands still and does nothing: a swing bag.</summary>
        Idle,
        /// <summary>Walks in, lands exactly one hit, then stands still.</summary>
        SinglePoke
    }

    /// <summary>
    /// The six Calibration Drills (F18). Static, engine-free, and the single source
    /// of drill identity for the list screen, the runner, and the content tests.
    /// </summary>
    public static class CalibrationDrillCatalog {

        /// <summary>One minute per attempt, on every drill.</summary>
        public const int DefaultTimeLimitFrames = 3600;

        /// <summary>Misses tolerated before an attempt is failed outright.</summary>
        public const int AllowedMisses = 3;

        private static readonly CalibrationDrillDefinition[] DrillList = {
            new(
                drillID: "drill_block_string",
                nameKey: "drill_block_string_name",
                objectiveKey: "drill_block_string_objective",
                passCoachKey: "drill_block_string_pass",
                failCoachKey: "drill_block_string_fail",
                timeLimitFrames: DefaultTimeLimitFrames,
                dummyRole: CalibrationDrillDummyRole.StringThrower),
            new(
                drillID: "drill_landing_tech",
                nameKey: "drill_landing_tech_name",
                objectiveKey: "drill_landing_tech_objective",
                passCoachKey: "drill_landing_tech_pass",
                failCoachKey: "drill_landing_tech_fail",
                timeLimitFrames: DefaultTimeLimitFrames,
                dummyRole: CalibrationDrillDummyRole.StringThrower),
            new(
                drillID: "drill_launch_di",
                nameKey: "drill_launch_di_name",
                objectiveKey: "drill_launch_di_objective",
                passCoachKey: "drill_launch_di_pass",
                failCoachKey: "drill_launch_di_fail",
                timeLimitFrames: DefaultTimeLimitFrames,
                dummyRole: CalibrationDrillDummyRole.StringThrower),
            new(
                drillID: "drill_grab_throw",
                nameKey: "drill_grab_throw_name",
                objectiveKey: "drill_grab_throw_objective",
                passCoachKey: "drill_grab_throw_pass",
                failCoachKey: "drill_grab_throw_fail",
                timeLimitFrames: DefaultTimeLimitFrames,
                dummyRole: CalibrationDrillDummyRole.Blocker),
            new(
                drillID: "drill_echo_step",
                nameKey: "drill_echo_step_name",
                objectiveKey: "drill_echo_step_objective",
                passCoachKey: "drill_echo_step_pass",
                failCoachKey: "drill_echo_step_fail",
                timeLimitFrames: DefaultTimeLimitFrames,
                dummyRole: CalibrationDrillDummyRole.Idle,
                objectiveStageTwoKey: "drill_echo_step_objective_two"),
            new(
                drillID: "drill_rally_reclaim",
                nameKey: "drill_rally_reclaim_name",
                objectiveKey: "drill_rally_reclaim_objective",
                passCoachKey: "drill_rally_reclaim_pass",
                failCoachKey: "drill_rally_reclaim_fail",
                timeLimitFrames: DefaultTimeLimitFrames,
                dummyRole: CalibrationDrillDummyRole.SinglePoke)
        };

        public static IReadOnlyList<CalibrationDrillDefinition> Drills => DrillList;

        public static int Count => DrillList.Length;

        /// <summary>Clamps an arbitrary index onto the catalog.</summary>
        public static int ClampIndex(int index) =>
            index < 0 ? 0 : index >= DrillList.Length ? DrillList.Length - 1 : index;

        public static CalibrationDrillDefinition At(int index) => DrillList[ClampIndex(index)];

        /// <summary>
        /// Whether a "Next Drill" button belongs on the result card. The final
        /// drill offers the list and the exit only (F18).
        /// </summary>
        public static bool HasNext(int index) => index >= 0 && index < DrillList.Length - 1;

        public static int IndexOf(string drillID) {
            for (int index = 0; index < DrillList.Length; index++) {
                if (DrillList[index].DrillID == drillID) return index;
            }
            return -1;
        }
    }

    /// <summary>
    /// The pass/fail state machine for one drill attempt. Pure C#: it is fed one
    /// <see cref="CalibrationDrillSample"/> per simulation frame and reports the
    /// attempt's standing. Everything it reads is an ordinary simulation
    /// observable, so no drill rule duplicates a combat number — the numbers stay
    /// in <see cref="BasicComboRules"/> and the deterministic systems.
    /// </summary>
    public sealed class CalibrationDrillProgress {

        private readonly CalibrationDrillDefinition _definition;
        private CalibrationDrillSample _previous;
        private bool _hasPrevious;

        private int _blocksMade;
        private int _hitsTaken;
        private int _misses;
        private bool _diHeldThisLaunch;
        private bool _echoSeen;
        private int _hpWhenEchoOpened;
        private CalibrationDrillOutcome _outcome = CalibrationDrillOutcome.InProgress;

        public CalibrationDrillProgress(CalibrationDrillDefinition definition) {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public CalibrationDrillDefinition Definition => _definition;
        public CalibrationDrillOutcome Outcome => _outcome;
        /// <summary>Misses recorded so far (a dropped tech, an undirected launch, a drained echo).</summary>
        public int Misses => _misses;
        /// <summary>Non-shatter blocked hits observed. Test surface for drill 1.</summary>
        public int BlocksMade => _blocksMade;
        /// <summary>
        /// 1 for a single-beat drill; 2 once a two-beat drill's first beat is done
        /// (Echo Step's meter charge). Drives which objective line the HUD shows.
        /// </summary>
        public int Stage { get; private set; } = 1;

        /// <summary>Clears every counter. Called on start and on retry.</summary>
        public void Reset() {
            _previous = default;
            _hasPrevious = false;
            _blocksMade = 0;
            _hitsTaken = 0;
            _misses = 0;
            _diHeldThisLaunch = false;
            _echoSeen = false;
            _hpWhenEchoOpened = 0;
            _outcome = CalibrationDrillOutcome.InProgress;
            Stage = 1;
        }

        /// <summary>
        /// Folds one frame into the attempt and returns the standing outcome.
        /// Frames observed before the attempt goes live (the match countdown) are
        /// ignored outright, so a countdown can never burn the attempt budget.
        /// </summary>
        public CalibrationDrillOutcome Observe(in CalibrationDrillSample sample) {
            if (_outcome != CalibrationDrillOutcome.InProgress) return _outcome;
            if (!sample.Live) {
                _previous = sample;
                _hasPrevious = true;
                return _outcome;
            }
            if (!_hasPrevious) {
                _previous = sample;
                _hasPrevious = true;
                _hpWhenEchoOpened = sample.StudentHP;
                return _outcome;
            }

            switch (_definition.DrillID) {
                case "drill_block_string": ObserveBlockString(in sample); break;
                case "drill_landing_tech": ObserveLandingTech(in sample); break;
                case "drill_launch_di": ObserveLaunchDi(in sample); break;
                case "drill_grab_throw": ObserveGrabThrow(in sample); break;
                case "drill_echo_step": ObserveEchoStep(in sample); break;
                case "drill_rally_reclaim": ObserveRallyReclaim(in sample); break;
            }

            if (_outcome == CalibrationDrillOutcome.InProgress
                && _misses >= CalibrationDrillCatalog.AllowedMisses) {
                _outcome = CalibrationDrillOutcome.Failed;
            }
            if (_outcome == CalibrationDrillOutcome.InProgress
                && sample.Frame >= _definition.TimeLimitFrames) {
                _outcome = CalibrationDrillOutcome.Failed;
            }

            _previous = sample;
            return _outcome;
        }

        // ---- per-drill rules -------------------------------------------------

        /// <summary>
        /// Drill 1 — hold the line. A non-shatter blocked hit is the frame the
        /// blocker's shieldstun lock arms (V7.3 block model), so the rise of
        /// <c>ShieldStunFrames</c> counts one blocked hit exactly once. Two blocked
        /// hits pass; eating the whole three-hit string without blocking fails.
        /// </summary>
        private void ObserveBlockString(in CalibrationDrillSample sample) {
            if (_previous.StudentShieldStunFrames == 0 && sample.StudentShieldStunFrames > 0) _blocksMade++;
            if (sample.StudentHP < _previous.StudentHP) _hitsTaken++;
            if (_blocksMade >= 2) {
                _outcome = CalibrationDrillOutcome.Passed;
                return;
            }
            // Three connected hits with nothing blocked is the whole string eaten.
            if (_hitsTaken >= 3 && _blocksMade == 0) _misses = CalibrationDrillCatalog.AllowedMisses;
        }

        /// <summary>
        /// Drill 2 — catch the ground. A successful landing tech is the only thing
        /// that arms <c>TechLockoutFrames</c>; a tumble that ends on the floor
        /// without one is a dropped tech.
        /// </summary>
        private void ObserveLandingTech(in CalibrationDrillSample sample) {
            if (_previous.StudentTechLockoutFrames == 0 && sample.StudentTechLockoutFrames > 0) {
                _outcome = CalibrationDrillOutcome.Passed;
                return;
            }
            if (_previous.StudentTumble != 0
                && sample.StudentTumble == 0
                && sample.StudentGrounded
                && sample.StudentTechLockoutFrames == 0) {
                _misses++;
            }
        }

        /// <summary>
        /// Drill 3 — steer the launch. A launching hit parks its velocity behind
        /// <c>PendingLaunchActive</c> for the hitstop; DI is whatever direction is
        /// held when that resolves.
        /// </summary>
        private void ObserveLaunchDi(in CalibrationDrillSample sample) {
            if (sample.StudentPendingLaunchActive != 0) {
                if (sample.StudentDirectionHeld) _diHeldThisLaunch = true;
                return;
            }
            if (_previous.StudentPendingLaunchActive == 0) return;
            if (_diHeldThisLaunch) {
                _outcome = CalibrationDrillOutcome.Passed;
                return;
            }
            _diHeldThisLaunch = false;
            _misses++;
        }

        /// <summary>
        /// Drill 4 — break the guard. Grab beats block, and the lesson is the whole
        /// triangle, so the pass is the throw, not the seize: phase 5 is the throw
        /// animation.
        /// </summary>
        private void ObserveGrabThrow(in CalibrationDrillSample sample) {
            const int throwAnimationPhase = 5;
            if (_previous.StudentGrabPhase != throwAnimationPhase
                && sample.StudentGrabPhase == throwAnimationPhase) {
                _outcome = CalibrationDrillOutcome.Passed;
            }
        }

        /// <summary>
        /// Drill 5 — fold the recovery. Two beats: charge Influence to the Echo Step
        /// cost by attacking the bag, then spend it out of a swing's recovery
        /// frames. The wind-up arming is the unambiguous "it fired" observable.
        /// </summary>
        private void ObserveEchoStep(in CalibrationDrillSample sample) {
            if (Stage == 1 && sample.StudentMeter >= BasicComboRules.EchoStepMeterCost) Stage = 2;
            if (_previous.StudentEchoStepWindupFrames == 0 && sample.StudentEchoStepWindupFrames > 0) {
                _outcome = CalibrationDrillOutcome.Passed;
            }
        }

        /// <summary>
        /// Drill 6 — reclaim the echo. Taking a hit opens an Echo pool that drains
        /// over <see cref="BasicComboRules.EchoDrainFrames"/>; landing a hit before
        /// it empties converts the remainder back into HP. The only thing that
        /// raises a fighter's HP mid-attempt is a reclaim, so an HP rise above the
        /// post-hit value is the pass.
        /// </summary>
        private void ObserveRallyReclaim(in CalibrationDrillSample sample) {
            if (!_echoSeen && sample.StudentEchoPool > 0f) {
                _echoSeen = true;
                _hpWhenEchoOpened = sample.StudentHP;
                return;
            }
            if (!_echoSeen) return;
            if (sample.StudentHP > _hpWhenEchoOpened) {
                _outcome = CalibrationDrillOutcome.Passed;
                return;
            }
            // Track further damage so a second hit re-bases the floor rather than
            // making the pass unreachable.
            if (sample.StudentHP < _hpWhenEchoOpened) _hpWhenEchoOpened = sample.StudentHP;
            if (_previous.StudentEchoPool > 0f && sample.StudentEchoPool <= 0f) {
                _echoSeen = false;
                _misses++;
            }
        }
    }

    /// <summary>
    /// The Calibration Drills route (F18). Two entry points share one drill list
    /// and one drill scene, and they differ only in where "Exit Calibration" goes:
    /// the standalone Main Menu route and the Story hub's Holodeck console route.
    /// The destination is carried in
    /// <c>GameManager.CurrentSession.CalibrationReturnScenePath</c>; this type owns
    /// the three scene paths and the two routing rules so no screen re-derives them.
    /// </summary>
    public static class CalibrationRoute {

        public const string MainMenuScenePath = "res://scenes/menus/MainMenu.tscn";
        public const string HubScenePath = "res://scenes/campaign/HubWorld.tscn";
        public const string PickerScenePath = "res://scenes/ui/CalibrationDrillPicker.tscn";
        public const string DrillListScenePath = "res://scenes/ui/CalibrationDrillList.tscn";
        public const string DrillScenePath = "res://scenes/ui/HolodeckDrill.tscn";

        /// <summary>
        /// Where "Exit Calibration" and a Back out of the route land. An empty or
        /// unset return path degrades to the Main Menu rather than stranding the
        /// player: a drill is never a campaign attempt, so the menu is always safe.
        /// </summary>
        public static string ExitDestination(string returnScenePath) =>
            string.IsNullOrWhiteSpace(returnScenePath) ? MainMenuScenePath : returnScenePath;

        /// <summary>
        /// Where Back from the drill list goes. The standalone route came through
        /// the character picker, so Back returns to it; the hub console route has
        /// no picker (the campaign character is locked), so Back returns to the hub.
        /// </summary>
        public static string DrillListBackDestination(string returnScenePath) =>
            ExitDestination(returnScenePath) == MainMenuScenePath
                ? PickerScenePath
                : ExitDestination(returnScenePath);

        /// <summary>True when this route showed the standalone character picker.</summary>
        public static bool UsesPicker(string returnScenePath) =>
            ExitDestination(returnScenePath) == MainMenuScenePath;

        /// <summary>
        /// Disarms the route. Every path that leaves calibration clears the marker
        /// on its way out, so an ordinary Fighter match pause afterwards still exits
        /// to the Fighter lobby and a relaunch can never resume a drill.
        /// </summary>
        public static SessionData Cleared(SessionData session) {
            session.CalibrationReturnScenePath = "";
            session.CalibrationDrillIndex = 0;
            return session;
        }

        /// <summary>True while a calibration route is armed on this session.</summary>
        public static bool IsActive(in SessionData session) =>
            !string.IsNullOrWhiteSpace(session.CalibrationReturnScenePath);
    }

    /// <summary>What the scripted dummy is holding for one tick.</summary>
    public struct CalibrationDrillDummyCommand {
        public float MoveX;
        public GameplayButtons Held;
    }

    /// <summary>The world state the dummy script reacts to.</summary>
    public struct CalibrationDrillDummyContext {
        /// <summary>Frames since the attempt went live.</summary>
        public int Frame;
        /// <summary>Student position minus dummy position, in world units.</summary>
        public float SignedDistanceX;
        public bool DummyCanAct;
        public int StudentHP;
        /// <summary>Student HP at the moment the attempt went live.</summary>
        public int StudentStartHP;
    }

    /// <summary>
    /// The scripted sparring dummy. Pure: it maps a world context onto held
    /// buttons, and the Godot side turns that into an injected
    /// <see cref="PlayerInputFrame"/>. Level 0's calibration scripting is the
    /// pattern — a fixed beat schedule rather than an AI.
    /// </summary>
    public static class CalibrationDrillDummyScript {

        /// <summary>How close the dummy walks before it swings, in world units.</summary>
        public const float SwingRangeUnits = 1.1f;

        /// <summary>How close the dummy stands for the grab lesson.</summary>
        public const float GuardRangeUnits = 0.7f;

        /// <summary>Frames between the three swings of a scripted string.</summary>
        public const int StringSwingIntervalFrames = 20;

        /// <summary>Frames the dummy waits between strings.</summary>
        public const int StringRestFrames = 90;

        /// <summary>Frames a scripted button is held so the edge is unambiguous.</summary>
        public const int ButtonHoldFrames = 3;

        private const int StringCycleFrames =
            StringSwingIntervalFrames * 3 + StringRestFrames;

        public static CalibrationDrillDummyCommand Step(
            CalibrationDrillDummyRole role,
            in CalibrationDrillDummyContext context) {
            var command = new CalibrationDrillDummyCommand();
            if (!context.DummyCanAct) return command;

            switch (role) {
                case CalibrationDrillDummyRole.StringThrower:
                    return StepStringThrower(in context);
                case CalibrationDrillDummyRole.Blocker:
                    command.MoveX = Approach(context.SignedDistanceX, GuardRangeUnits);
                    command.Held = GameplayButtons.Block;
                    return command;
                case CalibrationDrillDummyRole.SinglePoke:
                    return StepSinglePoke(in context);
                default:
                    return command;
            }
        }

        private static CalibrationDrillDummyCommand StepStringThrower(
            in CalibrationDrillDummyContext context) {
            var command = new CalibrationDrillDummyCommand {
                MoveX = Approach(context.SignedDistanceX, SwingRangeUnits)
            };
            if (Math.Abs(context.SignedDistanceX) > SwingRangeUnits) return command;

            int phase = context.Frame % StringCycleFrames;
            for (int swing = 0; swing < 3; swing++) {
                int start = swing * StringSwingIntervalFrames;
                if (phase >= start && phase < start + ButtonHoldFrames) {
                    command.Held |= GameplayButtons.BasicAttack;
                    break;
                }
            }
            return command;
        }

        /// <summary>
        /// One hit, then stillness. The Rally lesson needs the student to have a
        /// live Echo pool and an unguarded target to cash it against.
        /// </summary>
        private static CalibrationDrillDummyCommand StepSinglePoke(
            in CalibrationDrillDummyContext context) {
            if (context.StudentHP < context.StudentStartHP) return default;
            var command = new CalibrationDrillDummyCommand {
                MoveX = Approach(context.SignedDistanceX, SwingRangeUnits)
            };
            if (Math.Abs(context.SignedDistanceX) > SwingRangeUnits) return command;
            if (context.Frame % StringSwingIntervalFrames < ButtonHoldFrames) {
                command.Held |= GameplayButtons.BasicAttack;
            }
            return command;
        }

        /// <summary>Walks toward the student and stops at the requested spacing.</summary>
        public static float Approach(float signedDistanceX, float stopRange) {
            if (Math.Abs(signedDistanceX) <= stopRange) return 0f;
            return signedDistanceX > 0f ? 1f : -1f;
        }
    }

    /// <summary>
    /// Injects the scripted dummy's held buttons as player 1's input frame through
    /// <c>InputManager.SetInputSource</c> — the same seam the Mirror Paradox clone
    /// uses. Pressed/Released edges are derived from the previous frame so the
    /// simulation sees a real button edge, never a permanently-held bit.
    /// </summary>
    public sealed class CalibrationDrillDummySource : IPlayerInputSource {
        private CalibrationDrillDummyCommand _command;

        public void SetCommand(in CalibrationDrillDummyCommand command) => _command = command;

        public CalibrationDrillDummyCommand Command => _command;

        public PlayerInputFrame Sample(uint tick, in PlayerInputFrame previousFrame) =>
            PlayerInputFrame.Create(tick, _command.MoveX, 0f, _command.Held, previousFrame.Held);
    }
}
