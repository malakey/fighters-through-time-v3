using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// One ICN-A hairline icon (UI_SPEC_GLASS 3.5, 7.2; TG-WIDGETS): the glass kit's white 64 x 64 texture of the glyph,
    /// glow baked, tinted by <see cref="Tint"/>. Below 30 px display size the bold weight is drawn (AC23), at 30 px and
    /// above the regular one. Dimming uses the ordinary <c>Modulate</c> / <c>SelfModulate</c>.
    ///
    /// <para>Transition (plan standing decision 10): a legacy pulp name resolves through its alias (<c>anchor</c> to
    /// <c>beacon_anchor</c>, <c>anchor_hollow</c> to <c>beacon_anchor_hollow</c>); a name with neither a glass icon nor an
    /// alias (<c>anchor_pip</c>, <c>anchor_pip_cracked</c>, <c>marker</c>, <c>prompt_circle</c>) still draws its three
    /// pulp layers through the frozen pulp kit, so no screen loses an icon before Phase B restyles it.
    /// <see cref="ShadowEnabled"/> and <see cref="Flat"/> apply only to that pulp fallback. The closeout deletes the
    /// aliases and the fallback. An unknown name draws nothing and warns once.</para>
    /// </summary>
    public partial class PulpIcon : Control {

        /// <summary>Legacy: the 26 pulp icon names (UR_ART.md section 3), still drawn by the pulp fallback.</summary>
        public static readonly IReadOnlyList<string> KitNames = new[] {
            "anchor", "anchor_hollow", "anchor_pip", "anchor_pip_cracked", "burn", "cross", "defy_barred",
            "defy_notready", "defy_ready", "defy_spent", "dilation", "dormant", "dust", "flag", "freeze",
            "hourglass", "marker", "prompt_circle", "root", "shield", "slot_p1", "slot_p2", "star4", "static",
            "suppress", "venom"
        };

        /// <summary>Legacy pulp names that map to a glass icon until the closeout.</summary>
        public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string> {
            ["anchor"] = "beacon_anchor",
            ["anchor_hollow"] = "beacon_anchor_hollow"
        };

        /// <summary>Below this display size (px) the bold weight is drawn (UI_SPEC_GLASS 3.5, AC23).</summary>
        public const float BoldBelow = 30f;

        /// <summary>Legacy: the pulp icon layers' directory (the fallback).</summary>
        public const string IconDirectory = "res://assets/sprites/ui/pulp/icons/";
        private static readonly Vector2 ShadowOffset = new(2f, 2f);

        private static readonly Dictionary<string, Texture2D[]> _cache = new();
        private static readonly HashSet<string> _warned = new();
        private static List<string> _glassNames;

        private string _iconName = "";
        private string _glassName;         // resolved glass name (after the alias), or null for the pulp fallback
        private Color _tint = Colors.White;
        private bool _shadowEnabled = true;
        private bool _flat;

        public PulpIcon() {
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        }

        public string IconName {
            get => _iconName;
            set { _iconName = value ?? ""; _glassName = ResolveGlass(_iconName); QueueRedraw(); }
        }
        public Color Tint { get => _tint; set { _tint = value; QueueRedraw(); } }
        /// <summary>The pulp fallback's drop shadow (a glass icon's glow is baked; this changes nothing for it).</summary>
        public bool ShadowEnabled { get => _shadowEnabled; set { _shadowEnabled = value; QueueRedraw(); } }
        /// <summary>The pulp fallback's fill-only draw (a glass icon is one layer; this changes nothing for it).</summary>
        public bool Flat { get => _flat; set { _flat = value; QueueRedraw(); } }

        /// <summary>The glass icon this control draws (after the alias), or null when it falls back to the pulp layers.</summary>
        public string GlassName => _glassName;

        /// <summary>True while this icon draws the bold weight (its display size is under <see cref="BoldBelow"/>).</summary>
        public bool DrawsBold => UseBold(DisplaySize);

        /// <summary>The size the glyph is drawn at: the control's shorter side.</summary>
        public float DisplaySize => Mathf.Min(Size.X, Size.Y);

        /// <summary>True when a glyph shown at <paramref name="displaySize"/> px takes the bold weight.</summary>
        public static bool UseBold(float displaySize) => displaySize < BoldBelow;

        /// <summary>An icon of the given tint, sized <paramref name="size"/> x <paramref name="size"/> (the art keeps its aspect).</summary>
        public static PulpIcon Create(string name, Color tint, float size) =>
            new() { IconName = name, Tint = tint, CustomMinimumSize = new Vector2(size, size), Size = new Vector2(size, size) };

        /// <summary>Every icon name the glass kit ships (its <c>glass_icons</c> index, read once).</summary>
        public static IReadOnlyList<string> GlassNames {
            get {
                if (_glassNames != null) return _glassNames;
                _glassNames = new List<string>();
                if (PulpKit.TryEntry("glass_icons", out JsonElement index) && index.TryGetProperty("names", out JsonElement names)) {
                    foreach (JsonElement name in names.EnumerateArray()) _glassNames.Add(name.GetString());
                }
                return _glassNames;
            }
        }

        /// <summary>The glass name <paramref name="name"/> draws: itself, its alias, or null when only the pulp kit has it.</summary>
        public static string ResolveGlass(string name) {
            if (string.IsNullOrEmpty(name)) return null;
            if (PulpKit.GlassIconTexture(name, false) != null) return name;
            if (Aliases.TryGetValue(name, out string alias) && PulpKit.GlassIconTexture(alias, false) != null) return alias;
            return null;
        }

        /// <summary>True when the glass kit (directly or through an alias) or the pulp kit can draw <paramref name="name"/>.</summary>
        public static bool Exists(string name) => ResolveGlass(name) != null || ExistsInPulp(name);

        private static bool ExistsInPulp(string name) {
            foreach (string known in KitNames) if (known == name) return true;
            return false;
        }

        /// <summary>The glass texture of <paramref name="name"/> for a <paramref name="displaySize"/> px draw (regular or bold); null when the glass kit lacks it.</summary>
        public static Texture2D TextureFor(string name, float displaySize) {
            string glass = ResolveGlass(name);
            return glass == null ? null : PulpKit.GlassIconTexture(glass, UseBold(displaySize));
        }

        /// <summary>Legacy: the three pulp layers (shadow, fill, ink) of <paramref name="name"/>; null when unknown or a file is missing.</summary>
        public static Texture2D[] LoadLayers(string name) {
            name ??= "";
            if (_cache.TryGetValue(name, out Texture2D[] cached)) return cached;
            Texture2D[] layers = null;
            if (ExistsInPulp(name)) {
                var loaded = new Texture2D[3];
                bool ok = true;
                string[] suffix = { "sh", "fill", "ink" };
                for (int i = 0; i < 3; i++) {
                    string path = $"{IconDirectory}ico_{name}_{suffix[i]}.png";
                    loaded[i] = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
                    ok &= loaded[i] != null;
                }
                if (ok) layers = loaded;
            }
            if (layers == null && ResolveGlass(name) == null && _warned.Add(name)) GD.PushWarning($"PulpIcon: no icon named '{name}'.");
            _cache[name] = layers;
            return layers;
        }

        public override void _Draw() {
            if (Size.X <= 0f || Size.Y <= 0f) return;
            if (_glassName != null) {
                Texture2D glyph = PulpKit.GlassIconTexture(_glassName, UseBold(DisplaySize));
                if (glyph == null) return;
                float side = DisplaySize;
                DrawTextureRect(glyph, new Rect2((Size - new Vector2(side, side)) / 2f, new Vector2(side, side)), false, _tint);
                return;
            }
            Texture2D[] layers = LoadLayers(_iconName);
            if (layers == null) return;
            Vector2 art = layers[1].GetSize();
            float scale = Mathf.Min(Size.X / art.X, Size.Y / art.Y);
            Vector2 drawn = art * scale;
            var rect = new Rect2((Size - drawn) / 2f, drawn);
            if (!_flat && _shadowEnabled) DrawTextureRect(layers[0], new Rect2(rect.Position + ShadowOffset, drawn), false);
            DrawTextureRect(layers[1], rect, false, _tint);
            if (!_flat) DrawTextureRect(layers[2], rect, false);
        }
    }
}
