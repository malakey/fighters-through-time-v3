using System;
using System.Collections.Generic;

namespace FTT.Core {

    /// <summary>
    /// Package 11 A6b — the single canonical runtime roster (recon A §9, the
    /// A9 mandate).
    ///
    /// <para>design-godot.md §2 carries a standing mandate: <i>"the roster will
    /// grow — narrative and spec text must never hardcode roster size or
    /// enumerate the cast in load-bearing ways."</i> Before this class existed,
    /// five identical nine-element literal arrays drove the story character
    /// grid, the Fighter select tiles, the Holodeck CPU list, the persisted
    /// unlock set and the ability-VFX resolution gate, and a dozen tests
    /// asserted the literal number 9. Adding a tenth character meant editing
    /// all of them, and forgetting one produced a character that existed in
    /// content but could not be selected.</para>
    ///
    /// <para>The data side was already correct:
    /// <c>resources/Content/content_manifest.csv</c>'s
    /// <see cref="ContentCategory.Character"/> rows are the authoritative
    /// roster. This class is the seam that makes them the <i>only</i> roster —
    /// every production consumer reads <see cref="IDs"/>, so a new manifest row
    /// flows through the whole UI, save and VFX surface with no code change.</para>
    ///
    /// <para>Cached for the process lifetime like
    /// <see cref="AuthoredResources"/>, because the manifest is immutable
    /// content. The cache is deliberately fault-tolerant: a missing or
    /// malformed manifest yields an empty roster and a warning rather than
    /// taking the main menu down.</para>
    ///
    /// <para><b>The one documented exception</b> is
    /// <c>FTT.FighterSim.FighterCharacterID</c>. That enum is the deterministic
    /// simulation's character identity: its ordinals serialize into snapshots
    /// and the network protocol, and <c>FighterEntitySystems</c> encodes
    /// <c>zoneTypeID = (int)id * 10 + slot</c> in roughly twenty places. It is
    /// <b>append-only</b> — a new member takes the next ordinal, no member is
    /// ever reordered, renumbered or reused, and the <c>id * 10 + slot</c>
    /// product must stay unique. Adding a tenth character is therefore a
    /// protocol-affecting change. See the plan's §9 <c>DEFER-ROSTER-ENUM</c>
    /// entry; de-enumerating it was deliberately out of scope for Package 11.</para>
    /// </summary>
    public static class CharacterRoster {

        private static IReadOnlyList<string> _ids;
        private static HashSet<string> _lookup;

        /// <summary>
        /// Every character ID the content manifest registers, in manifest order
        /// — which is the authored grid order the character select paints.
        /// </summary>
        public static IReadOnlyList<string> IDs {
            get {
                EnsureLoaded();
                return _ids;
            }
        }

        /// <summary>How many characters the roster currently holds.</summary>
        public static int Count => IDs.Count;

        /// <summary>True when <paramref name="characterID"/> is a roster member.</summary>
        public static bool Contains(string characterID) {
            if (string.IsNullOrWhiteSpace(characterID)) return false;
            EnsureLoaded();
            return _lookup.Contains(characterID);
        }

        /// <summary>
        /// The roster ID at <paramref name="index"/>, or an empty string when the
        /// index falls outside the roster. Selection grids index straight into
        /// the roster, so an out-of-range cursor must not throw.
        /// </summary>
        public static string At(int index) {
            EnsureLoaded();
            return index >= 0 && index < _ids.Count ? _ids[index] : "";
        }

        /// <summary>
        /// The roster's index for <paramref name="characterID"/>, or -1. Used by
        /// the select screens to place a cursor on a restored session pick.
        /// </summary>
        public static int IndexOf(string characterID) {
            if (string.IsNullOrWhiteSpace(characterID)) return -1;
            EnsureLoaded();
            for (int index = 0; index < _ids.Count; index++) {
                if (string.Equals(_ids[index], characterID, StringComparison.Ordinal)) return index;
            }
            return -1;
        }

        /// <summary>A fresh mutable copy — for save payload defaults.</summary>
        public static List<string> ToList() => new(IDs);

        /// <summary>
        /// A fresh array copy, for the select screens that index and
        /// <c>Array.IndexOf</c> their way around the roster. A copy rather than
        /// a shared instance so a UI screen can never mutate the canonical list.
        /// </summary>
        public static string[] ToArray() {
            IReadOnlyList<string> ids = IDs;
            var copy = new string[ids.Count];
            for (int index = 0; index < ids.Count; index++) copy[index] = ids[index];
            return copy;
        }

        /// <summary>Test seam: drops the cached manifest roster.</summary>
        internal static void ResetCacheForTests() {
            _ids = null;
            _lookup = null;
        }

        private static void EnsureLoaded() {
            if (_ids != null) return;
            var ids = new List<string>();
            try {
                ContentManifest manifest = ContentManifest.LoadDefault();
                foreach (ContentManifestEntry entry in manifest.ForCategory(ContentCategory.Character)) {
                    if (string.IsNullOrWhiteSpace(entry.ContentID)) continue;
                    if (ids.Contains(entry.ContentID)) continue;
                    ids.Add(entry.ContentID);
                }
            } catch (Exception error) {
                // A missing or malformed manifest must not take the menus down;
                // the consumers all degrade to "no characters" rather than
                // throwing out of a _Ready.
                Godot.GD.PushWarning($"CharacterRoster could not read the content manifest: {error.Message}");
            }
            _ids = ids;
            _lookup = new HashSet<string>(ids, StringComparer.Ordinal);
        }
    }
}
