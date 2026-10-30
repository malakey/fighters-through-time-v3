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
    /// <summary>
    /// Package 11 A6b: the roster is the content manifest, never a literal
    /// cast list. design-godot.md §2 forbids enumerating the cast in
    /// load-bearing ways, and a duplicated array here is exactly the thing
    /// that blocks a roster addition — the content would be complete and the
    /// test suite would still fail.
    /// </summary>
    private static readonly IReadOnlyList<string> InitialRoster = FTT.Core.CharacterRoster.IDs;

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
        ["tubman"] = new[] { "tubman_conductors_call", "tubman_foresight", "tubman_north_star_leap", "tubman_freedom_line" }
    };

    /// <summary>
    /// 2026-10-18 user ruling "expand loops: run 6, idle 4; everything else 3",
    /// with the run raised to an 8-frame cycle on 2026-10-20 (the user found 6
    /// jerky and incomplete).
    /// </summary>
    /// <summary>
    /// 2026-10-28: the idle is a ping-pong bob. The image model draws N unique
    /// poses per character (tools/retro_sprites/roster_spec.py IDLE_POSES, the
    /// one table in the pipeline; mirrored here once), played 0..N-1 then N-2..1,
    /// so the idle has 2(N-1) frames at 10 fps.
    /// </summary>
    private static readonly Dictionary<string, int> IdlePoses = new() {
        ["joan"] = 5, ["cleopatra"] = 5, ["mozart"] = 5,
        ["einstein"] = 5, ["leonardo"] = 6, ["tesla"] = 5, ["shakespeare"] = 6, ["tubman"] = 6,
        ["lincoln"] = 7
    };

    /// <summary>The number of unique drawn idle poses for a roster character.</summary>
    internal static int IdlePoseCount(string characterID) => IdlePoses[characterID];

    private static int ExpectedFrameCount(string characterID, string animationName) => animationName switch {
        "idle" => 2 * (IdlePoses[characterID] - 1),
        "run" => 8,
        _ => 3
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
                    .OverrideFailureMessage($"{characterID}:{animationName} has the wrong retro frame count")
                    .IsEqual(ExpectedFrameCount(characterID, animationName));
            }
            foreach (string animationName in RuntimeOnlyAnimationNames) {
                AssertThat(frames.HasAnimation(animationName)).IsTrue();
                AssertThat(frames.GetFrameCount(animationName))
                    .OverrideFailureMessage($"{characterID}:{animationName} has the wrong retro frame count")
                    .IsEqual(ExpectedFrameCount(characterID, animationName));
            }
            // 2026-10-20 (user): the fall plays once and holds its pose until
            // landing; looping it pumped the legs. The idle and the run loop.
            AssertThat(frames.GetAnimationLoopMode("fall") == SpriteFrames.LoopMode.None)
                .OverrideFailureMessage($"{characterID}:fall must hold its last pose, not loop")
                .IsTrue();
            AssertThat(frames.GetAnimationLoopMode("idle") == SpriteFrames.LoopMode.Linear
                       && frames.GetAnimationLoopMode("run") == SpriteFrames.LoopMode.Linear)
                .OverrideFailureMessage($"{characterID}: idle and run must loop")
                .IsTrue();
        }
    }

    /// <summary>
    /// H03 (design-godot.md Section 9, 2026-09-26; Package 12 W10): the player
    /// presentation contract is exactly the 30-name list, and the retro set's old
    /// <c>down_attack</c> name is gone everywhere in favour of <c>down_air</c>.
    /// grab/throw ship as placeholder rows reusing combat-atlas regions.
    /// </summary>
    [TestCase]
    public void ThePresentationContractIsTheThirtyNameListAndDownAttackIsRetired() {
        string[] thirty = {
            "idle", "run", "jump", "fall", "skid", "crouch",
            "roll_startup", "roll", "roll_recovery", "ledge_hang", "ledge_pull_up", "ledge_drop",
            "basic_attack_1", "basic_attack_2", "basic_attack_3", "up_attack", "down_air", "grab", "throw",
            "block", "hitstun",
            "dazed", "death", "respawn", "victory", "defeat",
            "special_1", "special_2", "movement_ability", "ultimate"
        };
        var contract = FTT.Core.AuthoredResources.Load<ContentSceneContract>(
            "res://resources/Contracts/player_presentation_contract.tres");
        AssertObject(contract).IsNotNull();
        var required = new HashSet<string>(contract.RequiredAnimationNames);
        AssertThat(required.Count).IsEqual(30);
        AssertThat(contract.RequiredAnimationNames.Length).IsEqual(30);
        var missing = new List<string>();
        foreach (string name in thirty) {
            if (!required.Contains(name)) missing.Add(name);
        }
        if (missing.Count > 0) AssertThat("contract missing: " + string.Join(", ", missing)).IsEqual("");

        foreach (string characterID in InitialRoster) {
            SpriteFrames frames = ResourceLoader.Load<SpriteFrames>(
                $"res://resources/SpriteFrames/{characterID}_frames.tres");
            AssertThat(frames.HasAnimation("down_attack"))
                .OverrideFailureMessage($"{characterID} still carries the retired down_attack name")
                .IsFalse();
        }
        // The runtime normalizer carries a pose target for every contract name.
        foreach (string name in thirty) {
            AssertThat(RetroSpriteScaleNormalizer.CharacterPoseFraction(name) > 0f)
                .OverrideFailureMessage($"RetroSpriteScaleNormalizer has no pose row for {name}")
                .IsTrue();
        }
        AssertThat(RetroSpriteScaleNormalizer.CharacterPoseFraction("down_attack")).IsEqual(-1f);
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
                // 192 legacy pixels; retro v2 sheets draw the same cell at 2x texels.
                int expectedCell = Mathf.RoundToInt(192f * RetroSpriteMetadata.TexelScale(frames));
                AssertThat(frame.GetWidth()).IsEqual(expectedCell);
                AssertThat(frame.GetHeight()).IsEqual(expectedCell);
            }
        }
    }

    /// <summary>
    /// 2026-10-18 user ruling "2x plus wide attack cells": every roster sprite
    /// set and ability-VFX set is retro v2 art at 2x texels with its figure
    /// scale baked in, so a stale 96 x 128 set cannot slip back in through the
    /// builder's legacy path, and every animation carries its own feet line.
    /// </summary>
    [TestCase]
    public void EveryRosterSpriteSetIsRetroV2AtTwoTimesTexels() {
        var contract = FTT.Core.AuthoredResources.Load<ContentSceneContract>(
            "res://resources/Contracts/player_presentation_contract.tres");
        var problems = new List<string>();
        foreach (string characterID in InitialRoster) {
            SpriteFrames frames = ResourceLoader.Load<SpriteFrames>(
                $"res://resources/SpriteFrames/{characterID}_frames.tres");
            if (RetroSpriteMetadata.TexelScale(frames) != 2f) problems.Add($"{characterID}: texel scale");
            if (!RetroSpriteMetadata.IsBakedScale(frames)) problems.Add($"{characterID}: figure scale not baked");
            if (!frames.HasMeta(RetroSpriteMetadata.FeetGapsKey)) {
                problems.Add($"{characterID}: no feet lines");
            } else {
                using Godot.Collections.Dictionary gaps = frames.GetMeta(RetroSpriteMetadata.FeetGapsKey).AsGodotDictionary();
                foreach (string name in contract.RequiredAnimationNames) {
                    if (!gaps.ContainsKey(name)) problems.Add($"{characterID}: no feet line for {name}");
                }
            }
            SpriteFrames vfx = ResourceLoader.Load<SpriteFrames>(
                $"res://resources/SpriteFrames/{characterID}_ability_vfx_frames.tres");
            if (RetroSpriteMetadata.TexelScale(vfx) != 2f) problems.Add($"{characterID}: VFX texel scale");
        }
        if (problems.Count > 0) AssertThat("not retro v2: " + string.Join("; ", problems)).IsEqual("");
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

        // Package 11 A6b: every roster member has its OWN frames and portrait
        // — the contract is distinctness across the roster, not the number 9.
        AssertThat(framePaths.Count).IsEqual(InitialRoster.Count);
        AssertThat(portraitPaths.Count).IsEqual(InitialRoster.Count);
    }

    [TestCase]
    public void ManifestPointsAtOneDistinctVisualSetPerRosterCharacter() {
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

        AssertThat(visualPaths.Count).IsEqual(InitialRoster.Count);
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
        AssertThat(sprite.SpriteFrames.GetFrameCount("up_attack")).IsEqual(3);
        AssertThat(sprite.SpriteFrames.GetFrameCount("down_air")).IsEqual(3);
        AssertThat(sprite.SpriteFrames.GetFrameCount("grab")).IsEqual(3);
        AssertThat(sprite.SpriteFrames.GetFrameCount("throw")).IsEqual(3);
        AssertObject(player.GetNodeOrNull("PlaceholderBody")).IsNull();
        AssertObject(player.GetNodeOrNull("MeleeHitVisual")).IsNull();
        player.Free();
    }
}
