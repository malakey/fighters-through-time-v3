using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
[RequireGodotRuntime]
public class ReferenceSceneBackdropTests {
    [TestCase]
    public void CroppedBackdropCoversTheActualViewportAfterPanAndZoomOut() {
        var viewport = new SubViewport { Size = new Vector2I(1920, 1080) };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(viewport);
        try {
            var level = new Node2D();
            viewport.AddChild(level);
            var camera = new Camera2D { Enabled = false };
            level.AddChild(camera);
            Image image = Image.CreateEmpty(160, 100, false, Image.Format.Rgba8);
            image.Fill(Colors.White);
            ReferenceSceneBackdrop backdrop = ReferenceSceneBackdrop.Attach(level, ImageTexture.CreateFromImage(image), camera);
            viewport.CanvasTransform = new Transform2D(new Vector2(2f, 0f), new Vector2(0f, 2f), new Vector2(-200f, -100f));
            backdrop.Refresh();
            AssertThat(backdrop.CoverageRect.Encloses(backdrop.ViewRect)).IsTrue();
            Vector2 initialCoverage = backdrop.CoverageRect.Size;
            viewport.CanvasTransform = new Transform2D(new Vector2(0.75f, 0f), new Vector2(0f, 0.75f), new Vector2(-4200f, -1300f));
            backdrop.Refresh();
            AssertThat(backdrop.CoverageRect.Encloses(backdrop.ViewRect)).IsTrue();
            AssertFloat(backdrop.CoverageRect.Size.X).IsGreater(initialCoverage.X);
            AssertFloat(backdrop.CoverageRect.Size.Y).IsGreater(initialCoverage.Y);
            AssertThat(backdrop.GlobalPosition).IsEqual(backdrop.ViewRect.GetCenter());
            AssertInt(backdrop.ZIndex).IsEqual(-2);
            AssertThat(backdrop.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);
        } finally {
            viewport.Free();
        }
    }
}
