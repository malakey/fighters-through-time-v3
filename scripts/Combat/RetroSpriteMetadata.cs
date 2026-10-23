using Godot;

namespace FTT.Combat {

    /// <summary>
    /// Reads the retro-sheet metadata the sprite builder writes on a SpriteFrames
    /// resource (2026-10-18 "retro v2" 2x texel sheets). Absent metadata means a
    /// legacy 1x sheet, so every default reproduces the pre-v2 behaviour.
    /// </summary>
    public static class RetroSpriteMetadata {
        public const string TexelScaleKey = "retro_texel_scale";
        public const string BakedScaleKey = "retro_baked_scale";
        public const string FeetGapsKey = "retro_feet_gaps";

        /// <summary>Texels per legacy pixel (1 for legacy sheets).</summary>
        public static float TexelScale(SpriteFrames frames) {
            if (frames == null || !frames.HasMeta(TexelScaleKey)) return 1f;
            Variant value = frames.GetMeta(TexelScaleKey);
            float scale = value.VariantType switch {
                Variant.Type.Float => (float)value.AsDouble(),
                Variant.Type.Int => value.AsInt32(),
                _ => 1f
            };
            return scale > 0f ? scale : 1f;
        }

        /// <summary>True when figure scale is baked consistently across the sheet.</summary>
        public static bool IsBakedScale(SpriteFrames frames) =>
            frames != null && frames.HasMeta(BakedScaleKey)
            && frames.GetMeta(BakedScaleKey).VariantType == Variant.Type.Bool
            && frames.GetMeta(BakedScaleKey).AsBool();

        /// <summary>Texels from the cell bottom to the feet line for an animation.</summary>
        public static int FeetGap(SpriteFrames frames, StringName animation) {
            int fallback = (int)RetroSpriteScaleNormalizer.FeetGapPixels;
            if (frames == null || animation == null || !frames.HasMeta(FeetGapsKey)) return fallback;
            Variant raw = frames.GetMeta(FeetGapsKey);
            if (raw.VariantType != Variant.Type.Dictionary) return fallback;
            Godot.Collections.Dictionary gaps = raw.AsGodotDictionary();
            string name = animation.ToString();
            foreach (Variant key in gaps.Keys) {
                if (key.VariantType != Variant.Type.String && key.VariantType != Variant.Type.StringName) continue;
                if (key.AsString() != name) continue;
                Variant value = gaps[key];
                return value.VariantType switch {
                    Variant.Type.Int => value.AsInt32(),
                    Variant.Type.Float => Mathf.RoundToInt(value.AsDouble()),
                    _ => fallback
                };
            }
            return fallback;
        }
    }
}
