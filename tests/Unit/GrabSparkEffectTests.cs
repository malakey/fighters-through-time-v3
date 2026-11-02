using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Grab pass 2026-10-31: the lifecycle of the pooled, code-drawn
/// <see cref="GrabSparkEffect"/> - spawn, duration, return to the pool and reuse
/// with clean state. The effect is aged by calling <c>_Process</c> directly.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GrabSparkEffectTests {

    [TestCase]
    public void TheEffectLivesItsDurationThenReturnsToThePool() {
        var host = new Node2D { Name = "GrabSparkLifecycleHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            AssertThat(PoolManager.Instance != null).IsTrue();
            GrabSparkEffect effect = GrabSparkEffect.Show(new Vector2(10f, -20f), facingRight: false, host);
            AssertThat(effect != null).IsTrue();
            AssertThat(effect.IsActive).IsTrue();
            AssertThat(effect.GetParent()).IsEqual(host);
            AssertThat(effect.GlobalPosition).IsEqual(new Vector2(10f, -20f));
            AssertThat(effect.Facing).IsEqual(-1f);
            AssertThat(effect.Duration).IsEqual(GrabSparkEffect.Seconds);

            effect._Process(GrabSparkEffect.Seconds - 0.02f);
            AssertThat(effect.IsActive).IsTrue();
            effect._Process(0.04f);
            AssertThat(effect.IsActive).IsFalse();
            AssertThat(effect.GetParent() == host)
                .OverrideFailureMessage("The finished effect must leave the level for the pool.")
                .IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void AReusedEffectStartsClean() {
        var host = new Node2D { Name = "GrabSparkReuseHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            GrabSparkEffect first = GrabSparkEffect.Show(Vector2.Zero, true, host);
            first._Process(0.2f);
            AssertThat(first.Age > 0.19f).IsTrue();
            first._Process(1f);
            AssertThat(first.IsActive).IsFalse();

            GrabSparkEffect second = GrabSparkEffect.Show(new Vector2(5f, 5f), false, host);
            AssertThat(second != null).IsTrue();
            AssertThat(second.IsActive).IsTrue();
            AssertThat(second.Age).IsEqual(0f);
            AssertThat(second.Facing).IsEqual(-1f);
            AssertThat(second.Modulate).IsEqual(Colors.White);
            second._Process(1f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ShowWithoutAParentSpawnsNothing() {
        AssertThat(GrabSparkEffect.Show(Vector2.Zero, true, null) == null).IsTrue();
    }
}
