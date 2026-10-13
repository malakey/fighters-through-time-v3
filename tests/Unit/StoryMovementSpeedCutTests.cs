using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The 2026-10-12 gameplay update's movement cut, Story half (workstream GU-C;
/// the user: "reduce the movement speed by about 20%, and the jump distance by
/// the same. The jump height should remain the same."), on real
/// <see cref="PlayerController"/>s: the nine <c>MaxMoveSpeed</c> values are the
/// pre-cut ones × 0.8; a jump's height and airtime do not read the run speed, so
/// its distance is airtime × run speed and falls by exactly the same fifth; and
/// the roll and the knockdown roll get-up keep their pre-cut travel through
/// <see cref="UniversalMovementRules.RollSpeedMultiplier"/> (1.5 → 1.875), so
/// Lincoln, the slowest hero, still rolls through a standard mob from contact.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryMovementSpeedCutTests {
    /// <summary>The user's cut: "about 20%", applied as exactly × 0.8.</summary>
    private const float SpeedCut = 0.8f;

    /// <summary>The roll multiplier before the cut, the distance the roll keeps.</summary>
    private const float PreCutRollSpeedMultiplier = 1.5f;

    /// <summary>The nine <c>MaxMoveSpeed</c> values from the 2026-08-10 retune until the cut.</summary>
    private static readonly Dictionary<string, float> PreCutMaxMoveSpeed = new() {
        ["cleopatra"] = 7.25f,
        ["einstein"] = 6.75f,
        ["joan"] = 7.75f,
        ["leonardo"] = 6.5f,
        ["lincoln"] = 4.75f,
        ["mozart"] = 6.75f,
        ["shakespeare"] = 6.5f,
        ["tesla"] = 6.0f,
        ["tubman"] = 6.75f
    };

    [TestCase]
    public void EveryRunSpeedIsItsPreCutValueTimesFourFifths() {
        // The cut's own roster; a hero added later never had a pre-cut speed.
        foreach (string id in PreCutMaxMoveSpeed.Keys) {
            if (!CharacterRoster.Contains(id)) continue;
            CharacterData data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            AssertFloat(data.MaxMoveSpeed)
                .OverrideFailureMessage($"{id}: MaxMoveSpeed {data.MaxMoveSpeed} is not {PreCutMaxMoveSpeed[id]} x 0.8.")
                .IsEqualApprox(PreCutMaxMoveSpeed[id] * SpeedCut, 0.0001f);
        }
        // The roll keeps its distance: the new multiplier is the old one over the cut.
        AssertFloat(UniversalMovementRules.RollSpeedMultiplier * SpeedCut)
            .IsEqualApprox(PreCutRollSpeedMultiplier, 0.0001f);
    }

    /// <summary>
    /// A held jump from full run speed with forward held rises exactly as high
    /// and stays up exactly as long as the same jump from a standstill (no
    /// horizontal term reaches the vertical step; the single jump's rise is the
    /// <see cref="StoryAirPhysics"/> arc's), and covers that airtime at the run
    /// speed (the air cap is the run speed), so the cut takes exactly a fifth
    /// off the distance and nothing off the height. The slowest and the fastest
    /// hero, single and double jump.
    /// </summary>
    [TestCase]
    public void AJumpKeepsItsHeightAndAirtimeAndCoversAirtimeTimesTheRunSpeed() {
        foreach (string id in new[] { "lincoln", "joan" }) {
            foreach (int jumps in new[] { 1, 2 }) {
                StaticBody2D floor = null;
                PlayerController player = null;
                try {
                    floor = CreateFlatFloor();
                    player = CreateGroundedPlayer(id);
                    (float standRise, float standDistance, int standAirtime) = MeasureJump(player, jumps, moveX: 0f);
                    Release(player, null);
                    player = CreateGroundedPlayer(id);
                    (float rise, float distance, int airtime) = MeasureJump(player, jumps, moveX: 1f);
                    float run = player.Data.MaxMoveSpeed * 60f;
                    AssertFloat(rise)
                        .OverrideFailureMessage($"{id} x{jumps}: the running jump rose {rise} px, the standing one {standRise}.")
                        .IsEqualApprox(standRise, 0.1f); // MoveAndSlide's float noise is ~0.02 px
                    AssertThat(airtime)
                        .OverrideFailureMessage($"{id} x{jumps}: the running jump stayed up {airtime} ticks, the standing one {standAirtime}.")
                        .IsEqual(standAirtime);
                    AssertFloat(standDistance).IsEqualApprox(0f, 0.01f);
                    if (jumps == 1) {
                        float launch = StoryAirPhysics.JumpLaunchSpeed(player.Data.MaxJumpForce);
                        AssertFloat(rise).IsEqualApprox(StoryAirPhysics.SingleJumpRise(launch, player.Data.Weight), 0.5f);
                    }
                    AssertFloat(distance)
                        .OverrideFailureMessage($"{id} x{jumps}: covered {distance} px, airtime x run is {airtime * run / 60f}.")
                        .IsEqualApprox(airtime * run / 60f, 1f);
                    float preCut = airtime * PreCutMaxMoveSpeed[id] * 60f / 60f;
                    AssertFloat(distance / preCut).IsEqualApprox(SpeedCut, 0.005f);
                } finally {
                    Release(player, floor);
                }
            }
        }
    }

    /// <summary>
    /// The roll's travel frames and the knockdown's roll get-up move at the run
    /// speed × <see cref="UniversalMovementRules.RollSpeedMultiplier"/>: the
    /// pre-cut speed × 1.5, so each covers what it did before the cut (Lincoln
    /// 85.5 px and 99.75 px). The 4-frame stop ramp after them scales with the
    /// run speed, so the trailing slide is slightly longer than before; it is
    /// not pinned. And Lincoln, rolling from pushbox contact with a standard
    /// mob, still ends on its far side.
    /// </summary>
    [TestCase]
    public void TheRollAndTheRollGetUpKeepTheirPreCutTravelAndLincolnStillRollsThroughAMob() {
        foreach (string id in new[] { "lincoln", "joan" }) {
            float preCutRollSpeed = PreCutMaxMoveSpeed[id] * 60f * PreCutRollSpeedMultiplier;

            StaticBody2D floor = null;
            PlayerController player = null;
            try {
                floor = CreateFlatFloor();
                player = CreateGroundedPlayer(id);
                player.IsFacingRight = true;
                float travel = 0f;
                int travelTicks = 0;
                Step(player, 1f, GameplayButtons.Roll);
                for (int frame = 0; frame < 60 && player.CurrentState == CharacterState.Rolling; frame++) {
                    float before = player.GlobalPosition.X;
                    Step(player, 0f, GameplayButtons.None);
                    if (Mathf.Abs(player.Velocity.X - preCutRollSpeed) < 0.5f) {
                        travel += player.GlobalPosition.X - before;
                        travelTicks++;
                    }
                }
                AssertThat(travelTicks)
                    .OverrideFailureMessage($"{id}: {travelTicks} ticks at the pre-cut roll speed {preCutRollSpeed} px/s.")
                    .IsEqual(UniversalMovementRules.RollTravelFrames);
                AssertFloat(travel).IsEqualApprox(preCutRollSpeed * UniversalMovementRules.RollTravelFrames / 60f, 0.5f);
            } finally {
                Release(player, floor);
            }

            floor = null;
            player = null;
            try {
                floor = CreateFlatFloor();
                player = CreateGroundedPlayer(id);
                player.GetNode<Hurtbox>("Hurtbox").TakeHit(Launcher());
                for (int frame = 0; frame < 240 && !player.IsKnockedDown; frame++) Step(player, 0f, GameplayButtons.None);
                AssertThat(player.IsKnockedDown).OverrideFailureMessage($"{id}: the launch never knocked down.").IsTrue();
                while (player.KnockdownFramesRemaining > 1) Step(player, 0f, GameplayButtons.None);
                Step(player, 1f, GameplayButtons.None);
                AssertThat(player.GetUpKind).IsEqual(BasicComboRules.GetUpRoll);
                float start = player.GlobalPosition.X;
                for (int frame = 0; frame < 60 && player.IsInKnockdownOrGetUp; frame++) Step(player, 0f, GameplayButtons.None);
                AssertFloat(player.GlobalPosition.X - start)
                    .OverrideFailureMessage($"{id}: the roll get-up travelled {player.GlobalPosition.X - start} px.")
                    .IsEqualApprox(preCutRollSpeed * BasicComboRules.RollGetUpFrames / 60f, 0.5f);
            } finally {
                Release(player, floor);
            }
        }

        StaticBody2D mobFloor = null;
        PlayerController lincoln = null;
        EnemyController mob = null;
        try {
            mobFloor = CreateFlatFloor();
            lincoln = CreateGroundedPlayer("lincoln");
            lincoln.IsFacingRight = true;
            mob = CreateEnemy("chrono_slasher");
            var heroBox = lincoln.GetNode<CombatantPushbox>("Pushbox");
            var mobBox = mob.GetNode<CombatantPushbox>("Pushbox");
            float contact = (heroBox.BoxSize.X + mobBox.BoxSize.X) * 0.5f;
            mob.GlobalPosition = new Vector2(lincoln.GlobalPosition.X + contact, lincoln.GlobalPosition.Y);
            Step(lincoln, 1f, GameplayButtons.Roll);
            for (int frame = 0; frame < 60 && lincoln.CurrentState == CharacterState.Rolling; frame++) {
                Step(lincoln, 0f, GameplayButtons.None);
            }
            Step(lincoln, 0f, GameplayButtons.None);
            AssertThat(lincoln.GlobalPosition.X > mob.GlobalPosition.X)
                .OverrideFailureMessage($"Lincoln ended at {lincoln.GlobalPosition.X}, short of the mob at {mob.GlobalPosition.X}.")
                .IsTrue();
            AssertFloat(heroBox.GetHorizontalOverlap(mobBox)).IsEqual(0f);
        } finally {
            if (mob != null && GodotObject.IsInstanceValid(mob)) mob.Free();
            Release(lincoln, mobFloor);
        }
    }

    // === Harness ===

    /// <summary>
    /// A held jump (and, for two, a second press on the first tick after the
    /// rise has stopped), with <paramref name="moveX"/> held from 20 ticks before
    /// take-off to the landing. Returns the rise, the horizontal distance from
    /// take-off to landing, and the airborne ticks (jump and landing tick included).
    /// </summary>
    private static (float rise, float distance, int airtime) MeasureJump(PlayerController player, int jumps, float moveX) {
        player.IsFacingRight = true;
        for (int frame = 0; frame < 20; frame++) Step(player, moveX, GameplayButtons.None);
        float startX = player.GlobalPosition.X;
        float startY = player.GlobalPosition.Y;
        float highest = startY;
        Step(player, moveX, GameplayButtons.Jump);
        int airtime = 1;
        bool secondPressed = jumps < 2;
        bool airborne = false;
        for (int frame = 0; frame < 600; frame++) {
            if (!secondPressed && player.Velocity.Y >= 0f) {
                Step(player, moveX, GameplayButtons.Jump, GameplayButtons.None);
                secondPressed = true;
            } else {
                Step(player, moveX, GameplayButtons.Jump, GameplayButtons.Jump);
            }
            airtime++;
            highest = Mathf.Min(highest, player.GlobalPosition.Y);
            if (!player.IsOnFloor()) airborne = true;
            if (airborne && player.IsOnFloor()) break;
        }
        return (startY - highest, player.GlobalPosition.X - startX, airtime);
    }

    private static HitPayload Launcher() => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.speed_cut.launch",
        HitboxID = "combo_3",
        AttackClass = AttackClass.Basic,
        Damage = 5f,
        Knockback = new Vector2(3f, -4f),
        Launches = true,
        HitstunDuration = 1.5f,
        HitOrigin = new Vector2(-20f, 0f),
        AttackerFacingRight = true
    };

    private static void Step(PlayerController player, float moveX, GameplayButtons held,
        GameplayButtons previousHeld = GameplayButtons.None) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, moveX, 0f, held, previousHeld));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }

    /// <summary>Frees whatever a case created; null-safe, so a failed setup still cleans up.</summary>
    private static void Release(PlayerController player, StaticBody2D floor) {
        if (player != null && GodotObject.IsInstanceValid(player)) {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
        if (floor != null && GodotObject.IsInstanceValid(floor)) floor.Free();
    }

    private static StaticBody2D CreateFlatFloor() {
        var floor = new StaticBody2D {
            Name = "StoryMovementSpeedCutFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(8000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    /// <summary>
    /// A hero standing still on the floor in Idle, built without Story
    /// progression so no save's grid lanes reach its speed. The grouped player
    /// leaves the tree again if the setup itself fails.
    /// </summary>
    private static PlayerController CreateGroundedPlayer(string characterID) {
        PlayerController player = CharacterFactory.CreateCharacter(characterID, applyStoryProgression: false);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        try {
            player.GlobalPosition = new Vector2(-1500f, -8f);
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                Step(player, 0f, GameplayButtons.None);
            }
            for (int frame = 0; frame < 10 && player.CurrentState != CharacterState.Idle; frame++) {
                Step(player, 0f, GameplayButtons.None);
            }
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            player.Velocity = Vector2.Zero;
            return player;
        } catch {
            Release(player, null);
            throw;
        }
    }

    /// <summary>A standard mob from its authored scene; never ticked here, so it stands where it is put.</summary>
    private static EnemyController CreateEnemy(string enemyID) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        return enemy;
    }
}
