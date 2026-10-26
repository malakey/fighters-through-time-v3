using System;
using FTT.Core;
using Godot;

namespace FTT.UI.Motion {

    /// <summary>
    /// UR-THEME. The MOT-B screen change (UI_SPEC section 5, row 5): a full-screen accent slab with a
    /// slanted trailing edge and a 6 px ink edge slides across in 8 discrete steps (0.26 s), the scene
    /// swaps while it fully covers the screen, and 8 more steps carry it out the other side.
    /// Under Reduced Temporal Effects there is no slab: an ink-black cross-fade of 0.2 s each way.
    ///
    /// <para>It lives on a <see cref="CanvasLayer"/> above the loading screen (layer 110, the loading
    /// screens are 100) and under <c>GameManager</c>, runs at <see cref="Node.ProcessModeEnum.Always"/>
    /// and drives its tween with the process pause mode, so a paused tree cannot strand it covering
    /// the screen. It swallows mouse input only while it is moving, never while idle.</para>
    ///
    /// <para><see cref="GameManager.LoadScene"/> uses it: the slab covers and uncovers the loading
    /// screen at the start, and the cover starts <see cref="CoverSeconds"/> before the two-second
    /// minimum display time ends, so the minimum is never lengthened.</para>
    /// </summary>
    public partial class ScreenWipe : CanvasLayer {

        public const int WipeLayer = 110;

        private SlabControl _slab;
        private Tween _tween;
        private bool _covering;
        private bool _covered;

        /// <summary>Raised the frame the screen is fully covered (the moment to swap the scene).</summary>
        public event Action Covered;

        /// <summary>Raised when a reveal finished and the layer is idle again.</summary>
        public event Action Revealed;

        /// <summary>How long a cover takes (the wipe's per-direction time, or the fade under Reduced Temporal Effects).</summary>
        public float CoverSeconds => PulpMotion.Reduced ? PulpMotionRules.WipeFadeSeconds : PulpMotionRules.WipeSeconds;

        /// <summary>True from <see cref="BeginCover"/> until the reveal finishes.</summary>
        public bool IsActive => _covering || _covered || _revealing;

        /// <summary>True once the screen is fully covered and until the reveal starts.</summary>
        public bool IsCovered => _covered;

        private bool _revealing;

        /// <summary>Finds the wipe under <paramref name="host"/> or creates it there.</summary>
        public static ScreenWipe EnsureFor(Node host) {
            var existing = host.GetNodeOrNull<ScreenWipe>("ScreenWipe");
            if (existing != null) return existing;
            var wipe = new ScreenWipe { Name = "ScreenWipe" };
            host.AddChild(wipe);
            return wipe;
        }

        public override void _Ready() {
            Layer = WipeLayer;
            ProcessMode = ProcessModeEnum.Always;
            _slab = new SlabControl { Name = "Slab", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            _slab.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_slab);
        }

        public override void _ExitTree() => _tween?.Kill();

        /// <summary>Slides the slab (or fades the ink) in until the screen is covered, then raises <see cref="Covered"/>.</summary>
        public void BeginCover() {
            if (_covering || _covered) return;
            _tween?.Kill();
            _covering = true;
            _revealing = false;
            _slab.Visible = true;
            _slab.MouseFilter = Control.MouseFilterEnum.Stop;
            _slab.Reduced = PulpMotion.Reduced;
            _slab.Progress = -1f;
            _slab.Alpha = 0f;
            _slab.QueueRedraw();

            _tween = PulpMotion.NewTween(this);
            _tween.TweenMethod(Callable.From<float>(SetCoverProgress), 0f, 1f, CoverSeconds);
            _tween.TweenCallback(Callable.From(FinishCover));
        }

        /// <summary>Slides the slab out the far side (or fades the ink out); the layer then goes idle.</summary>
        public void BeginReveal() {
            if (!_covered && !_covering) return;
            _tween?.Kill();
            _covering = false;
            _covered = false;
            _revealing = true;
            _slab.Visible = true;
            _slab.Reduced = PulpMotion.Reduced;
            _tween = PulpMotion.NewTween(this);
            _tween.TweenMethod(Callable.From<float>(SetRevealProgress), 0f, 1f, CoverSeconds);
            _tween.TweenCallback(Callable.From(FinishReveal));
        }

        /// <summary>A cover that reveals as soon as it covers (the start of a load).</summary>
        public void PlayThrough() {
            void Once() { Covered -= Once; BeginReveal(); }
            Covered += Once;
            BeginCover();
        }

        /// <summary>Drops any motion and returns to idle at once (an aborted load must never leave the screen covered).</summary>
        public void Release() {
            _tween?.Kill();
            _covering = _covered = _revealing = false;
            if (_slab == null) return;
            _slab.Visible = false;
            _slab.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        private void SetCoverProgress(float progress) {
            if (_slab.Reduced) {
                _slab.Alpha = progress;
            } else {
                _slab.Progress = PulpMotionRules.WipeOffsetCover(progress);
            }
            _slab.QueueRedraw();
        }

        private void SetRevealProgress(float progress) {
            if (_slab.Reduced) {
                _slab.Alpha = 1f - progress;
            } else {
                _slab.Progress = PulpMotionRules.WipeOffsetReveal(progress);
            }
            _slab.QueueRedraw();
        }

        private void FinishCover() {
            _covering = false;
            _covered = true;
            SetCoverProgress(1f);
            Covered?.Invoke();
        }

        private void FinishReveal() {
            _revealing = false;
            _slab.Visible = false;
            _slab.MouseFilter = Control.MouseFilterEnum.Ignore;
            Revealed?.Invoke();
        }

        /// <summary>The slab itself: drawn, so it needs no texture.</summary>
        private partial class SlabControl : Control {
            /// <summary>The slab's left edge as a fraction of its width: -1 offscreen left, 0 covering, +1 offscreen right.</summary>
            public float Progress = -1f;
            public bool Reduced;
            public float Alpha;

            public override void _Draw() {
                Vector2 size = Size;
                if (Reduced) {
                    DrawRect(new Rect2(Vector2.Zero, size), new Color(UIPalette.InkBlack, Alpha));
                    return;
                }
                float ink = PulpMotionRules.WipeInkEdge;
                // Wide enough that the slanted trailing edge clears the screen at rest.
                float width = (size.X + ink * 2f) / PulpMotionRules.WipeSlantFraction;
                float x = Progress * width - ink;
                float slant = width * PulpMotionRules.WipeSlantFraction;
                var inkPoly = new[] {
                    new Vector2(x - ink, -ink), new Vector2(x + width + ink, -ink),
                    new Vector2(x + slant + ink, size.Y + ink), new Vector2(x - ink, size.Y + ink)
                };
                var fillPoly = new[] {
                    new Vector2(x, 0), new Vector2(x + width, 0),
                    new Vector2(x + slant, size.Y), new Vector2(x, size.Y)
                };
                DrawColoredPolygon(inkPoly, UIPalette.InkBlack);
                DrawColoredPolygon(fillPoly, UIPalette.Accent);
            }
        }
    }
}
