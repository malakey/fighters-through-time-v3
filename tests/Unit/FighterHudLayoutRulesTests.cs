using FTT.Core;
using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-HUDF, re-numbered by TG-HUDF: the pure rules behind the HUD-F2 top band. Where the HP tube stops shrinking (the
/// glass formula of UI_SPEC_GLASS 4.5), how wide Sudden Death's plate may grow, how far a long name shrinks, the icon
/// each status and Defy state draws, and the mode caption's key.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterHudLayoutRulesTests {

    [TestCase]
    public void TheHpBarShrinksOnlyWhenTheClustersWouldMeetTheTimer() {
        // 4.5: clamp(510 s, 300, (960 - 117 s - 24) - (36 + 130 s)) screen px: 510 at 1.0, 554 at 1.4 (714 unshrunk).
        AssertFloat(FighterHudModel.HpBarScreenWidth(1f)).IsEqualApprox(510f, 0.01f);
        AssertFloat(FighterHudModel.HpBarScreenWidth(0.9f)).IsEqualApprox(459f, 0.01f);
        AssertFloat(FighterHudModel.HpBarScreenWidth(1.4f)).IsEqualApprox(554.2f, 0.01f);
        AssertThat(FighterHudModel.HpBarWidth(1f)).IsEqual(510f);
        // In design units (the HUD lays out on viewport / s), floored so the clearance always holds.
        AssertThat(FighterHudModel.HpBarWidth(1.4f)).IsEqual(395f);
        foreach (float scale in new[] { 1f, 1.1f, 1.2f, 1.3f, 1.4f }) {
            // The tube's screen end, from the pinned 36 px margin through the scaled portrait column, stays 24 px clear of
            // the scaled plate's edge.
            float tubeEnd = FighterHudModel.ClusterMargin
                + (FighterHudModel.RoundelColumnWidth + FighterHudModel.HpBarWidth(scale)) * scale;
            float plateLeft = 960f - FighterHudModel.TimerPlateWidth / 2f * scale;
            if (tubeEnd + FighterHudModel.TimerClearance > plateLeft + 0.01f) {
                AssertThat($"at {scale} the tube ends at {tubeEnd}, within 24 px of the plate at {plateLeft}").IsEqual("");
            }
        }
        // The floor wins over the room on a narrow viewport (the CSS clamp), and never exceeds 510 s.
        AssertFloat(FighterHudModel.HpBarScreenWidth(1.4f, 1200f)).IsEqualApprox(FighterHudModel.HpBarMinScreenWidth, 0.01f);
        AssertFloat(FighterHudModel.HpBarScreenWidth(1f, 4000f)).IsEqualApprox(510f, 0.01f);
        // The Ultimate tube keeps 390 / 510 of its HP tube.
        AssertThat(FighterHudModel.MeterBarWidth(510f)).IsEqual(390f);
        AssertThat(FighterHudModel.MeterBarWidth(395f)).IsEqual(302f);
        AssertThat(FighterHudModel.ClusterWidth(510f)).IsEqual(640f);
        AssertThat(FighterHudModel.ChipsShowText(1920f)).IsTrue();
        AssertThat(FighterHudModel.ChipsShowText(1920f / 1.4f)).IsFalse();

        // Sudden Death's plate (4.5: "widens to fit (409 px of text)"): one line at 1.0, where the room is 520; at 1.4 the
        // room is the normal plate's (the formula spends it), so the line wraps inside 234.
        AssertFloat(FighterHudModel.TimerPlateMaxWidth(1f)).IsEqualApprox(520f, 0.01f);
        AssertFloat(FighterHudModel.SuddenDeathPlateWidth(409f, FighterHudModel.TimerPlateMaxWidth(1f))).IsEqualApprox(473f, 0.01f);
        // (the floored tube leaves under 2 px more than the normal plate)
        AssertFloat(FighterHudModel.TimerPlateMaxWidth(1.4f)).IsEqualApprox(FighterHudModel.TimerPlateWidth + 1f, 1f);
        AssertFloat(FighterHudModel.SuddenDeathPlateWidth(409f, FighterHudModel.TimerPlateMaxWidth(1.4f)))
            .IsEqualApprox(FighterHudModel.TimerPlateWidth + 1f, 1f);
        AssertThat(FighterHudModel.SuddenDeathPlateWidth(40f, 520f)).IsEqual(FighterHudModel.TimerPlateWidth);

        // A long name shrinks to its room, never below 18 px on screen (28 px Cinzel: 0.643 at 1.0, 0.459 at 1.4).
        AssertThat(FighterHudModel.NameScale(300f, 428f, 28f, 1f)).IsEqual(1f);
        AssertFloat(FighterHudModel.NameScale(600f, 428f, 28f, 1f)).IsEqualApprox(428f / 600f, 0.0001f);
        AssertFloat(FighterHudModel.NameScale(900f, 428f, 28f, 1f)).IsEqualApprox(18f / 28f, 0.0001f);
        AssertFloat(FighterHudModel.NameScale(900f, 313f, 28f, 1.4f)).IsEqualApprox(18f / (28f * 1.4f), 0.0001f);
    }

    [TestCase]
    public void EveryStatusHasADistinctKitIconAndTheModeCaptionHasAKey() {
        var seen = new System.Collections.Generic.HashSet<string>();
        foreach (StatusType status in new[] {
            StatusType.TimeDilation, StatusType.Venom, StatusType.StaticCharge, StatusType.RadiantBurn, StatusType.Root }) {
            string icon = FighterHudModel.StatusIconName(status);
            if (!seen.Add(icon)) AssertThat("duplicate status icon " + icon).IsEqual("");
            if (!FTT.UI.Pulp.PulpIcon.Exists(icon)) AssertThat("missing kit icon " + icon).IsEqual("");
        }
        AssertThat(FighterHudModel.ModeLabelKey((int)MatchMode.Stock)).IsEqual("fighter_hud_mode_stock");
        AssertThat(FighterHudModel.ModeLabelKey((int)MatchMode.TimeLimit)).IsEqual("fighter_hud_mode_time");
    }
}
