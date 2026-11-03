using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The running attack, Fighter side (GR-RUN, user ruling 9b, 2026-11-03). A fresh
/// BasicAttack press made grounded, with the stick toward the facing and already
/// moving at the shared fraction of the maximum run speed latches
/// <see cref="FighterBasicAttackRules.FlagRunAttack"/> (the spare <c>AttackFlags</c>
/// bit 1024 on component 305: no new component, no protocol change). Under ruling
/// 15 that predicate approximates Story's Running state, the recorded exception.
///
/// <para>Pure C#: default loadouts, no Godot runtime.</para>
/// </summary>
[TestSuite]
public class FighterRunAttackTests {
    private const sbyte Toward = 127;
    private const int ClearFlags = FighterBasicAttackRules.FlagBuffered | FighterBasicAttackRules.FlagQueuedBuffered;

    [TestCase]
    public void ARunningPressLatchesTheRunAttackWithItsProfilesFramesAndNeverChains() {
        // The flag is a free bit of AttackFlags, and a single strike.
        int otherFlags = FighterBasicAttackRules.FlagAerial | FighterBasicAttackRules.FlagHitResolved
            | FighterBasicAttackRules.FlagBuffered | FighterBasicAttackRules.FlagUpAttack
            | FighterBasicAttackRules.FlagDownAir | FighterBasicAttackRules.FlagConstructHitResolved
            | FighterBasicAttackRules.FlagQueuedBuffered | FighterBasicAttackRules.FlagAirAttack
            | FighterBasicAttackRules.FlagActiveFacingRight | FighterBasicAttackRules.FlagLedgeAttack;
        AssertThat(otherFlags & FighterBasicAttackRules.FlagRunAttack).IsEqual(0);
        AssertThat(FighterBasicAttackRules.VariantMask & FighterBasicAttackRules.FlagRunAttack)
            .IsEqual(FighterBasicAttackRules.FlagRunAttack);

        (FighterCharacterID Id, int Startup)[] cases = {
            (FighterCharacterID.Joan, 7),
            (FighterCharacterID.Tesla, 9),
            (FighterCharacterID.Einstein, 10),
            (FighterCharacterID.Shakespeare, 10),
            (FighterCharacterID.Lincoln, 11)
        };
        foreach ((FighterCharacterID id, int startup) in cases) {
            FighterSimulation simulation = Far(id, seed: 901);
            RunUp(simulation, 8);
            Step(simulation, GameplayButtons.BasicAttack, Toward);
            FighterRuntimeComponent started = Runtime(simulation, 0);
            AssertThat(started.AttackFlags & FighterBasicAttackRules.FlagRunAttack)
                .OverrideFailureMessage($"{id}: a press at a run must latch the running attack.")
                .IsEqual(FighterBasicAttackRules.FlagRunAttack);
            AssertThat(FighterBasicAttackRules.IsVariantSwing(in started)).IsTrue();
            AssertThat(started.ComboIndex).IsEqual(0);
            AssertThat(started.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseStartup);
            AssertThat(started.AttackPhaseFrames).IsEqual(startup);
            AssertThat(FighterBasicAttackRules.NextChainStep(in started))
                .OverrideFailureMessage($"{id}: the CPU band must read an opener during the running attack.")
                .IsEqual(0);
            AssertThat(started.AbilityLockoutFrames)
                .OverrideFailureMessage($"{id}: the swing start arms the ability lockout.")
                .IsEqual(BasicComboRules.AbilityLockoutFrames);

            // Mash BasicAttack on every tick of the strike: every press is dropped.
            int previousPhase = FighterBasicAttackRules.PhaseStartup;
            int ticks = 0;
            for (; ticks < 120; ticks++) {
                Step(simulation, GameplayButtons.BasicAttack, Toward);
                FighterRuntimeComponent runtime = Runtime(simulation, 0);
                AssertThat(runtime.AttackPhase)
                    .OverrideFailureMessage($"{id}: the running attack entered the chain hold.")
                    .IsNotEqual(FighterBasicAttackRules.PhaseChainHold);
                AssertThat(runtime.AttackFlags & ClearFlags)
                    .OverrideFailureMessage($"{id}: a press during the running attack was buffered.")
                    .IsEqual(0);
                AssertThat(runtime.ComboIndex).IsEqual(0);
                if (runtime.AttackPhase == previousPhase) continue;
                if (runtime.AttackPhase == FighterBasicAttackRules.PhaseActive) {
                    AssertThat(runtime.AttackPhaseFrames).IsEqual(BasicComboRules.RunAttackActiveFrames);
                } else if (runtime.AttackPhase == FighterBasicAttackRules.PhaseRecovery) {
                    AssertThat(runtime.AttackPhaseFrames).IsEqual(BasicComboRules.RunAttackRecoveryFrames);
                } else {
                    ticks++;
                    break;
                }
                previousPhase = runtime.AttackPhase;
            }
            // A press on the exit tick is a fresh swing, so the strike ends there.
            AssertThat(ticks)
                .OverrideFailureMessage($"{id}: the running attack must run its whole window and exit straight out.")
                .IsEqual(startup + BasicComboRules.RunAttackActiveFrames + BasicComboRules.RunAttackRecoveryFrames);
        }
    }

