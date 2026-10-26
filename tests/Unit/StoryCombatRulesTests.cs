using System.IO;
using System.Linq;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
[RequireGodotRuntime]
public class StoryCombatRulesTests {
    [TestCase]
    public void DropThroughAllowsOnlyTheCanonicalStates() {
        AssertThat(StoryCombatRules.IsDropThroughAllowed(CharacterState.Idle)).IsTrue();
        AssertThat(StoryCombatRules.IsDropThroughAllowed(CharacterState.Running)).IsTrue();
        AssertThat(StoryCombatRules.IsDropThroughAllowed(CharacterState.Crouching)).IsTrue();
        AssertThat(StoryCombatRules.IsDropThroughAllowed(CharacterState.Blocking)).IsTrue();
        AssertThat(StoryCombatRules.IsDropThroughAllowed(CharacterState.Attacking)).IsTrue();

        AssertThat(StoryCombatRules.IsDropThroughAllowed(CharacterState.Airborne)).IsFalse();
        AssertThat(StoryCombatRules.IsDropThroughAllowed(CharacterState.Stunned)).IsFalse();
        AssertThat(StoryCombatRules.IsDropThroughAllowed(CharacterState.Dazed)).IsFalse();
        AssertThat(StoryCombatRules.IsDropThroughAllowed(CharacterState.Dead)).IsFalse();
    }

    [TestCase]
    public void HyperArmorCoversStartupAndActiveButUltimatesBypassIt() {
        AssertThat(StoryCombatRules.HyperArmorPreventsInterruption(true, AbilityPhase.Startup, AttackClass.Basic)).IsTrue();
        AssertThat(StoryCombatRules.HyperArmorPreventsInterruption(true, AbilityPhase.Active, AttackClass.Special)).IsTrue();
        AssertThat(StoryCombatRules.HyperArmorPreventsInterruption(true, AbilityPhase.Recovery, AttackClass.Basic)).IsFalse();
        AssertThat(StoryCombatRules.HyperArmorPreventsInterruption(true, AbilityPhase.Active, AttackClass.Ultimate)).IsFalse();
        AssertThat(StoryCombatRules.HyperArmorPreventsInterruption(false, AbilityPhase.Active, AttackClass.Basic)).IsFalse();
    }

    /// <summary>
    /// D3 (user direction 2026-10-11): a landed Special's recovery opens the
    /// roll and jump cancels at recovery frame 0 and the basic-attack and
    /// movement-ability cancels at frame 6; a whiff (not landed) opens nothing,
    /// and no gate opens outside Recovery (frame -1). A buffered cancel press
    /// holds for six unfrozen ticks (provisional).
    /// </summary>
    [TestCase]
    public void ALandedSpecialsRecoveryOpensEvadesAtFrameZeroActionsAtFrameSixAndBuffersSixFrames() {
        AssertThat(StoryCombatRules.SpecialRecoveryEvadeCancelFrame).IsEqual(0);
        AssertThat(StoryCombatRules.SpecialRecoveryActionCancelFrame).IsEqual(6);
        AssertThat(StoryCombatRules.SpecialCancelBufferFrames).IsEqual(6);
        foreach (int frame in new[] { -1, 0, 5, 6 }) {
            AssertThat(StoryCombatRules.SpecialRecoveryAllowsEvade(false, frame)).IsFalse();
            AssertThat(StoryCombatRules.SpecialRecoveryAllowsAction(false, frame)).IsFalse();
        }
        AssertThat(StoryCombatRules.SpecialRecoveryAllowsEvade(true, -1)).IsFalse();
        AssertThat(StoryCombatRules.SpecialRecoveryAllowsEvade(true, 0)).IsTrue();
        AssertThat(StoryCombatRules.SpecialRecoveryAllowsEvade(true, 5)).IsTrue();
        AssertThat(StoryCombatRules.SpecialRecoveryAllowsEvade(true, 6)).IsTrue();
        AssertThat(StoryCombatRules.SpecialRecoveryAllowsAction(true, -1)).IsFalse();
        AssertThat(StoryCombatRules.SpecialRecoveryAllowsAction(true, 0)).IsFalse();
        AssertThat(StoryCombatRules.SpecialRecoveryAllowsAction(true, 5)).IsFalse();
        AssertThat(StoryCombatRules.SpecialRecoveryAllowsAction(true, 6)).IsTrue();
        AssertThat(StoryCombatRules.SpecialCancelBufferHolds(0)).IsTrue();
        AssertThat(StoryCombatRules.SpecialCancelBufferHolds(6)).IsTrue();
        AssertThat(StoryCombatRules.SpecialCancelBufferHolds(7)).IsFalse();
        AssertThat(StoryCombatRules.SpecialCancelBufferHolds(-1)).IsFalse();
    }

