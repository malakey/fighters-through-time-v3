using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Union Indestructible content contracts: the authored
/// lincoln/ultimate.tres carries the multi-hit smash structure (per-hit
/// damage, HitCount, tick interval), the fence-pen trap window (active
/// frames, Lifetime, Root status), and the heavy fence-shatter finisher
/// knockback that both modes consume.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LincolnUltimateContentTests {

    [TestCase]
    public void UltimateResourceAuthorsTheMultiHitSmashSequence() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/lincoln/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.AbilityID).IsEqual("lincoln_union_indestructible");
        // Per-hit damage x hit count on a 0.5 s cadence: 5 smashes of 8.
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.BaseDamage).IsEqual(8f);
        AssertThat(data.HitCount).IsEqual(5);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(30);
        // The smash cadence exactly fills the active trap window.
        AssertThat(data.ActiveFrames).IsEqual(data.HitCount * data.DamageTickIntervalFrames);
    }

    [TestCase]
    public void UltimateResourceAuthorsTheFencePenTrapWindow() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/lincoln/ultimate.tres");
        AssertObject(data).IsNotNull();
        // Fighter zone lifetime matches the Story active window (2.5 s).
        AssertThat(data.Lifetime).IsEqual(2.5f);
        AssertThat(Mathf.RoundToInt(data.Lifetime * 60f)).IsEqual(data.ActiveFrames);
        // The pen holds via Root: each 0.5 s smash re-applies the 0.6 s Root so
        // the trap never lapses between hits.
        AssertThat(data.AppliedStatus).IsEqual(FTT.Core.StatusType.Root);
        AssertThat(data.StatusDuration).IsEqual(0.6f);
        AssertThat(data.StatusDuration * 60f > data.DamageTickIntervalFrames).IsTrue();
        // The pen is a wide forward fence line, not a point hit.
        AssertThat(data.HitboxSize.X > data.HitboxSize.Y).IsTrue();
        AssertThat(data.HitboxOffset.X > 0f).IsTrue();
    }

    [TestCase]
    public void UltimateResourceAuthorsTheHeavyFinisherKnockback() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/lincoln/ultimate.tres");
        AssertObject(data).IsNotNull();
        // Massive knockback: dominant horizontal shove with a real launch, far
        // heavier than the Emancipator special's (2, -6).
        AssertThat(data.KnockbackForce.X).IsEqual(12f);
        AssertThat(data.KnockbackForce.Y).IsEqual(-8f);
        AssertThat(data.HitstunDuration).IsEqual(0.5f);
        // Ultimates are meter-gated, not cooldown-gated.
        AssertThat(data.CooldownDuration).IsEqual(0f);
    }
}
