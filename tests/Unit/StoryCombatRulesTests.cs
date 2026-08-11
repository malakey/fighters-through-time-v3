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
            var sprite = player.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(sprite.Animation.ToString()).IsEqual("basic_attack_1");
            AssertThat(sprite.IsPlaying()).IsTrue();
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

    [TestCase]
    public void StoryBlockCancelsGroundedHitstunButNotAirborneHitstunOrDaze() {
        // Gameplay-feel plan §2.4, Story half. The Fighter sim clears
        // HitstunFrames on exactly the same condition; the two must not drift.
        StaticBody2D floor = CreateFlatFloor();
        PlayerController grounded = CreateGroundedPlayer();
        PlayerController airborne = CreateGroundedPlayer();
        PlayerController control = CreateGroundedPlayer();
        try {
            // Grounded: the launch is dropped so the victim is standing again,
            // then Block is held while plenty of hitstun is still left.
            StunOnTheFloor(grounded);
            AssertThat(grounded.CurrentState).IsEqual(CharacterState.Stunned);
            HoldInput(grounded, GameplayButtons.Block, frames: 2);
            AssertThat(grounded.CurrentState)
                .OverrideFailureMessage("A grounded victim holding Block must leave hitstun into the stance.")
                .IsEqual(CharacterState.Blocking);

            // Control: the same victim without Block rides the stun out.
            StunOnTheFloor(control);
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
    public void StoryKnockbackScalesWithTheVictimsMissingHPAfterTheHit() {
        // Gameplay-feel plan §2.5, Story half: the same hit pushes a wounded
        // victim further, using the post-damage HP and the shared ramp.
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer();
        try {
            Hurtbox hurtbox = player.GetNode<Hurtbox>("Hurtbox");
            HitPayload hit = Hit(AttackClass.Basic);
            float weight = player.Data?.Weight ?? 1f;
            int maxHP = player.MaximumHP;

            hurtbox.TakeHit(hit);
            int hpAfterFirst = player.CurrentHP;
            Vector2 firstImpulse = player.Velocity;

            hurtbox.TakeHit(hit);
            int hpAfterSecond = player.CurrentHP;
            Vector2 secondImpulse = player.Velocity;

            AssertThat(hpAfterSecond < hpAfterFirst)
                .OverrideFailureMessage("The second hit must actually have landed.")
                .IsTrue();
            Vector2 expectedFirst = DamageCalculator.CalculateKnockback(hit.Knockback, weight, true)
                * BasicComboRules.LowHealthKnockbackScale(hpAfterFirst, maxHP) * 60f;
            Vector2 expectedSecond = DamageCalculator.CalculateKnockback(hit.Knockback, weight, true)
                * BasicComboRules.LowHealthKnockbackScale(hpAfterSecond, maxHP) * 60f;
            AssertThat(firstImpulse.X).IsEqualApprox(expectedFirst.X, 0.01f);
            AssertThat(secondImpulse.X).IsEqualApprox(expectedSecond.X, 0.01f);
            AssertThat(secondImpulse.X > firstImpulse.X)
                .OverrideFailureMessage("A wounded victim must be knocked back further by the same hit.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
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

    private static PlayerController CreateGroundedPlayer() {
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
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
    /// floor for the escape check, with most of the hitstun still to run.
    /// </summary>
    private static void StunOnTheFloor(PlayerController player) {
        player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(AttackClass.Basic));
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

            // No direction held: the ordinary string opener.
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.BasicAttack);
            AssertThat(hitbox.HitboxID).IsEqual("combo_1");
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
            AssertThat(player.AerialComboCounter)
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
