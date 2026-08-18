using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The death-triggered rewind path (the only way a real playthrough ever
/// rewinds — the scripted tutorial demonstration has its own pins). A lethal
/// hit must start the rewind, play the buffer back, and leave the player
/// alive at the landing frame — including when the killing blow arrives
/// inside a physics in/out callback, which is how every real hit lands.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DeathTriggeredRewindTests {

    [TestCase]
    public void ALethalHitTriggersTheRewindAndRevivesThePlayer() {
        RunDeathRewind(killInsidePhysicsCallback: false);
    }

    [TestCase]
    public void ALethalHitInsideAPhysicsCallbackStillRewinds() {
        // Real deaths arrive through Hitbox.OnAreaEntered (or a guarded hazard
        // lambda), so the whole chain runs under PhysicsCallbackGuard.
        RunDeathRewind(killInsidePhysicsCallback: true);
    }

    private static void RunDeathRewind(bool killInsidePhysicsCallback) {
        var tree = Engine.GetMainLoop() as SceneTree;
        AssertThat(tree).IsNotNull();
        var host = new Node2D { Name = "DeathRewindHost" };
        tree.Root.AddChild(host);
        try {
            StoryManager.Instance?.SetRewinds(3);
            PlayerController player = CharacterFactory.CreateCharacter("einstein", 0);
            host.AddChild(player);
            var manager = new ChronalRewindManager { Name = "RewindManager" };
            host.AddChild(manager);

            // Record movement history for the playback to scrub.
            for (int frame = 0; frame < 60; frame++) {
                player.GlobalPosition = new Vector2(400 + frame * 4, 850);
                manager._PhysicsProcess(1.0 / 60.0);
            }

            int poolBefore = manager.RemainingRewinds;
            AssertThat(poolBefore > 0).IsTrue();

            if (killInsidePhysicsCallback) {
                using var scope = PhysicsCallbackGuard.Enter();
                player.ApplyDamage(player.CurrentHP);
            } else {
                player.ApplyDamage(player.CurrentHP);
            }

            AssertThat(player.CurrentState).IsEqual(CharacterState.Dead);
            AssertThat(manager.IsRewinding)
                .OverrideFailureMessage("The death did not start a rewind — the player is stranded dead.")
                .IsTrue();

            // 2026-08-15 pacing: the mechanic opens with a hold in which nothing
            // plays back — the player stays where they died — and the whole
            // thing then takes exactly hold + playback ticks for this history.
            Vector2 deathPosition = player.GlobalPosition;
            AssertThat(manager.IsInPreRewindHold).IsTrue();
            for (int i = 0; i < ChronalRewindManager.PreRewindHoldFrames - 1; i++) {
                manager._PhysicsProcess(1.0 / 60.0);
                AssertThat(manager.IsInPreRewindHold).IsTrue();
                AssertThat(player.GlobalPosition).IsEqual(deathPosition);
            }
            manager._PhysicsProcess(1.0 / 60.0);
            AssertThat(manager.IsInPreRewindHold).IsFalse();
            AssertThat(manager.IsRewinding).IsTrue();

            int playbackTicks = 0;
            for (int i = 0; i < 900 && manager.IsRewinding; i++) {
                manager._PhysicsProcess(1.0 / 60.0);
                playbackTicks++;
            }
            // 60 recorded frames → 30-tick playback floor.
            AssertThat(playbackTicks).IsEqual(ChronalRewindManager.ComputePlaybackTicks(60));

            AssertThat(manager.IsRewinding)
                .OverrideFailureMessage("The rewind never completed — playback stalled.")
                .IsFalse();
            AssertThat(player.CurrentState == CharacterState.Dead)
                .OverrideFailureMessage("The rewind completed but the player is still dead.")
                .IsFalse();
            AssertThat(player.CurrentHP > 0).IsTrue();
            AssertThat(manager.RemainingRewinds).IsEqual(poolBefore - 1);
        } finally {
            host.Free();
            StoryManager.Instance?.SetRewinds(3);
        }
    }
}
