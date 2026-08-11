using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
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

    private static readonly Dictionary<string, string[]> AbilityVfxAnimations = new() {
        ["einstein"] = new[] { "mass_energy_projectile", "relativity_rift", "relativity_warp", "cosmological_constant" },
        ["joan"] = new[] { "joan_righteous_smite", "joan_divine_piercing", "joan_ascendant_wings", "joan_grand_crusade" },
        ["leonardo"] = new[] { "leonardo_golden_ratio", "leonardo_clockwork_turret", "leonardo_ornithopter_flight", "leonardo_vitruvian_matrix" },
        ["lincoln"] = new[] { "lincoln_emancipator", "lincoln_splitting_strike", "lincoln_rail_charge", "lincoln_union_indestructible" },
        ["cleopatra"] = new[] { "cleopatra_serpent_nest", "cleopatra_sandstorm_vortex", "cleopatra_desert_mirage", "cleopatra_wrath_of_the_nile" },
        ["tesla"] = new[] { "tesla_tesla_coil", "tesla_lorentz_pulse", "tesla_lightning_blink", "tesla_wardenclyffe_cataclysm" },
        ["shakespeare"] = new[] { "shakespeare_yoricks_lament", "shakespeare_the_tempest", "shakespeare_prosperos_flight", "shakespeare_all_the_worlds_a_stage" },
        ["mozart"] = new[] { "mozart_requiem_chord", "mozart_fortissimo_wave", "mozart_sonata_drift", "mozart_symphony_of_sorrow" },
        ["pocahontas"] = new[] { "pocahontas_spirit_strike", "pocahontas_vine_snare", "pocahontas_breeze_glide", "pocahontas_tidewater_tempest" }
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
                AssertThat(frames.GetFrameCount(animationName))
                    .OverrideFailureMessage($"{characterID}:{animationName} is not a three-frame retro animation")
                    .IsEqual(3);
            }
            foreach (string animationName in RuntimeOnlyAnimationNames) {
                AssertThat(frames.HasAnimation(animationName)).IsTrue();
                AssertThat(frames.GetFrameCount(animationName))
                    .OverrideFailureMessage($"{characterID}:{animationName} is not a three-frame retro animation")
                    .IsEqual(3);
            }
        }
    }

    [TestCase]
    public void EveryCharacterHasFourThreeFrameAbilityVfxAnimations() {
        foreach (string characterID in InitialRoster) {
            string path = $"res://resources/SpriteFrames/{characterID}_ability_vfx_frames.tres";
            AssertThat(ResourceLoader.Exists(path))
                .OverrideFailureMessage($"{characterID} ability VFX resource is missing")
                .IsTrue();
            SpriteFrames frames = ResourceLoader.Load<SpriteFrames>(path);
            AssertObject(frames).IsNotNull();
            foreach (string animationName in AbilityVfxAnimations[characterID]) {
                AssertThat(frames.HasAnimation(animationName))
                    .OverrideFailureMessage($"{characterID} is missing VFX animation {animationName}")
                    .IsTrue();
                AssertThat(frames.GetFrameCount(animationName)).IsEqual(3);
                Texture2D frame = frames.GetFrameTexture(animationName, 0);
                AssertObject(frame).IsNotNull();
                AssertThat(frame.GetWidth()).IsEqual(192);
                AssertThat(frame.GetHeight()).IsEqual(192);
            }
        }
    }

    [TestCase]
    public void RuntimeAbilityVisualLibraryResolvesEveryCanonicalAbility() {
        foreach (string characterID in InitialRoster) {
            CharacterData character = AuthoredResources.Load<CharacterData>(
                $"res://resources/Characters/{characterID}_data.tres");
            foreach (AbilityData ability in new[] {
                         character.SpecialAttackOne, character.SpecialAttackTwo,
                         character.MovementAbility, character.UltimateAttack }) {
                AssertObject(ability).IsNotNull();
                AssertThat(AbilityVisualLibrary.TryResolve(ability.AbilityID,
                        out SpriteFrames frames, out StringName animation))
                    .OverrideFailureMessage($"Runtime visual lookup failed for {ability.AbilityID}")
                    .IsTrue();
                AssertObject(frames).IsNotNull();
                AssertThat(frames.HasAnimation(animation)).IsTrue();
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
            AssertThat(character.CharacterPortrait.ResourcePath).IsEqual(
                $"res://assets/sprites/characters/portraits/{characterID}_portrait.png");
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
        AssertThat(sprite.SpriteFrames.GetFrameCount("basic_attack_1")).IsEqual(3);
        AssertThat(sprite.SpriteFrames.GetFrameCount("basic_attack_2")).IsEqual(3);
        AssertThat(sprite.SpriteFrames.GetFrameCount("basic_attack_3")).IsEqual(3);
        AssertObject(player.GetNodeOrNull("PlaceholderBody")).IsNull();
        AssertObject(player.GetNodeOrNull("MeleeHitVisual")).IsNull();
        player.Free();
    }
}
