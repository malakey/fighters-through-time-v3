using System.Threading.Tasks;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pause;

/// <summary>UR-PAUSE: the PAUSE-A panel (UI_SPEC 3.1): padding, the halftone corner and the out-of-flow level tag.</summary>
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

            var box = panel.GetThemeStylebox("panel") as StyleBoxTexture;
            AssertObject(box).OverrideFailureMessage("The pause panel must carry its own padded plate.").IsNotNull();
            AssertThat(box.ContentMarginLeft).IsEqual(PausePanel.Border + PausePanel.PadSides);
            AssertThat(box.ContentMarginTop).IsEqual(PausePanel.Border + PausePanel.PadTop);
            AssertThat(box.ContentMarginBottom).IsEqual(PausePanel.Border + PausePanel.PadBottom);
            AssertThat(panel.GetNodeOrNull("HalftoneCorner")).IsNotNull();

            // The layout fills the padded rect; the tag does not squash it.
            AssertThat(layout.Position.X).IsEqual(box.ContentMarginLeft);
            AssertThat(layout.Size.Y).IsEqual(200f);
            AssertThat(panel.Tag.Visible).IsTrue();
            AssertThat(panel.Tag.Position.Y).IsLess(0f);
            AssertThat(panel.Tag.RotationDegrees).IsEqualApprox(-1.2f, 0.01f);

            panel.TagText = "";
            AssertThat(panel.Tag.Visible).IsFalse();
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }
}
