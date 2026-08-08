using Godot;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// Placeholder renderer for local Fighter Mode flow beats: the 3-2-1-GO
    /// countdown and the post-match KO/DRAW stamp sequence. It is a pure
    /// consumer of <see cref="EventBus.OnFighterPresentation"/> — it never reads
    /// the simulation and never feeds anything back into it, so Package 8 can
    /// replace this whole class without touching match flow.
    /// Mirrors <see cref="RewindPresentationOverlay"/>'s subscription contract.
    /// </summary>
    public partial class FighterPresentationOverlay : CanvasLayer {
        private ColorRect _dim;
        private Label _banner;
        private double _bannerSecondsRemaining;

        public override void _Ready() {
            Layer = 80;
            // The stamp has to keep animating through a forced pause modal.
            ProcessMode = ProcessModeEnum.Always;

            _dim = new ColorRect {
                Name = "Dim",
                Color = new Color(0f, 0f, 0f, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false
            };
            _dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_dim);

            _banner = new Label {
                Name = "Banner",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false
            };
            _banner.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _banner.AddThemeFontSizeOverride("font_size", 96);
            _banner.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.35f));
            AddChild(_banner);

            if (EventBus.Instance != null) {
                EventBus.Instance.OnFighterPresentation += OnFighterPresentation;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnFighterPresentation -= OnFighterPresentation;
            }
        }

        public override void _Process(double delta) {
            if (_bannerSecondsRemaining <= 0.0) return;
            _bannerSecondsRemaining -= delta;
            if (_bannerSecondsRemaining <= 0.0 && _banner != null) _banner.Visible = false;
        }

        /// <summary>Current banner copy; empty when nothing is displayed. Test seam.</summary>
        public string BannerText => _banner != null && _banner.Visible ? _banner.Text : "";

        private void OnFighterPresentation(FighterPresentationPayload payload) {
            switch (payload.Phase) {
                case FighterPresentationPhase.Countdown:
                    ShowBanner(
                        payload.CountdownValue > 0
                            ? string.Format(Tr("fighter_countdown_digit"), payload.CountdownValue)
                            : Tr("fighter_countdown_go"),
                        payload.DurationSeconds,
                        new Color(0f, 0.92f, 0.95f));
                    break;
                case FighterPresentationPhase.MatchStart:
                    ShowBanner(Tr("fighter_countdown_go"), payload.DurationSeconds, new Color(1f, 0.92f, 0.35f));
                    break;
                case FighterPresentationPhase.Spotlight:
                    SetDim(0.4f);
                    break;
                case FighterPresentationPhase.KOStamp:
                    ShowBanner(Tr("match_ko"), 3.0f, new Color(1f, 0.28f, 0.2f));
                    break;
                case FighterPresentationPhase.DrawStamp:
                    ShowBanner(Tr("match_draw"), 3.0f, new Color(0.8f, 0.82f, 0.9f));
                    break;
                case FighterPresentationPhase.WinnerPose:
                    if (!payload.IsTrueTie) {
                        ShowBanner(
                            string.Format(Tr("fighter_winner_pose"), payload.WinnerPlayerID + 1),
                            payload.DurationSeconds,
                            new Color(1f, 0.92f, 0.35f));
                    }
                    break;
                case FighterPresentationPhase.Results:
                    SetDim(0f);
                    HideBanner();
                    break;
            }
        }

        private void ShowBanner(string text, float seconds, Color color) {
            if (_banner == null) return;
            _banner.Text = text;
            _banner.AddThemeColorOverride("font_color", color);
            _banner.Visible = true;
            _bannerSecondsRemaining = seconds > 0f ? seconds : 1.0;
        }

        private void HideBanner() {
            if (_banner == null) return;
            _banner.Visible = false;
            _bannerSecondsRemaining = 0.0;
        }

        private void SetDim(float alpha) {
            if (_dim == null) return;
            _dim.Color = new Color(0f, 0f, 0f, alpha);
            _dim.Visible = alpha > 0f;
        }
    }
}
