using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The tutorial's Chronal Rewind calibration step runs a scripted rewind
/// through the real manager — rewind is otherwise only triggered by death, and
/// the old gate waited on an event nothing in the calibration phase could ever
/// raise (the 2026-08-09 soft-lock). These tests pin the scripted entry point:
/// it must run the full sequence without a death, never harm a healthy player,
/// and leave the pool refundable, so the tutorial can never strand again.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScriptedRewindTests {

    [TestCase]
    public void ScriptedRewindRunsTheFullSequenceWithoutADeath() {
        var tree = Engine.GetMainLoop() as SceneTree;
        AssertThat(tree).IsNotNull();
        var host = new Node2D { Name = "ScriptedRewindHost" };
        tree.Root.AddChild(host);
        try {
            StoryManager.Instance?.SetRewinds(3);
            PlayerController player = CharacterFactory.CreateCharacter("einstein", 0);
            host.AddChild(player);
            var manager = new ChronalRewindManager { Name = "RewindManager" };
            host.AddChild(manager);

            // Record a little movement history for the playback to scrub.
            for (int frame = 0; frame < 30; frame++) {
                player.GlobalPosition = new Vector2(400 + frame * 4, 850);
                manager._PhysicsProcess(1.0 / 60.0);
            }

            int poolBefore = manager.RemainingRewinds;
            AssertThat(poolBefore > 0).IsTrue();
            int hpBefore = player.CurrentHP;
            bool rewindEventFired = false;
            void OnRewindTriggered(Vector2 _) => rewindEventFired = true;
            EventBus.Instance.OnRewindTriggered += OnRewindTriggered;
            try {
                AssertThat(manager.TriggerScriptedRewind()).IsTrue();
                AssertThat(manager.IsRewinding).IsTrue();
                // A second trigger while one is running must be refused.
                AssertThat(manager.TriggerScriptedRewind()).IsFalse();

                for (int i = 0; i < 600 && manager.IsRewinding; i++) {
                    manager._PhysicsProcess(1.0 / 60.0);
                }
                AssertThat(manager.IsRewinding).IsFalse();
            } finally {
                EventBus.Instance.OnRewindTriggered -= OnRewindTriggered;
            }

            // The completion event is what advances the tutorial's gate.
            AssertThat(rewindEventFired).IsTrue();
            AssertThat(manager.RemainingRewinds).IsEqual(poolBefore - 1);
            // A demonstration must never damage a healthy player (the death
            // path still applies the difficulty restore from zero HP).
            AssertThat(player.CurrentHP).IsEqual(hpBefore);
            // Package 12 W1 (R03): the scripted demonstration ends in the same
            // Post-Landing Hold, and its protection starts at the thaw.
            AssertThat(manager.IsPostLandingHoldActive).IsTrue();
            AssertThat(manager.PostLandingHoldCause).IsEqual(StoryRecoveryHoldCause.ScriptedRewind);
            AssertThat(player.IsPostRewindInvulnerable).IsFalse();
            for (int i = 0; i < ChronalRewindManager.PostLandingHoldFrames; i++) manager._PhysicsProcess(1.0 / 60.0);
            AssertThat(manager.IsPostLandingHoldActive).IsFalse();
            AssertThat(player.IsPostRewindInvulnerable).IsTrue();

            // The tutorial refunds the demonstration's charge afterwards.
            manager.RefundRewind();
            AssertThat(manager.RemainingRewinds).IsEqual(poolBefore);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ScriptedRewindRefusesToRunWithAnEmptyPool() {
        var tree = Engine.GetMainLoop() as SceneTree;
        AssertThat(tree).IsNotNull();
        var host = new Node2D { Name = "ScriptedRewindEmptyPoolHost" };
        tree.Root.AddChild(host);
        try {
            StoryManager.Instance?.SetRewinds(0);
            PlayerController player = CharacterFactory.CreateCharacter("einstein", 0);
            host.AddChild(player);
            var manager = new ChronalRewindManager { Name = "RewindManager" };
            host.AddChild(manager);
            manager._PhysicsProcess(1.0 / 60.0);

            // An empty pool must refuse rather than trigger a Timeline
            // Collapse mid-demonstration.
            AssertThat(manager.TriggerScriptedRewind()).IsFalse();
            AssertThat(manager.IsRewinding).IsFalse();
        } finally {
            host.Free();
            StoryManager.Instance?.SetRewinds(3);
        }
    }
}
