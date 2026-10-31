using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The ability lockout (special global cooldown, user direction 2026-10-08),
/// Fighter side: <c>FighterRuntimeComponent.AbilityLockoutFrames</c> on
/// component 305. A swing start, a Special cast or an accepted Ultimate arms
/// 30 frames; Special 1, Special 2 and the Ultimate are refused until tick
/// T + 30, at no cost and without re-arming; basics and the movement ability
/// are never refused. Same-tick presses resolve swing, Special 1, Special 2,
/// Ultimate; the counter pauses in the fighter's own hitstop and is rollback
/// state. The CPU reads it from its observation and never picks a locked press.
///
/// <para>Pure C#: engine-free loadouts whose Specials are slow Projectile
/// executions with a 1-frame slot cooldown, so a spawned shot is the proof of
/// acceptance and only the lockout can refuse a re-press.</para>
/// </summary>
[TestSuite]
public class FighterAbilityLockoutTests {

    private const int Lockout = BasicComboRules.AbilityLockoutFrames;
    /// <summary>Quantized "Up held" (MoveY &lt; -30), as the sampler produces it.</summary>
    private const sbyte UpAxis = -127;

    // === Arming and refusal ===

    [TestCase]
    public void ASwingStartArmsThirtyFramesAndSpecialOneIsRefusedUntilTheThirtiethTick() {
        FighterSimulation simulation = NewSimulation(seed: 101);
        Step(simulation, GameplayButtons.BasicAttack);
        AssertThat(Runtime(simulation, 0).AttackPhase).IsEqual(FighterBasicAttackRules.PhaseStartup);
        AssertThat(LockoutOf(simulation, 0)).IsEqual(Lockout);

        for (int elapsed = 1; elapsed < Lockout; elapsed++) {
            bool probe = elapsed == 1 || elapsed == Lockout - 1;
            Step(simulation, probe ? GameplayButtons.Special1 : GameplayButtons.None);
            AssertThat(LockoutOf(simulation, 0)).IsEqual(Lockout - elapsed);
            if (!probe) continue;
            AssertThat(Shots(simulation, 0, 1))
                .OverrideFailureMessage($"Special 1 on T+{elapsed} must be refused.")
                .IsEqual(0);
            AssertThat(Runtime(simulation, 0).SpecialOneReuseFrames)
                .OverrideFailureMessage("A refused press spends no cooldown.")
                .IsEqual(0);
        }

        Step(simulation, GameplayButtons.Special1);
        AssertThat(Shots(simulation, 0, 1))
            .OverrideFailureMessage("Special 1 is legal again on T+30.")
            .IsEqual(1);
        AssertThat(Runtime(simulation, 0).SpecialOneReuseFrames > 0).IsTrue();
        AssertThat(LockoutOf(simulation, 0))
            .OverrideFailureMessage("The accepted cast re-arms the lockout.")
            .IsEqual(Lockout);
    }

    [TestCase]
    public void ASameTickSwingAndSpecialOneStartsTheSwingAndRefusesTheSpecial() {
        FighterSimulation simulation = NewSimulation(seed: 102);
        Step(simulation, GameplayButtons.BasicAttack | GameplayButtons.Special1);

        AssertThat(Runtime(simulation, 0).AttackPhase)
            .OverrideFailureMessage("The swing wins the tick.")
            .IsEqual(FighterBasicAttackRules.PhaseStartup);
        AssertThat(Shots(simulation, 0, 1)).IsEqual(0);
        AssertThat(Runtime(simulation, 0).SpecialOneReuseFrames).IsEqual(0);
        AssertThat(LockoutOf(simulation, 0)).IsEqual(Lockout);
    }

    [TestCase]
    public void ASameTickSpecialOneAndSpecialTwoCastsOnlySpecialOne() {
        FighterSimulation simulation = NewSimulation(seed: 103);
        Step(simulation, GameplayButtons.Special1 | GameplayButtons.Special2);

        AssertThat(Shots(simulation, 0, 1)).IsEqual(1);
        AssertThat(Shots(simulation, 0, 2))
            .OverrideFailureMessage("Special 1 beats Special 2 and locks it out.")
            .IsEqual(0);
        AssertThat(Runtime(simulation, 0).SpecialOneReuseFrames > 0).IsTrue();
        AssertThat(Runtime(simulation, 0).SpecialTwoReuseFrames).IsEqual(0);
        AssertThat(LockoutOf(simulation, 0)).IsEqual(Lockout);
    }

