using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for the canonical Lincoln kit: the
/// Emancipator ground wave (LN03, Package 13 W7b — a travelling wave on the
/// shared ground-wave primitive, no longer a static zone) that knocks a
/// grounded target upward, Rail Charge's armor window (LN02: exactly the
/// travel), and rollback safety across a wave in flight.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LincolnKitTests {

    [TestCase]
    public void EmancipatorGroundWaveCarriesKnockUpImpulse() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildEmancipatorCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 61,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));

        // LN03 (Package 13 W7b), rewritten in place: the wave is a travelling
        // ground wave (projectile type CharacterID 3 * 10 + slot 1) riding the
        // floor, not a zone.
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent wave)).IsTrue();
        AssertThat(wave.ProjectileTypeID).IsEqual((int)FighterCharacterID.Lincoln * 10 + 1);
        AssertThat(FighterGroundWave.SurfaceY(in wave)).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.Zero);

        // It reaches the grounded opponent within a few ticks and carries real
        // impulse: damage, hitstun, and an upward launch off the ground.
        for (int tick = 1; tick <= 6; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(80);
        AssertThat(target.IsGrounded).IsEqual(0);
        AssertThat(target.Velocity.y > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();
        // The hit consumed the wave.
        AssertThat(simulation.ProjectileCount).IsEqual(0);
    }

    [TestCase]
    public void RailChargeArmorsExactlyItsTravel() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildRailChargeCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 62,
            spawnDistance: 5,
            rules: FighterMatchRules.Disabled);

        // LN02 (Package 13 W7b), rewritten in place: the retired 3 s charge
        // armored 180 frames. Rail Charge is now 5 units over 30 frames and is
        // armored for exactly that travel.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
        simulation.Advance(Frame(0, 0, GameplayButtons.MovementAbility), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charging)).IsTrue();
        AssertThat(charging.HyperArmorFrames).IsEqual(30);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.UniversalMovementState).IsEqual(FighterKitMotion.RailCharge);

        for (int tick = 1; tick <= 31; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
        AssertThat(after.HyperArmorFrames).IsEqual(0);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent ended)).IsTrue();
        AssertThat(ended.UniversalMovementState).IsEqual(0);
        var travelled = after.Position.x - before.Position.x;
        AssertThat(travelled > xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(4.9)).IsTrue();
        AssertThat(travelled < xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(5.1)).IsTrue();
    }

    [TestCase]
    public void EmancipatorZoneLifecycleIsSnapshotAndRollbackSafe() {
        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(BuildEmancipatorCharacter());
        var uninterrupted = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 63, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 63, spawnDistance: 1, rules: FighterMatchRules.Disabled);

        // Cast the wave, then snapshot while it is in flight so both the wave
        // entity and the target's impulse state roll back.
        uninterrupted.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick < 2; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
        }

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        for (int tick = 2; tick < 420; tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 64, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 64, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    private static CharacterData BuildEmancipatorCharacter() => new() {
        CharacterID = "lincoln",
        MaxHP = 130,
        Weight = 1.6f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 5.5f,
        MaxJumpForce = 11f,
        BasicAttackDamage = 15f,
        BasicAttackKnockback = 5f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.Projectile,
            BaseDamage = 20f,
            KnockbackForce = new Vector2(2f, -6f),
            Launches = true,
            ProjectileSpeed = 600f,
            ProjectileLifetime = 0.5f,
            CooldownDuration = 10f
        },
        SpecialAttackTwo = new AbilityData { BaseDamage = 18f },
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 25f }
    };

    private static CharacterData BuildRailChargeCharacter() => new() {
        CharacterID = "lincoln",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 5.5f,
        MaxJumpForce = 11f,
        BasicAttackDamage = 15f,
        BasicAttackKnockback = 5f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData {
            MovementType = MovementType.Dash,
            MovementDuration = 0.5f,
            DistanceMoved = 300f,
            MovementSpeed = 600f,
            GrantsHyperArmor = true,
            BaseDamage = 6f,
            KnockbackForce = new Vector2(5f, 0f),
            Launches = false,
            CooldownDuration = 5f
        },
        UltimateAttack = new AbilityData { BaseDamage = 25f }
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
