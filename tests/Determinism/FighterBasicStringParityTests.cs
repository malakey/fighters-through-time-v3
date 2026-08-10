using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Pins the shared basic-combo rulebook (FTT.Combat.BasicComboRules) on the
/// Fighter side: phased swings with committed startup/active frames, grounded
/// movement lock with aerial drift, recovery cancels that reset the chain, the
/// buffered three-hit progression, the grounded block stance, shared hitstun
/// values, and block-charge regeneration. Story implements the identical rules
/// through PlayerController; the numbers come from the same static tables, so
/// drift in either mode breaks this suite or its Story counterparts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterBasicStringParityTests {

    [TestCase]
    public void StartupCommitsBeforeTheHitLandsExactlyAtTheActiveWindow() {
        var simulation = NewAdjacentSimulation(seed: 91);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        Advance(simulation, 0, p1Buttons: GameplayButtons.BasicAttack);
        for (int tick = 1; tick < BasicComboRules.GroundStartupFrames[0]; tick++) {
            Advance(simulation, tick);
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent duringStartup)).IsTrue();
            AssertThat(duringStartup.CurrentHP)
                .OverrideFailureMessage($"No damage may land during startup (tick {tick}).")
                .IsEqual(before.CurrentHP);
        }

        // The first active tick applies the 0.8x opener and the shared 9-frame
        // hitstun in the same simulation tick.
        Advance(simulation, BasicComboRules.GroundStartupFrames[0]);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        int expectedDamage = FighterLoadout.Default(FighterCharacterID.Einstein).BasicDamage * 8 / 10;
        AssertThat(before.CurrentHP - struck.CurrentHP).IsEqual(expectedDamage);
        AssertThat(struck.HitstunFrames).IsEqual(BasicComboRules.HitstunFrames[0]);
    }

    [TestCase]
    public void GroundedSwingLocksSteeringWhileAerialSwingKeepsAirDrift() {
        // Grounded: run up to speed, then swing while still holding the
        // direction — the shared rule decelerates to zero, no steering.
        var grounded = NewAdjacentSimulation(seed: 92, spawnDistance: 6);
        for (int tick = 0; tick < 20; tick++) {
            Advance(grounded, tick, p1MoveX: 127);
        }
        Advance(grounded, 20, p1MoveX: 127, p1Buttons: GameplayButtons.BasicAttack);
        for (int tick = 21; tick < 33; tick++) {
            Advance(grounded, tick, p1MoveX: 127);
        }
        AssertThat(grounded.TryGetFighter(0, out FighterStateComponent committed)).IsTrue();
        AssertThat(committed.Velocity.x == FP64.Zero)
            .OverrideFailureMessage("A grounded swing must decelerate to zero despite held input.")
            .IsTrue();

        // Aerial: the same held drift keeps moving the fighter mid-swing.
        var aerial = NewAdjacentSimulation(seed: 93, spawnDistance: 6);
        Advance(aerial, 0, p1Buttons: GameplayButtons.Jump);
        Advance(aerial, 1, p1MoveX: 127, p1Buttons: GameplayButtons.BasicAttack);
        AssertThat(aerial.TryGetFighter(0, out FighterStateComponent airborneStart)).IsTrue();
        AssertThat(airborneStart.IsGrounded).IsEqual(0);
        FP64 xBefore = airborneStart.Position.x;
        for (int tick = 2; tick < 10; tick++) {
            Advance(aerial, tick, p1MoveX: 127);
        }
        AssertThat(aerial.TryGetFighter(0, out FighterStateComponent drifted)).IsTrue();
        AssertThat(drifted.Position.x > xBefore)
            .OverrideFailureMessage("An aerial swing must keep full air drift.")
            .IsTrue();
    }

    [TestCase]
    public void MovementDuringRecoveryCancelsTheSwingAndResetsTheChain() {
        var simulation = NewAdjacentSimulation(seed: 94);
        Advance(simulation, 0, p1Buttons: GameplayButtons.BasicAttack);
        int recoveryTick = BasicComboRules.GroundStartupFrames[0] + BasicComboRules.GroundActiveFrames[0] + 1;
        for (int tick = 1; tick <= recoveryTick; tick++) {
            Advance(simulation, tick);
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent inRecovery)).IsTrue();
        AssertThat(inRecovery.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseRecovery);

        Advance(simulation, recoveryTick + 1, p1MoveX: 127);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent cancelled)).IsTrue();
        AssertThat(cancelled.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseNone);
        AssertThat(cancelled.ComboIndex).IsEqual(0);
    }

    [TestCase]
    public void BufferedChainLandsTheFullThreeHitProgression() {
        // Zero-knockback basics keep the stationary target inside melee range
        // for the whole string (the ultimate suites charge meters the same way).
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildZeroKnockbackAttacker()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 95,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        BasicStringTestDriver.LandChainedBasics(simulation, 0, 3);

        const int basicDamage = 10;
        int expected = basicDamage * 8 / 10 + basicDamage + basicDamage * 15 / 10;
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(before.CurrentHP - after.CurrentHP).IsEqual(expected);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent finished)).IsTrue();
        AssertThat(finished.ComboIndex).IsEqual(0);
        AssertThat(finished.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseNone);
    }

    [TestCase]
    public void BlockStanceIsGroundedAbsorbsInFrontAndSuppressesActions() {
        // Grounded stance: the swing is absorbed for one charge, no damage,
        // and the blocker cannot move or start their own swing.
        var grounded = NewAdjacentSimulation(seed: 96);
        AssertThat(grounded.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        FP64 xBefore = before.Position.x;
        Advance(grounded, 0, p1Buttons: GameplayButtons.BasicAttack, p2Buttons: GameplayButtons.Block);
        for (int tick = 1; tick <= 12; tick++) {
            Advance(grounded, tick,
                p2MoveX: 127,
                p2Buttons: GameplayButtons.Block | GameplayButtons.BasicAttack,
                p2Held: GameplayButtons.Block);
        }
        AssertThat(grounded.TryGetFighter(1, out FighterStateComponent blocked)).IsTrue();
        AssertThat(blocked.CurrentHP).IsEqual(before.CurrentHP);
        AssertThat(blocked.BlockCharges).IsEqual(before.BlockCharges - 1);
        AssertThat(blocked.Position.x == xBefore)
            .OverrideFailureMessage("The block stance must lock movement to zero.")
            .IsTrue();
        AssertThat(grounded.TryGetFighterRuntime(1, out FighterRuntimeComponent blockedRuntime)).IsTrue();
        AssertThat(blockedRuntime.AttackPhase)
            .OverrideFailureMessage("Attack inputs are ignored while the stance is up.")
            .IsEqual(FighterBasicAttackRules.PhaseNone);

        // Airborne with Block held is not a stance: the hit lands in full.
        var airborne = NewAdjacentSimulation(seed: 97);
        AssertThat(airborne.TryGetFighter(1, out FighterStateComponent beforeAir)).IsTrue();
        Advance(airborne, 0, p1Buttons: GameplayButtons.BasicAttack, p2Buttons: GameplayButtons.Jump);
        for (int tick = 1; tick <= 8; tick++) {
            Advance(airborne, tick, p2Held: GameplayButtons.Block);
        }
        AssertThat(airborne.TryGetFighter(1, out FighterStateComponent struckAir)).IsTrue();
        AssertThat(struckAir.CurrentHP < beforeAir.CurrentHP)
            .OverrideFailureMessage("Holding Block while airborne must not absorb hits.")
            .IsTrue();
    }

    [TestCase]
    public void BlockChargesRegenerateOnTheSharedIntervalAfterTheStanceDrops() {
        var simulation = NewAdjacentSimulation(seed: 98);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        // Spend one charge on a blocked opener.
        Advance(simulation, 0, p1Buttons: GameplayButtons.BasicAttack, p2Buttons: GameplayButtons.Block);
        int tick = 1;
        for (; tick <= BasicComboRules.GroundStartupFrames[0] + 1; tick++) {
            Advance(simulation, tick, p2Held: GameplayButtons.Block);
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent spent)).IsTrue();
        AssertThat(spent.BlockCharges).IsEqual(before.BlockCharges - 1);

        // Released stance: one charge returns after the shared interval.
        int halfway = tick + BasicComboRules.BlockChargeRegenFrames / 2;
        for (; tick < halfway; tick++) {
            Advance(simulation, tick);
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent waiting)).IsTrue();
        AssertThat(waiting.BlockCharges).IsEqual(before.BlockCharges - 1);
        int fullInterval = tick + BasicComboRules.BlockChargeRegenFrames / 2 + 4;
        for (; tick < fullInterval; tick++) {
            Advance(simulation, tick);
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent regenerated)).IsTrue();
        AssertThat(regenerated.BlockCharges).IsEqual(before.BlockCharges);
    }

    private static FighterSimulation NewAdjacentSimulation(int seed, int spawnDistance = 1) => new(
        FighterCharacterID.Einstein,
        FighterCharacterID.Joan,
        seed: seed,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);

    private static CharacterData BuildZeroKnockbackAttacker() => new() {
        CharacterID = "einstein",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static void Advance(
        FighterSimulation simulation,
        int tick,
        sbyte p1MoveX = 0,
        GameplayButtons p1Buttons = GameplayButtons.None,
        sbyte p2MoveX = 0,
        GameplayButtons p2Buttons = GameplayButtons.None,
        GameplayButtons p2Held = GameplayButtons.None) {
        simulation.Advance(
            new PlayerInputFrame {
                Tick = (uint)tick,
                MoveX = p1MoveX,
                Held = p1Buttons,
                Pressed = p1Buttons
            },
            new PlayerInputFrame {
                Tick = (uint)tick,
                MoveX = p2MoveX,
                Held = p2Buttons | p2Held,
                Pressed = p2Buttons
            });
    }
}
