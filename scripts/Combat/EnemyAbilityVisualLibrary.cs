using System;
using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.Combat {

    /// <summary>
    /// Resolves the production retro enemy and boss VFX atlases from authored
    /// presentation event IDs. Gameplay resources keep their tuning paths; this
    /// presentation-only lookup remains best-effort so missing art never blocks an
    /// attack from executing.
    /// </summary>
    public static class EnemyAbilityVisualLibrary {
        private static readonly HashSet<string> EnemyIDs = new(StringComparer.Ordinal) {
            "chrono_chariot_raider", "chrono_guard_elite", "chrono_rioter",
            "chrono_slasher", "cyber_cavalry_commander", "cyber_centurion",
            "cyber_guard", "holo_page", "hologram_drone",
            "infrared_border_sentry", "kinetic_royal_guard", "laser_archer",
            "laser_pistol_deckhand", "laser_rifle_infantry",
            "neural_linked_knight", "neural_mech_walker",
            "overcharged_cannon_master", "plasma_sabre_captain",
            "plasma_spear_ward", "rift_phantom", "shock_shield_legionnaire",
            "steam_automaton", "tech_enforcer", "tesla_exo_baron",
            "vacuum_digger", "void_enforcer", "voltaic_shock_drone"
        };

        private static readonly HashSet<string> BossIDs = new(StringComparer.Ordinal) {
            "apex_eraser", "archive_prime", "borgia_inquisitor",
            "chronal_inventor", "dread_admiral", "gravity_overseer",
            "iron_chancellor", "jackal_priest", "revolutionary_tribunal",
            "siege_cannon", "siegemaster_duke", "tidal_eraser",
            "tragedy_king", "vulcan_decimator"
        };

        public static bool TryResolve(string presentationEventID, out SpriteFrames frames,
            out StringName animation, out bool isBoss) {
            frames = null;
            animation = default;
            isBoss = false;
            if (!TryParseOwner(presentationEventID, out string ownerID, out isBoss)) return false;

            string category = isBoss ? "Bosses" : "Enemies";
            string path = $"res://resources/SpriteFrames/{category}/{ownerID}_ability_vfx_frames.tres";
            if (!ResourceLoader.Exists(path)) return false;
            frames = ResourceLoader.Load<SpriteFrames>(path);
            animation = presentationEventID;
            return frames != null && frames.HasAnimation(animation);
        }

        public static bool IsAuthoredOwner(string presentationEventID) =>
            TryParseOwner(presentationEventID, out _, out _);

        public static bool Apply(AnimatedSprite2D sprite, string presentationEventID,
            float scale, bool flipH = false) {
            if (sprite == null || !TryResolve(presentationEventID, out SpriteFrames frames,
                    out StringName animation, out _)) return false;
            sprite.SpriteFrames = frames;
            sprite.Animation = animation;
            sprite.Frame = 0;
            sprite.FlipH = flipH;
            sprite.Scale = new Vector2(scale, scale);
            sprite.Visible = true;
            sprite.Play();
            return true;
        }

        private static bool TryParseOwner(string presentationEventID, out string ownerID,
            out bool isBoss) {
            ownerID = "";
            isBoss = false;
            if (string.IsNullOrWhiteSpace(presentationEventID)) return false;

            string[] segments = presentationEventID.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3 && segments[0] == "enemy") {
                ownerID = segments[1];
                return EnemyIDs.Contains(ownerID);
            }
            if (segments.Length >= 3 && segments[0] == "boss") {
                ownerID = segments[1];
                isBoss = true;
                return BossIDs.Contains(ownerID);
            }

            // Standard mobs without a PrimaryAttack synthesize "{enemyID}.basic".
            if (segments.Length >= 2 && EnemyIDs.Contains(segments[0])) {
                ownerID = segments[0];
                return true;
            }
            return false;
        }
    }
}
