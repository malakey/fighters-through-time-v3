using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical content contract for Cleopatra's Wrath of the Nile ultimate:
/// the authored resource carries the multi-hit storm structure (10 ticks of 8
/// at a 21-frame interval across the 3.5 s active window) and the heavy Venom
/// debuff (5 s at 1.5 intensity) that both modes consume.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CleopatraUltimateContentTests {

    [TestCase]
    public void WrathOfTheNileResourceMatchesDesignSpecification() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/cleopatra/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.AbilityID).IsEqual("cleopatra_wrath_of_the_nile");
        AssertThat(data.CharacterID).IsEqual("cleopatra");
        AssertThat(data.Slot).IsEqual(AbilitySlot.Ultimate);

        // Multi-hit storm: 10 ticks of 8 per-hit damage (80 total) at a
        // 21-frame interval filling the 210-frame (3.5 s) active window.
        AssertThat(data.BaseDamage).IsEqual(8f);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(10);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(21);
        AssertThat(data.Lifetime).IsEqual(3.5f);
        AssertThat(data.ActiveFrames).IsEqual(210);
        // The tick schedule must fit the authored active window exactly.
        AssertThat(data.DamageTickIntervalFrames * data.HitCount).IsEqual(data.ActiveFrames);
        // Not a projectile: the storm is an area effect, so no stray projectile
        // lifetime may contradict the zone timing.
        AssertThat(data.ProjectileLifetime).IsEqual(0f);

        // Heavy Venom debuff: 5 s at 1.5 intensity (3 HP chip per second under
        // the shared 2 x intensity Venom tick in both modes).
        AssertThat(data.AppliedStatus).IsEqual(StatusType.Venom);
        AssertThat(data.StatusDuration).IsEqual(5f);
        AssertThat(data.StatusIntensity).IsEqual(1.5f);
    }
}