    [TestCase]
    public void ThePredicateNeedsTheSpeedAndTheStickTowardTheFacing() {
        // The ramp reaches full speed in 6 ticks: 3 is one half, 4 is two thirds.
        foreach ((int ticks, bool running) in new[] { (0, false), (1, false), (3, false), (4, true), (6, true) }) {
            FighterSimulation simulation = Far(FighterCharacterID.Tesla, seed: 902);
            RunUp(simulation, ticks);
            Step(simulation, GameplayButtons.BasicAttack, Toward);
            AssertThat(Runtime(simulation, 0).AttackFlags & FighterBasicAttackRules.FlagRunAttack)
                .OverrideFailureMessage($"A press after {ticks} ticks of a push.")
                .IsEqual(running ? FighterBasicAttackRules.FlagRunAttack : 0);
        }

        // A released stick while still sliding at speed is the string's opener.
        FighterSimulation released = Far(FighterCharacterID.Tesla, seed: 903);
        RunUp(released, 8);
        Step(released, GameplayButtons.BasicAttack, moveX: 0);
        AssertThat(Runtime(released, 0).AttackFlags & FighterBasicAttackRules.VariantMask).IsEqual(0);
        AssertThat(Runtime(released, 0).AttackPhase).IsEqual(FighterBasicAttackRules.PhaseStartup);

        // A stick pushed against the facing is the same: it is a reversal, not a run.
        FighterSimulation reversal = Far(FighterCharacterID.Tesla, seed: 904);
        RunUp(reversal, 8);
        Step(reversal, GameplayButtons.BasicAttack, moveX: -127);
        AssertThat(Runtime(reversal, 0).AttackFlags & FighterBasicAttackRules.VariantMask).IsEqual(0);

        // Running left mirrors it: the facing and the speed along it count.
        FighterSimulation leftward = Far(FighterCharacterID.Tesla, seed: 905);
        for (int tick = 0; tick < 8; tick++) Step(leftward, GameplayButtons.None, moveX: -127);
        AssertThat(Fighter(leftward, 0).FacingRight).IsEqual(0);
        Step(leftward, GameplayButtons.BasicAttack, moveX: -127);
        AssertThat(Runtime(leftward, 0).AttackFlags & FighterBasicAttackRules.FlagRunAttack)
            .IsEqual(FighterBasicAttackRules.FlagRunAttack);
    }

