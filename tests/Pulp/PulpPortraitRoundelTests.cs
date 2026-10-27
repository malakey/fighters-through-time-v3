using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>The G5 portrait frame (TG-WIDGETS; the pulp roundel's class): the 22 % chamfer, the 2 px edge and the 142 % face crop.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpPortraitRoundelTests {

    private static bool Near(Vector2 a, Vector2 b) => a.DistanceTo(b) < 0.01f;

    [TestCase]
    public void TheFrameIsChamferedAtTwentyTwoPercentAndTheArtIsTheInnerBoxAtOneFortyTwoPercent() {
        // UI_SPEC_GLASS 3.4.4 / 55_misc.css: the art at 142 % of the inner box (the frame inset 2 px), offset (-21 %, -6.8 %).
        Rect2 rect = PulpPortraitRoundel.PortraitRect(112f);
        AssertThat(Mathf.IsEqualApprox(rect.Size.X, 1.42f * 108f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(rect.Position.X, 2f - 0.21f * 108f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(rect.Position.Y, 2f - 0.068f * 108f)).IsTrue();

        foreach (float size in new[] { 112f, 110f, 86f, 150f, 320f }) {
            var frame = new PulpPortraitRoundel { Diameter = size };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(frame);
            try {
                AssertThat(frame.CustomMinimumSize).IsEqual(new Vector2(size, size));
                AssertThat(frame.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
                // The outer polygon cuts the top-left and bottom-right corners at 22 % of the side.
                Vector2[] outer = frame.OuterPolygon;
                AssertThat(Near(outer[0], new Vector2(0.22f * size, 0f))).IsTrue();
                AssertThat(Near(outer[1], new Vector2(size, 0f))).IsTrue();
                AssertThat(Near(outer[2], new Vector2(size, 0.78f * size))).IsTrue();
                AssertThat(Near(outer[3], new Vector2(0.78f * size, size))).IsTrue();
                // The inner polygon is the box inset 2 px, cut at 22 % of its own side.
                Vector2[] inner = frame.InnerPolygon;
                float side = size - 4f;
                AssertThat(Near(inner[0], new Vector2(2f + 0.22f * side, 2f))).IsTrue();
                AssertThat(Near(inner[4], new Vector2(2f, 2f + side))).IsTrue();
                // No pulp parts: the round mask, the halftone and the back plate are gone.
                AssertThat(frame.GetNodeOrNull("Portrait")).IsNull();
                AssertThat(frame.GetNodeOrNull("Halftone")).IsNull();
                AssertThat(frame.GetChildCount()).IsEqual(0);
            } finally {
                frame.Free();
            }
        }
    }
}
