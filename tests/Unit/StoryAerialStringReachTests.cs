using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// D6b (2026-10-11, ledger <c>USER-STORY-AERIAL-REACH</c>): a Story aerial
/// string hit takes its grounded twin's x-span — the same near and far edge
/// through <c>PlayerController.ResolveGroundStringBoxPixels</c>, with the
/// profile's reach percentage and the AttackRange minor scaling it exactly as
/// they scale the grounded box — so the aerial far edge is the sim's aerial
/// reach and the box is front-only. Its vertical span stays Story's own
/// (<c>BasicComboRules.AerialStringBoxCentreYUnits</c> /
/// <c>AerialStringBoxHeightUnits</c>), and its facing is read at active start,
/// not latched when the swing starts.
///
/// <para>Every hero is built with <c>applyStoryProgression: false</c>, so no
/// grid can move an edge; Leonardo's AttackRange minor is set directly.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryAerialStringReachTests {
    private const double Step = 1.0 / 60.0;
    private const float Tolerance = 0.01f;

    // The Story aerial vertical span shipped before D6b (y-down from the feet,
    // at 100 % height): hit 1 −78 … −14, hit 2 −91 … −9, hit 3 −64 … +40.
    private static readonly float[] AerialTopAt100 = { -78f, -91f, -64f };
    private static readonly float[] AerialBottomAt100 = { -14f, -9f, 40f };

    private static readonly Vector2 GroundedSpawn = new(0f, -8f);
    private static readonly Vector2 AirborneSpawn = new(30000f, -4000f);

    [TestCase]
    public void AnAerialStringHitSharesTheGroundedXSpanInFrontAndKeepsItsVerticalSpan() {
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
                    string where = $"{hero} (minor {minor:F2}) facing {(facingRight ? "right" : "left")}";
                    List<Box> grounded = RunHeldChain(hero, minor, airborne: false, facingRight);
                    List<Box> aerial = RunHeldChain(hero, minor, airborne: true, facingRight);

                    for (int step = 0; step < BasicComboRules.ComboHits; step++) {
                        string at = $"{where} hit {step + 1}";
                        Box g = grounded[step];
                        Box a = aerial[step];

                        // Front-only, mirrored by facing: the whole box lies on the
                        // facing side of the pivot.
                        AssertThat(a.NearEdge >= 0f)
                            .OverrideFailureMessage($"{at}: the aerial box reaches {-a.NearEdge:F2} px behind the hero.")
                            .IsTrue();
                        AssertThat(facingRight ? a.Centre.X > 0f : a.Centre.X < 0f)
                            .OverrideFailureMessage($"{at}: the aerial box sits on the wrong side ({a.Centre.X:F2}).")
                            .IsTrue();

                        // The x-span is the grounded box's, edge for edge.
                        AssertThat(System.Math.Abs(a.NearEdge - g.NearEdge) < Tolerance)
                            .OverrideFailureMessage(
                                $"{at}: aerial near edge {a.NearEdge:F3} px, grounded {g.NearEdge:F3} px.")
                            .IsTrue();
                        AssertThat(System.Math.Abs(a.FarEdge - g.FarEdge) < Tolerance)
                            .OverrideFailureMessage(
                                $"{at}: aerial far edge {a.FarEdge:F3} px, grounded {g.FarEdge:F3} px.")
                            .IsTrue();

                        // ...which is the one helper's span (template × width % × minor).
                        PlayerController.ResolveGroundStringBoxPixels(
                            step, profile, minor, out Vector2 helperSize, out Vector2 helperOffset);
                        float helperFar = helperOffset.X + helperSize.X * 0.5f;
                        AssertThat(System.Math.Abs(a.FarEdge - helperFar) < Tolerance)
                            .OverrideFailureMessage(
                                $"{at}: aerial far edge {a.FarEdge:F3} px, the helper's {helperFar:F3} px.")
                            .IsTrue();

                        // The vertical span is today's aerial span, scaled about its
                        // centre by the height percentage and the minor.
                        float centre = (AerialTopAt100[step] + AerialBottomAt100[step]) * 0.5f;
                        float halfHeight = (AerialBottomAt100[step] - AerialTopAt100[step]) * 0.5f * heightScale;
                        AssertThat(System.Math.Abs(a.Top - (centre - halfHeight)) < Tolerance)
                            .OverrideFailureMessage(
                                $"{at}: aerial top {a.Top:F3} px, expected {centre - halfHeight:F3} px.")
                            .IsTrue();
                        AssertThat(System.Math.Abs(a.Bottom - (centre + halfHeight)) < Tolerance)
                            .OverrideFailureMessage(
                                $"{at}: aerial bottom {a.Bottom:F3} px, expected {centre + halfHeight:F3} px.")
                            .IsTrue();
                    }
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
    /// Builds <paramref name="hero"/>, holds BasicAttack through a full
    /// three-hit string (grounded on the floor, or in free fall) and records the
    /// live melee box on each hit's first active frame.
    /// </summary>
    private static List<Box> RunHeldChain(string hero, float minor, bool airborne, bool facingRight) {
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
            for (int frame = 0; frame < 240 && boxes.Count < BasicComboRules.ComboHits; frame++) {
                SendFrame(player, moveX, GameplayButtons.BasicAttack, previous);
                previous = GameplayButtons.BasicAttack;
                bool active = hitbox.IsActive;
                if (active && !wasActive) {
                    AssertThat(player.ComboCounter)
                        .OverrideFailureMessage($"{hero}: hit {boxes.Count + 1} opened as step {player.ComboCounter}.")
                        .IsEqual(boxes.Count);
                    AssertThat(player.IsOnFloor())
                        .OverrideFailureMessage(
                            $"{hero} hit {boxes.Count + 1}: the {(airborne ? "aerial" : "grounded")} harness changed ground state.")
                        .IsEqual(!airborne);
                    AssertThat(player.IsFacingRight).IsEqual(facingRight);
                    boxes.Add(new Box(shape.Position, ((RectangleShape2D)shape.Shape).Size, facingRight));
                }
                wasActive = active;
            }
            AssertThat(boxes.Count)
                .OverrideFailureMessage(
                    $"{hero}: the held {(airborne ? "aerial" : "grounded")} string opened only {boxes.Count} of three boxes.")
                .IsEqual(BasicComboRules.ComboHits);
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
