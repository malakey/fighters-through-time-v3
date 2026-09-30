using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for the canonical Cleopatra kit: the
/// max-1 Serpent Nest construct with its Venom bite (the design's brief Root is
/// delivered as one second of bite hitstun; the Venom itself rides the V7
/// damage status slot, so later control statuses no longer erase it), the
/// Sandstorm Vortex pull/tick/TimeDilation zone (thrown up to 5 units ahead,
/// C01), the Desert Mirage sand rush (C03, Package 13 W7b), and rollback safety
/// across nest and vortex lifecycles.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CleopatraKitTests {

    [TestCase]
    public void SerpentNestDeploysWithDesignSpecAndCapsAtOneActive() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildNestCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 61,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.PersistentObjectCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent nest)).IsTrue();
        AssertThat(nest.ObjectTypeID).IsEqual(3);
        AssertThat(nest.MaxHP).IsEqual(15);
        AssertThat(nest.CurrentHP).IsEqual(15);
        // 12 s lifetime = 720 frames, minus the frames already simulated.
        AssertThat(nest.LifetimeFrames > 700).IsTrue();
        AssertThat(nest.StatusType).IsEqual((int)StatusType.Venom);
        AssertThat(nest.StatusFrames).IsEqual(240);

        // Max 1 active: a second deploy replaces the first instead of stacking.
        for (int tick = 1; tick <= 4; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        simulation.Advance(Frame(5, 0, GameplayButtons.Special1), Frame(5, 0, GameplayButtons.None));
        AssertThat(simulation.PersistentObjectCount).IsEqual(1);
    }

    [TestCase]
    public void SerpentNestBiteAppliesVenomWithARootLengthHitstun() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildNestCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 62,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        // Deploy at the owner's feet; the first bite lands after the nest's 1 s
        // action cooldown (V7 tuning batch) against the opponent standing in
        // range. The 70-frame window covers exactly one bite. C01 (Package 13
        // W7b): the nest is 2.0 units wide, so the opponent walks into it (the
        // 0.6-unit pushbox leaves them well inside its 1.0-unit reach).
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        int maxHitstunObserved = 0;
        for (int tick = 1; tick <= 70; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            if (simulation.TryGetFighter(1, out FighterStateComponent observed)
                && observed.HitstunFrames > maxHitstunObserved) {
                maxHitstunObserved = observed.HitstunFrames;
            }
        }

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(90);
        // The brief Root rides on the bite as ~1 s of hitstun (60 frames), well
        // above the generic 10-frame persistent-object hitstun.
        AssertThat(maxHitstunObserved >= 55).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
        // Venom occupies the V7 damage status slot.
        AssertThat(runtime.DamageStatusType).IsEqual((int)StatusType.Venom);
    }

    [TestCase]
    public void SandstormVortexPullsOpponentTowardCenterWithTicksAndTimeDilation() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildVortexCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 63,
            spawnDistance: 2,
            rules: FighterMatchRules.Disabled);

        // C01 (Package 13 W7b): the vortex is thrown five units ahead of
        // Cleopatra (x = -2 → 3) and snapped to the floor, with the 1.8-unit
        // radius; the opponent at x = 2 is inside it.
        simulation.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual((int)FighterCharacterID.Cleopatra * 10 + 2);
        AssertThat(zone.Position.x).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(3));
        AssertThat(zone.Position.y).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.Zero);
        AssertThat(zone.HalfExtents.x).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(1.8));

        // The pull is a steady per-frame positional drag toward the zone center
        // at 3 units/s: 20 frames move the opponent the full unit to x = 3.
        for (int tick = 1; tick <= 10; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent pulling)).IsTrue();
        AssertThat(pulling.Position.x > xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2)).IsTrue();
        AssertThat(pulling.Position.x < xpTURN.Klotho.Deterministic.Math.FP64.FromInt(3)).IsTrue();
        for (int tick = 11; tick <= 30; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent pulled)).IsTrue();
        AssertThat(pulled.Position.x).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(3));
        AssertThat(pulled.HitstunFrames).IsEqual(0);

        // Five impulse-free ticks of 2 over the 2 s lifetime, TimeDilation held,
        // and no block-charge or hitstun interaction.
        for (int tick = 31; tick <= 130; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(90);
        AssertThat(target.HitstunFrames).IsEqual(0);
        AssertThat(target.BlockCharges).IsEqual(3);
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.StatusType).IsEqual((int)StatusType.TimeDilation);
        AssertThat(simulation.ZoneCount).IsEqual(0);
    }

    [TestCase]
    public void DesertMirageRushesTheAuthoredDistanceInTheHeldDirectionOverFifteenFrames() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildMirageCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 64,
            spawnDistance: 4,
            rules: FighterMatchRules.Disabled);

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
        simulation.Advance(
            Frame(0, -127, GameplayButtons.MovementAbility),
            Frame(0, 0, GameplayButtons.None));
        // C03 (Package 13 W7b): a rush, not a teleport — nothing has moved on
        // the press tick; the travel runs over the next 15 frames.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent pressed)).IsTrue();
        AssertThat(before.Position.x - pressed.Position.x < xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(0.5)).IsTrue();
        for (int tick = 1; tick <= 16; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();

        // 240 px at 60 px/unit is 4 world units of leftward travel.
        var travelled = before.Position.x - after.Position.x;
        AssertThat(travelled > xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(3.9)).IsTrue();
        AssertThat(travelled < xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(4.1)).IsTrue();
    }

    [TestCase]
    public void NestAndVortexLifecyclesAreSnapshotAndRollbackSafe() {
        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(BuildFullCharacter());
        var uninterrupted = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 65, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 65, spawnDistance: 2, rules: FighterMatchRules.Disabled);

        uninterrupted.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        uninterrupted.Advance(Frame(1, 0, GameplayButtons.None), Frame(1, 0, GameplayButtons.None));
        uninterrupted.Advance(Frame(2, 0, GameplayButtons.Special2), Frame(2, 0, GameplayButtons.None));
        for (int tick = 3; tick < 60; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
        }

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // 60..800 covers nest bites, Venom ticks, vortex pull/pulses/expiry, and
        // the nest's 12 s lifetime expiry.
        for (int tick = 60; tick < 800; tick++) {
            long expected = uninterrupted.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            long actual = restored.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    private static CharacterData BuildNestCharacter() => new() {
        CharacterID = "cleopatra",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.PersistentObject,
            BaseDamage = 10f,
            KnockbackForce = new Vector2(2f, -1f),
            PersistentObjectID = "serpent_nest",
            MaxActiveObjects = 1,
            Lifetime = 12f,
            CooldownDuration = 0f,
            AppliedStatus = StatusType.Venom,
            StatusDuration = 4f
        },
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildVortexCharacter() => new() {
        CharacterID = "cleopatra",
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
            BaseDamage = 2f,
            KnockbackForce = Vector2.Zero,
            DamageTickIntervalFrames = 24,
            CooldownDuration = 10f,
            Lifetime = 2f,
            AppliedStatus = StatusType.TimeDilation,
            StatusDuration = 2f,
            StatusIntensity = 0.8f
        },
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildMirageCharacter() => new() {
        CharacterID = "cleopatra",
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
            MovementType = MovementType.SandRush,
            MovementDuration = 0.25f,
            DistanceMoved = 240f,
            MovementSpeed = 960f,
            CooldownDuration = 5f
        },
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildFullCharacter() {
        CharacterData data = BuildNestCharacter();
        data.SpecialAttackTwo = BuildVortexCharacter().SpecialAttackTwo;
        data.MovementAbility = BuildMirageCharacter().MovementAbility;
        return data;
    }

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
