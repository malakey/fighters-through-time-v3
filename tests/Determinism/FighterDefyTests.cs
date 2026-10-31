using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The active Defy History in the Fighter sim (meter and Defy update 2026-10-31, plan
/// sections 2.4 and 2.6): the press, the 50 Influence cost, the 30-frame clock with its
/// six invulnerable frames, the claim on the tick, the legality matrix, the hitstop buffer,
/// the grab release, Sudden Death, and rollback identity. The blast's effects on the
/// opponent are in <c>FighterDefyBlastTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterDefyTests {

    private const int Invulnerable = BasicComboRules.DefyInvulnerableFrames;
    private const int BlastFrame = BasicComboRules.DefyBlastFrame;
    private const int Total = BasicComboRules.DefyTotalFrames;

    [TestCase]
    public void ADefyPressSpendsFiftyInfluenceStartsFrameOneAndArmsNeitherLockout() {
        FighterSimulation simulation = NewSimulation(seed: 901, spawnDistance: 4);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 80)).IsTrue();
        Step(simulation, GameplayButtons.Defy);

        AssertThat(Action(simulation, 0).DefyFrames).IsEqual(1);
        AssertThat(Fighter(simulation, 0).Influence.RawValue)
            .OverrideFailureMessage("A Defy costs exactly 50 Influence, never scaled.")
            .IsEqual(FP64.FromInt(30).RawValue);
        AssertThat(Fighter(simulation, 0).InvulnerabilityFrames).IsEqual(Invulnerable);
        AssertThat(FighterDefyRules.Counter(Action(simulation, 0)))
            .OverrideFailureMessage("The presentation counter has not stepped at frame 1: it steps on the blast frame.")
            .IsEqual(0);
        for (int tick = 0; tick < BlastFrame; tick++) Step(simulation, GameplayButtons.None);
        AssertThat(FighterDefyRules.Counter(Action(simulation, 0)))
            .OverrideFailureMessage("The presentation counter steps once per Defy, on the blast frame.")
            .IsEqual(1);
        AssertThat(Runtime(simulation, 0).AbilityLockoutFrames).IsEqual(0);
        AssertThat(Runtime(simulation, 0).SpecialLockoutFrames).IsEqual(0);
    }

    [TestCase]
    public void TheClockRunsOneToThirtyWithTheBlastFreezeInsideAndFreeAtTheThirtyEighthTick() {
        FighterSimulation simulation = NewSimulation(seed: 902, spawnDistance: 4);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        var frames = new List<int>();
        var invulnerable = new List<int>();
        Step(simulation, GameplayButtons.Defy);
        frames.Add(Action(simulation, 0).DefyFrames);
        invulnerable.Add(Fighter(simulation, 0).InvulnerabilityFrames);
        for (int tick = 1; tick < 60; tick++) {
            Step(simulation, GameplayButtons.None);
            frames.Add(Action(simulation, 0).DefyFrames);
            invulnerable.Add(Fighter(simulation, 0).InvulnerabilityFrames);
        }

        // Frames 1 to 6 on ticks 0 to 5.
        for (int tick = 0; tick < BlastFrame; tick++) AssertThat(frames[tick]).IsEqual(tick + 1);
        // The blast's 8-frame freeze holds frame 6 on ticks 5 to 13 (both clocks pause).
        for (int tick = BlastFrame - 1; tick <= BlastFrame + 7; tick++) {
            AssertThat(frames[tick]).OverrideFailureMessage($"tick {tick}: the freeze holds frame 6").IsEqual(BlastFrame);
        }
        // Frames 7 to 30 on ticks 14 to 37, and free on tick 38: the press plus 38 frames.
        for (int tick = BlastFrame + 8; tick <= 37; tick++) AssertThat(frames[tick]).IsEqual(tick - 7);
        AssertThat(frames[37]).IsEqual(Total);
        AssertThat(frames[38]).OverrideFailureMessage("Free again 38 frames after the press (P11).").IsEqual(0);
        AssertThat(frames[39]).IsEqual(0);

        // Fully invulnerable through frame 6 (the freeze included), hittable from frame 7.
        for (int tick = 0; tick <= BlastFrame + 7; tick++) {
            AssertThat(invulnerable[tick] > 0).OverrideFailureMessage($"tick {tick}: invulnerable").IsTrue();
        }
        AssertThat(invulnerable[BlastFrame + 8]).OverrideFailureMessage("Frame 7 is vulnerable.").IsEqual(0);
    }

    [TestCase]
    public void TheStartupHoldsAnAirborneFighterInPlaceAndTheRecoveryFallsUnderGravity() {
        FighterSimulation simulation = NewSimulation(seed: 903, spawnDistance: 4);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        Step(simulation, GameplayButtons.Jump);
        for (int tick = 0; tick < 6; tick++) Step(simulation, GameplayButtons.None);
        AssertThat(Fighter(simulation, 0).IsGrounded).OverrideFailureMessage("Harness: airborne.").IsEqual(0);

        Step(simulation, GameplayButtons.Defy);
        FP64 pinnedY = Fighter(simulation, 0).Position.y;
        AssertThat(Fighter(simulation, 0).Velocity.y.RawValue).IsEqual(0L);
        for (int tick = 1; tick < BlastFrame + 8; tick++) {
            Step(simulation, GameplayButtons.None);
            AssertThat(Fighter(simulation, 0).Position.y.RawValue)
                .OverrideFailureMessage($"tick {tick}: no gravity through the startup and the freeze.")
                .IsEqual(pinnedY.RawValue);
        }
        Step(simulation, GameplayButtons.None);
        Step(simulation, GameplayButtons.None);
        AssertThat(Action(simulation, 0).DefyFrames > Invulnerable).IsTrue();
        AssertThat(Fighter(simulation, 0).Position.y < pinnedY)
            .OverrideFailureMessage("The vulnerable recovery falls.")
            .IsTrue();
    }

    [TestCase]
    public void UnderFiftyInfluenceAndAPressInTheRecoveryAreRefusedAndSpendNothing() {
        FighterSimulation poor = NewSimulation(seed: 904, spawnDistance: 4);
        AssertThat(poor.SeedFighterInfluenceForTest(0, 49)).IsTrue();
        Step(poor, GameplayButtons.Defy);
        AssertThat(Action(poor, 0).DefyFrames).IsEqual(0);
        AssertThat(Fighter(poor, 0).Influence.RawValue).IsEqual(FP64.FromInt(49).RawValue);
        AssertThat(Fighter(poor, 0).InvulnerabilityFrames).IsEqual(0);

        FighterSimulation rich = NewSimulation(seed: 905, spawnDistance: 4);
        AssertThat(rich.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        Step(rich, GameplayButtons.Defy);
        for (int tick = 1; tick < 20; tick++) Step(rich, GameplayButtons.None);
        int running = Action(rich, 0).DefyFrames;
        AssertThat(running > BlastFrame).IsTrue();
        FP64 before = Fighter(rich, 0).Influence;
        Step(rich, GameplayButtons.Defy);
        AssertThat(Fighter(rich, 0).Influence.RawValue)
            .OverrideFailureMessage("A second press while Defying is refused and spends nothing.")
            .IsEqual(before.RawValue);
        AssertThat(Action(rich, 0).DefyFrames).IsEqual(running + 1);
        AssertThat(FighterDefyRules.Counter(Action(rich, 0))).IsEqual(1);
    }

    [TestCase]
    public void APressCancelsASwingAndACastButTheirSpentPipAndReuseDelayStandAndNothingRearmsTheLockout() {
        FighterSimulation swing = NewSimulation(seed: 906, spawnDistance: 4);
        AssertThat(swing.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        Step(swing, GameplayButtons.BasicAttack);
        AssertThat(Runtime(swing, 0).AttackPhase).IsEqual(FighterBasicAttackRules.PhaseStartup);
        for (int tick = 1; tick < 4; tick++) Step(swing, GameplayButtons.None);
        Step(swing, GameplayButtons.Defy);
        AssertThat(Runtime(swing, 0).AttackPhase).IsEqual(FighterBasicAttackRules.PhaseNone);
        AssertThat(Action(swing, 0).DefyFrames).IsEqual(1);
        AssertThat(Runtime(swing, 0).AbilityLockoutFrames)
            .OverrideFailureMessage("The swing's lockout keeps running; Defy neither reads nor re-arms it.")
            .IsEqual(BasicComboRules.AbilityLockoutFrames - 4);

        FighterSimulation cast = NewSimulation(seed: 907, spawnDistance: 4);
        AssertThat(cast.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        Step(cast, GameplayButtons.Special1);
        AssertThat(SpecialCastTestKit.IsCasting(cast, 0)).OverrideFailureMessage("Harness: casting.").IsTrue();
        for (int tick = 1; tick < 5; tick++) Step(cast, GameplayButtons.None);
        Step(cast, GameplayButtons.Defy);
        AssertThat(SpecialCastTestKit.IsCasting(cast, 0)).OverrideFailureMessage("The cast is cancelled.").IsFalse();
        AssertThat(Action(cast, 0).DefyFrames).IsEqual(1);
        AssertThat(Verb(cast, 0).MomentumUnits)
            .OverrideFailureMessage("The cast's pip stays spent.")
            .IsEqual(BasicComboRules.MomentumStartUnits - BasicComboRules.MomentumSpecialCostUnits
                     + 6 * BasicComboRules.MomentumRegenUnitsPerFrame);
        AssertThat(Runtime(cast, 0).SpecialOneReuseFrames)
            .OverrideFailureMessage("And so does its reuse delay, still counting down.")
            .IsEqual(BasicComboRules.SpecialReuseDelayFrames - 5);
    }

    [TestCase]
    public void ADefyDropsTheInvulnerabilityOfTheActionItCancelsSoRecoveryFramesSevenToThirtyAreHittable() {
        // Knockdown (31), landing tech (13), roll (8) and air dodge (8) all ride the shared counter.
        foreach (int cancelled in new[] { 31, 13, 8, 8 }) {
            FighterSimulation simulation = NewSimulation(seed: 930 + cancelled, spawnDistance: 4);
            AssertThat(simulation.SeedFighterInfluenceForTest(0, 80)).IsTrue();
            AssertThat(simulation.SeedFighterInvulnerabilityForTest(0, cancelled)).IsTrue();
            Step(simulation, GameplayButtons.Defy);
            AssertThat(Fighter(simulation, 0).InvulnerabilityFrames).IsEqual(Invulnerable);
            while (Action(simulation, 0).DefyFrames < Invulnerable + 1) Step(simulation, GameplayButtons.None);
            for (int frame = Action(simulation, 0).DefyFrames; frame <= Total && Action(simulation, 0).DefyFrames > 0; frame++) {
                AssertThat(Fighter(simulation, 0).InvulnerabilityFrames)
                    .OverrideFailureMessage($"A Defy out of a {cancelled}-frame grant: recovery frame {Action(simulation, 0).DefyFrames} must be hittable.")
                    .IsEqual(0);
                Step(simulation, GameplayButtons.None);
            }
        }
    }

    [TestCase]
    public void ADefyKeepsTheLongerRespawnProtectionBecauseItIsIndependentAndNotOffensive() {
        FighterSimulation simulation = NewSimulation(seed: 940, spawnDistance: 4);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 80)).IsTrue();
        AssertThat(simulation.SeedFighterInvulnerabilityForTest(0, 100)).IsTrue();
        FighterActionStateComponent protectedAction = Action(simulation, 0);
        protectedAction.ActionFlags |= FighterActionStateComponent.ActionFlagRespawnProtection;
        AssertThat(simulation.SeedFighterActionStateForTest(0, in protectedAction)).IsTrue();
        Step(simulation, GameplayButtons.Defy);
        for (int tick = 0; tick < Invulnerable + 3; tick++) Step(simulation, GameplayButtons.None);
        AssertThat(Fighter(simulation, 0).InvulnerabilityFrames)
            .OverrideFailureMessage("Respawn protection outlives Defy's own six frames.")
            .IsGreater(60);
        AssertThat((Action(simulation, 0).ActionFlags & FighterActionStateComponent.ActionFlagRespawnProtection) != 0).IsTrue();
    }

    [TestCase]
    public void TheLegalityMatrixRefusesOnlyTheDeadTheCapturedTheUltimateOwnerTheBrokeAndTheDefying() {
        FighterStateComponent alive = new() {
            PlayerID = 0, Stocks = 3, MaxHP = 100, CurrentHP = 100, Influence = FP64.FromInt(50)
        };
        FighterActionStateComponent idle = default;
        FighterUltimateActivationComponent free = default;
        AssertThat(FighterDefyRules.CanBegin(in alive, in idle, in free, matchLive: true)).IsTrue();

        // Refused.
        AssertThat(FighterDefyRules.CanBegin(in alive, in idle, in free, matchLive: false))
            .OverrideFailureMessage("not before GO or after the match").IsFalse();
        FighterStateComponent dead = alive;
        dead.CurrentHP = 0;
        AssertThat(FighterDefyRules.CanBegin(in dead, in idle, in free, true)).IsFalse();
        FighterStateComponent noStocks = alive;
        noStocks.Stocks = 0;
        AssertThat(FighterDefyRules.CanBegin(in noStocks, in idle, in free, true)).IsFalse();
        FighterStateComponent platform = alive;
        platform.RespawnFramesRemaining = 120;
        AssertThat(FighterDefyRules.CanBegin(in platform, in idle, in free, true)).IsFalse();
        FighterStateComponent broke = alive;
        broke.Influence = FP64.FromInt(50) - FP64.FromDouble(0.01);
        AssertThat(FighterDefyRules.CanBegin(in broke, in idle, in free, true)).IsFalse();
        FighterActionStateComponent defying = idle;
        defying.DefyFrames = 12;
        AssertThat(FighterDefyRules.CanBegin(in alive, in defying, in free, true)).IsFalse();
        FighterUltimateActivationComponent captured = free;
        captured.CapturedFrames = 30;
        AssertThat(FighterDefyRules.CanBegin(in alive, in idle, in captured, true))
            .OverrideFailureMessage("Once the hold has connected the press is refused (R3).")
            .IsFalse();
        foreach (int phase in new[] {
                     FighterUltimateActivationRules.PhaseWindup, FighterUltimateActivationRules.PhaseActive,
                     FighterUltimateActivationRules.PhaseWhiffRecovery, FighterUltimateActivationRules.PhaseCinematic }) {
            FighterUltimateActivationComponent owner = free;
            owner.Phase = phase;
            AssertThat(FighterDefyRules.CanBegin(in alive, in idle, in owner, true))
                .OverrideFailureMessage($"The fighter's own Ultimate, phase {phase}, refuses a Defy.")
                .IsFalse();
        }

        // Legal from the states a hit or an action leaves a fighter in: none of them gates it.
        FighterStateComponent stunned = alive;
        stunned.HitstunFrames = 20;
        stunned.DazeFrames = 20;
        AssertThat(FighterDefyRules.CanBegin(in stunned, in idle, in free, true)).IsTrue();
        FighterActionStateComponent casting = idle;
        casting.SpecialPhase = FighterSpecialCastRules.PhaseRecovery;
        AssertThat(FighterDefyRules.CanBegin(in alive, in casting, in free, true)).IsTrue();
    }

    [TestCase]
    public void APressHeldThroughTheFightersOwnHitstopBeginsOnTheThawTickAndOverridesTheHit() {
        FighterSimulation simulation = NewSimulation(seed: 908, spawnDistance: 1);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        for (int tick = 0; tick < 60; tick++) {
            bool close = Fighter(simulation, 1).Position.x - Fighter(simulation, 0).Position.x <= FP64.FromDouble(1.2);
            simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.None), Input(simulation.CurrentTick, GameplayButtons.None, close ? (sbyte)0 : (sbyte)-127));
            if (close) break;
        }
        for (int tick = 0; tick < 20; tick++) Step(simulation, GameplayButtons.None);
        // Player two swings; hold player one's Influence at 50 through its own damage meter.
        int hpBefore = Fighter(simulation, 0).CurrentHP;
        simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.None), Input(simulation.CurrentTick, GameplayButtons.BasicAttack));
        for (int tick = 0; tick < 30 && Fighter(simulation, 0).CurrentHP == hpBefore; tick++) Step(simulation, GameplayButtons.None);
        AssertThat(Fighter(simulation, 0).CurrentHP < hpBefore).OverrideFailureMessage("Harness: the hit must land.").IsTrue();
        AssertThat(Verb(simulation, 0).HitstopFrames > 1).OverrideFailureMessage("Harness: a freeze to press into.").IsTrue();
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 50)).IsTrue();

        // Press on the first frozen tick: nothing begins until the thaw tick.
        Step(simulation, GameplayButtons.Defy);
        bool thawed = false;
        for (int tick = 0; tick < 12 && !thawed; tick++) {
            int frozen = Verb(simulation, 0).HitstopFrames;
            if (frozen > 1) {
                AssertThat(Action(simulation, 0).DefyFrames)
                    .OverrideFailureMessage("Held through the freeze: no Defy while frozen.")
                    .IsEqual(0);
            }
            Step(simulation, GameplayButtons.None);
            if (frozen == 1) {
                thawed = true;
                AssertThat(Action(simulation, 0).DefyFrames).OverrideFailureMessage("It begins on the thaw tick.").IsEqual(1);
                AssertThat(Fighter(simulation, 0).HitstunFrames).OverrideFailureMessage("The hit's stun is overridden.").IsEqual(0);
                AssertThat(Verb(simulation, 0).PendingLaunchActive).IsEqual(0);
                AssertThat(Fighter(simulation, 0).Velocity.x.RawValue).OverrideFailureMessage("and its push with it.").IsEqual(0L);
                AssertThat(Fighter(simulation, 0).Influence.RawValue < FP64.FromInt(50).RawValue).IsTrue();
            }
        }
        AssertThat(thawed).IsTrue();
    }

    [TestCase]
    public void ADefyReleasesAHoldFromEitherSide() {
        // The victim Defies out of a grab: the grabber's hold ends and the victim is free.
        FighterSimulation held = HeldPair(seed: 909);
        AssertThat(simulationHeld(held)).IsTrue();
        AssertThat(held.SeedFighterInfluenceForTest(1, 50)).IsTrue();
        held.Advance(Input(held.CurrentTick, GameplayButtons.None), Input(held.CurrentTick, GameplayButtons.Defy));
        AssertThat(Verb(held, 1).BeingHeld).OverrideFailureMessage("The victim is released.").IsEqual(0);
        AssertThat(Action(held, 1).DefyFrames).IsEqual(1);
        AssertThat(Verb(held, 0).GrabPhase)
            .OverrideFailureMessage("The grabber's hold ends with it.")
            .IsEqual(FighterGrabRules.PhaseNone);

        // The grabber Defies: its own grab is cancelled and the victim is let go.
        FighterSimulation grabbing = HeldPair(seed: 910);
        AssertThat(simulationHeld(grabbing)).IsTrue();
        AssertThat(grabbing.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        grabbing.Advance(Input(grabbing.CurrentTick, GameplayButtons.Defy), Input(grabbing.CurrentTick, GameplayButtons.None));
        AssertThat(Verb(grabbing, 0).GrabPhase).IsEqual(FighterGrabRules.PhaseNone);
        AssertThat(Verb(grabbing, 1).BeingHeld).OverrideFailureMessage("The victim is let go.").IsEqual(0);
        AssertThat(Action(grabbing, 0).DefyFrames).IsEqual(1);

        static bool simulationHeld(FighterSimulation simulation) => Verb(simulation, 1).BeingHeld == 1;
    }

    [TestCase]
    public void SuddenDeathDoesNotBarDefyOnceFiftyIsEarnedAgain() {
        var simulation = new FighterSimulation(
            FighterLoadout.Default(FighterCharacterID.Joan), FighterLoadout.Default(FighterCharacterID.Joan),
            matchSeconds: 1, seed: 911, spawnDistance: 4,
            rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0));
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        for (int tick = 0; tick < 70 && simulation.GetMatchState().SuddenDeathActive == 0; tick++) Step(simulation, GameplayButtons.None);
        AssertThat(simulation.GetMatchState().SuddenDeathActive).IsEqual(1);
        for (int tick = 0; tick < 400 && simulation.GetMatchState().MatchState != FighterMatchStates.InProgress; tick++) {
            Step(simulation, GameplayButtons.None);
        }
        AssertThat(simulation.GetMatchState().MatchState).IsEqual(FighterMatchStates.InProgress);
        AssertThat(Fighter(simulation, 0).Influence.RawValue).OverrideFailureMessage("Influence restarts at 0.").IsEqual(0L);

        Step(simulation, GameplayButtons.Defy);
        AssertThat(Action(simulation, 0).DefyFrames).OverrideFailureMessage("Empty-handed: refused.").IsEqual(0);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 60)).IsTrue();
        Step(simulation, GameplayButtons.Defy);
        AssertThat(Action(simulation, 0).DefyFrames)
            .OverrideFailureMessage("Defy is legal in Sudden Death once 50 is earned (5.2).")
            .IsEqual(1);
        AssertThat(Fighter(simulation, 0).Influence.RawValue).IsEqual(FP64.FromInt(10).RawValue);
    }

    [TestCase]
    public void ADefyReplaysIdenticallyAcrossASnapshotAndALateRemotePress() {
        // Snapshot round trip in the middle of a Defy.
        FighterSimulation first = NewSimulation(seed: 912, spawnDistance: 1);
        AssertThat(first.SeedFighterInfluenceForTest(0, 70)).IsTrue();
        for (int tick = 0; tick < 12; tick++) Step(first, tick == 3 ? GameplayButtons.Defy : GameplayButtons.None);
        byte[] snapshot = first.CaptureFullState();
        FighterSimulation restored = NewSimulation(seed: 912, spawnDistance: 1);
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(first.CurrentHash);
        AssertThat(Action(restored, 0).DefyFrames).IsEqual(Action(first, 0).DefyFrames);
        for (int tick = 12; tick < 60; tick++) {
            AssertThat(Step(restored, GameplayButtons.None)).IsEqual(Step(first, GameplayButtons.None));
        }

        // A remote press arriving seven frames late converges on the clean timeline.
        const int Press = 30;
        const int Final = 90;
        PlayerInputFrame RemoteInput(int tick) => Input(tick, tick == Press ? GameplayButtons.Defy : GameplayButtons.None);
        PlayerInputFrame LocalInput(int tick) => Input(tick, GameplayButtons.None);
        FighterSimulation clean = NewSimulation(seed: 913, spawnDistance: 1);
        AssertThat(clean.SeedFighterInfluenceForTest(1, 100)).IsTrue();
        var cleanHashes = new long[Final];
        for (int tick = 0; tick < Final; tick++) cleanHashes[tick] = clean.Advance(LocalInput(tick), RemoteInput(tick));
        AssertThat(Action(clean, 1).DefyFrames > 0 || FighterDefyRules.Counter(Action(clean, 1)) == 1)
            .OverrideFailureMessage("Harness: the remote Defy must run in the clean timeline.")
            .IsTrue();

        FighterSimulation network = NewSimulation(seed: 913, spawnDistance: 1);
        AssertThat(network.SeedFighterInfluenceForTest(1, 100)).IsTrue();
        const int LastLate = Press + 7;
        for (int tick = 0; tick < Press; tick++) network.Advance(LocalInput(tick), RemoteInput(tick));
        for (int tick = Press; tick <= LastLate; tick++) network.AdvanceWithPredictedPlayerTwo(LocalInput(tick));
        for (int tick = Press; tick <= LastLate; tick++) {
            AssertThat(network.CorrectPlayerTwoInput(tick, RemoteInput(tick))).IsTrue();
        }
        AssertThat(network.CurrentHash)
            .OverrideFailureMessage("The corrected run did not converge on the clean one.")
            .IsEqual(cleanHashes[LastLate]);
        for (int tick = LastLate + 1; tick < Final; tick++) {
            AssertThat(network.Advance(LocalInput(tick), RemoteInput(tick))).IsEqual(cleanHashes[tick]);
        }
    }

    [TestCase]
    public void ThePresentationCounterStepsOncePerDefyAndWrapsAtSixteen() {
        FighterSimulation simulation = NewSimulation(seed: 914, spawnDistance: 4);
        int expected = 0;
        for (int defy = 0; defy < 17; defy++) {
            AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
            AssertThat(simulation.SeedFighterDefyForTest(0, 0)).IsTrue();
            Step(simulation, GameplayButtons.Defy);
            AssertThat(FighterDefyRules.Counter(Action(simulation, 0)))
                .OverrideFailureMessage("The counter has not stepped at frame 1.")
                .IsEqual(expected);
            expected = (expected + 1) & 0xF;
            for (int tick = 0; tick < 40; tick++) Step(simulation, GameplayButtons.None);
            AssertThat(Action(simulation, 0).DefyFrames).IsEqual(0);
            AssertThat(FighterDefyRules.Counter(Action(simulation, 0)))
                .OverrideFailureMessage("The count outlives the Defy it counted.")
                .IsEqual(expected);
        }
        AssertThat(expected).OverrideFailureMessage("17 Defies wrap a 4-bit counter to 1.").IsEqual(1);
    }

    [TestCase]
    public void TheBlastFiresOnceHoweverLongTheFreezeHoldsFrameSix() {
        FighterSimulation simulation = NewSimulation(seed: 915, spawnDistance: 1);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        Step(simulation, GameplayButtons.Defy);
        for (int tick = 1; tick <= BlastFrame - 1; tick++) Step(simulation, GameplayButtons.None);
        AssertThat(Verb(simulation, 1).HitstopFrames).IsEqual(BasicComboRules.DefyBlastHitstopFrames);
        AssertThat((Action(simulation, 0).ActionFlags & FighterActionStateComponent.ActionFlagDefyBlastFired) != 0).IsTrue();

        // The freeze counts down 8 to 0 on the victim and the Defier alike; it is never re-armed,
        // and the stagger is not reassigned once the freeze lifts.
        int previousStun = Fighter(simulation, 1).HitstunFrames;
        for (int expected = BasicComboRules.DefyBlastHitstopFrames - 1; expected >= 0; expected--) {
            Step(simulation, GameplayButtons.None);
            AssertThat(Verb(simulation, 1).HitstopFrames).IsEqual(expected);
            AssertThat(Verb(simulation, 0).HitstopFrames).IsEqual(expected);
            AssertThat(Fighter(simulation, 1).HitstunFrames <= previousStun).IsTrue();
            previousStun = Fighter(simulation, 1).HitstunFrames;
        }
        for (int tick = 0; tick < 6; tick++) {
            Step(simulation, GameplayButtons.None);
            AssertThat(Fighter(simulation, 1).HitstunFrames < previousStun)
                .OverrideFailureMessage("After the freeze the stagger only counts down.")
                .IsTrue();
            previousStun = Fighter(simulation, 1).HitstunFrames;
        }
    }

    // === helpers ==============================================================

    private static FighterSimulation NewSimulation(int seed, int spawnDistance) => new(
        FighterLoadout.Default(FighterCharacterID.Joan), FighterLoadout.Default(FighterCharacterID.Joan),
        seed: seed, spawnDistance: spawnDistance, rules: FighterMatchRules.Disabled);

    /// <summary>Player one has just seized player two with the Block + Attack chord and holds it.</summary>
    private static FighterSimulation HeldPair(int seed) {
        FighterSimulation simulation = NewSimulation(seed, spawnDistance: 1);
        for (int tick = 0; tick < 90; tick++) {
            if (FP64.Abs(Fighter(simulation, 1).Position.x - Fighter(simulation, 0).Position.x)
                <= FP64.FromDouble(BasicComboRules.GrabReachUnits)) break;
            simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.None, 127), Input(simulation.CurrentTick, GameplayButtons.None));
        }
        simulation.Advance(
            new PlayerInputFrame {
                Tick = (uint)simulation.CurrentTick,
                Held = GameplayButtons.Block | GameplayButtons.BasicAttack,
                Pressed = GameplayButtons.BasicAttack
            },
            Input(simulation.CurrentTick, GameplayButtons.None));
        for (int step = 0; step < BasicComboRules.GrabStartupFrames + 3 && Verb(simulation, 1).BeingHeld == 0; step++) {
            simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.None), Input(simulation.CurrentTick, GameplayButtons.None));
        }
        return simulation;
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

    private static FighterActionStateComponent Action(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent action)).IsTrue();
        return action;
    }

    private static long Step(FighterSimulation simulation, GameplayButtons playerOne) =>
        simulation.Advance(
            Input(simulation.CurrentTick, playerOne),
            Input(simulation.CurrentTick, GameplayButtons.None));

    private static PlayerInputFrame Input(int tick, GameplayButtons buttons, sbyte moveX = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = buttons,
        Pressed = buttons
    };
}
