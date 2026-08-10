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

        /// <summary>
        /// Percentage of maximum HP below which the design's second climax trigger
        /// fires ("drops to their last stock life <em>or falls below 20% health</em>",
        /// design-godot.md:2832). Shared with the LowHealth audio snapshot so the
        /// two ears of the same tension cue cannot drift apart.
        /// </summary>
        public const int LowHealthClimaxPercent = 20;

        /// <summary>
        /// True when a fighter has fallen strictly below 20% of maximum HP. Integer
        /// arithmetic, matching how the driver reads deterministic HP fields. Zero
        /// HP is not a climax — that fighter is mid-knockout and the stock-loss
        /// beat or the KO sequence owns the moment.
        /// </summary>
        public static bool IsLowHealthClimax(int currentHP, int maxHP) =>
            maxHP > 0 && currentHP > 0 && currentHP * 100 < maxHP * LowHealthClimaxPercent;

        /// <summary>The stock half of the test: mode plus stock state.</summary>
        public static bool ShouldEnterClimax(int matchMode, int playerOneStocks, int playerTwoStocks) =>
            ModeUsesStocks(matchMode) && IsLastStockClimax(playerOneStocks, playerTwoStocks);

        /// <summary>
        /// The full design trigger (audit M-32): last stock in a stock-bearing mode,
        /// or either fighter below 20% health in <em>any</em> mode — the HP branch is
        /// what gives a pure TimeLimit match a climax at all.
        /// </summary>
        public static bool ShouldEnterClimax(
            int matchMode,
            int playerOneStocks, int playerTwoStocks,
            int playerOneHP, int playerOneMaxHP,
            int playerTwoHP, int playerTwoMaxHP) =>
            ShouldEnterClimax(matchMode, playerOneStocks, playerTwoStocks)
            || IsLowHealthClimax(playerOneHP, playerOneMaxHP)
            || IsLowHealthClimax(playerTwoHP, playerTwoMaxHP);
    }
}
