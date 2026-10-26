using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>UR-WIDGETS step 2. The portrait roundel: the 142 % crop and the circle mask.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpPortraitRoundelTests {

    [TestCase]
    public void ThePortraitIsDrawnAt142PercentOffsetAndMaskedToTheDisc() {
        Rect2 rect = PulpPortraitRoundel.PortraitRect(112f);
        AssertThat(Mathf.IsEqualApprox(rect.Size.X, 159.04f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(rect.Position.X, -23.52f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(rect.Position.Y, -7.616f)).IsTrue();

        var roundel = new PulpPortraitRoundel { Diameter = 86f };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(roundel);
        try {
            AssertThat(roundel.CustomMinimumSize).IsEqual(new Vector2(86f, 86f));
            AssertThat(roundel.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            var portrait = roundel.GetNode<TextureRect>("Portrait");
            var mask = (ShaderMaterial)portrait.Material;
            Vector2 center = (Vector2)mask.GetShaderParameter("circle_center");
            AssertThat(Mathf.IsEqualApprox(center.X, 0.5f)).IsTrue();
            AssertThat(Mathf.IsEqualApprox(center.Y, 0.4f)).IsTrue();
            AssertThat(roundel.GetNode<TextureRect>("Halftone").Size).IsEqual(new Vector2(86f, 86f));
        } finally {
            roundel.Free();
        }
    }
}
