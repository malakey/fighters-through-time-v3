using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W7 (M35): the hero-name tokens — <c>{HeroName}</c>,
/// <c>{HeroAddressName}</c>, <c>{HeroPossessiveName}</c>, <c>{HomeEraName}</c> —
/// and the "named address" rule they exist to serve: "Traveler" survives only as
/// Sarah's affectionate nickname in her opening line (design §16, the
/// Character-Specific Narrative Layer, family 1).
///
/// <para>Pure C#: <see cref="DialogueTokens"/> takes an injected translator and the
/// copy checks read <c>localization/en.csv</c> directly, so nothing here reaches
/// the engine (CLAUDE.md failure signature 7). The compiled-translation half is
/// <c>CampaignLocalizationTests.TheHeroNameTableResolvesForEveryRosterHeroAndEveryTokenRenders</c>.
/// One <c>[TestSuite]</c> per file.</para>
/// </summary>
[TestSuite]
public class DialogueTokenTests {

    private static readonly Dictionary<string, string> FakeTable = new(StringComparer.Ordinal) {
        ["hero_name_leonardo"] = "Da Vinci",
        ["hero_address_leonardo"] = "Maestro",
        ["hero_possessive_leonardo"] = "Da Vinci's",
        ["hero_home_era_leonardo"] = "Florence",
        ["hero_address_fallback"] = "traveler",
    };

    private static string Translate(string key) => FakeTable.TryGetValue(key, out string v) ? v : key;

    [TestCase]
    public void EveryHeroTokenResolvesFromTheTableForTheSavedHero() {
        string rendered = DialogueTokens.SubstituteHeroTokens(
            "{HomeEraName}? Thank you, {HeroAddressName}. {HeroName} and {HeroPossessiveName} bench.",
            "leonardo", Translate);
        AssertString(rendered).IsEqual("Florence? Thank you, Maestro. Da Vinci and Da Vinci's bench.");
    }

    [TestCase]
    public void AnUnknownHeroFallsBackToTheFallbackRowAndNeverShowsARawKeyOrToken() {
        string rendered = DialogueTokens.SubstituteHeroTokens(
            "Thank you, {HeroAddressName}. {HomeEraName}.", "not_a_hero", Translate);
        // Address has a fallback row; home era has none, so the fixed last resort applies.
        AssertString(rendered).IsEqual("Thank you, traveler. Home.");
        AssertThat(DialogueTokens.HasHeroToken(rendered)).IsFalse();
        AssertThat(rendered.Contains("hero_")).IsFalse();

        string empty = DialogueTokens.SubstituteHeroTokens("{HeroName}", "", null);
        AssertString(empty).IsEqual("the traveler");
    }

    [TestCase]
    public void ALineWithoutAHeroTokenIsReturnedUntouchedAndCaptiveTokensAreLeftForTheirOwnPass() {
        const string captive = "They took {CaptiveName1}. And {CaptiveName2}.";
        AssertString(DialogueTokens.SubstituteHeroTokens(captive, "leonardo", Translate)).IsEqual(captive);
        AssertThat(DialogueTokens.HasHeroToken(captive)).IsFalse();
    }

    [TestCase]
    public void TravelerSurvivesOnlyAsSarahsOpeningNicknameAndTheFallbackRows() {
        // Design §16 family 1: the hero's rows use their name and voice; "Traveler"
        // survives only as Sarah's nickname in her opening line. The fallback rows
        // are what an unknown hero resolves to, and speaker_player is the label a
        // slotless launch falls back to - neither is dialogue copy.
        var allowed = new HashSet<string>(StringComparer.Ordinal) {
            "dlg_l00_intro_1", "speaker_player",
            "hero_name_fallback", "hero_address_fallback", "hero_possessive_fallback"
        };
        var survivors = new List<string>();
        var traveler = new Regex(@"\btraveler\b", RegexOptions.IgnoreCase);
        foreach ((string key, string english) in EnglishRows()) {
            if (allowed.Contains(key)) continue;
            if (traveler.IsMatch(english)) survivors.Add(key);
        }
        if (survivors.Count > 0) AssertThat(string.Join(" | ", survivors)).IsEqual("");
    }

    [TestCase]
    public void TheMasterAuthoredTokenLinesCarryTheirTokens() {
        Dictionary<string, string> rows = RowMap();
        // design-godot.md §16 Level 0: "...Where am I? {HomeEraName}? ..."
        AssertString(rows["dlg_l00_intro_2"]).Contains(DialogueTokens.HomeEraName);
        // §16 Level 15 ending: "Thank you, {HeroAddressName}."
        AssertString(rows["dlg_l15_ending_4"]).Contains("Thank you, " + DialogueTokens.HeroAddressName + ".");
        // M12: sealing the moment is what shuts the siphons and frees the locals.
        AssertString(rows["dlg_l00_intro_5"]).Contains("Seal the moment and the siphons die");
        AssertThat(rows["dlg_l15_ending_2"].Contains("dissolve from guards", StringComparison.Ordinal))
            .OverrideFailureMessage("M12: the locals' gear already fell away at each sealing.").IsFalse();
    }

    private static Dictionary<string, string> RowMap() {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string english) in EnglishRows()) map[key] = english;
        return map;
    }

    /// <summary>Minimal reader: one row per line, <c>key,value</c>, value optionally quoted.</summary>
    private static IEnumerable<(string Key, string English)> EnglishRows() {
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            if (line.Length == 0 || line[0] == '#') continue;
            int comma = line.IndexOf(',');
            if (comma <= 0) continue;
            string key = line[..comma];
            string value = line[(comma + 1)..];
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') {
                value = value[1..^1].Replace("\"\"", "\"");
            }
            yield return (key, value);
        }
    }
}
