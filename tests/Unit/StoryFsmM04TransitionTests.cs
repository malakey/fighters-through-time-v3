using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W3 — M04 (D2(a) adopted). Pins the GDD's new FSM rows on the
/// Story <see cref="PlayerController"/> where they are expressible: Running →
/// Special/Ultimate; Crouching → Blocking/Rolling/Airborne/Special;
/// Attacking → Airborne/Rolling (recovery only) and → Ultimate (any point);
/// aerial Attacking → Idle on landing; Blocking → Rolling/Airborne. Under
/// D2(a) the landing tech and the Echo Step wind-up stay FLAGS, not state
/// moves — the tech pin below records that mapping (see the ledger entry
/// <c>DEFER-M04-SUBPHASE-FLAGS</c>). The M05 knockdown row belongs to W3b.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryFsmM04TransitionTests {

    [TestCase]
    public void RunningCastsSpecialsAndTheUltimate() {
        Run(player => {
            HoldUntil(player, GameplayButtons.None, 1f, CharacterState.Running);
            Step(player, GameplayButtons.Special1, 1f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
        });
        Run(player => {
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            HoldUntil(player, GameplayButtons.None, 1f, CharacterState.Running);
            Step(player, GameplayButtons.Ultimate, 1f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingUltimate);
        });
    }

    [TestCase]
    public void CrouchingLeavesForTheStanceTheRollTheJumpAndASpecial() {
        (GameplayButtons press, CharacterState expected)[] rows = {
            (GameplayButtons.Down | GameplayButtons.Block, CharacterState.Blocking),
            (GameplayButtons.Down | GameplayButtons.Roll, CharacterState.Rolling),
            (GameplayButtons.Down | GameplayButtons.Jump, CharacterState.Airborne),
            (GameplayButtons.Down | GameplayButtons.Special1, CharacterState.UsingSpecial)
        };
        foreach ((GameplayButtons press, CharacterState expected) in rows) {
            Run(player => {
                HoldUntil(player, GameplayButtons.Down, 0f, CharacterState.Crouching);
                Step(player, press, 0f);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"Crouching + {press} must reach {expected}.")
                    .IsEqual(expected);
            });
        }
    }

    [TestCase]
    public void AttackingCancelsIntoJumpAndRollOnlyInRecovery() {
        foreach (GameplayButtons cancel in new[] { GameplayButtons.Jump, GameplayButtons.Roll }) {
            Run(player => {
                Step(player, GameplayButtons.BasicAttack, 0f);
                AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
                // Startup + active: the cancel input is ignored.
                int guarded = BasicComboRules.GroundStartupFrames[0] - 1;
                for (int frame = 0; frame < guarded; frame++) {
                    Step(player, cancel, 0f);
                    AssertThat(player.CurrentState)
                        .OverrideFailureMessage($"{cancel} must not cancel a swing's startup (frame {frame}).")
                        .IsEqual(CharacterState.Attacking);
                }
                CharacterState expected = cancel == GameplayButtons.Jump
                    ? CharacterState.Airborne : CharacterState.Rolling;
                bool reached = false;
                for (int frame = 0; frame < 60 && !reached; frame++) {
                    Step(player, cancel, 0f);
                    reached = player.CurrentState == expected;
                }
                AssertThat(reached)
                    .OverrideFailureMessage($"{cancel} in the swing's recovery must reach {expected}.")
                    .IsTrue();
            });
        }
    }

    [TestCase]
    public void TheUltimateCancelsASwingAtAnyPoint() {
        Run(player => {
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            Step(player, GameplayButtons.BasicAttack, 0f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            Step(player, GameplayButtons.Ultimate, 0f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingUltimate);
        });
    }

    [TestCase]
    public void BlockingLeavesForTheRollAndTheJump() {
        foreach (GameplayButtons exit in new[] { GameplayButtons.Roll, GameplayButtons.Jump }) {
            Run(player => {
                HoldUntil(player, GameplayButtons.Block, 0f, CharacterState.Blocking);
                Step(player, GameplayButtons.Block | exit, 0f);
                CharacterState expected = exit == GameplayButtons.Roll
                    ? CharacterState.Rolling : CharacterState.Airborne;
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"Blocking + {exit} must reach {expected}.")
                    .IsEqual(expected);
            });
        }
    }

    [TestCase]
    public void AnAerialSwingEndsOnLandingAndTheTechIsAFlagNotARoll() {
        Run(player => {
            Step(player, GameplayButtons.Jump, 0f);
            for (int frame = 0; frame < 4; frame++) Step(player, GameplayButtons.None, 0f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
            Step(player, GameplayButtons.BasicAttack, 0f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            bool landedIdle = false;
            for (int frame = 0; frame < 120 && !landedIdle; frame++) {
                Step(player, GameplayButtons.None, 0f);
                if (player.IsOnFloor() && player.CurrentState == CharacterState.Idle) landedIdle = true;
                if (player.CurrentState == CharacterState.Idle && !player.IsOnFloor()) break;
            }
            AssertThat(landedIdle)
                .OverrideFailureMessage("An aerial swing must end in Idle on ground contact.")
                .IsTrue();
        });
        Run(player => {
            // D2(a): the landing tech is the IsInTechLockout flag over the
            // existing states, never a move into Rolling.
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = 1, AttackID = "test.hit", HitboxID = "combo_3",
                AttackClass = AttackClass.Basic, Damage = 5f, Knockback = new Vector2(3f, -5f),
                HitstunDuration = 0.8f, HitOrigin = player.GlobalPosition + new Vector2(-20f, 0f),
                AttackerFacingRight = true
            });
            bool teched = false;
            for (int frame = 0; frame < 120 && !teched; frame++) {
                Step(player, GameplayButtons.Block, 0f);
                teched = player.IsInTechLockout;
            }
            AssertThat(teched).IsTrue();
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.Rolling);
        });
    }

    private static void Run(Action<PlayerController> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var floor = new StaticBody2D {
            Name = "M04Floor", CollisionLayer = CollisionLayers.Environment, CollisionMask = 0
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
                Step(player, GameplayButtons.None, 0f);
            }
            for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) {
                Step(player, GameplayButtons.None, 0f);
            }
            AssertThat(player.CurrentState).OverrideFailureMessage($"setup: {player.CurrentState} onFloor={player.IsOnFloor()} pos={player.GlobalPosition} vel={player.Velocity}").IsEqual(CharacterState.Idle);
            body(player);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    private static void HoldUntil(PlayerController player, GameplayButtons buttons, float horizontal, CharacterState state) {
        for (int frame = 0; frame < 30 && player.CurrentState != state; frame++) Step(player, buttons, horizontal);
        AssertThat(player.CurrentState).IsEqual(state);
    }

    private static void Step(PlayerController player, GameplayButtons buttons, float horizontal) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, horizontal, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }
}
