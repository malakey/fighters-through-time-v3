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
        // F21 (Package 11 A1c): Stock is the only stock-bearing mode. The retired
        // Hybrid ordinal used to answer true here; it is not a playable mode any
        // more, and a legacy saved Hybrid normalizes to Stock before a match is
        // ever created, so nothing that reaches the audio rules can carry it.
        AssertThat(FighterAudioRules.ModeUsesStocks(SavedMatchSettings.LegacyHybridMode)).IsFalse();
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
        AssertThat(FighterAudioRules.ShouldEnterClimax((int)MatchMode.Stock, 2, 1)).IsTrue();
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

    // === The low-health branch (design-godot.md:2832, audit M-32) ===

    [TestCase]
    public void FallingStrictlyBelowTwentyPercentHealthIsAClimax() {
        AssertThat(FighterAudioRules.IsLowHealthClimax(19, 100)).IsTrue();
        AssertThat(FighterAudioRules.IsLowHealthClimax(1, 100)).IsTrue();
        // Exactly 20% is not "below 20% health".
        AssertThat(FighterAudioRules.IsLowHealthClimax(20, 100)).IsFalse();
        AssertThat(FighterAudioRules.IsLowHealthClimax(100, 100)).IsFalse();
    }

    /// <summary>
    /// Zero HP is a knockout in flight, not a tension state: the stock-loss beat or
    /// the KO sequence owns that moment, mirroring the zero-stocks rule above. A
    /// zeroed max pool (defensive) can never be a climax either.
    /// </summary>
    [TestCase]
    public void AKnockedOutOrDegenerateHealthPoolIsNotAClimax() {
        AssertThat(FighterAudioRules.IsLowHealthClimax(0, 100)).IsFalse();
        AssertThat(FighterAudioRules.IsLowHealthClimax(0, 0)).IsFalse();
        AssertThat(FighterAudioRules.IsLowHealthClimax(5, 0)).IsFalse();
    }

    /// <summary>
    /// The full trigger: the HP branch fires in every mode — it is what gives a
    /// pure TimeLimit match a climax at all — while the stock branch stays gated
    /// on stock-bearing modes.
    /// </summary>
    [TestCase]
    public void TheFullTriggerAcceptsLowHealthInAnyModeAndStocksOnlyInStockModes() {
        // TimeLimit can never climax on stocks, but low health gets it there.
        AssertThat(FighterAudioRules.ShouldEnterClimax(
            (int)MatchMode.TimeLimit, 1, 1, 100, 100, 100, 100)).IsFalse();
        AssertThat(FighterAudioRules.ShouldEnterClimax(
            (int)MatchMode.TimeLimit, 3, 3, 100, 100, 19, 100)).IsTrue();
        // Either fighter's HP qualifies.
        AssertThat(FighterAudioRules.ShouldEnterClimax(
            (int)MatchMode.Stock, 3, 3, 15, 100, 100, 100)).IsTrue();
        // Healthy HP falls back to the stock branch.
        AssertThat(FighterAudioRules.ShouldEnterClimax(
            (int)MatchMode.Stock, 1, 3, 100, 100, 100, 100)).IsTrue();
        AssertThat(FighterAudioRules.ShouldEnterClimax(
            (int)MatchMode.Stock, 2, 2, 100, 100, 100, 100)).IsFalse();
    }
}
