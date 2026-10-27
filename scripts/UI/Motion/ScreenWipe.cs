using System;
using FTT.Core;
using Godot;

namespace FTT.UI.Motion {

    /// <summary>
    /// TG-THEME (first written by UR-THEME as the pulp diagonal wipe; the class, node name and API are kept). The
    /// MOT-A screen change (UI_SPEC_GLASS section 5, row 6): a full-screen layer in the backdrop colour (bg0, #05071A)
    /// fades in over 0.2 s (Sine InOut), the scene swaps while it fully covers the screen, then it fades out over 0.2 s.
    /// A cross-dissolve through bg0 is already a stable cue, so Reduced Temporal Effects plays the same fade; the
    /// spec's soft blur is not available on <c>gl_compatibility</c>, and the demo's gold-edged sweep is not used (AC21).
    ///
    /// <para>It lives on a <see cref="CanvasLayer"/> above the loading screen (layer 110, the loading screens are 100)
    /// and under <c>GameManager</c>, runs at <see cref="Node.ProcessModeEnum.Always"/> and drives its tween with the
    /// process pause mode, so a paused tree cannot strand it covering the screen. It swallows mouse input only while
    /// it is moving or covering, never while idle. Its layer is the child named <c>Slab</c>.</para>
    ///
    /// <para><see cref="GameManager.LoadScene"/> uses it: the dissolve covers and uncovers the loading screen at the
    /// start, and the cover starts <see cref="CoverSeconds"/> before the two-second minimum display time ends, so the
    /// minimum is never lengthened.</para>
    /// </summary>
    public partial class ScreenWipe : CanvasLayer {

        public const int WipeLayer = 110;

        private ColorRect _slab;
        private Tween _tween;
        private bool _covering;
        private bool _covered;
        private bool _revealing;

        /// <summary>Raised the frame the screen is fully covered (the moment to swap the scene).</summary>
        public event Action Covered;

        /// <summary>Raised when a reveal finished and the layer is idle again.</summary>
        public event Action Revealed;

        /// <summary>How long a cover (or a reveal) takes: the dissolve's 0.2 s, the same under Reduced Temporal Effects.</summary>
        public float CoverSeconds => PulpMotionRules.DissolveSeconds;

        /// <summary>True from <see cref="BeginCover"/> until the reveal finishes.</summary>
        public bool IsActive => _covering || _covered || _revealing;

        /// <summary>True once the screen is fully covered and until the reveal starts.</summary>
        public bool IsCovered => _covered;

        /// <summary>The dissolve layer's current opacity (0 idle, 1 covering the screen).</summary>
        public float Opacity => _slab == null ? 0f : _slab.Color.A;

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
            _slab = new ColorRect {
                Name = "Slab", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore,
                Color = new Color(UIPalette.Backdrop, 0f)
            };
            _slab.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_slab);
        }

        public override void _ExitTree() => _tween?.Kill();

        /// <summary>Fades the layer in until the screen is covered, then raises <see cref="Covered"/>.</summary>
        public void BeginCover() {
            if (_covering || _covered) return;
            _tween?.Kill();
            _covering = true;
            _revealing = false;
            _slab.Visible = true;
            _slab.MouseFilter = Control.MouseFilterEnum.Stop;
            float from = Opacity;
            SetOpacity(from);
            _tween = PulpMotion.NewTween(this);
            _tween.TweenMethod(Callable.From<float>(SetOpacity), from, 1f, CoverSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            _tween.TweenCallback(Callable.From(FinishCover));
        }

        /// <summary>Fades the layer out; the layer then goes idle.</summary>
        public void BeginReveal() {
            if (!_covered && !_covering) return;
            _tween?.Kill();
            _covering = false;
            _covered = false;
            _revealing = true;
            _slab.Visible = true;
            _tween = PulpMotion.NewTween(this);
            _tween.TweenMethod(Callable.From<float>(SetOpacity), Opacity, 0f, CoverSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
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
            SetOpacity(0f);
            _slab.Visible = false;
            _slab.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        private void SetOpacity(float alpha) {
            Color c = _slab.Color;
            if (c.A == alpha) return;
            c.A = alpha;
            _slab.Color = c;
        }

        private void FinishCover() {
            _covering = false;
            _covered = true;
            SetOpacity(1f);
            Covered?.Invoke();
        }

        private void FinishReveal() {
            _revealing = false;
            SetOpacity(0f);
            _slab.Visible = false;
            _slab.MouseFilter = Control.MouseFilterEnum.Ignore;
            Revealed?.Invoke();
        }
    }
}
