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
    public void RingKeepsEightSecondsOfHistory() {
        // 2026-08-11: raised from 300 frames (5 s) by user direction.
        // 2026-08-15: cut from 900 frames (15 s) to 480 (8 s) by user direction.
        var buffer = new ChronalRewindBuffer();
        for (int frame = 0; frame < 1000; frame++) {
            buffer.Record(new RewindFrame(new Vector2(frame, 10), frame == 700, true, "run"));
        }

        AssertThat(ChronalRewindBuffer.DefaultCapacity).IsEqual(480);
        AssertThat(buffer.Count).IsEqual(480);
        AssertThat(buffer.TryFindSafeLanding(Vector2.Zero, out RewindFrame landing)).IsTrue();
        AssertThat(landing.Position).IsEqual(new Vector2(700, 10));
    }

    [TestCase]
    public void TheMechanicTakesHalfTheRewoundDurationAndOpensWithAHold() {
        // 2026-08-15 rework: an 8 s history is a 4 s mechanic — a 0.75 s hold
        // before any frame plays back, then 3.25 s of continuous playback.
        AssertThat(ChronalRewindManager.PreRewindHoldFrames).IsEqual(45);
        AssertThat(ChronalRewindManager.MechanicDurationFraction).IsEqual(0.5f);
        AssertThat(ChronalRewindManager.ComputePlaybackTicks(480)).IsEqual(195);
        AssertThat(ChronalRewindManager.ComputeTotalMechanicTicks(480)).IsEqual(240);
        // Proportional for a partial history: 6 s rewound → 3 s mechanic.
        AssertThat(ChronalRewindManager.ComputeTotalMechanicTicks(360)).IsEqual(180);
        // A very young buffer never compresses playback below half a second.
        AssertThat(ChronalRewindManager.ComputePlaybackTicks(60)).IsEqual(30);
        AssertThat(ChronalRewindManager.ComputePlaybackTicks(0)).IsEqual(30);
    }

    [TestCase]
    public void PlaybackWalksTheFullResolutionPathContinuouslyAndLandsOnTheLastFrame() {
        // 480 frames over 195 ticks: about 2.5 frames per tick, monotonic, no
        // 12-frame teleports, first tick starts at the newest frame's side and
        // the final tick is exactly the landing frame.
        const int count = 480, ticks = 195;
        int previous = -1;
        for (int tick = 1; tick <= ticks; tick++) {
            int index = ChronalRewindManager.PathIndexForTick(tick, ticks, count);
            AssertThat(index >= previous).OverrideFailureMessage($"tick {tick} went backwards").IsTrue();
            AssertThat(index - previous <= 3)
                .OverrideFailureMessage($"tick {tick} jumped {index - previous} frames").IsTrue();
            previous = index;
        }
        AssertThat(previous).IsEqual(count - 1);
        AssertThat(ChronalRewindManager.PathIndexForTick(1, 30, 60) < 60).IsTrue();
        AssertThat(ChronalRewindManager.PathIndexForTick(30, 30, 60)).IsEqual(59);
        AssertThat(ChronalRewindManager.PathIndexForTick(1, 30, 1)).IsEqual(0);
    }

    [TestCase]
    public void AGroundedDeathRewindsTheFullRecordedHistoryNotZeroFrames() {
        // Regression: the playback used to stop at the FIRST grounded frame
        // walking backward, so any death on solid ground rewound zero distance
        // — the character froze into the death pose and "the rewind never
        // occurred". The landing must be the grounded frame nearest the target
        // depth instead.
        var buffer = new ChronalRewindBuffer();
        for (int frame = 0; frame < 480; frame++) {
            buffer.Record(new RewindFrame(new Vector2(frame, 10), true, true, "run"));
        }

        // Stride 1 is what the manager uses now (the pacing decides speed):
        // the path is the entire history, newest first, landing on the oldest.
        var path = buffer.BuildPlaybackPath(Vector2.Zero, 1, ChronalRewindBuffer.DefaultCapacity);
        AssertThat(path.Count).IsEqual(480);
        AssertThat(path[0].Position).IsEqual(new Vector2(479, 10));
        AssertThat(path[path.Count - 1].Position).IsEqual(new Vector2(0, 10));
    }

    [TestCase]
    public void AnAirborneRingTailTrimsThePathBackToTheDeepestGroundedFrame() {
        // Ring wrapped mid-jump: the oldest surviving frames are airborne, so
        // the landing is the deepest grounded frame and the path must not run
        // past it into the airborne tail.
        var buffer = new ChronalRewindBuffer(120);
        for (int frame = 0; frame < 20; frame++) {
            buffer.Record(new RewindFrame(new Vector2(1000 + frame, 60), false, true, "fall"));
        }
        for (int frame = 0; frame < 100; frame++) {
            buffer.Record(new RewindFrame(new Vector2(frame, 10), true, true, "run"));
        }

        var path = buffer.BuildPlaybackPath(Vector2.Zero, 12, ChronalRewindBuffer.DefaultCapacity);
        AssertThat(path[path.Count - 1].Position).IsEqual(new Vector2(0, 10));
        foreach (RewindFrame frame in path) {
            AssertThat(frame.Position.Y).IsEqual(10f);
        }
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

        // V7.6: Time Freeze is the ONE time mechanic with no difficulty spread.
        // One authored row, identical on Easy, Normal and Hard: 5 s, no charges,
        // 45-second cooldown.
        AssertThat(TimeFreezeController.FreezeSeconds).IsEqual(5f);
        AssertThat(TimeFreezeController.CooldownSeconds).IsEqual(45f);
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            // Nothing in the Time Freeze contract reads difficulty at all, which
            // is the pin: the rewind rules above still differ per difficulty and
            // these two constants deliberately do not.
            AssertThat(TimeFreezeController.FreezeSeconds).IsEqual(5f);
            AssertThat(TimeFreezeController.CooldownSeconds).IsEqual(45f);
            _ = difficulty;
        }
    }

    [TestCase]
    public void ThePathPlatformReversesAlongItsOwnRecordedPathDuringADeathRewind() {
        // Salvaged from the retired ManualRewindScrubTests when the manual scrub
        // verb was deleted (Package 11 A2): the platform reversal is a DEATH
        // rewind behaviour and outlived the verb that first exercised it. Time
        // Freeze is the opposite case and is pinned in TimeFreezeTests.
        var platform = new PathMovingPlatform {
            Name = "DeathRewindPlatform",
            Waypoints = new[] { Vector2.Zero, new Vector2(400f, 0f) },
            Speed = 120f,
            EndpointWaitSeconds = 0f
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(platform);
        try {
            for (int frame = 0; frame < 90; frame++) platform._PhysicsProcess(1.0 / 60.0);
            Vector2 present = platform.Position;
            AssertThat(present.X > 100f).IsTrue();

            platform.BeginRewindScrub();
            platform.ApplyRewindScrub(60);
            AssertThat(platform.Position.X < present.X)
                .OverrideFailureMessage("The rewind must walk the platform back along its own path.")
                .IsTrue();
            float rewoundX = platform.Position.X;

            // While the rewind owns it, its own motion is suspended.
            platform._PhysicsProcess(1.0 / 60.0);
            AssertThat(platform.Position.X).IsEqual(rewoundX);

            // Ending the reversal resumes motion from the rewound position — the
            // checkpoint-state snap at rewind end must not undo it.
            platform.EndRewindScrub();
            EventBus.Instance?.RaiseRewindTriggered(Vector2.Zero);
            AssertThat(platform.Position.X).IsEqual(rewoundX);
            platform._PhysicsProcess(1.0 / 60.0);
            AssertThat(platform.Position.X > rewoundX).IsTrue();
        } finally {
            platform.Free();
        }
    }
}