    [TestCase]
    public void ItLandsOneAndAFifthBasicsAtOneAndAHalfKnockbackWithoutLaunching() {
        // Joan's hit 1 is a flat 1.0x at 1.0x knockback, so it is the exact control.
        AssertThat(BasicComboRules.StringProfileFor("joan").DamageTenths[0]).IsEqual(10);
        Landing run = StrikeRunning(FighterCharacterID.Joan, runTicks: 20, seed: 906);
        Landing opener = StrikeStanding(FighterCharacterID.Joan, seed: 907);
        AssertThat(run.Landed && opener.Landed)
            .OverrideFailureMessage("Harness: both strikes must land.")
            .IsTrue();

        int basicDamage = FighterLoadout.Default(FighterCharacterID.Joan).BasicDamage;
        AssertThat(run.Flags & FighterBasicAttackRules.FlagRunAttack).IsEqual(FighterBasicAttackRules.FlagRunAttack);
        AssertThat(opener.Flags & FighterBasicAttackRules.VariantMask).IsEqual(0);
        AssertThat(run.Damage).IsEqual((int)System.Math.Round(basicDamage * 1.2, System.MidpointRounding.AwayFromZero));
        AssertThat(opener.Damage).IsEqual(basicDamage);
        AssertThat(run.Victim.HitstunFrames).IsEqual(BasicComboRules.RunAttackHitstunFrames);

        // The missing-HP ramp reads the larger hit too, so the ratio sits a shade above 1.5.
        double ratio = (double)run.Victim.Velocity.x.RawValue / opener.Victim.Velocity.x.RawValue;
        AssertThat(ratio >= 1.5 && ratio < 1.6)
            .OverrideFailureMessage($"The running attack's knockback was {ratio:F3}x hit 1's.")
            .IsTrue();
        AssertThat(run.Victim.IsGrounded)
            .OverrideFailureMessage("A non-launching hit leaves a grounded victim on the ground.")
            .IsEqual(1);
        AssertThat(run.Victim.Velocity.y).IsEqual(FP64.Zero);

        // D1 and the Momentum gauge, through the names GR-MOM owns the values of.
        AssertThat(run.SwingLanded).IsEqual(1);
        AssertThat(run.GaugeGain).IsEqual(BasicComboRules.MomentumDirectionalGainUnits);
    }

    [TestCase]
    public void TheActiveFramesTravelTheLungeWhateverTheStickAndTheRecoveryBrakes() {
        double lunge = BasicComboRules.RunAttackLungeUnits;
        foreach ((string name, sbyte startupStick, sbyte committedStick) in new[] {
            ("the stick held", Toward, Toward),
            ("the stick released", (sbyte)0, (sbyte)0),
            ("the stick pushed back after the startup", Toward, (sbyte)-127)
        }) {
            FighterSimulation simulation = Far(FighterCharacterID.Tesla, seed: 908);
            RunUp(simulation, 8);
            Step(simulation, GameplayButtons.BasicAttack, Toward);

            double travelled = 0;
            int activeTicks = 0;
            FighterRuntimeComponent runtime = Runtime(simulation, 0);
            for (int tick = 0; tick < 60 && runtime.AttackPhase != FighterBasicAttackRules.PhaseNone; tick++) {
                FP64 before = Fighter(simulation, 0).Position.x;
                bool committed = FighterBasicAttackRules.RunAttackCommitted(in runtime)
                    || (runtime.AttackPhase == FighterBasicAttackRules.PhaseStartup && runtime.AttackPhaseFrames == 1);
                Step(simulation, GameplayButtons.None, committed ? committedStick : startupStick);
                runtime = Runtime(simulation, 0);
                FighterStateComponent fighter = Fighter(simulation, 0);
                if (runtime.AttackPhase == FighterBasicAttackRules.PhaseActive) {
                    activeTicks++;
                    travelled += Units(fighter.Position.x - before);
                }
                if (FighterBasicAttackRules.RunAttackCommitted(in runtime)) {
                    AssertThat(fighter.FacingRight)
                        .OverrideFailureMessage($"{name}: the committed swing must keep its facing.")
                        .IsEqual(1);
                }
                if (runtime.AttackPhase == FighterBasicAttackRules.PhaseRecovery && runtime.AttackPhaseFrames == 1) {
                    AssertThat(fighter.Velocity.x)
                        .OverrideFailureMessage($"{name}: the recovery must brake to a stop.")
                        .IsEqual(FP64.Zero);
                }
            }
            AssertThat(activeTicks).IsEqual(BasicComboRules.RunAttackActiveFrames);
            AssertThat(System.Math.Abs(travelled - lunge) < 0.01)
                .OverrideFailureMessage($"{name}: the active frames travelled {travelled:F4} units, not {lunge}.")
                .IsTrue();
        }
    }

