using System;
using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// CM-MOVE (combat and movement update 2026-10-16, decision 5.2; plan §2.5.2;
/// ledger <c>USER-FIGHTER-LEDGE-GETUPS</c>, <c>PROVISIONAL-LEDGE-GETUP-FRAMES</c>):
/// the Fighter ledge get-ups — the neutral climb on a fresh toward-stage or Up
/// tilt (20 frames, intangible 1–12), the ledge roll (an 8-frame intangible
/// climb, then the grounded roll toward the stage) and the ledge attack (an
/// 8-frame intangible climb, then an 8 / 4 / 14 strike) — plus the first grab's
/// 30 intangible frames, the one-action-per-tick priority, and rollback.
/// </summary>
/// <remarks>
/// Pure C#: default loadouts on a <see cref="FighterStageGeometry.ForTest"/>
/// stage — one 6-unit one-way platform 1.5 u up that both fighters spawn under
/// (at x = ∓2). Player one walks off its left end and hangs there (anchor 0;
/// the stage is to its right). Every edge-reading input is
/// <see cref="PlayerInputFrame.Create"/>-built (plan §1.8). Intangibility is
/// read with a probe: a plain basic from player two through
/// <see cref="FighterDamageRules.ApplyFighterHit"/> on copies of the live
/// components, which is exactly what that tick's combat pass saw.
/// </remarks>
[TestSuite]
public class FighterLedgeGetUpTests {
    private static readonly FP64 Surface = FP64.FromDouble(1.5);
    private static readonly FP64 HalfWidth = FP64.FromInt(3);
    private static readonly FighterStageGeometry Stage = FighterStageGeometry.ForTest(
        "cm_move_ledge_getup", new[] { new FighterStagePlatform(FP64.Zero, FP64.FromDouble(1.5), FP64.FromInt(3)) },
        spawnDistance: 2);

    private const sbyte TowardStage = 127;
    private const sbyte AwayFromStage = -127;

    /// <summary>
    /// Each get-up ends standing on the platform at its frame count: the neutral
    /// climb is locked for 20 ticks and stands 0.5 u inside the edge, acting on
    /// the 21st; the ledge roll climbs 8 ticks and rolls its 4 / 12 / 10 toward
    /// the stage, ending on the platform; the ledge attack climbs 8 ticks and
    /// strikes 8 / 4 / 14 from the stand position.
    /// </summary>
    [TestCase]
    public void EachGetUpEndsOnThePlatformAtItsFrameCount() {
        FPVector2 stand = Stand();

        // The neutral climb.
        FighterSimulation climb = HangingReady(out int tick);
        int press = tick;
        tick = Tilt(climb, tick, TowardStage);
        for (int frame = 1; frame < BasicComboRules.LedgeNeutralClimbFrames; frame++) {
            AssertThat(Runtime(climb).UniversalMovementState)
                .OverrideFailureMessage($"neutral climb frame {frame}: not climbing.")
                .IsEqual((int)UniversalMovementPhase.LedgeClimb);
            tick = Step(climb, tick, Neutral(tick));
        }
        // Tick press + 19 is the 20th and last climb tick: standing at the stand position.
        AssertThat(Runtime(climb).UniversalMovementState).IsEqual((int)UniversalMovementPhase.LedgeClimb);
        AssertThat(Fighter(climb).Position.x.RawValue).IsEqual(stand.x.RawValue);
        AssertThat(Fighter(climb).Position.y.RawValue).IsEqual(stand.y.RawValue);
        AssertThat(tick - press).IsEqual(BasicComboRules.LedgeNeutralClimbFrames);
        // The 21st tick acts: a jump pressed there leaves the platform.
        tick = Step(climb, tick, PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.Jump));
        AssertThat(Runtime(climb).UniversalMovementState).IsEqual((int)UniversalMovementPhase.None);
        AssertThat(Fighter(climb).Velocity.y > FP64.Zero)
            .OverrideFailureMessage("the tick after the neutral climb must take the fighter's input.")
            .IsTrue();

