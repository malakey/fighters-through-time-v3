using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pause;

/// <summary>
/// UR-PAUSE, TG-PAUSE: the Story pause (both build paths), the Fighter pause and its disconnect modal take the PAUSE-A
/// look: a scrim, the tagged glass slab (<c>GlassPanelPause</c>), the PauseTitle role, the status strip, decorated 64 px
/// chevron rows sharing one D6 width (at least 560, at most 700 at UI scale 1.0) and the prompt bar; opening fades the
/// dim and the slab in. Every case restores <see cref="SceneTree.Paused"/> in <c>finally</c> (failure signature 4).
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
                AssertThat(panel.ThemeTypeVariation.ToString()).IsEqual(UIPalette.Variations.GlassPanelPause);
                AssertThat(pause.GetNodeOrNull<PanelContainer>("Root/Shade")?.ThemeTypeVariation.ToString())
                    .OverrideFailureMessage(who + ": the scrim must be the ScrimPanel plate.").IsEqual("ScrimPanel");
                AssertThat(panel.Tag.Visible).OverrideFailureMessage(who + ": the level tag shows on open.").IsTrue();

                var status = pause.GetNodeOrNull<Label>("Root/Center/Panel/Layout/StatusLine");
                AssertObject(status).OverrideFailureMessage(who + ": no status line.").IsNotNull();
                AssertThat(status.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.StatusLine);
                AssertThat(status.Uppercase).IsTrue();
                AssertThat(status.Text.Length > 0).OverrideFailureMessage(who + ": the status line is empty.").IsTrue();

                float shared = -1f;
                foreach (string name in Rows) {
                    var row = pause.GetNodeOrNull<Button>("Root/Center/Panel/Layout/" + name);
                    AssertObject(row).OverrideFailureMessage($"{who}: {name} missing.").IsNotNull();
                    // TG-PAUSE (D6): one width for the whole stack, from its widest label, 560 to 700 at UI scale 1.0.
                    if (shared < 0f) shared = row.CustomMinimumSize.X;
                    AssertThat(row.CustomMinimumSize.X).OverrideFailureMessage($"{who}: {name} leaves the stack's width.").IsEqual(shared);
                    AssertThat(row.CustomMinimumSize.X >= PulpScreenKit.PauseButtonWidth).IsTrue();
                    AssertThat(row.CustomMinimumSize.X <= PausePanel.RowCapAtOne * PulpButtonDecor.UiScaleFactor).IsTrue();
                    AssertThat(row.CustomMinimumSize.Y).IsEqual(PulpScreenKit.PauseButtonHeight * PulpButtonDecor.UiScaleFactor);
                    AssertObject(row.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor"))
                        .OverrideFailureMessage($"{who}: {name} has no banner decor.").IsNotNull();
                }
                AssertObject(pause.GetNodeOrNull<PulpPromptBar>("Root/" + PulpScreenKit.PromptBarName))
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
            AssertObject(backdrop.GetNodeOrNull<PulpPromptBar>(PulpScreenKit.PromptBarName)).IsNotNull();

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

    private static async Task Frames(SceneTree tree, int count) {
        for (int i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>
    /// TG-PAUSE (MOT-A rows 4 and 14, UI_SPEC_GLASS 9.2): opening the Story pause fades its dim in from nothing to .70 over
    /// 0.16 s while the panel fades in and rises into its slot, and both keep running while the tree is paused (the tweens
    /// run in the process pause mode, or the opened pause would sit dark and empty). Closing is immediate.
    /// </summary>
    [TestCase]
    public async Task ThePauseDimIsSeventyPercentAndThePanelFadeRisesWhileTheTreeIsPaused() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = "PauseMotionHost" };
        tree.Root.AddChild(host);
        bool reduced = ComfortSettings.ReducedTemporalEffects;
        try {
            ComfortSettings.Apply(false);
            PauseMenu pause = PauseMenu.CreateDefault();
            host.AddChild(pause);
            await Frames(tree, 2);
            var shade = pause.GetNode<PanelContainer>("Root/Shade");
            var panel = pause.GetNode<PausePanel>("Root/Center/Panel");

            pause.SetPaused(true);
            AssertThat(tree.Paused).IsTrue();
            AssertThat(shade.SelfModulate.A).OverrideFailureMessage("The dim fades in from nothing.").IsLess(0.05f);
            AssertThat(panel.Modulate.A).OverrideFailureMessage("The panel fades in from nothing.").IsLess(0.05f);
            AssertObject(panel.DimTween).OverrideFailureMessage("No dim tween runs.").IsNotNull();
            AssertObject(panel.AppearTween).OverrideFailureMessage("No panel tween runs.").IsNotNull();

            await Frames(tree, 2);   // a paused tree: only process-mode tweens move
            float risingY = panel.Position.Y;
            AssertThat(shade.SelfModulate.A).OverrideFailureMessage("The dim froze with the tree.").IsGreater(0.05f);
            AssertThat(shade.SelfModulate.A).IsLess(PulpScreenKit.ScrimAlpha - 0.05f);
            AssertThat(panel.Modulate.A).OverrideFailureMessage("The panel froze with the tree.").IsGreater(0.05f);

            await Frames(tree, 30);
            AssertThat(shade.SelfModulate.A).IsEqualApprox(PulpScreenKit.ScrimAlpha, 0.001f);
            AssertThat(panel.Modulate.A).IsEqualApprox(1f, 0.001f);
            AssertThat(risingY - panel.Position.Y).OverrideFailureMessage("The panel did not rise into its slot.").IsGreater(4f);
            AssertObject(panel.AppearTween).IsNull();

            pause.SetPaused(false);
            AssertThat(pause.GetNode<Control>("Root").Visible).IsFalse();
            AssertThat(shade.SelfModulate.A).IsEqualApprox(PulpScreenKit.ScrimAlpha, 0.001f);
        } finally {
            ComfortSettings.Apply(reduced);
            tree.Paused = false;
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }
}
