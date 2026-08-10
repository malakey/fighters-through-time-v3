using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

[TestSuite]
[RequireGodotRuntime]
public class CharacterPresentationTests {
    private static readonly string[] InitialRoster = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "pocahontas"
    };

    // Runtime states PlayerController plays that the presentation contract does
    // not list yet; the per-character placeholder frames must still carry them.
    // ("dash" left this list with the universal dash's removal, 2026-08-09; the
    // placeholder frame sets may keep the orphaned animation harmlessly.)
    private static readonly string[] RuntimeOnlyAnimationNames = {
        "roll_startup", "roll", "roll_recovery"
    };

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [TestCase]
    public void EveryCharacterHasSpriteFramesWithAllContractAnimations() {
        var contract = FTT.Core.AuthoredResources.Load<ContentSceneContract>(
            "res://resources/Contracts/player_presentation_contract.tres");
        AssertObject(contract).IsNotNull();
        AssertThat(contract.RequiredAnimationNames.Length > 0).IsTrue();

        foreach (string characterID in InitialRoster) {
            string path = $"res://resources/SpriteFrames/{characterID}_frames.tres";
            AssertThat(ResourceLoader.Exists(path)).IsTrue();
            SpriteFrames frames = ResourceLoader.Load<SpriteFrames>(path);
            AssertObject(frames).IsNotNull();

            foreach (string animationName in contract.RequiredAnimationNames) {
                AssertThat(frames.HasAnimation(animationName)).IsTrue();
            }
            foreach (string animationName in RuntimeOnlyAnimationNames) {
                AssertThat(frames.HasAnimation(animationName)).IsTrue();
            }
        }
    }

    [TestCase]
    public void EveryCharacterDataReferencesItsOwnFramesAndPortrait() {
        var framePaths = new HashSet<string>();
        var portraitPaths = new HashSet<string>();

        foreach (string characterID in InitialRoster) {
            CharacterData character = FTT.Core.AuthoredResources.Load<CharacterData>(
                $"res://resources/Characters/{characterID}_data.tres");
            AssertObject(character).IsNotNull();

            AssertObject(character.SpriteFramesResource).IsNotNull();
            AssertThat(character.SpriteFramesResource.ResourcePath
                .Contains($"{characterID}_frames")).IsTrue();
            AssertThat(framePaths.Add(character.SpriteFramesResource.ResourcePath)).IsTrue();

            AssertObject(character.CharacterPortrait).IsNotNull();
            AssertThat(character.CharacterPortrait.ResourcePath
                .Contains($"{characterID}_portrait")).IsTrue();
            AssertThat(portraitPaths.Add(character.CharacterPortrait.ResourcePath)).IsTrue();
        }

        AssertThat(framePaths.Count).IsEqual(9);
        AssertThat(portraitPaths.Count).IsEqual(9);
    }

    [TestCase]
    public void ManifestPointsAtNineDistinctPerCharacterVisualSets() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        var visualPaths = new HashSet<string>();

        foreach (string characterID in InitialRoster) {
            string expectedID = $"visual_character_{characterID}";
            ContentManifestEntry match = null;
            foreach (ContentManifestEntry entry in manifest.ForCategory(ContentCategory.VisualSet)) {
                if (entry.ContentID == expectedID) { match = entry; break; }
            }
            AssertObject(match).IsNotNull();
            AssertThat(match.ResourcePath).IsEqual(
                $"res://resources/SpriteFrames/{characterID}_frames.tres");
            AssertThat(ResourceLoader.Exists(match.ResourcePath)).IsTrue();
            AssertThat(match.AssetStatus == ContentAssetStatus.Placeholder).IsTrue();
            AssertThat(match.ValidationState == ContentValidationState.Valid).IsTrue();
            AssertThat(visualPaths.Add(match.ResourcePath)).IsTrue();
        }

        AssertThat(visualPaths.Count).IsEqual(9);
    }

    [TestCase]
    public void FactoryBuiltPlayerUsesPerCharacterAnimatedSprite() {
        PlayerController player = CharacterFactory.CreateCharacter("einstein", applyStoryProgression: false);
        var sprite = player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        AssertObject(sprite).IsNotNull();
        AssertObject(sprite.SpriteFrames).IsNotNull();
        AssertThat(sprite.SpriteFrames.ResourcePath
            .Contains("einstein_frames")).IsTrue();
        AssertThat(sprite.SpriteFrames.HasAnimation("idle")).IsTrue();
        AssertObject(player.GetNodeOrNull("PlaceholderBody")).IsNull();
        player.Free();
    }
}
