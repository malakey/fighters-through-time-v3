using System;
using System.Collections.Generic;
using Godot;

namespace FTT.Combat {

    /// <summary>
    /// Resolves the production retro ability atlases without moving visual paths
    /// into gameplay tuning. Every roster member owns one four-animation
    /// SpriteFrames resource; callers remain best-effort and fall back to the
    /// existing presentation geometry when an asset is absent.
    /// </summary>
    public static class AbilityVisualLibrary {
        private static readonly HashSet<string> RosterIDs = new(StringComparer.Ordinal) {
            "einstein", "joan", "leonardo", "lincoln", "cleopatra",
            "tesla", "shakespeare", "mozart", "pocahontas"
        };

        public static bool TryResolve(string abilityID, out SpriteFrames frames,
            out StringName animation) {
            frames = null;
            animation = default;
            if (string.IsNullOrWhiteSpace(abilityID)) return false;

            int separator = abilityID.IndexOf('_');
            if (separator <= 0) return false;
            string characterID = abilityID[..separator];
            if (!RosterIDs.Contains(characterID)) return false;

            string path = $"res://resources/SpriteFrames/{characterID}_ability_vfx_frames.tres";
            if (!ResourceLoader.Exists(path)) return false;
            frames = ResourceLoader.Load<SpriteFrames>(path);
            animation = ResolveAnimationName(abilityID);
            return frames != null && frames.HasAnimation(animation);
        }

        public static bool Apply(AnimatedSprite2D sprite, string abilityID, float scale,
            bool flipH = false) {
            if (sprite == null || !TryResolve(abilityID, out SpriteFrames frames,
                    out StringName animation)) return false;
            sprite.SpriteFrames = frames;
            sprite.Animation = animation;
            sprite.Frame = 0;
            sprite.FlipH = flipH;
            sprite.Scale = new Vector2(scale, scale);
            sprite.Visible = true;
            sprite.Play();
            return true;
        }

        private static StringName ResolveAnimationName(string abilityID) => abilityID switch {
            // Preserve the four animation names already consumed by Einstein's
            // authored scenes and the shipped SpriteFrames resource.
            "einstein_mass_energy_conversion" => "mass_energy_projectile",
            "einstein_relativity_rift" => "relativity_rift",
            "einstein_relativity_warp" => "relativity_warp",
            "einstein_cosmological_constant" => "cosmological_constant",
            _ => abilityID
        };
    }
}
