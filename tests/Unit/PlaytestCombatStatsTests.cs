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

        meter.Rebaseline(1, 0f, false);
        AssertThat(meter.LevelStart).IsEqual(0f);

        meter.Sample(2, 0f, false);
        meter.Sample(3, 50f, false);
        meter.Sample(61, InfluenceMeterRules.MaxValue, false);
        AssertThat(meter.FillFrames.Count).IsEqual(1);
        AssertThat(meter.FillFrames[0])
            .OverrideFailureMessage("The window starts at the latest sample at or below 1.0 (frame 2).")
            .IsEqual(59);
        AssertThat(meter.FirstFullFrame).IsEqual(61);

        meter.Sample(62, 0f, false);
        AssertThat(meter.Spent).IsEqual(100f);
        meter.Sample(63, 40f, false);
        meter.Sample(64, 20f, false);
        meter.Sample(65, InfluenceMeterRules.MaxValue, false);
        AssertThat(meter.FillFrames.Count)
            .OverrideFailureMessage("A fill whose window saw a decrease is not timed.")
            .IsEqual(1);
        AssertThat(meter.FillsDirty).IsEqual(1);
        AssertThat(meter.Gained).IsEqual(220f);
        AssertThat(meter.Spent).IsEqual(120f);

        // A rebuilt hero carrying a durable 60: the jump is not booked.
        meter.Rebaseline(200, 60f, false);
        AssertThat(meter.Gained).IsEqual(220f);
        AssertThat(meter.Spent).IsEqual(120f);
        AssertThat(meter.LevelStart).IsEqual(0f);

        meter.Sample(201, 60f, true);
        AssertThat(meter.DefyProcs).IsEqual(1);

        // A rebuilt hero that inherits a spent Defy is not a proc.
        meter.Rebaseline(300, 0f, true);
        meter.Sample(301, 0f, true);
        AssertThat(meter.DefyProcs).IsEqual(1);
        AssertThat(meter.End).IsEqual(0f);
    }

    [TestCase]
    public void RefusalConditionsAreEveryReadableReasonThatHeldWithNoInferredOrder() {
        // C3 (spec C §1.4(d)): the labels are the conditions present at the press's
        // pre-update snapshot, in table order, with no precedence and no state list.
        PressSnapshot mixed = PressSnapshot.Clear(PressSlot.Special1);
        mixed.HitstopFrames = 3;
        mixed.GateFrames = 20;
        mixed.CooldownSeconds = 0.5f;
        AssertLabels(mixed, "hitstop", "lockout", "cooldown");

        PressSnapshot gateTwo = PressSnapshot.Clear(PressSlot.Special2);
        gateTwo.GateFrames = 2;
        AssertLabels(gateTwo, "lockout");

        // The gate ticks before the poll, so a reading of 1 clears this tick.
        PressSnapshot gateOne = PressSnapshot.Clear(PressSlot.Special1);
        gateOne.GateFrames = 1;
        AssertLabels(gateOne);

        // So does the cooldown timer: exactly one tick left is ready by the poll.
        PressSnapshot lastTick = PressSnapshot.Clear(PressSlot.Special1);
        lastTick.CooldownSeconds = PlaytestCombatMath.CooldownTickSeconds;
        AssertLabels(lastTick);

        PressSnapshot ultimate = PressSnapshot.Clear(PressSlot.Ultimate);
        ultimate.MeterFull = 99f >= InfluenceMeterRules.MaxValue;
        ultimate.CooldownSeconds = 0.5f; // never a cooldown label for the Ultimate
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
        gatedCooling.CooldownSeconds = 0.5f;
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
        AssertThat(s1.RefusedWith.GetValueOrDefault("cooldown")).IsEqual(1);
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
}
