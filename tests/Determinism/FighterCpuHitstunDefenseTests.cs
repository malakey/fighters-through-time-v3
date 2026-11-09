using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// V7.4: the CPU learns its escape verbs (design §10 CPU matrices, the
/// "Hitstun Defense" rows). The old controller zeroed every input while in
/// hitstun, surrendering the hit-2 block escape, the landing tech, and DI all
/// at once — so human loops that a human defender breaks ran forever against
/// the AI. The fix is input-side only: a per-difficulty roll, once per hitstun
/// instance, to hold Block through the hitstun (and DI a launch toward stage
/// centre); the escapes themselves fire through the ordinary simulation rules.
/// Pure C#: hand-built observations for the roll gating, and a real
/// <see cref="FighterSimulation"/> for the escape/tech integrations.
/// </summary>
[TestSuite]
public class FighterCpuHitstunDefenseTests {

    [TestCase]
    public void HitstunDefenseTuningMatchesTheDesignMatrices() {
        // design §10: Easy 10%/never, Medium 45%/40%, Hard 85%/80%.
        AssertThat(CpuBandTuning.Easy.HitstunDefensePercent).IsEqual(10);
        AssertThat(CpuBandTuning.Easy.DiPercent).IsEqual(0);
        AssertThat(CpuBandTuning.Normal.HitstunDefensePercent).IsEqual(45);
        AssertThat(CpuBandTuning.Normal.DiPercent).IsEqual(40);
        AssertThat(CpuBandTuning.Hard.HitstunDefensePercent).IsEqual(85);
        AssertThat(CpuBandTuning.Hard.DiPercent).IsEqual(80);
    }

    [TestCase]
    public void ZeroPercentDefenseNeverHoldsBlockAndHundredPercentAlwaysDoes() {
        var never = NewCpu(DefenseTuning(hitstunDefense: 0, di: 0), seed: 11);
        var always = NewCpu(DefenseTuning(hitstunDefense: 100, di: 0), seed: 11);
        PlayerInputFrame previousNever = default;
        PlayerInputFrame previousAlways = default;

        for (int instance = 0; instance < 12; instance++) {
            for (int hitstun = 20; hitstun >= 1; hitstun--) {
                CpuDecisionObservation observation = HitstunObservation(hitstun);
                PlayerInputFrame neverFrame = never.Sample(
                    Tick(instance, 20 - hitstun), in observation, in previousNever);
                PlayerInputFrame alwaysFrame = always.Sample(
                    Tick(instance, 20 - hitstun), in observation, in previousAlways);
                previousNever = neverFrame;
                previousAlways = alwaysFrame;
                AssertThat(neverFrame.IsHeld(GameplayButtons.Block))
                    .OverrideFailureMessage("A 0% defense roll must never hold Block in hitstun.")
                    .IsFalse();
                AssertThat(alwaysFrame.IsHeld(GameplayButtons.Block))
                    .OverrideFailureMessage("A 100% defense roll must hold Block for the whole hitstun.")
                    .IsTrue();
            }
            // Grounded recovery frames between instances release the hold
            // (the escape stance lingers briefly, then lets go).
            for (int frame = 0; frame < FighterCpuController.EscapeStanceLingerFrames + 10; frame++) {
                CpuDecisionObservation observation = HitstunObservation(0);
                previousNever = never.Sample(Tick(instance, 100 + frame), in observation, in previousNever);
                previousAlways = always.Sample(Tick(instance, 100 + frame), in observation, in previousAlways);
            }
            AssertThat(previousAlways.IsHeld(GameplayButtons.Block))
                .OverrideFailureMessage("The hold must release once grounded with hitstun over.")
                .IsFalse();
        }
    }

    [TestCase]
    public void TheDefenseRollHappensOncePerHitstunInstanceNotPerFrame() {
        // At 50% a per-frame roll would flip mid-instance almost surely across
        // 12 instances x 30 frames; a per-instance roll holds one answer for
        // the whole instance. Both outcomes must appear across the instances.
        var cpu = NewCpu(DefenseTuning(hitstunDefense: 50, di: 0), seed: 4242);
        PlayerInputFrame previous = default;
        int heldInstances = 0;
        int unheldInstances = 0;

        for (int instance = 0; instance < 12; instance++) {
            bool? instanceHeld = null;
            for (int hitstun = 30; hitstun >= 1; hitstun--) {
                CpuDecisionObservation observation = HitstunObservation(hitstun);
                PlayerInputFrame frame = cpu.Sample(
                    Tick(instance, 30 - hitstun), in observation, in previous);
                previous = frame;
                bool held = frame.IsHeld(GameplayButtons.Block);
                if (instanceHeld == null) instanceHeld = held;
                AssertThat(held)
                    .OverrideFailureMessage("The roll is once per instance: the answer may not change mid-instance.")
                    .IsEqual(instanceHeld.Value);
            }
            if (instanceHeld == true) heldInstances++; else unheldInstances++;
            for (int frame = 0; frame < FighterCpuController.EscapeStanceLingerFrames + 10; frame++) {
                CpuDecisionObservation observation = HitstunObservation(0);
                previous = cpu.Sample(Tick(instance, 100 + frame), in observation, in previous);
            }
        }
        AssertThat(heldInstances > 0).IsTrue();
        AssertThat(unheldInstances > 0).IsTrue();
    }

