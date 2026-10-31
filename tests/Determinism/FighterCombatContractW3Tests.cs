using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 12 W3, the Fighter-side pins: M06 (the Block escape never fires out
/// of tumble), the guard-break push (away from the attacker, unscaled,
/// assigned), block-cancel eligibility after a Special (since decision 1.2,
/// 2026-10-16, only from the cast's recovery frame 0), M01 as amended by the 2026-10-08 air model (fast-fall ramps
/// to a 15 u/s cap, normal falls cap at 12, launches keep the 20 u/s clamp),
/// and the M09 respawn state.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterCombatContractW3Tests {
    private const int BlockButton = 1 << 6;

    [TestCase]
    public void TheBlockEscapeGateRefusesTumbleInBothModes() {
        // The one shared rule Story's ProcessStunned and the sim's movement
        // system both call (M06): grounded, Block held, NOT tumbling, past the
        // hit-1 gate, and a stance that can actually rise.
        AssertThat(BasicComboRules.CanBlockEscapeHitstun(true, true, false, false, true)).IsTrue();
        AssertThat(BasicComboRules.CanBlockEscapeHitstun(true, true, true, false, true))
            .OverrideFailureMessage("M06: a tumbling victim techs on ground contact and never escapes into the stance.")
            .IsFalse();
        AssertThat(BasicComboRules.CanBlockEscapeHitstun(false, true, false, false, true)).IsFalse();
        AssertThat(BasicComboRules.CanBlockEscapeHitstun(true, false, false, false, true)).IsFalse();
        AssertThat(BasicComboRules.CanBlockEscapeHitstun(true, true, false, true, true)).IsFalse();
        AssertThat(BasicComboRules.CanBlockEscapeHitstun(true, true, false, false, false)).IsFalse();
    }

    [TestCase]
    public void ALaunchedVictimThatLandsWithoutTheTechCannotBlockOutOfTheKnockdown() {
        // Rewritten in place by Package 12 W3b (M05). A tumble that lands
        // WITHOUT Block held is a missed tech, and since M05 that landing is a
        // knockdown rather than a grounded tumble — so the M06 question becomes
        // "can Block held on the floor escape the knockdown into the stance?".
        // Lincoln's hit 2 is the launcher (M07); the victim then holds Block on
        // every grounded tick of the knockdown.
        var simulation = new FighterSimulation(
            FighterCharacterID.Lincoln, FighterCharacterID.Joan,
            spawnDistance: 1, rules: FighterMatchRules.Disabled);
        // Walk in: the 2-unit spawn gap is beyond Lincoln's 85 % hit 1/2 reach.
        int tick = 0;
        for (; tick < 60; tick++) {
            simulation.TryGetFighter(0, out FighterStateComponent a);
            simulation.TryGetFighter(1, out FighterStateComponent v);
            bool close = v.Position.x - a.Position.x <= FP64.FromDouble(1.2);
            simulation.Advance(Frame(tick, GameplayButtons.None, close ? (sbyte)0 : (sbyte)127), Frame(tick, GameplayButtons.None));
            if (close) break;
        }
        for (int settle = 0; settle < 20; settle++, tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }

        bool launched = false;
        bool sawKnockdown = false;
        bool stanceDuringKnockdown = false;
        for (int step = 0; step < 240; step++, tick++) {
            simulation.TryGetFighter(1, out FighterStateComponent victim);
            simulation.TryGetFighterVerb(1, out FighterVerbComponent verb);
            simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent knockdown);
            if (verb.Tumble != 0 && victim.IsGrounded == 0) launched = true;
            // Stop attacking once the launch landed, so no later hit muddies it.
            GameplayButtons attacker = !launched && step % 2 == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None;
            bool down = FighterKnockdownRules.IsActive(in knockdown);
            GameplayButtons defender = down ? GameplayButtons.Block : GameplayButtons.None;
            simulation.Advance(Frame(tick, attacker), Frame(tick, defender));
            simulation.TryGetFighter(1, out FighterStateComponent after);
            simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent afterRuntime);
            simulation.TryGetFighterVerb(1, out FighterVerbComponent afterVerb);
            simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent afterKnockdown);
            if (FighterKnockdownRules.IsActive(in afterKnockdown)) {
                sawKnockdown = true;
                if (FighterBasicAttackRules.IsBlockStance(in after, in afterRuntime, in afterVerb)) {
                    stanceDuringKnockdown = true;
                }
            }
            if (sawKnockdown && !FighterKnockdownRules.IsActive(in afterKnockdown)) break;
        }
        AssertThat(launched)
            .OverrideFailureMessage("M07: Lincoln's hit 2 must launch the victim into a tumble.")
            .IsTrue();
        AssertThat(sawKnockdown)
            .OverrideFailureMessage("M05: a tumble landing without the tech must be a knockdown.")
            .IsTrue();
        AssertThat(stanceDuringKnockdown)
            .OverrideFailureMessage("M06/M05: Block held on the floor must not raise the stance out of the knockdown.")
            .IsFalse();
    }

    [TestCase]
    public void TheShatterPushIsAwayFromTheAttackerUnscaledAndAssigned() {
        // Attacker on the RIGHT of a right-facing blocker: push toward -X.
        AssertShatterPush(defenderFacingRight: true, attackerOffset: 1, expectedSign: -1, weight: 0.7);
        // Attacker on the LEFT of a left-facing blocker: push toward +X, and a
        // heavy low-HP victim gets exactly the same vector (unscaled).
        AssertShatterPush(defenderFacingRight: false, attackerOffset: -1, expectedSign: 1, weight: 1.6, lowHp: true);
        // Coincident: backward from facing.
        AssertThat(FighterDamageRules.GuardBreakPushSign(FP64.Zero, FP64.Zero, true)).IsEqual(-1);
        AssertThat(BasicComboRules.GuardBreakPushSign(0f, 0f, false)).IsEqual(1);
    }

    [TestCase]
    public void BlockCancelsASpecialOnlyFromItsRecoveryFrameZero() {
        // Rewritten in place for decision 1.2 (2026-10-16): a Fighter Special
        // now has its authored startup, active and recovery, so Block cancels
        // it only from recovery frame 0 — parity with Story, where Block
        // cancels a Special's recovery frames. Production Joan, Righteous Smite
        // (12 / 6 / 20); the opponent stands out of the wave's 2.5 units, so no
        // hitstop moves the clock. Block is held from the tick after the press.
        CharacterData joan = AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres");
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(joan), FighterLoadout.Default(FighterCharacterID.Joan),
            spawnDistance: 8, rules: FighterMatchRules.Disabled);
        int startup = joan.SpecialAttackOne.StartupFrames;
        int active = joan.SpecialAttackOne.ActiveFrames;
        AssertThat(startup).IsEqual(12);
        AssertThat(active).IsEqual(6);
        int tick = 0;
        for (; tick < 5; tick++) simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        int press = tick;
        simulation.Advance(Frame(tick, GameplayButtons.Special1), Frame(tick, GameplayButtons.None));
        tick++;
        simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent cast);
        AssertThat(cast.SpecialOneReuseFrames > 0)
            .OverrideFailureMessage("The special must have been accepted (cooldown armed).")
            .IsTrue();

        int raisedOn = -1;
        for (; tick < press + 60 && raisedOn < 0; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.Block), Frame(tick, GameplayButtons.None));
            simulation.TryGetFighter(0, out FighterStateComponent fighter);
            simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime);
            simulation.TryGetFighterVerb(0, out FighterVerbComponent verb);
            simulation.TryGetFighterActionState(0, out FighterActionStateComponent action);
            if (FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb)) {
                raisedOn = tick;
                AssertThat(FighterSpecialCastRules.IsBusy(in action))
                    .OverrideFailureMessage("The Block cancel ends the cast.")
                    .IsFalse();
            } else {
                AssertThat(FighterSpecialCastRules.IsStartup(in action) || FighterSpecialCastRules.IsActive(in action))
                    .OverrideFailureMessage($"Tick {tick}: the stance must rise on the first recovery tick.")
                    .IsTrue();
            }
        }
        // Startup T..T+11, active T+12..T+17, recovery frame 0 on T+18.
        AssertThat(raisedOn)
            .OverrideFailureMessage("Holding Block through a Special raises the stance on recovery frame 0.")
            .IsEqual(press + startup + active);
    }

    /// <summary>
    /// Rewritten in place for the 2026-10-08 air model (user direction): M01's
    /// snap to a shared 20 u/s terminal is gone. Fast-fall ramps in under
    /// <c>FastFallGravityMultiplier</c> toward a 15 u/s cap, a normal-control
    /// fall is capped at 12 u/s, a fall already past its cap eases down by one
    /// base-gravity step a tick, and only hitstun and daze keep the 20 u/s
    /// launch clamp.
    /// </summary>
    [TestCase]
    public void FastFallRampsToItsFifteenUnitCapAndTheFallCapsAreTwelveAndTwenty() {
        AssertThat(UniversalMovementRules.FastFallSpeed).IsEqual(15f);
        AssertThat(UniversalMovementRules.TerminalFallSpeed).IsEqual(12f);
        AssertThat(UniversalMovementRules.LaunchTerminalFallSpeed).IsEqual(20f);
        AssertThat(UniversalMovementRules.FastFallGravityMultiplier).IsEqual(3f);
        FP64 baseStep = FP64.FromInt(-30) * (FP64.One / FP64.FromInt(60));
        FP64 fastFallStep = baseStep * FP64.FromDouble(UniversalMovementRules.FastFallGravityMultiplier);
        FP64 terminal = -FP64.FromInt(12);
        FP64 fastFallCap = -FP64.FromInt(15);
        FP64 launchClamp = -FP64.FromInt(20);

        // The launch clamp: hitstun and daze, flat gravity, 20 u/s.
        AssertThat(FighterAirPhysics.StepLaunch(-FP64.FromInt(35)).RawValue).IsEqual(launchClamp.RawValue);
        AssertThat(FighterAirPhysics.StepLaunch(-FP64.FromInt(5)).RawValue).IsEqual((-FP64.FromInt(5) + baseStep).RawValue);
        AssertThat(FighterAirPhysics.StepLaunch(FP64.FromInt(9)).RawValue).IsEqual((FP64.FromInt(9) + baseStep).RawValue);
        // Normal control: a fall reaching 12 stops on it; one already faster eases.
        AssertThat(FighterAirPhysics.StepMovement(-FP64.FromDouble(11.8), false, false, false).RawValue)
            .IsEqual(terminal.RawValue);
        AssertThat(FighterAirPhysics.StepMovement(-FP64.FromInt(15), false, false, false).RawValue)
            .IsEqual((-FP64.FromInt(15) - baseStep).RawValue);
        AssertThat(FighterAirPhysics.StepMovement(-FP64.FromInt(20), false, false, false).RawValue)
            .IsEqual((-FP64.FromInt(20) - baseStep).RawValue);
        // Fast-falling: the cap is 15, reached and held.
        AssertThat(FighterAirPhysics.StepMovement(-FP64.FromDouble(14.9), true, false, false).RawValue)
            .IsEqual(fastFallCap.RawValue);
        AssertThat(FighterAirPhysics.StepMovement(fastFallCap, true, false, false).RawValue)
            .IsEqual(fastFallCap.RawValue);

        // Live: jump, then hold Down from tick 4 — the descent ramps by one
        // fast-fall step a tick and never passes 15, where it used to read
        // exactly -20 on every tick.
        var simulation = new FighterSimulation(spawnDistance: 8, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.Jump), Frame(0, GameplayButtons.None));
        int tick = 1;
        for (; tick < 4; tick++) simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        simulation.TryGetFighter(0, out FighterStateComponent rising);
        FP64 previous = rising.Velocity.y;
        int airborneTicks = 0;
        for (; tick < 60; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.Down), Frame(tick, GameplayButtons.None));
            simulation.TryGetFighter(0, out FighterStateComponent fighter);
            if (fighter.IsGrounded != 0) break;
            airborneTicks++;
            FP64 expected = previous + fastFallStep < fastFallCap ? fastFallCap : previous + fastFallStep;
            AssertThat(fighter.Velocity.y.RawValue)
                .OverrideFailureMessage("Fast-fall must ramp by one heavier-gravity step a tick, capped at 15.")
                .IsEqual(expected.RawValue);
            previous = fighter.Velocity.y;
        }
        AssertThat(airborneTicks > 1).IsTrue();
    }

    [TestCase]
    public void AStockLossRespawnsAtFullHPWithBuffsAndMarksClearedButCooldownsAndDefyKept() {
        var fighter = new FighterStateComponent {
            PlayerID = 1, Stocks = 3, MaxHP = 100, CurrentHP = 0, BlockCharges = 0,
            Influence = FP64.FromInt(80)
        };
        var runtime = new FighterRuntimeComponent {
            UsesStocks = 1,
            ZoneSpeedBonusFrames = 40,
            FloatFrames = 12,
            SpecialOneReuseFrames = 300,
            SpecialTwoReuseFrames = 200,
            MovementCooldownFrames = 90,
            StatusType = (int)StatusType.Root,
            StatusFrames = 30
        };
        var verb = new FighterVerbComponent {
            DefyHistoryUsed = 1,
            EchoPool = FP64.FromInt(5)
        };
        FighterDefenseComponent defense = default;
        var tuning = new FighterTuningComponent { MaxBlockCharges = 3, MaxJumpCount = 2 };

        FighterSimulationRules.ApplyStockLoss(ref fighter, ref runtime, ref verb, ref defense, in tuning);

        AssertThat(fighter.CurrentHP).IsEqual(100);
        AssertThat(fighter.BlockCharges).IsEqual(3);
        AssertThat(fighter.Influence.RawValue).IsEqual(FP64.FromInt(60).RawValue);
        AssertThat(runtime.ZoneSpeedBonusFrames).OverrideFailureMessage("M09: the zone speed buff must clear.").IsEqual(0);
        AssertThat(runtime.FloatFrames).OverrideFailureMessage("M09: the float window must clear.").IsEqual(0);
        AssertThat(runtime.StatusType).IsEqual((int)StatusType.None);
        AssertThat(verb.EchoPool.RawValue).IsEqual(FP64.Zero.RawValue);
        AssertThat(runtime.SpecialOneReuseFrames).OverrideFailureMessage("Cooldowns keep their time.").IsEqual(300);
        AssertThat(runtime.SpecialTwoReuseFrames).IsEqual(200);
        AssertThat(runtime.MovementCooldownFrames).IsEqual(90);
        AssertThat(verb.DefyHistoryUsed).OverrideFailureMessage("Defy is once per match.").IsEqual(1);
        AssertThat(fighter.RespawnFramesRemaining > 0).IsTrue();

        // The victim's Conductive mark (component 318) is dropped while on the
        // respawn platform; a live fighter keeps its mark.
        var mark = new FighterConductiveComponent { FramesRemaining = 50, SourcePlayerID = 0 };
        FighterCombatSystem.ClearMarkIfRespawning(in fighter, ref mark);
        AssertThat(mark.FramesRemaining).IsEqual(0);
        AssertThat(mark.SourcePlayerID).IsEqual(-1);
        var liveMark = new FighterConductiveComponent { FramesRemaining = 50, SourcePlayerID = 0 };
        var live = new FighterStateComponent { Stocks = 2, CurrentHP = 50, MaxHP = 100 };
        FighterCombatSystem.ClearMarkIfRespawning(in live, ref liveMark);
        AssertThat(liveMark.FramesRemaining).IsEqual(50);
    }

    private static void AssertShatterPush(bool defenderFacingRight, int attackerOffset, int expectedSign,
        double weight, bool lowHp = false) {
        FighterStateComponent attacker = new() { PlayerID = 0, Stocks = 3, MaxHP = 100, CurrentHP = 100, IsGrounded = 1 };
        FighterRuntimeComponent attackerRuntime = default;
        FighterVerbComponent attackerVerb = default;
        FighterStateComponent target = new() {
            PlayerID = 1, Stocks = 3, MaxHP = 100, CurrentHP = lowHp ? 10 : 100, BlockCharges = 3,
            IsGrounded = 1, FacingRight = defenderFacingRight ? 1 : 0,
            Weight = FP64.FromDouble(weight),
            // Prior motion that an ADDED push would keep.
            Velocity = new xpTURN.Klotho.Deterministic.Math.FPVector2(FP64.FromInt(7), FP64.FromInt(-3))
        };
        FighterRuntimeComponent targetRuntime = new() { HeldButtons = BlockButton };
        FighterVerbComponent targetVerb = default;
        FighterDefenseComponent targetDefense = default;
        FighterTuningComponent tuning = default;
        AssertThat(FighterBasicAttackRules.IsBlockStance(in target, in targetRuntime, in targetVerb)).IsTrue();

        FighterDamageRules.ApplyFighterHit(
            ref attacker, ref attackerRuntime, ref attackerVerb,
            ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in tuning,
            FighterDamageRules.SpecialAttackClass,
            20, FP64.FromInt(6), 18,
            (int)StatusType.None, 0, FP64.One,
            target.Position.x + FP64.FromInt(attackerOffset),
            // A01 (Package 13 W1): only a Shield-Breaker shatters a full shield.
            shieldBreaker: true);

        AssertThat(target.BlockCharges).IsEqual(0);
        AssertThat(target.DazeFrames > 0).IsTrue();
        AssertThat(target.Velocity.x.RawValue)
            .OverrideFailureMessage("The shatter push X must be exactly ±2 away from the attacker.")
            .IsEqual((FP64.FromInt(2) * FP64.FromInt(expectedSign)).RawValue);
        AssertThat(target.Velocity.y.RawValue)
            .OverrideFailureMessage("The shatter push Y must be exactly +1 (Y-up) — the Y-down -1.0 hop.")
            .IsEqual(FP64.One.RawValue);
    }

    private static PlayerInputFrame Frame(int tick, GameplayButtons buttons, sbyte moveX = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = buttons,
        Pressed = buttons
    };
}
