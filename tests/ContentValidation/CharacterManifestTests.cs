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
    private static readonly string[] InitialRoster = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "pocahontas"
    };

    [TestCase]
    public void InitialRosterHasNineUniqueStableIDs() {
        var ids = new HashSet<string>(InitialRoster);
        AssertThat(InitialRoster.Length).IsEqual(9);
        AssertThat(ids.Count).IsEqual(9);
    }

    [TestCase]
    public void EveryCharacterOwnsFourValidUniqueAbilityDefinitions() {
        var abilityIDs = new HashSet<string>();

        foreach (string characterID in InitialRoster) {
            CharacterData character = ResourceLoader.Load<CharacterData>(
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
            CharacterData character = ResourceLoader.Load<CharacterData>(
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
