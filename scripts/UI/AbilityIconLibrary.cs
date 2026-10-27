using FTT.Core;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-ART: the lookup for the 144 x 144 ability-slot icons the lenses draw. Each
    /// character's four slot icons are cropped from its retro v2 ability-VFX atlas by
    /// <c>tools/asset_src/glass/build_ability_icons.py</c> and live at
    /// <c>assets/sprites/ui/ability_icons/&lt;characterId&gt;_&lt;slot&gt;.png</c> (moved out of the pulp kit,
    /// with their .import uids, by the glass closeout), so a HUD or a select card never loads a whole
    /// 384 px-cell atlas.
    ///
    /// <para>The roster is never listed here: the caller passes any character ID
    /// (<see cref="CharacterRoster"/> is the source of the IDs) and an ID with no icon
    /// yields <c>null</c>, which a caller draws as an empty slot frame. Nothing is cached:
    /// textures are ordinary scene resources the engine already shares through its resource
    /// cache.</para>
    /// </summary>
    public static class AbilityIconLibrary {

        /// <summary>Folder of the baked icons.</summary>
        public const string Folder = "res://assets/sprites/ui/ability_icons/";

        /// <summary>Edge length of every icon in pixels.</summary>
        public const int IconSize = 144;

        /// <summary>The file-name suffix of a slot (the same names <c>resources/Abilities/&lt;id&gt;/</c> uses).</summary>
        public static string SlotName(AbilitySlot slot) {
            switch (slot) {
                case AbilitySlot.Special1: return "special_1";
                case AbilitySlot.Special2: return "special_2";
                case AbilitySlot.MovementAbility: return "movement";
                default: return "ultimate";
            }
        }

        /// <summary>The resource path of a character's slot icon (the file may not exist).</summary>
        public static string PathFor(string characterId, AbilitySlot slot) {
            return Folder + characterId + "_" + SlotName(slot) + ".png";
        }

        /// <summary>The slot icon, or <c>null</c> when the character or file is unknown.</summary>
        public static Texture2D Load(string characterId, AbilitySlot slot) {
            if (string.IsNullOrEmpty(characterId)) {
                return null;
            }
            string path = PathFor(characterId, slot);
            return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        }
    }
}
