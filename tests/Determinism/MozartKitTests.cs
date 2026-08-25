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
/// Deterministic Fighter-side coverage for the canonical Mozart kit: the
/// Requiem Chord projectile folding its multi-hit burst total into one hit,
/// the heavy-knockback Fortissimo Wave, the harmless type-5 Sonata
/// staff-platform lifecycle, and rollback safety across the whole kit.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MozartKitTests {

    [TestCase]
    public void RequiemChordFoldsTheMultiHitBurstTotalIntoOneProjectileHit() {
        // The authored burst is 3 hits x 4 damage; the deterministic projectile
        // lands as a single hit, so the loadout folds the 12-damage total.
        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(BuildMozart());
        AssertThat(loadout.SpecialOneDamage).IsEqual(12);

        var simulation = new FighterSimulation(
            loadout,
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 71,
            spawnDistance: 2,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick <= 60; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(88);
    }

    [TestCase]
    public void FortissimoWaveDealsTwelveWithHeavyForwardKnockback() {
        // V7: the wave is a lobbed arc that rises past a point-blank target and
        // crashes down ~4.5-6 units of travel out, so the opponent stands in the
        // landing zone (fighters spawn at +/-spawnDistance = 6 units apart)
        // rather than at melee range.
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildMozart()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 72,
            spawnDistance: 3,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));
        int hitTick = -1;
        for (int tick = 1; tick <= 120; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            if (simulation.TryGetFighter(1, out FighterStateComponent probe) && probe.CurrentHP < 100) {
                hitTick = tick;
                break;
            }
        }

        AssertThat(hitTick > 0).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(88);
        // Heavy pushback: authored knockback 8 against weight 1 resolves to a
        // launch of 8 / (1 + 1) = 4 units/s away from the wave, well above the
        // ~1.5 a basic-strength hit would produce.
        AssertThat(target.Velocity.x >= FP64.FromInt(3)).IsTrue();
    }

    [TestCase]
    public void SonataDriftSpawnsAHarmlessTypeFivePlatformThatExpiresOnSchedule() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildMozart()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 73,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.MovementAbility), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.PersistentObjectCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent platform)).IsTrue();
        AssertThat(platform.ObjectTypeID).IsEqual(5);
        AssertThat(platform.Damage).IsEqual(0);
        AssertThat(platform.LifetimeFrames >= 179 && platform.LifetimeFrames <= 180).IsTrue();

        // The staff persists through its authored 3 s (180-frame) lifetime while
        // the adjacent opponent takes no damage from it, then expires.
        for (int tick = 1; tick <= 170; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.PersistentObjectCount).IsEqual(1);
        for (int tick = 171; tick <= 220; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.PersistentObjectCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent opponent)).IsTrue();
        AssertThat(opponent.CurrentHP).IsEqual(100);
    }

    [TestCase]
    public void MozartKitLifecycleIsSnapshotAndRollbackSafe() {
        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(BuildMozart());
        var uninterrupted = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 74, spawnDistance: 3, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 74, spawnDistance: 3, rules: FighterMatchRules.Disabled);

        // Platform out, chord in flight, wave following — snapshot mid-lifecycle.
        uninterrupted.Advance(Frame(0, 0, GameplayButtons.MovementAbility), Frame(0, 0, GameplayButtons.None));
        uninterrupted.Advance(Frame(1, 0, GameplayButtons.Special1), Frame(1, 0, GameplayButtons.None));
        uninterrupted.Advance(Frame(2, 0, GameplayButtons.Special2), Frame(2, 0, GameplayButtons.None));
        for (int tick = 3; tick < 60; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
        }

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // Resimulation across platform expiry and further projectile traffic must
        // converge tick-for-tick.
        for (int tick = 60; tick < 420; tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    private static CharacterData BuildMozart() => new() {
        CharacterID = "mozart",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
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
        UltimateAttack = new AbilityData { BaseDamage = 8f, IsMultiHit = true, HitCount = 10 }
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
