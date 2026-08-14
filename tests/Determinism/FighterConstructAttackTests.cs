using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// 2026-08-11 construct rebalance pins: deployed constructs sit with their base
/// on the floor (they used to spawn centered on the owner's feet, half-buried),
/// the opponent's basic swings damage and destroy them (nothing in the
/// simulation could previously reduce a construct's HP), and construct attacks
/// carry the resource-authored zero knockback without freezing the victim's
/// velocity.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterConstructAttackTests {

    [TestCase]
    public void ConstructsSpawnWithTheirBaseOnTheFloorLine() {
        var simulation = NewSimulation(seed: 71, spawnDistance: 1);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent coil)).IsTrue();
        // Grounded owner feet at y = 0; a 0.6 half-extent box bottom-anchored on
        // the floor centers at y = 0.6.
        AssertThat(coil.Position.y).IsEqual(coil.HalfExtents.y);
    }

    [TestCase]
    public void OpponentBasicSwingsDamageAndEventuallyDestroyAConstruct() {
        // Spawn distance 1: the coil deploys at Tesla's feet, inside the
        // opponent's 2-unit basic reach.
        var simulation = NewSimulation(seed: 72, spawnDistance: 1);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent coil)).IsTrue();
        int hpBefore = coil.CurrentHP;
        AssertThat(hpBefore).IsEqual(25);

        // The opponent mashes basics until the coil dies; each swing may land
        // exactly one construct sweep. 25 HP against Joan's default basics
        // falls well inside the window.
        for (int tick = 1; tick <= 600 && simulation.PersistentObjectCount > 0; tick++) {
            simulation.Advance(
                Frame(tick, 0, GameplayButtons.None),
                Frame(tick, 0, GameplayButtons.BasicAttack));
            if (simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent observed)
                && observed.CurrentHP < hpBefore) {
                hpBefore = observed.CurrentHP;
            }
        }

        AssertThat(hpBefore < 25).IsTrue();
        AssertThat(simulation.PersistentObjectCount).IsEqual(0);
    }

    [TestCase]
    public void ZeroKnockbackCoilArcStunsWithoutMovingTheVictim() {
        var simulation = NewSimulation(seed: 73, spawnDistance: 2);

        // Deploy, then both fighters hold still through the coil's first arc
        // (240-frame action cooldown after the rebalance).
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        bool everStunned = false;
        for (int tick = 1; tick <= 300; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            if (simulation.TryGetFighter(1, out FighterStateComponent observed)
                && observed.HitstunFrames > 0) {
                everStunned = true;
            }
        }

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        // Damage landed with hitstun, but the impulse-free arc never displaced
        // or launched the standing victim.
        AssertThat(after.CurrentHP < after.MaxHP).IsTrue();
        AssertThat(everStunned).IsTrue();
        AssertThat(after.Position.x).IsEqual(before.Position.x);
        AssertThat(after.IsGrounded).IsEqual(1);
    }

    private static FighterSimulation NewSimulation(int seed, int spawnDistance) => new(
        FighterLoadoutFactory.FromCharacterData(BuildCoilCharacter()),
        FighterLoadout.Default(FighterCharacterID.Joan),
        seed: seed,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);

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
            KnockbackForce = Vector2.Zero,
            PersistentObjectID = "tesla_coil",
            MaxActiveObjects = 2,
            Lifetime = 30f,
            CooldownDuration = 0f
        },
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
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
