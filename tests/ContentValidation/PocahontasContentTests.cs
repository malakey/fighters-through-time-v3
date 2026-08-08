using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Pocahontas content contracts: authored resource numbers match the
/// design Section 4/5 specification, and the authored Vine Snare construct
/// scene satisfies the Package 0 persistent-construct contract.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PocahontasContentTests {

    [TestCase]
    public void SpiritStrikeResourceMatchesDesignSpecification() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/pocahontas/special_1.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.BaseDamage).IsEqual(14f);
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Melee);
        AssertThat(data.HitstunDuration > 0f).IsTrue();
    }

    [TestCase]
    public void VineSnareResourceMatchesDesignSpecification() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/pocahontas/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.Lifetime).IsEqual(10f);
        AssertThat(data.MaxActiveObjects).IsEqual(2);
        AssertThat(data.PersistentObjectID).IsEqual("vine_snare");
        AssertThat(data.AppliedStatus).IsEqual(StatusType.Root);
        AssertThat(data.StatusDuration).IsEqual(1.5f);
        AssertThat(data.CooldownDuration).IsEqual(10f);
        AssertObject(data.PersistentObjectScene).IsNotNull();
    }

    [TestCase]
    public void BreezeGlideResourceGlidesThreeSecondsAndResetsDoubleJump() {
        var data = ResourceLoader.Load<MovementAbilityData>("res://resources/Abilities/pocahontas/movement.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.MovementType).IsEqual(MovementType.Glide);
        AssertThat(data.MovementDuration).IsEqual(3f);
        AssertThat(data.ResetsDoubleJump).IsTrue();
        AssertThat(data.CooldownDuration).IsEqual(5f);
    }

    [TestCase]
    public void VineSnareSceneSatisfiesThePersistentConstructContract() {
        var scene = ResourceLoader.Load<PackedScene>("res://scenes/constructs/VineSnare.tscn");
        AssertObject(scene).IsNotNull();
        Node root = AutoFree(scene.Instantiate());
        var contract = FTT.Core.AuthoredResources.Load<ContentSceneContract>(
            "res://resources/Contracts/persistent_construct_contract.tres");
        AssertObject(contract).IsNotNull();
        var issues = ContentSceneContractValidator.Validate(root, contract);
        AssertThat(issues.Count).IsEqual(0);
    }
}
