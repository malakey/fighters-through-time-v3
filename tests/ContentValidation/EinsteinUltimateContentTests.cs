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
        var data = ResourceLoader.Load<AbilityData>("res://resources/Abilities/einstein/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.Slot).IsEqual(AbilitySlot.Ultimate);
        AssertThat(data.BaseDamage).IsEqual(15f);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(5);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(18);
        AssertThat(data.Lifetime).IsEqual(1.5f);
        AssertThat(data.KnockbackForce).IsEqual(new Vector2(6f, -4f));
    }

    [TestCase]
    public void CosmologicalConstantPhaseFramesSpanTheSingularity() {
        var data = ResourceLoader.Load<AbilityData>("res://resources/Abilities/einstein/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.StartupFrames).IsEqual(30);
        // The active window is exactly the singularity's lifetime, and the
        // authored tick interval divides it into the authored hit count:
        // 5 hits x 18 frames = 90 frames = 1.5 s.
        AssertThat(data.ActiveFrames).IsEqual(90);
        AssertThat(data.ActiveFrames).IsEqual(data.HitCount * data.DamageTickIntervalFrames);
        AssertThat(data.ActiveFrames).IsEqual(Mathf.RoundToInt(data.Lifetime * 60f));
        AssertThat(data.RecoveryFrames).IsEqual(30);
    }
}
