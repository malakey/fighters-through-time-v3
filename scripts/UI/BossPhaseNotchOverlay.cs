using System.Collections.Generic;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 8 B1. Draws the phase notches across the Story HUD's boss bar and
    /// flashes the bar when a phase is actually crossed.
    ///
    /// It sits over the <see cref="ProgressBar"/> as a full-rect, input-transparent
    /// child, so the notches follow the bar's layout automatically instead of
    /// duplicating its geometry. The positions come from
    /// <see cref="BossBarPhaseNotches"/>, which reads the authored
    /// <c>BossData.PhaseThresholds</c> — the same array
    /// <c>BossController.CheckPhaseTransition</c> uses, so a notch cannot disagree
    /// with the fight.
    /// </summary>
    public partial class BossPhaseNotchOverlay : Control {

        /// <summary>Seconds a phase-transition flash lasts.</summary>
        public const float FlashSeconds = 0.6f;

        private const float NotchWidth = 3f;

        private readonly List<float> _positions = new();
        private float _flashTimer;
        private int _crossedPhases;

        /// <summary>Normalised notch positions currently drawn, highest first.</summary>
        public IReadOnlyList<float> Positions => _positions;

        /// <summary>Phases crossed so far; the notches behind them draw dimmed.</summary>
        public int CrossedPhases => _crossedPhases;

        /// <summary>True while a transition flash is playing.</summary>
        public bool IsFlashing => _flashTimer > 0f;

        public override void _Ready() {
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.FullRect);
            SetProcess(false);
        }

        /// <summary>Rebuilds the notches for a boss's authored thresholds.</summary>
        public void SetThresholds(IReadOnlyList<float> thresholds) {
            _positions.Clear();
            _positions.AddRange(BossBarPhaseNotches.Normalized(thresholds));
            _crossedPhases = 0;
            _flashTimer = 0f;
            SetProcess(false);
            Modulate = Colors.White;
            QueueRedraw();
        }

        /// <summary>Clears every notch, for a bar being hidden or reused.</summary>
        public void Clear() => SetThresholds(null);

        /// <summary>
        /// Marks a phase as crossed and starts the flash. Driven from
        /// <c>EventBus.OnBossPhaseChanged</c>, which carries the new phase index.
        /// </summary>
        public void NotifyPhaseChanged(int phaseIndex) {
            _crossedPhases = Mathf.Clamp(phaseIndex, 0, _positions.Count);
            _flashTimer = FlashSeconds;
            SetProcess(true);
            QueueRedraw();
        }

        public override void _Process(double delta) {
            if (_flashTimer <= 0f) return;
            _flashTimer -= (float)delta;
            if (_flashTimer <= 0f) {
                _flashTimer = 0f;
                Modulate = Colors.White;
                SetProcess(false);
                QueueRedraw();
                return;
            }
            // A short, decaying brighten rather than a blink: the bar has to stay
            // readable through the transition, which is exactly when the player is
            // looking at it.
            float strength = _flashTimer / FlashSeconds;
            Modulate = Colors.White.Lerp(UIPalette.GoldBright, strength);
            QueueRedraw();
        }

        public override void _Draw() {
            if (_positions.Count == 0) return;
            float width = Size.X;
            float height = Size.Y;
            if (width <= 0f || height <= 0f) return;

            for (int index = 0; index < _positions.Count; index++) {
                float x = _positions[index] * width;
                // Notches already passed dim down so the remaining fight reads at a
                // glance; the upcoming ones stay bright.
                Color color = index < _crossedPhases
                    ? new Color(UIPalette.SlateDim, 0.55f)
                    : new Color(UIPalette.NavyDeep, 0.95f);
                DrawRect(new Rect2(x - NotchWidth * 0.5f, 0f, NotchWidth, height), color);
            }
        }
    }
}
