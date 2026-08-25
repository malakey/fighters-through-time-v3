using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Cleopatra content contracts: authored resource numbers match the
/// design Section 4/5 specifications, and the authored Serpent Nest construct
/// scene satisfies the Package 0 persistent-construct contract.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CleopatraContentTests {

    [TestCase]
    public void SerpentNestResourceMatchesDesignSpecification() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/cleopatra/special_1.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.Lifetime).IsEqual(12f);
        AssertThat(data.MaxActiveObjects).IsEqual(1);
        AssertThat(data.PersistentObjectID).IsEqual("serpent_nest");
        AssertObject(data.PersistentObjectScene).IsNotNull();
        // V7 tuning batch: the documented 1 s bite cadence at a lighter 6-damage
        // bite, so standing on the nest is real area denial instead of one bite.
        AssertThat(data.DamageTickIntervalFrames).IsEqual(60);
        AssertThat(data.BaseDamage).IsEqual(6f);
        AssertThat(data.CooldownDuration).IsEqual(10f);
        // The bite carries Venom for 4 s alongside its brief hitstun; under the
        // V7 two-slot status rule the DoT survives later control statuses.
        AssertThat(data.AppliedStatus).IsEqual(StatusType.Venom);
        AssertThat(data.StatusDuration).IsEqual(4f);
    }

    [TestCase]
    public void SandstormVortexResourceMatchesDesignSpecification() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/cleopatra/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.BaseDamage).IsEqual(4f);
        AssertThat(data.HitCount).IsEqual(5);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(24);
        AssertThat(data.Lifetime).IsEqual(2f);
        AssertThat(data.AppliedStatus).IsEqual(StatusType.TimeDilation);
        AssertThat(data.StatusDuration).IsEqual(2f);
        // -40% speed: TimeDilation multiplies speed by (1 - 0.5 * intensity).
        AssertThat(data.StatusIntensity).IsEqual(0.8f);
    }

    [TestCase]
    public void DesertMirageResourceStaysWithinTheThreeSecondDesignCap() {
        var data = ResourceLoader.Load<MovementAbilityData>("res://resources/Abilities/cleopatra/movement.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.MovementType).IsEqual(MovementType.Teleport);
        AssertThat(data.MovementDuration <= 3f).IsTrue();
        AssertThat(data.DistanceMoved).IsEqual(180f);
        AssertThat(data.CooldownDuration).IsEqual(5f);
    }

    [TestCase]
    public void SerpentNestSceneSatisfiesThePersistentConstructContract() {
        var scene = ResourceLoader.Load<PackedScene>("res://scenes/constructs/SerpentNest.tscn");
        AssertObject(scene).IsNotNull();
        Node root = AutoFree(scene.Instantiate());
        var contract = FTT.Core.AuthoredResources.Load<ContentSceneContract>(
            "res://resources/Contracts/persistent_construct_contract.tres");
        AssertObject(contract).IsNotNull();
        var issues = ContentSceneContractValidator.Validate(root, contract);
        AssertThat(issues.Count).IsEqual(0);
    }
}
