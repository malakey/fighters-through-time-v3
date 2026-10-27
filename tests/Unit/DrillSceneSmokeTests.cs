using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A11 (F18). The shared Holodeck drill scene: it boots a real
/// <see cref="FighterSimulationDriver"/> sandbox on a Sealed stage's geometry,
/// runs the deterministic simulation with a scripted dummy injected through
/// <c>InputManager.SetInputSource</c>, rebuilds cleanly on retry, and never
/// writes <see cref="SceneTree.Paused"/> itself.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DrillSceneSmokeTests {

    [TestCase]
    public void TheDrillSceneBootsASandboxOnASealedStageWithTwoNormalizedFighters() {
        (Node host, CalibrationDrillRunner runner) = OpenDrill("DrillBootHost", drillIndex: 0);
        try {
            AssertThat(runner.CurrentDrill != null).IsTrue();
            AssertThat(runner.CurrentDrill.DrillID).IsEqual("drill_block_string");
            AssertThat(runner.Driver != null).IsTrue();
            AssertThat(runner.Driver.IsInitialized).IsTrue();

            // A Sealed stage: an unbroken floor, so no drill can end in a pit.
            FighterStageGeometry geometry = FighterStageGeometry.ForStage(CalibrationDrillRunner.DrillStageID);
            AssertThat(geometry.StageID).IsEqual(CalibrationDrillRunner.DrillStageID);
            AssertThat(geometry.IsOpenStage).IsFalse();

            // Both fighters are normalized Fighter kits: no Resonance stats, no
            // Story perks, no Legacy Unlock gate. Neither route may grant any.
            var student = runner.GetNodeOrNull<PlayerController>("DrillStudent");
            var dummy = runner.GetNodeOrNull<PlayerController>("DrillDummy");
            AssertThat(student != null).IsTrue();
            AssertThat(dummy != null).IsTrue();
            AssertThat(student.StoryAbilityPerks.Count).IsEqual(0);
            AssertThat(dummy.StoryAbilityPerks.Count).IsEqual(0);
            AssertThat(student.LegacyAbilityLocksActive).IsFalse();
            AssertThat(dummy.LegacyAbilityLocksActive).IsFalse();

            // Items and hazards are off in a drill, whatever the player's saved
            // Fighter rules say, and the sandbox never rewrites those rules.
            MatchSettings drillRules = CalibrationDrillRunner.DrillMatchSettings();
            AssertThat(drillRules.ItemsEnabled).IsFalse();
            AssertThat(drillRules.StageHazardsEnabled).IsFalse();
            AssertThat(drillRules.StockCount).IsGreater(1);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void RunningTheSandboxAdvancesTheSimulationAndNeverPausesTheTree() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool pausedBefore = tree.Paused;
        (Node host, CalibrationDrillRunner runner) = OpenDrill("DrillRunHost", drillIndex: 3);
        try {
            int startTick = runner.Driver.CurrentTick;
            Step(runner, 240);

            // The countdown (180 frames) plus live frames: the simulation really ran.
            AssertThat(runner.Driver.CurrentTick).IsGreater(startTick + 200);
            AssertThat(runner.Driver.Simulation.GetMatchState().MatchState)
                .IsEqual(FighterMatchStates.InProgress);

            // The scripted dummy holds the guard for drill 4 (Break the Guard):
            // the injected source is live on player 1.
            AssertThat(runner.Driver.TryGetFighter(1, out FighterStateComponent guard)).IsTrue();
            AssertThat(guard.Stocks).IsGreater(0);

            // A drill is not a match: nothing here may end it.
            AssertThat(runner.Driver.Simulation.GetMatchState().MatchState)
                .IsNotEqual(FighterMatchStates.Complete);

            // The runner owns no pause. LocalFighterPause / PauseMenuBase does.
            AssertThat(tree.Paused).IsEqual(pausedBefore);
        } finally {
            Teardown(host);
            tree.Paused = pausedBefore;
        }
    }

    [TestCase]
    public void RetryRebuildsTheSandboxAndResetsTheAttemptAndItsResources() {
        (Node host, CalibrationDrillRunner runner) = OpenDrill("DrillRetryHost", drillIndex: 0);
        try {
            Step(runner, 200);
            FighterSimulationDriver first = runner.Driver;
            AssertThat(runner.Driver.CurrentTick).IsGreater(100);

            runner.Retry();

            // A brand-new driver and a brand-new pair of fighters: HP, meter,
            // shield charges, cooldowns, status and position all reset together.
            AssertThat(ReferenceEquals(runner.Driver, first)).IsFalse();
            AssertThat(runner.Driver.CurrentTick).IsEqual(0);
            AssertThat(runner.CurrentDrillIndex).IsEqual(0);
            AssertThat(runner.Outcome).IsEqual(CalibrationDrillOutcome.InProgress);
            AssertThat(runner.IsResultCardVisible).IsFalse();

            AssertThat(runner.Driver.TryGetFighter(0, out FighterStateComponent student)).IsTrue();
            AssertThat(student.CurrentHP).IsEqual(student.MaxHP);
            AssertThat(student.Influence.ToFloat()).IsEqualApprox(0f, 0.001f);
            AssertThat(student.BlockCharges).IsGreater(0);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheResultCardOffersRetryListAndExitAndOnlyOffersNextWhenThereIsOne() {
        // The final drill offers the list or the exit only (F18).
        (Node host, CalibrationDrillRunner runner) =
            OpenDrill("DrillCardHost", drillIndex: CalibrationDrillCatalog.Count - 1);
        try {
            const string card = "DrillLayer/Overlay/ResultCard/Layout/Buttons/";
            foreach (string button in new[] { "RetryButton", "NextButton", "ListButton", "ExitButton" }) {
                AssertThat(runner.GetNodeOrNull<Button>(card + button) != null)
                    .OverrideFailureMessage($"The result card has no {button}")
                    .IsTrue();
            }
            AssertThat(runner.GetNode<Control>("DrillLayer/Overlay/ResultCard").Visible).IsFalse();

            AssertThat(CalibrationDrillCatalog.HasNext(runner.CurrentDrillIndex)).IsFalse();
            // NextDrill on the last drill is a no-op rather than a wrap-around.
            int before = runner.CurrentDrillIndex;
            runner.NextDrill();
            AssertThat(runner.CurrentDrillIndex).IsEqual(before);

            // The objective line is resolved copy, not a raw key.
            string objective = runner.GetNode<Label>("DrillLayer/Overlay/TopPanel/Layout/Objective").Text;
            AssertThat(objective.Length > 0).IsTrue();
            AssertThat(objective).IsNotEqual(runner.CurrentDrill.ObjectiveKey);
        } finally {
            Teardown(host);
        }
    }

    /// <summary>
    /// TG-SCREENS (UI_SPEC_GLASS 9.2): the drill list shows each drill's last result this session at its row's right, a
    /// check in hp for a pass and the cross in ink2 for a fail (shape as well as colour), and nothing for a drill not
    /// finished yet; a later result replaces an earlier one.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheListShowsEachDrillsLastResultAtItsRowsRight() {
        CalibrationDrillList.ForgetOutcomesForTest();
        CalibrationDrillList.NoteOutcome(0, CalibrationDrillOutcome.Passed);
        CalibrationDrillList.NoteOutcome(1, CalibrationDrillOutcome.Failed);
        CalibrationDrillList.NoteOutcome(1, CalibrationDrillOutcome.Passed);
        CalibrationDrillList.NoteOutcome(2, CalibrationDrillOutcome.Failed);
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = "DrillListResultHost" };
        tree.Root.AddChild(host);
        try {
            var list = ResourceLoader.Load<PackedScene>(CalibrationRoute.DrillListScenePath)
                .Instantiate<CalibrationDrillList>();
            host.AddChild(list);
            // Let the column lay out, so a row has its real width when the glyph's place is read.
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var issues = new System.Collections.Generic.List<string>();
            for (int index = 0; index < list.DrillButtons.Count; index++) {
                Button row = list.DrillButtons[index];
                var glyph = row.GetNodeOrNull<FTT.UI.Pulp.PulpIcon>(CalibrationDrillList.ResultGlyphName);
                CalibrationDrillOutcome last = CalibrationDrillList.LastOutcome(index);
                if (last == CalibrationDrillOutcome.InProgress) {
                    if (glyph != null) issues.Add($"row {index} shows a result it never had");
                    continue;
                }
                if (glyph == null) { issues.Add($"row {index} shows no {last} glyph"); continue; }
                bool passed = last == CalibrationDrillOutcome.Passed;
                if (glyph.IconName != (passed ? "check" : "cross")) issues.Add($"row {index} draws '{glyph.IconName}' for {last}");
                if (glyph.Tint != (passed ? UIPalette.Hp : UIPalette.Ink2)) issues.Add($"row {index}'s {last} glyph is {glyph.Tint}");
                if (row.Size.X <= 0f || glyph.Position.X < row.Size.X) {
                    issues.Add($"row {index}'s glyph sits at x {glyph.Position.X}, inside its {row.Size.X} px chevron, not at its right");
                }
            }
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
            AssertThat(CalibrationDrillList.LastOutcome(1)).IsEqual(CalibrationDrillOutcome.Passed);
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
            CalibrationDrillList.ForgetOutcomesForTest();
        }
    }

    // ---- helpers -------------------------------------------------------------

    private static (Node, CalibrationDrillRunner) OpenDrill(string hostName, int drillIndex) {
        if (GameManager.Instance != null) {
            SessionData session = GameManager.Instance.CurrentSession;
            session.CalibrationDrillIndex = drillIndex;
            session.CalibrationReturnScenePath = CalibrationRoute.MainMenuScenePath;
            if (string.IsNullOrWhiteSpace(session.SelectedCharacterID)) {
                session.SelectedCharacterID = CalibrationDrillRunner.DefaultStudentCharacterID;
            }
            GameManager.Instance.CurrentSession = session;
        }
        var host = new Node { Name = hostName };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var packed = ResourceLoader.Load<PackedScene>(CalibrationRoute.DrillScenePath);
        var runner = packed.Instantiate<CalibrationDrillRunner>();
        host.AddChild(runner);
        return (host, runner);
    }

    /// <summary>
    /// Drives the sandbox by hand: the driver advances the simulation, then the
    /// runner observes the frame it produced. That is the scene-tree order (parent
    /// before child is reversed here deliberately so the observation describes the
    /// frame just simulated), and it needs no scene runner.
    /// </summary>
    private static void Step(CalibrationDrillRunner runner, int frames) {
        const double delta = 1.0 / 60.0;
        for (int frame = 0; frame < frames; frame++) {
            runner.Driver._PhysicsProcess(delta);
            runner._PhysicsProcess(delta);
        }
    }

    private static void Teardown(Node host) {
        InputManager.Instance?.ClearInputSource(1);
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
