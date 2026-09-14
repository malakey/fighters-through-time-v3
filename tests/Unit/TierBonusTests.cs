using FTT.Core;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A10 — the F05 Integrity tier bonus
/// (docs/design-contracts/DUST_ECONOMY.md §"Integrity bonus and banking"):
/// <c>bonus = floor(retainedBaseDust x tierRate)</c> at Restored >=50% 10%,
/// Stabilized >=20% 5%, Fractured &lt;20% 0%, calculated exactly once on
/// successful completion. Pure C#.
/// </summary>
[TestSuite]
public class TierBonusTests {

    [TestCase]
    public void TheThreeTiersPayTenFiveAndZeroPercentAtTheAuthoredLines() {
        // The tier lines themselves are A3's (V7.6 moved them 90/70 -> 50/20);
        // the RATES are the economy contract's and are read from the one place
        // that owns them rather than restated here.
        AssertThat(TimelineIntegrityRules.DustBonusPercent(100f)).IsEqual(10);
        AssertThat(TimelineIntegrityRules.DustBonusPercent(50f)).IsEqual(10);
        AssertThat(TimelineIntegrityRules.DustBonusPercent(49.9f)).IsEqual(5);
        AssertThat(TimelineIntegrityRules.DustBonusPercent(20f)).IsEqual(5);
        AssertThat(TimelineIntegrityRules.DustBonusPercent(19.9f)).IsEqual(0);
        AssertThat(TimelineIntegrityRules.DustBonusPercent(0f)).IsEqual(0);

        AssertThat(RewardAllocator.TierBonus(100, 100f, levelIsTimed: true)).IsEqual(10);
        AssertThat(RewardAllocator.TierBonus(100, 30f, levelIsTimed: true)).IsEqual(5);
        AssertThat(RewardAllocator.TierBonus(100, 10f, levelIsTimed: true)).IsEqual(0);
    }

    [TestCase]
    public void TheBonusFloorsRatherThanRounds() {
        // A level's whole base budget is 40-60 dust, so rounding up would drift
        // the campaign maxima off the contract's 787 / 1,094 within a few levels.
        AssertThat(RewardAllocator.TierBonus(25, 100f, levelIsTimed: true)).IsEqual(2);   // 2.5
        AssertThat(RewardAllocator.TierBonus(39, 100f, levelIsTimed: true)).IsEqual(3);   // 3.9
        AssertThat(RewardAllocator.TierBonus(59, 30f, levelIsTimed: true)).IsEqual(2);    // 2.95
        AssertThat(RewardAllocator.TierBonus(9, 100f, levelIsTimed: true)).IsEqual(0);    // 0.9
    }

    [TestCase]
    public void AnUntimedLevelPaysNoBonusAtAll() {
        // "Level 1 has no Integrity clock and grants no Integrity bonus." This
        // single exclusion is exactly why the all-Restored maxima are 787
        // required and 1,094 thorough rather than 792 / 1,100.
        AssertThat(RewardAllocator.TierBonus(50, 100f, levelIsTimed: false)).IsEqual(0);
        AssertThat(RewardAllocator.TierBonus(60, 100f, levelIsTimed: false)).IsEqual(0);
        // The same wallet on a timed level would have been paid.
        AssertThat(RewardAllocator.TierBonus(50, 100f, levelIsTimed: true)).IsEqual(5);
    }

    [TestCase]
    public void TheBonusIsNeverCompoundedAndNeverPaidOnNothing() {
        // retainedBaseDust "excludes deposited dust, respec refunds, and any
        // prior tier bonus": a second calculation over an already-bonused wallet
        // would pay more than the contract allows, so the single application is
        // what the completion transaction must perform.
        const int retained = 100;
        int once = RewardAllocator.TierBonus(retained, 100f, levelIsTimed: true);
        int paidTwice = once + RewardAllocator.TierBonus(retained + once, 100f, levelIsTimed: true);
        AssertThat(once).IsEqual(10);
        AssertThat(paidTwice)
            .OverrideFailureMessage(
                "Guard case: paying the bonus twice must be visibly larger than paying it once.")
            .IsEqual(21);

        // A failed attempt, a fully deposited wallet and a negative balance all
        // pay nothing rather than fabricating currency.
        AssertThat(RewardAllocator.TierBonus(0, 100f, levelIsTimed: true)).IsEqual(0);
        AssertThat(RewardAllocator.TierBonus(-40, 100f, levelIsTimed: true)).IsEqual(0);
    }
}
