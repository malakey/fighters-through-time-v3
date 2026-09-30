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
/// Deterministic Fighter-side coverage for Mozart's canonical Symphony of
/// Sorrow ultimate (M05, Package 13 W6): the press spends the meter and flicks
/// the baton's six-unit sound bolt (A02); its contact starts the bombardment
/// zone on the held victim — eight 7-damage piano keys and then the 24-damage
/// grand-chord finale (80) — while Mozart hovers on FloatFrames. The finale
/// launches, reaches beyond melee range, bypasses block, and the whole
/// sequence stays snapshot/rollback safe.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MozartUltimateTests {

    private const int KeyDamage = 7;
    private const int KeyCount = 8;
    private const int FinaleDamage = 24;
    private const int KeyIntervalFrames = 18;
    private const int CinematicFrames = KeyCount * KeyIntervalFrames;
    private const int UltimateTotal = KeyDamage * KeyCount + FinaleDamage;

    /// <summary>
    /// The ultimate tests churn large deterministic-simulation allocations
    /// alongside throwaway Godot resource instances. Draining pending
    /// finalizers keeps them from racing a later suite's ResourceLoader work,
    /// which can trip the engine's script-instance dispose FATAL mid-load.
    /// </summary>
    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [TestCase]
    public void SymphonySpendsTheMeterAndLandsTheKeysAndFinale() {
        var simulation = NewSimulation(seed: 91, spawnDistance: 1);
        int tick = ChargeInfluence(simulation);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hpBefore = before.CurrentHP;

        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();

        // The meter was spent on acceptance (D03h: no re-credit) and the
        // bombardment zone (type 73 = Mozart 7 * 10 + ultimate slot 3) is live.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence.RawValue).IsEqual(FP64.Zero.RawValue);
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual(73);

        UltimateActivationTestKit.Idle(simulation, tick, CinematicFrames + 30);

        // M05: 8 keys x 7 + the 24-damage finale = 80, and the zone is gone.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(hpBefore - target.CurrentHP).IsEqual(UltimateTotal);
        AssertThat(simulation.ZoneCount).IsEqual(0);
    }

    [TestCase]
    public void SymphonyGrantsTheHoverFloatWindowForTheBombardment() {
        var simulation = NewSimulation(seed: 92, spawnDistance: 1);
        int tick = ChargeInfluence(simulation);

        UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();

        // Mozart rises into the hover: airborne, moving up, with the
        // snapshotted FloatFrames window covering the cinematic.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.IsGrounded).IsEqual(0);
        AssertThat(caster.Velocity.y > FP64.Zero).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.FloatFrames >= CinematicFrames - 1).IsTrue();
        AssertThat(runtime.FloatFrames <= CinematicFrames).IsTrue();
    }

    [TestCase]
    public void SymphonyConnectsBeyondMeleeRangeAndTheFinaleLaunches() {
        var simulation = NewSimulation(seed: 93, spawnDistance: 1);
        int tick = ChargeInfluence(simulation);

        // Walk Mozart away until the pair is beyond the 2-unit generic melee
        // range, then turn back: the sound bolt must still reach.
        for (int i = 0; i < 30; i++, tick++) {
            simulation.Advance(Frame(tick, -127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        simulation.Advance(Frame(tick, 127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        tick = UltimateActivationTestKit.Idle(simulation, tick + 1, 20);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent opponent)).IsTrue();
        AssertThat(FP64.Abs(opponent.Position.x - caster.Position.x) > FP64.FromInt(2)).IsTrue();
        int hpBefore = opponent.CurrentHP;

        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();

        // Track the finale: every key is an impulse-free pin; the finale carries
        // the authored ultimate knockback. The full total identifies the frame
        // it lands on.
        bool sawFinalLaunch = false;
        for (int i = 0; i < CinematicFrames + 30; i++, tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            if (!sawFinalLaunch
                && simulation.TryGetFighter(1, out FighterStateComponent struck)
                && hpBefore - struck.CurrentHP == UltimateTotal) {
                sawFinalLaunch = FP64.Abs(struck.Velocity.x) >= FP64.FromInt(2);
                AssertThat(sawFinalLaunch).IsTrue();
            }
        }

        AssertThat(sawFinalLaunch).IsTrue();
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(hpBefore - target.CurrentHP).IsEqual(UltimateTotal);
    }

    [TestCase]
    public void SymphonyBypassesBlockWithoutSpendingBlockCharges() {
        var simulation = NewSimulation(seed: 94, spawnDistance: 1);
        int tick = ChargeInfluence(simulation);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hpBefore = before.CurrentHP;
        int chargesBefore = before.BlockCharges;

        tick = UltimateActivationTestKit.CastAndConnect(
            simulation, tick, out bool connected, victimHeld: GameplayButtons.Block);
        AssertThat(connected).IsTrue();
        UltimateActivationTestKit.Idle(simulation, tick, CinematicFrames + 30, GameplayButtons.Block);

        // Ultimates bypass the shield entirely: the full total lands and the
        // held block never spends a charge against it.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(hpBefore - target.CurrentHP).IsEqual(UltimateTotal);
        AssertThat(target.BlockCharges).IsEqual(chargesBefore);
    }

    [TestCase]
    public void SymphonyBombardmentIsSnapshotAndRollbackSafe() {
        var uninterrupted = NewSimulation(seed: 95, spawnDistance: 1);
        var restored = NewSimulation(seed: 95, spawnDistance: 1);

        int tick = ChargeInfluence(uninterrupted);
        tick = UltimateActivationTestKit.CastAndConnect(uninterrupted, tick);

        // Snapshot mid-bombardment: hover active, keys still falling.
        tick = UltimateActivationTestKit.Idle(uninterrupted, tick, 60);
        AssertThat(uninterrupted.ZoneCount).IsEqual(1);

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // Resimulation across the remaining keys, the finale, the zone expiry,
        // and the float decay must converge tick-for-tick.
        for (int i = 0; i < 300; i++, tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    private static FighterSimulation NewSimulation(int seed, int spawnDistance) => new(
        FighterLoadoutFactory.FromCharacterData(BuildMozart()),
        FighterLoadoutFactory.FromCharacterData(BuildTankOpponent()),
        seed: seed,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);

    /// <summary>
    /// Charges Mozart's Influence to the full 100 by landing heavy basics on
    /// the adjacent tanky opponent (1 point per HP dealt), leaving both
    /// fighters idle and out of hitstun. Returns the next input tick.
    /// </summary>
    private static int ChargeInfluence(FighterSimulation simulation) {
        int tick = 0;
        for (int swing = 0; swing < 40; swing++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            if (attacker.Influence.RawValue >= FP64.FromInt(100).RawValue) break;
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent prey)).IsTrue();
            // Chase while swinging: pushbox jostling and hit pushback otherwise
            // drift the pair beyond the 2-unit basic range.
            sbyte chase = prey.Position.x >= attacker.Position.x ? (sbyte)127 : (sbyte)-127;
            simulation.Advance(Frame(tick, chase, GameplayButtons.BasicAttack), Frame(tick, 0, GameplayButtons.None));
            tick++;
            for (int cooldown = 0; cooldown < 20; cooldown++, tick++) {
                simulation.TryGetFighter(0, out attacker);
                simulation.TryGetFighter(1, out prey);
                chase = prey.Position.x >= attacker.Position.x ? (sbyte)127 : (sbyte)-127;
                simulation.Advance(Frame(tick, chase, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            }
        }
        // Settle back to an idle, grounded stance before the ultimate press.
        for (int i = 0; i < 30; i++, tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.RawValue >= FP64.FromInt(100).RawValue).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent sparPartner)).IsTrue();
        AssertThat(sparPartner.CurrentHP > 0).IsTrue();
        return tick;
    }

    private static CharacterData BuildMozart() => new() {
        CharacterID = "mozart",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 25f,
        BasicAttackKnockback = 0.5f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.Projectile,
            BaseDamage = 4f,
            IsMultiHit = true,
            HitCount = 3,
            KnockbackForce = new Vector2(3f, -2f),
            ProjectileSpeed = 280f,
            ProjectileLifetime = 5f,
            CooldownDuration = 10f
        },
        SpecialAttackTwo = new AbilityData {
            ExecutionType = AbilityExecutionType.Projectile,
            BaseDamage = 12f,
            KnockbackForce = new Vector2(8f, -2f),
            ProjectileSpeed = 250f,
            ProjectileLifetime = 6f,
            CooldownDuration = 10f
        },
        MovementAbility = new MovementAbilityData {
            MovementType = MovementType.Float,
            MovementDuration = 3f,
            CooldownDuration = 5f,
            PersistentObjectID = "sonata_platform",
            MaxActiveObjects = 1,
            Lifetime = 3f
        },
        UltimateAttack = new AbilityData {
            ExecutionType = AbilityExecutionType.Cinematic,
            BaseDamage = KeyDamage,
            IsMultiHit = true,
            HitCount = KeyCount,
            DamageTickIntervalFrames = KeyIntervalFrames,
            FinaleDamage = FinaleDamage,
            FinaleLaunches = true,
            KnockbackForce = new Vector2(5f, -4f),
            Lifetime = 2.7f
        }
    };

    /// <summary>
    /// A heavy sparring target with enough HP to absorb the meter-charging
    /// basics plus the full bombardment without losing a stock.
    /// </summary>
    private static CharacterData BuildTankOpponent() => new() {
        CharacterID = "joan",
        MaxHP = 400,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 7f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 8f,
        BasicAttackKnockback = 3f
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
