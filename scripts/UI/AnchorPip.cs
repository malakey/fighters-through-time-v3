using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// One Act III Beacon anchor charge on the Story HUD (M15, Package 12 W10).
    ///
    /// <para>HUD_CONTRACT: an <c>8 × 8 px</c> gold <b>diamond</b> — a shape distinct
    /// from the round rewind glyph, so the count reads without colour. An Anchor
    /// Snap <b>cracks and fades</b> the spent pip; a pip that was already spent on
    /// load or bind is drawn spent without replaying the crack.</para>
    ///
    /// <para>Code-built by <see cref="StoryHUD"/> inside the authored
    /// <c>BeaconAnchors/AnchorPips</c> row, because the pip count is data-driven
    /// (Easy 3 / Normal 2 / Hard 1). Presentation only; it never reads or writes
    /// the charge count itself.</para>
    /// </summary>
    public partial class AnchorPip : Control {
        public enum PipState {
            /// <summary>A remaining anchor charge.</summary>
            Filled,
            /// <summary>Spent by a Snap this session: the crack-and-fade beat is playing.</summary>
            Cracking,
            /// <summary>Spent: a faint diamond outline.</summary>
            Spent
        }

        /// <summary>
        /// The pip's edge length, in reference pixels: 18 since the pulp UI redesign (UR-HUDS, the
        /// HUD_CONTRACT's 8 px at about twice the size, drawn with the kit's <c>anchor_pip</c> icons).
        /// </summary>
        public const int PipSize = 18;

        /// <summary>The Beacon icon's edge length (24 before UR-HUDS), the 46 px badge size of the time tools.</summary>
        public const int IconSize = 46;

        /// <summary>Length of the crack-and-fade beat.</summary>
        public const float CrackSeconds = 0.6f;

        /// <summary>Opacity a spent pip settles at, so the capacity stays legible.</summary>
        public const float SpentAlpha = 0.22f;

        private PipState _state = PipState.Filled;
        private float _crackElapsed;

        public PipState State => _state;

        /// <summary>0 at the start of the crack, 1 once it has fully faded.</summary>
        public float CrackProgress => _state == PipState.Spent ? 1f
            : _state == PipState.Cracking ? Mathf.Clamp(_crackElapsed / CrackSeconds, 0f, 1f)
            : 0f;

        public Color FillColor { get; set; } = UIPalette.GoldBright;

        private readonly PulpIcon _icon;

        public AnchorPip() {
            CustomMinimumSize = new Vector2(PipSize, PipSize);
            MouseFilter = MouseFilterEnum.Ignore;
            SizeFlagsVertical = SizeFlags.ShrinkCenter;
            _icon = PulpIcon.Create("anchor_pip", FillColor, PipSize);
            AddChild(_icon);
            UpdateIcon();
        }

        private void UpdateIcon() {
            if (_icon == null) return;
            bool cracking = _state == PipState.Cracking;
            _icon.IconName = cracking ? "anchor_pip_cracked" : "anchor_pip";
            _icon.Tint = FillColor;
            float alpha = _state switch {
                PipState.Filled => 1f,
                PipState.Cracking => Mathf.Lerp(1f, SpentAlpha, CrackProgress),
                _ => SpentAlpha
            };
            _icon.Modulate = new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>Filled or spent, with no animation (bind, load, refill).</summary>
        public void SetFilled(bool filled) {
            _state = filled ? PipState.Filled : PipState.Spent;
            _crackElapsed = 0f;
            SetProcess(false);
            UpdateIcon();
        }

        /// <summary>An Anchor Snap spent this charge: crack, then fade to spent.</summary>
        public void Crack() {
            if (_state != PipState.Filled) return;
            _state = PipState.Cracking;
            _crackElapsed = 0f;
            SetProcess(true);
            UpdateIcon();
        }

        /// <summary>Advances the crack beat. Public so tests can drive it headlessly.</summary>
        public void Advance(float seconds) {
            if (_state != PipState.Cracking) return;
            _crackElapsed += Mathf.Max(0f, seconds);
            if (_crackElapsed >= CrackSeconds) {
                _state = PipState.Spent;
                SetProcess(false);
            }
            UpdateIcon();
        }

        public override void _Ready() => SetProcess(_state == PipState.Cracking);

        public override void _Process(double delta) => Advance((float)delta);
    }
}
