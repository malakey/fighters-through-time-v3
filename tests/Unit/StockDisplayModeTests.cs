using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A8 / F21. The Fighter HUD's stock readout is mode-dependent.
///
/// <para>Stock mode has a finite pool, so finite pips are the truth. Time mode has
/// <em>unlimited</em> respawns and is decided by fewest stocks lost, so pips would
/// be actively misleading — there is nothing to run out of. The label replaces
/// them and counts up from zero, where lower is better.</para>
///
/// <para>The counter is the victim-side <c>KnockoutsSuffered</c>, which the
/// simulation already increments for every cause of death — opponent damage,
/// blast-zone falls, hazards, self-KOs, damage over time. F21 is explicit that no
/// attacker credit is involved, and the label copy must never read as KOs scored,
/// points or remaining lives.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StockDisplayModeTests {

    private const int TickRate = 60;

    [TestCase]
    public void StockModeKeepsTheFinitePipsAndHidesTheLostCounter() {
        FighterHUD hud = Mount();
        try {
            TranslationServer.SetLocale("en");
            hud.ConfigurePlayer(0, null, maxStocks: 3, maxShieldCharges: 3);
            hud.ApplyMatchState(true, 480 * TickRate, TickRate, (int)MatchMode.Stock, suddenDeath: false);

            FighterRuntimeComponent runtime = Runtime();
            runtime.KnockoutsSuffered = 2;
            hud.ApplyPlayerState(0, State(stocks: 1), runtime, TickRate);

            AssertThat(hud.StockPipsVisible(0)).IsTrue();
            AssertThat(hud.LitStockPips(0)).IsEqual(1);
            AssertThat(hud.StocksLostText(0))
                .OverrideFailureMessage("Stock mode must not show the stocks-lost label.")
                .IsEqual("");
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public void TimeModeReplacesThePipsWithTheStocksLostLabel() {
        FighterHUD hud = Mount();
        try {
            TranslationServer.SetLocale("en");
            hud.ConfigurePlayer(0, null, maxStocks: 3, maxShieldCharges: 3);
            hud.ApplyMatchState(true, 480 * TickRate, TickRate, (int)MatchMode.TimeLimit, suddenDeath: false);

            // Both counters start at zero, and zero is a real, visible state —
            // the label is how the player reads the score all match.
            hud.ApplyPlayerState(0, State(), Runtime(), TickRate);
            AssertThat(hud.StockPipsVisible(0)).IsFalse();
            AssertThat(hud.StocksLostText(0))
                .IsEqual(string.Format(
                    TranslationServer.Translate("fighter_hud_stocks_lost").ToString(), 0));

            FighterRuntimeComponent runtime = Runtime();
            runtime.KnockoutsSuffered = 3;
            hud.ApplyPlayerState(0, State(), runtime, TickRate);
            AssertThat(hud.StocksLostText(0))
                .IsEqual(string.Format(
                    TranslationServer.Translate("fighter_hud_stocks_lost").ToString(), 3));
        } finally {
            hud.Free();
        }
    }

    /// <summary>
    /// F21 reserves the legacy Hybrid ordinal 2 and normalizes it to Stock. An
    /// unknown mode must therefore keep the pips rather than guess Time: showing
    /// pips in a mode that has them is merely redundant, while hiding them in
    /// Stock hides the elimination rule.
    /// </summary>
    [TestCase]
    public void TheModeSwitchIsExplicitAndAnUnknownModeKeepsThePips() {
        AssertThat((int)MatchMode.Stock).IsEqual(0);
        AssertThat((int)MatchMode.TimeLimit).IsEqual(1);

        AssertThat(FighterHudModel.UsesStocksLostDisplay((int)MatchMode.TimeLimit)).IsTrue();
        AssertThat(FighterHudModel.UsesStocksLostDisplay((int)MatchMode.Stock)).IsFalse();
        // Legacy Hybrid (2) and anything unrecognised.
        AssertThat(FighterHudModel.UsesStocksLostDisplay(2)).IsFalse();
        AssertThat(FighterHudModel.UsesStocksLostDisplay(99)).IsFalse();
    }

    // ---- helpers --------------------------------------------------------

    private static FighterHUD Mount() {
        var packed = ResourceLoader.Load<PackedScene>(FighterHUD.ScenePath);
        var hud = packed.Instantiate<FighterHUD>();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(hud);
        return hud;
    }

    private static FighterStateComponent State(int stocks = 3) => new() {
        CurrentHP = 100,
        MaxHP = 100,
        Stocks = stocks,
        BlockCharges = 3,
        Influence = FP64.FromFloat(0f)
    };

    private static FighterRuntimeComponent Runtime() => new() {
        StatusType = (int)StatusType.None
    };
}
