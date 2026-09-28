using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W7 (M35, design §13 "Pseudo-locale gate"): the automated gates build
/// a pseudo-locale — +40 % string length, accented and bracketed glyphs — so the
/// EFIGS expansion budget is exercised before any translator is paid.
///
/// <para>This suite is the <b>expansion-budget</b> half of that gate: every row of
/// <c>localization/en.csv</c> is pseudo-localized and must grow by the budget, stay
/// bracketed, and keep every placeholder (<c>{0}</c>, <c>{CaptiveName1}</c>,
/// <c>{HeroAddressName}</c>, <c>\n</c>) byte-identical and in order — a pseudo
/// string that breaks a format item would crash <c>string.Format</c> at runtime in
/// a real locale too. The on-screen half (every screen, and the HUD at 90–140 %
/// UI Scale, checked for clipping and overlap) needs a scene-sweep harness and is
/// recorded as open in <c>docs/handoffs/P12_W7.md</c>.</para>
///
/// <para>Pure C#: no engine calls. One <c>[TestSuite]</c> per file.</para>
/// </summary>
[TestSuite]
public class PseudoLocaleExpansionTests {

    private static readonly Regex Placeholder = new(@"\{[^{}]*\}|\\.", RegexOptions.Compiled);

    [TestCase]
    public void TheTransformGrowsByTheBudgetBracketsAndAccentsWithoutTouchingPlaceholders() {
        const string source = "Thank you, {HeroAddressName}. Deposited {0} dust.\\nGo.";
        string pseudo = PseudoLocale.Transform(source);

        AssertThat(pseudo.Length).IsGreaterEqual((int)Math.Ceiling(source.Length * PseudoLocale.ExpansionFactor));
        AssertString(pseudo[..1]).IsEqual(PseudoLocale.OpenBracket.ToString());
        AssertString(pseudo[^1..]).IsEqual(PseudoLocale.CloseBracket.ToString());
        AssertString(pseudo).Contains("{HeroAddressName}");
        AssertString(pseudo).Contains("{0}");
        AssertString(pseudo).Contains("\\n");
        AssertThat(pseudo.Contains("Thank", StringComparison.Ordinal))
            .OverrideFailureMessage("Letters outside placeholders must be accented.").IsFalse();
        AssertString(PseudoLocale.Transform("")).IsEqual("");
    }

    [TestCase]
    public void EveryEnglishRowSurvivesThePseudoLocaleWithinTheExpansionBudget() {
        var issues = new List<string>();
        int rows = 0;
        foreach ((string key, string english) in EnglishRows()) {
            if (english.Length == 0) continue;
            rows++;
            string pseudo = PseudoLocale.Transform(english);
            int budget = (int)Math.Ceiling(english.Length * PseudoLocale.ExpansionFactor);
            if (pseudo.Length < budget) issues.Add($"{key}: {pseudo.Length} < {budget}");
            string before = string.Join("|", Placeholders(english));
            string after = string.Join("|", Placeholders(pseudo[1..^1]));
            if (before != after) issues.Add($"{key}: placeholders changed ({before} -> {after})");
        }
        AssertThat(rows).OverrideFailureMessage($"Only {rows} rows parsed; the reader is broken.")
            .IsGreaterEqual(900);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    private static IEnumerable<string> Placeholders(string text) {
        foreach (Match match in Placeholder.Matches(text)) yield return match.Value;
    }

    private static IEnumerable<(string Key, string English)> EnglishRows() {
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            if (line.Length == 0 || line[0] == '#') continue;
            int comma = line.IndexOf(',');
            if (comma <= 0) continue;
            string key = line[..comma];
            if (key == "key") continue;
            string value = line[(comma + 1)..];
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') {
                value = value[1..^1].Replace("\"\"", "\"");
            }
            yield return (key, value);
        }
    }
}
