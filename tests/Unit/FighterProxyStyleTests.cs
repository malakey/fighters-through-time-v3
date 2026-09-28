using System.Collections.Generic;
using FTT.Combat;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B6: shape and pulse identity for the Fighter driver's 112 pooled
/// presentation proxies.
///
/// <para>The point of this suite is the determinism boundary, not the prettiness.
/// Every styling function is a pure function of a presentation frame counter and an
/// entity's type id, so nothing here can leak into <c>FighterSimulation</c>. The
/// existing hash and rollback-convergence suites are the real proof and were not
/// modified; these tests pin the properties that make that claim checkable —
/// determinism of the styling itself, and no dependency on any simulation value
/// beyond the type ids the driver already reads.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterProxyStyleTests {

    [TestCase]
    public void StylingIsAPureFunctionOfTheFrameAndTheTypeID() {
        for (int frame = 0; frame < 200; frame++) {
            AssertFloat(FighterProxyStyle.Pulse(frame, FighterProxyStyle.PulsePeriodFrames))
                .IsEqualApprox(FighterProxyStyle.Pulse(frame, FighterProxyStyle.PulsePeriodFrames), 0.000001);
            AssertThat(FighterProxyStyle.OrbColor(1, frame)).IsEqual(FighterProxyStyle.OrbColor(1, frame));
            AssertFloat(FighterProxyStyle.HazardRotation(4, frame))
                .IsEqualApprox(FighterProxyStyle.HazardRotation(4, frame), 0.000001);
        }
    }

    [TestCase]
    public void ThePulseIsBoundedAndPeriodic() {
        for (int frame = -50; frame < 250; frame++) {
            float value = FighterProxyStyle.Pulse(frame, FighterProxyStyle.PulsePeriodFrames);
            AssertThat(value >= 0f && value <= 1f).IsTrue();
            // A negative frame must not produce a negative phase.
            AssertFloat(value).IsEqualApprox(
                FighterProxyStyle.Pulse(frame + FighterProxyStyle.PulsePeriodFrames,
                    FighterProxyStyle.PulsePeriodFrames), 0.0001);
        }
        // A degenerate period must not divide by zero.
        AssertFloat(FighterProxyStyle.Pulse(7, 0)).IsEqualApprox(1f, 0.0001);
    }

    [TestCase]
    public void OrbColourIsTheSameCanonicalValueStoryModeUses() {
        var seen = new HashSet<string>();
        // Package 12 W5 (M24): the five simulation orb types map onto the shared
        // table explicitly — the old straight ordinal cast painted Chronal Haste in
        // the meter gold — and every type reads as its own hue, Resonance Surge in
        // the meter gold.
        for (int effect = 0; effect < 5; effect++) {
            Color story = ChronalOrbItem.EffectColor(FighterProxyStyle.PaletteEffectFor(effect));
            Color fighter = FighterProxyStyle.OrbColor(effect, 0);
            AssertFloat(fighter.R).IsEqualApprox(story.R, 0.0001);
            AssertFloat(fighter.G).IsEqualApprox(story.G, 0.0001);
            AssertFloat(fighter.B).IsEqualApprox(story.B, 0.0001);
            seen.Add($"{story.R},{story.G},{story.B}");
        }
        AssertThat(seen.Count).IsEqual(5);
        AssertThat(FighterProxyStyle.PaletteEffectFor(FighterOrbSystem.ResonanceSurgeEffectType))
            .IsEqual(OrbEffect.MeterBoost);
    }

    [TestCase]
    public void OrbsBreatheWithoutEverCollapsingOrExceedingTheirHitbox() {
        float min = float.MaxValue;
        float max = float.MinValue;
        for (int frame = 0; frame < FighterProxyStyle.PulsePeriodFrames * 3; frame++) {
            float scale = FighterProxyStyle.OrbScale(frame);
            min = Mathf.Min(min, scale);
            max = Mathf.Max(max, scale);
        }
        AssertThat(min > 0.85f).IsTrue();
        // The proxy must not grow so far past the simulated half-extents that it
        // misrepresents the pickup's reach.
        AssertThat(max < 1.1f).IsTrue();
        AssertThat(max > min).IsTrue();
    }

    [TestCase]
    public void AHazardWarningBlinksFasterAndDimmerThanItsActivePhase() {
        AssertThat(FighterProxyStyle.WarningPulsePeriodFrames <
                   FighterProxyStyle.PulsePeriodFrames).IsTrue();

        float min = float.MaxValue;
        float max = float.MinValue;
        for (int frame = 0; frame < FighterProxyStyle.WarningPulsePeriodFrames * 3; frame++) {
            float alpha = FighterProxyStyle.HazardWarningAlpha(frame);
            min = Mathf.Min(min, alpha);
            max = Mathf.Max(max, alpha);
        }
        AssertThat(min > 0f).IsTrue();
        // A warning must stay visibly weaker than an active hazard (0.72-0.82 alpha).
        AssertThat(max < 0.7f).IsTrue();
        AssertThat(max - min > 0.2f).IsTrue();
    }

    [TestCase]
    public void OnlyTheRotatingHazardIdentitiesSpin() {
        var spinning = new List<int>();
        for (int typeID = 0; typeID <= 10; typeID++) {
            bool spins = false;
            for (int frame = 0; frame < 200; frame++) {
                if (!Mathf.IsZeroApprox(FighterProxyStyle.HazardRotation(typeID, frame))) {
                    spins = true;
                    break;
                }
            }
            if (spins) spinning.Add(typeID);
        }
        AssertThat(string.Join(",", spinning)).IsEqual("4,7");
    }

    [TestCase]
    public void ZonesSwellSlowlyAndStayTranslucent() {
        AssertThat(FighterProxyStyle.ZonePulsePeriodFrames >
                   FighterProxyStyle.PulsePeriodFrames).IsTrue();
        for (int frame = 0; frame < FighterProxyStyle.ZonePulsePeriodFrames * 2; frame++) {
            float alpha = FighterProxyStyle.ZoneAlpha(frame);
            // A zone that reached opacity would hide the fighters standing in it.
            AssertThat(alpha > 0.2f && alpha < 0.45f).IsTrue();
        }
    }
}
