using System.Text;

namespace FTT.UI {

    /// <summary>
    /// Package 12 W7 (M35): the pseudo-locale the design's localization gate
    /// builds — every string grown by at least <see cref="ExpansionFactor"/>
    /// (+40 %, the EFIGS budget), its letters swapped for accented look-alikes,
    /// and the whole string bracketed so clipping is visible at a glance.
    ///
    /// <para>Placeholders survive untouched: <c>{0}</c>-style format items and
    /// named tokens such as <c>{CaptiveName1}</c> or <c>{HeroAddressName}</c> are
    /// copied verbatim, as are escape sequences (<c>\n</c>), so a pseudo string
    /// still formats and still substitutes exactly like the English one.</para>
    ///
    /// <para>Pure C#, no Godot types: the expansion-budget gate runs in the
    /// plain .NET host. The on-screen half of the gate (every screen and the
    /// HUD at 90–140 % UI Scale checked for clipping) is recorded as open in
    /// docs/handoffs/P12_W7.md.</para>
    /// </summary>
    public static class PseudoLocale {

        /// <summary>Minimum length growth over the English source (+40 %).</summary>
        public const float ExpansionFactor = 1.4f;

        public const char OpenBracket = '[';
        public const char CloseBracket = ']';

        /// <summary>Filler appended to reach the expansion budget.</summary>
        public const char PadCharacter = '~';

        private const string Plain = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string Accented = "àƀçđéƒĝĥíĵķĺɱñöþʠŕšţüṽŵẋýžÀßÇĐÉƑĜĤÍĴĶĹṀÑÖÞǪŔŠŢÜṼŴẊÝŽ";

        /// <summary>The pseudo-localized form of <paramref name="source"/>.</summary>
        public static string Transform(string source) {
            if (string.IsNullOrEmpty(source)) return source ?? "";
            var body = new StringBuilder(source.Length * 2);
            int i = 0;
            while (i < source.Length) {
                char c = source[i];
                if (c == '{') {
                    int close = source.IndexOf('}', i + 1);
                    if (close > i) {
                        body.Append(source, i, close - i + 1);
                        i = close + 1;
                        continue;
                    }
                }
                if (c == '\\' && i + 1 < source.Length) {
                    body.Append(source, i, 2);
                    i += 2;
                    continue;
                }
                int plain = Plain.IndexOf(c);
                body.Append(plain >= 0 ? Accented[plain] : c);
                i++;
            }
            int target = (int)System.MathF.Ceiling(source.Length * ExpansionFactor);
            // Two brackets count towards the budget; pad the remainder.
            int pad = target - (body.Length + 2);
            if (pad > 0) body.Append(PadCharacter, pad);
            return OpenBracket + body.ToString() + CloseBracket;
        }
    }
}
