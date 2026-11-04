using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// GR-TRAIN (9c). The training room's settings menu: three session-only dummy settings, three actions, and a tree pause
/// that is raised only when nothing else holds it and always handed back.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TrainingRoomPanelTests {

    private static (Node host, TrainingRoomPanel panel) Create(TrainingDummySettings settings = default) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = "TrainingPanelHost" };
        tree.Root.AddChild(host);
        var panel = new TrainingRoomPanel { Name = "TrainingRoomPanel", Settings = settings };
        host.AddChild(panel);
        return (host, panel);
    }

    [TestCase]
    public void ThePanelOffersTheThreeSettingsAndThreeActionsStartingFromItsSettings() {
        var start = new TrainingDummySettings {
            Block = TrainingBlockMode.AfterHit, Jump = TrainingJumpMode.Timed, Hitstun = TrainingHitstunResponse.DirectionalInfluence };
        (Node host, TrainingRoomPanel panel) = Create(start);
        try {
            AssertThat(panel.BlockSelect.ItemCount).IsEqual(3);
            AssertThat(panel.JumpSelect.ItemCount).IsEqual(3);
            AssertThat(panel.HitstunSelect.ItemCount).IsEqual(3);
            AssertThat((int)panel.BlockSelect.GetSelectedId()).IsEqual((int)TrainingBlockMode.AfterHit);
            AssertThat((int)panel.JumpSelect.GetSelectedId()).IsEqual((int)TrainingJumpMode.Timed);
            AssertThat((int)panel.HitstunSelect.GetSelectedId()).IsEqual((int)TrainingHitstunResponse.DirectionalInfluence);
            AssertThat(panel.ResumeButton.FocusMode).IsEqual(Control.FocusModeEnum.All);
            // Resolved copy, not a raw key: the panel is code-built.
            AssertThat(panel.ResetButton.Text).IsNotEqual("training_reset");
            AssertThat(panel.ExitButton.Text.Length > 0).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ChangingASettingRaisesItAndWritesNothingToTheSessionOrTheSave() {
        GameManager gameManager = GameManager.Instance;
        AssertObject(gameManager).IsNotNull();
        SessionData savedSession = gameManager.CurrentSession;
        SavedMatchSettings savedRules = SaveManager.Instance?.GlobalData?.LastMatchSettings;
        string rulesBefore = savedRules == null ? "" : Newtonsoft.Json.JsonConvert.SerializeObject(savedRules);

        (Node host, TrainingRoomPanel panel) = Create();
        try {
            TrainingDummySettings raised = default;
            int raises = 0;
            panel.SettingsChanged += settings => { raised = settings; raises++; };
            panel.BlockSelect.Select((int)TrainingBlockMode.Always);
            panel.BlockSelect.EmitSignal(OptionButton.SignalName.ItemSelected, (int)TrainingBlockMode.Always);
            panel.HitstunSelect.Select((int)TrainingHitstunResponse.HoldBlock);
            panel.HitstunSelect.EmitSignal(OptionButton.SignalName.ItemSelected, (int)TrainingHitstunResponse.HoldBlock);

            AssertThat(raises).IsEqual(2);
            AssertThat(raised.Block).IsEqual(TrainingBlockMode.Always);
            AssertThat(raised.Jump).IsEqual(TrainingJumpMode.Never);
            AssertThat(raised.Hitstun).IsEqual(TrainingHitstunResponse.HoldBlock);
            AssertThat(panel.Settings.Block).IsEqual(TrainingBlockMode.Always);
            AssertThat(gameManager.CurrentSession.FighterMatchOrigin).IsEqual(savedSession.FighterMatchOrigin);
            AssertThat(gameManager.CurrentSession.OpponentCharacterID).IsEqual(savedSession.OpponentCharacterID);
            string rulesAfter = savedRules == null ? "" : Newtonsoft.Json.JsonConvert.SerializeObject(savedRules);
            AssertThat(rulesAfter).IsEqual(rulesBefore);
        } finally {
            host.Free();
            gameManager.CurrentSession = savedSession;
        }
    }

    [TestCase]
    public void OpeningPausesTheTreeClosingAndLeavingTheTreeHandItBackAndAForeignPauseIsLeftAlone() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool wasPaused = tree.Paused;
        tree.Paused = false;
        (Node host, TrainingRoomPanel panel) = Create();
        (Node otherHost, TrainingRoomPanel other) = (null, null);
        try {
            int resumed = 0;
            panel.Resumed += () => resumed++;
            panel.Open();
            AssertThat(panel.Visible).IsTrue();
            AssertThat(tree.Paused).IsTrue();
            AssertThat(panel.HoldsPause).IsTrue();
            panel.Close();
            AssertThat(panel.Visible).IsFalse();
            AssertThat(tree.Paused).IsFalse();
            AssertThat(resumed).IsEqual(1);

            // Freed while open: the pause goes back with it.
            panel.Open();
            AssertThat(tree.Paused).IsTrue();
            host.Free();
            host = null;
            AssertThat(tree.Paused).IsFalse();

            // A pause somebody else raised is not this panel's to lower.
            tree.Paused = true;
            (otherHost, other) = Create();
            other.Open();
            AssertThat(other.HoldsPause).IsFalse();
            other.Close();
            AssertThat(tree.Paused).IsTrue();
            otherHost.Free();
            otherHost = null;
            AssertThat(tree.Paused).IsTrue();
        } finally {
            tree.Paused = wasPaused;
            if (host != null && GodotObject.IsInstanceValid(host)) host.Free();
            if (otherHost != null && GodotObject.IsInstanceValid(otherHost)) otherHost.Free();
        }
    }
}
