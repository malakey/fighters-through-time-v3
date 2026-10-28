using System;
using FTT.UI.Motion;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>The state an ability slot is in (UI_SPEC_GLASS 3.4.2).</summary>
    public enum PulpSlotState { Ready, Cooling, Charging, Dormant, Suppressed }

    /// <summary>How a slot's readiness ring is drawn. Append only.</summary>
    public enum PulpRingMode { None, Meter, Ready, Cold, Accent }

    /// <summary>
    /// What a slot draws in a given state, derived without touching pixels. <c>GlyphName</c> is the logical lock
    /// ("dormant", "cross"); the lens draws <c>lock_dormant</c> / <c>lock_suppressed</c> for it.
    /// </summary>
    public readonly record struct PulpSlotLook(
        bool ArtVisible, float Saturation, float Brightness, float SweepElapsed, string CenterText,
        string GlyphName, PulpRingMode Ring, bool ReadyTag, bool KeyDim, bool ReadyFrame);

    /// <summary>
    /// TG-WIDGETS (GAUGE-A lens, UI_SPEC_GLASS 3.4.2; first written by UR-WIDGETS as the GAUGE-B slot). A round glass
    /// lens of <see cref="SlotSize"/> (74 Story, 54 Fighter, 56 select) or the Ultimate's 92 lens inside its 110 ring box:
    /// the radial lens fill with its inner glow and a 1.5 px edge ring outside it; the ability art inset 9 % and clipped to
    /// the lens (a child with the saturation shader); the cooldown sweep; the readiness ring outside the lens
    /// (<see cref="Ring"/>, a <see cref="RadialProgress"/>: 3 px with its outer edge 6 px out, the Ultimate's 5 px, 9 px
    /// out, a gradient arc while charging and a res2 / res / res2 ring when ready); the lock overlays inset 16 %; the
    /// centre text (seconds, the percent, READY); the key label under the lens. Entering ready plays one 600 ms bloom
    /// (MOT-A row 10; the steady look at once under Reduced Temporal Effects).
    ///
    /// <para>The owner writes <see cref="State"/>, <see cref="Progress"/> and <see cref="SecondsRemaining"/> (nothing
    /// here ticks, so a Time Freeze stops the sweep) and the art; <see cref="Derive"/> is the pure rule table. Geometry is
    /// built once per size (<see cref="GeometryBuilds"/>); a redraw allocates nothing. The not-yet flash's circle goes
    /// round <see cref="Ring"/> (<c>NotYetFlash.Play(slot.Ring, slot, NotYetShape.Circle)</c>).</para>
    /// </summary>
    public partial class PulpAbilitySlot : Control {

        /// <summary>The Ultimate's box: its ring's outer diameter (92 + 2 x 9).</summary>
        public const float RingOuterDiameter = 110f;
        /// <summary>The Ultimate lens.</summary>
        public const float UltimateFrame = 92f;
        /// <summary>The Ultimate ring's width (the small slots' is <see cref="SmallRingWidth"/>).</summary>
        public const float RingWidth = 5f;
        public const float SmallRingWidth = 3f, SmallRingOutset = 6f, UltimateRingOutset = 9f;
        public const float ArtInset = 0.09f, LockInset = 0.16f, CenterShare = 0.36f;
        public const float EdgeWidth = GlassGeometry.EdgeWidth;
        public const float KeyGap = 9f;
        /// <summary>The <c>SlotNumber</c> role's size at 100 % (T19 for the 54 lens), the base of the 0.36 x lens rule.</summary>
        public const int SlotNumberBaseSize = 19;
        /// <summary>The compact Story HUD's READY pill: 8 px of padding a side, 22 px tall, its top 19 px above the lens's bottom.</summary>
        public const float ReadyPillPadX = 8f, ReadyPillHeight = 22f, ReadyPillRise = 19f, ReadyPillSlant = 6f;
        private const int CircleSegments = 48;

        private static readonly Color RingTrack = new(UIPalette.Accent, 0.16f);
        private static readonly Color ResGlowColor = PulpBarDecor.ResGlow;
        // Suppressed is the Severed's cold (UI_SPEC_GLASS 3.4.2), never a chrome cyan.
        private static readonly Color ColdGlowColor = UIPalette.UnboundCold;

        private float _slotSize = 74f;
        private bool _ultimate;
        private PulpSlotState _state = PulpSlotState.Ready;
        private float _progress;
        private float _seconds;
        private string _keyText = "";
        private Texture2D _art;
        private bool _keyOverScene;
        private float _ultimateLens = UltimateFrame;
        private float _ringWidthOverride;
        private float _ringOutsetOverride;
        private float _centerTextSize;
        private float _effectScale = 1f;
        private bool _readyPill;

        private readonly LensArt _artRect;
        private readonly SweepLayer _sweep;
        private readonly RadialProgress _ring;
        private readonly PulpIcon _glyph;
        private readonly Label _center;
        private readonly Label _keyLabel;
        private readonly Label _readyLabel;
        private readonly ReadyPillLayer _readyPillLayer;
        private readonly ShaderMaterial _artMaterial;
        private readonly StyleBoxFlat _glowBox = new() { DrawCenter = false, CornerDetail = 16 };
        private float _bloomGlow = 1f;
        private Tween _bloom;

        public PulpAbilitySlot() {
            Name = "PulpAbilitySlot";
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;

            _artMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ui_desaturate.gdshader") };
            _artRect = new LensArt(this) { Name = "Art", MouseFilter = MouseFilterEnum.Ignore, Material = _artMaterial };
            AddChild(_artRect);
            _sweep = new SweepLayer(this) { Name = "Sweep", MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_sweep);
            _ring = new RadialProgress { Name = "Ring", MouseFilter = MouseFilterEnum.Ignore, Segments = 64, TrackColor = RingTrack };
            AddChild(_ring);
            _glyph = new PulpIcon { Name = "Glyph", Visible = false };
            AddChild(_glyph);
            _center = new Label {
                Name = "Center", ThemeTypeVariation = UIPalette.LabelRoles.SlotNumber, MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visible = false
            };
            AddChild(_center);
            _readyPillLayer = new ReadyPillLayer { Name = "ReadyPill", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            AddChild(_readyPillLayer);
            _readyLabel = new Label {
                Name = "ReadyLabel", Text = "common_ready", Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.UltimateReadyLabel,
                MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, Visible = false
            };
            AddChild(_readyLabel);
            _keyLabel = new Label {
                Name = "KeyLabel", Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.SlotKeyTag,
                MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center, Visible = false
            };
            AddChild(_keyLabel);
            Relayout();
            Refresh();
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
        /// What a slot draws (3.4.2's state table). <paramref name="p"/> is the elapsed cooldown share (cooling) or the
        /// meter share (the Ultimate); a non-Ultimate "Charging" reads as cooling and an Ultimate "Cooling" as charging.
        /// </summary>
        public static PulpSlotLook Derive(PulpSlotState state, bool ultimate, float p, float secondsRemaining) {
            p = Mathf.Clamp(p, 0f, 1f);
            if (state == PulpSlotState.Charging && !ultimate) state = PulpSlotState.Cooling;
            if (state == PulpSlotState.Cooling && ultimate) state = PulpSlotState.Charging;
            switch (state) {
                case PulpSlotState.Cooling:
                    return new PulpSlotLook(true, 0.5f, 0.72f, p, FormatSeconds(secondsRemaining), null, PulpRingMode.Accent, false, false, false);
                case PulpSlotState.Charging:
                    return new PulpSlotLook(true, 0.55f, 0.62f, -1f, PercentText[Mathf.RoundToInt(p * 100f)], null, PulpRingMode.Meter, false, false, false);
                case PulpSlotState.Dormant:
                    return new PulpSlotLook(false, 1f, 1f, -1f, "", "dormant", PulpRingMode.None, false, false, false);
                case PulpSlotState.Suppressed:
                    return new PulpSlotLook(true, 0f, 0.5f, -1f, "", "cross", PulpRingMode.Cold, false, false, false);
                default:
                    return new PulpSlotLook(true, 1f, 1f, -1f, "", null, ultimate ? PulpRingMode.Ready : PulpRingMode.Accent, ultimate, false, true);
            }
        }

        /// <summary>The lock overlay a logical glyph draws as (3.4.2).</summary>
        public static string LockIconFor(string glyphName) => glyphName switch {
            "dormant" => "lock_dormant",
            "cross" => "lock_suppressed",
            _ => glyphName
        };

        /// <summary>The art square inside a lens of <paramref name="lens"/>: inset 9 % (74 -> 60.7, 92 -> 75.4, 54 -> 44.3, 56 -> 45.9).</summary>
        public static Rect2 ArtRect(float lens) {
            float offset = lens * ArtInset;
            float side = lens - 2f * offset;
            return new Rect2(offset, offset, side, side);
        }

        /// <summary>
        /// The centre number's size for a lens: 0.36 x the lens at 100 % (19.44, 26.64, 33.12), times the scale of the theme the
        /// lens resolves <c>SlotNumber</c> in (<see cref="SlotNumberSizeInContext"/>), so the UI scale reaches it exactly once.
        /// </summary>
        public static int CenterFontSize(float lens, int themeSlotNumberSize) =>
            Mathf.RoundToInt(CenterShare * lens * themeSlotNumberSize / SlotNumberBaseSize);

        /// <summary>The readiness ring's box relative to the lens box: its outer edge <c>outset</c> outside the lens.</summary>
        public static Rect2 RingRectFor(float lens, bool ultimate) {
            float outset = ultimate ? UltimateRingOutset : SmallRingOutset;
            return new Rect2(-outset, -outset, lens + 2f * outset, lens + 2f * outset);
        }

        // ---- the owner's properties ----

        /// <summary>
        /// The Ultimate lens's diameter: 92 by default (its ring box then 110); the compact Story HUD (2026-10-27) uses 64.
        /// The ring box is the lens plus twice <see cref="RingOutset"/>.
        /// </summary>
        public float UltimateLensSize {
            get => _ultimateLens;
            set { if (_ultimateLens == value) return; _ultimateLens = value; _lookDirty = true; Relayout(); Refresh(); }
        }

        /// <summary>The readiness ring's width in pixels; 0 (default) keeps 3 (small) or 5 (the Ultimate).</summary>
        public float RingWidthOverride { get => _ringWidthOverride; set { if (_ringWidthOverride == value) return; _ringWidthOverride = value; Relayout(); } }

        /// <summary>How far the ring's outer edge sits outside the lens; 0 (default) keeps 6 (small) or 9 (the Ultimate).</summary>
        public float RingOutsetOverride { get => _ringOutsetOverride; set { if (_ringOutsetOverride == value) return; _ringOutsetOverride = value; Relayout(); } }

        /// <summary>
        /// The centre number's size at 100 % in pixels; 0 (default) keeps the 0.36 x lens rule (<see cref="CenterFontSize"/>).
        /// The compact Story HUD sets 18 for its 48 px lenses (the rule would give 17, under the HUD's 18 px floor) and 21 for
        /// its 64 px Ultimate.
        /// </summary>
        public float CenterTextSize { get => _centerTextSize; set { if (_centerTextSize == value) return; _centerTextSize = value; Relayout(); } }

        /// <summary>Scales the lens's inner and outer glow widths (1 by default); the compact Story HUD scales them with its lenses.</summary>
        public float EffectScale { get => _effectScale; set { if (_effectScale == value) return; _effectScale = value; _builtGlowWidth = -1f; QueueRedraw(); } }

        /// <summary>
        /// The ready Ultimate's READY label as a pill laid over the bottom of the lens (the compact Story HUD) instead of
        /// centred in the lens (default false).
        /// </summary>
        public bool ReadyPill { get => _readyPill; set { if (_readyPill == value) return; _readyPill = value; Relayout(); Refresh(); } }

        /// <summary>The ring's width in use (the override, else 3 or 5).</summary>
        public float RingThickness => _ringWidthOverride > 0f ? _ringWidthOverride : _ultimate ? RingWidth : SmallRingWidth;

        /// <summary>The ring's outer edge outside the lens in use (the override, else 6 or 9).</summary>
        public float RingOutset => _ringOutsetOverride > 0f ? _ringOutsetOverride : _ultimate ? UltimateRingOutset : SmallRingOutset;

        /// <summary>The Ultimate's box side: the lens plus twice the ring outset (110 by default).</summary>
        public float UltimateBoxSize => _ultimateLens + 2f * RingOutset;

        /// <summary>74 (Story), 54 (Fighter) or 56 (select). Ignored for the Ultimate (a 92 lens in a 110 box).</summary>
        public float SlotSize { get => _slotSize; set { if (_slotSize == value) return; _slotSize = value; _lookDirty = true; Relayout(); Refresh(); } }
        public bool Ultimate { get => _ultimate; set { if (_ultimate == value) return; _ultimate = value; _lookDirty = true; Relayout(); Refresh(); } }
        public PulpSlotState State {
            get => _state;
            set {
                if (_state == value) return;
                PulpSlotState previous = _state;
                _state = value;
                _lookDirty = true;
                Refresh();
                if (value == PulpSlotState.Ready && previous != PulpSlotState.Ready) StartBloom();
            }
        }
        /// <summary>The elapsed cooldown share, or the Ultimate's meter share, 0 to 1.</summary>
        public float Progress { get => _progress; set { if (_progress == value) return; _progress = value; _lookDirty = true; Refresh(); } }
        public float SecondsRemaining { get => _seconds; set { if (_seconds == value) return; _seconds = value; _lookDirty = true; Refresh(); } }
        public Texture2D Art { get => _art; set { _art = value; _artRect.QueueRedraw(); } }
        /// <summary>The raw translation key shown under the lens (blank hides it).</summary>
        public string KeyText { get => _keyText; set { string next = value ?? ""; if (_keyText == next) return; _keyText = next; _lookDirty = true; Refresh(); } }
        /// <summary>True for a key label drawn over the scene (Fighter: T18 with the halo S) rather than on a plate.</summary>
        public bool KeyOverScene {
            get => _keyOverScene;
            set {
                _keyOverScene = value;
                _keyLabel.ThemeTypeVariation = value ? UIPalette.LabelRoles.SlotKeyTagScene : UIPalette.LabelRoles.SlotKeyTag;
            }
        }

        public PulpSlotLook Look {
            get {
                if (_lookDirty) { _look = Derive(_state, _ultimate, _progress, _seconds); _lookDirty = false; }
                return _look;
            }
        }
        private PulpSlotLook _look;
        private bool _lookDirty = true;
        private float _appliedSaturation = -1f, _appliedBrightness = -1f;
        private string _appliedCenter = "";
        private string _appliedGlyph;
        private static readonly StringName SaturationParam = "saturation", BrightnessParam = "brightness";
        private static readonly StringName FontSizeName = "font_size";
        private static readonly StringName SlotNumberType = UIPalette.LabelRoles.SlotNumber;

        /// <summary>
        /// The <c>SlotNumber</c> size as a label under this lens resolves it: through the nearest ancestor's theme (the HUDs'
        /// unscaled copy, whose whole layout <see cref="UiScaleBinder"/> scales; a screen's scale-mutated shared theme), else
        /// the authored <see cref="SlotNumberBaseSize"/>. Never the shared theme directly, which a HUD would scale twice.
        /// </summary>
        public int SlotNumberSizeInContext =>
            HasThemeFontSize(FontSizeName, SlotNumberType) ? GetThemeFontSize(FontSizeName, SlotNumberType) : SlotNumberBaseSize;

        public Label CenterLabel => _center;
        public PulpIcon GlyphIcon => _glyph;
        /// <summary>The READY label inside the ready Ultimate lens (T60).</summary>
        public Control ReadyTag => _readyLabel;
        /// <summary>The key label under the lens (T18, plain text).</summary>
        public Control KeyTag => _keyLabel;
        /// <summary>The art layer (a <see cref="TextureRect"/> with the saturation shader that draws the art clipped to the lens).</summary>
        public TextureRect ArtNode => _artRect;
        /// <summary>The readiness ring (the not-yet circle's host).</summary>
        public RadialProgress Ring => _ring;
        /// <summary>The lens diameter: <see cref="SlotSize"/>, or <see cref="UltimateLensSize"/> (92) for the Ultimate.</summary>
        public float LensSize => _ultimate ? _ultimateLens : _slotSize;
        /// <summary>Where the lens sits inside this control: (0, 0), or (outset, outset) for the Ultimate's ring box (9 in 110).</summary>
        public Vector2 LensOrigin => _ultimate ? Vector2.One * RingOutset : Vector2.Zero;
        /// <summary>How many times the lens geometry was rebuilt (once per size).</summary>
        public int GeometryBuilds { get; private set; }
        /// <summary>How many ready blooms were played (none under Reduced Temporal Effects).</summary>
        public int Blooms { get; private set; }
        /// <summary>The ready glow's current strength as a multiple of its steady alpha (the bloom rises to 1.5, settles at 1).</summary>
        public float BloomGlow => _bloomGlow;

        private void Relayout() {
            float lens = LensSize;
            float box = _ultimate ? UltimateBoxSize : lens;
            CustomMinimumSize = new Vector2(box, box);
            Size = new Vector2(box, box);
            Vector2 origin = LensOrigin;
            _artRect.Position = origin;
            _artRect.Size = new Vector2(lens, lens);
            _sweep.Position = origin;
            _sweep.Size = new Vector2(lens, lens);
            float outset = RingOutset;
            var ring = new Rect2(-outset, -outset, lens + 2f * outset, lens + 2f * outset);
            _ring.Position = origin + ring.Position;
            _ring.Size = ring.Size;
            _ring.Thickness = RingThickness;
            float lockSide = lens * (1f - 2f * LockInset);
            _glyph.Size = new Vector2(lockSide, lockSide);
            _glyph.Position = origin + Vector2.One * (lens * LockInset);
            _center.Position = origin;
            _center.Size = new Vector2(lens, lens);
            int centerSize = _centerTextSize > 0f
                ? Mathf.RoundToInt(_centerTextSize * SlotNumberSizeInContext / SlotNumberBaseSize)
                : CenterFontSize(lens, SlotNumberSizeInContext);
            _center.AddThemeFontSizeOverride(FontSizeName, centerSize);
            if (_readyPill) {
                // The compact Story HUD's pill: the label's text plus 8 px a side, 22 tall, its top 19 px above the
                // lens's bottom, centred (the mock's translate(-50%, -19px) from the lens's bottom edge).
                _readyLabel.ResetSize();
                Vector2 text = _readyLabel.GetCombinedMinimumSize();
                var pill = new Vector2(text.X + 2f * ReadyPillPadX, ReadyPillHeight);
                var pillAt = new Vector2(origin.X + (lens - pill.X) / 2f, origin.Y + lens - ReadyPillRise);
                _readyPillLayer.Position = pillAt;
                _readyPillLayer.Size = pill;
                _readyLabel.Position = pillAt;
                _readyLabel.Size = pill;
                _readyPillLayer.QueueRedraw();
            } else {
                _readyLabel.Position = origin;
                _readyLabel.Size = new Vector2(lens, lens);
            }
            _keyLabel.ResetSize();
            _keyLabel.Position = new Vector2(origin.X + (lens - _keyLabel.Size.X) / 2f, origin.Y + lens + KeyGap);
            QueueRedraw();
            _sweep.QueueRedraw();
            _artRect.QueueRedraw();
        }

        public override void _Notification(int what) {
            if (what == NotificationThemeChanged) CallDeferred(nameof(Relayout));
        }

        public override void _Ready() => CallDeferred(nameof(Relayout));

        public override void _ExitTree() => _bloom?.Kill();

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
            _readyLabel.Visible = look.ReadyTag;
            _readyPillLayer.Visible = look.ReadyTag && _readyPill;
            bool glyph = look.GlyphName != null;
            _glyph.Visible = glyph;
            if (glyph && look.GlyphName != _appliedGlyph) {
                _appliedGlyph = look.GlyphName;
                _glyph.IconName = LockIconFor(look.GlyphName);
                bool suppressed = look.GlyphName == "cross";
                _glyph.Tint = suppressed ? UIPalette.UnboundCold : UIPalette.Ink3;
                _glyph.Modulate = suppressed ? Colors.White : new Color(1f, 1f, 1f, 0.8f);
            } else if (!glyph) {
                _appliedGlyph = null;
            }
            if (_keyLabel.Text != _keyText) {
                _keyLabel.Text = _keyText;
                CallDeferred(nameof(Relayout));
            }
            _keyLabel.Visible = _keyText.Length > 0;
            ApplyRing(look);
            _sweep.QueueRedraw();
            QueueRedraw();
        }

        private void ApplyRing(PulpSlotLook look) {
            _ring.Visible = look.Ring != PulpRingMode.None;
            switch (look.Ring) {
                case PulpRingMode.Meter:
                    _ring.GradientMode = RadialGradientMode.Arc;
                    _ring.FillColor = UIPalette.Meter;
                    _ring.FillColorEnd = UIPalette.Meter2;
                    _ring.Fraction = _progress;
                    break;
                case PulpRingMode.Ready:
                    _ring.GradientMode = RadialGradientMode.Loop;
                    _ring.FillColor = UIPalette.Res2;
                    _ring.FillColorEnd = UIPalette.Res;
                    _ring.Fraction = 1f;
                    break;
                case PulpRingMode.Cold:
                    _ring.GradientMode = RadialGradientMode.None;
                    _ring.FillColor = UIPalette.UnboundCold;
                    _ring.Fraction = _ultimate ? 1f : _progress;   // the small slot's ring keeps ticking in cold
                    break;
                case PulpRingMode.Accent:
                    _ring.GradientMode = RadialGradientMode.None;
                    _ring.FillColor = UIPalette.Accent;
                    _ring.Fraction = look.ReadyFrame ? 1f : _progress;
                    break;
            }
        }

        // MOT-A row 10: one 600 ms bloom on entering ready (the steady look at once under Reduced Temporal Effects).
        private void StartBloom() {
            if (!IsInsideTree()) return;
            _bloom?.Kill();
            _bloom = PulpMotion.Bloom(this, glow => { _bloomGlow = glow; QueueRedraw(); });
            if (_bloom != null) Blooms++;
        }

        // ---- drawing: the lens ----

        private readonly Vector2[] _lens = new Vector2[CircleSegments];
        private readonly Vector2[] _lensUvs = new Vector2[CircleSegments];
        private readonly Color[] _lensColors = new Color[CircleSegments];
        private readonly Vector2[] _glowPoints = new Vector2[CircleSegments * 2];
        private readonly Color[] _glowColors = new Color[CircleSegments * 2];
        private int[] _glowIndices;
        private float _builtLens = -1f;
        private Vector2 _builtOrigin = new(-1f, -1f);
        private float _builtGlowWidth = -1f;
        private Color _builtGlowColor;

        private void EnsureLens(float glowWidth, Color glowColor) {
            float lens = LensSize;
            Vector2 origin = LensOrigin;
            if (lens != _builtLens || origin != _builtOrigin) {
                _builtLens = lens;
                _builtOrigin = origin;
                _builtGlowWidth = -1f;
                GeometryBuilds++;
                var box = new Rect2(origin, new Vector2(lens, lens));
                GlassGeometry.Circle(box.GetCenter(), lens / 2f, CircleSegments, box, _lens, _lensUvs);
                for (int i = 0; i < CircleSegments; i++) _lensColors[i] = Colors.White;
                if (_glowIndices == null) {
                    _glowIndices = new int[CircleSegments * 6];
                    for (int i = 0; i < CircleSegments; i++) {
                        int a = i, b = (i + 1) % CircleSegments, c = CircleSegments + i, d = CircleSegments + (i + 1) % CircleSegments;
                        _glowIndices[i * 6] = a; _glowIndices[i * 6 + 1] = b; _glowIndices[i * 6 + 2] = d;
                        _glowIndices[i * 6 + 3] = a; _glowIndices[i * 6 + 4] = d; _glowIndices[i * 6 + 5] = c;
                    }
                }
            }
            if (!Mathf.IsEqualApprox(glowWidth, _builtGlowWidth) || glowColor != _builtGlowColor) {
                _builtGlowWidth = glowWidth;
                _builtGlowColor = glowColor;
                Vector2 centre = origin + new Vector2(lens, lens) / 2f;
                float r = lens / 2f, inner = Mathf.Max(0f, r - glowWidth);
                for (int i = 0; i < CircleSegments; i++) {
                    float a = Mathf.Tau * i / CircleSegments;
                    var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                    _glowPoints[i] = centre + dir * r;
                    _glowPoints[CircleSegments + i] = centre + dir * inner;
                    _glowColors[i] = glowColor;
                    _glowColors[CircleSegments + i] = new Color(glowColor, 0f);
                }
            }
        }

        public override void _Draw() {
            PulpSlotLook look = Look;
            bool readyUltimate = _ultimate && look.Ring == PulpRingMode.Ready;
            bool suppressed = _state == PulpSlotState.Suppressed;
            bool ready = look.ReadyFrame;
            float lens = LensSize;
            Vector2 origin = LensOrigin;
            Vector2 centre = origin + new Vector2(lens, lens) / 2f;
            float r = lens / 2f;

            // The inner glow (CSS inset): rgba(glow, .22) over 14 px; ready .30; suppressed cold .25 over 16; Ultimate ready .35 over 18.
            (float width, Color color) inner = suppressed ? (16f, new Color(ColdGlowColor, 0.25f))
                : readyUltimate ? (18f, new Color(ResGlowColor, 0.35f))
                : ready ? (14f, new Color(UIPalette.Accent, 0.30f))
                : (14f, new Color(UIPalette.Accent, 0.22f));
            inner.width *= _effectScale;
            EnsureLens(inner.width, inner.color);

            // The outer glow (OuterGlowOf): ready, the ready Ultimate and the suppressed Ultimate (round the ring).
            if (readyUltimate || ready || (suppressed && _ultimate)) {
                float radius = suppressed ? r + RingOutset : r;
                (int size, Color glow) = OuterGlowOf(readyUltimate, suppressed, _bloomGlow);
                if (_effectScale != 1f) size = Mathf.Max(1, Mathf.RoundToInt(size * _effectScale));
                if (glow.A > 0f) {
                    int corner = Mathf.RoundToInt(radius);
                    _glowBox.CornerRadiusTopLeft = _glowBox.CornerRadiusTopRight = corner;
                    _glowBox.CornerRadiusBottomLeft = _glowBox.CornerRadiusBottomRight = corner;
                    _glowBox.ShadowSize = size;
                    _glowBox.ShadowColor = glow;
                    DrawStyleBox(_glowBox, new Rect2(centre - Vector2.One * radius, Vector2.One * radius * 2f));
                }
            }

            // The lens fill (the radial gradient through UVs), its inner glow and its 1.5 px edge just outside it.
            Texture2D fill = PulpKit.Texture("glass_lens_radial");
            if (fill != null) DrawPolygon(_lens, _lensColors, _lensUvs, fill);
            else DrawColoredPolygon(_lens, UIPalette.LensFill);
            RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), _glowIndices, _glowPoints, _glowColors);
            Color edge = suppressed ? UIPalette.UnboundCold : readyUltimate ? UIPalette.Res : ready ? UIPalette.EdgeHi : UIPalette.Edge;
            DrawArc(centre, r + EdgeWidth / 2f, 0f, Mathf.Tau, 64, edge, EdgeWidth, true);
        }

        /// <summary>
        /// The lens's outer glow (UI_SPEC_GLASS 3.4.2) as a <c>StyleBoxFlat</c> shadow: ready 22 at .35, the ready Ultimate 30
        /// at .45 (both times the bloom), the suppressed Ultimate 12 cold round its ring at .2: the spec's 0 0 12 rgba(cold, .4)
        /// is a CSS blur, which starts at half its colour at the edge, and a Godot shadow starts at its full colour, so the .4
        /// read about twice as heavy as rec_05 (TG-CLOSE, TG-HUDS kit gap 4).
        /// </summary>
        public static (int Size, Color Color) OuterGlowOf(bool readyUltimate, bool suppressed, float bloomGlow = 1f) {
            if (suppressed) return (12, new Color(ColdGlowColor, 0.2f));
            if (readyUltimate) return (30, new Color(ResGlowColor, 0.45f * bloomGlow));
            return (22, new Color(UIPalette.Accent, Mathf.Min(1f, 0.35f * bloomGlow)));
        }

        /// <summary>The art, inset 9 % and clipped to the lens circle (a polygon with UVs; the shader sets saturation and brightness).</summary>
        private sealed partial class LensArt : TextureRect {
            private readonly PulpAbilitySlot _owner;
            private Vector2[] _points = Array.Empty<Vector2>();
            private Vector2[] _uvs = Array.Empty<Vector2>();
            private Color[] _colors = Array.Empty<Color>();
            private readonly Vector2[] _circle = new Vector2[CircleSegments];
            private readonly Vector2[] _a = new Vector2[CircleSegments + 8], _b = new Vector2[CircleSegments + 8];
            private Vector2 _builtFor = new(-1f, -1f);

            public LensArt(PulpAbilitySlot owner) { _owner = owner; }

            public override void _Draw() {
                Texture2D art = _owner._art;
                if (art == null) return;
                if (Size != _builtFor) Build();
                if (_points.Length >= 3) DrawPolygon(_points, _colors, _uvs, art);
            }

            // The lens circle clipped to the art square, with UVs mapping the square onto the whole texture.
            private void Build() {
                _builtFor = Size;
                float lens = Mathf.Min(Size.X, Size.Y);
                Rect2 square = ArtRect(lens);
                GlassGeometry.Circle(new Vector2(lens, lens) / 2f, lens / 2f, CircleSegments, new Rect2(0f, 0f, 1f, 1f), _circle, null);
                int n = GlassGeometry.ClipHalfPlane(_circle, CircleSegments, new Vector2(-1f, 0f), -square.Position.X, _a);
                n = GlassGeometry.ClipHalfPlane(_a, n, new Vector2(1f, 0f), square.End.X, _b);
                n = GlassGeometry.ClipHalfPlane(_b, n, new Vector2(0f, -1f), -square.Position.Y, _a);
                n = GlassGeometry.ClipHalfPlane(_a, n, new Vector2(0f, 1f), square.End.Y, _b);
                _points = new Vector2[n];
                _uvs = new Vector2[n];
                _colors = new Color[n];
                Array.Copy(_b, _points, n);
                GlassGeometry.BoxUvs(_points, n, square, _uvs);
                for (int i = 0; i < n; i++) _colors[i] = Colors.White;
            }
        }

        /// <summary>
        /// The READY pill's plate (the compact Story HUD): a parallelogram slanted 6 px, filled rgba(6, 10, 26, .94), a 1.5 px
        /// <c>res</c> edge and a soft res glow. Its points are rebuilt only when its size changes; a draw allocates nothing.
        /// </summary>
        private sealed partial class ReadyPillLayer : Control {
            private readonly Vector2[] _quad = new Vector2[4];
            private readonly Vector2[] _edge = new Vector2[5];
            private readonly StyleBoxFlat _glow = new() { DrawCenter = false, ShadowSize = 8, ShadowColor = new Color(ResGlowColor, 0.45f) };
            private Vector2 _builtFor = new(-1f, -1f);
            private static readonly Color Fill = new(6f / 255f, 10f / 255f, 26f / 255f, 0.94f);

            public override void _Draw() {
                if (Size != _builtFor) {
                    _builtFor = Size;
                    float w = Size.X, h = Size.Y, s = ReadyPillSlant;
                    _quad[0] = new Vector2(s, 0f); _quad[1] = new Vector2(w, 0f); _quad[2] = new Vector2(w - s, h); _quad[3] = new Vector2(0f, h);
                    for (int i = 0; i < 4; i++) _edge[i] = _quad[i];
                    _edge[4] = _quad[0];
                    _glow.Skew = new Vector2(s / Mathf.Max(1f, h), 0f);
                }
                DrawStyleBox(_glow, new Rect2(new Vector2(ReadyPillSlant / 2f, 0f), new Vector2(Mathf.Max(1f, Size.X - ReadyPillSlant), Size.Y)));
                DrawColoredPolygon(_quad, Fill);
                DrawPolyline(_edge, UIPalette.Res, EdgeWidth, true);
            }
        }

        /// <summary>The cooldown sweep: clear for the elapsed share, <c>SweepShade</c> for the rest, clockwise from 12 o'clock, clipped to the lens.</summary>
        private sealed partial class SweepLayer : Control {
            private readonly PulpAbilitySlot _owner;
            private const int Steps = 72;
            private readonly Vector2[] _points = new Vector2[Steps + 2];
            public SweepLayer(PulpAbilitySlot owner) { _owner = owner; }

            public override void _Draw() {
                float elapsed = _owner.Look.SweepElapsed;
                if (elapsed < 0f || elapsed >= 1f) return;
                var center = Size / 2f;
                float radius = Mathf.Min(Size.X, Size.Y) / 2f;
                Vector2[] points = _points;
                points[0] = center;
                for (int i = 0; i <= Steps; i++) {
                    float t = elapsed + (1f - elapsed) * i / Steps;
                    float angle = -Mathf.Pi / 2f + t * Mathf.Tau;
                    points[i + 1] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                DrawColoredPolygon(points, UIPalette.SweepShade);
            }
        }
    }
}
