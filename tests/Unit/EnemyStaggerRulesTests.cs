using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.4 Enemy Stagger Discipline rulebook pins (design Section 4, Defense
/// Mechanics — PvE only). The constants are the design contract; the behavior
/// they drive is pinned in <c>EnemyControllerTests</c> (getup armor, elite
/// budget, diminishing special stun, pressure-exit) and the boss compliance
/// pin lives in <c>BossControllerTests</c>.
/// </summary>
[TestSuite]
public class EnemyStaggerRulesTests {

    [TestCase]
    public void GetupArmorIsThirtySixFramesWhichIsSixTenthsOfASecond() {
        AssertThat(EnemyStaggerRules.GetupArmorFrames).IsEqual(36);
        AssertThat(EnemyStaggerRules.GetupArmorSeconds).IsEqualApprox(0.6f, 0.0001f);
    }

    [TestCase]
    public void StaggerBudgetsDecayAndArmoredRecoveryWindowsMatchTheDesign() {
        AssertThat(EnemyStaggerRules.EliteStaggerBudgetSeconds).IsEqualApprox(2.0f, 0.0001f);
        AssertThat(EnemyStaggerRules.BossStaggerBudgetSeconds).IsEqualApprox(1.5f, 0.0001f);
        AssertThat(EnemyStaggerRules.StaggerDecayPerSecond).IsEqualApprox(1.0f, 0.0001f);
        AssertThat(EnemyStaggerRules.EliteArmoredRecoverySeconds).IsEqualApprox(1.5f, 0.0001f);
        AssertThat(EnemyStaggerRules.BossArmoredRecoverySeconds).IsEqualApprox(1.0f, 0.0001f);
    }

    [TestCase]
    public void DiminishingSpecialStunHalvesInsideAFourSecondWindow() {
        AssertThat(EnemyStaggerRules.SpecialStunDiminishWindowSeconds).IsEqualApprox(4.0f, 0.0001f);
        AssertThat(EnemyStaggerRules.SpecialStunDiminishFactor).IsEqualApprox(0.5f, 0.0001f);
    }
}
