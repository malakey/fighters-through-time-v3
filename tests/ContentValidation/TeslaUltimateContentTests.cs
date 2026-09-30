using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Content contracts for Tesla's authored Wardenclyffe Cataclysm ultimate
/// resource: the multi-hit column structure (per-hit damage, hit count, tick
/// cadence, and an active window that exactly covers the hits) plus the
/// ultimate-slot identity both modes key off.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TeslaUltimateContentTests {

    [TestCase]
    public void WardenclyffeCataclysmResourceAuthorsTheMultiHitColumn() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/tesla/ultimate.tres");
        AssertObject(data).IsNotNull();
        // T03 (Package 13 W6): the AC column's five 10-damage strikes, then
        // the 20-damage final strike (D15) = 70 before coil detonations.
        AssertThat(data.BaseDamage).IsEqual(10f);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(5);
        AssertThat(data.FinaleDamage).IsEqual(20f);
        AssertThat(data.UltimateImpactTotal).IsEqual(70f);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(30);
        // The active window spans exactly the strikes and the finale, and the
        // Lifetime mirrors it in seconds.
        AssertThat(data.ActiveFrames).IsEqual(data.CinematicHitCount * data.DamageTickIntervalFrames);
        AssertThat(data.Lifetime).IsEqual(3f);
    }

    [TestCase]
    public void WardenclyffeCataclysmResourceCarriesTheUltimateIdentity() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/tesla/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.HasValidIdentity()).IsTrue();
        AssertThat(data.AbilityID).IsEqual("tesla_wardenclyffe_cataclysm");
        AssertThat(data.CharacterID).IsEqual("tesla");
        AssertThat(data.Slot).IsEqual(FTT.Core.AbilitySlot.Ultimate);
        // Ultimates are meter-gated, never cooldown-gated.
        AssertThat(data.CooldownDuration).IsEqual(0f);
    }
}
