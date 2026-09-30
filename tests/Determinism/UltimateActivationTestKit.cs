using FTT.Core;
using FTT.FighterSim;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W6 (A02): shared driving helpers for the Ultimate suites. Not a
/// test suite. Since A02 an Ultimate press only ACCEPTS the Ultimate (meter
/// spent, hyper-armored wind-up); the character's cinematic starts on the
/// activation strike's contact. These helpers press and step until contact, so
/// every per-character suite measures the cinematic from the same tick.
/// </summary>
internal static class UltimateActivationTestKit {

    /// <summary>Safety bound: wind-up + active + a margin, far below the whiff recovery.</summary>
    private const int MaxFramesToContact = 60;

    public static PlayerInputFrame Input(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };

    public static PlayerInputFrame Holding(int tick, GameplayButtons held) => new() {
        Tick = (uint)tick,
        Held = held,
        Pressed = GameplayButtons.None
    };

    public static int Phase(FighterSimulation simulation, int playerID) =>
        simulation.TryGetFighterUltimateActivation(playerID, out FighterUltimateActivationComponent activation)
            ? activation.Phase
            : -1;

    public static bool IsCinematic(FighterSimulation simulation, int playerID) =>
        Phase(simulation, playerID) == FighterUltimateActivationRules.PhaseCinematic;

    /// <summary>
    /// Presses the Ultimate for <paramref name="casterID"/> at <paramref name="tick"/>
    /// and steps idle frames (the victim sends <paramref name="victimHeld"/>)
    /// until the activation strike connects. Returns the next input tick; the
    /// contact tick is the last one advanced. <paramref name="connected"/> is
    /// false if the strike whiffed.
    /// </summary>
    public static int CastAndConnect(
        FighterSimulation simulation, int tick, out bool connected,
        int casterID = 0, GameplayButtons victimHeld = GameplayButtons.None) {
        Advance(simulation, casterID, Input(tick, 0, GameplayButtons.Ultimate), Holding(tick, victimHeld));
        tick++;
        connected = false;
        for (int i = 0; i < MaxFramesToContact; i++) {
            Advance(simulation, casterID, Input(tick, 0, GameplayButtons.None), Holding(tick, victimHeld));
            tick++;
            if (IsCinematic(simulation, casterID)) {
                connected = true;
                break;
            }
            if (Phase(simulation, casterID) == FighterUltimateActivationRules.PhaseWhiffRecovery) break;
        }
        return tick;
    }

    /// <inheritdoc cref="CastAndConnect(FighterSimulation, int, out bool, int, GameplayButtons)"/>
    public static int CastAndConnect(FighterSimulation simulation, int tick) =>
        CastAndConnect(simulation, tick, out _);

    /// <summary>Steps <paramref name="frames"/> idle frames for both players.</summary>
    public static int Idle(FighterSimulation simulation, int tick, int frames,
        GameplayButtons victimHeld = GameplayButtons.None, int casterID = 0) {
        for (int i = 0; i < frames; i++) {
            Advance(simulation, casterID, Input(tick, 0, GameplayButtons.None), Holding(tick, victimHeld));
            tick++;
        }
        return tick;
    }

    private static void Advance(
        FighterSimulation simulation, int casterID, PlayerInputFrame caster, PlayerInputFrame victim) {
        if (casterID == 0) simulation.Advance(caster, victim);
        else simulation.Advance(victim, caster);
    }
}
