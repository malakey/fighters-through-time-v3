using Godot;

namespace FTT.UI {

    /// <summary>
    /// Placeholder consumer for Chronal Rewind presentation events: cyan screen
    /// tint, scanline strips, and music ducking through the audio bus. Production
    /// post-processing replaces the visuals through this same event contract.
    /// </summary>
    public partial class RewindPresentationOverlay : CanvasLayer {
        private ColorRect _tint;
        private Control _scanlines;
        private bool _musicDucked;

        public override void _Ready() {
            Layer = 80;
            ProcessMode = ProcessModeEnum.Always;
            Visible = false;

            _tint = new ColorRect { Name = "Tint", Color = new Color(0f, 0.85f, 0.9f, 0.18f) };
            _tint.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _tint.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(_tint);

            _scanlines = new Control { Name = "Scanlines", MouseFilter = Control.MouseFilterEnum.Ignore };
            _scanlines.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_scanlines);
            for (int index = 0; index < 27; index++) {
                var line = new ColorRect {
                    Color = new Color(0f, 0.9f, 0.95f, 0.08f),
                    Position = new Vector2(0, index * 40),
                    Size = new Vector2(1920, 3),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                _scanlines.AddChild(line);
            }

            if (FTT.Core.EventBus.Instance != null)
                FTT.Core.EventBus.Instance.OnRewindPresentation += OnRewindPresentation;
        }

        public override void _ExitTree() {
            if (FTT.Core.EventBus.Instance != null)
                FTT.Core.EventBus.Instance.OnRewindPresentation -= OnRewindPresentation;
            if (_musicDucked) SetMusicDuck(0f);
        }

        private void OnRewindPresentation(FTT.Core.RewindPresentationPayload payload) {
            bool active = payload.Phase == FTT.Core.RewindPresentationPhase.Started
                || payload.Phase == FTT.Core.RewindPresentationPhase.Playback
                || payload.Phase == FTT.Core.RewindPresentationPhase.TimelineCollapse;
            Visible = active && (payload.ScreenTintEnabled || payload.ScanlinesEnabled);
            if (_tint != null) _tint.Visible = payload.ScreenTintEnabled && active;
            if (_scanlines != null) _scanlines.Visible = payload.ScanlinesEnabled && active;
            SetMusicDuck(active ? payload.MusicDuckDecibels : 0f);
        }

        /// <summary>
        /// Ducks music for the rewind through the shared snapshot layer.
        ///
        /// <para>This used to snapshot the Music bus's absolute dB on entry and write
        /// it back on exit, which quietly reverted any volume change the player made
        /// mid-rewind and fought anything else that touched the bus. The snapshot
        /// mixer instead holds the duck as an <em>offset</em> over whatever base
        /// volume the settings own, and tweens it in and out.</para>
        ///
        /// <para>The authored depth still wins: <c>MusicDuckDecibels</c> from the
        /// rewind payload is passed through rather than using the snapshot's
        /// default.</para>
        /// </summary>
        private void SetMusicDuck(float decibels) {
            FTT.Core.AudioManager audio = FTT.Core.AudioManager.Instance;
            if (audio == null) return;
            if (decibels != 0f) {
                audio.ApplySnapshot(FTT.Core.AudioSnapshot.Rewind, decibels);
                _musicDucked = true;
            } else if (_musicDucked) {
                audio.ReleaseSnapshot(FTT.Core.AudioSnapshot.Rewind);
                _musicDucked = false;
            }
        }
    }
}
