using FTT.UI.Motion;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// Skins a <see cref="CheckButton"/> as the glass toggle (UI_SPEC_GLASS 3.3, G13; TG-WIDGETS restyled the UR-WIDGETS
    /// skin): a 132 x 44 parallelogram track (run 14) at the right end of the button, with a 1.5 px <c>edge-dim</c> hairline
    /// over the <c>ToggleTrackFill</c> (on: an <c>accent</c> hairline over a 90 degree gold wash .55 to .18); a white
    /// 34 x 34 knob (run 10) 5 px in from the left (off) or the right (on) with its 14 px gold glow (D4: drawn, the mock
    /// clipped it), sliding in about 0.1 s (at once under Reduced Temporal Effects); and the T54 text under the knob's
    /// travel: "ON" white at the left, "OFF" <c>ink2</c> at the right. A focused toggle lights its hairline (the focus
    /// gradient) and adds the 14 px gold glow; a disabled one is dimmed. Every CheckButton behaviour is kept.
    ///
    /// <para>TG-CLOSE: the track, knob, insets and glows follow the UI scale (<see cref="Scale"/>), as the chevrons' heights
    /// and paddings do (it drew a fixed 132 x 44 at every scale); and the toggle draws the button's text itself as the T52
    /// settings-row label, upper case, in a <see cref="RowLabelName"/> child (left of the track, or wherever a
    /// <see cref="SettingsRowLayout"/> puts it) while the button's own glyphs, which tests and the focus chain read, draw
    /// nothing (SettingsMenu.SkinToggle did this for the Settings rows only). Geometry is cached per size; the draw
    /// allocates nothing.</para>
    /// </summary>
    public partial class PulpToggleDecor : Node {

        public const float TrackWidth = 132f;
        public const float TrackHeight = 44f;
        public const float TrackRun = 14f;
        public const float KnobSize = 34f;
        public const float KnobRun = 10f;
        public const float KnobInset = 5f;
        public const float TextInset = 16f;
        public const float SlideSeconds = 0.1f;
        /// <summary>The focus glow round the track and the knob's glow, at 100 %.</summary>
        public const int GlowSize = 14;
        /// <summary>The toggle's own label: the T52 settings-row label child the decor draws the button's text in.</summary>
        public const string RowLabelName = "RowLabel";
        private static readonly string[] IconSlots = {
            "checked", "unchecked", "checked_disabled", "unchecked_disabled",
            "checked_mirrored", "unchecked_mirrored", "checked_disabled_mirrored", "unchecked_disabled_mirrored"
        };

        /// <summary>The scale the toggle's metrics follow: the shared theme's applied UI scale (0.9 to 1.4).</summary>
        public static float Scale => PulpButtonDecor.UiScaleFactor;

        private static readonly string[] OwnTextColorSlots = {
            "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color", "font_disabled_color"
        };

        private CheckButton _button;
        private ToggleLayer _layer;
        private Label _rowLabel;
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
        /// <summary>How many times the track geometry was rebuilt (a redraw with no size change rebuilds nothing).</summary>
        public int GeometryBuilds => _layer.GeometryBuilds;

        /// <summary>Skins <paramref name="button"/>; a second call returns the decor already on it.</summary>
        public static PulpToggleDecor Apply(CheckButton button) {
            for (int i = 0, n = button.GetChildCount(); i < n; i++) if (button.GetChild(i) is PulpToggleDecor existing) return existing;
            var decor = new PulpToggleDecor { Name = "PulpToggleDecor", _button = button };
            var blank = new PlaceholderTexture2D { Size = new Vector2(1f, 1f) };
            foreach (string slot in IconSlots) button.AddThemeIconOverride(slot, blank);
            // The toggle is a track on a plain settings row: drop the inherited field plate and keep the text readable.
            foreach (string box in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" }) {
                button.AddThemeStyleboxOverride(box, new StyleBoxEmpty());
            }
            // The button's own text draws nothing: the row label below draws it in T52, upper case.
            foreach (string slot in OwnTextColorSlots) button.AddThemeColorOverride(slot, new Color(0f, 0f, 0f, 0f));
            if (button.GetNodeOrNull<Label>(RowLabelName) is not { } rowLabel) {
                rowLabel = new Label {
                    Name = RowLabelName, Uppercase = true, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore,
                    ThemeTypeVariation = UIPalette.LabelRoles.SettingsRowLabel
                };
                button.AddChild(rowLabel);
            }
            rowLabel.Text = button.Text;
            decor._rowLabel = rowLabel;
            button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, Mathf.Max(button.CustomMinimumSize.Y, MinimumHeight()));
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
            _button.Draw += OnButtonDraw;
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
                _button.Draw -= OnButtonDraw;
            }
        }

        // The Disabled flag has no signal, but toggling it redraws the button.
        private void OnButtonDraw() {
            if (_button != null && _lastDisabled != _button.Disabled) Callable.From(_layer.Refresh).CallDeferred();
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

        /// <summary>The track's box inside a button of the given size: 132 x 44 at the UI scale, at the right end, centred vertically.</summary>
        public static Rect2 TrackRect(Vector2 buttonSize) {
            float s = Scale;
            return new Rect2(buttonSize.X - TrackWidth * s, Mathf.Round((buttonSize.Y - TrackHeight * s) / 2f), TrackWidth * s, TrackHeight * s);
        }

        /// <summary>The knob's left edge inside the track at slide position <paramref name="t"/>: 5 from the left (off) to 5 from the right (on), at the UI scale.</summary>
        public static float KnobX(float t) => Mathf.Lerp(KnobInset, TrackWidth - KnobInset - KnobSize, t) * Scale;

        /// <summary>The toggle's minimum height: the track and 8 px, at the UI scale.</summary>
        public static float MinimumHeight() => Mathf.Ceil((TrackHeight + 8f) * Scale);

        // The row label: the button's text (kept in step), from the button's left to the gap before the track, unless a
        // SettingsRowLayout places it in its label column. Runs on a resize, a theme change (the UI scale) and a refresh.
        private void LayoutRowLabel(Rect2 track) {
            if (_rowLabel == null || !IsInstanceValid(_rowLabel)) return;
            if (_rowLabel.Text != _button.Text) _rowLabel.Text = _button.Text;
            float minHeight = MinimumHeight();
            if (_button.CustomMinimumSize.Y < minHeight) _button.CustomMinimumSize = new Vector2(_button.CustomMinimumSize.X, minHeight);
            if (_button.GetParent() is SettingsRowLayout) return;
            var rect = new Rect2(0f, 0f, Mathf.Max(0f, track.Position.X - SettingsRowLayout.SpecItemGap * Scale), _button.Size.Y);
            if (_rowLabel.Position != rect.Position || _rowLabel.Size != rect.Size) {
                _rowLabel.Position = rect.Position;
                _rowLabel.Size = rect.Size;
            }
        }

        private partial class ToggleLayer : Control {
            private static readonly Color OnWashFrom = new(UIPalette.Accent, 0.55f);
            private static readonly Color OnWashTo = new(UIPalette.Accent, 0.18f);
            private static readonly Color FocusGlowColor = UIPalette.FocusGlow;

            private readonly PulpToggleDecor _owner;
            private readonly Vector2[] _edge = new Vector2[4];
            private readonly Vector2[] _fill = new Vector2[4];
            private readonly Color[] _edgeColors = new Color[4];
            private readonly Color[] _fillColors = new Color[4];
            private readonly KnobLayer _knobLayer;
            private readonly StyleBoxFlat _glowBox;
            private Rect2 _track;
            private Vector2 _builtFor = new(-1f, -1f);
            private int _builtState = -1;

            public Label OnLabel { get; }
            public Label OffLabel { get; }
            public int GeometryBuilds { get; private set; }

            public ToggleLayer(PulpToggleDecor owner) {
                _owner = owner;
                MouseFilter = MouseFilterEnum.Ignore;
                TextureFilter = TextureFilterEnum.LinearWithMipmaps;
                OnLabel = MakeLabel("OnLabel", "ui_toggle_on", UIPalette.TextFocus);
                OffLabel = MakeLabel("OffLabel", "ui_toggle_off", UIPalette.Ink2);
                // The knob draws over the text (the text is "under the knob", 3.3): a child added after the labels.
                _knobLayer = new KnobLayer(owner) { Name = "Knob", MouseFilter = MouseFilterEnum.Ignore };
                _knobLayer.SetAnchorsPreset(LayoutPreset.FullRect);
                AddChild(_knobLayer);
                _glowBox = new StyleBoxFlat {
                    DrawCenter = false, BgColor = new Color(0, 0, 0, 0), Skew = new Vector2(TrackRun / TrackHeight, 0f),
                    ShadowSize = GlowSize, ShadowColor = FocusGlowColor, AntiAliasing = true
                };
            }

            private Label MakeLabel(string name, string key, Color color) {
                var label = new Label {
                    Name = name, Text = key, Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.ToggleText,
                    MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center
                };
                label.AddThemeColorOverride("font_color", color);
                AddChild(label);
                return label;
            }

            public void Refresh() {
                CheckButton button = _owner._button;
                if (button == null || !IsInstanceValid(button)) return;
                _owner._lastDisabled = button.Disabled;
                Modulate = button.Disabled ? UIPalette.DimModulate : Colors.White;
                Rect2 track = TrackRect(button.Size);
                float s = PulpToggleDecor.Scale;
                _glowBox.ShadowSize = Mathf.RoundToInt(GlowSize * s);
                _knobLayer.SetGlow(Mathf.RoundToInt(GlowSize * s));
                bool on = _owner._knob >= 0.5f;
                OnLabel.Visible = on;
                OffLabel.Visible = !on;
                OnLabel.ResetSize();
                OffLabel.ResetSize();
                OnLabel.Position = new Vector2(track.Position.X + TextInset * s, track.Position.Y + (track.Size.Y - OnLabel.Size.Y) / 2f);
                OffLabel.Position = new Vector2(track.End.X - TextInset * s - OffLabel.Size.X, track.Position.Y + (track.Size.Y - OffLabel.Size.Y) / 2f);
                _owner.LayoutRowLabel(track);
                QueueRedraw();
                _knobLayer.QueueRedraw();
            }

            public override void _Notification(int what) {
                if (what == NotificationResized || what == NotificationThemeChanged) Callable.From(Refresh).CallDeferred();
            }

            // 0 off, 1 on, 2 focused off, 3 focused on.
            private void EnsureGeometry(Rect2 track, int state) {
                if (track.Size == _builtFor && track.Position == _track.Position && state == _builtState) return;
                if (track.Size != _builtFor || track.Position != _track.Position) {
                    GeometryBuilds++;
                    _builtFor = track.Size;
                    _track = track;
                    float run = TrackRun * PulpToggleDecor.Scale;
                    GlassGeometry.Parallelogram(track, run, false, _edge);
                    GlassGeometry.Parallelogram(track.Grow(-GlassGeometry.EdgeWidth), run - 1f, false, _fill);
                }
                _builtState = state;
                bool on = (state & 1) == 1, focused = state >= 2;
                for (int i = 0; i < 4; i++) {
                    _edgeColors[i] = focused
                        ? GlassGeometry.Stop2(UIPalette.Focus, UIPalette.FocusGlow, GlassGeometry.GradientT(_edge[i], track, 100f))
                        : on ? UIPalette.Accent : UIPalette.EdgeDim;
                    _fillColors[i] = on
                        ? GlassGeometry.Stop2(OnWashFrom, OnWashTo, GlassGeometry.GradientT(_fill[i], track, 90f))
                        : UIPalette.ToggleTrackFill;
                }
            }

            public override void _Draw() {
                CheckButton button = _owner._button;
                if (button == null) return;
                Rect2 track = TrackRect(button.Size);
                bool on = _owner._knob >= 0.5f;
                bool focused = button.HasFocus();
                EnsureGeometry(track, (on ? 1 : 0) + (focused ? 2 : 0));
                float run = TrackRun * PulpToggleDecor.Scale;
                if (focused) DrawStyleBox(_glowBox, new Rect2(track.Position.X + run / 2f, track.Position.Y, track.Size.X - run, track.Size.Y));
                DrawPolygon(_edge, _edgeColors);
                DrawPolygon(_fill, _fillColors);
            }
        }

        private partial class KnobLayer : Control {
            private static readonly Color KnobGlow = new(UIPalette.Accent, 0.8f);
            private readonly PulpToggleDecor _owner;
            private readonly StyleBoxFlat _knobBox;

            public KnobLayer(PulpToggleDecor owner) {
                _owner = owner;
                // A white parallelogram knob (6.2: skew 10 / 34) with its 14 px gold glow; StyleBoxFlat shears the box, so it
                // is drawn on a rect one run narrower than the knob (6.3).
                _knobBox = new StyleBoxFlat {
                    BgColor = Colors.White, Skew = new Vector2(KnobRun / KnobSize, 0f), ShadowSize = GlowSize, ShadowColor = KnobGlow,
                    AntiAliasing = true
                };
            }

            /// <summary>The knob's glow at the UI scale (written on a refresh, never in a draw).</summary>
            public void SetGlow(int size) {
                if (_knobBox.ShadowSize != size) _knobBox.ShadowSize = size;
            }

            public override void _Draw() {
                CheckButton button = _owner._button;
                if (button == null) return;
                Rect2 track = TrackRect(button.Size);
                float s = PulpToggleDecor.Scale;
                float x = track.Position.X + KnobX(_owner._knob);
                DrawStyleBox(_knobBox, new Rect2(x + KnobRun * s / 2f, track.Position.Y + KnobInset * s, (KnobSize - KnobRun) * s, KnobSize * s));
            }
        }
    }
}
