using FTT.Core;
using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-HUDF: the pure rules behind the HUD-F2 top band. Where the HP bar stops shrinking, the icon each
/// status and Defy state draws, and the mode caption's key. The numbers are UI_SPEC 4.5 / 3.3.1 sizes and
/// plan item 14's fit rule ("Fighter HP bars shrink before they meet the timer").
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterHudLayoutRulesTests {

    [TestCase]
    public void TheHpBarShrinksOnlyWhenTheClustersWouldMeetTheTimer() {
        AssertThat(FighterHudModel.HpBarWidth(1920f)).IsEqual(502f);
        AssertThat(FighterHudModel.HpBarWidth(2133f)).IsEqual(502f);
        // 1920 / 1.4: both clusters plus the 240 px plate no longer fit, so each bar gives up half the shortfall.
        float narrow = FighterHudModel.HpBarWidth(1920f / 1.4f);
        AssertThat(narrow).IsLess(502f);
        float used = 2f * (FighterHudModel.ClusterMargin + FighterHudModel.ClusterWidth(narrow))
            + FighterHudModel.TimerPlateWidth;
        if (used > 1920f / 1.4f) AssertThat("clusters overlap the timer at 1.4: used " + used).IsEqual("");
        // A tiny canvas stops at the floor rather than collapsing the bar.
        AssertThat(FighterHudModel.HpBarWidth(400f)).IsEqual(FighterHudModel.HpBarMinWidth);
        // The meter keeps the 390 : 502 proportion.
        AssertThat(FighterHudModel.MeterBarWidth(502f)).IsEqual(390f);
        AssertThat(FighterHudModel.ChipsShowText(1920f)).IsTrue();
        AssertThat(FighterHudModel.ChipsShowText(1920f / 1.4f)).IsFalse();
    }

    [TestCase]
    public void EveryStatusAndDefyStateHasADistinctKitIconAndTheModeCaptionHasAKey() {
        var seen = new System.Collections.Generic.HashSet<string>();
        foreach (StatusType status in new[] {
            StatusType.TimeDilation, StatusType.Venom, StatusType.StaticCharge, StatusType.RadiantBurn, StatusType.Root }) {
            string icon = FighterHudModel.StatusIconName(status);
            if (!seen.Add(icon)) AssertThat("duplicate status icon " + icon).IsEqual("");
            if (!FTT.UI.Pulp.PulpIcon.Exists(icon)) AssertThat("missing kit icon " + icon).IsEqual("");
        }
        var defy = new System.Collections.Generic.HashSet<string>();
        foreach (DefySealState state in new[] {
            DefySealState.Building, DefySealState.Ready, DefySealState.Spent, DefySealState.Barred }) {
            string icon = FighterHudModel.DefyIconName(state);
            if (!defy.Add(icon)) AssertThat("duplicate Defy icon " + icon).IsEqual("");
            if (!FTT.UI.Pulp.PulpIcon.Exists(icon)) AssertThat("missing kit icon " + icon).IsEqual("");
        }
        AssertThat(FighterHudModel.ModeLabelKey((int)MatchMode.Stock)).IsEqual("fighter_hud_mode_stock");
        AssertThat(FighterHudModel.ModeLabelKey((int)MatchMode.TimeLimit)).IsEqual("fighter_hud_mode_time");
    }
}
