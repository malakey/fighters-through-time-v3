using System;
using FTT.Core;
using FTT.FighterSim;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Combat and movement update 2026-10-16 (decision 1.2, plan §2.1.6): shared
/// stepping helpers for suites that press a Fighter Special and read its
/// effect. Since the cast honours its authored startup, the effect appears on
/// T+S, not on the press tick, so a fixture steps through the startup first.
/// Not a test suite.
/// </summary>
internal static class SpecialCastTestKit {
    /// <summary>Safety bound: the longest authored startup is 16 frames.</summary>
    private const int MaxStartupTicks = 60;

    /// <summary>True once the player's current cast has fired its effect (or resolved its melee contact).</summary>
    public static bool HasFired(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent cast)).IsTrue();
        return FighterSpecialCastRules.HasFlag(in cast, FighterSpecialCastRules.FlagEffectFired)
            || FighterSpecialCastRules.HasFlag(in cast, FighterSpecialCastRules.FlagMeleeResolved);
    }

    /// <summary>True while the player is casting a clocked Special (Startup, Active or Recovery).</summary>
    public static bool IsCasting(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent cast)).IsTrue();
        return FighterSpecialCastRules.IsCasting(in cast);
    }

    /// <summary>
    /// True when the player's next unfrozen Advance is its cast's active entry —
    /// the last startup tick, <c>SpecialPhaseFrames == 1</c> — so a fixture can
    /// give the firing tick its own frames (an opponent's hop, say) with the
    /// offset it had when Specials fired on the press tick.
    /// </summary>
    public static bool FiresOnNextAdvance(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent cast)).IsTrue();
        return FighterSpecialCastRules.IsStartup(in cast) && cast.SpecialPhaseFrames == 1;
    }

    /// <summary>
    /// Steps neutral frames for both players from <paramref name="tick"/> until
    /// <paramref name="playerID"/>'s cast has fired its effect — its active
    /// entry, T+S. Returns the next tick. A cast that already fired returns
    /// <paramref name="tick"/> unchanged.
    /// </summary>
    public static int StepUntilFired(FighterSimulation simulation, int playerID, int tick) =>
        StepUntilFired(simulation, playerID, tick, t => (Neutral(t), Neutral(t)));

    /// <summary>
    /// <see cref="StepUntilFired(FighterSimulation, int, int)"/> with each
    /// tick's frames supplied by <paramref name="frames"/> (player one, player two).
    /// </summary>
    public static int StepUntilFired(
        FighterSimulation simulation, int playerID, int tick,
        Func<int, (PlayerInputFrame PlayerOne, PlayerInputFrame PlayerTwo)> frames) {
        for (int step = 0; step < MaxStartupTicks; step++) {
            if (HasFired(simulation, playerID)) return tick;
            AssertThat(IsCasting(simulation, playerID))
                .OverrideFailureMessage($"Harness: player {playerID} is not casting at tick {tick}.")
                .IsTrue();
            (PlayerInputFrame one, PlayerInputFrame two) = frames(tick);
            simulation.Advance(one, two);
            tick++;
        }
        AssertThat(HasFired(simulation, playerID))
            .OverrideFailureMessage($"Harness: player {playerID}'s cast never fired.")
            .IsTrue();
        return tick;
    }

    /// <summary>Steps neutral frames until <paramref name="playerID"/> is no longer casting. Returns the next tick.</summary>
    public static int StepUntilCastEnds(FighterSimulation simulation, int playerID, int tick) {
        for (int step = 0; step < 200 && IsCasting(simulation, playerID); step++, tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
        AssertThat(IsCasting(simulation, playerID)).IsFalse();
        return tick;
    }

    public static PlayerInputFrame Neutral(int tick) => new() { Tick = (uint)tick };
}
