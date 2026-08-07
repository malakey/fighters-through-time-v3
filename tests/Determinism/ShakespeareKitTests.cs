using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for the canonical Shakespeare kit: the
/// rolling Yorick skull projectile applying TimeDilation at the authored 0.6
/// intensity (the 30% slow on the shared 1 - intensity / 2 formula), the
/// owner-centered Tempest zone that shoves the opponent away and lifts the
/// caster, the Prospero's Flight glide float window, and rollback safety across
/// the storm and glide lifecycles.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShakespeareKitTests {

    [TestCase]
    public void YorickSkullAppliesTimeDilationAtTheAuthoredIntensity() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildLamentCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 61,
            spawnDistance: 3,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.ProjectileCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent skull)).IsTrue();
        AssertThat(skull.ProjectileTypeID).IsEqual((int)FighterCharacterID.Shakespeare * 10 + 1);

        // The skull rolls to the opponent; on impact the wailing wave lands the
        // damage and the slow. In the 1v1 sim the impact and the sonic wave are
        // one deterministic hit.
        bool everSlowed = false;
        xpTURN.Klotho.Deterministic.Math.FP64 observedIntensity = default;
        for (int tick = 1; tick <= 180 && !everSlowed; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            if (simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)
                && runtime.StatusType == (int)StatusType.TimeDilation) {
                everSlowed = true;
                observedIntensity = runtime.StatusIntensity;
            }
        }

        AssertThat(everSlowed).IsTrue();
        // Intensity 0.6 on the shared TimeDilation formula (speed x (1 - 0.5 x
        // intensity)) is exactly the design's 30% slow.
        AssertThat(observedIntensity.RawValue)
            .IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromFloat(0.6f).RawValue);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(86);
    }

    [TestCase]
    public void TempestZonePushesTheOpponentAwayFromCenterAndLiftsTheOwner() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildTempestCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 62,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        simulation.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));

        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual((int)FighterCharacterID.Shakespeare * 10 + 2);

        // The first pulse shoves the opponent outward (the opponent spawns to the
        // right of the storm center, so the wind pushes further right) with zero
        // damage.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(100);
        AssertThat(target.Velocity.x > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();

        // The gust lifts the caster airborne on cast.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent owner)).IsTrue();
        AssertThat(owner.IsGrounded).IsEqual(0);
        AssertThat(owner.Velocity.y > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();

        for (int tick = 1; tick <= 20; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.Position.x > before.Position.x).IsTrue();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent lifted)).IsTrue();
        AssertThat(lifted.Position.y > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();
    }

    [TestCase]
    public void ProsperosFlightGlideHoldsTheAuthoredFloatWindow() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildFlightCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 63,
            spawnDistance: 4,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.MovementAbility), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent owner)).IsTrue();
        AssertThat(owner.IsGrounded).IsEqual(0);
        AssertThat(owner.Velocity.y > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        // The authored 3 s glide becomes 180 reduced-gravity float frames after
        // the boost.
        AssertThat(runtime.FloatFrames).IsEqual(180);

        for (int tick = 1; tick <= 180; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent settled)).IsTrue();
        AssertThat(settled.FloatFrames).IsEqual(0);
    }

    [TestCase]
    public void TempestAndGlideLifecyclesAreSnapshotAndRollbackSafe() {
        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(BuildKitCharacter());
        var uninterrupted = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 64, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 64, spawnDistance: 1, rules: FighterMatchRules.Disabled);

        uninterrupted.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));
        uninterrupted.Advance(Frame(1, 0, GameplayButtons.MovementAbility), Frame(1, 0, GameplayButtons.None));
        uninterrupted.Advance(Frame(2, 0, GameplayButtons.Special1), Frame(2, 0, GameplayButtons.None));
        for (int tick = 3; tick < 60; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
        }

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        for (int tick = 60; tick < 420; tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    private static CharacterData BuildLamentCharacter() => new() {
        CharacterID = "shakespeare",
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
            BaseDamage = 14f,
            KnockbackForce = new Vector2(3f, -2f),
            ProjectileSpeed = 200f,
            ProjectileLifetime = 5f,
            CooldownDuration = 10f,
            AppliedStatus = StatusType.TimeDilation,
            StatusDuration = 2.5f,
            StatusIntensity = 0.6f
        },
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 14f }
    };

    private static CharacterData BuildTempestCharacter() => new() {
        CharacterID = "shakespeare",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData {
            ExecutionType = AbilityExecutionType.Area,
            BaseDamage = 0f,
            KnockbackForce = new Vector2(4f, -2f),
            CooldownDuration = 10f,
            Lifetime = 1.2f,
            DamageTickIntervalFrames = 30
        },
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 14f }
    };

    private static CharacterData BuildFlightCharacter() => new() {
        CharacterID = "shakespeare",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData {
            MovementType = MovementType.Glide,
            MovementDuration = 3f,
            MovementSpeed = 180f,
            CooldownDuration = 5f
        },
        UltimateAttack = new AbilityData { BaseDamage = 14f }
    };

    private static CharacterData BuildKitCharacter() => new() {
        CharacterID = "shakespeare",
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
            BaseDamage = 14f,
            KnockbackForce = new Vector2(3f, -2f),
            ProjectileSpeed = 200f,
            ProjectileLifetime = 5f,
            CooldownDuration = 10f,
            AppliedStatus = StatusType.TimeDilation,
            StatusDuration = 2.5f,
            StatusIntensity = 0.6f
        },
        SpecialAttackTwo = new AbilityData {
            ExecutionType = AbilityExecutionType.Area,
            BaseDamage = 0f,
            KnockbackForce = new Vector2(4f, -2f),
            CooldownDuration = 10f,
            Lifetime = 1.2f,
            DamageTickIntervalFrames = 30
        },
        MovementAbility = new MovementAbilityData {
            MovementType = MovementType.Glide,
            MovementDuration = 3f,
            MovementSpeed = 180f,
            CooldownDuration = 5f
        },
        UltimateAttack = new AbilityData { BaseDamage = 14f }
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
