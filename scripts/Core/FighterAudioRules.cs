namespace FTT.Core {

    /// <summary>
    /// When a Fighter match's music layer changes (Package 8 B5). Pure C# with no
    /// Godot and no Klotho types, deliberately: the decision belongs to a test, not
    /// to a running match, and <c>scripts/FighterSim/</c> is a closed boundary.
    /// <c>FighterSimulationDriver</c> reads deterministic component fields and asks
    /// these two questions.
    /// </summary>
    public static class FighterAudioRules {
        /// <summary>Stocks below which the climax layer is meaningless.</summary>
        public const int LastStockThreshold = 1;

        /// <summary>
        /// Only stock-bearing modes can have a "last stock". A pure time-limit match
        /// never removes a stock, so a climax keyed on stocks would never fire there;
        /// its own final-seconds climax is a separate rule and is not implemented.
        /// </summary>
        public static bool ModeUsesStocks(int matchMode) =>
            matchMode == (int)MatchMode.Stock || matchMode == (int)MatchMode.Hybrid;

        /// <summary>
        /// True when a stock loss has left either fighter on their last stock.
        /// Zero is not a climax: that fighter is already out and the match is over,
        /// so the KO sequence owns the moment instead.
        /// </summary>
        public static bool IsLastStockClimax(int playerOneStocks, int playerTwoStocks) =>
            playerOneStocks == LastStockThreshold || playerTwoStocks == LastStockThreshold;

        /// <summary>The full test: mode plus stock state, as the driver evaluates it.</summary>
        public static bool ShouldEnterClimax(int matchMode, int playerOneStocks, int playerTwoStocks) =>
            ModeUsesStocks(matchMode) && IsLastStockClimax(playerOneStocks, playerTwoStocks);
    }
}
