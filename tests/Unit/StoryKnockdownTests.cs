using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W3b (M05/M07), the Story-side pins: only an authored launcher
/// tumbles the player (a non-launcher on a grounded player is grounded
/// knockback, which re-opens the hit-2 Block escape — the M06 follow-up); a
/// missed tech is a 30-frame invulnerable knockdown inside
/// <see cref="CharacterState.Stunned"/>, then a 10-frame neutral or 14-frame
/// roll get-up with no invulnerability, mirroring the sim's component 320; and
/// Lincoln's melee hitbox launches on hit 2.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryKnockdownTests {

    [TestCase]
    public void AMissedTechKnocksDownForThirtyInvulnerableFramesThenStandsUpInTen() {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer("einstein");
        try {
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Launcher());
            AssertThat(player.IsInTumble).IsTrue();

            // Ride the launch out without Block: the landing is a missed tech.
            bool down = false;
            for (int frame = 0; frame < 180 && !down; frame++) {
                Step(player, GameplayButtons.None);
                down = player.IsKnockedDown;
            }
            AssertThat(down).OverrideFailureMessage("A tumble landing without Block must knock down.").IsTrue();
            AssertThat(player.IsInTechLockout).IsFalse();
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);

            int downFrames = 0;
            while (player.IsKnockedDown && downFrames < 60) {
                downFrames++;
                int hp = player.CurrentHP;
                AssertThat(player.ApplyDamage(10))
                    .OverrideFailureMessage("A downed player is fully invulnerable.")
                    .IsEqual(0);
                AssertThat(player.CurrentHP).IsEqual(hp);
                Step(player, GameplayButtons.BasicAttack);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage("No action may start from the knockdown.")
                    .IsNotEqual(CharacterState.Attacking);
            }
            AssertThat(downFrames).IsEqual(BasicComboRules.KnockdownFrames);
            AssertThat(player.GetUpKind).IsEqual(BasicComboRules.GetUpNeutral);

            int getUpFrames = 0;
            float standX = player.GlobalPosition.X;
            bool vulnerable = false;
            while (player.IsInKnockdownOrGetUp && getUpFrames < 60) {
                getUpFrames++;
                if (getUpFrames == 1) vulnerable = player.ApplyDamage(1) > 0;
                Step(player, GameplayButtons.Jump);
                AssertThat(Mathf.Abs(player.GlobalPosition.X - standX) < 0.5f)
                    .OverrideFailureMessage("The neutral get-up stands in place.")
                    .IsTrue();
            }
            AssertThat(vulnerable).OverrideFailureMessage("The get-up has no invulnerability.").IsTrue();
            AssertThat(getUpFrames).IsEqual(BasicComboRules.NeutralGetUpFrames);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void HoldingRightAsTheKnockdownEndsRollsRightForFourteenFrames() {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer("einstein");
        try {
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Launcher());
            for (int frame = 0; frame < 180 && !player.IsKnockedDown; frame++) {
                Step(player, GameplayButtons.None);
            }
            AssertThat(player.IsKnockedDown).IsTrue();
            // Only the knockdown's final frame reads the stick.
            while (player.KnockdownFramesRemaining > 1) Step(player, GameplayButtons.None);
            Step(player, GameplayButtons.None, horizontal: 1f);
            AssertThat(player.GetUpKind).IsEqual(BasicComboRules.GetUpRoll);
            AssertThat(player.GetUpFramesRemaining).IsEqual(BasicComboRules.RollGetUpFrames);

            float startX = player.GlobalPosition.X;
            int getUpFrames = 0;
            while (player.IsInKnockdownOrGetUp && getUpFrames < 60) {
                getUpFrames++;
                Step(player, GameplayButtons.None);
            }
            AssertThat(getUpFrames).IsEqual(BasicComboRules.RollGetUpFrames);
            AssertThat(player.GlobalPosition.X > startX + 20f)
                .OverrideFailureMessage("The roll get-up travels in the held direction.")
                .IsTrue();
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void ANonLaunchingHitIsGroundedKnockbackAndHitTwoReopensTheBlockEscape() {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer("einstein");
        try {
            // Hit 1: knockback but not a launcher — a grounded slide, no tumble.
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(StringHit("combo_1"));
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(player.IsInTumble).IsFalse();
            AssertThat(player.HasPendingLaunch).IsFalse();
            AssertThat(player.Velocity.Y).IsEqual(0f);
            AssertThat(player.Velocity.X > 0f).IsTrue();
            // Hit 1's gate holds the Block escape shut.
            for (int frame = 0; frame < 10; frame++) Step(player, GameplayButtons.Block);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(player.IsOnFloor()).OverrideFailureMessage("A non-launcher never lifts a grounded player.").IsTrue();

            // Hit 2 (non-launching for everyone but Lincoln): the grounded escape
            // is open again — the M06 follow-up.
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(StringHit("combo_2"));
            AssertThat(player.IsInTumble).IsFalse();
            bool escaped = false;
            for (int frame = 0; frame < 20 && !escaped; frame++) {
                Step(player, GameplayButtons.Block);
                escaped = player.CurrentState == CharacterState.Blocking;
            }
            AssertThat(escaped)
                .OverrideFailureMessage("M06: grounded non-launch hitstun after hit 2 must Block-escape.")
                .IsTrue();

            // A non-player source (enemies author no launch flag yet) keeps the
            // legacy "knockback launches" rule; a player-sourced payload obeys
            // its flag.
            AssertThat(PlayerController.ResolveHitLaunches(new HitPayload {
                AttackerIndex = -1, Knockback = new Vector2(3f, -2f) })).IsTrue();
            AssertThat(PlayerController.ResolveHitLaunches(new HitPayload {
                AttackerIndex = 1, Knockback = new Vector2(3f, -2f), Launches = false })).IsFalse();
            AssertThat(PlayerController.ResolveHitLaunches(new HitPayload {
                AttackerIndex = 1, Knockback = new Vector2(3f, -2f), Launches = true })).IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void LincolnsStoryHitTwoIsAuthoredAsALauncher() {
        // M07: the Story melee hitbox reads the string profile, so Lincoln's
        // hit 2 carries Launches = true and a template character's does not.
        AssertThat(HitTwoLaunches("lincoln")).IsTrue();
        AssertThat(HitTwoLaunches("joan")).IsFalse();
    }

    // === Harness ===

    private static bool HitTwoLaunches(string characterID) {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer(characterID);
        try {
            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            for (int frame = 0; frame < 240 && hitbox.HitboxID != "combo_2"; frame++) {
                Step(player, frame % 2 == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None);
            }
            AssertThat(hitbox.HitboxID)
                .OverrideFailureMessage($"{characterID}: the string never reached hit 2.")
                .IsEqual("combo_2");
            return hitbox.Launches;
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    private static HitPayload Launcher() => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.launch",
        HitboxID = "combo_3",
        AttackClass = AttackClass.Basic,
        Damage = 5f,
        Knockback = new Vector2(3f, -4f),
        Launches = true,
        HitstunDuration = 1.5f,
        HitOrigin = new Vector2(-20f, 0f),
        AttackerFacingRight = true
    };

    private static HitPayload StringHit(string hitboxID) => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.string",
        HitboxID = hitboxID,
        AttackClass = AttackClass.Basic,
        Damage = 5f,
        Knockback = new Vector2(3f, -2f),
        Launches = false,
        HitstunDuration = 0.6f,
        HitOrigin = new Vector2(-20f, 0f),
        AttackerFacingRight = true
    };

    private static void Step(PlayerController player, GameplayButtons buttons, float horizontal = 0f) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, horizontal, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }

    private static StaticBody2D CreateFlatFloor() {
        var floor = new StaticBody2D {
            Name = "StoryW3bFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    private static PlayerController CreateGroundedPlayer(string characterID) {
        PlayerController player = CharacterFactory.CreateCharacter(characterID);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        player.GlobalPosition = new Vector2(0f, -8f);
        for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
            player.Velocity = new Vector2(player.Velocity.X, 400f);
            Step(player, GameplayButtons.None);
        }
        for (int frame = 0; frame < 10 && player.CurrentState != CharacterState.Idle; frame++) {
            Step(player, GameplayButtons.None);
        }
        AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
        return player;
    }
}
