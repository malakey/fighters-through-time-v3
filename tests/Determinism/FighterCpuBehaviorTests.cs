using System.Collections.Generic;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 6 A3: the CPU behaviours the deterministic table gained beyond
/// approach/attack — off-stage recovery, Chronal Orb pursuit, stage-hazard
/// evasion, per-band kit gating, and the button-edge pulse that makes repeated
/// actions possible at all.
/// </summary>
/// <remarks>
/// Pure C#: no Godot runtime, no simulation instance. The decision table reads a
/// <see cref="CpuDecisionObservation"/>, so every scenario here is a hand-built
/// observation plus, where the world matters, a scripted
/// <see cref="ICpuWorldObserver"/>. Band rates come from
/// <c>design-godot.md</c> §10 "Difficulty Settings &amp; Behavior Matrices".
/// </remarks>
[TestSuite]
public class FighterCpuBehaviorTests {
    private const int Ticks = 600;

    // === Per-band tuning matrix ===

    [TestCase]
    public void BandTuningMatchesTheDesignDifficultyMatrices() {
        // Easy: walks and jabs. No neutral specials, no movement ability, no
        // Ultimate, no orb pathing, no hazard reaction, 10% shield.
        CpuBandTuning easy = CpuBandTuning.Easy;
        AssertThat(easy.SpecialOneClosePercent).IsEqual(0);
        AssertThat(easy.SpecialOneRangedPercent).IsEqual(0);
        AssertThat(easy.SpecialTwoPercent).IsEqual(0);
        AssertThat(easy.MovementAbilityPercent).IsEqual(0);
        AssertThat(easy.UltimatePercent).IsEqual(0);
        AssertThat(easy.ApproachJumpPercent).IsEqual(0);
        AssertThat(easy.OrbPursuitPercent).IsEqual(0);
        AssertThat(easy.HazardAvoidPercent).IsEqual(0);
        AssertThat(easy.BlockPercent).IsEqual(10);
        // F19 (Package 11 A9b) rewrote the recovery row. Easy recovers with its
        // remaining jumps plus AT MOST ONE movement-ability activation per
        // offstage episode, and "Specials remain disabled on Easy" — including
        // off-stage, which is the exact inverse of the shipped ladder (Special 2
        // at 55%, the movement ability never). It uses neither grab nor Echo Step.
        AssertThat(easy.RecoveryJumpPercent > 0).IsTrue();
        AssertThat(easy.RecoveryMovementPercent > 0).IsTrue();
        AssertThat(easy.RecoveryMovementActivationsPerEpisode).IsEqual(1);
        AssertThat(easy.RecoveryMobilitySpecialPercent).IsEqual(0);
        AssertThat(easy.PlansMultiActionRecovery).IsFalse();
        AssertThat(easy.GrabAdmissionPercent).IsEqual(0);
        AssertThat(easy.EchoStepAdmissionPercent).IsEqual(0);

        // design :3161 Easy: "Rarely blocks projectiles" — below even the 10%
        // standard shield rate, and never a blink.
        AssertThat(easy.ProjectileBlockPercent > 0).IsTrue();
        AssertThat(easy.ProjectileBlockPercent < easy.BlockPercent).IsTrue();
        AssertThat(easy.ProjectileBlinkPercent).IsEqual(0);
        AssertThat(easy.ShieldOrbPursuitPercent).IsEqual(0);

        CpuBandTuning normal = CpuBandTuning.Normal;
        AssertThat(normal.BlockPercent).IsEqual(40);
        AssertThat(normal.OrbPursuitPercent).IsEqual(25);
        AssertThat(normal.HealingOrbHPPercent).IsEqual(40);
        AssertThat(normal.UltimatePercent).IsEqual(100);
        AssertThat(normal.RequiresUltimateSetup).IsFalse();
        AssertThat(normal.AvoidsHazardWarning).IsFalse();
        AssertThat(normal.HazardAvoidPercent > 0).IsTrue();
        // design :3175 Medium: blocks projectiles at the standard 40% shield rate,
        // but only "if they are far enough away" for its reflex window.
        AssertThat(normal.ProjectileBlockPercent).IsEqual(40);
        AssertThat(normal.ProjectileBlockMinRangeRaw > 0).IsTrue();
        AssertThat(normal.ProjectileBlinkPercent).IsEqual(0);
        AssertThat(normal.ShieldOrbPursuitPercent).IsEqual(0);
        // F19 Medium: the movement ability is the DEFAULT recovery tool, one
        // activation per episode, no multi-ability chains. CPU_COMBAT_POLICY.md's
        // provisional 25% admission for both verbs, and the immediate-Ultimate
        // policy still beats Echo Step when both are legal in one decision.
        AssertThat(normal.RecoveryMovementPercent > 0).IsTrue();
        AssertThat(normal.RecoveryMovementActivationsPerEpisode).IsEqual(1);
        AssertThat(normal.PlansMultiActionRecovery).IsFalse();
        AssertThat(normal.GrabAdmissionPercent).IsEqual(25);
        AssertThat(normal.EchoStepAdmissionPercent).IsEqual(25);
        AssertThat(normal.UltimateBeatsEchoStep).IsTrue();
        AssertThat(normal.ScoresGrabTactically).IsFalse();
        AssertThat(normal.ReservesMeterForDefy).IsFalse();

        CpuBandTuning hard = CpuBandTuning.Hard;
        AssertThat(hard.BlockPercent).IsEqual(80);
        AssertThat(hard.OrbPursuitPercent).IsEqual(75);
        AssertThat(hard.HealingOrbHPPercent).IsEqual(50);
        AssertThat(hard.RequiresUltimateSetup).IsTrue();
        AssertThat(hard.AvoidsHazardWarning).IsTrue();
        AssertThat(hard.HazardAvoidPercent).IsEqual(100);
        // design :3186 Hard: blinks through player projectiles with the movement
        // ability, shields at 80% otherwise, at any range.
        AssertThat(hard.ProjectileBlinkPercent > 0).IsTrue();
        AssertThat(hard.ProjectileBlockPercent).IsEqual(80);
        AssertThat(hard.ProjectileBlockMinRangeRaw).IsEqual(0L);
        // design :3191 Hard: shield orbs when the opponent's meter is near full.
        AssertThat(hard.ShieldOrbPursuitPercent).IsEqual(95);
        AssertThat(hard.ShieldOrbTargetMeterFloor).IsEqual(85);
        // F19 Hard: route comparison with no fixed script and no per-episode
        // activation cap (the ability's own cooldown is the constraint), both
        // verbs scored rather than rolled flat, and full meter behind an unused
        // Defy carries reserve value against the 30-meter Echo Step spend.
        AssertThat(hard.PlansMultiActionRecovery).IsTrue();
        AssertThat(hard.RecoveryMovementActivationsPerEpisode).IsEqual(0);
        AssertThat(hard.ScoresGrabTactically).IsTrue();
        AssertThat(hard.GrabAdmissionPercent < 100).IsTrue();
        AssertThat(hard.EchoStepAdmissionPercent < 100).IsTrue();
        AssertThat(hard.ReservesMeterForDefy).IsTrue();
        AssertThat(hard.UltimateBeatsEchoStep).IsFalse();

        AssertThat(CpuBandTuning.For(CpuDifficulty.Easy).BlockPercent).IsEqual(10);
        AssertThat(CpuBandTuning.For(CpuDifficulty.Normal).BlockPercent).IsEqual(40);
        AssertThat(CpuBandTuning.For(CpuDifficulty.Hard).BlockPercent).IsEqual(80);
    }

    // === Button-edge pulse ===

