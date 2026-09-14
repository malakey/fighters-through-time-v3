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
                .IsEqual(FTT.Core.CharacterRoster.Count);
            AssertThat(panel.StageSelect.ItemCount).IsGreater(0);
            AssertThat(panel.StageSelect.Disabled).IsFalse();
        } finally {
            host.Free();
        }
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
            panel.GetNode<SpinBox>("Panel/Layout/RulesRow/StockCount").Value = 5;
            panel.GetNode<OptionButton>("Panel/Layout/ItemFrequencyRow/ItemFrequency").Select(0); // Off

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
    public void TheBackButtonClosesWithoutTouchingTheSession() {
        GameManager gameManager = GameManager.Instance;
        AssertObject(gameManager).IsNotNull();
        SessionData savedSession = gameManager.CurrentSession;

        (Node host, HolodeckConsolePanel panel) = CreatePanel("HolodeckBackHost");
        try {
            bool closed = false;
            panel.Closed += () => closed = true;
            panel.GetNode<Button>("Panel/Layout/BackButton")
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
