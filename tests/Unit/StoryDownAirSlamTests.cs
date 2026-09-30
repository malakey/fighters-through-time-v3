using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W1 — A12 (Story half). A Down-Air slam spikes its victim straight
/// down; a slammed <see cref="PlayerController"/> on the ground bounces ONCE,
/// forced (a held Block cannot tech it), back into a tumble, and its next
/// landing techs normally. The player's own Down-Air hitbox authors the
/// downward vector. The Fighter half is <c>tests/Determinism/DownAirSlamTests.cs</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryDownAirSlamTests {

    [TestCase]
    public void ASlammedGroundedVictimBouncesOnceUntechablyThenTechsTheNextLanding() {
        Run(player => {
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = 1,
                TargetIndex = 0,
                AttackID = "einstein.basic",
                HitboxID = BasicComboRules.DownAirHitboxID,
                AttackClass = AttackClass.Basic,
                Damage = 5f,
                Knockback = new Vector2(0f, 7.5f),
                HitstunDuration = BasicComboRules.DirectionalAttackHitstunFrames / 60f,
                HitOrigin = player.GlobalPosition + new Vector2(0f, -60f),
                AttackerFacingRight = true,
                Launches = true
            });
            AssertThat(player.SlamBouncePending).IsTrue();
            AssertThat(player.Velocity.Y > 0f)
                .OverrideFailureMessage("The slam spikes the victim downward (Godot Y-down).")
                .IsTrue();

            bool bounced = false;
            for (int frame = 0; frame < 60 && !bounced; frame++) {
                Step(player, GameplayButtons.Block);
                if (!player.SlamBouncePending) {
                    bounced = true;
                    AssertThat(player.IsInTechLockout)
                        .OverrideFailureMessage("The bounce is forced: a held Block cannot tech it.")
                        .IsFalse();
                    AssertThat(player.Velocity.Y < 0f)
                        .OverrideFailureMessage("The bounce pops the victim back up.")
                        .IsTrue();
                    AssertThat(player.IsInTumble).IsTrue();
                }
            }
            AssertThat(bounced).IsTrue();

            bool teched = false;
            for (int frame = 0; frame < 120 && !teched; frame++) {
                Step(player, GameplayButtons.Block);
                teched = player.IsInTechLockout;
            }
            AssertThat(teched)
                .OverrideFailureMessage("After the one bounce, the next landing techs normally.")
                .IsTrue();
        });
    }

    private static void Run(Action<PlayerController> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var floor = new StaticBody2D {
            Name = "SlamFloor", CollisionLayer = CollisionLayers.Environment, CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(8000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        tree.Root.AddChild(floor);
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.GlobalPosition = new Vector2(0f, -8f);
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                Step(player, GameplayButtons.None);
            }
            for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) {
                Step(player, GameplayButtons.None);
            }
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            body(player);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    private static void Step(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }
}
