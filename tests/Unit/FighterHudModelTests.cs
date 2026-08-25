using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B2: the Fighter HUD's display arithmetic, exercised without a scene
/// tree. The HUD itself is a scene, so these rules would otherwise only be
/// reachable by instantiating one — and the interesting failures here (a clock
/// that reads 0:00 while the match is still live, an HP band that never turns
/// red) are arithmetic, not layout.
/// </summary>
// Godot's Color and Mathf put this over the GdUnit0501 analyzer's bar even
// though no engine node is involved.
[TestSuite]
[RequireGodotRuntime]
public class FighterHudModelTests {

    private const int TickRate = 60;

    [TestCase]
    public void TheMatchClockFollowsTheTimerEnabledFlag() {
        // V7: Stock mode defaults to the 8:00 timer too (configurable, including
        // Off), so clock visibility follows the deterministic TimerEnabled flag
        // rather than the match mode.
        AssertThat(FighterHudModel.TimerIsVisible(timerEnabled: false)).IsFalse();
        AssertThat(FighterHudModel.TimerIsVisible(timerEnabled: true)).IsTrue();
    }

    [TestCase]
    public void TheClockRoundsUpSoALiveMatchNeverReadsZero() {
        // One frame left is still a live match; flooring would show 0:00 while
        // the fighters are playing, which is the worst thing a clock can do.
        AssertThat(FighterHudModel.TotalSeconds(1, TickRate)).IsEqual(1);
        AssertThat(FighterHudModel.TotalSeconds(59, TickRate)).IsEqual(1);
        AssertThat(FighterHudModel.TotalSeconds(60, TickRate)).IsEqual(1);
        AssertThat(FighterHudModel.TotalSeconds(61, TickRate)).IsEqual(2);
        AssertThat(FighterHudModel.TotalSeconds(0, TickRate)).IsEqual(0);
        AssertThat(FighterHudModel.TotalSeconds(-40, TickRate)).IsEqual(0);
    }

    [TestCase]
    public void TheClockSplitsIntoMinutesAndPaddedSeconds() {
        // The default 480 s time limit.
        AssertThat(FighterHudModel.ClockMinutes(480 * TickRate, TickRate)).IsEqual(8);
        AssertThat(FighterHudModel.ClockSeconds(480 * TickRate, TickRate)).IsEqual(0);

        AssertThat(FighterHudModel.ClockMinutes(47 * TickRate, TickRate)).IsEqual(0);
        AssertThat(FighterHudModel.ClockSeconds(47 * TickRate, TickRate)).IsEqual(47);

        AssertThat(FighterHudModel.ClockMinutes(125 * TickRate, TickRate)).IsEqual(2);
        AssertThat(FighterHudModel.ClockSeconds(125 * TickRate, TickRate)).IsEqual(5);
    }

    [TestCase]
    public void ATickRateOfZeroCannotDivideByZero() {
        AssertThat(FighterHudModel.TotalSeconds(600, 0)).IsEqual(0);
        AssertThat(FighterHudModel.CooldownSeconds(600, 0)).IsEqual(0f);
    }

    [TestCase]
    public void BarFractionsClampAndSurviveADegenerateMaximum() {
        AssertThat(FighterHudModel.BarFraction(50, 100)).IsEqual(0.5f);
        AssertThat(FighterHudModel.BarFraction(-10, 100)).IsEqual(0f);
        AssertThat(FighterHudModel.BarFraction(150, 100)).IsEqual(1f);
        AssertThat(FighterHudModel.BarFraction(10, 0)).IsEqual(0f);
    }

    [TestCase]
    public void TheHpBarReadsInThreeBandsRatherThanAGradient() {
        AssertThat(FighterHudModel.HpFillColor(100, 100)).IsEqual(UIPalette.Cyan);
        AssertThat(FighterHudModel.HpFillColor(51, 100)).IsEqual(UIPalette.Cyan);
        AssertThat(FighterHudModel.HpFillColor(50, 100)).IsEqual(UIPalette.Warning);
        AssertThat(FighterHudModel.HpFillColor(26, 100)).IsEqual(UIPalette.Warning);
        AssertThat(FighterHudModel.HpFillColor(25, 100)).IsEqual(UIPalette.BossRed);
        AssertThat(FighterHudModel.HpFillColor(0, 100)).IsEqual(UIPalette.BossRed);
    }

    [TestCase]
    public void TheUltimateSlotIsMeterGatedAtAFullBar() {
        // The deterministic ultimate gate is Influence >= 100 with no cooldown,
        // so the HUD slot has to read the meter rather than a frame counter.
        AssertThat(FighterHudModel.UltimateIsReady(99.9f)).IsFalse();
        AssertThat(FighterHudModel.UltimateIsReady(FighterHudModel.MaxInfluence)).IsTrue();
        AssertThat(FighterHudModel.MeterFraction(50f)).IsEqual(0.5f);
        AssertThat(FighterHudModel.MeterFraction(140f)).IsEqual(1f);
        AssertThat(FighterHudModel.MeterFraction(-5f)).IsEqual(0f);
    }

    [TestCase]
    public void PipsClampIntoTheAuthoredCapacity() {
        AssertThat(FighterHudModel.FilledPips(2, 3)).IsEqual(2);
        AssertThat(FighterHudModel.FilledPips(9, 3)).IsEqual(3);
        AssertThat(FighterHudModel.FilledPips(-1, 3)).IsEqual(0);
        AssertThat(FighterHudModel.FilledPips(2, 0)).IsEqual(0);
    }

    [TestCase]
    public void StatusKeysMatchTheFamilyTheDebugLayerAlreadyUsed() {
        // The production HUD and the debug overlay must never name the same
        // status differently.
        AssertThat(FighterHudModel.StatusKey(StatusType.None)).IsEqual("status_none");
        AssertThat(FighterHudModel.StatusKey(StatusType.TimeDilation)).IsEqual("status_timedilation");
        AssertThat(FighterHudModel.StatusKey(StatusType.Venom)).IsEqual("status_venom");
        AssertThat(FighterHudModel.StatusKey(StatusType.StaticCharge)).IsEqual("status_staticcharge");
        AssertThat(FighterHudModel.StatusKey(StatusType.RadiantBurn)).IsEqual("status_radiantburn");
        AssertThat(FighterHudModel.StatusKey(StatusType.Root)).IsEqual("status_root");
    }

    [TestCase]
    public void EveryCooldownSlotHasItsOwnGlyphKey() {
        var keys = new System.Collections.Generic.HashSet<string>();
        foreach (FighterCooldownSlot slot in System.Enum.GetValues<FighterCooldownSlot>()) {
            keys.Add(FighterHudModel.CooldownGlyphKey(slot));
        }
        AssertThat(keys.Count).OverrideFailureMessage(
            "Two cooldown slots share a glyph key, so the HUD would label two "
            + "different abilities identically.").IsEqual(4);
    }

    [TestCase]
    public void OpacityIsClampedToTheSameFloorTheSaveEnforces() {
        // A corrupt payload must not be able to make the HUD invisible, because
        // the setting that would fix it is behind the HUD's own pause menu.
        AssertThat(FighterHudModel.ResolveOpacity(0f)).IsEqual(0.2f);
        AssertThat(FighterHudModel.ResolveOpacity(-3f)).IsEqual(0.2f);
        AssertThat(FighterHudModel.ResolveOpacity(0.6f)).IsEqual(0.6f);
        AssertThat(FighterHudModel.ResolveOpacity(4f)).IsEqual(1f);
    }
}
