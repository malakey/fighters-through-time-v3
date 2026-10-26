using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pause;

/// <summary>
/// UR-PAUSE: the Story pause (both build paths), the Fighter pause and its disconnect modal take the PAUSE-A look:
/// a scrim, the tagged ink panel, the PauseTitle role, the status strip, decorated 560 x 64 rows and the prompt bar.
/// Every case restores <see cref="SceneTree.Paused"/> in <c>finally</c> (failure signature 4).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PauseSkinTests {

    private static readonly string[] Rows = {
        "ResumeButton", "SettingsButton", "MoveListButton", "LogButton", "SaveButton", "RestartButton",
        "LowerDifficultyButton", "QuitButton"
    };

    [TestCase]
    public void TheStoryPauseSkinsTheSameNodesOnBothBuildPaths() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = "PauseSkinHost" };
        tree.Root.AddChild(host);
        try {
            PauseMenu authored = PauseMenu.CreateDefault();
            host.AddChild(authored);
            var fallback = new PauseMenu { Name = "FallbackPause" };
            host.AddChild(fallback);

            foreach (PauseMenu pause in new[] { authored, fallback }) {
                string who = pause.Name.ToString();
                pause.SetPaused(true);
                var panel = pause.GetNodeOrNull<PausePanel>("Root/Center/Panel");
                AssertObject(panel).OverrideFailureMessage(who + ": the panel must be a PausePanel.").IsNotNull();
                AssertThat(pause.GetNodeOrNull<PanelContainer>("Root/Shade")?.ThemeTypeVariation.ToString())
                    .OverrideFailureMessage(who + ": the scrim must be the ScrimPanel plate.").IsEqual("ScrimPanel");
                AssertThat(panel.Tag.Visible).OverrideFailureMessage(who + ": the level tag shows on open.").IsTrue();

                var status = pause.GetNodeOrNull<Label>("Root/Center/Panel/Layout/StatusLine");
                AssertObject(status).OverrideFailureMessage(who + ": no status line.").IsNotNull();
                AssertThat(status.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.StatusLine);
                AssertThat(status.Uppercase).IsTrue();
                AssertThat(status.Text.Length > 0).OverrideFailureMessage(who + ": the status line is empty.").IsTrue();

                foreach (string name in Rows) {
                    var row = pause.GetNodeOrNull<Button>("Root/Center/Panel/Layout/" + name);
                    AssertObject(row).OverrideFailureMessage($"{who}: {name} missing.").IsNotNull();
                    AssertThat(row.CustomMinimumSize.X).IsEqual(PulpScreenKit.PauseButtonWidth);
                    AssertThat(row.CustomMinimumSize.Y).IsEqual(PulpScreenKit.PauseButtonHeight);
                    AssertObject(row.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor"))
                        .OverrideFailureMessage($"{who}: {name} has no banner decor.").IsNotNull();
                }
                AssertObject(pause.GetNodeOrNull<PulpPromptBar>("Root/PromptBar"))
                    .OverrideFailureMessage(who + ": the prompt bar is missing.").IsNotNull();
                pause.SetPaused(false);
            }
        } finally {
            tree.Paused = false;
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    [TestCase]
    public void TheFighterPauseAndItsDisconnectModalUseThePauseLanguage() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = "FighterPauseSkinHost" };
        tree.Root.AddChild(host);
        try {
            var pause = new LocalFighterPause { Name = "LocalFighterPause" };
            host.AddChild(pause);
            var panel = pause.FindChild("PausePanel", true, false) as PausePanel;
            AssertObject(panel).IsNotNull();
            AssertThat(panel.TagText.Length > 0).IsTrue();
            foreach (string name in new[] { "ResumeButton", "SettingsButton", "ExitButton" }) {
                var row = pause.FindChild(name, true, false) as Button;
                AssertObject(row).OverrideFailureMessage(name + " missing.").IsNotNull();
                AssertObject(row.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor")).IsNotNull();
            }
            var backdrop = pause.GetNodeOrNull<Control>("Backdrop");
            AssertObject(backdrop).IsNotNull();
            AssertThat(backdrop.GetNodeOrNull<PanelContainer>("Shade")?.ThemeTypeVariation.ToString()).IsEqual("ScrimPanel");
            AssertObject(backdrop.GetNodeOrNull<PulpPromptBar>("PromptBar")).IsNotNull();

            var modal = pause.FindChild("DisconnectModal", true, false) as PanelContainer;
            AssertObject(modal).IsNotNull();
            AssertThat(modal.ThemeTypeVariation.ToString()).IsEqual("ScrimPanel");
            AssertObject(modal.FindChild("DisconnectPanel", true, false) as PausePanel).IsNotNull();
            foreach (string name in new[] { "RebindButton", "ForfeitButton" }) {
                AssertObject((modal.FindChild(name, true, false) as Button)?.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor"))
                    .OverrideFailureMessage(name + " has no banner decor.").IsNotNull();
            }
        } finally {
            tree.Paused = false;
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }
}
