using System;
using FTT.Characters;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-MOVE (combat and movement update 2026-10-16, decision 6.2; plan §2.6.2;
/// ledger <c>USER-CORNER-FORGIVENESS</c>, <c>PROVISIONAL-CORNER-FORGIVENESS-RULES</c>):
/// the Story hero's two stateless corner corrections, on a real
/// <see cref="PlayerController"/> in real physics frames — the head-clip nudge
/// (a ceiling corner overlapping the rising head by up to
/// <see cref="PlayerController.HeadClipNudgePixels"/>, 12 px, is slid past;
/// 13 px bonks) and the rising lip pop (a wall top up to
/// <see cref="PlayerController.LipPopPixels"/>, 10 px, above the feet is popped
/// onto; 11 px is not) — and that a launched hero gets neither.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryCornerForgivenessTests {
    private const float Dt = 1f / 60f;
    private const float HalfWidth = CharacterFactory.HeroBodyBoxWidthPixels / 2f;
    private const float Height = CharacterFactory.HeroBodyBoxHeightPixels;
    private static readonly Vector2 Origin = new(91000f, -30000f);

    [TestCase]
    public void ACeilingCornerClippingTheHeadByUpToTwelvePixelsIsSlidPast() {
        AssertThat(PlayerController.HeadClipNudgePixels).IsEqualApprox(12f, 0.001f);
        foreach (int clip in new[] { 1, 6, 12 }) {
            HeadClipRun run = RunHeadClip(clip, launched: false);
            AssertThat(run.Nudges)
                .OverrideFailureMessage($"{clip} px: the rising head was not nudged.")
                .IsEqual(1);
            // The slide's own margin recovery can add a fraction of a pixel.
            AssertThat(run.ShiftX >= clip - 0.01f && run.ShiftX <= PlayerController.HeadClipNudgePixels + 0.25f)
                .OverrideFailureMessage($"{clip} px: the nudge moved the hero {run.ShiftX} px.")
                .IsTrue();
            AssertThat(run.PassedTheCorner)
                .OverrideFailureMessage($"{clip} px: the hero never rose past the corner.")
                .IsTrue();
        }
    }

    [TestCase]
    public void AThirteenPixelClipBonks() {
        HeadClipRun run = RunHeadClip(13, launched: false);
        AssertThat(run.Nudges).IsEqual(0);
        AssertThat(Math.Abs(run.ShiftX)).IsLess(0.01f);
        AssertThat(run.PassedTheCorner)
            .OverrideFailureMessage("A 13 px clip must bonk the head, not slide past.")
            .IsFalse();
    }

    [TestCase]
    public void AWallTopUpToTenPixelsAboveTheFeetIsPoppedOntoAndElevenIsNot() {
        AssertThat(PlayerController.LipPopPixels).IsEqualApprox(10f, 0.001f);
        foreach (int lip in new[] { 1, 5, 10 }) {
            LipRun run = RunLip(lip, launched: false);
            AssertThat(run.Pops).OverrideFailureMessage($"{lip} px: no lip pop.").IsEqual(1);
            AssertThat(run.LandedOnTop)
                .OverrideFailureMessage($"{lip} px: the hero did not end standing on the wall top.")
                .IsTrue();
        }
        LipRun missed = RunLip(11, launched: false);
        AssertThat(missed.Pops).IsEqual(0);
        AssertThat(missed.LandedOnTop)
            .OverrideFailureMessage("An 11 px lip is out of the pop's reach.")
            .IsFalse();
    }

    [TestCase]
    public void ALaunchedHeroGetsNeitherTheNudgeNorThePop() {
        HeadClipRun clip = RunHeadClip(6, launched: true);
        AssertThat(clip.Nudges).IsEqual(0);
        AssertThat(clip.PassedTheCorner)
            .OverrideFailureMessage("A launched hero slid past a ceiling corner.")
            .IsFalse();
        LipRun lip = RunLip(5, launched: true);
        AssertThat(lip.Pops).IsEqual(0);
        AssertThat(lip.LandedOnTop)
            .OverrideFailureMessage("A launched hero was popped onto a lip.")
            .IsFalse();
    }

    // === harness ===

    private readonly struct HeadClipRun {
        public readonly int Nudges;
        public readonly float ShiftX;
        public readonly bool PassedTheCorner;

        public HeadClipRun(int nudges, float shiftX, bool passed) {
            Nudges = nudges;
            ShiftX = shiftX;
            PassedTheCorner = passed;
        }
    }

    private readonly struct LipRun {
        public readonly int Pops;
        public readonly bool LandedOnTop;

        public LipRun(int pops, bool landed) {
            Pops = pops;
            LandedOnTop = landed;
        }
    }

    /// <summary>
    /// A ceiling block whose right edge overlaps the hero's head by
    /// <paramref name="clip"/> px, its underside 2 px above the head; the hero
    /// rises into it at 300 px/s with no stick. Returns the nudges, the sideways
    /// shift on the contact tick, and whether the head then rose 20 px past the
    /// ceiling's underside within 20 ticks.
    /// </summary>
    private static HeadClipRun RunHeadClip(int clip, bool launched) {
        Vector2 feet = Origin + new Vector2(5000f * clip + (launched ? 2500f : 0f), 0f);
        float ceilingBottom = feet.Y - Height - 2f;
        StaticBody2D ceiling = Block("CornerCeiling",
            new Rect2(feet.X - 400f, ceilingBottom - 80f, 400f - HalfWidth + clip, 80f));
        PlayerController player = AirborneAt(feet, launched);
        try {
            int nudgesBefore = player.HeadClipNudges;
            player.Velocity = new Vector2(0f, -300f);
            float startX = player.GlobalPosition.X;
            Step(player, 0f);
            float shift = player.GlobalPosition.X - startX;
            bool passed = false;
            for (int frame = 0; frame < 20 && !passed; frame++) {
                if (player.GlobalPosition.Y - Height < ceilingBottom - 20f) passed = true;
                if (launched) player.Velocity = new Vector2(0f, Math.Min(player.Velocity.Y, -300f));
                Step(player, 0f);
            }
            if (player.GlobalPosition.Y - Height < ceilingBottom - 20f) passed = true;
            return new HeadClipRun(player.HeadClipNudges - nudgesBefore, shift, passed);
        } finally {
            Release(player, ceiling);
        }
    }

    /// <summary>
    /// A wall whose face is 0.5 px ahead of the hero and whose top is
    /// <paramref name="lip"/> px above its feet; the hero drifts into it at 120
    /// px/s with the stick held and rises for exactly one more tick (30 px/s up,
    /// so this tick's gravity leaves it barely rising and the next one falling).
    /// Returns the pops and whether it ended standing on the wall top (there is
    /// no other floor, so a hero that misses the lip falls on).
    /// </summary>
    private static LipRun RunLip(int lip, bool launched) {
        Vector2 feet = Origin + new Vector2(5000f * lip + (launched ? 2500f : 0f), 40000f);
        float faceX = feet.X + HalfWidth + 0.5f;
        float top = feet.Y - lip;
        StaticBody2D wall = Block("CornerWall", new Rect2(faceX, top, 400f, 400f));
        PlayerController player = AirborneAt(feet, launched);
        try {
            int popsBefore = player.LipPops;
            player.Velocity = new Vector2(120f, -30f);
            for (int frame = 0; frame < 60; frame++) {
                Step(player, launched ? 0f : 1f);
                if (player.IsOnFloor()) break;
            }
            bool landed = player.IsOnFloor() && Math.Abs(player.GlobalPosition.Y - top) < 1f;
            return new LipRun(player.LipPops - popsBefore, landed);
        } finally {
            Release(player, wall);
        }
    }

    private static StaticBody2D Block(string name, Rect2 rect) {
        var body = new StaticBody2D {
            Name = name,
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = rect.GetCenter()
        };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(body);
        return body;
    }

    /// <summary>
    /// An airborne hero with its feet at <paramref name="feet"/>; a launched one
    /// is put in hitstun first (the launch states: Stunned, Thrown, Dazed).
    /// </summary>
    private static PlayerController AirborneAt(Vector2 feet, bool launched) {
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        try {
            player.GlobalPosition = feet;
            player.TransitionTo(CharacterState.Airborne);
            if (launched) {
                player.ApplyStun(2f);
                AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            }
            return player;
        } catch {
            Release(player, null);
            throw;
        }
    }

    private static void Step(PlayerController player, float moveX) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, moveX, 0f, GameplayButtons.None));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Dt);
    }

    private static void Release(PlayerController player, Node body) {
        if (player != null && GodotObject.IsInstanceValid(player)) {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
        if (body != null && GodotObject.IsInstanceValid(body)) body.Free();
    }
}
