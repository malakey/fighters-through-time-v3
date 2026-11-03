using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The hit-confirm cancel (D1), the landed-contact definition (D1a), per-hit
/// Momentum (D2) and the freeze-only Special buffer (user rulings 2026-10-11),
/// Fighter side. A swing that LANDS (damage applied, or absorbed by Temporal
/// Aegis or a barrier) releases Special 1 and Special 2 from its own 30-frame
/// lockout while the Ultimate keeps it; a whiff, a block, a refusal and a
/// construct hit do not. A Special accepted inside the last 30 frames keeps the
/// other Special refused (the Special-sourced lockout S, paused by hitstop like
/// the lockout A). Every landed swing refunds both running Special cooldowns
/// once — 20 / 20 / 60 frames, Up-Attack and Down-Air 20 — capped at 120
/// frames per slot per cycle. A Special press made inside the freeze of a
/// landed swing fires on the thaw tick if it is legal then.
///
/// <para>Pure C#: engine-free loadouts whose Specials are slow Projectile
/// executions with a 1-frame slot cooldown, so a spawned shot is the proof of
/// acceptance and only the lockout can refuse a re-press. Timings are measured
/// (landing tick, freeze, unfrozen ticks), never hard-coded.</para>
/// </summary>
[TestSuite]
public class FighterHitConfirmTests {

    private const int Lockout = BasicComboRules.AbilityLockoutFrames;
    private const sbyte UpAxis = -127;
    private const int LongCooldown = 900;

    // === D1: the hit-confirm cancel ===

