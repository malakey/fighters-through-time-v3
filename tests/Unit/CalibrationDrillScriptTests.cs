using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A11 (F18 Calibration Drills). The six drills' catalog contract,
/// their pass/fail rules, the scripted dummy, and the route helpers — all pure
/// C#, no engine. Everything the evaluator reads is an ordinary simulation
/// observable, so these rules can be exercised frame by frame without a
/// simulation and without duplicating a single combat number: the numbers stay
/// in <c>BasicComboRules</c> and the deterministic systems.
/// </summary>
[TestSuite]
public class CalibrationDrillScriptTests {

    [TestCase]
    public void TheCatalogCarriesTheDesignsSixDrillsWithTheirLocalizedCoaching() {
        AssertThat(CalibrationDrillCatalog.Count).IsEqual(6);

        // F18's six lessons, in the design's order.
        var expected = new List<string> {
            "drill_block_string", "drill_landing_tech", "drill_launch_di",
            "drill_grab_throw", "drill_echo_step", "drill_rally_reclaim"
        };
        var actual = new List<string>();
        foreach (CalibrationDrillDefinition drill in CalibrationDrillCatalog.Drills) {
            actual.Add(drill.DrillID);
        }
        AssertThat(string.Join(",", actual)).IsEqual(string.Join(",", expected));

        var issues = new List<string>();
        foreach (CalibrationDrillDefinition drill in CalibrationDrillCatalog.Drills) {
            // Each drill is pass/fail with one line of coaching either way.
            if (string.IsNullOrWhiteSpace(drill.NameKey)) issues.Add($"{drill.DrillID}: no name key");
            if (string.IsNullOrWhiteSpace(drill.ObjectiveKey)) issues.Add($"{drill.DrillID}: no objective key");
            if (string.IsNullOrWhiteSpace(drill.PassCoachKey)) issues.Add($"{drill.DrillID}: no pass coaching");
            if (string.IsNullOrWhiteSpace(drill.FailCoachKey)) issues.Add($"{drill.DrillID}: no fail coaching");
            if (drill.TimeLimitFrames <= 0) issues.Add($"{drill.DrillID}: no attempt budget");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void OnlyTheFinalDrillOffersNoNextDrillAndTheIndexIsAlwaysClamped() {
        for (int index = 0; index < CalibrationDrillCatalog.Count - 1; index++) {
            AssertThat(CalibrationDrillCatalog.HasNext(index))
                .OverrideFailureMessage($"drill {index} should offer Next Drill")
                .IsTrue();
        }
        // F18: "the final drill offers list or exit only".
        AssertThat(CalibrationDrillCatalog.HasNext(CalibrationDrillCatalog.Count - 1)).IsFalse();

        AssertThat(CalibrationDrillCatalog.ClampIndex(-4)).IsEqual(0);
        AssertThat(CalibrationDrillCatalog.ClampIndex(99)).IsEqual(CalibrationDrillCatalog.Count - 1);
        AssertThat(CalibrationDrillCatalog.IndexOf("drill_echo_step")).IsEqual(4);
        AssertThat(CalibrationDrillCatalog.IndexOf("drill_not_a_thing")).IsEqual(-1);
    }

    [TestCase]
    public void HoldTheLinePassesOnTwoBlockedHitsAndFailsOnAWholeStringEaten() {
        CalibrationDrillProgress progress = ProgressFor("drill_block_string");

        // Two non-shatter blocked hits: each arms the V7.3 shieldstun lock, and
        // the rising edge is what counts one blocked hit exactly once.
        Feed(progress, 1, s => { });
        Feed(progress, 1, s => s.StudentShieldStunFrames = 8);
        Feed(progress, 6, s => s.StudentShieldStunFrames = 4);
        AssertThat(progress.Outcome).IsEqual(CalibrationDrillOutcome.InProgress);
        Feed(progress, 1, s => s.StudentShieldStunFrames = 0);
        Feed(progress, 1, s => s.StudentShieldStunFrames = 8);
        AssertThat(progress.BlocksMade).IsEqual(2);
        AssertThat(progress.Outcome).IsEqual(CalibrationDrillOutcome.Passed);

        // Eating all three hits without blocking any of them fails the attempt.
        CalibrationDrillProgress eaten = ProgressFor("drill_block_string");
        int hp = 100;
        Feed(eaten, 1, s => s.StudentHP = hp);
        for (int hit = 0; hit < 3; hit++) {
            hp -= 6;
            int captured = hp;
            Feed(eaten, 1, s => s.StudentHP = captured);
        }
        AssertThat(eaten.Outcome).IsEqual(CalibrationDrillOutcome.Failed);
    }

    [TestCase]
    public void CatchTheGroundPassesOnATechAndCountsADroppedTumbleAsAMiss() {
        CalibrationDrillProgress progress = ProgressFor("drill_landing_tech");
        Feed(progress, 1, s => { });
        // A tumble that ends on the floor with no tech lockout is a dropped tech.
        Feed(progress, 1, s => s.StudentTumble = 1);
        Feed(progress, 1, s => { s.StudentTumble = 0; s.StudentGrounded = true; });
        AssertThat(progress.Misses).IsEqual(1);
        AssertThat(progress.Outcome).IsEqual(CalibrationDrillOutcome.InProgress);

        // TechLockoutFrames is armed by nothing but a successful landing tech.
        Feed(progress, 1, s => s.StudentTumble = 1);
        Feed(progress, 1, s => { s.StudentTechLockoutFrames = 12; s.StudentGrounded = true; });
        AssertThat(progress.Outcome).IsEqual(CalibrationDrillOutcome.Passed);
    }

    [TestCase]
    public void SteerTheLaunchNeedsADirectionHeldWhileTheLaunchIsStillPending() {
        // Undirected: the pending launch resolves with nothing held — a miss, and
        // three of them end the attempt.
        CalibrationDrillProgress undirected = ProgressFor("drill_launch_di");
        Feed(undirected, 1, s => { });
        for (int attempt = 0; attempt < 3; attempt++) {
            Feed(undirected, 4, s => s.StudentPendingLaunchActive = 1);
            Feed(undirected, 1, s => s.StudentPendingLaunchActive = 0);
        }
        AssertThat(undirected.Outcome).IsEqual(CalibrationDrillOutcome.Failed);

        CalibrationDrillProgress steered = ProgressFor("drill_launch_di");
        Feed(steered, 1, s => { });
        Feed(steered, 2, s => s.StudentPendingLaunchActive = 1);
        Feed(steered, 2, s => { s.StudentPendingLaunchActive = 1; s.StudentDirectionHeld = true; });
        AssertThat(steered.Outcome).IsEqual(CalibrationDrillOutcome.InProgress);
        Feed(steered, 1, s => s.StudentPendingLaunchActive = 0);
        AssertThat(steered.Outcome).IsEqual(CalibrationDrillOutcome.Passed);
    }

    [TestCase]
    public void BreakTheGuardPassesOnTheThrowNotTheSeizeAndEchoStepIsTwoBeats() {
        // Grab phase 4 is the 30-frame holding window; 5 is the throw. The lesson
        // is the whole triangle, so holding alone is not the pass.
        CalibrationDrillProgress grab = ProgressFor("drill_grab_throw");
        Feed(grab, 1, s => { });
        Feed(grab, 10, s => s.StudentGrabPhase = 1);
        Feed(grab, 4, s => s.StudentGrabPhase = 4);
        AssertThat(grab.Outcome).IsEqual(CalibrationDrillOutcome.InProgress);
        Feed(grab, 1, s => s.StudentGrabPhase = 5);
        AssertThat(grab.Outcome).IsEqual(CalibrationDrillOutcome.Passed);

        // Echo Step: the meter charge is beat one, the wind-up arming is the pass.
        CalibrationDrillProgress echo = ProgressFor("drill_echo_step");
        Feed(echo, 1, s => { });
        Feed(echo, 4, s => s.StudentMeter = BasicComboRules.EchoStepMeterCost - 1);
        AssertThat(echo.Stage).IsEqual(1);
        Feed(echo, 1, s => s.StudentMeter = BasicComboRules.EchoStepMeterCost);
        AssertThat(echo.Stage).IsEqual(2);
        AssertThat(echo.Outcome).IsEqual(CalibrationDrillOutcome.InProgress);
        Feed(echo, 1, s => {
            s.StudentMeter = 0;
            s.StudentEchoStepWindupFrames = BasicComboRules.EchoStepWindupFrames;
        });
        AssertThat(echo.Outcome).IsEqual(CalibrationDrillOutcome.Passed);
    }

    [TestCase]
    public void ReclaimTheEchoPassesOnHealthComingBackAndFailsWhenThePoolDrains() {
        CalibrationDrillProgress reclaimed = ProgressFor("drill_rally_reclaim");
        Feed(reclaimed, 1, s => s.StudentHP = 100);
        // The hit opens the pool; the HP floor is read at that moment.
        Feed(reclaimed, 1, s => { s.StudentHP = 88; s.StudentEchoPool = 12f; });
        Feed(reclaimed, 5, s => { s.StudentHP = 88; s.StudentEchoPool = 9f; });
        AssertThat(reclaimed.Outcome).IsEqual(CalibrationDrillOutcome.InProgress);
        // Only a reclaim raises a fighter's HP mid-attempt.
        Feed(reclaimed, 1, s => { s.StudentHP = 96; s.StudentEchoPool = 1f; });
        AssertThat(reclaimed.Outcome).IsEqual(CalibrationDrillOutcome.Passed);

        CalibrationDrillProgress drained = ProgressFor("drill_rally_reclaim");
        Feed(drained, 1, s => s.StudentHP = 100);
        for (int attempt = 0; attempt < 3; attempt++) {
            Feed(drained, 1, s => { s.StudentHP = 88; s.StudentEchoPool = 12f; });
            Feed(drained, 1, s => { s.StudentHP = 88; s.StudentEchoPool = 0f; });
        }
        AssertThat(drained.Outcome).IsEqual(CalibrationDrillOutcome.Failed);
    }

    [TestCase]
    public void AnAttemptFailsOnItsBudgetAndAResetClearsEveryCounter() {
        CalibrationDrillProgress progress = ProgressFor("drill_grab_throw");
        var sample = new CalibrationDrillSample { Live = true, Frame = 0 };
        progress.Observe(in sample);
        sample.Frame = CalibrationDrillCatalog.DefaultTimeLimitFrames;
        AssertThat(progress.Observe(in sample)).IsEqual(CalibrationDrillOutcome.Failed);

        // Retry is a clean slate: the outcome, the misses and the stage all reset.
        progress.Reset();
        AssertThat(progress.Outcome).IsEqual(CalibrationDrillOutcome.InProgress);
        AssertThat(progress.Misses).IsEqual(0);
        AssertThat(progress.Stage).IsEqual(1);

        // Countdown frames are ignored outright, so a countdown can never burn the
        // attempt budget or bank a stray observation.
        CalibrationDrillProgress blocked = ProgressFor("drill_block_string");
        var countdown = new CalibrationDrillSample { Live = false, StudentShieldStunFrames = 8 };
        blocked.Observe(in countdown);
        countdown.StudentShieldStunFrames = 0;
        blocked.Observe(in countdown);
        AssertThat(blocked.BlocksMade).IsEqual(0);
    }

    [TestCase]
    public void TheScriptedDummyWalksInSwingsItsStringGuardsOrPokesOnce() {
        // Out of range: walk toward the student, never swing.
        var far = new CalibrationDrillDummyContext {
            Frame = 0, SignedDistanceX = 5f, DummyCanAct = true
        };
        CalibrationDrillDummyCommand approach =
            CalibrationDrillDummyScript.Step(CalibrationDrillDummyRole.StringThrower, in far);
        AssertThat(approach.MoveX).IsEqual(1f);
        AssertThat((approach.Held & GameplayButtons.BasicAttack) != 0).IsFalse();

        // In range on a swing beat: stop and press Attack.
        var close = new CalibrationDrillDummyContext {
            Frame = 0, SignedDistanceX = 0.5f, DummyCanAct = true
        };
        CalibrationDrillDummyCommand swing =
            CalibrationDrillDummyScript.Step(CalibrationDrillDummyRole.StringThrower, in close);
        AssertThat(swing.MoveX).IsEqual(0f);
        AssertThat((swing.Held & GameplayButtons.BasicAttack) != 0).IsTrue();

        // Three swings per string, then a rest — the scripted string, not an AI.
        int swings = 0;
        for (int frame = 0; frame < CalibrationDrillDummyScript.StringSwingIntervalFrames * 3
                 + CalibrationDrillDummyScript.StringRestFrames; frame++) {
            var context = new CalibrationDrillDummyContext {
                Frame = frame, SignedDistanceX = 0.5f, DummyCanAct = true
            };
            CalibrationDrillDummyCommand command =
                CalibrationDrillDummyScript.Step(CalibrationDrillDummyRole.StringThrower, in context);
            if ((command.Held & GameplayButtons.BasicAttack) != 0) swings++;
        }
        AssertThat(swings).IsEqual(3 * CalibrationDrillDummyScript.ButtonHoldFrames);

        // The guard never drops its Block and never swings.
        CalibrationDrillDummyCommand guard =
            CalibrationDrillDummyScript.Step(CalibrationDrillDummyRole.Blocker, in close);
        AssertThat((guard.Held & GameplayButtons.Block) != 0).IsTrue();
        AssertThat((guard.Held & GameplayButtons.BasicAttack) != 0).IsFalse();

        // The Rally poke stops entirely once its single hit has landed.
        var poked = new CalibrationDrillDummyContext {
            Frame = 0, SignedDistanceX = 0.5f, DummyCanAct = true, StudentStartHP = 100, StudentHP = 92
        };
        CalibrationDrillDummyCommand after =
            CalibrationDrillDummyScript.Step(CalibrationDrillDummyRole.SinglePoke, in poked);
        AssertThat(after.Held).IsEqual(GameplayButtons.None);
        AssertThat(after.MoveX).IsEqual(0f);

        // A dummy in hitstun holds nothing at all.
        var stunned = new CalibrationDrillDummyContext {
            Frame = 0, SignedDistanceX = 0.5f, DummyCanAct = false
        };
        AssertThat(CalibrationDrillDummyScript.Step(CalibrationDrillDummyRole.Blocker, in stunned).Held)
            .IsEqual(GameplayButtons.None);
    }

    [TestCase]
    public void TheDummySourceTurnsAHeldCommandIntoARealButtonEdge() {
        var source = new CalibrationDrillDummySource();
        source.SetCommand(new CalibrationDrillDummyCommand { Held = GameplayButtons.BasicAttack });
        PlayerInputFrame first = source.Sample(10, default);
        AssertThat(first.IsPressed(GameplayButtons.BasicAttack)).IsTrue();

        // Held across a second tick: still held, no longer a press. A permanently
        // set bit with no edge would never start a swing in the simulation.
        PlayerInputFrame second = source.Sample(11, first);
        AssertThat(second.IsHeld(GameplayButtons.BasicAttack)).IsTrue();
        AssertThat(second.IsPressed(GameplayButtons.BasicAttack)).IsFalse();

        source.SetCommand(new CalibrationDrillDummyCommand());
        PlayerInputFrame third = source.Sample(12, second);
        AssertThat(third.IsReleased(GameplayButtons.BasicAttack)).IsTrue();
    }

    [TestCase]
    public void TheRouteSendsTheMenuRouteBackToThePickerAndTheHubRouteBackToTheHub() {
        // Standalone route: Back from the list returns to the picker, Exit leaves.
        AssertThat(CalibrationRoute.ExitDestination(CalibrationRoute.MainMenuScenePath))
            .IsEqual(CalibrationRoute.MainMenuScenePath);
        AssertThat(CalibrationRoute.DrillListBackDestination(CalibrationRoute.MainMenuScenePath))
            .IsEqual(CalibrationRoute.PickerScenePath);
        AssertThat(CalibrationRoute.UsesPicker(CalibrationRoute.MainMenuScenePath)).IsTrue();

        // Hub console route: no picker exists, so Back and Exit both go to the hub.
        AssertThat(CalibrationRoute.ExitDestination(CalibrationRoute.HubScenePath))
            .IsEqual(CalibrationRoute.HubScenePath);
        AssertThat(CalibrationRoute.DrillListBackDestination(CalibrationRoute.HubScenePath))
            .IsEqual(CalibrationRoute.HubScenePath);
        AssertThat(CalibrationRoute.UsesPicker(CalibrationRoute.HubScenePath)).IsFalse();

        // An unset marker degrades to the menu rather than stranding the player.
        AssertThat(CalibrationRoute.ExitDestination("")).IsEqual(CalibrationRoute.MainMenuScenePath);
        AssertThat(CalibrationRoute.ExitDestination(null)).IsEqual(CalibrationRoute.MainMenuScenePath);

        // Leaving disarms the route, so an ordinary Fighter pause afterwards still
        // exits to the Fighter lobby and a relaunch can never resume a drill.
        var session = new SessionData {
            CalibrationReturnScenePath = CalibrationRoute.HubScenePath,
            CalibrationDrillIndex = 4
        };
        AssertThat(CalibrationRoute.IsActive(in session)).IsTrue();
        SessionData cleared = CalibrationRoute.Cleared(session);
        AssertThat(CalibrationRoute.IsActive(in cleared)).IsFalse();
        AssertThat(cleared.CalibrationDrillIndex).IsEqual(0);
    }

    // ---- helpers -------------------------------------------------------------

    private static readonly Dictionary<CalibrationDrillProgress, int> Clocks = new();

    private static CalibrationDrillProgress ProgressFor(string drillID) {
        var progress = new CalibrationDrillProgress(
            CalibrationDrillCatalog.At(CalibrationDrillCatalog.IndexOf(drillID)));
        Clocks[progress] = 0;
        return progress;
    }

    /// <summary>
    /// Feeds <paramref name="frames"/> live frames shaped by
    /// <paramref name="shape"/>, advancing that attempt's own clock one frame at a
    /// time so no test can inherit another's frame counter.
    /// </summary>
    private static void Feed(
        CalibrationDrillProgress progress,
        int frames,
        System.Action<Shaper> shape) {
        for (int index = 0; index < frames; index++) {
            var shaper = new Shaper();
            shape(shaper);
            CalibrationDrillSample sample = shaper.Sample;
            sample.Live = true;
            sample.Frame = ++Clocks[progress];
            progress.Observe(in sample);
        }
    }

    /// <summary>Mutable wrapper so a test can shape one frame with a lambda.</summary>
    private sealed class Shaper {
        public CalibrationDrillSample Sample;
        public int StudentHP { set => Sample.StudentHP = value; }
        public int StudentShieldStunFrames { set => Sample.StudentShieldStunFrames = value; }
        public int StudentTumble { set => Sample.StudentTumble = value; }
        public int StudentTechLockoutFrames { set => Sample.StudentTechLockoutFrames = value; }
        public int StudentPendingLaunchActive { set => Sample.StudentPendingLaunchActive = value; }
        public bool StudentDirectionHeld { set => Sample.StudentDirectionHeld = value; }
        public bool StudentGrounded { set => Sample.StudentGrounded = value; }
        public int StudentGrabPhase { set => Sample.StudentGrabPhase = value; }
        public int StudentMeter { set => Sample.StudentMeter = value; }
        public int StudentEchoStepWindupFrames { set => Sample.StudentEchoStepWindupFrames = value; }
        public float StudentEchoPool { set => Sample.StudentEchoPool = value; }
    }
}
