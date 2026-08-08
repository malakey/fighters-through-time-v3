using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B2: the phase-to-treatment table behind
/// <see cref="FighterPresentationOverlay"/>.
///
/// The overlay renders one Label, so "which phase draws what" is the whole of
/// its behaviour and is worth pinning without an engine tree or an EventBus
/// round trip. The rule that actually bites is the scrim: several phases must
/// leave it alone rather than resetting it, or a countdown tick would clear the
/// dim a KO spotlight is holding.
/// </summary>
// Godot's Color struct puts this over the GdUnit0501 analyzer's bar.
[TestSuite]
[RequireGodotRuntime]
public class FighterOverlayModelTests {

    private static FighterPresentationPayload Payload(
        FighterPresentationPhase phase,
        int countdownValue = 0,
        int winner = -1,
        bool trueTie = false,
        float seconds = 1f) => new() {
            Phase = phase,
            CountdownValue = countdownValue,
            WinnerPlayerID = winner,
            SubjectPlayerID = -1,
            IsTrueTie = trueTie,
            DurationSeconds = seconds
        };

    [TestCase]
    public void CountdownNumeralsAndGoGetDifferentTreatments() {
        AssertThat(FighterOverlayModel.TryResolveBanner(
            Payload(FighterPresentationPhase.Countdown, countdownValue: 3),
            out FighterOverlayBeat numeral)).IsTrue();
        AssertThat(numeral.Style).IsEqual(FighterBannerStyle.CountdownNumeral);
        AssertThat(numeral.BannerKey).IsEqual("fighter_countdown_digit");
        AssertThat(numeral.ShowsPlate).IsFalse();
        AssertThat(numeral.FontSize).IsEqual(FighterOverlayModel.CountdownFontSize);

        // Countdown value 0 is the GO beat, not a numeral reading "0".
        AssertThat(FighterOverlayModel.TryResolveBanner(
            Payload(FighterPresentationPhase.Countdown, countdownValue: 0),
            out FighterOverlayBeat go)).IsTrue();
        AssertThat(go.Style).IsEqual(FighterBannerStyle.Go);
        AssertThat(go.BannerKey).IsEqual("fighter_countdown_go");
    }

    [TestCase]
    public void TheStampsCarryAPlateAndTheirOwnColours() {
        AssertThat(FighterOverlayModel.TryResolveBanner(
            Payload(FighterPresentationPhase.KOStamp), out FighterOverlayBeat ko)).IsTrue();
        AssertThat(ko.Style).IsEqual(FighterBannerStyle.Stamp);
        AssertThat(ko.BannerKey).IsEqual("match_ko");
        AssertThat(ko.Color).IsEqual(UIPalette.BossRed);
        AssertThat(ko.ShowsPlate).IsTrue();

        AssertThat(FighterOverlayModel.TryResolveBanner(
            Payload(FighterPresentationPhase.DrawStamp), out FighterOverlayBeat draw)).IsTrue();
        AssertThat(draw.BannerKey).IsEqual("match_draw");
        AssertThat(draw.Color).IsEqual(UIPalette.Slate);
        AssertThat(draw.ShowsPlate).IsTrue();
    }

    [TestCase]
    public void ATrueTieDrawsNoWinnerBanner() {
        // There is no winner to name, and "Player 0 wins" is what a naive
        // WinnerPlayerID + 1 would produce from the -1 sentinel.
        AssertThat(FighterOverlayModel.TryResolveBanner(
            Payload(FighterPresentationPhase.WinnerPose, winner: -1, trueTie: true),
            out FighterOverlayBeat _)).IsFalse();

        AssertThat(FighterOverlayModel.TryResolveBanner(
            Payload(FighterPresentationPhase.WinnerPose, winner: 1),
            out FighterOverlayBeat winner)).IsTrue();
        AssertThat(winner.Style).IsEqual(FighterBannerStyle.Winner);
        AssertThat(winner.BannerKey).IsEqual("fighter_winner_pose");
    }

    [TestCase]
    public void TheSilentPhasesDrawNoBanner() {
        FighterPresentationPhase[] silent = {
            FighterPresentationPhase.StockLost,
            FighterPresentationPhase.HitFreeze,
            FighterPresentationPhase.SlowMotion,
            FighterPresentationPhase.Spotlight,
            FighterPresentationPhase.Results
        };
        foreach (FighterPresentationPhase phase in silent) {
            AssertThat(FighterOverlayModel.TryResolveBanner(Payload(phase), out FighterOverlayBeat _))
                .OverrideFailureMessage($"{phase} should not draw a banner.").IsFalse();
        }
    }

    [TestCase]
    public void OnlyTheSpotlightAndResultsOwnTheScrim() {
        // A phase that returns a negative alpha is saying "no opinion". If
        // Countdown returned 0 instead, a countdown tick arriving during a KO
        // spotlight would clear the dim the spotlight is holding.
        AssertThat(FighterOverlayModel.DimAlphaFor(FighterPresentationPhase.Spotlight))
            .IsEqual(FighterOverlayModel.SpotlightDim);
        AssertThat(FighterOverlayModel.DimAlphaFor(FighterPresentationPhase.Results)).IsEqual(0f);
        AssertThat(FighterOverlayModel.DimAlphaFor(FighterPresentationPhase.Countdown) < 0f).IsTrue();
        AssertThat(FighterOverlayModel.DimAlphaFor(FighterPresentationPhase.KOStamp) < 0f).IsTrue();
        AssertThat(FighterOverlayModel.DimAlphaFor(FighterPresentationPhase.WinnerPose) < 0f).IsTrue();
    }

    [TestCase]
    public void ResultsIsTheOnlyPhaseThatClearsTheBanner() {
        AssertThat(FighterOverlayModel.ClearsBanner(FighterPresentationPhase.Results)).IsTrue();
        AssertThat(FighterOverlayModel.ClearsBanner(FighterPresentationPhase.KOStamp)).IsFalse();
        AssertThat(FighterOverlayModel.ClearsBanner(FighterPresentationPhase.Countdown)).IsFalse();
    }

    [TestCase]
    public void TheCountdownBlipRisesAcrossTheTicksAndPeaksOnGo() {
        float three = FighterOverlayModel.CountdownBlipPitch(3);
        float two = FighterOverlayModel.CountdownBlipPitch(2);
        float one = FighterOverlayModel.CountdownBlipPitch(1);
        float go = FighterOverlayModel.CountdownBlipPitch(0);
        AssertThat(three < two).IsTrue();
        AssertThat(two < one).IsTrue();
        AssertThat(one < go).IsTrue();
        AssertThat(three > 0f).IsTrue();
    }

    [TestCase]
    public void APayloadWithNoDurationStillGetsTimeOnScreen() {
        AssertThat(FighterOverlayModel.TryResolveBanner(
            Payload(FighterPresentationPhase.MatchStart, seconds: 0f),
            out FighterOverlayBeat beat)).IsTrue();
        AssertThat(beat.Seconds > 0f).OverrideFailureMessage(
            "A zero-duration payload would flash a banner for a single frame.").IsTrue();
    }
}
