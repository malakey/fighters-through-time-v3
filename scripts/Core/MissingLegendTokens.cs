using System;
using System.Collections.Generic;

namespace FTT.Core {

    /// <summary>
    /// Package 13 W4 (design §16 M14 / S29): the two Mystery Thread list tokens —
    /// the lines where the hero or Sarah enumerates the legends found missing
    /// <i>so far</i>.
    ///
    /// <list type="bullet">
    /// <item><c>{MissingSoFar}</c> — Level 3's exit: the non-active legends whose
    /// absence the player has already seen ("Da Vinci, Joan"; Leonardo active
    /// "Joan"; Joan active "Da Vinci").</item>
    /// <item><c>{MissingPlaces}</c> — Level 8's post-boss: the same absences as
    /// places ("Florence. Like Orléans. Like Chicago"), dropping the active hero's
    /// home (Leonardo: Florence; Joan: Orléans; Tesla: Chicago).</item>
    /// </list>
    ///
    /// <para><b>Per-hero rows with a fallback, not per-hero sequence variants.</b>
    /// Every form lives in <c>localization/en.csv</c> as
    /// <c>missing_so_far_&lt;heroID&gt;</c> / <c>missing_places_&lt;heroID&gt;</c>;
    /// only a hero whose own absence the list would name carries a row, and every
    /// other hero — including a roster addition — reads the
    /// <c>_fallback</c> row. That keeps M14 ("the active hero is never named as
    /// missing") a one-row edit per hero instead of a duplicated sequence, and it
    /// retired the four Package 12 <c>__leonardo</c>/<c>__joan</c> sequence
    /// variants of Levels 3 and 8. A missing fallback row falls back to a fixed
    /// English list so a raw key or token never reaches the screen.</para>
    ///
    /// <para>Kept out of <see cref="DialogueTokens"/> deliberately: a sibling
    /// Package 13 workstream extends that class concurrently. The render-time pass
    /// (<c>FTT.UI.DialogueManager.SubstituteCaptiveNames</c>) calls both.</para>
    ///
    /// <para>Pure C#: the translator is injected.</para>
    /// </summary>
    public static class MissingLegendTokens {

        public const string MissingSoFar = "{MissingSoFar}";
        public const string MissingPlaces = "{MissingPlaces}";

        /// <summary>Every token this pass resolves, in resolution order.</summary>
        public static readonly IReadOnlyList<string> Tokens = new[] { MissingSoFar, MissingPlaces };

        /// <summary>The row a hero without its own entry resolves to.</summary>
        public const string FallbackHeroID = "fallback";

        public static string MissingSoFarKey(string heroID) => $"missing_so_far_{heroID}";
        public static string MissingPlacesKey(string heroID) => $"missing_places_{heroID}";

        /// <summary>Last-resort English when even the fallback row is missing.</summary>
        public const string MissingSoFarLastResort = "Da Vinci, Joan";
        public const string MissingPlacesLastResort = "Florence. Like Orléans. Like Chicago";

        /// <summary>True when the text carries either token.</summary>
        public static bool HasToken(string text) {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (string token in Tokens) {
                if (text.Contains(token, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// Replaces both tokens for <paramref name="heroID"/>.
        /// <paramref name="translate"/> must return its argument unchanged for an
        /// unknown key (the <c>TranslationServer.Translate</c> contract). A line with
        /// no token is returned untouched.
        /// </summary>
        public static string Substitute(string text, string heroID, Func<string, string> translate) {
            if (!HasToken(text)) return text;
            translate ??= key => key;
            if (text.Contains(MissingSoFar, StringComparison.Ordinal))
                text = text.Replace(MissingSoFar, Resolve(MissingSoFarKey, heroID, translate, MissingSoFarLastResort));
            if (text.Contains(MissingPlaces, StringComparison.Ordinal))
                text = text.Replace(MissingPlaces, Resolve(MissingPlacesKey, heroID, translate, MissingPlacesLastResort));
            return text;
        }

        private static string Resolve(Func<string, string> keyFor, string heroID, Func<string, string> translate,
                                      string lastResort) {
            if (!string.IsNullOrWhiteSpace(heroID)) {
                string key = keyFor(heroID);
                string value = translate(key);
                if (!string.IsNullOrEmpty(value) && value != key) return value;
            }
            string fallbackKey = keyFor(FallbackHeroID);
            string fallback = translate(fallbackKey);
            return !string.IsNullOrEmpty(fallback) && fallback != fallbackKey ? fallback : lastResort;
        }
    }
}
