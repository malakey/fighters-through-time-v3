using System.Collections.Generic;
using System.Text.Json;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// TG-SELECT (CARD-A layer 3, UI_SPEC_GLASS 3.7, 4.2): the hovered or locked hero standing beside its player's side of the
    /// Fighter select, playing its idle loop at the hero's own rate (a ping-pong of the baked unique poses, in the catalog's recorded play order). It draws TG-ART's baked idle frames
    /// (<c>assets/sprites/ui/glass/select_heroes/</c>, catalog stems <c>hero_&lt;id&gt;_idle_&lt;n&gt;</c>), never the roster's
    /// <c>SpriteFrames</c>, which would load all six of a hero's atlases (41-51 MiB each). The frame is drawn at the catalog's
    /// <c>select_scale</c> (2.05) with its pivot and feet line on <see cref="FeetPoint"/> (4.2: x 260 / 1660, y 700), mirrored for
    /// player 2, over the soft shadow <c>drop-shadow(0 8px 6px rgba(0,0,0,.5))</c> as nine black taps (Godot draws no blurred
    /// texture shadow). A hero with no baked frames draws nothing. The textures are cached per hero; a frame step redraws, and
    /// nothing allocates per frame.
    /// </summary>
    public partial class SelectHeroSprite : Control {

        /// <summary>The scale and feet line the catalog's index states (fallbacks are 4.2's values).</summary>
        public static float SelectScale => Index().Scale;
        public static float SelectFeetY => Index().FeetY;

        /// <summary>The shadow: offset 8 px down, a 6 px blur approximated by taps 3 px apart, total alpha about .5.</summary>
        public const float ShadowOffsetY = 8f, ShadowSpread = 3f, ShadowTapAlpha = 0.075f;

        private sealed class HeroFrames {
            public Texture2D[] Textures;
            public Vector2[] Pivots;     // (pivot_x, feet_y) per frame, image pixels
            public int[] Order;          // play order, indices into Textures (e.g. 0 1 2 3 2 1)
            public float Fps;
        }

        private static readonly Dictionary<string, HeroFrames> Cache = new();
        private static (float Scale, float FeetY)? _index;

        private HeroFrames _frames;
        private string _heroId;
        private bool _mirrored;
        private Vector2 _feet;
        private int _frame;             // the unique pose drawn
        private double _clock;
        private static readonly Color ShadowTint = new(0f, 0f, 0f, ShadowTapAlpha);

        public SelectHeroSprite() {
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            SetProcess(false);
        }

        /// <summary>The roster ID drawn, or null when nothing is shown.</summary>
        public string HeroID => _frames == null ? null : _heroId;

        /// <summary>Player 2's hero faces the other way.</summary>
        public bool Mirrored { get => _mirrored; set { if (_mirrored == value) return; _mirrored = value; QueueRedraw(); } }

        /// <summary>Where the frame's pivot and feet line land, in this control's coordinates.</summary>
        public Vector2 FeetPoint { get => _feet; set { if (_feet == value) return; _feet = value; QueueRedraw(); } }

        /// <summary>The current idle pose (an index into the baked unique poses) and its texture.</summary>
        public int FrameIndex => _frame;
        public Texture2D FrameTexture => _frames?.Textures[_frame];
        /// <summary>The hero's idle rate from the catalog (10 fps today).</summary>
        public float FramesPerSecond => _frames?.Fps ?? 0f;
        /// <summary>How many unique poses are baked, and how many steps the loop plays (the ping-pong repeats poses).</summary>
        public int FrameCount => _frames?.Textures.Length ?? 0;
        public int LoopLength => _frames?.Order.Length ?? 0;

        /// <summary>The frame's drawn rectangle in this control's coordinates (unmirrored extent; a mirrored frame covers the reflection about <see cref="FeetPoint"/>'s x).</summary>
        public Rect2 DrawnRect {
            get {
                Texture2D texture = FrameTexture;
                if (texture == null) return new Rect2();
                Vector2 pivot = _frames.Pivots[_frame];
                float scale = SelectScale;
                Vector2 size = texture.GetSize() * scale;
                float left = _mirrored ? _feet.X - (texture.GetWidth() - pivot.X) * scale : _feet.X - pivot.X * scale;
                return new Rect2(left, _feet.Y - pivot.Y * scale, size);
            }
        }

        /// <summary>Shows <paramref name="heroId"/>'s idle loop (from its first frame), or nothing for null or a hero with no frames.</summary>
        public void ShowHero(string heroId) {
            if (heroId == _heroId && (_frames != null || heroId == null)) return;
            _heroId = heroId;
            _frames = heroId == null ? null : FramesFor(heroId);
            _frame = 0;
            _clock = 0d;
            SetProcess(_frames != null && _frames.Order.Length > 1 && _frames.Fps > 0f);
            if (_frames != null) _frame = _frames.Order[0];
            QueueRedraw();
        }

        /// <summary>Whether TG-ART baked idle frames for <paramref name="heroId"/>.</summary>
        public static bool HasFrames(string heroId) => FramesFor(heroId) != null;

        public override void _Process(double delta) {
            if (_frames == null) return;
            _clock += delta;
            int frame = _frames.Order[(int)(_clock * _frames.Fps) % _frames.Order.Length];
            if (frame == _frame) return;
            _frame = frame;
            QueueRedraw();
        }

        public override void _Draw() {
            Texture2D texture = FrameTexture;
            if (texture == null) return;
            Vector2 pivot = _frames.Pivots[_frame];
            float scale = SelectScale;
            var flip = new Vector2(_mirrored ? -scale : scale, scale);
            var origin = new Rect2(-pivot, texture.GetSize());
            for (int y = -1; y <= 1; y++) {
                for (int x = -1; x <= 1; x++) {
                    DrawSetTransform(_feet + new Vector2(x * ShadowSpread, ShadowOffsetY + y * ShadowSpread), 0f, flip);
                    DrawTextureRect(texture, origin, false, ShadowTint);
                }
            }
            DrawSetTransform(_feet, 0f, flip);
            DrawTextureRect(texture, origin, false);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        private static (float Scale, float FeetY) Index() {
            if (_index.HasValue) return _index.Value;
            float scale = 2.05f, feet = 700f;
            if (PulpKit.TryEntry("hero_select_index", out JsonElement entry)) {
                if (entry.TryGetProperty("select_scale", out JsonElement s) && s.TryGetSingle(out float sv)) scale = sv;
                if (entry.TryGetProperty("select_feet_y", out JsonElement f) && f.TryGetSingle(out float fv)) feet = fv;
            }
            _index = (scale, feet);
            return _index.Value;
        }

        private static HeroFrames FramesFor(string heroId) {
            if (string.IsNullOrEmpty(heroId)) return null;
            if (Cache.TryGetValue(heroId, out HeroFrames cached)) return cached;
            var textures = new List<Texture2D>();
            var pivots = new List<Vector2>();
            float fps = 0f;
            int[] order = null;
            for (int n = 0; ; n++) {
                string stem = $"hero_{heroId}_idle_{n}";
                if (!PulpKit.TryEntry(stem, out JsonElement entry)) break;
                Texture2D texture = PulpKit.Texture(stem);
                if (texture == null) break;
                textures.Add(texture);
                pivots.Add(new Vector2(Read(entry, "pivot_x"), Read(entry, "feet_y")));
                if (fps <= 0f) fps = Read(entry, "fps");
                // The play order rides on pose 0's entry; a missing one plays the poses straight through.
                if (n == 0 && entry.TryGetProperty("order", out JsonElement o) && o.ValueKind == JsonValueKind.Array) {
                    // Any element that is not a whole number discards the whole order (straight-through fallback).
                    var list = new List<int>();
                    bool valid = true;
                    foreach (JsonElement i in o.EnumerateArray()) {
                        if (i.ValueKind != JsonValueKind.Number || !i.TryGetInt32(out int v)) { valid = false; break; }
                        list.Add(v);
                    }
                    if (valid && list.Count > 0) order = list.ToArray();
                }
            }
            if (textures.Count > 0) {
                if (order != null) foreach (int i in order) if (i < 0 || i >= textures.Count) { order = null; break; }
                if (order == null) { order = new int[textures.Count]; for (int i = 0; i < order.Length; i++) order[i] = i; }
            }
            HeroFrames frames = textures.Count == 0 ? null
                : new HeroFrames { Textures = textures.ToArray(), Pivots = pivots.ToArray(), Order = order, Fps = fps };
            Cache[heroId] = frames;
            return frames;
        }

        private static float Read(JsonElement entry, string name) =>
            entry.TryGetProperty(name, out JsonElement value) && value.TryGetSingle(out float result) ? result : 0f;
    }
}
