using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Shakespeare content contracts: authored resource numbers match the
/// design Section 5 kit (Yorick's Lament 30% TimeDilation slow for 2.5 s on a
/// 7 s cooldown, bursting into a 1.5-unit wave since Package 13 W7a; the
/// zero-damage Tempest windbox; and Prospero's Flight as a single gust burst).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShakespeareContentTests {

    [TestCase]
    public void YoricksLamentResourceMatchesTheDesignNumbers() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/shakespeare/special_1.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Projectile);
        // V7: the design's cheap short-end poke whose payload is the slow —
        // base 16 on a 7 s cooldown, no longer the roster's biggest projectile.
        AssertThat(data.BaseDamage).IsEqual(16f);
        AssertThat(data.AppliedStatus).IsEqual(StatusType.TimeDilation);
        AssertThat(data.StatusDuration).IsEqual(2.5f);
        // The shared TimeDilation formula in both modes is
        // speed x (1 - 0.5 x intensity); the authored 0.6 intensity yields the
        // design's 30% movement/animation slow (0.5 x 0.6 = 0.3).
        AssertThat(data.StatusIntensity).IsEqual(0.6f);
        AssertThat(data.CooldownDuration).IsEqual(7f);
        // S03 (Package 13 W7a): thrown straight at 10 units/s up to 8 units,
        // bursting on a fighter, terrain or max range into a 1.5-unit wave —
        // 5 contact + 16 wave (BaseDamage is the wave).
        AssertThat(data.ProjectileSpeed).IsEqual(600f);
        AssertThat(data.ProjectileSpeed * data.ProjectileLifetime).IsEqual(480f);
        AssertThat(data.ProjectileBurstRadius).IsEqual(90f);
        AssertThat(data.ProjectileContactDamage).IsEqual(5f);
        AssertThat(data.ProjectileBurstsOnTerrain).IsTrue();
        AssertThat(data.StartupFrames).IsEqual(10);
        AssertThat(data.RecoveryFrames).IsEqual(14);
    }

    [TestCase]
    public void TempestResourceIsAZeroDamageOwnerCenteredWindbox() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/shakespeare/special_2.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Area);
        AssertThat(data.BaseDamage).IsEqual(0f);
        // V7: pure utility priced entirely in its cooldown (short end of the
        // band); the unused damage-tick field is zeroed so the resource stays
        // canonical.
        AssertThat(data.DamageTickIntervalFrames).IsEqual(0);
        AssertThat(data.CooldownDuration).IsEqual(7f);
        // S02 (Package 13 W7a): 8 startup, a 12-frame push window that is also
        // the storm's Fighter zone lifetime, 16 recovery.
        AssertThat(data.StartupFrames).IsEqual(8);
        AssertThat(data.ActiveFrames).IsEqual(KitMotionRules.TempestPushFrames);
        AssertThat(data.RecoveryFrames).IsEqual(16);
        AssertThat(Mathf.RoundToInt(data.Lifetime * 60f)).IsEqual(KitMotionRules.TempestPushFrames);
        AssertThat(KitMotionRules.TempestRadiusUnits).IsEqual(2f);
        AssertThat(KitMotionRules.TempestPushUnits).IsEqual(3f);
        AssertThat(KitMotionRules.TempestLiftUnits).IsEqual(2.5f);
    }

    [TestCase]
    public void ProsperosFlightResourceIsASingleGustBurst() {
        var data = ResourceLoader.Load<MovementAbilityData>("res://resources/Abilities/shakespeare/movement.tres");
        AssertObject(data).IsNotNull();
        // A08 (Package 13 W7a): ~4 units forward and 2.5 up over 20 frames, no glide.
        AssertThat(data.MovementType).IsEqual(MovementType.Gust);
        AssertThat(data.DistanceMoved)
            .IsEqual(KitMotionRules.ProsperoGustForwardUnits * KitMotionRules.StoryPixelsPerUnit);
        AssertThat(data.ActiveFrames).IsEqual(KitMotionRules.ProsperoGustFrames);
        AssertThat(Mathf.RoundToInt(data.MovementDuration * 60f)).IsEqual(KitMotionRules.ProsperoGustFrames);
        AssertThat(data.CooldownDuration).IsEqual(5f);
    }
}
