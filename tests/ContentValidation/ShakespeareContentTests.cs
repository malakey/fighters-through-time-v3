using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Shakespeare content contracts: authored resource numbers match the
/// design Section 5 kit (Yorick's Lament 30% TimeDilation slow for 2.5 s on a
/// flat 10 s cooldown, the zero-damage Tempest storm, and the 3 s Prospero's
/// Flight glide).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShakespeareContentTests {

    [TestCase]
    public void YoricksLamentResourceMatchesTheDesignNumbers() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/shakespeare/special_1.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Projectile);
        AssertThat(data.BaseDamage).IsEqual(28f);
        AssertThat(data.AppliedStatus).IsEqual(StatusType.TimeDilation);
        AssertThat(data.StatusDuration).IsEqual(2.5f);
        // The shared TimeDilation formula in both modes is
        // speed x (1 - 0.5 x intensity); the authored 0.6 intensity yields the
        // design's 30% movement/animation slow (0.5 x 0.6 = 0.3).
        AssertThat(data.StatusIntensity).IsEqual(0.6f);
        AssertThat(data.CooldownDuration).IsEqual(10f);
    }

    [TestCase]
    public void TempestResourceIsAZeroDamageOwnerCenteredStorm() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/shakespeare/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Area);
        AssertThat(data.BaseDamage).IsEqual(0f);
        AssertThat(data.Lifetime).IsEqual(1.2f);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(30);
        AssertThat(data.CooldownDuration).IsEqual(10f);
        // The authored active window matches the storm's Fighter zone lifetime.
        AssertThat(data.ActiveFrames).IsEqual(72);
    }

    [TestCase]
    public void ProsperosFlightResourceGlidesForThreeSeconds() {
        var data = ResourceLoader.Load<MovementAbilityData>("res://resources/Abilities/shakespeare/movement.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.MovementType).IsEqual(MovementType.Glide);
        AssertThat(data.MovementDuration).IsEqual(3f);
        AssertThat(data.CooldownDuration).IsEqual(5f);
    }
}
