using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Mozart content contracts: authored resource numbers match the
/// design Section 5 kit, and the authored Sonata staff-platform construct
/// scene satisfies the Package 0 persistent-construct contract with a
/// one-way-platform body.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MozartContentTests {

    [TestCase]
    public void RequiemChordResourceAuthorsTheMultiHitBurst() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/mozart/special_1.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Projectile);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(3);
        AssertThat(data.BaseDamage).IsEqual(8f);
        AssertThat(data.CooldownDuration).IsEqual(10f);
    }

    [TestCase]
    public void FortissimoWaveResourceDealsTwentyFourWithHeavyFullScreenPushback() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/mozart/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Projectile);
        AssertThat(data.BaseDamage).IsEqual(24f);
        AssertThat(data.KnockbackForce.X).IsEqual(8f);
        // Full-screen sweep: at the authored speed the wave must outlive a full
        // arena crossing (250 px/s x 6 s = 25 world units > the 24-unit arena).
        AssertThat(data.ProjectileLifetime).IsEqual(6f);
    }

    [TestCase]
    public void SonataDriftResourceDeploysTheThreeSecondStaffPlatform() {
        var data = ResourceLoader.Load<MovementAbilityData>("res://resources/Abilities/mozart/movement.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.PersistentObjectID).IsEqual("sonata_platform");
        AssertObject(data.PersistentObjectScene).IsNotNull();
        AssertThat(data.Lifetime).IsEqual(3f);
        AssertThat(data.MaxActiveObjects).IsEqual(1);
        AssertThat(data.CooldownDuration).IsEqual(5f);
    }

    [TestCase]
    public void SonataPlatformSceneSatisfiesThePersistentConstructContract() {
        var scene = ResourceLoader.Load<PackedScene>("res://scenes/constructs/SonataPlatform.tscn");
        AssertObject(scene).IsNotNull();
        Node root = AutoFree(scene.Instantiate());
        var contract = FTT.Core.AuthoredResources.Load<ContentSceneContract>(
            "res://resources/Contracts/persistent_construct_contract.tres");
        AssertObject(contract).IsNotNull();
        var issues = ContentSceneContractValidator.Validate(root, contract);
        AssertThat(issues.Count).IsEqual(0);

        // The staff body must be a genuine one-way platform: OneWayPlatform
        // layer plus one-way collision, so Mozart can land on and drop through it.
        var body = root.GetNodeOrNull<StaticBody2D>("Body");
        AssertObject(body).IsNotNull();
        AssertThat(body.CollisionLayer == CollisionLayers.OneWayPlatform).IsTrue();
        var bodyShape = root.GetNodeOrNull<CollisionShape2D>("Body/CollisionShape2D");
        AssertObject(bodyShape).IsNotNull();
        AssertThat(bodyShape.OneWayCollision).IsTrue();
    }
}