    [TestCase]
    public void CrouchingResizesBodyAndHurtboxFromTheBottomAndRestoresThem() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);

        var bodyShape = player.GetNode<CollisionShape2D>("CollisionShape2D");
        var bodyRect = (RectangleShape2D)bodyShape.Shape;
        var hurtboxShape = player.GetNode<CollisionShape2D>("Hurtbox/CollisionShape2D");
        var hurtboxRect = (RectangleShape2D)hurtboxShape.Shape;
        float bodyBottom = bodyShape.Position.Y + bodyRect.Size.Y * 0.5f;
        float hurtboxBottom = hurtboxShape.Position.Y + hurtboxRect.Size.Y * 0.5f;

        player.TransitionTo(CharacterState.Crouching);

        AssertThat(bodyRect.Size.Y).IsEqualApprox(64f * StoryCombatRules.CrouchHurtboxScale, 0.001f);
        AssertThat(hurtboxRect.Size.Y).IsEqualApprox(64f * StoryCombatRules.CrouchHurtboxScale, 0.001f);
        AssertThat(bodyShape.Position.Y + bodyRect.Size.Y * 0.5f).IsEqualApprox(bodyBottom, 0.001f);
        AssertThat(hurtboxShape.Position.Y + hurtboxRect.Size.Y * 0.5f).IsEqualApprox(hurtboxBottom, 0.001f);

        player.TransitionTo(CharacterState.Idle);
        AssertThat(bodyRect.Size.Y).IsEqualApprox(64f, 0.001f);
        AssertThat(hurtboxRect.Size.Y).IsEqualApprox(64f, 0.001f);
        player.Free();
    }

    [TestCase]
    public void NoSecondAttackClockDuplicatesTheFrameTables() {
        // 2026-10-04 feel pass (F2): the placeholder method-track library that
        // timed template-profile swings on an AnimationPlayer idle clock was a
        // second copy of the BasicComboRules frame tables, and its clock ignored
        // hitstop. It is deleted; every swing runs on the physics frame clock.
        AssertThat(ResourceLoader.Exists("res://resources/Animations/placeholder_combat_animation_library.tres"))
            .OverrideFailureMessage("The method-track library must not come back as a second copy of the frame tables.")
            .IsFalse();
        PlayerController player = CharacterFactory.CreateCharacter("tesla", 0, applyStoryProgression: false);
        try {
            AssertObject(player.GetNodeOrNull("CombatAnimationPlayer")).IsNull();
            var children = player.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                AssertThat(child is AnimationPlayer)
                    .OverrideFailureMessage($"{child.Name} is a second attack clock.")
                    .IsFalse();
            }
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void TemplateTimedSwingsActivateTheRuntimeHitboxOnTheFrameClock() {
        // Tesla's V7.1 string profile is the exact template — the character whose
        // grounded opener used to be timed by the deleted animation library. The
        // frame clock now opens the box on the authored startup frame and closes
        // it after the authored active window.
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer("tesla");
        try {
            SendInput(player, GameplayButtons.BasicAttack);
            var sprite = player.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(sprite.Animation.ToString()).IsEqual("basic_attack_1");
            AssertThat(hitbox.IsActive).IsFalse();

            // The press frame runs no attack tick: the Nth call after it is
            // elapsed frame N-1.
            int startup = BasicComboRules.StringProfileFor("tesla").GroundStartupFrames[0];
            for (int frame = 0; frame < startup; frame++) {
                SendInput(player, GameplayButtons.None);
                AssertThat(hitbox.IsActive)
                    .OverrideFailureMessage($"The box opened during startup (elapsed {frame}).")
                    .IsFalse();
            }
            for (int frame = 0; frame < BasicComboRules.GroundActiveFrames[0]; frame++) {
                SendInput(player, GameplayButtons.None);
                AssertThat(hitbox.IsActive)
                    .OverrideFailureMessage($"The box must be live through the active window (frame {frame}).")
                    .IsTrue();
            }
            SendInput(player, GameplayButtons.None);
            AssertThat(hitbox.IsActive).IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void AnAirPressIsOneStrikeThatNeverAdvancesTheStringAndLandingLeavesItFresh() {
        // GU-A (2026-10-12; this case pinned the retired aerial string's own
        // counter): the template-profile air strike runs its whole authored
        // window on the frame clock and exits straight out, with no chain hold,
        // so the next air press is a fresh air strike, never hit two.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("tesla");
        tree.Root.AddChild(player);
        try {
            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.BasicAttack);
            AssertThat(hitbox.HitboxID).IsEqual(BasicComboRules.AirAttackHitboxID);
            int strike = BasicComboRules.StringProfileFor("tesla").AirAttackStartupFrames
                + BasicComboRules.AirAttackActiveFrames
                + BasicComboRules.AirAttackRecoveryFrames;
            for (int frame = 0; frame < strike; frame++) SendInput(player, GameplayButtons.None);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The air strike exits straight out, with no chain hold.")
                .IsEqual(CharacterState.Airborne);
            SendInput(player, GameplayButtons.BasicAttack);

            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(hitbox.HitboxID).IsEqual(BasicComboRules.AirAttackHitboxID);
            AssertThat(player.ComboCounter).IsEqual(0);
            AssertThat(player.GroundComboCounter).IsEqual(0);

            player.TransitionTo(CharacterState.Idle);
            AssertThat(player.ComboCounter).IsEqual(0);
            AssertThat(player.GroundComboCounter).IsEqual(0);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void StoryHyperArmorKeepsDamageButSuppressesBasicInterruptionAndUltimateBypassesIt() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("lincoln");
        tree.Root.AddChild(player);
        try {
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.MovementAbility);
            AssertThat(player.HasActiveHyperArmor).IsTrue();

            int startingHP = player.CurrentHP;
            Vector2 armoredVelocity = player.Velocity;
            Hurtbox hurtbox = player.GetNode<Hurtbox>("Hurtbox");
            float basicDamage = hurtbox.TakeHit(Hit(AttackClass.Basic));
            AssertThat(basicDamage).IsEqual(10f);
            AssertThat(player.CurrentHP).IsEqual(startingHP - 10);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingMovementAbility);
            AssertThat(player.Velocity).IsEqual(armoredVelocity);

            float ultimateDamage = hurtbox.TakeHit(Hit(AttackClass.Ultimate));
            AssertThat(ultimateDamage).IsEqual(10f);
            AssertThat(player.CurrentHP).IsEqual(startingHP - 20);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(player.Velocity).IsNotEqual(armoredVelocity);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    /// <summary>
    /// V7.3 ledge trump, Story half: a second grabber takes a held ledge — the
    /// hanger is forced off through its normal drop path with the regrab
    /// lockout armed, so it cannot instantly trump back; once the lockout
    /// expires it may contest the edge again. Release stays owner-only.
    /// </summary>
    [TestCase]
    public void ASecondGrabberTrumpsTheHangerOnAStoryLedge() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PackedScene ledgeScene = ResourceLoader.Load<PackedScene>("res://scenes/templates/LedgeGrabPointTemplate.tscn");
        var ledge = ledgeScene.Instantiate<LedgeGrabPoint>();
        PlayerController first = CharacterFactory.CreateCharacter("einstein");
        PlayerController second = CharacterFactory.CreateCharacter("joan");
        tree.Root.AddChild(ledge);
        tree.Root.AddChild(first);
        tree.Root.AddChild(second);
        try {
            first.TransitionTo(CharacterState.Airborne);
            first.Velocity = Vector2.Down;
            AssertThat(first.TryGrabLedge(ledge)).IsTrue();
            AssertObject(ledge.Occupant).IsSame(first);

            second.TransitionTo(CharacterState.Airborne);
            second.Velocity = Vector2.Down;
            AssertThat(second.TryGrabLedge(ledge))
                .OverrideFailureMessage("The second grabber must trump the hanger, not be refused.")
                .IsTrue();
            AssertObject(ledge.Occupant).IsSame(second);
            AssertThat(second.CurrentState).IsEqual(CharacterState.LedgeHanging);
            AssertThat(first.CurrentState)
                .OverrideFailureMessage("The trumped hanger must be dropped into the air.")
                .IsEqual(CharacterState.Airborne);

            // Release is owner-only: a non-occupant cannot clear the hold.
            ledge.Release(first);
            AssertObject(ledge.Occupant).IsSame(second);

            // The forced release armed the regrab lockout — no instant
            // trump-back.
            first.Velocity = Vector2.Down;
            AssertThat(first.TryGrabLedge(ledge))
                .OverrideFailureMessage("The regrab lockout must refuse an instant trump-back.")
                .IsFalse();

            // Once the lockout (the sim's 30 frames) expires, the edge is
            // contestable again and the trump works in the other direction.
            SendInput(first, GameplayButtons.None, 31.0 / 60.0);
            first.TransitionTo(CharacterState.Airborne);
            first.Velocity = Vector2.Down;
            AssertThat(first.TryGrabLedge(ledge)).IsTrue();
            AssertObject(ledge.Occupant).IsSame(first);
            AssertThat(second.CurrentState).IsEqual(CharacterState.Airborne);
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            first.Free();
            second.Free();
            ledge.Free();
        }
    }

    [TestCase]
    public void OneWayPlatformTemplateUsesGodotOneWayCollision() {
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/templates/OneWayPlatformTemplate.tscn");
        AssertObject(scene).IsNotNull();
        Node instance = scene.Instantiate();
        var shape = instance.GetNode<CollisionShape2D>("CollisionShape2D");

        AssertThat(shape.OneWayCollision).IsTrue();
        AssertThat(shape.OneWayCollisionMargin).IsEqualApprox(12f, 0.001f);
        AssertThat(instance.IsInGroup("OneWayPlatform")).IsTrue();
        instance.Free();
    }

    [TestCase]
    public void TutorialAuthorsRepresentativeOneWayPlatformAndBothLedgeOrientations() {
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/campaign/Level_00_Tutorial.tscn");
        Node instance = scene.Instantiate();
        var platform = instance.GetNode<OneWayPlatform>("AuthoredOneWayPlatform");
        var left = instance.GetNode<LedgeGrabPoint>("LeftLedge");
        var right = instance.GetNode<LedgeGrabPoint>("RightLedge");

        AssertObject(platform).IsNotNull();
        AssertThat(left.StageIsToRight).IsTrue();
        AssertThat(right.StageIsToRight).IsFalse();
        instance.Free();
    }

    [TestCase]
    public void LedgeTimeoutDamageJumpAndSceneExitReleaseOccupancy() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        // The floor lets the run touch down between hangs: the V7.3 per-airtime
        // budget (LedgeRegrabsPerAirtime = 3) would otherwise refuse the fourth
        // grab this test takes.
        StaticBody2D floor = CreateFlatFloor();
        PackedScene ledgeScene = ResourceLoader.Load<PackedScene>("res://scenes/templates/LedgeGrabPointTemplate.tscn");
        var ledge = ledgeScene.Instantiate<LedgeGrabPoint>();
        var player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(ledge);
        tree.Root.AddChild(player);
        try {
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            AssertThat(player.TryGrabLedge(ledge)).IsTrue();
            SendInput(player, GameplayButtons.None, 5.1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
            AssertObject(ledge.Occupant).IsNull();

            player.Velocity = Vector2.Down;
            AssertThat(player.TryGrabLedge(ledge)).IsTrue();
            SendInput(player, GameplayButtons.Jump);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
            AssertThat(player.Velocity.Y < 0f).IsTrue();
            AssertObject(ledge.Occupant).IsNull();

            player.Velocity = Vector2.Down;
            AssertThat(player.TryGrabLedge(ledge)).IsTrue();
            int startingHP = player.CurrentHP;
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(AttackClass.Basic));
            AssertThat(player.CurrentHP).IsEqual(startingHP - 10);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertObject(ledge.Occupant).IsNull();

            // Three grabs spent this airtime: land to reset the V7.3 budget
            // before the scene-exit case takes its own grab.
            player.Velocity = new Vector2(0f, 400f);
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                SendInput(player, GameplayButtons.None);
            }
            AssertThat(player.IsOnFloor())
                .OverrideFailureMessage("The player must touch down to reset the regrab budget.")
                .IsTrue();
            // The reset reads the landing on the following tick.
            SendInput(player, GameplayButtons.None);

            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            AssertThat(player.TryGrabLedge(ledge)).IsTrue();
            player.Free();
            AssertObject(ledge.Occupant).IsNull();
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            if (GodotObject.IsInstanceValid(player)) player.Free();
            ledge.Free();
            floor.Free();
        }
    }

    /// <summary>
    /// Gameplay-feel plan §2.11: Story ledge capture widened from "falling only"
    /// to "falling, or rising slowly", so jumping up to a ledge catches it the way
    /// the Fighter simulation now does. Godot screen space is +Y down, so the
    /// rising side of the band is negative.
    /// </summary>
    [TestCase]
    public void LedgeCaptureAllowsASlowRiseButNotAFastOne() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PackedScene ledgeScene = ResourceLoader.Load<PackedScene>("res://scenes/templates/LedgeGrabPointTemplate.tscn");
        var ledge = ledgeScene.Instantiate<LedgeGrabPoint>();
        var player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(ledge);
        tree.Root.AddChild(player);
        try {
            // Rising fast, well past the band: no capture.
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = new Vector2(0f, PlayerController.LedgeGrabMaximumRiseSpeed - 1f);
            AssertThat(player.TryGrabLedge(ledge)).IsFalse();
            AssertObject(ledge.Occupant).IsNull();

            // Exactly at the band's rising edge: captured.
            player.Velocity = new Vector2(0f, PlayerController.LedgeGrabMaximumRiseSpeed);
            AssertThat(player.TryGrabLedge(ledge)).IsTrue();
            AssertThat(player.CurrentState).IsEqual(CharacterState.LedgeHanging);
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            if (GodotObject.IsInstanceValid(player)) player.Free();
            ledge.Free();
        }
    }

    [TestCase]
    public void DeathRespawnAndPausePathsEnforceTheirStateContracts() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var player = CharacterFactory.CreateCharacter("einstein");
        var pauseMenu = new PauseMenu();
        tree.Root.AddChild(player);
        tree.Root.AddChild(pauseMenu);
        try {
            player.ApplyDamage(player.MaximumHP);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Dead);
            player.TransitionTo(CharacterState.Idle);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Dead);

            player.SetRewindSuspended(true);
            player.CompleteStoryRewind(Vector2.Zero, 50, grounded: true);
            // 2026-09-14: the landing hands control straight back — no
            // Respawning input lock. Package 12 W1 (R03): the landing opens the
            // Post-Landing Hold, and the two-second protection is armed at the
            // THAW, so the whole window faces live enemies.
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            AssertThat(player.IsPostRewindInvulnerable).IsFalse();
            player.BeginRecoveryHold(StoryRecoveryHoldCause.DeathRewind, 60);
            for (int frame = 0; frame < 60; frame++) SendInput(player, GameplayButtons.None);
            AssertThat(player.IsPostRewindInvulnerable)
                .OverrideFailureMessage("No protection may tick away during the hold.")
                .IsFalse();
            player.EndRecoveryHold();
            AssertThat(player.IsPostRewindInvulnerable).IsTrue();
            for (int frame = 0; frame < PlayerController.StoryRewindInvulnerabilityFrames - 1; frame++) {
                SendInput(player, GameplayButtons.None);
            }
            AssertThat(player.IsPostRewindInvulnerable).IsTrue();
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.Respawning);
            SendInput(player, GameplayButtons.None);
            AssertThat(player.IsPostRewindInvulnerable).IsFalse();

            AssertThat(pauseMenu.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);
            pauseMenu.TogglePause();
            bool pausedStateWasApplied = tree.Paused;
            pauseMenu.TogglePause();
            AssertThat(pausedStateWasApplied).IsTrue();
            AssertThat(tree.Paused).IsFalse();
        } finally {
            tree.Paused = false;
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            pauseMenu.Free();
        }
    }

    [TestCase]
    public void StoryBlockCancelsGroundedHitstunButNotAirborneHitstunOrDaze() {
        // Gameplay-feel plan §2.4, Story half, amended by the V7.3 hit-2
        // cancel gate: string hit 1 ("combo_1") is never block-cancelable;
        // from hit two on a grounded victim holding Block escapes into the
        // stance. The Fighter sim clears HitstunFrames on exactly the same
        // condition; the two must not drift.
        StaticBody2D floor = CreateFlatFloor();
        PlayerController grounded = CreateGroundedPlayer();
        PlayerController airborne = CreateGroundedPlayer();
        PlayerController control = CreateGroundedPlayer();
        try {
            // Grounded, hit 1 of the string: the escape is gated shut.
            StunOnTheFloor(grounded, hitboxID: "combo_1");
            AssertThat(grounded.CurrentState).IsEqual(CharacterState.Stunned);
            HoldInput(grounded, GameplayButtons.Block, frames: 2);
            AssertThat(grounded.CurrentState)
                .OverrideFailureMessage("Hit one's hitstun must not be block-cancelable (V7.3 hit-2 gate).")
                .IsEqual(CharacterState.Stunned);

            // Grounded, hit 2 in plain (non-tumble) hitstun — M06 (Package 12
            // W3) reserves the escape for grounded NON-tumble hitstun, so this
            // hit carries no launch. Block is held while plenty of hitstun is
            // still left.
            StunOnTheFloor(grounded, hitboxID: "combo_2", launch: false);
            AssertThat(grounded.CurrentState).IsEqual(CharacterState.Stunned);
            HoldInput(grounded, GameplayButtons.Block, frames: 2);
            AssertThat(grounded.CurrentState)
                .OverrideFailureMessage("From hit two on, a grounded victim holding Block must leave hitstun into the stance.")
                .IsEqual(CharacterState.Blocking);

            // Control: the same victim without Block rides the stun out.
            StunOnTheFloor(control, hitboxID: "combo_2", launch: false);
            HoldInput(control, GameplayButtons.None, frames: 2);
            AssertThat(control.CurrentState)
                .OverrideFailureMessage("Without Block the victim must still be stunned at the same frame.")
                .IsEqual(CharacterState.Stunned);

            // Airborne: the block stance is grounded-only, so nothing happens.
            // The two neutral frames clear the cached MoveAndSlide floor flag
            // that a bare teleport leaves behind.
            airborne.GlobalPosition = new Vector2(0f, -600f);
            airborne.TransitionTo(CharacterState.Airborne);
            HoldInput(airborne, GameplayButtons.None, frames: 2);
            AssertThat(airborne.IsOnFloor())
                .OverrideFailureMessage("The airborne harness player must be off the floor before the hit.")
                .IsFalse();
            airborne.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(AttackClass.Basic));
            AssertThat(airborne.CurrentState).IsEqual(CharacterState.Stunned);
            HoldInput(airborne, GameplayButtons.Block, frames: 2);
            AssertThat(airborne.IsOnFloor()).IsFalse();
            AssertThat(airborne.CurrentState)
                .OverrideFailureMessage("An airborne victim holding Block must stay in hitstun.")
                .IsEqual(CharacterState.Stunned);

            // Daze: the guard-break punish window is deliberately uncancelable.
            grounded.TransitionTo(CharacterState.Dazed);
            HoldInput(grounded, GameplayButtons.Block, frames: 10);
            AssertThat(grounded.CurrentState)
                .OverrideFailureMessage("Holding Block must not shorten the guard-break daze.")
                .IsEqual(CharacterState.Dazed);
        } finally {
            InputManager.Instance?.ClearInputSource(grounded.PlayerIndex);
            grounded.Free();
            airborne.Free();
            control.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void StoryKnockbackOnTheHeroIsStaticAtEveryHP() {
        // User direction 2026-10-12 (request 5): Story knockback ignores the
        // victim's HP — "the health effects of the knockback should only apply
        // to fighting mode". The same hit writes the same impulse, weight-scaled
        // only, at full HP, after a hit, and at a fifth of max HP. This replaces
        // the gameplay-feel plan §2.5 Story half (the post-damage HP ramp),
        // which the Fighter sim keeps (FighterKnockbackScalingTests).
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer();
        try {
            Hurtbox hurtbox = player.GetNode<Hurtbox>("Hurtbox");
            HitPayload hit = Hit(AttackClass.Basic);
            float weight = player.Data?.Weight ?? 1f;
            Vector2 expected = DamageCalculator.CalculateKnockback(hit.Knockback, weight, true) * 60f;

            int hp0 = player.CurrentHP;
            hurtbox.TakeHit(hit);
            int hpAfterFirst = player.CurrentHP;
            Vector2 firstImpulse = player.Velocity;

            // Each later hit starts from rest, so it proves it wrote its own
            // impulse (review fix 2026-10-13: a hit that wrote nothing used to
            // pass on the first hit's leftover velocity).
            player.Velocity = Vector2.Zero;
            hurtbox.TakeHit(hit);
            int hpAfterSecond = player.CurrentHP;
            Vector2 secondImpulse = player.Velocity;

            // Truly at a fifth of max HP (review fix 2026-10-13: Mathf.Max kept
            // the hero at 79 of 100, never near low HP), above one hit's damage
            // so the third hit does not kill.
            player.CurrentHP = player.MaximumHP / 5;
            int hpBeforeThird = player.CurrentHP;
            AssertThat(hpBeforeThird < hpAfterSecond && hpBeforeThird > (int)System.MathF.Round(hit.Damage))
                .OverrideFailureMessage(
                    $"Harness: the third hit must land between one hit's damage and {hpAfterSecond} HP; it lands at {hpBeforeThird}.")
                .IsTrue();
            player.Velocity = Vector2.Zero;
            hurtbox.TakeHit(hit);
            int hpAfterThird = player.CurrentHP;
            Vector2 thirdImpulse = player.Velocity;

            AssertThat(hpAfterFirst < hp0 && hpAfterSecond < hpAfterFirst && hpAfterThird < hpBeforeThird)
                .OverrideFailureMessage(
                    $"Every hit must actually have landed: {hp0} -> {hpAfterFirst} -> {hpAfterSecond}, {hpBeforeThird} -> {hpAfterThird}.")
                .IsTrue();
            AssertThat(hpAfterThird > 0).IsTrue();
            AssertThat(hpAfterThird * 5 <= player.MaximumHP)
                .OverrideFailureMessage($"The third hit must leave the hero at low HP; it left {hpAfterThird} of {player.MaximumHP}.")
                .IsTrue();
            // FU-STORYSHOVE: the grounded non-launcher's horizontal speed carries the
            // shove scale (x3), still the same at every HP - that is what is pinned.
            // (A grounded victim of this non-launcher slides: M05 zeroes the
            // vertical, so the horizontal carries the rule.)
            float expectedX = expected.X * (float)BasicComboRules.HitstunShoveScale;
            foreach ((string label, Vector2 impulse) in new[] {
                         ("full HP", firstImpulse), ("after a hit", secondImpulse), ("at a fifth of max HP", thirdImpulse) }) {
                AssertThat(impulse.X).OverrideFailureMessage($"{label}: vx {impulse.X:F3}, static {expectedX:F3}.")
                    .IsEqualApprox(expectedX, 0.001f);
                AssertThat(impulse).OverrideFailureMessage($"{label}: {impulse}, at full HP {firstImpulse}.")
                    .IsEqual(firstImpulse);
            }
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void NoStoryScriptReadsTheFighterOnlyLowHealthKnockbackScale() {
        // User direction 2026-10-12: the missing-HP knockback ramp is Fighter
        // Mode's alone. BasicComboRules states it (its float form) and the sim
        // applies it in FP64; nothing else under scripts/ may read it.
        string root = ProjectSettings.GlobalizePath("res://scripts");
        string simRoot = Path.GetFullPath(Path.Combine(root, "FighterSim")) + Path.DirectorySeparatorChar;
        const char newline = (char)10;
        var offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetFullPath(file).StartsWith(simRoot, System.StringComparison.OrdinalIgnoreCase))
            .Where(file => Path.GetFileName(file) != "BasicComboRules.cs")
            .Where(file => File.ReadAllText(file).Split(newline)
                // Code, not the prose that explains the rule.
                .Where(line => !line.TrimStart().StartsWith("//", System.StringComparison.Ordinal))
                .Any(line => line.Contains("LowHealthKnockbackScale")))
            .Select(Path.GetFileName)
            .ToList();
        if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");

        // And the Story knockback chokepoint takes no HP at all: one overload,
        // (base knockback, victim weight, attacker facing).
        var overloads = typeof(DamageCalculator).GetMethods(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static)
            .Where(method => method.Name == nameof(DamageCalculator.CalculateKnockback))
            .Select(method => string.Join(", ", method.GetParameters()
                .Select(parameter => $"{parameter.ParameterType.Name} {parameter.Name}")))
            .ToList();
        AssertThat(string.Join(" | ", overloads))
            .IsEqual("Vector2 baseKnockback, Single targetWeight, Boolean attackerFacingRight");
    }

    [TestCase]
    public void StoryFacingFollowsHeldMovementDuringASwing() {
        // Gameplay-feel plan §2.12, superseding the 2026-08-09 "facing committed
        // for the whole string" rule. The sim drops its lockFacing on the same
        // change; hitbox placement still reads facing at active-start.
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer();
        try {
            AssertThat(player.IsFacingRight).IsTrue();
            SendInput(player, GameplayButtons.BasicAttack);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);

            var source = new BufferedInputSource();
            source.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None));
            InputManager.Instance.SetInputSource(player.PlayerIndex, source);
            player._PhysicsProcess(1.0 / 60.0);

            AssertThat(player.IsFacingRight)
                .OverrideFailureMessage("Steering backward mid-swing must turn the attacker around.")
                .IsFalse();
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Turning around must not cancel the swing.")
                .IsEqual(CharacterState.Attacking);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    /// <summary>
    /// V7.3 Story landing tech: the 12-frame recovery is a LOCKED window —
    /// invulnerable, in place, no actions or movement — then Idle. It mirrors
    /// the sim's TechLockoutFrames; before this pass Story teched straight
    /// into a fully actionable Idle.
    /// </summary>
    [TestCase]
    public void LandingTechLocksThePlayerForTwelveFrames() {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer();
        try {
            // A launching hit on string hit 1 (never block-cancelable, so the
            // held Block below can only ever read as the tech input). The long
            // hitstun guarantees the flight ends while the tumble still runs.
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = 1,
                TargetIndex = 0,
                AttackID = "test.launch",
                HitboxID = "combo_1",
                AttackClass = AttackClass.Basic,
                Damage = 10f,
                Knockback = new Vector2(4f, -2f),
                // M05 (Package 12 W3b): an authored launcher, so it tumbles.
                Launches = true,
                HitstunDuration = 1.5f,
                HitOrigin = new Vector2(-20f, 0f),
                AttackerFacingRight = true
            });
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);

            // Hold Block through hitstop, the launch, and the flight; the
            // ground-contact frame techs into the locked recovery.
            bool teched = false;
            for (int frame = 0; frame < 180 && !teched; frame++) {
                HoldInput(player, GameplayButtons.Block, frames: 1);
                teched = player.IsInTechLockout;
            }
            AssertThat(teched)
                .OverrideFailureMessage("The Block-held landing never teched into the locked recovery.")
                .IsTrue();

            // Locked: no damage, no actions, no movement.
            Vector2 lockedPosition = player.GlobalPosition;
            AssertThat(player.ApplyDamage(10))
                .OverrideFailureMessage("The tech lock must be fully invulnerable.")
                .IsEqual(0);
            for (int frame = 0; frame < 5; frame++) {
                HoldInput(player, GameplayButtons.BasicAttack, frames: 1);
            }
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("No action may start during the tech lock.")
                .IsNotEqual(CharacterState.Attacking);
            AssertThat(player.GlobalPosition)
                .OverrideFailureMessage("The tech lock holds the player in place.")
                .IsEqual(lockedPosition);

            // Release: the remaining lock frames run off into Idle.
            for (int frame = 0; frame < BasicComboRules.LandingTechRecoveryFrames && player.IsInTechLockout; frame++) {
                HoldInput(player, GameplayButtons.None, frames: 1);
            }
            AssertThat(player.IsInTechLockout).IsFalse();
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The expired tech lock must release to Idle.")
                .IsEqual(CharacterState.Idle);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    /// <summary>Environment floor with its top surface at y = 0.</summary>
    private static StaticBody2D CreateFlatFloor() {
        var floor = new StaticBody2D {
            Name = "StoryCombatFeelFloor",
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

    private static PlayerController CreateGroundedPlayer(string characterID = "einstein") {
        PlayerController player = CharacterFactory.CreateCharacter(characterID);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        player.GlobalPosition = new Vector2(0f, -8f);
        var neutral = new BufferedInputSource();
        neutral.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
        InputManager.Instance.SetInputSource(player.PlayerIndex, neutral);
        for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
            player.Velocity = new Vector2(player.Velocity.X, 400f);
            player._PhysicsProcess(1.0 / 60.0);
        }
        AssertThat(player.IsOnFloor())
            .OverrideFailureMessage("The harness player must be standing on the floor.")
            .IsTrue();
        // The landing frame only leaves Airborne on the *next* tick, and an
        // Airborne dispatch swallows the tick's action input on its way to
        // Idle - settle the state machine before a test scripts an input.
        for (int frame = 0; frame < 10 && player.CurrentState != CharacterState.Idle; frame++) {
            player._PhysicsProcess(1.0 / 60.0);
        }
        AssertThat(player.CurrentState)
            .OverrideFailureMessage("The harness player must settle to Idle before a test scripts input.")
            .IsEqual(CharacterState.Idle);
        return player;
    }

    /// <summary>
    /// Lands a stunning hit and drops the launch so the victim is back on the
    /// floor for the escape check, with most of the hitstun still to run. The
    /// hitboxID drives the V7.3 hit-2 cancel gate ("combo_1" is unescapable).
    /// </summary>
    private static void StunOnTheFloor(
        PlayerController player, string hitboxID = "primary", bool launch = true) {
        HitPayload hit = Hit(AttackClass.Basic, hitboxID);
        // M06 (Package 12 W3): a hit with a knockback vector is a tumble
        // (launched hitstun) until W3b's authored Launches flag lands; a
        // zero-knockback hit is plain grounded hitstun.
        if (!launch) hit.Knockback = Vector2.Zero;
        player.GetNode<Hurtbox>("Hurtbox").TakeHit(hit);
        // V7.1: run the hit's short hitstop freeze off first — its expiry
        // resolves the stashed DI launch, which the manual zero below then
        // drops so the victim is standing on the floor again.
        HoldInput(player, GameplayButtons.None, frames: BasicComboRules.HitstopFrames(10));
        player.Velocity = Vector2.Zero;
        HoldInput(player, GameplayButtons.None, frames: 1);
    }

    private static void HoldInput(PlayerController player, GameplayButtons buttons, int frames) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        for (int frame = 0; frame < frames; frame++) player._PhysicsProcess(1.0 / 60.0);
    }


    /// <summary>
    /// Gameplay feel §2.8: Story routes Up + BasicAttack and airborne
    /// Down + BasicAttack to their own single-strike hitboxes, off the chain,
    /// using the frame data and knockback components in
    /// <see cref="BasicComboRules"/> — the same table the Fighter simulation
    /// reads. Neither move has an authored animation, so both must run on the
    /// frame clock rather than arming the animation-driven path.
    /// </summary>
    [TestCase]
    public void DirectionalAttacksRouteToTheirOwnSingleStrikeOutsideTheChain() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            var hitShape = hitbox.GetChild<CollisionShape2D>(0);
            float baseKnockback = player.Data.BasicAttackKnockback;

            // No direction held in the air: the single air strike (GU-A), not
            // the string opener.
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.BasicAttack);
            AssertThat(hitbox.HitboxID).IsEqual(BasicComboRules.AirAttackHitboxID);
            player.TransitionTo(CharacterState.Airborne);

            // Up held: the up-attack.
            SendInput(player, GameplayButtons.BasicAttack, vertical: -1f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(hitbox.HitboxID).IsEqual(BasicComboRules.UpAttackHitboxID);
            AssertThat(hitbox.AttackClass).IsEqual(AttackClass.Basic);
            AssertThat(hitbox.Damage)
                .IsEqualApprox(
                    player.Data.BasicAttackDamage * BasicComboRules.DirectionalAttackDamageMultiplier,
                    0.001f);
            AssertThat(hitbox.HitstunDuration)
                .IsEqualApprox(BasicComboRules.DirectionalAttackHitstunFrames / 60f, 0.001f);
            AssertThat(hitbox.KnockbackForce.X)
                .IsEqualApprox(baseKnockback * BasicComboRules.DirectionalAttackHorizontalKnockback, 0.001f);
            AssertThat(hitbox.KnockbackForce.Y)
                .OverrideFailureMessage("The up-attack launches upward (Godot 2D Y is down).")
                .IsEqualApprox(-baseKnockback * BasicComboRules.DirectionalAttackVerticalKnockback, 0.001f);

            // The box opens on the authored startup frame, above the origin.
            // The press frame itself runs no attack tick, so the Nth call after
            // it is elapsed frame N-1.
            for (int frame = 0; frame < BasicComboRules.UpAttackStartupFrames; frame++) {
                SendInput(player, GameplayButtons.None, vertical: -1f);
                AssertThat(hitbox.IsActive)
                    .OverrideFailureMessage($"The up-attack box opened during startup (elapsed frame {frame}).")
                    .IsFalse();
            }
            SendInput(player, GameplayButtons.None, vertical: -1f);
            AssertThat(hitbox.IsActive).IsTrue();
            AssertThat(hitShape.Position.Y < 0f)
                .OverrideFailureMessage("The up-attack box must sit above the fighter's origin.")
                .IsTrue();

            // It exits straight out — no chain-hold, no advanced counter.
            int remaining = BasicComboRules.UpAttackActiveFrames + BasicComboRules.UpAttackRecoveryFrames;
            for (int frame = 0; frame < remaining; frame++) SendInput(player, GameplayButtons.None, vertical: -1f);
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.Attacking);
            AssertThat(player.ComboCounter)
                .OverrideFailureMessage("A directional attack never advances the chain.")
                .IsEqual(0);
            AssertThat(player.GroundComboCounter).IsEqual(0);

            // Airborne Down held: the down-air, with its box below the origin.
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.BasicAttack | GameplayButtons.Down);
            AssertThat(hitbox.HitboxID).IsEqual(BasicComboRules.DownAirHitboxID);
            for (int frame = 0; frame <= BasicComboRules.DownAirStartupFrames; frame++) {
                SendInput(player, GameplayButtons.Down);
            }
            AssertThat(hitbox.IsActive).IsTrue();
            AssertThat(hitShape.Position.Y > 0f)
                .OverrideFailureMessage("The down-air box must sit below the fighter's origin.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    private static void SendInput(
        PlayerController player,
        GameplayButtons buttons,
        double delta = 1.0 / 60.0,
        float vertical = 0f) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, vertical, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(delta);
    }

    private static HitPayload Hit(AttackClass attackClass, string hitboxID = "primary") => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.hit",
        HitboxID = hitboxID,
        AttackClass = attackClass,
        Damage = 10f,
        Knockback = new Vector2(4f, -2f),
        HitstunDuration = 0.25f,
        HitOrigin = new Vector2(-20f, 0f),
        AttackerFacingRight = true
    };
}
