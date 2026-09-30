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
/// Cataclysm ultimate (T03, Package 13 W6): the press spends the meter and
/// fires the six-unit teleforce beam (A02); its contact raises the column
/// (zone type 53) on the held victim — five 10-damage AC strikes, then the
/// 20-damage final strike (70) — and detonates every live coil for
/// <see cref="UltimateActivationRules.CoilDetonationDamage"/> each when the
/// victim is inside that coil's arc radius (80 with both), destroying them. A
/// whiffed beam detonates nothing. Shield bypass and snapshot/rollback
/// convergence across the whole window.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TeslaUltimateTests {

    private static readonly FP64 FullMeter = FP64.FromInt(100);
    private const int StrikeDamage = 10;
    private const int StrikeCount = 5;
    private const int FinaleDamage = 20;
    private const int BaseTotal = StrikeDamage * StrikeCount + FinaleDamage;

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
    public void TheBeamContactRaisesTheColumnAndSpendsTheMeter() {
        var simulation = NewSimulation(withCoils: false, seed: 71);
        int tick = FillMeter(simulation, 0);
        tick = AdvanceNeutral(simulation, tick, 30);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        AssertThat(before.HitstunFrames).IsEqual(0);

        UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();

        // D03h: spent on acceptance, no re-credit.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
        AssertThat(attacker.Influence == FP64.Zero).IsTrue();
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual((int)FighterCharacterID.Tesla * 10 + 3);

        // Exactly one impulse-free 10-damage column strike landed on contact.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(before.CurrentHP - target.CurrentHP).IsEqual(StrikeDamage);
        AssertThat(target.HitstunFrames).IsEqual(0);
        AssertThat(target.BlockCharges).IsEqual(3);
    }

    [TestCase]
    public void CataclysmLandsSeventyWithoutCoilsAndHoldsTheVictim() {
        var simulation = NewSimulation(withCoils: false, seed: 72);
        int tick = FillMeter(simulation, 0);
        tick = SeparateBeyondMeleeRange(simulation, tick);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent anchored)).IsTrue();

        // The victim is held on the column while the strikes land.
        tick = AdvanceNeutral(simulation, tick, 60);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent held)).IsTrue();
        AssertThat(held.Position.x).IsEqual(anchored.Position.x);

        // T03: five strikes x 10, then the 20-damage final strike = 70.
        AdvanceNeutral(simulation, tick, 120);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(before.CurrentHP - after.CurrentHP).IsEqual(BaseTotal);
        AssertThat(simulation.ZoneCount).IsEqual(0);
    }

    [TestCase]
    public void TheBeamConnectsBeyondMeleeRange() {
        var simulation = NewSimulation(withCoils: false, seed: 73);
        int tick = FillMeter(simulation, 0);
        tick = SeparateBeyondMeleeRange(simulation, tick);

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        AssertThat(FP64.Abs(before.Position.x - attacker.Position.x) > FP64.FromInt(2)).IsTrue();

        UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent spent)).IsTrue();
        AssertThat(spent.Influence == FP64.Zero).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(before.CurrentHP - target.CurrentHP).IsEqual(StrikeDamage);
    }

    [TestCase]
    public void CataclysmDetonatesEveryActiveCoilForFiveEach() {
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

        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();

        // Both coils are destroyed and their detonations landed with the first
        // column strike: 10 + 2 coils x 5 = 20.
        AssertThat(simulation.PersistentObjectCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent atContact)).IsTrue();
        AssertThat(before.CurrentHP - atContact.CurrentHP)
            .IsEqual(StrikeDamage + 2 * UltimateActivationRules.CoilDetonationDamage);

        // T03: the maximum is 80.
        AdvanceNeutral(simulation, tick, 180);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(before.CurrentHP - after.CurrentHP)
            .IsEqual(BaseTotal + 2 * UltimateActivationRules.CoilDetonationDamage);
    }

    [TestCase]
    public void AWhiffedBeamDetonatesNothing() {
        var simulation = NewSimulation(withCoils: true, seed: 77);
        int tick = FillMeter(simulation, 0);
        tick = AdvanceNeutral(simulation, tick, 30);
        simulation.Advance(Frame(tick, 0, GameplayButtons.Special1), Frame(tick, 0, GameplayButtons.None));
        tick++;
        simulation.Advance(Frame(tick, 0, GameplayButtons.Special1), Frame(tick, 0, GameplayButtons.None));
        tick++;
        AssertThat(simulation.PersistentObjectCount).IsEqual(2);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        // The opponent jumps over the straight beam as it fires.
        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        tick++;
        simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.Jump));
        tick++;
        AdvanceNeutral(simulation, tick, 35);

        AssertThat(UltimateActivationTestKit.Phase(simulation, 0))
            .IsEqual(FighterUltimateActivationRules.PhaseWhiffRecovery);
        AssertThat(simulation.PersistentObjectCount).IsEqual(2);
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.CurrentHP).IsEqual(before.CurrentHP);
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

        UltimateActivationTestKit.CastAndConnect(
            simulation, tick, out bool connected, victimHeld: GameplayButtons.Block);
        AssertThat(connected).IsTrue();

        // Ultimate-class hits ignore the shield entirely: full damage (column
        // strike + both coil detonations) with no block charge spent.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.BlockCharges).IsEqual(3);
        AssertThat(before.CurrentHP - after.CurrentHP)
            .IsEqual(StrikeDamage + 2 * UltimateActivationRules.CoilDetonationDamage);
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
        // the acceptance, the beam, the contact, the column and the coil
        // destruction.
        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        long expected = uninterrupted.Advance(
            Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        long actual = restored.Advance(
            Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        AssertThat(actual).IsEqual(expected);
        tick++;

        for (int end = tick + 260; tick < end; tick++) {
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
    /// range (well inside the beam's six units), then Tesla turns back to face
    /// the opponent.
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
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent tesla)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent opponent)).IsTrue();
        sbyte face = opponent.Position.x >= tesla.Position.x ? (sbyte)127 : (sbyte)-127;
        simulation.Advance(Frame(tick, face, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        tick++;
        // Settle frames so the retreat momentum dies before the press.
        return AdvanceNeutral(simulation, tick, 20);
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
        // The authored ultimate.tres numbers: 5 strikes x 10 every 30 frames,
        // then the 20-damage final strike; a straight six-unit beam.
        UltimateAttack = new AbilityData {
            BaseDamage = StrikeDamage,
            IsMultiHit = true,
            HitCount = StrikeCount,
            DamageTickIntervalFrames = 30,
            FinaleDamage = FinaleDamage,
            FinaleLaunches = true,
            ActivationShape = UltimateActivationShape.Projectile,
            ActivationRange = 360f,
            ActivationHitboxSize = new Vector2(60f, 30f),
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
