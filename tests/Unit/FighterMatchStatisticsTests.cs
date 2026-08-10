using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 6 A2. Versus statistics logging, including the design's rule that a
/// true draw counts in neither the overall nor the per-character tallies.
/// </summary>
[TestSuite]
public class FighterMatchStatisticsTests {

    [TestCase]
    public void PlayerOneWinCreditsBothCharacterTallies() {
        var data = new GlobalSaveData();

        FighterMatchStatistics.Record(data, "joan", "tesla", winnerPlayerID: 0, isTrueTie: false);

        AssertThat(data.TotalWins).IsEqual(1);
        AssertThat(data.TotalLosses).IsEqual(0);
        AssertThat(FighterMatchStatistics.WinsFor(data, "joan")).IsEqual(1);
        AssertThat(FighterMatchStatistics.LossesFor(data, "tesla")).IsEqual(1);
        AssertThat(FighterMatchStatistics.WinsFor(data, "tesla")).IsEqual(0);
        AssertThat(FighterMatchStatistics.LossesFor(data, "joan")).IsEqual(0);
    }

    [TestCase]
    public void PlayerTwoWinCreditsTheOpposingCharacters() {
        var data = new GlobalSaveData();

        FighterMatchStatistics.Record(data, "joan", "tesla", winnerPlayerID: 1, isTrueTie: false);

        AssertThat(data.TotalWins).IsEqual(0);
        AssertThat(data.TotalLosses).IsEqual(1);
        AssertThat(FighterMatchStatistics.WinsFor(data, "tesla")).IsEqual(1);
        AssertThat(FighterMatchStatistics.LossesFor(data, "joan")).IsEqual(1);
    }

    [TestCase]
    public void ATrueDrawIsRecordedAsNeitherAWinNorALossAnywhere() {
        var data = new GlobalSaveData();
        FighterMatchStatistics.Record(data, "joan", "tesla", winnerPlayerID: -1, isTrueTie: true);

        AssertThat(data.TotalWins).IsEqual(0);
        AssertThat(data.TotalLosses).IsEqual(0);
        AssertThat(data.CharacterWins.Count).IsEqual(0);
        AssertThat(data.CharacterLosses.Count).IsEqual(0);
        AssertThat(FighterMatchStatistics.WinsFor(data, "joan")).IsEqual(0);
        AssertThat(FighterMatchStatistics.LossesFor(data, "tesla")).IsEqual(0);
    }

    /// <summary>
    /// Audit §4 Fighter Low: a true draw used to be recorded nowhere — the
    /// design's "total matches played" was unrecoverable. It now lands in the
    /// additive <see cref="GlobalSaveData.TotalDraws"/> tally, decided matches
    /// leave it untouched, and Normalize preserves it.
    /// </summary>
    [TestCase]
    public void ATrueDrawIncrementsTheDrawTallyAndDecidedMatchesDoNot() {
        var data = new GlobalSaveData();
        FighterMatchStatistics.Record(data, "joan", "tesla", winnerPlayerID: -1, isTrueTie: true);
        FighterMatchStatistics.Record(data, "joan", "tesla", winnerPlayerID: -1, isTrueTie: true);
        FighterMatchStatistics.Record(data, "joan", "tesla", winnerPlayerID: 0, isTrueTie: false);

        AssertThat(data.TotalDraws).IsEqual(2);
        AssertThat(data.TotalWins).IsEqual(1);
        AssertThat(data.TotalLosses).IsEqual(0);

        data.Normalize();
        AssertThat(data.TotalDraws).IsEqual(2);

        // A hand-edited negative tally is clamped rather than trusted.
        data.TotalDraws = -5;
        data.Normalize();
        AssertThat(data.TotalDraws).IsEqual(0);
    }

    [TestCase]
    public void CharacterTalliesAccumulateAcrossMatchesAndSurviveNormalize() {
        var data = new GlobalSaveData();
        FighterMatchStatistics.Record(data, "joan", "tesla", 0, false);
        FighterMatchStatistics.Record(data, "joan", "mozart", 0, false);
        FighterMatchStatistics.Record(data, "einstein", "joan", 1, false);

        AssertThat(FighterMatchStatistics.WinsFor(data, "joan")).IsEqual(3);
        AssertThat(FighterMatchStatistics.LossesFor(data, "einstein")).IsEqual(1);
        AssertThat(FighterMatchStatistics.LossesFor(data, "tesla")).IsEqual(1);
        AssertThat(FighterMatchStatistics.LossesFor(data, "mozart")).IsEqual(1);
        AssertThat(data.TotalWins).IsEqual(2);
        AssertThat(data.TotalLosses).IsEqual(1);

        data.Normalize();
        AssertThat(FighterMatchStatistics.WinsFor(data, "joan")).IsEqual(3);
        AssertThat(FighterMatchStatistics.LossesFor(data, "mozart")).IsEqual(1);
    }

    [TestCase]
    public void MissingCharacterIdentifiersAreIgnoredRatherThanTallied() {
        var data = new GlobalSaveData();
        FighterMatchStatistics.Record(data, null, "", 0, false);

        AssertThat(data.TotalWins).IsEqual(1);
        AssertThat(data.CharacterWins.Count).IsEqual(0);
        AssertThat(data.CharacterLosses.Count).IsEqual(0);
    }
}