    [TestCase]
    public void TheBoxReachesFourTenthsPastHitOnesFarEdgeSoItConnectsWhereHitOneCannot() {
        foreach (FighterCharacterID id in new[] { FighterCharacterID.Joan, FighterCharacterID.Tesla, FighterCharacterID.Lincoln }) {
            var attacker = new FighterStateComponent { CharacterID = (int)id, FacingRight = 1 };
            BasicStringProfile profile = FighterBasicAttackRules.StringProfileFor((int)id);
            foreach (bool facingRight in new[] { true, false }) {
                FighterCombatSystem.ResolveStringBox(
                    in attacker, in profile, BasicComboRules.RunAttackStringStep, facingRight,
                    out FPVector2 stringCentre, out FPVector2 stringHalf);
                FighterCombatSystem.ResolveRunAttackBox(
                    in attacker, in profile, facingRight, out FPVector2 centre, out FPVector2 half);
                double sign = facingRight ? 1 : -1;
                double far = sign * Units(centre.x) + Units(half.x);
                double near = sign * Units(centre.x) - Units(half.x);
                double stringFar = sign * Units(stringCentre.x) + Units(stringHalf.x);
                double stringNear = sign * Units(stringCentre.x) - Units(stringHalf.x);
                AssertThat(System.Math.Abs(far - stringFar - BasicComboRules.RunAttackReachBonusUnits) < 0.001)
                    .OverrideFailureMessage($"{id}: the far edge moved {far - stringFar:F4}.")
                    .IsTrue();
                AssertThat(System.Math.Abs(near - stringNear) < 0.001)
                    .OverrideFailureMessage($"{id}: the near edge moved {near - stringNear:F4}.")
                    .IsTrue();
                AssertThat(half.y).IsEqual(stringHalf.y);
                AssertThat(centre.y).IsEqual(stringCentre.y);
            }
        }

        // In the simulation: the longest-range landing is out of hit 1's reach.
        var joan = new FighterStateComponent { CharacterID = (int)FighterCharacterID.Joan, FacingRight = 1 };
        BasicStringProfile joanProfile = FighterBasicAttackRules.StringProfileFor((int)FighterCharacterID.Joan);
        FighterCombatSystem.ResolveStringBox(
            in joan, in joanProfile, BasicComboRules.RunAttackStringStep, true, out FPVector2 hitCentre, out FPVector2 hitHalf);
        double hitOneReach = Units(hitCentre.x) + Units(hitHalf.x) + Units(FighterHurtbox.HalfExtents.x);
        Landing landing = default;
        for (int runTicks = 6; runTicks < 80 && !landing.Landed; runTicks++) {
            landing = StrikeRunning(FighterCharacterID.Joan, runTicks, seed: 909);
        }
        AssertThat(landing.Landed).OverrideFailureMessage("Harness: no run ever landed.").IsTrue();
        AssertThat(landing.Gap > hitOneReach)
            .OverrideFailureMessage($"It first landed at a gap of {landing.Gap:F3}, inside hit 1's reach {hitOneReach:F3}.")
            .IsTrue();
    }

    [TestCase]
    public void TheDirectionsTheAirAndTheGrabChordAllWinOverTheRun() {
        FighterSimulation up = Far(FighterCharacterID.Tesla, seed: 910);
        RunUp(up, 8);
        Step(up, GameplayButtons.BasicAttack, Toward, moveY: -127);
        AssertThat(Runtime(up, 0).AttackFlags & FighterBasicAttackRules.VariantMask)
            .IsEqual(FighterBasicAttackRules.FlagUpAttack);

        FighterSimulation down = Far(FighterCharacterID.Tesla, seed: 911);
        RunUp(down, 8);
        Step(down, GameplayButtons.BasicAttack | GameplayButtons.Down, Toward);
        AssertThat(Runtime(down, 0).AttackPhase).IsEqual(FighterBasicAttackRules.PhaseStartup);
        AssertThat(Runtime(down, 0).AttackFlags & FighterBasicAttackRules.VariantMask)
            .OverrideFailureMessage("A grounded Down-attack stays the string's opener.")
            .IsEqual(0);

        FighterSimulation air = Far(FighterCharacterID.Tesla, seed: 912);
        RunUp(air, 8);
        Step(air, GameplayButtons.Jump, Toward);
        Step(air, GameplayButtons.BasicAttack, Toward);
        AssertThat(Runtime(air, 0).AttackFlags & FighterBasicAttackRules.VariantMask)
            .IsEqual(FighterBasicAttackRules.FlagAirAttack);

        // The grab chord: Block + BasicAttack starts no swing, at a run or not.
        FighterSimulation chord = Far(FighterCharacterID.Tesla, seed: 913);
        RunUp(chord, 8);
        Step(chord, GameplayButtons.BasicAttack | GameplayButtons.Block, Toward);
        AssertThat(Runtime(chord, 0).AttackPhase)
            .OverrideFailureMessage("The grab chord must never swing.")
            .IsEqual(FighterBasicAttackRules.PhaseNone);
        AssertThat(Verb(chord, 0).GrabPhase)
            .OverrideFailureMessage("The grab chord must start the grab.")
            .IsNotEqual(FighterGrabRules.PhaseNone);
    }

