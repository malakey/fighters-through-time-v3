using FTT.Combat;
using FTT.Core;
using FTT.Characters;
using FTT.FighterSim;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.Tests.Determinism;

/// <summary>
/// FU-KNOCK (2026-10-25): shared harness for the finisher, wall-bounce and
/// hitstun-shove suites. Not a suite itself (GdUnit runs one [TestSuite] per
/// file).
/// </summary>
internal static class KnockbackFeelTestKit {

    public static double D(FP64 v) => (double)v.RawValue / FP64.One.RawValue;

    public static PlayerInputFrame Frame(int tick, GameplayButtons buttons = GameplayButtons.None, sbyte moveX = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = buttons,
        Pressed = buttons
    };

    /// <summary>A Tesla-reach, weight-1 kit whose basic knockback is <paramref name="knockback"/>.</summary>
    public static CharacterData Attacker(float knockback) => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = knockback,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    /// <summary>A weight-1 victim with no knockback of its own.</summary>
    public static CharacterData Victim(int maxHP = 100) => new() {
        CharacterID = "joan",
        MaxHP = maxHP,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    /// <summary>
    /// Walks the string to string hit <paramref name="step"/> (0 to 2) against
    /// <paramref name="victimKit"/>, whiffing the earlier hits from four units
    /// apart (the finisher-separation protocol), and returns the simulation on
    /// the tick the target hit connects. Both fighters close in before the press.
    /// </summary>
    public static (FighterSimulation Simulation, int ConnectTick, int Damage) WalkToHit(
        FighterLoadout attackerKit, FighterLoadout victimKit, int step, int seed = 4500) {
        var sim = new FighterSimulation(attackerKit, victimKit, seed: seed, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        int swings = 0;
        bool pressedTarget = false;
        for (int tick = 0; tick < 700; tick++) {
            sim.TryGetFighterRuntime(0, out FighterRuntimeComponent rt);
            sim.TryGetFighter(0, out FighterStateComponent a);
            sim.TryGetFighter(1, out FighterStateComponent v);
            FP64 gap = v.Position.x - a.Position.x;
            bool press = false;
            sbyte closing = 0;
            bool ready = swings == 0
                ? rt.AttackPhase == FighterBasicAttackRules.PhaseNone
                : rt.AttackPhase == FighterBasicAttackRules.PhaseChainHold;
            if (swings < step) {
                press = ready;
            } else if (!pressedTarget) {
                bool safe = rt.AttackPhase == FighterBasicAttackRules.PhaseNone
                    || rt.AttackPhase == FighterBasicAttackRules.PhaseRecovery
                    || rt.AttackPhase == FighterBasicAttackRules.PhaseChainHold;
                if (gap > FP64.FromDouble(1.6)) {
                    if (safe) closing = 1;
                } else if (ready) {
                    press = true;
                    pressedTarget = true;
                }
            }
            int hpBefore = v.CurrentHP;
            sim.Advance(
                Frame(tick, press ? GameplayButtons.BasicAttack : GameplayButtons.None, (sbyte)(closing * 127)),
                Frame(tick, GameplayButtons.None, (sbyte)(closing * -127)));
            if (press) swings++;
            sim.TryGetFighter(1, out FighterStateComponent after);
            if (pressedTarget && after.CurrentHP < hpBefore) return (sim, tick, hpBefore - after.CurrentHP);
        }
        throw new System.InvalidOperationException("the string hit never connected");
    }

    public static void Idle(FighterSimulation sim, ref int tick) {
        sim.Advance(Frame(tick), Frame(tick));
        tick++;
    }
}
