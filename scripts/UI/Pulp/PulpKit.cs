using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// The UI kit reader ("Pulp" is the kit's legacy name). Reads two catalogs once: the Temporal Glass kit
    /// <c>assets/sprites/ui/glass/glass_kit.json</c> (TG-ART) first, then the frozen pulp kit
    /// <c>assets/sprites/ui/pulp/pulp_kit.json</c> (UR-ART), so every pulp stem keeps resolving until the
    /// closeout retires it (plan <c>docs/UI_GLASS_2026-10-26_PLAN.md</c>, standing decision 10). Every glass stem
    /// carries a glass prefix (<c>glass_</c>, <c>glass_ico_</c>, <c>hero_</c>), so no stem is in both. Nine-slice
    /// <see cref="StyleBoxTexture"/>s are built from the catalog in code, the way
    /// <c>tools/ui_theme/build_ftt_theme.py</c> does for the theme; they are never loaded as scripted resources.
    /// </summary>
    public static class PulpKit {

        /// <summary>The pulp kit's directory (a pulp stem's texture is <c>Directory + stem + ".png"</c>).</summary>
        public const string Directory = "res://assets/sprites/ui/pulp/";
        /// <summary>The glass kit's directory (a glass stem's texture is its catalog <c>file</c> under it).</summary>
        public const string GlassDirectory = "res://assets/sprites/ui/glass/";
        private const string JsonPath = Directory + "pulp_kit.json";
        private const string GlassJsonPath = GlassDirectory + "glass_kit.json";

        private static Dictionary<string, JsonElement> _kit;
        private static HashSet<string> _glass;

        /// <summary>True when either catalog lists <paramref name="stem"/> (the glass one is read first).</summary>
        public static bool TryEntry(string stem, out JsonElement entry) {
            EnsureLoaded();
            return _kit.TryGetValue(stem ?? "", out entry);
        }

        /// <summary>True when either catalog lists <paramref name="stem"/>.</summary>
        public static bool Has(string stem) => TryEntry(stem, out _);

        /// <summary>True when <paramref name="stem"/> comes from the glass catalog.</summary>
        public static bool IsGlass(string stem) {
            EnsureLoaded();
            return stem != null && _glass.Contains(stem);
        }

        private static void EnsureLoaded() {
            if (_kit != null) return;
            _kit = new Dictionary<string, JsonElement>();
            _glass = new HashSet<string>();
            Read(GlassJsonPath, glass: true);
            Read(JsonPath, glass: false);
        }

        private static void Read(string path, bool glass) {
            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) return;
            using JsonDocument doc = JsonDocument.Parse(file.GetAsText());
            foreach (JsonProperty property in doc.RootElement.EnumerateObject()) {
                if (_kit.ContainsKey(property.Name)) continue;
                _kit[property.Name] = property.Value.Clone();
                if (glass) _glass.Add(property.Name);
            }
        }

        private static readonly Dictionary<string, Texture2D> Textures = new();
        private static readonly Dictionary<string, int[]> Expands = new();
        private static readonly Dictionary<string, StyleBoxTexture> Shared = new();
        private static readonly Dictionary<(string, bool), Texture2D> GlassIcons = new();

        /// <summary>
        /// The res:// path <paramref name="stem"/>'s texture loads from: a glass stem's catalog <c>file</c> under
        /// <see cref="GlassDirectory"/> (a PNG, or a <c>GradientTexture2D</c> .tres), else the pulp convention
        /// <c>Directory + stem + ".png"</c>.
        /// </summary>
        public static string PathOf(string stem) {
            if (IsGlass(stem) && _kit[stem].TryGetProperty("file", out JsonElement file)) {
                return GlassDirectory + file.GetString();
            }
            return Directory + stem + ".png";
        }

        /// <summary>The texture of <paramref name="stem"/> (cached: the path string and the lookup are built once, so a draw path may call it every frame).</summary>
        public static Texture2D Texture(string stem) {
            if (!Textures.TryGetValue(stem, out Texture2D texture)) {
                texture = GD.Load<Texture2D>(PathOf(stem));
                if (texture != null) Textures[stem] = texture;
            }
            return texture;
        }

        /// <summary>The expand margins (L, T, R, B) the catalog lists for <paramref name="stem"/>; zeros when none. The array is shared: read it, never write it.</summary>
        public static int[] Expand(string stem) {
            if (!Expands.TryGetValue(stem, out int[] margins)) {
                margins = TryEntry(stem, out JsonElement entry) && entry.TryGetProperty("expand_margins_LTRB", out _)
                    ? ReadFour(entry, "expand_margins_LTRB") : new int[4];
                Expands[stem] = margins;
            }
            return margins;
        }

        /// <summary>
        /// A shared, read-only <see cref="Plate"/> for draw paths (<c>DrawStyleBox</c> every frame): built once per
        /// stem. Callers must not modify it or hand it to a node as an override; use <see cref="Plate"/> for that.
        /// </summary>
        public static StyleBoxTexture SharedPlate(string stem) {
            if (!Shared.TryGetValue(stem, out StyleBoxTexture plate)) {
                plate = Plate(stem);
                if (plate != null) Shared[stem] = plate;
            }
            return plate;
        }

        /// <summary>
        /// A nine-slice plate for <paramref name="stem"/> with the catalog's slice and expand margins; null when the
        /// stem is unknown. A glass entry with <c>"axis_stretch": "tile"</c> (the disabled chevron's dashed edge)
        /// tiles horizontally.
        /// </summary>
        public static StyleBoxTexture Plate(string stem) {
            if (!TryEntry(stem, out JsonElement entry) || !entry.TryGetProperty("nine_slice_margins_LTRB", out _)) return null;
            var plate = new StyleBoxTexture { Texture = Texture(stem) };
            int[] slice = ReadFour(entry, "nine_slice_margins_LTRB");
            plate.TextureMarginLeft = slice[0]; plate.TextureMarginTop = slice[1];
            plate.TextureMarginRight = slice[2]; plate.TextureMarginBottom = slice[3];
            if (entry.TryGetProperty("expand_margins_LTRB", out _)) {
                int[] expand = ReadFour(entry, "expand_margins_LTRB");
                plate.ExpandMarginLeft = expand[0]; plate.ExpandMarginTop = expand[1];
                plate.ExpandMarginRight = expand[2]; plate.ExpandMarginBottom = expand[3];
            }
            if (IsGlass(stem) && entry.TryGetProperty("axis_stretch", out JsonElement stretch) && stretch.GetString() == "tile") {
                plate.AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile;
            }
            return plate;
        }

        /// <summary>The glass catalog stem of icon <paramref name="name"/>: <c>glass_ico_&lt;name&gt;</c>, or <c>glass_ico_&lt;name&gt;_bold</c>.</summary>
        public static string GlassIconStem(string name, bool bold) => bold ? $"glass_ico_{name}_bold" : $"glass_ico_{name}";

        /// <summary>True when the glass set has icon <paramref name="name"/> in both weights (allocates; not for a draw path).</summary>
        public static bool HasGlassIcon(string name) =>
            !string.IsNullOrEmpty(name) && IsGlass(GlassIconStem(name, false)) && IsGlass(GlassIconStem(name, true));

        /// <summary>
        /// The white 64 x 64 texture of glass icon <paramref name="name"/> (ICN-A, glow baked, tinted by modulate) in
        /// its bold or regular weight (UI_SPEC_GLASS 3.5: bold below 30 px display size); null when the glass set has
        /// no such icon. Cached per name and weight, so a draw path may call it every frame without allocating.
        /// </summary>
        public static Texture2D GlassIconTexture(string name, bool bold) {
            if (string.IsNullOrEmpty(name)) return null;
            if (!GlassIcons.TryGetValue((name, bold), out Texture2D texture)) {
                string stem = GlassIconStem(name, bold);
                texture = IsGlass(stem) ? Texture(stem) : null;
                GlassIcons[(name, bold)] = texture;
            }
            return texture;
        }

        private static int[] ReadFour(JsonElement entry, string key) {
            var values = new int[4];
            int i = 0;
            foreach (JsonElement v in entry.GetProperty(key).EnumerateArray()) values[i++] = v.GetInt32();
            return values;
        }
    }
}
