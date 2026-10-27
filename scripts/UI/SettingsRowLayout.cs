using System.Collections.Generic;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// TG-PAUSE. The settings row of UI_SPEC_GLASS 3.3, as a container: every row 62 tall with its items 22 apart and
    /// vertically centred, the row label in a 360 px column, the control (a 360 px slider, a 300 x 44 glass option field, a
    /// button at its own width), and a slider's value in a 90 px column. It lays out its <b>own direct children</b>, so the
    /// Settings scene keeps every node path a script or test binds to (<c>Audio/MasterSlider</c>,
    /// <c>Gameplay/UiScaleLabel</c>, <c>Controls/Options/HapticToggle</c> ...).
    ///
    /// <para>How children pair up, in child order: a <see cref="Label"/> followed by a <see cref="Range"/> or an
    /// <see cref="OptionButton"/> is a control row, and a following <see cref="Label"/> whose name ends in <c>Value</c> is its
    /// value; a <see cref="CheckButton"/> is a toggle row, spanning the label column, the gap and its 132 px track (its label
    /// is the <c>RowLabel</c> child the screen gives it, placed in the label column); anything else (a help line, a heading, a
    /// rule, a separator, a box row) spans the full width at its own height, a box row being at least a row tall with its
    /// first label set to the label column. With <see cref="Columns"/> 2 the control and toggle rows run two to a line (the
    /// Controls tab's options).</para>
    ///
    /// <para>Every metric grows with the UI scale (<see cref="UIPalette.AppliedUiScale"/>), as the button heights do
    /// (6.7 item 8); the fonts grow through the theme. Hidden children take no room. With <see cref="Scrolls"/> the rows
    /// scroll inside the layout's rect behind a glass scroll bar, and the focused row is kept in view.</para>
    /// </summary>
    public partial class SettingsRowLayout : Container {

        /// <summary>3.3: a settings row is 62 tall, its items 22 apart; the label column 360, the value column 90.</summary>
        public const float SpecRowHeight = 62f, SpecItemGap = 22f, SpecLabelWidth = 360f, SpecValueWidth = 90f;

        /// <summary>3.3: a slider is 360 wide; an option button is a 300 x 44 glass field; a toggle's track is 132.</summary>
        public const float SpecSliderWidth = 360f, SpecFieldWidth = 300f, SpecFieldHeight = 44f;

        public const string RowLabelName = "RowLabel";
        public const string ValueSuffix = "Value";

        [Export] public int Columns { get; set; } = 1;
        [Export] public float LabelWidth { get; set; } = SpecLabelWidth;
        [Export] public float ControlWidth { get; set; } = SpecSliderWidth;
        [Export] public float FieldWidth { get; set; } = SpecFieldWidth;
        [Export] public float ValueWidth { get; set; } = SpecValueWidth;
        [Export] public float ItemGap { get; set; } = SpecItemGap;
        [Export] public float RowHeight { get; set; } = SpecRowHeight;
        [Export] public float ColumnGap { get; set; } = 48f;
        [Export] public float RowGap { get; set; } = 0f;

        /// <summary>
        /// The rows scroll inside the layout's own rect (a Settings tab: 9.2 puts the active tab's rows in a scroll
        /// container) instead of growing the slab; see the scrolling section.
        /// </summary>
        [Export] public bool Scrolls { get; set; }

        private enum Kind { Pair, Toggle, Full }

        private struct Row {
            public Kind Kind;
            public Control Label, Control, Value, Whole;
        }

        // Reused between layout passes (a pass runs only when a child, the size or the theme changed).
        private readonly List<Row> _rows = new();
        private readonly List<Control> _visible = new();
        private readonly List<Row> _line = new();

        /// <summary>The scale the metrics are applied at: the shared theme's UI scale (0.9 to 1.4).</summary>
        public static float MetricScale => PulpButtonDecor.UiScaleFactor;

        /// <summary>
        /// The width a toggle row's check button takes: the label column, the gap and the 132 px track the toggle decor
        /// draws at the button's right end, all at the UI scale (TG-CLOSE: the track follows the scale too). So the track
        /// starts on the control column, under the sliders and fields, at every scale.
        /// </summary>
        public float ToggleRowWidth => (LabelWidth + ItemGap + PulpToggleDecor.TrackWidth) * MetricScale;

        public SettingsRowLayout() {
            MouseFilter = MouseFilterEnum.Pass;
        }

        public override void _Notification(int what) {
            switch ((long)what) {
                case NotificationSortChildren:
                    Arrange();
                    break;
                case NotificationReady:
                    SizeWrappingLabels();
                    SetUpScrolling();
                    break;
                case NotificationExitTree:
                    TearDownScrolling();
                    break;
                case NotificationEnterTree:
                    // A layout that left the tree and came back listens to focus again.
                    if (_bar != null && _focusViewport == null) {
                        _focusViewport = GetViewport();
                        if (_focusViewport != null) _focusViewport.GuiFocusChanged += OnGuiFocusChanged;
                    }
                    break;
                case NotificationThemeChanged:
                    SizeWrappingLabels();
                    UpdateMinimumSize();
                    QueueSort();
                    break;
            }
        }

        /// <summary>
        /// Gives every wrapping label the width it will be laid out at as its minimum width: a row label the label column,
        /// a full-width line the layout's own width. A label wraps at its current width, and one that has never been laid out
        /// (on a tab that is not showing, whose minimum size the Settings tabs still count) has none, so it would break after
        /// every word and report a page-tall minimum height.
        /// </summary>
        private void SizeWrappingLabels() {
            CollectRows();
            float s = MetricScale;
            float full = NaturalWidth(s);
            foreach (Row row in _rows) {
                switch (row.Kind) {
                    case Kind.Pair:
                        if (row.Label is Label label && label.AutowrapMode != TextServer.AutowrapMode.Off) SetMinWidth(label, LabelWidth * s);
                        break;
                    case Kind.Full:
                        if (row.Whole is Label line && line.AutowrapMode != TextServer.AutowrapMode.Off) SetMinWidth(line, full);
                        break;
                }
            }
        }

        private static void SetMinWidth(Control control, float width) {
            var want = new Vector2(width, control.CustomMinimumSize.Y);
            if (control.CustomMinimumSize != want) control.CustomMinimumSize = want;
        }

        /// <summary>The layout's width before any full-width row widens it: its columns and the gaps between them.</summary>
        private float NaturalWidth(float s) {
            int columns = Mathf.Max(1, Columns);
            return columns * ColumnWidth(s) + (columns - 1) * ColumnGap * s;
        }

        // ---- Rows ---------------------------------------------------------------------------------------------

        private void CollectRows() {
            _rows.Clear();
            List<Control> visible = _visible;
            visible.Clear();
            for (int i = 0, n = GetChildCount(); i < n; i++) {
                if (GetChild(i) is Control c && c.Visible && !c.TopLevel) visible.Add(c);
            }
            for (int i = 0; i < visible.Count; i++) {
                Control c = visible[i];
                if (c is Label label && i + 1 < visible.Count && IsRowControl(visible[i + 1])) {
                    var row = new Row { Kind = Kind.Pair, Label = label, Control = visible[i + 1] };
                    i++;
                    if (i + 1 < visible.Count && visible[i + 1] is Label value && value.Name.ToString().EndsWith(ValueSuffix)) {
                        row.Value = value;
                        i++;
                    }
                    _rows.Add(row);
                } else if (c is CheckButton) {
                    _rows.Add(new Row { Kind = Kind.Toggle, Whole = c });
                } else {
                    _rows.Add(new Row { Kind = Kind.Full, Whole = c });
                }
            }
        }

        private static bool IsRowControl(Control c) => c is Range || c is OptionButton;

        private float ColumnWidth(float s) => (LabelWidth + ItemGap + Mathf.Max(ControlWidth, FieldWidth) + ItemGap + ValueWidth) * s;

        private float RowHeightOf(in Row row, float s) {
            float height = RowHeight * s;
            switch (row.Kind) {
                case Kind.Pair:
                    height = Mathf.Max(height, row.Label.GetCombinedMinimumSize().Y);
                    height = Mathf.Max(height, ControlSize(row.Control, s).Y);
                    if (row.Value != null) height = Mathf.Max(height, row.Value.GetCombinedMinimumSize().Y);
                    return height;
                case Kind.Toggle:
                    return Mathf.Max(height, row.Whole.GetCombinedMinimumSize().Y);
                default:
                    float own = row.Whole.GetCombinedMinimumSize().Y;
                    return row.Whole is BoxContainer ? Mathf.Max(height, own) : own;
            }
        }

        /// <summary>A control row's control size: a slider the slider column, a field the field width, else its own.</summary>
        private Vector2 ControlSize(Control control, float s) {
            Vector2 min = control.GetCombinedMinimumSize();
            float width = control switch {
                Range => Mathf.Max(min.X, ControlWidth * s),
                OptionButton => Mathf.Max(min.X, FieldWidth * s),
                _ => min.X
            };
            float height = control is OptionButton ? Mathf.Max(min.Y, SpecFieldHeight * s) : min.Y;
            return new Vector2(width, height);
        }

        public override Vector2 _GetMinimumSize() {
            Measure(MetricScale, out float width, out float height);
            if (!Scrolls) return new Vector2(width, height);
            // A scrolling layout asks only for its width (and the bar beside it) and two rows: the slab stays its own height
            // and the rows past it scroll.
            float s = MetricScale;
            return new Vector2(width + ScrollReserve(), Mathf.Min(height, 2f * RowHeight * s));
        }

        /// <summary>The rows' natural width and total height at scale <paramref name="s"/>.</summary>
        private void Measure(float s, out float width, out float height) {
            CollectRows();
            int columns = Mathf.Max(1, Columns);
            width = NaturalWidth(s);
            height = 0f;
            int lines = 0;
            int col = 0;
            float line = 0f;
            foreach (Row row in _rows) {
                if (row.Kind == Kind.Full) {
                    if (col > 0) { height += line; lines++; col = 0; line = 0f; }
                    width = Mathf.Max(width, row.Whole.GetCombinedMinimumSize().X);
                    height += RowHeightOf(row, s);
                    lines++;
                    continue;
                }
                line = Mathf.Max(line, RowHeightOf(row, s));
                col++;
                if (col >= columns) { height += line; lines++; col = 0; line = 0f; }
            }
            if (col > 0) { height += line; lines++; }
            height += Mathf.Max(0, lines - 1) * RowGap * s;
        }

        private void Arrange() {
            CollectRows();
            float s = MetricScale;
            float column = ColumnWidth(s);
            int columns = Mathf.Max(1, Columns);
            float fullWidth = Mathf.Max(0f, Size.X - (Scrolls ? ScrollReserve() : 0f));
            float y = Scrolls ? -_scroll : 0f;
            float line = 0f;
            bool first = true;
            // A line of paired rows is placed once its height is known: collect it, then place it.
            _line.Clear();
            foreach (Row row in _rows) {
                if (row.Kind == Kind.Full) {
                    FlushLine(column, s, ref y, ref line, ref first);
                    if (!first) y += RowGap * s;
                    first = false;
                    float h = RowHeightOf(row, s);
                    if (row.Whole is BoxContainer box) AlignBoxRow(box, s);
                    FitChildInRect(row.Whole, new Rect2(0f, y, fullWidth, h));
                    y += h;
                    continue;
                }
                _line.Add(row);
                line = Mathf.Max(line, RowHeightOf(row, s));
                if (_line.Count >= columns) FlushLine(column, s, ref y, ref line, ref first);
            }
            FlushLine(column, s, ref y, ref line, ref first);
            if (Scrolls) UpdateScroll(y + _scroll);
        }

        private void FlushLine(float column, float s, ref float y, ref float line, ref bool first) {
            if (_line.Count == 0) return;
            if (!first) y += RowGap * s;
            first = false;
            for (int k = 0; k < _line.Count; k++) PlaceRow(_line[k], k * (column + ColumnGap * s), y, line, s);
            y += line;
            _line.Clear();
            line = 0f;
        }

        private void PlaceRow(in Row row, float x, float y, float height, float s) {
            if (row.Kind == Kind.Toggle) {
                var toggle = row.Whole;
                float width = Mathf.Max(ToggleRowWidth, toggle.GetCombinedMinimumSize().X);
                FitChildInRect(toggle, new Rect2(x, y, width, height));
                if (toggle.GetNodeOrNull<Label>(RowLabelName) is { } label) {
                    var rect = new Rect2(0f, 0f, LabelWidth * s, height);
                    if (label.Position != rect.Position || label.Size != rect.Size) {
                        label.Position = rect.Position;
                        label.Size = rect.Size;
                    }
                }
                return;
            }
            float labelWidth = LabelWidth * s;
            FitChildInRect(row.Label, new Rect2(x, y, labelWidth, height));
            Vector2 control = ControlSize(row.Control, s);
            float cx = x + labelWidth + ItemGap * s;
            FitChildInRect(row.Control, new Rect2(cx, y + (height - control.Y) / 2f, control.X, control.Y));
            if (row.Value != null) {
                float vx = cx + Mathf.Max(ControlWidth, FieldWidth) * s + ItemGap * s;
                FitChildInRect(row.Value, new Rect2(vx, y, ValueWidth * s, height));
            }
        }

        /// <summary>A box row (a label and a field authored together) starts its field at the control column.</summary>
        private void AlignBoxRow(BoxContainer box, float s) {
            int gap = Mathf.RoundToInt(ItemGap * s);
            if (box.GetThemeConstant("separation") != gap) box.AddThemeConstantOverride("separation", gap);
            for (int i = 0, n = box.GetChildCount(); i < n; i++) {
                if (box.GetChild(i) is not Label label) continue;
                var want = new Vector2(LabelWidth * s, label.CustomMinimumSize.Y);
                if (label.CustomMinimumSize != want) label.CustomMinimumSize = want;
                break;
            }
        }

        // ---- Scrolling (UI_SPEC_GLASS 9.2: "the active tab's rows in a scroll container (glass scroll bar)") ---------
        //
        // The rows stay this node's own children (their paths are what scripts and tests bind to), so the layout scrolls
        // them itself: it offsets them, clips to its rect, and keeps a glass VScrollBar of its own as an internal child (out
        // of the row and focus walks). The focused row is kept in view; the mouse wheel scrolls by a row.

        /// <summary>The gap between the rows and the scroll bar.</summary>
        public const float ScrollBarGap = 12f;

        /// <summary>The scroll bar's node name (an internal child).</summary>
        public const string ScrollBarName = "ScrollBar";

        private VScrollBar _bar;
        private Viewport _focusViewport;
        private float _scroll;
        private bool _settingBar;

        /// <summary>How far the rows are scrolled, in pixels. Test surface.</summary>
        internal float ScrollOffset => _scroll;

        /// <summary>The glass scroll bar, or null when the layout does not scroll. Test surface.</summary>
        internal VScrollBar ScrollBar => _bar;

        private float ScrollReserve() {
            float bar = _bar?.GetCombinedMinimumSize().X ?? 0f;
            return Mathf.Max(bar, 10f) + ScrollBarGap * MetricScale;
        }

        private void SetUpScrolling() {
            if (!Scrolls || _bar != null) return;
            _bar = new VScrollBar {
                Name = ScrollBarName,
                FocusMode = FocusModeEnum.None,
                MouseFilter = MouseFilterEnum.Stop,
                Visible = false,
                Step = 1
            };
            AddChild(_bar, false, InternalMode.Back);
            _bar.ValueChanged += OnBarValueChanged;
            _focusViewport = GetViewport();
            if (_focusViewport != null) _focusViewport.GuiFocusChanged += OnGuiFocusChanged;
        }

        private void TearDownScrolling() {
            if (_focusViewport != null && IsInstanceValid(_focusViewport)) _focusViewport.GuiFocusChanged -= OnGuiFocusChanged;
            _focusViewport = null;
        }

        private void OnBarValueChanged(double value) {
            if (_settingBar) return;
            ScrollTo((float)value);
        }

        private void ScrollTo(float offset) {
            float clamped = Mathf.Clamp(offset, 0f, MaxScroll());
            if (Mathf.IsEqualApprox(clamped, _scroll)) return;
            _scroll = clamped;
            QueueSort();
        }

        private float _contentHeight;

        private float MaxScroll() => Mathf.Max(0f, _contentHeight - Size.Y);

        /// <summary>After a pass: the content's height, the clamp, the bar and the clip.</summary>
        private void UpdateScroll(float contentHeight) {
            _contentHeight = contentHeight;
            bool overflows = contentHeight > Size.Y + 0.5f;
            if (ClipContents != overflows) ClipContents = overflows;
            float max = MaxScroll();
            if (_scroll > max) {
                _scroll = max;
                QueueSort();
            }
            if (_bar == null) return;
            if (_bar.Visible != overflows) _bar.Visible = overflows;
            float barWidth = Mathf.Max(_bar.GetCombinedMinimumSize().X, 10f);
            FitChildInRect(_bar, new Rect2(Size.X - barWidth, 0f, barWidth, Size.Y));
            _settingBar = true;
            _bar.MaxValue = Mathf.Max(contentHeight, Size.Y);
            _bar.Page = Size.Y;
            _bar.Value = _scroll;
            _settingBar = false;
        }

        /// <summary>Keeps a newly focused row (any control inside this layout) fully in view.</summary>
        private void OnGuiFocusChanged(Control focused) {
            if (!Scrolls || focused == null || !IsVisibleInTree() || !IsAncestorOf(focused)) return;
            Transform2D toLocal = GetGlobalTransform().AffineInverse();
            Rect2 rect = focused.GetGlobalRect();
            float top = (toLocal * rect.Position).Y;
            float bottom = (toLocal * rect.End).Y;
            if (top < 0f) ScrollTo(_scroll + top);
            else if (bottom > Size.Y) ScrollTo(_scroll + Mathf.Min(top, bottom - Size.Y));
        }

        public override void _GuiInput(InputEvent @event) {
            if (!Scrolls || @event is not InputEventMouseButton { Pressed: true } wheel) return;
            float step = RowHeight * MetricScale;
            if (wheel.ButtonIndex == MouseButton.WheelUp) ScrollTo(_scroll - step);
            else if (wheel.ButtonIndex == MouseButton.WheelDown) ScrollTo(_scroll + step);
            else return;
            AcceptEvent();
        }
    }
}
