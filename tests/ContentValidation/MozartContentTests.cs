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
        // V7: the bread-and-butter poke — light per-pulse damage on a short
        // cooldown (design §5: fast/flat/cheap, opposite of Fortissimo's lob).
        AssertThat(data.BaseDamage).IsEqual(4f);
        AssertThat(data.CooldownDuration).IsEqual(7f);
        // M03 (Package 13 W7b): straight at 14 units/s (840 px/s) with no range
        // limit — the lifetime crosses any stage; a wall bursts it.
        AssertThat(data.ProjectileSpeed).IsEqual(840f);
        AssertThat(data.ProjectileSpeed * data.ProjectileLifetime > 20f * 60f).IsTrue();
        AssertThat(data.StartupFrames).IsEqual(10);
        AssertThat(data.RecoveryFrames).IsEqual(14);
    }

    [TestCase]
    public void FortissimoWaveResourceDealsTwentyFourWithHeavyFullScreenPushback() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/mozart/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Projectile);
        AssertThat(data.BaseDamage).IsEqual(24f);
        AssertThat(data.KnockbackForce.X).IsEqual(8f);
        // M01 (Package 13 W7b): the lob is retired — a slow wall of sound 1.5
        // units tall (90 px) that travels 4 units/s (240 px/s) along the ground
        // for 6 units, with committed haymaker frames and a top-band cooldown.
        AssertThat(data.ProjectileSpeed).IsEqual(240f);
        AssertThat(data.ProjectileLifetime).IsEqual(1.5f);
        AssertFloat(data.ProjectileSpeed * data.ProjectileLifetime).IsEqualApprox(360f, 0.01f);
        AssertThat(data.HitboxSize.Y).IsEqual(90f);
        AssertThat(data.CooldownDuration).IsEqual(12f);
        AssertThat(data.StartupFrames).IsEqual(16);
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
        // M04 (Package 13 W7b): the glissando rises about 3 units (180 px)
        // along the held direction over its active frames.
        AssertThat(data.DistanceMoved).IsEqual(180f);
        AssertFloat(data.MovementDuration * 60f).IsEqualApprox(data.ActiveFrames, 0.01f);
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
        // M04 (Package 13 W7b): the staff is 2.0 units (120 px) wide.
        AssertThat(((RectangleShape2D)bodyShape.Shape).Size.X).IsEqual(120f);
        AssertFloat(FTT.Characters.Abilities.SonataPlatformNode.StandHalfWidthPixels).IsEqualApprox(60f, 0.01f);
    }
}
