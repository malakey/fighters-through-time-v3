using FTT.Core;
using FTT.FighterSim;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Drives the shared three-hit basic string for deterministic suites. Basics
/// are phased swings (FTT.Combat.BasicComboRules): a press starts a swing, the
/// next hit chains from the post-recovery hold window, and a suite must settle
/// the string back to idle before scripting its next scenario. Suites that
/// used to mash BasicAttack against the old instant-hit model route their
/// meter charging through this driver instead.
/// </summary>
internal static class BasicStringTestDriver {

    /// <summary>
    /// Lands <paramref name="hits"/> chained basic hits from player one on a
    /// stationary player two (0.8x / 1.0x / 1.5x progression), then settles
    /// until the string is idle and all hitstun has expired. Returns the next
    /// free input tick. Callers keep both fighters inside the 2-unit melee
    /// range (zero-knockback attackers or adjacent spawns).
    /// </summary>
    public static int LandChainedBasics(FighterSimulation simulation, int startTick, int hits) {
        int tick = ApproachMeleeRange(simulation, startTick);
        int swingsStarted = 0;
        for (int limit = startTick + 600; tick < limit && swingsStarted < hits; tick++) {
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            bool press = swingsStarted == 0
                ? runtime.AttackPhase == FighterBasicAttackRules.PhaseNone
                : runtime.AttackPhase == FighterBasicAttackRules.PhaseChainHold;
            Advance(simulation, tick, press);
            if (press) swingsStarted++;
        }
        AssertThat(swingsStarted)
            .OverrideFailureMessage("The basic string never reached the requested chain length.")
            .IsEqual(hits);
        return SettleToNeutral(simulation, tick);
    }

    /// <summary>
    /// Walks player one toward player two until the gap sits inside the
    /// shortest authored basic reach (V7.1 string profiles: Lincoln at 1.7
    /// units), so suites that spawn at the template's 2-unit kiss range still
    /// connect with every character's string. No-op when already close.
    /// Returns the next free input tick.
    /// </summary>
    private static int ApproachMeleeRange(FighterSimulation simulation, int startTick) {
        int tick = startTick;
        for (int limit = startTick + 300; tick < limit; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
            if (FP64.Abs(target.Position.x - attacker.Position.x) <= FP64.FromDouble(1.4)) return tick;
            sbyte toward = target.Position.x >= attacker.Position.x ? (sbyte)127 : (sbyte)-127;
            simulation.Advance(
                new PlayerInputFrame { Tick = (uint)tick, MoveX = toward },
                new PlayerInputFrame { Tick = (uint)tick });
        }
        return tick;
    }

    /// <summary>
    /// Advances neutral frames until player one's string is fully idle and
    /// neither fighter carries hitstun, so later assertions see only the
    /// scenario under test. Returns the next free input tick.
    /// </summary>
    public static int SettleToNeutral(FighterSimulation simulation, int startTick) {
        int tick = startTick;
        for (int limit = startTick + 300; tick < limit; tick++) {
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent targetRuntime)).IsTrue();
            if (runtime.AttackPhase == FighterBasicAttackRules.PhaseNone
                && attacker.HitstunFrames == 0
                && target.HitstunFrames == 0
                // V7.1 string riders: a charging finisher can leave a status on
                // the target (Cleopatra's venom mark, Tesla's Static Charge);
                // wait those out too so later HP assertions see only the
                // scenario under test.
                && targetRuntime.StatusFrames == 0
                && targetRuntime.DamageStatusFrames == 0) {
                return tick;
            }
            Advance(simulation, tick, press: false);
        }
        return tick;
    }

    private static void Advance(FighterSimulation simulation, int tick, bool press) {
        GameplayButtons buttons = press ? GameplayButtons.BasicAttack : GameplayButtons.None;
        simulation.Advance(
            new PlayerInputFrame { Tick = (uint)tick, Held = buttons, Pressed = buttons },
            new PlayerInputFrame { Tick = (uint)tick });
    }
}
