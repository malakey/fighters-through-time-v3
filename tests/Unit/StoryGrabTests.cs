using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the Story half of V7.2's grab &amp; throws: the Block+BasicAttack
/// chord seizes a standard mob, the decision window resolves into a throw
/// whose damage routes through the ordinary chokepoint, the thrown mob flies
/// as a projectile (crowd bowling), and elites are grab-immune (the attempt
/// whiffs into the long recovery). Numbers come from
/// <see cref="BasicComboRules"/> — the same table the Fighter sim reads.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryGrabTests {

    [TestCase]
    public void TheChordGrabsAStandardMobAndTheThrowLaunchesIt() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer(new Vector2(0f, 0f));
        EnemyController mob = CreateEnemy("chrono_slasher", new Vector2(40f, 0f));
        try {
            SettleOnFloor(player);
            AssertThat(mob.IsGrabbable).IsTrue();
            int hpBefore = mob.CurrentHP;

            // The chord from neutral: same-frame Block+BasicAttack grabs — no
            // block rises, no swing fires.
            HoldInput(player, GameplayButtons.Block | GameplayButtons.BasicAttack,
                pressed: GameplayButtons.Block | GameplayButtons.BasicAttack, frames: 1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Grabbing);

            // Startup then the active window seizes the mob.
            HoldInput(player, GameplayButtons.Block,
                pressed: GameplayButtons.None,
                frames: BasicComboRules.GrabStartupFrames + 2);
            AssertThat(mob.IsHeldByPlayer)
                .OverrideFailureMessage("The active window must seize the standard mob.")
                .IsTrue();
            AssertThat(player.GrabbedEnemy).IsEqual(mob);

            // Ride the decision window and throw animation: the mob takes the
            // 1.0x throw damage and leaves as a thrown projectile.
            HoldInput(player, GameplayButtons.None, pressed: GameplayButtons.None,
                frames: BasicComboRules.ThrowDecisionFrames + BasicComboRules.ThrowAnimationFrames + 2);
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.Grabbing);
            AssertThat(mob.CurrentHP < hpBefore)
                .OverrideFailureMessage("The throw's damage must land.")
                .IsTrue();
            AssertThat(mob.IsThrownFlight)
                .OverrideFailureMessage("A thrown mob is a projectile until it lands.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            mob.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void ElitesAreGrabImmuneAndTheAttemptWhiffsIntoRecovery() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer(new Vector2(0f, 0f));
        EnemyController elite = CreateEnemy("chrono_guard_elite", new Vector2(40f, 0f));
        try {
            SettleOnFloor(player);
            AssertThat(elite.Data.Tier).IsEqual(EnemyTier.Elite);
            AssertThat(elite.IsGrabbable).IsFalse();

            HoldInput(player, GameplayButtons.Block | GameplayButtons.BasicAttack,
                pressed: GameplayButtons.Block | GameplayButtons.BasicAttack, frames: 1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Grabbing);
            HoldInput(player, GameplayButtons.None, pressed: GameplayButtons.None,
                frames: BasicComboRules.GrabStartupFrames + BasicComboRules.GrabActiveFrames + 2);

            AssertThat(elite.IsHeldByPlayer).IsFalse();
            AssertThat(player.GrabPhase)
                .OverrideFailureMessage("The immune attempt must whiff into the long recovery.")
                .IsEqual(3);

            // The whiff recovery holds the player, then releases to Idle.
            HoldInput(player, GameplayButtons.None, pressed: GameplayButtons.None,
                frames: BasicComboRules.GrabWhiffRecoveryFrames + 2);
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.Grabbing);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            elite.Free();
            floor.Free();
        }
    }

    // ---- Harness -------------------------------------------------------------

    private static void SettleOnFloor(PlayerController player) {
        HoldInput(player, GameplayButtons.None, GameplayButtons.None, frames: 5);
        AssertThat(player.IsOnFloor())
            .OverrideFailureMessage("The harness player must be standing before the chord.")
            .IsTrue();
    }

    private static void HoldInput(
        PlayerController player, GameplayButtons held, GameplayButtons pressed, int frames) {
        var source = new BufferedInputSource();
        source.SetNextFrame(new PlayerInputFrame {
            Tick = 0, MoveX = 0, MoveY = 0, Held = held, Pressed = pressed
        });
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        for (int frame = 0; frame < frames; frame++) {
            player._PhysicsProcess(1.0 / 60.0);
            if (frame == 0 && pressed != GameplayButtons.None) {
                // Only the first pumped frame carries the press edge.
                source.SetNextFrame(new PlayerInputFrame {
                    Tick = 0, MoveX = 0, MoveY = 0, Held = held, Pressed = GameplayButtons.None
                });
            }
        }
    }

    private static PlayerController CreatePlayer(Vector2 position) {
        PlayerController player = CharacterFactory.CreateCharacter("lincoln");
        player.Position = position;
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
            Name = "GrabTestFloor",
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
