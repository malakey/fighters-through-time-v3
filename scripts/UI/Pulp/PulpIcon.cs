using System.Collections.Generic;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// UR-WIDGETS. One ICN-B icon from the UR-ART kit, drawn as its three layers: the black silhouette
    /// <c>_sh</c> under at a (2, 2) pixel offset, the white <c>_fill</c> tinted by <see cref="Tint"/>, and
    /// the <c>_ink</c> outline over, never tinted. <see cref="Flat"/> draws the fill only (the spec draws the
    /// <c>dormant</c> and <c>cross</c> slot-lock glyphs that way). Dimming uses the ordinary
    /// <c>Modulate</c> / <c>SelfModulate</c>. An unknown name draws nothing and warns once.
    /// </summary>
    public partial class PulpIcon : Control {

        /// <summary>Every icon name the kit ships (UR_ART.md section 3).</summary>
        public static readonly IReadOnlyList<string> KitNames = new[] {
            "anchor", "anchor_hollow", "anchor_pip", "anchor_pip_cracked", "burn", "cross", "defy_barred",
            "defy_notready", "defy_ready", "defy_spent", "dilation", "dormant", "dust", "flag", "freeze",
            "hourglass", "marker", "prompt_circle", "root", "shield", "slot_p1", "slot_p2", "star4", "static",
            "suppress", "venom"
        };

        public const string IconDirectory = "res://assets/sprites/ui/pulp/icons/";
        private static readonly Vector2 ShadowOffset = new(2f, 2f);

        private static readonly Dictionary<string, Texture2D[]> _cache = new();
        private static readonly HashSet<string> _warned = new();

        private string _iconName = "";
        private Color _tint = Colors.White;
        private bool _shadowEnabled = true;
        private bool _flat;

        public PulpIcon() {
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        }

        public string IconName { get => _iconName; set { _iconName = value ?? ""; QueueRedraw(); } }
        public Color Tint { get => _tint; set { _tint = value; QueueRedraw(); } }
        public bool ShadowEnabled { get => _shadowEnabled; set { _shadowEnabled = value; QueueRedraw(); } }
        /// <summary>Fill only: no ink and no shadow.</summary>
        public bool Flat { get => _flat; set { _flat = value; QueueRedraw(); } }

        /// <summary>An icon of the given tint, sized <paramref name="size"/> x <paramref name="size"/> (the art keeps its aspect).</summary>
        public static PulpIcon Create(string name, Color tint, float size) =>
            new() { IconName = name, Tint = tint, CustomMinimumSize = new Vector2(size, size), Size = new Vector2(size, size) };

        /// <summary>True when the kit ships <paramref name="name"/>.</summary>
        public static bool Exists(string name) {
            foreach (string known in KitNames) if (known == name) return true;
            return false;
        }

        /// <summary>The three layers (shadow, fill, ink) of <paramref name="name"/>; null when unknown or a file is missing.</summary>
        public static Texture2D[] LoadLayers(string name) {
            name ??= "";
            if (_cache.TryGetValue(name, out Texture2D[] cached)) return cached;
            Texture2D[] layers = null;
            if (Exists(name)) {
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
            if (layers == null && _warned.Add(name)) GD.PushWarning($"PulpIcon: no icon named '{name}'.");
            _cache[name] = layers;
            return layers;
        }

        public override void _Draw() {
            Texture2D[] layers = LoadLayers(_iconName);
            if (layers == null || Size.X <= 0f || Size.Y <= 0f) return;
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
