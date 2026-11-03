using FTT.Combat;
using FTT.FighterSim;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The meter and Defy update's shared rulebook (2026-10-31, plan section 1.4):
/// the Momentum gauge and Defy History regions of <see cref="BasicComboRules"/>
/// and the sim's integer view, <see cref="FighterMomentumRules"/>. Pure C#:
/// nothing here touches the Godot runtime.
/// </summary>
[TestSuite]
public class MeterDefyRulesTests {

    [TestCase]
    public void TheGaugeConstantsAreConsistentWithOneAnother() {
        AssertThat(BasicComboRules.MomentumMaxUnits).IsEqual(BasicComboRules.MomentumMaxPips * BasicComboRules.MomentumUnitsPerPip);
        AssertThat(BasicComboRules.MomentumStartUnits).IsEqual(BasicComboRules.MomentumStartPips * BasicComboRules.MomentumUnitsPerPip);
        AssertThat(BasicComboRules.MomentumSpecialCostUnits).IsEqual(BasicComboRules.MomentumUnitsPerPip);
        AssertThat(BasicComboRules.SpecialReuseDelayFrames).IsEqual(120);
        // A pip's worth of regen frames is exactly one pip: the per-frame figure is derived, not free.
        AssertThat(BasicComboRules.MomentumRegenUnitsPerFrame * BasicComboRules.MomentumRegenFramesPerPip)
            .IsEqual(BasicComboRules.MomentumUnitsPerPip);
    }

    [TestCase]
    public void AGainReviewPipIsFiftyFourHundredUnitsAndRegeneratesInNineSeconds() {
        // Gameplay review 2026-11-03 (ruling 10a+): 9 s a pip, by changing the unit and not the rate.
        AssertThat(BasicComboRules.MomentumUnitsPerPip).IsEqual(5400);
        AssertThat(BasicComboRules.MomentumRegenFramesPerPip).IsEqual(540);
        AssertThat(BasicComboRules.MomentumRegenUnitsPerFrame).IsEqual(10);
        AssertThat(BasicComboRules.MomentumRegenFramesPerPip / 60).IsEqual(9);
        AssertThat(BasicComboRules.MomentumMaxUnits).IsEqual(21600);
        AssertThat(BasicComboRules.MomentumStartUnits).IsEqual(10800);
        AssertThat(FighterMomentumRules.StartUnits).IsEqual(10800);
    }

    [TestCase]
    public void TheGainsStayFiveHundredthsAndAFifthOfAPip() {
        // The unit moved, the pip fractions did not: 0.05, 0.05 and 0.2 of a pip.
        AssertThat(BasicComboRules.MomentumStringHitGainUnits).IsEqual(270);
        AssertThat(BasicComboRules.MomentumDirectionalGainUnits).IsEqual(270);
        AssertThat(BasicComboRules.MomentumFinisherGainUnits).IsEqual(1080);
        AssertThat(BasicComboRules.MomentumStringHitGainUnits * 20).IsEqual(BasicComboRules.MomentumUnitsPerPip);
        AssertThat(BasicComboRules.MomentumFinisherGainUnits * 5).IsEqual(BasicComboRules.MomentumUnitsPerPip);
    }

