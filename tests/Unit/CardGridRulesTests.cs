using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>UR-SELECT. The pure card-grid rules: five columns, rows that grow with the roster, a compact scale past two rows, the stat maxima.</summary>
[TestSuite]
public class CardGridRulesTests {

    [TestCase]
    public void TheGridIsFiveColumnsWideAndGrowsRowsWithTheTileCount() {
        AssertThat(CardGridRules.Columns).IsEqual(5);
        AssertThat(CardGridRules.RowsFor(1)).IsEqual(1);
        AssertThat(CardGridRules.RowsFor(5)).IsEqual(1);
        AssertThat(CardGridRules.RowsFor(10)).IsEqual(2);
        AssertThat(CardGridRules.RowsFor(11)).IsEqual(3);
        AssertThat(CardGridRules.RowsFor(20)).IsEqual(4);
    }

    [TestCase]
    public void TheCompactScaleStartsOnlyOnceTheGridOutgrowsTwoRows() {
        AssertThat(CardGridRules.ScaleFor(10)).IsEqual(1f);
        AssertThat(CardGridRules.ScaleFor(9)).IsEqual(1f);
        float three = CardGridRules.ScaleFor(11);
        AssertThat(three).IsLess(1f);
        // Three rows at the compact scale are as tall as two full rows.
        AssertThat(System.MathF.Abs(three * 3f - 2f)).IsLess(0.001f);
        AssertThat(CardGridRules.ScaleFor(200)).IsEqual(CardGridRules.MinScale);
    }

    [TestCase]
    public void TheStatMaximaAreTheLargestValuesSuppliedNotConstants() {
        CardStatMaxima max = CardStatMaxima.FromValues(new[] { (90f, 4.8f, 1.1f), (120f, 3.8f, 0.85f), (100f, 6.2f, 1.7f) });
        AssertThat(max).IsEqual(new CardStatMaxima(120f, 6.2f, 1.7f));
        AssertThat(CardStatMaxima.FromValues(new (float, float, float)[0])).IsEqual(new CardStatMaxima(0f, 0f, 0f));
    }
}
