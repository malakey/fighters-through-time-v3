using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 2026-08-11 sprite-size pass pins: the generated retro sheets draw the same
/// figure at wildly different scales between animations (an enemy's attack
/// rows at half the idle height), so RetroSpriteScaleNormalizer rescales the
/// sprite per animation from measured art heights, anchored feet-on-origin.
/// The character base scale is 0.845 (two +30% size increases over 0.5), and
/// enemy scenes were rebased to match.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RetroSpriteScaleNormalizerTests {
    private const int CellWidth = 96;
    private const int CellHeight = 128;
    private const int FeetGap = 3;

    private static ImageTexture FrameWithArtHeight(int artHeight) {
        Image image = Image.CreateEmpty(CellWidth, CellHeight, false, Image.Format.Rgba8);
        image.FillRect(
            new Rect2I(30, CellHeight - FeetGap - artHeight, 30, artHeight),
            Colors.White);
        return ImageTexture.CreateFromImage(image);
    }

    private static SpriteFrames BuildFrames(params (string Name, int ArtHeight)[] animations) {
        var frames = new SpriteFrames();
        foreach ((string name, int artHeight) in animations) {
            if (!frames.HasAnimation(name)) frames.AddAnimation(name);
            frames.AddFrame(name, FrameWithArtHeight(artHeight));
        }
        frames.RemoveAnimation("default");
        return frames;
    }

    private static (AnimatedSprite2D sprite, Node host) BuildSprite(
        SpriteFrames frames, RetroSpriteScaleNormalizer.FigureKind kind, float baseScale) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "NormalizerTestHost" };
        tree.Root.AddChild(host);
        var sprite = new AnimatedSprite2D {
            Name = "AnimatedSprite2D",
            SpriteFrames = frames,
            Animation = "idle",
            Scale = new Vector2(baseScale, baseScale),
            Position = new Vector2(0, -(CellHeight * 0.5f) * baseScale)
        };
        host.AddChild(sprite);
        RetroSpriteScaleNormalizer.Attach(sprite, kind);
        return (sprite, host);
    }

    [TestCase]
    public void IdleKeepsTheAuthoredBaseScaleWithFeetOnTheOrigin() {
        SpriteFrames frames = BuildFrames(("idle", 122), ("attack", 61));
        (AnimatedSprite2D sprite, Node host) = BuildSprite(
            frames, RetroSpriteScaleNormalizer.FigureKind.Enemy, 0.65f);
        try {
            AssertFloat(sprite.Scale.X).IsEqualApprox(0.65f, 0.001f);
            // Feet anchor: cell bottom minus the 3 px baseline sits at parent y=0.
            float expectedY = -(CellHeight * 0.5f - FeetGap) * 0.65f;
            AssertFloat(sprite.Position.Y).IsEqualApprox(expectedY, 0.01f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void AHalfScaleAttackAnimationIsBoostedTowardThePoseTarget() {
        SpriteFrames frames = BuildFrames(("idle", 122), ("attack", 61));
        (AnimatedSprite2D sprite, Node host) = BuildSprite(
            frames, RetroSpriteScaleNormalizer.FigureKind.Enemy, 0.65f);
        try {
            sprite.Animation = "attack";
            // Enemy attack pose target is 0.95 x idle: 0.95 * 122 / 61 = 1.9.
            float expectedFactor = 0.95f * 122f / 61f;
            AssertFloat(sprite.Scale.X).IsEqualApprox(0.65f * expectedFactor, 0.001f);
            // Feet stay anchored at the (scaled) cell baseline.
            float expectedY = -(CellHeight * 0.5f - FeetGap) * 0.65f * expectedFactor;
            AssertFloat(sprite.Position.Y).IsEqualApprox(expectedY, 0.01f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ExtremeCorrectionsClampAtMaxBoostAndNothingEverShrinks() {
        SpriteFrames frames = BuildFrames(("idle", 122), ("attack", 20), ("hitstun", 122));
        (AnimatedSprite2D sprite, Node host) = BuildSprite(
            frames, RetroSpriteScaleNormalizer.FigureKind.Enemy, 0.65f);
        try {
            sprite.Animation = "attack";
            AssertFloat(sprite.Scale.X).IsEqualApprox(
                0.65f * RetroSpriteScaleNormalizer.MaxBoost, 0.001f);
            // A full-height animation with a sub-1 pose target must not be
            // scaled down (0.9 * 122 / 122 clamps up to 1).
            sprite.Animation = "hitstun";
            AssertFloat(sprite.Scale.X).IsEqualApprox(0.65f, 0.001f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void FactoryBuiltCharacterCarriesTheEnlargedBaseScaleAndNormalizer() {
        PlayerController player = CharacterFactory.CreateCharacter("einstein", applyStoryProgression: false);
        try {
            var sprite = player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            AssertObject(sprite).IsNotNull();
            AssertFloat(sprite.Scale.X).IsEqualApprox(0.845f, 0.001f);
            AssertObject(sprite.GetNodeOrNull<RetroSpriteScaleNormalizer>("ScaleNormalizer"))
                .IsNotNull();
        } finally {
            player.Free();
        }
    }
}
