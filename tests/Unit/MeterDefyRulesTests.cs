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
        AssertThat(BasicComboRules.MomentumUnitsPerPip).IsEqual(7200);
        AssertThat(BasicComboRules.MomentumMaxUnits).IsEqual(28800);
        AssertThat(BasicComboRules.MomentumStartUnits).IsEqual(14400);
        AssertThat(BasicComboRules.MomentumSpecialCostUnits).IsEqual(BasicComboRules.MomentumUnitsPerPip);
        AssertThat(BasicComboRules.SpecialReuseDelayFrames).IsEqual(120);
        // 12 s of regen is exactly one pip: the per-frame figure is derived, not free.
        AssertThat(BasicComboRules.MomentumRegenUnitsPerFrame * BasicComboRules.MomentumRegenFramesPerPip)
            .IsEqual(BasicComboRules.MomentumUnitsPerPip);
    }

    [TestCase]
    public void TheGainTableCoversEverySwingVariant() {
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantChain, 0)).IsEqual(360);
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantChain, 1)).IsEqual(360);
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantChain, 2)).IsEqual(1440);
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantUpAttack, 0)).IsEqual(360);
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantDownAir, 0)).IsEqual(360);
        AssertThat(BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantAirAttack, 0)).IsEqual(360);
        // A full landed string is 0.3 pip.
        AssertThat(
            BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantChain, 0)
            + BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantChain, 1)
            + BasicComboRules.MomentumGainUnitsFor(BasicComboRules.VariantChain, 2)).IsEqual(2160);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox("combo_1")).IsEqual(360);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox("combo_3")).IsEqual(1440);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox(BasicComboRules.AirAttackHitboxID)).IsEqual(360);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox(BasicComboRules.UpAttackHitboxID)).IsEqual(360);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox(BasicComboRules.DownAirHitboxID)).IsEqual(360);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox("throw")).IsEqual(0);
        AssertThat(BasicComboRules.MomentumGainUnitsForHitbox(null)).IsEqual(0);
    }

    [TestCase]
    public void ASpecialIsAffordableFromOnePipAndSpendingNeverGoesBelowZero() {
        AssertThat(FighterMomentumRules.CanAfford(7199)).IsFalse();
        AssertThat(FighterMomentumRules.CanAfford(7200)).IsTrue();
        AssertThat(FighterMomentumRules.Spend(14400)).IsEqual(7200);
        AssertThat(FighterMomentumRules.Spend(7200)).IsEqual(0);
        AssertThat(FighterMomentumRules.Spend(100)).IsEqual(0);
        // Saturates instead of wrapping at the integer extremes (Codex review, Phase A).
        AssertThat(FighterMomentumRules.Spend(int.MinValue)).IsEqual(0);
    }

    [TestCase]
    public void GainAndRegenClampAtFourPips() {
        AssertThat(FighterMomentumRules.Gain(28800 - 100, 360)).IsEqual(28800);
        AssertThat(FighterMomentumRules.Gain(0, 1440)).IsEqual(1440);
        AssertThat(FighterMomentumRules.Regen(0)).IsEqual(10);
        AssertThat(FighterMomentumRules.Regen(28795)).IsEqual(28800);
        AssertThat(FighterMomentumRules.Gain(28800, int.MaxValue)).IsEqual(28800);
        AssertThat(FighterMomentumRules.Regen(int.MaxValue)).IsEqual(28800);
        // 720 frames of regen from empty is exactly one pip.
        int units = 0;
        for (int frame = 0; frame < BasicComboRules.MomentumRegenFramesPerPip; frame++) units = FighterMomentumRules.Regen(units);
        AssertThat(units).IsEqual(7200);
    }

    [TestCase]
    public void SpawnAndSuddenDeathStartAtTwoPipsAndClampIsTotal() {
        AssertThat(FighterMomentumRules.StartUnits).IsEqual(14400);
        AssertThat(FighterMomentumRules.SuddenDeathUnits).IsEqual(14400);
        AssertThat(FighterMomentumRules.Clamp(-5)).IsEqual(0);
        AssertThat(FighterMomentumRules.Clamp(99999)).IsEqual(28800);
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
