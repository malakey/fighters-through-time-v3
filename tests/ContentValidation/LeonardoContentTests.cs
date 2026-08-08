using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Leonardo content contracts: authored resource numbers match the
/// design Section 4/5 Golden Ratio, Clockwork Turret, and Ornithopter Flight
/// specifications, and the authored turret construct scene satisfies the
/// Package 0 persistent-construct contract.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LeonardoContentTests {

    [TestCase]
    public void GoldenRatioResourceMatchesDesignSpecification() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/leonardo/special_1.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Area);
        AssertThat(data.BaseDamage).IsEqual(10f);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(3);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(30);
        AssertThat(data.Lifetime).IsEqual(1.5f);
    }

    [TestCase]
    public void ClockworkTurretResourceMatchesDesignSpecification() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/leonardo/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.PersistentObject);
        AssertThat(data.BaseDamage).IsEqual(5f);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(120);
        AssertThat(data.Lifetime).IsEqual(15f);
        AssertThat(data.MaxActiveObjects).IsEqual(1);
        AssertThat(data.PersistentObjectID).IsEqual("clockwork_turret");
        AssertObject(data.PersistentObjectScene).IsNotNull();
    }

    [TestCase]
    public void OrnithopterFlightResourceIsAThreeSecondGlide() {
        var data = ResourceLoader.Load<MovementAbilityData>("res://resources/Abilities/leonardo/movement.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.MovementType).IsEqual(MovementType.Glide);
        AssertThat(data.MovementDuration).IsEqual(3f);
        AssertThat(data.MovementSpeed).IsEqual(380f);
        AssertThat(data.CooldownDuration).IsEqual(5f);
    }

    [TestCase]
    public void ClockworkTurretSceneSatisfiesThePersistentConstructContract() {
        var scene = ResourceLoader.Load<PackedScene>("res://scenes/constructs/ClockworkTurret.tscn");
        AssertObject(scene).IsNotNull();
        Node root = AutoFree(scene.Instantiate());
        var contract = FTT.Core.AuthoredResources.Load<ContentSceneContract>(
            "res://resources/Contracts/persistent_construct_contract.tres");
        AssertObject(contract).IsNotNull();
        var issues = ContentSceneContractValidator.Validate(root, contract);
        AssertThat(issues.Count).IsEqual(0);
    }
}
