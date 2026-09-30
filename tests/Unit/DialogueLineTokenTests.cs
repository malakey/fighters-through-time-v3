using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W3 (S07/S48): the two whole-line hero tokens —
/// <c>{HeroAcceptLine}</c>, spoken in Level 0 after Sarah says the hero cannot go
/// home, and <c>{HeroFarewellLine}</c>, its answer in the Time-Ship send-off.
///
/// <para>Pure C#: <see cref="DialogueTokens"/> takes an injected translator and the
/// copy checks read <c>localization/en.csv</c> directly, so nothing here reaches
/// the engine (CLAUDE.md failure signature 7). The compiled-translation sweep is
/// <c>CampaignLocalizationTests.TheHeroNameTableResolvesForEveryRosterHeroAndEveryTokenRenders</c>,
/// which now walks all six table keys per hero. One <c>[TestSuite]</c> per file.</para>
/// </summary>
[TestSuite]
public class DialogueLineTokenTests {

    /// <summary>The design's §16 drafts, verbatim (S07 / S48), keyed by saved hero ID.</summary>
    private static readonly Dictionary<string, (string Accept, string Farewell)> DesignLines = new(StringComparer.Ordinal) {
        ["einstein"] = ("Then the problem is clear, if not simple. Show me where the equations break.",
            "There's an equation on my board I won't finish. I think that's all right."),
        ["joan"] = ("I know the sound of a calling. Where do we ride?", "My banner's waiting. So are they."),
        ["leonardo"] = ("A broken machine is only a puzzle no one has finished. Show me the pieces.",
            "Every piece back where it belongs. Now — I have a wall to paint."),
        ["tesla"] = ("Then we reroute the current. Show me where it leaks.", "The lights are on. That's enough."),
        ["mozart"] = ("Every piece comes home to its key. Very well — let's play it through.",
            "Every piece comes home to its key. So do I."),
        ["cleopatra"] = ("Egypt has outlasted worse conquerors. Name their terms, Commander — then hear mine.",
            "My terms were met, Commander. The rest, I'll choose myself."),
        ["shakespeare"] = ("Then the play's not ended; we are mid-act. Give me my cue.",
            "I have a fifth act to finish. Give me my cue."),
        ["lincoln"] = ("A house divided cannot stand — nor, it seems, a time. Then we mend it.",
            "It's mended. Now I'd best go and finish the work."),
        ["tubman"] = ("I never ran my train off the track, and I never lost a passenger. Show me where the line broke.",
            "The road's clear again. Take me back to the line — there's folks still waiting."),
    };

    [TestCase]
    public void BothLineTokensAreHeroTokensWithTheirOwnKeyFamilies() {
        AssertThat(DialogueTokens.HeroTokens.Contains(DialogueTokens.HeroAcceptLine)).IsTrue();
        AssertThat(DialogueTokens.HeroTokens.Contains(DialogueTokens.HeroFarewellLine)).IsTrue();
        AssertString(DialogueTokens.AcceptLineKey("joan")).IsEqual("hero_accept_line_joan");
        AssertString(DialogueTokens.FarewellLineKey("joan")).IsEqual("hero_farewell_line_joan");
        string[] keys = DialogueTokens.TableKeysFor("joan");
        AssertThat(keys.Length).IsEqual(6);
        AssertThat(Array.IndexOf(keys, "hero_accept_line_joan") >= 0).IsTrue();
        AssertThat(Array.IndexOf(keys, "hero_farewell_line_joan") >= 0).IsTrue();
        AssertThat(DialogueTokens.HasHeroToken("{HeroAcceptLine}")).IsTrue();
        AssertThat(DialogueTokens.HasHeroToken("{HeroFarewellLine}")).IsTrue();
    }

    [TestCase]
    public void EveryDesignDraftIsAuthoredVerbatimForItsHero() {
        Dictionary<string, string> rows = RowMap();
        var issues = new List<string>();
        foreach ((string hero, (string accept, string farewell)) in DesignLines) {
            if (!rows.TryGetValue(DialogueTokens.AcceptLineKey(hero), out string a) || a != accept) {
                issues.Add($"{hero}: accept line is not the design's draft");
            }
            if (!rows.TryGetValue(DialogueTokens.FarewellLineKey(hero), out string f) || f != farewell) {
                issues.Add($"{hero}: farewell line is not the design's draft");
            }
        }
        // A roster addition with no rows must still speak: the fallback pair exists.
        foreach (string key in new[] { "hero_accept_line_fallback", "hero_farewell_line_fallback" }) {
            if (!rows.ContainsKey(key)) issues.Add($"{key} is missing");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheTokenLineResolvesToTheSavedHerosRowAndFallsBackWithoutARawToken() {
        Dictionary<string, string> rows = RowMap();
        string Translate(string key) => rows.TryGetValue(key, out string v) ? v : key;

        AssertString(DialogueTokens.SubstituteHeroTokens("{HeroAcceptLine}", "joan", Translate))
            .IsEqual("I know the sound of a calling. Where do we ride?");
        AssertString(DialogueTokens.SubstituteHeroTokens("{HeroFarewellLine}", "tesla", Translate))
            .IsEqual("The lights are on. That's enough.");

        // Unknown hero: the fallback row, never the raw token or a raw key.
        string unknown = DialogueTokens.SubstituteHeroTokens("{HeroFarewellLine}", "not_a_hero", Translate);
        AssertString(unknown).IsEqual(rows["hero_farewell_line_fallback"]);
        AssertThat(DialogueTokens.HasHeroToken(unknown)).IsFalse();

        // No table at all (a translator that knows nothing): the fixed last resort.
        AssertString(DialogueTokens.SubstituteHeroTokens("{HeroAcceptLine}", "joan", key => key))
            .IsEqual("Then I will fight for it.");
        AssertString(DialogueTokens.SubstituteHeroTokens("{HeroFarewellLine}", "", null))
            .IsEqual("Take me home.");
    }

    [TestCase]
    public void TheLevelZeroAcceptanceAndTheSendOffFarewellCarryTheirTokens() {
        Dictionary<string, string> rows = RowMap();
        AssertString(rows["dlg_l00_intro_accept"]).IsEqual(DialogueTokens.HeroAcceptLine);
        AssertString(rows["dlg_epilogue_sendoff_4"]).IsEqual(DialogueTokens.HeroFarewellLine);
        // S06: Sarah answers the acceptance with the design's line, verbatim.
        AssertString(rows["dlg_l00_intro_12"])
            .IsEqual("Then welcome aboard. Now — we calibrate it. Step up to the bay console.");
        // S40: "You'll wake in your own time", not "where they tried to take you from".
        AssertString(rows["dlg_epilogue_sendoff_3"]).Contains("You'll wake in your own time.");
        AssertThat(rows["dlg_epilogue_sendoff_3"].Contains("where they tried to take you from")).IsFalse();
    }

    private static Dictionary<string, string> RowMap() {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            if (line.Length == 0 || line[0] == '#') continue;
            int comma = line.IndexOf(',');
            if (comma <= 0) continue;
            string value = line[(comma + 1)..];
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') {
                value = value[1..^1].Replace("\"\"", "\"");
            }
            map[line[..comma]] = value;
        }
        return map;
    }
}
