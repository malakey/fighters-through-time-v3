using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Diagnostics;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// A default-priority node that runs one <see cref="PlaytestTelemetry"/> tick per
/// physics frame. Added to the root before the hero, so — as the playtest session
/// does in a real run — it ticks after the autoloads and before the hero's update.
/// Used by <see cref="PlaytestCombatStatsTests"/>; not a suite.
/// </summary>
public partial class TelemetryTickProbe : Node {
    public PlaytestTelemetry Telemetry;

    public override void _PhysicsProcess(double delta) => Telemetry?.Tick(GetTree());
}

/// <summary>
/// D6d (user direction 2026-10-11; <c>specs/C-meter-telemetry.md</c> §1.4, package
/// C1): the bots' combat telemetry. B1–B4 pin the Godot-free trackers in
/// <c>scripts/Diagnostics/PlaytestCombatStats.cs</c>; B5 drives a real swing string
/// through real physics frames, where a first-active-frame contact arrives in the
/// physics flush before any telemetry tick has seen the hitbox active.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PlaytestCombatStatsTests {
    /// <summary>GR-PILOT: the report's rank block, with a result and on a run that did not complete.</summary>
    [TestCase]
    public void TheRankBlockReadsTheLevelsResultAndIsEmptyWithoutOne() {
        var result = new FTT.Core.LevelRankResult(FTT.Core.LevelRankLetter.A, 80.26, 35, 15, 65);
        System.Collections.Generic.Dictionary<string, object> block = PlaytestTelemetry.RankBlock(result, 35);
        AssertThat(block["letter"]).IsEqual("A");
        AssertThat(block["points"]).IsEqual(80.3);
        // 15 % of a bar lost against a budget of 1.5 bars.
        AssertThat(block["damage_score"]).IsEqual(90.0);
        AssertThat(block["damage_percent"]).IsEqual(15);
        AssertThat(block["speed_score"]).IsEqual(65);
        AssertThat(block["dust_paid"]).IsEqual(35);

        block = PlaytestTelemetry.RankBlock(null, 0);
        AssertObject(block["letter"]).IsNull();
        AssertThat(block["points"]).IsEqual(-1.0);
    }

    /// <summary>Far from every other suite's geometry; the floor's top is at y = 0.</summary>
    private const float OriginX = 87000f;

    [TestCase]
    public void AabbGapIsZeroOnOverlapAndEuclideanApart() {
        AssertThat(PlaytestCombatMath.AabbGap(0f, 0f, 10f, 10f, 5f, 5f, 15f, 15f)).IsEqual(0f);
        AssertThat(PlaytestCombatMath.AabbGap(0f, 0f, 10f, 10f, 20f, 0f, 30f, 10f)).IsEqual(10f);
        AssertThat(PlaytestCombatMath.AabbGap(0f, 0f, 10f, 10f, 13f, 14f, 20f, 20f)).IsEqual(5f);
        AssertThat(PlaytestCombatMath.AabbGap(13f, 14f, 20f, 20f, 0f, 0f, 10f, 10f)).IsEqual(5f);
        AssertThat(PlaytestCombatMath.HorizontalGap(5f, 0f, 10f)).IsEqual(0f);
        AssertThat(PlaytestCombatMath.HorizontalGap(-7f, 0f, 10f)).IsEqual(7f);
        AssertThat(PlaytestCombatMath.HorizontalGap(14f, 0f, 10f)).IsEqual(4f);
    }

    [TestCase]
    public void AFinisherRecordClosesOnTheNextSwingAndExcludesKillsAndAbilities() {
        var tracker = new FinisherReapproachTracker();

        tracker.OnFinisherConnect(100, 11UL, "standard", air: false, targetX: 500f);
        for (int frame = 101; frame <= 120; frame++) {
            tracker.OnTargetSample(frame, alive: true, grounded: false, 500f + (frame - 100) * 4f, 40f);
        }
        tracker.OnTargetSample(121, alive: true, grounded: true, 580f, 30f);
        FinisherReapproachTracker.Record first = tracker.Records[0];
        AssertThat(first.CarryPx).IsEqual(80);
        AssertThat(first.LandGapPx).IsEqual(30);
        AssertThat(first.IsOpen).IsTrue();

        tracker.OnSwingStart(150, 7);
        AssertThat(first.Outcome).IsEqual("swing");
        AssertThat(first.Frames).IsEqual(50);
        AssertThat(first.ClosedBySwingIndex).IsEqual(7);

        tracker.OnFinisherConnect(200, 12UL, "standard", air: false, targetX: 0f);
        tracker.OnTargetSample(201, alive: false, grounded: true, 0f, 0f);
        AssertThat(tracker.Records[1].Outcome).IsEqual("killed");
        AssertThat(tracker.Records[1].Frames).IsEqual(-1);

        tracker.OnFinisherConnect(300, 13UL, "elite", air: true, targetX: 0f);
        tracker.OnAbilityAccepted(310, ultimate: false);
        AssertThat(tracker.Records[2].Outcome).IsEqual("special");
        AssertThat(tracker.Records[2].Frames).IsEqual(10);

        tracker.OnFinisherConnect(400, 14UL, "boss", air: false, targetX: 0f);
        tracker.Tick(999);
        AssertThat(tracker.Records[3].IsOpen).IsTrue();
        tracker.Tick(1001);
        AssertThat(tracker.Records[3].Outcome).IsEqual("lost");
        AssertObject(tracker.Open).IsNull();

        AssertThat(tracker.Records.Count).IsEqual(4);
        AssertThat(PlaytestCombatMath.Median(tracker.SwingFrames()))
            .OverrideFailureMessage("Only swing outcomes are re-approach samples.")
            .IsEqual(50);
        AssertThat(tracker.Records[2].CarryVoided)
            .OverrideFailureMessage("A cast closing the record before the target lands ends the carry measurement.")
            .IsTrue();
        AssertThat(tracker.Records[2].CarryPx).IsEqual(-1);

        // The carry after a swing close: kept while nothing hits the target again,
        // voided once its HP falls below the last reading before the close.
        var carry = new FinisherReapproachTracker();
        carry.OnFinisherConnect(500, 21UL, "standard", air: false, targetX: 1000f);
        carry.NoteTargetHealth(40);
        carry.OnTargetSample(501, alive: true, grounded: false, 1010f, 50f);
        carry.NoteTargetHealth(40);
        carry.OnSwingStart(520, 3);
        carry.NoteTargetHealth(40);
        carry.OnTargetSample(521, alive: true, grounded: false, 1060f, 60f);
        carry.NoteTargetHealth(40);
        carry.OnTargetSample(530, alive: true, grounded: true, 1090f, 70f);
        AssertThat(carry.Records[0].CarryPx)
            .OverrideFailureMessage("A swing at another foe leaves the finisher's own carry measurable.")
            .IsEqual(90);
        AssertThat(carry.Records[0].LandGapPx).IsEqual(70);
        AssertThat(carry.Records[0].CarryVoided).IsFalse();

        carry.OnFinisherConnect(600, 22UL, "standard", air: false, targetX: 2000f);
        carry.NoteTargetHealth(40);
        carry.OnTargetSample(601, alive: true, grounded: false, 2010f, 50f);
        carry.NoteTargetHealth(40);
        carry.OnSwingStart(610, 4);
        carry.NoteTargetHealth(32);
        carry.OnTargetSample(611, alive: true, grounded: true, 2150f, 90f);
        AssertThat(carry.Records[1].CarryPx)
            .OverrideFailureMessage("A hit after the close must not be booked as the finisher's carry.")
            .IsEqual(-1);
        AssertThat(carry.Records[1].LandGapPx).IsEqual(-1);
        AssertThat(carry.Records[1].CarryVoided).IsTrue();
        AssertObject(carry.Sampled).IsNull();
    }

    [TestCase]
    public void APressIsAcceptedOnlyWhenItsSlotsExecutionSerialAdvances() {
        var presses = new AbilityPressTracker();

        presses.Register(10, PressSlot.Special1, 3);
        presses.Resolve(10, 3, 0, 0);
        AssertThat(presses.PendingCount)
            .OverrideFailureMessage("A press is resolved only on a later tick.")
            .IsEqual(1);
        presses.Resolve(11, 3, 0, 0);
        AssertThat(presses.PendingCount).IsEqual(0);

        presses.Register(20, PressSlot.Special2, 5);
        presses.Resolve(21, 3, 6, 0);

        presses.Register(30, PressSlot.Ultimate, 0);
        presses.AbandonPending();

        AssertCounts(presses.For(PressSlot.Special1), pressed: 1, accepted: 0, refused: 1, unresolved: 0);
        AssertCounts(presses.For(PressSlot.Special2), pressed: 1, accepted: 1, refused: 0, unresolved: 0);
        AssertCounts(presses.For(PressSlot.Ultimate), pressed: 1, accepted: 0, refused: 0, unresolved: 1);
    }

    [TestCase]
    public void TheMeterTrackerBooksOnlyRealChangesAndTimesOnlyCleanFills() {
        var meter = new MeterTracker();

        meter.Rebaseline(1, 0f);
        AssertThat(meter.LevelStart).IsEqual(0f);

        meter.Sample(2, 0f);
        meter.Sample(3, 50f);
        meter.Sample(61, InfluenceMeterRules.MaxValue);
        AssertThat(meter.FillFrames.Count).IsEqual(1);
        AssertThat(meter.FillFrames[0])
            .OverrideFailureMessage("The window starts at the latest sample at or below 1.0 (frame 2).")
            .IsEqual(59);
        AssertThat(meter.FirstFullFrame).IsEqual(61);

        meter.Sample(62, 0f);
        AssertThat(meter.Spent).IsEqual(100f);
        meter.Sample(63, 40f);
        meter.Sample(64, 20f);
        meter.Sample(65, InfluenceMeterRules.MaxValue);
        AssertThat(meter.FillFrames.Count)
            .OverrideFailureMessage("A fill whose window saw a decrease is not timed.")
            .IsEqual(1);
        AssertThat(meter.FillsDirty).IsEqual(1);
        AssertThat(meter.Gained).IsEqual(220f);
        AssertThat(meter.Spent).IsEqual(120f);

        // A rebuilt hero carrying a durable 60: the jump is not booked.
        meter.Rebaseline(200, 60f);
        AssertThat(meter.Gained).IsEqual(220f);
        AssertThat(meter.Spent).IsEqual(120f);
        AssertThat(meter.LevelStart).IsEqual(0f);

        // A rebuilt hero resets the baseline again: still no booked jump.
        meter.Rebaseline(300, 0f);
        meter.Sample(301, 0f);
        AssertThat(meter.Gained).IsEqual(220f);
        AssertThat(meter.End).IsEqual(0f);
    }

    [TestCase]
    public void RefusalConditionsAreEveryReadableReasonThatHeldWithNoInferredOrder() {
        // C3 (spec C §1.4(d)): the labels are the conditions present at the press's
        // pre-update snapshot, in table order, with no precedence and no state list.
        PressSnapshot mixed = PressSnapshot.Clear(PressSlot.Special1);
        mixed.HitstopFrames = 3;
        mixed.GateFrames = 20;
        mixed.ReuseSeconds = 0.5f;
        AssertLabels(mixed, "hitstop", "lockout", "reuse_delay");

        PressSnapshot gateTwo = PressSnapshot.Clear(PressSlot.Special2);
        gateTwo.GateFrames = 2;
        AssertLabels(gateTwo, "lockout");

        // The gate ticks before the poll, so a reading of 1 clears this tick.
        PressSnapshot gateOne = PressSnapshot.Clear(PressSlot.Special1);
        gateOne.GateFrames = 1;
        AssertLabels(gateOne);

        // So does the reuse delay: exactly one tick left is ready by the poll.
        PressSnapshot lastTick = PressSnapshot.Clear(PressSlot.Special1);
        lastTick.ReuseSeconds = PlaytestCombatMath.ReuseTickSeconds;
        AssertLabels(lastTick);

        // The Momentum gauge: under one pip is no_momentum, whatever the reuse delay; a gauge a hair
        // under a pip still pays (the hero's own slack); both can hold at once; the Ultimate never reads it.
        PressSnapshot starved = PressSnapshot.Clear(PressSlot.Special2);
        starved.MomentumPips = 0.6f;
        AssertLabels(starved, "no_momentum");
        PressSnapshot starvedAndResting = starved;
        starvedAndResting.ReuseSeconds = 1.5f;
        AssertLabels(starvedAndResting, "reuse_delay", "no_momentum");
        PressSnapshot almostAPip = PressSnapshot.Clear(PressSlot.Special1);
        almostAPip.MomentumPips = 0.99999f;
        AssertLabels(almostAPip);
        PressSnapshot ultimateNoPips = PressSnapshot.Clear(PressSlot.Ultimate);
        ultimateNoPips.MomentumPips = 0f;
        AssertLabels(ultimateNoPips);

        PressSnapshot ultimate = PressSnapshot.Clear(PressSlot.Ultimate);
        ultimate.MeterFull = 99f >= InfluenceMeterRules.MaxValue;
        ultimate.ReuseSeconds = 0.5f; // never a reuse_delay label for the Ultimate
        AssertLabels(ultimate, "meter");

        // A full meter is never a label for a Special.
        PressSnapshot special = PressSnapshot.Clear(PressSlot.Special2);
        special.MeterFull = false;
        AssertLabels(special);

        PressSnapshot dormant = PressSnapshot.Clear(PressSlot.Special2);
        dormant.SlotUnlocked = false;
        dormant.GateFrames = 20;
        AssertLabels(dormant, "lockout", "dormant");

        PressSnapshot frozen = PressSnapshot.Clear(PressSlot.Special1);
        frozen.TimeFrozen = true;
        AssertLabels(frozen, "time_freeze");

        PressSnapshot suppressed = PressSnapshot.Clear(PressSlot.Ultimate);
        suppressed.Suppressed = true;
        AssertLabels(suppressed, "suppressed");

        PressSnapshot executing = PressSnapshot.Clear(PressSlot.Special1);
        executing.AbilityExecuting = true;
        AssertLabels(executing, "executing");

        // Through the tracker: three refused Special 1 presses with snapshots, one
        // accepted, and one registered without a snapshot (C1's overload).
        var presses = new AbilityPressTracker();
        PressSnapshot gated = PressSnapshot.Clear(PressSlot.Special1);
        gated.GateFrames = 20;
        PressSnapshot gatedCooling = gated;
        gatedCooling.ReuseSeconds = 0.5f;
        PressSnapshot casting = PressSnapshot.Clear(PressSlot.Special1, "UsingSpecial");
        presses.Register(10, PressSlot.Special1, 4, gated);
        presses.Register(11, PressSlot.Special1, 4, gatedCooling);
        presses.Register(12, PressSlot.Special1, 4, casting);
        presses.Register(13, PressSlot.Special1, 4, gated);
        presses.Resolve(13, 4, 0, 0);
        presses.Resolve(14, 5, 0, 0);
        presses.Register(20, PressSlot.Special2, 7);
        presses.Resolve(21, 5, 7, 0);

        AbilityPressTracker.Counts s1 = presses.For(PressSlot.Special1);
        AssertCounts(s1, pressed: 4, accepted: 1, refused: 3, unresolved: 0);
        AssertThat(s1.RefusedWith.GetValueOrDefault("lockout")).IsEqual(2);
        AssertThat(s1.RefusedWith.GetValueOrDefault("reuse_delay")).IsEqual(1);
        AssertThat(s1.RefusedWith.Count).IsEqual(2);
        AssertThat(s1.RefusedSole.GetValueOrDefault("lockout"))
            .OverrideFailureMessage("Only the press whose sole readable reason was the lockout is a sole lockout refusal.")
            .IsEqual(1);
        AssertThat(s1.RefusedSole.Count).IsEqual(1);
        AssertThat(s1.RefusedNone)
            .OverrideFailureMessage("A refusal with nothing readable (a busy state) carries no label.")
            .IsEqual(1);
        AssertThat(s1.RefusedState.GetValueOrDefault("UsingSpecial")).IsEqual(1);
        AssertThat(s1.RefusedState.GetValueOrDefault("Idle")).IsEqual(2);

        AbilityPressTracker.Counts s2 = presses.For(PressSlot.Special2);
        AssertCounts(s2, pressed: 1, accepted: 0, refused: 1, unresolved: 0);
        AssertThat(s2.RefusedWith.Count + s2.RefusedSole.Count + s2.RefusedState.Count + s2.RefusedNone)
            .OverrideFailureMessage("A press registered without a snapshot books counts only.")
            .IsEqual(0);
    }

    [TestCase]
    public void TheDefyTrackerCountsAnExecutionItsHitsBeforeAndAfterAndADeathWithinThreeSeconds() {
        var defy = new DefyTracker();
        // Hits at 100, 130, 160 (before a Defy at 200), 230 and 330 (after), 400 (181 frames after: outside).
        foreach (int hit in new[] { 20, 100, 130, 160, 200, 230, 330, 381 }) defy.RecordHit(hit);
        defy.RecordPress(canDefy: true, meter: 80f);
        defy.RecordExecution(200, meterBefore: 80f, targets: 3, healed: 22);
        // A second Defy near the end of the run; the hero dies 100 frames after it.
        defy.RecordPress(canDefy: false, meter: 10f);
        defy.RecordPress(canDefy: false, meter: 90f);
        defy.RecordExecution(1000, meterBefore: 55f, targets: 0, healed: 9);
        defy.RecordDeath(1100);
        defy.RecordDeath(1500);

        AssertThat(defy.Executed).IsEqual(2);
        AssertThat(defy.Pressed).IsEqual(3);
        AssertThat(defy.RefusedBy.GetValueOrDefault("meter")).IsEqual(1);
        AssertThat(defy.RefusedBy.GetValueOrDefault("state")).IsEqual(1);

        List<DefyTracker.Resolved> resolved = defy.Resolve(endFrame: 1050);
        AssertThat(resolved.Count).IsEqual(2);
        AssertThat(resolved[0].HitsBefore)
            .OverrideFailureMessage("Hits 20 (180 frames before is frame 20: inside) through 160 are before; the one at the Defy's own frame is neither.")
            .IsEqual(4);
        AssertThat(resolved[0].HitsAfter).OverrideFailureMessage("230 and 330 are inside; 381 is one frame past the window.").IsEqual(2);
        AssertThat(resolved[0].DiedWithin3s).IsFalse();
        AssertThat(resolved[0].AfterTruncated).IsFalse();
        AssertThat(resolved[1].DiedWithin3s).OverrideFailureMessage("Death at 1100 is within 180 of 1000; the one at 1500 is not.").IsTrue();
        AssertThat(resolved[1].AfterTruncated).OverrideFailureMessage("The run ended at 1050, inside the window.").IsTrue();
        AssertThat(resolved[0].Execution.Targets).IsEqual(3);
        AssertThat(resolved[0].Execution.Healed).IsEqual(22);
        AssertThat(resolved[0].Execution.MeterBefore).IsEqual(80f);
    }

    [TestCase]
    public void TheMomentumBlockStartsAtTwoPipsAndBooksGainsBySourceAndSpendsBySpecial() {
        const float regen = 1f / BasicComboRules.MomentumRegenFramesPerPip;
        var momentum = new MomentumTracker();
        momentum.Rebaseline(2f);
        AssertThat(momentum.Start).OverrideFailureMessage("A fresh level starts at two pips, the analogue of meter0.").IsEqual(2f);

        // Two quiet ticks of regen, a string hit landing (+0.05 on top of the tick's regen), a
        // Special 1 cast (-1 pip) and a Special 2 cast with a finisher gain on the same tick.
        float pips = 2f;
        pips += regen; momentum.Sample(pips, regen, false, false, null);
        pips += regen; momentum.Sample(pips, regen, false, false, null);
        pips += regen + 0.05f; momentum.Sample(pips, regen, false, false, "combo_1");
        pips += regen - 1f; momentum.Sample(pips, regen, true, false, null);
        pips += regen + 0.2f - 1f; momentum.Sample(pips, regen, false, true, "combo_3");

        AssertThat(momentum.SpentSpecial1).IsEqual(1);
        AssertThat(momentum.SpentSpecial2).IsEqual(1);
        AssertThat(momentum.SpecialsCast).IsEqual(2);
        AssertThat((float)momentum.GainedBy["combo_1"]).IsEqualApprox(0.05f, 0.0005f);
        AssertThat((float)momentum.GainedBy["combo_3"]).IsEqualApprox(0.2f, 0.0005f);
        AssertThat((float)momentum.GainedBy["regen"]).OverrideFailureMessage("Five ticks of regen, none of it hidden by a spend.").IsEqualApprox(5 * regen, 0.0002f);
        AssertThat(momentum.GainedBy.ContainsKey("other")).IsFalse();
        AssertThat(momentum.End).IsEqualApprox(pips, 0.0001f);
        AssertThat(momentum.FramesStarved).OverrideFailureMessage("Only the last tick (0.25 pips) is under one pip.").IsEqual(1);

        // A rise with no heard hit books to "other"; a gauge under a pip is starved; a full one is counted.
        momentum.Sample(pips + 0.1f, regen, false, false, null);
        AssertThat(momentum.GainedBy.ContainsKey("other")).IsTrue();
        var drained = new MomentumTracker();
        drained.Rebaseline(0.4f);
        drained.Sample(0.4f, regen, false, false, null);
        AssertThat(drained.FramesStarved).IsEqual(1);
        var full = new MomentumTracker();
        full.Rebaseline(4f);
        full.Sample(4f, regen, false, false, null);
        AssertThat(full.FramesFull).IsEqual(1);
        AssertThat(full.Gained).IsEqual(0.0);
    }

    [TestCase]
    public async Task ARealSwingIsOpenedByItsFirstContactAndItsFinisherOpensARecord() {
        var tree = (SceneTree)Engine.GetMainLoop();
        var probe = new TelemetryTickProbe { Name = "TelemetryTickProbe" };
        tree.Root.AddChild(probe);
        StaticBody2D floor = CreateFloor();
        EnemyController mob = null;
        PlayerController hero = null;
        PlaytestWorld world = null;
        PlaytestTelemetry telemetry = null;
        try {
            mob = CreatePassiveMob("chrono_slasher", new Vector2(OriginX + 50f, 0f));
            hero = CharacterFactory.CreateCharacter("joan", 0);
            hero.Position = new Vector2(OriginX, -2f);
            tree.Root.AddChild(hero);
            hero.GlobalPosition = new Vector2(OriginX, -2f);
            if (!hero.IsFacingRight) mob.GlobalPosition = new Vector2(OriginX - 50f, 0f);

            var source = new BufferedInputSource();
            InputManager.Instance.SetInputSource(0, source);
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
            for (int frame = 0; frame < 10; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            world = new PlaytestWorld(tree);
            telemetry = new PlaytestTelemetry();
            telemetry.Attach(world, bot: null);
            probe.Telemetry = telemetry;

            bool everStunned = false;
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.BasicAttack));
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            everStunned |= hero.CurrentState == CharacterState.Stunned;
            // Held without a new edge: the chain continues through the finisher (F3).
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.BasicAttack,
                previousHeld: GameplayButtons.BasicAttack));
            for (int frame = 0; frame < 180; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                everStunned |= hero.CurrentState == CharacterState.Stunned;
                if (telemetry.Swings.Any(s => s.Hit == "combo_3" && !s.IsOpen)) break;
            }

            AssertThat(everStunned)
                .OverrideFailureMessage("Harness fault: the passive mob stunned the hero.")
                .IsFalse();
            AssertThat(telemetry.Swings.Count > 0)
                .OverrideFailureMessage("No swing was recorded.")
                .IsTrue();
            SwingRecord first = telemetry.Swings[0];
            AssertThat(first.Hit).IsEqual("combo_1");
            AssertThat(first.Outcome)
                .OverrideFailureMessage("Hit 1's first-active-frame contact must open and connect its swing.")
                .IsEqual("c");
            AssertThat(first.Gap).IsEqual(0);
            AssertThat(first.Landed).IsGreaterEqual(1);
            AssertThat(telemetry.OrphanBasicHits)
                .OverrideFailureMessage("Every basic hit confirm must belong to a heard melee contact.")
                .IsEqual(0);

            SwingRecord finisher = telemetry.Swings.FirstOrDefault(s => s.Hit == "combo_3" && s.Outcome == "c");
            AssertThat(finisher != null)
                .OverrideFailureMessage("The finisher swing must connect.")
                .IsTrue();
            AssertThat(telemetry.Finishers.Records.Count).IsEqual(1);
            AssertThat(telemetry.Finishers.Records[0].ConnectFrame).IsEqual(finisher.F);
        } finally {
            probe.Telemetry = null;
            telemetry?.FinishPending();
            telemetry?.Detach();
            world?.Detach();
            InputManager.Instance?.ClearInputSource(0);
            if (hero != null && GodotObject.IsInstanceValid(hero)) hero.Free();
            if (mob != null && GodotObject.IsInstanceValid(mob)) mob.Free();
            floor.Free();
            probe.Free();
        }
    }

    /// <summary>
    /// CM-BOT (decision 7.2): two mobs' real attack payloads reach the hero
    /// through its damage chokepoint 10 physics frames apart. The telemetry reads
    /// one cross-source re-hit, and raising the observation changes nothing: the
    /// same sequence on a fresh hero with no subscriber leaves HP, meter and the
    /// Rally echo pool identical after each hit and after the drain.
    /// </summary>
    [TestCase]
    public async Task TwoMobsHittingTenFramesApartReadOneCrossSourceReHitAndTheObservationChangesNothing() {
        var tree = (SceneTree)Engine.GetMainLoop();
        var probe = new TelemetryTickProbe { Name = "TelemetryTickProbe" };
        tree.Root.AddChild(probe);
        StaticBody2D floor = CreateFloor();
        EnemyController first = null;
        EnemyController second = null;
        PlayerController hero = null;
        PlaytestWorld world = null;
        PlaytestTelemetry telemetry = null;
        try {
            first = CreatePassiveMob("chrono_slasher", new Vector2(OriginX + 400f, 0f));
            second = CreatePassiveMob("chrono_slasher", new Vector2(OriginX - 400f, 0f));
            var source = new BufferedInputSource();
            InputManager.Instance.SetInputSource(0, source);
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));

            // Unobserved: nothing subscribes to the hero's DamageTaken.
            hero = await SpawnHeroAsync(tree);
            List<(int HP, float Meter, float Echo)> unobserved = await TwoHitSequenceAsync(tree, hero, first, second);
            hero.Free();
            hero = null;

            // Observed: the telemetry subscribes through PlaytestWorld's seam.
            hero = await SpawnHeroAsync(tree);
            world = new PlaytestWorld(tree);
            telemetry = new PlaytestTelemetry();
            telemetry.Attach(world, bot: null);
            probe.Telemetry = telemetry;
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            List<(int HP, float Meter, float Echo)> observed = await TwoHitSequenceAsync(tree, hero, first, second);

            AssertThat(unobserved[0].HP < hero.MaximumHP && unobserved[1].HP < unobserved[0].HP)
                .OverrideFailureMessage("Harness fault: a mob hit dealt no damage.")
                .IsTrue();
            for (int i = 0; i < unobserved.Count; i++) {
                AssertThat(observed[i].HP).OverrideFailureMessage($"HP differs at sample {i}.").IsEqual(unobserved[i].HP);
                AssertThat(observed[i].Meter).OverrideFailureMessage($"Meter differs at sample {i}.").IsEqual(unobserved[i].Meter);
                AssertThat(observed[i].Echo).OverrideFailureMessage($"The Rally pool differs at sample {i}.").IsEqual(unobserved[i].Echo);
            }

            RehitTracker rehits = telemetry.Rehits;
            AssertThat(rehits.Hits).IsEqual(2);
            AssertThat(rehits.CrossSource30)
                .OverrideFailureMessage("Two mobs 10 frames apart are one cross-source re-hit.")
                .IsEqual(1);
            AssertThat(rehits.CrossSource20).IsEqual(1);
            AssertThat(rehits.SameSource30).IsEqual(0);
            AssertThat(rehits.Log[0].Kind).IsEqual(RehitTracker.KindStandard);
            AssertThat(rehits.Log[0].Label).IsEqual("chrono_slasher");
            AssertThat(rehits.Log[1].CrossGap)
                .OverrideFailureMessage("Both observations map onto the telemetry frames 10 apart.")
                .IsEqual(10);
            AssertThat(rehits.Log[0].Source != rehits.Log[1].Source)
                .OverrideFailureMessage("Two mob instances are two sources.")
                .IsTrue();
            AssertThat(telemetry.UnobservedHeroHits)
                .OverrideFailureMessage("Every HP loss the bus carried was observed.")
                .IsEqual(0);
            AssertThat((int)telemetry.CombatSummary()["xsrc_rehits"]).IsEqual(1);
        } finally {
            probe.Telemetry = null;
            telemetry?.FinishPending();
            telemetry?.Detach();
            world?.Detach();
            InputManager.Instance?.ClearInputSource(0);
            if (hero != null && GodotObject.IsInstanceValid(hero)) hero.Free();
            if (first != null && GodotObject.IsInstanceValid(first)) first.Free();
            if (second != null && GodotObject.IsInstanceValid(second)) second.Free();
            floor.Free();
            probe.Free();
        }
    }

    /// <summary>
    /// CM-BOT (decision 7.2): a boss's melee payload names its boss (the
    /// <c>BossController</c> stamp), so the re-hit counter attributes the hit to
    /// the boss, and a mob's hit 5 frames later is a cross-source re-hit whose
    /// predecessor is the boss.
    /// </summary>
    [TestCase]
    public async Task ABossHitIsAttributedToTheBoss() {
        var tree = (SceneTree)Engine.GetMainLoop();
        var probe = new TelemetryTickProbe { Name = "TelemetryTickProbe" };
        tree.Root.AddChild(probe);
        StaticBody2D floor = CreateFloor();
        BossController boss = null;
        EnemyController mob = null;
        PlayerController hero = null;
        PlaytestWorld world = null;
        PlaytestTelemetry telemetry = null;
        try {
            BossData data = AuthoredResources.Load<BossData>("res://resources/Bosses/borgia_inquisitor.tres");
            PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
            boss = scene.Instantiate<BossController>();
            boss.Data = data;
            boss.Position = new Vector2(OriginX + 600f, 0f);
            tree.Root.AddChild(boss);
            boss.ApplyData(data);
            boss.SetPhysicsProcess(false);
            boss.GlobalPosition = new Vector2(OriginX + 600f, 0f);
            mob = CreatePassiveMob("chrono_slasher", new Vector2(OriginX - 400f, 0f));
            var source = new BufferedInputSource();
            InputManager.Instance.SetInputSource(0, source);
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));

            hero = await SpawnHeroAsync(tree);
            world = new PlaytestWorld(tree);
            telemetry = new PlaytestTelemetry();
            telemetry.Attach(world, bot: null);
            probe.Telemetry = telemetry;
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            HitPayload bossHit = AttackHitboxOf(boss).CreatePayload(0);
            AssertThat(bossHit.SourceActorId)
                .OverrideFailureMessage("The boss's melee hitbox must name its boss.")
                .IsEqual(boss.GetInstanceId());
            AssertThat(hero.TakeDamage(in bossHit) > 0f)
                .OverrideFailureMessage("Harness fault: the boss hit dealt no damage.")
                .IsTrue();
            for (int frame = 0; frame < 5; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            HitPayload mobHit = AttackHitboxOf(mob).CreatePayload(0);
            hero.TakeDamage(in mobHit);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            RehitTracker rehits = telemetry.Rehits;
            AssertThat(rehits.Log.Count).IsEqual(2);
            AssertThat(rehits.Log[0].Kind)
                .OverrideFailureMessage("A boss's own hit is attributed to the boss.")
                .IsEqual(RehitTracker.KindBoss);
            AssertThat(rehits.Log[0].Label).IsEqual("borgia_inquisitor");
            AssertThat(rehits.Log[0].Source).IsEqual("actor:" + boss.GetInstanceId());
            AssertThat(rehits.Log[1].Kind).IsEqual(RehitTracker.KindStandard);
            AssertThat(rehits.Log[1].Cross30).IsTrue();
            AssertThat(rehits.Log[1].CrossKind).IsEqual(RehitTracker.KindBoss);
            AssertThat(rehits.Log[1].CrossGap).IsEqual(5);
            AssertThat(rehits.ByKind[RehitTracker.KindBoss].Hits).IsEqual(1);
        } finally {
            probe.Telemetry = null;
            telemetry?.FinishPending();
            telemetry?.Detach();
            world?.Detach();
            InputManager.Instance?.ClearInputSource(0);
            if (hero != null && GodotObject.IsInstanceValid(hero)) hero.Free();
            if (mob != null && GodotObject.IsInstanceValid(mob)) mob.Free();
            // The boss node only: its BossData is a cached authored resource (never Dispose a Resource).
            if (boss != null && GodotObject.IsInstanceValid(boss)) boss.Free();
            floor.Free();
            probe.Free();
        }
    }

    /// <summary>
    /// CM-FIXA-PRES (2026-10-16): a real <see cref="FTT.Environment.RisingWaterZone"/> drown tick
    /// reaches the telemetry as a periodic tick (<c>HitDelivery.Tick</c> through
    /// the environmental chokepoint), so a mob's hit 10 frames later is no
    /// cross-source re-hit; a discrete hazard contact through the same
    /// chokepoint is still a hit and a headline predecessor, but not a
    /// combat-only one. Every HP loss is still observed.
    /// </summary>
    [TestCase]
    public async Task ADrownTickIsAPeriodicTickAndAHazardContactIsNoCombatPredecessor() {
        var tree = (SceneTree)Engine.GetMainLoop();
        var probe = new TelemetryTickProbe { Name = "TelemetryTickProbe" };
        tree.Root.AddChild(probe);
        StaticBody2D floor = CreateFloor();
        EnemyController mob = null;
        PlayerController hero = null;
        FTT.Environment.RisingWaterZone water = null;
        PlaytestWorld world = null;
        PlaytestTelemetry telemetry = null;
        try {
            mob = CreatePassiveMob("chrono_slasher", new Vector2(OriginX + 400f, 0f));
            var source = new BufferedInputSource();
            InputManager.Instance.SetInputSource(0, source);
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));

            hero = await SpawnHeroAsync(tree);
            // The water line 200 px above the hero's feet, no grace, a tick due on
            // the first submersion step; disabled so only the explicit step ticks.
            water = new FTT.Environment.RisingWaterZone {
                Name = "CmFixaPresWater",
                Enabled = false,
                DrownGraceSeconds = 0f,
                DrownTickSeconds = 0.05f,
                DrownDamage = 4,
                LabelKey = ""
            };
            tree.Root.AddChild(water);
            water.GlobalPosition = new Vector2(OriginX, hero.GlobalPosition.Y - 200f);
            world = new PlaytestWorld(tree);
            telemetry = new PlaytestTelemetry();
            telemetry.Attach(world, bot: null);
            probe.Telemetry = telemetry;
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            int hpBefore = hero.CurrentHP;
            AssertThat(water.AddPlayer(hero)).IsTrue();
            AssertThat(water.IsPlayerSubmerged(hero)).IsTrue();
            water.TickSubmersion(0.1f);
            AssertThat(hero.CurrentHP)
                .OverrideFailureMessage("Harness fault: the drown tick dealt no damage.")
                .IsEqual(hpBefore - 4);
            for (int frame = 0; frame < 10; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            HitPayload afterDrown = AttackHitboxOf(mob).CreatePayload(0);
            AssertThat(hero.TakeDamage(in afterDrown) > 0f)
                .OverrideFailureMessage("Harness fault: the mob hit dealt no damage.")
                .IsTrue();

            RehitTracker rehits = telemetry.Rehits;
            AssertThat(rehits.Ticks)
                .OverrideFailureMessage("The drown tick is booked as a periodic tick.")
                .IsEqual(1);
            AssertThat(rehits.TicksByKind[RehitTracker.KindHazard].Ticks).IsEqual(1);
            AssertThat(rehits.Hits).IsEqual(1);
            AssertThat(rehits.CrossSource30)
                .OverrideFailureMessage("A drown tick is never the predecessor of a mob re-hit.")
                .IsEqual(0);

            // Out of the water; then a discrete hazard contact (a discharge-style
            // hit through the same chokepoint) and the mob again 10 frames later.
            water.RemovePlayer(hero);
            for (int frame = 0; frame < 40; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(hero.ApplyEnvironmentalDamage(5) > 0)
                .OverrideFailureMessage("Harness fault: the hazard contact dealt no damage.")
                .IsTrue();
            for (int frame = 0; frame < 10; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            HitPayload afterContact = AttackHitboxOf(mob).CreatePayload(0);
            hero.TakeDamage(in afterContact);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            AssertThat(rehits.Hits).IsEqual(3);
            AssertThat(rehits.Ticks).IsEqual(1);
            AssertThat(rehits.CrossSource30)
                .OverrideFailureMessage("A discrete hazard contact stays a headline predecessor.")
                .IsEqual(1);
            AssertThat(rehits.CrossSource30Combat)
                .OverrideFailureMessage("A hazard contact is never a combat-only predecessor.")
                .IsEqual(0);
            AssertThat(rehits.Log.Count).IsEqual(3);
            AssertThat(rehits.Log[2].CrossKind).IsEqual(RehitTracker.KindHazard);
            AssertThat(telemetry.UnobservedHeroHits)
                .OverrideFailureMessage("Every HP loss the bus carried was observed, the tick included.")
                .IsEqual(0);
            AssertThat((int)telemetry.CombatSummary()["xsrc_rehits"]).IsEqual(1);
            AssertThat((int)telemetry.CombatSummary()["xsrc_rehits_combat"]).IsEqual(0);
        } finally {
            probe.Telemetry = null;
            telemetry?.FinishPending();
            telemetry?.Detach();
            world?.Detach();
            InputManager.Instance?.ClearInputSource(0);
            if (water != null && GodotObject.IsInstanceValid(water)) water.Free();
            if (hero != null && GodotObject.IsInstanceValid(hero)) hero.Free();
            if (mob != null && GodotObject.IsInstanceValid(mob)) mob.Free();
            floor.Free();
            probe.Free();
        }
    }

    /// <summary>Joan on the floor at the origin, settled for 10 physics frames.</summary>
    private static async Task<PlayerController> SpawnHeroAsync(SceneTree tree) {
        PlayerController hero = CharacterFactory.CreateCharacter("joan", 0);
        hero.Position = new Vector2(OriginX, -2f);
        tree.Root.AddChild(hero);
        hero.GlobalPosition = new Vector2(OriginX, -2f);
        for (int frame = 0; frame < 10; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        return hero;
    }

    /// <summary>
    /// <paramref name="first"/>'s attack payload, 10 physics frames, then
    /// <paramref name="second"/>'s, then 30 frames of drain. Samples HP, meter and
    /// the Rally pool after each hit and at the end.
    /// </summary>
    private static async Task<List<(int HP, float Meter, float Echo)>> TwoHitSequenceAsync(
        SceneTree tree, PlayerController hero, EnemyController first, EnemyController second) {
        var samples = new List<(int HP, float Meter, float Echo)>();
        HitPayload a = AttackHitboxOf(first).CreatePayload(0);
        hero.TakeDamage(in a);
        samples.Add((hero.CurrentHP, hero.CurrentUltimateMeter, hero.EchoPool));
        for (int frame = 0; frame < 10; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        HitPayload b = AttackHitboxOf(second).CreatePayload(0);
        hero.TakeDamage(in b);
        samples.Add((hero.CurrentHP, hero.CurrentUltimateMeter, hero.EchoPool));
        for (int frame = 0; frame < 30; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        samples.Add((hero.CurrentHP, hero.CurrentUltimateMeter, hero.EchoPool));
        return samples;
    }

    /// <summary>The attack hitbox a mob or boss resolves (its "Hitbox" node, else "AttackHitbox").</summary>
    private static Hitbox AttackHitboxOf(Node body) =>
        body.GetNodeOrNull<Hitbox>("Hitbox") ?? body.GetNode<Hitbox>("AttackHitbox");

    private static void AssertLabels(in PressSnapshot snapshot, params string[] expected) {
        AssertThat(string.Join(",", PlaytestCombatMath.RefusalConditions(snapshot)))
            .IsEqual(string.Join(",", expected));
    }

    private static void AssertCounts(AbilityPressTracker.Counts counts, int pressed, int accepted, int refused, int unresolved) {
        AssertThat(counts.Pressed).IsEqual(pressed);
        AssertThat(counts.Accepted).IsEqual(accepted);
        AssertThat(counts.Refused).IsEqual(refused);
        AssertThat(counts.Unresolved).IsEqual(unresolved);
    }

    /// <summary>
    /// A chrono_slasher that takes hits and does nothing else: 400 HP (a 40-HP mob
    /// is too close to Joan's string) and its physics process off, so no AI,
    /// attack, movement or knockback integration runs. Its hurtbox still receives
    /// contacts and its HP still drops through the receiver.
    /// </summary>
    private static EnemyController CreatePassiveMob(string enemyID, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        data.MaxHP = 400;
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        enemy.SetPhysicsProcess(false);
        enemy.GlobalPosition = position;
        return enemy;
    }

    /// <summary>A 2000-wide Environment floor whose top is at y = 0 (StoryEnemyCombatTests' floor).</summary>
    private static StaticBody2D CreateFloor() {
        var floor = new StaticBody2D {
            Name = "PlaytestCombatStatsFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(2000f, 40f) },
            Position = new Vector2(OriginX, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    /// <summary>
    /// MM-FIXB2C (Codex minor): a death in the Defy window is booked on the same observation clock as the
    /// execution and the hits. Raised before a physics frame's telemetry tick it belongs to Frame + 1, so a
    /// death at Frame = E + 180 (observed E + 181) is outside the 180-frame window and one at E + 179 is inside.
    /// </summary>
    [TestCase]
    public async Task ADeathBeforeTheTickIsBookedOnTheObservationFrameAtTheDefyWindowBoundary() {
        var tree = (SceneTree)Engine.GetMainLoop();
        var probe = new TelemetryTickProbe { Name = "TelemetryTickProbe" };
        tree.Root.AddChild(probe);
        StaticBody2D floor = CreateFloor();
        PlayerController hero = null;
        PlaytestWorld world = null;
        PlaytestTelemetry telemetry = null;
        try {
            hero = CharacterFactory.CreateCharacter("joan", 0);
            hero.Position = new Vector2(OriginX, -2f);
            tree.Root.AddChild(hero);
            hero.GlobalPosition = new Vector2(OriginX, -2f);
            var source = new BufferedInputSource();
            InputManager.Instance.SetInputSource(0, source);
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
            for (int frame = 0; frame < 5; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            world = new PlaytestWorld(tree);
            telemetry = new PlaytestTelemetry();
            telemetry.Attach(world, bot: null);
            probe.Telemetry = telemetry;
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            // Before this physics frame's tick: the observation belongs to Frame + 1.
            int inside = telemetry.Frame + 1;
            telemetry.Defy.RecordExecution(inside, 80f, 0, 0);
            while (telemetry.Frame < inside + 179) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            EventBus.Instance.RaisePlayerDied(0);   // Frame = E + 179, observed E + 180: inside
            int outsideExec = telemetry.Frame + 1;
            telemetry.Defy.RecordExecution(outsideExec, 80f, 0, 0);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            while (telemetry.Frame < outsideExec + 180) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            EventBus.Instance.RaisePlayerDied(0);   // Frame = E2 + 180, observed E2 + 181: outside

            List<DefyTracker.Resolved> resolved = telemetry.Defy.Resolve(telemetry.Frame);
            AssertThat(resolved.Count).IsEqual(2);
            AssertThat(resolved[0].DiedWithin3s).OverrideFailureMessage("Observed E + 180 is inside the window.").IsTrue();
            AssertThat(resolved[1].DiedWithin3s)
                .OverrideFailureMessage("Observed E + 181 is outside; booking it on Frame (E + 180) counted it inside.").IsFalse();
        } finally {
            probe.Telemetry = null;
            telemetry?.FinishPending();
            telemetry?.Detach();
            world?.Detach();
            InputManager.Instance?.ClearInputSource(0);
            if (hero != null && GodotObject.IsInstanceValid(hero)) hero.Free();
            floor.Free();
            probe.Free();
        }
    }
}
