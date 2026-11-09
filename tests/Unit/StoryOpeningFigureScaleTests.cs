using FTT.Combat;
using FTT.Characters;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
[RequireGodotRuntime]
public class StoryOpeningFigureScaleTests {
    [TestCase]
    public void ResumedOpeningRegistersItsWorldWithoutMovingOrRescalingTheCheckpointHero() {
        var host = new Node2D();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            PlayerController player = CharacterFactory.CreateCharacter("einstein", 0,
                applyStoryProgression: false, applyLegacyUnlockLocks: false);
            host.AddChild(player);
            Vector2 checkpoint = new(1400f, 800f);
            player.GlobalPosition = checkpoint;
            var camera = new StoryCameraConfiner { ActiveBounds = new Rect2(0, 0, 10000, 2000) };
            player.AddChild(camera);
            AnimatedSprite2D sprite = player.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            var normalizer = RetroSpriteScaleNormalizer.Attach(sprite, RetroSpriteScaleNormalizer.FigureKind.Character);
            normalizer.FigureScale = 1.13f;
            Vector2 expectedSpriteScale = sprite.Scale;

            StoryOpeningPresentation opening = StoryOpeningPresentation.Apply(host, player, camera, 2, resumed: true);
            AssertThat(opening != null).IsTrue();
            AssertFloat(opening.PlateOpacity).IsEqual(0f);
            AssertThat(opening.ContinuationBackdrop.GetParent() == host).IsTrue();
            AssertThat(player.GlobalPosition).IsEqual(checkpoint);
            AssertThat(camera.HasOpeningFraming).IsFalse();
            AssertFloat(normalizer.FigureScale).IsEqualApprox(1.13f, 0.001f);
            AssertThat(sprite.Scale).IsEqual(expectedSpriteScale);
            camera.EnableFollow(player);
            AssertThat(camera.HasOpeningFraming).IsFalse();
            AssertFloat(camera.TargetZoom).IsEqual(StoryCameraRules.ViewZoom);
            AssertThat(player.GlobalPosition).IsEqual(checkpoint);
            AssertThat(sprite.Scale).IsEqual(expectedSpriteScale);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ReleasedOpeningFadesThePlateAndRestoresStyledTerrainWhileBackdropSurvivesDeactivation() {
        var host = new Node2D();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            StoryOpeningProfile profile = StoryOpeningProfile.ForLevel(2);
            AssertObject(profile).IsNotNull();
            PlayerController player = CharacterFactory.CreateCharacter("einstein", 0,
                applyStoryProgression: false, applyLegacyUnlockLocks: false);
            host.AddChild(player);
            var camera = new StoryCameraConfiner { ActiveBounds = new Rect2(0, 0, 10000, 2000) };
            player.AddChild(camera);
            var original = new StaticBody2D { Name = "Floor_0", Position = new Vector2(800f, profile.Spawn.Y + 20f) };
            var collision = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(1600f, 40f) } };
            original.AddChild(collision);
            host.AddChild(original);
            StoryOpeningPresentation.TrackSurface(original, new Rect2(0f, profile.Spawn.Y, 1600f, 40f));
            StoryOpeningPresentation opening = StoryOpeningPresentation.Apply(host, player, camera, 2, false, new[] { original });
            camera.EnableFollow(player);
            AssertFloat(opening.PlateOpacity).IsEqual(1f);
            AssertThat(original.GetParent() == null).IsTrue();
            ReferenceSceneBackdrop backdrop = opening.ContinuationBackdrop;
            player.GlobalPosition = new Vector2(profile.ReleaseX + 100f, player.GlobalPosition.Y);
            for (int frame = 0; frame < 180; frame++) {
                camera.StepFollow(1f / 60f);
                opening._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(camera.HasOpeningFraming).IsFalse();
            AssertFloat(opening.PlateOpacity).IsEqual(0f);
            opening.Deactivate();
            AssertThat(backdrop.IsInsideTree()).IsTrue();
            AssertThat(backdrop.GetParent() == host).IsTrue();
            AssertThat(original.GetParent() == host).IsTrue();
            AssertObject(original.GetNodeOrNull<Node2D>("ReferenceTerrain")).IsNotNull();
            AssertThat(((RectangleShape2D)collision.Shape).Size).IsEqual(new Vector2(1600f, 40f));
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void OpeningScaleMatchesOpaqueHeightKeepsFeetAndRestoresAnExistingFigureScale() {
        var host = new Node2D();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            Image image = Image.CreateEmpty(96, 128, false, Image.Format.Rgba8);
            image.FillRect(new Rect2I(30, 25, 30, 100), Colors.White);
            var frames = new SpriteFrames();
            frames.AddAnimation("idle");
            frames.AddFrame("idle", ImageTexture.CreateFromImage(image));
            var sprite = new AnimatedSprite2D {
                SpriteFrames = frames, Animation = "idle", Scale = Vector2.One * 0.8f
            };
            var label = new Label { Name = "NameLabel", Position = new Vector2(-55f, -84f), Size = new Vector2(110f, 18f) };
            host.AddChild(label);
            host.AddChild(sprite);
            var normalizer = RetroSpriteScaleNormalizer.Attach(sprite, RetroSpriteScaleNormalizer.FigureKind.Character);
            normalizer.FigureScale = 1.2f;
            var scale = new StoryOpeningFigureScale(sprite, 450f, 0.4f);
            AssertFloat(scale.AchievedFraction).IsEqualApprox(0.4f, 0.001f);
            AssertFloat(sprite.Scale.Y * 100f).IsEqualApprox(180f, 0.01f);
            AssertFloat(sprite.Position.Y + (64f - 3f) * sprite.Scale.Y).IsEqualApprox(0f, 0.001f);
            AssertFloat(label.Position.Y + label.Size.Y).IsLess(-180f);
            AssertThat(host.Scale).IsEqual(Vector2.One);
            scale.SetWeight(0.5f);
            AssertFloat(sprite.Scale.Y * 100f).IsEqualApprox(138f, 0.01f);
            scale.Restore();
            AssertThat(label.Position).IsEqual(new Vector2(-55f, -84f));
            AssertFloat(normalizer.FigureScale).IsEqualApprox(1.2f, 0.001f);
            AssertFloat(sprite.Scale.Y).IsEqualApprox(0.96f, 0.001f);
            var capped = new StoryOpeningFigureScale(sprite, 450f, 1f);
            AssertFloat(capped.Multiplier).IsEqual(StoryOpeningFigureScale.MaximumMultiplier);
            AssertFloat(capped.AchievedFraction).IsEqualApprox(240f / 450f, 0.001f);
            capped.Restore();
        } finally {
            host.Free();
        }
    }
}
