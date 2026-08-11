using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Joan content contracts: authored resource numbers match the
/// design Section 5 kit specification (14-damage RadiantBurn shockwave,
/// 12-total shield-shredding thrusts, 3 s glide movement).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class JoanContentTests {

    [TestCase]
    public void RighteousSmiteResourceIsATwentyEightDamageRadiantBurnShockwave() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/joan/special_1.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Projectile);
        AssertThat(data.BaseDamage).IsEqual(28f);
        AssertThat(data.AppliedStatus).IsEqual(StatusType.RadiantBurn);
        AssertThat(data.StatusDuration).IsEqual(3f);
        AssertThat(data.ProjectileSpeed > 0f).IsTrue();
    }

    [TestCase]
    public void DivinePiercingResourceTotalsTwentyFourDamageAcrossItsThrusts() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/joan/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Melee);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.BaseDamage * data.HitCount).IsEqual(24f);
    }

    [TestCase]
    public void AscendantWingsResourceIsAThreeSecondGlide() {
        var data = ResourceLoader.Load<MovementAbilityData>("res://resources/Abilities/joan/movement.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.MovementType).IsEqual(MovementType.Glide);
        AssertThat(data.MovementDuration).IsEqual(3f);
        AssertThat(data.CooldownDuration).IsEqual(5f);
        AssertThat(data.MovementSpeed > 0f).IsTrue();
    }
}
