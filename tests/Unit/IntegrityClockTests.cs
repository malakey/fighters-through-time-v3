using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A8 / F01. The Timeline Integrity era-clock in the Story HUD's
/// top-right panel.
///
/// <para>The readout this replaces was a code-built percentage label that inferred
/// "draining" from a frame-to-frame fall in the value. That heuristic got both
/// interesting cases wrong: a frozen clock read as steady (indistinguishable from
/// safe), and a single-frame rounding wobble read as an active siphon. The
/// payload carries the real siphon count and the frozen flag instead, so the
/// widget renders state rather than guessing at it.</para>
///
/// <para>Driven through the EventBus, which is the whole point of §2.9: the
/// Integrity workstream publishes and never touches this scene.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IntegrityClockTests {

    [TestCase]
    public void TheClockFaceAndWedgeReadTheEventRatherThanPollingTheManager() {
        Node host = CreateHost("IntegrityClockHost");
        try {
            StoryHUD hud = AddHud(host);
            var wedge = hud.GetNode<RadialProgress>("SafeArea/TopRight_Panel/IntegrityClock/ClockWedge");
            var text = hud.GetNode<Label>("SafeArea/TopRight_Panel/IntegrityClock/IntegrityText");

            EventBus.Instance.RaiseTimelineIntegrityChanged(new IntegrityPayload {
                Percent = 62f, Tier = IntegrityTier.Stabilized, LivingExtractors = 0, Frozen = false
            });
            AssertFloat(wedge.Fraction).IsEqualApprox(0.62f, 0.0001f);
            AssertThat(text.Text.Contains("62")).IsTrue();

            EventBus.Instance.RaiseTimelineIntegrityChanged(new IntegrityPayload {
                Percent = 8f, Tier = IntegrityTier.Fractured, LivingExtractors = 0, Frozen = false
            });
            AssertFloat(wedge.Fraction).IsEqualApprox(0.08f, 0.0001f);
            AssertThat(text.Text.Contains("8")).IsTrue();
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// The siphon streams are bound to the count of living, engaged Extractors —
    /// not to a falling number. A frozen gauge shows no streams however low it is,
    /// because nothing is draining it.
    /// </summary>
    [TestCase]
    public void SiphonStreamsFollowLivingExtractorsAndVanishWhileFrozen() {
        Node host = CreateHost("IntegritySiphonHost");
        try {
            StoryHUD hud = AddHud(host);
            var streams = hud.GetNode<Label>("SafeArea/TopRight_Panel/IntegrityClock/SiphonStreams");

            EventBus.Instance.RaiseTimelineIntegrityChanged(new IntegrityPayload {
                Percent = 80f, Tier = IntegrityTier.Stabilized, LivingExtractors = 0,
                DrainPerSecond = 0f, Frozen = false
            });
            AssertThat(streams.Visible).IsFalse();

            EventBus.Instance.RaiseTimelineIntegrityChanged(new IntegrityPayload {
                Percent = 79f, Tier = IntegrityTier.Stabilized, LivingExtractors = 2,
                DrainPerSecond = 0.2f, Frozen = false
            });
            AssertThat(streams.Visible).IsTrue();
            AssertThat(streams.Text.Contains("2")).IsTrue();

            // The PreBoss seal (and a rewind/scrub pause) freezes the gauge: the
            // drain stops, so the streams must stop with it.
            EventBus.Instance.RaiseTimelineIntegrityChanged(new IntegrityPayload {
                Percent = 79f, Tier = IntegrityTier.Stabilized, LivingExtractors = 2,
                DrainPerSecond = 0.2f, Frozen = true
            });
            AssertThat(streams.Visible)
                .OverrideFailureMessage("A frozen gauge is not being siphoned.").IsFalse();
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// V7.6 Collapse Tremor is a HUD presentation level, not a boolean: &lt;20% is
    /// a faint cold wash, &lt;10% deepens it, and zero removes it. A steady tint
    /// with no flicker, which is also what C01a's reduced treatment wants.
    /// </summary>
    [TestCase]
    public void TheCollapseTremorOverlayFollowsItsThreeLevels() {
        Node host = CreateHost("IntegrityTremorHost");
        try {
            StoryHUD hud = AddHud(host);
            var overlay = hud.GetNode<ColorRect>("SafeArea/TremorOverlay");
            AssertThat(overlay.Visible).IsFalse();
            AssertThat(hud.TremorLevel).IsEqual(0);

            EventBus.Instance.RaiseCollapseTremorChanged(new TremorPayload { Level = 1 });
            AssertThat(hud.TremorLevel).IsEqual(1);
            AssertThat(overlay.Visible).IsTrue();
            float faint = overlay.Color.A;

            EventBus.Instance.RaiseCollapseTremorChanged(new TremorPayload { Level = 2 });
            AssertThat(hud.TremorLevel).IsEqual(2);
            AssertThat(overlay.Color.A > faint)
                .OverrideFailureMessage("Below 10% the tremor must intensify, not merely persist.")
                .IsTrue();

            EventBus.Instance.RaiseCollapseTremorChanged(new TremorPayload { Level = 0 });
            AssertThat(hud.TremorLevel).IsEqual(0);
            AssertThat(overlay.Visible).IsFalse();
        } finally {
            host.Free();
        }
    }

    private static StoryHUD AddHud(Node host) {
        StoryHUD hud = StoryHUD.CreateDefault();
        host.AddChild(hud);
        return hud;
    }

    private static Node CreateHost(string name) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = name };
        tree.Root.AddChild(host);
        return host;
    }
}
