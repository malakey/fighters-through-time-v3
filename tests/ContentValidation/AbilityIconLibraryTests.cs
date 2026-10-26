using System.Collections.Generic;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// UR-ART: every roster character has all four 144 x 144 ability-slot icons of the pulp kit,
/// and the lookup answers <c>null</c> (never throws) for a character that has none.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AbilityIconLibraryTests {

    private static readonly AbilitySlot[] Slots = {
        AbilitySlot.Special1, AbilitySlot.Special2, AbilitySlot.MovementAbility, AbilitySlot.Ultimate
    };

    [TestCase]
    public void EveryRosterCharacterHasAllFourSlotIcons() {
        var issues = new List<string>();
        foreach (string id in CharacterRoster.IDs) {
            foreach (AbilitySlot slot in Slots) {
                Texture2D tex = AbilityIconLibrary.Load(id, slot);
                if (tex == null) {
                    issues.Add($"{id} {slot}: no icon at {AbilityIconLibrary.PathFor(id, slot)}");
                } else if (tex.GetWidth() != AbilityIconLibrary.IconSize || tex.GetHeight() != AbilityIconLibrary.IconSize) {
                    issues.Add($"{id} {slot}: {tex.GetWidth()}x{tex.GetHeight()}");
                }
            }
        }
        if (issues.Count > 0) {
            AssertThat(string.Join(" | ", issues)).IsEqual("");
        }
        AssertThat(CharacterRoster.IDs.Count > 0).IsTrue();
    }

    [TestCase]
    public void AnUnknownCharacterOrEmptyIdGivesNull() {
        AssertThat(AbilityIconLibrary.Load("nobody_at_all", AbilitySlot.Special1) == null).IsTrue();
        AssertThat(AbilityIconLibrary.Load("", AbilitySlot.Ultimate) == null).IsTrue();
        AssertThat(AbilityIconLibrary.Load(null, AbilitySlot.MovementAbility) == null).IsTrue();
    }

    [TestCase]
    public void SlotNamesMatchTheAbilityResourceFileNames() {
        AssertThat(AbilityIconLibrary.SlotName(AbilitySlot.Special1)).IsEqual("special_1");
        AssertThat(AbilityIconLibrary.SlotName(AbilitySlot.Special2)).IsEqual("special_2");
        AssertThat(AbilityIconLibrary.SlotName(AbilitySlot.MovementAbility)).IsEqual("movement");
        AssertThat(AbilityIconLibrary.SlotName(AbilitySlot.Ultimate)).IsEqual("ultimate");
    }
}
