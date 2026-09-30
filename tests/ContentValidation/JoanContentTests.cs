using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Joan content contracts: authored resource numbers match the
/// design Section 5 kit and the 2026-09-29 character review (Package 13 W7b):
/// J02's short grounded shockwave, J01/J03's lunging Shield-Breaker flurry, and
/// A08's rising leap with the held Wing-Dive (no glide).
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
        // J02 (Package 13 W7b): a SHORT ground wave — about 2.5 units of travel
        // (speed x lifetime, 60 px per unit), 12/20 frames, 30 frames of
        // hitstun and no launch, so the Radiant Burn string can follow.
        AssertFloat(data.ProjectileSpeed * data.ProjectileLifetime).IsEqualApprox(150f, 0.01f);
        AssertThat(data.StartupFrames).IsEqual(12);
        AssertThat(data.RecoveryFrames).IsEqual(20);
        AssertThat(data.HitstunFrames).IsEqual(30);
        AssertThat(data.Launches).IsFalse();
        AssertThat(Mathf.Abs(data.KnockbackForce.X) <= 3f).IsTrue();
    }

    [TestCase]
    public void DivinePiercingResourceTotalsTwentyFourDamageAcrossItsThrusts() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/joan/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Melee);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.BaseDamage * data.HitCount).IsEqual(24f);
        // J03 (Package 13 W7b): three thrusts x 8, still a Shield-Breaker, no
        // longer poke-speed — 11 s cooldown, 13-frame startup, 20 recovery.
        AssertThat(data.HitCount).IsEqual(3);
        AssertThat(data.BaseDamage).IsEqual(8f);
        AssertThat(data.BlockClass).IsEqual(BlockClass.ShieldBreaker);
        AssertThat(data.CooldownDuration).IsEqual(11f);
        AssertThat(data.StartupFrames).IsEqual(13);
        AssertThat(data.RecoveryFrames).IsEqual(20);
    }

    [TestCase]
    public void AscendantWingsResourceIsARisingLeapWithAOneSecondWingDive() {
        var data = ResourceLoader.Load<MovementAbilityData>("res://resources/Abilities/joan/movement.tres");
        AssertObject(data).IsNotNull();
        // A08 (Package 13 W7b): the 3 s glide is retired; the held Wing-Dive
        // lasts up to 1 s.
        AssertThat(data.MovementType).IsEqual(MovementType.WingDive);
        AssertThat(data.MovementDuration).IsEqual(1f);
        AssertThat(data.CooldownDuration).IsEqual(5f);
        AssertThat(data.MovementSpeed > 0f).IsTrue();
    }
}
