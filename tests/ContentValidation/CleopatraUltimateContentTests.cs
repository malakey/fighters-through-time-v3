using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical content contract for Cleopatra's Wrath of the Nile ultimate
/// (C04, Package 13 W6): six 9-damage cobra strikes at a 21-frame interval,
/// the 12-damage sarcophagus finale (D15), and the heavy Venom (3 s at 2.0
/// intensity = 12) that rides the finale in both modes — 78 in all.
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

        // Six cobras of 9 and the 12-damage slam finale (66 impacts) at a
        // 21-frame interval filling the 147-frame (2.45 s) active window.
        AssertThat(data.BaseDamage).IsEqual(9f);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(6);
        AssertThat(data.FinaleDamage).IsEqual(12f);
        AssertThat(data.FinaleLaunches).IsFalse();
        AssertThat(data.UltimateImpactTotal).IsEqual(66f);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(21);
        AssertThat(data.Lifetime).IsEqual(2.45f);
        AssertThat(data.ActiveFrames).IsEqual(147);
        // The tick schedule must fit the authored active window exactly.
        AssertThat(data.DamageTickIntervalFrames * data.CinematicHitCount).IsEqual(data.ActiveFrames);
        // Not a projectile: the storm is an area effect, so no stray projectile
        // lifetime may contradict the zone timing.
        AssertThat(data.ProjectileLifetime).IsEqual(0f);

        // Heavy Venom: 3 s at 2.0 intensity (4 HP per tick, three ticks = 12
        // under the shared 2 x intensity Venom tick), and only the finale
        // carries it.
        AssertThat(data.AppliedStatus).IsEqual(StatusType.Venom);
        AssertThat(data.StatusDuration).IsEqual(3f);
        AssertThat(data.StatusIntensity).IsEqual(2f);
        AssertThat(data.CinematicHitCarriesStatus(1)).IsFalse();
        AssertThat(data.CinematicHitCarriesStatus(data.CinematicHitCount)).IsTrue();
    }
}
