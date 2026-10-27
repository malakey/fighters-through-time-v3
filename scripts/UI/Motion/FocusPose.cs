using Godot;

namespace FTT.UI.Motion {

    /// <summary>
    /// TG-THEME (first written by UR-THEME). The MOT-A button focus pose (UI_SPEC_GLASS 3.2 and section 5, rows 1-3):
    /// on focus or mouse-over the row slides +14 px over 120 ms (Quad Out) with no tilt and no scale, and slides back
    /// over 120 ms on blur. Pressing applies at once a +6 px slide and a 0.985 scale about the centre (from focus the row
    /// moves 8 px back); releasing returns to the focus state over 120 ms.
    ///
    /// <para><b>Layout-safe.</b> The pose writes only <c>Position</c>, <c>Rotation</c>, <c>Scale</c> and <c>ZIndex</c>
    /// of its target, never a minimum size, so it cannot disturb a <see cref="FocusChainBuilder"/> chain; hit-testing
    /// follows the transform. A <see cref="Container"/> resets a child's position, rotation and scale each time it
    /// sorts, so when the target sits in one the pose re-bases and re-applies itself after every <c>SortChildren</c>.
    /// For a target that must never be touched by a container, pass a <paramref name="visual"/> child instead.</para>
    ///
    /// <para>Under Reduced Temporal Effects the pose is applied at once (the position cue stays). Tweens run with the
    /// process pause mode, so a paused tree does not strand a half-posed button; the pose is always written back to exact
    /// identity when it ends.</para>
    /// </summary>
    public partial class FocusPose : Node {

        /// <summary>The focused pose's translation in design pixels.</summary>
        public Vector2 FocusOffset { get; set; } = new(PulpMotionRules.FocusTranslateX, 0f);
        public float TiltDegrees { get; set; } = PulpMotionRules.FocusTiltDegrees;
        public float PoseScale { get; set; } = PulpMotionRules.FocusScale;
        /// <summary>The pressed pose's translation from rest (it replaces the focus slide while pressed).</summary>
        public Vector2 PressOffset { get; set; } = new(PulpMotionRules.PressTranslate, 0f);
        /// <summary>The pressed pose's scale about the centre.</summary>
        public float PressScale { get; set; } = PulpMotionRules.PressScale;

        /// <summary>
        /// TG-WIDGETS (additive). The focus and press-release tweens' curve, named so the focus row gap
        /// (<c>PulpScreenKit.RequiredRowGap</c>) can sample the real pose, overshoot included (MOT-A row 1: Quad Out).
        /// </summary>
        public const Tween.TransitionType Transition = Tween.TransitionType.Quad;
        public const Tween.EaseType Ease = Tween.EaseType.Out;

        private Control _host;
        private Control _target;
        private Tween _tween;
        private Tween _pressTween;
        private Vector2 _basePosition;
        private float _baseRotation;
        private Vector2 _baseScale = Vector2.One;
        private int _baseZ;
        private float _t;                  // 0 identity, 1 the full focus pose
        private float _p;                  // 0 not pressed, 1 the full press pose
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
            _pressTween?.Kill();
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
            if (_t != 0f || _p != 0f) Apply(_t);
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
            _pressTween?.Kill();
            _p = 0f;
            Settle(0f);
        }

        private void OnButtonDown() {
            // The press applies on the press frame, with no tween.
            _pressTween?.Kill();
            if (!_capturedBase || (_t == 0f && _p == 0f)) CaptureBase();
            _pressed = true;
            _p = 1f;
            Apply(_t);
        }

        private void OnButtonUp() {
            _pressed = false;
            _pressTween?.Kill();
            if (PulpMotion.Reduced) {
                SetPress(0f);
                return;
            }
            _pressTween = PulpMotion.NewTween(this);
            _pressTween.TweenMethod(Callable.From<float>(SetPress), _p, 0f, PulpMotionRules.PressReleaseSeconds)
                .SetTrans(Transition).SetEase(Ease);
            _pressTween.TweenCallback(Callable.From(() => SetPress(0f)));
        }

        private void SetPress(float p) {
            _p = p;
            if (_p == 0f && _t == 0f) Settle(0f);
            else Apply(_t);
        }

        private void Refresh() {
            float goal = PoseActive ? 1f : 0f;
            _tween?.Kill();
            if (!_capturedBase || (_t == 0f && _p == 0f)) CaptureBase();
            if (PulpMotion.Reduced) {
                Settle(goal);
                return;
            }
            float seconds = goal > _t ? PulpMotionRules.FocusInSeconds : PulpMotionRules.FocusOutSeconds;
            _tween = PulpMotion.NewTween(this);
            _tween.TweenMethod(Callable.From<float>(Apply), _t, goal, seconds)
                .SetTrans(Transition).SetEase(Ease);
            _tween.TweenCallback(Callable.From(() => Settle(goal)));
        }

        /// <summary>Writes the pose at its resting value; identity is written exactly.</summary>
        private void Settle(float t) {
            Apply(t);
            if (t == 0f && _p == 0f) {
                _target.Position = _basePosition;
                _target.Rotation = _baseRotation;
                _target.Scale = _baseScale;
                _target.ZIndex = _baseZ;
            }
        }

        /// <summary>
        /// Applies the pose at focus progress <paramref name="t"/> (identity at 0, the full slide at 1), blended toward the
        /// press pose by the current press amount (the press replaces the focus slide: +6 px and 0.985 about the centre).
        /// </summary>
        public void Apply(float t) {
            if (!GodotObject.IsInstanceValid(_target)) return;
            _t = t;
            Vector2 focusOffset = FocusOffset * t;
            float focusScale = 1f + (PoseScale - 1f) * t;
            Vector2 offset = focusOffset.Lerp(PressOffset, _p);
            float scale = Mathf.Lerp(focusScale, PressScale, _p);
            _target.Position = _basePosition + offset;
            _target.Rotation = _baseRotation + Mathf.DegToRad(TiltDegrees) * t * (1f - _p);
            _target.Scale = _baseScale * scale;
            _target.ZIndex = _baseZ + (PoseActive || t > 0.01f || _p > 0f ? 1 : 0);
        }
    }
}
