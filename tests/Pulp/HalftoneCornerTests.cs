using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>UR-WIDGETS. The halftone corner layer: shader, mouse, child order and dot colours.</summary>
[TestSuite]
[RequireGodotRuntime]
public class HalftoneCornerTests {

    [TestCase]
    public void TheCornerIsTheFirstChildWithItsShaderAndIgnoresTheMouse() {
        var panel = new PanelContainer();
        panel.AddChild(new Label { Text = "content" });
        HalftoneCorner corner = HalftoneCorner.Attach(panel);
        try {
            AssertThat(panel.GetChild(0)).IsEqual(corner);
            AssertThat(corner.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            var material = corner.Material as ShaderMaterial;
            AssertObject(material).IsNotNull();
            AssertObject(material.Shader).IsNotNull();
            AssertThat(material.Shader.ResourcePath).IsEqual(HalftoneCorner.ShaderPath);
        } finally {
            panel.Free();
        }
    }

    [TestCase]
    public void DarkPanelsGetWhiteDotsAndPaperPanelsBlackOnes() {
        var panel = new PanelContainer();
        HalftoneCorner dark = HalftoneCorner.Attach(panel);
        HalftoneCorner paper = HalftoneCorner.Attach(panel, paper: true);
        try {
            Color darkColor = (Color)((ShaderMaterial)dark.Material).GetShaderParameter("dot_color");
            Color paperColor = (Color)((ShaderMaterial)paper.Material).GetShaderParameter("dot_color");
            AssertThat(darkColor).IsEqual(HalftoneCorner.DarkDots);
            AssertThat(paperColor).IsEqual(HalftoneCorner.PaperDots);
            AssertThat(panel.GetChild(0)).IsEqual(paper);
        } finally {
            panel.Free();
        }
    }
}
