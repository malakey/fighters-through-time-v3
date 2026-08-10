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
/// Fighter side: phased swings with committed startup/active frames, free
/// steering with committed facing during a swing (grounded and aerial),
/// movement that never cancels the string, chain-holding hitstun, the
/// buffered three-hit progression at the authored pace, the grounded block
/// stance, shared hitstun and knockback tables, and block-charge
/// regeneration. Story implements the identical rules through
/// PlayerController; the numbers come from the same static tables, so drift
/// in either mode breaks this suite or its Story counterparts.
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

        // The first active tick applies the 0.8x opener and the shared table's
        // opener hitstun in the same simulation tick.
        Advance(simulation, BasicComboRules.GroundStartupFrames[0]);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        int expectedDamage = FighterLoadout.Default(FighterCharacterID.Einstein).BasicDamage * 8 / 10;
        AssertThat(before.CurrentHP - struck.CurrentHP).IsEqual(expectedDamage);
        AssertThat(struck.HitstunFrames).IsEqual(BasicComboRules.HitstunFrames[0]);
    }

    [TestCase]
    public void GroundedSwingKeepsSteeringWithCommittedFacingWhileAerialKeepsAirDrift() {
        // Grounded: run up to speed, then swing while still holding the
        // direction — the attacker keeps moving at the normal run ramp for
        // the whole swing instead of decelerating to zero.
        var grounded = NewAdjacentSimulation(seed: 92, spawnDistance: 6);
        for (int tick = 0; tick < 20; tick++) {
            Advance(grounded, tick, p1MoveX: 127);
        }
        AssertThat(grounded.TryGetFighter(0, out FighterStateComponent beforeSwing)).IsTrue();
        FP64 xAtSwingStart = beforeSwing.Position.x;
        Advance(grounded, 20, p1MoveX: 127, p1Buttons: GameplayButtons.BasicAttack);
        for (int tick = 21; tick < 33; tick++) {
            Advance(grounded, tick, p1MoveX: 127);
            AssertThat(grounded.TryGetFighter(0, out FighterStateComponent moving)).IsTrue();
            AssertThat(moving.Velocity.x > FP64.Zero)
                .OverrideFailureMessage($"A grounded swing must keep full steering (tick {tick}).")
                .IsTrue();
        }
        AssertThat(grounded.TryGetFighterRuntime(0, out FighterRuntimeComponent midSwing)).IsTrue();
        AssertThat(midSwing.AttackPhase)
            .OverrideFailureMessage("The held direction must not have cancelled the swing.")
            .IsNotEqual(FighterBasicAttackRules.PhaseNone);
        AssertThat(grounded.TryGetFighter(0, out FighterStateComponent steered)).IsTrue();
        AssertThat(steered.Position.x > xAtSwingStart)
            .OverrideFailureMessage("A moving grounded attacker must keep advancing during the swing.")
            .IsTrue();

        // Facing stays committed for the duration of the string: steering
        // backward moves the fighter without flipping the hitbox direction.
        // Once the string fully ends, the held direction takes the facing.
        var reversed = NewAdjacentSimulation(seed: 93);
        Advance(reversed, 0, p1Buttons: GameplayButtons.BasicAttack);
        int stringEnd = BasicComboRules.GroundStartupFrames[0]
            + BasicComboRules.GroundActiveFrames[0]
            + BasicComboRules.GroundRecoveryFrames[0]
            + BasicComboRules.ChainHoldFrames;
        for (int tick = 1; tick < stringEnd; tick++) {
            Advance(reversed, tick, p1MoveX: -127);
            AssertThat(reversed.TryGetFighter(0, out FighterStateComponent committed)).IsTrue();
            AssertThat(committed.FacingRight)
                .OverrideFailureMessage($"Facing must stay committed mid-string (tick {tick}).")
                .IsEqual(1);
        }
        AssertThat(reversed.TryGetFighter(0, out FighterStateComponent backpedaling)).IsTrue();
        AssertThat(backpedaling.Velocity.x < FP64.Zero)
            .OverrideFailureMessage("Backward steering must move the attacker while facing holds.")
            .IsTrue();
        for (int tick = stringEnd; tick < stringEnd + 2; tick++) {
            Advance(reversed, tick, p1MoveX: -127);
        }
        AssertThat(reversed.TryGetFighter(0, out FighterStateComponent released)).IsTrue();
        AssertThat(released.FacingRight)
            .OverrideFailureMessage("The next swing may start with a new facing once the string ends.")
            .IsEqual(0);

        // Aerial: the same held drift keeps moving the fighter mid-swing.
        var aerial = NewAdjacentSimulation(seed: 95, spawnDistance: 6);
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
    public void HeldMovementNeverCancelsTheSwingAndTheChainSurvivesWhileMoving() {
        // Walk into the swing and keep the direction held through recovery and
        // the chain-hold window: the string must survive and hit two must land
        // while the attacker is still moving. (Movement-cancel also allowed a
        // moving attacker to restart hit one faster than the authored pace;
        // that exploit dies with the clause.)
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildZeroKnockbackAttacker()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 94,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        Advance(simulation, 0, p1MoveX: 127, p1Buttons: GameplayButtons.BasicAttack);
        int firstRecoveryTick = BasicComboRules.GroundStartupFrames[0] + BasicComboRules.GroundActiveFrames[0] + 1;
        for (int tick = 1; tick <= firstRecoveryTick; tick++) {
            Advance(simulation, tick, p1MoveX: 127);
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent inRecovery)).IsTrue();
        AssertThat(inRecovery.AttackPhase)
            .OverrideFailureMessage("Held movement during recovery must not cancel the swing.")
            .IsEqual(FighterBasicAttackRules.PhaseRecovery);

        // Ride the held direction to the chain-hold window...
        int holdTick = BasicComboRules.GroundStartupFrames[0]
            + BasicComboRules.GroundActiveFrames[0]
            + BasicComboRules.GroundRecoveryFrames[0];
        for (int tick = firstRecoveryTick + 1; tick <= holdTick; tick++) {
            Advance(simulation, tick, p1MoveX: 127);
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent holding)).IsTrue();
        AssertThat(holding.AttackPhase)
            .OverrideFailureMessage("Held movement during the chain-hold window must not reset the chain.")
            .IsEqual(FighterBasicAttackRules.PhaseChainHold);

        // ...and continue the string: hit two lands while moving.
        Advance(simulation, holdTick + 1, p1MoveX: 127, p1Buttons: GameplayButtons.BasicAttack);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent chained)).IsTrue();
        AssertThat(chained.ComboIndex).IsEqual(1);
        AssertThat(chained.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseStartup);
        for (int tick = holdTick + 2; tick <= holdTick + 1 + BasicComboRules.GroundStartupFrames[1]; tick++) {
            Advance(simulation, tick, p1MoveX: 127);
        }
        const int basicDamage = 10;
        int expected = basicDamage * 8 / 10 + basicDamage;
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        AssertThat(before.CurrentHP - struck.CurrentHP)
            .OverrideFailureMessage("Hit two must land from the moving chain.")
            .IsEqual(expected);
    }

    [TestCase]
    public void AMidStringVictimIsStillInHitstunWhenHitsTwoAndThreeConnect() {
        // The shared hitstun table (30/40/24) must hold the victim through the
        // buffered string's gaps: connect-to-connect is about 28 frames from
        // hit one to hit two and about 38 from hit two to the finisher.
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildZeroKnockbackAttacker()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 96,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        int hits = 0;
        int[] hitstunBeforeHit = new int[BasicComboRules.ComboHits];
        for (int tick = 0; tick <= 90 && hits < BasicComboRules.ComboHits; tick++) {
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent beforeTick)).IsTrue();
            int hpBefore = beforeTick.CurrentHP;
            int stunBefore = beforeTick.HitstunFrames;
            Advance(simulation, tick, p1Buttons: GameplayButtons.BasicAttack);
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent afterTick)).IsTrue();
            if (afterTick.CurrentHP < hpBefore) {
                hitstunBeforeHit[hits] = stunBefore;
                hits++;
            }
        }

        AssertThat(hits)
            .OverrideFailureMessage("The mashed string must land all three chain hits.")
            .IsEqual(BasicComboRules.ComboHits);
        AssertThat(hitstunBeforeHit[1] > 0)
            .OverrideFailureMessage("The victim must still be in hitstun when hit two connects.")
            .IsTrue();
        AssertThat(hitstunBeforeHit[2] > 0)
            .OverrideFailureMessage("The victim must still be in hitstun when the finisher connects.")
            .IsTrue();
    }

    [TestCase]
    public void MashingWhileMovingLandsNoFasterThanTheAuthoredStringPace() {
        // The dead exploit: movement-cancelling recovery let a moving masher
        // restart hit one faster than the authored string. With the clause
        // gone, mash-plus-move deals exactly the stationary chain's damage —
        // three full buffered strings inside 310 ticks, and not a point more.
        var moving = NewZeroKnockbackSimulation(seed: 97);
        var stationary = NewZeroKnockbackSimulation(seed: 97);
        const int ticks = 310;
        for (int tick = 0; tick < ticks; tick++) {
            Advance(moving, tick, p1MoveX: 127, p1Buttons: GameplayButtons.BasicAttack);
            Advance(stationary, tick, p1Buttons: GameplayButtons.BasicAttack);
        }

        const int basicDamage = 10;
        int fullChain = basicDamage * 8 / 10 + basicDamage + basicDamage * 15 / 10;
        AssertThat(moving.TryGetFighter(1, out FighterStateComponent movingTarget)).IsTrue();
        AssertThat(stationary.TryGetFighter(1, out FighterStateComponent stationaryTarget)).IsTrue();
        int movingDamage = movingTarget.MaxHP - movingTarget.CurrentHP;
        int stationaryDamage = stationaryTarget.MaxHP - stationaryTarget.CurrentHP;
        AssertThat(movingDamage)
            .OverrideFailureMessage("A moving masher must land hits at exactly the stationary pace.")
            .IsEqual(stationaryDamage);
        AssertThat(movingDamage)
            .OverrideFailureMessage("310 ticks of mash fit exactly three authored strings.")
            .IsEqual(3 * fullChain);
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

    private static FighterSimulation NewZeroKnockbackSimulation(int seed) => new(
        FighterLoadoutFactory.FromCharacterData(BuildZeroKnockbackAttacker()),
        FighterLoadout.Default(FighterCharacterID.Joan),
        seed: seed,
        spawnDistance: 1,
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
