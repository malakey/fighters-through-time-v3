using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 13 W7a — canonical Einstein content contracts against
/// <c>docs/design-contracts/ABILITY_DATA.md</c>: E=mc² (E05) as a straight
/// 12-unit/s two-stage shot with no range limit (7 contact + 20 burst in 1.2
/// units, startup 14 / recovery 20), Relativity Rift (E03) with its 0.5 s Time
/// Dilation linger, and Relativity Warp as the E04 spacetime fold (10-frame
/// startup, up to 4 units). Where the sim shares a number through
/// <see cref="KitMotionRules"/>, the resource is pinned equal to it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EinsteinContentTests {

    [TestCase]
    public void MassEnergyConversionIsAStraightTwoStageShotWithNoRangeLimit() {
        var data = AuthoredResources.Load<AbilityData>("res://resources/Abilities/einstein/special_1.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Projectile);
        AssertThat(data.ProjectileSpeed).IsEqual(720f);
        AssertThat(data.ProjectileContactDamage).IsEqual(7f);
        AssertThat(data.BaseDamage).IsEqual(20f);
        AssertThat(data.ProjectileBurstRadius).IsEqual(72f);
        AssertThat(data.ProjectileBurstsOnTerrain).IsTrue();
        AssertThat(data.StartupFrames).IsEqual(14);
        AssertThat(data.RecoveryFrames).IsEqual(20);
        AssertThat(data.CooldownDuration).IsEqual(11f);
        // "No range limit": the flight cap outlasts any wall-to-wall crossing
        // (the widest arena is 20 units) at 12 units/s.
        AssertThat(data.ProjectileSpeed * data.ProjectileLifetime > 20f * KitMotionRules.StoryPixelsPerUnit).IsTrue();
    }

    [TestCase]
    public void RelativityRiftLingersHalfASecondAndIsThrownFiveUnits() {
        var data = AuthoredResources.Load<AbilityData>("res://resources/Abilities/einstein/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Area);
        AssertThat(data.AppliedStatus).IsEqual(StatusType.TimeDilation);
        AssertThat(Mathf.RoundToInt(data.StatusDuration * 60f)).IsEqual(KitMotionRules.RelativityRiftLingerFrames);
        AssertThat(data.Lifetime).IsEqual(3f);
        AssertThat(data.BaseDamage).IsEqual(3f);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(30);
        AssertThat(KitMotionRules.RelativityRiftThrowUnits).IsEqual(5f);
        AssertThat(KitMotionRules.RelativityRiftRadiusUnits).IsEqual(1.5f);
        AssertThat(KitMotionRules.RiftCollapsePullFrames).IsEqual(6);
    }

    [TestCase]
    public void RelativityWarpIsATenFrameFourUnitFold() {
        var data = ResourceLoader.Load<MovementAbilityData>("res://resources/Abilities/einstein/movement.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.MovementType).IsEqual(MovementType.Warp);
        AssertThat(data.StartupFrames).IsEqual(KitMotionRules.RelativityWarpStartupFrames);
        AssertThat(data.DistanceMoved)
            .IsEqual(KitMotionRules.RelativityWarpDistanceUnits * KitMotionRules.StoryPixelsPerUnit);
        AssertThat(data.CooldownDuration).IsEqual(5f);
    }
}
