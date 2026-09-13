using System;
using System.Collections.Generic;
using System.IO;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 11 A6: the V7.5 Act III knowledge boundary, mechanised.
///
/// <para><b>The rule.</b> design-godot.md section 2, "The Villain's True Plan —
/// concealed from the player until Act III": <i>no dialogue line, level brief, or UI
/// string may state any of it before Level 13.</i> Through Acts I and II the game
/// presents only the Wardens' honest, mistaken read — the sabotage <i>is</i> the plan.
/// Level 12 reveals <i>where</i> (a fortress, live signatures, draining); Level 14
/// reveals <i>why</i> (the Forge, the deficit, the rewrite).</para>
///
/// <para><b>Why a test.</b> A knowledge boundary is the one kind of narrative rule that
/// erodes silently: every individual line that leaks it reads perfectly well on its own,
/// and the damage is only visible to someone holding all sixteen levels in their head at
/// once. Nobody reviews copy that way. Before this package the shipped build leaked the
/// (then-current) endgame two levels early in <c>dlg_l12_preboss_2</c> and
/// <c>dlg_l12_preboss_4</c> and no gate noticed for four packages. The proper nouns below
/// are the reveal's own vocabulary, so a sweep for them is a cheap, exact proxy for the
/// rule — the only mechanical way to keep the boundary from eroding again.</para>
///
/// <para>Pure C#: it reads <c>localization/en.csv</c> directly and needs no Godot
/// runtime, so a leak fails fast and without the engine. One <c>[TestSuite]</c> per
/// file (CLAUDE.md failure signature 6).</para>
/// </summary>
[TestSuite]
public class NarrativeKnowledgeBoundaryTests {

    /// <summary>
    /// Act I and Act II dialogue key prefixes. Level 13 is the first level at which
    /// any Villain's-True-Plan fact may appear at all, so the sweep covers levels 0
    /// through 12 plus the hub (which is revisited throughout both acts).
    /// </summary>
    private static readonly string[] GuardedKeyPrefixes = {
        "dlg_l00_", "dlg_l01_", "dlg_l02_", "dlg_l03_", "dlg_l04_", "dlg_l05_",
        "dlg_l06_", "dlg_l07_", "dlg_l08_", "dlg_l09_", "dlg_l10_", "dlg_l11_",
        "dlg_l12_", "dlg_hub_"
    };

    /// <summary>
    /// The reveal's own vocabulary. Matched case-insensitively as substrings, so
    /// "Extraction Cradle" also catches "Extraction Cradles".
    /// </summary>
    private static readonly string[] ForbiddenBeforeActIII = {
        "Anchor Forge",
        "Prime Anchor",
        "Extraction Cradle",
        "Meridian Founding",
        "Bastion",
        "the Landing",
        "deficit"
    };

    /// <summary>
    /// The other half of the same rule, stated positively: the retired "your
    /// resonance fades as history heals" arc. V7.5 is explicit that the power
    /// <i>grows</i> across the campaign and that the hero surrenders it exactly once,
    /// by choice, at the very end — so no line outside Level 15's ending may claim a
    /// mid-campaign loss. These phrases are the shipped arc's own wording.
    /// </summary>
    private static readonly string[] RetiredPowerFadePhrases = {
        "resonance will fade",
        "the strength fades",
        "glowing less",
        "resonance is fading",
        "resonance is thinning",
        "lose all of your powers",
        "mortal again"
    };

    [TestCase]
    public void NoActIOrActIILineStatesAVillainsTruePlanFact() {
        var leaks = new List<string>();
        foreach ((string key, string english) in EnglishRows()) {
            if (!IsGuarded(key)) continue;
            foreach (string noun in ForbiddenBeforeActIII) {
                if (english.Contains(noun, StringComparison.OrdinalIgnoreCase)) {
                    leaks.Add($"{key} states '{noun}' before Level 13");
                }
            }
        }
        if (leaks.Count > 0) AssertThat(string.Join(" | ", leaks)).IsEqual("");
    }

    [TestCase]
    public void NoDialogueLineClaimsTheHeroLosesResonanceMidCampaign() {
        var survivors = new List<string>();
        foreach ((string key, string english) in EnglishRows()) {
            if (!key.StartsWith("dlg_", StringComparison.Ordinal)) continue;
            foreach (string phrase in RetiredPowerFadePhrases) {
                if (english.Contains(phrase, StringComparison.OrdinalIgnoreCase)) {
                    survivors.Add($"{key} still carries the retired power-fade arc ('{phrase}')");
                }
            }
        }
        if (survivors.Count > 0) AssertThat(string.Join(" | ", survivors)).IsEqual("");
    }

    /// <summary>
    /// The sweep is worthless if the corpus walk silently reaches nothing — a renamed
    /// prefix family would make both cases above pass by checking zero rows.
    /// </summary>
    [TestCase]
    public void TheGuardedCorpusIsLargeEnoughForTheSweepToMeanSomething() {
        int guarded = 0;
        int rows = 0;
        foreach ((string key, string _) in EnglishRows()) {
            rows++;
            if (IsGuarded(key)) guarded++;
        }
        AssertThat(rows).OverrideFailureMessage(
            $"Only {rows} rows parsed out of localization/en.csv; the parser is broken.")
            .IsGreaterEqual(900);
        AssertThat(guarded).OverrideFailureMessage(
            $"Only {guarded} Act I/II dialogue rows were reached; the prefix roster is stale.")
            .IsGreaterEqual(100);
    }

    private static bool IsGuarded(string key) {
        foreach (string prefix in GuardedKeyPrefixes) {
            if (key.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>
    /// Minimal CSV reader: the table is one row per line, <c>key,value</c>, with the
    /// value optionally double-quoted and internal quotes doubled.
    /// </summary>
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