    [TestCase]
    public void ALandedSwingReleasesBothSpecialsAndKeepsTheUltimateLocked() {
        foreach ((GameplayButtons press, int slot) in new[] { (GameplayButtons.Special1, 1), (GameplayButtons.Special2, 2) }) {
            FighterSimulation simulation = NewSim(seed: 301 + slot);
            Approach(simulation);
            int maxHP = Fighter(simulation, 1).CurrentHP;
            Step(simulation, GameplayButtons.BasicAttack);
            AssertThat(Runtime(simulation, 0).SwingLanded).IsEqual(0);

            int landingStep = -1;
            int freeze = 0;
            int acceptedStep = -1;
            for (int step = 1; step <= 60 && acceptedStep < 0; step++) {
                int lockoutBefore = Lock(simulation);
                bool acceptedNow = StepAccepts(simulation, press, slot);
                if (landingStep < 0 && Fighter(simulation, 1).CurrentHP < maxHP) {
                    landingStep = step;
                    freeze = Verb(simulation, 0).HitstopFrames;
                    AssertThat(Runtime(simulation, 0).SwingLanded).IsEqual(1);
                }
                if (acceptedNow) {
                    acceptedStep = step;
                    AssertThat(lockoutBefore > 0)
                        .OverrideFailureMessage($"Special {slot} must be accepted while the swing's lockout still runs.")
                        .IsTrue();
                }
            }
            AssertThat(landingStep > 0).OverrideFailureMessage("Harness: the opener never landed.").IsTrue();
            AssertThat(freeze > 0).OverrideFailureMessage("Harness: the landing must freeze the attacker.").IsTrue();
            AssertThat(acceptedStep)
                .OverrideFailureMessage($"Special {slot} must be accepted on the landing's last freeze tick (L+F).")
                .IsEqual(landingStep + freeze);
        }

        // The Ultimate: refused, the meter untouched, for as long as the lockout
        // runs — the landing does not release it.
        FighterSimulation ultimate = NewSim(seed: 305);
        AssertThat(ultimate.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        Approach(ultimate);
        int targetHP = Fighter(ultimate, 1).CurrentHP;
        Step(ultimate, GameplayButtons.BasicAttack);
        int landed = -1;
        int thaw = -1;
        bool refusedAfterTheConfirm = false;
        bool accepted = false;
        for (int step = 1; step <= 90 && !accepted; step++) {
            int lockoutBefore = Lock(ultimate);
            Step(ultimate, GameplayButtons.Ultimate);
            if (landed < 0 && Fighter(ultimate, 1).CurrentHP < targetHP) {
                landed = step;
                thaw = step + Verb(ultimate, 0).HitstopFrames;
            }
            if (UltimatePhase(ultimate, 0) != FighterUltimateActivationRules.PhaseNone) {
                accepted = true;
                AssertThat(lockoutBefore <= 1)
                    .OverrideFailureMessage("The Ultimate is accepted only once the lockout runs out.")
                    .IsTrue();
                break;
            }
            AssertThat(Fighter(ultimate, 0).Influence).IsEqual(FP64.FromInt(100));
            AssertThat(Lock(ultimate) > 0)
                .OverrideFailureMessage($"Step {step}: an Ultimate refused with the lockout clear.")
                .IsTrue();
            if (thaw > 0 && step >= thaw) refusedAfterTheConfirm = true;
        }
        AssertThat(landed > 0).IsTrue();
        AssertThat(accepted).IsTrue();
        AssertThat(refusedAfterTheConfirm)
            .OverrideFailureMessage("The Ultimate must still be refused where a Special would be confirmed.")
            .IsTrue();
    }

    [TestCase]
    public void AWhiffedSwingAndAnUnlandedFollowUpSwingStillLockSpecials() {
        // A whiff at four units: Special 1 waits out the whole lockout.
        FighterSimulation whiff = NewSim(seed: 311, spawnDistance: 4);
        Step(whiff, GameplayButtons.BasicAttack);
        for (int step = 1; step < Lockout; step++) {
            Step(whiff, GameplayButtons.Special1);
            AssertThat(Shots(whiff, 0, 1))
                .OverrideFailureMessage($"Special 1 on T+{step} after a whiff must be refused.")
                .IsEqual(0);
            AssertThat(Runtime(whiff, 0).SwingLanded).IsEqual(0);
        }
        Step(whiff, GameplayButtons.Special1);
        AssertThat(Shots(whiff, 0, 1)).OverrideFailureMessage("Special 1 is legal on T+30.").IsEqual(1);

        // A landed opener, then hit 2 starts: the new swing re-locks Specials
        // through its startup (A = 30, C = 0) until it lands in turn.
        FighterSimulation chain = NewSim(seed: 312, basicKnockback: FP64.Zero);
        Approach(chain);
        int maxHP = Fighter(chain, 1).CurrentHP;
        Step(chain, GameplayButtons.BasicAttack);
        bool buffered = false;
        bool secondStarted = false;
        for (int step = 0; step < 90 && !secondStarted; step++) {
            bool pressNext = !buffered && Fighter(chain, 1).CurrentHP < maxHP;
            buffered |= pressNext;
            Step(chain, pressNext ? GameplayButtons.BasicAttack : GameplayButtons.None);
            FighterRuntimeComponent runtime = Runtime(chain, 0);
            if (runtime.ComboIndex == 1 && runtime.AttackPhase == FighterBasicAttackRules.PhaseStartup) {
                secondStarted = true;
            }
        }
        AssertThat(buffered).OverrideFailureMessage("Harness: the opener never landed.").IsTrue();
        AssertThat(secondStarted).OverrideFailureMessage("Harness: hit 2 never started.").IsTrue();
        AssertThat(Runtime(chain, 0).SwingLanded).IsEqual(0);
        AssertThat(Lock(chain)).IsEqual(Lockout);
        int probes = 0;
        while (Runtime(chain, 0).AttackPhase == FighterBasicAttackRules.PhaseStartup && probes < 20) {
            AssertThat(StepAccepts(chain, GameplayButtons.Special1, 1))
                .OverrideFailureMessage("Hit 2's startup re-locks the Specials.")
                .IsFalse();
            probes++;
        }
        AssertThat(probes > 0).IsTrue();
    }

    [TestCase]
    public void ASpecialWithinThirtyTicksKeepsSpecialsLockedWhateverLands() {
        FighterSimulation simulation = NewSim(seed: 321);
        Approach(simulation);
        int maxHP = Fighter(simulation, 1).CurrentHP;
        // Turn away with a one-tick tap, then cast Special 1 (a slow shot) away
        // from the target on T.
        Step(simulation, GameplayButtons.None, moveX: -127);
        Step(simulation, GameplayButtons.Special1);
        AssertThat(Shots(simulation, 0, 1)).IsEqual(1);
        AssertThat(Fighter(simulation, 0).FacingRight).IsEqual(0);

        int unfrozen = 0;
        int landingStep = -1;
        int freezeTicks = 0;
        int acceptedStep = -1;
        for (int step = 1; step <= 120 && acceptedStep < 0; step++) {
            GameplayButtons press = step == 20 ? GameplayButtons.BasicAttack
                : step > 20 ? GameplayButtons.Special2
                : GameplayButtons.None;
            sbyte moveX = step == 19 ? (sbyte)127 : (sbyte)0;
            bool frozenAtStart = Verb(simulation, 0).HitstopFrames > 0;
            if (!frozenAtStart) unfrozen++;
            else freezeTicks++;
            bool accepted = StepAccepts(simulation, press, 2, moveX);
            if (landingStep < 0 && Fighter(simulation, 1).CurrentHP < maxHP) landingStep = step;
            if (landingStep < 0) {
                AssertThat(frozenAtStart)
                    .OverrideFailureMessage("Harness: Special 1's shot must never connect and freeze its caster.")
                    .IsFalse();
            }
            if (accepted) {
                acceptedStep = step;
                AssertThat(unfrozen)
                    .OverrideFailureMessage("Special 2 must be accepted on the 30th unfrozen tick after Special 1.")
                    .IsEqual(Lockout);
            } else if (step > 20) {
                AssertThat(unfrozen < Lockout)
                    .OverrideFailureMessage($"Special 2 refused on the {unfrozen}th unfrozen tick after Special 1.")
                    .IsTrue();
            }
        }
        AssertThat(landingStep > 20).OverrideFailureMessage("Harness: the T+20 swing must land.").IsTrue();
        AssertThat(Runtime(simulation, 0).SwingLanded).IsEqual(1);
        AssertThat(freezeTicks > 0).IsTrue();
        AssertThat(acceptedStep).IsEqual(Lockout + freezeTicks);
    }

    [TestCase]
    public void BlockedCounteredAndInvulnerableSwingsNeitherConfirmNorPayTheGaugeButAnAegisAbsorbedOneDoesBoth() {
        // Blocked: the target holds Block.
        SwingOutcome blocked = SwingIntoTarget(
            NewSim(seed: 331), targetHeld: GameplayButtons.Block, prepare: null);
        AssertThat(blocked.TargetBlockChargesSpent > 0).OverrideFailureMessage("Harness: the swing must be blocked.").IsTrue();
        AssertNoConfirm(blocked, "a blocked swing");

        // Countered: Tubman's Foresight catches the swing.
        SwingOutcome countered = SwingIntoTarget(
            NewSim(seed: 332, target: FighterCharacterID.Tubman), targetHeld: GameplayButtons.None, prepare: null,
            targetPressOnSwing: GameplayButtons.Special2);
        AssertThat(countered.TargetCounterTriggered).OverrideFailureMessage("Harness: Foresight must catch the swing.").IsTrue();
        AssertNoConfirm(countered, "a countered swing");

        // Refused: invulnerability frames.
        SwingOutcome invulnerable = SwingIntoTarget(
            NewSim(seed: 333), targetHeld: GameplayButtons.None,
            prepare: simulation => AssertThat(simulation.SeedFighterInvulnerabilityForTest(1, 60)).IsTrue());
        AssertNoConfirm(invulnerable, "a swing refused by invulnerability");

        // Absorbed: Temporal Aegis takes the contact — it LANDED (D1a).
        SwingOutcome aegis = SwingIntoTarget(
            NewSim(seed: 334), targetHeld: GameplayButtons.None,
            prepare: simulation => AssertThat(simulation.SeedFighterAegisForTest(1)).IsTrue());
        AssertThat(aegis.TargetHPLost).OverrideFailureMessage("Aegis absorbs the whole contact.").IsEqual(0);
        AssertThat(aegis.SwingLanded).IsEqual(1);
        AssertThat(aegis.GaugeGain).IsEqual(BasicComboRules.MomentumStringHitGainUnits);
        AssertThat(aegis.LockoutAfter > 0).IsTrue();
        AssertThat(aegis.SpecialGateAfter)
            .OverrideFailureMessage("An absorbed landing opens the Special gate while the lockout runs.")
            .IsEqual(0);
    }

    [TestCase]
    public void ASwingThatOnlyStrikesAConstructDoesNotConfirm() {
        FighterAbilityLoadout coil = ProjectileModes() with {
            SpecialOneExecutionType = 3,
            SpecialOnePersistentTypeID = 1,
            SpecialOneMaxActiveObjects = 1,
            SpecialOnePersistentLifetimeFrames = 1800
        };
        FighterSimulation simulation = NewSim(seed: 341, targetModes: coil);
        AssertThat(simulation.SeedFighterSpecialReuseForTest(0, LongCooldown, LongCooldown)).IsTrue();
        // Player two deploys its coil at its feet, then walks away from it.
        simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.None),
            Input(simulation.CurrentTick, GameplayButtons.Special1));
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent deployed)).IsTrue();
        int coilHP = deployed.CurrentHP;
        FP64 coilX = deployed.Position.x;
        for (int step = 0; step < 40; step++) {
            sbyte toward = Fighter(simulation, 0).Position.x < coilX - FP64.FromDouble(0.6) ? (sbyte)127 : (sbyte)0;
            simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.None, toward),
                Input(simulation.CurrentTick, GameplayButtons.None, 127));
        }
        Idle(simulation, 6);
        AssertThat(Fighter(simulation, 1).Position.x - Fighter(simulation, 0).Position.x > FP64.FromDouble(2.5))
            .OverrideFailureMessage("Harness: the fighter must be out of the swing's reach.")
            .IsTrue();

        int targetHP = Fighter(simulation, 1).CurrentHP;
        int unitsBeforeSwing = Verb(simulation, 0).MomentumUnits;
        Step(simulation, GameplayButtons.BasicAttack);
        for (int step = 0; step < 30; step++) Step(simulation, GameplayButtons.None);
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent struck)).IsTrue();
        AssertThat(struck.CurrentHP < coilHP).OverrideFailureMessage("Harness: the swing must strike the coil.").IsTrue();
        AssertThat(Fighter(simulation, 1).CurrentHP).IsEqual(targetHP);
        AssertThat(Runtime(simulation, 0).SwingLanded)
            .OverrideFailureMessage("A construct hit is not a landed contact (D1a).")
            .IsEqual(0);
        AssertThat(Verb(simulation, 0).MomentumUnits <= unitsBeforeSwing + 31 * FighterMomentumRules.RegenUnitsPerFrame)
            .OverrideFailureMessage("A construct hit pays the gauge nothing beyond passive regeneration.")
            .IsTrue();
    }

    [TestCase]
    public void ResolveFighterHitNamesRefusedBlockedAbsorbedAndLanded() {
        AssertThat((int)new Fixture().With(f => f.Target.InvulnerabilityFrames = 5).Resolve(8)).IsEqual((int)FighterHitOutcome.Refused);
        AssertThat((int)new Fixture().With(f => f.Target.Stocks = 0).Resolve(8)).IsEqual((int)FighterHitOutcome.Refused);

        AssertThat((int)new Fixture().With(f => f.Defense.AegisActive = 1).Resolve(8)).IsEqual((int)FighterHitOutcome.Absorbed);
        AssertThat((int)new Fixture().With(f => {
            f.Defense.BarrierPoints = 20;
            f.Defense.BarrierCapacity = 20;
        }).Resolve(8)).IsEqual((int)FighterHitOutcome.Absorbed);

        AssertThat((int)new Fixture().With(f => f.Runtime.HeldButtons = (int)GameplayButtons.Block).Resolve(8)).IsEqual((int)FighterHitOutcome.Blocked);
        AssertThat((int)new Fixture().With(f => {
            f.Runtime.HeldButtons = (int)GameplayButtons.Block;
            f.Target.BlockCharges = 1;
        }).Resolve(8)).OverrideFailureMessage("A shatter is a block, not a landing.").IsEqual((int)FighterHitOutcome.Blocked);
        AssertThat((int)new Fixture().With(f => {
            f.Runtime.HeldButtons = (int)GameplayButtons.Block;
            f.Defense.BarrierPoints = 3;
            f.Defense.BarrierCapacity = 3;
        }).Resolve(8)).OverrideFailureMessage("A partial barrier absorb followed by a block is a block.")
            .IsEqual((int)FighterHitOutcome.Blocked);

        Fixture plain = new();
        AssertThat((int)plain.Resolve(8)).IsEqual((int)FighterHitOutcome.Landed);
        AssertThat(plain.Target.CurrentHP).IsEqual(92);

        // The historical bool is exactly "Landed".
        foreach (System.Action<Fixture> setup in new System.Action<Fixture>[] {
            f => { },
            f => f.Defense.AegisActive = 1,
            f => f.Runtime.HeldButtons = (int)GameplayButtons.Block,
            f => f.Target.InvulnerabilityFrames = 5
        }) {
            Fixture viaOutcome = new Fixture().With(setup);
            Fixture viaBool = new Fixture().With(setup);
            AssertThat(viaBool.Apply(8)).IsEqual(viaOutcome.Resolve(8) == FighterHitOutcome.Landed);
        }
    }

    // === The Momentum gauge's landed-swing gains (meter and Defy update 2026-10-31) ===

    [TestCase]
    public void EveryLandedBasicHitAddsItsGaugeGainOncePerSwing() {
        // The string: 0.05, 0.05 and 0.2 pip.
        FighterSimulation simulation = NewSim(seed: 371, basicKnockback: FP64.Zero);
        AssertThat(simulation.SeedFighterSpecialReuseForTest(0, LongCooldown, LongCooldown)).IsTrue();
        Approach(simulation);
        int unitsBeforeString = Verb(simulation, 0).MomentumUnits;
        List<Landing> landings = LandString(simulation, hits: 3);
        AssertThat(landings.Count).IsEqual(3);
        for (int index = 0; index < 3; index++) {
            int expected = index == 2 ? BasicComboRules.MomentumFinisherGainUnits : BasicComboRules.MomentumStringHitGainUnits;
            AssertThat(landings[index].ComboIndex).IsEqual(index);
            AssertThat(landings[index].GaugeGain)
                .OverrideFailureMessage($"Hit {index + 1} must add {expected} units.")
                .IsEqual(expected);
        }
        AssertThat(Verb(simulation, 0).MomentumUnits > unitsBeforeString).IsTrue();

        // The Up-Attack: 360.
        FighterSimulation up = NewSim(seed: 372);
        Approach(up);
        Landing upLanding = SwingUntilLanded(up, GameplayButtons.BasicAttack, moveY: UpAxis);
        AssertThat((Runtime(up, 0).AttackFlags & FighterBasicAttackRules.FlagUpAttack) != 0
                   || upLanding.WasUpAttack).IsTrue();
        AssertThat(upLanding.GaugeGain).IsEqual(BasicComboRules.MomentumDirectionalGainUnits);

        // The Down-Air: jump over the grounded target, then Down + attack: 360.
        FighterSimulation down = NewSim(seed: 373, spawnDistance: 0);
        Step(down, GameplayButtons.Jump);
        for (int step = 0; step < 120; step++) {
            FighterStateComponent rising = Fighter(down, 0);
            if (rising.IsGrounded == 0 && rising.Velocity.y <= FP64.Zero) break;
            Step(down, GameplayButtons.None);
        }
        Landing downLanding = SwingUntilLanded(down, GameplayButtons.BasicAttack | GameplayButtons.Down,
            heldAfter: GameplayButtons.Down);
        AssertThat(downLanding.WasDownAir).OverrideFailureMessage("Harness: the swing must be the Down-Air.").IsTrue();
        AssertThat(downLanding.GaugeGain).IsEqual(BasicComboRules.MomentumDirectionalGainUnits);
    }

    [TestCase]
    public void TheGaugeStopsAtFourPipsWhateverALandedSwingAdds() {
        FighterSimulation simulation = NewSim(seed: 381, basicKnockback: FP64.Zero);
        Approach(simulation);
        AssertThat(simulation.SeedFighterMomentumForTest(0, BasicComboRules.MomentumMaxUnits - 100)).IsTrue();
        List<Landing> string_ = LandString(simulation, hits: 3);
        AssertThat(string_.Count).IsEqual(3);
        AssertThat(Verb(simulation, 0).MomentumUnits)
            .OverrideFailureMessage("A full string at 100 units under the cap clamps at 28 800.")
            .IsEqual(BasicComboRules.MomentumMaxUnits);
        Settle(simulation);
        for (int tick = 0; tick < 30; tick++) Step(simulation, GameplayButtons.None);
        AssertThat(Verb(simulation, 0).MomentumUnits)
            .OverrideFailureMessage("Passive regeneration also stops at the cap.")
            .IsEqual(BasicComboRules.MomentumMaxUnits);
    }

    // === §1.7: a meter-paid Ultimate held back by the lockout ===

    [TestCase]
    public void AMeterPaidUltimateHeldBackByTheLockoutTakesTheTickFromTheSpecials() {
        FighterSimulation held = ConfirmedSwing(seed: 391, meter: 100);
        AssertThat(StepAccepts(held, GameplayButtons.Special2 | GameplayButtons.Ultimate, 2))
            .OverrideFailureMessage("The held-back Ultimate claims the tick.")
            .IsFalse();
        AssertThat(Runtime(held, 0).SpecialTwoReuseFrames).IsEqual(0);
        AssertThat(Fighter(held, 0).Influence).IsEqual(FP64.FromInt(100));
        AssertThat(UltimatePhase(held, 0)).IsEqual(FighterUltimateActivationRules.PhaseNone);
        AssertThat(StepAccepts(held, GameplayButtons.Special2, 2))
            .OverrideFailureMessage("Special 2 alone is the confirm.")
            .IsTrue();

        // Control: an Ultimate the meter cannot pay for claims nothing (seeded
        // low enough that the landed opener's meter gain cannot fill it).
        FighterSimulation unpaid = ConfirmedSwing(seed: 392, meter: 50);
        AssertThat(Fighter(unpaid, 0).Influence < FP64.FromInt(100)).IsTrue();
        AssertThat(StepAccepts(unpaid, GameplayButtons.Special2 | GameplayButtons.Ultimate, 2)).IsTrue();

        // The movement ability is outside the claim: it still fires beside a
        // held-back Ultimate.
        FighterSimulation movement = NewSim(seed: 393, spawnDistance: 4);
        AssertThat(movement.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        Step(movement, GameplayButtons.Special1);
        AssertThat(Lock(movement)).IsEqual(Lockout);
        Step(movement, GameplayButtons.MovementAbility | GameplayButtons.Ultimate);
        AssertThat(Runtime(movement, 0).MovementCooldownFrames > 0)
            .OverrideFailureMessage("The movement ability is never claimed by a held-back Ultimate.")
            .IsTrue();
        AssertThat(UltimatePhase(movement, 0)).IsEqual(FighterUltimateActivationRules.PhaseNone);
        AssertThat(Fighter(movement, 0).Influence).IsEqual(FP64.FromInt(100));
    }

    // === Rollback and resets ===

    [TestCase]
    public void TheHitConfirmStateIsRollbackStateAndACorrectedPressConverges() {
        FighterSimulation simulation = NewSim(seed: 401);
        Approach(simulation);
        int maxHP = Fighter(simulation, 1).CurrentHP;
        Step(simulation, GameplayButtons.BasicAttack);
        while (Fighter(simulation, 1).CurrentHP == maxHP) Step(simulation, GameplayButtons.None);
        AssertThat(Runtime(simulation, 0).SwingLanded).IsEqual(1);
        byte[] snapshot = simulation.CaptureFullState();

        var script = new GameplayButtons[40];
        script[1] = GameplayButtons.Special1;
        script[6] = GameplayButtons.Special1;
        script[12] = GameplayButtons.BasicAttack;
        script[20] = GameplayButtons.Special2;
        var hashes = new long[script.Length];
        var packed = new (int s, int c, int a)[script.Length];
        for (int frame = 0; frame < script.Length; frame++) {
            hashes[frame] = Step(simulation, script[frame]);
            FighterRuntimeComponent runtime = Runtime(simulation, 0);
            packed[frame] = (runtime.SpecialLockoutFrames, runtime.SwingLanded, runtime.AbilityLockoutFrames);
        }
        simulation.RestoreFullState(snapshot);
        AssertThat(Runtime(simulation, 0).SwingLanded).IsEqual(1);
        for (int frame = 0; frame < script.Length; frame++) {
            AssertThat(Step(simulation, script[frame]))
                .OverrideFailureMessage($"Restored replay diverged at frame {frame}.")
                .IsEqual(hashes[frame]);
            FighterRuntimeComponent runtime = Runtime(simulation, 0);
            AssertThat((runtime.SpecialLockoutFrames, runtime.SwingLanded, runtime.AbilityLockoutFrames))
                .IsEqual(packed[frame]);
        }

        // A remote confirm press: predicted away, sent on the landing tick
        // (refused), or sent on the first freeze tick (held, then confirmed) —
        // each corrected to the truth converges.
        RemoteConfirmTimeline timeline = ProbeRemoteConfirm();
        AssertCorrectionConverges(timeline, sentPressTick: -1, truePressTick: timeline.ConfirmPressTick);
        AssertCorrectionConverges(timeline, sentPressTick: timeline.ConfirmPressTick - 1, truePressTick: timeline.ConfirmPressTick);
        AssertCorrectionConverges(timeline, sentPressTick: timeline.ConfirmPressTick, truePressTick: timeline.ConfirmPressTick - 1);
    }

    [TestCase]
    public void SuddenDeathClearsTheHitConfirmState() {
        var simulation = new FighterSimulation(
            Loadout(FighterCharacterID.Einstein, ProjectileModes(), FP64.FromInt(3)),
            Loadout(FighterCharacterID.Einstein, ProjectileModes(), FP64.FromInt(3)),
            matchSeconds: 3, seed: 411, spawnDistance: 1,
            rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));
        AssertThat(simulation.SeedFighterSpecialReuseForTest(0, 0, LongCooldown)).IsTrue();
        Approach(simulation);
        while (simulation.GetMatchState().RemainingFrames > 16) Step(simulation, GameplayButtons.None);

        int maxHP = Fighter(simulation, 1).CurrentHP;
        Step(simulation, GameplayButtons.BasicAttack);
        int lastSpecialLockout = 0;
        int lastSwingLanded = 0;
        for (int step = 0; step < 60 && simulation.GetMatchState().SuddenDeathActive == 0; step++) {
            bool landed = Fighter(simulation, 1).CurrentHP < maxHP;
            Step(simulation, landed ? GameplayButtons.Special1 : GameplayButtons.None);
            if (simulation.GetMatchState().SuddenDeathActive != 0) break;
            lastSpecialLockout = Runtime(simulation, 0).SpecialLockoutFrames;
            lastSwingLanded = Runtime(simulation, 0).SwingLanded;
        }
        AssertThat(simulation.GetMatchState().SuddenDeathActive).IsEqual(1);
        AssertThat(lastSpecialLockout > 0 && lastSwingLanded == 1)
            .OverrideFailureMessage($"Harness: S={lastSpecialLockout} C={lastSwingLanded} into the buzzer.")
            .IsTrue();
        FighterRuntimeComponent reset = Runtime(simulation, 0);
        AssertThat(reset.SpecialLockoutFrames).IsEqual(0);
        AssertThat(reset.SwingLanded).IsEqual(0);
        AssertThat(reset.AbilityLockoutFrames).IsEqual(0);
        AssertThat(Verb(simulation, 0).MomentumUnits)
            .OverrideFailureMessage("Sudden Death restarts the gauge at two pips.")
            .IsEqual(BasicComboRules.MomentumStartUnits);
        AssertThat(reset.SpecialOneReuseFrames).IsEqual(0);
        AssertThat(reset.SpecialTwoReuseFrames).IsEqual(0);
    }

    // === Special → Special and the Specials' own hits ===

    [TestCase]
    public void AHitConfirmedSpecialStillLocksTheOtherSpecialForThirtyFrames() {
        FighterSimulation simulation = NewSim(seed: 421);
        Approach(simulation);
        int maxHP = Fighter(simulation, 1).CurrentHP;
        Step(simulation, GameplayButtons.BasicAttack);
        bool confirmed = false;
        for (int step = 0; step < 60 && !confirmed; step++) {
            int lockoutBefore = Lock(simulation);
            if (StepAccepts(simulation,
                    Fighter(simulation, 1).CurrentHP < maxHP ? GameplayButtons.Special1 : GameplayButtons.None, 1)) {
                confirmed = true;
                // Above 1: the tick's own countdown cannot have cleared it.
                AssertThat(lockoutBefore > 1)
                    .OverrideFailureMessage("Special 1 must confirm while the swing's lockout still runs.")
                    .IsTrue();
            }
        }
        AssertThat(confirmed).OverrideFailureMessage("Harness: the hit-confirm Special 1 never fired.").IsTrue();

        int unfrozen = 0;
        bool accepted = false;
        for (int step = 1; step <= 90 && !accepted; step++) {
            if (Verb(simulation, 0).HitstopFrames == 0) unfrozen++;
            accepted = StepAccepts(simulation, GameplayButtons.Special2, 2);
            if (accepted) {
                AssertThat(unfrozen)
                    .OverrideFailureMessage("Special 2 must wait the 30 unfrozen ticks after the confirmed Special 1.")
                    .IsEqual(Lockout);
            } else {
                AssertThat(unfrozen < Lockout).IsTrue();
            }
        }
        AssertThat(accepted).IsTrue();
    }

    [TestCase]
    public void ASpecialsOwnLandedHitNeverSetsTheConfirm() {
        // A melee Special 1 landing at the two-unit kiss range.
        FighterAbilityLoadout melee = ProjectileModes() with {
            SpecialOneExecutionType = 0,
            SpecialOneHit = new FighterAbilityHitData {
                HitstunFrames = 12,
                BlockClass = (int)BlockClass.Special,
                Launches = false,
                Delivery = (int)HitDelivery.DirectHit,
                Origin = (int)HitOrigin.Special
            }
        };
        FighterSimulation meleeRun = NewSim(seed: 431, attackerModes: melee);
        AssertThat(meleeRun.SeedFighterSpecialReuseForTest(0, 0, LongCooldown)).IsTrue();
        Idle(meleeRun, 2);
        int maxHP = Fighter(meleeRun, 1).CurrentHP;
        Step(meleeRun, GameplayButtons.Special1);
        AssertThat(Fighter(meleeRun, 1).CurrentHP < maxHP).OverrideFailureMessage("Harness: the melee Special must land.").IsTrue();
        AssertThat(Runtime(meleeRun, 0).SwingLanded).IsEqual(0);
        AssertThat(Verb(meleeRun, 0).MomentumUnits <= BasicComboRules.MomentumStartUnits)
            .OverrideFailureMessage("A Special's own hit pays the gauge nothing (the cast spent a pip).")
            .IsTrue();

        // A fast projectile Special 1 landing.
        FighterAbilityLoadout fast = ProjectileModes() with { SpecialOneProjectileSpeed = FP64.FromInt(12) };
        FighterSimulation shotRun = NewSim(seed: 432, attackerModes: fast);
        AssertThat(shotRun.SeedFighterSpecialReuseForTest(0, 0, LongCooldown)).IsTrue();
        int shotHP = Fighter(shotRun, 1).CurrentHP;
        Step(shotRun, GameplayButtons.Special1);
        for (int step = 0; step < 60 && Fighter(shotRun, 1).CurrentHP == shotHP; step++) Step(shotRun, GameplayButtons.None);
        AssertThat(Fighter(shotRun, 1).CurrentHP < shotHP).OverrideFailureMessage("Harness: the shot must land.").IsTrue();
        AssertThat(Runtime(shotRun, 0).SwingLanded).IsEqual(0);
        AssertThat(Verb(shotRun, 0).MomentumUnits <= BasicComboRules.MomentumStartUnits)
            .OverrideFailureMessage("A Special's own shot pays the gauge nothing.")
            .IsTrue();
    }

    [TestCase]
    public void AnAerialHitThatLandsKeepsItsConfirmThroughTheLandingCancel() {
        FighterSimulation simulation = NewSim(seed: 441, basicKnockback: FP64.Zero);
        Approach(simulation);
        int maxHP = Fighter(simulation, 1).CurrentHP;
        Step(simulation, GameplayButtons.Jump);
        Step(simulation, GameplayButtons.BasicAttack);
        AssertThat((Runtime(simulation, 0).AttackFlags & FighterBasicAttackRules.FlagAerial) != 0)
            .OverrideFailureMessage("Harness: the swing must start airborne.")
            .IsTrue();
        // Down held from the swing's second tick (the variant was chosen at its
        // start): a fast-fall keeps the airtime inside the lockout.
        int step = 0;
        for (; step < 30 && Fighter(simulation, 1).CurrentHP == maxHP; step++) Step(simulation, GameplayButtons.Down);
        AssertThat(Fighter(simulation, 1).CurrentHP < maxHP).OverrideFailureMessage("Harness: the aerial hit must land.").IsTrue();
        AssertThat(Runtime(simulation, 0).SwingLanded).IsEqual(1);
        // Fast-fall back to the floor: landing cancels the aerial string.
        for (; step < 120 && Fighter(simulation, 0).IsGrounded == 0; step++) Step(simulation, GameplayButtons.Down);
        AssertThat(Fighter(simulation, 0).IsGrounded).IsEqual(1);
        // The movement system cancels the aerial string on its next grounded tick.
        Step(simulation, GameplayButtons.None);
        AssertThat(Runtime(simulation, 0).AttackPhase)
            .OverrideFailureMessage("Landing cancels the aerial string.")
            .IsEqual(FighterBasicAttackRules.PhaseNone);
        AssertThat(Runtime(simulation, 0).SwingLanded).IsEqual(1);
        int lockoutBefore = Lock(simulation);
        AssertThat(lockoutBefore > 1).OverrideFailureMessage("Harness: the lockout must still run after the landing.").IsTrue();
        AssertThat(StepAccepts(simulation, GameplayButtons.Special1, 1))
            .OverrideFailureMessage("The landed aerial hit's confirm survives the landing cancel.")
            .IsTrue();
    }

    // === The CPU ===

    [TestCase]
    public void TheCpuHitConfirmsIntoASpecialOnlyOnceItsOwnSwingHasLanded() {
        CpuDecisionObservation close = Neutral(targetX: 1);
        AssertThat(CountPresses(close, lockout: 15, special: 0, landed: 1, GameplayButtons.Special1) > 0).IsTrue();
        AssertThat(CountPresses(close, lockout: 15, special: 0, landed: 1, GameplayButtons.Special2) > 0).IsTrue();
        AssertThat(CountPresses(close, lockout: 15, special: 15, landed: 1, GameplayButtons.Special1)).IsEqual(0);
        AssertThat(CountPresses(close, lockout: 15, special: 15, landed: 1, GameplayButtons.Special2)).IsEqual(0);
        AssertThat(CountPresses(close, lockout: 15, special: 0, landed: 0, GameplayButtons.Special1))
            .OverrideFailureMessage("A swing that has not landed keeps the whole lockout.")
            .IsEqual(0);

        // A viable Ultimate waiting on the lockout still stops everything (the
        // wait rule): the CPU never cashes its confirm into a Special then.
        CpuDecisionObservation finisher = Neutral(targetX: 1);
        finisher.InfluenceRaw = FP64.FromInt(100).RawValue;
        finisher.TargetCurrentHP = 20;
        AssertThat(CountPresses(finisher, 15, 0, 1, GameplayButtons.Ultimate)).IsEqual(0);
        AssertThat(CountPresses(finisher, 15, 0, 1, GameplayButtons.Special1)).IsEqual(0);
        AssertThat(CountPresses(finisher, 15, 0, 1, GameplayButtons.Special2)).IsEqual(0);
        AssertThat(CountPresses(finisher, 15, 0, 1, GameplayButtons.BasicAttack)).IsEqual(0);
        finisher.InfluenceRaw = FP64.FromInt(99).RawValue;
        AssertThat(CountPresses(finisher, 15, 0, 1, GameplayButtons.Special1) > 0)
            .OverrideFailureMessage("Below a full meter the CPU confirms into Special 1.")
            .IsTrue();

        // Observe copies both fields from the CPU's own component 305.
        var self = new FighterStateComponent { PlayerID = 0, Stocks = 3, CurrentHP = 100, MaxHP = 100 };
        var target = new FighterStateComponent { PlayerID = 1, Stocks = 3, CurrentHP = 100, MaxHP = 100 };
        var selfRuntime = new FighterRuntimeComponent { AbilityLockoutFrames = 17, SpecialLockoutFrames = 9, SwingLanded = 1 };
        var targetRuntime = new FighterRuntimeComponent { AbilityLockoutFrames = 4, SpecialLockoutFrames = 3, SwingLanded = 0 };
        CpuDecisionObservation observed = FighterCpuController.Observe(in self, in selfRuntime, in target, in targetRuntime);
        AssertThat(observed.SpecialLockoutFrames).IsEqual(9);
        AssertThat(observed.SwingLanded).IsEqual(1);
        AssertThat(observed.AbilityLockoutFrames).IsEqual(17);
    }

    [TestCase]
    public void AHardCpuHitConfirmsIntoSpecialOneInAMatch() {
        CpuBandTuning tuning = CpuBandTuning.Hard with {
            SpecialOneClosePercent = 100,
            SpecialOneRangedPercent = 0,
            SpecialTwoPercent = 0,
            UltimatePercent = 0,
            GrabAdmissionPercent = 0,
            EvasiveRollPercent = 0,
            BlockPercent = 0,
            ApproachJumpPercent = 0,
            MovementAbilityPercent = 0
        };
        FighterSimulation simulation = NewSim(seed: 451);
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 20261011, null, null, tuning);
        PlayerInputFrame previous = default;
        int confirms = 0;
        int previousLockout = 0;
        for (int tick = 0; tick < 900; tick++) {
            FighterStateComponent self = Fighter(simulation, 0);
            FighterRuntimeComponent selfRuntime = Runtime(simulation, 0);
            FighterStateComponent target = Fighter(simulation, 1);
            FighterRuntimeComponent targetRuntime = Runtime(simulation, 1);
            CpuDecisionObservation observation = FighterCpuController.Observe(
                in self, in selfRuntime, in target, in targetRuntime);
            PlayerInputFrame frame = cpu.Sample((uint)tick, in observation, in previous);
            previous = frame;
            int cooldownBefore = selfRuntime.SpecialOneReuseFrames;
            simulation.Advance(frame, Input(tick, GameplayButtons.None));
            FighterRuntimeComponent after = Runtime(simulation, 0);
            if (after.SpecialOneReuseFrames > cooldownBefore && previousLockout >= 2) confirms++;
            previousLockout = after.AbilityLockoutFrames;
        }
        AssertThat(confirms > 0)
            .OverrideFailureMessage("A Hard CPU must accept Special 1 while its swing's lockout still runs (a hit-confirm).")
            .IsTrue();
    }

    // === The freeze-only Special buffer (user ruling 2026-10-11) ===

    [TestCase]
    public void ASpecialPressedInsideTheLandedSwingsFreezeFiresOnTheThawTick() {
        FighterSimulation simulation = NewSim(seed: 461);
        Approach(simulation);
        int maxHP = Fighter(simulation, 1).CurrentHP;
        Step(simulation, GameplayButtons.BasicAttack);
        while (Fighter(simulation, 1).CurrentHP == maxHP) Step(simulation, GameplayButtons.None);
        int freeze = Verb(simulation, 0).HitstopFrames;
        AssertThat(freeze >= 2).OverrideFailureMessage("Harness: the freeze must have a tick before its last.").IsTrue();
        AssertThat(Runtime(simulation, 0).FreezeBufferArmed).IsEqual(1);

        // Pressed on the freeze's first tick only — a tick the sim never reads.
        AssertThat(StepAccepts(simulation, GameplayButtons.Special1, 1)).IsFalse();
        AssertThat(Runtime(simulation, 0).FreezeBufferedSpecials).IsEqual(1);
        int shotStep = -1;
        for (int step = 2; step <= freeze + 2 && shotStep < 0; step++) {
            int hitstopBefore = Verb(simulation, 0).HitstopFrames;
            if (StepAccepts(simulation, GameplayButtons.None, 1)) {
                shotStep = step;
                AssertThat(hitstopBefore)
                    .OverrideFailureMessage("The held press fires on the thaw tick (the freeze's last).")
                    .IsEqual(1);
            }
        }
        AssertThat(shotStep).IsEqual(freeze);
        AssertThat(Runtime(simulation, 0).FreezeBufferedSpecials).IsEqual(0);
        AssertThat(Runtime(simulation, 0).FreezeBufferArmed).IsEqual(0);

        // Control: the same tick with nothing pressed fires nothing.
        FighterSimulation control = NewSim(seed: 461);
        Approach(control);
        Step(control, GameplayButtons.BasicAttack);
        while (Fighter(control, 1).CurrentHP == maxHP) Step(control, GameplayButtons.None);
        bool controlFired = false;
        for (int step = 0; step <= freeze + 2; step++) controlFired |= StepAccepts(control, GameplayButtons.None, 1);
        AssertThat(controlFired).IsFalse();
    }

    [TestCase]
    public void TheFreezeBufferHoldsNoUltimateNoLockedPressAndNoOtherFreeze() {
        // The Ultimate chord inside the freeze: nothing is held, nothing fires.
        FighterSimulation chord = NewSim(seed: 471);
        AssertThat(chord.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        int chordHP = LandAndReturnFreeze(chord, out int chordFreeze);
        AssertThat(chordFreeze >= 2).IsTrue();
        Step(chord, GameplayButtons.Special2 | GameplayButtons.MovementAbility | GameplayButtons.Ultimate);
        AssertThat(Runtime(chord, 0).FreezeBufferedSpecials).IsEqual(0);
        bool chordFired = false;
        for (int step = 0; step <= chordFreeze + 2; step++) chordFired |= StepAccepts(chord, GameplayButtons.None, 2);
        AssertThat(chordFired).OverrideFailureMessage("The chord's Special 2 must not fire on its own.").IsFalse();
        AssertThat(UltimatePhase(chord, 0))
            .OverrideFailureMessage("The Ultimate is never buffered.")
            .IsEqual(FighterUltimateActivationRules.PhaseNone);
        AssertThat(Fighter(chord, 0).Influence).IsEqual(FP64.FromInt(100));
        AssertThat(chordHP > 0).IsTrue();

        // A held press the gate refuses at the thaw is dropped, not kept: a
        // Special cast (away from the target) inside the last 30 frames.
        FighterSimulation locked = NewSim(seed: 472);
        Approach(locked);
        Step(locked, GameplayButtons.None, moveX: -127);
        Step(locked, GameplayButtons.Special1);
        Step(locked, GameplayButtons.None, moveX: 127);
        int lockedHP = Fighter(locked, 1).CurrentHP;
        Step(locked, GameplayButtons.BasicAttack);
        while (Fighter(locked, 1).CurrentHP == lockedHP) Step(locked, GameplayButtons.None);
        int lockedFreeze = Verb(locked, 0).HitstopFrames;
        AssertThat(lockedFreeze >= 2).IsTrue();
        AssertThat(Runtime(locked, 0).SpecialLockoutFrames > lockedFreeze + 2)
            .OverrideFailureMessage("Harness: Special 1's lockout must outlast the freeze.")
            .IsTrue();
        Step(locked, GameplayButtons.Special2);
        AssertThat(Runtime(locked, 0).FreezeBufferedSpecials).IsEqual(2);
        bool lockedFired = false;
        for (int step = 0; step < 60; step++) lockedFired |= StepAccepts(locked, GameplayButtons.None, 2);
        AssertThat(lockedFired)
            .OverrideFailureMessage("A refused held press is dropped, never re-tried when the gate opens.")
            .IsFalse();

        // A victim's freeze holds nothing: player one is struck by player two.
        FighterSimulation victim = NewSim(seed: 473);
        for (int step = 0; step < 60; step++) {
            bool close = Fighter(victim, 1).Position.x - Fighter(victim, 0).Position.x <= FP64.FromDouble(1.2);
            victim.Advance(Input(victim.CurrentTick, GameplayButtons.None),
                Input(victim.CurrentTick, GameplayButtons.None, close ? (sbyte)0 : (sbyte)-127));
            if (close) break;
        }
        for (int step = 0; step < 20; step++) {
            victim.Advance(Input(victim.CurrentTick, GameplayButtons.None), Input(victim.CurrentTick, GameplayButtons.None));
        }
        int victimHP = Fighter(victim, 0).CurrentHP;
        victim.Advance(Input(victim.CurrentTick, GameplayButtons.None), Input(victim.CurrentTick, GameplayButtons.BasicAttack));
        for (int step = 0; step < 30 && Fighter(victim, 0).CurrentHP == victimHP; step++) {
            victim.Advance(Input(victim.CurrentTick, GameplayButtons.None), Input(victim.CurrentTick, GameplayButtons.None));
        }
        AssertThat(Fighter(victim, 0).CurrentHP < victimHP).OverrideFailureMessage("Harness: player two must land.").IsTrue();
        AssertThat(Verb(victim, 0).HitstopFrames > 0).IsTrue();
        victim.Advance(Input(victim.CurrentTick, GameplayButtons.Special1), Input(victim.CurrentTick, GameplayButtons.None));
        AssertThat(Runtime(victim, 0).FreezeBufferArmed).IsEqual(0);
        AssertThat(Runtime(victim, 0).FreezeBufferedSpecials)
            .OverrideFailureMessage("A victim's freeze is not a landed swing's freeze.")
            .IsEqual(0);
    }

    // === Harness ===

    /// <summary>
    /// What one Advance added to player one's Momentum gauge beyond that tick's
    /// passive regeneration (the match is live, so it ran), given the gauge read
    /// before the tick.
    /// </summary>
    private static int GaugeGainSince(FighterSimulation simulation, int unitsBefore) =>
        Verb(simulation, 0).MomentumUnits - unitsBefore - FighterMomentumRules.RegenUnitsPerFrame;

    private struct Landing {
        public int ComboIndex;
        /// <summary>The gauge change on the landing tick, less that tick's regeneration.</summary>
        public int GaugeGain;
        public bool WasUpAttack;
        public bool WasDownAir;
    }

    private struct SwingOutcome {
        public int SwingLanded;
        /// <summary>The gauge change on the tick the swing resolved, less that tick's regeneration.</summary>
        public int GaugeGain;
        public int TargetHPLost;
        public int TargetBlockChargesSpent;
        public bool TargetCounterTriggered;
        public int LockoutAfter;
        public int SpecialGateAfter;
    }

    /// <summary>
    /// Both slots cooling, the swing resolved against the target: records the
    /// cooldown change on the tick the swing's box resolved.
    /// </summary>
    private static SwingOutcome SwingIntoTarget(
        FighterSimulation simulation, GameplayButtons targetHeld, System.Action<FighterSimulation> prepare,
        GameplayButtons targetPressOnSwing = GameplayButtons.None) {
        AssertThat(simulation.SeedFighterSpecialReuseForTest(0, LongCooldown, LongCooldown)).IsTrue();
        for (int step = 0; step < 60; step++) {
            bool close = Fighter(simulation, 1).Position.x - Fighter(simulation, 0).Position.x <= FP64.FromDouble(1.2);
            simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.None, close ? (sbyte)0 : (sbyte)127),
                Input(simulation.CurrentTick, targetHeld));
            if (close) break;
        }
        for (int step = 0; step < 20; step++) {
            simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.None), Input(simulation.CurrentTick, targetHeld));
        }
        prepare?.Invoke(simulation);
        FighterStateComponent before = Fighter(simulation, 1);
        simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.BasicAttack),
            Input(simulation.CurrentTick, targetHeld | targetPressOnSwing));
        var outcome = new SwingOutcome();
        for (int step = 0; step < 30; step++) {
            FighterRuntimeComponent runtimeBefore = Runtime(simulation, 0);
            int unitsBefore = Verb(simulation, 0).MomentumUnits;
            bool resolvedBefore = (runtimeBefore.AttackFlags & FighterBasicAttackRules.FlagHitResolved) != 0;
            simulation.Advance(Input(simulation.CurrentTick, GameplayButtons.None), Input(simulation.CurrentTick, targetHeld));
            FighterRuntimeComponent runtimeAfter = Runtime(simulation, 0);
            bool counteredNow = false;
            if (simulation.TryGetFighterCounter(1, out FighterCounterComponent counter) && counter.CountersLanded > 0
                && !outcome.TargetCounterTriggered) {
                outcome.TargetCounterTriggered = true;
                counteredNow = true;
            }
            // A Foresight answer can strike the attacker on the same tick and
            // cancel its swing (clearing the resolved flag), so the counter
            // itself marks that tick too.
            bool resolvedNow = !resolvedBefore
                && ((runtimeAfter.AttackFlags & FighterBasicAttackRules.FlagHitResolved) != 0 || counteredNow);
            if (!resolvedNow) continue;
            outcome.GaugeGain = GaugeGainSince(simulation, unitsBefore);
            outcome.SwingLanded = runtimeAfter.SwingLanded;
            outcome.LockoutAfter = runtimeAfter.AbilityLockoutFrames;
            outcome.SpecialGateAfter = BasicComboRules.SpecialGateFrames(
                runtimeAfter.AbilityLockoutFrames, runtimeAfter.SpecialLockoutFrames, runtimeAfter.SwingLanded != 0);
            break;
        }
        FighterStateComponent after = Fighter(simulation, 1);
        outcome.TargetHPLost = before.CurrentHP - after.CurrentHP;
        outcome.TargetBlockChargesSpent = before.BlockCharges - after.BlockCharges;
        if (simulation.TryGetFighterCounter(1, out FighterCounterComponent final) && final.CountersLanded > 0) {
            outcome.TargetCounterTriggered = true;
        }
        return outcome;
    }

    private static void AssertNoConfirm(SwingOutcome outcome, string what) {
        AssertThat(outcome.SwingLanded).OverrideFailureMessage($"{what} must not confirm.").IsEqual(0);
        AssertThat(outcome.GaugeGain).OverrideFailureMessage($"{what} adds nothing to the gauge.").IsEqual(0);
        AssertThat(outcome.TargetHPLost).IsEqual(0);
        AssertThat(outcome.SpecialGateAfter > 0)
            .OverrideFailureMessage($"After {what} the Specials still wait for the whole lockout.")
            .IsTrue();
    }

    /// <summary>
    /// Lands <paramref name="hits"/> chained string hits from the current
    /// position (a zero-knockback attacker keeps the target in reach), recording
    /// each landing tick's cooldown change.
    /// </summary>
    private static List<Landing> LandString(FighterSimulation simulation, int hits) {
        var landings = new List<Landing>();
        int swings = 0;
        for (int step = 0; step < 400 && landings.Count < hits; step++) {
            FighterRuntimeComponent before = Runtime(simulation, 0);
            int unitsBefore = Verb(simulation, 0).MomentumUnits;
            int hp = Fighter(simulation, 1).CurrentHP;
            bool press = swings == 0
                ? before.AttackPhase == FighterBasicAttackRules.PhaseNone
                : swings < hits && before.AttackPhase == FighterBasicAttackRules.PhaseChainHold;
            if (press) swings++;
            Step(simulation, press ? GameplayButtons.BasicAttack : GameplayButtons.None);
            if (Fighter(simulation, 1).CurrentHP >= hp) continue;
            FighterRuntimeComponent after = Runtime(simulation, 0);
            landings.Add(new Landing {
                ComboIndex = after.ComboIndex,
                GaugeGain = GaugeGainSince(simulation, unitsBefore)
            });
        }
        return landings;
    }

    /// <summary>One swing pressed now, stepped until it lands; the landing tick's cooldown change.</summary>
    private static Landing SwingUntilLanded(
        FighterSimulation simulation, GameplayButtons swing, sbyte moveY = 0,
        GameplayButtons heldAfter = GameplayButtons.None) {
        int hp = Fighter(simulation, 1).CurrentHP;
        Step(simulation, swing, moveY: moveY);
        int flags = Runtime(simulation, 0).AttackFlags;
        var landing = new Landing {
            WasUpAttack = (flags & FighterBasicAttackRules.FlagUpAttack) != 0,
            WasDownAir = (flags & FighterBasicAttackRules.FlagDownAir) != 0
        };
        for (int step = 0; step < 40; step++) {
            int unitsBefore = Verb(simulation, 0).MomentumUnits;
            Step(simulation, heldAfter);
            if (Fighter(simulation, 1).CurrentHP >= hp) continue;
            landing.GaugeGain = GaugeGainSince(simulation, unitsBefore);
            return landing;
        }
        AssertThat(false).OverrideFailureMessage("Harness: the swing never landed.").IsTrue();
        return landing;
    }

    /// <summary>Approached, the opener landed and its freeze fully thawed: C = 1, S = 0, A &gt; 0.</summary>
    private static FighterSimulation ConfirmedSwing(int seed, int meter) {
        FighterSimulation simulation = NewSim(seed);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, meter)).IsTrue();
        Approach(simulation);
        int maxHP = Fighter(simulation, 1).CurrentHP;
        Step(simulation, GameplayButtons.BasicAttack);
        while (Fighter(simulation, 1).CurrentHP == maxHP) Step(simulation, GameplayButtons.None);
        while (Verb(simulation, 0).HitstopFrames > 0) Step(simulation, GameplayButtons.None);
        FighterRuntimeComponent runtime = Runtime(simulation, 0);
        AssertThat(runtime.SwingLanded).IsEqual(1);
        AssertThat(runtime.SpecialLockoutFrames).IsEqual(0);
        AssertThat(runtime.AbilityLockoutFrames > 1).IsTrue();
        return simulation;
    }

    /// <summary>Approaches, lands the opener and returns the target's HP; <paramref name="freeze"/> is the attacker's freeze.</summary>
    private static int LandAndReturnFreeze(FighterSimulation simulation, out int freeze) {
        Approach(simulation);
        int maxHP = Fighter(simulation, 1).CurrentHP;
        Step(simulation, GameplayButtons.BasicAttack);
        while (Fighter(simulation, 1).CurrentHP == maxHP) Step(simulation, GameplayButtons.None);
        freeze = Verb(simulation, 0).HitstopFrames;
        return Fighter(simulation, 1).CurrentHP;
    }

    private static void Settle(FighterSimulation simulation) {
        for (int step = 0; step < 240; step++) {
            if (Runtime(simulation, 0).AttackPhase == FighterBasicAttackRules.PhaseNone
                && Fighter(simulation, 1).HitstunFrames == 0
                && Verb(simulation, 0).HitstopFrames == 0) return;
            Step(simulation, GameplayButtons.None);
        }
    }

    // --- Remote confirm convergence ---

    private struct RemoteConfirmTimeline {
        public sbyte[] LocalMoveX;
        public int SwingTick;
        /// <summary>
        /// The first tick a Special press confirms: the landing's first freeze
        /// tick (held by the freeze-only buffer, fired on the thaw tick). A press
        /// on the landing tick itself is read before the landing and refused.
        /// </summary>
        public int ConfirmPressTick;
    }

    private const int RemoteFinalTick = 110;

    /// <summary>
    /// Player one walks in (its inputs recorded so every run replays them),
    /// player two swings and lands, and the probe measures the first tick on
    /// which player two's Special 1 press confirms.
    /// </summary>
    private static RemoteConfirmTimeline ProbeRemoteConfirm() {
        FighterSimulation probe = NewSim(seed: 7201);
        var moves = new sbyte[RemoteFinalTick];
        int swingTick = -1;
        int landingTick = -1;
        int maxHP = Fighter(probe, 0).CurrentHP;
        bool approached = false;
        int settledAt = -1;
        for (int tick = 0; tick < RemoteFinalTick && landingTick < 0; tick++) {
            if (!approached) {
                approached = Fighter(probe, 1).Position.x - Fighter(probe, 0).Position.x <= FP64.FromDouble(1.2);
                if (approached) settledAt = tick;
            }
            sbyte moveX = approached ? (sbyte)0 : (sbyte)127;
            moves[tick] = moveX;
            if (swingTick < 0 && approached && tick == settledAt + 20) swingTick = tick;
            GameplayButtons remote = tick == swingTick ? GameplayButtons.BasicAttack : GameplayButtons.None;
            probe.Advance(Input(tick, GameplayButtons.None, moveX), Input(tick, remote));
            if (landingTick < 0 && swingTick >= 0 && Fighter(probe, 0).CurrentHP < maxHP) {
                landingTick = tick;
                AssertThat(Verb(probe, 1).HitstopFrames > 0).IsTrue();
            }
        }
        AssertThat(landingTick > 0).OverrideFailureMessage("Harness: player two's opener never landed.").IsTrue();
        return new RemoteConfirmTimeline { LocalMoveX = moves, SwingTick = swingTick, ConfirmPressTick = landingTick + 1 };
    }

    private static PlayerInputFrame LocalInput(in RemoteConfirmTimeline timeline, int tick) =>
        Input(tick, GameplayButtons.None, tick < timeline.LocalMoveX.Length ? timeline.LocalMoveX[tick] : (sbyte)0);

    private static PlayerInputFrame RemoteInput(in RemoteConfirmTimeline timeline, int tick, int pressTick) => Input(
        tick,
        tick == timeline.SwingTick ? GameplayButtons.BasicAttack
            : tick == pressTick ? GameplayButtons.Special1
            : GameplayButtons.None);

    private static void AssertCorrectionConverges(RemoteConfirmTimeline timeline, int sentPressTick, int truePressTick) {
        int firstLate = timeline.ConfirmPressTick - 1;
        int lastLate = firstLate + 8;
        FighterSimulation clean = NewSim(seed: 7201);
        var cleanHashes = new long[RemoteFinalTick];
        int cleanSpecialLockout = 0;
        for (int tick = 0; tick < RemoteFinalTick; tick++) {
            cleanHashes[tick] = clean.Advance(LocalInput(timeline, tick), RemoteInput(timeline, tick, truePressTick));
            if (tick == lastLate) cleanSpecialLockout = Runtime(clean, 1).SpecialLockoutFrames;
        }

        FighterSimulation network = NewSim(seed: 7201);
        for (int tick = 0; tick <= lastLate; tick++) {
            if (tick < firstLate) {
                network.Advance(LocalInput(timeline, tick), RemoteInput(timeline, tick, truePressTick));
            } else if (tick == sentPressTick) {
                network.Advance(LocalInput(timeline, tick), RemoteInput(timeline, tick, sentPressTick));
            } else {
                network.AdvanceWithPredictedPlayerTwo(LocalInput(timeline, tick));
            }
        }
        // The Special-sourced lockout is armed only by an accepted Special.
        AssertThat(Runtime(network, 1).SpecialLockoutFrames)
            .OverrideFailureMessage($"Harness: the misprediction (sent {sentPressTick}, true {truePressTick}) must cross the confirm boundary.")
            .IsNotEqual(cleanSpecialLockout);
        for (int tick = firstLate; tick <= lastLate; tick++) {
            AssertThat(network.CorrectPlayerTwoInput(tick, RemoteInput(timeline, tick, truePressTick))).IsTrue();
        }
        AssertThat(network.CurrentHash)
            .OverrideFailureMessage($"Corrected run (sent {sentPressTick}, true {truePressTick}) did not converge.")
            .IsEqual(cleanHashes[lastLate]);
        AssertThat(Runtime(network, 1).SpecialLockoutFrames).IsEqual(cleanSpecialLockout);
        for (int tick = lastLate + 1; tick < RemoteFinalTick; tick++) {
            long hash = network.Advance(LocalInput(timeline, tick), RemoteInput(timeline, tick, truePressTick));
            AssertThat(hash).OverrideFailureMessage($"Diverged after convergence at tick {tick}.").IsEqual(cleanHashes[tick]);
        }
    }

    // --- CPU ---

    private static int CountPresses(
        CpuDecisionObservation observation, int lockout, int special, int landed, GameplayButtons button) {
        observation.AbilityLockoutFrames = lockout;
        observation.SpecialLockoutFrames = special;
        observation.SwingLanded = landed;
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 20261011, null, null, null, null);
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

    // --- Direct ResolveFighterHit calls ---

    private sealed class Fixture {
        public FighterStateComponent Attacker = NewFighter(0);
        public FighterRuntimeComponent AttackerRuntime;
        public FighterVerbComponent AttackerVerb;
        public FighterStateComponent Target = NewFighter(1);
        public FighterRuntimeComponent Runtime;
        public FighterVerbComponent Verb;
        public FighterDefenseComponent Defense;
        public FighterTuningComponent Tuning = new() { MaxBlockCharges = 3, MaxJumpCount = 2 };

        public Fixture With(System.Action<Fixture> setup) {
            setup(this);
            return this;
        }

        public FighterHitOutcome Resolve(int damage) {
            Attacker.Position = new FPVector2(Target.Position.x + FP64.One, Target.Position.y);
            return FighterDamageRules.ResolveFighterHit(
                ref Attacker, ref AttackerRuntime, ref AttackerVerb,
                ref Target, ref Runtime, ref Verb, ref Defense, in Tuning,
                FighterDamageRules.BasicAttackClass, damage, FP64.FromInt(2), 20,
                (int)StatusType.None, 0, FP64.One, Attacker.Position.x);
        }

        public bool Apply(int damage) {
            Attacker.Position = new FPVector2(Target.Position.x + FP64.One, Target.Position.y);
            return FighterDamageRules.ApplyFighterHit(
                ref Attacker, ref AttackerRuntime, ref AttackerVerb,
                ref Target, ref Runtime, ref Verb, ref Defense, in Tuning,
                FighterDamageRules.BasicAttackClass, damage, FP64.FromInt(2), 20,
                (int)StatusType.None, 0, FP64.One, Attacker.Position.x);
        }

        private static FighterStateComponent NewFighter(int playerID) => new() {
            PlayerID = playerID,
            Stocks = 3,
            MaxHP = 100,
            CurrentHP = 100,
            BlockCharges = 3,
            IsGrounded = 1,
            FacingRight = 1,
            Weight = FP64.One
        };
    }

    // --- Simulation ---

    /// <summary>
    /// Walks player one in until the opponent is inside the string's reach,
    /// then lets the run decelerate (the FighterAbilityLockoutTests approach).
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

    private static FighterSimulation NewSim(
        int seed,
        int spawnDistance = 1,
        FighterAbilityLoadout? attackerModes = null,
        FighterAbilityLoadout? targetModes = null,
        FighterCharacterID attacker = FighterCharacterID.Einstein,
        FighterCharacterID target = FighterCharacterID.Einstein,
        FP64? basicKnockback = null) {
        FP64 knockback = basicKnockback ?? FP64.FromInt(3);
        return new FighterSimulation(
            Loadout(attacker, attackerModes ?? ProjectileModes(), knockback),
            Loadout(target, targetModes ?? ProjectileModes(), knockback),
            seed: seed, spawnDistance: spawnDistance, rules: FighterMatchRules.Disabled);
    }

    /// <summary>The Default kit's numbers with zero authored Special cooldowns (1 frame after PositiveCooldown).</summary>
    private static FighterLoadout Loadout(FighterCharacterID character, FighterAbilityLoadout modes, FP64 basicKnockback) => new(
        (int)character,
        100, 3, 2,
        10, 14, 12, 20,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        FP64.One,
        FP64.FromInt(7),
        FP64.FromDouble(11.5),
        basicKnockback,
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

    private static int Lock(FighterSimulation simulation) => Runtime(simulation, 0).AbilityLockoutFrames;

    private static int Cooldown(FighterSimulation simulation, int slot) => slot == 1
        ? Runtime(simulation, 0).SpecialOneReuseFrames
        : Runtime(simulation, 0).SpecialTwoReuseFrames;

    /// <summary>
    /// One step; true when it armed player one's <paramref name="slot"/>
    /// cooldown — an accepted Special. (A shot spawned at the target can land
    /// and despawn on its own tick, so a projectile count is no proof.)
    /// </summary>
    private static bool StepAccepts(FighterSimulation simulation, GameplayButtons press, int slot, sbyte moveX = 0) {
        int before = Cooldown(simulation, slot);
        Step(simulation, press, moveX);
        return before == 0 && Cooldown(simulation, slot) > 0;
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
