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
/// Emancipator forward ground-wave zone whose single pulse carries a real
/// knock-up impulse (the one impulse-carrying zone type), Rail Charge
/// hyper-armor frames that keep damage but suppress hitstun, and rollback
/// safety across an active Emancipator zone.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LincolnKitTests {

    [TestCase]
    public void EmancipatorZonePulseCarriesKnockUpImpulse() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildEmancipatorCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 61,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));

        // The wave is a forward-offset zone (type CharacterID 3 * 10 + slot 1).
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual((int)FighterCharacterID.Lincoln * 10 + 1);
        AssertThat(zone.Position.x > xpTURN.Klotho.Deterministic.Math.FP64.FromInt(-1)).IsTrue();

        // Unlike every other zone, the Emancipator pulse carries real impulse:
        // damage, hitstun, and an upward launch off the ground.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(80);
        AssertThat(target.HitstunFrames).IsEqual(18);
        AssertThat(target.IsGrounded).IsEqual(0);
        AssertThat(target.Velocity.y > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();

        // The 0.25 s wave expires after its single pulse.
        for (int tick = 1; tick <= 20; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.ZoneCount).IsEqual(0);
    }

    [TestCase]
    public void RailChargeHyperArmorFramesPreventHitstun() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildRailChargeCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 62,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        // Rail Charge grants hyper-armor for the full authored 3 s duration.
        simulation.Advance(Frame(0, 0, GameplayButtons.MovementAbility), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charging)).IsTrue();
        AssertThat(charging.HyperArmorFrames).IsEqual(180);
        AssertThat(charging.Velocity.x > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();

        // The opponent's basic lands during the charge: damage sticks, but the
        // armored Lincoln takes no hitstun (design: damage yes, hitstun no).
        simulation.Advance(Frame(1, 0, GameplayButtons.None), Frame(1, 0, GameplayButtons.BasicAttack));
        // Basics are phased swings: the hit lands during the active window
        // (6 startup frames), still deep inside the 180-frame armor.
        for (int tick = 2; tick <= 15; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent struck)).IsTrue();
        AssertThat(struck.CurrentHP).IsEqual(92);
        AssertThat(struck.HitstunFrames).IsEqual(0);
        AssertThat(struck.HyperArmorFrames).IsEqual(165);
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

        // Cast the wave, then snapshot while the zone is alive and the target is
        // mid-knock-up so both the zone entity and the impulse state roll back.
        uninterrupted.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick < 8; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
        }

        byte[] snapshot = uninterrupted.CaptureFullState();
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        for (int tick = 8; tick < 420; tick++) {
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
            ExecutionType = AbilityExecutionType.Area,
            BaseDamage = 20f,
            KnockbackForce = new Vector2(2f, -6f),
            CooldownDuration = 10f,
            Lifetime = 0.25f
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
            MovementDuration = 3f,
            MovementSpeed = 360f,
            GrantsHyperArmor = true,
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
