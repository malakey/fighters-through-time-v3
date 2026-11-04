using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Design Section 10.3 "In-Hub Configuration Surface": the Holodeck console
/// opens a compact configuration panel in the hub — CPU difficulty, CPU
/// character, stage, and rules — then launches straight into the match, never
/// through the three-screen Fighter select.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class HolodeckConsoleTests {

    private static (Node host, HolodeckConsolePanel panel) CreatePanel(string hostName) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = hostName };
        tree.Root.AddChild(host);
        var panel = new HolodeckConsolePanel { Name = "HolodeckConsolePanel" };
        host.AddChild(panel);
        return (host, panel);
    }

    [TestCase]
    public void TheConsoleOffersDifficultyRosterStageAndRules() {
        (Node host, HolodeckConsolePanel panel) = CreatePanel("HolodeckPopulateHost");
        try {
            AssertThat(panel.CpuDifficultySelect.ItemCount).IsEqual(3);
            // Package 11 A6b: the CPU list is built from the manifest roster.
            AssertThat(panel.CpuCharacterSelect.ItemCount)
                .IsEqual(FTT.Core.CharacterRoster.Count + 1); // + the G15a Random entry
            AssertThat(panel.StageSelect.ItemCount).IsGreater(0);
            AssertThat(panel.StageSelect.Disabled).IsFalse();
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// TG-CLOSE polish: a stage and its Open / Sealed layout are joined by the spaced em dash ("Florence Workshop — Sealed"),
    /// as HUD_CONTRACT and the rest of the copy write it, through one translation format key; it was a hyphen joined in code.
    /// </summary>
    [TestCase]
    public void TheStageListJoinsEachStageAndItsLayoutWithASpacedEmDash() {
        (Node host, HolodeckConsolePanel panel) = CreatePanel("HolodeckStageDashHost");
        var problems = new System.Collections.Generic.List<string>();
        try {
            OptionButton stages = panel.StageSelect;
            for (int index = 0; index < stages.ItemCount; index++) {
                if (stages.GetItemId(index) == HolodeckConsolePanel.RandomItemID) continue;
                string text = stages.GetItemText(index);
                if (!text.Contains(" — ") || text.Contains(" - ")) problems.Add($"'{text}'");
            }
            AssertThat(stages.ItemCount).IsGreater(1);
        } finally {
            host.Free();
        }
        if (problems.Count > 0) AssertThat("not joined by ' — ': " + string.Join(", ", problems)).IsEqual("");
    }

    [TestCase]
    public void LaunchWritesACpuPracticeSessionThatReturnsToTheHub() {
        GameManager gameManager = GameManager.Instance;
        AssertObject(gameManager).IsNotNull();
        SessionData savedSession = gameManager.CurrentSession;
        SavedMatchSettings savedRules = SaveManager.Instance?.GlobalData?.LastMatchSettings;

        (Node host, HolodeckConsolePanel panel) = CreatePanel("HolodeckLaunchHost");
        try {
            panel.CpuDifficultySelect.Select((int)CpuDifficulty.Hard);
            panel.CpuCharacterSelect.Select(1); // joan
            panel.GetNode<SpinBox>("Center/Tagged/Panel/Layout/RulesRow/StockCount").Value = 5;
            panel.GetNode<OptionButton>("Center/Tagged/Panel/Layout/ItemFrequencyRow/ItemFrequency").Select(0); // Off

            FTT.Environment.FighterStageData stage = panel.ApplyToSession();
            AssertObject(stage).IsNotNull();

            SessionData session = gameManager.CurrentSession;
            AssertThat(session.FighterOpponentType).IsEqual(FighterOpponentType.Cpu);
            AssertThat(session.ReturnToHubAfterFighterMatch).IsTrue();
            AssertThat(session.CpuDifficulty).IsEqual(CpuDifficulty.Hard);
            AssertString(session.OpponentCharacterID).IsEqual("joan");
            AssertString(session.SelectedStageID).IsEqual(stage.StageID);
            AssertThat(session.MatchSettings.StockCount).IsEqual(5);
            AssertThat(session.MatchSettings.ItemSpawnRate).IsEqual(ChronalOrbFrequency.Off);
            AssertThat(session.MatchSettings.ItemsEnabled).IsFalse();
        } finally {
            host.Free();
            gameManager.CurrentSession = savedSession;
            if (SaveManager.Instance?.GlobalData != null && savedRules != null) {
                SaveManager.Instance.GlobalData.LastMatchSettings = savedRules;
                SaveManager.Instance.SaveGlobalData();
            }
        }
    }

    [TestCase]
    public void TheConsoleKeepsItsPracticeBoutAndGainsACalibrationDrillsEntryBesideIt() {
        // Package 11 A11 (F18): the hub console's Calibration Drills entry is an
        // addition, never a replacement — the CPU practice bout is the interim
        // training mode and stays exactly where it was.
        (Node host, HolodeckConsolePanel panel) = CreatePanel("HolodeckDrillsEntryHost");
        try {
            AssertThat(panel.GetNodeOrNull<Button>("Center/Tagged/Panel/Layout/LaunchButton") != null)
                .OverrideFailureMessage("The CPU practice bout's launch button is gone")
                .IsTrue();
            var drills = panel.GetNodeOrNull<Button>("Center/Tagged/Panel/Layout/CalibrationDrillsButton");
            AssertThat(drills != null)
                .OverrideFailureMessage("The console has no Calibration Drills entry")
                .IsTrue();
            // Resolved copy, not a raw key: this panel is code-built, so it
            // translates at build time rather than through control auto-translation.
            AssertThat(drills.Text).IsNotEqual("holodeck_calibration_drills");
            AssertThat(drills.Text.Length > 0).IsTrue();
            // Both entries are reachable by keyboard and controller.
            AssertThat(drills.FocusMode).IsEqual(Control.FocusModeEnum.All);
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// GR-TRAIN (9c, ruling Q5): the third entry sits in the hub console only, never in the front-end Versus CPU panel, and
    /// its route marks the session as a training visit while leaving the save slot, difficulty and match settings alone.
    /// </summary>
    [TestCase]
    public void TheHubConsoleGainsATrainingRoomEntryTheFrontEndDoesNotAndItsRouteTouchesOnlyTheOrigin() {
        GameManager gameManager = GameManager.Instance;
        AssertObject(gameManager).IsNotNull();
        SessionData savedSession = gameManager.CurrentSession;
        (Node host, HolodeckConsolePanel panel) = CreatePanel("HolodeckTrainingEntryHost");
        Node frontHost = null;
        try {
            var training = panel.GetNodeOrNull<Button>("Center/Tagged/Panel/Layout/TrainingRoomButton");
            AssertThat(training != null).OverrideFailureMessage("The console has no Training Room entry").IsTrue();
            AssertThat(training.Text).IsNotEqual("holodeck_training_room");
            AssertThat(training.FocusMode).IsEqual(Control.FocusModeEnum.All);
            AssertThat(panel.GetNodeOrNull<Button>("Center/Tagged/Panel/Layout/CalibrationDrillsButton") != null).IsTrue();

            frontHost = new Node { Name = "HolodeckFrontEndHost" };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(frontHost);
            var front = new HolodeckConsolePanel { Name = "HolodeckConsolePanel", FrontEnd = true };
            frontHost.AddChild(front);
            AssertThat(front.GetNodeOrNull<Button>("Center/Tagged/Panel/Layout/TrainingRoomButton") == null).IsTrue();

            SessionData before = gameManager.CurrentSession;
            string scene = HolodeckConsolePanel.ApplyTrainingRouteToSession();
            AssertThat(scene).IsEqual(TrainingRoom.ScenePath);
            AssertThat(ResourceLoader.Exists(scene)).IsTrue();
            SessionData after = gameManager.CurrentSession;
            AssertThat(after.FighterMatchOrigin).IsEqual(FighterMatchOrigin.Training);
            AssertThat(after.ActiveSaveSlot).IsEqual(before.ActiveSaveSlot);
            AssertThat(after.SelectedCharacterID).IsEqual(before.SelectedCharacterID);
            AssertThat(after.MatchSettings.StockCount).IsEqual(before.MatchSettings.StockCount);
            AssertThat(after.CalibrationReturnScenePath).IsEqual("");
        } finally {
            host.Free();
            if (frontHost != null) frontHost.Free();
            gameManager.CurrentSession = savedSession;
        }
    }

    [TestCase]
    public void TheBackButtonClosesWithoutTouchingTheSession() {
        GameManager gameManager = GameManager.Instance;
        AssertObject(gameManager).IsNotNull();
        SessionData savedSession = gameManager.CurrentSession;

        (Node host, HolodeckConsolePanel panel) = CreatePanel("HolodeckBackHost");
        try {
            bool closed = false;
            panel.Closed += () => closed = true;
            panel.GetNode<Button>("Center/Tagged/Panel/Layout/BackButton")
                .EmitSignal(BaseButton.SignalName.Pressed);

            AssertThat(closed).IsTrue();
            AssertThat(gameManager.CurrentSession.ReturnToHubAfterFighterMatch)
                .IsEqual(savedSession.ReturnToHubAfterFighterMatch);
        } finally {
            host.Free();
            gameManager.CurrentSession = savedSession;
        }
    }
}
