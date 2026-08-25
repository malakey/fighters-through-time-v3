using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Tesla content contracts: authored resource numbers match the
/// design Section 4 coil specification, and the authored coil construct scene
/// satisfies the Package 0 persistent-construct contract.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TeslaContentTests {

    [TestCase]
    public void TeslaCoilResourceMatchesDesignSpecification() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/tesla/special_1.tres");
        AssertObject(data).IsNotNull();
        // V7 tuning batch: 5-damage arcs every 2 s so a lone coil is a real
        // threat (the doc's 0.5 s figure predates the fence rebalance).
        AssertThat(data.BaseDamage).IsEqual(5f);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(120);
        AssertThat(data.Lifetime).IsEqual(30f);
        AssertThat(data.MaxActiveObjects).IsEqual(2);
        AssertThat(data.PersistentObjectID).IsEqual("tesla_coil");
        AssertObject(data.PersistentObjectScene).IsNotNull();
    }

    [TestCase]
    public void LorentzPulseResourceRootsForTwoSecondsWithOnePulseLifetime() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/tesla/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.AppliedStatus).IsEqual(StatusType.Root);
        AssertThat(data.StatusDuration).IsEqual(2f);
        AssertThat(data.Lifetime).IsEqual(0.25f);
        // V7: the unearned (coil-less) cast is trimmed out of the top band; the
        // chain-lightning reward with coils primed makes up the difference.
        AssertThat(data.BaseDamage).IsEqual(20f);
        AssertThat(data.CooldownDuration).IsEqual(11f);
    }

    [TestCase]
    public void LightningBlinkResourceStaysWithinTheOneSecondDesignCap() {
        var data = ResourceLoader.Load<MovementAbilityData>("res://resources/Abilities/tesla/movement.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.MovementType).IsEqual(MovementType.Blink);
        AssertThat(data.MovementDuration <= 1f).IsTrue();
        AssertThat(data.DistanceMoved).IsEqual(160f);
        AssertThat(data.CooldownDuration).IsEqual(5f);
    }

    [TestCase]
    public void TeslaCoilSceneSatisfiesThePersistentConstructContract() {
        var scene = ResourceLoader.Load<PackedScene>("res://scenes/constructs/TeslaCoil.tscn");
        AssertObject(scene).IsNotNull();
        Node root = AutoFree(scene.Instantiate());
        var contract = FTT.Core.AuthoredResources.Load<ContentSceneContract>(
            "res://resources/Contracts/persistent_construct_contract.tres");
        AssertObject(contract).IsNotNull();
        var issues = ContentSceneContractValidator.Validate(root, contract);
        AssertThat(issues.Count).IsEqual(0);
    }
}
