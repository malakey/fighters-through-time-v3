using System.Collections.Generic;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using static GdUnit4.Assertions;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.Tests.Determinism;

/// <summary>
/// One scripted scenario per era hazard identity (`design-godot.md` Â§10 stage
/// table, Package 6 plan Â§4.1), plus the shared phase/block contracts and a
/// snapshot-restore convergence run through every hazard's active window.
///
/// <para>These are the tests that make the ten hazards genuinely distinct rather
/// than ten recolours of one static damage box: each asserts the behaviour that
/// only that identity produces â€” debris crossing the stage, a beam bouncing off
/// the walls while draining meter and dealing no damage, a rock falling from the
/// ceiling and leaving a slowing pool, a mortar launching upward exactly once,
/// quicksand that ignores airborne fighters, a searchlight that needs 90
/// consecutive dwell frames, a crowd that only pelts a fighter standing still,
/// and the widest artillery band striking once after a two-second sight line.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterHazardBehaviorTests {

    /// <summary>
    /// At High frequency the first hazard lands on the 1800-frame boundary, so
    /// every scenario warms up to there before its own script begins.
    /// </summary>
    private const int FirstHazardFrame = 1800;

    [TestCase]
    public void FlorenceSteamPipeIsAStaticVentThatTicksDamageInItsAuthoredBand() {
        var harness = new HazardHarness(FighterStageGeometry.Florence, FighterHazardTypeID.FlorenceSteamPipe, 6101);
        AssertThat(harness.StepUntilHazardExists()).IsTrue();
        FP64 anchorX = harness.Hazard().Position.x;

        AssertThat(harness.Hazard().Damage >= 5 && harness.Hazard().Damage <= 10).IsTrue();
        harness.StepWhileWarning(anchorX);

        FPVector2 positionAtIgnition = harness.Hazard().Position;
        int hpBefore = harness.Fighter(0).CurrentHP;
        for (int frame = 0; frame < 150; frame++) harness.StepToward(anchorX);

        // The steam pipe never moves â€” it is the shipped static identity.
        AssertThat(harness.Hazard().Position.x.RawValue).IsEqual(positionAtIgnition.x.RawValue);
        AssertThat(harness.Hazard().Position.y.RawValue).IsEqual(positionAtIgnition.y.RawValue);
        AssertThat(harness.Fighter(0).CurrentHP < hpBefore).IsTrue();
    }

    [TestCase]
    public void OrleansDebrisRollsFromAWallAcrossTheStageAndHitsEachFighterOnce() {
        var harness = new HazardHarness(FighterStageGeometry.Orleans, FighterHazardTypeID.OrleansTrebuchetDebris, 6102);
        AssertThat(harness.StepUntilHazardExists()).IsTrue();

        // The debris is launched over one of the two wall anchors (Â±8).
        FP64 spawnX = harness.Hazard().Position.x;
        AssertThat(FP64.Abs(spawnX).RawValue).IsEqual(FP64.FromInt(8).RawValue);
        harness.StepWhileWarning();

        bool rollsRight = spawnX < FP64.Zero;
        FP64 previousX = harness.Hazard().Position.x;
        bool crossedCentre = false;
        bool travelledOneWay = true;
        int hitMask = 0;
        for (int frame = 0; frame < 400 && harness.HasHazard() && harness.Hazard().Phase == 1; frame++) {
            harness.Step();
            if (!harness.HasHazard()) break;
            FighterHazardComponent hazard = harness.Hazard();
            hitMask |= hazard.HitMask;
            if (rollsRight ? hazard.Position.x <= previousX : hazard.Position.x >= previousX) travelledOneWay = false;
            previousX = hazard.Position.x;
            if (FP64.Abs(hazard.Position.x) < FP64.One) crossedCentre = true;
        }

        AssertThat(travelledOneWay).IsTrue();
        AssertThat(crossedCentre).IsTrue();
        // One hit per fighter per boulder: both fighters were run over exactly once.
        AssertThat(hitMask).IsEqual(0b11);
        AssertThat(harness.Fighter(0).CurrentHP).IsEqual(93);
        AssertThat(harness.Fighter(1).CurrentHP).IsEqual(93);
    }

    [TestCase]
    public void ChicagoInductionGridIsAWideStaticShockZoneThatAppliesStaticCharge() {
        var harness = new HazardHarness(FighterStageGeometry.Chicago, FighterHazardTypeID.ChicagoTeslaInduction, 6103);
        AssertThat(harness.StepUntilHazardExists()).IsTrue();

        FighterHazardComponent spawned = harness.Hazard();
        // Chicago's single anchor is stage centre, and the grid spans half the stage.
        AssertThat(spawned.Position.x.RawValue).IsEqual(FP64.Zero.RawValue);
        AssertThat(spawned.HalfExtents.x.RawValue).IsEqual(FP64.FromInt(4).RawValue);
        AssertThat(spawned.Damage).IsEqual(6);

        harness.StepWhileWarning(FP64.Zero);
        int hpBefore = harness.Fighter(0).CurrentHP;
        // M05 (Package 12 W3b): the grid's shock still launches, so a victim who
        // misses the tech lies in the invulnerable knockdown and its Static
        // Charge can run out before frame 120 — the status is asserted as
        // APPLIED during the window rather than still live at its end.
        bool charged = false;
        for (int frame = 0; frame < 120; frame++) {
            harness.StepToward(FP64.Zero);
            if (harness.Runtime(0).StatusType == (int)StatusType.StaticCharge) charged = true;
        }

        AssertThat(harness.Fighter(0).CurrentHP < hpBefore).IsTrue();
        AssertThat(charged).OverrideFailureMessage("The induction grid must apply Static Charge.").IsTrue();
    }

    [TestCase]
    public void ParisBeamSweepsBetweenTheWallsDrainingInfluenceWithoutDealingDamage() {
        var harness = new HazardHarness(FighterStageGeometry.Paris, FighterHazardTypeID.ParisDampeningBeam, 6104);

        // Build Influence first: the drain is unobservable from an empty meter.
        // Paris is an Open stage since A9, so closing the distance means crossing
        // the courtyard pit rather than walking a flat floor.
        harness.EngageOpponent(400);
        AssertThat(harness.Fighter(0).Influence > FP64.FromInt(10)).IsTrue();

        AssertThat(harness.StepUntilHazardExists()).IsTrue();
        AssertThat(harness.Hazard().Damage).IsEqual(0);
        harness.StepWhileWarning();

        FP64 influenceBefore = harness.Fighter(0).Influence;
        int hpBefore = harness.Fighter(0).CurrentHP;
        FP64 minimumX = harness.Hazard().Position.x;
        FP64 maximumX = minimumX;
        bool reversed = false;
        FP64 previousVelocityX = harness.Hazard().Velocity.x;
        for (int frame = 0; frame < 360 && harness.HasHazard() && harness.Hazard().Phase == 1; frame++) {
            harness.StepTowardOnFloor(harness.Hazard().Position.x);
            if (!harness.HasHazard()) break;
            FighterHazardComponent hazard = harness.Hazard();
            if (hazard.Position.x < minimumX) minimumX = hazard.Position.x;
            if (hazard.Position.x > maximumX) maximumX = hazard.Position.x;
            if (hazard.Velocity.x != FP64.Zero
                && previousVelocityX != FP64.Zero
                && (hazard.Velocity.x > FP64.Zero) != (previousVelocityX > FP64.Zero)) {
                reversed = true;
            }
            previousVelocityX = hazard.Velocity.x;
        }

        // It sweeps a real distance and bounces off a wall inside one active window.
        AssertThat(maximumX - minimumX > FP64.FromInt(4)).IsTrue();
        AssertThat(reversed).IsTrue();
        // Pure meter denial: the meter falls, the health bar does not.
        AssertThat(harness.Fighter(0).Influence < influenceBefore).IsTrue();
        AssertThat(harness.Fighter(0).CurrentHP).IsEqual(hpBefore);
    }

    /// <summary>
    /// V7.3 / design §10: the Dampening Beam drains 5% of the meter per
    /// second — 2.5 points on each 30-frame tick (the doc's number; the old
    /// 5-per-tick drained double the authored rate).
    /// </summary>
    [TestCase]
    public void DampeningBeamDrainsFivePercentPerSecond() {
        var harness = new HazardHarness(FighterStageGeometry.Paris, FighterHazardTypeID.ParisDampeningBeam, 6120);

        // Build a meter worth measuring, then go passive.
        harness.EngageOpponent(400);
        AssertThat(harness.Fighter(0).Influence > FP64.FromInt(20)).IsTrue();
        AssertThat(harness.StepUntilHazardExists()).IsTrue();
        harness.StepWhileWarning();

        // Chase the beam through its active sweep, logging every meter drop.
        var dropFrames = new System.Collections.Generic.List<int>();
        var dropSizes = new System.Collections.Generic.List<long>();
        FP64 previous = harness.Fighter(0).Influence;
        // The whole 360-frame active window: on an Open stage the beam has to
        // cross the courtyard before it reaches the walkway the fighter can
        // actually stand on, so the first contact lands later than it did on the
        // old flat Paris floor.
        for (int frame = 0; frame < 400 && harness.HasHazard()
             && harness.Hazard().Phase == FighterHazardSystem.ActivePhase; frame++) {
            harness.StepTowardOnFloor(harness.Hazard().Position.x);
            FP64 current = harness.Fighter(0).Influence;
            if (current < previous) {
                dropFrames.Add(frame);
                dropSizes.Add((previous - current).RawValue);
            }
            previous = current;
        }

        AssertThat(dropFrames.Count >= 3)
            .OverrideFailureMessage("The tracked fighter must eat at least three drain ticks.")
            .IsTrue();
        foreach (long size in dropSizes) {
            AssertThat(size)
                .OverrideFailureMessage("Each 30-frame tick drains exactly 2.5 meter points (5%/s).")
                .IsEqual(FP64.FromDouble(2.5).RawValue);
        }
        for (int index = 1; index < dropFrames.Count; index++) {
            AssertThat(dropFrames[index] - dropFrames[index - 1])
                .OverrideFailureMessage("Drain ticks land every 30 frames — two per second.")
                .IsEqual(30);
        }
    }

    /// <summary>
    /// Audit M-11 / design-godot.md ~1570 ("completely invulnerable while
    /// standing on the respawn platform"): the beam's meter drain must honour
    /// the same invulnerability gate as every damaging effect. The legacy flat
    /// arena is the one geometry whose bottom blast zone is reachable, so the
    /// script builds meter, rides the bottom blast zone just before the
    /// 1800-frame hazard boundary, and then sits pinned at (0, 3) — inside the
    /// full-height beam column — through the platform hold and the post-drop
    /// invulnerability window. The beam always crosses stage centre within its
    /// first 140 active frames (it ignites moving toward the far wall), so the
    /// overlap the assertion depends on is guaranteed, not seed luck.
    /// </summary>
    [TestCase]
    public void ParisBeamCannotDrainMeterFromAnInvulnerableRespawningFighter() {
        var harness = new HazardHarness(null, FighterHazardTypeID.ParisDampeningBeam, 6112);

        // Build a visible meter, then stop swinging so timing stays scripted.
        for (int frame = 0; frame < 500 && harness.Fighter(0).Influence < FP64.FromInt(10); frame++) {
            GameplayButtons buttons = frame % 40 == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None;
            harness.StepToward(harness.Fighter(1).Position.x, buttons);
        }
        AssertThat(harness.Fighter(0).Influence >= FP64.FromInt(10)).IsTrue();

        // Idle to just before the hazard boundary, then drop through the legacy
        // floor and ride the fall to a stock loss.
        while (harness.Tick < 1560) harness.Step();
        bool onPlatform = false;
        for (int frame = 0; frame < 200 && !onPlatform; frame++) {
            harness.Step(0, frame == 0
                ? GameplayButtons.Jump | GameplayButtons.Down
                : GameplayButtons.Down);
            FighterStateComponent state = harness.Fighter(0);
            onPlatform = FighterMatchFlowRules.IsOnRespawnPlatform(in state);
        }
        AssertThat(onPlatform).IsTrue();

        // Stock loss retains 75% meter, so there is something to protect.
        FP64 protectedMeter = harness.Fighter(0).Influence;
        AssertThat(protectedMeter > FP64.Zero).IsTrue();

        bool sawInvulnerableOverlap = false;
        for (int guard = 0; guard < 900; guard++) {
            FighterStateComponent before = harness.Fighter(0);
            if (before.InvulnerabilityFrames <= 0 && !FighterMatchFlowRules.IsOnRespawnPlatform(in before)) break;
            harness.Step();
            FighterStateComponent fighter = harness.Fighter(0);
            if (fighter.InvulnerabilityFrames <= 0) continue;
            if (harness.HasHazard()) {
                FighterHazardComponent hazard = harness.Hazard();
                if (hazard.Phase == FighterHazardSystem.ActivePhase
                    && FP64.Abs(hazard.Position.x - fighter.Position.x) <= hazard.HalfExtents.x + FP64.FromDouble(0.5)
                    && FP64.Abs(hazard.Position.y - fighter.Position.y) <= hazard.HalfExtents.y + FP64.One) {
                    sawInvulnerableOverlap = true;
                }
            }
            if (fighter.Influence.RawValue != protectedMeter.RawValue) {
                AssertThat($"meter moved at tick {harness.Tick} while invulnerable").IsEqual("");
            }
        }

        AssertThat(sawInvulnerableOverlap).IsTrue();
        AssertThat(harness.Fighter(0).Influence.RawValue).IsEqual(protectedMeter.RawValue);
    }

    [TestCase]
    public void VesuviusRockFallsFromTheCeilingThenLeavesATimeDilationPool() {
        var harness = new HazardHarness(FighterStageGeometry.Vesuvius, FighterHazardTypeID.VesuviusRockfall, 6105);
        AssertThat(harness.StepUntilHazardExists()).IsTrue();

        // The telegraph is a ground marker at the anchor; the rock comes later.
        AssertThat(harness.Hazard().Position.y.RawValue).IsEqual(FP64.Zero.RawValue);
        FP64 anchorX = harness.Hazard().Position.x;
        harness.StepWhileWarning(anchorX);

        // Ignition relocates the hazard to the ceiling as a falling rock.
        AssertThat(harness.Hazard().SubTypeID).IsEqual(0);
        AssertThat(harness.Hazard().Position.y.RawValue)
            .IsEqual(FighterStageGeometry.Vesuvius.Ceiling.RawValue);
        AssertThat(harness.Hazard().Velocity.y < FP64.Zero).IsTrue();

        FP64 previousY = harness.Hazard().Position.y;
        bool descended = true;
        for (int frame = 0; frame < 200 && harness.Hazard().SubTypeID == 0; frame++) {
            harness.StepToward(anchorX);
            if (harness.Hazard().Position.y > previousY) descended = false;
            previousY = harness.Hazard().Position.y;
        }
        AssertThat(descended).IsTrue();

        // Impact shatters the rock into a ground pool.
        FighterHazardComponent pool = harness.Hazard();
        AssertThat(pool.SubTypeID).IsEqual(1);
        AssertThat(pool.Position.y.RawValue).IsEqual(FP64.Zero.RawValue);
        AssertThat(pool.HalfExtents.x.RawValue).IsEqual(FP64.One.RawValue);
        // The impact itself is the only damage the rockfall deals.
        AssertThat(harness.Fighter(0).CurrentHP).IsEqual(92);

        int hpAfterImpact = harness.Fighter(0).CurrentHP;
        for (int frame = 0; frame < 120; frame++) harness.StepToward(anchorX);
        AssertThat(harness.Runtime(0).StatusType).IsEqual((int)StatusType.TimeDilation);
        AssertThat(harness.Fighter(0).CurrentHP).IsEqual(hpAfterImpact);
    }

    [TestCase]
    public void NassauMortarTelegraphsThenLaunchesEachFighterUpwardExactlyOnce() {
        var harness = new HazardHarness(FighterStageGeometry.Nassau, FighterHazardTypeID.NassauMortar, 6106);
        AssertThat(harness.StepUntilHazardExists()).IsTrue();

        FighterHazardComponent spawned = harness.Hazard();
        AssertThat(spawned.WarningFrames).IsEqual(90);
        AssertThat(spawned.ActiveFrames).IsEqual(30);
        AssertThat(spawned.Damage).IsEqual(10);
        FP64 anchorX = spawned.Position.x;
        harness.StepWhileWarning(anchorX);

        int hpBefore = harness.Fighter(0).CurrentHP;
        bool hit = false;
        for (int frame = 0; frame < 40 && harness.HasHazard(); frame++) {
            harness.StepToward(anchorX);
            if (harness.Fighter(0).CurrentHP < hpBefore) { hit = true; break; }
        }
        AssertThat(hit).IsTrue();

        FighterStateComponent launched = harness.Fighter(0);
        AssertThat(launched.CurrentHP).IsEqual(hpBefore - 10);
        AssertThat(launched.IsGrounded).IsEqual(0);
        // The mortar is a launcher: the vertical impulse beats the horizontal one.
        AssertThat(launched.Velocity.y > FP64.Abs(launched.Velocity.x)).IsTrue();

        // One shell, one hit: the rest of the explosion window costs nothing more.
        int hpAfterHit = launched.CurrentHP;
        for (int frame = 0; frame < 40; frame++) harness.StepToward(anchorX);
        AssertThat(harness.Fighter(0).CurrentHP).IsEqual(hpAfterHit);
    }

    [TestCase]
    public void AlexandriaSinkholeGripsGroundedFightersAndIgnoresAirborneOnes() {
        var harness = new HazardHarness(
            FighterStageGeometry.Alexandria, FighterHazardTypeID.AlexandriaSinkhole, 6107);
        AssertThat(harness.StepUntilHazardExists()).IsTrue();

        FighterHazardComponent spawned = harness.Hazard();
        AssertThat(spawned.Damage).IsEqual(2);
        AssertThat(spawned.ActiveFrames).IsEqual(480);
        FP64 anchorX = spawned.Position.x;
        harness.StepWhileWarning(anchorX);

        int hpBefore = harness.Fighter(0).CurrentHP;
        bool damaged = false;
        for (int frame = 0; frame < 200; frame++) {
            harness.StepToward(anchorX);
            if (harness.Fighter(0).CurrentHP < hpBefore) { damaged = true; break; }
        }
        AssertThat(damaged).IsTrue();
        AssertThat(harness.Fighter(0).CurrentHP).IsEqual(hpBefore - 2);
        // The design's "reducing speed by 50%" is TimeDilation at full intensity.
        AssertThat(harness.Runtime(0).StatusType).IsEqual((int)StatusType.TimeDilation);
        AssertThat(harness.Runtime(0).StatusIntensity.RawValue).IsEqual(FP64.One.RawValue);

        // Replay the identical match, jumping two frames before the tick that hit,
        // and the quicksand finds nothing to grip.
        List<PlayerInputFrame> recorded = harness.RecordedInputs;
        int damageFrame = recorded.Count - 1;
        var replay = HazardHarness.NewSimulation(
            FighterStageGeometry.Alexandria, FighterHazardTypeID.AlexandriaSinkhole, 6107);
        for (int index = 0; index < recorded.Count; index++) {
            PlayerInputFrame input = recorded[index];
            if (index == damageFrame - 2) {
                input.Held |= GameplayButtons.Jump;
                input.Pressed |= GameplayButtons.Jump;
            }
            replay.Advance(input, new PlayerInputFrame { Tick = (uint)index });
        }
        replay.TryGetFighter(0, out FighterStateComponent airborne);
        AssertThat(airborne.IsGrounded).IsEqual(0);
        AssertThat(airborne.CurrentHP).IsEqual(hpBefore);
    }

    [TestCase]
    public void BerlinSearchlightOnlyFiresAfterNinetyConsecutiveDwellFrames() {
        var harness = new HazardHarness(FighterStageGeometry.Berlin, FighterHazardTypeID.BerlinSearchlight, 6108);
        AssertThat(harness.StepUntilHazardExists()).IsTrue();
        FP64 anchorX = harness.Hazard().Position.x;
        harness.StepWhileWarning(anchorX);

        int hpBefore = harness.Fighter(0).CurrentHP;
        int firstDwellFrame = -1;
        int damageFrame = -1;
        for (int frame = 0; frame < 400 && harness.HasHazard(); frame++) {
            harness.StepToward(anchorX);
            if (!harness.HasHazard()) break;
            if (firstDwellFrame < 0 && harness.Hazard().DwellFramesPlayerOne == 1) firstDwellFrame = frame;
            if (harness.Fighter(0).CurrentHP < hpBefore) { damageFrame = frame; break; }
        }

        AssertThat(firstDwellFrame >= 0).IsTrue();
        AssertThat(damageFrame >= 0).IsTrue();
        // The beam itself is harmless; the drone answers on the 90th consecutive frame.
        AssertThat(damageFrame - firstDwellFrame)
            .IsEqual(FighterHazardSystem.SearchlightDwellFrames - 1);
        AssertThat(harness.Fighter(0).CurrentHP).IsEqual(hpBefore - 8);
        AssertThat(harness.Hazard().DwellFramesPlayerOne).IsEqual(0);
    }

    [TestCase]
    public void GlobeHecklersPeltTheIdleFighterAndNeverTheMovingOne() {
        var harness = new HazardHarness(FighterStageGeometry.Globe, FighterHazardTypeID.GlobeAudienceHeckle, 6109);
        AssertThat(harness.StepUntilHazardExists()).IsTrue();
        harness.StepWhileWarning();

        // Player one paces; player two camps.
        for (int frame = 0; frame < 200; frame++) {
            harness.Step(frame / 30 % 2 == 0 ? (sbyte)127 : (sbyte)-127);
        }

        AssertThat(harness.Fighter(1).CurrentHP).IsEqual(95);
        AssertThat(harness.Fighter(0).CurrentHP).IsEqual(100);
    }

    [TestCase]
    public void GettysburgArtilleryUsesATwoSecondSightLineThenStrikesOnce() {
        var harness = new HazardHarness(
            FighterStageGeometry.Gettysburg, FighterHazardTypeID.GettysburgArtillery, 6110);
        AssertThat(harness.StepUntilHazardExists()).IsTrue();

        FighterHazardComponent spawned = harness.Hazard();
        // The only 2 s telegraph, the widest band, and the hardest single hit.
        AssertThat(spawned.WarningFrames).IsEqual(120);
        AssertThat(spawned.ActiveFrames).IsEqual(30);
        AssertThat(spawned.HalfExtents.x.RawValue).IsEqual(FP64.FromInt(5).RawValue);
        AssertThat(spawned.Damage).IsEqual(12);
        AssertThat(spawned.Knockback.x.RawValue).IsEqual(FP64.FromInt(6).RawValue);

        FP64 anchorX = spawned.Position.x;
        int hpBefore = harness.Fighter(0).CurrentHP;
        harness.StepWhileWarning(anchorX);
        // Nothing happens while the sight line is up.
        AssertThat(harness.Fighter(0).CurrentHP).IsEqual(hpBefore);

        bool struck = false;
        for (int frame = 0; frame < 40 && harness.HasHazard(); frame++) {
            harness.StepToward(anchorX);
            if (harness.Fighter(0).CurrentHP < hpBefore) { struck = true; break; }
        }
        AssertThat(struck).IsTrue();
        AssertThat(harness.Fighter(0).CurrentHP).IsEqual(hpBefore - 12);

        int hpAfterStrike = harness.Fighter(0).CurrentHP;
        for (int frame = 0; frame < 40; frame++) harness.StepToward(anchorX);
        AssertThat(harness.Fighter(0).CurrentHP).IsEqual(hpAfterStrike);
    }

    /// <summary>
    /// Package 6 plan Â§2.4 / `design-godot.md` Â§10 "Block Compatibility": a hazard
    /// damage tick is a basic attack, so an active front-facing block absorbs it at
    /// the cost of exactly one shield charge.
    /// </summary>
    [TestCase]
    public void BlockingAbsorbsAHazardTickForOneShieldCharge() {
        int unblockedHP = RunSteamPipeStandoff(blocking: false, out int unblockedCharges);
        int blockedHP = RunSteamPipeStandoff(blocking: true, out int blockedCharges);

        AssertThat(unblockedHP < 100).IsTrue();
        AssertThat(unblockedCharges).IsEqual(3);
        AssertThat(blockedHP).IsEqual(100);
        AssertThat(blockedCharges < 3).IsTrue();
        AssertThat(blockedCharges > 0).IsTrue();
    }

    /// <summary>
    /// A snapshot taken on the last warning frame and restored into a fresh
    /// simulation must resimulate the entire active window â€” movement, dwell
    /// counters, one-shot masks and sub-type transitions included â€” bit-identically.
    /// </summary>
    [TestCase]
    public void EveryHazardConvergesThroughSnapshotRestoreAcrossItsActiveWindow() {
        for (int hazardTypeID = 1; hazardTypeID <= FighterHazardTypeID.Count; hazardTypeID++) {
            FighterStageGeometry geometry = GeometryForHazard(hazardTypeID);
            int seed = 7100 + hazardTypeID;
            FighterSimulation first = HazardHarness.NewSimulation(geometry, hazardTypeID, seed);
            for (int tick = 0; tick < FirstHazardFrame + 89; tick++) {
                first.Advance(ConvergenceInput(tick), new PlayerInputFrame { Tick = (uint)tick });
            }

            byte[] snapshot = first.CaptureFullState();
            FighterSimulation restored = HazardHarness.NewSimulation(geometry, hazardTypeID, seed);
            restored.RestoreFullState(snapshot);

            for (int tick = FirstHazardFrame + 89; tick < FirstHazardFrame + 800; tick++) {
                PlayerInputFrame p1 = ConvergenceInput(tick);
                var p2 = new PlayerInputFrame { Tick = (uint)tick };
                long expected = first.Advance(p1, p2);
                long actual = restored.Advance(p1, p2);
                if (expected != actual) {
                    AssertThat($"hazard {hazardTypeID} diverged at tick {tick}").IsEqual("");
                }
            }
        }
    }

    /// <summary>Every hazard runs warning â†’ active â†’ recovery and then disappears.</summary>
    [TestCase]
    public void EveryHazardRunsWarningActiveAndRecoveryBeforeDespawning() {
        for (int hazardTypeID = 1; hazardTypeID <= FighterHazardTypeID.Count; hazardTypeID++) {
            var harness = new HazardHarness(GeometryForHazard(hazardTypeID), hazardTypeID, 7300 + hazardTypeID);
            AssertThat(harness.StepUntilHazardExists()).IsTrue();
            AssertThat(harness.Hazard().Phase).IsEqual(FighterHazardSystem.WarningPhase);
            AssertThat(harness.Hazard().CooldownFrames).IsEqual(FighterHazardSystem.RecoveryFrames);

            bool sawActive = false;
            bool sawRecovery = false;
            bool despawned = false;
            for (int frame = 0; frame < 1400; frame++) {
                harness.Step();
                if (!harness.HasHazard()) { despawned = true; break; }
                if (harness.Hazard().Phase == FighterHazardSystem.ActivePhase) sawActive = true;
                if (harness.Hazard().Phase == FighterHazardSystem.RecoveryPhase) sawRecovery = true;
            }
            if (!sawActive || !sawRecovery || !despawned) {
                AssertThat($"hazard {hazardTypeID} phase chain incomplete").IsEqual("");
            }
        }
    }

    /// <summary>
    /// Walks player one up to a hold line one unit short of the steam vent on the
    /// side it approached from, then stops. The one-directional approach matters:
    /// a fighter that overshoots and steps back flips its facing, and a hazard hit
    /// from behind is not blockable by design.
    /// </summary>
    private static int RunSteamPipeStandoff(bool blocking, out int blockCharges) {
        var harness = new HazardHarness(FighterStageGeometry.Florence, FighterHazardTypeID.FlorenceSteamPipe, 6111);
        harness.StepUntilHazardExists();
        FP64 anchorX = harness.Hazard().Position.x;
        bool approachFromLeft = harness.Fighter(0).Position.x < anchorX;
        FP64 holdLine = approachFromLeft ? anchorX - FP64.One : anchorX + FP64.One;

        for (int guard = 0; guard < 200; guard++) {
            if (!harness.HasHazard() || harness.Hazard().Phase != FighterHazardSystem.WarningPhase) break;
            harness.Step(ApproachAxis(harness, holdLine, approachFromLeft));
        }
        for (int frame = 0; frame < 35; frame++) {
            harness.Step(
                ApproachAxis(harness, holdLine, approachFromLeft),
                blocking ? GameplayButtons.Block : GameplayButtons.None);
        }
        blockCharges = harness.Fighter(0).BlockCharges;
        return harness.Fighter(0).CurrentHP;
    }

    private static sbyte ApproachAxis(HazardHarness harness, FP64 holdLine, bool fromLeft) {
        FP64 x = harness.Fighter(0).Position.x;
        if (fromLeft) return x < holdLine ? (sbyte)127 : (sbyte)0;
        return x > holdLine ? (sbyte)-127 : (sbyte)0;
    }

    private static FighterStageGeometry GeometryForHazard(int hazardTypeID) => hazardTypeID switch {
        FighterHazardTypeID.OrleansTrebuchetDebris => FighterStageGeometry.Orleans,
        FighterHazardTypeID.ChicagoTeslaInduction => FighterStageGeometry.Chicago,
        FighterHazardTypeID.ParisDampeningBeam => FighterStageGeometry.Paris,
        FighterHazardTypeID.VesuviusRockfall => FighterStageGeometry.Vesuvius,
        FighterHazardTypeID.NassauMortar => FighterStageGeometry.Nassau,
        FighterHazardTypeID.AlexandriaSinkhole => FighterStageGeometry.Alexandria,
        FighterHazardTypeID.BerlinSearchlight => FighterStageGeometry.Berlin,
        FighterHazardTypeID.GlobeAudienceHeckle => FighterStageGeometry.Globe,
        FighterHazardTypeID.GettysburgArtillery => FighterStageGeometry.Gettysburg,
        _ => FighterStageGeometry.Florence
    };

    private static PlayerInputFrame ConvergenceInput(int tick) {
        sbyte axis = (sbyte)((tick % 9 - 4) * 30);
        GameplayButtons buttons = GameplayButtons.None;
        if (tick % 53 == 0) buttons |= GameplayButtons.Jump;
        if (tick % 71 == 0) buttons |= GameplayButtons.Block;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = axis,
            Held = buttons,
            Pressed = buttons
        };
    }

    /// <summary>
    /// Drives one hazard scenario: player one is scriptable and steerable, player
    /// two idles at its spawn, and every applied input is recorded so a scenario can
    /// be replayed with a single frame changed.
    /// </summary>
    private sealed class HazardHarness {
        private static readonly FP64 SteerBand = FP64.FromDouble(0.15);

        private readonly FighterSimulation _simulation;
        private readonly FighterStageGeometry _geometry;
        private int _tick;

        public readonly List<PlayerInputFrame> RecordedInputs = new();

        public HazardHarness(FighterStageGeometry geometry, int hazardTypeID, int seed) {
            _geometry = geometry;
            _simulation = NewSimulation(geometry, hazardTypeID, seed);
        }

        public static FighterSimulation NewSimulation(
            FighterStageGeometry geometry, int hazardTypeID, int seed) {
            FighterMatchRules rules = new(
                (int)MatchMode.Stock,
                itemsEnabled: false,
                itemFrequency: 0,
                hazardsEnabled: true,
                hazardCadenceFrames: 1800,
                stageHazardTypeID: hazardTypeID);
            return new FighterSimulation(seed: seed, rules: rules, stageGeometry: geometry);
        }

        public void Step(sbyte moveX = 0, GameplayButtons buttons = GameplayButtons.None) {
            var input = new PlayerInputFrame {
                Tick = (uint)_tick,
                MoveX = moveX,
                Held = buttons,
                Pressed = buttons
            };
            RecordedInputs.Add(input);
            _simulation.Advance(input, new PlayerInputFrame { Tick = (uint)_tick });
            _tick++;
        }

        public void StepToward(FP64 targetX, GameplayButtons buttons = GameplayButtons.None) {
            FP64 dx = targetX - Fighter(0).Position.x;
            sbyte axis = dx > SteerBand ? (sbyte)127 : dx < -SteerBand ? (sbyte)-127 : (sbyte)0;
            Step(axis, buttons);
        }

        /// <summary>
        /// Walks toward <paramref name="targetX"/> without stepping off the floor
        /// segment the fighter is standing on. Package 11 A9 gave three stages a
        /// real pit, so a naive chase at a sweeping hazard now ends in a ledge
        /// hang over the hole instead of under the hazard. Sealed stages (and the
        /// legacy flat arena) have no segments, so this is plain
        /// <see cref="StepToward"/> there.
        /// </summary>
        public void StepTowardOnFloor(FP64 targetX, GameplayButtons buttons = GameplayButtons.None) =>
            StepToward(ClampToSupportingFloor(targetX), buttons);

        /// <summary>
        /// Clamps <paramref name="targetX"/> into the floor segment currently
        /// under player one, leaving a full unit of margin inside the ledge —
        /// enough for the twelve-frame stop ramp, which otherwise carries a
        /// running fighter over the edge and straight into a ledge hang.
        /// </summary>
        private FP64 ClampToSupportingFloor(FP64 targetX) {
            if (_geometry == null || _geometry.FloorSegments.Length == 0) return targetX;
            FP64 fighterX = Fighter(0).Position.x;
            FP64 margin = FP64.One;
            foreach (FighterStagePlatform segment in _geometry.FloorSegments) {
                if (!segment.Supports(fighterX)) continue;
                FP64 left = segment.EdgeX(0) + margin;
                FP64 right = segment.EdgeX(1) - margin;
                return targetX < left ? left : targetX > right ? right : targetX;
            }
            return targetX;
        }

        /// <summary>
        /// Builds player one's Influence by closing on player two and swinging.
        /// On an Open stage the two spawns are separated by the pit, so the script
        /// leaps the gap and drops through the landing platform first; on a Sealed
        /// floor it degenerates to the walk-and-swing this suite always used.
        /// </summary>
        public void EngageOpponent(int frames) {
            for (int frame = 0; frame < frames; frame++) {
                FighterStateComponent self = Fighter(0);
                FP64 dx = Fighter(1).Position.x - self.Position.x;
                sbyte axis = dx > SteerBand ? (sbyte)127 : dx < -SteerBand ? (sbyte)-127 : (sbyte)0;
                bool closed = FP64.Abs(dx) <= FP64.FromInt(2);
                bool overhead = self.Position.y > FP64.One;
                GameplayButtons buttons;
                if (!closed) {
                    // Hop the courtyard. Harmless on a stage with no gap to cross.
                    buttons = frame % 12 == 0 ? GameplayButtons.Jump : GameplayButtons.None;
                } else if (overhead) {
                    // Standing on the walkway above the opponent: drop through it.
                    axis = 0;
                    buttons = frame % 12 == 0
                        ? GameplayButtons.Down | GameplayButtons.Jump
                        : GameplayButtons.Down;
                } else {
                    buttons = frame % 40 == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None;
                }
                Step(axis, buttons);
            }
        }

        /// <summary>Advances to the frame the first hazard becomes observable.</summary>
        public bool StepUntilHazardExists() {
            for (int guard = 0; guard < FirstHazardFrame + 120; guard++) {
                if (HasHazard()) return true;
                Step();
            }
            return HasHazard();
        }

        /// <summary>Runs out the telegraph, optionally walking player one somewhere.</summary>
        public void StepWhileWarning() => StepWhileWarning(null);

        public void StepWhileWarning(FP64 targetX) => StepWhileWarning((FP64?)targetX);

        private void StepWhileWarning(FP64? targetX) {
            for (int guard = 0; guard < 200; guard++) {
                if (!HasHazard() || Hazard().Phase != FighterHazardSystem.WarningPhase) return;
                if (targetX.HasValue) StepToward(targetX.Value);
                else Step();
            }
        }

        public int Tick => _tick;

        public bool HasHazard() => _simulation.TryGetFirstHazard(out _);

        public FighterHazardComponent Hazard() {
            _simulation.TryGetFirstHazard(out FighterHazardComponent hazard);
            return hazard;
        }

        public FighterStateComponent Fighter(int playerID) {
            _simulation.TryGetFighter(playerID, out FighterStateComponent fighter);
            return fighter;
        }

        public FighterRuntimeComponent Runtime(int playerID) {
            _simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime);
            return runtime;
        }
    }
}

