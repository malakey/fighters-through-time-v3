using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 B6: every one of the 36 canonical ability resources must name a cast
/// and an impact effect, and both must resolve to a scene that exists.
///
/// <para><c>CastVFXScene</c> / <c>ImpactVFXScene</c> were exported on
/// <c>AbilityData</c> for the entire life of the project with no value assigned
/// anywhere, so A3's consumer in <c>BaseSpecial</c> was provably dead code. An
/// unassigned field fails silently at runtime — no error, just no effect — which is
/// exactly the kind of gap a content sweep exists to catch.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AbilityVfxAssignmentTests {

    /// <summary>
    /// Package 11 A6b: the roster is the content manifest, never a literal
    /// cast list. design-godot.md §2 forbids enumerating the cast in
    /// load-bearing ways, and a duplicated array here is exactly the thing
    /// that blocks a roster addition — the content would be complete and the
    /// test suite would still fail.
    /// </summary>
    private static readonly IReadOnlyList<string> Characters = FTT.Core.CharacterRoster.IDs;

    private static readonly string[] Slots = { "special_1", "special_2", "movement", "ultimate" };

    private static IEnumerable<(string Path, AbilityData Data)> AllAbilities() {
        foreach (string character in Characters) {
            foreach (string slot in Slots) {
                string path = $"res://resources/Abilities/{character}/{slot}.tres";
                yield return (path, AuthoredResources.Load<AbilityData>(path));
            }
        }
    }

    [TestCase]
    public void EverySlotAssignsBothVfxFieldsToAnExistingScene() {
        var issues = new List<string>();
        int inspected = 0;

        foreach ((string path, AbilityData data) in AllAbilities()) {
            inspected++;
            if (data == null) { issues.Add($"{path}: failed to load"); continue; }
            if (data.CastVFXScene == null) issues.Add($"{path}: CastVFXScene unassigned");
            else if (!ResourceLoader.Exists(data.CastVFXScene.ResourcePath)) {
                issues.Add($"{path}: CastVFXScene -> missing {data.CastVFXScene.ResourcePath}");
            }
            if (data.ImpactVFXScene == null) issues.Add($"{path}: ImpactVFXScene unassigned");
            else if (!ResourceLoader.Exists(data.ImpactVFXScene.ResourcePath)) {
                issues.Add($"{path}: ImpactVFXScene -> missing {data.ImpactVFXScene.ResourcePath}");
            }
        }

        AssertThat(inspected).IsEqual(36);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryAssignedSceneComesFromTheSharedTaxonomy() {
        var authored = new HashSet<string>();
        foreach (string path in VfxLibrary.AllPaths.Values) authored.Add(path);

        var issues = new List<string>();
        foreach ((string path, AbilityData data) in AllAbilities()) {
            if (data?.CastVFXScene != null && !authored.Contains(data.CastVFXScene.ResourcePath)) {
                issues.Add($"{path}: cast effect {data.CastVFXScene.ResourcePath} is outside scenes/vfx/");
            }
            if (data?.ImpactVFXScene != null && !authored.Contains(data.ImpactVFXScene.ResourcePath)) {
                issues.Add($"{path}: impact effect {data.ImpactVFXScene.ResourcePath} is outside scenes/vfx/");
            }
        }
        // 36 bespoke effects would be 36 placeholder scenes to throw away at P10.
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheAssignmentExercisesEveryFamilyRatherThanCollapsingToOne() {
        var used = new HashSet<string>();
        foreach ((string _, AbilityData data) in AllAbilities()) {
            if (data?.CastVFXScene != null) used.Add(data.CastVFXScene.ResourcePath);
            if (data?.ImpactVFXScene != null) used.Add(data.ImpactVFXScene.ResourcePath);
        }
        // A mapping that routed everything to one scene would satisfy the two tests
        // above and carry no information at all.
        AssertThat(used.Count).IsEqual(VfxLibrary.AllPaths.Count);
    }

    [TestCase]
    public void CastEffectsAreTintedByTheOwningCharacterRatherThanSharedFlat() {
        var accents = new HashSet<Color>();
        foreach (string character in Characters) {
            accents.Add(VfxAccentPalette.ForCharacter(character));
        }
        AssertThat(accents.Count).IsEqual(Characters.Count);

        // The accent is derived from the canonical character colour, never re-authored.
        Color expected = CharacterFactory.GetCharacterColor("tesla")
            .Lerp(Colors.White, VfxAccentPalette.LiftTowardWhite);
        Color actual = VfxAccentPalette.ForCharacter("tesla");
        AssertFloat(actual.R).IsEqualApprox(expected.R, 0.0001);
        AssertFloat(actual.G).IsEqualApprox(expected.G, 0.0001);
        AssertFloat(actual.B).IsEqualApprox(expected.B, 0.0001);
        AssertFloat(actual.A).IsEqualApprox(VfxAccentPalette.CastAlpha, 0.0001);
    }

    [TestCase]
    public void AnAbilityWithNoCharacterStillResolvesAUsableAccent() {
        // Null-safety matters: BaseSpecial calls this on every cast.
        Color fallback = VfxAccentPalette.ForAbility(null);
        AssertFloat(fallback.A).IsGreater(0f);
        AssertThat(fallback.R + fallback.G + fallback.B > 0f).IsTrue();
    }
}