    [TestCase]
    public void ASuccessfulDiRollHoldsTowardSafetyDuringTheLaunchWindow() {
        // The held direction is what FighterVerbRules.ResolvePendingLaunch
        // reads when the launch hitstop ends. On a Sealed stage — an unbroken
        // floor wall to wall — toward stage centre is still the answer, and
        // this half of the case is unchanged from V7.4.
        var cpu = NewCpu(DefenseTuning(hitstunDefense: 100, di: 100), seed: 9);
        PlayerInputFrame previous = default;

        CpuDecisionObservation leftOfCentre = HitstunObservation(12);
        // M05 (Package 12 W3b): DI is only held on a LAUNCH, read input-side as
        // "airborne in hitstun" — a non-launching hit leaves its victim grounded.
        leftOfCentre.IsGrounded = 0;
        leftOfCentre.HasStageBounds = 1;
        leftOfCentre.LeftWallRaw = FP64.FromInt(-9).RawValue;
        leftOfCentre.RightWallRaw = FP64.FromInt(9).RawValue;
        leftOfCentre.SelfPositionXRaw = FP64.FromInt(-5).RawValue;
        PlayerInputFrame frame = cpu.Sample(0, in leftOfCentre, in previous);
        AssertThat(frame.MoveX > 0)
            .OverrideFailureMessage("Launched left of centre, DI must hold right (toward centre).")
            .IsTrue();
        AssertThat(frame.IsHeld(GameplayButtons.Block)).IsTrue();

        var mirrored = NewCpu(DefenseTuning(hitstunDefense: 100, di: 100), seed: 9);
        PlayerInputFrame previousMirrored = default;
        CpuDecisionObservation rightOfCentre = leftOfCentre;
        rightOfCentre.SelfPositionXRaw = FP64.FromInt(5).RawValue;
        PlayerInputFrame mirroredFrame = mirrored.Sample(0, in rightOfCentre, in previousMirrored);
        AssertThat(mirroredFrame.MoveX < 0)
            .OverrideFailureMessage("Launched right of centre, DI must hold left (toward centre).")
            .IsTrue();

        // Package 11 A9b: the V7.4 deferral expired with A9's Open stages. When
        // the launch trajectory ends over a pit, the useful hold is toward the
        // nearest pit-facing floor edge — which on Paris is AWAY from centre,
        // because stage centre is the hole.
        CpuDecisionObservation overTheParisPit = HitstunObservation(12);
        overTheParisPit.IsGrounded = 0;
        overTheParisPit.HasStageBounds = 1;
        overTheParisPit.LeftWallRaw = FP64.FromInt(-9).RawValue;
        overTheParisPit.RightWallRaw = FP64.FromInt(9).RawValue;
        overTheParisPit.SelfPositionXRaw = FP64.FromDouble(-1.5).RawValue;
        overTheParisPit.HasFloorSegments = 1;
        overTheParisPit.HasFloorSupportUnderSelf = 0;
        overTheParisPit.LaunchTrajectoryCrossesGap = 1;
        overTheParisPit.HasFloorEdgeLeft = 1;
        overTheParisPit.NearestFloorEdgeLeftXRaw = FP64.FromDouble(-2.5).RawValue;
        overTheParisPit.HasFloorEdgeRight = 1;
        overTheParisPit.NearestFloorEdgeRightXRaw = FP64.FromDouble(2.5).RawValue;

        var pitAware = NewCpu(DefenseTuning(hitstunDefense: 100, di: 100), seed: 9);
        PlayerInputFrame previousPit = default;
        PlayerInputFrame pitFrame = pitAware.Sample(0, in overTheParisPit, in previousPit);
        AssertThat(pitFrame.MoveX < 0)
            .OverrideFailureMessage(
                "Left of a central pit, DI must hold toward the nearer floor edge, not toward centre.")
            .IsTrue();

        // Mirrored, and still not toward centre.
        CpuDecisionObservation rightOfThePit = overTheParisPit;
        rightOfThePit.SelfPositionXRaw = FP64.FromDouble(1.5).RawValue;
        var mirroredPit = NewCpu(DefenseTuning(hitstunDefense: 100, di: 100), seed: 9);
        PlayerInputFrame previousMirroredPit = default;
        PlayerInputFrame mirroredPitFrame = mirroredPit.Sample(0, in rightOfThePit, in previousMirroredPit);
        AssertThat(mirroredPitFrame.MoveX > 0)
            .OverrideFailureMessage(
                "Right of a central pit, DI must hold toward the nearer floor edge.")
            .IsTrue();

        // And a supported trajectory on the same Open stage keeps the old hold:
        // the pit-aware read only engages while the launch actually ends in a gap.
        CpuDecisionObservation supported = overTheParisPit;
        supported.SelfPositionXRaw = FP64.FromInt(-5).RawValue;
        supported.HasFloorSupportUnderSelf = 1;
        supported.LaunchTrajectoryCrossesGap = 0;
        var unchangedHold = NewCpu(DefenseTuning(hitstunDefense: 100, di: 100), seed: 9);
        PlayerInputFrame previousSupported = default;
        PlayerInputFrame supportedFrame = unchangedHold.Sample(0, in supported, in previousSupported);
        AssertThat(supportedFrame.MoveX > 0)
            .OverrideFailureMessage("A supported launch must keep the toward-centre hold.")
            .IsTrue();

        // M05: grounded (non-launching) hitstun gets the Block hold — the hit-2
        // escape — and no DI stick, because there is no launch to bend.
        CpuDecisionObservation grounded = leftOfCentre;
        grounded.IsGrounded = 1;
        var groundedCpu = NewCpu(DefenseTuning(hitstunDefense: 100, di: 100), seed: 9);
        PlayerInputFrame previousGrounded = default;
        PlayerInputFrame groundedFrame = groundedCpu.Sample(0, in grounded, in previousGrounded);
        AssertThat(groundedFrame.IsHeld(GameplayButtons.Block)).IsTrue();
        AssertThat(groundedFrame.MoveX == 0)
            .OverrideFailureMessage("Grounded non-launch hitstun must not hold a DI direction.")
            .IsTrue();
    }

