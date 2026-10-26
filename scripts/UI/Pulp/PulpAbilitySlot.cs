using System;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>The state an ability slot is in (UI_SPEC 3.3.2).</summary>
    public enum PulpSlotState { Ready, Cooling, Charging, Dormant, Suppressed }

    /// <summary>How the Ultimate ring is drawn.</summary>
    public enum PulpRingMode { None, Meter, Ready, Cold }

    /// <summary>What a slot draws in a given state, derived without touching pixels.</summary>
    public readonly record struct PulpSlotLook(
        bool ArtVisible, float Saturation, float Brightness, float SweepElapsed, string CenterText,
        string GlyphName, PulpRingMode Ring, bool ReadyTag, bool KeyDim, bool ReadyFrame);

    /// <summary>
    /// UR-WIDGETS. The GAUGE-B ability slot (UI_SPEC 3.3.2): a square slot (74 Story, 54 Fighter, 50 select)
    /// or the circular Ultimate (92 px frame inside a 116 px ring). The owner writes <see cref="State"/>,
    /// <see cref="Progress"/> and <see cref="SecondsRemaining"/> (nothing here ticks, so a Time Freeze stops
    /// the sweep) and the art; <see cref="Derive"/> is the pure rule table. Children are laid out by hand,
    /// every control ignores the mouse, and the art uses a per-slot saturation shader.
    /// </summary>
    public partial class PulpAbilitySlot : Control {

        public const float RingOuterDiameter = 116f;
        public const float UltimateFrame = 92f;
        public const float RingWidth = 9f;
        private const float FrameBorder = 4f;
        private static readonly Color SweepShade = new(8f / 255f, 6f / 255f, 12f / 255f, 0.78f);

        private float _slotSize = 74f;
        private bool _ultimate;
        private PulpSlotState _state = PulpSlotState.Ready;
        private float _progress;
        private float _seconds;
        private string _keyText = "";
        private Texture2D _art;

        private readonly TextureRect _artRect;
        private readonly SweepLayer _sweep;
        private readonly PulpIcon _glyph;
        private readonly PulpIcon _glyphGlow;
        private readonly Label _center;
        private readonly PanelContainer _keyTag;
        private readonly Label _keyLabel;
        private readonly PanelContainer _readyTag;
        private readonly ShaderMaterial _artMaterial;

        public PulpAbilitySlot() {
            Name = "PulpAbilitySlot";
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;

            _artMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ui_desaturate.gdshader") };
            _artRect = new TextureRect {
                Name = "Art", MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale, Material = _artMaterial
            };
            AddChild(_artRect);
            _sweep = new SweepLayer(this) { Name = "Sweep", MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_sweep);
            _glyphGlow = new PulpIcon { Name = "GlyphGlow", Flat = true, Visible = false };
            AddChild(_glyphGlow);
            _glyph = new PulpIcon { Name = "Glyph", Flat = true, Visible = false };
            AddChild(_glyph);
            _center = new Label {
                Name = "Center", ThemeTypeVariation = UIPalette.LabelRoles.SlotNumber, MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visible = false
            };
            AddChild(_center);

            _keyLabel = new Label {
                Name = "KeyLabel", ThemeTypeVariation = UIPalette.LabelRoles.SlotKeyTag, MouseFilter = MouseFilterEnum.Ignore
            };
            _keyTag = new PanelContainer {
                Name = "KeyTag", ThemeTypeVariation = UIPalette.Variations.KeyTagPlate, MouseFilter = MouseFilterEnum.Ignore, Visible = false
            };
            _keyTag.AddChild(_keyLabel);
            AddChild(_keyTag);

            var readyLabel = new Label {
                Name = "ReadyLabel", Text = "common_ready", Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.SlotKeyTag, MouseFilter = MouseFilterEnum.Ignore
            };
            _readyTag = new PanelContainer {
                Name = "ReadyTag", ThemeTypeVariation = UIPalette.Variations.KeyTagPlate, MouseFilter = MouseFilterEnum.Ignore, Visible = false
            };
            _readyTag.AddChild(readyLabel);
            AddChild(_readyTag);
            Relayout();
        }

        // ---- the pure rule table ----

        /// <summary>Formats a remaining time for the centre number: whole seconds from 10 up, one decimal below, blank at 0.</summary>
        public static string FormatSeconds(float seconds) {
            if (seconds <= 0f) return "";
            // Quantize once, then derive both the cache key and the text from that one value, so the result never
            // depends on which call filled the cache. A cooldown redraws every frame and must not allocate each time.
            int tenths = seconds >= 10f ? 0 : Mathf.CeilToInt(seconds * 10f - 0.0001f);
            int whole = seconds >= 10f ? Mathf.CeilToInt(seconds - 0.0001f) : 0;
            int key = seconds >= 10f ? 100000 + whole : tenths;
            if (!SecondsText.TryGetValue(key, out string text)) {
                text = seconds >= 10f ? whole.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : (tenths / 10) + "." + (tenths % 10);
                SecondsText[key] = text;
            }
            return text;
        }

        private static readonly System.Collections.Generic.Dictionary<int, string> SecondsText = new();
        private static readonly string[] PercentText = BuildPercentText();
        private static string[] BuildPercentText() {
            var all = new string[101];
            for (int i = 0; i <= 100; i++) all[i] = i + "%";
            return all;
        }

        /// <summary>
        /// What a slot draws. <paramref name="p"/> is the elapsed cooldown share (cooling) or the meter share (the
        /// Ultimate); a non-Ultimate "Charging" reads as cooling and an Ultimate "Cooling" as charging.
        /// </summary>
        public static PulpSlotLook Derive(PulpSlotState state, bool ultimate, float p, float secondsRemaining) {
            p = Mathf.Clamp(p, 0f, 1f);
            if (state == PulpSlotState.Charging && !ultimate) state = PulpSlotState.Cooling;
            if (state == PulpSlotState.Cooling && ultimate) state = PulpSlotState.Charging;
            switch (state) {
                case PulpSlotState.Cooling:
                    return new PulpSlotLook(true, 0.5f, 0.72f, p, FormatSeconds(secondsRemaining), null, PulpRingMode.None, false, false, false);
                case PulpSlotState.Charging:
                    return new PulpSlotLook(true, 0.55f, 0.62f, -1f, PercentText[Mathf.RoundToInt(p * 100f)], null, PulpRingMode.Meter, false, false, false);
                case PulpSlotState.Dormant:
                    return new PulpSlotLook(false, 1f, 1f, -1f, "", "dormant", PulpRingMode.None, false, true, false);
                case PulpSlotState.Suppressed:
                    return new PulpSlotLook(true, 0f, 0.5f, -1f, "", "cross", ultimate ? PulpRingMode.Cold : PulpRingMode.None, false, false, false);
                default:
                    return new PulpSlotLook(true, 1f, 1f, -1f, "", null, ultimate ? PulpRingMode.Ready : PulpRingMode.None, ultimate, false, true);
            }
        }

        /// <summary>The art rectangle inside a frame of side <paramref name="frame"/>: 9 % of the padding box in from the border.</summary>
        public static Rect2 ArtRect(float frame) {
            float pad = frame - 2f * FrameBorder;
            float side = pad * 0.82f;
            float offset = FrameBorder + pad * 0.09f;
            return new Rect2(offset, offset, side, side);
        }

        // ---- the owner's properties ----

        /// <summary>74 (Story), 54 (Fighter) or 50 (select card). Ignored for the Ultimate (92 in a 116 ring).</summary>
        public float SlotSize { get => _slotSize; set { if (_slotSize == value) return; _slotSize = value; _lookDirty = true; Relayout(); } }
        public bool Ultimate { get => _ultimate; set { if (_ultimate == value) return; _ultimate = value; _lookDirty = true; Relayout(); } }
        public PulpSlotState State { get => _state; set { if (_state == value) return; _state = value; _lookDirty = true; Refresh(); } }
        /// <summary>The elapsed cooldown share, or the Ultimate's meter share, 0 to 1.</summary>
        public float Progress { get => _progress; set { if (_progress == value) return; _progress = value; _lookDirty = true; Refresh(); } }
        public float SecondsRemaining { get => _seconds; set { if (_seconds == value) return; _seconds = value; _lookDirty = true; Refresh(); } }
        public Texture2D Art { get => _art; set { _art = value; _artRect.Texture = value; } }
        /// <summary>The raw translation key shown on the key tag (blank hides the tag).</summary>
        public string KeyText { get => _keyText; set { string next = value ?? ""; if (_keyText == next) return; _keyText = next; _lookDirty = true; Refresh(); } }

        public PulpSlotLook Look {
            get {
                if (_lookDirty) { _look = Derive(_state, _ultimate, _progress, _seconds); _lookDirty = false; }
                return _look;
            }
        }
        private PulpSlotLook _look;
        private bool _lookDirty = true;
        private float _appliedSaturation = -1f, _appliedBrightness = -1f;
        private string _laidKey = "";
        private bool _laidReady;
        private string _appliedCenter = "";
        private string _appliedGlyph;
        private bool _appliedKeyDim;
        private static readonly StringName SaturationParam = "saturation", BrightnessParam = "brightness";
        public Label CenterLabel => _center;
        public PulpIcon GlyphIcon => _glyph;
        public Control ReadyTag => _readyTag;
        public Control KeyTag => _keyTag;
        public TextureRect ArtNode => _artRect;

        private float FrameSide => _ultimate ? UltimateFrame : _slotSize;
        private Vector2 FrameOrigin => _ultimate ? Vector2.One * ((RingOuterDiameter - UltimateFrame) / 2f) : Vector2.Zero;

        private void Relayout() {
            float side = _ultimate ? RingOuterDiameter : _slotSize;
            CustomMinimumSize = new Vector2(side, side);
            Size = new Vector2(side, side);
            Vector2 origin = FrameOrigin;
            float frame = FrameSide;
            Rect2 art = ArtRect(frame);
            _artRect.Position = origin + art.Position;
            _artRect.Size = art.Size;
            _sweep.Position = origin;
            _sweep.Size = new Vector2(frame, frame);
            float glyphSize = frame * 0.68f;
            _glyph.Size = new Vector2(glyphSize, glyphSize);
            _glyph.Position = origin + Vector2.One * (frame * 0.16f);
            _glyphGlow.Size = _glyph.Size * 1.35f;
            _glyphGlow.Position = _glyph.Position - (_glyphGlow.Size - _glyph.Size) / 2f;
            _center.Position = origin;
            _center.Size = new Vector2(frame, frame);
            _keyTag.ResetSize();
            _keyTag.Position = origin + new Vector2(-10f, -12f);
            _keyTag.PivotOffset = _keyTag.Size / 2f;
            _keyTag.Rotation = Mathf.DegToRad(-4f);
            _readyTag.ResetSize();
            _readyTag.Position = new Vector2((RingOuterDiameter - _readyTag.Size.X) / 2f, RingOuterDiameter - _readyTag.Size.Y + 6f);
            QueueRedraw();
            _sweep.QueueRedraw();
        }

        public override void _Notification(int what) {
            if (what == NotificationThemeChanged) CallDeferred(nameof(Relayout));
        }

        public override void _Ready() => CallDeferred(nameof(Relayout));

        private void Refresh() {
            PulpSlotLook look = Look;
            _artRect.Visible = look.ArtVisible;
            if (look.Saturation != _appliedSaturation || look.Brightness != _appliedBrightness) {
                _appliedSaturation = look.Saturation; _appliedBrightness = look.Brightness;
                _artMaterial.SetShaderParameter(SaturationParam, look.Saturation);
                _artMaterial.SetShaderParameter(BrightnessParam, look.Brightness);
            }
            if (look.CenterText != _appliedCenter) {
                _appliedCenter = look.CenterText;
                _center.Text = look.CenterText;
            }
            _center.Visible = look.CenterText.Length > 0;
            bool glyph = look.GlyphName != null;
            _glyph.Visible = glyph;
            _glyphGlow.Visible = glyph && look.GlyphName == "cross";
            if (glyph && look.GlyphName != _appliedGlyph) {
                _appliedGlyph = look.GlyphName;
                _glyph.IconName = look.GlyphName;
                Color tint = look.GlyphName == "cross" ? UIPalette.UnboundCold : UIPalette.Ink3;
                _glyph.Tint = tint;
                _glyph.Modulate = look.GlyphName == "dormant" ? new Color(1f, 1f, 1f, 0.8f) : Colors.White;
                _glyphGlow.IconName = "cross";
                _glyphGlow.Tint = UIPalette.UnboundCold;
                _glyphGlow.Modulate = new Color(1f, 1f, 1f, 0.32f);
            } else if (!glyph) {
                _appliedGlyph = null;
            }
            if (_keyLabel.Text != _keyText) _keyLabel.Text = _keyText;
            _keyTag.Visible = _keyText.Length > 0;
            if (look.KeyDim != _appliedKeyDim) {
                _appliedKeyDim = look.KeyDim;
                if (look.KeyDim) _keyLabel.AddThemeColorOverride("font_color", UIPalette.Ink3);
                else _keyLabel.RemoveThemeColorOverride("font_color");
            }
            _readyTag.Visible = look.ReadyTag;
            _sweep.QueueRedraw();
            QueueRedraw();
            // The tags change size only with their text or visibility; a cooldown that just ticks relays out nothing.
            if (_laidKey != _keyText || _laidReady != look.ReadyTag) {
                _laidKey = _keyText; _laidReady = look.ReadyTag;
                CallDeferred(nameof(Relayout));
            }
        }

        // ---- drawing: the ring and the frame ----

        public override void _Draw() {
            PulpSlotLook look = Look;
            Vector2 origin = FrameOrigin;
            if (_ultimate) {
                DrawUltimateRing(look);
                string stem = look.ReadyFrame ? "slot_ult_ready" : "slot_ult";
                int[] expand = PulpKit.Expand(stem);
                Texture2D tex = PulpKit.Texture(stem);
                if (tex != null) DrawTexture(tex, origin - new Vector2(expand[0], expand[1]));
            } else {
                StyleBoxTexture plate = PulpKit.SharedPlate(look.ReadyFrame ? "slot_frame_ready" : "slot_frame");
                if (plate != null) DrawStyleBox(plate, new Rect2(origin, new Vector2(_slotSize, _slotSize)));
            }
        }

        private void DrawUltimateRing(PulpSlotLook look) {
            if (look.Ring == PulpRingMode.None) return;
            var center = Vector2.One * (RingOuterDiameter / 2f);
            float radius = RingOuterDiameter / 2f - RingWidth / 2f;
            // Shadow (3, 4), then the empty track, then the arc.
            DrawArc(center + new Vector2(3f, 4f), radius, 0f, Mathf.Tau, 96, Colors.Black, RingWidth, true);
            DrawArc(center, radius, 0f, Mathf.Tau, 96, UIPalette.Track, RingWidth, true);
            switch (look.Ring) {
                case PulpRingMode.Cold:
                    DrawArc(center, radius, 0f, Mathf.Tau, 96, UIPalette.UnboundCold, RingWidth, true);
                    break;
                case PulpRingMode.Ready:
                    DrawGradientArc(center, radius, 1f, t => t < 0.5f
                        ? UIPalette.Res2.Lerp(UIPalette.Res, t * 2f) : UIPalette.Res.Lerp(UIPalette.Res2, (t - 0.5f) * 2f));
                    break;
                case PulpRingMode.Meter:
                    if (_progress > 0f) DrawGradientArc(center, radius, Mathf.Clamp(_progress, 0f, 1f), t => UIPalette.Meter.Lerp(UIPalette.Meter2, t));
                    break;
            }
        }

        private static readonly System.Collections.Generic.Dictionary<int, (Vector2[] Points, Color[] Colors)> ArcBuffers = new();

        private void DrawGradientArc(Vector2 center, float radius, float fraction, Func<float, Color> colorAt) {
            int count = Mathf.Max(2, Mathf.CeilToInt(fraction * 120f) + 1);
            // Polyline wants exact-length arrays; the ring has at most 121 points, so one buffer per length is kept.
            if (!ArcBuffers.TryGetValue(count, out (Vector2[] Points, Color[] Colors) buffer)) {
                buffer = (new Vector2[count], new Color[count]);
                ArcBuffers[count] = buffer;
            }
            Vector2[] points = buffer.Points;
            Color[] colors = buffer.Colors;
            for (int i = 0; i < count; i++) {
                float t = i / (float)(count - 1);
                float angle = -Mathf.Pi / 2f + t * fraction * Mathf.Tau;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                colors[i] = colorAt(t);
            }
            DrawPolylineColors(points, colors, RingWidth, true);
        }

        /// <summary>The cooldown sweep: clear for the elapsed share, a dark shade for the rest, clockwise from 12 o'clock.</summary>
        private partial class SweepLayer : Control {
            private readonly PulpAbilitySlot _owner;
            private const int Steps = 72;
            private readonly Vector2[] _points = new Vector2[Steps + 2];
            public SweepLayer(PulpAbilitySlot owner) { _owner = owner; }

            public override void _Draw() {
                float elapsed = _owner.Look.SweepElapsed;
                if (elapsed < 0f || elapsed >= 1f) return;
                bool circle = _owner._ultimate;
                var center = Size / 2f;
                float half = Size.X / 2f - FrameBorder;
                Vector2[] points = _points;
                points[0] = center;
                for (int i = 0; i <= Steps; i++) {
                    float t = elapsed + (1f - elapsed) * i / Steps;
                    float angle = -Mathf.Pi / 2f + t * Mathf.Tau;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float reach = circle ? half : half / Mathf.Max(Mathf.Abs(dir.X), Mathf.Abs(dir.Y));
                    points[i + 1] = center + dir * reach;
                }
                DrawColoredPolygon(points, SweepShade);
            }
        }
    }
}