    [TestCase]
    public void AStepForwardMidStringNeverLosesTheStringAndOnlyAFreshPressRuns() {
        // Hit 1 from a standstill, then the stick held through its recovery: by the
        // chain hold the fighter is at a full run, and a press still continues.
        FighterSimulation simulation = Far(FighterCharacterID.Tesla, seed: 914);
        Step(simulation, GameplayButtons.BasicAttack, moveX: 0);
        FighterRuntimeComponent runtime = Runtime(simulation, 0);
        for (int tick = 0; tick < 60 && runtime.AttackPhase != FighterBasicAttackRules.PhaseChainHold; tick++) {
            Step(simulation, GameplayButtons.None, Toward);
            runtime = Runtime(simulation, 0);
        }
        AssertThat(runtime.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseChainHold);
        FighterStateComponent fighter = Fighter(simulation, 0);
        AssertThat(FighterBasicAttackRules.IsRunningForAttack(
                in fighter, in runtime, FighterLoadout.Default(FighterCharacterID.Tesla).MoveSpeed))
            .OverrideFailureMessage("Harness: the fighter must be at a run in the chain hold.")
            .IsTrue();
        Step(simulation, GameplayButtons.BasicAttack, Toward);
        runtime = Runtime(simulation, 0);
        AssertThat(runtime.AttackFlags & FighterBasicAttackRules.VariantMask).IsEqual(0);
        AssertThat(runtime.ComboIndex).IsEqual(1);

        // A press buffered in hit 1's recovery continues too.
        FighterSimulation buffered = Far(FighterCharacterID.Tesla, seed: 915);
        Step(buffered, GameplayButtons.BasicAttack, moveX: 0);
        runtime = Runtime(buffered, 0);
        for (int tick = 0; tick < 60 && runtime.AttackPhase != FighterBasicAttackRules.PhaseRecovery; tick++) {
            Step(buffered, GameplayButtons.None, Toward);
            runtime = Runtime(buffered, 0);
        }
        Step(buffered, GameplayButtons.BasicAttack, Toward);
        for (int tick = 0; tick < 40 && Runtime(buffered, 0).ComboIndex == 0; tick++) {
            Step(buffered, GameplayButtons.None, Toward);
        }
        AssertThat(Runtime(buffered, 0).ComboIndex).IsEqual(1);
        AssertThat(Runtime(buffered, 0).AttackFlags & FighterBasicAttackRules.VariantMask).IsEqual(0);

        // Once the chain hold has run out, the same press is a fresh one.
        FighterSimulation expired = Far(FighterCharacterID.Tesla, seed: 916);
        Step(expired, GameplayButtons.BasicAttack, moveX: 0);
        runtime = Runtime(expired, 0);
        for (int tick = 0; tick < 120; tick++) {
            Step(expired, GameplayButtons.None, Toward);
            runtime = Runtime(expired, 0);
            if (runtime.AttackPhase == FighterBasicAttackRules.PhaseNone) break;
        }
        AssertThat(runtime.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseNone);
        Step(expired, GameplayButtons.BasicAttack, Toward);
        AssertThat(Runtime(expired, 0).AttackFlags & FighterBasicAttackRules.FlagRunAttack)
            .IsEqual(FighterBasicAttackRules.FlagRunAttack);
    }

