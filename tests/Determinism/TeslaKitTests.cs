using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Deterministic Fighter-side coverage for the canonical Tesla kit: coil
/// deployment, the 8-unit alternating-current fence link with StaticCharge,
/// the radial Lorentz Pulse Root zone with StaticCharge-primed coil chains,
/// the data-driven Lightning Blink, and rollback safety across coil linking.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TeslaKitTests {

    [TestCase]
    public void LinkedCoilFenceDamagesAndStaticChargesTheOpponent() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildCoilCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 51,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        // Deploy the first coil, walk left to open space, then deploy the second:
        // the pair sits a few units apart (within the 8-unit link range) and the
        // fence spans the gap between them.
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick <= 40; tick++) {
            simulation.Advance(Frame(tick, -127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        simulation.Advance(Frame(41, 0, GameplayButtons.Special1), Frame(41, 0, GameplayButtons.None));
        AssertThat(simulation.PersistentObjectCount).IsEqual(2);

        // The opponent walks into the fence span; over the window the fence must
        // land ticks (HP loss) and prime StaticCharge.
        // Stop well before the fence's cumulative damage KOs the target — a
        // respawn would restore full HP and mask the assertion.
        bool everStaticCharged = false;
        for (int tick = 42; tick < 200; tick++) {
            simulation.Advance(Frame(tick, -127, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            if (simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)
                && runtime.StatusType == (int)StatusType.StaticCharge) {
                everStaticCharged = true;
            }
        }

        AssertThat(everStaticCharged).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP < target.MaxHP).IsTrue();
    }

    [TestCase]
    public void LorentzPulseIsARadialOwnerCenteredRootBurst() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildLorentzCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 52,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));

        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual((int)FighterCharacterID.Tesla * 10 + 2);

        // One impulse-free pulse: damage plus Root, no hitstun/knockback/block use.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(88);
        AssertThat(target.HitstunFrames).IsEqual(0);
        AssertThat(target.BlockCharges).IsEqual(3);
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.StatusType).IsEqual((int)StatusType.Root);

        // Root immobilizes: the target holds toward Tesla but cannot move.
        FP64ApproxUnchangedX(simulation);

        // The 0.25 s zone expires after its single pulse.
        AssertThat(simulation.ZoneCount).IsEqual(0);
    }

    private static void FP64ApproxUnchangedX(FighterSimulation simulation) {
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        for (int tick = 1; tick <= 30; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.Position.x).IsEqual(before.Position.x);
    }

    [TestCase]
    public void LorentzPulseChainsCoilLightningIntoStaticChargedTargets() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildChainCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 53,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        // Deploy one coil; its first arc fires after its 2 s action cooldown
        // (V7 tuning batch) and primes the target with StaticCharge.
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick <= 125; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent primed)).IsTrue();
        AssertThat(primed.CurrentHP).IsEqual(95);
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent primedRuntime)).IsTrue();
        AssertThat(primedRuntime.StatusType).IsEqual((int)StatusType.StaticCharge);

        // The pulse against a StaticCharge-primed target adds one chain strike
        // per live coil: 12 base + 5 chain = 17.
        simulation.Advance(Frame(126, 0, GameplayButtons.Special2), Frame(126, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent chained)).IsTrue();
        AssertThat(chained.CurrentHP).IsEqual(78);
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent chainedRuntime)).IsTrue();
        AssertThat(chainedRuntime.StatusType).IsEqual((int)StatusType.Root);
    }

    [TestCase]
    public void LightningBlinkTravelsAuthoredDistanceInHeldDirection() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildBlinkCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 54,
            spawnDistance: 4,
            rules: FighterMatchRules.Disabled);

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
        simulation.Advance(
            Frame(0, -127, GameplayButtons.MovementAbility),
            Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();

        // 160 px at 60 px/unit is 2.666... world units of leftward travel.
        var travelled = before.Position.x - after.Position.x;
        AssertThat(travelled > xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(2.5)).IsTrue();
        AssertThat(travelled < xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(2.8)).IsTrue();
    }

    [TestCase]
    public void CoilLinkLifecycleIsSnapshotAndRollbackSafe() {
        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(BuildCoilCharacter());
        var uninterrupted = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 55, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var restored = new FighterSimulation(
            loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 55, spawnDistance: 1, rules: FighterMatchRules.Disabled);

        uninterrupted.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        uninterrupted.Advance(Frame(1, 0, GameplayButtons.None), Frame(1, 0, GameplayButtons.None));
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

    private static CharacterData BuildCoilCharacter() => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.PersistentObject,
            BaseDamage = 5f,
            PersistentObjectID = "tesla_coil",
            MaxActiveObjects = 2,
            Lifetime = 30f,
            CooldownDuration = 0f
        },
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildLorentzCharacter() => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData {
            ExecutionType = AbilityExecutionType.Area,
            BaseDamage = 12f,
            KnockbackForce = Vector2.Zero,
            CooldownDuration = 10f,
            Lifetime = 0.25f,
            AppliedStatus = StatusType.Root,
            StatusDuration = 2f,
            StatusIntensity = 1f
        },
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildChainCharacter() => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.PersistentObject,
            BaseDamage = 5f,
            PersistentObjectID = "tesla_coil",
            MaxActiveObjects = 2,
            Lifetime = 30f,
            CooldownDuration = 0f,
            AppliedStatus = StatusType.StaticCharge,
            StatusDuration = 2f
        },
        SpecialAttackTwo = new AbilityData {
            ExecutionType = AbilityExecutionType.Area,
            BaseDamage = 12f,
            KnockbackForce = Vector2.Zero,
            CooldownDuration = 10f,
            Lifetime = 0.25f,
            AppliedStatus = StatusType.Root,
            StatusDuration = 2f,
            StatusIntensity = 1f
        },
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildBlinkCharacter() => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData {
            MovementType = MovementType.Blink,
            MovementDuration = 0.2f,
            DistanceMoved = 160f,
            MovementSpeed = 800f,
            CooldownDuration = 5f
        },
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