    [TestCase]
    public void RepeatedIdenticalDecisionsStillProduceRepeatedPressedEdges() {
        // The old controller latched the chosen buttons indefinitely, so a CPU that
        // kept choosing "attack" produced exactly one Pressed edge for the whole
        // match and could never double jump or re-attack.
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 77);
        CpuDecisionObservation observation = Neutral(selfX: 0, targetX: 1);
        int basicEdges = CountEdges(cpu, observation, GameplayButtons.BasicAttack, Ticks);
        AssertThat(basicEdges > 5).IsTrue();
    }

    [TestCase]
    public void EveryEdgeButtonIsReleasedBeforeItIsPressedAgain() {
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 4242);
        CpuDecisionObservation observation = Neutral(selfX: 0, targetX: 1);
        PlayerInputFrame previous = default;
        bool basicHeldLastFrame = false;
        for (uint tick = 0; tick < Ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            bool held = frame.IsHeld(GameplayButtons.BasicAttack);
            // A Pressed edge may never appear while the button was already held.
            if (frame.IsPressed(GameplayButtons.BasicAttack)) AssertThat(basicHeldLastFrame).IsFalse();
            basicHeldLastFrame = held;
            previous = frame;
        }
    }

    /// <summary>
    /// 1.1(5) (2026-10-16, ledger <c>FIX-CPU-JUMP-HOLD</c>): Jump used to pulse for
    /// two ticks like every edge button. Now the CPU keeps it held after the pulse
    /// until the fighter falls at 2 u/s or faster (just past the apex hang), so a
    /// CPU jump is a full hop. One grounded approach decision at tick 0 queues
    /// the only jump; from tick 1 the hand-built fighter is airborne, rising at
    /// 8 u/s until the press and then decelerating by 0.5 u/s per tick.
    /// </summary>
    [TestCase]
    public void JumpStaysHeldAfterItsPulseUntilTheFighterFallsAtTwoUnitsPerSecond() {
        CpuBandTuning tuning = CpuBandTuning.Hard with {
            ApproachJumpPercent = 100,
            SpecialOneRangedPercent = 0,
            MovementAbilityPercent = 0
        };
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 77, geometry: null, world: null, tuningOverride: tuning);
        CpuDecisionObservation observation = Neutral(selfX: 0, targetX: 9);
        PlayerInputFrame previous = default;
        int firstPress = -1;
        int releaseTick = -1;
        double velocityY = 8.0;
        for (uint tick = 0; tick < 120 && releaseTick < 0; tick++) {
            if (tick == 1) observation.IsGrounded = 0;
            if (firstPress >= 0) velocityY -= 0.5;
            observation.SelfVelocityYRaw = FP64.FromDouble(velocityY).RawValue;
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.Jump) && firstPress < 0) {
                firstPress = (int)tick;
                continue;
            }
            if (firstPress < 0) continue;
            if (!frame.IsHeld(GameplayButtons.Jump)) {
                releaseTick = (int)tick;
                // The release is the tick the observed fall first reaches 2 u/s.
                AssertThat(velocityY <= -2.0)
                    .OverrideFailureMessage($"Jump was released at {velocityY:F1} u/s, before the fall reached 2 u/s.")
                    .IsTrue();
                AssertThat(velocityY > -2.5)
                    .OverrideFailureMessage($"Jump was still held at {velocityY + 0.5:F1} u/s, past the release speed.")
                    .IsTrue();
            }
        }
        AssertThat(firstPress >= 0).OverrideFailureMessage("Harness: the CPU never pressed Jump.").IsTrue();
        AssertThat(releaseTick > firstPress + 2)
            .OverrideFailureMessage($"Jump pressed at {firstPress} was released at {releaseTick}: still a two-tick pulse.")
            .IsTrue();
    }

    /// <summary>
    /// 1.1(5): the sustained hold must not swallow a second jump. A recovering
    /// CPU hanging at its apex (vertical speed 0: recovery jumps only from a
    /// non-rising state, and the hold ends only at a 2 u/s fall) queues a jump
    /// on every decision; each later press still reaches the simulation as its
    /// own edge, with exactly one released tick in front of it and Jump held
    /// throughout before that.
    /// </summary>
    [TestCase]
    public void TwoQueuedJumpsStillProduceTwoPressedEdgesWithOneReleaseTickBetween() {
        foreach (CpuDifficulty band in Bands()) {
            var cpu = new FighterCpuController(band, 9001);
            CpuDecisionObservation observation = OffStage(selfX: -8, selfY: -1);
            // At the apex: the sustained hold has no reason to end before the next press.
            observation.SelfVelocityYRaw = 0;
            observation.RemainingJumps = 2;
            PlayerInputFrame previous = default;
            var presses = new List<int>();
            var held = new List<bool>();
            for (uint tick = 0; tick < Ticks && presses.Count < 2; tick++) {
                PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
                previous = frame;
                held.Add(frame.IsHeld(GameplayButtons.Jump));
                if (frame.IsPressed(GameplayButtons.Jump)) presses.Add((int)tick);
            }
            AssertThat(presses.Count)
                .OverrideFailureMessage($"{band} produced {presses.Count} Jump edge(s); the hold swallowed the second.")
                .IsEqual(2);
            int first = presses[0];
            int second = presses[1];
            AssertThat(held[second - 1])
                .OverrideFailureMessage($"{band}: Jump was held on the tick before its second press.")
                .IsFalse();
            for (int tick = first; tick <= second - 2; tick++) {
                AssertThat(held[tick])
                    .OverrideFailureMessage($"{band}: Jump dropped at tick {tick}, between the press at {first} and the release at {second - 1}.")
                    .IsTrue();
            }
        }
    }

    // === Off-stage recovery ===

    [TestCase]
    public void OffStageRecoverySteersTowardCentreAndSpendsJumpsOnEveryBand() {
        foreach (CpuDifficulty band in Bands()) {
            var cpu = new FighterCpuController(band, 9001);
            CpuDecisionObservation observation = OffStage(selfX: -8, selfY: -1);
            PlayerInputFrame previous = default;
            int jumpEdges = 0;
            for (uint tick = 0; tick < Ticks; tick++) {
                PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
                previous = frame;
                if (frame.IsPressed(GameplayButtons.Jump)) jumpEdges++;
                // Never steer further out, and never stop to fight while falling.
                AssertThat(frame.MoveX >= 0).IsTrue();
                AssertThat(frame.IsHeld(GameplayButtons.BasicAttack)).IsFalse();
                AssertThat(frame.IsHeld(GameplayButtons.Block)).IsFalse();
            }
            // Repeated edges are the proof the held-latch fix works: without it a
            // fighter gets one jump per airtime and can never chain a recovery.
            AssertThat(jumpEdges > 1).IsTrue();
        }
    }

    [TestCase]
    public void CommandedRecoveryClimbsBackOverTheFloorWithinTheNormalAndHardReflexWindows() {
        AssertThat(SimulateRecovery(CpuDifficulty.Hard, seed: 5150)).IsTrue();
        AssertThat(SimulateRecovery(CpuDifficulty.Normal, seed: 5150)).IsTrue();
        // Normal survives this depth under the 2026-10-08 heavier fall only because
        // a stale recovery jump no longer shadows its movement ability (the next
        // test pins that). Easy is deliberately not asserted to survive. Its
        // 30–45-frame reflex window costs more than half a second, and under the
        // simulation's fall gravity (−30 u/s² × 2.2, capped at 12 u/s since
        // 2026-10-08) a fighter that has crossed the floor plane is already past
        // the −5 blast zone by then. design-godot.md §10 says as much for Easy
        // ("No active recovery attempts... simply falls"); the CPU still commands
        // the recovery, which OffStageRecoverySteersTowardCentreAndSpendsJumpsOnEveryBand
        // pins.
    }

    [TestCase]
    public void ARecoveryJumpArrivingAfterTheJumpsAreSpentIsDroppedAndTheMovementAbilityStillFires() {
        // Every decision carries the full reaction delay, so the jumps a falling
        // CPU queues before its first press is observed keep arriving after the
        // budget is spent. Delivery revalidates them on self-state, as
        // CPU_RECOVERY.md requires before each planned action: an empty jump is
        // dropped instead of taking the edge pulse. Pressed, it could overwrite
        // a pending movement-ability edge, which on Easy and Normal is the one
        // activation the episode allows. The 2026-10-08 heavier fall turned
        // that into a lost stock in the commanded-recovery case above.
        foreach (CpuDifficulty band in Bands()) {
            var cpu = new FighterCpuController(band, 5150);
            // The observation stays "falling" throughout, the worst case for
            // stale jumps: every decision before the first press picks one.
            CpuDecisionObservation observation = OffStage(selfX: -8, selfY: -1);
            observation.RemainingJumps = 2;
            PlayerInputFrame previous = default;
            int jumpPresses = 0;
            int upwardMovementPresses = 0;
            for (uint tick = 0; tick < Ticks; tick++) {
                PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
                previous = frame;
                if (frame.IsPressed(GameplayButtons.Jump)) {
                    jumpPresses++;
                    if (observation.RemainingJumps > 0) observation.RemainingJumps--;
                }
                if (frame.IsPressed(GameplayButtons.MovementAbility) && frame.MoveY < -30) {
                    upwardMovementPresses++;
                }
            }
            AssertThat(jumpPresses)
                .OverrideFailureMessage($"{band} pressed Jump {jumpPresses} times on a two-jump budget.")
                .IsEqual(2);
            AssertThat(upwardMovementPresses > 0)
                .OverrideFailureMessage($"{band} never delivered its movement ability after the jumps.")
                .IsTrue();
        }
    }

    [TestCase]
    public void EveryBandRecoversWithItsMovementAbilityAndOnlyEasyAndMediumAreCappedPerEpisode() {
        // F19 / CPU_RECOVERY.md, Package 11 A9b. This test replaces
        // OnlyHardChainsAnUpwardMovementAbilityIntoItsRecovery, which pinned the
        // inverted ladder: it asserted Easy and Medium NEVER press the movement
        // ability, when the contract makes it Easy's one permitted recovery tool
        // and Medium's default. Jumps are spent first by every band, so the
        // scenario starts with none left.
        CpuDecisionObservation exhausted = OffStage(selfX: -8, selfY: -1);
        exhausted.RemainingJumps = 0;

        foreach (CpuDifficulty band in Bands()) {
            var cpu = new FighterCpuController(band, 31337);
            PlayerInputFrame previous = default;
            int upwardMovementEdges = 0;
            for (uint tick = 0; tick < Ticks; tick++) {
                PlayerInputFrame frame = cpu.Sample(tick, in exhausted, in previous);
                previous = frame;
                // The stick must aim up (world Y is up, the axis is Y-down) or a
                // directional warp travels sideways off the stage.
                if (frame.IsPressed(GameplayButtons.MovementAbility) && frame.MoveY < -30) {
                    upwardMovementEdges++;
                }
            }
            AssertThat(upwardMovementEdges > 0)
                .OverrideFailureMessage($"{band} must attempt its movement ability off-stage.")
                .IsTrue();
            // Easy and Medium are capped at one activation for the whole episode;
            // the fighter never lands in this static observation, so the episode
            // never ends and the cap never resets. Hard has no planning cap.
            int expectedCap = band == CpuDifficulty.Hard ? int.MaxValue : 1;
            AssertThat(cpu.EpisodeMovementActivations <= expectedCap)
                .OverrideFailureMessage(
                    $"{band} spent {cpu.EpisodeMovementActivations} activations in one episode.")
                .IsTrue();
        }
    }

    [TestCase]
    public void EasyNeverPressesASpecialOnStageOrOffIt() {
        // F19 replaces design §10's old "Special 2 is only triggered when the AI
        // is off-stage" line outright: "Specials remain disabled on Easy". This
        // test replaces EasyUsesSpecialTwoOffStageAndNeverInNeutral, which pinned
        // exactly the behaviour the contract retires.
        var recovering = new FighterCpuController(
            CpuDifficulty.Easy, 606);
        CpuDecisionObservation offStage = OffStage(selfX: -8, selfY: -1);
        AssertThat(CountEdges(recovering, offStage, GameplayButtons.Special2, Ticks)).IsEqual(0);
        AssertThat(CountEdges(
            new FighterCpuController(CpuDifficulty.Easy, 606),
            offStage, GameplayButtons.Special1, Ticks)).IsEqual(0);

        foreach (int targetX in new[] { 1, 4, 8 }) {
            var neutral = new FighterCpuController(CpuDifficulty.Easy, 606);
            AssertThat(CountEdges(neutral, Neutral(0, targetX), GameplayButtons.Special2, Ticks)).IsEqual(0);
        }
    }

    [TestCase]
    public void AStoryStyleObservationWithoutStageBoundsNeverEntersRecovery() {
        // The Mirror Paradox adapter leaves HasStageBounds at 0. A campaign level's
        // floor is nowhere near y = 0, so a negative Y must not read as "off-stage".
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 12);
        CpuDecisionObservation observation = Neutral(selfX: 0, targetX: 1);
        observation.SelfPositionYRaw = FP64.FromInt(-40).RawValue;
        observation.IsGrounded = 0;
        AssertThat(CountEdges(cpu, observation, GameplayButtons.BasicAttack, Ticks) > 0).IsTrue();
    }

    // === Chronal Orb pursuit ===

    [TestCase]
    public void OrbsArePursuedByNormalAndHardAndIgnoredByEasy() {
        // The opponent sits left of the CPU and the orb sits right, so the two
        // intents are distinguishable by the sign of the movement axis alone.
        AssertThat(CountOrbPursuitFrames(CpuDifficulty.Hard) > 0).IsTrue();
        AssertThat(CountOrbPursuitFrames(CpuDifficulty.Normal) > 0).IsTrue();
        AssertThat(CountOrbPursuitFrames(CpuDifficulty.Easy)).IsEqual(0);
    }

    [TestCase]
    public void AWoundedHardCpuChasesHealingOrbsHarderThanFullHealth() {
        int healthy = CountOrbPursuitFrames(CpuDifficulty.Hard, selfHP: 100, orbEffect: 0);
        int wounded = CountOrbPursuitFrames(CpuDifficulty.Hard, selfHP: 20, orbEffect: 0);
        AssertThat(wounded >= healthy).IsTrue();
        AssertThat(wounded > 0).IsTrue();
    }

    [TestCase]
    public void OrbsBeyondAwarenessRangeAreNotPursued() {
        AssertThat(CountOrbPursuitFrames(CpuDifficulty.Hard, orbX: 12)).IsEqual(0);
    }

    [TestCase]
    public void HardPrioritizesShieldOrbsWhenTheOpponentsMeterIsNearFull() {
        // design :3191: "shield orbs when the opponent's Ultimate meter is near
        // full". Aegis is EffectType 3; the floor is 85 on the 0–100 meter.
        int nearFull = CountOrbPursuitFrames(CpuDifficulty.Hard, orbEffect: 3, targetMeter: 90);
        int low = CountOrbPursuitFrames(CpuDifficulty.Hard, orbEffect: 3, targetMeter: 50);
        AssertThat(nearFull > 0).IsTrue();
        // The priority rate (95%) can only raise the pursuit count over the base
        // 75% rate the low-meter run rolls against.
        AssertThat(nearFull >= low).IsTrue();
        // The priority is Hard-only: Normal keeps its base 25% and Easy still
        // ignores orbs entirely even against a near-full opponent.
        AssertThat(CountOrbPursuitFrames(CpuDifficulty.Easy, orbEffect: 3, targetMeter: 90)).IsEqual(0);
    }

    // === Projectile defense (M-8) ===

    [TestCase]
    public void EasyRarelyBlocksProjectilesWhileNormalAndHardShieldAtTheirDesignRates() {
        // design :3161/:3175/:3186. Movement on cooldown so Hard cannot blink and
        // must fall back to the shield.
        int easy = CountProjectileBlockFrames(CpuDifficulty.Easy, relativeX: 4);
        int normal = CountProjectileBlockFrames(CpuDifficulty.Normal, relativeX: 4);
        int hard = CountProjectileBlockFrames(CpuDifficulty.Hard, relativeX: 4);
        AssertThat(normal > 0).IsTrue();
        AssertThat(hard > 0).IsTrue();
        // "Rarely": far below the bands that actually defend.
        AssertThat(easy < normal).IsTrue();
        AssertThat(easy < hard).IsTrue();
    }

    [TestCase]
    public void NormalOnlyCommitsToTheProjectileBlockWhenTheShotIsFarEnoughAway() {
        // design :3175 Medium: "will attempt to block projectiles if they are far
        // enough away" — a shot already inside the 2.5-unit reflex range is not
        // reacted to, while Hard blocks at any range.
        AssertThat(CountProjectileBlockFrames(CpuDifficulty.Normal, relativeX: 1.5)).IsEqual(0);
        AssertThat(CountProjectileBlockFrames(CpuDifficulty.Normal, relativeX: 4) > 0).IsTrue();
        AssertThat(CountProjectileBlockFrames(CpuDifficulty.Hard, relativeX: 1.5) > 0).IsTrue();
    }

    [TestCase]
    public void OnlyHardBlinksThroughAnIncomingProjectileWhenItsMovementAbilityIsReady() {
        // design :3186 Hard: "blinking through player projectiles" with the
        // movement ability. Target close enough (3 < FarRange) that neutral combat
        // can never press MovementAbility itself, so every edge here is the blink.
        AssertThat(CountProjectileMovementEdges(CpuDifficulty.Hard) > 0).IsTrue();
        AssertThat(CountProjectileMovementEdges(CpuDifficulty.Normal)).IsEqual(0);
        AssertThat(CountProjectileMovementEdges(CpuDifficulty.Easy)).IsEqual(0);
    }

    [TestCase]
    public void HardFallsBackToTheShieldWhenTheMovementAbilityIsOnCooldown() {
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 424242);
        CpuDecisionObservation observation = IncomingProjectile(
            Neutral(selfX: 0, targetX: -3), relativeX: 4, movementCooldown: 300);
        PlayerInputFrame previous = default;
        int blinkEdges = 0;
        int blockFrames = 0;
        for (uint tick = 0; tick < Ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.MovementAbility)) blinkEdges++;
            if (frame.IsHeld(GameplayButtons.Block)) blockFrames++;
        }
        AssertThat(blinkEdges).IsEqual(0);
        AssertThat(blockFrames > 0).IsTrue();
    }

    [TestCase]
    public void AProjectileFlyingAwayOrPassingOverheadIsIgnored() {
        // A shot that already passed (receding) and one sailing well above the
        // fighter's band are no threat: no block, no blink, combat continues.
        foreach (bool overhead in new[] { false, true }) {
            var cpu = new FighterCpuController(CpuDifficulty.Hard, 987);
            CpuDecisionObservation observation = Neutral(selfX: 0, targetX: 1);
            observation.HasHostileProjectile = 1;
            observation.ProjectileRelativeXRaw = FP64.FromInt(3).RawValue;
            observation.ProjectileRelativeYRaw = overhead
                ? FP64.FromInt(4).RawValue : FP64.Zero.RawValue;
            // Receding when level; closing when testing the overhead miss.
            observation.ProjectileVelocityXRaw = overhead
                ? FP64.FromInt(-10).RawValue : FP64.FromInt(10).RawValue;
            PlayerInputFrame previous = default;
            int attacks = 0;
            for (uint tick = 0; tick < Ticks; tick++) {
                PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
                previous = frame;
                AssertThat(frame.IsHeld(GameplayButtons.Block)).IsFalse();
                AssertThat(frame.IsPressed(GameplayButtons.MovementAbility)).IsFalse();
                if (frame.IsPressed(GameplayButtons.BasicAttack)) attacks++;
            }
            AssertThat(attacks > 0).IsTrue();
        }
    }

    // === Stage hazard evasion ===

    [TestCase]
    public void HardVacatesATelegraphedHazardDuringTheWarningPhase() {
        AssertThat(CountHazardEscapeFrames(CpuDifficulty.Hard, hazardPhase: 0) > 0).IsTrue();
    }

    [TestCase]
    public void NormalIgnoresTheWarningAndOnlyLeavesOnceTheHazardIsDamaging() {
        // design §10 Medium: "Reacts to active hazard zones (after the warning phase
        // ends and damage begins)... does not preemptively avoid warning indicators."
        AssertThat(CountHazardEscapeFrames(CpuDifficulty.Normal, hazardPhase: 0)).IsEqual(0);
        AssertThat(CountHazardEscapeFrames(CpuDifficulty.Normal, hazardPhase: 1) > 0).IsTrue();
    }

    [TestCase]
    public void EasyWalksIntoHazardsInBothPhases() {
        // design §10 Easy: "Does not react to stage hazard warning indicators. Will
        // walk into active hazard zones and take damage freely."
        AssertThat(CountHazardEscapeFrames(CpuDifficulty.Easy, hazardPhase: 0)).IsEqual(0);
        AssertThat(CountHazardEscapeFrames(CpuDifficulty.Easy, hazardPhase: 1)).IsEqual(0);
    }

    [TestCase]
    public void HardKeepsFightingWhenAHazardsDangerRegionSpansTheWholeStage() {
        // M-9: the Globe heckle authors HalfExtents (10,10) against ±9 walls, so no
        // escape band exists anywhere. The heckle punishes only idling; the old
        // blanket flee made Hard stop fighting and wall-ping-pong for ~11.5 s of
        // every cycle. With no escape room, evasion must yield to normal combat —
        // in both the warning and the active phase.
        foreach (int phase in new[] { 0, 1 }) {
            var cpu = new FighterCpuController(CpuDifficulty.Hard, 1010);
            CpuDecisionObservation observation = Neutral(selfX: 0, targetX: 1);
            observation.HasStageBounds = 1;
            observation.LeftWallRaw = FP64.FromInt(-9).RawValue;
            observation.RightWallRaw = FP64.FromInt(9).RawValue;
            observation.HasHazard = 1;
            observation.HazardPhase = phase;
            observation.HazardPositionXRaw = FP64.Zero.RawValue;
            observation.HazardHalfWidthRaw = FP64.FromInt(10).RawValue;

            PlayerInputFrame previous = default;
            int attackEdges = 0;
            for (uint tick = 0; tick < Ticks; tick++) {
                PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
                previous = frame;
                if (frame.IsPressed(GameplayButtons.BasicAttack)) attackEdges++;
                // Close combat holds position at this range; any full-tilt ±127
                // steering could only be the abolished flee response.
                AssertThat(frame.MoveX).IsEqual((sbyte)0);
            }
            AssertThat(attackEdges > 0).IsTrue();
        }
    }

    [TestCase]
    public void NarrowHazardEvasionSurvivesTheFullStageRegionFix() {
        // Regression guard for the §9 A3 logged behaviours: the Orléans/Paris/
        // Vesuvius-style narrow bands must still be fled exactly as before now
        // that a stage-spanning region is exempt.
        AssertThat(CountHazardEscapeFrames(CpuDifficulty.Hard, hazardPhase: 0) > 0).IsTrue();
        AssertThat(CountHazardEscapeFrames(CpuDifficulty.Normal, hazardPhase: 1) > 0).IsTrue();
    }

    [TestCase]
    public void HazardEvasionCutsThroughRatherThanFleeingIntoAWall() {
        // Cornered against the right wall with the hazard to the left: fleeing right
        // would trap the fighter inside the damaging band.
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 8080);
        CpuDecisionObservation observation = Neutral(selfX: 0, targetX: 0);
        observation.SelfPositionXRaw = FP64.FromDouble(8.5).RawValue;
        observation.LeftWallRaw = FP64.FromInt(-9).RawValue;
        observation.RightWallRaw = FP64.FromInt(9).RawValue;
        observation.HasStageBounds = 1;
        observation.HasHazard = 1;
        observation.HazardPhase = 0;
        observation.HazardPositionXRaw = FP64.FromInt(7).RawValue;
        observation.HazardHalfWidthRaw = FP64.FromInt(3).RawValue;

        PlayerInputFrame previous = default;
        int leftward = 0;
        for (uint tick = 0; tick < Ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.MoveX == -127) leftward++;
        }
        AssertThat(leftward > 0).IsTrue();
    }

    // === Ranged kit ===

    [TestCase]
    public void MovementAbilityFiresInRangedNeutral() {
        // Historical: it used to sit behind the (since-removed universal) dash
        // in an else-if chain and was effectively starved.
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 2718);
        CpuDecisionObservation observation = Neutral(selfX: 0, targetX: 8);
        observation.SpecialOneCooldownFrames = 120;
        AssertThat(CountEdges(cpu, observation, GameplayButtons.MovementAbility, Ticks) > 0).IsTrue();
    }

    [TestCase]
    public void NormalZonesWithSpecialOneAtRangeWhereItPreviouslyNeverCould() {
        // Special 1 used to be Hard-only in every branch; design §10 Medium uses it
        // "when at medium-to-long distance (cooldown permitting)".
        var cpu = new FighterCpuController(CpuDifficulty.Normal, 1618);
        AssertThat(CountEdges(cpu, Neutral(0, 7), GameplayButtons.Special1, Ticks) > 0).IsTrue();
        var easy = new FighterCpuController(CpuDifficulty.Easy, 1618);
        AssertThat(CountEdges(easy, Neutral(0, 7), GameplayButtons.Special1, Ticks)).IsEqual(0);
    }

    // === Match gating ===

    [TestCase]
    public void ANonLiveMatchStateSuppressesEveryGameplayButtonAndAllMovement() {
        // Robustness for the countdown / respawn-platform phases: any state the
        // world observer reports as not live must produce an inert pad.
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 55);
        CpuDecisionObservation observation = Neutral(selfX: 0, targetX: 1);
        observation.SuppressGameplayInput = 1;
        PlayerInputFrame previous = default;
        for (uint tick = 0; tick < 240; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            AssertThat(frame.Held).IsEqual(GameplayButtons.None);
            AssertThat(frame.MoveX).IsEqual((sbyte)0);
            AssertThat(frame.MoveY).IsEqual((sbyte)0);
        }
    }

    // === Observation projection ===

    [TestCase]
    public void GeometryBackedObservationCarriesBoundsAndTheNearestPlatformSummary() {
        FighterStageGeometry geometry = FighterStageGeometry.Florence;
        var self = new FighterStateComponent {
            Stocks = 3, CurrentHP = 80, MaxHP = 100, RemainingJumps = 1,
            Position = new FPVector2(FP64.FromDouble(3.5), FP64.FromInt(2)),
            Velocity = new FPVector2(FP64.FromInt(1), FP64.FromInt(-4))
        };
        var target = new FighterStateComponent { CurrentHP = 30, MaxHP = 100, HitstunFrames = 5 };
        FighterRuntimeComponent selfRuntime = default;
        FighterRuntimeComponent targetRuntime = default;

        CpuDecisionObservation observation = FighterCpuController.Observe(
            in self, in selfRuntime, in target, in targetRuntime, geometry, null);

        AssertThat(observation.HasStageBounds).IsEqual(1);
        AssertThat(observation.LeftWallRaw).IsEqual(geometry.LeftWall.RawValue);
        AssertThat(observation.RightWallRaw).IsEqual(geometry.RightWall.RawValue);
        AssertThat(observation.CeilingRaw).IsEqual(geometry.Ceiling.RawValue);
        AssertThat(observation.BottomBlastZoneRaw).IsEqual(geometry.BottomBlastZone.RawValue);
        AssertThat(observation.PlatformCount).IsEqual(geometry.Platforms.Length);
        // Nearest of Florence's two gear platforms to x = 3.5 is the one at +4.
        AssertThat(observation.NearestPlatformCenterXRaw).IsEqual(FP64.FromInt(4).RawValue);
        AssertThat(observation.NearestPlatformSurfaceYRaw).IsEqual(FP64.FromDouble(2.4).RawValue);
        AssertThat(observation.SelfPositionYRaw).IsEqual(self.Position.y.RawValue);
        AssertThat(observation.SelfVelocityYRaw).IsEqual(self.Velocity.y.RawValue);
        AssertThat(observation.RemainingJumps).IsEqual(1);
        AssertThat(observation.SelfCurrentHP).IsEqual(80);
        AssertThat(observation.TargetCurrentHP).IsEqual(30);
        AssertThat(observation.TargetHitstunFrames).IsEqual(5);
        // No world observer: the orb/hazard/projectile blocks stay explicitly absent.
        AssertThat(observation.HasOrb).IsEqual(0);
        AssertThat(observation.HasHazard).IsEqual(0);
        AssertThat(observation.HasHostileProjectile).IsEqual(0);
        AssertThat(observation.SuppressGameplayInput).IsEqual(0);
    }

    [TestCase]
    public void TheWorldObserverFillsTheOrbAndHazardBlocksAndTheLiveGate() {
        var world = new ScriptedWorld {
            IsMatchLive = false,
            Orb = new FighterOrbComponent {
                EffectType = 2,
                Position = new FPVector2(FP64.FromInt(3), FP64.FromDouble(2.9))
            },
            Hazard = new FighterHazardComponent {
                Phase = 1,
                Position = new FPVector2(FP64.FromInt(-2), FP64.Zero),
                HalfExtents = new FPVector2(FP64.FromInt(2), FP64.FromInt(2))
            },
            Projectile = new FighterProjectileComponent {
                OwnerPlayerID = 0,
                Position = new FPVector2(FP64.FromInt(2), FP64.One),
                Velocity = new FPVector2(FP64.FromInt(-8), FP64.Zero)
            }
        };
        FighterStateComponent self = default;
        FighterRuntimeComponent selfRuntime = default;
        FighterStateComponent target = default;
        FighterRuntimeComponent targetRuntime = default;

        CpuDecisionObservation observation = FighterCpuController.Observe(
            in self, in selfRuntime, in target, in targetRuntime, null, world);

        AssertThat(observation.SuppressGameplayInput).IsEqual(1);
        AssertThat(observation.HasOrb).IsEqual(1);
        AssertThat(observation.OrbEffectType).IsEqual(2);
        AssertThat(observation.OrbPositionXRaw).IsEqual(FP64.FromInt(3).RawValue);
        AssertThat(observation.HasHazard).IsEqual(1);
        AssertThat(observation.HazardPhase).IsEqual(1);
        AssertThat(observation.HazardHalfWidthRaw).IsEqual(FP64.FromInt(2).RawValue);
        // The projectile block carries self-relative position and raw velocity.
        AssertThat(observation.HasHostileProjectile).IsEqual(1);
        AssertThat(observation.ProjectileRelativeXRaw).IsEqual(FP64.FromInt(2).RawValue);
        AssertThat(observation.ProjectileRelativeYRaw).IsEqual(FP64.One.RawValue);
        AssertThat(observation.ProjectileVelocityXRaw).IsEqual(FP64.FromInt(-8).RawValue);
        AssertThat(observation.ProjectileVelocityYRaw).IsEqual(FP64.Zero.RawValue);
        AssertThat(observation.HasStageBounds).IsEqual(0);
    }

    // === Determinism ===

    [TestCase]
    public void TheExpandedTableStaysBitIdenticalForTheSameSeedAndObservationStream() {
        var first = new FighterCpuController(CpuDifficulty.Hard, 20260808, FighterStageGeometry.Florence, null);
        var second = new FighterCpuController(CpuDifficulty.Hard, 20260808, FighterStageGeometry.Florence, null);
        PlayerInputFrame previousFirst = default;
        PlayerInputFrame previousSecond = default;
        for (uint tick = 0; tick < Ticks; tick++) {
            // A moving scenario so recovery, orbs, and hazards all get exercised.
            CpuDecisionObservation observation = Neutral(0, 1);
            observation.SelfPositionXRaw = FP64.FromInt((int)(tick % 17) - 8).RawValue;
            observation.SelfPositionYRaw = FP64.FromInt((int)(tick % 5) - 2).RawValue;
            observation.IsGrounded = tick % 3 == 0 ? 1 : 0;
            observation.RemainingJumps = (int)(tick % 3);
            observation.HasOrb = 1;
            observation.OrbPositionXRaw = FP64.FromInt(4).RawValue;
            observation.HasHazard = 1;
            observation.HazardPhase = (int)(tick % 2);
            observation.HazardHalfWidthRaw = FP64.FromInt(2).RawValue;

            PlayerInputFrame firstFrame = first.Sample(tick, in observation, in previousFirst);
            PlayerInputFrame secondFrame = second.Sample(tick, in observation, in previousSecond);
            AssertThat(firstFrame.Equals(secondFrame)).IsTrue();
            previousFirst = firstFrame;
            previousSecond = secondFrame;
        }
    }

    [TestCase]
    public void DifferentSeedsDivergeSoTheMatchSeedDerivationActuallyMatters() {
        var first = new FighterCpuController(CpuDifficulty.Hard, 1);
        var second = new FighterCpuController(CpuDifficulty.Hard, 2);
        CpuDecisionObservation observation = Neutral(0, 4);
        PlayerInputFrame previousFirst = default;
        PlayerInputFrame previousSecond = default;
        bool diverged = false;
        for (uint tick = 0; tick < Ticks; tick++) {
            PlayerInputFrame firstFrame = first.Sample(tick, in observation, in previousFirst);
            PlayerInputFrame secondFrame = second.Sample(tick, in observation, in previousSecond);
            if (!firstFrame.Equals(secondFrame)) diverged = true;
            previousFirst = firstFrame;
            previousSecond = secondFrame;
        }
        AssertThat(diverged).IsTrue();
    }

    // === CM-CANCEL (combat and movement update 2026-10-16, decision 9.3): the CPU's own Special cast ===

    /// <summary>
    /// Plan §2.9.3: since decision 1.2 a Special cast discards its caster's
    /// presses until its recovery ends, so the CPU reads its own cast
    /// (<see cref="CpuDecisionObservation.SelfSpecialCastPhase"/>) before it
    /// schedules a grab or Echo Step. With both admissions forced to 100 %, a
    /// grab opportunity and a whiffed swing's recovery produce no chord while a
    /// cast runs (startup, active or recovery), and the grab opportunity is never
    /// opened, so no admission is rolled or spent on it; the moment the cast ends,
    /// the same continuing opportunity is the CPU's first and it acts on it.
    /// </summary>
    [TestCase]
    public void NoGrabOrEchoStepIsScheduledDuringTheCpusOwnCastAndNoAdmissionIsSpent() {
        CpuBandTuning tuning = CpuBandTuning.Normal with {
            GrabAdmissionPercent = 100,
            EchoStepAdmissionPercent = 100,
            UltimatePercent = 0,
            SpecialOneClosePercent = 0,
            SpecialOneRangedPercent = 0,
            SpecialTwoPercent = 0
        };
        var failures = new List<string>();
        foreach ((string name, int phase, int frames) in CastPhases()) {
            // The grab: a blocking opponent half a unit in front.
            var grabber = new FighterCpuController(CpuDifficulty.Normal, 4401, null, null, tuning);
            CpuDecisionObservation opportunity = CastGrabOpportunity();
            CpuDecisionObservation casting = opportunity;
            casting.SelfSpecialCastPhase = phase;
            casting.SelfSpecialCastPhaseFrames = frames;
            uint tick = 0;
            PlayerInputFrame previous = default;
            int castChords = CountCastChords(grabber, casting, ref tick, ref previous, 120, grab: true);
            if (castChords != 0) failures.Add($"{name}: {castChords} grab chords during the cast");
            if (grabber.GrabOpportunityID != 0) {
                failures.Add($"{name}: the cast opened grab opportunity {grabber.GrabOpportunityID}");
            }
            int freeChords = CountCastChords(grabber, opportunity, ref tick, ref previous, 60, grab: true);
            if (freeChords == 0) failures.Add($"{name}: no grab once the cast ended");
            if (grabber.GrabOpportunityID != 1) {
                failures.Add($"{name}: the post-cast opportunity is #{grabber.GrabOpportunityID}, not the first");
            }

            // Echo Step: the recovery of the CPU's own whiffed swing.
            var stepper = new FighterCpuController(CpuDifficulty.Normal, 4402, null, null, tuning);
            CpuDecisionObservation whiff = CastWhiffRecovery();
            CpuDecisionObservation whiffCasting = whiff;
            whiffCasting.SelfSpecialCastPhase = phase;
            whiffCasting.SelfSpecialCastPhaseFrames = frames;
            tick = 0;
            previous = default;
            int castSteps = CountCastChords(stepper, whiffCasting, ref tick, ref previous, 120, grab: false);
            if (castSteps != 0) failures.Add($"{name}: {castSteps} Echo Step chords during the cast");
            if (CountCastChords(stepper, whiff, ref tick, ref previous, 60, grab: false) == 0) {
                failures.Add($"{name}: no Echo Step once the cast ended");
            }
        }
        if (failures.Count > 0) AssertThat(string.Join(" | ", failures)).IsEqual("");
    }

    /// <summary>
    /// Plan §2.9.3: a full-meter Hard CPU with a viable Ultimate (the opponent
    /// one unit away, grounded, in hitstun) waits out its own Special cast — here
    /// Splitting Strike's 14 / 6 / 22, accepted the tick before tick 0, with the
    /// 30-frame ability lockout it armed — and its Ultimate press arrives only
    /// once the cast has ended (tick 41 is the first tick the sim reads a press
    /// again: the cast's last recovery frame ends in that tick's movement pass).
    /// The same stream without the cast fields presses the Ultimate as soon as
    /// the lockout allows, inside the recovery, where the sim would discard it.
    /// </summary>
    [TestCase]
    public void AFullMeterHardUltimateWaitsOutItsOwnCastAndArrivesOnlyAfterIt() {
        CpuBandTuning tuning = CpuBandTuning.Hard with {
            UltimatePercent = 100,
            SpecialOneClosePercent = 0,
            SpecialOneRangedPercent = 0,
            SpecialTwoPercent = 0,
            GrabAdmissionPercent = 0,
            EchoStepAdmissionPercent = 0,
            EvasiveRollPercent = 0,
            BlockPercent = 0
        };
        const int firstLiveTick = 14 + 6 + 22 - 1;
        int aware = FirstUltimateEdge(new FighterCpuController(CpuDifficulty.Hard, 4403, null, null, tuning), castAware: true);
        int blind = FirstUltimateEdge(new FighterCpuController(CpuDifficulty.Hard, 4403, null, null, tuning), castAware: false);
        AssertThat(blind >= 0 && blind < firstLiveTick)
            .OverrideFailureMessage($"Harness: without the cast fields the press must land inside the recovery (tick {blind}).")
            .IsTrue();
        AssertThat(aware >= firstLiveTick)
            .OverrideFailureMessage($"The Ultimate press arrived on tick {aware}, inside the CPU's own cast (free from {firstLiveTick}).")
            .IsTrue();
        AssertThat(aware <= firstLiveTick + 20)
            .OverrideFailureMessage($"The Ultimate never followed the cast promptly (tick {aware}).")
            .IsTrue();
    }

    /// <summary>The three busy cast phases, each with a representative number of frames left.</summary>
    private static IEnumerable<(string Name, int Phase, int Frames)> CastPhases() {
        yield return ("startup", CpuCastPhase.Startup, 10);
        yield return ("active", CpuCastPhase.Active, 4);
        yield return ("recovery", CpuCastPhase.Recovery, 12);
    }

    /// <summary>A blocking, grounded opponent half a unit in front of a grounded CPU with the verb layer present.</summary>
    private static CpuDecisionObservation CastGrabOpportunity() {
        CpuDecisionObservation observation = Neutral(selfX: 0, targetX: 0);
        observation.TargetPositionXRaw = FP64.FromDouble(0.5).RawValue;
        observation.HasVerbState = 1;
        observation.SelfFacingRight = 1;
        observation.HasTargetVerbState = 1;
        observation.TargetIsGrounded = 1;
        observation.TargetBlockStance = 1;
        observation.HasStageBounds = 1;
        observation.LeftWallRaw = FP64.FromInt(-9).RawValue;
        observation.RightWallRaw = FP64.FromInt(9).RawValue;
        observation.CeilingRaw = FP64.FromInt(9).RawValue;
        observation.BottomBlastZoneRaw = FP64.FromInt(-5).RawValue;
        return observation;
    }

    /// <summary>The CPU in the recovery of its own whiffed swing, with the meter, cooldown and destination Echo Step needs.</summary>
    private static CpuDecisionObservation CastWhiffRecovery() {
        CpuDecisionObservation observation = CastGrabOpportunity();
        observation.TargetBlockStance = 0;
        observation.TargetPositionXRaw = FP64.FromInt(3).RawValue;
        observation.SelfAttackPhase = FighterBasicAttackRules.PhaseRecovery;
        observation.InfluenceRaw = FP64.FromInt(60).RawValue;
        observation.HasEchoStepDestination = 1;
        observation.EchoStepDestinationXRaw = FP64.FromInt(-2).RawValue;
        observation.EchoStepDestinationYRaw = FP64.Zero.RawValue;
        return observation;
    }

    /// <summary>Grab chords (Block held + BasicAttack pressed) or Echo Step chords (Block + Roll held) over <paramref name="ticks"/> ticks.</summary>
    private static int CountCastChords(
        FighterCpuController cpu, CpuDecisionObservation observation,
        ref uint tick, ref PlayerInputFrame previous, int ticks, bool grab) {
        int chords = 0;
        for (uint end = tick + (uint)ticks; tick < end; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            bool chord = grab
                ? frame.IsPressed(GameplayButtons.BasicAttack) && frame.IsHeld(GameplayButtons.Block)
                : frame.IsHeld(GameplayButtons.Block) && frame.IsHeld(GameplayButtons.Roll);
            if (chord) chords++;
        }
        return chords;
    }

    /// <summary>
    /// The tick of the first Ultimate edge while the CPU's own Splitting Strike
    /// runs out (tick 0 is the first tick after its acceptance), the opponent
    /// held in hitstun one unit away throughout; -1 when none comes in 120 ticks.
    /// </summary>
    private static int FirstUltimateEdge(FighterCpuController cpu, bool castAware) {
        const int startup = 14, active = 6, recovery = 22;
        CpuDecisionObservation baseline = Neutral(selfX: 0, targetX: 1);
        baseline.InfluenceRaw = FP64.FromInt(100).RawValue;
        baseline.TargetHitstunFrames = 30;
        baseline.HasVerbState = 1;
        baseline.SelfFacingRight = 1;
        baseline.HasTargetVerbState = 1;
        baseline.TargetIsGrounded = 1;
        baseline.SpecialTwoCooldownFrames = 600;
        PlayerInputFrame previous = default;
        for (uint tick = 0; tick < 120; tick++) {
            int t = (int)tick;
            CpuDecisionObservation observation = baseline;
            observation.AbilityLockoutFrames = System.Math.Max(0, 30 - t);
            observation.SpecialLockoutFrames = observation.AbilityLockoutFrames;
            if (castAware) {
                if (t < startup) {
                    observation.SelfSpecialCastPhase = CpuCastPhase.Startup;
                    observation.SelfSpecialCastPhaseFrames = startup - t;
                } else if (t < startup + active) {
                    observation.SelfSpecialCastPhase = CpuCastPhase.Active;
                    observation.SelfSpecialCastPhaseFrames = startup + active - t;
                } else if (t < startup + active + recovery) {
                    observation.SelfSpecialCastPhase = CpuCastPhase.Recovery;
                    observation.SelfSpecialCastPhaseFrames = startup + active + recovery - t;
                }
            }
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.Ultimate)) return t;
        }
        return -1;
    }

    // === Helpers ===

    private static IEnumerable<CpuDifficulty> Bands() {
        yield return CpuDifficulty.Easy;
        yield return CpuDifficulty.Normal;
        yield return CpuDifficulty.Hard;
    }

    private static CpuDecisionObservation Neutral(int selfX, int targetX) => new() {
        SelfPositionXRaw = FP64.FromInt(selfX).RawValue,
        TargetPositionXRaw = FP64.FromInt(targetX).RawValue,
        Stocks = 3,
        IsGrounded = 1,
        RemainingJumps = 1,
        SelfCurrentHP = 100,
        SelfMaxHP = 100,
        TargetCurrentHP = 100,
        TargetMaxHP = 100
    };

    private static CpuDecisionObservation OffStage(double selfX, double selfY) => new() {
        SelfPositionXRaw = FP64.FromDouble(selfX).RawValue,
        SelfPositionYRaw = FP64.FromDouble(selfY).RawValue,
        SelfVelocityYRaw = FP64.FromInt(-6).RawValue,
        TargetPositionXRaw = FP64.Zero.RawValue,
        Stocks = 3,
        IsGrounded = 0,
        RemainingJumps = 1,
        SelfCurrentHP = 100,
        SelfMaxHP = 100,
        HasStageBounds = 1,
        LeftWallRaw = FP64.FromInt(-9).RawValue,
        RightWallRaw = FP64.FromInt(9).RawValue,
        CeilingRaw = FP64.FromInt(9).RawValue,
        BottomBlastZoneRaw = FP64.FromInt(-5).RawValue,
        PlatformCount = 2
    };

    private static int CountEdges(
        FighterCpuController cpu, CpuDecisionObservation observation, GameplayButtons button, int ticks) {
        PlayerInputFrame previous = default;
        int edges = 0;
        for (uint tick = 0; tick < ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsPressed(button)) edges++;
        }
        return edges;
    }

    private static int CountOrbPursuitFrames(
        CpuDifficulty band, int selfHP = 100, int orbEffect = 1, int orbX = 4, int targetMeter = 0) {
        var cpu = new FighterCpuController(band, 1357);
        CpuDecisionObservation observation = Neutral(selfX: 0, targetX: -6);
        observation.SelfCurrentHP = selfHP;
        observation.TargetInfluenceRaw = FP64.FromInt(targetMeter).RawValue;
        observation.HasOrb = 1;
        observation.OrbEffectType = orbEffect;
        observation.OrbPositionXRaw = FP64.FromInt(orbX).RawValue;
        observation.OrbPositionYRaw = FP64.Zero.RawValue;

        PlayerInputFrame previous = default;
        int pursuing = 0;
        for (uint tick = 0; tick < Ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            // Combat approach walks toward the opponent at -110; only orb pursuit
            // sends the CPU the other way.
            if (frame.MoveX == 110) pursuing++;
        }
        return pursuing;
    }

    /// <summary>
    /// Adds an incoming hostile shot to <paramref name="observation"/>: level with
    /// the fighter, <paramref name="relativeX"/> units away, closing horizontally.
    /// </summary>
    private static CpuDecisionObservation IncomingProjectile(
        CpuDecisionObservation observation, double relativeX, int movementCooldown = 300) {
        observation.HasHostileProjectile = 1;
        observation.ProjectileRelativeXRaw = FP64.FromDouble(relativeX).RawValue;
        observation.ProjectileRelativeYRaw = FP64.Zero.RawValue;
        observation.ProjectileVelocityXRaw = relativeX >= 0
            ? FP64.FromInt(-10).RawValue
            : FP64.FromInt(10).RawValue;
        observation.ProjectileVelocityYRaw = FP64.Zero.RawValue;
        observation.MovementCooldownFrames = movementCooldown;
        return observation;
    }

    /// <summary>
    /// Frames spent holding Block against an incoming shot. The target sits at
    /// range 3 with no pressed buttons, so neutral combat can never hold Block
    /// itself; movement is on cooldown so Hard cannot blink instead.
    /// </summary>
    private static int CountProjectileBlockFrames(CpuDifficulty band, double relativeX) {
        var cpu = new FighterCpuController(band, 90210);
        CpuDecisionObservation observation = IncomingProjectile(
            Neutral(selfX: 0, targetX: -3), relativeX, movementCooldown: 300);
        PlayerInputFrame previous = default;
        int blocking = 0;
        for (uint tick = 0; tick < Ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsHeld(GameplayButtons.Block)) blocking++;
        }
        return blocking;
    }

    /// <summary>
    /// MovementAbility edges against an incoming shot with the ability ready. The
    /// target is within far range, so neutral combat never presses MovementAbility
    /// and every edge counted here is the Hard blink.
    /// </summary>
    private static int CountProjectileMovementEdges(CpuDifficulty band) {
        var cpu = new FighterCpuController(band, 90210);
        CpuDecisionObservation observation = IncomingProjectile(
            Neutral(selfX: 0, targetX: -3), relativeX: 4, movementCooldown: 0);
        return CountEdges(cpu, observation, GameplayButtons.MovementAbility, Ticks);
    }

    private static int CountHazardEscapeFrames(CpuDifficulty band, int hazardPhase) {
        var cpu = new FighterCpuController(band, 246);
        // Opponent left, hazard centred on the CPU: fleeing is rightward at full
        // tilt, approaching is leftward at 110, so the two never alias.
        CpuDecisionObservation observation = Neutral(selfX: 1, targetX: -6);
        observation.HasStageBounds = 1;
        observation.LeftWallRaw = FP64.FromInt(-9).RawValue;
        observation.RightWallRaw = FP64.FromInt(9).RawValue;
        observation.HasHazard = 1;
        observation.HazardPhase = hazardPhase;
        observation.HazardPositionXRaw = FP64.Zero.RawValue;
        observation.HazardHalfWidthRaw = FP64.FromInt(2).RawValue;

        PlayerInputFrame previous = default;
        int escaping = 0;
        for (uint tick = 0; tick < Ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.MoveX == 127) escaping++;
        }
        return escaping;
    }

    /// <summary>
    /// Feeds the CPU's own commands through a point-mass proxy of
    /// <c>FighterMovementSystem</c>'s airborne rules — the sim's own air step
    /// (<see cref="FighterAirPhysics.StepMovement"/>: apex hang after a jump,
    /// the heavier fall and its cap, the fast-fall ramp) at a 1/60 s step, a
    /// jump impulse equal to the default loadout's 11.5 u/s jump speed, and
    /// horizontal air control toward <c>MoveX</c> over four frames at the
    /// default 7 u/s move speed (both retuned by the 2026-08-10 feel batch
    /// §2.1/§2.2) — and reports whether the fighter climbs back
    /// over the floor plane while still inside the walls. It deliberately models
    /// only what the recovery decision can influence; the authoritative movement
    /// rules stay in the simulation.
    /// </summary>
    private static bool SimulateRecovery(CpuDifficulty band, int seed) {
        var cpu = new FighterCpuController(band, seed);
        FP64 step = FighterAirPhysics.FixedDelta;
        const int maxJumpCount = 2;
        FP64 jumpSpeed = FP64.FromDouble(11.5);
        FP64 moveSpeed = FP64.FromInt(7);
        FP64 leftWall = FP64.FromInt(-9);
        FP64 rightWall = FP64.FromInt(9);
        FP64 blastZone = FP64.FromInt(-5);

        FP64 x = FP64.FromDouble(-8.5);
        FP64 y = FP64.FromDouble(-0.5);
        FP64 velocityX = FP64.Zero;
        FP64 velocityY = FP64.FromInt(-4);
        // §2.3 gave the whole roster two jumps, so an off-stage fighter that
        // walked or was knocked off the ledge carries both into its recovery.
        // The harness modelled a single-jump kit, which no longer exists — and
        // with the §2.2 jump cut (apex 2.82 → 2.20 units) one jump can no longer
        // cover a Normal-band reaction delay from this depth.
        int remainingJumps = maxJumpCount;
        int movementCooldown = 0;
        PlayerInputFrame previous = default;

        for (uint tick = 0; tick < 600; tick++) {
            var observation = new CpuDecisionObservation {
                SelfPositionXRaw = x.RawValue,
                SelfPositionYRaw = y.RawValue,
                SelfVelocityXRaw = velocityX.RawValue,
                SelfVelocityYRaw = velocityY.RawValue,
                TargetPositionXRaw = FP64.Zero.RawValue,
                Stocks = 3,
                IsGrounded = 0,
                RemainingJumps = remainingJumps,
                SelfCurrentHP = 100,
                SelfMaxHP = 100,
                MovementCooldownFrames = movementCooldown,
                HasStageBounds = 1,
                LeftWallRaw = leftWall.RawValue,
                RightWallRaw = rightWall.RawValue,
                CeilingRaw = FP64.FromInt(9).RawValue,
                BottomBlastZoneRaw = blastZone.RawValue,
                PlatformCount = 2
            };
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;

            if (frame.IsPressed(GameplayButtons.Jump) && remainingJumps > 0) {
                velocityY = jumpSpeed;
                remainingJumps--;
            }
            if (frame.IsPressed(GameplayButtons.MovementAbility) && movementCooldown <= 0) {
                if (frame.MoveY < -30) y += FP64.FromInt(2);
                movementCooldown = 300;
            }
            if (movementCooldown > 0) movementCooldown--;

            FP64 targetSpeed = FP64.FromInt(frame.MoveX) / FP64.FromInt(127) * moveSpeed;
            FP64 accelerationStep = moveSpeed / FP64.FromInt(4);
            velocityX = velocityX < targetSpeed
                ? FP64.Min(velocityX + accelerationStep, targetSpeed)
                : FP64.Max(velocityX - accelerationStep, targetSpeed);
            velocityY = FighterAirPhysics.StepMovement(
                velocityY,
                fastFalling: frame.IsHeld(GameplayButtons.Down),
                floating: false,
                jumpedThisAirtime: FighterAirPhysics.HasJumpedThisAirtime(remainingJumps, maxJumpCount));
            x = FP64.Clamp(x + velocityX * step, leftWall, rightWall);
            y += velocityY * step;

            if (y < blastZone) return false;
            if (y >= FP64.Zero) return true;
        }
        return false;
    }

    private sealed class ScriptedWorld : ICpuWorldObserver {
        public bool IsMatchLive { get; set; } = true;
        public FighterOrbComponent? Orb { get; set; }
        public FighterHazardComponent? Hazard { get; set; }
        public FighterProjectileComponent? Projectile { get; set; }

        public bool TryGetNearestOrb(in FPVector2 selfPosition, out FighterOrbComponent orb) {
            orb = Orb ?? default;
            return Orb.HasValue;
        }

        public bool TryGetRelevantHazard(in FPVector2 selfPosition, out FighterHazardComponent hazard) {
            hazard = Hazard ?? default;
            return Hazard.HasValue;
        }

        public bool TryGetNearestHostileProjectile(
            int selfPlayerID, in FPVector2 selfPosition, out FighterProjectileComponent projectile) {
            projectile = Projectile ?? default;
            return Projectile.HasValue;
        }
    }
}