    [TestCase]
    public void AHundredPercentDefenseCpuEscapesTheStringAfterHitTwoThroughTheSimRules() {
        // The whole point of the pass: the same three-hit string that runs a
        // defenseless CPU down for all three hits loses its finisher against a
        // defender holding Block — the hit-2 escape and the raised stance are
        // ordinary sim rules, not CPU special cases.
        int defenselessHits = RunStringAgainstCpu(DefenseTuning(hitstunDefense: 0, di: 0));
        int defendedHits = RunStringAgainstCpu(DefenseTuning(hitstunDefense: 100, di: 0));

        AssertThat(defenselessHits)
            .OverrideFailureMessage($"The control CPU must eat the full three-hit string (took {defenselessHits}).")
            .IsEqual(3);
        AssertThat(defendedHits)
            .OverrideFailureMessage($"A 100%-defense CPU must escape after hit two and deny the finisher (took {defendedHits}).")
            .IsEqual(2);
    }

    [TestCase]
    public void AHundredPercentDefenseCpuTechsTheTumbleLanding() {
        // A launching opener puts the victim in tumble; holding Block on
        // ground contact techs (hitstun ends into the invulnerable in-place
        // recovery — the FighterVerbRules signature pinned by the verb suite).
        AssertThat(RunLauncherAgainstCpu(DefenseTuning(hitstunDefense: 100, di: 0)))
            .OverrideFailureMessage("A 100%-defense CPU must tech the tumble landing.")
            .IsTrue();
        AssertThat(RunLauncherAgainstCpu(DefenseTuning(hitstunDefense: 0, di: 0)))
            .OverrideFailureMessage("A 0%-defense CPU must ride the tumble out, not tech.")
            .IsFalse();
    }

    // === CM-FIXB-CPU: the hold once hitstun is over (decision 3.2, 5.2's climb jump) ===

    /// <summary>
    /// CM-FIXB-CPU (finding B0/B20): a 100 %-defense Hard CPU launched off
    /// Nassau's stern ledge, whose 30-frame hitstun ends while it is still in the
    /// air over the 3.0-wide gap, must recover. The Block hold used to stay
    /// exclusive until the fighter was grounded, so every recovery jump and
    /// movement-ability press the planner sent was eaten and the CPU fell into
    /// the blast zone. Real simulation frames; DI is off so the launch, not the
    /// DI read, decides where hitstun ends.
    /// </summary>
    [TestCase]
    public void AHundredPercentDefenseCpuLaunchedOverAnOpenStagePitRecoversOnceItsHitstunEnds() {
        var failures = new System.Collections.Generic.List<string>();
        foreach ((double kx, double ky) in new[] { (13.0, 11.0), (14.0, 12.0), (12.0, 10.0) }) {
            foreach (int seed in new[] { 1, 2, 3 }) {
                string run = $"launch ({kx}, {ky}) seed {seed}";
                PitLaunchResult result = RunPitLaunch(kx, ky, seed);
                if (!result.HitstunEndedOverTheGap) {
                    failures.Add($"{run}: harness, hitstun did not end airborne over the gap");
                    continue;
                }
                if (result.RecoveryPresses == 0) failures.Add($"{run}: no Jump or movement-ability press after hitstun");
                if (result.StockLost) failures.Add($"{run}: the CPU fell into the blast zone; {result.Trace}");
                else if (!result.Landed) failures.Add($"{run}: the CPU never got back onto the stage");
            }
        }
        if (failures.Count > 0) AssertThat(string.Join(" | ", failures)).IsEqual("");
    }

    /// <summary>
    /// CM-FIXB-CPU (finding B0/B20): the same CPU caught on Nassau's pit-facing
    /// stern ledge after its hitstun ended, with the defense hold still active,
    /// leaves by the climb jump (5.2: "the CPU stays climb-jump only") within its
    /// reaction window — not by the 300-frame auto-release it used to wait out
    /// holding Block alone — and never by a get-up.
    /// </summary>
    [TestCase]
    public void AHundredPercentDefenseCpuCaughtOnAPitFacingLedgeAfterHitstunClimbJumps() {
        var failures = new System.Collections.Generic.List<string>();
        foreach (int seed in new[] { 1, 2, 3 }) {
            PitLaunchResult result = RunPitLaunch(9.0, 14.0, seed, stageForLedgeCapture: true);
            string run = $"seed {seed}";
            if (result.HangTick < 0) {
                failures.Add($"{run}: harness, the ledge never caught the CPU; {result.Trace}");
                continue;
            }
            if (result.HitstunAtHang > 0) failures.Add($"{run}: harness, the hang began inside hitstun");
            if (result.GetUp) failures.Add($"{run}: the CPU left the ledge by a get-up");
            if (result.HangEndTick < 0) {
                failures.Add($"{run}: the CPU never left the ledge");
            } else {
                int hangFrames = result.HangEndTick - result.HangTick;
                if (!result.LeftByClimbJump) failures.Add($"{run}: left the ledge after {hangFrames} frames without a climb jump");
                else if (hangFrames > 30) failures.Add($"{run}: climb-jumped only after {hangFrames} frames");
            }
            if (result.StockLost) failures.Add($"{run}: the CPU lost the stock");
        }
        if (failures.Count > 0) AssertThat(string.Join(" | ", failures)).IsEqual("");
    }

