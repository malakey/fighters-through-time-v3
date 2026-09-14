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
/// Sorrow ultimate (audit gap X7): the bespoke dispatch consumes the meter and
/// replaces the generic melee-range hit, the stage-wide zone lands the
/// authored 10 x 8 meteor bombardment, Mozart hovers on FloatFrames for the
/// window, the bombardment reaches beyond melee range, bypasses block, and
/// stays snapshot/rollback safe.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MozartUltimateTests {

    private const int BombardmentFrames = 180;
    private const int MeteorDamage = 8;
    private const int MeteorCount = 10;

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
    public void SymphonyConsumesTheMeterAndLandsTheAuthoredBombardmentTotal() {
        var simulation = NewSimulation(seed: 91, spawnDistance: 1);
        int tick = ChargeInfluence(simulation);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hpBefore = before.CurrentHP;

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        tick++;

        // Bespoke dispatch fired: the meter was consumed (only the first
        // same-frame meteor's 8 damage has re-credited it) and the stage-wide
        // bombardment zone (type 73 = Mozart 7 * 10 + ultimate slot 3) is live.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        // V7.6 D03h (Package 11 A1b): Ultimate-origin damage awards its
        // caster ZERO damage-dealt meter, so the same-frame tick no longer
        // re-credits anything - the meter reads exactly 0 after the cast.
        AssertThat(caster.Influence.RawValue).IsEqual(FP64.Zero.RawValue);
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual(73);

        for (int i = 0; i < BombardmentFrames + 30; i++, tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }

        // Exactly the authored 10 x 8 = 80 total: the generic melee ultimate
        // (another 8 at this range) must NOT have double-fired on the press,
        // and the zone must have expired after its 10th meteor.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(hpBefore - target.CurrentHP).IsEqual(MeteorDamage * MeteorCount);
        AssertThat(simulation.ZoneCount).IsEqual(0);
    }

    [TestCase]
    public void SymphonyGrantsTheHoverFloatWindowForTheBombardment() {
        var simulation = NewSimulation(seed: 92, spawnDistance: 1);
        int tick = ChargeInfluence(simulation);

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));

        // Mozart rises into the hover: airborne, moving up, with the
        // snapshotted FloatFrames window covering the bombardment.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.IsGrounded).IsEqual(0);
        AssertThat(caster.Velocity.y > FP64.Zero).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.FloatFrames >= BombardmentFrames - 1).IsTrue();
        AssertThat(runtime.FloatFrames <= BombardmentFrames).IsTrue();
    }

    [TestCase]
    public void SymphonyFiresBeyondMeleeRangeAndTheFinalMeteorLaunches() {
        var simulation = NewSimulation(seed: 93, spawnDistance: 1);
        int tick = ChargeInfluence(simulation);

        // Walk Mozart away until the pair is beyond the 2-unit generic melee
        // range; the bombardment must still fire and cover the opponent.
        for (int i = 0; i < 30; i++, tick++) {
            simulation.Advance(Frame(tick, -127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent opponent)).IsTrue();
        AssertThat(FP64.Abs(opponent.Position.x - caster.Position.x) > FP64.FromInt(2)).IsTrue();
        int hpBefore = opponent.CurrentHP;

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        tick++;
        // Meter consumed (only the first same-frame meteor has re-credited it)
        // even though the generic melee ultimate could never reach from here.
        AssertThat(simulation.TryGetFighter(0, out caster)).IsTrue();
        // V7.6 D03h (Package 11 A1b): Ultimate-origin damage awards its
        // caster ZERO damage-dealt meter, so the same-frame tick no longer
        // re-credits anything - the meter reads exactly 0 after the cast.
        AssertThat(caster.Influence.RawValue).IsEqual(FP64.Zero.RawValue);
        AssertThat(simulation.ZoneCount).IsEqual(1);

        // Track the final meteor: the last pulse carries the authored ultimate
        // knockback (magnitude 5 against weight 1 resolves to 2.5 units/s),
        // where every earlier meteor is an impulse-free pin. The full 80-damage
        // total identifies the frame the closing strike lands on.
        bool sawFinalLaunch = false;
        for (int i = 0; i < BombardmentFrames + 30; i++, tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            if (!sawFinalLaunch
                && simulation.TryGetFighter(1, out FighterStateComponent struck)
                && hpBefore - struck.CurrentHP == MeteorDamage * MeteorCount) {
                sawFinalLaunch = FP64.Abs(struck.Velocity.x) >= FP64.FromInt(2);
                AssertThat(sawFinalLaunch).IsTrue();
            }
        }

        AssertThat(sawFinalLaunch).IsTrue();
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(hpBefore - target.CurrentHP).IsEqual(MeteorDamage * MeteorCount);
    }

    [TestCase]
    public void SymphonyBypassesBlockWithoutSpendingBlockCharges() {
        var simulation = NewSimulation(seed: 94, spawnDistance: 1);
        int tick = ChargeInfluence(simulation);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hpBefore = before.CurrentHP;
        int chargesBefore = before.BlockCharges;

        simulation.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.Block));
        tick++;
        for (int i = 0; i < BombardmentFrames + 30; i++, tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.Block));
        }

        // Ultimates bypass the shield entirely: full bombardment damage lands
        // and the held block never spends a charge against it.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(hpBefore - target.CurrentHP).IsEqual(MeteorDamage * MeteorCount);
        AssertThat(target.BlockCharges).IsEqual(chargesBefore);
    }

    [TestCase]
    public void SymphonyBombardmentIsSnapshotAndRollbackSafe() {
        var uninterrupted = NewSimulation(seed: 95, spawnDistance: 1);
        var restored = NewSimulation(seed: 95, spawnDistance: 1);

        int tick = ChargeInfluence(uninterrupted);
        uninterrupted.Advance(Frame(tick, 0, GameplayButtons.Ultimate), Frame(tick, 0, GameplayButtons.None));
        tick++;

        // Snapshot mid-bombardment: hover active, meteors still falling.
        for (int i = 0; i < 60; i++, tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(uninterrupted.ZoneCount).IsEqual(1);

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // Resimulation across the remaining meteors, the final launch, the zone
        // expiry, and the float decay must converge tick-for-tick.
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
            BaseDamage = 8f,
            IsMultiHit = true,
            HitCount = 10,
            DamageTickIntervalFrames = 18,
            KnockbackForce = new Vector2(5f, -4f),
            Lifetime = 3f
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
