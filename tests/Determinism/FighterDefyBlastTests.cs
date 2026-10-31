using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The Defy blast and heal in the Fighter sim (meter and Defy update 2026-10-31, plan
/// sections 2.5 and 2.6): who it reaches, what it does to them (an unblockable, non-launching
/// push and stagger with no damage, meter, echo or Momentum), what it cancels, and the
/// heal. Applied on Defy frame 6, before the tick's intents, never through ApplyFighterHit.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterDefyBlastTests {

    private const int BlastTick = BasicComboRules.DefyBlastFrame - 1;

    [TestCase]
    public void TheBlastPushesAnOpponentInTheRingAwayAndStaggersItThirtyFramesWithEightOfHitstop() {
        FighterSimulation simulation = NewSimulation(seed: 921, spawnDistance: 1);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        for (int tick = 0; tick <= BlastTick; tick++) {
            AssertThat(Fighter(simulation, 1).HitstunFrames).OverrideFailureMessage($"tick {tick}: not before frame 6").IsEqual(0);
            Step(simulation, tick == 0 ? GameplayButtons.Defy : GameplayButtons.None);
        }
        FighterStateComponent victim = Fighter(simulation, 1);
        AssertThat(victim.HitstunFrames).IsEqual(BasicComboRules.DefyBlastStaggerFrames);
        AssertThat(victim.Velocity.x.RawValue).OverrideFailureMessage("Pushed away from the Defier at 6 u/s.")
            .IsEqual(FP64.FromInt(BasicComboRules.DefyBlastPushXUnitsPerSecond).RawValue);
        AssertThat(victim.Velocity.y.RawValue).OverrideFailureMessage("and up at 6 u/s.")
            .IsEqual(FP64.FromInt(BasicComboRules.DefyBlastPushYUnitsPerSecond).RawValue);
        AssertThat(victim.IsGrounded).IsEqual(0);
        AssertThat(Verb(simulation, 1).Tumble).OverrideFailureMessage("A stagger, never a launch.").IsEqual(0);
        AssertThat(Verb(simulation, 1).HitstopFrames).IsEqual(BasicComboRules.DefyBlastHitstopFrames);
        AssertThat(Verb(simulation, 0).HitstopFrames).OverrideFailureMessage("The Defier freezes with it.")
            .IsEqual(BasicComboRules.DefyBlastHitstopFrames);

        // The mirror image: a victim on the Defier's left is pushed left.
        FighterSimulation mirrored = NewSimulation(seed: 922, spawnDistance: 1);
        AssertThat(mirrored.SeedFighterInfluenceForTest(1, 50)).IsTrue();
        for (int tick = 0; tick <= BlastTick; tick++) {
            mirrored.Advance(Input(mirrored.CurrentTick, GameplayButtons.None), Input(mirrored.CurrentTick, tick == 0 ? GameplayButtons.Defy : GameplayButtons.None));
        }
        AssertThat(Fighter(mirrored, 0).Velocity.x < FP64.Zero).IsTrue();
    }

    [TestCase]
    public void TheRingIsASquaredFixedPointDistanceOfTwoAndAHalfUnits() {
        var defier = new FighterStateComponent { PlayerID = 0, Position = new FPVector2(FP64.FromInt(3), FP64.FromInt(1)) };
        FighterStateComponent At(double dx, double dy) => new() {
            PlayerID = 1, Position = new FPVector2(defier.Position.x + FP64.FromDouble(dx), defier.Position.y + FP64.FromDouble(dy))
        };
        FighterStateComponent edge = At(2.5, 0);
        AssertThat(FighterDefyBlastRules.InRadius(in defier, in edge)).OverrideFailureMessage("exactly 2.5 u is inside").IsTrue();
        FighterStateComponent justOut = At(2.5, 0);
        justOut.Position.x += FP64.FromRaw(1);
        AssertThat(FighterDefyBlastRules.InRadius(in defier, in justOut)).OverrideFailureMessage("one raw unit further is outside").IsFalse();
        FighterStateComponent diagonal = At(1.5, 2.0);
        AssertThat(FighterDefyBlastRules.InRadius(in defier, in diagonal)).OverrideFailureMessage("1.5, 2.0 is exactly 2.5 away").IsTrue();
        FighterStateComponent diagonalOut = At(1.5, 2.01);
        AssertThat(FighterDefyBlastRules.InRadius(in defier, in diagonalOut)).IsFalse();
        FighterStateComponent below = At(-2.4, -0.5);
        AssertThat(FighterDefyBlastRules.InRadius(in defier, in below)).IsTrue();
        AssertThat(FighterDefyRules.RadiusSquaredRaw.RawValue).IsEqual(FP64.FromDouble(6.25).RawValue);

        // Outside the ring in a real match: 4 u apart is untouched.
        FighterSimulation far = NewSimulation(seed: 923, spawnDistance: 2);
        AssertThat(far.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        for (int tick = 0; tick < 12; tick++) Step(far, tick == 0 ? GameplayButtons.Defy : GameplayButtons.None);
        AssertThat(Fighter(far, 1).HitstunFrames).IsEqual(0);
        AssertThat(Verb(far, 1).HitstopFrames).IsEqual(0);
        AssertThat(Verb(far, 0).HitstopFrames > 0 || Action(far, 0).DefyFrames > BasicComboRules.DefyBlastFrame)
            .OverrideFailureMessage("The Defier still takes its world freeze with nobody in the ring.")
            .IsTrue();
    }

    [TestCase]
    public void TheBlastDealsNoDamageAndMovesNoMeterEchoMomentumOrLandedSignal() {
        FighterSimulation simulation = NewSimulation(seed: 924, spawnDistance: 1);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        AssertThat(simulation.SeedFighterInfluenceForTest(1, 30)).IsTrue();
        int units = Verb(simulation, 0).MomentumUnits;
        for (int tick = 0; tick <= BlastTick; tick++) Step(simulation, tick == 0 ? GameplayButtons.Defy : GameplayButtons.None);

        FighterStateComponent victim = Fighter(simulation, 1);
        AssertThat(victim.CurrentHP).OverrideFailureMessage("no damage, so it cannot kill").IsEqual(victim.MaxHP);
        AssertThat(victim.Influence.RawValue).OverrideFailureMessage("no meter for the victim").IsEqual(FP64.FromInt(30).RawValue);
        AssertThat(Verb(simulation, 1).EchoPool.RawValue).OverrideFailureMessage("no Rally echo").IsEqual(0L);
        AssertThat(Fighter(simulation, 0).Influence.RawValue).OverrideFailureMessage("none for the Defier beyond its 50 spent")
            .IsEqual(0L);
        AssertThat(Verb(simulation, 0).MomentumUnits)
            .OverrideFailureMessage("no Momentum: only the six frames of regeneration")
            .IsEqual(units + (BlastTick + 1) * BasicComboRules.MomentumRegenUnitsPerFrame);
        AssertThat(Runtime(simulation, 0).SwingLanded).OverrideFailureMessage("no D1a landed signal").IsEqual(0);
        AssertThat(Runtime(simulation, 0).SpecialLockoutFrames).IsEqual(0);
    }

    [TestCase]
    public void TheBlastIsUnblockableAndIgnoresHyperArmourAndAnyShield() {
        var defier = new FighterStateComponent { PlayerID = 0, Position = FPVector2.Zero, Stocks = 3 };
        var target = new FighterStateComponent {
            PlayerID = 1, Stocks = 3, MaxHP = 100, CurrentHP = 100, BlockCharges = 3, IsGrounded = 1,
            HyperArmorFrames = 200, Position = new FPVector2(FP64.FromInt(1), FP64.Zero)
        };
        var runtime = new FighterRuntimeComponent { HeldButtons = (int)GameplayButtons.Block };
        var verb = new FighterVerbComponent { ShieldStunFrames = 8 };
        var action = new FighterActionStateComponent();
        var counter = new FighterCounterComponent();
        var knockdown = new FighterKnockdownComponent();
        var activation = new FighterUltimateActivationComponent();

        FighterDefyBlastRules.ApplyToTarget(in defier, ref target, ref runtime, ref verb, ref action, ref counter, ref knockdown, ref activation);

        AssertThat(target.HitstunFrames).OverrideFailureMessage("staggered through the block and the armour").IsEqual(30);
        AssertThat(target.BlockCharges).OverrideFailureMessage("no shield charge is spent").IsEqual(3);
        AssertThat(target.CurrentHP).IsEqual(100);
        AssertThat(verb.HitstunBlockCancelBlocked)
            .OverrideFailureMessage("and the held Block cannot escape the stagger")
            .IsEqual(1);
        AssertThat(verb.ShieldStunFrames).IsEqual(0);
        AssertThat(target.IsGrounded).IsEqual(0);
    }

    [TestCase]
    public void AnInvulnerableOrOutOfPlayTargetIsUntouchedButTheDefierStillHealsAndFreezes() {
        FighterSimulation simulation = NewSimulation(seed: 925, spawnDistance: 1);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        AssertThat(simulation.SeedFighterInvulnerabilityForTest(1, 60)).IsTrue();
        for (int tick = 0; tick <= BlastTick; tick++) Step(simulation, tick == 0 ? GameplayButtons.Defy : GameplayButtons.None);
        AssertThat(Fighter(simulation, 1).HitstunFrames).OverrideFailureMessage("invulnerable: untouched (P9)").IsEqual(0);
        AssertThat(Fighter(simulation, 1).Velocity.y.RawValue).IsEqual(0L);
        AssertThat(Verb(simulation, 1).HitstopFrames).IsEqual(0);
        AssertThat(Verb(simulation, 0).HitstopFrames).IsEqual(BasicComboRules.DefyBlastHitstopFrames);
        AssertThat((Action(simulation, 0).ActionFlags & FighterActionStateComponent.ActionFlagDefyBlastFired) != 0)
            .OverrideFailureMessage("the blast still fired once")
            .IsTrue();

        var defier = new FighterStateComponent { PlayerID = 0, Position = FPVector2.Zero };
        var onPlatform = new FighterStateComponent {
            PlayerID = 1, Stocks = 3, RespawnFramesRemaining = 100, Position = new FPVector2(FP64.One, FP64.Zero)
        };
        AssertThat(FighterDefyBlastRules.Reaches(in defier, in onPlatform)).IsFalse();
        var dead = new FighterStateComponent { PlayerID = 1, Stocks = 0, Position = new FPVector2(FP64.One, FP64.Zero) };
        AssertThat(FighterDefyBlastRules.Reaches(in defier, in dead)).IsFalse();
    }

    [TestCase]
    public void TheStaggerCancelsASwingACastAGrabForesightAndAGetUp() {
        var defier = new FighterStateComponent { PlayerID = 0, Position = FPVector2.Zero, Stocks = 3 };
        var target = new FighterStateComponent {
            PlayerID = 1, Stocks = 3, MaxHP = 100, CurrentHP = 100, IsGrounded = 1,
            Position = new FPVector2(FP64.One, FP64.Zero)
        };
        var runtime = new FighterRuntimeComponent {
            AttackPhase = FighterBasicAttackRules.PhaseActive, AttackPhaseFrames = 4, ComboIndex = 1,
            UniversalMovementState = (int)UniversalMovementPhase.RollTravel, UniversalMovementFramesRemaining = 6
        };
        var verb = new FighterVerbComponent { GrabPhase = FighterGrabRules.PhaseStartup, GrabPhaseFrames = 3 };
        var action = new FighterActionStateComponent {
            SpecialSlot = 1, SpecialPhase = FighterSpecialCastRules.PhaseStartup, SpecialPhaseFrames = 5, SpecialCancelBuffer = 5
        };
        var counter = new FighterCounterComponent { Phase = FighterForesightRules.PhaseWindow, PhaseFrames = 10 };
        var knockdown = new FighterKnockdownComponent { GetUpFrames = 8, GetUpKind = BasicComboRules.GetUpNeutral };
        var activation = new FighterUltimateActivationComponent();

        FighterDefyBlastRules.ApplyToTarget(in defier, ref target, ref runtime, ref verb, ref action, ref counter, ref knockdown, ref activation);

        AssertThat(runtime.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseNone);
        AssertThat(runtime.ComboIndex).IsEqual(0);
        AssertThat(runtime.UniversalMovementState).IsEqual((int)UniversalMovementPhase.None);
        AssertThat(verb.GrabPhase).IsEqual(FighterGrabRules.PhaseNone);
        AssertThat(FighterSpecialCastRules.IsBusy(in action)).IsFalse();
        AssertThat(action.SpecialCancelBuffer).IsEqual(0);
        AssertThat(FighterForesightRules.IsBusy(in counter)).OverrideFailureMessage("the blast is no strike: the stance just ends").IsFalse();
        AssertThat(FighterKnockdownRules.IsActive(in knockdown)).IsFalse();
    }

    [TestCase]
    public void AnUltimateWindupOrActiveWindowIsCancelledWithItsMeterSpentButAWhiffRecoveryRunsOn() {
        var defier = new FighterStateComponent { PlayerID = 0, Position = FPVector2.Zero, Stocks = 3 };
        foreach (int phase in new[] { FighterUltimateActivationRules.PhaseWindup, FighterUltimateActivationRules.PhaseActive }) {
            var target = new FighterStateComponent {
                PlayerID = 1, Stocks = 3, MaxHP = 100, CurrentHP = 100, IsGrounded = 1, HyperArmorFrames = 30,
                Influence = FP64.Zero, Position = new FPVector2(FP64.One, FP64.Zero)
            };
            var runtime = new FighterRuntimeComponent();
            var verb = new FighterVerbComponent();
            var action = new FighterActionStateComponent();
            var counter = new FighterCounterComponent();
            var knockdown = new FighterKnockdownComponent();
            var activation = new FighterUltimateActivationComponent { Phase = phase, PhaseFrames = 9, StartedAerial = 0, FacingRight = 1 };
            FighterDefyBlastRules.ApplyToTarget(in defier, ref target, ref runtime, ref verb, ref action, ref counter, ref knockdown, ref activation);
            AssertThat(activation.Phase).OverrideFailureMessage($"phase {phase} is cancelled").IsEqual(FighterUltimateActivationRules.PhaseNone);
            AssertThat(target.Influence.RawValue).OverrideFailureMessage("the meter it spent is not refunded").IsEqual(0L);
            AssertThat(target.HyperArmorFrames).IsEqual(0);
        }
        var recovering = new FighterStateComponent {
            PlayerID = 1, Stocks = 3, MaxHP = 100, CurrentHP = 100, IsGrounded = 1, Position = new FPVector2(FP64.One, FP64.Zero)
        };
        var recoveringActivation = new FighterUltimateActivationComponent {
            Phase = FighterUltimateActivationRules.PhaseWhiffRecovery, PhaseFrames = 20
        };
        var r = new FighterRuntimeComponent();
        var v = new FighterVerbComponent();
        var a = new FighterActionStateComponent();
        var c = new FighterCounterComponent();
        var k = new FighterKnockdownComponent();
        FighterDefyBlastRules.ApplyToTarget(in defier, ref recovering, ref r, ref v, ref a, ref c, ref k, ref recoveringActivation);
        AssertThat(recoveringActivation.Phase).IsEqual(FighterUltimateActivationRules.PhaseWhiffRecovery);
    }

    [TestCase]
    public void TheHealIsTheLargerOfTheEchoPoolAndATenthOfMaxHpCappedAtTheMissingHpAndThePoolIsZeroed() {
        // (missing, echo pool, max HP) -> heal
        (int Missing, double Echo, int Expected)[] cases = {
            (60, 25.0, 25),   // the pool beats the floor
            (60, 3.0, 10),    // an empty-ish pool still heals the 10 % floor
            (60, 0.0, 10),
            (4, 25.0, 4),     // capped at what is missing
            (0, 25.0, 0),     // nothing missing, nothing healed
            (60, 24.6, 25)    // the pool rounds half up
        };
        foreach ((int missing, double echo, int expected) in cases) {
            var fighter = new FighterStateComponent { MaxHP = 100, CurrentHP = 100 - missing };
            var verb = new FighterVerbComponent {
                EchoPool = FP64.FromDouble(echo), EchoDrainPerFrame = FP64.FromDouble(echo / 150.0)
            };
            int healed = FighterDefyBlastRules.Heal(ref fighter, ref verb);
            AssertThat(healed).OverrideFailureMessage($"missing {missing}, pool {echo}").IsEqual(expected);
            AssertThat(fighter.CurrentHP).IsEqual(100 - missing + expected);
            AssertThat(verb.EchoPool.RawValue).OverrideFailureMessage("the pool is cashed in").IsEqual(0L);
            AssertThat(verb.EchoDrainPerFrame.RawValue).IsEqual(0L);
        }

        // In a match: a fighter that took a hit heals at least the floor on the blast frame.
        FighterSimulation simulation = NewSimulation(seed: 926, spawnDistance: 1);
        for (int tick = 0; tick < 60; tick++) {
            bool close = Fighter(simulation, 1).Position.x - Fighter(simulation, 0).Position.x <= FP64.FromDouble(1.2);
            simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.None), Input(simulation.CurrentTick, GameplayButtons.None, close ? (sbyte)0 : (sbyte)-127));
            if (close) break;
        }
        for (int tick = 0; tick < 20; tick++) Step(simulation, GameplayButtons.None);
        int hpFull = Fighter(simulation, 0).CurrentHP;
        simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.None), Input(simulation.CurrentTick, GameplayButtons.BasicAttack));
        for (int tick = 0; tick < 30 && Fighter(simulation, 0).CurrentHP == hpFull; tick++) Step(simulation, GameplayButtons.None);
        int hurt = Fighter(simulation, 0).CurrentHP;
        AssertThat(hurt < hpFull).OverrideFailureMessage("Harness: the hit must land.").IsTrue();
        for (int tick = 0; tick < 12 && Verb(simulation, 0).HitstopFrames > 0; tick++) Step(simulation, GameplayButtons.None);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        Step(simulation, GameplayButtons.Defy);
        int hpAtPress = Fighter(simulation, 0).CurrentHP;
        for (int tick = 0; tick < BlastTick; tick++) Step(simulation, GameplayButtons.None);
        AssertThat(Fighter(simulation, 0).CurrentHP - hpAtPress >= 10)
            .OverrideFailureMessage("The blast frame heals at least a tenth of max HP.")
            .IsTrue();
        AssertThat(Verb(simulation, 0).EchoPool.RawValue).IsEqual(0L);
    }

    [TestCase]
    public void TwoDefiesOnTheSameFrameTouchNeitherAndALaterOneIsCaughtInItsRecovery() {
        FighterSimulation same = NewSimulation(seed: 927, spawnDistance: 1);
        AssertThat(same.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        AssertThat(same.SeedFighterInfluenceForTest(1, 50)).IsTrue();
        for (int tick = 0; tick <= BlastTick + 1; tick++) {
            GameplayButtons press = tick == 0 ? GameplayButtons.Defy : GameplayButtons.None;
            same.Advance(Input(same.CurrentTick, press), Input(same.CurrentTick, press));
        }
        for (int player = 0; player < 2; player++) {
            AssertThat(Fighter(same, player).HitstunFrames).OverrideFailureMessage($"player {player}: both are invulnerable on frame 6").IsEqual(0);
            AssertThat(Fighter(same, player).Influence.RawValue).OverrideFailureMessage("both paid").IsEqual(0L);
            AssertThat(Action(same, player).DefyFrames).IsEqual(BasicComboRules.DefyBlastFrame);
        }

        // Player two Defies at once; player one, standing there, is staggered by its blast and
        // frozen for eight frames. It presses Defy inside that freeze (the press is held), the
        // Defy begins on the thaw tick, and its own blast lands five ticks later: player two
        // is then on frame 11 of its recovery, vulnerable, so it is staggered and its Defy
        // is interrupted.
        FighterSimulation later = NewSimulation(seed: 928, spawnDistance: 1);
        AssertThat(later.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        AssertThat(later.SeedFighterInfluenceForTest(1, 50)).IsTrue();
        later.Advance(Input(later.CurrentTick, GameplayButtons.None), Input(later.CurrentTick, GameplayButtons.Defy));
        for (int tick = 1; tick < 6; tick++) Step(later, GameplayButtons.None);
        AssertThat(Fighter(later, 0).HitstunFrames)
            .OverrideFailureMessage("The idle fighter is caught by the first blast.")
            .IsEqual(BasicComboRules.DefyBlastStaggerFrames);
        for (int tick = 6; tick < 9; tick++) Step(later, GameplayButtons.None);
        Step(later, GameplayButtons.Defy);
        AssertThat(Action(later, 0).DefyFrames).OverrideFailureMessage("held through the freeze").IsEqual(0);
        for (int tick = 10; tick < 14; tick++) Step(later, GameplayButtons.None);
        AssertThat(Action(later, 0).DefyFrames).OverrideFailureMessage("begins on the thaw tick").IsEqual(1);
        AssertThat(Action(later, 1).DefyFrames > BasicComboRules.DefyBlastFrame - 1).IsTrue();
        for (int tick = 14; tick < 19; tick++) Step(later, GameplayButtons.None);
        AssertThat(Fighter(later, 1).HitstunFrames)
            .OverrideFailureMessage("The earlier Defier is caught in its vulnerable recovery.")
            .IsEqual(BasicComboRules.DefyBlastStaggerFrames);
        Step(later, GameplayButtons.None);
        Step(later, GameplayButtons.None);
        AssertThat(Action(later, 1).DefyFrames).OverrideFailureMessage("and a hit interrupts its recovery").IsEqual(0);
    }

    [TestCase]
    public void AnUltimateWindupIsCancelledByTheBlastAndNoStrikeEverReachesTheDefier() {
        FighterSimulation simulation = NewSimulation(seed: 929, spawnDistance: 1);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        AssertThat(simulation.SeedFighterInfluenceForTest(1, 50)).IsTrue();
        int hp = Fighter(simulation, 1).CurrentHP;
        simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.Ultimate), Input(simulation.CurrentTick, GameplayButtons.None));
        AssertThat(Activation(simulation, 0).Phase).OverrideFailureMessage("Harness: wind-up.").IsEqual(FighterUltimateActivationRules.PhaseWindup);
        for (int tick = 0; tick < 11; tick++) Step(simulation, GameplayButtons.None);
        simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.None), Input(simulation.CurrentTick, GameplayButtons.Defy));
        int cancelled = -1;
        for (int tick = 0; tick < 12 && cancelled < 0; tick++) {
            Step(simulation, GameplayButtons.None);
            if (Activation(simulation, 0).Phase == FighterUltimateActivationRules.PhaseNone) cancelled = tick;
        }
        AssertThat(cancelled >= 0).OverrideFailureMessage("The blast cancels the wind-up (R3).").IsTrue();
        AssertThat(Fighter(simulation, 0).Influence.RawValue).OverrideFailureMessage("and its meter stays spent").IsEqual(0L);
        for (int tick = 0; tick < 40; tick++) Step(simulation, GameplayButtons.None);
        AssertThat(Fighter(simulation, 1).CurrentHP).OverrideFailureMessage("No strike ever landed on the Defier.").IsEqual(hp);
        AssertThat(Activation(simulation, 1).CapturedFrames).IsEqual(0);
    }

    // === helpers ==============================================================

    private static FighterSimulation NewSimulation(int seed, int spawnDistance) => new(
        FighterLoadout.Default(FighterCharacterID.Joan), FighterLoadout.Default(FighterCharacterID.Joan),
        seed: seed, spawnDistance: spawnDistance, rules: FighterMatchRules.Disabled);

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

    private static FighterUltimateActivationComponent Activation(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterUltimateActivation(playerID, out FighterUltimateActivationComponent activation)).IsTrue();
        return activation;
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
