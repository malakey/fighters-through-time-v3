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
    public void AuthoredBasicAnimationsContainMethodEventTracks() {
        AnimationLibrary library = ResourceLoader.Load<AnimationLibrary>(
            "res://resources/Animations/placeholder_combat_animation_library.tres");
        AssertObject(library).IsNotNull();

        foreach (string animationName in new[] {
            "basic_ground_1", "basic_ground_2", "basic_ground_3",
            "basic_air_1", "basic_air_2", "basic_air_3"
        }) {
            AssertThat(library.HasAnimation(animationName)).IsTrue();
            Animation animation = library.GetAnimation(animationName);
            AssertThat(animation.GetTrackCount()).IsEqual(1);
            AssertThat((int)animation.TrackGetType(0)).IsEqual((int)Animation.TrackType.Method);
            AssertThat(animation.TrackGetKeyCount(0)).IsEqual(2);
        }
    }

    [TestCase]
    public void AuthoredAnimationEventsActivateAndDeactivateTheRuntimeHitbox() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.BasicAttack);

            var animationPlayer = player.GetNode<AnimationPlayer>("CombatAnimationPlayer");
            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(hitbox.IsActive).IsFalse();

            animationPlayer.Advance(0.1);
            AssertThat(hitbox.IsActive).IsTrue();
            animationPlayer.Advance(0.15);
            AssertThat(hitbox.IsActive).IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void AerialComboUsesIndependentStateAndLandingResetDoesNotLeakIntoGroundCombo() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.BasicAttack);
            var animationPlayer = player.GetNode<AnimationPlayer>("CombatAnimationPlayer");
            animationPlayer.Advance(0.5);
            SendInput(player, GameplayButtons.BasicAttack);

            AssertThat(player.AerialComboCounter).IsEqual(1);
            AssertThat(player.GroundComboCounter).IsEqual(0);

            player.TransitionTo(CharacterState.Idle);
            AssertThat(player.AerialComboCounter).IsEqual(0);
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

    [TestCase]
    public void LedgeGrabPointEnforcesSingleOccupancyAndReleasesOwnerOnly() {
        var ledge = new LedgeGrabPoint();
        var first = new PlayerController();
        var second = new PlayerController();

        AssertThat(ledge.TryAcquire(first)).IsTrue();
        AssertThat(ledge.TryAcquire(second)).IsFalse();
        ledge.Release(second);
        AssertThat(ledge.TryAcquire(second)).IsFalse();
        ledge.Release(first);
        AssertThat(ledge.TryAcquire(second)).IsTrue();

        ledge.Free();
        first.Free();
        second.Free();
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

            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            AssertThat(player.TryGrabLedge(ledge)).IsTrue();
            player.Free();
            AssertObject(ledge.Occupant).IsNull();
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
            player.CompleteStoryRewind(Vector2.Zero, 50);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Respawning);
            SendInput(player, GameplayButtons.None, 2.1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);

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

    private static void SendInput(PlayerController player, GameplayButtons buttons, double delta = 1.0 / 60.0) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(delta);
    }

    private static HitPayload Hit(AttackClass attackClass) => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.hit",
        HitboxID = "primary",
        AttackClass = attackClass,
        Damage = 10f,
        Knockback = new Vector2(4f, -2f),
        HitstunDuration = 0.25f,
        HitOrigin = new Vector2(-20f, 0f),
        AttackerFacingRight = true
    };
}