    [TestCase]
    public void ASpecialLocksTheOtherSlotAndItsOwnRecastForExactlyThirtyTicks() {
        // Special 2 after Special 1: refused on t+29, accepted on t+30.
        FighterSimulation other = NewSimulation(seed: 104);
        Step(other, GameplayButtons.Special1);
        Idle(other, Lockout - 2);
        Step(other, GameplayButtons.Special2);
        AssertThat(Shots(other, 0, 2)).OverrideFailureMessage("Special 2 on t+29 must be refused.").IsEqual(0);
        AssertThat(Runtime(other, 0).SpecialTwoReuseFrames).IsEqual(0);
        AssertThat(LockoutOf(other, 0)).IsEqual(1);
        Step(other, GameplayButtons.Special2);
        AssertThat(Shots(other, 0, 2)).OverrideFailureMessage("Special 2 on t+30 is accepted.").IsEqual(1);
        AssertThat(LockoutOf(other, 0)).IsEqual(Lockout);

        // Special 1 recast: the slot's own 2 s reuse delay would refuse a recast
        // this early (the meter and Defy update replaced the 1-frame slot
        // cooldown this fixture used), so it is cleared by hand and the lockout
        // alone refuses it, as before.
        FighterSimulation recast = NewSimulation(seed: 105);
        Step(recast, GameplayButtons.Special1);
        Step(recast, GameplayButtons.None);
        AssertThat(Runtime(recast, 0).SpecialOneReuseFrames)
            .OverrideFailureMessage("Harness: the slot's reuse delay is running.")
            .IsEqual(BasicComboRules.SpecialReuseDelayFrames - 1);
        AssertThat(recast.SeedFighterSpecialReuseForTest(0, 0, 0)).IsTrue();
        Idle(recast, Lockout - 3);
        Step(recast, GameplayButtons.Special1);
        AssertThat(Shots(recast, 0, 1)).OverrideFailureMessage("The t+29 recast must be refused.").IsEqual(1);
        Step(recast, GameplayButtons.Special1);
        AssertThat(Shots(recast, 0, 1)).OverrideFailureMessage("The t+30 recast is accepted.").IsEqual(2);
    }

    [TestCase]
    public void TheUltimateIsRefusedWithTheMeterUntouchedUntilTheThirtiethTickAndArmsOnAcceptance() {
        FighterSimulation simulation = NewSimulation(seed: 106);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        Step(simulation, GameplayButtons.BasicAttack);
        Idle(simulation, Lockout - 2);

        Step(simulation, GameplayButtons.Ultimate);
        AssertThat(Fighter(simulation, 0).Influence)
            .OverrideFailureMessage("A refused Ultimate spends no meter.")
            .IsEqual(FP64.FromInt(100));
        AssertThat(UltimatePhase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseNone);
        AssertThat(LockoutOf(simulation, 0)).IsEqual(1);

        Step(simulation, GameplayButtons.Ultimate);
        AssertThat(Fighter(simulation, 0).Influence).IsEqual(FP64.Zero);
        AssertThat(UltimatePhase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseWindup);
        AssertThat(LockoutOf(simulation, 0))
            .OverrideFailureMessage("Acceptance arms the lockout.")
            .IsEqual(Lockout);
    }

