using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W4 — Pocahontas's Breeze Glide kit rules in Story:
/// <list type="bullet">
/// <item><b>Second Glide</b> (design §5, Low-item decision 2026-09-26): the
/// once-per-airtime re-entry "ignores and does not restart the movement
/// cooldown". Before W4 the re-entry went through <c>BaseSpecial.TryExecute</c>,
/// which re-armed the full five seconds.</item>
/// <item><b>The glide can attack</b> (V7 kit copy): the basic string is usable
/// mid-glide without ending the glide.</item>
/// </list>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PocahontasGlideTests {

    [TestCase]
    public void ASecondGlideReEntryNeitherChecksNorRestartsTheRunningCooldown() {
        PlayerController player = CharacterFactory.CreateCharacter("pocahontas");
        Node host = Attach(player);
        try {
            var glide = player.GetNode<PocahontasBreezeGlide>("MovementAbility");
            player.TransitionTo(CharacterState.Airborne);
            player.MovementAbilityCooldownTimer = 3f;

            // Without the node a running cooldown refuses the press outright.
            Press(player, GameplayButtons.MovementAbility);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
            AssertThat(glide.IsExecuting).IsFalse();

            player.StoryAbilityPerks.Add(PocahontasBreezeGlide.SecondGlidePerkKey);
            float before = player.MovementAbilityCooldownTimer;
            Press(player, GameplayButtons.MovementAbility);

            AssertThat(glide.IsExecuting)
                .OverrideFailureMessage("The Second Glide re-entry must start the glide.").IsTrue();
            AssertThat(glide.SecondGlideConsumed).IsTrue();
            // One frame of ordinary countdown, never a restart to the full 5 s.
            AssertFloat(player.MovementAbilityCooldownTimer)
                .OverrideFailureMessage("The re-entry must not restart the movement cooldown.")
                .IsLess(before + 0.001f);
            AssertFloat(player.MovementAbilityCooldownTimer).IsGreater(before - 0.05f);
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void TheBasicStringStartsMidGlideAndTheGlideResumesAfterIt() {
        PlayerController player = CharacterFactory.CreateCharacter("pocahontas");
        Node host = Attach(player);
        try {
            var glide = player.GetNode<PocahontasBreezeGlide>("MovementAbility");
            player.TransitionTo(CharacterState.Airborne);
            Press(player, GameplayButtons.MovementAbility);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingMovementAbility);
            // Dash startup + active, then the held glide begins at recovery.
            for (int frame = 0; frame < glide.Data.StartupFrames + glide.Data.ActiveFrames + 1; frame++) {
                glide._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(glide.IsGliding).IsTrue();

            Press(player, GameplayButtons.BasicAttack);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("A basic attack must start mid-glide.")
                .IsEqual(CharacterState.Attacking);
            glide._PhysicsProcess(1.0 / 60.0);
            AssertThat(glide.IsGliding)
                .OverrideFailureMessage("Swinging must not end the glide.").IsTrue();

            // Ride the swing out in the air; the glide takes the state back.
            var idle = new BufferedInputSource();
            idle.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
            InputManager.Instance.SetInputSource(player.PlayerIndex, idle);
            for (int frame = 0; frame < 90 && player.CurrentState == CharacterState.Attacking; frame++) {
                player._PhysicsProcess(1.0 / 60.0);
                glide._PhysicsProcess(1.0 / 60.0);
            }
            glide._PhysicsProcess(1.0 / 60.0);
            AssertThat(glide.IsGliding).IsTrue();
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingMovementAbility);
        } finally {
            Teardown(host, player);
        }
    }

    private static void Press(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }

    private static Node Attach(PlayerController player) {
        var host = new Node { Name = "PocahontasGlideHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        host.AddChild(player);
        return host;
    }

    private static void Teardown(Node host, PlayerController player) {
        InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
