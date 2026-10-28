using FTT.UI.Motion;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// One Act III Beacon anchor charge on the Story HUD (M15, Package 12 W10).
    ///
    /// <para>HUD_CONTRACT: a gold <b>diamond</b>, a shape distinct from the round rewind glyph, so the count reads without
    /// colour. Since the Temporal Glass rebuild (TG-HUDS, UI_SPEC_GLASS 3.4.4) the pip draws itself: a 10 x 10 diamond
    /// (18 x 18 until the compact Story HUD of 2026-10-27) filled <c>res</c> while the charge remains, a 1.5 px <c>ink3</c> outline once
    /// spent. An Anchor Snap <b>splits and fades</b> the spent pip over 600 ms (MOT-A row 18): its two halves part and fade
    /// while the spent outline comes up; under Reduced Temporal Effects a plain 150 ms fade. A pip that was already spent on
    /// load or bind is drawn spent without replaying the split.</para>
    ///
    /// <para>Code-built by <see cref="StoryHUD"/> inside the authored <c>BeaconAnchors/AnchorPips</c> row, because the pip
    /// count is data-driven (Easy 3 / Normal 2 / Hard 1). Presentation only; it never reads or writes the charge count
    /// itself. The diamond's points are fixed; a draw allocates nothing.</para>
    /// </summary>
    public partial class AnchorPip : Control {
        public enum PipState {
            /// <summary>A remaining anchor charge.</summary>
            Filled,
            /// <summary>Spent by a Snap this session: the split-and-fade beat is playing.</summary>
            Cracking,
            /// <summary>Spent: a faint diamond outline.</summary>
            Spent
        }

        /// <summary>
        /// The pip's edge length, in reference pixels: the 10 px diamond of the compact Story HUD (2026-10-27; 18 from the
        /// 2026-10-26 redesign, 8 before it).
        /// </summary>
        public const int PipSize = 10;

        /// <summary>The Beacon badge's edge length: the compact Story HUD's 26 px time-tool badge (46 from 2026-10-26, 24 before).</summary>
        public const int IconSize = 26;

        /// <summary>Length of the split-and-fade beat (MOT-A row 18); <see cref="PulpMotionRules.AnchorSplitReducedSeconds"/> under Reduced Temporal Effects.</summary>
        public const float CrackSeconds = PulpMotionRules.AnchorSplitSeconds;

        /// <summary>The spent outline's width.</summary>
        public const float OutlineWidth = 1.5f;

        /// <summary>How far each half of a splitting pip travels, in pixels.</summary>
        public const float SplitPixels = 2.5f;

        private PipState _state = PipState.Filled;
        private float _crackElapsed;
        private float _crackDuration = CrackSeconds;
        private bool _crackSplits = true;
        private Color _fillColor = UIPalette.Res;

        private static readonly Vector2[] Diamond = {
            new(PipSize / 2f, 0f), new(PipSize, PipSize / 2f), new(PipSize / 2f, PipSize), new(0f, PipSize / 2f)
        };
        private static readonly Vector2[] LeftHalf = { new(PipSize / 2f, 0f), new(PipSize / 2f, PipSize), new(0f, PipSize / 2f) };
        private static readonly Vector2[] RightHalf = { new(PipSize / 2f, 0f), new(PipSize, PipSize / 2f), new(PipSize / 2f, PipSize) };
        private static readonly Vector2[] Outline = BuildOutline();
        private readonly Vector2[] _left = new Vector2[3];
        private readonly Vector2[] _right = new Vector2[3];

        private static Vector2[] BuildOutline() {
            // The stroke's centre line sits half the stroke inside the diamond, closed.
            float inset = OutlineWidth / 2f * Mathf.Sqrt2;
            float c = PipSize / 2f;
            return new[] {
                new Vector2(c, inset), new Vector2(PipSize - inset, c), new Vector2(c, PipSize - inset),
                new Vector2(inset, c), new Vector2(c, inset)
            };
        }

        public PipState State => _state;

        /// <summary>0 at the start of the split, 1 once it has fully faded.</summary>
        public float CrackProgress => _state == PipState.Spent ? 1f
            : _state == PipState.Cracking ? Mathf.Clamp(_crackElapsed / _crackDuration, 0f, 1f)
            : 0f;

        /// <summary>True while a split (not the Reduced fade) is playing or was last played.</summary>
        public bool CrackSplits => _crackSplits;

        /// <summary>The filled pip's colour (<c>res</c>).</summary>
        public Color FillColor { get => _fillColor; set { _fillColor = value; QueueRedraw(); } }

        public AnchorPip() {
            CustomMinimumSize = new Vector2(PipSize, PipSize);
            MouseFilter = MouseFilterEnum.Ignore;
            SizeFlagsVertical = SizeFlags.ShrinkCenter;
        }

        /// <summary>Filled or spent, with no animation (bind, load, refill).</summary>
        public void SetFilled(bool filled) {
            _state = filled ? PipState.Filled : PipState.Spent;
            _crackElapsed = 0f;
            SetProcess(false);
            QueueRedraw();
        }

        /// <summary>An Anchor Snap spent this charge: split and fade to spent (a plain fade under Reduced Temporal Effects).</summary>
        public void Crack() {
            if (_state != PipState.Filled) return;
            _state = PipState.Cracking;
            _crackElapsed = 0f;
            _crackSplits = !PulpMotion.Reduced;
            _crackDuration = _crackSplits ? PulpMotionRules.AnchorSplitSeconds : PulpMotionRules.AnchorSplitReducedSeconds;
            SetProcess(true);
            QueueRedraw();
        }

        /// <summary>Advances the split beat. Public so tests can drive it headlessly.</summary>
        public void Advance(float seconds) {
            if (_state != PipState.Cracking) return;
            _crackElapsed += Mathf.Max(0f, seconds);
            if (_crackElapsed >= _crackDuration) {
                _state = PipState.Spent;
                SetProcess(false);
            }
            QueueRedraw();
        }

        public override void _Ready() => SetProcess(_state == PipState.Cracking);

        public override void _Process(double delta) => Advance((float)delta);

        public override void _Draw() {
            switch (_state) {
                case PipState.Filled:
                    DrawColoredPolygon(Diamond, _fillColor);
                    return;
                case PipState.Spent:
                    DrawPolyline(Outline, UIPalette.Ink3, OutlineWidth, true);
                    return;
            }
            float p = CrackProgress;
            DrawPolyline(Outline, new Color(UIPalette.Ink3, p), OutlineWidth, true);
            var fading = new Color(_fillColor, _fillColor.A * (1f - p));
            if (!_crackSplits) {
                DrawColoredPolygon(Diamond, fading);
                return;
            }
            float shift = SplitPixels * PulpMotionRules.CubicOut(p);
            for (int i = 0; i < 3; i++) {
                _left[i] = LeftHalf[i] + new Vector2(-shift, 0f);
                _right[i] = RightHalf[i] + new Vector2(shift, 0f);
            }
            DrawColoredPolygon(_left, fading);
            DrawColoredPolygon(_right, fading);
        }
    }
}
