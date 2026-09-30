using System;
using System.Collections.Generic;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W4 (design §16 M14 / S29): the two Mystery Thread list tokens,
/// <c>{MissingSoFar}</c> (Level 3's exit) and <c>{MissingPlaces}</c> (Level 8's
/// post-boss), resolved from per-hero rows with a fallback by
/// <see cref="MissingLegendTokens"/>.
///
/// <para>Pure C#: the translator is injected, so nothing here reaches the engine
/// (CLAUDE.md failure signature 7). The authored rows and the dialogue sweep are
/// <c>MysteryThreadAbsenceTests</c>. One <c>[TestSuite]</c> per file.</para>
/// </summary>
[TestSuite]
public class MissingLegendTokenTests {

    private static readonly Dictionary<string, string> FakeTable = new(StringComparer.Ordinal) {
        ["missing_so_far_fallback"] = "Da Vinci, Joan",
        ["missing_so_far_leonardo"] = "Joan",
        ["missing_so_far_joan"] = "Da Vinci",
        ["missing_places_fallback"] = "Florence. Like Orléans. Like Chicago",
        ["missing_places_leonardo"] = "Orléans. Like Chicago",
        ["missing_places_joan"] = "Florence. Like Chicago",
        ["missing_places_tesla"] = "Florence. Like Orléans"
    };

    private static string Translate(string key) => FakeTable.TryGetValue(key, out string value) ? value : key;

    [TestCase]
    public void EachHeroReadsItsOwnRowAndNeverHearsItselfNamedAsMissing() {
        const string chicago = "{MissingSoFar}, now him.";
        AssertString(MissingLegendTokens.Substitute(chicago, "leonardo", Translate)).IsEqual("Joan, now him.");
        AssertString(MissingLegendTokens.Substitute(chicago, "joan", Translate)).IsEqual("Da Vinci, now him.");

        const string alexandria = "The era holds its breath — like {MissingPlaces}.";
        AssertString(MissingLegendTokens.Substitute(alexandria, "leonardo", Translate))
            .IsEqual("The era holds its breath — like Orléans. Like Chicago.");
        AssertString(MissingLegendTokens.Substitute(alexandria, "tesla", Translate))
            .IsEqual("The era holds its breath — like Florence. Like Orléans.");
    }

    [TestCase]
    public void AHeroWithNoRowReadsTheFallbackAndAMissingFallbackReadsTheFixedEnglish() {
        AssertString(MissingLegendTokens.Substitute("{MissingSoFar}", "einstein", Translate)).IsEqual("Da Vinci, Joan");
        AssertString(MissingLegendTokens.Substitute("{MissingPlaces}", "mozart", Translate))
            .IsEqual("Florence. Like Orléans. Like Chicago");
        // A roster addition with no rows at all, and an empty hero, still resolve.
        AssertString(MissingLegendTokens.Substitute("{MissingPlaces}", "", Translate))
            .IsEqual("Florence. Like Orléans. Like Chicago");
        // No table at all: the fixed English, never a raw key or token.
        string bare = MissingLegendTokens.Substitute("{MissingSoFar} / {MissingPlaces}", "tesla", null);
        AssertString(bare).IsEqual(MissingLegendTokens.MissingSoFarLastResort + " / " +
                                   MissingLegendTokens.MissingPlacesLastResort);
        AssertThat(MissingLegendTokens.HasToken(bare)).IsFalse();
    }

    [TestCase]
    public void ALineWithoutEitherTokenIsReturnedUntouchedAndHeroTokensAreLeftForTheirOwnPass() {
        const string plain = "Come home. I want a closer look at the shards.";
        AssertString(MissingLegendTokens.Substitute(plain, "leonardo", Translate)).IsEqual(plain);
        const string other = "{HeroAddressName}, they took {CaptiveName1}.";
        AssertString(MissingLegendTokens.Substitute(other, "leonardo", Translate)).IsEqual(other);
        AssertThat(MissingLegendTokens.HasToken(other)).IsFalse();
    }

    [TestCase]
    public void TheRowKeysAreBuiltFromTheSavedHeroID() {
        AssertString(MissingLegendTokens.MissingSoFarKey("joan")).IsEqual("missing_so_far_joan");
        AssertString(MissingLegendTokens.MissingPlacesKey("tesla")).IsEqual("missing_places_tesla");
        AssertString(MissingLegendTokens.MissingSoFarKey(MissingLegendTokens.FallbackHeroID))
            .IsEqual("missing_so_far_fallback");
        AssertThat(MissingLegendTokens.Tokens.Count).IsEqual(2);
    }
}