    // === helpers ==============================================================

    private readonly struct Landing {
        public Landing(bool landed, FighterStateComponent victim, int damage, int flags, int swingLanded, int gaugeGain, double gap) {
            Landed = landed;
            Victim = victim;
            Damage = damage;
            Flags = flags;
            SwingLanded = swingLanded;
            GaugeGain = gaugeGain;
            Gap = gap;
        }

        public bool Landed { get; }
        public FighterStateComponent Victim { get; }
        public int Damage { get; }
        public int Flags { get; }
        public int SwingLanded { get; }
        public int GaugeGain { get; }
        public double Gap { get; }
    }

    /// <summary>Runs at a standing Tesla six units away for <paramref name="runTicks"/>, then presses.</summary>
    private static Landing StrikeRunning(FighterCharacterID id, int runTicks, int seed) {
        var simulation = new FighterSimulation(
            FighterLoadout.Default(id),
            FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: seed, spawnDistance: 3, rules: FighterMatchRules.Disabled);
        RunUp(simulation, runTicks);
        Step(simulation, GameplayButtons.BasicAttack, Toward);
        return UntilLanded(simulation, Toward);
    }

    /// <summary>A standing press beside a standing Tesla: the string's opener, the control.</summary>
    private static Landing StrikeStanding(FighterCharacterID id, int seed) {
        var simulation = new FighterSimulation(
            FighterLoadout.Default(id),
            FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        for (int tick = 0; tick < 60 && Units(Fighter(simulation, 1).Position.x - Fighter(simulation, 0).Position.x) > 1.0; tick++) {
            Step(simulation, GameplayButtons.None, Toward);
        }
        for (int tick = 0; tick < 20; tick++) Step(simulation, GameplayButtons.None, moveX: 0);
        Step(simulation, GameplayButtons.BasicAttack, moveX: 0);
        return UntilLanded(simulation, moveX: 0);
    }

    private static Landing UntilLanded(FighterSimulation simulation, sbyte moveX) {
        int flags = Runtime(simulation, 0).AttackFlags;
        int hpBefore = Fighter(simulation, 1).CurrentHP;
        int units = Verb(simulation, 0).MomentumUnits;
        for (int tick = 0; tick < 60 && Fighter(simulation, 1).CurrentHP == hpBefore
            && Runtime(simulation, 0).AttackPhase != FighterBasicAttackRules.PhaseNone; tick++) {
            units = Verb(simulation, 0).MomentumUnits;
            Step(simulation, GameplayButtons.None, moveX);
        }
        FighterStateComponent victim = Fighter(simulation, 1);
        return new Landing(
            victim.CurrentHP < hpBefore,
            victim,
            hpBefore - victim.CurrentHP,
            flags,
            Runtime(simulation, 0).SwingLanded,
            Verb(simulation, 0).MomentumUnits - units - FighterMomentumRules.RegenUnitsPerFrame,
            Units(victim.Position.x - Fighter(simulation, 0).Position.x));
    }

    /// <summary>Eight units apart: nothing lands, so no hitstop bends the clock.</summary>
    private static FighterSimulation Far(FighterCharacterID id, int seed) => new(
        FighterLoadout.Default(id),
        FighterLoadout.Default(FighterCharacterID.Tesla),
        seed: seed, spawnDistance: 4, rules: FighterMatchRules.Disabled);

    private static void RunUp(FighterSimulation simulation, int ticks) {
        for (int tick = 0; tick < ticks; tick++) Step(simulation, GameplayButtons.None, Toward);
    }

    private static double Units(FP64 value) => (double)value.RawValue / FP64.One.RawValue;

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

    private static long Step(FighterSimulation simulation, GameplayButtons playerOne, sbyte moveX = 0, sbyte moveY = 0) =>
        simulation.Advance(
            new PlayerInputFrame {
                Tick = (uint)simulation.CurrentTick,
                MoveX = moveX,
                MoveY = moveY,
                Held = playerOne,
                Pressed = playerOne
            },
            new PlayerInputFrame { Tick = (uint)simulation.CurrentTick });
}
