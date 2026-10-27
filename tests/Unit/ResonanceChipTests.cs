using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-SCREENS, TG-SCREENS. The Resonance chips: tier by shape, prerequisite edges that stop at the chip border, and (glass,
/// orchestrator ruling R10) a satisfied prerequisite edge in Resonance gold while every other edge keeps its meaning.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceChipTests {

    [TestCase]
    public void TheSatisfiedEdgeIsGoldAndTheOtherEdgesKeepTheirMeaning() {
        var from = new Vector2(0f, 0f);
        var to = new Vector2(100f, 0f);
        var satisfiedAll = new ResonanceEdgeLayer.Edge(from, to, AnyOf: false, Satisfied: true);
        var satisfiedAny = new ResonanceEdgeLayer.Edge(from, to, AnyOf: true, Satisfied: true);
        var openAll = new ResonanceEdgeLayer.Edge(from, to, AnyOf: false, Satisfied: false);
        var openAny = new ResonanceEdgeLayer.Edge(from, to, AnyOf: true, Satisfied: false);

        // R10: satisfied reads as Resonance (gold, UIPalette.Res2), whatever the mode, never the cyan it was.
        foreach (ResonanceEdgeLayer.Edge edge in new[] { satisfiedAll, satisfiedAny }) {
            Color color = ResonanceEdgeLayer.ColorFor(edge);
            AssertThat(new Color(color.R, color.G, color.B).IsEqualApprox(new Color(UIPalette.Res2.R, UIPalette.Res2.G, UIPalette.Res2.B)))
                .OverrideFailureMessage($"a satisfied edge draws {color}, not Resonance gold {UIPalette.Res2}").IsTrue();
            AssertThat(color.A).IsGreaterEqual(0.9f);
            AssertThat(ResonanceEdgeLayer.GlowWidthFor(edge)).IsGreater(ResonanceEdgeLayer.WidthFor(edge));
        }
        // Unsatisfied keeps the built dormant colour: quieter than the gold and not gold, with no glow.
        foreach (ResonanceEdgeLayer.Edge edge in new[] { openAll, openAny }) {
            Color color = ResonanceEdgeLayer.ColorFor(edge);
            AssertThat(color.IsEqualApprox(ResonanceEdgeLayer.DormantColor)).IsTrue();
            AssertThat(color.Luminance).IsLess(UIPalette.Res2.Luminance);
            AssertThat(ResonanceEdgeLayer.GlowWidthFor(edge)).IsEqual(0f);
            AssertThat(ResonanceEdgeLayer.WidthFor(edge)).IsLess(ResonanceEdgeLayer.WidthFor(satisfiedAll));
        }
        // Solid is All-of and dotted is Any-of, satisfied or not.
        AssertThat(ResonanceEdgeLayer.IsDotted(satisfiedAll) || ResonanceEdgeLayer.IsDotted(openAll)).IsFalse();
        AssertThat(ResonanceEdgeLayer.IsDotted(satisfiedAny) && ResonanceEdgeLayer.IsDotted(openAny)).IsTrue();
    }

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
