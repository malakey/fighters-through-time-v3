using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// One ICN-A hairline icon (UI_SPEC_GLASS 3.5, 7.2; TG-WIDGETS): the glass kit's white 64 x 64 texture of the glyph,
    /// glow baked, tinted by <see cref="Tint"/>. Below 30 px display size the bold weight is drawn (AC23), at 30 px and
    /// above the regular one. Dimming uses the ordinary <c>Modulate</c> / <c>SelfModulate</c>.
    ///
    /// <para>Every name is a glass icon name (<see cref="GlassNames"/>). The pulp names, their aliases and the pulp
    /// layer fallback were retired by the glass closeout (TG-CLOSE, 2026-10-27); an unknown name draws nothing and warns
    /// once.</para>
    /// </summary>
    public partial class PulpIcon : Control {

        /// <summary>Below this display size (px) the bold weight is drawn (UI_SPEC_GLASS 3.5, AC23).</summary>
        public const float BoldBelow = 30f;

        private static readonly HashSet<string> _warned = new();
        private static List<string> _glassNames;

        private string _iconName = "";
        private string _glassName;         // the glass name drawn, or null when the kit has no such icon
        private Color _tint = Colors.White;

        public PulpIcon() {
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        }

        public string IconName {
            get => _iconName;
            set {
                _iconName = value ?? "";
                _glassName = ResolveGlass(_iconName);
                if (_glassName == null && _iconName.Length > 0 && _warned.Add(_iconName)) {
                    GD.PushWarning($"PulpIcon: no icon named '{_iconName}'.");
                }
                QueueRedraw();
            }
        }
        public Color Tint { get => _tint; set { _tint = value; QueueRedraw(); } }

        /// <summary>The glass icon this control draws, or null when the kit has no icon of that name.</summary>
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

        /// <summary>The glass name <paramref name="name"/> draws: itself when the kit has it, else null.</summary>
        public static string ResolveGlass(string name) =>
            !string.IsNullOrEmpty(name) && PulpKit.GlassIconTexture(name, false) != null ? name : null;

        /// <summary>True when the glass kit can draw <paramref name="name"/>.</summary>
        public static bool Exists(string name) => ResolveGlass(name) != null;

        /// <summary>The glass texture of <paramref name="name"/> for a <paramref name="displaySize"/> px draw (regular or bold); null when the kit lacks it.</summary>
        public static Texture2D TextureFor(string name, float displaySize) {
            string glass = ResolveGlass(name);
            return glass == null ? null : PulpKit.GlassIconTexture(glass, UseBold(displaySize));
        }

        public override void _Draw() {
            if (Size.X <= 0f || Size.Y <= 0f || _glassName == null) return;
            Texture2D glyph = PulpKit.GlassIconTexture(_glassName, UseBold(DisplaySize));
            if (glyph == null) return;
            float side = DisplaySize;
            DrawTextureRect(glyph, new Rect2((Size - new Vector2(side, side)) / 2f, new Vector2(side, side)), false, _tint);
        }
    }
}
