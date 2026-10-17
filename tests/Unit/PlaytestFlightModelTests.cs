using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Diagnostics;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-MOVE (combat and movement update 2026-10-16, decisions 6.1 and 6.2; plan
/// §2.6.1 / §2.6.2): the Story bots' flight model (<see cref="PlaytestNav.StepFlight"/>)
/// keeps matching the controller under the two movement changes it plans
/// around — the apex hang that now needs Jump held (the final jump of a flight
/// is held through the whole hang band, so it keeps its hang and its reach) and
/// corner forgiveness (the head-clip nudge and the rising lip pop, modelled from
/// the controller's own <see cref="PlayerController.HeadClipNudgePixels"/> and
/// <see cref="PlayerController.LipPopPixels"/>).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PlaytestFlightModelTests {
    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();
    private const float FloorY = 0f;

    /// <summary>
    /// A straight-up single jump and a double jump, planned by the flight model
    /// and replayed on a real controller with the model's own Jump hold on every
    /// tick: the model's final jump is held through the hang band (it hangs as
    /// long as a held jump does), the model and the controller land on the same
    /// tick, and their peaks agree to a pixel.
    /// </summary>
    [TestCase]
    public void AHeldFinalJumpKeepsItsHangAndTheModelLandsWithTheController() {
        // The nav graph reads the current scene: the host, under the root (so
        // its local coordinates are world coordinates), is it until the finally.
        Node originalScene = Tree.CurrentScene;
        var host = new Node2D { Name = "PlaytestFlightModelHost" };
        Tree.Root.AddChild(host);
        Tree.CurrentScene = host;
        PlayerController hero = null;
        try {
            var floor = new StaticBody2D {
                Name = "FlightModelFloor",
                Position = new Vector2(0f, FloorY + 20f),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(6000f, 40f) } });
            host.AddChild(floor);
            hero = CharacterFactory.CreateCharacter("joan", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
            host.AddChild(hero);

            var nav = new PlaytestNav(Tree);
            foreach (bool doubleJump in new[] { false, true }) {
                Settle(hero);
                nav.Update(hero);
                Vector2 feet = hero.GlobalPosition;
                PlaytestNav.Flight flight = nav.JumpFlight(feet, 0f, feet.X);
                if (doubleJump) {
                    flight.UseAirJumps = true;
                    flight.AirJumps = 1;
                }
                var held = new List<bool>();
                float band = StoryAirPhysics.ApexHangSpeedBandPixels;
                int modelHang = 0;
                float modelPeak = 0f;
                int modelLanding = -1;
                for (int frame = 1; frame < 400 && modelLanding < 0; frame++) {
                    float vyBefore = flight.Vel.Y;
                    bool holdingBefore = flight.Holding;
                    PlaytestNav.FlightStep step = nav.StepFlight(ref flight, nav.Obstacles as List<NavRect> ?? new List<NavRect>(), out _);
                    held.Add(flight.Holding);
                    if (frame > 1 && holdingBefore && Math.Abs(vyBefore) < band && flight.AirJumps == 0) modelHang++;
                    modelPeak = Math.Max(modelPeak, feet.Y - flight.Pos.Y);
                    AssertThat(step).IsNotEqual(PlaytestNav.FlightStep.Crashed);
                    if (step == PlaytestNav.FlightStep.Landed) modelLanding = frame;
                }
                AssertThat(modelLanding > 0).OverrideFailureMessage("the model never landed.").IsTrue();
                AssertThat(modelHang)
                    .OverrideFailureMessage($"double {doubleJump}: the model's final jump must hang through the band.")
                    .IsGreater(10);

                // The controller, fed the model's hold tick for tick.
                int landing = -1;
                int hang = 0;
                float peak = 0f;
                GameplayButtons previous = GameplayButtons.None;
                float g = StoryAirPhysics.GravityPerFrame(hero.Data.Weight);
                for (int frame = 1; frame <= held.Count + 5 && landing < 0; frame++) {
                    GameplayButtons buttons = frame == 1 || (frame <= held.Count && held[frame - 1])
                        ? GameplayButtons.Jump : GameplayButtons.None;
                    float before = hero.Velocity.Y;
                    Step(hero, buttons, previous);
                    previous = buttons;
                    if (frame > 1 && Math.Abs(before) < band && Math.Abs(hero.Velocity.Y - before - g * 0.5f) < 0.001f) hang++;
                    peak = Math.Max(peak, feet.Y - hero.GlobalPosition.Y);
                    if (frame > 1 && hero.IsOnFloor()) landing = frame;
                }
                AssertThat(landing)
                    .OverrideFailureMessage($"double {doubleJump}: the model landed on frame {modelLanding}, the controller on {landing}.")
                    .IsEqual(modelLanding);
                AssertThat(Math.Abs(peak - modelPeak) <= 1f)
                    .OverrideFailureMessage($"double {doubleJump}: peaks {peak} vs the model's {modelPeak}.")
                    .IsTrue();
                AssertThat(hang).OverrideFailureMessage($"double {doubleJump}: the controller's final jump did not hang.")
                    .IsGreater(10);
            }
        } finally {
            if (hero != null && GodotObject.IsInstanceValid(hero)) InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            if (Tree.CurrentScene == host) Tree.CurrentScene = originalScene;
            if (GodotObject.IsInstanceValid(host)) {
                host.GetParent()?.RemoveChild(host);
                host.Free();
            }
        }
    }

    /// <summary>
    /// The model's corner forgiveness, from the controller's constants: a
    /// rising flight whose head a ceiling corner overlaps by up to 12 px slides
    /// past it and keeps rising, 13 px bonks; a rising flight drifting into a
    /// wall whose top is up to 10 px above its feet is lifted onto it, 11 px
    /// crashes.
    /// </summary>
    [TestCase]
    public void TheModelNudgesAndPopsLikeTheController() {
        var nav = new PlaytestNav(Tree) {
            Hero = new HeroPhysics {
                JumpSpeed = 700f, Weight = 1f, GravityPerFrame = StoryAirPhysics.GravityPerFrame(1f),
                RunSpeed = 300f, AirControl = 1f, MaxJumps = 2
            }
        };
        float half = HeroPhysics.BodyHalfWidth;
        float height = HeroPhysics.BodyHeight;

        foreach (int clip in new[] { 4, 12, 13 }) {
            // A ceiling whose right edge overlaps the head by `clip` px, its
            // underside 2 px above the head.
            float bottom = -height - 2f;
            var ceiling = Rect(new Rect2(-400f, bottom - 80f, 400f - half + clip, 80f));
            var flight = new PlaytestNav.Flight {
                Pos = Vector2.Zero, Vel = new Vector2(0f, -400f), Holding = true, Jumped = true, TargetX = 0f
            };
            nav.StepFlight(ref flight, new List<NavRect> { ceiling }, out _);
            bool slid = flight.Vel.Y < 0f && flight.Pos.X > 0f;
            AssertThat(slid)
                .OverrideFailureMessage($"{clip} px clip: slid {slid} (x {flight.Pos.X}, vy {flight.Vel.Y}).")
                .IsEqual(clip <= PlayerController.HeadClipNudgePixels);
            if (!slid) {
                AssertThat(flight.Vel.Y).IsEqual(0f);
                AssertThat(flight.Pos.X).IsEqual(0f);
            }
        }

        foreach (int lip in new[] { 3, 10, 11 }) {
            // A wall 0.5 px ahead whose top is `lip` px above the feet; the
            // flight drifts into it, barely rising.
            var wall = Rect(new Rect2(half + 0.5f, -lip, 300f, 300f));
            var flight = new PlaytestNav.Flight {
                Pos = Vector2.Zero, Vel = new Vector2(120f, -30f), Holding = true, Jumped = true, TargetX = 600f
            };
            PlaytestNav.FlightStep step = nav.StepFlight(ref flight, new List<NavRect> { wall }, out _);
            bool popped = step != PlaytestNav.FlightStep.Crashed;
            AssertThat(popped)
                .OverrideFailureMessage($"{lip} px lip: popped {popped}.")
                .IsEqual(lip <= PlayerController.LipPopPixels);
            if (popped) AssertThat(flight.Pos.Y <= -lip).IsTrue();
        }
    }

    // === helpers ===

    private static NavRect Rect(Rect2 rect) {
        var r = new NavRect {
            Center = rect.GetCenter(),
            Half = rect.Size / 2f,
            Solid = true,
            AxisAligned = true
        };
        r.RecomputeAabb();
        return r;
    }

    /// <summary>Puts the hero back on the floor at x = 0 and lets it settle into Idle.</summary>
    private static void Settle(PlayerController hero) {
        hero.GlobalPosition = new Vector2(0f, FloorY - 8f);
        hero.Velocity = Vector2.Zero;
        // At least a few ticks: IsOnFloor is the last slide's, so a hero that
        // just landed elsewhere still reads it.
        for (int frame = 0; frame < 120 && (frame < 3 || !hero.IsOnFloor()); frame++) {
            hero.Velocity = new Vector2(0f, 400f);
            Step(hero, GameplayButtons.None, GameplayButtons.None);
        }
        for (int frame = 0; frame < 20 && hero.CurrentState != CharacterState.Idle; frame++) {
            Step(hero, GameplayButtons.None, GameplayButtons.None);
        }
        AssertThat(hero.IsOnFloor() && hero.CurrentState == CharacterState.Idle).IsTrue();
        hero.Velocity = Vector2.Zero;
    }

    private static void Step(PlayerController hero, GameplayButtons held, GameplayButtons previous) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, held, previous));
        InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
        hero._PhysicsProcess(1.0 / 60.0);
    }
}