        // The ledge roll.
        FighterSimulation roll = HangingReady(out tick);
        press = tick;
        tick = Step(roll, tick, PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.Roll));
        for (int frame = 1; frame < BasicComboRules.LedgeActionClimbFrames; frame++) {
            AssertThat(Runtime(roll).UniversalMovementState).IsEqual((int)UniversalMovementPhase.LedgeRoll);
            tick = Step(roll, tick, Neutral(tick));
        }
        AssertThat(Fighter(roll).Position.x.RawValue).IsEqual(stand.x.RawValue);
        tick = Step(roll, tick, Neutral(tick));
        AssertThat(tick - press).IsEqual(BasicComboRules.LedgeActionClimbFrames + 1);
        AssertThat(Runtime(roll).UniversalMovementState).IsEqual((int)UniversalMovementPhase.RollStartup);
        int rollTicks = 1;
        while (Runtime(roll).UniversalMovementState != (int)UniversalMovementPhase.None && rollTicks < 60) {
            tick = Step(roll, tick, Neutral(tick));
            rollTicks++;
        }
        AssertThat(rollTicks)
            .OverrideFailureMessage("the ledge roll's roll must run exactly as long as a pressed 4 / 12 / 10 roll.")
            .IsEqual(PressedRollTicks());
        AssertThat(Fighter(roll).IsGrounded).IsEqual(1);
        AssertThat(Fighter(roll).Position.y.RawValue).IsEqual(Surface.RawValue);
        AssertThat(Fighter(roll).Position.x > stand.x)
            .OverrideFailureMessage("the ledge roll must travel toward the stage.")
            .IsTrue();

        // The ledge attack.
        FighterSimulation attack = HangingReady(out tick);
        press = tick;
        tick = Step(attack, tick, PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.BasicAttack));
        for (int frame = 1; frame < BasicComboRules.LedgeActionClimbFrames; frame++) {
            AssertThat(Runtime(attack).UniversalMovementState).IsEqual((int)UniversalMovementPhase.LedgeAttackClimb);
            AssertThat(Runtime(attack).AttackPhase).IsEqual(FighterBasicAttackRules.PhaseNone);
            tick = Step(attack, tick, Neutral(tick));
        }
        tick = Step(attack, tick, Neutral(tick));
        AssertThat(Runtime(attack).AttackPhase).IsEqual(FighterBasicAttackRules.PhaseStartup);
        AssertThat((Runtime(attack).AttackFlags & FighterBasicAttackRules.FlagLedgeAttack) != 0).IsTrue();
        AssertThat(Fighter(attack).Position.x.RawValue).IsEqual(stand.x.RawValue);
        int startup = 1, active = 0, recovery = 0;
        for (int step = 0; step < 60 && Runtime(attack).AttackPhase != FighterBasicAttackRules.PhaseNone; step++) {
            tick = Step(attack, tick, Neutral(tick));
            int phase = Runtime(attack).AttackPhase;
            if (phase == FighterBasicAttackRules.PhaseStartup) startup++;
            else if (phase == FighterBasicAttackRules.PhaseActive) active++;
            else if (phase == FighterBasicAttackRules.PhaseRecovery) recovery++;
        }
        AssertThat(startup).IsEqual(BasicComboRules.LedgeAttackStartupFrames);
        AssertThat(active).IsEqual(BasicComboRules.LedgeAttackActiveFrames);
        AssertThat(recovery).IsEqual(BasicComboRules.LedgeAttackRecoveryFrames);
        AssertThat(Fighter(attack).IsGrounded).IsEqual(1);
        AssertThat(Fighter(attack).Position.y.RawValue).IsEqual(Surface.RawValue);
    }

    /// <summary>
    /// Each get-up's intangible frames, read by the probe: the neutral climb
    /// refuses a hit on its first 12 ticks and takes one from the 13th to its
    /// end; the ledge roll's and the ledge attack's 8-tick climbs refuse a hit
    /// throughout.
    /// </summary>
    [TestCase]
    public void EachGetUpIsIntangibleForItsClimbFrames() {
        FighterSimulation climb = HangingReady(out int tick);
        AssertThat(ProbeHitLands(climb)).OverrideFailureMessage("the hang itself is intangible after its first 30 frames.").IsTrue();
        tick = Tilt(climb, tick, TowardStage);
        for (int frame = 1; frame <= BasicComboRules.LedgeNeutralClimbFrames; frame++) {
            bool intangible = frame <= BasicComboRules.LedgeNeutralClimbIntangibleFrames;
            AssertThat(ProbeHitLands(climb))
                .OverrideFailureMessage($"neutral climb frame {frame}: intangible {intangible}.")
                .IsEqual(!intangible);
            tick = Step(climb, tick, Neutral(tick));
        }

        foreach (GameplayButtons button in new[] { GameplayButtons.Roll, GameplayButtons.BasicAttack }) {
            FighterSimulation action = HangingReady(out tick);
            tick = Step(action, tick, PlayerInputFrame.Create((uint)tick, 0f, 0f, button));
            for (int frame = 1; frame <= BasicComboRules.LedgeActionClimbFrames; frame++) {
                AssertThat(ProbeHitLands(action))
                    .OverrideFailureMessage($"{button} climb frame {frame} must be intangible.")
                    .IsFalse();
                tick = Step(action, tick, Neutral(tick));
            }
        }
    }

    /// <summary>
    /// The ledge attack strikes a target standing at the edge — one basic, string
    /// hit 1's knockback, 30 frames of hitstun, no launch — and it is an
    /// offensive action at its press: <see cref="FighterLedgeRules.GetUpActionKind"/>
    /// hands the chokepoint <see cref="FighterActionKind.Swing"/> (CM-HIT's body
    /// ends respawn and Defy protection on it), while the climb and the roll are
    /// <see cref="FighterActionKind.LedgeGetUp"/>. The climb's own intangibility is
    /// max-assigned after the chokepoint, so a shorter grant already running
    /// gives way to its 8 frames.
    /// </summary>
    [TestCase]
    public void TheLedgeAttackStrikesATargetAtTheEdgeAndIsAnOffensiveActionWhoseClimbFramesSurvive() {
        AssertThat(FighterLedgeRules.GetUpActionKind((int)UniversalMovementPhase.LedgeAttackClimb))
            .IsEqual(FighterActionKind.Swing);
        AssertThat(FighterLedgeRules.GetUpActionKind((int)UniversalMovementPhase.LedgeClimb))
            .IsEqual(FighterActionKind.LedgeGetUp);
        AssertThat(FighterLedgeRules.GetUpActionKind((int)UniversalMovementPhase.LedgeRoll))
            .IsEqual(FighterActionKind.LedgeGetUp);
        AssertThat(FighterActionRules.IsOffensive(FighterActionKind.Swing)).IsTrue();
        AssertThat(FighterActionRules.IsOffensive(FighterActionKind.LedgeGetUp)).IsFalse();

        FighterSimulation simulation = HangingReady(out int tick, opponentX: FP64.FromDouble(-1.6));
        AssertThat(simulation.SeedFighterInvulnerabilityForTest(0, 3)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        tick = Step(simulation, tick, PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.BasicAttack));
        AssertThat(Fighter(simulation).InvulnerabilityFrames)
            .OverrideFailureMessage("the ledge attack's climb intangibility must be max-assigned over a shorter grant.")
            .IsEqual(BasicComboRules.LedgeActionClimbFrames);
        bool struck = false;
        for (int step = 0; step < 40 && !struck; step++) {
            tick = Step(simulation, tick, Neutral(tick));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
            if (target.CurrentHP >= before.CurrentHP) continue;
            struck = true;
            // One basic at x1.0 (LedgeAttackDamageMultiplier).
            AssertThat(BasicComboRules.LedgeAttackDamageMultiplier).IsEqual(1f);
            AssertThat(before.CurrentHP - target.CurrentHP)
                .IsEqual(FighterLoadout.Default(FighterCharacterID.Einstein).BasicDamage);
            AssertThat(target.HitstunFrames > 0 && target.HitstunFrames <= BasicComboRules.LedgeAttackHitstunFrames).IsTrue();
        }
        AssertThat(struck).OverrideFailureMessage("the ledge attack never struck the target at the edge.").IsTrue();
        // Past the hit's freeze: pushed toward the stage, and no launch — the
        // target slides grounded, never airborne.
        for (int step = 0; step < 20; step++) {
            tick = Step(simulation, tick, Neutral(tick));
            AssertThat(Fighter(simulation, 1).IsGrounded)
                .OverrideFailureMessage("the ledge attack launched its target.")
                .IsEqual(1);
        }
        AssertThat(Fighter(simulation, 1).Position.x > before.Position.x)
            .OverrideFailureMessage("the strike must push the target toward the stage.")
            .IsTrue();
    }

    /// <summary>
    /// Phase B merge fix-up (CM-MOVE x CM-BLOCK, decision 3.4): the ledge attack
    /// is a blockable basic, so its hit site passes the victim's component 323
    /// like every other one. A raise long before contact is an ordinary block
    /// (one charge, shieldstun); a raise two ticks before contact is perfect (no
    /// charge, no shieldstun).
    /// </summary>
    [TestCase]
    public void TheLedgeAttackCanBePerfectBlocked() {
        int contact = LedgeAttackBlockedStep(raiseStep: 0,
            out FighterStateComponent ordinaryStart, out FighterStateComponent ordinary,
            out FighterVerbComponent ordinaryVerb, out int ordinaryPerfect);
        AssertThat(contact)
            .OverrideFailureMessage("Harness: the ledge attack must take at least four ticks to reach player two.")
            .IsGreaterEqual(4);
        AssertThat(ordinary.CurrentHP).IsEqual(ordinaryStart.CurrentHP);
        AssertThat(ordinary.BlockCharges)
            .OverrideFailureMessage("Harness: a raise long before contact is an ordinary block of the ledge attack.")
            .IsEqual(ordinaryStart.BlockCharges - 1);
        AssertThat(ordinaryVerb.ShieldStunFrames > 0).IsTrue();
        AssertThat(ordinaryPerfect).IsEqual(0);

        int perfectContact = LedgeAttackBlockedStep(raiseStep: contact - 2,
            out FighterStateComponent start, out FighterStateComponent blocker,
            out FighterVerbComponent verb, out int perfect);
        AssertThat(perfectContact).IsEqual(contact);
        AssertThat(blocker.CurrentHP).IsEqual(start.CurrentHP);
        AssertThat(blocker.BlockCharges)
            .OverrideFailureMessage("A raise two ticks before the ledge attack's contact is perfect: no charge.")
            .IsEqual(start.BlockCharges);
        AssertThat(verb.ShieldStunFrames)
            .OverrideFailureMessage("A perfect block of the ledge attack sets no shieldstun.")
            .IsEqual(0);
        AssertThat(perfect).IsEqual(1);
    }

    /// <summary>
    /// Player one ledge-attacks from the hang at step 0; player two, standing at
    /// the edge facing it, holds Block from <paramref name="raiseStep"/> on
    /// (a real press edge). Returns the step the blocked contact shows on.
    /// </summary>
    private static int LedgeAttackBlockedStep(
        int raiseStep,
        out FighterStateComponent start,
        out FighterStateComponent blocker,
        out FighterVerbComponent verb,
        out int perfectBlocks) {
        FighterSimulation simulation = HangingReady(out int tick, opponentX: FP64.FromDouble(-1.6));
        start = Fighter(simulation, 1);
        GameplayButtons previousTwo = GameplayButtons.None;
        for (int step = 0; step < 40; step++, tick++) {
            GameplayButtons two = step >= raiseStep ? GameplayButtons.Block : GameplayButtons.None;
            simulation.Advance(
                PlayerInputFrame.Create((uint)tick, 0f, 0f, step == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None),
                PlayerInputFrame.Create((uint)tick, 0f, 0f, two, previousTwo));
            previousTwo = two;
            blocker = Fighter(simulation, 1);
            verb = Verb(simulation, 1);
            AssertThat(simulation.TryGetFighterActionState(1, out FighterActionStateComponent action)).IsTrue();
            perfectBlocks = FighterBlockTimingRules.PerfectBlockCount(in action);
            if (blocker.BlockCharges < start.BlockCharges || verb.ShieldStunFrames > 0
                || blocker.CurrentHP < start.CurrentHP || perfectBlocks > 0) {
                return step;
            }
        }
        AssertThat(false).OverrideFailureMessage("Harness: the ledge attack never reached player two.").IsTrue();
        blocker = default;
        verb = default;
        perfectBlocks = 0;
        return -1;
    }

    /// <summary>
    /// The first grab of an airtime is intangible for 30 frames; a regrab in the
    /// same airtime (the climb jump falling back into the box) gets nothing.
    /// </summary>
    [TestCase]
    public void TheFirstGrabIsIntangibleAndARegrabIsNot() {
        FighterSimulation simulation = Hanging(out int tick);
        AssertThat(Verb(simulation).LedgeGrabsThisAirtime).IsEqual(1);
        AssertThat(Fighter(simulation).InvulnerabilityFrames).IsEqual(BasicComboRules.LedgeFirstGrabIntangibleFrames);
        for (int frame = 1; frame < BasicComboRules.LedgeFirstGrabIntangibleFrames; frame++) {
            AssertThat(ProbeHitLands(simulation)).OverrideFailureMessage($"first grab, hang frame {frame}.").IsFalse();
            tick = Step(simulation, tick, Neutral(tick));
        }
        for (int frame = 0; frame < 3; frame++) tick = Step(simulation, tick, Neutral(tick));
        AssertThat(ProbeHitLands(simulation)).IsTrue();

        // Climb-jump straight up and fall back into the box: a regrab.
        tick = Step(simulation, tick, PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.Jump));
        AssertThat(FighterLedgeRules.IsHanging(Runtime(simulation))).IsFalse();
        GameplayButtons previous = GameplayButtons.Jump;
        bool regrabbed = false;
        for (int step = 0; step < 120 && !regrabbed; step++) {
            tick = Step(simulation, tick, PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None, previous));
            previous = GameplayButtons.None;
            regrabbed = FighterLedgeRules.IsHanging(Runtime(simulation));
        }
        AssertThat(regrabbed).OverrideFailureMessage("the climb jump never fell back into the capture box.").IsTrue();
        AssertThat(Verb(simulation).LedgeGrabsThisAirtime).IsEqual(2);
        AssertThat(ProbeHitLands(simulation))
            .OverrideFailureMessage("a regrab in the same airtime must not be intangible.")
            .IsTrue();
    }

    /// <summary>
    /// Block+Roll (the Echo Step chord) and Block+Attack (the grab chord) start
    /// nothing from the hang, and neither do the logical Echo Step and Grab bits
    /// on their own; the fighter keeps hanging, untouched.
    /// </summary>
    [TestCase]
    public void TheBlockChordsAndTheLogicalChordBitsStartNothingFromTheHang() {
        FighterSimulation simulation = HangingReady(out int tick);
        FighterStateComponent hanging = Fighter(simulation);
        GameplayButtons[] presses = {
            GameplayButtons.Block | GameplayButtons.Roll,
            GameplayButtons.Block | GameplayButtons.BasicAttack,
            GameplayButtons.Roll | GameplayButtons.EchoStep,
            GameplayButtons.BasicAttack | GameplayButtons.Grab,
            GameplayButtons.Block | GameplayButtons.Roll | GameplayButtons.EchoStep,
            GameplayButtons.Block | GameplayButtons.BasicAttack | GameplayButtons.Grab
        };
        foreach (GameplayButtons press in presses) {
            tick = Step(simulation, tick, PlayerInputFrame.Create((uint)tick, 0f, 0f, press));
            tick = Step(simulation, tick, PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None, press));
            FighterRuntimeComponent runtime = Runtime(simulation);
            AssertThat(FighterLedgeRules.IsHanging(in runtime)).OverrideFailureMessage($"{press} left the ledge.").IsTrue();
            AssertThat(runtime.UniversalMovementState).OverrideFailureMessage($"{press} started a get-up.")
                .IsEqual((int)UniversalMovementPhase.None);
            AssertThat(runtime.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseNone);
            AssertThat(Fighter(simulation).Position.x.RawValue).IsEqual(hanging.Position.x.RawValue);
        }
    }

    /// <summary>
    /// A hit in the neutral climb's vulnerable frames (13–20) ends it: the
    /// climber goes into hitstun and never stands up out of the climb.
    /// </summary>
    [TestCase]
    public void AHitInTheVulnerableFramesEndsTheGetUp() {
        FighterSimulation simulation = HangingReady(out int tick, opponentX: FP64.FromDouble(-1.6));
        int press = tick;
        tick = Tilt(simulation, tick, TowardStage);
        int startup = BasicComboRules.StringProfileFor("joan").GroundStartupFrames[0];
        int swingAt = press + BasicComboRules.LedgeNeutralClimbIntangibleFrames + 2 - startup;
        int hitTick = -1;
        for (int step = 0; step < 40 && hitTick < 0; step++) {
            PlayerInputFrame two = tick == swingAt
                ? PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.BasicAttack)
                : Neutral(tick);
            simulation.Advance(Neutral(tick), two);
            if (Fighter(simulation).HitstunFrames > 0) hitTick = tick;
            tick++;
        }
        AssertThat(hitTick >= 0).OverrideFailureMessage("the swing never struck the climber.").IsTrue();
        int frame = hitTick - press + 1;
        AssertThat(frame > BasicComboRules.LedgeNeutralClimbIntangibleFrames && frame <= BasicComboRules.LedgeNeutralClimbFrames)
            .OverrideFailureMessage($"the hit landed on climb frame {frame}, outside the vulnerable frames.")
            .IsTrue();
        // The hit's hitstop freezes the climber first; the climb ends on the thaw.
        for (int step = 0; step < 12; step++) tick = Step(simulation, tick, Neutral(tick));
        AssertThat(FighterLedgeRules.IsGetUpClimb(Runtime(simulation)))
            .OverrideFailureMessage("a hit in the vulnerable frames must end the climb.")
            .IsFalse();
        AssertThat(Fighter(simulation).HitstunFrames > 0 || Fighter(simulation).IsGrounded == 0)
            .OverrideFailureMessage("the struck climber stood up out of the climb.")
            .IsTrue();
    }

    /// <summary>
    /// The neutral-climb trigger, Create-built: a stick held toward the stage on
    /// every hang tick from the first never climbs, however long; one neutral
    /// tick re-arms it, and the next tilt (the hang being 6 frames old) climbs;
    /// a fresh tilt before 6 hang frames does not; and a Jump pressed on the
    /// tick of a fresh tilt climb-jumps instead (the CPU's hang answer).
    /// </summary>
    [TestCase]
    public void TheNeutralClimbNeedsAFreshTiltAfterSixFramesAndJumpWinsItsTick() {
        // Held toward the stage from the first hang tick: never climbs.
        FighterSimulation held = Hanging(out int tick);
        GameplayButtons previous = GameplayButtons.None;
        for (int step = 0; step < 60; step++) {
            tick = Step(held, tick, PlayerInputFrame.Create((uint)tick, 1f, 0f, GameplayButtons.None, previous));
            AssertThat(FighterLedgeRules.IsHanging(Runtime(held)))
                .OverrideFailureMessage($"hang tick {step}: a stick held toward the stage from the grab climbed.")
                .IsTrue();
        }
        // One neutral tick re-arms; the next tilt (Up, this time) climbs.
        tick = Step(held, tick, PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None));
        AssertThat(FighterLedgeRules.IsHanging(Runtime(held))).IsTrue();
        tick = Step(held, tick, PlayerInputFrame.Create((uint)tick, 0f, -1f, GameplayButtons.None));
        AssertThat(Runtime(held).UniversalMovementState)
            .OverrideFailureMessage("a fresh Up tilt after a neutral tick must climb.")
            .IsEqual((int)UniversalMovementPhase.LedgeClimb);

        // Re-armed on the first hang tick, a tilt held from the second does not
        // climb until the hang is 6 frames old, and then climbs at once.
        FighterSimulation early = Hanging(out tick);
        tick = Step(early, tick, PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None));
        int tilted = -1;
        for (int step = 0; step < 20 && tilted < 0; step++) {
            int hangFrames = Runtime(early).LedgeStateFrames;
            tick = Step(early, tick, PlayerInputFrame.Create((uint)tick, 1f, 0f, GameplayButtons.None));
            if (Runtime(early).UniversalMovementState == (int)UniversalMovementPhase.LedgeClimb) tilted = hangFrames;
        }
        AssertThat(tilted).OverrideFailureMessage("the held tilt never climbed.")
            .IsEqual(BasicComboRules.LedgeGetUpMinHangFrames);

        // Jump on the tick of a fresh tilt: the climb jump.
        FighterSimulation jump = HangingReady(out tick);
        tick = Step(jump, tick, PlayerInputFrame.Create((uint)tick, 1f, 0f, GameplayButtons.Jump));
        AssertThat(FighterLedgeRules.IsHanging(Runtime(jump))).IsFalse();
        AssertThat(Runtime(jump).UniversalMovementState).IsEqual((int)UniversalMovementPhase.None);
        AssertThat(Fighter(jump).Velocity.y > FP64.Zero).OverrideFailureMessage("Jump must win the tilt's tick.").IsTrue();
    }

    /// <summary>
    /// Every get-up is snapshotted state: player two runs each option from a
    /// hang (the neutral climb, the ledge roll, the ledge attack, the climb
    /// jump); the scripts replay to identical hashes, and a rollback window
    /// across each get-up's start resimulates onto the authoritative hash.
    /// </summary>
    [TestCase]
    public void EveryGetUpConvergesUnderRollback() {
        foreach (GameplayButtons option in new[] {
            GameplayButtons.None, GameplayButtons.Roll, GameplayButtons.BasicAttack, GameplayButtons.Jump
        }) {
            List<PlayerInputFrame> script = PlayerTwoGetUpScript(option, out int getUpTick);
            var authoritative = new FighterSimulation(seed: 5201, rules: FighterMatchRules.Disabled, stageGeometry: Stage);
            var replay = new FighterSimulation(seed: 5201, rules: FighterMatchRules.Disabled, stageGeometry: Stage);
            var predicted = new FighterSimulation(seed: 5201, rules: FighterMatchRules.Disabled, stageGeometry: Stage);
            int predictFrom = getUpTick - 3;
            int predictTo = getUpTick + 6;
            bool left = false;
            for (int t = 0; t < script.Count; t++) {
                long hash = authoritative.Advance(Neutral(t), script[t]);
                authoritative.TryGetFighterRuntime(1, out FighterRuntimeComponent now);
                if (t >= getUpTick && !FighterLedgeRules.IsHanging(in now)) left = true;
                AssertThat(replay.Advance(Neutral(t), script[t])).IsEqual(hash);
                if (t >= predictFrom && t < predictTo) predicted.AdvanceWithPredictedPlayerTwo(Neutral(t));
                else predicted.Advance(Neutral(t), script[t]);
                if (t != predictTo) continue;
                for (int corrected = predictFrom; corrected < predictTo; corrected++) {
                    AssertThat(predicted.CorrectPlayerTwoInput(corrected, script[corrected])).IsTrue();
                }
                AssertThat(predicted.CurrentHash)
                    .OverrideFailureMessage($"{option}: the rollback window did not converge.")
                    .IsEqual(authoritative.CurrentHash);
            }
            AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
            AssertThat(left).OverrideFailureMessage($"{option}: never left the ledge.").IsTrue();
        }
    }

    // === helpers ===

    /// <summary>Where every get-up stands player one: 0.5 u inside the platform's left end.</summary>
    private static FPVector2 Stand() {
        AssertThat(Stage.TryGetStandPosition(0, out FPVector2 stand)).IsTrue();
        AssertThat(stand.x.RawValue).IsEqual((-HalfWidth + FP64.FromDouble(0.5)).RawValue);
        AssertThat(stand.y.RawValue).IsEqual(Surface.RawValue);
        return stand;
    }

    /// <summary>
    /// Both fighters jump onto the platform; player one walks off its left end
    /// (stick away from the stage) until it hangs at anchor 0. Returns the next
    /// tick; the grab tick's stick was away from the stage.
    /// </summary>
    private static FighterSimulation Hanging(out int tick) {
        var simulation = new FighterSimulation(seed: 5200, rules: FighterMatchRules.Disabled, stageGeometry: Stage);
        tick = 0;
        for (int step = 0; step < 120 && !(tick > 2 && OnTop(simulation, 0) && OnTop(simulation, 1)); step++, tick++) {
            simulation.Advance(HeldJump(tick), HeldJump(tick));
        }
        AssertThat(OnTop(simulation, 0) && OnTop(simulation, 1))
            .OverrideFailureMessage("the fighters never stood on the platform.")
            .IsTrue();
        for (int step = 0; step < 200 && !FighterLedgeRules.IsHanging(Runtime(simulation)); step++, tick++) {
            simulation.Advance(PlayerInputFrame.Create((uint)tick, -1f, 0f, GameplayButtons.None), Neutral(tick));
        }
        AssertThat(Runtime(simulation).LedgeAnchor).OverrideFailureMessage("player one never hung at anchor 0.").IsEqual(0);
        return simulation;
    }

    /// <summary>
    /// <see cref="Hanging"/>, then player two walks to <paramref name="opponentX"/>
    /// (if given) while player one hangs with a neutral stick until the first
    /// grab's intangibility has run out (the stick re-armed, the hang 35+ frames old).
    /// </summary>
    private static FighterSimulation HangingReady(out int tick, FP64? opponentX = null) {
        FighterSimulation simulation = Hanging(out tick);
        for (int step = 0; step < 200; step++, tick++) {
            FighterStateComponent two = Fighter(simulation, 1);
            float moveTwo = 0f;
            if (opponentX.HasValue) {
                FP64 dx = opponentX.Value - two.Position.x;
                moveTwo = dx < FP64.FromDouble(-0.15) ? -1f : 0f;
            }
            bool settled = (!opponentX.HasValue || moveTwo == 0f && FP64.Abs(two.Velocity.x) < FP64.FromDouble(0.01))
                && Runtime(simulation).LedgeStateFrames >= 35;
            if (settled) break;
            simulation.Advance(PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None),
                PlayerInputFrame.Create((uint)tick, moveTwo, 0f, GameplayButtons.None));
        }
        AssertThat(FighterLedgeRules.IsHanging(Runtime(simulation))).IsTrue();
        AssertThat(Fighter(simulation).InvulnerabilityFrames).IsEqual(0);
        if (opponentX.HasValue) {
            AssertThat(FP64.Abs(Fighter(simulation, 1).Position.x - opponentX.Value) < FP64.FromDouble(0.4))
                .OverrideFailureMessage("player two never reached its spot at the edge.")
                .IsTrue();
            AssertThat(OnTop(simulation, 1)).IsTrue();
        }
        return simulation;
    }

    /// <summary>
    /// The ticks a pressed grounded roll occupies the universal-movement slot,
    /// from its press tick to its last (player one standing on the base floor).
    /// </summary>
    private static int PressedRollTicks() {
        var simulation = new FighterSimulation(seed: 5202, rules: FighterMatchRules.Disabled, stageGeometry: Stage);
        int tick = 0;
        for (; tick < 10; tick++) simulation.Advance(Neutral(tick), Neutral(tick));
        simulation.Advance(PlayerInputFrame.Create((uint)tick, 1f, 0f, GameplayButtons.Roll), Neutral(tick));
        tick++;
        AssertThat(Runtime(simulation).UniversalMovementState).IsEqual((int)UniversalMovementPhase.RollStartup);
        int ticks = 1;
        for (; ticks < 60 && Runtime(simulation).UniversalMovementState != (int)UniversalMovementPhase.None; ticks++, tick++) {
            simulation.Advance(Neutral(tick), Neutral(tick));
        }
        return ticks;
    }

    /// <summary>One tick of a fresh tilt (player one), returning the next tick.</summary>
    private static int Tilt(FighterSimulation simulation, int tick, sbyte direction) =>
        Step(simulation, tick, PlayerInputFrame.Create((uint)tick, direction / 127f, 0f, GameplayButtons.None));

    private static int Step(FighterSimulation simulation, int tick, PlayerInputFrame one) {
        simulation.Advance(one, Neutral(tick));
        return tick + 1;
    }

    /// <summary>
    /// Player two's script: onto the platform, off its right end (anchor 1, the
    /// stage to its left), a neutral hang past its first 35 frames, then the
    /// option on <paramref name="getUpTick"/> (None = the neutral climb's
    /// toward-stage tilt) and 60 neutral ticks.
    /// </summary>
    private static List<PlayerInputFrame> PlayerTwoGetUpScript(GameplayButtons option, out int getUpTick) {
        var scratch = new FighterSimulation(seed: 5201, rules: FighterMatchRules.Disabled, stageGeometry: Stage);
        var script = new List<PlayerInputFrame>();
        int tick = 0;
        for (; tick < 120 && !(tick > 2 && OnTop(scratch, 1)); tick++) {
            script.Add(HeldJump(tick));
            scratch.Advance(Neutral(tick), script[tick]);
        }
        AssertThat(OnTop(scratch, 1)).IsTrue();
        for (int step = 0; step < 200; step++, tick++) {
            scratch.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime);
            if (FighterLedgeRules.IsHanging(in runtime)) break;
            script.Add(PlayerInputFrame.Create((uint)tick, 1f, 0f, GameplayButtons.None));
            scratch.Advance(Neutral(tick), script[tick]);
        }
        scratch.TryGetFighterRuntime(1, out FighterRuntimeComponent hang);
        AssertThat(hang.LedgeAnchor).IsEqual(1);
        for (int step = 0; step < 40; step++, tick++) {
            script.Add(PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None));
            scratch.Advance(Neutral(tick), script[tick]);
        }
        getUpTick = tick;
        // The stage is to the left of a side-1 anchor.
        script.Add(option == GameplayButtons.None
            ? PlayerInputFrame.Create((uint)tick, -1f, 0f, GameplayButtons.None)
            : PlayerInputFrame.Create((uint)tick, 0f, 0f, option));
        tick++;
        for (int step = 0; step < 60; step++, tick++) {
            script.Add(PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None,
                step == 0 ? option : GameplayButtons.None));
        }
        return script;
    }

    /// <summary>
    /// A plain basic from player two on player one through the hit pipeline,
    /// against copies of the live components (the simulation is untouched).
    /// </summary>
    private static bool ProbeHitLands(FighterSimulation simulation) {
        simulation.TryGetFighter(1, out FighterStateComponent attacker);
        simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent attackerRuntime);
        simulation.TryGetFighterVerb(1, out FighterVerbComponent attackerVerb);
        simulation.TryGetFighter(0, out FighterStateComponent target);
        simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent targetRuntime);
        simulation.TryGetFighterVerb(0, out FighterVerbComponent targetVerb);
        simulation.TryGetFighterDefense(0, out FighterDefenseComponent targetDefense);
        targetRuntime.HeldButtons = 0;
        FighterTuningComponent targetTuning = default;
        return FighterDamageRules.ApplyFighterHit(
            ref attacker, ref attackerRuntime, ref attackerVerb,
            ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
            FighterDamageRules.BasicAttackClass, 5, FP64.One, 12,
            (int)StatusType.None, 0, FP64.One, attacker.Position.x);
    }

    private static bool OnTop(FighterSimulation simulation, int playerID) {
        FighterStateComponent fighter = Fighter(simulation, playerID);
        return fighter.IsGrounded != 0 && fighter.Position.y.RawValue == Surface.RawValue;
    }

    private static FighterStateComponent Fighter(FighterSimulation simulation, int playerID = 0) {
        AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent fighter)).IsTrue();
        return fighter;
    }

    private static FighterRuntimeComponent Runtime(FighterSimulation simulation, int playerID = 0) {
        AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
        return runtime;
    }

    private static FighterVerbComponent Verb(FighterSimulation simulation, int playerID = 0) {
        AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
        return verb;
    }

    private static PlayerInputFrame HeldJump(int tick) => new() {
        Tick = (uint)tick,
        Held = GameplayButtons.Jump,
        Pressed = tick == 0 ? GameplayButtons.Jump : GameplayButtons.None
    };

    private static PlayerInputFrame Neutral(int tick) => new() { Tick = (uint)tick };
}
