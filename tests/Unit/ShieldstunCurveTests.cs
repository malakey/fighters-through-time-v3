using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-BLOCK (combat and movement update 2026-10-16, decisions 4.1 / 11.2;
/// ledger <c>USER-SCALED-SHIELDSTUN</c> / <c>PROVISIONAL-SHIELDSTUN-CURVE</c>):
/// the one shieldstun function both modes read —
/// <c>clamp(6 + damage / 4, 6, 16)</c> with integer division — replacing the
/// flat 8 frames. Pure C#: nothing here touches the Godot runtime.
/// </summary>
[TestSuite]
public class ShieldstunCurveTests {

    [TestCase]
    public void TheShieldstunTableScalesWithTheBlockedDamage() {
        int[] damage = { 0, 3, 4, 8, 12, 20, 40, 100 };
        int[] frames = { 6, 6, 7, 8, 9, 11, 16, 16 };
        for (int index = 0; index < damage.Length; index++) {
            AssertThat(BasicComboRules.ShieldstunFramesFor(damage[index]))
                .OverrideFailureMessage($"{damage[index]} blocked damage must lock {frames[index]} frames.")
                .IsEqual(frames[index]);
        }
        AssertThat(BasicComboRules.ShieldstunBaseFrames).IsEqual(6);
        AssertThat(BasicComboRules.ShieldstunDamageDivisor).IsEqual(4);
    }

    [TestCase]
    public void TheCapIsTheShatterFreezeAndNoDamageGoesBelowTheBase() {
        AssertThat(BasicComboRules.ShieldstunMaxFrames)
            .OverrideFailureMessage("A non-shatter block never locks longer than a shatter freezes.")
            .IsEqual(BasicComboRules.ShatterFreezeFrames);
        AssertThat(BasicComboRules.ShieldstunMaxFrames).IsEqual(16);
        // The first damage that reaches the cap, and every larger one.
        AssertThat(BasicComboRules.ShieldstunFramesFor(39)).IsEqual(15);
        AssertThat(BasicComboRules.ShieldstunFramesFor(40)).IsEqual(16);
        AssertThat(BasicComboRules.ShieldstunFramesFor(int.MaxValue)).IsEqual(16);
        // A negative or zero basis (a zero-damage cast such as Siphon Snare) is the base.
        AssertThat(BasicComboRules.ShieldstunFramesFor(-12)).IsEqual(BasicComboRules.ShieldstunBaseFrames);
        AssertThat(BasicComboRules.ShieldstunFramesFor(0)).IsEqual(BasicComboRules.ShieldstunBaseFrames);
        for (int blocked = 0; blocked <= 120; blocked++) {
            int value = BasicComboRules.ShieldstunFramesFor(blocked);
            if (value < BasicComboRules.ShieldstunBaseFrames || value > BasicComboRules.ShieldstunMaxFrames) {
                AssertThat($"{blocked} damage gave {value} frames").IsEqual("");
            }
            if (blocked > 0 && value < BasicComboRules.ShieldstunFramesFor(blocked - 1)) {
                AssertThat($"the curve fell between {blocked - 1} and {blocked} damage").IsEqual("");
            }
        }
    }
}
