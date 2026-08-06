using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
[RequireGodotRuntime]
public class ChronalRewindTests {
    [TestCase]
    public void RingKeepsFiveSecondsAndFindsMostRecentGroundedFrame() {
        var buffer = new ChronalRewindBuffer();
        for (int frame = 0; frame < 400; frame++) {
            bool grounded = frame == 350;
            buffer.Record(new RewindFrame(new Vector2(frame, 10), grounded, true, "run"));
        }

        AssertThat(buffer.Count).IsEqual(300);
        AssertThat(buffer.TryFindSafeLanding(Vector2.Zero, out RewindFrame landing)).IsTrue();
        AssertThat(landing.Position).IsEqual(new Vector2(350, 10));
    }

    [TestCase]
    public void LastGroundedFallbackSurvivesLongerThanRingCapacity() {
        var buffer = new ChronalRewindBuffer(8);
        buffer.Record(new RewindFrame(new Vector2(4, 2), true, false, "idle"));
        for (int frame = 0; frame < 20; frame++) {
            buffer.Record(new RewindFrame(new Vector2(10 + frame, 100), false, true, "fall"));
        }

        AssertThat(buffer.TryFindSafeLanding(new Vector2(-9, -9), out RewindFrame landing)).IsTrue();
        AssertThat(landing.Position).IsEqual(new Vector2(4, 2));
        AssertThat(buffer.BuildPlaybackPath(Vector2.Zero, 4)[2].Position).IsEqual(new Vector2(4, 2));
    }

    [TestCase]
    public void PhysicalCheckpointIsUsedWhenNoGroundedHistoryExists() {
        var buffer = new ChronalRewindBuffer(8);
        buffer.Record(new RewindFrame(new Vector2(10, 100), false, true, "fall"));
        Vector2 checkpoint = new(42, 88);

        AssertThat(buffer.TryFindSafeLanding(checkpoint, out RewindFrame landing)).IsFalse();
        AssertThat(landing.Position).IsEqual(checkpoint);
        AssertThat(landing.IsGrounded).IsTrue();
    }

    [TestCase]
    public void DifficultyRulesMatchDesign() {
        AssertThat(ChronalRewindManager.GetMaximumRewinds(Difficulty.Easy)).IsEqual(5);
        AssertThat(ChronalRewindManager.GetMaximumRewinds(Difficulty.Normal)).IsEqual(3);
        AssertThat(ChronalRewindManager.GetMaximumRewinds(Difficulty.Hard)).IsEqual(1);
        AssertThat(ChronalRewindManager.ApplyCheckpointRefresh(Difficulty.Easy, 1)).IsEqual(5);
        AssertThat(ChronalRewindManager.ApplyCheckpointRefresh(Difficulty.Normal, 1)).IsEqual(2);
        AssertThat(ChronalRewindManager.ApplyCheckpointRefresh(Difficulty.Normal, 3)).IsEqual(3);
        AssertThat(ChronalRewindManager.ApplyCheckpointRefresh(Difficulty.Hard, 0)).IsEqual(0);
        AssertThat(ChronalRewindManager.GetHPRestorePercent(Difficulty.Hard)).IsEqual(0.3f);
    }
}
