using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the V7.2 manual-rewind building blocks that are drivable without live
/// input: the history buffer's scrub peek, the commit's
/// nearest-grounded-at-depth landing, the PathMovingPlatform's own recorded
/// scrub (the world-interaction exemplar) with its V7.3 cancel snap-back, and
/// the V7.3 12-second manual-rewind cooldown. (The Stasis Echo's plate and
/// beam interactions moved to StasisEchoPhysicsTests, which drive the REAL
/// physics path instead of calling RegisterBody directly.)
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ManualRewindScrubTests {

    [TestCase]
    public void TheBufferPeeksAtScrubDepthAndCommitsToTheNearestGroundedFrame() {
        var buffer = new ChronalRewindBuffer();
        // 100 frames walking right; frames 0-49 grounded, 50-99 airborne.
        for (int frame = 0; frame < 100; frame++) {
            buffer.Record(new RewindFrame(
                new Vector2(frame * 10f, 0f), frame < 50, true, "run"));
        }

        AssertThat(buffer.TryPeek(1, out RewindFrame newest)).IsTrue();
        AssertThat(newest.Position.X).IsEqual(990f);
        AssertThat(buffer.TryPeek(30, out RewindFrame back)).IsTrue();
        AssertThat(back.Position.X).IsEqual(700f);
        // Depth past the recorded history clamps to the oldest frame.
        AssertThat(buffer.TryPeek(500, out RewindFrame oldest)).IsTrue();
        AssertThat(oldest.Position.X).IsEqual(0f);

        // A commit at depth 30 (an airborne frame) lands on the nearest
        // grounded frame at or beyond that depth — frame 49, depth 51.
        var path = buffer.BuildPlaybackPath(Vector2.Zero, 1, 30);
        AssertThat(path[^1].IsGrounded).IsTrue();
        AssertThat(path[^1].Position.X).IsEqual(490f);
    }

    [TestCase]
    public void ThePathPlatformScrubsAlongItsOwnRecordedPathAndResumesFromThere() {
        var platform = new PathMovingPlatform {
            Name = "ScrubPlatform",
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
                .OverrideFailureMessage("The scrub must walk the platform back along its own path.")
                .IsTrue();
            float scrubbedX = platform.Position.X;

            // While scrubbing, its own motion is suspended.
            platform._PhysicsProcess(1.0 / 60.0);
            AssertThat(platform.Position.X).IsEqual(scrubbedX);

            // Ending the scrub resumes motion from the scrubbed position —
            // the checkpoint-state snap must not undo it.
            platform.EndRewindScrub();
            EventBus.Instance?.RaiseRewindTriggered(Vector2.Zero);
            AssertThat(platform.Position.X).IsEqual(scrubbedX);
            platform._PhysicsProcess(1.0 / 60.0);
            AssertThat(platform.Position.X > scrubbedX).IsTrue();
        } finally {
            platform.Free();
        }
    }

    [TestCase]
    public void ACancelledScrubSnapsThePlatformBackToThePresent() {
        // V7.3: cancel is "the preview never happened". The old cancel path
        // called EndRewindScrub, which left the platform at the scrubbed
        // position AND armed the skip-next-restore flag with no rewind event
        // coming — so the NEXT real rewind's restore was silently swallowed.
        var platform = new PathMovingPlatform {
            Name = "CancelScrubPlatform",
            Waypoints = new[] { Vector2.Zero, new Vector2(400f, 0f) },
            Speed = 120f,
            EndpointWaitSeconds = 0f
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(platform);
        try {
            for (int frame = 0; frame < 90; frame++) platform._PhysicsProcess(1.0 / 60.0);
            Vector2 present = platform.Position;

            platform.BeginRewindScrub();
            platform.ApplyRewindScrub(60);
            AssertThat(platform.Position.X < present.X).IsTrue();

            platform.CancelRewindScrub();
            AssertThat(platform.Position)
                .OverrideFailureMessage("A cancelled scrub must snap the platform back to the present.")
                .IsEqual(present);

            // And the next REAL rewind's checkpoint restore still lands (the
            // cancel armed no skip flag). Checkpoint state was captured at
            // the initial waypoint by default (_Ready position).
            platform._PhysicsProcess(1.0 / 60.0);
            EventBus.Instance?.RaiseRewindTriggered(Vector2.Zero);
            AssertThat(platform.Position.X)
                .OverrideFailureMessage("The rewind after a cancelled scrub must restore checkpoint state.")
                .IsEqual(0f);
        } finally {
            platform.Free();
        }
    }

    [TestCase]
    public void TheManualRewindCooldownArmsOnCommitAndScriptedRewindsAreExempt() {
        AssertThat(ChronalRewindManager.ManualRewindCooldownSeconds).IsEqual(12f);
        var manager = new ChronalRewindManager { Name = "CooldownTestManager" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(manager);
        try {
            // Ready by default; committing arms the 12 s cooldown.
            AssertThat(manager.IsManualRewindOffCooldown).IsTrue();
            manager.StartManualRewindCooldown();
            AssertThat(manager.ManualRewindCooldownRemaining).IsEqual(12f);
            AssertThat(manager.IsManualRewindOffCooldown)
                .OverrideFailureMessage("The verb must be gated while the cooldown ticks.")
                .IsFalse();

            // The cooldown ticks down in real play time (no player needed).
            for (int frame = 0; frame < 60; frame++) manager._PhysicsProcess(1.0 / 60.0);
            AssertThat(manager.ManualRewindCooldownRemaining).IsEqualApprox(11f, 0.05f);

            // Scripted tutorial rewinds bypass the cooldown entirely: they
            // neither honor a live one nor arm a new one.
            manager.ScriptedFreeRewind = true;
            AssertThat(manager.IsManualRewindOffCooldown).IsTrue();
            manager.ScriptedFreeRewind = false;

            for (int frame = 0; frame < 60 * 12; frame++) manager._PhysicsProcess(1.0 / 60.0);
            AssertThat(manager.ManualRewindCooldownRemaining).IsEqual(0f);
            AssertThat(manager.IsManualRewindOffCooldown).IsTrue();

            manager.ScriptedFreeRewind = true;
            manager.StartManualRewindCooldown();
            AssertThat(manager.ManualRewindCooldownRemaining)
                .OverrideFailureMessage("A scripted free rewind must not arm the cooldown.")
                .IsEqual(0f);
        } finally {
            manager.Free();
        }
    }
}
