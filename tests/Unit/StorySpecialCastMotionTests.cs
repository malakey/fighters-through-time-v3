using System;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Combat and movement update 2026-10-16, the Q1 ruling ("brake to a stop,
/// both modes"; plan §2.1.6), Story half. A grounded Special cast brakes its
/// horizontal velocity to zero over the run-stop ramp
/// (<c>UniversalMovementRules.RunDecelerationFrames</c>, the same per-tick step
/// as the run stop), so a running caster stops within four ticks instead of
/// sliding at its entry speed; an airborne cast keeps its drift, with the stick
/// ignored and no added friction. The Fighter sim's half is pinned in
/// <c>FighterSpecialTimingTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StorySpecialCastMotionTests {
    private const double Step = 1.0 / 60.0;
    // Away from every other suite's geometry.
    private static readonly Vector2 Origin = new(-64500f, 1500f);
    private static readonly string[] AbilityNodes = { "Special1", "Special2", "MovementAbility", "Ultimate" };

    [TestCase]
    public void ARunningGroundedCasterStopsWithinFourTicksAndStaysStopped() {
        Run("joan", player => {
            var smite = player.GetNode<JoanRighteousSmite>("Special1");
            for (int tick = 0; tick < 20; tick++) Tick(player, GameplayButtons.None, horizontal: 1f);
            float entry = player.Velocity.X;
            AssertThat(entry > 100f).OverrideFailureMessage($"Harness: the hero never ran ({entry} px/s).").IsTrue();
            Tick(player, GameplayButtons.Special1, horizontal: 1f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);

            float previous = player.Velocity.X;
            for (int tick = 1; tick <= FTT.Core.UniversalMovementRules.RunDecelerationFrames; tick++) {
                Tick(player, GameplayButtons.None, horizontal: 1f);
                AssertThat(player.Velocity.X < previous || player.Velocity.X == 0f)
                    .OverrideFailureMessage($"Cast tick {tick}: {player.Velocity.X} px/s did not brake from {previous}.")
                    .IsTrue();
                previous = player.Velocity.X;
            }
            AssertFloat(player.Velocity.X)
                .OverrideFailureMessage($"A grounded cast must stop within {FTT.Core.UniversalMovementRules.RunDecelerationFrames} ticks.")
                .IsEqual(0f);
            for (int tick = 0; tick < 30 && smite.IsExecuting; tick++) {
                Tick(player, GameplayButtons.None, horizontal: 1f);
                AssertFloat(player.Velocity.X)
                    .OverrideFailureMessage("The held stick must not move a casting hero.")
                    .IsEqual(0f);
            }
        });
    }

    [TestCase]
    public void AnAirborneCasterKeepsItsDriftWithTheStickIgnored() {
        Run("joan", player => {
            var smite = player.GetNode<JoanRighteousSmite>("Special1");
            for (int tick = 0; tick < 20; tick++) Tick(player, GameplayButtons.None, horizontal: 1f);
            Tick(player, GameplayButtons.Jump, horizontal: 1f);
            for (int tick = 0; tick < 3; tick++) {
                Tick(player, GameplayButtons.Jump, previous: GameplayButtons.Jump, horizontal: 1f);
            }
            AssertThat(player.IsOnFloor()).OverrideFailureMessage("Harness: the hero must be airborne.").IsFalse();
            Tick(player, GameplayButtons.Special1, horizontal: 1f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            float drift = player.Velocity.X;
            AssertThat(drift > 100f).OverrideFailureMessage($"Harness: no drift to keep ({drift} px/s).").IsTrue();
            int airborneTicks = 0;
            for (int tick = 0; tick < 20 && smite.IsExecuting && !player.IsOnFloor(); tick++) {
                Tick(player, GameplayButtons.None, horizontal: -1f);
                if (player.IsOnFloor()) break;
                airborneTicks++;
                AssertFloat(player.Velocity.X)
                    .OverrideFailureMessage($"The airborne cast's drift changed on tick {tick}.")
                    .IsEqualApprox(drift, 0.01f);
            }
            AssertThat(airborneTicks > 3).OverrideFailureMessage("Harness: the hero landed at once.").IsTrue();
        });
    }

    // === Harness ===============================================================

    private static void Run(string heroID, Action<PlayerController> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool paused = tree.Paused;
        var host = new Node2D { Name = "SpecialCastMotionHost" };
        tree.Root.AddChild(host);
        var pools = new PoolManager { Name = "SpecialCastMotionPools" };
        tree.Root.AddChild(pools);
        var floor = new StaticBody2D {
            Name = "SpecialCastMotionFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = Origin
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(6000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        host.AddChild(floor);
        PlayerController player = CharacterFactory.CreateCharacter(heroID, 0,
            applyStoryProgression: false, applyLegacyUnlockLocks: false);
        player.Position = Origin + new Vector2(0f, -8f);
        host.AddChild(player);
        try {
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                Tick(player, GameplayButtons.None);
            }
            for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) {
                Tick(player, GameplayButtons.None);
            }
            AssertThat(player.CurrentState)
                .OverrideFailureMessage($"setup: {player.CurrentState} onFloor={player.IsOnFloor()} pos={player.GlobalPosition}")
                .IsEqual(CharacterState.Idle);
            body(player);
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            pools.ClearAllPools();
            tree.Root.RemoveChild(pools);
            pools.Free();
            host.Free();
            tree.Paused = paused;
        }
    }

    /// <summary>One tick in the tree order: the controller, then its four ability nodes.</summary>
    private static void Tick(PlayerController player, GameplayButtons held,
        GameplayButtons previous = GameplayButtons.None, float horizontal = 0f) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, horizontal, 0f, held, previous));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Step);
        foreach (string name in AbilityNodes) {
            if (player.GetNodeOrNull<BaseSpecial>(name) is { } ability && GodotObject.IsInstanceValid(ability)) {
                ability._PhysicsProcess(Step);
            }
        }
    }
}
