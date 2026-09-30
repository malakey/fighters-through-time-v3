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
        // intensity)) is exactly the design's 30% slow. The value round-trips
        // through the status slot's deterministic thousandths quantization.
        AssertThat(observedIntensity.RawValue)
            .IsEqual((xpTURN.Klotho.Deterministic.Math.FP64.FromInt(600)
                / xpTURN.Klotho.Deterministic.Math.FP64.FromInt(1000)).RawValue);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(86);
    }

    /// <summary>
    /// S01/S02 (Package 13 W7a): The Tempest is a windbox — the opponent it
    /// touches is shoved ~3 units outward over the storm's 12 frames by a
    /// positional push, with no damage, hitstun or velocity change, while
    /// Shakespeare is lifted.
    /// </summary>
    [TestCase]
    public void TempestWindboxPushesTheOpponentThreeUnitsAndLiftsTheOwner() {
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

        // The storm pushes, it does not hit: no damage, no hitstun, and the
        // opponent's own velocity is untouched.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(100);
        AssertThat(target.HitstunFrames).IsEqual(0);
        AssertThat(target.Velocity.x).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.Zero);
        AssertThat(target.Position.x > before.Position.x).IsTrue();

        // The gust lifts the caster airborne on cast.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent owner)).IsTrue();
        AssertThat(owner.IsGrounded).IsEqual(0);
        AssertThat(owner.Velocity.y > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();

        for (int tick = 1; tick <= 20; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        xpTURN.Klotho.Deterministic.Math.FP64 pushed = after.Position.x - before.Position.x;
        AssertThat(pushed > xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(2.95)
            && pushed < xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(3.05)).IsTrue();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent lifted)).IsTrue();
        AssertThat(lifted.Position.y > xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2)).IsTrue();
    }

    /// <summary>
    /// S01: a blocking opponent is pushed all the same, but the windbox spends no
    /// block charge, applies no shieldstun, hitstun or hitstop, and builds no meter
    /// for either side.
    /// </summary>
    [TestCase]
    public void TempestWindboxSpendsNoBlockChargeAndBuildsNoMeter() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildTempestCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 65,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);
        // The opponent turns to face the storm, then raises the stance.
        simulation.Advance(Frame(0, 0, GameplayButtons.None), Frame(0, -127, GameplayButtons.None));
        for (int tick = 1; tick < 4; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.Block));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        simulation.Advance(Frame(4, 0, GameplayButtons.Special2), Frame(4, 0, GameplayButtons.Block));
        for (int tick = 5; tick <= 20; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.Block));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.Position.x > before.Position.x + xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2)).IsTrue();
        AssertThat(after.BlockCharges).IsEqual(before.BlockCharges);
        AssertThat(after.HitstunFrames).IsEqual(0);
        AssertThat(after.Influence).IsEqual(before.Influence);
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
        AssertThat(verb.ShieldStunFrames).IsEqual(0);
        AssertThat(verb.HitstopFrames).IsEqual(0);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.Zero);
    }

    /// <summary>
    /// A08 (Package 13 W7a): Prospero's Flight is a single gust burst — about 4
    /// units forward and 2.5 up over 20 frames, then a normal fall with no glide.
    /// </summary>
    [TestCase]
    public void ProsperosFlightIsAGustBurstThenANormalFall() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildFlightCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 63,
            spawnDistance: 4,
            rules: FighterMatchRules.Disabled);

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent start)).IsTrue();
        simulation.Advance(Frame(0, 0, GameplayButtons.MovementAbility), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.UniversalMovementState).IsEqual(FighterKitMotion.GustBurst);
        for (int tick = 1; tick <= KitMotionRules.ProsperoGustFrames; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent apex)).IsTrue();
        xpTURN.Klotho.Deterministic.Math.FP64 forward = apex.Position.x - start.Position.x;
        xpTURN.Klotho.Deterministic.Math.FP64 rise = apex.Position.y - start.Position.y;
        AssertThat(forward > xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(3.9)
            && forward < xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(4.1)).IsTrue();
        AssertThat(rise > xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(2.4)
            && rise < xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(2.6)).IsTrue();

        // Then the burst is spent: no glide float window, and an ordinary fall.
        simulation.Advance(Frame(21, 0, GameplayButtons.None), Frame(21, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent after)).IsTrue();
        AssertThat(after.FloatFrames).IsEqual(0);
        AssertThat(after.UniversalMovementState).IsEqual((int)UniversalMovementPhase.None);
        for (int tick = 22; tick <= 120; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent landed)).IsTrue();
        AssertThat(landed.IsGrounded).IsEqual(1);
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
            KnockbackForce = Vector2.Zero,
            CooldownDuration = 10f,
            // S02 (Package 13 W7a): the windbox lives for its 12 active frames.
            Lifetime = 0.2f,
            DamageTickIntervalFrames = 0
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
            // A08 (Package 13 W7a): the gust burst.
            MovementType = MovementType.Gust,
            MovementDuration = 1f / 3f,
            DistanceMoved = 240f,
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
