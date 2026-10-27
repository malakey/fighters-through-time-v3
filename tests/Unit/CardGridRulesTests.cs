using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// TG-SELECT (CARD-A, UI_SPEC_GLASS 4.2, 8.3). The pure tile-grid rules: three columns and the Random tile on a full-width
/// row of its own; a fourth row starting the grid higher at the compact size with the Random tile kept on its line; past the
/// compact size, tiles shrinking to keep the block inside its band. The stat maxima ride the first case.
/// </summary>
[TestSuite]
public class CardGridRulesTests {

    [TestCase]
    public void TheGridIsThreeColumnsAndTheRandomTileHasAFullWidthRowOfItsOwn() {
        AssertThat(CardGridRules.Columns).IsEqual(3);
        AssertThat(CardGridRules.RowsFor(9)).IsEqual(3);
        AssertThat(CardGridRules.RowsFor(10)).IsEqual(4);
        AssertThat(CardGridRules.RowsFor(1)).IsEqual(1);

        // Nine fighters: the drawn layout (4.2: 164 px tiles at a 180 pitch from y 196, the Random tile 524 x 72, bottom 808).
        CardGridRules.GridLayout nine = CardGridRules.LayoutFor(9);
        AssertThat(nine.Tile).IsEqual(164f);
        AssertThat(nine.Pitch).IsEqual(180f);
        AssertThat(nine.Top).IsEqual(196f);
        AssertThat(nine.Width).IsEqual(524f);
        AssertThat(nine.RandomTop).IsEqual(540f);
        AssertThat(nine.Bottom).IsEqual(808f);
        AssertThat(nine.Compact).IsFalse();

        // The cursor: the Random tile (index 9) sits alone below the last row; a step right stays on it, a step down from
        // it wraps to the top row, a step up from it lands on the last row; a horizontal step wraps within its row.
        AssertThat(CardGridRules.Step(6, 0, 1, 9)).IsEqual(9);
        AssertThat(CardGridRules.Step(8, 0, 1, 9)).IsEqual(9);
        AssertThat(CardGridRules.Step(9, 1, 0, 9)).IsEqual(9);
        AssertThat(CardGridRules.Step(9, 0, 1, 9)).IsEqual(0);
        AssertThat(CardGridRules.Step(9, 0, -1, 9)).IsEqual(6);
        AssertThat(CardGridRules.Step(2, 1, 0, 9)).IsEqual(0);
        AssertThat(CardGridRules.Step(0, -1, 0, 9)).IsEqual(2);
        AssertThat(CardGridRules.Step(1, 0, -1, 9)).IsEqual(9);

        // The stat tubes' full scale is the largest value supplied, not a constant.
        CardStatMaxima max = CardStatMaxima.FromValues(new[] { (90f, 4.8f, 1.1f), (120f, 3.8f, 0.85f), (100f, 6.2f, 1.7f) });
        AssertThat(max).IsEqual(new CardStatMaxima(120f, 6.2f, 1.7f));
        AssertThat(CardStatMaxima.FromValues(new (float, float, float)[0])).IsEqual(new CardStatMaxima(0f, 0f, 0f));
    }

    [TestCase]
    public void AFourthRowStartsTheGridHigherAtTheCompactSizeAndKeepsTheRandomTileOnItsLine() {
        CardGridRules.GridLayout nine = CardGridRules.LayoutFor(9);
        foreach (int roster in new[] { 10, 11, 12 }) {
            CardGridRules.GridLayout grown = CardGridRules.LayoutFor(roster);
            AssertThat(grown.Rows).IsEqual(4);
            // Four full rows and the Random tile (792 px) do not fit the band under the title (y 112 to 808): compact tiles.
            AssertThat(CardGridRules.BlockHeightAt(4, CardGridRules.TilePitch))
                .IsGreater(CardGridRules.BlockBottom - CardGridRules.BandTop);
            AssertThat(grown.Tile).IsEqual(CardGridRules.CompactTileSize);
            AssertThat(grown.Pitch).IsEqual(CardGridRules.CompactPitch);
            AssertThat(grown.Top).IsLess(nine.Top);
            AssertThat(grown.Top).IsGreaterEqual(CardGridRules.BandTop);
            AssertThat(grown.Bottom).IsEqual(nine.Bottom);
            AssertThat(grown.Width).IsEqual(3f * 152f - 12f);
        }
        // Ten fighters: the tenth tile is alone on the fourth row; a step right stays there, a step down reaches Random.
        AssertThat(CardGridRules.Step(9, 1, 0, 10)).IsEqual(9);
        AssertThat(CardGridRules.Step(8, 0, 1, 10)).IsEqual(9);
        AssertThat(CardGridRules.Step(9, 0, 1, 10)).IsEqual(10);
        AssertThat(CardGridRules.Step(10, 0, -1, 10)).IsEqual(9);
    }

    [TestCase]
    public void PastTheCompactSizeTheTilesShrinkToKeepTheBlockInsideItsBand() {
        float band = CardGridRules.BlockBottom - CardGridRules.BandTop;
        CardGridRules.GridLayout previous = CardGridRules.LayoutFor(12);
        foreach (int roster in new[] { 13, 15, 18 }) {
            CardGridRules.GridLayout layout = CardGridRules.LayoutFor(roster);
            AssertThat(layout.Rows).IsEqual(CardGridRules.RowsFor(roster));
            AssertThat(layout.Pitch).IsLess(CardGridRules.CompactPitch);
            AssertThat(layout.Pitch).IsLessEqual(previous.Pitch);
            AssertThat(layout.BlockHeight).IsLessEqual(band + 0.01f);
            AssertThat(System.MathF.Abs(layout.Bottom - CardGridRules.BlockBottom)).IsLess(0.01f);
            // The compact proportions: tile / pitch = 140 / 152.
            AssertThat(System.MathF.Abs(layout.Tile / layout.Pitch - 140f / 152f)).IsLess(0.0001f);
            previous = layout;
        }
        // Never under half the full pitch: a roster too large for the band overflows it rather than shrinking to nothing.
        CardGridRules.GridLayout huge = CardGridRules.LayoutFor(300);
        AssertThat(huge.Pitch).IsEqual(CardGridRules.MinPitch);
    }
}
