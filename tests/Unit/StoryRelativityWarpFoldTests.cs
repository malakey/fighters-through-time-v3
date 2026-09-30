using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W7a (E04) — Relativity Warp in Story is a spacetime fold: a
/// visible destination ghost for a 10-frame startup, then an instant relocation
/// of up to 4 units (240 px), shortened to the farthest point Einstein's body
/// clears; a hit in the startup cancels it with the cooldown spent.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryRelativityWarpFoldTests {
    private const double Step = 1.0 / 60.0;
    private static readonly Vector2 Origin = new(-54000f, 600f);

    [TestCase]
    public void TheFoldShowsAGhostThenRelocatesInstantlyShortOfAWall() {
        var host = new Node2D { Name = "WarpFoldWall" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            var wall = new StaticBody2D {
                Name = "Wall",
                CollisionLayer = CollisionLayers.Environment,
                Position = Origin + new Vector2(160f, 0f)
            };
            wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(40f, 600f) } });
            host.AddChild(wall);
            PlayerController einstein = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
            host.AddChild(einstein);
            einstein.GlobalPosition = Origin;
            var warp = einstein.GetNode<EinsteinRelativityWarp>("MovementAbility");
            AssertThat(warp.Data.StartupFrames).IsEqual(KitMotionRules.RelativityWarpStartupFrames);

            AssertThat(warp.TryExecute()).IsTrue();
            AssertThat(warp.GhostVisible).IsTrue();
            Vector2 destination = warp.FoldDestination;
            // Shortened: the wall's near face is 140 px out, well inside 240.
            float reach = destination.X - Origin.X;
            AssertThat(reach > 60f && reach < 140f)
                .OverrideFailureMessage($"The fold did not stop short of the wall (reach {reach}).")
                .IsTrue();

            for (int frame = 0; frame < KitMotionRules.RelativityWarpStartupFrames - 1; frame++) {
                warp._PhysicsProcess(Step);
                AssertThat(einstein.GlobalPosition).IsEqual(Origin);
            }
            warp._PhysicsProcess(Step);
            AssertThat(warp.GhostVisible).IsFalse();
            AssertThat(einstein.GlobalPosition.DistanceTo(destination) < 0.5f).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void AHitInTheStartupCancelsTheFoldWithTheCooldownSpent() {
        var host = new Node2D { Name = "WarpFoldCancel" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            PlayerController einstein = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
            host.AddChild(einstein);
            einstein.GlobalPosition = Origin;
            var warp = einstein.GetNode<EinsteinRelativityWarp>("MovementAbility");
            AssertThat(warp.TryExecute()).IsTrue();
            Vector2 destination = warp.FoldDestination;
            AssertThat(Mathf.Abs(destination.X - Origin.X)).IsEqual(240f);
            for (int frame = 0; frame < 3; frame++) warp._PhysicsProcess(Step);

            warp.Interrupt();
            for (int frame = 0; frame < 20; frame++) warp._PhysicsProcess(Step);
            AssertThat(warp.IsExecuting).IsFalse();
            AssertThat(warp.GhostVisible).IsFalse();
            AssertThat(einstein.GlobalPosition).IsEqual(Origin);
            AssertThat(einstein.MovementAbilityCooldownTimer > 0f).IsTrue();
        } finally {
            host.Free();
        }
    }
}
