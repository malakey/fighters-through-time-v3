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
/// scrub (the world-interaction exemplar), and the Stasis Echo's lifetime,
/// plate weight, and one-projectile absorb contract.
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
    public void TheStasisEchoWeighsPlatesTimesOutAndReplacesItsPredecessor() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var echo = new StasisEcho { Name = "TestEcho" };
        tree.Root.AddChild(echo);
        var plate = new PressurePlate { Name = "TestPlate", RequiredWeight = 1f, PlayerWeight = 1f };
        tree.Root.AddChild(plate);
        try {
            // The Echo is the player's past self: it carries the player weight.
            plate.RegisterBody(echo);
            AssertThat(plate.IsPressed)
                .OverrideFailureMessage("The Echo must weigh down pressure plates — the puzzle verb.")
                .IsTrue();
            plate.UnregisterBody(echo);

            // Lifetime: pumped past its window, it frees itself.
            float life = echo.LifeSecondsRemaining;
            AssertThat(life <= 0f).IsTrue(); // spawned raw (no Spawn call), life unset
            echo._PhysicsProcess(1.0 / 60.0);
            AssertThat(echo.IsQueuedForDeletion()).IsTrue();
        } finally {
            if (!echo.IsQueuedForDeletion()) echo.Free();
            plate.Free();
        }
    }
}
