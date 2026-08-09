using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B6: the three Chronal Rewind payload fields that shipped with no
/// consumer — <c>GhostTrailEnabled</c>, <c>ReverseSweepEnabled</c>,
/// <c>ClockTickEnabled</c>.
///
/// <para><c>ChronalRewindManager</c> has been populating all three since the rewind
/// system landed, and <c>StoryDropsAndRewindTests</c> already asserted the manager
/// sets them — so the fields looked covered while nothing rendered or played
/// anything. These tests cover the consumer side: the edges, the cadence, and the
/// ghost sampling.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RewindCuePresentationTests {

    private static RewindPresentationPayload Payload(RewindPresentationPhase phase, bool active) =>
        FTT.Environment.ChronalRewindManager.CreatePresentationPayload(phase, Vector2.Zero, active);

    [TestCase]
    public void TheReverseSweepFiresOncePerRewindNotOncePerPayload() {
        var cues = new RewindCueState();

        AssertThat(cues.Apply(Payload(RewindPresentationPhase.Started, true))).IsTrue();
        // Playback republishes every frame while the rewind runs.
        for (int frame = 0; frame < 30; frame++) {
            AssertThat(cues.Apply(Payload(RewindPresentationPhase.Playback, true))).IsFalse();
        }
        AssertThat(cues.SweepPlayed).IsTrue();

        // Landing ends the rewind; the next one sweeps again.
        cues.Apply(Payload(RewindPresentationPhase.Landed, false));
        AssertThat(cues.IsActive).IsFalse();
        AssertThat(cues.Apply(Payload(RewindPresentationPhase.Started, true))).IsTrue();
    }

    [TestCase]
    public void TheClockTicksOnTheAuthoredCadenceAndStopsWhenTheRewindEnds() {
        var cues = new RewindCueState();
        cues.Apply(Payload(RewindPresentationPhase.Started, true));

        // The first tick lands immediately rather than a quarter second late.
        AssertThat(cues.Advance(0.001f)).IsEqual(1);

        int ticks = 1;
        for (int frame = 0; frame < 60; frame++) ticks += cues.Advance(1f / 60f);
        // One second of playback at the authored interval.
        AssertThat(ticks).IsEqual(1 + Mathf.FloorToInt(1f / RewindCueState.ClockTickIntervalSeconds));
        AssertThat(cues.ClockTicks).IsEqual(ticks);

        cues.Apply(Payload(RewindPresentationPhase.Landed, false));
        AssertThat(cues.Advance(5f)).IsEqual(0);
        AssertThat(cues.ClockTicks).IsEqual(0);
    }

    [TestCase]
    public void ADisabledCueFieldSuppressesItsCueWithoutSuppressingTheOthers() {
        var cues = new RewindCueState();
        var payload = new RewindPresentationPayload {
            Phase = RewindPresentationPhase.Started,
            GhostTrailEnabled = true,
            ReverseSweepEnabled = false,
            ClockTickEnabled = false
        };

        AssertThat(cues.Apply(payload)).IsFalse();
        AssertThat(cues.GhostTrailActive).IsTrue();
        AssertThat(cues.Advance(1f)).IsEqual(0);
    }

    [TestCase]
    public void TimelineCollapseCountsAsAnActiveRewind() {
        var cues = new RewindCueState();
        AssertThat(cues.Apply(Payload(RewindPresentationPhase.TimelineCollapse, true))).IsTrue();
        AssertThat(cues.IsActive).IsTrue();
        AssertThat(cues.GhostTrailActive).IsTrue();
    }

    [TestCase]
    public void GhostsFadeMonotonicallyBackwardsThroughTheHistory() {
        float previous = float.MaxValue;
        int previousFrames = -1;
        for (int index = 0; index < RewindGhostTrail.GhostCount; index++) {
            float alpha = RewindGhostTrail.GhostAlpha(index);
            AssertFloat(alpha).IsGreater(0f);
            AssertThat(alpha < previous).IsTrue();
            previous = alpha;

            int framesAgo = RewindGhostTrail.GhostFramesAgo(index);
            AssertThat(framesAgo > previousFrames).IsTrue();
            previousFrames = framesAgo;
        }
        // The whole trail must fit inside the history buffer that feeds it.
        AssertThat(previousFrames).IsLess(FTT.Environment.TemporalPositionHistory.HistoryFrames);
    }

    [TestCase]
    public void TheTrailSamplesTheRealPositionHistoryAndReleasesItsGhostsWhenOff() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var scene = new Node2D { Name = "GhostTrailScene" };
        tree.Root.AddChild(scene);

        var player = new Node2D { Name = "GhostTrailPlayer" };
        var history = new FTT.Environment.TemporalPositionHistory { Name = "TemporalPositionHistory" };
        player.AddChild(history);
        var sprite = new Sprite2D { Name = "Visual", Texture = new PlaceholderTexture2D() };
        player.AddChild(sprite);
        scene.AddChild(player);
        player.AddToGroup("Players");

        var trail = new RewindGhostTrail { Name = RewindGhostTrail.NodeName };
        scene.AddChild(trail);

        try {
            for (int frame = 0; frame < 120; frame++) history.Record(new Vector2(frame * 4f, 0f));

            trail.SetActive(true);
            trail.Refresh();
            AssertThat(trail.AllocatedGhosts).IsEqual(RewindGhostTrail.GhostCount);

            var lead = trail.GetNode<Sprite2D>("Ghost_0");
            AssertThat(lead.Visible).IsTrue();
            // Ghost 0 sits where the player was GhostFramesAgo(0) frames back, offset
            // by the sprite's own local position so it draws on the body, not at its feet.
            history.TryGetFramesAgo(RewindGhostTrail.GhostFramesAgo(0), out Vector2 expected);
            AssertFloat(lead.GlobalPosition.X).IsEqualApprox(expected.X + sprite.Position.X, 0.01);

            trail.SetActive(false);
            AssertThat(trail.Visible).IsFalse();
            AssertThat(lead.Visible).IsFalse();
        } finally {
            scene.Free();
        }
    }

    [TestCase]
    public void TheTrailInstallsExactlyOncePerScene() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var scene = new Node2D { Name = "GhostTrailInstallScene" };
        tree.Root.AddChild(scene);
        var caller = new Node2D { Name = "GhostTrailCaller" };
        scene.AddChild(caller);

        try {
            RewindGhostTrail first = RewindGhostTrail.EnsureInstalled(caller);
            AssertObject(first).IsNotNull();
            // Whichever host it picked, a second call must find the same node.
            AssertObject(RewindGhostTrail.EnsureInstalled(caller)).IsSame(first);
            first.GetParent()?.RemoveChild(first);
            first.Free();
        } finally {
            scene.Free();
        }
    }

    [TestCase]
    public void TheOverlayDrivesTheCueStateFromTheBusPayload() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        EventBus bus = EventBus.Instance;
        AssertObject(bus).IsNotNull();

        var overlay = new RewindPresentationOverlay { Name = "RewindOverlayUnderTest" };
        tree.Root.AddChild(overlay);

        try {
            // The cue assets exist; a missing one would silently no-op at runtime,
            // which is exactly how A2 found the KO stinger had been dead since P6.
            AssertThat(ResourceLoader.Exists(RewindPresentationOverlay.ReverseSweepPath)).IsTrue();
            AssertThat(ResourceLoader.Exists(RewindPresentationOverlay.ClockTickPath)).IsTrue();

            bus.RaiseRewindPresentation(Payload(RewindPresentationPhase.Started, true));
            AssertThat(overlay.Cues.IsActive).IsTrue();
            AssertThat(overlay.Cues.SweepPlayed).IsTrue();
            AssertThat(overlay.Cues.GhostTrailActive).IsTrue();

            bus.RaiseRewindPresentation(Payload(RewindPresentationPhase.Landed, false));
            AssertThat(overlay.Cues.IsActive).IsFalse();
            AssertThat(overlay.Cues.GhostTrailActive).IsFalse();
        } finally {
            // _ExitTree unsubscribes from the shared bus and drops the duck.
            overlay.Free();
        }
    }
}
