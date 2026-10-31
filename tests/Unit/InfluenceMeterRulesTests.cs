using FTT.Combat;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The one Influence meter rulebook (<see cref="InfluenceMeterRules"/>) and the
/// Fighter simulation's fixed-point view of it (<see cref="FighterMeterRules"/>).
/// Before the rulebook the cap and the rates lived in three code copies — the
/// Story <c>UltimateMeter</c> node and literals across the sim, CPU and driver —
/// so a retune could move one mode and not the other.
///
/// <para>The sim view must reproduce the expressions the sim used to inline
/// <b>bit for bit</b> (<c>+ FromInt(damage)</c>, <c>/ FromInt(4)</c>,
/// <c>* FromInt(3) / FromInt(4)</c>), because a reordered fixed-point operation
/// moves every Fighter hash. The Story gain functions must reproduce the old
/// float expressions at a gain factor of 1. Pure C#: nothing here touches the
/// Godot runtime.</para>
/// </summary>
[TestSuite]
public class InfluenceMeterRulesTests {

    [TestCase]
    public void TheRulebookHoldsTheDesignedCapAndRatesAsIntegerRationals() {
        // Design §"Influence Meter": 0-100, 1 point per HP dealt, 0.25 per HP
        // taken, and a Fighter stock loss keeps 50% (1/2 since the 2026-10-31 meter and Defy update).
        AssertThat(InfluenceMeterRules.Cap).IsEqual(100);
        AssertThat(InfluenceMeterRules.DamageDealtNumerator).IsEqual(1);
        AssertThat(InfluenceMeterRules.DamageDealtDenominator).IsEqual(1);
        AssertThat(InfluenceMeterRules.DamageTakenNumerator).IsEqual(1);
        AssertThat(InfluenceMeterRules.DamageTakenDenominator).IsEqual(4);
        AssertThat(InfluenceMeterRules.StockLossRetentionNumerator).IsEqual(1);
        AssertThat(InfluenceMeterRules.StockLossRetentionDenominator).IsEqual(2);

        // The float views are exact (every value is a power-of-two fraction).
        AssertThat(InfluenceMeterRules.MaxValue).IsEqual(100f);
        AssertThat(InfluenceMeterRules.PointsPerDamageDealt).IsEqual(1f);
        AssertThat(InfluenceMeterRules.PointsPerDamageTaken).IsEqual(0.25f);
        AssertThat(InfluenceMeterRules.StockLossRetention).IsEqual(0.5f);

        // The Story node's names are forwarders, not a second copy.
        AssertThat(UltimateMeter.MaxValue).IsEqual(InfluenceMeterRules.MaxValue);
        AssertThat(UltimateMeter.PointsPerDamageDealt).IsEqual(InfluenceMeterRules.PointsPerDamageDealt);
        AssertThat(UltimateMeter.PointsPerDamageTaken).IsEqual(InfluenceMeterRules.PointsPerDamageTaken);
    }

    [TestCase]
    public void TheSimViewReproducesTheRetiredFixedPointExpressionsBitForBit() {
        AssertThat(FighterMeterRules.CapPoints).IsEqual(100);
        AssertThat(FighterMeterRules.Cap.RawValue).IsEqual(FP64.FromInt(100).RawValue);

        int mismatches = 0;
        string first = "";
        for (int damage = -5; damage <= 400; damage++) {
            long dealt = FighterMeterRules.FromDamageDealt(damage).RawValue;
            long dealtBefore = FP64.FromInt(damage).RawValue;
            if (dealt != dealtBefore && mismatches++ == 0) first = $"dealt {damage}: {dealt} != {dealtBefore}";
        }

        // Taken and retention run on arbitrary fixed-point inputs: whole damage
        // minus a fractional Rally echo, per-frame echo drains, and partial meters.
        FP64[] samples = {
            FP64.Zero, FP64.One, FP64.FromInt(3), FP64.FromInt(7), FP64.FromInt(37), FP64.FromInt(100),
            FP64.FromDouble(0.3), FP64.FromDouble(1.87), FP64.FromDouble(12.345), FP64.FromDouble(66.6667),
            FP64.FromDouble(99.99), FP64.FromInt(18) - FP64.FromDouble(4.4), FP64.FromInt(-2)
        };
        foreach (FP64 sample in samples) {
            for (int step = 0; step < 64; step++) {
                FP64 value = sample + FP64.FromRaw(step * 977L);
                long taken = FighterMeterRules.FromDamageTaken(value).RawValue;
                long takenBefore = (value / FP64.FromInt(4)).RawValue;
                if (taken != takenBefore && mismatches++ == 0) first = $"taken {value}: {taken} != {takenBefore}";

                long kept = FighterMeterRules.RetainOnStockLoss(value).RawValue;
                long keptBefore = (value * FP64.FromInt(1) / FP64.FromInt(2)).RawValue;
                if (kept != keptBefore && mismatches++ == 0) first = $"retain {value}: {kept} != {keptBefore}";
            }
        }
        if (mismatches > 0) {
            AssertThat($"{mismatches} mismatches; first {first}")
                .OverrideFailureMessage("The sim's meter view must stay bit-identical to the inline expressions it replaced.")
                .IsEqual("");
        }
    }

    [TestCase]
    public void TheStoryGainFunctionsScaleGainByTheFactorAndMatchTheOldFormulasAtOne() {
        // At a factor of 1 the gain is exactly the pre-rulebook expression.
        foreach (float damage in new[] { 0f, 0.5f, 1f, 3.3f, 17f, 20f, 47.25f, 100f, 250f }) {
            AssertThat(InfluenceMeterRules.GainFromDamageDealt(damage)).IsEqual(System.MathF.Max(0f, damage) * 1.0f);
            AssertThat(InfluenceMeterRules.GainFromDamageTaken(damage)).IsEqual(System.MathF.Max(0f, damage) * 0.25f);
        }

        // Mozart's Minor Resonance (+10%) scales both gains.
        AssertFloat(InfluenceMeterRules.GainFromDamageDealt(20f, 1.1f)).IsEqualApprox(22f, 0.0001f);
        AssertFloat(InfluenceMeterRules.GainFromDamageTaken(20f, 1.1f)).IsEqualApprox(5.5f, 0.0001f);

        // Nothing is ever earned from negative damage or a negative factor.
        AssertThat(InfluenceMeterRules.GainFromDamageDealt(-10f, 1.1f)).IsEqual(0f);
        AssertThat(InfluenceMeterRules.GainFromDamageTaken(-10f, 1.1f)).IsEqual(0f);
        AssertThat(InfluenceMeterRules.GainFromDamageDealt(10f, -1f)).IsEqual(0f);
        AssertThat(InfluenceMeterRules.GainFromDamageTaken(10f, -1f)).IsEqual(0f);
    }
}
