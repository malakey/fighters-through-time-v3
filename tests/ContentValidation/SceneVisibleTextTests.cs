using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 C1: the authored-scene half of the localization sweep.
///
/// <para><b>Why this suite exists.</b> A1 established the pattern the whole package
/// follows — visible copy in an authored <c>.tscn</c> is stored as a raw translation
/// key and resolved by Godot's automatic control translation, so a language change
/// follows without rebuilding the surface. That pattern has exactly one failure mode,
/// and it is silent: type the English sentence into the inspector instead of the key.
/// The scene loads, the screen looks right in English, and the string is unreachable by
/// every translation gate in the repository forever. B4 found and fixed the two
/// offenders that existed (<c>Player.tscn</c>'s "Player" debug label and
/// <c>scenes/TestScene.tscn</c>); this proves the tree stays clean now that Package 8
/// has authored a dozen new screens.</para>
///
/// <para><b>The check is stronger than "looks like a key".</b> A lowercase_underscore
/// shape alone would accept a plausible-looking typo, which is the likeliest way this
/// actually breaks. Every key-shaped value must also exist in <c>localization/en.csv</c>
/// <i>and</i> resolve through the compiled <c>en.en.translation</c> — the same
/// compiled-resource doctrine <see cref="CampaignLocalizationTests"/> documents, since
/// automatic control translation reads the compiled resource exactly as
/// <c>Node.Tr()</c> does.</para>
///
/// <para><b>What is allowed not to be a key.</b> An empty string (a label whose content
/// is written at runtime — the debug HUD's state/tick/hash lines across the eleven
/// arena scenes are all of these), and a value containing no letters at all: glyphs and
/// numerals such as the character-select opponent arrows carry across every language
/// this project plans to ship and have nothing to translate. Anything else needs an
/// explicit entry in <see cref="AllowedLiterals"/> with a reason.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SceneVisibleTextTests {
    private const string SceneRoot = "scenes";
    private const int MinimumTextProperties = 90;

    /// <summary>
    /// Exact <c>scene path|text value</c> pairs that are deliberately raw literals.
    /// Empty strings and letter-free glyph strings are handled by rule and do not
    /// belong here.
    /// </summary>
    private static readonly HashSet<string> AllowedLiterals = new(StringComparer.Ordinal);

    private static readonly Regex TextProperty = new(@"^text = ""(.*)""\s*$", RegexOptions.Compiled);
    private static readonly Regex TranslationKeyShape = new(@"^[a-z0-9]+(_[a-z0-9]+)*$", RegexOptions.Compiled);

    [TestCase]
    public void EveryAuthoredVisibleTextIsATranslationKeyOrAnAllowedLiteral() {
        TranslationServer.SetLocale("en");
        HashSet<string> csvKeys = EnglishTranslationKeys();

        var rawEnglish = new List<string>();
        var unknownKeys = new List<string>();
        var unresolved = new List<string>();
        int total = 0;
        int keyShaped = 0;

        foreach ((string scene, int line, string value) in AuthoredTextProperties()) {
            total++;
            string where = $"{scene}:{line} text = \"{value}\"";

            if (value.Length == 0) continue;                              // filled at runtime
            if (!value.Any(char.IsLetter)) continue;                      // glyphs / numerals
            if (AllowedLiterals.Contains($"{scene}|{value}")) continue;   // documented exception

            if (!TranslationKeyShape.IsMatch(value)) { rawEnglish.Add(where); continue; }

            keyShaped++;
            if (!csvKeys.Contains(value)) unknownKeys.Add(where);
            else if (TranslationServer.Translate(value).ToString() == value) unresolved.Add(where);
        }

        AssertThat(total).OverrideFailureMessage(
            $"Only {total} `text =` properties were found under {SceneRoot}/; expected at least " +
            $"{MinimumTextProperties}. The scan is broken, not the content.")
            .IsGreaterEqual(MinimumTextProperties);
        AssertThat(keyShaped).OverrideFailureMessage(
            $"Only {keyShaped} translation-key-shaped values were checked; the shape filter is " +
            "rejecting everything and the suite proves nothing.")
            .IsGreaterEqual(60);

        AssertThat(rawEnglish.Count).OverrideFailureMessage(
            "Authored scenes carry visible text that is not a translation key. Replace the " +
            "literal with a lowercase_underscore key and add it to localization/en.csv - Godot's " +
            "automatic control translation resolves it. Offenders: " + string.Join("; ", rawEnglish))
            .IsEqual(0);

        AssertThat(unknownKeys.Count).OverrideFailureMessage(
            "Authored scenes reference translation keys that are not in localization/en.csv " +
            "(they render as the raw key in game): " + string.Join("; ", unknownKeys))
            .IsEqual(0);

        AssertThat(unresolved.Count).OverrideFailureMessage(
            "Authored scenes reference keys present in localization/en.csv but missing from the " +
            "compiled localization/en.en.translation - the compiled resource is stale. Run " +
            "`--headless --import` and commit the regenerated artifact. Offenders: " +
            string.Join("; ", unresolved))
            .IsEqual(0);
    }

    /// <summary>
    /// An allowlist entry pointing at a scene or a value that no longer exists is a
    /// suppression waiting to cover the next real offender.
    /// </summary>
    [TestCase]
    public void EveryAllowlistEntryStillMatchesSomethingInTheSceneTree() {
        if (AllowedLiterals.Count == 0) {
            AssertThat(true).IsTrue(); // Nothing allowlisted today - both rules cover the tree.
            return;
        }

        var live = new HashSet<string>(
            AuthoredTextProperties().Select(entry => $"{entry.Scene}|{entry.Value}"),
            StringComparer.Ordinal);
        string[] stale = AllowedLiterals.Where(entry => !live.Contains(entry)).ToArray();

        AssertThat(stale.Length).OverrideFailureMessage(
            "These SceneVisibleTextTests.AllowedLiterals entries no longer match any authored " +
            "scene text and must be removed: " + string.Join(", ", stale))
            .IsEqual(0);
    }

    private static IEnumerable<(string Scene, int Line, string Value)> AuthoredTextProperties() {
        foreach (string path in Directory.EnumerateFiles(SceneRoot, "*.tscn", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal)) {
            string relative = path.Replace('\\', '/');
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++) {
                Match match = TextProperty.Match(lines[index]);
                if (match.Success) yield return (relative, index + 1, match.Groups[1].Value);
            }
        }
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
