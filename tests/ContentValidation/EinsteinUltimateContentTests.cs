using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Einstein ultimate content contract: the authored Cosmological
/// Constant resource carries the multi-hit black-hole structure both modes
/// execute (per-hit damage, hit count, tick interval, lifetime, launch
/// knockback, and phase frames).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EinsteinUltimateContentTests {

    [TestCase]
    public void CosmologicalConstantResourceCarriesTheMultiHitStructure() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/einstein/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.Slot).IsEqual(AbilitySlot.Ultimate);
        // E06 (Package 13 W6): six 10-damage pull hits, then the 18-damage
        // launch finale (D15) = 78.
        AssertThat(data.BaseDamage).IsEqual(10f);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(6);
        AssertThat(data.FinaleDamage).IsEqual(18f);
        AssertThat(data.FinaleLaunches).IsTrue();
        AssertThat(data.UltimateImpactTotal).IsEqual(78f);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(18);
        AssertThat(data.Lifetime).IsEqual(2.1f);
        AssertThat(data.KnockbackForce).IsEqual(new Vector2(6f, -4f));
    }

    [TestCase]
    public void CosmologicalConstantPhaseFramesSpanTheSingularity() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/einstein/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.StartupFrames).IsEqual(30);
        // The active window is exactly the singularity's lifetime, and the
        // authored tick interval divides it into the six pull hits plus the
        // finale: 7 hits x 18 frames = 126 frames = 2.1 s.
        AssertThat(data.ActiveFrames).IsEqual(126);
        AssertThat(data.ActiveFrames).IsEqual(data.CinematicHitCount * data.DamageTickIntervalFrames);
        AssertThat(data.ActiveFrames).IsEqual(Mathf.RoundToInt(data.Lifetime * 60f));
        AssertThat(data.RecoveryFrames).IsEqual(30);
    }
}
