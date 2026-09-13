using System.Collections.Generic;
using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pure-logic coverage for the Resonance Grid's D-pad/keyboard navigation.
///
/// <para>Package 11 A4 (V7.6) replaced the row-major index arithmetic these
/// cases used to pin: every character now has a UNIQUE authored topology, so
/// navigation is nearest-neighbour-in-direction over the authored
/// <c>LayoutPosition</c> points. Navigation still CLAMPS at the edges of the
/// constellation (no wrap) by design.</para>
/// </summary>
[TestSuite]
public class ResonanceGridNavigationTests {

    /// <summary>
    /// Einstein's mesh: three root columns at x 0.15, three Tier 2 at 0.50 and
    /// three Majors at 0.85, each column at y 0.20 / 0.50 / 0.80.
    /// </summary>
    private static readonly List<(float X, float Y)> Mesh = new() {
        (0.15f, 0.20f), (0.15f, 0.50f), (0.15f, 0.80f),
        (0.50f, 0.20f), (0.50f, 0.50f), (0.50f, 0.80f),
        (0.85f, 0.20f), (0.85f, 0.50f), (0.85f, 0.80f)
    };

    [TestCase]
    public void MovePicksTheNearestNodeLyingInThePressedDirection() {
        // Index 4 is the centre of the mesh.
        AssertThat(ResonanceGridNavigation.Move(4, -1, 0, Mesh)).IsEqual(1);
        AssertThat(ResonanceGridNavigation.Move(4, 1, 0, Mesh)).IsEqual(7);
        AssertThat(ResonanceGridNavigation.Move(4, 0, -1, Mesh)).IsEqual(3);
        AssertThat(ResonanceGridNavigation.Move(4, 0, 1, Mesh)).IsEqual(5);
    }

    [TestCase]
    public void MoveClampsAtEveryEdgeOfTheConstellationWithoutWrapping() {
        // Top-left root: nothing lies left of it or above it.
        AssertThat(ResonanceGridNavigation.Move(0, -1, 0, Mesh)).IsEqual(0);
        AssertThat(ResonanceGridNavigation.Move(0, 0, -1, Mesh)).IsEqual(0);
        // Bottom-right Major: nothing lies right of it or below it.
        AssertThat(ResonanceGridNavigation.Move(8, 1, 0, Mesh)).IsEqual(8);
        AssertThat(ResonanceGridNavigation.Move(8, 0, 1, Mesh)).IsEqual(8);
    }

    [TestCase]
    public void MovePrefersANodeStraightAheadOverANearerOneFarOffAxis() {
        // A closer node well off to the side must lose to the node directly
        // ahead: the cross-axis penalty is what makes an authored topology
        // navigable rather than merely reachable.
        var layout = new List<(float X, float Y)> {
            (0.10f, 0.50f),  // 0: origin
            (0.32f, 0.50f),  // 1: straight right, distance 0.22
            (0.26f, 0.65f)   // 2: closer in raw distance, but off axis
        };
        AssertThat(ResonanceGridNavigation.Move(0, 1, 0, layout)).IsEqual(1);
    }

    [TestCase]
    public void MoveClampsDegenerateInput() {
        // Out-of-range starting indices are pulled into the layout first.
        AssertThat(ResonanceGridNavigation.Move(-5, 0, 0, Mesh)).IsEqual(0);
        AssertThat(ResonanceGridNavigation.Move(42, 0, 0, Mesh)).IsEqual(8);
        // A null or empty layout resolves to index 0 rather than throwing.
        AssertThat(ResonanceGridNavigation.Move(3, 1, 0, new List<(float X, float Y)>())).IsEqual(0);
        AssertThat(ResonanceGridNavigation.Move(3, 1, 0, null)).IsEqual(0);
        // No direction pressed keeps the current node.
        AssertThat(ResonanceGridNavigation.Move(4, 0, 0, Mesh)).IsEqual(4);
    }
}
