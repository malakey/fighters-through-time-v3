using System.Collections.Generic;
using System.IO;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

[TestSuite]
[RequireGodotRuntime]
public class CharacterManifestTests {
    /// <summary>
    /// Package 11 A6b: the roster is the content manifest, never a literal
    /// cast list. design-godot.md §2 forbids enumerating the cast in
    /// load-bearing ways, and a duplicated array here is exactly the thing
    /// that blocks a roster addition — the content would be complete and the
    /// test suite would still fail.
    /// </summary>
    private static readonly IReadOnlyList<string> InitialRoster = FTT.Core.CharacterRoster.IDs;

    /// <summary>
    /// Package 11 A6b: this used to assert the literal number 9 twice, which
    /// meant adding a tenth character failed a test that had nothing to say
    /// about the tenth character. What matters is that the roster is non-empty,
    /// that its IDs are unique and well-formed, and that the code's notion of
    /// the roster is exactly the manifest's — so that is what it pins now. The
    /// manifest's own row count is pinned in exactly one place
    /// (<c>ContentManifestTests</c>), so an accidental deletion still fails.
    /// </summary>
    [TestCase]
    public void RosterIDsAreUniqueWellFormedAndComeFromTheManifest() {
        AssertThat(InitialRoster.Count > 0).IsTrue();

        var ids = new HashSet<string>(InitialRoster);
        AssertThat(ids.Count).IsEqual(InitialRoster.Count);

        var issues = new List<string>();
        foreach (string id in InitialRoster) {
            if (string.IsNullOrWhiteSpace(id)) issues.Add("blank roster ID");
            else if (id != id.ToLowerInvariant()) issues.Add($"'{id}' is not lowercase");
            else if (id.Contains(' ')) issues.Add($"'{id}' contains a space");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

        // The code's roster IS the manifest's Character rows, in order.
        FTT.Core.ContentManifest manifest = FTT.Core.ContentManifest.LoadDefault();
        var manifestIDs = new List<string>();
        foreach (FTT.Core.ContentManifestEntry entry
            in manifest.ForCategory(FTT.Core.ContentCategory.Character)) {
            manifestIDs.Add(entry.ContentID);
        }
        AssertThat(string.Join(",", InitialRoster)).IsEqual(string.Join(",", manifestIDs));
    }

    [TestCase]
    public void EveryCharacterOwnsFourValidUniqueAbilityDefinitions() {
        var abilityIDs = new HashSet<string>();

        foreach (string characterID in InitialRoster) {
            CharacterData character = FTT.Core.AuthoredResources.Load<CharacterData>(
                $"res://resources/Characters/{characterID}_data.tres");
            AssertObject(character).IsNotNull();
            AssertThat(character.CharacterID == characterID).IsTrue();
            AssertThat(string.IsNullOrWhiteSpace(character.DisplayNameKey)).IsFalse();

            AbilityData[] kit = {
                character.SpecialAttackOne,
                character.SpecialAttackTwo,
                character.MovementAbility,
                character.UltimateAttack
            };
            for (int slot = 0; slot < kit.Length; slot++) {
                AbilityData ability = kit[slot];
                AssertObject(ability).IsNotNull();
                AssertThat(ability.CharacterID == characterID).IsTrue();
                AssertThat((int)ability.Slot).IsEqual(slot);
                AssertThat(ability.HasValidIdentity()).IsTrue();
                AssertThat(abilityIDs.Add(ability.AbilityID)).IsTrue();
            }

            AssertThat(character.MovementAbility is MovementAbilityData).IsTrue();
        }

        AssertThat(abilityIDs.Count).IsEqual(36);
    }

    [TestCase]
    public void EveryDefinitionTranslationKeyExistsInEnglishTable() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }

        foreach (string characterID in InitialRoster) {
            CharacterData character = FTT.Core.AuthoredResources.Load<CharacterData>(
                $"res://resources/Characters/{characterID}_data.tres");
            AssertThat(keys.Contains(character.DisplayNameKey)).IsTrue();
            foreach (AbilityData ability in new AbilityData[] {
                character.SpecialAttackOne, character.SpecialAttackTwo,
                character.MovementAbility, character.UltimateAttack
            }) {
                AssertThat(keys.Contains(ability.DisplayNameKey)).IsTrue();
                AssertThat(keys.Contains(ability.DescriptionKey)).IsTrue();
            }
        }
    }
}
