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
        // V7 named-outlier correction (design §5): 8x3 = 24 total on a
        // shortened 8 s cooldown — a zoning tool, not the roster's biggest nuke.
        AssertThat(data.BaseDamage).IsEqual(8f);
        AssertThat(data.CooldownDuration).IsEqual(8f);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(3);
        // L02 (Package 13 W7a): 3 ticks 0.3 s apart, and a lifetime that fits
        // exactly those three (ticks at 0 / 18 / 36 frames, gone by 42).
        AssertThat(data.DamageTickIntervalFrames).IsEqual(18);
        AssertThat(data.Lifetime).IsEqual(0.7f);
        int lifetimeFrames = Mathf.RoundToInt(data.Lifetime * 60f);
        AssertThat(lifetimeFrames > 2 * data.DamageTickIntervalFrames).IsTrue();
        AssertThat(lifetimeFrames <= 3 * data.DamageTickIntervalFrames).IsTrue();
        AssertThat(data.StartupFrames).IsEqual(12);
        AssertThat(data.RecoveryFrames).IsEqual(18);
    }

    [TestCase]
    public void ClockworkTurretResourceMatchesDesignSpecification() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/leonardo/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.PersistentObject);
        // V7 tuning batch: 4 bolts of 6 every 2 s inside the 15 s life, so the
        // turret is an active threat from t=2 instead of firing at t=4/8/12.
        AssertThat(data.BaseDamage).IsEqual(6f);
        AssertThat(data.HitCount).IsEqual(4);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(120);
        AssertThat(data.Lifetime).IsEqual(15f);
        AssertThat(data.MaxActiveObjects).IsEqual(1);
        AssertThat(data.PersistentObjectID).IsEqual("clockwork_turret");
        AssertObject(data.PersistentObjectScene).IsNotNull();
        // L03 (Package 13 W7a): straight bolts at 12 units/s, pinned to the
        // rulebook the sim's bolt fallback reads.
        AssertThat(data.ProjectileSpeed)
            .IsEqual(KitMotionRules.TurretBoltSpeedUnits * KitMotionRules.StoryPixelsPerUnit);
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
