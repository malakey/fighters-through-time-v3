using System;
using System.Collections.Generic;

namespace FTT.Core {

    /// <summary>
    /// V7.5 Mystery Thread support (Package 11 A5; plan §2.10 item 2). The
    /// campaign's absent legends — every roster character <b>except</b> the
    /// locked campaign hero — are the pool the Unbound's captives are drawn from,
    /// and dialogue lines refer to two of them by name through the
    /// <c>{CaptiveName1}</c> / <c>{CaptiveName2}</c> tokens
    /// <see cref="FTT.UI.DialogueManager"/> substitutes at render time.
    ///
    /// <para>The roster is read from
    /// <see cref="ContentManifest"/>'s <see cref="ContentCategory.Character"/>
    /// rows, <b>never</b> from a literal array: adding a tenth character must
    /// widen the captive pool with no code change (the A9 count-agnostic
    /// mandate). Only the <i>priority</i> of the two names the writers reach for
    /// first is authored here, and even that is intersected with the manifest —
    /// a priority entry the manifest does not carry is skipped rather than
    /// invented.</para>
    ///
    /// <para>Spoken names live in their own <c>captive_name_*</c> translation key
    /// family (A6 authors the rows) rather than reusing <c>character_*_name</c>,
    /// because a captive is named the way a character speaks of them —
    /// Leonardo is "Da Vinci" in dialogue but "Leonardo da Vinci" on the
    /// character select.</para>
    /// </summary>
    public static class CampaignCaptiveRoster {

        /// <summary>
        /// Authored order the writers' first two captive names are drawn in
        /// (design: Leonardo — spoken "Da Vinci" — then Cleopatra, then Tesla).
        /// Entries not present in the manifest are ignored, and the manifest's
        /// own order fills any remaining slot, so this is a preference, not a
        /// roster.
        /// </summary>
        public static readonly IReadOnlyList<string> NamedExamplePriority = new[] {
            "leonardo", "cleopatra", "tesla"
        };

        /// <summary>Number of named captives a line may reference: {CaptiveName1}, {CaptiveName2}.</summary>
        public const int NamedExampleCount = 2;

        /// <summary>
        /// Every character ID the content manifest registers, in manifest order.
        ///
        /// <para>Package 11 A6b: this used to carry its own manifest read and
        /// its own process-lifetime cache. It now forwards to
        /// <see cref="CharacterRoster.IDs"/> — the single canonical roster — so
        /// there is exactly one manifest read, one cache and one answer to
        /// "who is on the roster". The A9 mandate is unchanged; it is simply
        /// enforced in one place now.</para>
        /// </summary>
        public static IReadOnlyList<string> Roster() => CharacterRoster.IDs;

        /// <summary>Test seam: drops the cached manifest roster.</summary>
        internal static void ResetCacheForTests() => CharacterRoster.ResetCacheForTests();

        /// <summary>
        /// The captives for a campaign locked to <paramref name="heroID"/>:
        /// the manifest roster minus that hero, in manifest order. Always
        /// <c>rosterCount - 1</c> entries for a roster member, and the whole
        /// roster for an unknown or empty hero ID.
        /// </summary>
        public static IReadOnlyList<string> For(string heroID) => For(heroID, Roster());

        /// <summary>Pure overload — the same filter over an explicit roster.</summary>
        public static IReadOnlyList<string> For(string heroID, IEnumerable<string> roster) {
            var captives = new List<string>();
            if (roster == null) return captives;
            foreach (string id in roster) {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (string.Equals(id, heroID, StringComparison.Ordinal)) continue;
                if (!captives.Contains(id)) captives.Add(id);
            }
            return captives;
        }

        /// <summary>
        /// The two captive IDs a line's <c>{CaptiveName1}</c> / <c>{CaptiveName2}</c>
        /// tokens resolve to: the authored priority first, then manifest order.
        /// Both entries are always distinct while the roster holds at least three
        /// characters; a degenerate roster yields empty strings rather than
        /// repeating a name.
        /// </summary>
        public static (string First, string Second) NamedExamplesFor(string heroID) =>
            NamedExamplesFor(heroID, Roster());

        /// <summary>Pure overload of <see cref="NamedExamplesFor(string)"/>.</summary>
        public static (string First, string Second) NamedExamplesFor(string heroID, IEnumerable<string> roster) {
            var captives = new List<string>(For(heroID, roster));
            var ordered = new List<string>(NamedExampleCount);
            foreach (string preferred in NamedExamplePriority) {
                if (ordered.Count >= NamedExampleCount) break;
                if (captives.Contains(preferred) && !ordered.Contains(preferred)) ordered.Add(preferred);
            }
            foreach (string id in captives) {
                if (ordered.Count >= NamedExampleCount) break;
                if (!ordered.Contains(id)) ordered.Add(id);
            }
            return (
                ordered.Count > 0 ? ordered[0] : "",
                ordered.Count > 1 ? ordered[1] : "");
        }

        /// <summary>The translation key carrying a captive's spoken name.</summary>
        public static string SpokenNameKey(string characterID) =>
            string.IsNullOrWhiteSpace(characterID) ? "" : $"captive_name_{characterID}";
    }
}
