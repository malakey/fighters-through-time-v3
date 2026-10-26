using System;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The CPU's neutral melee band (2026-10-11, ledger
/// <c>PROVISIONAL-CPU-MELEE-BAND</c>). Before it, the neutral close range
/// (2.0 units) decided both "commit the close verbs" and "press BasicAttack",
/// so a CPU Lincoln (hit-1 pivot reach 1.86) or Joan (1.94) pressed its
/// opener from up to 2.0 and whiffed. Only the BasicAttack opener now reads a
/// per-fighter band: hit 1's far edge from the one cross-mode string table,
/// scaled by the fighter's reach width, plus the target's hurtbox half-width,
/// less <see cref="FighterCpuController.MeleeBandMarginUnits"/>. The Ultimate
/// commit, the grab, the threat roll and the close Special ladder keep the
/// close range. A press that continues a running string reads the band of the
/// hit it would start, and delivery re-reads that step, so a press decided
/// mid-string that arrives after the string has ended is gated as an opener.
/// </summary>
/// <remarks>
/// Pure C#: hand-built observations plus one default-loadout simulation, no
/// Godot runtime.
/// </remarks>
[TestSuite]
public class CpuMeleeBandTests {
    private const int Ticks = 300;
    private static readonly FP64 CloseRange = FP64.FromInt(2);

