using Godot;

namespace FTT.UI.Motion {

    /// <summary>
    /// UR-THEME. The MOT-B button focus pose (UI_SPEC 3.2 and section 5, rows 1-3): on focus or
    /// mouse-over the plate moves +16 px, tilts -1.2 degrees and scales 1.04 about its centre with a
    /// back-eased 0.14 s tween (peak about 1.044 and 1.3 degrees), and undoes it in 0.10 s on blur.
    /// Pressing adds a +7,+7 travel with no tween.
    ///
    /// <para><b>Layout-safe.</b> The pose writes only <c>Position</c>, <c>Rotation</c>, <c>Scale</c>
    /// and <c>ZIndex</c> of its target, never a minimum size, so it cannot disturb a
    /// <see cref="FocusChainBuilder"/> chain; hit-testing follows the transform, as the spec expects.
    /// A <see cref="Container"/> resets a child's position, rotation and scale each time it sorts, so
    /// when the target sits in one the pose re-bases and re-applies itself after every
    /// <c>SortChildren</c>. For a target that must never be touched by a container, pass a
    /// <paramref name="visual"/> child (a plate drawn inside the button) instead.</para>
    ///
    /// <para>Under Reduced Temporal Effects the pose is applied instantly with no overshoot. Tweens
    /// run with the process pause mode, so a paused tree does not strand a half-posed button; the
    /// pose is always written back to exact identity when it ends.</para>
    /// </summary>
    public partial class FocusPose : Node {

        /// <summary>The focused pose's translation in design pixels.</summary>
        public Vector2 FocusOffset { get; set; } = new(PulpMotionRules.FocusTranslateX, 0f);
        public float TiltDegrees { get; set; } = PulpMotionRules.FocusTiltDegrees;
        public float PoseScale { get; set; } = PulpMotionRules.FocusScale;

        private Control _host;
        private Control _target;
        private Tween _tween;
        private Vector2 _basePosition;
        private float _baseRotation;
        private Vector2 _baseScale = Vector2.One;
        private int _baseZ;
        private float _t;                  // 0 identity, 1 the full pose (the back ease overshoots a little)
        private bool _focused;
        private bool _hovered;
        private bool _pressed;
        private bool _capturedBase;

        /// <summary>True while the pose is (or is easing toward) the focused state.</summary>
        public bool PoseActive => _focused || _hovered;

        /// <summary>The control being posed.</summary>
        public Control Target => _target;

        /// <summary>
        /// Attaches a pose to <paramref name="host"/>. <paramref name="visual"/> is the control to pose
        /// (default: the host itself). Returns the node, which is a child of the host.
        /// </summary>
        public static FocusPose Attach(Control host, Control visual = null) {
            var pose = new FocusPose { Name = "FocusPose" };
            pose._host = host;
            pose._target = visual ?? host;
            host.AddChild(pose);
            return pose;
        }

        public override void _Ready() {
            ProcessMode = ProcessModeEnum.Always;
            _host ??= GetParent() as Control;
            _target ??= _host;
            if (_host == null) return;

            _host.FocusEntered += OnFocusEntered;
            _host.FocusExited += OnFocusExited;
            _host.MouseEntered += OnMouseEntered;
            _host.MouseExited += OnMouseExited;
            _host.VisibilityChanged += OnVisibilityChanged;
            _target.Resized += OnTargetResized;
            if (_host is BaseButton button) {
                button.ButtonDown += OnButtonDown;
                button.ButtonUp += OnButtonUp;
            }
            if (_target.GetParent() is Container container) container.SortChildren += OnContainerSorted;
            OnTargetResized();
            CaptureBase();
        }

        public override void _ExitTree() {
            _tween?.Kill();
            if (_host == null || !GodotObject.IsInstanceValid(_host)) return;
            _host.FocusEntered -= OnFocusEntered;
            _host.FocusExited -= OnFocusExited;
            _host.MouseEntered -= OnMouseEntered;
            _host.MouseExited -= OnMouseExited;
            _host.VisibilityChanged -= OnVisibilityChanged;
            if (_target != null && GodotObject.IsInstanceValid(_target)) {
                _target.Resized -= OnTargetResized;
                if (_target.GetParent() is Container container) container.SortChildren -= OnContainerSorted;
            }
            if (_host is BaseButton button) {
                button.ButtonDown -= OnButtonDown;
                button.ButtonUp -= OnButtonUp;
            }
        }

        private void CaptureBase() {
            _basePosition = _target.Position;
            _baseRotation = _target.Rotation;
            _baseScale = _target.Scale;
            _baseZ = _target.ZIndex;
            _capturedBase = true;
        }

        private void OnTargetResized() => _target.PivotOffset = _target.Size / 2f;

        private void OnContainerSorted() {
            // The container just reset the target to its slot; that is the new resting pose.
            CaptureBase();
            if (_t != 0f || _pressed) Apply(_t);
        }

        private bool HostDisabled => _host is BaseButton b && b.Disabled;

        private void OnFocusEntered() { _focused = true; Refresh(); }
        private void OnFocusExited() { _focused = false; Refresh(); }
        private void OnMouseEntered() { _hovered = !HostDisabled; Refresh(); }
        private void OnMouseExited() { _hovered = false; Refresh(); }

        private void OnVisibilityChanged() {
            if (_host.IsVisibleInTree()) return;
            _focused = _hovered = _pressed = false;
            _tween?.Kill();
            Settle(0f);
        }

        private void OnButtonDown() { _pressed = true; Apply(_t); }

        private void OnButtonUp() {
            // Released on the next frame after the release, with no tween.
            CallDeferred(nameof(ReleasePress));
        }

        private void ReleasePress() { _pressed = false; Apply(_t); }

        private void Refresh() {
            float goal = PoseActive ? 1f : 0f;
            _tween?.Kill();
            if (!_capturedBase || _t == 0f) CaptureBase();
            if (PulpMotion.Reduced) {
                Settle(goal);
                return;
            }
            float seconds = goal > _t ? PulpMotionRules.FocusInSeconds : PulpMotionRules.FocusOutSeconds;
            _tween = PulpMotion.NewTween(this);
            var step = _tween.TweenMethod(Callable.From<float>(Apply), _t, goal, seconds);
            if (goal > 0f) step.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            else step.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            _tween.TweenCallback(Callable.From(() => Settle(goal)));
        }

        /// <summary>Writes the pose at its resting value; identity is written exactly.</summary>
        private void Settle(float t) {
            Apply(t);
            if (t == 0f && !_pressed) {
                _target.Position = _basePosition;
                _target.Rotation = _baseRotation;
                _target.Scale = _baseScale;
                _target.ZIndex = _baseZ;
            }
        }

        /// <summary>Applies the pose at progress <paramref name="t"/> (identity at 0, the full pose at 1).</summary>
        public void Apply(float t) {
            if (!GodotObject.IsInstanceValid(_target)) return;
            _t = t;
            Vector2 press = _pressed ? new Vector2(PulpMotionRules.PressTranslate, PulpMotionRules.PressTranslate) : Vector2.Zero;
            _target.Position = _basePosition + FocusOffset * t + press;
            _target.Rotation = _baseRotation + Mathf.DegToRad(TiltDegrees) * t;
            _target.Scale = _baseScale * (1f + (PoseScale - 1f) * t);
            _target.ZIndex = _baseZ + (PoseActive || t > 0.01f ? 1 : 0);
        }
    }
}
