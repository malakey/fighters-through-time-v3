using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// FU-BLOCK (feel and UI update 2026-10-25; ledger <c>USER-BLOCK-SUCCESS-EFFECT</c>):
/// the lifecycle of the pooled, code-drawn <see cref="BlockSparkEffect"/> -
/// spawn, kind-specific duration, end, return to the pool and reuse with clean
/// state. The effect is aged by calling <c>_Process</c> directly.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BlockSparkEffectTests {

    [TestCase]
    public void EachKindLivesItsOwnDurationThenReturnsToThePool() {
        var host = new Node2D { Name = "BlockSparkLifecycleHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            AssertThat(PoolManager.Instance != null).IsTrue();
            foreach (BlockSparkKind kind in new[] { BlockSparkKind.Ordinary, BlockSparkKind.Perfect, BlockSparkKind.Shatter }) {
                BlockSparkEffect effect = BlockSparkEffect.Show(kind, new Vector2(10f, -20f), facingRight: false, host);
                AssertThat(effect != null).IsTrue();
                AssertThat(effect.IsActive).IsTrue();
                AssertThat(effect.Kind).IsEqual(kind);
                AssertThat(effect.GetParent()).IsEqual(host);
                AssertThat(effect.GlobalPosition).IsEqual(new Vector2(10f, -20f));
                AssertThat(effect.Facing).IsEqual(-1f);
                float expected = kind switch {
                    BlockSparkKind.Perfect => 0.40f,
                    BlockSparkKind.Shatter => 0.45f,
                    _ => 0.28f
                };
                AssertThat(effect.Duration).IsEqual(expected);

                // Alive just before its duration, gone just after.
                effect._Process(expected - 0.02f);
                AssertThat(effect.IsActive).IsTrue();
                effect._Process(0.04f);
                AssertThat(effect.IsActive).IsFalse();
                AssertThat(effect.GetParent() == host)
                    .OverrideFailureMessage($"{kind}: the finished effect must leave the level for the pool.")
                    .IsFalse();
            }
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void AReusedEffectStartsClean() {
        var host = new Node2D { Name = "BlockSparkReuseHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            BlockSparkEffect first = BlockSparkEffect.Show(BlockSparkKind.Shatter, Vector2.Zero, true, host);
            first._Process(0.2f);
            AssertThat(first.Age > 0.19f).IsTrue();
            first._Process(1f); // ends and releases
            AssertThat(first.IsActive).IsFalse();

            BlockSparkEffect second = BlockSparkEffect.Show(BlockSparkKind.Ordinary, new Vector2(5f, 5f), false, host);
            AssertThat(second != null).IsTrue();
            AssertThat(second.IsActive).IsTrue();
            AssertThat(second.Kind).IsEqual(BlockSparkKind.Ordinary);
            AssertThat(second.Age).IsEqual(0f);
            AssertThat(second.Duration).IsEqual(BlockSparkEffect.OrdinarySeconds);
            AssertThat(second.Facing).IsEqual(-1f);
            AssertThat(second.Modulate).IsEqual(Colors.White);
            second._Process(1f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ShowWithoutAParentSpawnsNothing() {
        AssertThat(BlockSparkEffect.Show(BlockSparkKind.Ordinary, Vector2.Zero, true, null) == null).IsTrue();
    }
}
