using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Combat and movement update 2026-10-16, decision 9.3 (plan §2.9.3,
/// CM-CANCEL) and the Phase A fix round's open CPU item (CM_FIXA_SIM §5): the
/// CPU's own action locks, as the sim's gates read them. The CPU's grab mirror
/// (<c>FighterCpuController.CanStartGrab</c>) refuses everything
/// <c>FighterGrabRules.CanStartGrab</c> refuses on the self side — its own
/// Special cast, the landing-tech lock, the M05 knockdown and its get-up, and its
/// own Ultimate activation — the world observer fills those self fields
/// (<see cref="CpuVerbState"/>) from components 323, 322, 310, 320 and 321
/// exactly, and a Special or Ultimate press is revalidated against the CPU's
/// own cast at delivery. Pure C#: hand-built loadouts, no engine.
/// </summary>
[TestSuite]
public class CpuSelfActionStateTests {

    /// <summary>
    /// A grab opportunity (a blocking opponent half a unit in front, a 100 %
    /// admission) produces a chord in the control case and none while the CPU is
    /// knocked down, in its tech lock, busy in its own Ultimate or casting a
    /// Special — and none of those opens the opportunity, so no admission is
    /// rolled on a press the sim would refuse.
    /// </summary>
    [TestCase]
    public void TheGrabMirrorRefusesTheKnockdownTheTechLockTheCpusOwnUltimateAndItsCast() {
        CpuDecisionObservation baseline = GrabOpportunity();
        var failures = new List<string>();
        var control = NewGrabber();
        if (CountGrabChords(control, baseline, 120) == 0) failures.Add("Harness: the control case never grabbed");
        foreach ((string name, System.Func<CpuDecisionObservation, CpuDecisionObservation> refuse) in new
                     (string, System.Func<CpuDecisionObservation, CpuDecisionObservation>)[] {
                     ("knocked down", o => { o.SelfKnockedDown = 1; return o; }),
                     ("in the tech lock", o => { o.SelfTechLockoutFrames = 12; return o; }),
                     ("busy in its own Ultimate", o => { o.SelfUltimateCasterBusy = 1; return o; }),
                     ("casting a Special", o => {
                         o.SelfSpecialCastPhase = CpuCastPhase.Recovery;
                         o.SelfSpecialCastPhaseFrames = 9;
                         return o;
                     })
                 }) {
            FighterCpuController cpu = NewGrabber();
            int chords = CountGrabChords(cpu, refuse(baseline), 240);
            if (chords != 0) failures.Add($"{name}: {chords} grab chords");
            if (cpu.GrabOpportunityID != 0) failures.Add($"{name}: opened grab opportunity {cpu.GrabOpportunityID}");
        }
        if (failures.Count > 0) AssertThat(string.Join(" | ", failures)).IsEqual("");
    }

    /// <summary>
    /// The sim's world observer projects the CPU's own locks losslessly: a
    /// clocked 8 / 2 / 6 Special reads Startup, Active and Recovery with
    /// component 323's frames left and None once it ends; Tubman's Foresight
    /// (Delegated) reads its component-322 stance (startup → Startup, window →
    /// Active, whiff recovery → Recovery) with 322's frames; a missed tech reads
    /// the knockdown (320), a held-Block landing the 12-frame tech lock (310),
    /// and an accepted Ultimate the activation (321).
    /// </summary>
    [TestCase]
    public void TheWorldObserverReadsTheCastTheKnockdownTheTechLockAndTheUltimateFromTheSim() {
        // A clocked Special: 323.
        var clocked = new FighterSimulation(
            Loadout(FighterAbilityLoadout.Default with {
                SpecialOneHit = FighterAbilityHitData.DefaultSpecial with {
                    StartupFrames = 8, ActiveFrames = 2, RecoveryFrames = 6, TotalFrames = 16
                }
            }),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 77, spawnDistance: 4, rules: FighterMatchRules.Disabled);
        var clockedObserver = new FighterSimulationWorldObserver(clocked);
        clocked.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
        var phases = new List<int>();
        for (int tick = 1; tick <= 40; tick++) {
            AssertThat(clocked.TryGetFighterActionState(0, out FighterActionStateComponent cast)).IsTrue();
            AssertThat(clockedObserver.TryGetVerbState(0, out CpuVerbState state)).IsTrue();
            int expectedPhase = FighterSpecialCastRules.IsCasting(in cast) ? cast.SpecialPhase : CpuCastPhase.None;
            int expectedFrames = FighterSpecialCastRules.IsCasting(in cast) ? cast.SpecialPhaseFrames : 0;
            AssertThat(state.SpecialCastPhase)
                .OverrideFailureMessage($"T+{tick}: the observer read phase {state.SpecialCastPhase}, 323 holds {cast.SpecialPhase}.")
                .IsEqual(expectedPhase);
            AssertThat(state.SpecialCastPhaseFrames)
                .OverrideFailureMessage($"T+{tick}: the observer read {state.SpecialCastPhaseFrames} frames, 323 holds {cast.SpecialPhaseFrames}.")
                .IsEqual(expectedFrames);
            if (phases.Count == 0 || phases[^1] != state.SpecialCastPhase) phases.Add(state.SpecialCastPhase);
            clocked.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(string.Join(",", phases))
            .OverrideFailureMessage("The clocked cast must read Startup, Active, Recovery, then None.")
            .IsEqual($"{CpuCastPhase.Startup},{CpuCastPhase.Active},{CpuCastPhase.Recovery},{CpuCastPhase.None}");
        // The scale is the sim's own: the Mirror adapter maps onto the same values.
        AssertThat(CpuCastPhase.Startup).IsEqual(FighterSpecialCastRules.PhaseStartup);
        AssertThat(CpuCastPhase.Active).IsEqual(FighterSpecialCastRules.PhaseActive);
        AssertThat(CpuCastPhase.Recovery).IsEqual(FighterSpecialCastRules.PhaseRecovery);

        // Foresight (Delegated): 322, whiffed far from anyone.
        var foresight = new FighterSimulation(
            FighterLoadout.Default(FighterCharacterID.Tubman), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 78, spawnDistance: 4, rules: FighterMatchRules.Disabled);
        var foresightObserver = new FighterSimulationWorldObserver(foresight);
        foresight.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        AssertThat(foresight.TryGetFighterActionState(0, out FighterActionStateComponent delegated)).IsTrue();
        AssertThat(FighterSpecialCastRules.IsDelegated(in delegated))
            .OverrideFailureMessage("Harness: a default Tubman's Special 2 must be the Delegated Foresight.")
            .IsTrue();
        var stances = new List<int>();
        for (int tick = 1; tick <= 90; tick++) {
            AssertThat(foresight.TryGetFighterCounter(0, out FighterCounterComponent counter)).IsTrue();
            AssertThat(foresightObserver.TryGetVerbState(0, out CpuVerbState state)).IsTrue();
            int expected = counter.Phase switch {
                FighterForesightRules.PhaseStartup => CpuCastPhase.Startup,
                FighterForesightRules.PhaseWindow => CpuCastPhase.Active,
                FighterForesightRules.PhaseWhiffRecovery => CpuCastPhase.Recovery,
                FighterForesightRules.PhaseSidestep => CpuCastPhase.Recovery,
                _ => -1
            };
            if (expected >= 0) {
                AssertThat(state.SpecialCastPhase)
                    .OverrideFailureMessage($"T+{tick}: stance {counter.Phase} read as cast phase {state.SpecialCastPhase}.")
                    .IsEqual(expected);
                AssertThat(state.SpecialCastPhaseFrames).IsEqual(counter.PhaseFrames);
            }
            if (stances.Count == 0 || stances[^1] != state.SpecialCastPhase) stances.Add(state.SpecialCastPhase);
            foresight.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(string.Join(",", stances))
            .OverrideFailureMessage("A whiffed Foresight must read Startup, Active (the window), Recovery, then None.")
            .IsEqual($"{CpuCastPhase.Startup},{CpuCastPhase.Active},{CpuCastPhase.Recovery},{CpuCastPhase.None}");

        // The knockdown (320) and the tech lock (310): a launched Joan lands
        // without Block, then with Block held.
        foreach (bool holdsBlock in new[] { false, true }) {
            FighterSimulation launched = LaunchedSimulation(seed: holdsBlock ? 80 : 79);
            var observer = new FighterSimulationWorldObserver(launched);
            // Block only once airborne: a grounded stance on the launch tick would block it.
            GameplayButtons victim = holdsBlock ? GameplayButtons.Block : GameplayButtons.None;
            launched.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
            AssertThat(launched.TryGetFighterVerb(1, out FighterVerbComponent launchedVerb)).IsTrue();
            AssertThat(launchedVerb.Tumble)
                .OverrideFailureMessage("Harness: the launcher must put the victim into tumble.")
                .IsEqual(1);
            bool sawLock = false;
            for (int tick = 1; tick < 150 && !sawLock; tick++) {
                launched.Advance(Frame(tick, GameplayButtons.None), Frame(tick, victim));
                AssertThat(launched.TryGetFighterKnockdown(1, out FighterKnockdownComponent knockdown)).IsTrue();
                AssertThat(launched.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
                AssertThat(observer.TryGetVerbState(1, out CpuVerbState state)).IsTrue();
                AssertThat(state.KnockedDown).IsEqual(FighterKnockdownRules.IsActive(in knockdown) ? 1 : 0);
                AssertThat(state.TechLockoutFrames).IsEqual(verb.TechLockoutFrames);
                sawLock = holdsBlock ? state.TechLockoutFrames > 0 : state.KnockedDown != 0;
            }
            AssertThat(sawLock)
                .OverrideFailureMessage(holdsBlock
                    ? "Harness: the held-Block landing never teched."
                    : "Harness: the landing never became a knockdown.")
                .IsTrue();
        }

        // The Ultimate activation (321): accepted with a full meter.
        var ultimate = new FighterSimulation(
            FighterLoadout.Default(FighterCharacterID.Einstein), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 81, spawnDistance: 4, rules: FighterMatchRules.Disabled);
        var ultimateObserver = new FighterSimulationWorldObserver(ultimate);
        AssertThat(ultimate.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        AssertThat(ultimateObserver.TryGetVerbState(0, out CpuVerbState before)).IsTrue();
        AssertThat(before.UltimateCasterBusy).IsEqual(0);
        ultimate.Advance(Frame(0, GameplayButtons.Ultimate), Frame(0, GameplayButtons.None));
        AssertThat(ultimate.TryGetFighterUltimateActivation(0, out FighterUltimateActivationComponent activation)).IsTrue();
        AssertThat(FighterUltimateActivationRules.IsCasterBusy(in activation))
            .OverrideFailureMessage("Harness: the paid Ultimate must start its activation.")
            .IsTrue();
        AssertThat(ultimateObserver.TryGetVerbState(0, out CpuVerbState during)).IsTrue();
        AssertThat(during.UltimateCasterBusy).IsEqual(1);
    }

    /// <summary>
    /// A Special press the CPU decided while its cast was not yet visible — the
    /// press that starts that cast was still in the reaction delay — is dropped
    /// at delivery, on current self-state, instead of arriving inside the cast
    /// that would discard it. Hard, Special 1 at 100 % in close range: the first
    /// decision schedules a press; from the next tick the CPU's own cast shows
    /// (as if an earlier press had just landed), so no Special 1 edge is emitted
    /// on any tick the cast still holds, and presses resume once it ends. The
    /// control stream, the same with no cast shown, emits one at once.
    /// </summary>
    [TestCase]
    public void ASpecialPressDecidedBeforeTheCastWasVisibleIsDroppedAtDelivery() {
        CpuBandTuning tuning = CpuBandTuning.Hard with {
            UltimatePercent = 0,
            SpecialOneClosePercent = 100,
            SpecialTwoPercent = 0,
            GrabAdmissionPercent = 0,
            EvasiveRollPercent = 0,
            BlockPercent = 0
        };
        CpuDecisionObservation idle = GrabOpportunity();
        idle.TargetBlockStance = 0;
        idle.TargetPositionXRaw = FP64.One.RawValue;
        const int castEnd = 40;

        int controlEdge = -1;
        var control = new FighterCpuController(CpuDifficulty.Hard, 6060, null, null, tuning);
        PlayerInputFrame previous = default;
        for (uint tick = 0; tick < 30 && controlEdge < 0; tick++) {
            PlayerInputFrame frame = control.Sample(tick, in idle, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.Special1)) controlEdge = (int)tick;
        }
        AssertThat(controlEdge >= 0 && controlEdge <= 12)
            .OverrideFailureMessage($"Harness: the control CPU must press Special 1 at once (tick {controlEdge}).")
            .IsTrue();

        var cpu = new FighterCpuController(CpuDifficulty.Hard, 6060, null, null, tuning);
        previous = default;
        int firstAfter = -1;
        var inside = new List<int>();
        for (uint tick = 0; tick < 90 && firstAfter < 0; tick++) {
            int t = (int)tick;
            CpuDecisionObservation observation = idle;
            if (t >= 1 && t < castEnd) {
                observation.SelfSpecialCastPhase = t < castEnd - 20 ? CpuCastPhase.Startup : CpuCastPhase.Recovery;
                observation.SelfSpecialCastPhaseFrames = t < castEnd - 20 ? castEnd - 20 - t : castEnd - t;
                observation.AbilityLockoutFrames = System.Math.Max(0, 30 - t);
                observation.SpecialLockoutFrames = observation.AbilityLockoutFrames;
                observation.SpecialOneCooldownFrames = 0;
            }
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (!frame.IsPressed(GameplayButtons.Special1)) continue;
            bool held = observation.SelfSpecialCastPhase != CpuCastPhase.None
                && !(observation.SelfSpecialCastPhase == CpuCastPhase.Recovery && observation.SelfSpecialCastPhaseFrames <= 1);
            if (held) inside.Add(t);
            else if (t >= 1) firstAfter = t;
        }
        if (inside.Count > 0) {
            AssertThat($"Special 1 pressed inside the CPU's own cast on ticks {string.Join(",", inside)}").IsEqual("");
        }
        AssertThat(firstAfter >= castEnd - 1)
            .OverrideFailureMessage($"Presses must resume after the cast (first on tick {firstAfter}).")
            .IsTrue();
    }

    /// <summary>
    /// CM-FIXB-CPU (finding B13): a Medium CPU over Nassau's stern gap during its
    /// own aerial Special (a 10 / 4 / 30 cast) plans no movement-ability press
    /// the cast would discard, and is charged nothing for one. Its recovery
    /// planner did not read the cast, so the press arrived inside it, the sim
    /// threw it away, and the one activation Medium gets per offstage episode
    /// was already spent, leaving it only its jumps once the cast ended. Real
    /// simulation frames through the real world observer: the fighter's walk,
    /// jumps and Special press are pinned with <see cref="PlayerInputFrame.Create"/>
    /// and the CPU drives from the tick after the press.
    /// </summary>
    [TestCase]
    public void ARecoveryMovementAbilityIsNeverSpentInsideTheCpusOwnCast() {
        FighterStageGeometry nassau = FighterStageGeometry.ForStage("nassau_flagship");
        FighterLoadout fighter = Loadout(FighterAbilityLoadout.Default with {
            SpecialOneExecutionType = 0,
            SpecialOneHit = FighterAbilityHitData.DefaultSpecial with {
                StartupFrames = 10, ActiveFrames = 4, RecoveryFrames = 30, TotalFrames = 44
            }
        });
        var simulation = new FighterSimulation(
            FighterLoadout.Default(FighterCharacterID.Joan), fighter,
            stocks: 3, seed: 11, rules: FighterMatchRules.Disabled, stageGeometry: nassau);
        var cpu = new FighterCpuController(
            CpuDifficulty.Normal, 11, nassau, new FighterSimulationWorldObserver(simulation), null,
            CpuRecoveryProfile.FromLoadout(in fighter));
        AssertThat(cpu.Tuning.RecoveryMovementActivationsPerEpisode).IsEqual(1);

        PlayerInputFrame previous = default;
        int step = 0;
        int stepTick = 0;
        int castTicks = 0;
        int castTicksOverTheGap = 0;
        var pressesInCast = new List<int>();
        int chargedInCast = 0;
        int pressesAfterCast = 0;
        bool resolved = false;
        for (int tick = 0; tick < 400 && !resolved; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent target)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent targetRuntime)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent self)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent selfRuntime)).IsTrue();
            AssertThat(simulation.TryGetFighterActionState(1, out FighterActionStateComponent cast)).IsTrue();
            if (self.Stocks < 3) break;
            PlayerInputFrame frame;
            if (step < 3) {
                // Walk to the stern, jump out with a short drift, double-jump at
                // +14 and press Special 1 six ticks later.
                GameplayButtons held = GameplayButtons.None;
                float stick = 0f;
                if (step == 0) {
                    stick = 1f;
                    if (self.Position.x >= FP64.FromDouble(5.6)) { held = GameplayButtons.Jump; step = 1; stepTick = tick; }
                } else if (step == 1) {
                    stick = tick - stepTick < 3 ? 1f : 0f;
                    held = tick - stepTick < 8 ? GameplayButtons.Jump : GameplayButtons.None;
                    if (tick - stepTick >= 14) { held = GameplayButtons.Jump; step = 2; stepTick = tick; }
                } else {
                    held = GameplayButtons.Jump;
                    if (tick - stepTick >= 6) { held = GameplayButtons.Special1; step = 3; }
                }
                frame = PlayerInputFrame.Create((uint)tick, stick, 0f, held, previous.Held);
            } else {
                frame = cpu.Sample((uint)tick, in self, in selfRuntime, in target, in targetRuntime, in previous);
                bool castHolds = FighterSpecialCastRules.IsBusy(in cast)
                    && !(cast.SpecialPhase == FighterSpecialCastRules.PhaseRecovery && cast.SpecialPhaseFrames <= 1);
                if (castHolds) {
                    castTicks++;
                    if (!nassau.HasFloorSupport(self.Position.x)) castTicksOverTheGap++;
                    if (frame.IsPressed(GameplayButtons.MovementAbility)) pressesInCast.Add(tick);
                    chargedInCast = System.Math.Max(chargedInCast, cpu.EpisodeMovementActivations);
                } else if (castTicks > 0 && frame.IsPressed(GameplayButtons.MovementAbility)) {
                    pressesAfterCast++;
                }
                if (castTicks > 0 && !castHolds
                    && (self.IsGrounded != 0 || FighterLedgeRules.IsHanging(in selfRuntime)) && pressesAfterCast > 0) {
                    resolved = true;
                }
            }
            previous = frame;
            simulation.Advance(new PlayerInputFrame { Tick = (uint)tick }, frame);
        }
        AssertThat(castTicks > 20)
            .OverrideFailureMessage($"Harness: the aerial Special must hold the CPU's ticks (held {castTicks}).")
            .IsTrue();
        AssertThat(castTicksOverTheGap > 0)
            .OverrideFailureMessage("Harness: the cast must carry the CPU out over the gap.")
            .IsTrue();
        if (pressesInCast.Count > 0) {
            AssertThat($"movement ability pressed inside the CPU's own cast on ticks {string.Join(",", pressesInCast)}")
                .IsEqual("");
        }
        AssertThat(chargedInCast)
            .OverrideFailureMessage("A movement activation was charged to the episode while the cast held the CPU.")
            .IsEqual(0);
        AssertThat(pressesAfterCast > 0)
            .OverrideFailureMessage("The CPU never sent its movement ability once the cast ended.")
            .IsTrue();
    }

    // === harnesses ===============================================================

    private static FighterCpuController NewGrabber() => new(
        CpuDifficulty.Normal, 5150, null, null,
        CpuBandTuning.Normal with {
            GrabAdmissionPercent = 100,
            UltimatePercent = 0,
            SpecialOneClosePercent = 0,
            SpecialOneRangedPercent = 0,
            SpecialTwoPercent = 0
        });

    /// <summary>A blocking, grounded opponent half a unit in front of a grounded, idle CPU (inside the 0.8-unit grab reach).</summary>
    private static CpuDecisionObservation GrabOpportunity() => new() {
        SelfPositionXRaw = FP64.Zero.RawValue,
        TargetPositionXRaw = FP64.FromDouble(0.5).RawValue,
        Stocks = 3,
        IsGrounded = 1,
        RemainingJumps = 1,
        SelfCurrentHP = 100,
        SelfMaxHP = 100,
        TargetCurrentHP = 100,
        TargetMaxHP = 100,
        HasVerbState = 1,
        SelfFacingRight = 1,
        HasTargetVerbState = 1,
        TargetIsGrounded = 1,
        TargetBlockStance = 1,
        HasStageBounds = 1,
        LeftWallRaw = FP64.FromInt(-9).RawValue,
        RightWallRaw = FP64.FromInt(9).RawValue,
        CeilingRaw = FP64.FromInt(9).RawValue,
        BottomBlastZoneRaw = FP64.FromInt(-5).RawValue
    };

    private static int CountGrabChords(FighterCpuController cpu, CpuDecisionObservation observation, int ticks) {
        PlayerInputFrame previous = default;
        int chords = 0;
        for (uint tick = 0; tick < ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.BasicAttack) && frame.IsHeld(GameplayButtons.Block)) chords++;
        }
        return chords;
    }

    /// <summary>Player one owns a press-tick launching Special (40-frame tumble) one unit from a default Joan.</summary>
    private static FighterSimulation LaunchedSimulation(int seed) => new(
        Loadout(FighterAbilityLoadout.Default with {
            SpecialOneExecutionType = 0,
            SpecialOneHit = new FighterAbilityHitData {
                HitstunFrames = 40,
                BlockClass = (int)BlockClass.Special,
                Launches = true,
                Delivery = (int)HitDelivery.DirectHit,
                Origin = (int)HitOrigin.Special
            }
        }),
        FighterLoadout.Default(FighterCharacterID.Joan),
        seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);

    /// <summary>The Default kit's numbers with the given ability modes.</summary>
    private static FighterLoadout Loadout(FighterAbilityLoadout modes) => new(
        (int)FighterCharacterID.Einstein,
        100, 3, 2,
        10, 14, 12, 20,
        600, 600,
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

    private static PlayerInputFrame Frame(int tick, GameplayButtons buttons) => new() {
        Tick = (uint)tick,
        Held = buttons,
        Pressed = buttons
    };
}