    /// <summary>
    /// CM-FIXB-CPU (finding B9): a hit on a hanging CPU leaves the hang set
    /// through the whole hitstop, so the CPU's frame reads <c>IsLedgeHanging</c>
    /// on every frozen tick — the thaw tick included, whose stick
    /// <c>FighterVerbRules.ResolvePendingLaunch</c> reads. The climb-jump-only
    /// override (fa18442) zeroed that stick, so the launch resolved with no DI.
    /// A 100 %-DI CPU hanging on Nassau's stern ledge (its first-grab
    /// intangibility run out) and launched by a Special must hold toward the
    /// stage through the thaw, and its velocity must leave the stashed launch
    /// rotated. Real simulation frames, inputs pinned with
    /// <see cref="PlayerInputFrame.Create"/>.
    /// </summary>
    [TestCase]
    public void AHangingCpuStruckByALauncherKeepsItsDiHoldThroughTheThaw() {
        FighterStageGeometry nassau = FighterStageGeometry.ForStage("nassau_flagship");
        FighterLoadout victim = FighterLoadout.Default(FighterCharacterID.Joan);
        var simulation = new FighterSimulation(
            PitLauncher(8.0, 8.0), victim, stocks: 3, seed: 7, rules: FighterMatchRules.Disabled, stageGeometry: nassau);
        var cpu = new FighterCpuController(
            CpuDifficulty.Hard, 7, nassau, new FighterSimulationWorldObserver(simulation),
            CpuBandTuning.Hard with { HitstunDefensePercent = 100, DiPercent = 100 },
            CpuRecoveryProfile.FromLoadout(in victim));
        PlayerInputFrame previousCpu = default;
        GameplayButtons attackerHeld = GameplayButtons.None;
        int hangTick = -1;
        bool pressed = false;
        bool cpuLive = false;
        FP64 launchX = FP64.Zero;
        FP64 launchY = FP64.Zero;
        bool thawed = false;
        sbyte thawStick = 0;
        FPVector2 resolved = FPVector2.Zero;
        var frozenSticks = new System.Collections.Generic.List<sbyte>();

        for (int tick = 0; tick < 400 && !thawed; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent attackerRuntime)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent self)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent selfRuntime)).IsTrue();
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent selfVerb)).IsTrue();
            bool hanging = FighterLedgeRules.IsHanging(in selfRuntime);
            if (hanging && hangTick < 0) hangTick = tick;
            if (!cpuLive && self.HitstunFrames > 0) {
                cpuLive = true;
                AssertThat(hanging)
                    .OverrideFailureMessage("Harness: the CPU must still read as hanging on the hit's frozen ticks.")
                    .IsTrue();
                AssertThat(selfVerb.PendingLaunchActive)
                    .OverrideFailureMessage("Harness: the hit must stash a launch for the thaw.")
                    .IsEqual(1);
                launchX = selfVerb.PendingLaunchX;
                launchY = selfVerb.PendingLaunchY;
            }

            // The attacker walks to the stern and presses the launcher once the
            // hanging CPU's first-grab intangibility has run out.
            GameplayButtons attackerButtons = GameplayButtons.None;
            float attackerX = 0f;
            if (!pressed) {
                if (attacker.Position.x < nassau.FloorSegments[0].EdgeX(1) - FP64.FromDouble(0.4)) attackerX = 1f;
                else if (hangTick >= 0 && tick > hangTick + 40 && self.InvulnerabilityFrames <= 0) {
                    attackerButtons = GameplayButtons.Special1;
                    pressed = true;
                }
            }
            PlayerInputFrame attackerFrame = PlayerInputFrame.Create(
                (uint)tick, attackerX, 0f, attackerButtons, attackerHeld);
            attackerHeld = attackerButtons;

            PlayerInputFrame selfFrame;
            int hitstopBefore = selfVerb.HitstopFrames;
            if (cpuLive) {
                selfFrame = cpu.Sample((uint)tick, in self, in selfRuntime, in attacker, in attackerRuntime, in previousCpu);
                previousCpu = selfFrame;
                if (hitstopBefore > 0) frozenSticks.Add(selfFrame.MoveX);
            } else {
                // Walk off the stern into the ledge capture, then hang on a neutral stick.
                float walk = hangTick < 0 && self.IsGrounded != 0 ? 0.6f : 0f;
                selfFrame = PlayerInputFrame.Create((uint)tick, walk, 0f, GameplayButtons.None, GameplayButtons.None);
            }
            simulation.Advance(attackerFrame, selfFrame);
            if (cpuLive && hitstopBefore == 1) {
                AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
                thawed = true;
                thawStick = selfFrame.MoveX;
                resolved = after.Velocity;
            }
        }
        AssertThat(hangTick >= 0).OverrideFailureMessage("Harness: the CPU never reached the stern ledge.").IsTrue();
        AssertThat(thawed).OverrideFailureMessage("Harness: the launch never thawed.").IsTrue();
        AssertThat(launchX > FP64.Zero)
            .OverrideFailureMessage("Harness: the launch must carry the hanging CPU out over the gap.")
            .IsTrue();
        // Pit-aware DI: the only floor edge is the stern ledge itself, to the left.
        AssertThat(thawStick < 0)
            .OverrideFailureMessage($"The thaw tick's stick was {thawStick}: the DI hold toward the stage was zeroed.")
            .IsTrue();
        foreach (sbyte stick in frozenSticks) {
            AssertThat(stick < 0)
                .OverrideFailureMessage($"A frozen tick sent stick {stick}; the DI hold must survive the whole hitstop.")
                .IsTrue();
        }
        AssertThat(resolved.x < launchX)
            .OverrideFailureMessage(
                $"The launch resolved at ({resolved.x}, {resolved.y}) against the stashed ({launchX}, {launchY}): it was not bent toward the stage.")
            .IsTrue();
    }

    /// <summary>
    /// CM-FIXB-CPU (finding B0's charge): a recovery movement-ability press the
    /// defense hold masks — it arrives on the first frame of a hit's hitstun —
    /// never reached the simulation, so it must not spend Medium's one
    /// activation per offstage episode. It used to be charged at delivery,
    /// leaving the CPU no movement ability once its hitstun ended. A control
    /// CPU on the same seed and observations finds the press's tick; the CPU
    /// under test is hit on exactly that tick.
    /// </summary>
    [TestCase]
    public void AMovementActivationTheDefenseHoldMasksIsNotChargedToTheEpisode() {
        CpuBandTuning tuning = CpuBandTuning.Normal with {
            HitstunDefensePercent = 100,
            DiPercent = 0,
            RecoveryMovementPercent = 100
        };
        CpuDecisionObservation offStage = new() {
            SelfPositionXRaw = FP64.FromInt(-8).RawValue,
            SelfPositionYRaw = FP64.FromInt(-1).RawValue,
            SelfVelocityYRaw = FP64.FromInt(-6).RawValue,
            Stocks = 3,
            IsGrounded = 0,
            RemainingJumps = 0,
            SelfCurrentHP = 100,
            SelfMaxHP = 100,
            HasStageBounds = 1,
            LeftWallRaw = FP64.FromInt(-9).RawValue,
            RightWallRaw = FP64.FromInt(9).RawValue,
            CeilingRaw = FP64.FromInt(9).RawValue,
            BottomBlastZoneRaw = FP64.FromInt(-5).RawValue,
            PlatformCount = 2
        };
        const int seed = 4711;
        const int hitstunFrames = 30;

        var control = new FighterCpuController(CpuDifficulty.Normal, seed, null, null, tuning);
        PlayerInputFrame previous = default;
        int pressTick = -1;
        for (uint tick = 0; tick < 120 && pressTick < 0; tick++) {
            PlayerInputFrame frame = control.Sample(tick, in offStage, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.MovementAbility)) pressTick = (int)tick;
        }
        AssertThat(pressTick > 0)
            .OverrideFailureMessage("Harness: the control CPU never pressed its movement ability.")
            .IsTrue();

        var cpu = new FighterCpuController(CpuDifficulty.Normal, seed, null, null, tuning);
        previous = default;
        int pressesInHitstun = 0;
        int pressesAfter = 0;
        int chargedAtHitstunEnd = -1;
        int hitstunEnd = pressTick + hitstunFrames;
        for (int tick = 0; tick < hitstunEnd + 120; tick++) {
            CpuDecisionObservation observation = offStage;
            if (tick >= pressTick && tick < hitstunEnd) observation.HitstunFrames = hitstunEnd - tick;
            PlayerInputFrame frame = cpu.Sample((uint)tick, in observation, in previous);
            previous = frame;
            if (tick == hitstunEnd - 1) chargedAtHitstunEnd = cpu.EpisodeMovementActivations;
            if (!frame.IsPressed(GameplayButtons.MovementAbility)) continue;
            if (tick < hitstunEnd) pressesInHitstun++;
            else pressesAfter++;
        }
        AssertThat(chargedAtHitstunEnd)
            .OverrideFailureMessage("The masked press was charged to the episode while hitstun still ran.")
            .IsEqual(0);
        AssertThat(pressesInHitstun)
            .OverrideFailureMessage("Harness: the defense hold must mask the press that arrives with the hit.")
            .IsEqual(0);
        AssertThat(pressesAfter > 0)
            .OverrideFailureMessage(
                "The masked press was charged: no movement-ability press went out once hitstun ended.")
            .IsTrue();
        AssertThat(cpu.EpisodeMovementActivations)
            .OverrideFailureMessage("Exactly the press that went out is charged to the episode.")
            .IsEqual(1);
    }

    // === Harness ===

    private struct PitLaunchResult {
        public bool HitstunEndedOverTheGap;
        public int RecoveryPresses;
        public bool StockLost;
        public bool Landed;
        public int HangTick;
        public int HitstunAtHang;
        public int HangEndTick;
        public bool LeftByClimbJump;
        public bool GetUp;
        public string Trace;
    }

    /// <summary>
    /// A Special 1 melee launcher with an authored knockback vector (D10) and a
    /// 30-frame hitstun, on the Default kit's numbers.
    /// </summary>
    private static FighterLoadout PitLauncher(double knockbackX, double knockbackY) {
        FighterAbilityLoadout modes = FighterAbilityLoadout.Default with {
            SpecialOneExecutionType = 0,
            SpecialOneHit = new FighterAbilityHitData {
                HitstunFrames = 30,
                BlockClass = (int)BlockClass.Special,
                Launches = true,
                Delivery = (int)HitDelivery.DirectHit,
                Origin = (int)HitOrigin.Special,
                HasKnockbackVector = true,
                KnockbackX = FP64.FromDouble(knockbackX),
                KnockbackY = FP64.FromDouble(knockbackY)
            }
        };
        return new FighterLoadout(
            (int)FighterCharacterID.Einstein,
            100, 3, 2,
            10, 4, 4, 20,
            (int)StatusType.None, 0,
            (int)StatusType.None, 0,
            (int)StatusType.None, 0,
            FP64.One,
            FP64.FromInt(7),
            FP64.FromDouble(11.5),
            FP64.FromInt(3),
            FP64.FromDouble(System.Math.Max(knockbackX, knockbackY)),
            FP64.FromInt(4),
            FP64.FromInt(5),
            FP64.One, FP64.One, FP64.One,
            modes);
    }

    /// <summary>
    /// Nassau Flagship (Open: the floor ends at the stern ledge, x = 6, over a
    /// 3.0-wide gap). Player one walks up to a default Joan standing at her
    /// spawn (x = 4) and launches her toward the stern; from the first frame of
    /// her hitstun a Hard CPU with a 100 % defense roll (and no DI) drives her,
    /// through the real world observer and her own recovery profile.
    /// </summary>
    private static PitLaunchResult RunPitLaunch(double knockbackX, double knockbackY, int seed, bool stageForLedgeCapture = false) {
        FighterStageGeometry nassau = FighterStageGeometry.ForStage("nassau_flagship");
        FighterLoadout victim = FighterLoadout.Default(FighterCharacterID.Joan);
        var simulation = new FighterSimulation(
            PitLauncher(knockbackX, knockbackY), victim,
            stocks: 3, seed: seed, rules: FighterMatchRules.Disabled, stageGeometry: nassau);
        var cpu = new FighterCpuController(
            CpuDifficulty.Hard, seed, nassau, new FighterSimulationWorldObserver(simulation),
            CpuBandTuning.Hard with { HitstunDefensePercent = 100, DiPercent = 0 },
            CpuRecoveryProfile.FromLoadout(in victim));
        var result = new PitLaunchResult { HangTick = -1, HangEndTick = -1 };
        PlayerInputFrame previousCpu = default;
        GameplayButtons attackerHeld = GameplayButtons.None;
        bool pressed = false;
        bool cpuLive = false;
        bool hitstunEnded = false;
        int lastHitstun = 0;
        bool wasHanging = false;
        bool arranged = !stageForLedgeCapture;
        var trace = new System.Collections.Generic.List<string>();
        // Recovery trajectories launch from the real match spawn. The separate
        // ledge-catch scenario stages two units inside the current lip: its slower
        // 9/14 launch then thaws inside the capture box rather than beyond it.
        FP64 launchStart = nassau.FloorSegments[0].EdgeX(1) - FP64.FromInt(2);

        for (int tick = 0; tick < 600; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent attackerRuntime)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent self)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent selfRuntime)).IsTrue();
            if (self.Stocks < 3) {
                result.StockLost = true;
                result.Trace = string.Join("; ", trace);
                return result;
            }
            if (!arranged) {
                if (self.Position.x > launchStart + FP64.FromDouble(0.05)) {
                    simulation.Advance(new PlayerInputFrame { Tick = (uint)tick },
                        new PlayerInputFrame { Tick = (uint)tick, MoveX = -127 });
                    continue;
                }
                arranged = true;
            }
            bool hanging = FighterLedgeRules.IsHanging(in selfRuntime);
            if (FighterLedgeRules.IsGetUpClimb(in selfRuntime)) result.GetUp = true;
            if (hanging && !wasHanging && result.HangTick < 0) {
                result.HangTick = tick;
                result.HitstunAtHang = self.HitstunFrames;
            }
            if (!hanging && wasHanging && result.HangEndTick < 0) {
                result.HangEndTick = tick;
                result.LeftByClimbJump = self.Velocity.y > FP64.Zero;
            }
            wasHanging = hanging;
            if (lastHitstun > 0 && self.HitstunFrames <= 0 && !hitstunEnded) {
                hitstunEnded = true;
                result.HitstunEndedOverTheGap = self.IsGrounded == 0 && !nassau.HasFloorSupport(self.Position.x);
            }
            lastHitstun = self.HitstunFrames;
            if (self.HitstunFrames > 0) cpuLive = true;
            if (hitstunEnded && !hanging && self.IsGrounded != 0 && self.HitstunFrames <= 0) {
                result.Landed = true;
                result.Trace = string.Join("; ", trace);
                return result;
            }

            GameplayButtons attackerButtons = GameplayButtons.None;
            float attackerX = 0f;
            if (!pressed) {
                if (self.Position.x - attacker.Position.x > FP64.FromDouble(0.9)) attackerX = 1f;
                else if (attackerRuntime.AttackPhase == FighterBasicAttackRules.PhaseNone) {
                    attackerButtons = GameplayButtons.Special1;
                    pressed = true;
                }
            }
            PlayerInputFrame attackerFrame = PlayerInputFrame.Create(
                (uint)tick, attackerX, 0f, attackerButtons, attackerHeld);
            attackerHeld = attackerButtons;

            PlayerInputFrame selfFrame = default;
            if (cpuLive) {
                selfFrame = cpu.Sample((uint)tick, in self, in selfRuntime, in attacker, in attackerRuntime, in previousCpu);
                previousCpu = selfFrame;
                if (hitstunEnded
                    && (selfFrame.IsPressed(GameplayButtons.Jump) || selfFrame.IsPressed(GameplayButtons.MovementAbility))) {
                    result.RecoveryPresses++;
                }
            }
            if (cpuLive && (tick % 10 == 0 || selfFrame.Pressed != GameplayButtons.None
                || hanging || lastHitstun == 1)) {
                trace.Add($"t{tick} p({self.Position.x},{self.Position.y}) v({self.Velocity.x},{self.Velocity.y}) "
                    + $"stun{self.HitstunFrames} j{self.RemainingJumps} hang{selfRuntime.LedgeAnchor} "
                    + $"axis{selfFrame.MoveX} held{selfFrame.Held} press{selfFrame.Pressed}");
                if (trace.Count > 35) trace.RemoveAt(0);
            }
            simulation.Advance(attackerFrame, selfFrame);
        }
        result.Trace = string.Join("; ", trace);
        return result;
    }

    /// <summary>
    /// Lands player one's chained basics on a CPU-driven player two and counts
    /// the hits that got through. The CPU input path is live from the first
    /// frame of hitstun (before that the victim stands neutral, so the
    /// scenario is the defense policy and nothing else).
    /// </summary>
    private static int RunStringAgainstCpu(CpuBandTuning tuning) {
        // A zero-knockback attacker keeps the whole string in reach (the
        // BasicStringTestDriver contract); the victim therefore stays grounded
        // through the hitstun, which is exactly the hit-2 escape's condition.
        var simulation = new FighterSimulation(
            Loadout(basicDamage: 10, basicKnockback: FP64.Zero, maxHP: 100),
            Loadout(basicDamage: 10, basicKnockback: FP64.Zero, maxHP: 400),
            seed: 77, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var cpu = NewCpu(tuning, seed: 31);
        PlayerInputFrame previousCpu = default;
        bool cpuLive = false;
        int hits = 0;
        int lastHP = int.MinValue;
        int swings = 0;

        for (int tick = 0; tick < 420; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent attackerRuntime)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent victimRuntime)).IsTrue();
            if (lastHP == int.MinValue) lastHP = victim.CurrentHP;
            if (victim.CurrentHP < lastHP) hits++;
            lastHP = victim.CurrentHP;
            if (!cpuLive && victim.HitstunFrames > 0) cpuLive = true;

            bool press = swings == 0
                ? attackerRuntime.AttackPhase == FighterBasicAttackRules.PhaseNone
                : swings < 3 && attackerRuntime.AttackPhase == FighterBasicAttackRules.PhaseChainHold;
            if (press) swings++;
            GameplayButtons attackerButtons = press ? GameplayButtons.BasicAttack : GameplayButtons.None;
            var attackerFrame = new PlayerInputFrame { Held = attackerButtons, Pressed = attackerButtons };

            PlayerInputFrame victimFrame = default;
            if (cpuLive) {
                CpuDecisionObservation observation = FighterCpuController.Observe(
                    in victim, in victimRuntime, in attacker, in attackerRuntime);
                victimFrame = cpu.Sample((uint)tick, in observation, in previousCpu);
                previousCpu = victimFrame;
            }
            simulation.Advance(attackerFrame, victimFrame);
        }
        AssertThat(swings)
            .OverrideFailureMessage("The attacker's string never reached three swings — the scenario is broken.")
            .IsEqual(3);
        return hits;
    }

    /// <summary>
    /// Lands one launching opener and reports whether the CPU-driven victim
    /// teched the landing (grounded, hitstun over, tech invulnerability up).
    /// </summary>
    private static bool RunLauncherAgainstCpu(CpuBandTuning tuning) {
        // A launching melee Special 1 (knockback 4, authored Launches = true;
        // M05 made the string opener non-launching): the victim tumbles
        // airborne and touches back down inside the 30-frame hitstun — held
        // Block on that contact is the tech; a missed tech is the knockdown.
        var simulation = new FighterSimulation(
            Loadout(basicDamage: 10, basicKnockback: FP64.FromInt(4), maxHP: 100, launcherSpecial: true),
            Loadout(basicDamage: 10, basicKnockback: FP64.Zero, maxHP: 400),
            seed: 90, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var cpu = NewCpu(tuning, seed: 8);
        PlayerInputFrame previousCpu = default;
        bool cpuLive = false;
        bool pressed = false;
        bool sawHitstun = false;

        for (int tick = 0; tick < 240; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent attackerRuntime)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent victimRuntime)).IsTrue();
            if (victim.HitstunFrames > 0) {
                sawHitstun = true;
                cpuLive = true;
            }
            // The tech signature: the in-place tech lockout itself. (M05: the
            // missed-tech knockdown is ALSO grounded, hitstun-free and
            // invulnerable, so the old "invulnerable on the ground" read would
            // mistake a knockdown for a tech.)
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent victimVerb)).IsTrue();
            if (sawHitstun && victimVerb.TechLockoutFrames > 0) {
                return true;
            }

            bool press = !pressed && attackerRuntime.AttackPhase == FighterBasicAttackRules.PhaseNone;
            if (press) pressed = true;
            GameplayButtons attackerButtons = press ? GameplayButtons.Special1 : GameplayButtons.None;
            var attackerFrame = new PlayerInputFrame { Held = attackerButtons, Pressed = attackerButtons };

            PlayerInputFrame victimFrame = default;
            if (cpuLive) {
                CpuDecisionObservation observation = FighterCpuController.Observe(
                    in victim, in victimRuntime, in attacker, in attackerRuntime);
                victimFrame = cpu.Sample((uint)tick, in observation, in previousCpu);
                previousCpu = victimFrame;
            }
            simulation.Advance(attackerFrame, victimFrame);
        }
        AssertThat(sawHitstun)
            .OverrideFailureMessage("The opener never connected — the scenario is broken.")
            .IsTrue();
        return false;
    }

    [TestCase]
    public void TheDefyReflexWinsItsTickOverTheBlockHoldAndTheHoldResumesAfterIt() {
        // Meter and Defy update (O23): a Hard CPU (60 Influence: under its 70 reserve) holding Block through every hitstun still Defies on
        // the tick the second hit of a string opens, with Defy alone on that frame (an exclusive
        // press, like the hold itself), and the Block hold carries on the tick after.
        CpuBandTuning tuning = CpuBandTuning.Hard with {
            HitstunDefensePercent = 100, DiPercent = 0, GrabAdmissionPercent = 0
        };
        var cpu = NewCpu(tuning, seed: 5);
        PlayerInputFrame previous = default;
        var defyTicks = new System.Collections.Generic.List<int>();
        for (int hit = 0; hit < 2; hit++) {
            for (int hitstun = 20; hitstun >= 1; hitstun--) {
                CpuDecisionObservation observation = HitstunObservation(hitstun);
                observation.InfluenceRaw = FP64.FromInt(60).RawValue;
                int index = hit * 40 + (20 - hitstun);
                PlayerInputFrame frame = cpu.Sample(Tick(0, index), in observation, in previous);
                previous = frame;
                if (frame.IsPressed(GameplayButtons.Defy)) {
                    defyTicks.Add(index);
                    AssertThat(frame.IsHeld(GameplayButtons.Block))
                        .OverrideFailureMessage("The Defy tick carries Defy alone, not Block beside it.")
                        .IsFalse();
                } else {
                    AssertThat(frame.IsHeld(GameplayButtons.Block))
                        .OverrideFailureMessage($"Every other hitstun tick holds Block (hit {hit}, hitstun {hitstun}).")
                        .IsTrue();
                }
            }
            for (int rest = 0; rest < 4; rest++) {
                CpuDecisionObservation observation = HitstunObservation(0);
                observation.InfluenceRaw = FP64.FromInt(60).RawValue;
                previous = cpu.Sample(Tick(0, 20 + hit * 40 + rest), in observation, in previous);
            }
        }
        AssertThat(defyTicks.Count).OverrideFailureMessage("One Defy, on the second hit's first tick.").IsEqual(1);
        AssertThat(defyTicks[0]).IsEqual(40);
    }

    private static FighterCpuController NewCpu(CpuBandTuning tuning, int seed) =>
        new(CpuDifficulty.Hard, seed, null, null, tuning);

    /// <summary>Synthetic deterministic loadout (the Default kit's numbers with
    /// authorable basic damage/knockback and HP) — no resources involved.</summary>
    private static FighterLoadout Loadout(
        int basicDamage, FP64 basicKnockback, int maxHP, bool launcherSpecial = false) => new(
        (int)FighterCharacterID.Einstein,
        maxHP, 3, 2,
        basicDamage, 14, 12, 20,
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
        launcherSpecial ? LauncherModes : FighterAbilityLoadout.Default);

    /// <summary>
    /// M05: the Default kit with Special 1 turned into a melee launcher (30 f,
    /// Launches = true) — the one authored launcher this suite needs.
    /// </summary>
    private static FighterAbilityLoadout LauncherModes {
        get {
            FighterAbilityLoadout modes = FighterAbilityLoadout.Default;
            return modes with {
                SpecialOneExecutionType = 0,
                SpecialOneHit = new FighterAbilityHitData {
                    HitstunFrames = 30,
                    BlockClass = (int)BlockClass.Special,
                    Launches = true,
                    Delivery = (int)HitDelivery.DirectHit,
                    Origin = (int)HitOrigin.Special
                }
            };
        }
    }

    /// <summary>
    /// A tuning that isolates the hitstun-defense policy: every other rate is
    /// zero, so no scheduled decision can hold Block (or anything else) and
    /// contaminate the roll assertions.
    /// </summary>
    private static CpuBandTuning DefenseTuning(int hitstunDefense, int di) => new() {
        HitstunDefensePercent = hitstunDefense,
        DiPercent = di
    };

    /// <summary>A grounded observation carrying only hitstun and a distant target.</summary>
    private static CpuDecisionObservation HitstunObservation(int hitstunFrames) => new() {
        Stocks = 3,
        SelfCurrentHP = 100,
        SelfMaxHP = 100,
        IsGrounded = 1,
        HitstunFrames = hitstunFrames,
        TargetPositionXRaw = FP64.FromInt(4).RawValue
    };

    private static uint Tick(int instance, int frame) => (uint)(instance * 1000 + frame);
}
