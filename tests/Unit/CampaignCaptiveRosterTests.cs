using System.Collections.Generic;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A5 — the Mystery Thread's captive pool (plan §2.10 item 2). The
/// absent legends a dialogue line names through <c>{CaptiveName1}</c> /
/// <c>{CaptiveName2}</c> are the content manifest's character roster minus the
/// locked campaign hero.
///
/// <para>Every case here is written <b>count-agnostically</b> against the
/// manifest (the A9 mandate): nothing asserts the literal nine, so adding a
/// tenth character widens the captive pool with no code change and no test
/// change. What is pinned is the shape — one fewer captive than the roster, the
/// hero never among them, and the authored Leonardo → Cleopatra → Tesla
/// preference for the two names writers reach for first.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CampaignCaptiveRosterTests {

    [TestCase]
    public void EveryHeroYieldsRosterCountMinusOneCaptivesAndIsNeverAmongThem() {
        IReadOnlyList<string> roster = CampaignCaptiveRoster.Roster();
        AssertThat(roster.Count > 2)
            .OverrideFailureMessage("The manifest must register a roster for this suite to mean anything.")
            .IsTrue();

        var issues = new List<string>();
        foreach (string hero in roster) {
            IReadOnlyList<string> captives = CampaignCaptiveRoster.For(hero);
            if (captives.Count != roster.Count - 1) {
                issues.Add($"{hero}: expected {roster.Count - 1} captives, got {captives.Count}");
            }
            foreach (string captive in captives) {
                if (captive == hero) issues.Add($"{hero}: is listed as their own captive");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheRosterIsReadFromTheManifestRatherThanALiteralList() {
        // The manifest is the source: what the resolver returns must be exactly
        // the Character rows, in their order.
        ContentManifest manifest = ContentManifest.LoadDefault();
        var manifestIDs = new List<string>();
        foreach (ContentManifestEntry entry in manifest.ForCategory(ContentCategory.Character)) {
            manifestIDs.Add(entry.ContentID);
        }
        AssertThat(string.Join(",", CampaignCaptiveRoster.Roster()))
            .OverrideFailureMessage("The captive pool must track the manifest, not a hardcoded array.")
            .IsEqual(string.Join(",", manifestIDs));

        // And a larger roster widens the pool with no code change.
        var tenCharacterRoster = new List<string>(manifestIDs) { "hypothetical_tenth" };
        AssertThat(CampaignCaptiveRoster.For(manifestIDs[0], tenCharacterRoster).Count)
            .IsEqual(tenCharacterRoster.Count - 1);
    }

    [TestCase]
    public void TheAuthoredPriorityIsLeonardoThenCleopatraThenTeslaAndSkipsTheActiveHero() {
        // A hero outside the priority list gets the top two as written.
        (string first, string second) = CampaignCaptiveRoster.NamedExamplesFor("einstein");
        AssertThat(first).IsEqual("leonardo");
        AssertThat(second).IsEqual("cleopatra");

        // The hero is filtered out and the next priority entry slides up.
        (first, second) = CampaignCaptiveRoster.NamedExamplesFor("leonardo");
        AssertThat(first).IsEqual("cleopatra");
        AssertThat(second).IsEqual("tesla");

        (first, second) = CampaignCaptiveRoster.NamedExamplesFor("cleopatra");
        AssertThat(first).IsEqual("leonardo");
        AssertThat(second).IsEqual("tesla");

        // Spoken names live in their own key family, not the display names.
        AssertThat(CampaignCaptiveRoster.SpokenNameKey("leonardo")).IsEqual("captive_name_leonardo");
        AssertThat(CampaignCaptiveRoster.SpokenNameKey("")).IsEqual("");
    }

    [TestCase]
    public void EveryHeroChoiceProducesADistinctNonEmptyPair() {
        var issues = new List<string>();
        foreach (string hero in CampaignCaptiveRoster.Roster()) {
            (string first, string second) = CampaignCaptiveRoster.NamedExamplesFor(hero);
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second)) {
                issues.Add($"{hero}: incomplete captive pair ({first}, {second})");
                continue;
            }
            if (first == second) issues.Add($"{hero}: both tokens resolve to {first}");
            if (first == hero || second == hero) issues.Add($"{hero}: named as their own captive");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

        // An unknown hero ID is not an error — nobody is filtered out.
        IReadOnlyList<string> all = CampaignCaptiveRoster.For("not_a_character");
        AssertThat(all.Count).IsEqual(CampaignCaptiveRoster.Roster().Count);
    }
}
