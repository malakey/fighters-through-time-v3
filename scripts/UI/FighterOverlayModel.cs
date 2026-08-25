using Godot;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// How a banner is drawn. The overlay renders one Label, so the visual
    /// difference between a countdown numeral and a KO stamp is entirely type
    /// scale plus the plate behind it.
    /// </summary>
    public enum FighterBannerStyle {
        /// <summary>Nothing on screen.</summary>
        None,
        /// <summary>3-2-1 numerals: largest type, no plate.</summary>
        CountdownNumeral,
        /// <summary>GO!: large type, no plate.</summary>
        Go,
        /// <summary>KO / DRAW: stamped onto a plate.</summary>
        Stamp,
        /// <summary>Winner banner: mid type on a plate.</summary>
        Winner
    }

    /// <summary>One resolved banner beat. Keys, not translated strings.</summary>
    public readonly struct FighterOverlayBeat {
        public FighterOverlayBeat(
            FighterBannerStyle style,
            string bannerKey,
            Color color,
            float seconds,
            int fontSize,
            bool showsPlate) {
            Style = style;
            BannerKey = bannerKey;
            Color = color;
            Seconds = seconds;
            FontSize = fontSize;
            ShowsPlate = showsPlate;
        }

        public FighterBannerStyle Style { get; }
        public string BannerKey { get; }
        public Color Color { get; }
        public float Seconds { get; }
        public int FontSize { get; }
        public bool ShowsPlate { get; }
    }

    /// <summary>
    /// Package 8 B2. The presentation-overlay decision table, lifted out of the
    /// node so the phase-to-treatment mapping is testable without an engine tree
    /// or an <see cref="EventBus"/> round trip.
    ///
    /// The overlay is a pure consumer of
    /// <see cref="EventBus.OnFighterPresentation"/> — it never reads the
    /// simulation — and this class preserves that: it takes a payload's phase and
    /// scalars and returns what to draw.
    /// </summary>
    public static class FighterOverlayModel {

        /// <summary>Type scale for the countdown numerals (3, 2, 1).</summary>
        public const int CountdownFontSize = 190;

        /// <summary>Type scale for GO!.</summary>
        public const int GoFontSize = 150;

        /// <summary>Type scale for the KO / DRAW stamp.</summary>
        public const int StampFontSize = 170;

        /// <summary>Type scale for the winner banner.</summary>
        public const int WinnerFontSize = 84;

        /// <summary>Scrim alpha under a KO spotlight.</summary>
        public const float SpotlightDim = 0.45f;

        /// <summary>
        /// Countdown blip pitch. Rising across 3-2-1 and a distinct higher note on
        /// GO, so the start of a match is audible without looking at the banner
        /// (A2's <c>AudioManager.PlayCountdownBlip</c> takes the pitch scale).
        /// </summary>
        public static float CountdownBlipPitch(int countdownValue) => countdownValue switch {
            3 => 0.9f,
            2 => 1.0f,
            1 => 1.1f,
            _ => 1.45f
        };

        /// <summary>
        /// Scrim alpha for a phase, or a negative value when the phase does not
        /// own the scrim. Returning "no opinion" rather than 0 is what stops the
        /// countdown from clearing a dim a KO spotlight is holding.
        /// </summary>
        public static float DimAlphaFor(FighterPresentationPhase phase) => phase switch {
            FighterPresentationPhase.Spotlight => SpotlightDim,
            FighterPresentationPhase.Results => 0f,
            _ => -1f
        };

        /// <summary>
        /// Resolves the banner for a payload. Returns false for phases that draw
        /// no banner (StockLost, HitFreeze, SlowMotion, Spotlight, and a true-tie
        /// WinnerPose, which has no winner to name).
        /// </summary>
        public static bool TryResolveBanner(in FighterPresentationPayload payload, out FighterOverlayBeat beat) {
            switch (payload.Phase) {
                case FighterPresentationPhase.Countdown:
                    beat = payload.CountdownValue > 0
                        ? new FighterOverlayBeat(
                            FighterBannerStyle.CountdownNumeral,
                            "fighter_countdown_digit",
                            UIPalette.Cyan,
                            Duration(payload.DurationSeconds),
                            CountdownFontSize,
                            showsPlate: false)
                        : new FighterOverlayBeat(
                            FighterBannerStyle.Go,
                            "fighter_countdown_go",
                            UIPalette.GoldBright,
                            Duration(payload.DurationSeconds),
                            GoFontSize,
                            showsPlate: false);
                    return true;

                case FighterPresentationPhase.MatchStart:
                    beat = new FighterOverlayBeat(
                        FighterBannerStyle.Go,
                        "fighter_countdown_go",
                        UIPalette.GoldBright,
                        Duration(payload.DurationSeconds),
                        GoFontSize,
                        showsPlate: false);
                    return true;

                case FighterPresentationPhase.KOStamp:
                    beat = new FighterOverlayBeat(
                        FighterBannerStyle.Stamp,
                        "match_ko",
                        UIPalette.BossRed,
                        StampSeconds,
                        StampFontSize,
                        showsPlate: true);
                    return true;

                case FighterPresentationPhase.DrawStamp:
                    beat = new FighterOverlayBeat(
                        FighterBannerStyle.Stamp,
                        "match_draw",
                        UIPalette.Slate,
                        StampSeconds,
                        StampFontSize,
                        showsPlate: true);
                    return true;

                case FighterPresentationPhase.WinnerPose:
                    if (payload.IsTrueTie) break;
                    beat = new FighterOverlayBeat(
                        FighterBannerStyle.Winner,
                        "fighter_winner_pose",
                        UIPalette.GoldBright,
                        Duration(payload.DurationSeconds),
                        WinnerFontSize,
                        showsPlate: true);
                    return true;

                // V7.1: the final minute announces itself, and Sudden Death
                // names its rule — both reuse the stamp treatment.
                case FighterPresentationPhase.OvertimeStamp:
                    beat = new FighterOverlayBeat(
                        FighterBannerStyle.Stamp,
                        "fighter_overtime_stamp",
                        UIPalette.Warning,
                        Duration(payload.DurationSeconds),
                        StampFontSize,
                        showsPlate: true);
                    return true;

                case FighterPresentationPhase.SuddenDeathStamp:
                    beat = new FighterOverlayBeat(
                        FighterBannerStyle.Stamp,
                        "fighter_sudden_death_stamp",
                        UIPalette.BossRed,
                        Duration(payload.DurationSeconds),
                        StampFontSize,
                        showsPlate: true);
                    return true;
            }

            beat = default;
            return false;
        }

        /// <summary>True when the phase clears whatever the overlay is showing.</summary>
        public static bool ClearsBanner(FighterPresentationPhase phase) =>
            phase == FighterPresentationPhase.Results;

        /// <summary>The stamp holds for three seconds regardless of the payload.</summary>
        private const float StampSeconds = 3.0f;

        /// <summary>A payload with no duration still gets a second on screen.</summary>
        private static float Duration(float seconds) => seconds > 0f ? seconds : 1.0f;
    }
}
