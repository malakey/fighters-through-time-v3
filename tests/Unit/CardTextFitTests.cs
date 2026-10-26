using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>UR-SELECT. The shrink-to-fit rule of the card text: scaled to its box, never below its size at UI scale 1.0, never enlarged.</summary>
[TestSuite]
public class CardTextFitTests {

    [TestCase]
    public void TextThatFitsIsLeftAloneAndTextThatDoesNotIsScaledToItsBox() {
        AssertThat(CardTextFit.ScaleFor(150f, 196f, 1f)).IsEqual(1f);
        AssertThat(CardTextFit.ScaleFor(196f, 196f, 1.4f)).IsEqual(1f);
        AssertThat(System.MathF.Abs(CardTextFit.ScaleFor(250f, 196f, 1.4f) - 196f / 250f)).IsLess(0.0001f);
        AssertThat(CardTextFit.ScaleFor(0f, 196f, 1.4f)).IsEqual(1f);
    }

    [TestCase]
    public void TheScaleNeverDropsBelowTheSizeTheSameTextHasAtUiScaleOne() {
        // A 400 px name in a 196 px box would want 0.49; at 1.4 the 1.0-scale size is 1 / 1.4.
        float scale = CardTextFit.ScaleFor(400f, 196f, 1.4f);
        AssertThat(System.MathF.Abs(scale - 1f / 1.4f)).IsLess(0.0001f);
        // At scale 1.0 the text is already its 1.0 size: it is never shrunk.
        AssertThat(CardTextFit.ScaleFor(400f, 196f, 1f)).IsEqual(1f);
        AssertThat(CardTextFit.ScaleFor(400f, 196f, 1.4f)).IsLessEqual(1f);
    }
}
