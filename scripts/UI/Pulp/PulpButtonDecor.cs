using System.Collections.Generic;
using FTT.Core;
using FTT.UI.Motion;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// TG-WIDGETS (BTN-A, UI_SPEC_GLASS 3.2; first written by UR-WIDGETS for the pulp banner). Decorates a chevron button
    /// with the diamond marker (<see cref="GlassMarker"/>), the MOT-A focus pose, the prompt badge on the focused row, the
    /// upper-case label and the selected look. Call <see cref="Apply"/> once per button (a second call returns the existing
    /// decor). The decor and its marker / badge / label layer are children of the button, so the focus pose carries them.
    ///
    /// <list type="bullet">
    /// <item>Label: <c>Tr(Text)</c> drawn upper-case in the button's own theme font, from the left padding (58, compact 52)
    /// to the right one (54, compact 40); colours per state from the theme (normal <c>ink2</c>, focus / hover / pressed white,
    /// disabled <c>ink3</c>), selected <c>ink</c>. A label too long shrinks (floors 26 / 20 px at 100 %), then ellipsizes;
    /// a multi-line or <see cref="GrowToFit"/> label wraps instead. <c>Button.Text</c> stays the raw key.</item>
    /// <item>Prompt badge: a 36 px ring with the confirm glyph (or a key-cap) in the label colour, at the label's right end,
    /// on the focused row only (<see cref="ShowPrompt"/> turns it off for footers, 3.2 D6).</item>
    /// <item>Hover is focus (AC8): the mouse entering a visible, enabled, focusable button gives it focus.</item>
    /// <item>UI scale (6.7 item 8): the button's design height, the paddings the label is drawn at, the marker and the badge
    /// grow with the UI scale.</item>
    /// <item>Width: the screen's when it sets one (a <c>CustomMinimumSize</c> width) or a D6 stack's
    /// (<see cref="ApplyStackWidth"/>). Otherwise the decor widens the row itself (orchestrator ruling, TG-WIDGETS step 2):
    /// its minimum width is <see cref="RequiredWidth"/>, the drawn upper-case label at the theme size with the paddings and,
    /// when the row can show its badge, the badge reserve, kept up to date after a theme, UI-scale, locale or text change.
    /// A row that wraps (<see cref="GrowToFit"/>, autowrap, a line break) or clips its text (<c>ClipText</c>, an overrun
    /// behaviour) keeps the width it has.</item>
    /// </list>
    /// Nothing polls per frame: the button's own redraw (its state, text or size changed) queues one refresh, and the label's
    /// text layout is cached until its text, font, size or width changes.
    /// </summary>
    public partial class PulpButtonDecor : Node {

        /// <summary>The marker's look in one state.</summary>
        public enum MarkerState { Normal, Focus, Selected, Pressed, Disabled }

        /// <summary>The selected marker: filled and outlined <c>accent</c>.</summary>
        public static readonly Color MarkerSelectedTint = UIPalette.Accent;
        /// <summary>The disabled marker: the <c>edge</c> outline, dashed, at .5 opacity.</summary>
        public static readonly Color MarkerDisabledTint = new(UIPalette.Edge, UIPalette.Edge.A * 0.5f);

        /// <summary>The marker's centre from the button's left (3.2: x + 31, compact x + 29).</summary>
        public const float MarkerCentreX = 31f, CompactMarkerCentreX = 29f;
        /// <summary>The prompt badge's size and the gap before it (3.2: the D6 rule reserves 16 + 36).</summary>
        public const float BadgeSize = 36f, BadgeGap = 16f;
        /// <summary>The badge and its gap: what a label leaves free on a row that can show the prompt.</summary>
        public const float BadgeReserve = BadgeSize + BadgeGap;
        /// <summary>The shrink-to-fit floors at 100 % (3.2: T10 26 px, T11 20 px), scaled with the UI scale, never under 18.</summary>
        public const float ShrinkFloor = 26f, CompactShrinkFloor = 20f;

        private static readonly StringName FontName = "font", FontSizeName = "font_size", NormalName = "normal";
        private static readonly StringName HoverName = "hover", SelectedName = "selected", HSeparationName = "h_separation";
        private static readonly StringName OutlineSizeName = "outline_size";

        private Button _button;
        private bool _compact;
        private GlassMarker _marker;
        private Control _badge;
        private GlassPromptRing _badgeRing;
        private Label _badgeLabel;
        private bool _selected;
        private PromptDevice _badgeDevice;
        private float _badgeScale = -1f;
        private Color _badgeColor;
        private bool _showPrompt = true;
        private bool _hooked;
        private bool _refreshQueued;
        private bool _layoutDirty;
        private bool _hasSavedNormal, _hasSavedHover;
        private StyleBox _savedNormal, _savedHover;
        private readonly Dictionary<string, Color?> _savedColors = new();
        private bool _uppercase = true;
        private bool _glyphsHidden;
        private readonly Dictionary<string, (Color Color, bool HadOverride)> _original = new();
        private LabelLayer _labelLayer;
        private string _sourceText;
        private string _drawn = "";
        private bool _lastDisabled;
        private float _lastWidth = -1f;
        private BaseButton.DrawMode _lastMode;
        private float _scale = 1f;

        // Size: the design minimum size (as the screen set it), what the decor last wrote, and the grow-to-fit extras.
        private Vector2 _design;
        private Vector2 _written;
        private bool _hasWritten;
        private bool _growToFit;
        private float _grownHeight;
        private float _wordMinWidth;
        private int _growFontSize;
        private string _growText;
        private float _growWidth = -1f, _growScale = -1f;

        // The label's cached layout (rebuilt only when its inputs change).
        private readonly TextLine _line = new();
        private readonly TextParagraph _paragraph = new();
        private string _layoutText;
        private Font _layoutFont;
        private int _layoutSize = -1, _layoutFitted = -1, _layoutMaxLines = -1;
        private float _layoutWidth = -1f, _layoutFitWidth = -1f, _layoutHeight = -1f;
        private HorizontalAlignment _layoutAlign;
        private bool _layoutMulti;
        private bool _layoutTrimmed;

        private static readonly string[] HideSlots = {
            "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color",
            "font_disabled_color", "font_outline_color"
        };

        private static readonly string[] FontColorSlots = {
            "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color"
        };

        /// <summary>
        /// UR-POLISH. When true a label too long for one line wraps instead of shrinking, and the button grows its minimum
        /// height to hold the lines. The wrap always leaves room for the focused row's prompt badge, so the button does not
        /// change height as focus moves. Off by default: menu rows are authored to fit one line.
        /// </summary>
        public bool GrowToFit {
            get => _growToFit;
            set {
                if (_growToFit == value || _button == null) return;
                _growToFit = value;
                _growText = null;
                _grownHeight = 0f;
                _wordMinWidth = 0f;
                Refresh();
            }
        }

        /// <summary>The diamond marker; its tint is the state's main colour.</summary>
        public GlassMarker Marker => _marker;
        /// <summary>The prompt badge control; visible only while the button has focus.</summary>
        public Control Badge => _badge;
        /// <summary>The device family the badge currently shows.</summary>
        public PromptDevice BadgeDevice => _badgeDevice;
        public Button Button => _button;

        /// <summary>Whether the focused row shows its prompt badge (3.2; compact footers set it off, D6).</summary>
        public bool ShowPrompt {
            get => _showPrompt;
            set { if (_showPrompt == value) return; _showPrompt = value; InvalidateLayout(); Refresh(); }
        }

        /// <summary>True while the decor draws the upper-cased label and hides the button's own glyphs.</summary>
        public bool Uppercase {
            get => _uppercase;
            set {
                if (_uppercase == value) return;
                _uppercase = value;
                if (_button != null && _button.IsInsideTree()) ApplyGlyphHiding();
                Refresh();
            }
        }

        /// <summary>The string the decor draws: the translated button text, upper-cased for the current locale.</summary>
        public string DrawnText => _button == null ? "" : ToDrawnText(_button.Tr(_button.Text));

        /// <summary>The colour the label draws in right now (<c>ink</c> when selected, else the theme colour of the state).</summary>
        public Color CurrentLabelColor => LabelColorNow();

        /// <summary>The right limit (from the button's left) the label may reach: the right padding, or the badge's room on the focused row.</summary>
        public float LabelRightLimit => LabelLimit();

        /// <summary>The font size the single-line label draws at (shrunk to fit, never under its floor); the theme size for a wrapping label.</summary>
        public int DrawnFontSize {
            get {
                if (_button == null) return 0;
                EnsureLayout();
                return _layoutMulti ? _layoutSize : _layoutFitted;
            }
        }

        /// <summary>How many times the label's text layout was rebuilt (a redraw without a change rebuilds nothing).</summary>
        public int LayoutBuilds { get; private set; }

        /// <summary>The UI scale the decor applies to heights, paddings, the marker and the badge (the shared theme's applied scale).</summary>
        public static float UiScaleFactor =>
            Mathf.Clamp(UIPalette.AppliedUiScale, GlobalSaveData.MinUiScale, GlobalSaveData.MaxUiScale);

        /// <summary>The shrink floor for a chevron (26 px, compact 20) at the current UI scale, never under the 18 px floor.</summary>
        public static int ShrinkFloorFor(bool compact) =>
            Mathf.Max(UIPalette.MinLabelFontSize, Mathf.RoundToInt((compact ? CompactShrinkFloor : ShrinkFloor) * UiScaleFactor));

        /// <summary>Which theme colour slot the label uses for a state (disabled, pressed, hover, focus, normal); selected is handled apart.</summary>
        public static string LabelColorSlot(bool disabled, BaseButton.DrawMode mode, bool focused) {
            if (disabled) return "font_disabled_color";
            return mode switch {
                BaseButton.DrawMode.Pressed => "font_pressed_color",
                BaseButton.DrawMode.HoverPressed => "font_hover_pressed_color",
                BaseButton.DrawMode.Hover => "font_hover_color",
                _ => focused ? "font_focus_color" : "font_color"
            };
        }

        /// <summary>Upper-cases <paramref name="text"/> through the text server for the current locale.</summary>
        public static string ToDrawnText(string text) =>
            TextServerManager.GetPrimaryInterface().StringToUpper(text ?? "", TranslationServer.GetLocale());

        /// <summary>True while the selected look is shown (the theme's <c>selected</c> chevron). In ToggleMode it follows <c>ButtonPressed</c>.</summary>
        public bool Selected {
            get => _selected;
            set {
                if (_selected == value) return;
                _selected = value;
                ApplySelectedLook();
                Refresh();
            }
        }

        /// <summary>
        /// Decorates <paramref name="button"/>; a second call returns the decor already on it. <paramref name="alignLeft"/>
        /// left-aligns the label (the spec puts it at x + 58) so screens need not. <paramref name="uppercase"/> (Direction A
        /// renders every label upper-case) hides the button's own glyphs and draws the translated label upper-cased in the
        /// decor; <c>Button.Text</c> stays the raw translation key.
        /// </summary>
        public static PulpButtonDecor Apply(Button button, bool compact = false, bool alignLeft = true, bool uppercase = true) {
            for (int i = 0, n = button.GetChildCount(); i < n; i++) {
                if (button.GetChild(i) is PulpButtonDecor existing) return existing;
            }
            var decor = new PulpButtonDecor { Name = "PulpButtonDecor", _button = button, _compact = compact, _uppercase = uppercase };
            if (alignLeft) button.Alignment = HorizontalAlignment.Left;
            decor.Build();
            button.AddChild(decor);
            // A decorated button in a row of buttons keeps room for its neighbour's focus pose.
            if (button.GetParent() is HBoxContainer row) {
                int buttons = 0;
                for (int i = 0, n = row.GetChildCount(); i < n; i++) if (row.GetChild(i) is Button) buttons++;
                if (buttons > 1) PulpScreenKit.SeparateFocusRow(row);
            }
            return decor;
        }

        /// <summary>
        /// The marker state for a button's observable state. Disabled and selected win; a focused (or hovered) row shows
        /// the focus diamond even while pressed (3.2: pressed keeps the underlying state); a held unfocused row is Pressed.
        /// </summary>
        public static MarkerState ComputeMarkerState(bool disabled, bool selected, bool held, bool focusOrHover) {
            if (disabled) return MarkerState.Disabled;
            if (selected) return MarkerState.Selected;
            if (focusOrHover) return MarkerState.Focus;
            return held ? MarkerState.Pressed : MarkerState.Normal;
        }

        /// <summary>The marker's main colour for <paramref name="state"/>: its outline, or its fill when filled.</summary>
        public static Color MarkerTint(MarkerState state) => state switch {
            MarkerState.Focus => UIPalette.Focus,
            MarkerState.Selected => MarkerSelectedTint,
            MarkerState.Disabled => MarkerDisabledTint,
            _ => UIPalette.Edge
        };

        /// <summary>True when the marker draws its 16 px glow and 4 px ring (focus only).</summary>
        public static bool MarkerHasShadow(MarkerState state) => state == MarkerState.Focus;

        /// <summary>True when the diamond is filled (focus with <c>focus</c>, selected with <c>accent</c>).</summary>
        public static bool MarkerFilled(MarkerState state) => state is MarkerState.Focus or MarkerState.Selected;

        /// <summary>The diamond's 1.5 px outline colour: white when focused, else the state's tint.</summary>
        public static Color MarkerOutline(MarkerState state) => state == MarkerState.Focus ? Colors.White : MarkerTint(state);

        /// <summary>True when the outline is dashed (disabled).</summary>
        public static bool MarkerDashed(MarkerState state) => state == MarkerState.Disabled;

        private void Build() {
            _button.ClipContents = false;
            FocusPose existingPose = null;
            for (int i = 0, n = _button.GetChildCount(); i < n; i++) if (_button.GetChild(i) is FocusPose pose) existingPose = pose;
            if (existingPose == null) FocusPose.Attach(_button);

            _marker = new GlassMarker { Name = "Marker" };
            _button.AddChild(_marker);
            _labelLayer = new LabelLayer(this) { Name = "UpperLabel", MouseFilter = Control.MouseFilterEnum.Ignore };
            _labelLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _button.AddChild(_labelLayer);
            _button.MoveChild(_labelLayer, 0);
            _badge = new Control { Name = "PromptBadge", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            _button.AddChild(_badge);

            if (_button.ToggleMode) _selected = _button.ButtonPressed;
            _badgeDevice = PulpPromptGlyphs.CurrentDevice();
            _scale = UiScaleFactor;
            BuildBadge(_badgeDevice);
            _design = _button.CustomMinimumSize;
            if (_selected) ApplySelectedLook();
        }

        public override void _EnterTree() {
            if (_hooked || _button == null) return;
            _hooked = true;
            _button.FocusEntered += OnStateChanged;
            _button.FocusExited += OnStateChanged;
            _button.MouseEntered += OnMouseEntered;
            _button.MouseExited += OnStateChanged;
            _button.ButtonDown += OnStateChanged;
            _button.ButtonUp += OnStateChanged;
            _button.Toggled += OnToggled;
            _button.Resized += OnStateChanged;
            _button.VisibilityChanged += OnStateChanged;
            _button.Draw += OnButtonDraw;
            // The button's text, icon, clipping or the screen's own width changed: refresh the width once, deferred. The
            // decor's own write raises this too, and that refresh measures the same width and writes nothing.
            _button.MinimumSizeChanged += QueueRefresh;
            if (InputManager.Instance != null) InputManager.Instance.DeviceAssigned += OnDeviceAssigned;
            Refresh();
        }

        public override void _ExitTree() {
            if (!_hooked || _button == null) return;
            _hooked = false;
            if (GodotObject.IsInstanceValid(_button)) {
                _button.FocusEntered -= OnStateChanged;
                _button.FocusExited -= OnStateChanged;
                _button.MouseEntered -= OnMouseEntered;
                _button.MouseExited -= OnStateChanged;
                _button.ButtonDown -= OnStateChanged;
                _button.ButtonUp -= OnStateChanged;
                _button.Toggled -= OnToggled;
                _button.Resized -= OnStateChanged;
                _button.VisibilityChanged -= OnStateChanged;
                _button.MinimumSizeChanged -= QueueRefresh;
                _button.Draw -= OnButtonDraw;
            }
            if (InputManager.Instance != null) InputManager.Instance.DeviceAssigned -= OnDeviceAssigned;
        }

        private void OnStateChanged() => Refresh();
        private void OnDeviceAssigned(int slot, int device) => Refresh();

        // Hover is focus (UI_SPEC_GLASS AC8): a hovered chevron reads exactly as a focused one. Not while a text field or an
        // open list holds the keyboard (Codex review 1, finding 2): the row then shows the hover look and the badge stays
        // with the focus, so typing and Enter still reach the field.
        private void OnMouseEntered() {
            if (_button != null && _button.IsVisibleInTree() && !_button.Disabled
                && _button.FocusMode != Control.FocusModeEnum.None && !_button.HasFocus() && !KeyboardIsHeld(_button)) {
                _button.GrabFocus();
            }
            Refresh();
        }

        /// <summary>
        /// True when the control that has the keyboard must keep it against hover: an editable text field
        /// (<see cref="LineEdit"/>, <see cref="TextEdit"/>) or a control whose list is open (an <see cref="OptionButton"/>
        /// or <see cref="MenuButton"/> popup). Hover then draws its look without taking focus.
        /// </summary>
        public static bool KeyboardIsHeld(Control hovered) {
            Control owner = hovered?.GetViewport()?.GuiGetFocusOwner();
            return owner switch {
                null => false,
                LineEdit line => line.Editable,
                TextEdit text => text.Editable,
                OptionButton option => option.GetPopup().Visible,
                MenuButton menu => menu.GetPopup().Visible,
                _ => false
            };
        }

        private void OnToggled(bool pressed) {
            if (_button.ToggleMode) Selected = pressed;
            Refresh();
        }

        // The button redrew: its disabled flag, text or width may have changed (none has a signal). Refresh once, after the
        // draw (a refresh may write the minimum size, which must not happen while the button records its draw).
        private void OnButtonDraw() {
            if (_button == null) return;
            // The hover state is set after the mouse signals run (the engine raises mouse_entered before the button marks
            // itself hovered), so the redraw that follows is where a hover look without focus is first readable.
            if (_button.Disabled == _lastDisabled && Mathf.IsEqualApprox(_button.Size.X, _lastWidth) && _button.Text == _sourceText
                && _button.GetDrawMode() == _lastMode) return;
            QueueRefresh();
        }

        /// <summary>Queues one deferred <see cref="Refresh"/> (a theme or translation change, or the button's own redraw).</summary>
        internal void QueueRefresh() {
            if (_refreshQueued) return;
            _refreshQueued = true;
            Callable.From(RefreshQueued).CallDeferred();
        }

        private void RefreshQueued() {
            _refreshQueued = false;
            if (!IsInstanceValid(this) || _button == null || !IsInstanceValid(_button)) return;
            Refresh();
            // A theme or locale change can change the stack's widest label: the bound D6 rule runs again, once.
            if (_layoutDirty) { _layoutDirty = false; _stackRule?.Queue(); }
        }

        /// <summary>Recomputes the marker, the badge, the label and the minimum size from the button's current state.</summary>
        public void Refresh() {
            if (_button == null || !GodotObject.IsInstanceValid(_button) || _marker == null) return;
            _lastDisabled = _button.Disabled;
            _lastWidth = _button.Size.X;
            _lastMode = _button.GetDrawMode();
            string text = _button.Text;
            if (text != _sourceText) {
                _sourceText = text;
                _drawn = ToDrawnText(_button.Tr(text));
                InvalidateLayout();
            }
            if (_button.IsInsideTree() && !_glyphsHidden && _uppercase) ApplyGlyphHiding();
            float scale = UiScaleFactor;
            if (!Mathf.IsEqualApprox(scale, _scale)) { _scale = scale; InvalidateLayout(); }

            BaseButton.DrawMode mode = _button.GetDrawMode();
            bool held = mode is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed && !_button.ToggleMode;
            bool focusOrHover = _button.HasFocus() || mode is BaseButton.DrawMode.Hover or BaseButton.DrawMode.HoverPressed;
            _marker.State = ComputeMarkerState(_button.Disabled, _selected, held, focusOrHover);
            _marker.MarkerScale = _scale;
            float diagonal = _marker.Diagonal;
            _marker.Size = new Vector2(diagonal, diagonal);
            float centreX = (_compact ? CompactMarkerCentreX : MarkerCentreX) * _scale;
            _marker.Position = new Vector2(centreX - diagonal / 2f, (_button.Size.Y - diagonal) / 2f);

            PromptDevice device = PulpPromptGlyphs.CurrentDevice();
            if (device != _badgeDevice || !Mathf.IsEqualApprox(_badgeScale, _scale)) {
                _badgeDevice = device;
                BuildBadge(device);
            }
            _badge.Visible = _showPrompt && _button.HasFocus() && !_button.Disabled;
            LayoutBadge();
            if (_growToFit) GrowToLines();
            ApplyMinimumSize();
            _labelLayer?.QueueRedraw();
        }

        // ---- The size (design height x UI scale, grown lines) ----

        private void ApplyMinimumSize() {
            Vector2 current = _button.CustomMinimumSize;
            // A size the decor did not write is the screen's new design size (in 100 % units), axis by axis: a stack that
            // sets only the width (ApplyStackWidth) keeps the design height the decor scales.
            if (!_hasWritten || !Mathf.IsEqualApprox(current.X, _written.X)) _design.X = current.X;
            if (!_hasWritten || !Mathf.IsEqualApprox(current.Y, _written.Y)) _design.Y = current.Y;
            float width = _design.X, height = _design.Y > 0f ? _design.Y * _scale : 0f;
            if (_growToFit) {
                width = Mathf.Max(width, _wordMinWidth);
                height = Mathf.Max(height, _grownHeight);
            } else if (_stackRule != null) {
                width = current.X;                 // the bound D6 rule owns the width
            } else if (AutoWidens) {
                width = AutoWidth();
            }
            var want = new Vector2(width, height);
            _written = want;
            _hasWritten = true;
            // Writing only a changed size keeps this out of a loop: the resize it causes refreshes to the same value.
            if (current != want) _button.CustomMinimumSize = want;
        }

        /// <summary>
        /// True when the decor sizes the row's width itself: no width from the screen, a label it draws on one line, in the
        /// tree (where the theme resolves), and no clipping the screen asked for. A D6 stack is handled before this.
        /// </summary>
        private bool AutoWidens => _design.X <= 0f && _uppercase && _button.IsInsideTree() && !IsMultiLine
            && !_button.ClipText && _button.TextOverrunBehavior == TextServer.OverrunBehavior.NoTrimming;

        // The decor's own width for the row (ruling 5): RequiredWidth with the badge reserved when the row can show it,
        // cached on its inputs so a refresh that changes none of them measures nothing.
        private string _autoText;
        private Font _autoFont;
        private int _autoSize = -1;
        private float _autoFixed = -1f;
        private float _autoWidth;

        private float AutoWidth() {
            Font font = _button.GetThemeFont(FontName);
            int size = _button.GetThemeFontSize(FontSizeName);
            float fixedPart = RequiredFixedWidth(_button, _showPrompt && _button.FocusMode != Control.FocusModeEnum.None, _scale);
            if (ReferenceEquals(_autoText, _drawn) && ReferenceEquals(_autoFont, font) && _autoSize == size
                && Mathf.IsEqualApprox(_autoFixed, fixedPart)) {
                return _autoWidth;
            }
            _autoText = _drawn;
            _autoFont = font;
            _autoSize = size;
            _autoFixed = fixedPart;
            _autoWidth = Mathf.Ceil(fixedPart + MeasureWidth(_drawn, font, size));
            return _autoWidth;
        }

        // Recomputes the wrapped line count only when the text, width, font size or UI scale changed.
        private void GrowToLines() {
            if (!_button.IsInsideTree() || !_glyphsHidden || !_uppercase) return;
            int fontSize = _button.GetThemeFontSize(FontSizeName);
            if (_growText == _drawn && Mathf.IsEqualApprox(_growWidth, _button.Size.X) && _growFontSize == fontSize
                && Mathf.IsEqualApprox(_growScale, _scale)) return;
            if (fontSize != _growFontSize) _wordMinWidth = 0f;
            _growText = _drawn;
            _growWidth = _button.Size.X;
            _growFontSize = fontSize;
            _growScale = _scale;
            float room = LabelAvailableWidthReserved();
            TextParagraph paragraph = BuildParagraph(_drawn, room);
            int lines = Mathf.Max(1, paragraph.GetLineCount());
            StyleBox box = _button.GetThemeStylebox(NormalName);
            float chrome = ((box?.GetContentMargin(Side.Top) ?? 0f) + (box?.GetContentMargin(Side.Bottom) ?? 0f)) * _scale;
            float needed = Mathf.Ceil(chrome + lines * Mathf.Max(1f, paragraph.GetLineSize(0).Y) + 2f);
            _grownHeight = lines > 1 ? needed : 0f;
            // A single word wider than the line cannot wrap: widen the button by the shortfall (a word never breaks).
            float widest = 0f;
            for (int i = 0; i < paragraph.GetLineCount(); i++) widest = Mathf.Max(widest, paragraph.GetLineWidth(i));
            if (widest > room + 0.5f) _wordMinWidth = Mathf.Max(_wordMinWidth, _button.Size.X + (widest - room) + 2f);
        }

        private float LabelAvailableWidthReserved() => Mathf.Max(1f, LabelLimit(true) - LabelLeft());

        // ---- The prompt badge ----

        private void BuildBadge(PromptDevice device) {
            for (int i = _badge.GetChildCount() - 1; i >= 0; i--) {
                Node child = _badge.GetChild(i);
                _badge.RemoveChild(child);
                child.QueueFree();
            }
            _badgeScale = _scale;
            _badgeRing = null;
            _badgeLabel = null;
            _badgeColor = new Color(0f, 0f, 0f, 0f);
            string text = PulpPromptGlyphs.LabelFor("ui_accept", device);
            Color color = _button != null ? LabelColorNow() : Colors.White;
            if (device == PromptDevice.Pad) {
                _badgeRing = new GlassPromptRing { Name = "Disc", RingColor = color };
                _badgeRing.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                _badge.AddChild(_badgeRing);
                _badgeLabel = PulpPromptGlyphs.CreateLetter(text, UIPalette.LabelRoles.PromptKey, color);
                _badge.AddChild(_badgeLabel);
            } else {
                _badge.AddChild(PulpPromptGlyphs.CreateKeyCap(text, UIPalette.LabelRoles.PromptKey, color, BadgeSize * _scale, out _badgeLabel));
            }
        }

        private void LayoutBadge() {
            if (_badge.GetChildCount() == 0) return;
            float right = RightPadding();
            Vector2 size;
            if (_badgeDevice == PromptDevice.Pad) {
                size = new Vector2(BadgeSize, BadgeSize) * _scale;
                _badge.Size = size;
            } else {
                var cap = (Control)_badge.GetChild(0);
                size = cap.GetCombinedMinimumSize();
                cap.Position = Vector2.Zero;
                cap.Size = size;
                _badge.Size = size;
            }
            // The badge sits at the label's right end (3.2: x + W - 90 to x + W - 54 on a menu row).
            _badge.Position = new Vector2(_button.Size.X - right - size.X, Mathf.Round((_button.Size.Y - size.Y) / 2f));
            if (_badge.Visible) {
                Color color = LabelColorNow();
                if (color == _badgeColor) return;
                _badgeColor = color;
                if (_badgeRing != null) _badgeRing.RingColor = color;
                _badgeLabel?.AddThemeColorOverride("font_color", color);
                if (_badgeDevice == PromptDevice.Keyboard && _badge.GetChild(0) is PanelContainer keyCap
                    && keyCap.GetThemeStylebox("panel") is StyleBoxFlat border) {
                    border.BorderColor = color;
                }
            }
        }

        public override void _Notification(int what) {
            if (what == NotificationPredelete && _glyphsHidden && _button != null && GodotObject.IsInstanceValid(_button)) RestoreGlyphs();
        }

        // ---- The upper-case label ----

        private void ApplyGlyphHiding() {
            if (_button == null) return;
            if (!_uppercase) {
                if (_glyphsHidden) RestoreGlyphs();
                return;
            }
            // Idempotent and re-entrancy proof: the flag is raised before the first override is written, because
            // an override raises the button's theme notification, which can reach Refresh again. The saved
            // colours are only ever read from the theme, never from an override this decor set.
            if (_glyphsHidden) return;
            _glyphsHidden = true;
            _original.Clear();
            foreach (string slot in HideSlots) {
                bool had = _button.HasThemeColorOverride(slot);
                _original[slot] = (_button.GetThemeColor(slot), had);
            }
            foreach (string slot in HideSlots) _button.AddThemeColorOverride(slot, Colors.Transparent);
            _labelLayer?.QueueRedraw();
        }

        private void RestoreGlyphs() {
            foreach (var pair in _original) {
                if (pair.Value.HadOverride) _button.AddThemeColorOverride(pair.Key, pair.Value.Color);
                else _button.RemoveThemeColorOverride(pair.Key);
            }
            _original.Clear();
            _glyphsHidden = false;
            _labelLayer?.QueueRedraw();
        }

        private Color OriginalColor(string slot) =>
            _original.TryGetValue(slot, out var entry) ? entry.Color : _button.GetThemeColor(slot);

        private Color LabelColorNow() {
            if (_selected) return UIPalette.Ink;
            string slot = LabelColorSlot(_button.Disabled, _button.GetDrawMode(), _button.HasFocus());
            return OriginalColor(slot);
        }

        private TextParagraph BuildParagraph(string text, float width) {
            var paragraph = new TextParagraph();
            paragraph.AddString(text, _button.GetThemeFont(FontName), _button.GetThemeFontSize(FontSizeName));
            paragraph.Width = width;
            paragraph.Alignment = _button.Alignment;
            paragraph.BreakFlags = TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound;
            return paragraph;
        }

        /// <summary>How many lines the whole upper-cased label wraps to inside <see cref="LabelAvailableWidth"/> (before any ellipsis).</summary>
        public int WrappedLineCount => BuildParagraph(DrawnText, LabelAvailableWidth).GetLineCount();

        /// <summary>The widest wrapped line, in pixels; never wider than <see cref="LabelAvailableWidth"/> unless one word is.</summary>
        public float WidestWrappedLine {
            get {
                TextParagraph paragraph = BuildParagraph(DrawnText, LabelAvailableWidth);
                float widest = 0f;
                for (int i = 0; i < paragraph.GetLineCount(); i++) widest = Mathf.Max(widest, paragraph.GetLineWidth(i));
                return widest;
            }
        }

        private float LabelLeft() {
            float left = (_button.GetThemeStylebox(NormalName)?.GetContentMargin(Side.Left) ?? 0f) * _scale;
            Texture2D icon = _button.Icon;
            if (icon != null) left += icon.GetWidth() + _button.GetThemeConstant(HSeparationName);
            return left;
        }

        private float RightPadding() => (_button.GetThemeStylebox(NormalName)?.GetContentMargin(Side.Right) ?? 0f) * _scale;

        /// <summary>The width the label may use: from the left padding (and icon) to <see cref="LabelRightLimit"/>.</summary>
        public float LabelAvailableWidth => Mathf.Max(1f, LabelLimit() - LabelLeft());

        /// <summary>True when the label as drawn ends in an ellipsis: still too wide at its shrink floor, or more wrapped lines than the button's height holds.</summary>
        public bool LabelIsTrimmed {
            get {
                if (_button == null || !_uppercase) return false;
                EnsureLayout();
                return _layoutTrimmed;
            }
        }

        /// <summary>The width the whole upper-cased label would take on one line at the theme font size.</summary>
        public float DrawnTextWidth => MeasureWidth(DrawnText, _button.GetThemeFont(FontName), _button.GetThemeFontSize(FontSizeName));

        private static float MeasureWidth(string text, Font font, int size) =>
            font == null ? 0f : font.GetStringSize(text ?? "", HorizontalAlignment.Left, -1f, size).X;

        private float LabelLimit(bool reserveBadge = false) {
            float right = RightPadding();
            bool badgeShown = _badge != null && _badge.Visible;
            if (_showPrompt && (badgeShown || reserveBadge || _growToFit)) {
                float badgeWidth = _badge != null && _badge.Size.X > 0f ? _badge.Size.X : BadgeSize * _scale;
                right += badgeWidth + BadgeGap * _scale;
            }
            return _button.Size.X - right;
        }

        private bool IsMultiLine => _button.AutowrapMode != TextServer.AutowrapMode.Off || _drawn.Contains('\n') || _growToFit;

        private void InvalidateLayout() {
            _layoutText = null;
            _layoutFont = null;
        }

        // Builds the cached TextLine or TextParagraph for the current text, font, size and width; nothing when unchanged.
        private void EnsureLayout() {
            if (_button == null) return;
            if (_sourceText == null) { _sourceText = _button.Text; _drawn = ToDrawnText(_button.Tr(_sourceText)); }
            Font font = _button.GetThemeFont(FontName);
            int size = _button.GetThemeFontSize(FontSizeName);
            float left = LabelLeft();
            float width = Mathf.Max(1f, LabelLimit() - left);
            float fitWidth = Mathf.Max(1f, LabelLimit(_showPrompt && _button.FocusMode != Control.FocusModeEnum.None) - left);
            bool multi = IsMultiLine;
            StyleBox box = _button.GetThemeStylebox(NormalName);
            float availHeight = _button.Size.Y - ((box?.GetContentMargin(Side.Top) ?? 0f) + (box?.GetContentMargin(Side.Bottom) ?? 0f)) * _scale;
            // The height counts only for a wrapping label, where it decides how many lines show (Codex review 1, finding 3).
            float height = multi ? availHeight : -1f;
            if (ReferenceEquals(_layoutText, _drawn) && ReferenceEquals(_layoutFont, font) && _layoutSize == size
                && Mathf.IsEqualApprox(_layoutWidth, width) && Mathf.IsEqualApprox(_layoutFitWidth, fitWidth)
                && Mathf.IsEqualApprox(_layoutHeight, height) && _layoutAlign == _button.Alignment && _layoutMulti == multi) {
                return;
            }
            LayoutBuilds++;
            _layoutText = _drawn;
            _layoutFont = font;
            _layoutSize = size;
            _layoutWidth = width;
            _layoutFitWidth = fitWidth;
            _layoutHeight = height;
            _layoutAlign = _button.Alignment;
            _layoutMulti = multi;
            if (multi) {
                _paragraph.Clear();
                _paragraph.AddString(_drawn, font, size);
                _paragraph.Width = width;
                _paragraph.Alignment = _button.Alignment;
                _paragraph.BreakFlags = TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound;
                _paragraph.MaxLinesVisible = -1;
                _paragraph.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
                float lineHeight = Mathf.Max(1f, _paragraph.GetLineSize(0).Y);
                _layoutMaxLines = Mathf.Max(1, Mathf.FloorToInt((availHeight + 1f) / lineHeight));
                _layoutTrimmed = _paragraph.GetLineCount() > _layoutMaxLines;
                if (_layoutTrimmed) {
                    _paragraph.MaxLinesVisible = _layoutMaxLines;
                    _paragraph.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                }
                _layoutFitted = size;
                return;
            }
            // One line: shrink toward the floor until it fits the room left with the badge reserved (so the label does
            // not change size as focus moves), then ellipsize at the current room.
            int fitted = FittedSize(_drawn, font, size, fitWidth, ShrinkFloorFor(_compact));
            _layoutFitted = fitted;
            _line.Clear();
            _line.AddString(_drawn, font, fitted);
            _line.Width = width;
            _line.Alignment = _button.Alignment;
            _line.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            _layoutTrimmed = MeasureWidth(_drawn, font, fitted) > width + 0.5f;
        }

        /// <summary>The largest size from <paramref name="size"/> down to <paramref name="floor"/> at which <paramref name="text"/> fits <paramref name="width"/>.</summary>
        private static int FittedSize(string text, Font font, int size, float width, int floor) {
            if (font == null || size <= floor || MeasureWidth(text, font, size) <= width) return size;
            float natural = MeasureWidth(text, font, size);
            int guess = Mathf.Clamp(Mathf.FloorToInt(size * width / Mathf.Max(1f, natural)), floor, size);
            while (guess > floor && MeasureWidth(text, font, guess) > width) guess--;
            while (guess + 1 < size && MeasureWidth(text, font, guess + 1) <= width) guess++;
            return guess;
        }

        private void DrawLabel(CanvasItem layer) {
            if (string.IsNullOrEmpty(_sourceText) || !_uppercase || !_glyphsHidden) return;
            EnsureLayout();
            StyleBox box = _button.GetThemeStylebox(NormalName);
            float top = (box?.GetContentMargin(Side.Top) ?? 0f) * _scale;
            float bottom = (box?.GetContentMargin(Side.Bottom) ?? 0f) * _scale;
            float availHeight = _button.Size.Y - top - bottom;
            float left = LabelLeft();
            Color color = LabelColorNow();
            Color outlineColor = OriginalColor("font_outline_color");
            int outline = _button.GetThemeConstant(OutlineSizeName);
            Rid canvas = layer.GetCanvasItem();
            if (_layoutMulti) {
                float height = Mathf.Min(_paragraph.GetSize().Y, availHeight + 1f);
                var pos = new Vector2(left, top + (availHeight - height) / 2f);
                if (outline > 0 && outlineColor.A > 0f) _paragraph.DrawOutline(canvas, pos, outline, outlineColor);
                _paragraph.Draw(canvas, pos, color);
            } else {
                var pos = new Vector2(left, top + (availHeight - _line.GetSize().Y) / 2f);
                if (outline > 0 && outlineColor.A > 0f) _line.DrawOutline(canvas, pos, outline, outlineColor);
                _line.Draw(canvas, pos, color);
            }
        }

        private partial class LabelLayer : Control {
            private readonly PulpButtonDecor _owner;
            public LabelLayer(PulpButtonDecor owner) { _owner = owner; }

            public override void _Notification(int what) {
                // A theme change (the UI scale) or a locale change reaches the label layer: refresh once it has propagated.
                if (what == NotificationThemeChanged || what == NotificationTranslationChanged) {
                    if (what == NotificationTranslationChanged) _owner._sourceText = null;
                    _owner.InvalidateLayout();
                    _owner._layoutDirty = true;
                    _owner.QueueRefresh();
                    QueueRedraw();
                }
            }

            public override void _Draw() {
                if (_owner._button == null) return;
                _owner.DrawLabel(this);
            }
        }

        // ---- The selected look ----

        private void ApplySelectedLook() {
            if (_button == null) return;
            if (_selected) {
                _hasSavedNormal = _button.HasThemeStyleboxOverride(NormalName);
                _savedNormal = _hasSavedNormal ? _button.GetThemeStylebox(NormalName) : null;
                _hasSavedHover = _button.HasThemeStyleboxOverride(HoverName);
                _savedHover = _hasSavedHover ? _button.GetThemeStylebox(HoverName) : null;
                StyleBox plate = BuildSelectedPlate();
                if (plate != null) {
                    _button.AddThemeStyleboxOverride(NormalName, plate);
                    _button.AddThemeStyleboxOverride(HoverName, plate);
                }
                _savedColors.Clear();
                if (!_uppercase) {
                    foreach (string slot in FontColorSlots) {
                        _savedColors[slot] = _button.HasThemeColorOverride(slot) ? _button.GetThemeColor(slot) : null;
                        _button.AddThemeColorOverride(slot, UIPalette.Ink);
                    }
                }
            } else {
                Restore(NormalName, _hasSavedNormal, _savedNormal);
                Restore(HoverName, _hasSavedHover, _savedHover);
                foreach (var pair in _savedColors) {
                    if (pair.Value.HasValue) _button.AddThemeColorOverride(pair.Key, pair.Value.Value);
                    else _button.RemoveThemeColorOverride(pair.Key);
                }
                _savedColors.Clear();
            }
        }

        private void Restore(StringName slot, bool had, StyleBox saved) {
            if (had && saved != null) _button.AddThemeStyleboxOverride(slot, saved);
            else _button.RemoveThemeStyleboxOverride(slot);
        }

        // The theme's selected chevron (TG-THEME: every chevron variation carries styles/selected = glass_btn_selected),
        // else the kit's plate with the normal box's paddings.
        private StyleBox BuildSelectedPlate() {
            if (_button.HasThemeStylebox(SelectedName)) return _button.GetThemeStylebox(SelectedName);
            StyleBoxTexture plate = PulpKit.Plate("glass_btn_selected");
            if (plate == null) return null;
            StyleBox current = _button.GetThemeStylebox(NormalName);
            if (current != null) {
                plate.ContentMarginLeft = current.GetContentMargin(Side.Left);
                plate.ContentMarginTop = current.GetContentMargin(Side.Top);
                plate.ContentMarginRight = current.GetContentMargin(Side.Right);
                plate.ContentMarginBottom = current.GetContentMargin(Side.Bottom);
            }
            return plate;
        }

        // ---- The stack width rule (UI_SPEC_GLASS 3.2, D6) ----

        /// <summary>
        /// The width a chevron needs for its label on one line at the theme size: its paddings (at the UI scale), an icon and
        /// its separation if it has one, the label, and when <paramref name="reservePrompt"/> the badge and its gap:
        /// <c>58 + label + 16 + 36 + 54</c> (compact <c>52 + label + 40</c> with no prompt). "CALIBRATION DRILLS" at Cinzel 700
        /// 34 px needs 636 in the mock's browser and 616 in the build (Godot's tracking is a whole 4 px, not 4.42).
        /// </summary>
        public static float RequiredWidth(Button button, bool reservePrompt = true) {
            if (button == null) return 0f;
            float label = MeasureWidth(ToDrawnText(button.Tr(button.Text)), button.GetThemeFont(FontName), button.GetThemeFontSize(FontSizeName));
            return Mathf.Ceil(RequiredFixedWidth(button, reservePrompt, UiScaleFactor) + label);
        }

        // Everything a row's width holds but the label: its paddings at the UI scale, an icon and its separation, and the
        // badge reserve when asked.
        private static float RequiredFixedWidth(Button button, bool reservePrompt, float scale) {
            StyleBox box = button.GetThemeStylebox(NormalName);
            float paddings = ((box?.GetContentMargin(Side.Left) ?? 0f) + (box?.GetContentMargin(Side.Right) ?? 0f)) * scale;
            float icon = button.Icon != null ? button.Icon.GetWidth() + button.GetThemeConstant(HSeparationName) : 0f;
            return paddings + icon + (reservePrompt ? BadgeReserve * scale : 0f);
        }

        /// <summary>
        /// The D6 rule: every button of one stack shares the widest <see cref="RequiredWidth"/>, at least
        /// <paramref name="minWidth"/> (menu and pause 560, compact 290) and at most <paramref name="cap"/> (700 for menu
        /// stacks); a label still too long then shrinks and ellipsizes in its decor. Writes each button's minimum width
        /// (its height is kept), sets <c>ClipText</c> so the button's own hidden glyphs cannot widen it past the cap, and
        /// returns the shared width.
        ///
        /// <para>The rule stays bound to the stack's decorated buttons: after a theme change reaches them (entering the
        /// tree, a UI-scale change) or a locale change, it runs again once, deferred. So a stack built before its screen is
        /// in the tree (its theme does not resolve yet, and the first width is only the minimum) or rescaled later still
        /// gets its D6 width. <paramref name="minWidth"/> and <paramref name="cap"/> are not scaled; a later call replaces
        /// the binding.</para>
        /// </summary>
        public static float ApplyStackWidth(IReadOnlyList<Button> stack, float minWidth, float cap = float.PositiveInfinity, bool reservePrompt = true) {
            if (stack == null || stack.Count == 0) return 0f;
            var rule = new StackWidthRule(stack, minWidth, cap, reservePrompt);
            foreach (Button button in rule.Buttons) {
                if (button == null || !IsInstanceValid(button)) continue;
                for (int i = 0, n = button.GetChildCount(); i < n; i++) {
                    if (button.GetChild(i) is PulpButtonDecor decor) decor._stackRule = rule;
                }
            }
            return rule.Apply();
        }

        private StackWidthRule _stackRule;

        /// <summary>One stack's D6 binding, shared by its decors; a theme or locale change queues one deferred pass.</summary>
        private sealed class StackWidthRule {
            public readonly Button[] Buttons;
            private readonly float _min, _cap;
            private readonly bool _reserve;
            private bool _queued;

            public StackWidthRule(IReadOnlyList<Button> stack, float min, float cap, bool reserve) {
                Buttons = new Button[stack.Count];
                for (int i = 0; i < stack.Count; i++) Buttons[i] = stack[i];
                _min = min;
                _cap = cap;
                _reserve = reserve;
            }

            public float Apply() {
                float width = _min;
                foreach (Button button in Buttons) {
                    if (button != null && IsInstanceValid(button) && button.Visible) width = Mathf.Max(width, RequiredWidth(button, _reserve));
                }
                width = Mathf.Min(width, Mathf.Max(_min, _cap));
                foreach (Button button in Buttons) {
                    if (button == null || !IsInstanceValid(button)) continue;
                    // The decor draws the label, so the button's own (hidden) glyphs must not widen it past the cap.
                    button.ClipText = true;
                    if (!Mathf.IsEqualApprox(button.CustomMinimumSize.X, width)) button.CustomMinimumSize = new Vector2(width, button.CustomMinimumSize.Y);
                }
                return width;
            }

            public void Queue() {
                if (_queued) return;
                _queued = true;
                Callable.From(Run).CallDeferred();
            }

            private void Run() {
                _queued = false;
                Apply();
            }
        }
    }
}
