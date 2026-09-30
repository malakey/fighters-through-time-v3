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
        // T02 (Package 13 W7a): coils arc within 4 units in both modes.
        AssertThat(FTT.Characters.Abilities.TeslaCoilNode.ArcRangePixels).IsEqual(240f);
        AssertThat(KitMotionRules.TeslaCoilArcRangeUnits).IsEqual(4f);
    }

    [TestCase]
    public void LorentzPulseResourceRootsForOneSecondWithOnePulseLifetime() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/tesla/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.AppliedStatus).IsEqual(StatusType.Root);
        // A05 (Package 13 W7a): Root 1.0 s; T02: 14 startup (the cane charge), 20 recovery.
        AssertThat(data.StatusDuration).IsEqual(1f);
        AssertThat(data.StartupFrames).IsEqual(14);
        AssertThat(data.RecoveryFrames).IsEqual(20);
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
        // Package 12 W4 (design §5, 2026-09-26): 3.0 units at 60 px/unit.
        AssertThat(data.DistanceMoved).IsEqual(180f);
        AssertThat(data.StartupFrames + data.ActiveFrames + data.RecoveryFrames <= 60).IsTrue();
        AssertThat(data.CooldownDuration).IsEqual(5f);
    }

    /// <summary>
    /// V7.6 F07: the finisher rider is a PAIR — the unchanged 0.4 s Static Charge
    /// interrupt plus a separate 1.5 s Conductive mark that enables Lorentz chains
    /// without locking actions. The two must never be collapsed back into one.
    /// </summary>
    [TestCase]
    public void TeslaFinisherRiderAuthorsBothTheInterruptAndTheConductiveMark() {
        BasicStringProfile tesla = BasicComboRules.StringProfileFor("tesla");
        AssertThat(tesla.FinisherStatusType).IsEqual((int)StatusType.StaticCharge);
        AssertThat(tesla.FinisherStatusFrames)
            .OverrideFailureMessage("Static Charge stays a pure 0.4 s interrupt.")
            .IsEqual(24);
        AssertThat(tesla.FinisherMarkType).IsEqual((int)ComboMarkType.Conductive);
        AssertThat(tesla.FinisherMarkFrames)
            .OverrideFailureMessage("The Conductive mark is 90 frames / 1.5 s at baseline.")
            .IsEqual(BasicComboRules.ConductiveMarkBaselineFrames);
        AssertThat(BasicComboRules.ConductiveMarkFenceFrames)
            .OverrideFailureMessage("A linked coil fence marks at the baseline and is never extended.")
            .IsEqual(BasicComboRules.ConductiveMarkBaselineFrames);
        AssertThat(BasicComboRules.ConductiveMarkUpgradedFrames)
            .OverrideFailureMessage("tesla_conductive_hold takes the mark to 2.5 s, Story only.")
            .IsEqual(150);
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
