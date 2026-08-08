using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 6 A4 — pins the pure-C# kit shapes that
/// <see cref="RollbackReadinessTests"/> drives to the authored character
/// resources.
///
/// The rollback gate itself deliberately avoids loading Godot resources so it can
/// run engine-free in the .NET test host. That leaves one honesty risk: its
/// per-character loadouts could silently stop resembling the real kits. This
/// suite is the guard — it is the only Godot-runtime part of A4, and it lives in
/// its own file because GdUnit4 does not execute a plain C# suite that shares a
/// source file with a <c>[RequireGodotRuntime]</c> suite.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RollbackReadinessKitShapeTests {

    [TestCase]
    public void HarnessKitShapesMatchTheAuthoredCharacterResources() {
        (string ID, FighterCharacterID Slot)[] roster = {
            ("einstein", FighterCharacterID.Einstein),
            ("joan", FighterCharacterID.Joan),
            ("leonardo", FighterCharacterID.Leonardo),
            ("lincoln", FighterCharacterID.Lincoln),
            ("cleopatra", FighterCharacterID.Cleopatra),
            ("tesla", FighterCharacterID.Tesla),
            ("shakespeare", FighterCharacterID.Shakespeare),
            ("mozart", FighterCharacterID.Mozart),
            ("pocahontas", FighterCharacterID.Pocahontas)
        };

        foreach ((string id, FighterCharacterID slot) in roster) {
            var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            AssertThat(data).IsNotNull();
            FighterAbilityLoadout harness = RollbackHarnessKits.KitShape(slot).AbilityModes;

            AssertThat(harness.SpecialOneExecutionType)
                .OverrideFailureMessage($"{id}: harness Special 1 execution shape drifted from the authored kit.")
                .IsEqual((int)data.SpecialAttackOne.ExecutionType);
            AssertThat(harness.SpecialTwoExecutionType)
                .OverrideFailureMessage($"{id}: harness Special 2 execution shape drifted from the authored kit.")
                .IsEqual((int)data.SpecialAttackTwo.ExecutionType);
            AssertThat(harness.MovementType)
                .OverrideFailureMessage($"{id}: harness movement type drifted from the authored kit.")
                .IsEqual((int)data.MovementAbility.MovementType);
            AssertThat(harness.SpecialOnePersistentTypeID)
                .OverrideFailureMessage($"{id}: harness Special 1 construct drifted from the authored kit.")
                .IsEqual(FighterLoadoutFactory.PersistentObjectTypeID(data.SpecialAttackOne.PersistentObjectID));
            AssertThat(harness.SpecialTwoPersistentTypeID)
                .OverrideFailureMessage($"{id}: harness Special 2 construct drifted from the authored kit.")
                .IsEqual(FighterLoadoutFactory.PersistentObjectTypeID(data.SpecialAttackTwo.PersistentObjectID));
            AssertThat(harness.MovementPersistentTypeID)
                .OverrideFailureMessage($"{id}: harness movement construct drifted from the authored kit.")
                .IsEqual(FighterLoadoutFactory.PersistentObjectTypeID(data.MovementAbility.PersistentObjectID));
        }
    }
}
