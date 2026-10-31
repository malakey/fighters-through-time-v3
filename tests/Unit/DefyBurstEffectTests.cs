using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// MM-FX (meter and Defy update 2026-10-31, plan section 2.11): the pooled, code-drawn
/// <see cref="DefyBurstEffect"/> - it lives its 0.4 s, returns to the pool and is reused with
/// clean state, and its bright fills follow <c>ComfortSettings.FullScreenFlashAllowed</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DefyBurstEffectTests {

    [TestCase]
    public void TheRingLivesItsDurationThenReturnsToThePoolAndAReusedOneStartsClean() {
        var host = new Node2D { Name = "DefyBurstLifecycleHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            AssertThat(PoolManager.Instance != null).IsTrue();
            DefyBurstEffect first = DefyBurstEffect.Show(new Vector2(10f, -20f), host, DefyBurstEffect.FighterRadiusPixels);
            AssertThat(first != null).IsTrue();
            AssertThat(first.IsActive).IsTrue();
            AssertThat(first.GetParent()).IsEqual(host);
            AssertThat(first.GlobalPosition).IsEqual(new Vector2(10f, -20f));
            AssertThat(first.Radius).IsEqual(DefyBurstEffect.FighterRadiusPixels);
            first._Process(DefyBurstEffect.Seconds - 0.02f);
            AssertThat(first.IsActive).IsTrue();
            first._Process(0.04f);
            AssertThat(first.IsActive).IsFalse();
            AssertThat(first.GetParent() == host)
                .OverrideFailureMessage("The finished ring must leave the level for the pool.")
                .IsFalse();

            DefyBurstEffect second = DefyBurstEffect.Show(new Vector2(5f, 5f), host);
            AssertThat(second != null).IsTrue();
            AssertThat(second.IsActive).IsTrue();
            AssertThat(second.Age).IsEqual(0f);
            AssertThat(second.Radius).IsEqual(DefyBurstEffect.StoryRadiusPixels);
            AssertThat(second.Modulate).IsEqual(Colors.White);
            second._Process(1f);
            AssertThat(DefyBurstEffect.Show(Vector2.Zero, null) == null).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheBrightFillsFollowFullScreenFlashAllowed() {
        var host = new Node2D { Name = "DefyBurstComfortHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        bool wasReduced = ComfortSettings.ReducedTemporalEffects;
        try {
            ComfortSettings.Apply(false);
            DefyBurstEffect normal = DefyBurstEffect.Show(Vector2.Zero, host);
            AssertThat(normal.FillsAllowed).IsTrue();
            normal._Process(1f);

            ComfortSettings.Apply(true);
            DefyBurstEffect reduced = DefyBurstEffect.Show(Vector2.Zero, host);
            AssertThat(reduced.FillsAllowed).IsFalse();
            // The ring is still drawn and still ends: reduced effects only drop the fills.
            AssertThat(reduced.IsActive).IsTrue();
            reduced._Process(1f);
            AssertThat(reduced.IsActive).IsFalse();
        } finally {
            ComfortSettings.Apply(wasReduced);
            host.Free();
        }
    }
}
