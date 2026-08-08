using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B5. The Fighter last-stock climax trigger.
///
/// <para>The rule lives outside <c>FighterSimulationDriver</c> precisely so it can be
/// tested here: reaching the real condition through the driver means simulating a
/// full match to a knockout, and the interesting cases (a time-limit match that can
/// never lose a stock, a one-stock match that starts on the last stock) are decisions
/// rather than gameplay.</para>
/// </summary>
[TestSuite]
public class FighterAudioRulesTests {

    [TestCase]
    public void OnlyStockBearingModesCanReachTheClimax() {
        AssertThat(FighterAudioRules.ModeUsesStocks((int)MatchMode.Stock)).IsTrue();
        AssertThat(FighterAudioRules.ModeUsesStocks((int)MatchMode.Hybrid)).IsTrue();
        AssertThat(FighterAudioRules.ModeUsesStocks((int)MatchMode.TimeLimit)).IsFalse();
    }

    [TestCase]
    public void EitherFighterFallingToOneStockIsTheClimax() {
        AssertThat(FighterAudioRules.IsLastStockClimax(1, 3)).IsTrue();
        AssertThat(FighterAudioRules.IsLastStockClimax(3, 1)).IsTrue();
        AssertThat(FighterAudioRules.IsLastStockClimax(1, 1)).IsTrue();
    }

    [TestCase]
    public void AHealthyStockCountIsNotAClimax() {
        AssertThat(FighterAudioRules.IsLastStockClimax(3, 3)).IsFalse();
        AssertThat(FighterAudioRules.IsLastStockClimax(2, 3)).IsFalse();
    }

    /// <summary>
    /// Zero stocks means that fighter is already out and the driver's KO sequence
    /// owns the moment. Layering the climax in underneath a knockout stinger would
    /// be the music arriving after the fight it was meant to underscore.
    /// </summary>
    [TestCase]
    public void AnEliminatedFighterIsNotAClimax() {
        AssertThat(FighterAudioRules.IsLastStockClimax(0, 3)).IsFalse();
        AssertThat(FighterAudioRules.IsLastStockClimax(3, 0)).IsFalse();
        AssertThat(FighterAudioRules.IsLastStockClimax(0, 0)).IsFalse();
    }

    [TestCase]
    public void TheCombinedTestRequiresBothTheModeAndTheStockState() {
        AssertThat(FighterAudioRules.ShouldEnterClimax((int)MatchMode.Stock, 1, 2)).IsTrue();
        AssertThat(FighterAudioRules.ShouldEnterClimax((int)MatchMode.Hybrid, 2, 1)).IsTrue();
        AssertThat(FighterAudioRules.ShouldEnterClimax((int)MatchMode.TimeLimit, 1, 1)).IsFalse();
        AssertThat(FighterAudioRules.ShouldEnterClimax((int)MatchMode.Stock, 2, 2)).IsFalse();
    }

    /// <summary>
    /// A one-stock match is a sudden-death rule set: every fighter starts on their
    /// last stock, so the climax is correct from the opening frame rather than a
    /// degenerate case to suppress.
    /// </summary>
    [TestCase]
    public void AOneStockMatchIsAClimaxFromTheStart() {
        AssertThat(FighterAudioRules.ShouldEnterClimax((int)MatchMode.Stock, 1, 1)).IsTrue();
    }
}
