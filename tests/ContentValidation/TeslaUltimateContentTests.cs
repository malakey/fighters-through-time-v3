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
        AssertThat(data.BaseDamage).IsEqual(18f);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(4);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(30);
        // The active window spans exactly the authored hit cadence, and the
        // Lifetime mirrors it in seconds for the Fighter-side column zone.
        AssertThat(data.ActiveFrames).IsEqual(data.HitCount * data.DamageTickIntervalFrames);
        AssertThat(data.Lifetime).IsEqual(2f);
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
