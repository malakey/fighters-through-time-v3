using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-HIT (combat and movement update 2026-10-16, decision 3.2, finding
/// PFC-04; ledger <c>USER-TUMBLE-AFTER-HITSTUN</c>), the Story half for the
/// hero: a launch whose hitstun ends in the air leaves the hero in
/// <see cref="CharacterState.Airborne"/> with control back but still tumbling
/// (<see cref="PlayerController.IsTumblingAfterHitstun"/>). Doing nothing lands
/// in the tumble — held Block techs it, otherwise it is the M05 knockdown; an
/// air jump, the air dodge, the air strike or a Special leaves it; a Defy press
/// clears it. The Fighter half is <c>tests/Determinism/FighterTumbleTests.cs</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryTumbleTests {

    [TestCase]
    public void APassiveHeroWhoseHitstunEndsInTheAirLandsInAKnockdown() {
        Run(player => {
            RideToTumbleAfterHitstun(player);
            bool down = false;
            for (int frame = 0; frame < 180 && !down; frame++) {
                Step(player, GameplayButtons.None);
                down = player.IsKnockedDown;
                if (!down) {
                    AssertThat(player.IsOnFloor() && player.CurrentState == CharacterState.Idle)
                        .OverrideFailureMessage("The tumble landing must not be a plain landing.")
                        .IsFalse();
                }
            }
            AssertThat(down)
                .OverrideFailureMessage("A passive tumble past its hitstun lands in the knockdown.")
                .IsTrue();
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(player.IsTumblingAfterHitstun).IsFalse();
            AssertThat(player.IsInTechLockout).IsFalse();
        });
    }

    [TestCase]
    public void HoldingBlockTechsTheTumbleLanding() {
        Run(player => {
            RideToTumbleAfterHitstun(player);
            bool teched = false;
            for (int frame = 0; frame < 180 && !teched; frame++) {
                Step(player, GameplayButtons.Block);
                teched = player.IsInTechLockout;
            }
            AssertThat(teched)
                .OverrideFailureMessage("Held Block techs a post-hitstun tumble landing.")
                .IsTrue();
            AssertThat(player.IsKnockedDown).IsFalse();
            AssertThat(player.IsTumblingAfterHitstun).IsFalse();
            // The tech is the ordinary 12-frame invulnerable lock, then Idle.
            AssertThat(player.ApplyDamage(5)).OverrideFailureMessage("The tech is invulnerable.").IsEqual(0);
            for (int frame = 0; frame < 30 && player.IsInTechLockout; frame++) Step(player, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
        });
    }

    [TestCase]
    public void TheAirJumpTheAirDodgeTheAirStrikeAndASpecialEachLeaveTheTumble() {
        foreach ((string name, GameplayButtons press) in new[] {
                     ("the air jump", GameplayButtons.Jump),
                     ("the air dodge", GameplayButtons.Roll),
                     ("the air strike", GameplayButtons.BasicAttack),
                     ("Special 1", GameplayButtons.Special1) }) {
            Run(player => {
                RideToTumbleAfterHitstun(player);
                CharacterState before = player.CurrentState;
                Step(player, press);
                AssertThat(player.CurrentState != before || press == GameplayButtons.Jump)
                    .OverrideFailureMessage($"{name} must be accepted out of the tumble.")
                    .IsTrue();
                AssertThat(player.IsTumblingAfterHitstun)
                    .OverrideFailureMessage($"{name} leaves the tumble.")
                    .IsFalse();
                bool knockedDown = false;
                bool teched = false;
                for (int frame = 0; frame < 240 && !player.IsOnFloor(); frame++) {
                    Step(player, GameplayButtons.None);
                    knockedDown |= player.IsKnockedDown;
                    teched |= player.IsInTechLockout;
                }
                AssertThat(player.IsOnFloor()).OverrideFailureMessage($"After {name} the hero must land.").IsTrue();
                // Two more frames: the landing itself resolves on the grounded frame.
                for (int frame = 0; frame < 2; frame++) {
                    Step(player, GameplayButtons.None);
                    knockedDown |= player.IsKnockedDown;
                    teched |= player.IsInTechLockout;
                }
                AssertThat(knockedDown).OverrideFailureMessage($"After {name} the landing is plain.").IsFalse();
                AssertThat(teched).IsFalse();
                // (A Special's own cast clock runs on its ability node, which this
                // harness's direct _PhysicsProcess calls never tick, so it may still
                // read UsingSpecial here; it is never the knockdown's Stunned.)
                AssertThat(player.CurrentState).IsNotEqual(CharacterState.Stunned);
            });
        }
    }

    [TestCase]
    public void ADefyMidTumbleClearsIt() {
        Run(player => {
            RideToTumbleAfterHitstun(player);
            player.SetUltimateMeter(BasicComboRules.DefyInfluenceCost);
            AssertThat(player.IsTumblingAfterHitstun).IsTrue();
            Step(player, GameplayButtons.Defy);
            AssertThat(player.IsDefying)
                .OverrideFailureMessage("setup: the Defy press must begin a Defy mid-tumble.")
                .IsTrue();
            AssertThat(player.IsTumblingAfterHitstun)
                .OverrideFailureMessage("A Defy releases the hero from the tumble.")
                .IsFalse();
            bool knockedDown = false;
            for (int frame = 0; frame < 300 && !(player.IsOnFloor() && !player.IsDefying); frame++) {
                Step(player, GameplayButtons.None);
                knockedDown |= player.IsKnockedDown;
            }
            Step(player, GameplayButtons.None);
            knockedDown |= player.IsKnockedDown;
            AssertThat(knockedDown).OverrideFailureMessage("The Defy survivor lands plainly.").IsFalse();
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
        });
    }

    [TestCase]
    public void AnOwedSlamBounceSurvivesTheHitstunEndAndTheNextPassiveLandingKnocksDown() {
        // A12 meets 3.2 (the sim twin is FighterTumbleTests'
        // AnOwedSlamBounceSurvivesTheHitstunEndAndIsStillForced): a Down-Air
        // slam whose short hitstun ends high in the air still owes its forced
        // bounce, and the bounce is a fresh launched stun whose next untouched
        // landing is the knockdown.
        Run(player => {
            player.GlobalPosition = new Vector2(0f, -640f);
            Step(player, GameplayButtons.None);
            AssertThat(player.IsOnFloor()).OverrideFailureMessage("setup: the hero must be airborne.").IsFalse();
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = 1,
                TargetIndex = 0,
                AttackID = "test.slam",
                HitboxID = BasicComboRules.DownAirHitboxID,
                AttackClass = AttackClass.Basic,
                Damage = 5f,
                Knockback = new Vector2(0f, 7.5f),
                HitstunDuration = 0.1f,
                HitOrigin = player.GlobalPosition + new Vector2(0f, -60f),
                AttackerFacingRight = true,
                Launches = true
            });
            AssertThat(player.SlamBouncePending).OverrideFailureMessage("setup: the slam owes a bounce.").IsTrue();
            for (int frame = 0; frame < 90 && !player.IsTumblingAfterHitstun; frame++) {
                Step(player, GameplayButtons.None);
            }
            AssertThat(player.IsTumblingAfterHitstun)
                .OverrideFailureMessage("setup: the slam's hitstun must end in the air.")
                .IsTrue();
            AssertThat(player.SlamBouncePending)
                .OverrideFailureMessage("The bounce is still owed after the slam's hitstun ends.")
                .IsTrue();

            bool bounced = false;
            for (int frame = 0; frame < 180 && !bounced; frame++) {
                Step(player, GameplayButtons.None);
                if (!player.SlamBouncePending) {
                    bounced = true;
                    AssertThat(player.IsKnockedDown)
                        .OverrideFailureMessage("The owed bounce is forced: neither knockdown nor tech.")
                        .IsFalse();
                    AssertThat(player.IsInTechLockout).IsFalse();
                    AssertThat(player.Velocity.Y < 0f)
                        .OverrideFailureMessage("The bounce pops the hero back up.")
                        .IsTrue();
                    AssertThat(player.IsInTumble).IsTrue();
                }
            }
            AssertThat(bounced).OverrideFailureMessage("The tumble landing must take the owed bounce.").IsTrue();

            bool down = false;
            for (int frame = 0; frame < 180 && !down; frame++) {
                Step(player, GameplayButtons.None);
                down = player.IsKnockedDown;
            }
            AssertThat(down)
                .OverrideFailureMessage("After the one bounce, the next passive landing is the knockdown.")
                .IsTrue();
        });
    }

    /// <summary>
    /// CM-FIXB-SIM (B1), the Story pin of the shared rule: a non-launching
    /// airborne hit ends the hero's tumble whether it lands inside the launch's
    /// hitstun or after it, and the landing is plain — no tech, no knockdown.
    /// The sim twin is FighterTumbleTests'
    /// <c>ANonLaunchingHitEndsTheTumbleInsideOrAfterTheLaunchsHitstunAndTheLandingIsPlain</c>.
    /// </summary>
    [TestCase]
    public void ANonLaunchingHitEndsTheTumbleInsideOrAfterTheLaunchsHitstun() {
        foreach (bool afterHitstun in new[] { false, true }) {
            string label = afterHitstun ? "after the launch's hitstun" : "inside the launch's hitstun";
            Run(player => {
                if (afterHitstun) {
                    RideToTumbleAfterHitstun(player);
                } else {
                    Launch(player, hitstunSeconds: 1.0f);
                    // Past the launch's freeze and off the floor, well inside its 60-frame hitstun.
                    for (int frame = 0; frame < 30 && player.IsOnFloor(); frame++) Step(player, GameplayButtons.None);
                    for (int frame = 0; frame < 3; frame++) Step(player, GameplayButtons.None);
                    AssertThat(player.IsInTumble && !player.IsTumblingAfterHitstun && player.CurrentState == CharacterState.Stunned)
                        .OverrideFailureMessage($"setup ({label}): the hero must still be in the launch's hitstun.")
                        .IsTrue();
                }
                AssertThat(player.IsOnFloor()).OverrideFailureMessage($"setup ({label}): airborne.").IsFalse();
                player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                    AttackerIndex = 1,
                    TargetIndex = 0,
                    AttackID = "test.air_strike",
                    HitboxID = "air_attack",
                    AttackClass = AttackClass.Basic,
                    Damage = 5f,
                    Knockback = new Vector2(2f, 0f),
                    Launches = false,
                    HitstunDuration = 0.5f,
                    HitOrigin = player.GlobalPosition + new Vector2(-30f, 0f),
                    AttackerFacingRight = true
                });
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"setup ({label}): the hit must stun.")
                    .IsEqual(CharacterState.Stunned);
                AssertThat(player.IsInTumble || player.IsTumblingAfterHitstun)
                    .OverrideFailureMessage($"A non-launching hit {label} ends the tumble.")
                    .IsFalse();
                bool knockedDown = false;
                bool teched = false;
                for (int frame = 0; frame < 240 && !player.IsOnFloor(); frame++) {
                    Step(player, GameplayButtons.None);
                    knockedDown |= player.IsKnockedDown;
                    teched |= player.IsInTechLockout;
                }
                AssertThat(player.IsOnFloor()).OverrideFailureMessage($"{label}: the hero must land.").IsTrue();
                for (int frame = 0; frame < 2; frame++) {
                    Step(player, GameplayButtons.None);
                    knockedDown |= player.IsKnockedDown;
                    teched |= player.IsInTechLockout;
                }
                AssertThat(knockedDown).OverrideFailureMessage($"{label}: the landing is plain, not a knockdown.").IsFalse();
                AssertThat(teched).OverrideFailureMessage($"{label}: the landing is plain, not a tech.").IsFalse();
            });
        }
    }

    // === Harness ===

    /// <summary>The strong vertical launcher <see cref="RideToTumbleAfterHitstun"/> uses, with its hitstun.</summary>
    private static void Launch(PlayerController player, float hitstunSeconds) {
        player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
            AttackerIndex = 1,
            TargetIndex = 0,
            AttackID = "test.tumble_launch",
            HitboxID = "combo_3",
            AttackClass = AttackClass.Basic,
            Damage = 5f,
            Knockback = new Vector2(1f, -22f),
            Launches = true,
            HitstunDuration = hitstunSeconds,
            HitOrigin = new Vector2(-20f, 0f),
            AttackerFacingRight = true
        });
        AssertThat(player.IsInTumble).OverrideFailureMessage("setup: the launch must tumble.").IsTrue();
    }

    /// <summary>
    /// Launches the grounded hero with a strong vertical blow and a short
    /// hitstun, then steps until the hitstun has ended in the air and the hero
    /// is back in Airborne, still tumbling.
    /// </summary>
    private static void RideToTumbleAfterHitstun(PlayerController player) {
        player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
            AttackerIndex = 1,
            TargetIndex = 0,
            AttackID = "test.tumble_launch",
            HitboxID = "combo_3",
            AttackClass = AttackClass.Basic,
            Damage = 5f,
            Knockback = new Vector2(1f, -22f),
            Launches = true,
            HitstunDuration = 0.15f,
            HitOrigin = new Vector2(-20f, 0f),
            AttackerFacingRight = true
        });
        AssertThat(player.IsInTumble).OverrideFailureMessage("setup: the launch must tumble.").IsTrue();
        for (int frame = 0; frame < 90 && !player.IsTumblingAfterHitstun; frame++) {
            Step(player, GameplayButtons.None);
            AssertThat(player.IsKnockedDown || player.IsInTechLockout)
                .OverrideFailureMessage("setup: the launch must outlast its hitstun.")
                .IsFalse();
        }
        AssertThat(player.IsTumblingAfterHitstun)
            .OverrideFailureMessage("setup: the hitstun must end in the air.")
            .IsTrue();
        AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
        AssertThat(player.IsOnFloor()).IsFalse();
    }

    private static void Run(Action<PlayerController> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var floor = new StaticBody2D {
            Name = "StoryTumbleFloor", CollisionLayer = CollisionLayers.Environment, CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(8000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        tree.Root.AddChild(floor);
        // Story progression off: no save-slot Legacy gate can leave Special 1 dormant.
        PlayerController player = CharacterFactory.CreateCharacter("einstein", applyStoryProgression: false);
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
