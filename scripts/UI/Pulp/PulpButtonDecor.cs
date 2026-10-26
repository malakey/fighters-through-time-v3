using System.Collections.Generic;
using System.Text.Json;
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

        private const string KitJsonPath = "res://assets/sprites/ui/pulp/pulp_kit.json";
        private const string KitDirectory = "res://assets/sprites/ui/pulp/";
        private const float BadgeEdgeInset = 30f;
        private const float PadBadgeSize = 36f;

        private static Dictionary<string, JsonElement> _kit;

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

        private static readonly string[] FontColorSlots = {
            "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color"
        };

        /// <summary>The marker icon (tint and shadow follow the state).</summary>
        public PulpIcon Marker => _marker;
        /// <summary>The prompt badge control; visible only while the button has focus.</summary>
        public Control Badge => _badge;
        /// <summary>The device family the badge currently shows.</summary>
        public PromptDevice BadgeDevice => _badgeDevice;
        public Button Button => _button;

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

        /// <summary>Decorates <paramref name="button"/>; a second call returns the decor already on it.</summary>
        public static PulpButtonDecor Apply(Button button, bool compact = false) {
            foreach (Node child in button.GetChildren()) {
                if (child is PulpButtonDecor existing) return existing;
            }
            var decor = new PulpButtonDecor { Name = "PulpButtonDecor", _button = button, _compact = compact };
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
            foreach (Node child in _button.GetChildren()) if (child is FocusPose pose) existingPose = pose;
            if (existingPose == null) FocusPose.Attach(_button);

            _marker = new PulpIcon { Name = "Marker", IconName = "marker", Tint = UIPalette.Ink3 };
            _button.AddChild(_marker);
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
            if (_button != null && _lastDisabled != _button.Disabled) Refresh();
        }

        private bool _lastDisabled;

        private void OnStateChanged() => Refresh();
        private void OnDeviceAssigned(int slot, int device) => Refresh();

        private void OnToggled(bool pressed) {
            if (_button.ToggleMode) Selected = pressed;
            Refresh();
        }

        /// <summary>Recomputes the marker, the badge and the layout from the button's current state.</summary>
        public void Refresh() {
            if (_button == null || !GodotObject.IsInstanceValid(_button) || _marker == null) return;
            _lastDisabled = _button.Disabled;
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
            foreach (Node child in _badge.GetChildren()) {
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
                foreach (string slot in FontColorSlots) {
                    _savedColors[slot] = _button.HasThemeColorOverride(slot) ? _button.GetThemeColor(slot) : null;
                    _button.AddThemeColorOverride(slot, UIPalette.LabelDark);
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
            string stem = "btn_selected_h" + SelectedPlateHeight(_button.ThemeTypeVariation);
            if (!TryReadKitEntry(stem, out JsonElement entry)) return null;
            var plate = new StyleBoxTexture { Texture = GD.Load<Texture2D>(KitDirectory + stem + ".png") };
            int[] slice = ReadFour(entry, "nine_slice_margins_LTRB");
            plate.TextureMarginLeft = slice[0]; plate.TextureMarginTop = slice[1];
            plate.TextureMarginRight = slice[2]; plate.TextureMarginBottom = slice[3];
            if (entry.TryGetProperty("expand_margins_LTRB", out _)) {
                int[] expand = ReadFour(entry, "expand_margins_LTRB");
                plate.ExpandMarginLeft = expand[0]; plate.ExpandMarginTop = expand[1];
                plate.ExpandMarginRight = expand[2]; plate.ExpandMarginBottom = expand[3];
            }
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

        private static int[] ReadFour(JsonElement entry, string key) {
            var values = new int[4];
            int i = 0;
            foreach (JsonElement v in entry.GetProperty(key).EnumerateArray()) values[i++] = v.GetInt32();
            return values;
        }

        private static bool TryReadKitEntry(string stem, out JsonElement entry) {
            if (_kit == null) {
                _kit = new Dictionary<string, JsonElement>();
                using FileAccess file = FileAccess.Open(KitJsonPath, FileAccess.ModeFlags.Read);
                if (file != null) {
                    using JsonDocument doc = JsonDocument.Parse(file.GetAsText());
                    foreach (JsonProperty property in doc.RootElement.EnumerateObject()) {
                        _kit[property.Name] = property.Value.Clone();
                    }
                }
            }
            return _kit.TryGetValue(stem, out entry);
        }
    }
}
