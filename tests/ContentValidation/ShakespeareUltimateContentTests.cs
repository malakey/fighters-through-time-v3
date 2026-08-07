using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Authored contract for Shakespeare's ultimate, All the World's a Stage: a
/// multi-hit sequence of six tragic-phantom strikes (the three Witches, Romeo
/// &amp; Juliet, Hamlet) whose per-strike damage, cadence, active window,
/// lifetime, finale knockback, and stage footprint drive both the Story ability
/// and the Fighter zone (type 63).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShakespeareUltimateContentTests {

    [TestCase]
    public void AllTheWorldsAStageAuthorsSixSequentialPhantomStrikes() {
        var data = ResourceLoader.Load<AbilityData>("res://resources/Abilities/shakespeare/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.HasValidIdentity()).IsTrue();
        AssertThat(data.Slot).IsEqual(AbilitySlot.Ultimate);

        // Six phantom strikes: the three Witches, Romeo & Juliet, and Hamlet.
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(6);
        // Per-strike damage; the sequence totals 6 x 14 = 84 in both modes.
        AssertThat(data.BaseDamage).IsEqual(14f);
        // The strike cadence: one phantom every 20 frames (1/3 s).
        AssertThat(data.DamageTickIntervalFrames).IsEqual(20);
    }

    [TestCase]
    public void AllTheWorldsAStageTimingSpansTheStrikeSequence() {
        var data = ResourceLoader.Load<AbilityData>("res://resources/Abilities/shakespeare/ultimate.tres");
        AssertObject(data).IsNotNull();

        // The Story active window holds exactly the authored sequence
        // (HitCount x cadence) and the zone Lifetime spans the same 2 seconds.
        AssertThat(data.ActiveFrames).IsEqual(data.HitCount * data.DamageTickIntervalFrames);
        AssertThat(data.ActiveFrames).IsEqual(120);
        AssertThat(data.Lifetime).IsEqual(2f);
        // Not a projectile: the stray authored projectile lifetime was cleared.
        AssertThat(data.ProjectileLifetime).IsEqual(0f);
        AssertThat(data.ProjectileSpeed).IsEqual(0f);
    }

    [TestCase]
    public void AllTheWorldsAStageAuthorsTheFinaleImpulseAndStageFootprint() {
        var data = ResourceLoader.Load<AbilityData>("res://resources/Abilities/shakespeare/ultimate.tres");
        AssertObject(data).IsNotNull();

        // Only the closing strike (Hamlet) carries this launch in both modes;
        // the Fighter sim reads its magnitude (5) as UltimateKnockback.
        AssertThat(data.KnockbackForce.X).IsEqual(5f);
        AssertThat(data.KnockbackForce.Y).IsEqual(-3f);

        // The Globe stage footprint: 720 x 240 px, mirrored by the Fighter
        // zone's owner-centered 6 x 2 unit half extents (60 px per unit).
        AssertThat(data.HitboxSize.X).IsEqual(720f);
        AssertThat(data.HitboxSize.Y).IsEqual(240f);
        AssertThat(data.HitboxOffset.X).IsEqual(0f);
        AssertThat(data.HitboxOffset.Y).IsEqual(0f);
    }
}
