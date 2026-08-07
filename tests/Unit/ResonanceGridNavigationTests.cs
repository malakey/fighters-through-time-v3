using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pure-logic coverage for the Resonance Grid's D-pad/keyboard navigation.
/// The grid CLAMPS at its edges (no wrap) by design.
/// </summary>
[TestSuite]
public class ResonanceGridNavigationTests {
    private const int Columns = 3;
    private const int NodeCount = 9;

    [TestCase]
    public void MoveTraversesRowsAndColumnsInRowMajorOrder() {
        // Center of the 3x3 grid is index 4.
        AssertThat(ResonanceGridNavigation.Move(4, -1, 0, Columns, NodeCount)).IsEqual(3);
        AssertThat(ResonanceGridNavigation.Move(4, 1, 0, Columns, NodeCount)).IsEqual(5);
        AssertThat(ResonanceGridNavigation.Move(4, 0, -1, Columns, NodeCount)).IsEqual(1);
        AssertThat(ResonanceGridNavigation.Move(4, 0, 1, Columns, NodeCount)).IsEqual(7);
    }

    [TestCase]
    public void MoveClampsAtEveryGridEdgeWithoutWrapping() {
        AssertThat(ResonanceGridNavigation.Move(0, -1, 0, Columns, NodeCount)).IsEqual(0);
        AssertThat(ResonanceGridNavigation.Move(0, 0, -1, Columns, NodeCount)).IsEqual(0);
        AssertThat(ResonanceGridNavigation.Move(2, 1, 0, Columns, NodeCount)).IsEqual(2);
        AssertThat(ResonanceGridNavigation.Move(8, 1, 0, Columns, NodeCount)).IsEqual(8);
        AssertThat(ResonanceGridNavigation.Move(8, 0, 1, Columns, NodeCount)).IsEqual(8);
        AssertThat(ResonanceGridNavigation.Move(6, -1, 0, Columns, NodeCount)).IsEqual(6);
    }

    [TestCase]
    public void MoveClampsDegenerateAndPartialGridInput() {
        // Out-of-range starting indices are pulled into the grid first.
        AssertThat(ResonanceGridNavigation.Move(-5, 0, 0, Columns, NodeCount)).IsEqual(0);
        AssertThat(ResonanceGridNavigation.Move(42, 0, 0, Columns, NodeCount)).IsEqual(8);
        // Empty or invalid grids resolve to index 0.
        AssertThat(ResonanceGridNavigation.Move(3, 1, 0, Columns, 0)).IsEqual(0);
        AssertThat(ResonanceGridNavigation.Move(3, 1, 0, 0, NodeCount)).IsEqual(0);
        // A partial final row never navigates past the last node: moving down
        // from the third column of a 5-node grid clamps onto the final node.
        AssertThat(ResonanceGridNavigation.Move(2, 0, 1, Columns, 5)).IsEqual(4);
    }
}
