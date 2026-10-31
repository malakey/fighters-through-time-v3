using FTT.Combat;
using FTT.FighterSim;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Meter and Defy update 2026-10-31: helpers for sim suites that press Specials
/// back to back. A fixture starts at two pips and every slot carries a 2 s
/// reuse delay, so a scripted run of presses starves or waits unless it tops the
/// gauge up first. Not a test suite.
/// </summary>
internal static class MomentumTestKit {

    /// <summary>
    /// Fills the player's gauge to four pips and clears both reuse delays, so
    /// the next scripted Special press is accepted whatever came before it.
    /// </summary>
    public static void KeepFull(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.SeedFighterMomentumForTest(playerID, BasicComboRules.MomentumMaxUnits)).IsTrue();
        AssertThat(simulation.SeedFighterSpecialReuseForTest(playerID, 0, 0)).IsTrue();
    }

    /// <summary>Fills both players' gauges and clears their reuse delays.</summary>
    public static void KeepFullBoth(FighterSimulation simulation) {
        KeepFull(simulation, 0);
        KeepFull(simulation, 1);
    }

    /// <summary>The player's gauge in units.</summary>
    public static int Units(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
        return verb.MomentumUnits;
    }
}
