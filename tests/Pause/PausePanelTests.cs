using System.Threading.Tasks;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pause;

/// <summary>UR-PAUSE: the PAUSE-A panel (UI_SPEC 3.1): padding, the glass sheen (TG-WIDGETS cross-edit) and the out-of-flow level tag.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PausePanelTests {

    [TestCase]
    public async Task ThePanelPadsThirtyTwentyTwentyEightInsideItsBorderAndTheTagStaysOutOfFlow() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new CanvasLayer { Name = "PausePanelHost" };
        tree.Root.AddChild(host);
        try {
            var panel = new PausePanel { Name = "Panel", TagText = "Pompeii 79 AD" };
            UIPalette.ApplyTheme(panel);
            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddChild(new Control { CustomMinimumSize = new Vector2(400f, 200f) });
            panel.AddChild(layout);
            host.AddChild(panel);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var box = panel.GetThemeStylebox("panel");   // TG-THEME cross-edit: any StyleBox (the step-1 slab is flat)
            AssertObject(box).OverrideFailureMessage("The pause panel must carry its own padded plate.").IsNotNull();
            AssertThat(box.ContentMarginLeft).IsEqual(PausePanel.Border + PausePanel.PadSides);
            AssertThat(box.ContentMarginTop).IsEqual(PausePanel.Border + PausePanel.PadTop);
            AssertThat(box.ContentMarginBottom).IsEqual(PausePanel.Border + PausePanel.PadBottom);
            AssertThat(panel.GetNodeOrNull("GlassSheen")).IsNotNull();   // TG-WIDGETS cross-edit: the sheen replaces the halftone

            // The layout fills the padded rect; the tag does not squash it.
            AssertThat(layout.Position.X).IsEqual(box.ContentMarginLeft);
            AssertThat(layout.Size.Y).IsEqual(200f);
            AssertThat(panel.Tag.Visible).IsTrue();
            AssertThat(panel.Tag.GetGlobalRect().Position.Y).IsLess(panel.GetGlobalRect().Position.Y);
            AssertThat(panel.Tag.RotationDegrees).IsEqualApprox(0f, 0.01f);   // TG-WIDGETS cross-edit: the glass tag is not tilted

            // The tag stays out of flow: an overlong one clamps inside the panel instead of widening it.
            float width = panel.Size.X;
            panel.TagText = "AN EXCEEDINGLY LONG LOCALIZED PANEL TITLE THAT RUNS PAST ANY PANEL EDGE";
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(panel.Size.X).IsEqual(width);
            AssertThat(panel.Tag.GetGlobalRect().End.X <= panel.GetGlobalRect().End.X - FTT.UI.Pulp.PulpTagPlate.RightMargin + 1f).IsTrue();

            panel.TagText = "";
            AssertThat(panel.Tag.Visible).IsFalse();
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    /// <summary>
    /// TG-CLOSE (TG-PAUSE kit gap 6): stopping the appear mid-rise settles the panel on its slot at full opacity, through the
    /// motion's public stop-and-settle (PulpMotion.Settle); killing the tween alone left the rise's offset in place until the
    /// next fade-rise.
    /// </summary>
    [TestCase]
    public async Task StoppingTheAppearMidRiseLeavesThePanelOnItsSlot() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool reduced = FTT.Core.ComfortSettings.ReducedTemporalEffects;
        var host = new Control { Name = "PausePanelSettleHost", Size = new Vector2(1920f, 1080f) };
        tree.Root.AddChild(host);
        try {
            FTT.Core.ComfortSettings.Apply(false);
            var panel = new PausePanel { Name = "Panel" };
            UIPalette.ApplyTheme(panel);
            panel.AddChild(new Control { CustomMinimumSize = new Vector2(400f, 200f) });
            host.AddChild(panel);
            var slot = new Vector2(300f, 200f);
            panel.Position = slot;
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            panel.PlayAppear();
            for (int i = 0; i < 3; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(panel.Position.Y > slot.Y + 1f).OverrideFailureMessage($"Harness: the panel is not mid-rise ({panel.Position})").IsTrue();
            panel.StopAppear();
            AssertThat(panel.Position.IsEqualApprox(slot)).OverrideFailureMessage($"stopped mid-rise the panel rests at {panel.Position}, not its slot {slot}").IsTrue();
            AssertThat(panel.Modulate.A).IsEqual(1f);
            for (int i = 0; i < 20; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(panel.Position.IsEqualApprox(slot)).OverrideFailureMessage($"the panel moved to {panel.Position} after the stop").IsTrue();
        } finally {
            FTT.Core.ComfortSettings.Apply(reduced);
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }
}
