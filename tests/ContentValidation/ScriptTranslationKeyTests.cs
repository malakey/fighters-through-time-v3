using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 C1: the source-side half of the localization sweep.
///
/// <para><b>Why this suite exists.</b> <see cref="CampaignLocalizationTests"/> walks
/// authored <i>resources</i> and the CSV's campaign key families. Nothing walked the
/// C# source, so a screen that called <c>Tr("some_key_i_invented")</c> shipped a raw
/// key on screen and every existing gate stayed green: the key is in no resource, so
/// the resource sweep never sees it, and it is in no CSV family, so the family sweep
/// never sees it either. Package 8 added roughly a hundred such call sites across
/// menus, settings, HUDs and pause screens at once, which is exactly the scale at
/// which one typo goes unnoticed.</para>
///
/// <para>Both assertions resolve through <see cref="TranslationServer.Translate(string)"/>
/// rather than stopping at CSV membership, for the reason
/// <see cref="CampaignLocalizationTests"/> documents at length: <c>Node.Tr()</c> reads
/// the compiled <c>localization/en.en.translation</c>, and
/// <c>--headless --quit</c> does not regenerate it — only <c>--headless --import</c>
/// does. A CSV that is ahead of the compiled resource looks perfect in review and
/// renders raw keys in game.</para>
///
/// <para><b>The exclusion list is empty, deliberately.</b> Every one of the literals
/// this suite finds today is a real translation key. If a future call site legitimately
/// passes a non-key literal to <c>Tr</c> — an already-resolved sentence, say — add it to
/// <see cref="AllowedNonKeyLiterals"/> <i>with the reason</i>. The second test case
/// refuses an entry that no longer appears in the source, so the list cannot rot into a
/// blanket suppression.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScriptTranslationKeyTests {
    private const string ScriptRoot = "scripts";

    /// <summary>Minimum literal count. A regex that stopped matching (a refactor onto a
    /// helper, say) would otherwise turn this whole suite into a no-op that still passes.</summary>
    private const int MinimumTrLiterals = 80;
    private const int MinimumTranslateLiterals = 8;

    /// <summary>
    /// Literals that are deliberately not translation keys. Each entry must still appear
    /// in the source; see the class remarks.
    /// </summary>
    private static readonly HashSet<string> AllowedNonKeyLiterals = new();

    // Tr("literal") — the Godot Node/Control helper, by far the most common call shape.
    private static readonly Regex TrLiteral = new(@"\bTr\(\s*""([^""]*)""", RegexOptions.Compiled);

    // TranslationServer.Translate("literal") — used where there is no Node in scope.
    private static readonly Regex TranslateLiteral =
        new(@"TranslationServer\.Translate\(\s*""([^""]*)""", RegexOptions.Compiled);

    [TestCase]
    public void EveryTrLiteralInScriptsResolvesThroughTheCompiledTranslation() {
        AssertLiteralsResolve(TrLiteral, "Tr(\"...\")", MinimumTrLiterals);
    }

    [TestCase]
    public void EveryTranslationServerLiteralInScriptsResolvesThroughTheCompiledTranslation() {
        AssertLiteralsResolve(TranslateLiteral, "TranslationServer.Translate(\"...\")", MinimumTranslateLiterals);
    }

    /// <summary>
    /// An exclusion that no longer appears in the source is a suppression with nothing
    /// left to suppress; the next real offender would inherit it silently.
    /// </summary>
    [TestCase]
    public void EveryDocumentedExclusionStillAppearsInTheSource() {
        if (AllowedNonKeyLiterals.Count == 0) {
            AssertThat(true).IsTrue(); // Nothing excluded today - see the class remarks.
            return;
        }

        Dictionary<string, List<string>> trLiterals = CollectLiterals(TrLiteral);
        Dictionary<string, List<string>> translateLiterals = CollectLiterals(TranslateLiteral);

        var stale = AllowedNonKeyLiterals
            .Where(entry => !trLiterals.ContainsKey(entry) && !translateLiterals.ContainsKey(entry))
            .ToArray();

        AssertThat(stale.Length).OverrideFailureMessage(
            "These entries in ScriptTranslationKeyTests.AllowedNonKeyLiterals no longer appear " +
            "in scripts/ and must be removed: " + string.Join(", ", stale))
            .IsEqual(0);
    }

    private static void AssertLiteralsResolve(Regex pattern, string label, int minimum) {
        TranslationServer.SetLocale("en");
        HashSet<string> csvKeys = EnglishTranslationKeys();
        Dictionary<string, List<string>> literals = CollectLiterals(pattern);

        AssertThat(literals.Count).OverrideFailureMessage(
            $"Only {literals.Count} distinct {label} literals were found under {ScriptRoot}/; " +
            $"expected at least {minimum}. The scan is broken, not the content.")
            .IsGreaterEqual(minimum);

        var missingFromCsv = new List<string>();
        var unresolved = new List<string>();

        foreach ((string literal, List<string> sites) in literals) {
            if (AllowedNonKeyLiterals.Contains(literal)) continue;
            string where = $"'{literal}' ({sites[0]}{(sites.Count > 1 ? $" +{sites.Count - 1} more" : "")})";

            if (string.IsNullOrWhiteSpace(literal)) { missingFromCsv.Add(where + " (empty literal)"); continue; }
            if (!csvKeys.Contains(literal)) missingFromCsv.Add(where);
            else if (TranslationServer.Translate(literal).ToString() == literal) unresolved.Add(where);
        }

        AssertThat(missingFromCsv.Count).OverrideFailureMessage(
            $"{label} literals with no key in localization/en.csv (they render as the raw key " +
            "in game). Add the key, or document the literal in " +
            "ScriptTranslationKeyTests.AllowedNonKeyLiterals: " + string.Join(", ", missingFromCsv))
            .IsEqual(0);

        AssertThat(unresolved.Count).OverrideFailureMessage(
            $"{label} literals present in localization/en.csv but not in the compiled " +
            "localization/en.en.translation - the compiled resource is stale. Run " +
            "`--headless --import` and commit the regenerated en.en.translation. Literals: " +
            string.Join(", ", unresolved))
            .IsEqual(0);
    }

    private static Dictionary<string, List<string>> CollectLiterals(Regex pattern) {
        var literals = new Dictionary<string, List<string>>();
        foreach (string path in Directory.EnumerateFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories)) {
            string[] lines = File.ReadAllLines(path);
            string relative = path.Replace('\\', '/');
            for (int index = 0; index < lines.Length; index++) {
                foreach (Match match in pattern.Matches(lines[index])) {
                    string literal = match.Groups[1].Value;
                    if (!literals.TryGetValue(literal, out List<string> sites)) {
                        sites = new List<string>();
                        literals[literal] = sites;
                    }
                    sites.Add($"{relative}:{index + 1}");
                }
            }
        }
        return literals;
    }

    private static HashSet<string> EnglishTranslationKeys() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            if (line.StartsWith('#')) continue;
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        return keys;
    }
}