    [TestCase]
    public void AnAcceptedUltimateTakesTheTickFromTheSpecialsAndTheMovementAbility() {
        // The default gamepad Ultimate is the Movement + Special 2 chord, so all
        // three can arrive on one tick: the Ultimate the meter pays for wins.
        FighterSimulation chord = NewSimulation(seed: 107);
        AssertThat(chord.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        Step(chord, GameplayButtons.Special1 | GameplayButtons.Special2
            | GameplayButtons.MovementAbility | GameplayButtons.Ultimate);

        AssertThat(UltimatePhase(chord, 0))
            .OverrideFailureMessage("An accepted Ultimate claims the tick.")
            .IsEqual(FighterUltimateActivationRules.PhaseWindup);
        AssertThat(Shots(chord, 0, 1)).IsEqual(0);
        AssertThat(Shots(chord, 0, 2)).IsEqual(0);
        AssertThat(Runtime(chord, 0).SpecialOneReuseFrames).IsEqual(0);
        AssertThat(Runtime(chord, 0).SpecialTwoReuseFrames).IsEqual(0);
        AssertThat(Runtime(chord, 0).MovementCooldownFrames)
            .OverrideFailureMessage("The movement ability yields to the Ultimate too.")
            .IsEqual(0);
        AssertThat(LockoutOf(chord, 0)).IsEqual(Lockout);

        // An Ultimate the meter cannot pay for leaves the tick to the special.
        FighterSimulation unpaid = NewSimulation(seed: 117);
        Step(unpaid, GameplayButtons.Special1 | GameplayButtons.Ultimate);
        AssertThat(Shots(unpaid, 0, 1)).IsEqual(1);
        AssertThat(UltimatePhase(unpaid, 0)).IsEqual(FighterUltimateActivationRules.PhaseNone);
        AssertThat(LockoutOf(unpaid, 0)).IsEqual(Lockout);
    }

    [TestCase]
    public void AFighterCapturedOnTheTickItPressesUltimateAndASpecialTakesNoAction() {
        // The ability system reads UltimatePressAccepted before the opponent's
        // activation strike resolves. If that strike captures the fighter later
        // in the same tick, the special held back for the Ultimate must not
        // reach BuildIntent's generic melee branch as a phantom hit.
        const int strikePressTick = 2;
        FighterSimulation probe = CaptureScenario(seed: 120);
        int captureTick = -1;
        for (int tick = 0; tick < 60 && captureTick < 0; tick++) {
            probe.Advance(
                Input(tick, GameplayButtons.None),
                Input(tick, tick == strikePressTick ? GameplayButtons.Ultimate : GameplayButtons.None));
            if (Activation(probe, 0).CapturedFrames > 0) captureTick = tick;
        }
        AssertThat(captureTick > strikePressTick)
            .OverrideFailureMessage("Harness: player two's activation strike must capture player one.")
            .IsTrue();

        FighterSimulation simulation = CaptureScenario(seed: 120);
        int opponentMaxHP = Fighter(simulation, 1).MaxHP;
        for (int tick = 0; tick <= captureTick; tick++) {
            simulation.Advance(
                Input(tick, tick == captureTick ? GameplayButtons.Ultimate | GameplayButtons.Special1 : GameplayButtons.None),
                Input(tick, tick == strikePressTick ? GameplayButtons.Ultimate : GameplayButtons.None));
        }
        AssertThat(Activation(simulation, 0).CapturedFrames > 0).IsTrue();
        AssertThat(Fighter(simulation, 1).CurrentHP)
            .OverrideFailureMessage("A fighter captured this tick must not land its held-back special as a melee hit.")
            .IsEqual(opponentMaxHP);
        AssertThat(Runtime(simulation, 0).SpecialOneReuseFrames).IsEqual(0);
        AssertThat(Shots(simulation, 0, 1)).IsEqual(0);
        AssertThat(Fighter(simulation, 0).Influence)
            .OverrideFailureMessage("The refused Ultimate keeps its meter.")
            .IsEqual(FP64.FromInt(100));
    }

    [TestCase]
    public void LincolnsSplittingStrikeTakesATickWhoseUltimateTheMeterCannotPayFor() {
        // Splitting Strike resolves in BuildIntent, after Ultimate acceptance. A
        // pressed but unpaid Ultimate no longer blocks it; a paid one wins.
        FighterAbilityLoadout splitting = ProjectileModes() with {
            SpecialTwoExecutionType = 0,
            SpecialTwoHit = new FighterAbilityHitData {
                HitstunFrames = 12,
                BlockClass = (int)BlockClass.Special,
                Launches = false,
                Delivery = (int)HitDelivery.DirectHit,
                Origin = (int)HitOrigin.Special
            }
        };
        FighterSimulation Lincoln(int seed) => new(
            Loadout(FighterCharacterID.Lincoln, splitting), Loadout(FighterCharacterID.Einstein, ProjectileModes()),
            seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);

        FighterSimulation unpaid = Lincoln(118);
        Idle(unpaid, 2);
        int maxHP = Fighter(unpaid, 1).MaxHP;
        Step(unpaid, GameplayButtons.Special2 | GameplayButtons.Ultimate);
        AssertThat(Fighter(unpaid, 1).CurrentHP < maxHP)
            .OverrideFailureMessage("Splitting Strike lands when the Ultimate cannot be paid for.")
            .IsTrue();
        AssertThat(Runtime(unpaid, 0).SpecialTwoReuseFrames > 0).IsTrue();
        AssertThat(LockoutOf(unpaid, 0)).IsEqual(Lockout);

        FighterSimulation paid = Lincoln(119);
        Idle(paid, 2);
        AssertThat(paid.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        Step(paid, GameplayButtons.Special2 | GameplayButtons.Ultimate);
        AssertThat(UltimatePhase(paid, 0)).IsEqual(FighterUltimateActivationRules.PhaseWindup);
        AssertThat(Runtime(paid, 0).SpecialTwoReuseFrames)
            .OverrideFailureMessage("A paid Ultimate claims the tick from Splitting Strike.")
            .IsEqual(0);
    }

    [TestCase]
    public void ABasicAttackIsNeverRefusedByTheLockout() {
        FighterSimulation simulation = NewSimulation(seed: 108);
        Step(simulation, GameplayButtons.Special1);
        Step(simulation, GameplayButtons.BasicAttack);

        AssertThat(Runtime(simulation, 0).AttackPhase)
            .OverrideFailureMessage("The swing starts one tick after a special.")
            .IsEqual(FighterBasicAttackRules.PhaseStartup);
        AssertThat(LockoutOf(simulation, 0))
            .OverrideFailureMessage("The swing re-arms the full lockout.")
            .IsEqual(Lockout);
    }

    [TestCase]
    public void TheMovementAbilityIsNeverRefusedAndNeverArms() {
        FighterSimulation simulation = NewSimulation(seed: 109);
        Step(simulation, GameplayButtons.Special1);
        AssertThat(LockoutOf(simulation, 0)).IsEqual(Lockout);

        Step(simulation, GameplayButtons.MovementAbility);
        AssertThat(Runtime(simulation, 0).MovementCooldownFrames > 0)
            .OverrideFailureMessage("The movement ability is accepted inside the lockout.")
            .IsTrue();
        AssertThat(LockoutOf(simulation, 0))
            .OverrideFailureMessage("The movement ability does not re-arm the lockout.")
            .IsEqual(Lockout - 1);
        Step(simulation, GameplayButtons.None);
        AssertThat(LockoutOf(simulation, 0)).IsEqual(Lockout - 2);

        // On one tick an accepted special still takes precedence over the
        // movement ability, the order Story polls them in.
        FighterSimulation sameTick = NewSimulation(seed: 121);
        Step(sameTick, GameplayButtons.Special1 | GameplayButtons.MovementAbility);
        AssertThat(Shots(sameTick, 0, 1)).IsEqual(1);
        AssertThat(Runtime(sameTick, 0).MovementCooldownFrames)
            .OverrideFailureMessage("A special accepted this tick takes it from the movement ability.")
            .IsEqual(0);
    }

    [TestCase]
    public void ARefusedPressArmsNothing() {
        FighterSimulation simulation = NewSimulation(seed: 110);
        Step(simulation, GameplayButtons.BasicAttack);
        Idle(simulation, 19);
        Step(simulation, GameplayButtons.Special1);
        AssertThat(Shots(simulation, 0, 1)).IsEqual(0);
        AssertThat(LockoutOf(simulation, 0))
            .OverrideFailureMessage("The refused press on T+20 left the swing's countdown alone.")
            .IsEqual(Lockout - 20);

        Idle(simulation, 9);
        Step(simulation, GameplayButtons.Special1);
        AssertThat(Shots(simulation, 0, 1))
            .OverrideFailureMessage("Special 1 on T+30 is accepted: the T+20 press armed nothing.")
            .IsEqual(1);
    }

    // === Hitstop ===

    [TestCase]
    public void TheLockoutHoldsThroughTheAttackersHitstopAndResumesAfter() {
        FighterSimulation simulation = NewSimulation(seed: 111, spawnDistance: 1);
        Approach(simulation);
        Step(simulation, GameplayButtons.BasicAttack);
        int previous = LockoutOf(simulation, 0);
        AssertThat(previous).IsEqual(Lockout);

        int assignedHitstop = -1;
        int frozenTicks = 0;
        bool resumed = false;
        for (int step = 0; step < 40 && !resumed; step++) {
            int hitstopBefore = Verb(simulation, 0).HitstopFrames;
            Step(simulation, GameplayButtons.None);
            int now = LockoutOf(simulation, 0);
            if (hitstopBefore > 0) {
                frozenTicks++;
                AssertThat(now)
                    .OverrideFailureMessage($"The lockout must not tick in hitstop (step {step}).")
                    .IsEqual(previous);
            } else {
                AssertThat(now)
                    .OverrideFailureMessage($"The lockout ticks once per free tick (step {step}).")
                    .IsEqual(previous > 0 ? previous - 1 : 0);
                if (frozenTicks > 0) resumed = true;
            }
            if (assignedHitstop < 0 && Verb(simulation, 0).HitstopFrames > 0) {
                assignedHitstop = Verb(simulation, 0).HitstopFrames;
            }
            previous = now;
        }
        AssertThat(assignedHitstop > 0)
            .OverrideFailureMessage("Harness: the swing never landed, so the attacker never froze.")
            .IsTrue();
        AssertThat(frozenTicks).IsEqual(assignedHitstop);
        AssertThat(resumed).OverrideFailureMessage("The countdown never resumed after the freeze.").IsTrue();
        AssertThat(previous > 0)
            .OverrideFailureMessage("Harness: the lockout must still be running when the freeze ends.")
            .IsTrue();
    }

    // === Rollback ===

    [TestCase]
    public void TheLockoutIsRollbackStateAndReplaysBitIdentically() {
        FighterSimulation simulation = NewSimulation(seed: 112);
        Step(simulation, GameplayButtons.BasicAttack);
        Idle(simulation, 4);
        AssertThat(LockoutOf(simulation, 0)).IsEqual(Lockout - 4);
        byte[] snapshot = simulation.CaptureFullState();

        // Lockout after frame f is 26 - (f + 1): S1 refused on 10, accepted on
        // 25; the swing on 32 re-arms; S2 on 39 is refused again.
        var script = new GameplayButtons[45];
        script[10] = GameplayButtons.Special1;
        script[25] = GameplayButtons.Special1;
        script[32] = GameplayButtons.BasicAttack;
        script[39] = GameplayButtons.Special2;

        var hashes = new long[script.Length];
        var lockouts = new int[script.Length];
        for (int frame = 0; frame < script.Length; frame++) {
            hashes[frame] = Step(simulation, script[frame]);
            lockouts[frame] = LockoutOf(simulation, 0);
            if (frame == 10) AssertThat(Shots(simulation, 0, 1)).IsEqual(0);
            if (frame == 25) AssertThat(Shots(simulation, 0, 1)).IsEqual(1);
        }
        AssertThat(Shots(simulation, 0, 2)).IsEqual(0);

        simulation.RestoreFullState(snapshot);
        AssertThat(LockoutOf(simulation, 0)).IsEqual(Lockout - 4);
        for (int frame = 0; frame < script.Length; frame++) {
            long replayed = Step(simulation, script[frame]);
            AssertThat(replayed).OverrideFailureMessage($"Restored replay diverged at frame {frame}.").IsEqual(hashes[frame]);
            AssertThat(LockoutOf(simulation, 0)).IsEqual(lockouts[frame]);
        }
    }

    [TestCase]
    public void ACorrectedSpecialPressConvergesToTheCleanRunOnEitherSideOfTheBoundary() {
        // Predicted away (nothing arrived), truly on swing + 30.
        AssertCorrectionConverges(sentPressTick: -1, truePressTick: RemoteSwingTick + Lockout);
        // Arrived on swing + 29 (refused), corrected to + 30 (accepted).
        AssertCorrectionConverges(RemoteSwingTick + Lockout - 1, RemoteSwingTick + Lockout);
        // Arrived on swing + 30 (accepted), corrected to + 29 (refused).
        AssertCorrectionConverges(RemoteSwingTick + Lockout, RemoteSwingTick + Lockout - 1);
    }

    // === Melee intents and kit ordering ===

    [TestCase]
    public void AMeleeSpecialIntentIsGatedByTheLockoutToo() {
        FighterAbilityLoadout melee = FighterAbilityLoadout.Default with {
            SpecialOneExecutionType = 0,
            SpecialOneHit = new FighterAbilityHitData {
                HitstunFrames = 12,
                BlockClass = (int)BlockClass.Special,
                Launches = false,
                Delivery = (int)HitDelivery.DirectHit,
                Origin = (int)HitOrigin.Special
            }
        };
        var simulation = new FighterSimulation(
            Loadout(FighterCharacterID.Einstein, melee), Loadout(FighterCharacterID.Einstein, ProjectileModes()),
            seed: 113, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        Idle(simulation, 2);
        int maxHP = Fighter(simulation, 1).MaxHP;

        // The up-attack arms the lockout and whiffs at the 2-unit gap (1.2 reach),
        // while the melee Special's 2-unit gate reaches.
        Step(simulation, GameplayButtons.BasicAttack, moveY: UpAxis);
        AssertThat((Runtime(simulation, 0).AttackFlags & FighterBasicAttackRules.FlagUpAttack) != 0).IsTrue();
        AssertThat(LockoutOf(simulation, 0)).IsEqual(Lockout);

        Idle(simulation, 14);
        Step(simulation, GameplayButtons.Special1);
        AssertThat(Fighter(simulation, 1).CurrentHP)
            .OverrideFailureMessage("A melee Special inside the lockout builds no intent.")
            .IsEqual(maxHP);
        AssertThat(Runtime(simulation, 0).SpecialOneReuseFrames).IsEqual(0);

        Idle(simulation, 14);
        AssertThat(Fighter(simulation, 1).CurrentHP)
            .OverrideFailureMessage("Harness: the up-attack must whiff.")
            .IsEqual(maxHP);
        Step(simulation, GameplayButtons.Special1);
        AssertThat(Fighter(simulation, 1).CurrentHP < maxHP)
            .OverrideFailureMessage("The melee Special lands on T+30.")
            .IsTrue();
        AssertThat(Runtime(simulation, 0).SpecialOneReuseFrames > 0).IsTrue();
        AssertThat(LockoutOf(simulation, 0)).IsEqual(Lockout);
    }

    [TestCase]
    public void TubmansSameTickSpecialsCastConductorsCallAndNotForesight() {
        FighterSimulation both = NewSimulation(seed: 114, character: FighterCharacterID.Tubman);
        Step(both, GameplayButtons.Special1 | GameplayButtons.Special2);
        AssertThat(Shots(both, 0, 1)).IsEqual(1);
        var shots = new List<FighterProjectileComponent>();
        both.CopyProjectilesTo(shots);
        AssertThat(shots[0].ProjectileTypeID).IsEqual(FighterConductorsCallRules.ProjectileTypeID);
        AssertThat(both.TryGetFighterCounter(0, out FighterCounterComponent counter)).IsTrue();
        AssertThat(counter.Phase)
            .OverrideFailureMessage("Special 1 resolves before Foresight and locks it out.")
            .IsEqual(FighterForesightRules.PhaseNone);
        AssertThat(Runtime(both, 0).SpecialTwoReuseFrames).IsEqual(0);
        AssertThat(LockoutOf(both, 0)).IsEqual(Lockout);

        // Control: Special 2 alone reaches Foresight and arms the lockout.
        FighterSimulation alone = NewSimulation(seed: 115, character: FighterCharacterID.Tubman);
        Step(alone, GameplayButtons.Special2);
        AssertThat(alone.TryGetFighterCounter(0, out FighterCounterComponent stance)).IsTrue();
        AssertThat(stance.Phase).IsEqual(FighterForesightRules.PhaseStartup);
        AssertThat(Runtime(alone, 0).SpecialTwoReuseFrames > 0).IsTrue();
        AssertThat(LockoutOf(alone, 0)).IsEqual(Lockout);
    }

    // === CPU ===

    [TestCase]
    public void TheCpuNeverPicksASpecialOrTheUltimateThatWouldLandInsideItsLockout() {
        CpuDecisionObservation close = Neutral(targetX: 1);
        CpuDecisionObservation ranged = Neutral(targetX: 4);
        CpuDecisionObservation finisher = Neutral(targetX: 1);
        finisher.InfluenceRaw = FP64.FromInt(100).RawValue;
        finisher.TargetCurrentHP = 20;

        // Clear: the Hard band picks every one of these in these setups.
        AssertThat(CountPresses(close, 0, GameplayButtons.Special1) > 0).IsTrue();
        AssertThat(CountPresses(close, 0, GameplayButtons.Special2) > 0).IsTrue();
        AssertThat(CountPresses(ranged, 0, GameplayButtons.Special1) > 0).IsTrue();
        AssertThat(CountPresses(finisher, 0, GameplayButtons.Ultimate) > 0).IsTrue();

        // Locked: never, and the close-range table still swings.
        AssertThat(CountPresses(close, 15, GameplayButtons.Special1)).IsEqual(0);
        AssertThat(CountPresses(close, 15, GameplayButtons.Special2)).IsEqual(0);
        AssertThat(CountPresses(close, 15, GameplayButtons.BasicAttack) > 0).IsTrue();
        AssertThat(CountPresses(ranged, 15, GameplayButtons.Special1)).IsEqual(0);
        AssertThat(CountPresses(finisher, 15, GameplayButtons.Ultimate)).IsEqual(0);
        AssertThat(CountPresses(finisher, 15, GameplayButtons.Special1)).IsEqual(0);
        // A viable Ultimate waiting on the lockout is not fed more swings: a
        // buffered chain hit would re-arm the lockout past the string.
        AssertThat(CountPresses(finisher, 15, GameplayButtons.BasicAttack))
            .OverrideFailureMessage("The CPU must let its swing finish while its Ultimate waits on the lockout.")
            .IsEqual(0);

        // The read is taken at delivery: a lockout that runs out within the
        // band's minimum reaction delay no longer refuses the press it will meet.
        int minimumDelay = FighterCpuController.HardReactionDelayMinFrames;
        AssertThat(CountPresses(close, minimumDelay, GameplayButtons.Special1) > 0)
            .OverrideFailureMessage("A lockout that clears before delivery must not hold the special back.")
            .IsTrue();
        AssertThat(CountPresses(finisher, minimumDelay, GameplayButtons.Ultimate) > 0).IsTrue();
        AssertThat(CountPresses(close, minimumDelay + 1, GameplayButtons.Special1)).IsEqual(0);
        AssertThat(CountPresses(finisher, minimumDelay + 1, GameplayButtons.Ultimate)).IsEqual(0);

        // The recovery planner's validated mobility Special is gated the same way.
        CpuDecisionObservation offStage = Neutral(targetX: 0);
        offStage.SelfPositionXRaw = FP64.FromInt(-8).RawValue;
        offStage.SelfPositionYRaw = FP64.FromInt(-1).RawValue;
        offStage.SelfVelocityYRaw = FP64.FromInt(-6).RawValue;
        offStage.IsGrounded = 0;
        offStage.RemainingJumps = 0;
        offStage.MovementCooldownFrames = 200;
        offStage.HasStageBounds = 1;
        offStage.LeftWallRaw = FP64.FromInt(-9).RawValue;
        offStage.RightWallRaw = FP64.FromInt(9).RawValue;
        offStage.CeilingRaw = FP64.FromInt(9).RawValue;
        offStage.BottomBlastZoneRaw = FP64.FromInt(-5).RawValue;
        var mobility = new CpuRecoveryProfile {
            HasKit = true,
            Character = FighterCharacterID.Einstein,
            MobilitySpecial = CpuMobilitySpecialSlot.SpecialOne
        };
        AssertThat(CountPresses(offStage, 0, GameplayButtons.Special1, mobility) > 0).IsTrue();
        AssertThat(CountPresses(offStage, 15, GameplayButtons.Special1, mobility)).IsEqual(0);
    }

    [TestCase]
    public void TheCpuObservationCarriesTheFightersOwnLockout() {
        var self = new FighterStateComponent { PlayerID = 0, Stocks = 3, CurrentHP = 100, MaxHP = 100 };
        var target = new FighterStateComponent { PlayerID = 1, Stocks = 3, CurrentHP = 100, MaxHP = 100 };
        var selfRuntime = new FighterRuntimeComponent { AbilityLockoutFrames = 17 };
        var targetRuntime = new FighterRuntimeComponent { AbilityLockoutFrames = 4 };
        AssertThat(FighterCpuController.Observe(in self, in selfRuntime, in target, in targetRuntime).AbilityLockoutFrames)
            .OverrideFailureMessage("The observation reads the CPU's own lockout, not the target's.")
            .IsEqual(17);

        FighterSimulation simulation = NewSimulation(seed: 116);
        Step(simulation, GameplayButtons.BasicAttack);
        Idle(simulation, 10);
        FighterStateComponent live = Fighter(simulation, 0);
        FighterRuntimeComponent liveRuntime = Runtime(simulation, 0);
        FighterStateComponent opponent = Fighter(simulation, 1);
        FighterRuntimeComponent opponentRuntime = Runtime(simulation, 1);
        AssertThat(FighterCpuController.Observe(in live, in liveRuntime, in opponent, in opponentRuntime).AbilityLockoutFrames)
            .IsEqual(Lockout - 10);
    }

    // === Harness ===

    private const int RemoteSwingTick = 5;
    /// <summary>The first remote frame that arrives late: one before the legal press.</summary>
    private const int FirstLateTick = RemoteSwingTick + Lockout - 1;
    private const int LastLateTick = FirstLateTick + 10;

    /// <summary>
    /// Player two swings on <see cref="RemoteSwingTick"/> and presses Special 1
    /// on <paramref name="truePressTick"/>. The network simulation receives
    /// player two's frames on time until <see cref="FirstLateTick"/>, then runs
    /// on the frame actually sent (a press on <paramref name="sentPressTick"/>,
    /// -1 for none) and prediction, and is finally corrected with the truth. It
    /// must land on the clean run's hash and stay there.
    /// </summary>
    private static void AssertCorrectionConverges(int sentPressTick, int truePressTick) {
        const int finalTick = 70;
        FighterSimulation clean = NewSimulation(seed: 7101);
        var cleanHashes = new long[finalTick];
        int cleanShots = 0;
        int cleanLockout = 0;
        for (int tick = 0; tick < finalTick; tick++) {
            cleanHashes[tick] = clean.Advance(Input(tick, GameplayButtons.None), RemoteInput(tick, truePressTick));
            if (tick != LastLateTick) continue;
            cleanShots = Shots(clean, 1, 1);
            cleanLockout = LockoutOf(clean, 1);
        }

        FighterSimulation network = NewSimulation(seed: 7101);
        for (int tick = 0; tick <= LastLateTick; tick++) {
            if (tick < FirstLateTick) {
                network.Advance(Input(tick, GameplayButtons.None), RemoteInput(tick, truePressTick));
            } else if (tick == sentPressTick) {
                network.Advance(Input(tick, GameplayButtons.None), RemoteInput(tick, sentPressTick));
            } else {
                network.AdvanceWithPredictedPlayerTwo(Input(tick, GameplayButtons.None));
            }
        }
        AssertThat(Shots(network, 1, 1))
            .OverrideFailureMessage($"Harness: the misprediction (sent {sentPressTick}, true {truePressTick}) must cross the lockout boundary.")
            .IsNotEqual(cleanShots);

        for (int tick = FirstLateTick; tick <= LastLateTick; tick++) {
            AssertThat(network.CorrectPlayerTwoInput(tick, RemoteInput(tick, truePressTick))).IsTrue();
        }
        AssertThat(network.CurrentHash)
            .OverrideFailureMessage($"Corrected run (sent {sentPressTick}, true {truePressTick}) did not converge.")
            .IsEqual(cleanHashes[LastLateTick]);
        AssertThat(Shots(network, 1, 1)).IsEqual(cleanShots);
        AssertThat(LockoutOf(network, 1)).IsEqual(cleanLockout);

        for (int tick = LastLateTick + 1; tick < finalTick; tick++) {
            long hash = network.Advance(Input(tick, GameplayButtons.None), RemoteInput(tick, truePressTick));
            AssertThat(hash).OverrideFailureMessage($"Diverged after convergence at tick {tick}.").IsEqual(cleanHashes[tick]);
        }
    }

    private static PlayerInputFrame RemoteInput(int tick, int pressTick) => Input(
        tick,
        tick == RemoteSwingTick ? GameplayButtons.BasicAttack
            : tick == pressTick ? GameplayButtons.Special1
            : GameplayButtons.None);

    private static int CountPresses(
        CpuDecisionObservation observation, int lockoutFrames, GameplayButtons button,
        CpuRecoveryProfile? profile = null) {
        observation.AbilityLockoutFrames = lockoutFrames;
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 20261008, null, null, null, profile);
        PlayerInputFrame previous = default;
        int presses = 0;
        for (uint tick = 0; tick < 600; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsPressed(button)) presses++;
        }
        return presses;
    }

    private static CpuDecisionObservation Neutral(int targetX) => new() {
        SelfPositionXRaw = FP64.Zero.RawValue,
        TargetPositionXRaw = FP64.FromInt(targetX).RawValue,
        Stocks = 3,
        IsGrounded = 1,
        RemainingJumps = 1,
        SelfCurrentHP = 100,
        SelfMaxHP = 100,
        TargetCurrentHP = 100,
        TargetMaxHP = 100
    };

    /// <summary>
    /// Walks player one in until the opponent is inside the string's reach,
    /// then lets the run decelerate (the FighterKnockdownTests approach).
    /// </summary>
    private static void Approach(FighterSimulation simulation) {
        for (int step = 0; step < 60; step++) {
            bool close = Fighter(simulation, 1).Position.x - Fighter(simulation, 0).Position.x <= FP64.FromDouble(1.2);
            Step(simulation, GameplayButtons.None, moveX: close ? (sbyte)0 : (sbyte)127);
            if (close) break;
        }
        Idle(simulation, 20);
    }

    /// <summary>Slow (0.5 u/s), long-lived shots: in flight for the whole of every case.</summary>
    private static FighterAbilityLoadout ProjectileModes() => FighterAbilityLoadout.Default with {
        SpecialOneExecutionType = 1,
        SpecialTwoExecutionType = 1,
        SpecialOneProjectileSpeed = FP64.FromDouble(0.5),
        SpecialTwoProjectileSpeed = FP64.FromDouble(0.5),
        SpecialOneProjectileLifetimeFrames = 600,
        SpecialTwoProjectileLifetimeFrames = 600
    };

    private static FighterSimulation NewSimulation(
        int seed, int spawnDistance = 4, FighterCharacterID character = FighterCharacterID.Einstein) {
        var simulation = new FighterSimulation(
            Loadout(character, ProjectileModes()),
            Loadout(FighterCharacterID.Einstein, ProjectileModes()),
            seed: seed, spawnDistance: spawnDistance, rules: FighterMatchRules.Disabled);
        return simulation;
    }

    /// <summary>The Default kit's numbers with zero authored Special cooldowns (1 frame after PositiveCooldown).</summary>
    private static FighterLoadout Loadout(FighterCharacterID character, FighterAbilityLoadout modes) => new(
        (int)character,
        100, 3, 2,
        10, 14, 12, 20,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        FP64.One,
        FP64.FromInt(7),
        FP64.FromDouble(11.5),
        FP64.FromInt(3),
        FP64.FromInt(4),
        FP64.FromInt(4),
        FP64.FromInt(5),
        FP64.One, FP64.One, FP64.One,
        modes);

    private static int Shots(FighterSimulation simulation, int ownerID, int slot) {
        int typeID = Fighter(simulation, ownerID).CharacterID * 10 + slot;
        var projectiles = new List<FighterProjectileComponent>();
        simulation.CopyProjectilesTo(projectiles);
        int count = 0;
        foreach (FighterProjectileComponent projectile in projectiles) {
            if (projectile.OwnerPlayerID == ownerID && projectile.ProjectileTypeID == typeID) count++;
        }
        return count;
    }

    private static int LockoutOf(FighterSimulation simulation, int playerID) =>
        Runtime(simulation, playerID).AbilityLockoutFrames;

    /// <summary>Both fighters at full meter, two units apart: player two's default sweep reaches player one.</summary>
    private static FighterSimulation CaptureScenario(int seed) {
        FighterSimulation simulation = NewSimulation(seed, spawnDistance: 1);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        AssertThat(simulation.SeedFighterInfluenceForTest(1, 100)).IsTrue();
        return simulation;
    }

    private static FighterUltimateActivationComponent Activation(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterUltimateActivation(playerID, out FighterUltimateActivationComponent activation)).IsTrue();
        return activation;
    }

    private static int UltimatePhase(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterUltimateActivation(playerID, out FighterUltimateActivationComponent activation)).IsTrue();
        return activation.Phase;
    }

    private static FighterStateComponent Fighter(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent state)).IsTrue();
        return state;
    }

    private static FighterRuntimeComponent Runtime(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
        return runtime;
    }

    private static FighterVerbComponent Verb(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
        return verb;
    }

    private static long Step(
        FighterSimulation simulation, GameplayButtons playerOne, sbyte moveX = 0, sbyte moveY = 0) =>
        simulation.Advance(
            Input(simulation.CurrentTick, playerOne, moveX, moveY),
            Input(simulation.CurrentTick, GameplayButtons.None));

    private static void Idle(FighterSimulation simulation, int frames) {
        for (int frame = 0; frame < frames; frame++) Step(simulation, GameplayButtons.None);
    }

    private static PlayerInputFrame Input(int tick, GameplayButtons buttons, sbyte moveX = 0, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = buttons,
        Pressed = buttons
    };
}
