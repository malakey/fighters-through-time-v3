using Godot;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// Renderer for local Fighter Mode flow beats: the 3-2-1-GO countdown and the
    /// post-match KO/DRAW/winner sequence. It is a pure consumer of
    /// <see cref="EventBus.OnFighterPresentation"/> — it never reads the
    /// simulation and never feeds anything back into it.
    /// Mirrors <see cref="RewindPresentationOverlay"/>'s subscription contract.
    ///
    /// Package 8 B2 themed it: the countdown numerals and the KO/DRAW stamp are
    /// on the shared type scale and palette, stamps and the winner banner sit on a
    /// plate so they read over any stage, and each countdown tick plays A2's
    /// <c>AudioManager.PlayCountdownBlip</c>. Which treatment a phase gets is
    /// decided by <see cref="FighterOverlayModel"/>, not here, so the mapping is
    /// testable without an engine tree.
    /// </summary>
    public partial class FighterPresentationOverlay : CanvasLayer {
        private ColorRect _dim;
        private PanelContainer _plate;
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

            var center = new CenterContainer {
                Name = "Center",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(center);
            AddChild(center);

            // A plate only appears behind the stamps and the winner banner; the
            // countdown numerals sit directly on the stage so they never hide it.
            _plate = new PanelContainer {
                Name = "Plate",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false
            };
            center.AddChild(_plate);

            _banner = new Label {
                Name = "Banner",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false
            };
            _banner.AddThemeFontSizeOverride("font_size", FighterOverlayModel.CountdownFontSize);
            _banner.AddThemeColorOverride("font_color", UIPalette.GoldBright);
            // A heavy outline is what keeps a numeral legible over an arbitrary
            // stage; the theme cannot express it because no other surface wants it.
            _banner.AddThemeColorOverride("font_outline_color", UIPalette.NavyDeep);
            _banner.AddThemeConstantOverride("outline_size", 12);
            _plate.AddChild(_banner);

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
            if (_bannerSecondsRemaining > 0.0) return;
            HideBanner();
        }

        /// <summary>Current banner copy; empty when nothing is displayed. Test seam.</summary>
        public string BannerText => _banner != null && _banner.Visible ? _banner.Text : "";

        /// <summary>Banner colour currently in force. Test seam.</summary>
        public Color BannerColor => _banner != null
            ? _banner.GetThemeColor("font_color")
            : default;

        /// <summary>Banner type size currently in force. Test seam.</summary>
        public int BannerFontSize => _banner?.GetThemeFontSize("font_size") ?? 0;

        /// <summary>Whether the stamp plate is behind the banner. Test seam.</summary>
        public bool PlateVisible => _plate != null && _plate.Visible && _plate.SelfModulate.A > 0f;

        /// <summary>Current scrim alpha. Test seam.</summary>
        public float DimAlpha => _dim != null && _dim.Visible ? _dim.Color.A : 0f;

        private void OnFighterPresentation(FighterPresentationPayload payload) => Present(payload);

        /// <summary>
        /// Renders one presentation beat. Separated from the event handler so a
        /// test can drive every phase without an <see cref="EventBus"/> round trip.
        /// </summary>
        public void Present(in FighterPresentationPayload payload) {
            float dim = FighterOverlayModel.DimAlphaFor(payload.Phase);
            if (dim >= 0f) SetDim(dim);

            if (FighterOverlayModel.ClearsBanner(payload.Phase)) {
                HideBanner();
                return;
            }

            if (!FighterOverlayModel.TryResolveBanner(in payload, out FighterOverlayBeat beat)) return;

            if (payload.Phase == FighterPresentationPhase.Countdown) {
                AudioManager.Instance?.PlayCountdownBlip(
                    FighterOverlayModel.CountdownBlipPitch(payload.CountdownValue));
            }

            ShowBanner(BannerCopy(in payload, in beat), in beat);
        }

        private string BannerCopy(in FighterPresentationPayload payload, in FighterOverlayBeat beat) =>
            beat.Style switch {
                FighterBannerStyle.CountdownNumeral =>
                    string.Format(Tr(beat.BannerKey), payload.CountdownValue),
                FighterBannerStyle.Winner =>
                    string.Format(Tr(beat.BannerKey), payload.WinnerPlayerID + 1),
                _ => Tr(beat.BannerKey)
            };

        private void ShowBanner(string text, in FighterOverlayBeat beat) {
            if (_banner == null) return;
            _banner.Text = text;
            _banner.AddThemeColorOverride("font_color", beat.Color);
            _banner.AddThemeFontSizeOverride("font_size", beat.FontSize);
            _banner.Visible = true;
            if (_plate != null) {
                _plate.Visible = true;
                // The plate is always in the tree so the banner keeps its centred
                // parent; transparency, not visibility, is what removes it.
                _plate.SelfModulate = new Color(1f, 1f, 1f, beat.ShowsPlate ? 1f : 0f);
            }
            _bannerSecondsRemaining = beat.Seconds;
        }

        private void HideBanner() {
            if (_banner != null) _banner.Visible = false;
            if (_plate != null) _plate.Visible = false;
            _bannerSecondsRemaining = 0.0;
        }

        private void SetDim(float alpha) {
            if (_dim == null) return;
            _dim.Color = new Color(UIPalette.NavyDeep.R, UIPalette.NavyDeep.G, UIPalette.NavyDeep.B, alpha);
            _dim.Visible = alpha > 0f;
        }
    }
}
