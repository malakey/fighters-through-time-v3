using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>
/// MM-HUD (meter and Defy update, 2026-10-31): the code-drawn Momentum pips. Four pips filling continuously within a pip,
/// a dim state for the Dormant Levels 0 to 2, one bloom when a pip completes by regen or a gain (not when a seed or a restore
/// jumps the gauge), and a redraw that rebuilds nothing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MomentumPipsTests {

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    private static async System.Threading.Tasks.Task Frames(int count) {
        for (int i = 0; i < count; i++) await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void TheRowFillsPipByPipAndContinuouslyInsideAPip() {
        // The pure rule: the gauge in pips fills pip i by clamp(pips - i).
        AssertFloat(MomentumPips.PipFill(2.5f, 0)).IsEqual(1f);
        AssertFloat(MomentumPips.PipFill(2.5f, 1)).IsEqual(1f);
        AssertFloat(MomentumPips.PipFill(2.5f, 2)).IsEqual(0.5f);
        AssertFloat(MomentumPips.PipFill(2.5f, 3)).IsEqual(0f);
        AssertThat(MomentumPips.PipCount).IsEqual(4);

        var pips = new MomentumPips { Size = new Vector2(120f, MomentumPips.RowHeight) };
        try {
            pips.Value = 2.5f;
            AssertThat(pips.FullPips).IsEqual(2);
            // Values clamp to 0..4 and a NaN reads as empty.
            pips.Value = 9f;
            AssertFloat(pips.Value).IsEqual(4f);
            AssertThat(pips.FullPips).IsEqual(4);
            pips.Value = float.NaN;
            AssertFloat(pips.Value).IsEqual(0f);
            pips.Value = -3f;
            AssertFloat(pips.Value).IsEqual(0f);
        } finally {
            pips.Free();
        }
    }

    [TestCase]
    public async System.Threading.Tasks.Task ACompletedPipBloomsOnceButASeedOrARestoreJumpDoesNot() {
        var host = new Control { Size = new Vector2(120f, 20f) };
        Tree.Root.AddChild(host);
        var pips = new MomentumPips { Size = new Vector2(120f, MomentumPips.RowHeight) };
        host.AddChild(pips);
        try {
            await Frames(2);
            // A seed (0 to 2.0) and a restore move the gauge by whole pips: no bloom.
            pips.Value = 2f;
            AssertThat(pips.Blooms).OverrideFailureMessage("a seed jump bloomed").IsEqual(0);
            // Regen creeps across the third pip's boundary: one bloom.
            pips.Value = 2.97f;
            AssertThat(pips.Blooms).IsEqual(0);
            pips.Value = 3.02f;
            AssertThat(pips.Blooms).OverrideFailureMessage("completing a pip by regen bloomed once").IsEqual(1);
            // Spending and creeping inside a pip do not bloom.
            pips.Value = 2.02f;
            pips.Value = 2.4f;
            AssertThat(pips.Blooms).IsEqual(1);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public async System.Threading.Tasks.Task TheDimmedRowAndARedrawBuildTheirPolygonsOnlyWhenTheSizeOrValueMoves() {
        var host = new Control { Size = new Vector2(120f, 20f) };
        Tree.Root.AddChild(host);
        var pips = new MomentumPips { Size = new Vector2(120f, MomentumPips.RowHeight) };
        host.AddChild(pips);
        try {
            pips.Value = 1.5f;
            await Frames(3);
            int builds = pips.GeometryBuilds;
            AssertThat(builds).IsGreater(0);
            // Dimming and several redraws at the same size and value rebuild nothing.
            pips.Dimmed = true;
            AssertThat(pips.Dimmed).IsTrue();
            pips.QueueRedraw();
            await Frames(3);
            AssertThat(pips.GeometryBuilds).OverrideFailureMessage("a redraw rebuilt the polygons").IsEqual(builds);
            // A value move does.
            pips.Value = 1.75f;
            await Frames(2);
            AssertThat(pips.GeometryBuilds).IsGreater(builds);
        } finally {
            host.Free();
        }
    }
}
