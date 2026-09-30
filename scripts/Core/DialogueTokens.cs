using System;
using System.Collections.Generic;

namespace FTT.Core {

    /// <summary>
    /// Package 12 W7 (M35): the hero-name tokens a dialogue line may carry,
    /// resolved at render time alongside <c>{CaptiveName1}</c> /
    /// <c>{CaptiveName2}</c> (<see cref="CampaignCaptiveRoster"/>).
    ///
    /// <list type="bullet">
    /// <item><c>{HeroName}</c> — the locked hero as narration names them ("Da Vinci").</item>
    /// <item><c>{HeroAddressName}</c> — how the Wardens address them ("Maestro", "Mr. President").</item>
    /// <item><c>{HeroPossessiveName}</c> — the possessive form ("Da Vinci's").</item>
    /// <item><c>{HomeEraName}</c> — the hero's own home era ("Florence", "Princeton").</item>
    /// <item><c>{HeroAcceptLine}</c> — Package 13 W3 (S07): the hero's whole Level 0
    /// acceptance line, spoken after Sarah says they cannot go home.</item>
    /// <item><c>{HeroFarewellLine}</c> — Package 13 W3 (S48): the hero's whole
    /// farewell in the Time-Ship send-off, answering the acceptance line.</item>
    /// </list>
    ///
    /// <para>The two line tokens are whole-line families: a sequence line whose
    /// English is exactly the token resolves to that hero's row. They follow the
    /// same fallback chain as the name forms, so a roster addition without its
    /// rows speaks the <c>_fallback</c> line rather than a raw token.</para>
    ///
    /// <para>Every form lives in <c>localization/en.csv</c> as a
    /// <c>hero_&lt;form&gt;_&lt;heroID&gt;</c> row — the nine-hero name table — so
    /// a translator owns the grammar, and a tenth roster character needs four
    /// CSV rows and no code. The rows are keyed by the <b>saved</b> hero ID,
    /// never by a localized name. A hero with no row falls back to the
    /// <c>hero_&lt;form&gt;_fallback</c> row, and a missing fallback to a fixed
    /// English word, so a raw key or a raw token never reaches the screen.</para>
    ///
    /// <para>Pure C#: the translator is injected, so the substitution is
    /// provable without the engine. The runtime pass is
    /// <c>FTT.UI.DialogueManager.SubstituteCaptiveNames</c>.</para>
    /// </summary>
    public static class DialogueTokens {

        public const string HeroName = "{HeroName}";
        public const string HeroAddressName = "{HeroAddressName}";
        public const string HeroPossessiveName = "{HeroPossessiveName}";
        public const string HomeEraName = "{HomeEraName}";
        public const string HeroAcceptLine = "{HeroAcceptLine}";
        public const string HeroFarewellLine = "{HeroFarewellLine}";

        /// <summary>Every hero token, in the order the pass resolves them.</summary>
        public static readonly IReadOnlyList<string> HeroTokens = new[] {
            HeroName, HeroAddressName, HeroPossessiveName, HomeEraName, HeroAcceptLine, HeroFarewellLine
        };

        /// <summary>The row a hero without a table entry resolves to.</summary>
        public const string FallbackHeroID = "fallback";

        public static string NameKey(string heroID) => $"hero_name_{heroID}";
        public static string AddressKey(string heroID) => $"hero_address_{heroID}";
        public static string PossessiveKey(string heroID) => $"hero_possessive_{heroID}";
        public static string HomeEraKey(string heroID) => $"hero_home_era_{heroID}";
        public static string AcceptLineKey(string heroID) => $"hero_accept_line_{heroID}";
        public static string FarewellLineKey(string heroID) => $"hero_farewell_line_{heroID}";

        /// <summary>The six table keys for one hero, in <see cref="HeroTokens"/> order.</summary>
        public static string[] TableKeysFor(string heroID) => new[] {
            NameKey(heroID), AddressKey(heroID), PossessiveKey(heroID), HomeEraKey(heroID),
            AcceptLineKey(heroID), FarewellLineKey(heroID)
        };

        /// <summary>True when the text carries any hero token.</summary>
        public static bool HasHeroToken(string text) {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (string token in HeroTokens) {
                if (text.Contains(token, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// Replaces every hero token in <paramref name="text"/> for
        /// <paramref name="heroID"/>. <paramref name="translate"/> must return
        /// its argument unchanged for a key it does not know (Godot's
        /// <c>TranslationServer.Translate</c> contract). A line with no token is
        /// returned untouched.
        /// </summary>
        public static string SubstituteHeroTokens(string text, string heroID, Func<string, string> translate) {
            if (!HasHeroToken(text)) return text;
            translate ??= key => key;
            if (text.Contains(HeroName, StringComparison.Ordinal))
                text = text.Replace(HeroName, Resolve(NameKey, heroID, translate, "the traveler"));
            if (text.Contains(HeroAddressName, StringComparison.Ordinal))
                text = text.Replace(HeroAddressName, Resolve(AddressKey, heroID, translate, "traveler"));
            if (text.Contains(HeroPossessiveName, StringComparison.Ordinal))
                text = text.Replace(HeroPossessiveName, Resolve(PossessiveKey, heroID, translate, "the traveler's"));
            if (text.Contains(HomeEraName, StringComparison.Ordinal))
                text = text.Replace(HomeEraName, Resolve(HomeEraKey, heroID, translate, "Home"));
            if (text.Contains(HeroAcceptLine, StringComparison.Ordinal))
                text = text.Replace(HeroAcceptLine,
                    Resolve(AcceptLineKey, heroID, translate, "Then I will fight for it."));
            if (text.Contains(HeroFarewellLine, StringComparison.Ordinal))
                text = text.Replace(HeroFarewellLine,
                    Resolve(FarewellLineKey, heroID, translate, "Take me home."));
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
