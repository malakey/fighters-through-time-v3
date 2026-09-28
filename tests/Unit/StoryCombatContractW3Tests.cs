using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W3, the Story-side pins: M06 (a tumbling victim never escapes
/// into the stance — it techs on ground contact instead), the guard-break push
/// (assigned, unscaled, away from the attacker), Block cancelling a Special's
/// RECOVERY frames (never its startup), and M01 (fast-fall snaps to 1200 px/s).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryCombatContractW3Tests {

    [TestCase]
    public void ATumblingVictimHoldingBlockNeverEntersTheStanceAndTechsTheLanding() {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer();
        try {
            // Hit 2 (the V7.3 gate is open) WITH a launch: a tumble. On the hit
            // frame the cached floor flag still reads grounded — the exact frame
            // the old rule escaped on.
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit("combo_2", new Vector2(4f, -4f)));
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(player.IsInTumble).IsTrue();

            bool enteredStanceWhileTumbling = false;
            bool teched = false;
            for (int frame = 0; frame < 90 && !teched; frame++) {
                bool tumbling = player.IsInTumble;
                Step(player, GameplayButtons.Block);
                if (tumbling && player.CurrentState == CharacterState.Blocking) enteredStanceWhileTumbling = true;
                if (player.IsInTechLockout) teched = true;
            }
            AssertThat(enteredStanceWhileTumbling)
                .OverrideFailureMessage("M06: a tumbling victim holding Block must not escape into the stance.")
                .IsFalse();
            AssertThat(teched)
                .OverrideFailureMessage("M06: the same held Block must tech the tumble's landing.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void TheGuardBreakPushIsAssignedAwayFromTheAttackerAndUnscaled() {
        // The pure vector: (±120, -60) px/s — (2.0, -1.0) units, Y-down.
        AssertThat(BlockSystem.GuardBreakPushVelocity(0f, 50f, true)).IsEqual(new Vector2(-120f, -60f));
        AssertThat(BlockSystem.GuardBreakPushVelocity(0f, -50f, false)).IsEqual(new Vector2(120f, -60f));

        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("lincoln");
        tree.Root.AddChild(player);
        try {
            BlockSystem block = player.GetNode<BlockSystem>("BlockSystem");
            player.TransitionTo(CharacterState.Blocking);
            block.StartBlock();
            AssertThat(block.IsBlocking).IsTrue();
            // Prior motion an ADDED push would keep, and a heavy victim.
            player.Velocity = new Vector2(500f, 300f);
            BlockResult result = block.ResolveHit(new HitPayload {
                AttackerIndex = 1,
                AttackID = "test.special",
                HitboxID = "primary",
                AttackClass = AttackClass.Special,
                Damage = 20f,
                Knockback = new Vector2(6f, -2f),
                HitstunDuration = 0.3f,
                // The player faces right: the attacker is in front, on the right.
                HitOrigin = player.GlobalPosition + new Vector2(40f, 0f),
                AttackerFacingRight = false
            });
            AssertThat(result).IsEqual(BlockResult.GuardBroken);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Dazed);
            AssertThat(player.Velocity)
                .OverrideFailureMessage("The push is assigned, unscaled, and away from the attacker.")
                .IsEqual(new Vector2(-120f, -60f));
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void BlockCancelsASpecialsRecoveryButNotItsStartup() {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer();
        try {
            Step(player, GameplayButtons.Special1);
            var ability = player.GetNode<BaseSpecial>("Special1");
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            AssertThat((int)ability.CurrentPhase).IsEqual((int)AbilityPhase.Startup);

            // Startup: Block does nothing.
            Step(player, GameplayButtons.Block);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Block must not cancel a Special's startup.")
                .IsEqual(CharacterState.UsingSpecial);

            // Recovery: Block cancels into the stance, the cast ends, the
            // cooldown it armed stands.
            ability.AdvanceToActive();
            ability.AdvanceToRecovery();
            AssertThat((int)ability.CurrentPhase).IsEqual((int)AbilityPhase.Recovery);
            Step(player, GameplayButtons.Block);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Block must cancel a Special's recovery frames.")
                .IsEqual(CharacterState.Blocking);
            AssertThat(ability.IsExecuting).IsFalse();
            AssertThat(player.SpecialOneCooldownTimer > 0f).IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void FastFallSnapsToTwelveHundredPixelsPerSecond() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.GlobalPosition = new Vector2(0f, -4000f);
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = new Vector2(0f, -300f);
            Step(player, GameplayButtons.Down);
            AssertThat(player.Velocity.Y)
                .OverrideFailureMessage("M01: fast-fall snaps to 20 u/s = 1200 px/s.")
                .IsEqualApprox(UniversalMovementRules.FastFallSpeed * 60f, 0.01f);
            AssertThat(UniversalMovementRules.FastFallSpeed * 60f).IsEqual(1200f);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    private static HitPayload Hit(string hitboxID, Vector2 knockback) => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.hit",
        HitboxID = hitboxID,
        AttackClass = AttackClass.Basic,
        Damage = 5f,
        Knockback = knockback,
        // M05 (Package 12 W3b): only an authored launcher tumbles; the fixture's
        // knockback hits stand for launchers.
        Launches = knockback != Vector2.Zero,
        HitstunDuration = 0.6f,
        HitOrigin = new Vector2(-20f, 0f),
        AttackerFacingRight = true
    };

    private static void Step(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }

    private static StaticBody2D CreateFlatFloor() {
        var floor = new StaticBody2D {
            Name = "StoryW3Floor",
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

    private static PlayerController CreateGroundedPlayer() {
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
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