    [TestCase]
    public void TheMeleeBandSitsTheMarginInsideEachProfilesSimHitOneReach() {
        FP64 margin = FP64.FromDouble(FighterCpuController.MeleeBandMarginUnits);
        var issues = new System.Collections.Generic.List<string>();
        foreach (FighterCharacterID id in Enum.GetValues<FighterCharacterID>()) {
            FTT.Combat.BasicStringProfile profile = FighterBasicAttackRules.StringProfileFor((int)id);
            var attacker = new FighterStateComponent { FacingRight = 1, CharacterID = (int)id };
            FighterCombatSystem.ResolveStringBox(in attacker, in profile, 0, out FPVector2 center, out FPVector2 half);
            var victimHalf = new FPVector2(FighterCombatSystem.VictimHurtboxHalfWidth, FP64.One);
            FP64 band = FighterCpuController.MeleeBand(profile.ReachWidthPercent, 0);

            // The band is the sim's own box, not a copy of its numbers: a victim
            // standing exactly at the band overlaps the opener's box, and one a
            // margin past the band's exact reach does not.
            FP64 exactReach = center.x + half.x + FighterCombatSystem.VictimHurtboxHalfWidth;
            if (band.RawValue != (exactReach - margin).RawValue) {
                issues.Add($"{id}: band {Describe(band)} != reach {Describe(exactReach)} - margin.");
            }
            if (!FighterEntityQueries.Overlaps(in center, in half, new FPVector2(band, FP64.Zero), in victimHalf)) {
                issues.Add($"{id}: a victim at the band ({Describe(band)}) is outside the opener's box.");
            }
            if (FighterEntityQueries.Overlaps(
                    in center, in half, new FPVector2(exactReach + margin, FP64.Zero), in victimHalf)) {
                issues.Add($"{id}: a victim past the exact reach still overlaps the opener's box.");
            }
            // A target's real hurtbox half-width (the Mirror supplies the hero's)
            // moves the band by exactly that difference.
            FP64 narrow = FP64.FromDouble(0.25);
            FP64 narrowBand = FighterCpuController.MeleeBand(profile.ReachWidthPercent, narrow.RawValue);
            if (narrowBand.RawValue != (band - FighterCombatSystem.VictimHurtboxHalfWidth + narrow).RawValue) {
                issues.Add($"{id}: a 0.25-unit target half-width gave band {Describe(narrowBand)}.");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

        // The premise of the fix: the two short-reach kits open from inside the
        // close range, so the close range alone would have whiffed them.
        AssertThat(FighterCpuController.MeleeBand(ReachOf(FighterCharacterID.Lincoln), 0) < CloseRange).IsTrue();
        AssertThat(FighterCpuController.MeleeBand(ReachOf(FighterCharacterID.Joan), 0) < CloseRange).IsTrue();
    }

    [TestCase]
    public void AShortReachCpuWalksInInsteadOfWhiffingItsOpenerFromInsideCloseRange() {
        foreach (CpuDifficulty band in new[] { CpuDifficulty.Easy, CpuDifficulty.Normal, CpuDifficulty.Hard }) {
            foreach (FighterCharacterID id in new[] { FighterCharacterID.Lincoln, FighterCharacterID.Joan }) {
                // 1.9 units: inside the 2.0 close range, outside the opener's band.
                CpuDecisionObservation outside = Facing(id, targetX: 1.9);
                var cpu = new FighterCpuController(band, 4242, null, null, BasicsOnly(band));
                (int edges, int walkIn) = Run(cpu, outside, GameplayButtons.BasicAttack);
                AssertThat(edges)
                    .OverrideFailureMessage($"{band} {id}: the opener was pressed from beyond its reach.")
                    .IsEqual(0);
                AssertThat(walkIn > 0)
                    .OverrideFailureMessage($"{band} {id}: the CPU neither swung nor walked in.")
                    .IsTrue();

                // Inside the band the same CPU swings.
                CpuDecisionObservation inside = Facing(id, targetX: 1.5);
                var swinging = new FighterCpuController(band, 4242, null, null, BasicsOnly(band));
                AssertThat(Run(swinging, inside, GameplayButtons.BasicAttack).Edges > 0)
                    .OverrideFailureMessage($"{band} {id}: no opener from inside its band.")
                    .IsTrue();
            }

            // A template-reach kit already reaches 1.9 and keeps swinging.
            var template = new FighterCpuController(band, 4242, null, null, BasicsOnly(band));
            AssertThat(Run(template, Facing(FighterCharacterID.Tesla, targetX: 1.9), GameplayButtons.BasicAttack).Edges > 0)
                .OverrideFailureMessage($"{band} Tesla: the template opener stopped at 1.9.")
                .IsTrue();
        }
    }

    [TestCase]
    public void AContinuationReadsItsNextHitsBandAndAnUnknownReachKeepsTheCloseRange() {
        // A press inside the CPU's own string continues it, so it reads the
        // band of the hit it would start. Lincoln's hit 2 band is 1.845 units,
        // which is still short of the 2.0 close range: from 1.9 his hit 2
        // would whiff, so he walks in; from 1.8 he presses.
        foreach (CpuDifficulty band in new[] { CpuDifficulty.Easy, CpuDifficulty.Normal, CpuDifficulty.Hard }) {
            CpuDecisionObservation hitTwoShort = Facing(FighterCharacterID.Lincoln, targetX: 1.9);
            hitTwoShort.SelfNextStringStep = 1;
            var shortCpu = new FighterCpuController(band, 77, null, null, BasicsOnly(band));
            (int shortEdges, int shortWalk) = Run(shortCpu, hitTwoShort, GameplayButtons.BasicAttack);
            AssertThat(shortEdges)
                .OverrideFailureMessage($"{band}: Lincoln pressed hit 2 from beyond its 1.845-unit band.")
                .IsEqual(0);
            AssertThat(shortWalk > 0).IsTrue();

            CpuDecisionObservation hitTwoIn = Facing(FighterCharacterID.Lincoln, targetX: 1.8);
            hitTwoIn.SelfNextStringStep = 1;
            var inCpu = new FighterCpuController(band, 77, null, null, BasicsOnly(band));
            AssertThat(Run(inCpu, hitTwoIn, GameplayButtons.BasicAttack).Edges > 0)
                .OverrideFailureMessage($"{band}: Lincoln did not continue into hit 2 from inside its band.")
                .IsTrue();

            // The finisher reaches 2.185 for him, past the close range.
            CpuDecisionObservation finisher = Facing(FighterCharacterID.Lincoln, targetX: 1.9);
            finisher.SelfNextStringStep = 2;
            var finishing = new FighterCpuController(band, 77, null, null, BasicsOnly(band));
            AssertThat(Run(finishing, finisher, GameplayButtons.BasicAttack).Edges > 0)
                .OverrideFailureMessage($"{band}: Lincoln did not continue into the finisher from 1.9.")
                .IsTrue();
        }

        // A hand-built observation carries no reach (0): the legacy close-range
        // press, unchanged.
        CpuDecisionObservation unknown = Facing(FighterCharacterID.Lincoln, targetX: 1.9);
        unknown.SelfStringReachWidthPercent = 0;
        var legacy = new FighterCpuController(CpuDifficulty.Hard, 77, null, null, BasicsOnly(CpuDifficulty.Hard));
        AssertThat(Run(legacy, unknown, GameplayButtons.BasicAttack).Edges > 0).IsTrue();
    }

    [TestCase]
    public void TheFinisherAndADirectionalSwingCannotContinueSoAPressDuringThemReadsTheOpenerBand() {
        // The finisher exits straight out and a directional swing stands
        // outside the chain, so a press decided during either can only ever
        // become a fresh opener. It must read the opener's band, never the
        // close range: Lincoln in his finisher's recovery 1.9 units from his
        // target used to press there and whiff the opener that followed.
        (int ComboIndex, int Phase, int Flags, int Expected)[] cases = {
            (0, FighterBasicAttackRules.PhaseNone, 0, 0),
            (0, FighterBasicAttackRules.PhaseStartup, 0, 1),
            (0, FighterBasicAttackRules.PhaseChainHold, 0, 1),
            (1, FighterBasicAttackRules.PhaseRecovery, 0, 2),
            (1, FighterBasicAttackRules.PhaseChainHold, 0, 2),
            (2, FighterBasicAttackRules.PhaseStartup, 0, 0),
            (2, FighterBasicAttackRules.PhaseRecovery, 0, 0),
            (0, FighterBasicAttackRules.PhaseActive, FighterBasicAttackRules.FlagUpAttack, 0),
            (0, FighterBasicAttackRules.PhaseRecovery,
                FighterBasicAttackRules.FlagDownAir | FighterBasicAttackRules.FlagAerial, 0),
            // GU-A: the single air strike never continues, so a press during it
            // reads the opener's band (the box it and the opener share).
            (0, FighterBasicAttackRules.PhaseStartup,
                FighterBasicAttackRules.FlagAirAttack | FighterBasicAttackRules.FlagAerial, 0),
            (0, FighterBasicAttackRules.PhaseRecovery,
                FighterBasicAttackRules.FlagAirAttack | FighterBasicAttackRules.FlagAerial, 0)
        };
        foreach ((int comboIndex, int phase, int flags, int expected) in cases) {
            var runtime = new FighterRuntimeComponent { ComboIndex = comboIndex, AttackPhase = phase, AttackFlags = flags };
            AssertThat(FighterBasicAttackRules.NextChainStep(in runtime))
                .OverrideFailureMessage($"combo {comboIndex}, phase {phase}, flags {flags}")
                .IsEqual(expected);
            AssertThat(ObserveLincoln(in runtime, 1.9).SelfNextStringStep).IsEqual(expected);
        }

        foreach (CpuDifficulty band in new[] { CpuDifficulty.Easy, CpuDifficulty.Normal, CpuDifficulty.Hard }) {
            var inFinisher = new FighterRuntimeComponent {
                ComboIndex = 2, AttackPhase = FighterBasicAttackRules.PhaseRecovery
            };
            var cpu = new FighterCpuController(band, 4242, null, null, BasicsOnly(band));
            (int edges, int walkIn) = Run(cpu, ObserveLincoln(in inFinisher, 1.9), GameplayButtons.BasicAttack);
            AssertThat(edges)
                .OverrideFailureMessage($"{band}: Lincoln pressed from 1.9 during his finisher.")
                .IsEqual(0);
            AssertThat(walkIn > 0).IsTrue();

            // Hit 2's recovery can still continue, into the finisher's 2.185 band.
            var inHitTwo = new FighterRuntimeComponent {
                ComboIndex = 1, AttackPhase = FighterBasicAttackRules.PhaseRecovery
            };
            var chaining = new FighterCpuController(band, 4242, null, null, BasicsOnly(band));
            AssertThat(Run(chaining, ObserveLincoln(in inHitTwo, 1.9), GameplayButtons.BasicAttack).Edges > 0)
                .OverrideFailureMessage($"{band}: Lincoln did not continue into his finisher from 1.9.")
                .IsTrue();
        }
    }

    [TestCase]
    public void APressDecidedMidStringIsDroppedAtDeliveryOnceTheStringHasEnded() {
        // The decision is read at decision time but delivered 4-8 (Hard),
        // 15-20 (Normal) or 30-45 (Easy) frames later. A press admitted on
        // Lincoln's hit 2 band (1.845) at 1.8 units is a whiff if the string
        // has ended by then, because it then swings his 1.76-unit opener.
        // Delivery re-reads the step on self-state (the gap stays the
        // decision-time one) and drops the press, walking in instead. The
        // control keeps the string running at delivery and must press once,
        // which also proves the decision was not overwritten in the schedule.
        foreach (CpuDifficulty band in new[] { CpuDifficulty.Easy, CpuDifficulty.Normal, CpuDifficulty.Hard }) {
            (int keptEdges, int keptWalk) = DeliverMidStringPress(band, deliveredStep: 1);
            AssertThat(keptEdges)
                .OverrideFailureMessage($"{band}: the control press did not arrive.")
                .IsEqual(1);
            AssertThat(keptWalk).IsEqual(0);

            (int droppedEdges, int droppedWalk) = DeliverMidStringPress(band, deliveredStep: 0);
            AssertThat(droppedEdges)
                .OverrideFailureMessage($"{band}: a press delivered after the string ended swung the opener from 1.8.")
                .IsEqual(0);
            AssertThat(droppedWalk > 0)
                .OverrideFailureMessage($"{band}: the dropped press did not walk in.")
                .IsTrue();
        }
    }

    [TestCase]
    public void ATemplateReachBandIsExactlyTheCloseRangeSoItsNeutralIsUnchanged() {
        // The ledger (FIX-CPU-REACH-BLIND-OPENER) promises that a CPU Tesla or
        // Tubman neutral is bit-identical to the old close-range press. That
        // holds only while the template opener's band equals the close range
        // to the raw unit and every later hit's band is at least as wide, for
        // every profile of 100 % reach or more. A change to the margin, the
        // string table or FP64 rounding that breaks this moves their CPU
        // hashes, and must fail here first.
        AssertThat(ReachOf(FighterCharacterID.Tesla)).IsEqual(100);
        AssertThat(ReachOf(FighterCharacterID.Tubman)).IsEqual(100);
        AssertThat(FighterCpuController.MeleeBand(100, 0).RawValue).IsEqual(CloseRange.RawValue);
        var issues = new System.Collections.Generic.List<string>();
        foreach (FighterCharacterID id in Enum.GetValues<FighterCharacterID>()) {
            int reach = ReachOf(id);
            if (reach < 100) continue;
            for (int step = 0; step < 3; step++) {
                FP64 band = FighterCpuController.MeleeBand(reach, step, 0);
                if (band < CloseRange) issues.Add($"{id} step {step}: band {Describe(band)} < 2.0.");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheUltimateCommitAndTheCloseSpecialLadderStillReadTheCloseRange() {
        // Lincoln at 1.9: outside his opener's band but inside the 2.0 close
        // range, which is still the A02 Ultimate-commit rule and the close
        // Special ladder's range. The band must not shrink either.
        CpuDecisionObservation full = Facing(FighterCharacterID.Lincoln, targetX: 1.9);
        full.InfluenceRaw = FighterMeterRules.Cap.RawValue;
        CpuBandTuning ultimateOnly = BasicsOnly(CpuDifficulty.Normal) with { UltimatePercent = 100 };
        var committing = new FighterCpuController(CpuDifficulty.Normal, 31337, null, null, ultimateOnly);
        AssertThat(Run(committing, full, GameplayButtons.Ultimate).Edges > 0)
            .OverrideFailureMessage("The Ultimate commit shrank with the melee band.")
            .IsTrue();

        CpuDecisionObservation ready = Facing(FighterCharacterID.Lincoln, targetX: 1.9);
        CpuBandTuning specialOnly = BasicsOnly(CpuDifficulty.Normal) with { SpecialOneClosePercent = 100 };
        var casting = new FighterCpuController(CpuDifficulty.Normal, 31337, null, null, specialOnly);
        AssertThat(Run(casting, ready, GameplayButtons.Special1).Edges > 0)
            .OverrideFailureMessage("The close Special ladder shrank with the melee band.")
            .IsTrue();

        // Beyond the close range neither fires, as before.
        CpuDecisionObservation far = Facing(FighterCharacterID.Lincoln, targetX: 2.4);
        far.InfluenceRaw = FighterMeterRules.Cap.RawValue;
        var distant = new FighterCpuController(CpuDifficulty.Normal, 31337, null, null, ultimateOnly);
        AssertThat(Run(distant, far, GameplayButtons.Ultimate).Edges).IsEqual(0);
    }

    [TestCase]
    public void ACpuLincolnOrJoanOpenerLandsFromTheEdgeOfCloseRangeWhereTheOldPressWhiffed() {
        // Spawn distance 1 puts the fighters exactly 2.0 apart, the old close
        // range: the old CPU pressed its opener there and Lincoln's (1.86 reach)
        // and Joan's (1.94) whiffed against a standing dummy. The band walks
        // them in first. A template kit (2.1 reach) lands either way.
        foreach (FighterCharacterID id in new[] { FighterCharacterID.Lincoln, FighterCharacterID.Joan }) {
            AssertThat(FirstOpenerLands(id, legacyReach: false))
                .OverrideFailureMessage($"{id}: the CPU's first opener whiffed.")
                .IsTrue();
            AssertThat(FirstOpenerLands(id, legacyReach: true))
                .OverrideFailureMessage($"{id}: the scenario no longer reproduces the reach-blind whiff.")
                .IsFalse();
        }
        AssertThat(FirstOpenerLands(FighterCharacterID.Tesla, legacyReach: false)).IsTrue();
        AssertThat(FirstOpenerLands(FighterCharacterID.Tesla, legacyReach: true)).IsTrue();
    }

    // === Harness ===

    /// <summary>
    /// A CPU of <paramref name="id"/> on player two against a standing default
    /// dummy 2.0 units away; true when the CPU's first string opener connects.
    /// <paramref name="legacyReach"/> blanks the observation's reach, which is
    /// exactly the reach-blind close-range press.
    /// </summary>
    private static bool FirstOpenerLands(FighterCharacterID id, bool legacyReach) {
        var simulation = new FighterSimulation(
            FighterLoadout.Default(FighterCharacterID.Tesla),
            FighterLoadout.Default(id),
            seed: 2026, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var cpu = new FighterCpuController(CpuDifficulty.Hard, 1011, null, null, BasicsOnly(CpuDifficulty.Hard));
        PlayerInputFrame previous = default;
        bool openerActive = false;
        for (int tick = 0; tick < Ticks; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent dummy)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent dummyRuntime)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent self)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent selfRuntime)).IsTrue();
            CpuDecisionObservation observation = FighterCpuController.Observe(
                in self, in selfRuntime, in dummy, in dummyRuntime);
            if (legacyReach) observation.SelfStringReachWidthPercent = 0;
            PlayerInputFrame frame = cpu.Sample((uint)tick, in observation, in previous);
            previous = frame;
            simulation.Advance(default, frame);

            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent after)).IsTrue();
            bool opener = after.ComboIndex == 0 && !FighterBasicAttackRules.IsVariantSwing(in after);
            if (!openerActive && opener && after.AttackPhase == FighterBasicAttackRules.PhaseActive) {
                openerActive = true;
            }
            if (!openerActive) continue;
            if ((after.AttackFlags & FighterBasicAttackRules.FlagHitResolved) != 0) return true;
            if (after.AttackPhase != FighterBasicAttackRules.PhaseActive) return false;
        }
        AssertThat(openerActive)
            .OverrideFailureMessage($"{id}: the CPU never swung an opener.")
            .IsTrue();
        return false;
    }

    /// <summary>
    /// The band's rates with every verb but the basic string and the threat
    /// answers switched off, so the close-range branch reaches its BasicAttack
    /// fallback deterministically.
    /// </summary>
    private static CpuBandTuning BasicsOnly(CpuDifficulty band) => CpuBandTuning.For(band) with {
        UltimatePercent = 0,
        SpecialOneClosePercent = 0,
        SpecialOneRangedPercent = 0,
        SpecialTwoPercent = 0,
        MovementAbilityPercent = 0,
        ApproachJumpPercent = 0,
        GrabAdmissionPercent = 0
    };

    private static int ReachOf(FighterCharacterID id) =>
        FighterBasicAttackRules.StringProfileFor((int)id).ReachWidthPercent;

    /// <summary>
    /// The real sim projection of a grounded CPU Lincoln with
    /// <paramref name="selfRuntime"/>'s string state, its target
    /// <paramref name="targetX"/> units to the right.
    /// </summary>
    private static CpuDecisionObservation ObserveLincoln(in FighterRuntimeComponent selfRuntime, double targetX) {
        // A zeroed runtime reads as hanging from ledge anchor 0, which would
        // send the decision table to its climb jump.
        FighterRuntimeComponent runtime = selfRuntime;
        runtime.LedgeAnchor = FighterLedgeRules.NoAnchor;
        var self = new FighterStateComponent {
            CharacterID = (int)FighterCharacterID.Lincoln, FacingRight = 1,
            Stocks = 3, IsGrounded = 1, RemainingJumps = 1, CurrentHP = 100, MaxHP = 100
        };
        var target = new FighterStateComponent {
            CharacterID = (int)FighterCharacterID.Tesla, Stocks = 3, IsGrounded = 1,
            CurrentHP = 100, MaxHP = 100,
            Position = new FPVector2(FP64.FromDouble(targetX), FP64.Zero)
        };
        var targetRuntime = new FighterRuntimeComponent();
        return FighterCpuController.Observe(in self, in runtime, in target, in targetRuntime);
    }

    /// <summary>
    /// One decision at tick 0, a CPU Lincoln mid-string (hit 2 next) 1.8 units
    /// from its target: inside hit 2's band, outside the opener's. Every later
    /// tick keeps the decision table quiet (no stocks) and reports the string
    /// step the press would start at delivery. Seed 4242 delivers the tick-0
    /// decision at tick 31 / 16 / 5 (Easy / Normal / Hard), and no later
    /// decision shares its slot. Returns its BasicAttack edges and walk-in frames.
    /// </summary>
    private static (int Edges, int WalkIn) DeliverMidStringPress(CpuDifficulty band, int deliveredStep) {
        CpuDecisionObservation decided = Facing(FighterCharacterID.Lincoln, targetX: 1.8);
        decided.SelfNextStringStep = 1;
        CpuDecisionObservation later = decided;
        later.Stocks = 0;
        later.SelfNextStringStep = deliveredStep;
        var cpu = new FighterCpuController(band, 4242, null, null, BasicsOnly(band));
        PlayerInputFrame previous = default;
        int edges = 0;
        int walkIn = 0;
        for (uint tick = 0; tick < 90; tick++) {
            CpuDecisionObservation observation = tick == 0 ? decided : later;
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.BasicAttack)) edges++;
            if (frame.MoveX > 0) walkIn++;
        }
        return (edges, walkIn);
    }

    /// <summary>A grounded fighter at the origin and its opponent <paramref name="targetX"/> to the right.</summary>
    private static CpuDecisionObservation Facing(FighterCharacterID id, double targetX) => new() {
        SelfPositionXRaw = 0,
        TargetPositionXRaw = FP64.FromDouble(targetX).RawValue,
        Stocks = 3,
        IsGrounded = 1,
        RemainingJumps = 1,
        SelfCurrentHP = 100,
        SelfMaxHP = 100,
        TargetCurrentHP = 100,
        TargetMaxHP = 100,
        SelfStringReachWidthPercent = ReachOf(id),
        TargetHurtboxHalfWidthRaw = FighterCombatSystem.VictimHurtboxHalfWidth.RawValue
    };

    /// <summary>Pressed edges of <paramref name="button"/>, and frames walking toward the target.</summary>
    private static (int Edges, int WalkIn) Run(
        FighterCpuController cpu, CpuDecisionObservation observation, GameplayButtons button) {
        PlayerInputFrame previous = default;
        int edges = 0;
        int walkIn = 0;
        for (uint tick = 0; tick < Ticks; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsPressed(button)) edges++;
            if (frame.MoveX > 0) walkIn++;
        }
        return (edges, walkIn);
    }

    private static string Describe(FP64 value) =>
        ((double)value.RawValue / FP64.One.RawValue).ToString("0.####");
}
