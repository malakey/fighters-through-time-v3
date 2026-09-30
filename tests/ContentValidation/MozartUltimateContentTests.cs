using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Symphony of Sorrow content contract: the authored ultimate
/// resource structures the piano-key meteor bombardment — per-hit damage,
/// meteor count, cadence, active window, and the final-strike launch — and its
/// numbers stay aligned with the deterministic Fighter constants (zone
/// lifetime 180 frames, 18-frame meteor cadence, 10 x 8 total).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MozartUltimateContentTests {

    [TestCase]
    public void SymphonyOfSorrowResourceAuthorsTheMeteorBombardment() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/mozart/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.AbilityID).IsEqual("mozart_symphony_of_sorrow");
        AssertThat(data.CharacterID).IsEqual("mozart");
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Cinematic);

        // M05 (Package 13 W6): 8 keys x 7 at an 18-frame cadence, then the
        // 24-damage grand-chord finale (D15) = 80.
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(8);
        AssertThat(data.BaseDamage).IsEqual(7f);
        AssertThat(data.FinaleDamage).IsEqual(24f);
        AssertThat(data.UltimateImpactTotal).IsEqual(80f);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(18);

        // The final strike's launch; earlier meteors pin impulse-free.
        AssertThat(data.KnockbackForce).IsEqual(new Vector2(5f, -4f));
    }

    [TestCase]
    public void SymphonyOfSorrowWindowFitsTheKeysAndTheFinale() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/mozart/ultimate.tres");
        AssertObject(data).IsNotNull();

        // The hover/bombardment window: 2.7 s (162 frames) active phase matching
        // the authored Lifetime, exactly enough for 9 strikes 18 frames apart.
        AssertThat(data.ActiveFrames).IsEqual(162);
        AssertThat(data.Lifetime).IsEqual(2.7f);
        AssertThat(data.CinematicHitCount * data.DamageTickIntervalFrames <= data.ActiveFrames).IsTrue();

        // An ultimate consumes the full meter instead of running a cooldown,
        // and the meteors are strikes, not traveling projectiles.
        AssertThat(data.CooldownDuration).IsEqual(0f);
        AssertThat(data.ProjectileLifetime).IsEqual(0f);
        AssertThat(data.ProjectileSpeed).IsEqual(0f);
    }
}
