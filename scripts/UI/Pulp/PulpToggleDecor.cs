using FTT.UI.Motion;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// UR-WIDGETS. Skins a <see cref="CheckButton"/> as the pulp 132 x 44 toggle (UI_SPEC Appendix B): the
    /// default switch icons are replaced by empty textures and a layer draws the track (9-slice, off or on),
    /// the 34 x 34 knob sliding in about 0.1 s (instantly under Reduced Temporal Effects) and a live ON / OFF
    /// label. The track sits at the right end of the button; the button's own text stays on the left. A focused
    /// toggle shows an accent outline; a disabled one is dimmed. Every CheckButton behaviour is kept.
    /// </summary>
    public partial class PulpToggleDecor : Node {

        public const float TrackWidth = 132f;
        public const float TrackHeight = 44f;
        public const float KnobSize = 34f;
        public const float KnobInset = 5f;
        public const float SlideSeconds = 0.1f;
        private static readonly string[] IconSlots = {
            "checked", "unchecked", "checked_disabled", "unchecked_disabled",
            "checked_mirrored", "unchecked_mirrored", "checked_disabled_mirrored", "unchecked_disabled_mirrored"
        };

        private CheckButton _button;
        private ToggleLayer _layer;
        private Tween _tween;
        private float _knob;       // 0 off, 1 on
        private bool _hooked;
        private bool _lastDisabled;

        public CheckButton Button => _button;
        /// <summary>The knob position, 0 (off) to 1 (on).</summary>
        public float KnobPosition => _knob;
        public Control Layer => _layer;
        public Label OnLabel => _layer.OnLabel;
        public Label OffLabel => _layer.OffLabel;

        /// <summary>Skins <paramref name="button"/>; a second call returns the decor already on it.</summary>
        public static PulpToggleDecor Apply(CheckButton button) {
            for (int i = 0, n = button.GetChildCount(); i < n; i++) if (button.GetChild(i) is PulpToggleDecor existing) return existing;
            var decor = new PulpToggleDecor { Name = "PulpToggleDecor", _button = button };
            var blank = new PlaceholderTexture2D { Size = new Vector2(1f, 1f) };
            foreach (string slot in IconSlots) button.AddThemeIconOverride(slot, blank);
            // The pulp toggle is a track on a plain row: drop the inherited banner plate and keep the text readable.
            foreach (string box in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" }) {
                button.AddThemeStyleboxOverride(box, new StyleBoxEmpty());
            }
            foreach (string slot in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color" }) {
                button.AddThemeColorOverride(slot, UIPalette.Ink);
            }
            button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, Mathf.Max(button.CustomMinimumSize.Y, TrackHeight + 8f));
            decor._layer = new ToggleLayer(decor) { Name = "ToggleLayer" };
            decor._layer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            button.AddChild(decor._layer);
            decor._knob = button.ButtonPressed ? 1f : 0f;
            button.AddChild(decor);
            return decor;
        }

        public override void _EnterTree() {
            if (_hooked || _button == null) return;
            _hooked = true;
            _button.Toggled += OnToggled;
            _button.FocusEntered += OnFocusChanged;
            _button.FocusExited += OnFocusChanged;
            _knob = _button.ButtonPressed ? 1f : 0f;
            _layer.Refresh();
        }

        public override void _ExitTree() {
            _tween?.Kill();
            if (!_hooked || _button == null) return;
            _hooked = false;
            if (IsInstanceValid(_button)) {
                _button.Toggled -= OnToggled;
                _button.FocusEntered -= OnFocusChanged;
                _button.FocusExited -= OnFocusChanged;
            }
        }

        public override void _Process(double delta) {
            if (_button != null && _lastDisabled != _button.Disabled) _layer.Refresh();
        }

        private void OnFocusChanged() => _layer.QueueRedraw();

        private void OnToggled(bool pressed) {
            _tween?.Kill();
            float goal = pressed ? 1f : 0f;
            if (PulpMotion.Reduced || !IsInsideTree()) {
                SetKnob(goal);
                return;
            }
            _tween = PulpMotion.NewTween(this);
            _tween.TweenMethod(Callable.From<float>(SetKnob), _knob, goal, SlideSeconds).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }

        private void SetKnob(float value) {
            _knob = value;
            _layer.Refresh();
        }

        /// <summary>The track's border box inside a button of the given size: 132 x 44, at the right end, centred vertically.</summary>
        public static Rect2 TrackRect(Vector2 buttonSize) =>
            new(buttonSize.X - TrackWidth, Mathf.Round((buttonSize.Y - TrackHeight) / 2f), TrackWidth, TrackHeight);

        /// <summary>The knob's left edge inside the track at slide position <paramref name="t"/>: 5 from the left (off) to 5 from the right (on).</summary>
        public static float KnobX(float t) => Mathf.Lerp(KnobInset, TrackWidth - KnobInset - KnobSize - 3f, t);

        private partial class ToggleLayer : Control {
            private readonly PulpToggleDecor _owner;
            public Label OnLabel { get; }
            public Label OffLabel { get; }

            public ToggleLayer(PulpToggleDecor owner) {
                _owner = owner;
                MouseFilter = MouseFilterEnum.Ignore;
                TextureFilter = TextureFilterEnum.LinearWithMipmaps;
                OnLabel = MakeLabel("OnLabel", "ui_toggle_on", UIPalette.LabelDark);
                OffLabel = MakeLabel("OffLabel", "ui_toggle_off", UIPalette.Paper);
            }

            private Label MakeLabel(string name, string key, Color color) {
                var label = new Label {
                    Name = name, Text = key, Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.SlotKeyTag,
                    MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center
                };
                label.AddThemeColorOverride("font_color", color);
                AddChild(label);
                return label;
            }

            public void Refresh() {
                CheckButton button = _owner._button;
                if (button == null) return;
                _owner._lastDisabled = button.Disabled;
                Modulate = new Color(1f, 1f, 1f, button.Disabled ? 0.45f : 1f);
                Rect2 track = TrackRect(button.Size);
                bool on = _owner._knob >= 0.5f;
                OnLabel.Visible = on;
                OffLabel.Visible = !on;
                OnLabel.ResetSize();
                OffLabel.ResetSize();
                OnLabel.Position = new Vector2(track.Position.X + 16f, track.Position.Y + (TrackHeight - OnLabel.Size.Y) / 2f);
                OffLabel.Position = new Vector2(track.End.X - 16f - OffLabel.Size.X, track.Position.Y + (TrackHeight - OffLabel.Size.Y) / 2f);
                QueueRedraw();
            }

            public override void _Notification(int what) {
                if (what == NotificationResized || what == NotificationThemeChanged) CallDeferred(nameof(Refresh));
            }

            public override void _Draw() {
                CheckButton button = _owner._button;
                if (button == null) return;
                Rect2 track = TrackRect(button.Size);
                bool on = _owner._knob >= 0.5f;
                StyleBoxTexture plate = PulpKit.SharedPlate(on ? "toggle_track_on" : "toggle_track_off");
                if (plate != null) DrawStyleBox(plate, track);
                Texture2D knob = PulpKit.Texture("toggle_knob");
                if (knob != null) {
                    float x = track.Position.X + KnobX(_owner._knob);
                    DrawTexture(knob, new Vector2(x, track.Position.Y + 6f));
                }
                if (button.HasFocus()) {
                    // The focus ring is an outline, so focus never rides on colour alone.
                    Rect2 ring = track.Grow(5f);
                    DrawRect(ring, UIPalette.Accent, false, 3f);
                }
            }
        }
    }
}
