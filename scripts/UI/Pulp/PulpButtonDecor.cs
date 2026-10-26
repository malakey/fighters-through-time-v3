using System.Collections.Generic;
using FTT.Core;
using FTT.UI.Motion;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// UR-WIDGETS. Decorates a banner button with the pulp marker, the MOT-B focus pose, the prompt badge on
    /// the focused row and the paper "selected" look (UI_SPEC 3.2). Call <see cref="Apply"/> once per button
    /// (a second call returns the existing decor). The decor and its marker / badge are children of the
    /// button, so the focus pose carries them along.
    /// </summary>
    public partial class PulpButtonDecor : Node {

        /// <summary>The marker's look in one state.</summary>
        public enum MarkerState { Normal, Focus, Selected, Pressed, Disabled }

        public static readonly Color MarkerSelectedTint = new(0xE0 / 255f, 0x41 / 255f, 0x2F / 255f);
        public static readonly Color MarkerDisabledTint = new(0x4A / 255f, 0x43 / 255f, 0x33 / 255f);

        private const float BadgeEdgeInset = 30f;
        private const float PadBadgeSize = 36f;

        private Button _button;
        private bool _compact;
        private PulpIcon _marker;
        private Control _badge;
        private bool _selected;
        private PromptDevice _badgeDevice;
        private bool _hooked;
        private bool _hasSavedNormal, _hasSavedHover;
        private StyleBox _savedNormal, _savedHover;
        private readonly Dictionary<string, Color?> _savedColors = new();
        private bool _uppercase = true;
        private bool _glyphsHidden;
        private readonly Dictionary<string, (Color Color, bool HadOverride)> _original = new();
        private LabelLayer _labelLayer;
        private string _lastText = "";
        private float _lastWidth;
        private bool _growToFit;
        private float _baseMinHeight;
        private float _baseMinWidth;
        private float _wordMinWidth;
        private int _growFontSize;
        private string _growKey = "";

        private static readonly string[] HideSlots = {
            "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color",
            "font_disabled_color", "font_outline_color"
        };

        private static readonly string[] FontColorSlots = {
            "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color"
        };

        /// <summary>
        /// UR-POLISH. When true a label too long for one line wraps instead of ending in an ellipsis, and the button grows
        /// its minimum height to hold the lines. The wrap always leaves room for the focused row's prompt badge, so the
        /// button does not change height as focus moves. Off by default: menu rows are authored to fit one line.
        /// </summary>
        public bool GrowToFit {
            get => _growToFit;
            set {
                if (_growToFit == value || _button == null) return;
                _growToFit = value;
                if (value) { _baseMinHeight = _button.CustomMinimumSize.Y; _baseMinWidth = _button.CustomMinimumSize.X; }
                else _button.CustomMinimumSize = new Vector2(_baseMinWidth, _baseMinHeight);
                _growKey = "";
                Refresh();
            }
        }

        /// <summary>The marker icon (tint and shadow follow the state).</summary>
        public PulpIcon Marker => _marker;
        /// <summary>The prompt badge control; visible only while the button has focus.</summary>
        public Control Badge => _badge;
        /// <summary>The device family the badge currently shows.</summary>
        public PromptDevice BadgeDevice => _badgeDevice;
        public Button Button => _button;

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

        /// <summary>The colour the label draws in right now (ink-dark when selected, else the theme colour of the state).</summary>
        public Color CurrentLabelColor => LabelColorNow();

        /// <summary>The right limit (from the button's left) the label may reach: the right content margin, or the badge's room on the focused row.</summary>
        public float LabelRightLimit => LabelLimit();

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

        /// <summary>True while the paper selected plate is shown. In ToggleMode it follows <c>ButtonPressed</c>.</summary>
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
        /// left-aligns the label (the spec puts it at x + 64) so screens need not. <paramref name="uppercase"/> (the
        /// spec renders banner labels upper-case) hides the button's own glyphs and draws the translated label
        /// upper-cased in the decor; <c>Button.Text</c> stays the raw translation key.
        /// </summary>
        public static PulpButtonDecor Apply(Button button, bool compact = false, bool alignLeft = true, bool uppercase = true) {
            for (int i = 0, n = button.GetChildCount(); i < n; i++) {
                if (button.GetChild(i) is PulpButtonDecor existing) return existing;
            }
            var decor = new PulpButtonDecor { Name = "PulpButtonDecor", _button = button, _compact = compact, _uppercase = uppercase };
            if (alignLeft) button.Alignment = HorizontalAlignment.Left;
            decor.Build();
            button.AddChild(decor);
            return decor;
        }

        /// <summary>The marker state for a button's observable state (disabled, selected, held, focused or hovered).</summary>
        public static MarkerState ComputeMarkerState(bool disabled, bool selected, bool held, bool focusOrHover) {
            if (disabled) return MarkerState.Disabled;
            if (selected) return MarkerState.Selected;
            if (held) return MarkerState.Pressed;
            return focusOrHover ? MarkerState.Focus : MarkerState.Normal;
        }

        /// <summary>The marker tint for <paramref name="state"/>.</summary>
        public static Color MarkerTint(MarkerState state) => state switch {
            MarkerState.Focus => UIPalette.LabelDark,
            MarkerState.Selected => MarkerSelectedTint,
            MarkerState.Disabled => MarkerDisabledTint,
            _ => UIPalette.Ink3
        };

        /// <summary>True when the marker keeps its drop shadow in <paramref name="state"/> (none on a focused or disabled row).</summary>
        public static bool MarkerHasShadow(MarkerState state) => state is not (MarkerState.Focus or MarkerState.Disabled);

        private void Build() {
            _button.ClipContents = false;
            FocusPose existingPose = null;
            for (int i = 0, n = _button.GetChildCount(); i < n; i++) if (_button.GetChild(i) is FocusPose pose) existingPose = pose;
            if (existingPose == null) FocusPose.Attach(_button);

            _marker = new PulpIcon { Name = "Marker", IconName = "marker", Tint = UIPalette.Ink3 };
            _button.AddChild(_marker);
            _labelLayer = new LabelLayer(this) { Name = "UpperLabel", MouseFilter = Control.MouseFilterEnum.Ignore };
            _labelLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _button.AddChild(_labelLayer);
            _button.MoveChild(_labelLayer, 0);
            _badge = new Control { Name = "PromptBadge", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            _button.AddChild(_badge);

            if (_button.ToggleMode) _selected = _button.ButtonPressed;
            _badgeDevice = PulpPromptGlyphs.CurrentDevice();
            BuildBadge(_badgeDevice);
            if (_selected) ApplySelectedLook();
        }

        public override void _EnterTree() {
            if (_hooked || _button == null) return;
            _hooked = true;
            _button.FocusEntered += OnStateChanged;
            _button.FocusExited += OnStateChanged;
            _button.MouseEntered += OnStateChanged;
            _button.MouseExited += OnStateChanged;
            _button.ButtonDown += OnStateChanged;
            _button.ButtonUp += OnStateChanged;
            _button.Toggled += OnToggled;
            _button.Resized += OnStateChanged;
            _button.VisibilityChanged += OnStateChanged;
            if (InputManager.Instance != null) InputManager.Instance.DeviceAssigned += OnDeviceAssigned;
            SetProcess(_button.IsVisibleInTree());
            Refresh();
        }

        public override void _ExitTree() {
            if (!_hooked || _button == null) return;
            _hooked = false;
            if (GodotObject.IsInstanceValid(_button)) {
                _button.FocusEntered -= OnStateChanged;
                _button.FocusExited -= OnStateChanged;
                _button.MouseEntered -= OnStateChanged;
                _button.MouseExited -= OnStateChanged;
                _button.ButtonDown -= OnStateChanged;
                _button.ButtonUp -= OnStateChanged;
                _button.Toggled -= OnToggled;
                _button.Resized -= OnStateChanged;
                _button.VisibilityChanged -= OnStateChanged;
            }
            if (InputManager.Instance != null) InputManager.Instance.DeviceAssigned -= OnDeviceAssigned;
        }

        public override void _Process(double delta) {
            // The Disabled flag has no signal: poll it, and re-check the cheap state only when it moved.
            if (_button == null) return;
            if (_growToFit) GrowToLines();
            if (_lastDisabled != _button.Disabled || _lastText != _button.Text || _lastWidth != _button.Size.X) {
                _lastText = _button.Text;
                _lastWidth = _button.Size.X;
                Refresh();
            }
        }

        private bool _lastDisabled;

        private void OnStateChanged() {
            // A button on a hidden screen needs no per-frame poll of Disabled / Text / width.
            if (_button != null && GodotObject.IsInstanceValid(_button)) SetProcess(_button.IsVisibleInTree());
            Refresh();
        }
        private void OnDeviceAssigned(int slot, int device) => Refresh();

        private void OnToggled(bool pressed) {
            if (_button.ToggleMode) Selected = pressed;
            Refresh();
        }

        // Recomputes the wrapped line count only when the text, width, font size or UI scale changed (the key), so the
        // per-frame poll allocates nothing while the label is steady.
        private void GrowToLines() {
            if (!_button.IsInsideTree() || !_button.IsVisibleInTree() || !_glyphsHidden || !_uppercase) return;
            string key = _button.Text + "|" + _button.Size.X + "|" + _button.GetThemeFontSize("font_size") + "|" + _button.Tr(_button.Text);
            if (key == _growKey) return;
            _growKey = key;
            TextParagraph paragraph = BuildParagraph(DrawnText, LabelAvailableWidthReserved());
            int lines = Mathf.Max(1, paragraph.GetLineCount());
            StyleBox box = _button.GetThemeStylebox("normal");
            float chrome = (box?.GetContentMargin(Side.Top) ?? 0f) + (box?.GetContentMargin(Side.Bottom) ?? 0f);
            float needed = Mathf.Ceil(chrome + lines * Mathf.Max(1f, paragraph.GetLineSize(0).Y) + 2f);
            float height = Mathf.Max(_baseMinHeight, lines > 1 ? needed : 0f);
            // A single word wider than the line cannot wrap: widen the button by the shortfall (a word never breaks).
            float widest = 0f;
            for (int i = 0; i < paragraph.GetLineCount(); i++) widest = Mathf.Max(widest, paragraph.GetLineWidth(i));
            float room = LabelAvailableWidthReserved();
            int fontSize = _button.GetThemeFontSize("font_size");
            if (fontSize != _growFontSize) { _growFontSize = fontSize; _wordMinWidth = 0f; }
            if (widest > room + 0.5f) _wordMinWidth = Mathf.Max(_wordMinWidth, _button.Size.X + (widest - room) + 2f);
            float width = Mathf.Max(_baseMinWidth, _wordMinWidth);
            if (!Mathf.IsEqualApprox(_button.CustomMinimumSize.Y, height) || !Mathf.IsEqualApprox(_button.CustomMinimumSize.X, width)) {
                _button.CustomMinimumSize = new Vector2(width, height);
                _growKey = "";
            }
        }

        private float LabelAvailableWidthReserved() => Mathf.Max(1f, LabelLimit(true) - LabelLeft());

        /// <summary>Recomputes the marker, the badge and the layout from the button's current state.</summary>
        public void Refresh() {
            if (_button == null || !GodotObject.IsInstanceValid(_button) || _marker == null) return;
            _lastDisabled = _button.Disabled;
            if (_button.IsInsideTree() && !_glyphsHidden && _uppercase) ApplyGlyphHiding();
            _labelLayer?.QueueRedraw();
            BaseButton.DrawMode mode = _button.GetDrawMode();
            bool held = mode is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed
                && !_button.ToggleMode;
            bool focusOrHover = _button.HasFocus() || mode is BaseButton.DrawMode.Hover or BaseButton.DrawMode.HoverPressed;
            MarkerState state = ComputeMarkerState(_button.Disabled, _selected, held, focusOrHover);
            _marker.Tint = MarkerTint(state);
            _marker.ShadowEnabled = MarkerHasShadow(state);

            Vector2 markerSize = _compact ? new Vector2(16f, 22f) : new Vector2(20f, 28f);
            _marker.Size = markerSize;
            _marker.Position = new Vector2(_compact ? 20f : 26f, Mathf.Round((_button.Size.Y - markerSize.Y) / 2f));

            PromptDevice device = PulpPromptGlyphs.CurrentDevice();
            if (device != _badgeDevice) {
                _badgeDevice = device;
                BuildBadge(device);
            }
            _badge.Visible = _button.HasFocus() && !_button.Disabled;
            LayoutBadge();
        }

        private void BuildBadge(PromptDevice device) {
            for (int i = _badge.GetChildCount() - 1; i >= 0; i--) {
                Node child = _badge.GetChild(i);
                _badge.RemoveChild(child);
                child.QueueFree();
            }
            string text = PulpPromptGlyphs.LabelFor("ui_accept", device);
            if (device == PromptDevice.Pad) {
                var disc = new PulpIcon { Name = "Disc", IconName = "prompt_circle", Tint = UIPalette.LabelDark, Flat = true };
                disc.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                _badge.AddChild(disc);
                _badge.AddChild(PulpPromptGlyphs.CreateLetter(text, UIPalette.LabelRoles.PromptKey, UIPalette.LabelDark));
            } else {
                _badge.AddChild(PulpPromptGlyphs.CreateKeyCap(text, UIPalette.LabelRoles.PromptKey, UIPalette.PromptCream, 34f, out _));
            }
        }

        private void LayoutBadge() {
            if (_badge.GetChildCount() == 0) return;
            Vector2 size;
            if (_badgeDevice == PromptDevice.Pad) {
                size = new Vector2(PadBadgeSize, PadBadgeSize);
                _badge.Size = size;
                _badge.Position = new Vector2(_button.Size.X - BadgeEdgeInset - size.X, Mathf.Round((_button.Size.Y - size.Y) / 2f));
            } else {
                var cap = (Control)_badge.GetChild(0);
                size = cap.GetCombinedMinimumSize();
                cap.Position = Vector2.Zero;
                cap.Size = size;
                _badge.Size = size;
                // The cap's art extends 2 px outside its rect, so the pill's right edge is at the inset.
                _badge.Position = new Vector2(_button.Size.X - BadgeEdgeInset - size.X, Mathf.Round((_button.Size.Y - size.Y) / 2f));
            }
        }

        public override void _Notification(int what) {
            if (what == NotificationPredelete && _glyphsHidden && _button != null && GodotObject.IsInstanceValid(_button)) RestoreGlyphs();
        }

        // ---- the upper-case label ----

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
            if (_selected) return UIPalette.LabelDark;
            string slot = LabelColorSlot(_button.Disabled, _button.GetDrawMode(), _button.HasFocus());
            return OriginalColor(slot);
        }

        private TextParagraph BuildParagraph(string text, float width) {
            var paragraph = new TextParagraph();
            paragraph.AddString(text, _button.GetThemeFont("font"), _button.GetThemeFontSize("font_size"));
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
            float left = _button.GetThemeStylebox("normal")?.GetContentMargin(Side.Left) ?? 0f;
            Texture2D icon = _button.Icon;
            if (icon != null) left += icon.GetWidth() + _button.GetThemeConstant("h_separation");
            return left;
        }

        /// <summary>The width the label may use: from the left content margin (and icon) to <see cref="LabelRightLimit"/>.</summary>
        public float LabelAvailableWidth => Mathf.Max(1f, LabelLimit() - LabelLeft());

        /// <summary>True when the label as drawn ends in an ellipsis: too wide for its single line, or more wrapped lines than the button's height holds.</summary>
        public bool LabelIsTrimmed {
            get {
                if (_button == null || !_uppercase) return false;
                bool multi = _button.AutowrapMode != TextServer.AutowrapMode.Off || DrawnText.Contains((char)10) || _growToFit;
                if (!multi) return DrawnTextWidth > LabelAvailableWidth + 0.5f;
                StyleBox box = _button.GetThemeStylebox("normal");
                float avail = _button.Size.Y - (box?.GetContentMargin(Side.Top) ?? 0f) - (box?.GetContentMargin(Side.Bottom) ?? 0f);
                TextParagraph paragraph = BuildParagraph(DrawnText, LabelAvailableWidth);
                int maxLines = Mathf.Max(1, Mathf.FloorToInt((avail + 1f) / Mathf.Max(1f, paragraph.GetLineSize(0).Y)));
                return paragraph.GetLineCount() > maxLines;
            }
        }

        /// <summary>The width the whole upper-cased label would take on one line, in the button's own font and size.</summary>
        public float DrawnTextWidth {
            get {
                var line = new TextLine();
                line.AddString(DrawnText, _button.GetThemeFont("font"), _button.GetThemeFontSize("font_size"));
                return line.GetSize().X;
            }
        }

        private float LabelLimit(bool reserveBadge = false) {
            float right = _button.GetThemeStylebox("normal")?.GetContentMargin(Side.Right) ?? 0f;
            if (_badge != null && (_badge.Visible || ((reserveBadge || _growToFit) && _badge.Size.X > 0f))) {
                right = Mathf.Max(right, _button.Size.X - _badge.Position.X + 6f);
            }
            return _button.Size.X - right;
        }

        private partial class LabelLayer : Control {
            private readonly PulpButtonDecor _owner;
            public LabelLayer(PulpButtonDecor owner) { _owner = owner; }

            public override void _Notification(int what) {
                if (what == NotificationThemeChanged || what == NotificationTranslationChanged) QueueRedraw();
            }

            public override void _Draw() {
                Button button = _owner._button;
                if (button == null || !_owner._uppercase || !_owner._glyphsHidden || string.IsNullOrEmpty(button.Text)) return;
                string text = _owner.DrawnText;
                Font font = button.GetThemeFont("font");
                int fontSize = button.GetThemeFontSize("font_size");
                StyleBox box = button.GetThemeStylebox("normal");
                float left = _owner.LabelLeft();
                float top = box?.GetContentMargin(Side.Top) ?? 0f;
                float bottom = box?.GetContentMargin(Side.Bottom) ?? 0f;
                float limit = _owner.LabelLimit();
                float width = Mathf.Max(1f, limit - left);
                float availHeight = button.Size.Y - top - bottom;
                Color color = _owner.LabelColorNow();
                Color outlineColor = _owner.OriginalColor("font_outline_color");
                int outline = button.GetThemeConstant("outline_size");
                Rid canvas = GetCanvasItem();
                bool multi = button.AutowrapMode != TextServer.AutowrapMode.Off || text.Contains('\n') || _owner._growToFit;
                if (multi) {
                    TextParagraph paragraph = _owner.BuildParagraph(text, width);
                    float lineHeight = Mathf.Max(1f, paragraph.GetLineSize(0).Y);
                    int maxLines = Mathf.Max(1, Mathf.FloorToInt((availHeight + 1f) / lineHeight));
                    if (paragraph.GetLineCount() > maxLines) {
                        paragraph.MaxLinesVisible = maxLines;
                        paragraph.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                    }
                    float height = Mathf.Min(paragraph.GetSize().Y, availHeight + 1f);
                    Vector2 pos = new(left, top + (availHeight - height) / 2f);
                    if (outline > 0 && outlineColor.A > 0f) paragraph.DrawOutline(canvas, pos, outline, outlineColor);
                    paragraph.Draw(canvas, pos, color);
                } else {
                    var line = new TextLine();
                    line.AddString(text, font, fontSize);
                    line.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                    line.Width = width;
                    line.Alignment = button.Alignment;
                    Vector2 pos = new(left, top + (availHeight - line.GetSize().Y) / 2f);
                    if (outline > 0 && outlineColor.A > 0f) line.DrawOutline(canvas, pos, outline, outlineColor);
                    line.Draw(canvas, pos, color);
                }
            }
        }

        // ---- the selected (paper) look ----

        private void ApplySelectedLook() {
            if (_button == null) return;
            if (_selected) {
                _hasSavedNormal = _button.HasThemeStyleboxOverride("normal");
                _savedNormal = _hasSavedNormal ? _button.GetThemeStylebox("normal") : null;
                _hasSavedHover = _button.HasThemeStyleboxOverride("hover");
                _savedHover = _hasSavedHover ? _button.GetThemeStylebox("hover") : null;
                StyleBoxTexture plate = BuildSelectedPlate();
                if (plate != null) {
                    _button.AddThemeStyleboxOverride("normal", plate);
                    _button.AddThemeStyleboxOverride("hover", plate);
                }
                _savedColors.Clear();
                if (!_uppercase) {
                    foreach (string slot in FontColorSlots) {
                        _savedColors[slot] = _button.HasThemeColorOverride(slot) ? _button.GetThemeColor(slot) : null;
                        _button.AddThemeColorOverride(slot, UIPalette.LabelDark);
                    }
                }
            } else {
                Restore("normal", _hasSavedNormal, _savedNormal);
                Restore("hover", _hasSavedHover, _savedHover);
                foreach (var pair in _savedColors) {
                    if (pair.Value.HasValue) _button.AddThemeColorOverride(pair.Key, pair.Value.Value);
                    else _button.RemoveThemeColorOverride(pair.Key);
                }
                _savedColors.Clear();
            }
        }

        private void Restore(string slot, bool had, StyleBox saved) {
            if (had && saved != null) _button.AddThemeStyleboxOverride(slot, saved);
            else _button.RemoveThemeStyleboxOverride(slot);
        }

        /// <summary>The selected plate height for a button variation: 80 PulpButton, 64 TemporalGlassButton, else 58.</summary>
        public static int SelectedPlateHeight(string variation) => variation switch {
            UIPalette.Variations.PulpButton => 80,
            UIPalette.Variations.TemporalGlassButton => 64,
            _ => 58
        };

        private StyleBoxTexture BuildSelectedPlate() {
            StyleBoxTexture plate = PulpKit.Plate("btn_selected_h" + SelectedPlateHeight(_button.ThemeTypeVariation));
            if (plate == null) return null;
            // Keep the label where the theme put it.
            StyleBox current = _button.GetThemeStylebox("normal");
            if (current != null) {
                plate.ContentMarginLeft = current.GetContentMargin(Side.Left);
                plate.ContentMarginTop = current.GetContentMargin(Side.Top);
                plate.ContentMarginRight = current.GetContentMargin(Side.Right);
                plate.ContentMarginBottom = current.GetContentMargin(Side.Bottom);
            }
            return plate;
        }
    }
}
