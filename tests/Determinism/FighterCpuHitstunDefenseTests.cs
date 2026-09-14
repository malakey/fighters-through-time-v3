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

    // === Harness ===

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
        // A launching opener (knockback 4, the verb-suite scenario): the
        // victim tumbles airborne and touches back down inside the 30-frame
        // hitstun — held Block on that contact is the tech.
        var simulation = new FighterSimulation(
            Loadout(basicDamage: 10, basicKnockback: FP64.FromInt(4), maxHP: 100),
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
            // The tech signature (see FighterVerbLayerTests): grounded with
            // hitstun over inside the invulnerable in-place recovery.
            if (sawHitstun && victim.IsGrounded != 0 && victim.HitstunFrames == 0
                && victim.InvulnerabilityFrames > 0) {
                return true;
            }

            bool press = !pressed && attackerRuntime.AttackPhase == FighterBasicAttackRules.PhaseNone;
            if (press) pressed = true;
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
        AssertThat(sawHitstun)
            .OverrideFailureMessage("The opener never connected — the scenario is broken.")
            .IsTrue();
        return false;
    }

    private static FighterCpuController NewCpu(CpuBandTuning tuning, int seed) =>
        new(CpuDifficulty.Hard, seed, null, null, tuning);

    /// <summary>Synthetic deterministic loadout (the Default kit's numbers with
    /// authorable basic damage/knockback and HP) — no resources involved.</summary>
    private static FighterLoadout Loadout(int basicDamage, FP64 basicKnockback, int maxHP) => new(
        (int)FighterCharacterID.Einstein,
        maxHP, 3, 2,
        basicDamage, 14, 12, 20,
        600, 600,
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
        FighterAbilityLoadout.Default);

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
