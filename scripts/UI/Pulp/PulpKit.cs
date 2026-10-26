using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// UR-WIDGETS. Reads <c>assets/sprites/ui/pulp/pulp_kit.json</c> (the UR-ART catalog) once and builds
    /// nine-slice <see cref="StyleBoxTexture"/>s from it in code, the way <c>tools/ui_theme/build_ftt_theme.py</c>
    /// does for the theme. The boxes are built in C#, never loaded as scripted resources.
    /// </summary>
    public static class PulpKit {

        public const string Directory = "res://assets/sprites/ui/pulp/";
        private const string JsonPath = Directory + "pulp_kit.json";

        private static Dictionary<string, JsonElement> _kit;

        /// <summary>True when the catalog lists <paramref name="stem"/>.</summary>
        public static bool TryEntry(string stem, out JsonElement entry) {
            if (_kit == null) {
                _kit = new Dictionary<string, JsonElement>();
                using FileAccess file = FileAccess.Open(JsonPath, FileAccess.ModeFlags.Read);
                if (file != null) {
                    using JsonDocument doc = JsonDocument.Parse(file.GetAsText());
                    foreach (JsonProperty property in doc.RootElement.EnumerateObject()) {
                        _kit[property.Name] = property.Value.Clone();
                    }
                }
            }
            return _kit.TryGetValue(stem, out entry);
        }

        /// <summary>The texture <c>stem.png</c>.</summary>
        public static Texture2D Texture(string stem) => GD.Load<Texture2D>(Directory + stem + ".png");

        /// <summary>The expand margins (L, T, R, B) the catalog lists for <paramref name="stem"/>; zeros when none.</summary>
        public static int[] Expand(string stem) =>
            TryEntry(stem, out JsonElement entry) && entry.TryGetProperty("expand_margins_LTRB", out _)
                ? ReadFour(entry, "expand_margins_LTRB") : new int[4];

        /// <summary>A nine-slice plate for <paramref name="stem"/> with the catalog's slice and expand margins; null when the stem is unknown.</summary>
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
            return plate;
        }

        private static int[] ReadFour(JsonElement entry, string key) {
            var values = new int[4];
            int i = 0;
            foreach (JsonElement v in entry.GetProperty(key).EnumerateArray()) values[i++] = v.GetInt32();
            return values;
        }
    }
}
