using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>Ruling 7d, plan section 1.13: the pure rule behind hazards hurting launched or thrown mobs.</summary>
[TestSuite]
public class HazardMobRulesTests {

    [TestCase]
    public void AMobIsVulnerableOnlyWhileThrownOrInALaunchOrFinisherFlight() {
        AssertThat(HazardMobRules.IsVulnerable(false, false, false, dormant: false, alive: true)).IsFalse();
        AssertThat(HazardMobRules.IsVulnerable(true, false, false, dormant: false, alive: true)).IsTrue();
        AssertThat(HazardMobRules.IsVulnerable(false, true, false, dormant: false, alive: true)).IsTrue();
        AssertThat(HazardMobRules.IsVulnerable(false, false, true, dormant: false, alive: true)).IsTrue();
    }

    [TestCase]
    public void ADormantOrDeadMobIsNeverVulnerable() {
        AssertThat(HazardMobRules.IsVulnerable(true, true, true, dormant: true, alive: true)).IsFalse();
        AssertThat(HazardMobRules.IsVulnerable(true, true, true, dormant: false, alive: false)).IsFalse();
    }

    [TestCase]
    public void AStandardTakesThirtyOrDies() {
        AssertThat(HazardMobRules.StandardImpactDamage).IsEqual(30);
        AssertThat(HazardMobRules.ImpactDamage(HazardMobClass.Standard, 400, 400)).IsEqual(30);
        AssertThat(HazardMobRules.ImpactDamage(HazardMobClass.Standard, 31, 400)).IsEqual(30);
        AssertThat(HazardMobRules.ImpactDamage(HazardMobClass.Standard, 12, 400)).IsEqual(12);
    }

    [TestCase]
    public void FodderAlwaysDies() {
        AssertThat(HazardMobRules.ImpactDamage(HazardMobClass.Fodder, 10, 10)).IsEqual(10);
        AssertThat(HazardMobRules.ImpactDamage(HazardMobClass.Fodder, 300, 300)).IsEqual(300);
    }

    [TestCase]
    public void AnEliteTakesAQuarterOfItsMaxHPAndNeverDiesToOneImpact() {
        AssertThat(HazardMobRules.ImpactDamage(HazardMobClass.Elite, 200, 200)).IsEqual(50);
        // 62.5 rounds away from zero.
        AssertThat(HazardMobRules.ImpactDamage(HazardMobClass.Elite, 90, 250)).IsEqual(63);
        AssertThat(HazardMobRules.ImpactDamage(HazardMobClass.Elite, 20, 200)).IsEqual(19);
        AssertThat(HazardMobRules.ImpactDamage(HazardMobClass.Elite, 1, 200)).IsEqual(0);
    }

    [TestCase]
    public void TheFodderIDClassifiesAsFodderAndAnEliteTierWinsOverIt() {
        AssertThat(HazardMobRules.Classify(false, HazardMobRules.FodderEnemyID)).IsEqual(HazardMobClass.Fodder);
        AssertThat(HazardMobRules.Classify(false, "chrono_slasher")).IsEqual(HazardMobClass.Standard);
        AssertThat(HazardMobRules.Classify(false, null)).IsEqual(HazardMobClass.Standard);
        AssertThat(HazardMobRules.Classify(true, HazardMobRules.FodderEnemyID)).IsEqual(HazardMobClass.Elite);
    }
}