    [TestCase]
    public void TheGainTableCoversEverySwingVariant() {
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantChain, 0)).IsEqual(BasicComboRules.MomentumStringHitGainUnits);
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantChain, 1)).IsEqual(BasicComboRules.MomentumStringHitGainUnits);
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantChain, 2)).IsEqual(BasicComboRules.MomentumFinisherGainUnits);
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantUpAttack, 0)).IsEqual(BasicComboRules.MomentumDirectionalGainUnits);
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantDownAir, 0)).IsEqual(BasicComboRules.MomentumDirectionalGainUnits);
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantAirAttack, 0)).IsEqual(BasicComboRules.MomentumDirectionalGainUnits);
        // A full landed string is 0.3 pip.
        AssertThat(
            BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantChain, 0)
            + BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantChain, 1)
            + BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantChain, 2)).IsEqual(2 * BasicComboRules.MomentumStringHitGainUnits + BasicComboRules.MomentumFinisherGainUnits);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox("combo_1")).IsEqual(BasicComboRules.MomentumStringHitGainUnits);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox("combo_3")).IsEqual(BasicComboRules.MomentumFinisherGainUnits);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox(BasicComboRules.AirAttackHitboxID)).IsEqual(BasicComboRules.MomentumDirectionalGainUnits);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox(BasicComboRules.UpAttackHitboxID)).IsEqual(BasicComboRules.MomentumDirectionalGainUnits);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox(BasicComboRules.DownAirHitboxID)).IsEqual(BasicComboRules.MomentumDirectionalGainUnits);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox("throw")).IsEqual(0);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox(null)).IsEqual(0);
    }

    [TestCase]
    public void ASpecialIsAffordableFromOnePipAndSpendingNeverGoesBelowZero() {
        const int pip = BasicComboRules.MomentumUnitsPerPip;
        AssertThat(FighterMomentumRules.CanAfford(pip - 1)).IsFalse();
        AssertThat(FighterMomentumRules.CanAfford(pip)).IsTrue();
        AssertThat(FighterMomentumRules.Spend(2 * pip)).IsEqual(pip);
        AssertThat(FighterMomentumRules.Spend(pip)).IsEqual(0);
        AssertThat(FighterMomentumRules.Spend(100)).IsEqual(0);
        // Saturates instead of wrapping at the integer extremes (Codex review, Phase A).
        AssertThat(FighterMomentumRules.Spend(int.MinValue)).IsEqual(0);
    }

    [TestCase]
    public void GainAndRegenClampAtFourPips() {
        const int max = BasicComboRules.MomentumMaxUnits;
        AssertThat(FighterMomentumRules.Gain(max - 100, BasicComboRules.MomentumStringHitGainUnits)).IsEqual(max);
        AssertThat(FighterMomentumRules.Gain(0, BasicComboRules.MomentumFinisherGainUnits)).IsEqual(BasicComboRules.MomentumFinisherGainUnits);
        AssertThat(FighterMomentumRules.Regen(0)).IsEqual(BasicComboRules.MomentumRegenUnitsPerFrame);
        AssertThat(FighterMomentumRules.Regen(max - BasicComboRules.MomentumRegenUnitsPerFrame / 2)).IsEqual(max);
        AssertThat(FighterMomentumRules.Gain(max, int.MaxValue)).IsEqual(max);
        AssertThat(FighterMomentumRules.Regen(int.MaxValue)).IsEqual(max);
        // A pip's worth of regen frames from empty is exactly one pip.
        int units = 0;
        for (int frame = 0; frame < BasicComboRules.MomentumRegenFramesPerPip; frame++) units = FighterMomentumRules.Regen(units);
        AssertThat(units).IsEqual(BasicComboRules.MomentumUnitsPerPip);
    }

    [TestCase]
    public void SpawnAndSuddenDeathStartAtTwoPipsAndClampIsTotal() {
        AssertThat(FighterMomentumRules.StartUnits).IsEqual(BasicComboRules.MomentumStartUnits);
        AssertThat(FighterMomentumRules.SuddenDeathUnits).IsEqual(BasicComboRules.MomentumStartUnits);
        AssertThat(FighterMomentumRules.Clamp(-5)).IsEqual(0);
        AssertThat(FighterMomentumRules.Clamp(99999)).IsEqual(BasicComboRules.MomentumMaxUnits);
    }

    [TestCase]
    public void TheDefyFrameTableIsSixInvulnerableThenTwentyFourRecovery() {
        AssertThat(BasicComboRules.DefyInfluenceCost).IsEqual(50);
        AssertThat(BasicComboRules.DefyInvulnerableFrames).IsEqual(6);
        AssertThat(BasicComboRules.DefyBlastFrame).IsEqual(6);
        AssertThat(BasicComboRules.DefyRecoveryFrames).IsEqual(24);
        AssertThat(BasicComboRules.DefyTotalFrames).IsEqual(30);
        AssertThat(BasicComboRules.DefyBlastFrame <= BasicComboRules.DefyInvulnerableFrames).IsTrue();
        AssertThat(BasicComboRules.DefyBlastRadiusUnits).IsEqual(2.5);
        AssertThat(BasicComboRules.DefyBlastStaggerFrames).IsEqual(30);
        AssertThat(BasicComboRules.DefyBlastHitstopFrames).IsEqual(8);
        AssertThat(BasicComboRules.DefyBlastPushXUnitsPerSecond).IsEqual(6);
        AssertThat(BasicComboRules.DefyBlastPushYUnitsPerSecond).IsEqual(6);
    }

    [TestCase]
    public void TheDefyHealIsThePoolOrATenthOfMaxHPCappedAtWhatIsMissing() {
        // Empty pool still heals the 10 % floor (ceil(MaxHP / 10)).
        AssertThat(BasicComboRules.DefyHealPoints(50, 0, 100)).IsEqual(10);
        AssertThat(BasicComboRules.DefyHealPoints(50, 0, 105)).IsEqual(11);
        // A bigger pool wins over the floor.
        AssertThat(BasicComboRules.DefyHealPoints(50, 25, 100)).IsEqual(25);
        // Never more than is missing, never negative.
        AssertThat(BasicComboRules.DefyHealPoints(4, 25, 100)).IsEqual(4);
        AssertThat(BasicComboRules.DefyHealPoints(0, 25, 100)).IsEqual(0);
        AssertThat(BasicComboRules.DefyHealPoints(-3, 25, 100)).IsEqual(0);
        // The 10 % floor never wraps for a huge max HP (Codex review, Phase A).
        AssertThat(BasicComboRules.DefyHealPoints(50, 0, int.MaxValue)).IsEqual(50);
    }
}
