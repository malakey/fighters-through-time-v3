using System.Collections.Generic;
using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B2. The two themed match-presentation surfaces: the
/// <see cref="FighterPresentationOverlay"/> banner sequence and the
/// <see cref="MatchResults"/> panel.
///
/// The overlay is driven through <see cref="FighterPresentationOverlay.Present"/>
/// rather than by raising bus events, so a failure names the phase that broke
/// instead of a subscription that did not fire — and so the suite cannot leave a
/// stray subscriber behind on the autoload.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterMatchPresentationTests {

    [TestCase]
    public void TheCountdownRendersNumeralsThenGo() {
        FighterPresentationOverlay overlay = MountOverlay();
        try {
            TranslationServer.SetLocale("en");

            overlay.Present(Payload(FighterPresentationPhase.Countdown, countdownValue: 3, seconds: 1f));
            AssertThat(overlay.BannerText)
                .IsEqual(string.Format(TranslationServer.Translate("fighter_countdown_digit").ToString(), 3));
            AssertThat(overlay.BannerColor).IsEqual(UIPalette.Cyan);
            AssertThat(overlay.BannerFontSize).IsEqual(FighterOverlayModel.CountdownFontSize);
            // Numerals sit on the stage, not on a plate.
            AssertThat(overlay.PlateVisible).IsFalse();

            overlay.Present(Payload(FighterPresentationPhase.Countdown, countdownValue: 0, seconds: 0.5f));
            AssertThat(overlay.BannerText)
                .IsEqual(TranslationServer.Translate("fighter_countdown_go").ToString());
        } finally {
            overlay.Free();
        }
    }

    [TestCase]
    public void TheKoStampSitsOnAPlateAndTheDrawStampReadsDifferently() {
        FighterPresentationOverlay overlay = MountOverlay();
        try {
            TranslationServer.SetLocale("en");

            overlay.Present(Payload(FighterPresentationPhase.KOStamp));
            AssertThat(overlay.BannerText).IsEqual(TranslationServer.Translate("match_ko").ToString());
            AssertThat(overlay.BannerColor).IsEqual(UIPalette.BossRed);
            AssertThat(overlay.PlateVisible).IsTrue();

            overlay.Present(Payload(FighterPresentationPhase.DrawStamp));
            AssertThat(overlay.BannerText).IsEqual(TranslationServer.Translate("match_draw").ToString());
            AssertThat(overlay.BannerColor).IsEqual(UIPalette.Slate);
        } finally {
            overlay.Free();
        }
    }

    [TestCase]
    public void ASpotlightDimIsNotClearedByALaterBannerBeat() {
        // The KO sequence raises Spotlight and the stamp in the same frame. If a
        // banner phase also owned the scrim it would immediately undo the dim.
        FighterPresentationOverlay overlay = MountOverlay();
        try {
            overlay.Present(Payload(FighterPresentationPhase.Spotlight));
            AssertThat(overlay.DimAlpha).IsEqual(FighterOverlayModel.SpotlightDim);
            AssertThat(overlay.BannerText).IsEqual("");

            overlay.Present(Payload(FighterPresentationPhase.KOStamp));
            AssertThat(overlay.DimAlpha).IsEqual(FighterOverlayModel.SpotlightDim);

            overlay.Present(Payload(FighterPresentationPhase.WinnerPose, winner: 0, seconds: 2f));
            AssertThat(overlay.DimAlpha).IsEqual(FighterOverlayModel.SpotlightDim);

            // Results is what hands the screen back.
            overlay.Present(Payload(FighterPresentationPhase.Results));
            AssertThat(overlay.DimAlpha).IsEqual(0f);
            AssertThat(overlay.BannerText).IsEqual("");
        } finally {
            overlay.Free();
        }
    }

    [TestCase]
    public void ATrueTieShowsNoWinnerBanner() {
        FighterPresentationOverlay overlay = MountOverlay();
        try {
            overlay.Present(Payload(FighterPresentationPhase.WinnerPose, winner: -1, trueTie: true, seconds: 2f));
            AssertThat(overlay.BannerText).OverrideFailureMessage(
                "A true tie has no winner to name.").IsEqual("");
        } finally {
            overlay.Free();
        }
    }

    [TestCase]
    public void ABannerExpiresOnItsOwnDuration() {
        FighterPresentationOverlay overlay = MountOverlay();
        try {
            overlay.Present(Payload(FighterPresentationPhase.MatchStart, seconds: 0.5f));
            AssertThat(overlay.BannerText).IsNotEqual("");

            overlay._Process(0.3);
            AssertThat(overlay.BannerText).IsNotEqual("");

            overlay._Process(0.3);
            AssertThat(overlay.BannerText).IsEqual("");
            AssertThat(overlay.PlateVisible).IsFalse();
        } finally {
            overlay.Free();
        }
    }

    [TestCase]
    public void TheResultsPanelStampsTheOutcomeAndTakesFocus() {
        MatchResults results = MountResults();
        try {
            TranslationServer.SetLocale("en");
            AssertThat(results.IsShowing).IsFalse();

            results.ShowResult(new FighterMatchResult(winnerPlayerID: 1, isTrueTie: false, completedTick: 900, finalHash: 7L));
            AssertThat(results.IsShowing).IsTrue();
            AssertThat(results.StampColor).IsEqual(UIPalette.BossRed);
            AssertThat(results.OutcomeText).IsEqual(
                string.Format(TranslationServer.Translate("fighter_results_winner").ToString(), 2));

            // A controller player must land on a button rather than nothing.
            Control focused = results.GetViewport()?.GuiGetFocusOwner();
            AssertThat(focused is Button).OverrideFailureMessage(
                "The results panel did not grab focus onto one of its buttons; a "
                + "controller player would have no way to leave the screen.").IsTrue();
        } finally {
            results.Free();
        }
    }

    /// <summary>V7.3 UI-scale pass: the stamp rides the TitleLabel theme
    /// variation with no font-size override (the old 56 px override froze it
    /// out of the accessibility UI scale), the outcome line the HeadingLabel.</summary>
    [TestCase]
    public void TheStampUsesTheTitleVariationWithNoFontSizeOverride() {
        MatchResults results = MountResults();
        try {
            var stamp = results.FindChild("Stamp", recursive: true, owned: false) as Label;
            AssertObject(stamp).IsNotNull();
            AssertThat(stamp.ThemeTypeVariation.ToString()).IsEqual(UIPalette.TitleLabelVariation);
            AssertThat(stamp.HasThemeFontSizeOverride("font_size")).IsFalse();
            var outcome = results.FindChild("Outcome", recursive: true, owned: false) as Label;
            AssertObject(outcome).IsNotNull();
            AssertThat(outcome.ThemeTypeVariation.ToString()).IsEqual(UIPalette.HeadingLabelVariation);
            AssertThat(outcome.HasThemeFontSizeOverride("font_size")).IsFalse();
        } finally {
            results.Free();
        }
    }

    [TestCase]
    public void ADrawStampsDifferentlyFromAKnockout() {
        MatchResults results = MountResults();
        try {
            TranslationServer.SetLocale("en");
            results.ShowResult(new FighterMatchResult(winnerPlayerID: -1, isTrueTie: true, completedTick: 900, finalHash: 7L));
            AssertThat(results.StampColor).IsEqual(UIPalette.Slate);
            AssertThat(results.OutcomeText)
                .IsEqual(TranslationServer.Translate("fighter_results_draw").ToString());
        } finally {
            results.Free();
        }
    }

    [TestCase]
    public void TheOverlayAdoptsTheSharedTheme() {
        // Package 8 A1: a screen adopts the theme by setting it on a Control root
        // and letting Godot propagate. A missing theme resource must not take the
        // overlay down, which is why UIPalette.ApplyTheme is null-tolerant.
        FighterPresentationOverlay overlay = MountOverlay();
        try {
            var themed = new List<Control>();
            CollectControls(overlay, themed);
            bool anyThemed = false;
            foreach (Control control in themed) {
                if (control.Theme != null) anyThemed = true;
            }
            AssertThat(anyThemed || UIPalette.LoadTheme() == null).IsTrue();
        } finally {
            overlay.Free();
        }
    }

    // ---- helpers --------------------------------------------------------

    private static void CollectControls(Node node, List<Control> found) {
        if (node is Control control) found.Add(control);
        int count = node.GetChildCount();
        for (int index = 0; index < count; index++) {
            CollectControls(node.GetChild(index), found);
        }
    }

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

    private static FighterPresentationOverlay MountOverlay() {
        var overlay = new FighterPresentationOverlay { Name = "OverlayUnderTest" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(overlay);
        return overlay;
    }

    private static MatchResults MountResults() {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/ui/MatchResults.tscn");
        var results = packed.Instantiate<MatchResults>();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(results);
        return results;
    }
}
