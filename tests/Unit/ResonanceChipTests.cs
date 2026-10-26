using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>UR-SCREENS. The Resonance chips: tier by shape, and prerequisite edges that stop at the chip border.</summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceChipTests {

    [TestCase]
    public void EachTierHasItsOwnShape() {
        AssertThat(ResonanceTierMark.PointsFor(1)).IsEqual(0);
        AssertThat(ResonanceTierMark.PointsFor(2)).IsEqual(4);
        AssertThat(ResonanceTierMark.PointsFor(3)).IsEqual(8);
    }

    [TestCase]
    public void AnEdgeLeavesTheChipAtItsBorderTowardTheOtherChip() {
        var chip = new Rect2(100f, 100f, 200f, 90f);
        Vector2 right = ResonanceGridPanel.ClipToRect(chip, new Vector2(800f, 145f));
        AssertThat(right.IsEqualApprox(new Vector2(300f, 145f))).IsTrue();
        Vector2 below = ResonanceGridPanel.ClipToRect(chip, new Vector2(200f, 600f));
        AssertThat(Mathf.IsEqualApprox(below.Y, 190f)).IsTrue();
        AssertThat(ResonanceGridPanel.ClipToRect(chip, chip.GetCenter()).IsEqualApprox(chip.GetCenter())).IsTrue();
    }
}
