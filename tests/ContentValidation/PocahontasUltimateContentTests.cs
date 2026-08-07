using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Content contract for Pocahontas's canonical Tidewater Tempest ultimate: the
/// authored resource carries a coherent multi-hit storm structure (per-hit
/// damage, hit count, tick cadence, active window, lifetime) plus the
/// final-surge knockback, with no leftover projectile data.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PocahontasUltimateContentTests {

    [TestCase]
    public void TidewaterTempestResourceAuthorsACoherentMultiHitStorm() {
        var data = ResourceLoader.Load<AbilityData>("res://resources/Abilities/pocahontas/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.AbilityID).IsEqual("pocahontas_tidewater_tempest");
        AssertThat(data.Slot).IsEqual(AbilitySlot.Ultimate);

        // The storm is an 8 x 10 multi-hit at a 21-frame cadence.
        AssertThat(data.BaseDamage).IsEqual(10f);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(8);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(21);

        // The active window fits exactly HitCount ticks, and the zone lifetime
        // matches it (168 frames = 2.8 s at 60 Hz).
        AssertThat(data.ActiveFrames).IsEqual(data.HitCount * data.DamageTickIntervalFrames);
        AssertThat(Mathf.RoundToInt(data.Lifetime * 60f)).IsEqual(data.ActiveFrames);

        // The final surge throws enemies outward and upward.
        AssertThat(data.KnockbackForce).IsEqual(new Vector2(5f, -3f));
    }

    [TestCase]
    public void TidewaterTempestResourceCarriesNoDeadProjectileOrConstructData() {
        var data = ResourceLoader.Load<AbilityData>("res://resources/Abilities/pocahontas/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.ProjectileSpeed).IsEqual(0f);
        AssertThat(data.ProjectileLifetime).IsEqual(0f);
        AssertThat(data.PersistentObjectID).IsEqual("");
        AssertThat(data.MaxActiveObjects).IsEqual(0);

        // The storm footprint is owner-centered and mirrors the Fighter zone's
        // 8 x 5 world-unit extents (480 x 300 px at 60 px/unit).
        AssertThat(data.HitboxSize).IsEqual(new Vector2(480f, 300f));
        AssertThat(data.HitboxOffset).IsEqual(Vector2.Zero);
    }
}
