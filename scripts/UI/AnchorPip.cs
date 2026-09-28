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

        /// <summary>The contract's pip edge length, in reference pixels.</summary>
        public const int PipSize = 8;

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

        /// <summary>An Anchor Snap spent this charge: crack, then fade to spent.</summary>
        public void Crack() {
            if (_state != PipState.Filled) return;
            _state = PipState.Cracking;
            _crackElapsed = 0f;
            SetProcess(true);
            QueueRedraw();
        }

        /// <summary>Advances the crack beat. Public so tests can drive it headlessly.</summary>
        public void Advance(float seconds) {
            if (_state != PipState.Cracking) return;
            _crackElapsed += Mathf.Max(0f, seconds);
            if (_crackElapsed >= CrackSeconds) {
                _state = PipState.Spent;
                SetProcess(false);
            }
            QueueRedraw();
        }

        public override void _Ready() => SetProcess(_state == PipState.Cracking);

        public override void _Process(double delta) => Advance((float)delta);

        public override void _Draw() {
            Vector2 size = Size.X > 0f && Size.Y > 0f ? Size : CustomMinimumSize;
            float half = Mathf.Min(size.X, size.Y) * 0.5f;
            var centre = new Vector2(size.X * 0.5f, size.Y * 0.5f);
            Vector2[] diamond = {
                centre + new Vector2(0f, -half),
                centre + new Vector2(half, 0f),
                centre + new Vector2(0f, half),
                centre + new Vector2(-half, 0f)
            };
            switch (_state) {
                case PipState.Filled:
                    DrawColoredPolygon(diamond, FillColor);
                    break;
                case PipState.Cracking: {
                    float t = CrackProgress;
                    Color fill = FillColor;
                    fill.A = Mathf.Lerp(1f, SpentAlpha, t);
                    DrawColoredPolygon(diamond, fill);
                    // The crack: a jagged line across the diamond, darkest at the start.
                    var crack = new Color(0.05f, 0.05f, 0.1f, 1f - t * 0.6f);
                    DrawPolyline(new[] {
                        centre + new Vector2(-half * 0.2f, -half * 0.9f),
                        centre + new Vector2(half * 0.15f, -half * 0.15f),
                        centre + new Vector2(-half * 0.15f, half * 0.2f),
                        centre + new Vector2(half * 0.1f, half * 0.9f)
                    }, crack, 1f);
                    break;
                }
                default: {
                    Color outline = FillColor;
                    outline.A = SpentAlpha;
                    var closed = new Vector2[diamond.Length + 1];
                    diamond.CopyTo(closed, 0);
                    closed[diamond.Length] = diamond[0];
                    DrawPolyline(closed, outline, 1f);
                    break;
                }
            }
        }
    }
}
