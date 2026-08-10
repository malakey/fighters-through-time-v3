using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for Tesla's canonical Wardenclyffe
/// Cataclysm ultimate (audit gap X7): full-meter dispatch that consumes the
/// meter without a generic melee double-hit, the owner-centered multi-hit
/// column (zone type 53) landing the authored 4 x 18 total with a vortex-style
/// pull, the chain-reaction detonation of every live coil, shield bypass, and
/// snapshot/rollback convergence across the whole ultimate window.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TeslaUltimateTests {

    private static readonly FP64 FullMeter = FP64.FromInt(100);

    /// <summary>
    /// The ultimate tests churn large deterministic-simulation allocations
    /// alongside throwaway Godot resource instances. Draining pending
    /// finalizers here keeps them from racing a later suite's ResourceLoader
    /// work, which can trip the engine's script-instance dispose FATAL mid-load.
    /// </summary>
    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [TestCase]
    public void CataclysmConsumesTheMeterWithoutAGenericMeleeDoubleHit() {
        var simulation = NewSimulation(withCoils: false, seed: 71);
        int tick = FillMeter(simulation, 0);
        // FillMeter already settles swings and hitstun; a further pad keeps a
        // clean gap before the ultimate press.
        tick = AdvanceNeutral(simulation, tick, 30);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        AssertThat(before.HitstunFrames).IsEqual(0);

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));

        // The bespoke dispatch consumed the full meter and spawned the
        // ultimate-slot column zone. The same-frame column tick re-credits its
        // 18 damage as influence, so the meter reads exactly 18 — not 100.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
        AssertThat(attacker.Influence == FP64.FromInt(18)).IsTrue();
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual((int)FighterCharacterID.Tesla * 10 + 3);

        // Exactly one 18-damage column tick landed on the press frame; the
        // generic melee ultimate (30 hitstun frames + knockback) must not have
        // double-fired on the same press.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(before.CurrentHP - target.CurrentHP).IsEqual(18);
        AssertThat(target.HitstunFrames).IsEqual(0);
        AssertThat(target.BlockCharges).IsEqual(3);
    }

    [TestCase]
    public void CataclysmColumnLandsTheAuthoredMultiHitTotalAndPullsTheOpponent() {
        var simulation = NewSimulation(withCoils: false, seed: 72);
        int tick = FillMeter(simulation, 0);
        tick = SeparateBeyondMeleeRange(simulation, tick);

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        FP64 gapBefore = FP64.Abs(before.Position.x - attacker.Position.x);

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        tick++;

        // The vortex-style pull drags the opponent toward the column center.
        tick = AdvanceNeutral(simulation, tick, 60);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attackerMid)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent pulled)).IsTrue();
        AssertThat(FP64.Abs(pulled.Position.x - attackerMid.Position.x) < gapBefore).IsTrue();

        // Over the full 120-frame column lifetime the authored multi-hit total
        // lands: 4 hits x 18 per-hit damage = 72.
        AdvanceNeutral(simulation, tick, 70);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(before.CurrentHP - after.CurrentHP).IsEqual(72);
        AssertThat(simulation.ZoneCount).IsEqual(0);
    }

    [TestCase]
    public void CataclysmConnectsBeyondMeleeRange() {
        var simulation = NewSimulation(withCoils: false, seed: 73);
        int tick = FillMeter(simulation, 0);
        tick = SeparateBeyondMeleeRange(simulation, tick);

        // The generic ultimate is gated to melee range (2 units); the bespoke
        // dispatch is not. At a gap beyond 2 units the press must still fire.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        AssertThat(FP64.Abs(before.Position.x - attacker.Position.x) > FP64.FromInt(2)).IsTrue();

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));

        // Meter consumed (only the same-frame tick's 18-damage credit remains).
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent spent)).IsTrue();
        AssertThat(spent.Influence == FP64.FromInt(18)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(before.CurrentHP - target.CurrentHP).IsEqual(18);
    }

    [TestCase]
    public void CataclysmDetonatesEveryActiveCoilInAChainReaction() {
        var simulation = NewSimulation(withCoils: true, seed: 74);
        int tick = FillMeter(simulation, 0);
        tick = AdvanceNeutral(simulation, tick, 30);

        // Deploy both coils just before the ultimate (their 2 s arc cooldown
        // means no arcs fire in this window).
        simulation.Advance(Frame(tick, 0, GameplayButtons.Special1), Frame(tick, 0, GameplayButtons.None));
        tick++;
        simulation.Advance(Frame(tick, 0, GameplayButtons.Special1), Frame(tick, 0, GameplayButtons.None));
        tick++;
        AssertThat(simulation.PersistentObjectCount).IsEqual(2);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));

        // Both coils are gone and the chain damage landed alongside the first
        // column tick: 18 + 2 coils x 10 (double the 5-damage arc) = 38.
        AssertThat(simulation.PersistentObjectCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(before.CurrentHP - after.CurrentHP).IsEqual(38);
        // The coil detonation carries real impulse (unlike the column ticks).
        AssertThat(after.HitstunFrames > 0).IsTrue();
    }

    [TestCase]
    public void CataclysmBypassesBlock() {
        var simulation = NewSimulation(withCoils: true, seed: 75);
        int tick = FillMeter(simulation, 0);

        // The target settles into a front-facing block before the detonation
        // (long enough for any lingering fill hitstun to expire).
        for (int i = 0; i < 30; i++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.Block));
            tick++;
        }
        simulation.Advance(Frame(tick, 0, GameplayButtons.Special1), Frame(tick, 0, GameplayButtons.Block));
        tick++;
        simulation.Advance(Frame(tick, 0, GameplayButtons.Special1), Frame(tick, 0, GameplayButtons.Block));
        tick++;
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        AssertThat(before.BlockCharges).IsEqual(3);

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.Block));

        // Ultimate-class hits ignore the shield entirely: full damage (column
        // tick + both coil detonations) with no block charge spent.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.BlockCharges).IsEqual(3);
        AssertThat(before.CurrentHP - after.CurrentHP).IsEqual(38);
    }

    [TestCase]
    public void CataclysmIsSnapshotAndRollbackSafe() {
        FighterLoadout tesla = FighterLoadoutFactory.FromCharacterData(BuildUltimateCharacter(withCoils: true));
        FighterLoadout opponent = FighterLoadoutFactory.FromCharacterData(BuildTankOpponent());
        var uninterrupted = new FighterSimulation(
            tesla, opponent, seed: 76, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            tesla, opponent, seed: 76, spawnDistance: 1, rules: FighterMatchRules.Disabled);

        int tick = FillMeter(uninterrupted, 0);
        tick = AdvanceNeutral(uninterrupted, tick, 30);
        uninterrupted.Advance(Frame(tick, 0, GameplayButtons.Special1), Frame(tick, 0, GameplayButtons.None));
        tick++;
        uninterrupted.Advance(Frame(tick, 0, GameplayButtons.Special1), Frame(tick, 0, GameplayButtons.None));
        tick++;

        // Snapshot just before the ultimate so the restored simulation replays
        // the dispatch, the zone lifecycle, and the coil destruction.
        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        long expected = uninterrupted.Advance(
            Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        long actual = restored.Advance(
            Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        AssertThat(actual).IsEqual(expected);
        tick++;

        for (int end = tick + 200; tick < end; tick++) {
            expected = uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            actual = restored.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    private static FighterSimulation NewSimulation(bool withCoils, int seed) => new(
        FighterLoadoutFactory.FromCharacterData(BuildUltimateCharacter(withCoils)),
        FighterLoadoutFactory.FromCharacterData(BuildTankOpponent()),
        seed: seed,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    /// <summary>
    /// Fills the attacker's ultimate meter deterministically by landing basic
    /// hits (1 influence point per HP of damage dealt) while both fighters walk
    /// toward each other. The high-HP opponent absorbs the build-up safely.
    /// </summary>
    private static int FillMeter(FighterSimulation simulation, int startTick) {
        int tick = startTick;
        for (int limit = startTick + 900; tick < limit; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            if (attacker.Influence >= FullMeter) break;
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent defender)).IsTrue();
            sbyte toward = defender.Position.x >= attacker.Position.x ? (sbyte)127 : (sbyte)-127;
            // Basics are phased swings: press only when the string is idle.
            // Held movement no longer cancels recovery — each swing runs its
            // full length plus the chain-hold window, whose expiry resets the
            // string, so every press is still a fresh 0.8x opener. The mutual
            // walk-in (attackers steer freely mid-swing now) keeps the pair
            // inside melee range.
            GameplayButtons buttons = runtime.AttackPhase == FighterBasicAttackRules.PhaseNone
                ? GameplayButtons.BasicAttack
                : GameplayButtons.None;
            simulation.Advance(
                Frame(tick, toward, buttons),
                Frame(tick, (sbyte)(-toward), GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent filled)).IsTrue();
        AssertThat(filled.Influence >= FullMeter).IsTrue();
        // Settle so no swing, chain window, or hitstun leaks into the scenario.
        return BasicStringTestDriver.SettleToNeutral(simulation, tick);
    }

    /// <summary>
    /// Walks both fighters apart until the gap exceeds the 2-unit generic melee
    /// range (staying inside the column zone's 3-unit horizontal reach). Both
    /// retreat so the separation cannot stall against an arena wall.
    /// </summary>
    private static int SeparateBeyondMeleeRange(FighterSimulation simulation, int startTick) {
        int tick = startTick;
        for (int limit = startTick + 300; tick < limit; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent defender)).IsTrue();
            if (FP64.Abs(defender.Position.x - attacker.Position.x) > FP64.FromDouble(2.05)) break;
            sbyte away = defender.Position.x >= attacker.Position.x ? (sbyte)127 : (sbyte)-127;
            simulation.Advance(
                Frame(tick, (sbyte)(-away), GameplayButtons.None),
                Frame(tick, away, GameplayButtons.None));
        }
        // Two settle frames so the retreat momentum dies before the press.
        return AdvanceNeutral(simulation, tick, 2);
    }

    private static int AdvanceNeutral(FighterSimulation simulation, int startTick, int frames) {
        int tick = startTick;
        for (int i = 0; i < frames; i++, tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        return tick;
    }

    private static CharacterData BuildUltimateCharacter(bool withCoils) => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        // High basic damage fills the 100-point meter in three hits.
        BasicAttackDamage = 50f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = withCoils
            ? new AbilityData {
                ExecutionType = AbilityExecutionType.PersistentObject,
                BaseDamage = 5f,
                PersistentObjectID = "tesla_coil",
                MaxActiveObjects = 2,
                Lifetime = 30f,
                CooldownDuration = 0f
            }
            : new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        // The authored ultimate.tres numbers: 18 per hit x 4 hits every 30 frames.
        UltimateAttack = new AbilityData {
            BaseDamage = 18f,
            IsMultiHit = true,
            HitCount = 4,
            DamageTickIntervalFrames = 30,
            KnockbackForce = new Vector2(6f, -4f)
        }
    };

    private static CharacterData BuildTankOpponent() => new() {
        CharacterID = "joan",
        MaxHP = 500,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
