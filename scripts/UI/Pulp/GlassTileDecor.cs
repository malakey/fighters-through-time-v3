using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// TG-WIDGETS. The glass tile (UI_SPEC_GLASS 3.0 G4, 3.7, 9.2): the slab behind a select tile, a stage tile, a drill
    /// picker tile, a character sub-screen tile or a Resonance chip. The content (names, art placement beyond the optional
    /// portrait window, layout) is the screen's; this draws the shape and the state.
    /// <list type="bullet">
    /// <item>Shape: chamfer <see cref="Chamfer"/> at top-left and bottom-right (16 the select and stage tiles, 12 the chips);
    /// a 1.5 px edge over the 160 degree fill rgba(26,38,74,.90) to rgba(6,10,26,.94).</item>
    /// <item>Edge per state (<see cref="Edge"/>): <c>Dim</c> (normal, a locked chip) <c>edge-dim</c>; <c>Standard</c> (an
    /// available chip) <c>edge</c>; <c>Lit</c> (an unlocked chip) <c>edge-hi</c> with a rgba(glow,.20) wash; <c>Focus</c> the
    /// button focus edge (100 degrees, <c>focus</c> to rgba(glow,.55)) and the 14 px gold glow. A focusable host follows its
    /// own focus, and the mouse entering it gives it focus (hover is focus, AC8).</item>
    /// <item>Owners (<see cref="Owners"/>, the select cursors): a 135 degree edge from white through the slot colour at 40 %
    /// to the slot colour at .5 (both players: white, P1 at 40 %, P2), a 14 px glow in the slot colour at .6, and the
    /// players' shape badges top-right (P2's 32 px left of P1's). Slot colours read the active player-slot palette through
    /// <see cref="UIPalette.OwnerOnDark"/>.</item>
    /// <item>Locked (<see cref="LockedBy"/>): a 28 px square in the player's colour at (9, 9) holding <c>check_solid</c>
    /// (20 px, #04141A).</item>
    /// <item>Optional portrait window (<see cref="Portrait"/>): inset 8 / 8 / 8 / 42, a radial glow rgba(glow,.28) at
    /// (50 %, 28 %) fading by 72 %, the art at 150 % of the window offset (-25 %, -4 %), clipped to the window.</item>
    /// </list>
    /// <see cref="Attach"/> adds the slab as the host's first child and a marks layer (badges, lock mark) above the host's
    /// content. Geometry is rebuilt only on a size or chamfer change; a state change recolours the cached polygons; the draw
    /// allocates nothing.
    /// </summary>
    public partial class GlassTileDecor : Control {

        public enum TileEdge { Dim, Standard, Lit, Focus }

        public const string NodeName = "GlassTileDecor";
        public const string MarksName = "GlassTileMarks";
        public const float SelectChamfer = 16f, ChipChamfer = 12f;
        public const int OwnerP1 = 1, OwnerP2 = 2;
        public const float BadgeBox = 28f, BadgeInset = 8f, BadgeShape = 22f, BadgePitch = 32f;
        public const float LockBox = 28f, LockInset = 9f, LockGlyph = 20f;
        public const int GlowSize = 14;
        public static readonly Color LitWash = new(UIPalette.Accent, 0.20f);
        public static readonly Color WindowGlow = new(UIPalette.Accent, 0.28f);

        private float _chamfer = SelectChamfer;
        private TileEdge _edge = TileEdge.Dim;
        private int _owners;
        private int _lockedBy = -1;
        private bool _showBadges = true;
        private bool _focused;
        private Control _host;
        private TileMarks _marks;
        private Texture2D _portrait;
        private Rect2 _portraitInsets = new(8f, 8f, 8f, 42f);   // left, top, right, bottom
        private readonly Vector2[] _outer = new Vector2[6];
        private readonly Vector2[] _fill = new Vector2[6];
        private readonly Color[] _edgeColors = new Color[6];
        private readonly Color[] _fillColors = new Color[6];
        private readonly Vector2[] _window = new Vector2[4];
        private readonly Vector2[] _windowUvs = new Vector2[4];
        private readonly Color[] _white = { Colors.White, Colors.White, Colors.White, Colors.White };
        private Vector2[] _glowFan = System.Array.Empty<Vector2>();
        private Color[] _glowFanColors = System.Array.Empty<Color>();
        private int[] _glowIndices = System.Array.Empty<int>();
        private readonly StyleBoxFlat _glow1, _glow2;
        private Vector2 _builtFor = new(-1f, -1f);
        private float _builtChamfer = -1f;
        private int _builtStateKey = int.MinValue;

        /// <summary>How many times the polygons were rebuilt (a state change recolours, a size change rebuilds).</summary>
        public int GeometryBuilds { get; private set; }
        /// <summary>The edge layer's vertex colours as last drawn (read-only; for tests and captures).</summary>
        public Color[] EdgeColors { get { EnsureGeometry(); return _edgeColors; } }

        public GlassTileDecor() {
            Name = NodeName;
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            SetAnchorsPreset(LayoutPreset.FullRect);
            _glow1 = new StyleBoxFlat { DrawCenter = false, BgColor = new Color(0, 0, 0, 0), ShadowSize = GlowSize, AntiAliasing = true, CornerDetail = 1 };
            _glow2 = new StyleBoxFlat { DrawCenter = false, BgColor = new Color(0, 0, 0, 0), ShadowSize = GlowSize, AntiAliasing = true, CornerDetail = 1 };
        }

        /// <summary>Adds the slab as <paramref name="tile"/>'s first child and the marks layer above its content; a second call returns the first.</summary>
        public static GlassTileDecor Attach(Control tile, float chamfer = SelectChamfer) {
            if (tile == null) return null;
            if (tile.GetNodeOrNull<GlassTileDecor>(NodeName) is { } existing) return existing;
            var decor = new GlassTileDecor { _chamfer = chamfer };
            tile.AddChild(decor);
            tile.MoveChild(decor, 0);
            decor._marks = new TileMarks(decor) { Name = MarksName };
            decor._marks.SetAnchorsPreset(LayoutPreset.FullRect);
            tile.AddChild(decor._marks);
            return decor;
        }

        public float Chamfer { get => _chamfer; set { _chamfer = Mathf.Max(0f, value); QueueRedraw(); } }
        /// <summary>The resting edge (a focused host shows <see cref="TileEdge.Focus"/> over it).</summary>
        public TileEdge Edge { get => _edge; set { _edge = value; Changed(); } }
        /// <summary>The players whose cursor is on the tile: bit <see cref="OwnerP1"/>, bit <see cref="OwnerP2"/>.</summary>
        public int Owners { get => _owners; set { _owners = value & 3; Changed(); } }
        /// <summary>The player who locked the tile (0 or 1), or -1.</summary>
        public int LockedBy { get => _lockedBy; set { _lockedBy = value is 0 or 1 ? value : -1; Changed(); } }
        public bool ShowOwnerBadges { get => _showBadges; set { _showBadges = value; Changed(); } }
        /// <summary>True while the host has focus (or the mouse gave it focus).</summary>
        public bool Focused => _focused;
        /// <summary>The edge drawn now: the focus edge on a focused host, else <see cref="Edge"/>.</summary>
        public TileEdge EffectiveEdge => _focused ? TileEdge.Focus : _edge;
        /// <summary>Optional art for the portrait window; null draws no window.</summary>
        public Texture2D Portrait { get => _portrait; set { _portrait = value; Changed(); } }
        /// <summary>The portrait window's insets as (left, top, right, bottom); 3.7's 8 / 8 / 8 / 42 by default.</summary>
        public Rect2 PortraitInsets { get => _portraitInsets; set { _portraitInsets = value; _builtFor = new Vector2(-1f, -1f); Changed(); } }

        private void Changed() {
            QueueRedraw();
            _marks?.QueueRedraw();
        }

        public override void _EnterTree() {
            _host = GetParent() as Control;
            if (_host == null) return;
            _host.FocusEntered += OnFocusEntered;
            _host.FocusExited += OnFocusExited;
            _host.MouseEntered += OnMouseEntered;
            _focused = _host.HasFocus();
        }

        public override void _ExitTree() {
            if (_host == null || !IsInstanceValid(_host)) return;
            _host.FocusEntered -= OnFocusEntered;
            _host.FocusExited -= OnFocusExited;
            _host.MouseEntered -= OnMouseEntered;
            _host = null;
        }

        private void OnFocusEntered() { _focused = true; Changed(); }
        private void OnFocusExited() { _focused = false; Changed(); }

        private void OnMouseEntered() {
            if (_host != null && _host.IsVisibleInTree() && _host.FocusMode != FocusModeEnum.None && !_host.HasFocus()
                && !(_host is BaseButton { Disabled: true })) {
                _host.GrabFocus();
            }
        }

        public override void _Notification(int what) {
            if (what == NotificationResized) Changed();
        }

        private int StateKey() => ((int)EffectiveEdge) | (_owners << 4) | ((_lockedBy + 1) << 8) | (_showBadges ? 1 << 12 : 0)
            | (_portrait != null ? 1 << 13 : 0);

        private void EnsureGeometry() {
            var box = new Rect2(Vector2.Zero, Size);
            bool sizeChanged = Size != _builtFor || !Mathf.IsEqualApprox(_chamfer, _builtChamfer);
            int key = StateKey();
            if (!sizeChanged && key == _builtStateKey) return;
            if (sizeChanged) {
                GeometryBuilds++;
                _builtFor = Size;
                _builtChamfer = _chamfer;
                GlassGeometry.Chamfer(box, _chamfer, _outer);
                GlassGeometry.ChamferFill(box, _chamfer, GlassGeometry.EdgeWidth, _fill);
                for (int i = 0; i < 6; i++) _fillColors[i] = GlassGeometry.Stop2(UIPalette.TileFillTop, UIPalette.TileFillBottom, GlassGeometry.GradientT(_fill[i], box, 160f));
                Rect2 window = WindowRect();
                _window[0] = window.Position;
                _window[1] = new Vector2(window.End.X, window.Position.Y);
                _window[2] = window.End;
                _window[3] = new Vector2(window.Position.X, window.End.Y);
                // The art at 150 % of the window, offset (-25 %, -4 %), clipped to the window by its UVs.
                var art = new Rect2(window.Position + new Vector2(-0.25f, -0.04f) * window.Size, window.Size * 1.5f);
                GlassGeometry.BoxUvs(_window, 4, art, _windowUvs);
                BuildWindowGlow(window);
            }
            _builtStateKey = key;
            Color edgeFlat = EffectiveEdge switch {
                TileEdge.Standard => UIPalette.Edge,
                TileEdge.Lit => UIPalette.EdgeHi,
                _ => UIPalette.EdgeDim
            };
            Color p1 = UIPalette.OwnerOnDark(0), p2 = UIPalette.OwnerOnDark(1);
            for (int i = 0; i < 6; i++) {
                float t135 = GlassGeometry.GradientT(_outer[i], box, 135f);
                _edgeColors[i] = _owners switch {
                    OwnerP1 => GlassGeometry.Stop3(Colors.White, p1, 0.40f, new Color(p1, 0.5f), t135),
                    OwnerP2 => GlassGeometry.Stop3(Colors.White, p2, 0.40f, new Color(p2, 0.5f), t135),
                    OwnerP1 | OwnerP2 => GlassGeometry.Stop3(Colors.White, p1, 0.40f, p2, t135),
                    _ => EffectiveEdge == TileEdge.Focus
                        ? GlassGeometry.Stop2(UIPalette.Focus, UIPalette.FocusGlow, GlassGeometry.GradientT(_outer[i], box, 100f))
                        : edgeFlat
                };
            }
            _glow1.ShadowColor = (_owners & OwnerP1) != 0 ? new Color(p1, 0.6f) : (_owners & OwnerP2) != 0 ? new Color(p2, 0.6f) : UIPalette.FocusGlow;
            _glow2.ShadowColor = new Color(p2, 0.6f);
            int cut = Mathf.RoundToInt(_chamfer);
            _glow1.CornerRadiusTopLeft = _glow1.CornerRadiusBottomRight = cut;
            _glow2.CornerRadiusTopLeft = _glow2.CornerRadiusBottomRight = cut;
        }

        private Rect2 WindowRect() => new(
            _portraitInsets.Position.X, _portraitInsets.Position.Y,
            Mathf.Max(0f, Size.X - _portraitInsets.Position.X - _portraitInsets.Size.X),
            Mathf.Max(0f, Size.Y - _portraitInsets.Position.Y - _portraitInsets.Size.Y));

        // The window's radial glow as a fan clipped to the window: a centre vertex at (50 %, 28 %) and a ring at 72 % of the
        // reference radius (the distance to the window's farthest corner), each spoke clipped where it leaves the window, so
        // the colour falls linearly along it.
        private void BuildWindowGlow(Rect2 window) {
            const int Spokes = 32;
            Vector2 centre = window.Position + new Vector2(0.5f, 0.28f) * window.Size;
            float far = 0f;
            foreach (Vector2 corner in _window) far = Mathf.Max(far, centre.DistanceTo(corner));
            float radius = 0.72f * far;
            _glowFan = new Vector2[Spokes + 1];
            _glowFanColors = new Color[Spokes + 1];
            _glowIndices = new int[Spokes * 3];
            for (int i = 0; i < Spokes; i++) {
                _glowIndices[i * 3] = 0;
                _glowIndices[i * 3 + 1] = i + 1;
                _glowIndices[i * 3 + 2] = i + 1 < Spokes ? i + 2 : 1;
            }
            _glowFan[0] = centre;
            _glowFanColors[0] = WindowGlow;
            for (int i = 0; i < Spokes; i++) {
                float a = Mathf.Tau * i / Spokes;
                var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                float reach = radius;
                if (dir.X > 0.0001f) reach = Mathf.Min(reach, (window.End.X - centre.X) / dir.X);
                if (dir.X < -0.0001f) reach = Mathf.Min(reach, (window.Position.X - centre.X) / dir.X);
                if (dir.Y > 0.0001f) reach = Mathf.Min(reach, (window.End.Y - centre.Y) / dir.Y);
                if (dir.Y < -0.0001f) reach = Mathf.Min(reach, (window.Position.Y - centre.Y) / dir.Y);
                _glowFan[i + 1] = centre + dir * reach;
                _glowFanColors[i + 1] = new Color(WindowGlow, WindowGlow.A * (1f - reach / Mathf.Max(radius, 0.001f)));
            }
        }

        public override void _Draw() {
            EnsureGeometry();
            var box = new Rect2(Vector2.Zero, Size);
            if (_owners != 0 || EffectiveEdge == TileEdge.Focus) DrawStyleBox(_glow1, box);
            if (_owners == (OwnerP1 | OwnerP2)) DrawStyleBox(_glow2, box);
            DrawPolygon(_outer, _edgeColors);
            DrawPolygon(_fill, _fillColors);
            if (EffectiveEdge == TileEdge.Lit && _owners == 0) DrawColoredPolygon(_fill, LitWash);
            if (_portrait != null) {
                // A fan of triangles from the centre (one indexed array), so the glow falls linearly along each spoke.
                if (_glowIndices.Length > 0) RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), _glowIndices, _glowFan, _glowFanColors);
                DrawPolygon(_window, _white, _windowUvs, _portrait);
            }
        }

        /// <summary>The marks above the tile's content: the owners' shape badges and the lock mark.</summary>
        private partial class TileMarks : Control {
            private readonly GlassTileDecor _owner;

            public TileMarks(GlassTileDecor owner) {
                _owner = owner;
                MouseFilter = MouseFilterEnum.Ignore;
                TextureFilter = TextureFilterEnum.LinearWithMipmaps;
                ZIndex = 1;
            }

            public override void _Draw() {
                Vector2 size = Size;
                if (_owner._showBadges && _owner._owners != 0) {
                    float right = size.X - BadgeInset;
                    if ((_owner._owners & OwnerP1) != 0) {
                        DrawShape("slot_p1", new Rect2(right - BadgeBox, BadgeInset, BadgeBox, BadgeBox), UIPalette.OwnerOnDark(0));
                        right -= BadgePitch;
                    }
                    if ((_owner._owners & OwnerP2) != 0) {
                        DrawShape("slot_p2", new Rect2(right - BadgeBox, BadgeInset, BadgeBox, BadgeBox), UIPalette.OwnerOnDark(1));
                    }
                }
                if (_owner._lockedBy >= 0) {
                    var square = new Rect2(LockInset, LockInset, LockBox, LockBox);
                    DrawRect(square, UIPalette.OwnerOnDark(_owner._lockedBy));
                    Texture2D check = PulpKit.GlassIconTexture("check_solid", false);
                    if (check != null) {
                        float inset = (LockBox - LockGlyph) / 2f;
                        DrawTextureRect(check, new Rect2(square.Position + new Vector2(inset, inset), new Vector2(LockGlyph, LockGlyph)), false, UIPalette.TabSelectedInk);
                    }
                }
            }

            private void DrawShape(string name, Rect2 box, Color color) {
                Texture2D shape = PulpKit.GlassIconTexture(name, false);
                if (shape == null) return;
                float inset = (BadgeBox - BadgeShape) / 2f;
                DrawTextureRect(shape, new Rect2(box.Position + new Vector2(inset, inset), new Vector2(BadgeShape, BadgeShape)), false, color);
            }
        }
    }
}
