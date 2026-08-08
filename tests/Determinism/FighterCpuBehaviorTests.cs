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
        AssertThat(easy.DashPercent).IsEqual(0);
        AssertThat(easy.ApproachJumpPercent).IsEqual(0);
        AssertThat(easy.OrbPursuitPercent).IsEqual(0);
        AssertThat(easy.HazardAvoidPercent).IsEqual(0);
        AssertThat(easy.BlockPercent).IsEqual(10);
        // ...but it does recover off-stage, which is the one place the design
        // grants Easy the double jump and Special 2.
        AssertThat(easy.RecoveryJumpPercent > 0).IsTrue();
        AssertThat(easy.RecoverySpecialTwoPercent > 0).IsTrue();
        AssertThat(easy.RecoveryMovementPercent).IsEqual(0);

        CpuBandTuning normal = CpuBandTuning.Normal;
        AssertThat(normal.BlockPercent).IsEqual(40);
        AssertThat(normal.OrbPursuitPercent).IsEqual(25);
        AssertThat(normal.HealingOrbHPPercent).IsEqual(40);
        AssertThat(normal.UltimatePercent).IsEqual(100);
        AssertThat(normal.RequiresUltimateSetup).IsFalse();
        AssertThat(normal.AvoidsHazardWarning).IsFalse();
        AssertThat(normal.HazardAvoidPercent > 0).IsTrue();

        CpuBandTuning hard = CpuBandTuning.Hard;
        AssertThat(hard.BlockPercent).IsEqual(80);
        AssertThat(hard.OrbPursuitPercent).IsEqual(75);
        AssertThat(hard.HealingOrbHPPercent).IsEqual(50);
        AssertThat(hard.RequiresUltimateSetup).IsTrue();
        AssertThat(hard.AvoidsHazardWarning).IsTrue();
        AssertThat(hard.HazardAvoidPercent).IsEqual(100);

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
        // Easy is deliberately not asserted to survive. Its 30–45-frame reflex window
        // costs more than half a second, and at the simulation's −30 u/s² gravity a
        // fighter that has crossed the floor plane is already past the −5 blast zone
        // by then. design-godot.md §10 says as much for Easy ("No active recovery
        // attempts... simply falls"); the CPU still commands the recovery, which
        // OffStageRecoverySteersTowardCentreAndSpendsJumpsOnEveryBand pins.
    }

    [TestCase]
    public void OnlyHardChainsAnUpwardMovementAbilityIntoItsRecovery() {
        // design §10: Hard combines double jump + movement ability; Medium recovers
        // with double jump and Special 2 but does not chain movement abilities;
        // Easy never presses the movement-ability button at all. Jumps are spent
        // first by every band, so this scenario starts with none left.
        CpuDecisionObservation exhausted = OffStage(selfX: -8, selfY: -1);
        exhausted.RemainingJumps = 0;

        var hard = new FighterCpuController(CpuDifficulty.Hard, 31337);
        PlayerInputFrame previous = default;
        int upwardMovementEdges = 0;
        for (uint tick = 0; tick < Ticks; tick++) {
            PlayerInputFrame frame = hard.Sample(tick, in exhausted, in previous);
            previous = frame;
            // The stick must aim up (world Y is up, the axis is Y-down) or the
            // directional warp travels sideways off the stage.
            if (frame.IsPressed(GameplayButtons.MovementAbility) && frame.MoveY < -30) upwardMovementEdges++;
        }
        AssertThat(upwardMovementEdges > 0).IsTrue();

        AssertThat(CountEdges(
            new FighterCpuController(CpuDifficulty.Easy, 31337),
            exhausted, GameplayButtons.MovementAbility, Ticks)).IsEqual(0);
        AssertThat(CountEdges(
            new FighterCpuController(CpuDifficulty.Normal, 31337),
            exhausted, GameplayButtons.MovementAbility, Ticks)).IsEqual(0);
    }

    [TestCase]
    public void EasyUsesSpecialTwoOffStageAndNeverInNeutral() {
        // design §10 Easy: "Special 2 is only triggered when the AI is off-stage and
        // below the main platform Y-coordinate."
        var recovering = new FighterCpuController(CpuDifficulty.Easy, 606);
        int offStageEdges = CountEdges(
            recovering, OffStage(selfX: -8, selfY: -1), GameplayButtons.Special2, Ticks);
        AssertThat(offStageEdges > 0).IsTrue();

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
    public void MovementAbilityIsNoLongerStarvedBehindTheDashRoll() {
        // It used to sit in an else-if chain after Dash and almost never fired.
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
        // No world observer: the orb/hazard blocks stay explicitly absent.
        AssertThat(observation.HasOrb).IsEqual(0);
        AssertThat(observation.HasHazard).IsEqual(0);
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
        CpuDifficulty band, int selfHP = 100, int orbEffect = 1, int orbX = 4) {
        var cpu = new FighterCpuController(band, 1357);
        CpuDecisionObservation observation = Neutral(selfX: 0, targetX: -6);
        observation.SelfCurrentHP = selfHP;
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
    /// <c>FighterMovementSystem</c>'s airborne rules — gravity −30 u/s² at a
    /// 1/60 s step, a jump impulse equal to the default loadout's 13 u/s jump
    /// speed, and horizontal air control toward <c>MoveX</c> over four frames at
    /// the default 8 u/s move speed — and reports whether the fighter climbs back
    /// over the floor plane while still inside the walls. It deliberately models
    /// only what the recovery decision can influence; the authoritative movement
    /// rules stay in the simulation.
    /// </summary>
    private static bool SimulateRecovery(CpuDifficulty band, int seed) {
        var cpu = new FighterCpuController(band, seed);
        FP64 step = FP64.One / FP64.FromInt(60);
        FP64 gravity = FP64.FromInt(-30);
        FP64 jumpSpeed = FP64.FromInt(13);
        FP64 moveSpeed = FP64.FromInt(8);
        FP64 leftWall = FP64.FromInt(-9);
        FP64 rightWall = FP64.FromInt(9);
        FP64 blastZone = FP64.FromInt(-5);

        FP64 x = FP64.FromDouble(-8.5);
        FP64 y = FP64.FromDouble(-0.5);
        FP64 velocityX = FP64.Zero;
        FP64 velocityY = FP64.FromInt(-4);
        int remainingJumps = 1;
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
            velocityY += gravity * step;
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

        public bool TryGetNearestOrb(in FPVector2 selfPosition, out FighterOrbComponent orb) {
            orb = Orb ?? default;
            return Orb.HasValue;
        }

        public bool TryGetRelevantHazard(in FPVector2 selfPosition, out FighterHazardComponent hazard) {
            hazard = Hazard ?? default;
            return Hazard.HasValue;
        }
    }
}
