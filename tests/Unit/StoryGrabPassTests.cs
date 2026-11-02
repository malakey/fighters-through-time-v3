using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The Story half of the 2026-10-31 grab pass: a Standard mob in hitstun is
/// grabbable, the hold throws early (a direction or Attack press after
/// <see cref="BasicComboRules.ThrowEarlyMinFrames"/> unfrozen ticks) or by
/// itself forward after <see cref="BasicComboRules.ThrowDecisionFrames"/>, the
/// catch freezes both parties, the direct gameplay_grab bind grabs, a frame
/// from a device with the chord switched off does not chord-grab, and the
/// held / thrown poses reset.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryGrabPassTests {

    private const GameplayButtons Chord = GameplayButtons.Block | GameplayButtons.BasicAttack;

    [TestCase]
    public void AStandardMobInHitstunIsCaughtAndThrown() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        EnemyController mob = CreateEnemy("chrono_slasher", new Vector2(40f, 0f));
        try {
            Settle(player);
            mob.ApplyStun(1.0f);
            AssertThat(mob.CurrentState).IsEqual(EnemyState.Stunned);
            AssertThat(mob.IsGrabbable).IsTrue();

            Catch(player);
            AssertThat(mob.IsHeldByPlayer)
                .OverrideFailureMessage("A stunned Standard mob must be caught.")
                .IsTrue();
            AssertThat(mob.CurrentState)
                .OverrideFailureMessage("The catch must end the stun.")
                .IsNotEqual(EnemyState.Stunned);

            int hpBefore = mob.CurrentHP;
            Pump(player, BasicComboRules.GrabCatchHitstopFrames + BasicComboRules.ThrowDecisionFrames
                + BasicComboRules.ThrowAnimationFrames + 4);
            AssertThat(mob.IsThrownFlight).IsTrue();
            AssertThat(mob.CurrentHP < hpBefore).IsTrue();
        } finally {
            Cleanup(player, mob, floor);
        }
    }

    [TestCase]
    public void AStunnedEliteIsStillImmune() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        EnemyController elite = CreateEnemy("chrono_guard_elite", new Vector2(40f, 0f));
        try {
            Settle(player);
            elite.ApplyStun(1.0f);
            AssertThat(elite.IsGrabbable).IsFalse();
            Pump(player, 1, Chord, Chord);
            Pump(player, BasicComboRules.GrabStartupFrames + BasicComboRules.GrabActiveFrames + 2, GameplayButtons.Block);
            AssertThat(elite.IsHeldByPlayer).IsFalse();
            AssertThat(player.GrabPhase).IsEqual(3);
        } finally {
            Cleanup(player, elite, floor);
        }
    }

    [TestCase]
    public void TheCatchFreezesBothAndAnAttackPressInTheFreezeNeverThrows() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        EnemyController mob = CreateEnemy("chrono_slasher", new Vector2(40f, 0f));
        try {
            Settle(player);
            Catch(player);
            AssertThat(player.HitstopFramesRemaining).IsEqual(BasicComboRules.GrabCatchHitstopFrames);
            AssertThat(mob.HitstopFramesRemaining >= BasicComboRules.GrabCatchHitstopFrames).IsTrue();
            Pump(player, BasicComboRules.GrabCatchHitstopFrames, GameplayButtons.None, GameplayButtons.BasicAttack);
            AssertThat(player.GrabPhase).IsEqual(4);
        } finally {
            Cleanup(player, mob, floor);
        }
    }

    [TestCase]
    public void AnAttackPressBeforeTheMinimumHoldDoesNothingAndAfterItThrowsAtOnce() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        EnemyController mob = CreateEnemy("chrono_slasher", new Vector2(40f, 0f));
        try {
            Settle(player);
            Catch(player);
            Pump(player, BasicComboRules.GrabCatchHitstopFrames);
            // Unfrozen hold ticks 0 .. 4 neutral, then the press on tick 5: too early.
            Pump(player, BasicComboRules.ThrowEarlyMinFrames - 1);
            Pump(player, 1, GameplayButtons.None, GameplayButtons.BasicAttack);
            AssertThat(player.GrabPhase)
                .OverrideFailureMessage("A press before the minimum hold must not throw.")
                .IsEqual(4);
            // Tick 6 is the first that may throw.
            Pump(player, 1, GameplayButtons.None, GameplayButtons.BasicAttack);
            AssertThat(player.GrabPhase)
                .OverrideFailureMessage("An Attack press at the minimum hold throws at once.")
                .IsEqual(5);
        } finally {
            Cleanup(player, mob, floor);
        }
    }

    [TestCase]
    public void AHeldDirectionThrowsEarlyAndPicksTheThrow() {
        float forwardVy = ThrowAndReadVerticalSpeed(moveY: 0, neutralAuto: true);
        float upVy = ThrowAndReadVerticalSpeed(moveY: -127, neutralAuto: false);
        AssertThat(upVy < forwardVy)
            .OverrideFailureMessage($"Up throw ({upVy}) must launch higher than the forward one ({forwardVy}).")
            .IsTrue();

        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        EnemyController mob = CreateEnemy("chrono_slasher", new Vector2(40f, 0f));
        try {
            Settle(player);
            Catch(player);
            bool facing = player.IsFacingRight;
            int away = facing ? -127 : 127;
            Pump(player, BasicComboRules.GrabCatchHitstopFrames);
            // Back is away from the facing; held from the first unfrozen tick it throws on tick 6.
            Pump(player, BasicComboRules.ThrowEarlyMinFrames, GameplayButtons.None, GameplayButtons.None, away);
            AssertThat(player.GrabPhase).IsEqual(4);
            Pump(player, 1, GameplayButtons.None, GameplayButtons.None, away);
            AssertThat(player.GrabPhase).IsEqual(5);
            Pump(player, BasicComboRules.ThrowAnimationFrames + 1);
            AssertThat(player.IsFacingRight)
                .OverrideFailureMessage("The back throw turns the thrower around.")
                .IsEqual(!facing);
        } finally {
            Cleanup(player, mob, floor);
        }
    }

    [TestCase]
    public void NeutralThrowsForwardByItselfAtTheDecisionFrames() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        EnemyController mob = CreateEnemy("chrono_slasher", new Vector2(40f, 0f));
        try {
            Settle(player);
            Catch(player);
            Pump(player, BasicComboRules.GrabCatchHitstopFrames + BasicComboRules.ThrowDecisionFrames - 1);
            AssertThat(player.GrabPhase).IsEqual(4);
            Pump(player, 1);
            AssertThat(player.GrabPhase).IsEqual(5);
        } finally {
            Cleanup(player, mob, floor);
        }
    }

    [TestCase]
    public void TheDirectGrabBindGrabsWithNoBlockOrAttack() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        EnemyController mob = CreateEnemy("chrono_slasher", new Vector2(40f, 0f));
        try {
            Settle(player);
            Pump(player, 1, GameplayButtons.Grab, GameplayButtons.Grab);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Grabbing);
        } finally {
            Cleanup(player, mob, floor);
        }
    }

    [TestCase]
    public void AFrameFromADeviceWithTheChordSwitchedOffDoesNotChordGrabButTheDirectBindStillDoes() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        EnemyController mob = CreateEnemy("chrono_slasher", new Vector2(40f, 0f));
        try {
            Settle(player);
            Pump(player, 1, Chord | GameplayButtons.DirectOrigin, Chord);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The Controls chord toggle must apply in Story.")
                .IsNotEqual(CharacterState.Grabbing);
            Pump(player, 3);
            Settle(player);
            Pump(player, 1, GameplayButtons.Grab | GameplayButtons.DirectOrigin, GameplayButtons.Grab);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Grabbing);
        } finally {
            Cleanup(player, mob, floor);
        }
    }

    [TestCase]
    public void TheHeldAndThrownPosesLeanAndAlwaysResetToUpright() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        EnemyController mob = CreateEnemy("chrono_slasher", new Vector2(40f, 0f));
        try {
            Settle(player);
            Catch(player);
            // The grabber stands left of the mob (hero x 0, mob x 40): the lean is negative.
            AssertThat(mob.GrabPoseRotation)
                .OverrideFailureMessage("A held mob leans toward the grabber.")
                .IsEqualApprox(-EnemyController.HeldLeanRadians, 0.0001f);

            Pump(player, BasicComboRules.GrabCatchHitstopFrames + BasicComboRules.ThrowDecisionFrames
                + BasicComboRules.ThrowAnimationFrames + 4);
            AssertThat(mob.IsThrownFlight).IsTrue();
            mob._PhysicsProcess(1.0 / 60.0);
            AssertThat(mob.GrabPoseRotation != 0f)
                .OverrideFailureMessage("A thrown mob tilts toward its travel.")
                .IsTrue();
            for (int i = 0; i < 400 && mob.IsThrownFlight; i++) mob._PhysicsProcess(1.0 / 60.0);
            AssertThat(mob.IsThrownFlight).IsFalse();
            AssertThat(mob.GrabPoseRotation)
                .OverrideFailureMessage("The pose must reset when the flight ends.")
                .IsEqual(0f);
        } finally {
            Cleanup(player, mob, floor);
        }
    }

    [TestCase]
    public void ReleasingAHeldMobAndAPoolCycleBothClearTheLean() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        EnemyController mob = CreateEnemy("chrono_slasher", new Vector2(40f, 0f));
        try {
            mob.BeginHeld(1f);
            AssertThat(mob.GrabPoseRotation > 0f).IsTrue();
            mob.ReleaseHeld();
            AssertThat(mob.GrabPoseRotation).IsEqual(0f);
            mob.BeginHeld(-1f);
            mob.OnDespawn();
            AssertThat(mob.GrabPoseRotation).IsEqual(0f);
            AssertThat(mob.IsHeldByPlayer).IsFalse();
        } finally {
            Cleanup(player, mob, floor);
        }
    }

    [TestCase]
    public void AHeldMobIsNeverPulledAndAConstructStunKeepsItsHeldPose() {
        // Merge review (grab + kit): zone pulls and a construct's own hit met the hold here.
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        EnemyController mob = CreateEnemy("chrono_slasher", new Vector2(40f, 0f));
        try {
            AssertThat(FTT.Characters.Abilities.CleopatraSandstormVortex.IsPullable(mob)).IsTrue();
            mob.BeginHeld(1f);
            AssertThat(FTT.Characters.Abilities.CleopatraSandstormVortex.IsPullable(mob))
                .OverrideFailureMessage("A pull must not take a held mob off its grabber.")
                .IsFalse();
            AnimatedSprite2D sprite = mob.GetNode<AnimatedSprite2D>("Presentation/AnimatedSprite2D");
            AssertThat(sprite.IsPlaying()).IsFalse();
            mob.ApplyStun(0.5f);
            AssertThat(sprite.IsPlaying())
                .OverrideFailureMessage("A construct stun must not restart the sheet the held pose has paused.")
                .IsFalse();
            AssertThat(sprite.Frame).IsEqual(EnemyController.HeldPoseFrame);
            mob.ReleaseHeld();
            AssertThat(FTT.Characters.Abilities.CleopatraSandstormVortex.IsPullable(mob)).IsTrue();
        } finally {
            Cleanup(player, mob, floor);
        }
    }

    // ---- Harness -------------------------------------------------------------

    private static float ThrowAndReadVerticalSpeed(int moveY, bool neutralAuto) {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        EnemyController mob = CreateEnemy("chrono_slasher", new Vector2(40f, 0f));
        try {
            Settle(player);
            Catch(player);
            Pump(player, BasicComboRules.GrabCatchHitstopFrames);
            if (neutralAuto) {
                Pump(player, BasicComboRules.ThrowDecisionFrames + 1);
            } else {
                // Up held from the first unfrozen tick throws on tick 6, never before.
                Pump(player, BasicComboRules.ThrowEarlyMinFrames, GameplayButtons.None, GameplayButtons.None, 0, moveY);
                AssertThat(player.GrabPhase).IsEqual(4);
                Pump(player, 1, GameplayButtons.None, GameplayButtons.None, 0, moveY);
            }
            AssertThat(player.GrabPhase).IsEqual(5);
            Pump(player, BasicComboRules.ThrowAnimationFrames + 1, GameplayButtons.None, GameplayButtons.None, 0, moveY);
            AssertThat(mob.IsThrownFlight).IsTrue();
            return mob.Velocity.Y;
        } finally {
            Cleanup(player, mob, floor);
        }
    }

    private static void Settle(PlayerController player) {
        Pump(player, 5);
        AssertThat(player.IsOnFloor())
            .OverrideFailureMessage("The harness player must be standing before the grab.")
            .IsTrue();
    }

    /// <summary>The chord, then frames until the hold begins (the catch tick).</summary>
    private static void Catch(PlayerController player) {
        Pump(player, 1, Chord, Chord);
        for (int i = 0; i < BasicComboRules.GrabStartupFrames + BasicComboRules.GrabActiveFrames + 2
            && player.GrabPhase != 4; i++) {
            Pump(player, 1, GameplayButtons.Block);
        }
        AssertThat(player.GrabPhase)
            .OverrideFailureMessage("The grab must connect.")
            .IsEqual(4);
    }

    private static void Pump(
        PlayerController player, int frames, GameplayButtons held = GameplayButtons.None,
        GameplayButtons pressed = GameplayButtons.None, int moveX = 0, int moveY = 0) {
        var source = new BufferedInputSource();
        source.SetNextFrame(new PlayerInputFrame {
            Tick = 0, MoveX = (sbyte)moveX, MoveY = (sbyte)moveY, Held = held, Pressed = pressed
        });
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        for (int frame = 0; frame < frames; frame++) {
            player._PhysicsProcess(1.0 / 60.0);
            if (frame == 0 && pressed != GameplayButtons.None) {
                source.SetNextFrame(new PlayerInputFrame {
                    Tick = 0, MoveX = (sbyte)moveX, MoveY = (sbyte)moveY, Held = held, Pressed = GameplayButtons.None
                });
            }
        }
    }

    private static void Cleanup(PlayerController player, EnemyController mob, StaticBody2D floor) {
        InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        player.Free();
        mob.Free();
        floor.Free();
    }

    private static PlayerController CreatePlayer() {
        PlayerController player = CharacterFactory.CreateCharacter("lincoln");
        player.Position = Vector2.Zero;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        return player;
    }

    private static EnemyController CreateEnemy(string enemyID, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        if (!enemy.IsInGroup("Enemies")) enemy.AddToGroup("Enemies");
        return enemy;
    }

    private static StaticBody2D CreateFloor() {
        var floor = new StaticBody2D {
            Name = "GrabPassTestFloor",
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
}
