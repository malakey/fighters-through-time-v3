using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for the canonical Leonardo kit: the
/// Golden Ratio spiral zone (3 impulse-free damage ticks then a radial
/// knockback pulse on expiry), the Clockwork Turret construct (20 HP, bolts at
/// 2 s, self-destruct after the 3rd bolt), the Ornithopter Flight glide float,
/// and rollback safety across the zone/turret lifecycles.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LeonardoKitTests {

    [TestCase]
    public void GoldenRatioZoneTicksThreeTimesThenPulsesRadialKnockback() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildSpiralCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 61,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        // Active entry (decision 1.2, 2026-10-16: after the authored startup):
        // the zone spawns owner-centered with Leonardo's identity and lands its
        // first impulse-free tick immediately.
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        int fired = SpecialCastTestKit.StepUntilFired(simulation, 0, 1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual((int)FighterCharacterID.Leonardo * 10 + 1);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterFirstTick)).IsTrue();
        AssertThat(afterFirstTick.CurrentHP).IsEqual(90);
        // Lingering-kit pass: tick 1 alone keeps a short hitstun (the later ticks hold by the pull).
        AssertThat(afterFirstTick.HitstunFrames).IsEqual(FighterSpiralRules.FirstTickHitstunFrames);

        // Ticks 2 and 3 land at the authored 0.5 s interval; still impulse-free.
        for (int tick = fired; tick < fired + 88; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterAllTicks)).IsTrue();
        AssertThat(afterAllTicks.CurrentHP).IsEqual(70);
        AssertThat(afterAllTicks.HitstunFrames).IsEqual(0);
        var xBeforeExpiry = afterAllTicks.Position.x;

        // The 1.5 s lifetime expires with a damage-free radial knockback pulse.
        int expiry = fired + 88;
        simulation.Advance(Frame(expiry, 0, GameplayButtons.None), Frame(expiry, 0, GameplayButtons.None));
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterExpiry)).IsTrue();
        AssertThat(afterExpiry.CurrentHP).IsEqual(70);
        AssertThat(afterExpiry.HitstunFrames > 0).IsTrue();

        // The target (right of the spiral center) is shoved farther right.
        for (int tick = expiry + 1; tick <= expiry + 21; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent shoved)).IsTrue();
        AssertThat(shoved.Position.x > xBeforeExpiry).IsTrue();
    }

    [TestCase]
    public void ClockworkTurretFiresFourOwnShotsAndOutlivesThem() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildTurretCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 62,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));
        // Decision 1.2 (2026-10-16): the turret deploys on the cast's active entry.
        int deployed = SpecialCastTestKit.StepUntilFired(simulation, 0, 1);
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent turret)).IsTrue();
        AssertThat(turret.ObjectTypeID).IsEqual(2);
        AssertThat(turret.MaxHP).IsEqual(20);
        AssertThat(turret.Damage).IsEqual(4);
        AssertThat(turret.RemainingAttacks).IsEqual(4);
        AssertThat(turret.MaxDeployLimit).IsEqual(1);

        // Lingering-kit pass: its own shot is 4 every 3 s (frames ~180/361/542/723) and spends
        // no charge, so after the fourth shot the turret is still up (the old one self-destructed).
        for (int tick = deployed; tick < deployed + 800; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.PersistentObjectCount).IsEqual(1);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(100 - 4 * 4);
    }

    [TestCase]
    public void OrnithopterGlideGrantsAuthoredFloatFrames() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildGlideCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 63,
            spawnDistance: 4,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.MovementAbility), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent boosted)).IsTrue();
        AssertThat(boosted.IsGrounded).IsEqual(0);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.FloatFrames >= 170).IsTrue();

        // The reduced-gravity float expires after the authored 3 s (180 frames).
        for (int tick = 1; tick <= 200; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent settled)).IsTrue();
        AssertThat(settled.FloatFrames).IsEqual(0);
    }

    [TestCase]
    public void SpiralAndTurretLifecyclesAreSnapshotAndRollbackSafe() {
        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(BuildFullKitCharacter());
        var uninterrupted = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 64, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 64, spawnDistance: 1, rules: FighterMatchRules.Disabled);

        // Spiral, then the turret the tick the spiral's ability lockout ends.
        int turretTick = BasicComboRules.AbilityLockoutFrames;
        uninterrupted.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick < turretTick; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        uninterrupted.Advance(Frame(turretTick, 0, GameplayButtons.Special2), Frame(turretTick, 0, GameplayButtons.None));
        for (int tick = turretTick + 1; tick < 60; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
        }
        // Both are live in the captured state: the 1.5 s spiral and the turret.
        AssertThat(uninterrupted.ZoneCount).IsEqual(1);
        AssertThat(uninterrupted.PersistentObjectCount).IsEqual(1);

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // The replayed window spans the spiral's expiry knockback and all four
        // turret bolts (one per 2 s from the deploy) plus the turret self-destruct.
        for (int tick = 60; tick < 560; tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
        // The turret no longer self-destructs after its own shots: it is still up inside its 15 s life.
        AssertThat(restored.PersistentObjectCount).IsEqual(1);
    }

    private static CharacterData BuildSpiralCharacter() => new() {
        CharacterID = "leonardo",
        MaxHP = 95,
        Weight = 0.9f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 7.5f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 9f,
        BasicAttackKnockback = 2.5f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.Area,
            BaseDamage = 10f,
            IsMultiHit = true,
            HitCount = 3,
            DamageTickIntervalFrames = 30,
            Lifetime = 1.5f,
            KnockbackForce = new Vector2(3, -2),
            CooldownDuration = 10f
        },
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 12f }
    };

    private static CharacterData BuildTurretCharacter() => new() {
        CharacterID = "leonardo",
        MaxHP = 95,
        Weight = 0.9f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 7.5f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 9f,
        BasicAttackKnockback = 2.5f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData {
            ExecutionType = AbilityExecutionType.PersistentObject,
            // The own shot as leonardo/special_2.tres authors it: 4 every 180 frames.
            BaseDamage = 4f,
            DamageTickIntervalFrames = 180,
            PersistentObjectID = "clockwork_turret",
            MaxActiveObjects = 1,
            Lifetime = 15f,
            KnockbackForce = new Vector2(2, -1),
            CooldownDuration = 10f
        },
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 12f }
    };

    private static CharacterData BuildGlideCharacter() => new() {
        CharacterID = "leonardo",
        MaxHP = 95,
        Weight = 0.9f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 7.5f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 9f,
        BasicAttackKnockback = 2.5f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData {
            MovementType = MovementType.Glide,
            MovementDuration = 3f,
            MovementSpeed = 380f,
            CooldownDuration = 5f
        },
        UltimateAttack = new AbilityData { BaseDamage = 12f }
    };

    private static CharacterData BuildFullKitCharacter() => new() {
        CharacterID = "leonardo",
        MaxHP = 95,
        Weight = 0.9f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 7.5f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 9f,
        BasicAttackKnockback = 2.5f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.Area,
            BaseDamage = 10f,
            IsMultiHit = true,
            HitCount = 3,
            DamageTickIntervalFrames = 30,
            Lifetime = 1.5f,
            KnockbackForce = new Vector2(3, -2),
            CooldownDuration = 10f
        },
        SpecialAttackTwo = new AbilityData {
            ExecutionType = AbilityExecutionType.PersistentObject,
            BaseDamage = 4f,
            DamageTickIntervalFrames = 180,
            PersistentObjectID = "clockwork_turret",
            MaxActiveObjects = 1,
            Lifetime = 15f,
            KnockbackForce = new Vector2(2, -1),
            CooldownDuration = 10f
        },
        MovementAbility = new MovementAbilityData {
            MovementType = MovementType.Glide,
            MovementDuration = 3f,
            MovementSpeed = 380f,
            CooldownDuration = 5f
        },
        UltimateAttack = new AbilityData { BaseDamage = 12f }
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
