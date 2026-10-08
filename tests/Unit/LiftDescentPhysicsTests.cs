using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// G3 (playtest bots, 2026-10-04): the Level 12 spire lift descended onto a hero
/// standing in its footprint and drove the hero through the regolith and out of
/// the world. A teleported deck's overlap is resolved by the character's own
/// depenetration, and with a floor underneath that is a squeeze: replayed here, a
/// deck whose bottom stop sat 16 px inside the floor (the shipped authoring)
/// pushed the hero 29 px into the floor, and even one resting exactly on the floor
/// pushed it 18 px in before it popped out on top.
///
/// <para>The fix is two-part: a descending deck never presses down on a body
/// beneath it (<see cref="PathMovingPlatform.IsHeldByBodyBelow"/>) — it holds
/// above the hero's head and finishes its descent once the hero steps out, and
/// riders are still carried — and every lift stop either stands clear of the
/// static geometry or sinks into it flush with its top, a walk-on stop
/// (<c>ArenaGeometryContentTests.NoMovingPlatformEndpointOverlapsStaticGeometry</c>);
/// the half-sunk stop with a step is refused. Both shipped walk-on stops (the
/// Level 12 spire lift, the Level 14 cargo lift) are played here as well as a
/// resting one. Real physics frames, a real Story hero and the Level 12 regolith
/// gravity.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LiftDescentPhysicsTests {
    private const string LiftTemplatePath = "res://scenes/templates/PathMovingPlatformTemplate.tscn";
    /// <summary>The template deck is 240 x 32, centred on the body.</summary>
    private const float DeckHalfThickness = 16f;
    private const float Rise = 400f;
    /// <summary>The Story hero's body is 64 px tall, feet on the origin.</summary>
    private const float HeroHeight = 64f;

    [TestCase]
    public async Task ADescendingLiftHoldsAboveAHeroBeneathItAndLandsOnceTheHeroStepsOut() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var floorTop = new Vector2(64000f, -30000f);
        var host = new Node2D { Name = "LiftDescentHost" };
        tree.Root.AddChild(host);
        PlayerController hero = null;
        var input = new BufferedInputSource();
        try {
            BuildFloor(host, floorTop);
            PathMovingPlatform lift = BuildLift(host, floorTop);
            hero = SpawnHero(host, floorTop);
            InputManager.Instance.SetInputSource(0, input);

            // 400 px at 300 px/s is 80 frames; give it well over that.
            float deepestFeet = float.MinValue;
            bool held = false;
            for (int frame = 0; frame < 140; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                deepestFeet = Mathf.Max(deepestFeet, hero.GlobalPosition.Y - floorTop.Y);
                held |= lift.IsHeldByBodyBelow;
            }
            AssertFloat(deepestFeet)
                .OverrideFailureMessage($"The descending deck pushed the hero {deepestFeet:0.0} px into the floor.")
                .IsLessEqual(1f);
            AssertThat(held).OverrideFailureMessage("The deck never held above the hero.").IsTrue();
            AssertThat(lift.IsHeldByBodyBelow).IsTrue();
            float deckBottom = lift.GlobalPosition.Y + DeckHalfThickness;
            AssertFloat(deckBottom)
                .OverrideFailureMessage($"The held deck (bottom {deckBottom - floorTop.Y:0.0}) overlaps the hero's head.")
                .IsLessEqual(hero.GlobalPosition.Y - HeroHeight + 0.5f);

            // The hero walks out of the footprint: the lift finishes its descent.
            input.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None));
            bool landed = false;
            for (int frame = 0; frame < 120 && !landed; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                deepestFeet = Mathf.Max(deepestFeet, hero.GlobalPosition.Y - floorTop.Y);
                landed = Mathf.Abs(lift.GlobalPosition.Y - (floorTop.Y - DeckHalfThickness)) < 0.5f;
            }
            AssertThat(landed)
                .OverrideFailureMessage($"The lift never reached its bottom stop (at {lift.GlobalPosition - floorTop}).")
                .IsTrue();
            AssertFloat(deepestFeet).IsLessEqual(1f);
        } finally {
            InputManager.Instance.ClearInputSource(0);
            FreeHero(hero);
            host.Free();
        }
    }

    [TestCase]
    public async Task ARiderIsStillCarriedAllTheWayDown() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var floorTop = new Vector2(66000f, -30000f);
        var host = new Node2D { Name = "LiftRiderHost" };
        tree.Root.AddChild(host);
        PlayerController hero = null;
        try {
            BuildFloor(host, floorTop);
            PathMovingPlatform lift = BuildLift(host, floorTop);
            // Standing on the deck at its top stop.
            hero = SpawnHero(host, floorTop + new Vector2(0f, -Rise - 2f * DeckHalfThickness));
            bool everHeld = false;
            bool landed = false;
            for (int frame = 0; frame < 140 && !landed; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                everHeld |= lift.IsHeldByBodyBelow;
                landed = Mathf.Abs(lift.GlobalPosition.Y - (floorTop.Y - DeckHalfThickness)) < 0.5f;
            }
            AssertThat(everHeld).OverrideFailureMessage("A rider on the deck stopped its descent.").IsFalse();
            AssertThat(landed).IsTrue();
            // Under the regolith's low gravity the rider may trail the deck; let it settle.
            float deckTop = lift.GlobalPosition.Y - DeckHalfThickness;
            for (int frame = 0; frame < 100 && !(hero.IsOnFloor() && Mathf.Abs(hero.GlobalPosition.Y - deckTop) < 2f); frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertFloat(hero.GlobalPosition.Y)
                .OverrideFailureMessage($"The rider is not on the landed deck (feet {hero.GlobalPosition.Y - deckTop:0.0} px from its top).")
                .IsEqualApprox(deckTop, 2f);
        } finally {
            FreeHero(hero);
            host.Free();
        }
    }

    [TestCase]
    public async Task AFlushWalkOnStopHoldsAboveAHeroThenLandsLevelWithTheFloorAndIsWalkedAcross() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var floorTop = new Vector2(68000f, -30000f);
        var host = new Node2D { Name = "LiftFlushStopHost" };
        tree.Root.AddChild(host);
        PlayerController hero = null;
        var input = new BufferedInputSource();
        try {
            BuildFloor(host, floorTop);
            // The Level 12 spire / Level 14 cargo shape: at the bottom stop the deck
            // is sunk into the floor with its top on the floor's top. A long wait
            // keeps it there for the walk across.
            PathMovingPlatform lift = BuildLift(host, floorTop, bottomCentreY: DeckHalfThickness, endpointWaitSeconds: 60f);
            hero = SpawnHero(host, floorTop);
            InputManager.Instance.SetInputSource(0, input);

            float deepestFeet = float.MinValue;
            bool held = false;
            for (int frame = 0; frame < 140; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                deepestFeet = Mathf.Max(deepestFeet, hero.GlobalPosition.Y - floorTop.Y);
                held |= lift.IsHeldByBodyBelow;
            }
            AssertFloat(deepestFeet)
                .OverrideFailureMessage($"The descending flush-stop deck pushed the hero {deepestFeet:0.0} px into the floor.")
                .IsLessEqual(1f);
            AssertThat(held).OverrideFailureMessage("The flush-stop deck never held above the hero.").IsTrue();

            // The hero steps out east: the deck finishes its descent, flush with the floor.
            input.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None));
            bool landed = false;
            for (int frame = 0; frame < 120 && !landed; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                deepestFeet = Mathf.Max(deepestFeet, hero.GlobalPosition.Y - floorTop.Y);
                landed = Mathf.Abs(lift.GlobalPosition.Y - (floorTop.Y + DeckHalfThickness)) < 0.5f;
            }
            AssertThat(landed)
                .OverrideFailureMessage($"The lift never reached its flush stop (at {lift.GlobalPosition - floorTop}).")
                .IsTrue();
            AssertFloat(deepestFeet).IsLessEqual(1f);

            // ...and the hero walks straight back west across the landed deck: no
            // step to hop, no sinking.
            input.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None));
            float highestFeet = float.MaxValue;
            bool crossed = false;
            for (int frame = 0; frame < 240 && !crossed; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                float feet = hero.GlobalPosition.Y - floorTop.Y;
                deepestFeet = Mathf.Max(deepestFeet, feet);
                highestFeet = Mathf.Min(highestFeet, feet);
                crossed = hero.GlobalPosition.X < floorTop.X - 160f;
            }
            AssertThat(crossed)
                .OverrideFailureMessage($"The hero never walked across the landed deck (stopped at x {hero.GlobalPosition.X - floorTop.X:0}).")
                .IsTrue();
            AssertFloat(highestFeet)
                .OverrideFailureMessage($"Crossing the landed deck lifted the hero {-highestFeet:0.0} px: a step, not a walk-on stop.")
                .IsGreaterEqual(-1f);
            AssertFloat(deepestFeet).IsLessEqual(1f);
        } finally {
            InputManager.Instance.ClearInputSource(0);
            FreeHero(hero);
            host.Free();
        }
    }

    /// <summary>
    /// A lift authored at its top stop (so nothing overlaps anything before the
    /// run), descending <see cref="Rise"/> to a bottom stop whose deck centre sits
    /// <paramref name="bottomCentreY"/> below the floor top: the default rests the
    /// deck on the floor (deck bottom == floor top); +<see cref="DeckHalfThickness"/>
    /// is a flush walk-on stop (deck top == floor top).
    /// </summary>
    private static PathMovingPlatform BuildLift(Node host, Vector2 floorTop,
        float bottomCentreY = -DeckHalfThickness, float endpointWaitSeconds = 1.8f) {
        var lift = ResourceLoader.Load<PackedScene>(LiftTemplatePath).Instantiate<PathMovingPlatform>();
        lift.Name = "DescendingLift";
        lift.PlatformID = "test.lift_descent";
        lift.Position = floorTop + new Vector2(0f, bottomCentreY - Rise);
        lift.Waypoints = new[] { Vector2.Zero, new Vector2(0f, Rise) };
        lift.Speed = 300f;
        lift.EndpointWaitSeconds = endpointWaitSeconds;
        host.AddChild(lift);
        return lift;
    }

    private static PlayerController SpawnHero(Node host, Vector2 feet) {
        PlayerController hero = CharacterFactory.CreateCharacter("einstein", 0);
        host.AddChild(hero);
        hero.GlobalPosition = feet;
        hero.Velocity = Vector2.Zero;
        // Level 12's regolith, where the bots fell through.
        hero.EnvironmentGravityScale = 0.36f;
        return hero;
    }

    private static void FreeHero(PlayerController hero) {
        if (hero == null || !GodotObject.IsInstanceValid(hero)) return;
        hero.GetParent()?.RemoveChild(hero);
        hero.Free();
    }

    private static void BuildFloor(Node host, Vector2 top) {
        var floor = new StaticBody2D {
            Name = "LiftDescentFloor",
            Position = top,
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        // 40 px thick like StoryLevelControllerBase.BuildFloor, top edge at the origin.
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(1200f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        host.AddChild(floor);
    }
}
