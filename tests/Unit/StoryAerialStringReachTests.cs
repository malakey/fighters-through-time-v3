using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The Story air strike's box (GU-A, 2026-10-12; this suite pinned the retired
/// aerial string's three boxes under D6b). An air press is one strike whose
/// box takes string hit 1's grounded x-span — the same near and far edge
/// through <c>PlayerController.ResolveGroundStringBoxPixels</c>, with the
/// profile's reach percentage and the AttackRange minor scaling it exactly as
/// they scale the grounded box — so it is front-only and reaches as far as the
/// grounded opener (D6b's rule, ledger <c>USER-STORY-AERIAL-REACH</c>). Its
/// vertical span is the air strike's own
/// (<c>BasicComboRules.AirAttackBoxCentreYUnits</c> /
/// <c>AirAttackBoxHeightUnits</c>, −64 … +40 px at 100 %), reaching below the
/// feet so a jump-in connects on a grounded foe, and its facing is read at
/// active start, not latched when the strike starts.
///
/// <para>Every hero is built with <c>applyStoryProgression: false</c>, so no
/// grid can move an edge; Leonardo's AttackRange minor is set directly.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryAerialStringReachTests {
    private const double Step = 1.0 / 60.0;
    private const float Tolerance = 0.01f;

    // The air strike's vertical span (y-down from the feet, at 100 % height):
    // the retired aerial hit 3's, the one that reached below the feet.
    private const float AirTopAt100 = -64f;
    private const float AirBottomAt100 = 40f;

    private static readonly Vector2 GroundedSpawn = new(0f, -8f);
    private static readonly Vector2 AirborneSpawn = new(30000f, -4000f);

    [TestCase]
    public void TheAirStrikeTakesHitOnesXSpanInFrontAndReachesBelowTheFeet() {
        (string Hero, float Minor)[] heroes = {
            ("tesla", 1f),      // 100 % width, 100 % height
            ("cleopatra", 1f),  // 120 % width
            ("joan", 1f),       // 90 % width, 110 % height
            ("leonardo", 1.08f) // 115 % width and an AttackRange minor
        };
        StaticBody2D floor = CreateFlatFloor();
        try {
            foreach ((string hero, float minor) in heroes) {
                BasicStringProfile profile = BasicComboRules.StringProfileFor(hero);
                float heightScale = profile.ReachHeightPercent / 100f * minor;
                foreach (bool facingRight in new[] { true, false }) {
                    string at = $"{hero} (minor {minor:F2}) facing {(facingRight ? "right" : "left")}";
                    Box g = RunHeldSwings(hero, minor, airborne: false, facingRight, wanted: 1)[0];
                    List<Box> air = RunHeldSwings(hero, minor, airborne: true, facingRight, wanted: 2);

                    // Holding Attack through the whole fall opens exactly one box:
                    // the air strike never chains and never repeats on a hold.
                    AssertThat(air.Count)
                        .OverrideFailureMessage($"{at}: a held air press opened {air.Count} boxes.")
                        .IsEqual(1);
                    Box a = air[0];

                    // Front-only, mirrored by facing: the whole box lies on the
                    // facing side of the pivot.
                    AssertThat(a.NearEdge >= 0f)
                        .OverrideFailureMessage($"{at}: the air strike reaches {-a.NearEdge:F2} px behind the hero.")
                        .IsTrue();
                    AssertThat(facingRight ? a.Centre.X > 0f : a.Centre.X < 0f)
                        .OverrideFailureMessage($"{at}: the air strike sits on the wrong side ({a.Centre.X:F2}).")
                        .IsTrue();

                    // The x-span is the grounded hit 1 box's, edge for edge.
                    AssertThat(System.Math.Abs(a.NearEdge - g.NearEdge) < Tolerance)
                        .OverrideFailureMessage(
                            $"{at}: air near edge {a.NearEdge:F3} px, grounded hit 1 {g.NearEdge:F3} px.")
                        .IsTrue();
                    AssertThat(System.Math.Abs(a.FarEdge - g.FarEdge) < Tolerance)
                        .OverrideFailureMessage(
                            $"{at}: air far edge {a.FarEdge:F3} px, grounded hit 1 {g.FarEdge:F3} px.")
                        .IsTrue();

                    // ...which is the one helper's hit 1 span (template × width % × minor).
                    PlayerController.ResolveGroundStringBoxPixels(
                        BasicComboRules.AirAttackStringStep, profile, minor,
                        out Vector2 helperSize, out Vector2 helperOffset);
                    float helperFar = helperOffset.X + helperSize.X * 0.5f;
                    AssertThat(System.Math.Abs(a.FarEdge - helperFar) < Tolerance)
                        .OverrideFailureMessage($"{at}: air far edge {a.FarEdge:F3} px, the helper's {helperFar:F3} px.")
                        .IsTrue();

                    // The vertical span is the air strike's, scaled about its
                    // centre by the height percentage and the minor, and it
                    // reaches below the feet.
                    float centre = (AirTopAt100 + AirBottomAt100) * 0.5f;
                    float halfHeight = (AirBottomAt100 - AirTopAt100) * 0.5f * heightScale;
                    AssertThat(System.Math.Abs(a.Top - (centre - halfHeight)) < Tolerance)
                        .OverrideFailureMessage($"{at}: air top {a.Top:F3} px, expected {centre - halfHeight:F3} px.")
                        .IsTrue();
                    AssertThat(System.Math.Abs(a.Bottom - (centre + halfHeight)) < Tolerance)
                        .OverrideFailureMessage($"{at}: air bottom {a.Bottom:F3} px, expected {centre + halfHeight:F3} px.")
                        .IsTrue();
                    AssertThat(a.Bottom > 0f)
                        .OverrideFailureMessage($"{at}: the air strike must reach below the feet (bottom {a.Bottom:F2} px).")
                        .IsTrue();

                    // The live box is the resolver's, mirrored by facing.
                    PlayerController.ResolveAirAttackBoxPixels(profile, minor, out Vector2 airSize, out Vector2 airOffset);
                    AssertThat(System.Math.Abs(a.Size.X - airSize.X) < Tolerance
                        && System.Math.Abs(a.Size.Y - airSize.Y) < Tolerance
                        && System.Math.Abs(a.Centre.X - (facingRight ? airOffset.X : -airOffset.X)) < Tolerance
                        && System.Math.Abs(a.Centre.Y - airOffset.Y) < Tolerance)
                        .OverrideFailureMessage(
                            $"{at}: live box {a.Size} @ {a.Centre} is not the resolver's {airSize} @ {airOffset}.")
                        .IsTrue();
                }
            }
        } finally {
            floor.Free();
        }
    }

    [TestCase]
    public void AnAerialSwingTurnedDuringItsStartupStrikesInFrontOfTheNewFacing() {
        PlayerController player = CreateAirborne("tesla");
        try {
            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            var shape = hitbox.GetNode<CollisionShape2D>("CollisionShape2D");
            AssertThat(player.IsFacingRight).IsTrue();

            // The press, facing right and holding nothing.
            SendFrame(player, 0f, GameplayButtons.BasicAttack, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(hitbox.IsActive).IsFalse();

            // Hold left through the startup: facing follows held movement mid-swing.
            for (int frame = 0; frame < 60 && !hitbox.IsActive; frame++) {
                SendFrame(player, -1f, GameplayButtons.None, GameplayButtons.None);
            }
            AssertThat(hitbox.IsActive)
                .OverrideFailureMessage("The aerial swing never reached its active window.")
                .IsTrue();
            AssertThat(player.ComboCounter).IsEqual(0);
            AssertThat(player.IsFacingRight)
                .OverrideFailureMessage("Holding left through the startup must turn the hero.")
                .IsFalse();

            float halfWidth = ((RectangleShape2D)shape.Shape).Size.X * 0.5f;
            AssertThat(shape.Position.X < 0f)
                .OverrideFailureMessage(
                    $"The box must open in front of the new facing (centre x {shape.Position.X:F2}).")
                .IsTrue();
            AssertThat(shape.Position.X + halfWidth <= 0f)
                .OverrideFailureMessage(
                    $"The box reaches {shape.Position.X + halfWidth:F2} px behind the turned hero.")
                .IsTrue();
        } finally {
            Release(player);
        }
    }

    // ---- helpers ------------------------------------------------------------

    /// <summary>A live melee box relative to the pivot, with its edges measured along the facing.</summary>
    private readonly struct Box {
        public Box(Vector2 centre, Vector2 size, bool facingRight) {
            Centre = centre;
            Size = size;
            float forwardCentre = facingRight ? centre.X : -centre.X;
            NearEdge = forwardCentre - size.X * 0.5f;
            FarEdge = forwardCentre + size.X * 0.5f;
            Top = centre.Y - size.Y * 0.5f;
            Bottom = centre.Y + size.Y * 0.5f;
        }

        public Vector2 Centre { get; }
        public Vector2 Size { get; }
        /// <summary>The edge nearest the pivot, in px in front of it (negative = behind).</summary>
        public float NearEdge { get; }
        /// <summary>The edge farthest in front of the pivot, in px.</summary>
        public float FarEdge { get; }
        public float Top { get; }
        public float Bottom { get; }
    }

    /// <summary>
    /// Builds <paramref name="hero"/>, presses and then holds BasicAttack for up
    /// to 120 frames (grounded on the floor, or in free fall) and records the
    /// live melee box on the first active frame of each swing, stopping at
    /// <paramref name="wanted"/> boxes. Grounded the first box is string hit 1;
    /// airborne it is the air strike.
    /// </summary>
    private static List<Box> RunHeldSwings(string hero, float minor, bool airborne, bool facingRight, int wanted) {
        PlayerController player = airborne ? CreateAirborne(hero) : CreateGroundedPlayer(hero);
        try {
            player.StoryAttackRangeMultiplier = minor;
            float moveX = facingRight ? 0f : -1f;
            if (!facingRight) {
                SendFrame(player, moveX, GameplayButtons.None, GameplayButtons.None);
                AssertThat(player.IsFacingRight)
                    .OverrideFailureMessage($"{hero}: the harness could not turn the hero left.")
                    .IsFalse();
            }

            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            var shape = hitbox.GetNode<CollisionShape2D>("CollisionShape2D");
            var boxes = new List<Box>();
            bool wasActive = false;
            GameplayButtons previous = GameplayButtons.None;
            for (int frame = 0; frame < 120 && boxes.Count < wanted; frame++) {
                SendFrame(player, moveX, GameplayButtons.BasicAttack, previous);
                previous = GameplayButtons.BasicAttack;
                bool active = hitbox.IsActive;
                if (active && !wasActive) {
                    AssertThat(player.ComboCounter)
                        .OverrideFailureMessage($"{hero}: swing {boxes.Count + 1} opened as step {player.ComboCounter}.")
                        .IsEqual(boxes.Count);
                    AssertThat(hitbox.HitboxID)
                        .IsEqual(airborne ? BasicComboRules.AirAttackHitboxID : $"combo_{boxes.Count + 1}");
                    AssertThat(player.IsOnFloor())
                        .OverrideFailureMessage(
                            $"{hero} swing {boxes.Count + 1}: the {(airborne ? "aerial" : "grounded")} harness changed ground state.")
                        .IsEqual(!airborne);
                    AssertThat(player.IsFacingRight).IsEqual(facingRight);
                    boxes.Add(new Box(shape.Position, ((RectangleShape2D)shape.Shape).Size, facingRight));
                }
                wasActive = active;
            }
            AssertThat(boxes.Count > 0)
                .OverrideFailureMessage($"{hero}: the {(airborne ? "aerial" : "grounded")} press opened no box.")
                .IsTrue();
            return boxes;
        } finally {
            Release(player);
        }
    }

    private static StaticBody2D CreateFlatFloor() {
        var floor = new StaticBody2D {
            Name = "StoryAerialStringReachFloor",
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
        PlayerController player = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        try {
            player.GlobalPosition = GroundedSpawn;
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                SendFrame(player, 0f, GameplayButtons.None, GameplayButtons.None);
            }
            AssertThat(player.IsOnFloor())
                .OverrideFailureMessage("The harness player must be standing on the floor.")
                .IsTrue();
            for (int frame = 0; frame < 10 && player.CurrentState != CharacterState.Idle; frame++) {
                SendFrame(player, 0f, GameplayButtons.None, GameplayButtons.None);
            }
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The harness player must settle to Idle before a test scripts input.")
                .IsEqual(CharacterState.Idle);
            AssertThat(player.IsFacingRight).IsTrue();
            return player;
        } catch {
            Release(player);
            throw;
        }
    }

    private static PlayerController CreateAirborne(string characterID) {
        PlayerController player = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        player.GlobalPosition = AirborneSpawn;
        player.TransitionTo(CharacterState.Airborne);
        player.Velocity = Vector2.Down;
        return player;
    }

    private static void Release(PlayerController player) {
        InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        player.Free();
    }

    private static void SendFrame(PlayerController player, float moveX, GameplayButtons held, GameplayButtons previousHeld) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, moveX, 0f, held, previousHeld));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Step);
    }
}
