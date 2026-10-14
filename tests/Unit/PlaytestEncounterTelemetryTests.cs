using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Diagnostics;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// GU-BOT (gameplay update 2026-10-12, plan §5): the Story playtest bots and
/// their telemetry under the update's encounter locks (GU-E) and engagement
/// director (GU-F). Three cases pin the Godot-free trackers in
/// <c>scripts/Diagnostics/PlaytestEncounterStats.cs</c> and the air-strike swing
/// key; three drive a real lock in real physics frames: the bot's lock-aware
/// targeting, entry, checkpoint skip and stuck-lock watchdog (with the nav
/// graph's barrier rebuilds), the telemetry's <c>locks</c> and
/// <c>mob_engagement</c> blocks, and an air strike recorded as its own swing.
/// Every scene restores the session, the input source, the director and the
/// pooled bodies in <c>finally</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PlaytestEncounterTelemetryTests {

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    // === The Godot-free trackers ===========================================

    [TestCase]
    public void TheCrowdTrackerCountsConcurrentAttackersHandOffsAndWaiterGaps() {
        var tracker = new MobEngagementTracker { SlotTotal = 2, PermitTotal = 2 };

        // A quiet frame (nobody engaged, nobody attacking) is not counted.
        tracker.SampleFrame(new EngagementFrameSample());
        AssertThat(tracker.EngagedFrames).IsEqual(0);

        tracker.SampleFrame(new EngagementFrameSample { Engaged = 3, AttackingNear = 1, MeleeAttackingNear = 1, Holders = 1 });
        tracker.SampleFrame(new EngagementFrameSample { Engaged = 3, AttackingNear = 3, MeleeAttackingNear = 3, Holders = 2, RangedInFlight = 3 });
        tracker.SampleFrame(new EngagementFrameSample { Engaged = 3, AttackingNear = 0, Holders = 2 });
        AssertThat(tracker.EngagedFrames).IsEqual(3);
        AssertThat(tracker.MaxAttackingNear).IsEqual(3);
        AssertThat(tracker.MaxMeleeAttackingNear).IsEqual(3);
        AssertThat(tracker.MeanOverEngaged(tracker.SumAttackingNear)).IsEqual(1.33);
        AssertThat(tracker.FramesMeleeOverSlots)
            .OverrideFailureMessage("Three melee attackers near the hero exceed Normal's two slots once.")
            .IsEqual(1);
        AssertThat(tracker.FramesHoldersOverSlots).IsEqual(0);
        AssertThat(tracker.FramesRangedOverPermits).IsEqual(1);
        AssertThat(tracker.AttackingNearHistogram[0]).IsEqual(1);
        AssertThat(tracker.AttackingNearHistogram[3]).IsEqual(1);

        // Roles: a slot taken is a grant, a slot given up for a wait role a
        // hand-off; leaving the rotation (death) is neither, and a new pool
        // generation of the same instance is a new body.
        tracker.NoteRole(1UL, 1, StoryEngagementRules.EngagementRole.Waiting);
        tracker.NoteRole(1UL, 1, StoryEngagementRules.EngagementRole.Slot);
        tracker.NoteRole(1UL, 1, StoryEngagementRules.EngagementRole.Slot);
        tracker.NoteRole(1UL, 1, StoryEngagementRules.EngagementRole.Waiting);
        tracker.NoteRole(2UL, 1, StoryEngagementRules.EngagementRole.Slot);
        tracker.NoteRole(2UL, 1, StoryEngagementRules.EngagementRole.None);
        tracker.NoteRole(2UL, 2, StoryEngagementRules.EngagementRole.Waiting);
        AssertThat(tracker.SlotGrants).IsEqual(2);
        AssertThat(tracker.SlotHandoffs).IsEqual(1);

        // Waiter gaps: every waiter, and the front one per side.
        tracker.NoteWaiterGap(170.4f, front: true);
        tracker.NoteWaiterGap(234f, front: false);
        tracker.NoteWaiterGap(298f, front: false);
        tracker.NoteWaiterGap(171f, front: true);
        AssertThat(tracker.WaiterGapSamples).IsEqual(4);
        AssertThat(tracker.WaiterGapMedian).IsEqual(234);
        AssertThat(tracker.FrontWaiterGapMedian).IsEqual(171);
        AssertThat(tracker.WaiterGapBuckets[168]).IsEqual(2);
        AssertThat(MobEngagementTracker.HistogramMedian(new SortedDictionary<int, int>())).IsEqual(-1);

        // A waiter far off its ring: every sample counted, one logged per window.
        tracker.NoteFarWaiter(MobEngagementTracker.FarWaiterSampleFrames, "chrono_slasher", 5000f, 900f, 1237f);
        tracker.NoteFarWaiter(MobEngagementTracker.FarWaiterSampleFrames + 1, "chrono_slasher", 5000f, 900f, 1237f);
        AssertThat(tracker.FarWaiterSamples).IsEqual(2);
        AssertThat(tracker.FarWaiters.Count).IsEqual(1);
        AssertThat(tracker.FarWaiters[0].Gap).IsEqual(1237);
    }

    [TestCase]
    public void TheLockTrackerBooksTheEngageTheClearAndEveryLeaveWithMembersAlive() {
        var tracker = new LockTracker();
        LockObservation Seen(EncounterLockState state, bool inside, int living, bool alive = true) => new() {
            LockID = "level_02_orleans_lock_test", State = state, Planned = 3, Registered = 3, Living = living,
            Remaining = living, Engages = 1, HeroInside = inside, HeroAlive = alive
        };

        tracker.Observe(10, Seen(EncounterLockState.Armed, inside: false, living: 3));
        LockRecord record = tracker.Records[0];
        AssertThat(record.EngageFrame).IsEqual(-1);
        AssertThat(record.FirstSeenFrame).IsEqual(10);
        tracker.Observe(20, Seen(EncounterLockState.Engaged, inside: true, living: 3));
        AssertThat(record.EngageFrame).IsEqual(20);
        AssertThat(record.State).IsEqual("engaged");

        // An Echo Step out (the lock reverts): a leave with members alive.
        tracker.Observe(30, Seen(EncounterLockState.Armed, inside: false, living: 2));
        AssertThat(record.LeftWithMembersAlive).IsEqual(1);
        AssertThat(record.LeftFrames[0]).IsEqual(30);
        // Staying outside is not a second leave.
        tracker.Observe(31, Seen(EncounterLockState.Armed, inside: false, living: 2));
        AssertThat(record.LeftWithMembersAlive).IsEqual(1);

        // A death inside is not judged; a rewind landing outside after it is a leave.
        tracker.Observe(40, Seen(EncounterLockState.Engaged, inside: true, living: 2));
        tracker.Observe(41, Seen(EncounterLockState.Engaged, inside: false, living: 2, alive: false));
        AssertThat(record.LeftWithMembersAlive).IsEqual(1);
        tracker.Observe(42, Seen(EncounterLockState.Armed, inside: false, living: 2));
        AssertThat(record.LeftWithMembersAlive).IsEqual(2);

        // Walking out of a cleared area is no leave at all.
        tracker.Observe(50, Seen(EncounterLockState.Engaged, inside: true, living: 1));
        tracker.Observe(60, Seen(EncounterLockState.Cleared, inside: true, living: 0));
        tracker.Observe(61, Seen(EncounterLockState.Cleared, inside: false, living: 0));
        AssertThat(record.LeftWithMembersAlive).IsEqual(2);
        AssertThat(tracker.LeftWithMembersAlive).IsEqual(2);
        AssertThat(record.EngageFrame).IsEqual(20);
        AssertThat(record.ClearFrame).IsEqual(60);
        AssertThat(record.FramesToClear).IsEqual(40);
        AssertThat(record.MaxLiving).IsEqual(3);

        // Members: killed means the telemetry's record reached 0 HP; relocations
        // keep each body's high-water mark (its counters restart at every spawn).
        var killed = new EnemyRecord { KilledFrame = 55, LastHP = 0 };
        var parked = new EnemyRecord { KilledFrame = 58, LastHP = 12 };
        var alive = new EnemyRecord { LastHP = 30 };
        tracker.NoteMember(record.LockID, 7UL, 1, killed, relocations: 1, fallRecoveries: 1);
        tracker.NoteMember(record.LockID, 7UL, 1, killed, relocations: 0, fallRecoveries: 0);
        tracker.NoteMember(record.LockID, 8UL, 1, alive, relocations: 2, fallRecoveries: 0);
        tracker.NoteMember(record.LockID, 10UL, 1, parked, relocations: 0, fallRecoveries: 0);
        tracker.NoteMember("another_lock", 9UL, 1, killed, relocations: 5, fallRecoveries: 5);
        tracker.Summarize();
        AssertThat(record.MemberRelocations).IsEqual(3);
        AssertThat(record.FallRecoveries).IsEqual(1);
        AssertThat(record.Killed)
            .OverrideFailureMessage("Only the record that reached 0 HP is a kill; one closed with HP left (a pool park) is not.")
            .IsEqual(1);
    }

    [TestCase]
    public void ALockRebuiltByASceneReloadIsItsOwnInstanceRecord() {
        // FIX-BOT (review items 12 and 17): an Act III Anchor Snap reloads the
        // scene and the run goes on, so every lock comes back as a new node under
        // the same ID — Armed with fresh members (a slotless bot run) or Cleared
        // from a save's baseline. One record per instance keeps them apart.
        var tracker = new LockTracker();
        LockObservation Seen(ulong instance, EncounterLockState state, bool inside, int living, int planned,
            int reverts = 0, bool restored = false, bool alive = true) => new() {
            LockID = "level_14_neo_earth_lock_test", InstanceId = instance, State = state, Planned = planned,
            Registered = planned, Living = living, Remaining = living, Reverts = reverts, Engages = state == EncounterLockState.Armed ? 0 : 1,
            RestoredCleared = restored, HeroInside = inside, HeroAlive = alive
        };
        var first = new EnemyRecord { KilledFrame = 30, LastHP = 0 };
        var second = new EnemyRecord { KilledFrame = 35, LastHP = 0 };
        var reborn = new EnemyRecord { KilledFrame = 200, LastHP = 0 };

        // Instance 1: fought live, reverted twice, cleared.
        tracker.Observe(10, Seen(101UL, EncounterLockState.Armed, inside: false, living: 2, planned: 2));
        LockRecord live = tracker.Observe(20, Seen(101UL, EncounterLockState.Engaged, inside: true, living: 2, planned: 2, reverts: 2));
        tracker.NoteMember(live, 7UL, 1, first, 0, 0);
        tracker.NoteMember(live, 8UL, 1, second, 0, 0);
        tracker.Observe(40, Seen(101UL, EncounterLockState.Cleared, inside: true, living: 0, planned: 2, reverts: 2));

        // A reload rebuilds it restored Cleared (planned 0, counters 0): its own record;
        // the live one keeps its clear, plan, reverts and kills.
        LockRecord restored = tracker.Observe(100, Seen(202UL, EncounterLockState.Cleared, inside: false, living: 0, planned: 0, restored: true));
        AssertThat(restored != live).OverrideFailureMessage("A rebuilt lock must start a new instance record.").IsTrue();
        AssertThat(restored.Instance).IsEqual(2);
        AssertThat(live.RestoredCleared)
            .OverrideFailureMessage("A lock cleared live must not be relabelled 'restored' by a later rebuild.")
            .IsFalse();
        AssertThat(live.Planned).IsEqual(2);
        AssertThat(live.Reverts).IsEqual(2);
        AssertThat(live.FramesToClear).IsEqual(20);
        AssertThat(restored.RestoredCleared).IsTrue();

        // Another reload rebuilds it Armed with fresh members while the hero died
        // inside the first: the respawn outside is no leave, and the new members'
        // kills stay with the new plan.
        LockRecord rearmed = tracker.Observe(150, Seen(303UL, EncounterLockState.Armed, inside: false, living: 1, planned: 1));
        tracker.NoteMember(rearmed, 9UL, 1, reborn, 0, 0);
        tracker.Observe(160, Seen(303UL, EncounterLockState.Engaged, inside: true, living: 1, planned: 1));
        AssertThat(rearmed.Instance).IsEqual(3);
        AssertThat(rearmed.LeftWithMembersAlive).IsEqual(0);
        tracker.Summarize();
        AssertThat(live.Killed).IsEqual(2);
        AssertThat(rearmed.Killed).IsEqual(1);
        AssertThat(tracker.Records.All(r => r.Killed <= Math.Max(r.Planned, 0)))
            .OverrideFailureMessage("Kills must never exceed the plan of the instance they were made in.")
            .IsTrue();

        // Totals: one lock, two rebuilds, cleared (the live clear counts), and the
        // current instance is the latest.
        AssertThat(tracker.LockCount).IsEqual(1);
        AssertThat(tracker.Reloads).IsEqual(2);
        AssertThat(tracker.ClearedLocks).IsEqual(1);
        AssertThat(tracker.EngagedLocks).IsEqual(1);
        AssertThat(tracker.Current("level_14_neo_earth_lock_test") == rearmed).IsTrue();

        // An unknown instance (0) is the same instance, as before the fix.
        var legacy = new LockTracker();
        LockRecord one = legacy.Observe(1, Seen(0UL, EncounterLockState.Armed, inside: false, living: 1, planned: 1));
        LockRecord same = legacy.Observe(2, Seen(0UL, EncounterLockState.Engaged, inside: true, living: 1, planned: 1));
        AssertThat(one == same).IsTrue();
        AssertThat(legacy.Reloads).IsEqual(0);
    }

    [TestCase]
    public void TheGearPlanIsTheFewestPressesThatAlignTheWholeTrain() {
        // FIX-BOT: Florence's print shop — gear A (1) drives gear B (2), both aim
        // at 0. Gear A three times aligns A and leaves B at 1, which B's own three
        // presses close.
        int[] florence = PlaytestPuzzleMath.GearPresses(new[] { 1, 2 }, new[] { 0, 0 },
            new IReadOnlyList<int>[] { new[] { 1 }, Array.Empty<int>() });
        AssertThat(string.Join(",", florence)).IsEqual("3,3");
        // A train already aligned needs nothing; a lone gear its own distance.
        AssertThat(string.Join(",", PlaytestPuzzleMath.GearPresses(new[] { 0, 0 }, new[] { 0, 0 },
            new IReadOnlyList<int>[] { new[] { 1 }, Array.Empty<int>() }))).IsEqual("0,0");
        AssertThat(string.Join(",", PlaytestPuzzleMath.GearPresses(new[] { 3 }, new[] { 1 },
            new IReadOnlyList<int>[] { Array.Empty<int>() }))).IsEqual("2");
        // Two gears that only ever turn together cannot reach different targets.
        AssertThat(PlaytestPuzzleMath.GearPresses(new[] { 0, 0 }, new[] { 1, 2 },
            new IReadOnlyList<int>[] { new[] { 1 }, new[] { 0 } }) == null).IsTrue();
        // Malformed or oversized trains are not searched.
        AssertThat(PlaytestPuzzleMath.GearPresses(Array.Empty<int>(), Array.Empty<int>(), Array.Empty<IReadOnlyList<int>>()) == null).IsTrue();
        int big = PlaytestPuzzleMath.MaxGearTrain + 1;
        AssertThat(PlaytestPuzzleMath.GearPresses(new int[big], new int[big], new IReadOnlyList<int>[big]) == null).IsTrue();
    }

    [TestCase]
    public void TheAirStrikeIsItsOwnSwingFamilyAndTheAutoRunLengthCarriesTheParScale() {
        AssertThat(PlaytestCombatMath.HitVariant(BasicComboRules.AirAttackHitboxID)).IsEqual("air_attack");
        AssertThat(PlaytestCombatMath.HitVariant(BasicComboRules.UpAttackHitboxID)).IsEqual("up_attack");
        AssertThat(PlaytestCombatMath.HitVariant(BasicComboRules.DownAirHitboxID)).IsEqual("down_air");
        AssertThat(PlaytestCombatMath.HitVariant("combo_3")).IsEqual("string");
        AssertThat(PlaytestCombatMath.HitVariant("")).IsEqual("other");
        // The air strike is airborne by definition: its by-hit key has no "_air".
        AssertThat(PlaytestCombatMath.SwingHitKey(BasicComboRules.AirAttackHitboxID, air: true)).IsEqual("air_attack");
        AssertThat(PlaytestCombatMath.SwingHitKey("up_attack", air: true)).IsEqual("up_attack_air");
        AssertThat(PlaytestCombatMath.SwingHitKey("combo_1", air: false)).IsEqual("combo_1");

        // --max-seconds=0: the clock's par (authored x the provisional scale), at
        // least 300 s, x the difficulty multiplier, + 60.
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            float multiplier = TimelineIntegrityRules.DifficultyTimeMultiplier(difficulty);
            int expected = (int)Math.Ceiling(360f * TimelineIntegrityRules.ProvisionalParScale * multiplier) + 60;
            AssertThat(PlaytestOptions.AutoMaxSeconds(360f, difficulty)).IsEqual(expected);
            AssertThat(PlaytestOptions.AutoMaxSeconds(120f, difficulty))
                .IsEqual((int)Math.Ceiling(PlaytestOptions.AutoMaxSecondsParFloor * multiplier) + 60);
        }
        AssertThat(PlaytestOptions.AutoMaxSeconds(360f, Difficulty.Normal) > (int)Math.Ceiling(360f * 1.5f) + 60)
            .OverrideFailureMessage("The par scale must lengthen the auto run.")
            .IsTrue();
    }

    // === Real frames ========================================================

    [TestCase(Timeout = 120000)]
    public async Task TheBotWalksIntoALockFightsItsMembersAtAnyDistanceAndReportsAStuckOne() {
        using var scene = new BotLockScene();
        EncounterLock encounterLock = scene.BuildLock();
        CheckpointTrigger checkpoint = scene.BuildCheckpoint(encounterLock.LockID);
        EnemyController near = encounterLock.LivingMembers.First(m => m.GlobalPosition.X < BotLockScene.FarX - 100f);
        EnemyController far = encounterLock.LivingMembers.First(m => m != near);
        var bot = new StoryBot(scene.World, FightStyle.Play, CampaignLevel.Orleans);
        InputManager.Instance.SetInputSource(0, bot);

        // Armed and ahead: the dormant member 550 px away is never a target (it
        // refuses every hit), the held checkpoint behind is not a task, and the
        // bot walks into the area.
        await Frames(4);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);
        AssertThat(PlaytestWorld.IsHeldByLock(checkpoint)).IsTrue();
        AssertThat(bot.CurrentAction).IsEqual("lock_enter");
        AssertThat(bot.CurrentTarget == near || bot.CurrentTarget == far)
            .OverrideFailureMessage("A dormant lock member must never be the bot's target.")
            .IsFalse();
        AssertThat(scene.World.Nav.ClosedBarriersAtBuild)
            .OverrideFailureMessage($"The nav graph must see the exit barrier the lock closed at arming "
                + $"(closed at build {scene.World.Nav.ClosedBarriersAtBuild}, rebuilds {scene.World.Nav.Rebuilds}, "
                + $"segments {scene.World.Nav.StaticSegments.Count}).")
            .IsEqual(1);
        int rebuildsBefore = scene.World.Nav.BarrierRebuilds;

        bool engaged = false;
        for (int frame = 0; frame < 240 && !engaged; frame++) {
            await Frames(1);
            engaged = encounterLock.State == EncounterLockState.Engaged;
        }
        AssertThat(engaged).OverrideFailureMessage("The bot never walked in far enough to engage the lock.").IsTrue();
        await Frames(2);
        AssertThat(scene.World.Nav.BarrierRebuilds > rebuildsBefore)
            .OverrideFailureMessage("The entry barrier's closing must rebuild the nav graph on the frame it toggles.")
            .IsTrue();
        AssertThat(scene.World.Nav.ClosedBarriersAtBuild).IsEqual(2);

        // Engaged: with the near member down, the far one 2400+ px away is the
        // target (no 620 px cap inside the lock).
        near.TakeDamage(99999);
        far.SetPhysicsProcess(false);
        far.GlobalPosition = new Vector2(BotLockScene.FarX, BotLockScene.FeetY);
        far.Velocity = Vector2.Zero;
        await Frames(3);
        AssertThat(far.GlobalPosition.DistanceTo(scene.Hero.GlobalPosition) > 620f).IsTrue();
        AssertThat(bot.CurrentTarget == far)
            .OverrideFailureMessage($"Expected the far member as the target, got {bot.CurrentTarget?.Name} ({bot.CurrentAction}).")
            .IsTrue();

        // Bypass fights only what the run requires; play fights the nearest foe.
        EnemyController stranger = scene.SpawnPassiveStranger(scene.Hero.GlobalPosition.X + 150f);
        await Frames(2);
        AssertThat(bot.CurrentTarget == stranger)
            .OverrideFailureMessage("The play bot fights the nearest foe, lock member or not.")
            .IsTrue();
        var bypass = new StoryBot(scene.World, FightStyle.Bypass, CampaignLevel.Orleans);
        bypass.Sample(0, default);
        AssertThat(bypass.CurrentTarget == far)
            .OverrideFailureMessage($"Bypass must fight the lock's member and skip a mob no lock owns, got {bypass.CurrentTarget?.Name}.")
            .IsTrue();
        scene.FreeStranger();

        // A member nobody can reach (frozen 650 px over the floor): the bot hunts
        // it from below, and with no progress the watchdog reports the lock.
        InputManager.Instance.ClearInputSource(0);
        far.GlobalPosition = new Vector2(BotLockScene.FarX, BotLockScene.FeetY - 650f);
        for (int frame = 0; frame < 90 && !scene.Hero.IsOnFloor(); frame++) await Frames(1);
        await Frames(2);
        bot.Sample(0, default);
        AssertThat(bot.CurrentAction).IsEqual("lock_hunt");
        AssertThat(bot.CurrentTarget == far).IsTrue();
        int samples = 0;
        while (bot.StuckLockID == null && samples < StoryBot.LockStallFrames + 10) {
            bot.Sample(0, default);
            samples++;
        }
        AssertThat(bot.StuckLockID).IsEqual(encounterLock.LockID);
        AssertThat(samples <= StoryBot.LockStallFrames + 10)
            .OverrideFailureMessage("An unreachable member must end the run at the single stall bound.")
            .IsTrue();
        AssertThat(bot.NavNotes.Any(note => note.StartsWith($"lock {encounterLock.LockID}", StringComparison.Ordinal)
                && note.Contains("no member reachable", StringComparison.Ordinal)))
            .OverrideFailureMessage(string.Join(" | ", bot.NavNotes.Prepend("notes:")))
            .IsTrue();

        // Cleared: the checkpoint is no longer held, and the bot goes to strike it.
        far.TakeDamage(99999);
        InputManager.Instance.SetInputSource(0, bot);
        bool striking = false;
        for (int frame = 0; frame < 120 && !striking; frame++) {
            await Frames(1);
            striking = encounterLock.State == EncounterLockState.Cleared && bot.CurrentAction == "checkpoint";
        }
        AssertThat(PlaytestWorld.IsHeldByLock(checkpoint)).IsFalse();
        AssertThat(striking)
            .OverrideFailureMessage($"After the clear the free checkpoint must become a task (action {bot.CurrentAction}).")
            .IsTrue();
    }

    [TestCase(Timeout = 120000)]
    public async Task TheTelemetryReportsTheLockAndTheCrowdTheDirectorShapes() {
        using var scene = new BotLockScene(addToCurrentScene: false);
        var probe = new TelemetryTickProbe { Name = "EncounterTelemetryTickProbe" };
        Tree.Root.AddChild(probe);
        PlaytestTelemetry telemetry = null;
        try {
            EncounterLock encounterLock = scene.BuildLock(nearOnly: true);
            telemetry = new PlaytestTelemetry();
            telemetry.Attach(scene.World, bot: null);
            probe.Telemetry = telemetry;
            await Frames(3);
            AssertThat(telemetry.Locks.Records.Count).IsEqual(1);
            AssertThat(telemetry.Locks.Records[0].State).IsEqual("armed");

            scene.PlaceHero(BotLockScene.AreaMin + 300f);
            await Frames(3);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
            LockRecord record = telemetry.Locks.Records[0];
            AssertThat(record.EngageFrame >= 0).IsTrue();
            // Two slashers on one side on Normal: one holds the slot, one waits.
            await Frames(60);

            // An Echo-Step-like move outside reverts the lock with both alive.
            scene.PlaceHero(BotLockScene.AreaMin - 300f);
            await Frames(3);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);
            AssertThat(record.LeftWithMembersAlive).IsEqual(1);

            scene.PlaceHero(BotLockScene.AreaMin + 300f);
            await Frames(3);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
            foreach (EnemyController member in encounterLock.LivingMembers) member.TakeDamage(99999);
            await Frames(3);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);

            Dictionary<string, object> report = telemetry.ToReport();
            var locks = (Dictionary<string, object>)report["locks"];
            AssertThat((int)locks["count"]).IsEqual(1);
            AssertThat((int)locks["cleared"]).IsEqual(1);
            AssertThat((int)locks["reverts"]).IsEqual(1);
            AssertThat((int)locks["left_with_members_alive"]).IsEqual(1);
            AssertThat((int)locks["members_planned"]).IsEqual(2);
            AssertThat((int)locks["members_killed"]).IsEqual(2);
            AssertThat(record.ClearFrame > record.EngageFrame).IsTrue();
            AssertThat(record.Engages).IsEqual(2);

            var enemies = (Dictionary<string, object>)report["enemy_summary"];
            AssertThat((int)enemies["lock_members_seen"]).IsEqual(2);
            AssertThat((int)enemies["lock_members_killed"]).IsEqual(2);
            AssertThat((int)enemies["left_lock_with_members_alive"]).IsEqual(1);

            MobEngagementTracker crowd = telemetry.Engagement;
            AssertThat(crowd.SlotTotal).IsEqual(StoryEngagementRules.MeleeSlotsTotal(Difficulty.Normal));
            AssertThat(crowd.EngagedFrames > 0).IsTrue();
            AssertThat(crowd.SlotGrants >= 1)
                .OverrideFailureMessage("The woken members must take a melee slot in the director.")
                .IsTrue();
            AssertThat(crowd.MaxHolders >= 1).IsTrue();
            AssertThat(crowd.FramesHoldersOverSlots).IsEqual(0);
            AssertThat(crowd.WaiterGapSamples > 0)
                .OverrideFailureMessage("Two slashers on one side on Normal: the second must ring as a waiter.")
                .IsTrue();
            var engagement = (Dictionary<string, object>)report["mob_engagement"];
            AssertThat((int)engagement["slot_total"]).IsEqual(2);
            Dictionary<string, object> summary = telemetry.EncounterSummary();
            AssertThat((int)summary["locks_cleared"]).IsEqual(1);
            AssertThat((int)summary["left_lock_alive"]).IsEqual(1);
        } finally {
            probe.Telemetry = null;
            telemetry?.FinishPending();
            telemetry?.Detach();
            probe.Free();
        }
    }

    [TestCase(Timeout = 60000)]
    public async Task AnAirStrikeIsRecordedAsItsOwnSwing() {
        using var scene = new BotLockScene(addToCurrentScene: false);
        var probe = new TelemetryTickProbe { Name = "AirStrikeTelemetryTickProbe" };
        Tree.Root.AddChild(probe);
        PlaytestTelemetry telemetry = null;
        var source = new BufferedInputSource();
        try {
            InputManager.Instance.SetInputSource(0, source);
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
            await Frames(10);
            telemetry = new PlaytestTelemetry();
            telemetry.Attach(scene.World, bot: null);
            probe.Telemetry = telemetry;

            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Jump));
            await Frames(1);
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Jump, previousHeld: GameplayButtons.Jump));
            for (int frame = 0; frame < 8; frame++) await Frames(1);
            AssertThat(scene.Hero.IsOnFloor()).OverrideFailureMessage("Harness fault: the hero never left the floor.").IsFalse();
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Jump | GameplayButtons.BasicAttack,
                previousHeld: GameplayButtons.Jump));
            await Frames(1);
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Jump | GameplayButtons.BasicAttack,
                previousHeld: GameplayButtons.Jump | GameplayButtons.BasicAttack));
            for (int frame = 0; frame < 40 && !telemetry.Swings.Any(s => !s.IsOpen); frame++) await Frames(1);

            SwingRecord swing = telemetry.Swings.FirstOrDefault();
            AssertThat(swing != null).OverrideFailureMessage("No swing was recorded.").IsTrue();
            AssertThat(swing.Hit).IsEqual(BasicComboRules.AirAttackHitboxID);
            AssertThat(swing.Air).IsTrue();
            AssertThat(PlaytestCombatMath.HitVariant(swing.Hit)).IsEqual("air_attack");
        } finally {
            probe.Telemetry = null;
            telemetry?.FinishPending();
            telemetry?.Detach();
            InputManager.Instance?.ClearInputSource(0);
            probe.Free();
        }
    }

    // === FIX-BOT (2026-10-13): reaching lock members and solving lock puzzles ===

    [TestCase(Timeout = 120000)]
    public async Task TheBotBreaksABlockBetweenItAndALockMemberInsteadOfPushingAtIt() {
        // Review item 11 (Level 4's cell B): the member presses against the far
        // face of a breakable wall the hero cannot jump; the old walk-in pushed at
        // the near face for the rest of the run.
        using var scene = new BotLockScene();
        DestructibleBlock wall = scene.AddBlock(new Vector2(5000f, BotLockScene.FloorY - 240f), new Vector2(1f, 5f), maxHp: 30);
        EncounterLock encounterLock = scene.BuildLock(new[] { new EncounterLockSpawn("chrono_slasher", new Vector2(5160f, BotLockScene.FeetY)) });
        EnemyController member = encounterLock.LivingMembers.Single();
        scene.PlaceHero(4800f);
        var bot = new StoryBot(scene.World, FightStyle.Play, CampaignLevel.Orleans);
        InputManager.Instance.SetInputSource(0, bot);

        var actions = new HashSet<string>();
        for (int frame = 0; frame < 1500 && member.IsAlive; frame++) {
            await Frames(1);
            actions.Add(bot.CurrentAction);
        }
        AssertThat(wall.IsDestroyed)
            .OverrideFailureMessage($"The bot never broke the wall between it and the member (actions: {string.Join(",", actions)}; "
                + $"hero x {scene.Hero.GlobalPosition.X:0}).")
            .IsTrue();
        AssertThat(member.IsAlive)
            .OverrideFailureMessage($"The bot broke the wall but never killed the member behind it (HP {member.CurrentHP}).")
            .IsFalse();
        AssertThat(actions.Contains("break_path")).IsTrue();
    }

    [TestCase(Timeout = 120000)]
    public async Task TheBotClimbsOntoTheLedgeALockMemberHoldsInsteadOfJumpingAtItFromBelow() {
        // Gap 3 (Level 9's seawall, Level 7's quay, Level 10's galleries): a member
        // posted on a ledge the hero can reach is fought up there. The old bot stood
        // under it and jumped into the ledge's underside.
        using var scene = new BotLockScene();
        const float ledgeTop = BotLockScene.FloorY - 200f;
        scene.AddSolid("PlaytestBotLedge", new Rect2(5300f, ledgeTop, 400f, 16f));
        EncounterLock encounterLock = scene.BuildLock(new[] { new EncounterLockSpawn("chrono_slasher", new Vector2(5500f, ledgeTop - 40f)) });
        EnemyController member = encounterLock.LivingMembers.Single();
        scene.PlaceHero(5480f);
        for (int frame = 0; frame < 60 && !(member.IsOnFloor() && scene.Hero.IsOnFloor()); frame++) await Frames(1);
        AssertThat(member.IsOnFloor() && Mathf.Abs(member.GlobalPosition.Y - ledgeTop) < 4f)
            .OverrideFailureMessage($"Harness fault: the member is not standing on the ledge ({member.GlobalPosition}).")
            .IsTrue();
        var bot = new StoryBot(scene.World, FightStyle.Play, CampaignLevel.Orleans);
        InputManager.Instance.SetInputSource(0, bot);

        bool stoodOnLedge = false;
        var actions = new HashSet<string>();
        for (int frame = 0; frame < 900 && !stoodOnLedge; frame++) {
            await Frames(1);
            actions.Add(bot.CurrentAction);
            stoodOnLedge = scene.Hero.IsOnFloor() && Mathf.Abs(scene.Hero.GlobalPosition.Y - ledgeTop) < 4f;
        }
        AssertThat(stoodOnLedge)
            .OverrideFailureMessage($"The bot never got onto the member's ledge (actions: {string.Join(",", actions)}; "
                + $"hero {scene.Hero.GlobalPosition}, member {member.GlobalPosition}).")
            .IsTrue();
        AssertThat(actions.Contains("air_strike"))
            .OverrideFailureMessage("A member on a reachable ledge must not be jumped at from below.")
            .IsFalse();
    }

    [TestCase(Timeout = 120000)]
    public async Task TheBotStandsOnALockPlateWhoseCentreAPropCovers() {
        // Level 6's east pan: the dropped boulder covers the plate's centre, so the
        // plate never counted as reachable, and the vault's puzzle was never a task.
        using var scene = new BotLockScene();
        const string puzzleID = "playtest_bot_test.plate_puzzle";
        var manager = new PuzzleManager {
            Name = "PlaytestBotPlatePuzzle", PuzzleID = puzzleID,
            RequiredConditionIDs = new[] { "plate_loaded" }, PersistCompletionToSave = false
        };
        scene.Host.AddChild(manager);
        PressurePlate plate = ResourceLoader.Load<PackedScene>("res://scenes/templates/PressurePlateTemplate.tscn").Instantiate<PressurePlate>();
        plate.Name = "PlaytestBotPlate";
        plate.RequiredWeight = 3f;
        plate.PlayerWeight = 1f;
        plate.ConditionID = "plate_loaded";
        plate.Position = new Vector2(5200f, BotLockScene.FloorY - 20f);
        scene.Host.AddChild(plate);
        plate.PuzzleManagerPath = plate.GetPathTo(manager);
        WeightedObject prop = ResourceLoader.Load<PackedScene>("res://scenes/templates/MovableWeightTemplate.tscn").Instantiate<WeightedObject>();
        prop.Name = "PlaytestBotPlateProp";
        prop.WeightUnits = 2f;
        prop.Position = new Vector2(5200f, BotLockScene.FloorY - 28f);
        scene.Host.AddChild(prop);
        EncounterLock encounterLock = scene.BuildLock(Array.Empty<EncounterLockSpawn>(), puzzleIDs: new[] { puzzleID });
        scene.PlaceHero(4700f);
        // A hero still in the air reads every point as reachable (the bot's
        // optimistic airborne rule, cached two seconds): start it on the floor,
        // as Level 6's hero walks in.
        await Frames(10);
        for (int frame = 0; frame < 60 && !scene.Hero.IsOnFloor(); frame++) await Frames(1);
        AssertThat(plate.CurrentWeight).OverrideFailureMessage("Harness fault: the prop is not weighing the plate.").IsEqual(2f);

        var bot = new StoryBot(scene.World, FightStyle.Play, CampaignLevel.Orleans);
        InputManager.Instance.SetInputSource(0, bot);
        var actions = new List<string>();
        string lastAction = null;
        for (int frame = 0; frame < 900 && encounterLock.State != EncounterLockState.Cleared; frame++) {
            await Frames(1);
            if (bot.CurrentAction == lastAction) continue;
            lastAction = bot.CurrentAction;
            actions.Add($"{lastAction}@{scene.Hero.GlobalPosition.X:0}");
        }
        AssertThat(manager.IsCompleted)
            .OverrideFailureMessage($"The bot never stood on the plate (actions {string.Join(",", actions)}, hero x {scene.Hero.GlobalPosition.X:0}, "
                + $"plate weight {plate.CurrentWeight}, prop {prop.GlobalPosition}).")
            .IsTrue();
        AssertThat(string.Join(",", actions).Contains("plate@"))
            .OverrideFailureMessage($"The plate was never the bot's task (actions {string.Join(",", actions)}, prop {prop.GlobalPosition}).")
            .IsTrue();
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
    }

    [TestCase(Timeout = 120000)]
    public async Task TheBotHopsShortToDownAirABreakableJustBelowItsLedge() {
        // Level 6's wedge: a lock-required breakable whose top sits 8 px under the
        // ledge the hero strikes it from. A full jump's apex left the down-air's
        // box short of it every time (Joan never broke the wedge).
        using var scene = new BotLockScene();
        const float ledgeTop = BotLockScene.FloorY - 260f;
        scene.AddSolid("PlaytestBotGallery", new Rect2(5320f, ledgeTop, 360f, 16f));
        DestructibleBlock wedge = scene.AddBlock(new Vector2(5260f, ledgeTop + 56f), Vector2.One, maxHp: 30);
        // The wedge rests on a pillar: nothing to stand on under it.
        scene.AddSolid("PlaytestBotPillar", new Rect2(5212f, ledgeTop + 104f, 96f, BotLockScene.FloorY - ledgeTop - 104f));
        EncounterLock encounterLock = scene.BuildLock(Array.Empty<EncounterLockSpawn>(), breakables: new DamageableEnvironmentObject[] { wedge });
        scene.PlaceHero(5600f);
        var bot = new StoryBot(scene.World, FightStyle.Play, CampaignLevel.Orleans);
        InputManager.Instance.SetInputSource(0, bot);

        for (int frame = 0; frame < 1200 && !wedge.IsDestroyed; frame++) await Frames(1);
        AssertThat(wedge.IsDestroyed)
            .OverrideFailureMessage($"The bot never broke the wedge (action {bot.CurrentAction}, hero {scene.Hero.GlobalPosition}, wedge HP {wedge.CurrentHP}).")
            .IsTrue();
        for (int frame = 0; frame < 10 && encounterLock.State != EncounterLockState.Cleared; frame++) await Frames(1);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
    }

    [TestCase(Timeout = 60000)]
    public async Task TheBotPlansAGearTrainAndPressesGearsThroughTheirSiblingAreas() {
        // Gap 1 (Florence's print shop): the gears keep their interaction areas
        // beside them, and the bot had no gear handling at all.
        using var scene = new BotLockScene();
        var manager = new PuzzleManager {
            Name = "PlaytestBotGearPuzzle", PuzzleID = "playtest_bot_test.gears",
            RequiredConditionIDs = new[] { "a_aligned", "b_aligned" }, PersistCompletionToSave = false
        };
        scene.Host.AddChild(manager);
        RotatingGear gearA = scene.AddGear("GearA", new Vector2(5200f, 820f), 1, "a_aligned", manager);
        RotatingGear gearB = scene.AddGear("GearB", new Vector2(5500f, 820f), 2, "b_aligned", manager);
        gearA.LinkedGearPaths.Add(gearA.GetPathTo(gearB));
        await Frames(2);
        scene.World.Tick();

        InteractionArea areaA = PlaytestWorld.InteractionAreaOf(gearA);
        AssertThat(areaA != null && areaA.Name.ToString() == "GearAInteraction")
            .OverrideFailureMessage("A gear's sibling interaction area must be found through its TargetPath.")
            .IsTrue();
        Dictionary<RotatingGear, int> plan = scene.World.GearPlan();
        AssertThat(plan.TryGetValue(gearA, out int pressesA) ? pressesA : -1).IsEqual(3);
        AssertThat(plan.TryGetValue(gearB, out int pressesB) ? pressesB : -1).IsEqual(3);

        // Pressing what the plan says, re-read after every press, solves it.
        for (int press = 0; press < 12 && !manager.IsCompleted; press++) {
            RotatingGear next = scene.World.GearPlan().Where(p => p.Value > 0).Select(p => p.Key).FirstOrDefault();
            AssertThat(next != null).OverrideFailureMessage("The plan ran out before the puzzle completed.").IsTrue();
            AssertThat(PlaytestWorld.InteractionAreaOf(next).TryInteract(scene.Hero)).IsTrue();
        }
        AssertThat(manager.IsCompleted).IsTrue();
        AssertThat(scene.World.GearPlan().Count).OverrideFailureMessage("A solved train has nothing left to press.").IsEqual(0);
    }

    [TestCase(Timeout = 60000)]
    public async Task TheNavBoardsAFlushLiftAndTracksAPlatformThatLeftItsStop() {
        // Gap 5 (Level 12's spire lift and highline ferry).
        using var scene = new BotLockScene();
        PathMovingPlatform lift = scene.AddPlatform("PlaytestBotLift", new Vector2(7500f, BotLockScene.FloorY + 16f),
            new[] { Vector2.Zero, new Vector2(0f, -400f) });
        lift.Enabled = false;
        scene.AddSolid("PlaytestBotPerch", new Rect2(8200f, BotLockScene.FloorY - 300f, 80f, 16f));
        await Frames(3);
        scene.World.Tick();
        PlaytestNav nav = scene.World.Nav;
        NavSegment floor = nav.StaticSegments.First(s => s.Platform == null && !s.IsRope && s.Spans(7500f) && Mathf.Abs(s.Top - BotLockScene.FloorY) < 2f);
        NavSegment bottomStop = nav.StaticSegments.First(s => s.Platform == lift && s.PlatformEndpoint == 0);

        // The floor holds a Walk edge onto the stop that sits flush in it, so a
        // hero who fell back to the floor can plan back up the lift.
        AssertThat(nav.EdgesFrom(floor).Any(e => e.To == bottomStop && e.Kind == NavEdgeKind.Walk))
            .OverrideFailureMessage("No edge boards a lift stop flush with the floor.")
            .IsTrue();

        // An aerial edge onto a platform stop lands where the platform is now.
        var edge = new NavEdge { From = floor, To = bottomStop, Kind = NavEdgeKind.Jump, LaunchX = 7300f, LandX = 7500f };
        var agent = new NavAgent(nav);
        AssertThat(agent.AirTargetX(edge)).IsEqual(7500f);
        lift.GlobalPosition += new Vector2(120f, 0f);
        AssertThat(agent.AirTargetX(edge)).IsEqual(7620f);

        // A dodge never runs off a ledge: on the narrow perch both ways drop, on
        // the open floor neither does.
        var bot = new StoryBot(scene.World, FightStyle.Play, CampaignLevel.Orleans);
        scene.Hero.GlobalPosition = new Vector2(8255f, BotLockScene.FloorY - 300f);
        scene.Hero.Velocity = Vector2.Zero;
        for (int frame = 0; frame < 30 && !scene.Hero.IsOnFloor(); frame++) await Frames(1);
        AssertThat(bot.RunsOffLedge(scene.Hero, 1f)).IsTrue();
        scene.PlaceHero(4600f);
        for (int frame = 0; frame < 30 && !scene.Hero.IsOnFloor(); frame++) await Frames(1);
        AssertThat(bot.RunsOffLedge(scene.Hero, 1f) || bot.RunsOffLedge(scene.Hero, -1f)).IsFalse();
    }

    [TestCase(Timeout = 60000)]
    public async Task TheBotGoesOnPastAFinishLineWhoseEscapeNeverCompleted() {
        // Level 6's lava run never completes for a hero who crosses its finish
        // trigger in one go (a game bug, reported); the bot used to stand past the
        // line "escaping" for the rest of the run.
        using var scene = new BotLockScene();
        var escape = new EscapeSequenceController {
            Name = "PlaytestBotEscape", SequenceID = "playtest_bot_test.escape", StartX = 2200f, EndX = 3000f, FinishX = 3100f
        };
        scene.Host.AddChild(escape);
        escape.Begin();
        scene.PlaceHero(3400f);
        await Frames(3);
        var bot = new StoryBot(scene.World, FightStyle.Play, CampaignLevel.Orleans);
        bot.Sample(0, default);
        AssertThat(scene.World.RunningEscape() == escape).OverrideFailureMessage("Harness fault: the escape is not running.").IsTrue();
        AssertThat(bot.CurrentAction == "escape")
            .OverrideFailureMessage("A hero past the finish line has nothing left to outrun.")
            .IsFalse();
        AssertThat(bot.NavNotes.Any(note => note.StartsWith("escape playtest_bot_test.escape", StringComparison.Ordinal))).IsTrue();
        scene.PlaceHero(2600f);
        await Frames(2);
        bot.Sample(0, default);
        AssertThat(bot.CurrentAction).IsEqual("escape");
    }

    private static async Task Frames(int count) {
        for (int frame = 0; frame < count; frame++) await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
    }

    /// <summary>
    /// A flat floor from x 2000 to 10000 (top at y 900), a Joan hero west of one
    /// lock from 4000 to 7000 (entry barrier at 3990, exit at 7010) with two
    /// pre-placed slashers, and a <see cref="PlaytestWorld"/> on the tree. The
    /// bot case parents everything to the current scene, so the world's scan
    /// and the nav graph (which read the current scene) see the floor, the
    /// barriers and the checkpoint; the default travel goal (x 11800) lies east.
    /// Hands back the session, the input source, the director, the pooled
    /// bodies (physics restarted) and everything it built.
    /// </summary>
    private sealed class BotLockScene : IDisposable {
        public const float FloorY = 900f;
        public const float FeetY = 850f;
        public const float AreaMin = 4000f;
        public const float AreaMax = 7000f;
        public const float NearX = 4400f;
        public const float FarX = 6800f;
        public const string LockID = "playtest_bot_test_lock";

        public readonly Node2D Host;
        public readonly PlayerController Hero;
        public readonly PlaytestWorld World;
        private readonly SessionData _originalSession;
        private readonly bool _originalPaused;
        private readonly Node _originalCurrentScene;
        private readonly bool _ownsCurrentScene;
        private EnemyController _stranger;

        public BotLockScene(bool addToCurrentScene = true) {
            _originalSession = GameManager.Instance.CurrentSession;
            _originalPaused = Tree.Paused;
            SessionData session = _originalSession;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = "joan";
            session.Difficulty = Difficulty.Normal;
            GameManager.Instance.CurrentSession = session;
            StoryEngagementDirector.ResetForTest();

            Host = new Node2D { Name = "PlaytestEncounterHost" };
            _originalCurrentScene = Tree.CurrentScene;
            if (addToCurrentScene && _originalCurrentScene != null) {
                _originalCurrentScene.AddChild(Host);
            } else {
                Tree.Root.AddChild(Host);
                // The world's scan and the nav graph read the current scene; with
                // none (the GdUnit host), the scene is it until Dispose.
                if (addToCurrentScene) {
                    Tree.CurrentScene = Host;
                    _ownsCurrentScene = true;
                }
            }
            var floor = new StaticBody2D {
                Name = "Floor",
                Position = new Vector2(6000f, FloorY + 20f),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(8000f, 40f) } });
            Host.AddChild(floor);

            Hero = CharacterFactory.CreateCharacter("joan", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
            Host.AddChild(Hero);
            PlaceHero(AreaMin - 150f);
            World = new PlaytestWorld(Tree);
        }

        public void PlaceHero(float x) {
            Hero.GlobalPosition = new Vector2(x, FeetY);
            Hero.Velocity = Vector2.Zero;
        }

        public EncounterLock BuildLock(bool nearOnly = false) {
            var spawns = new List<EncounterLockSpawn> { new("chrono_slasher", new Vector2(NearX, FeetY)) };
            spawns.Add(new EncounterLockSpawn("chrono_slasher", new Vector2(nearOnly ? NearX + 200f : FarX, FeetY)));
            return BuildLock(spawns.ToArray());
        }

        /// <summary>The 4000–7000 lock with these pre-placed members (none: a puzzle or breakable lock) and clear conditions.</summary>
        public EncounterLock BuildLock(EncounterLockSpawn[] spawns, DamageableEnvironmentObject[] breakables = null,
            string[] puzzleIDs = null) {
            EncounterLock encounterLock = EncounterLock.Create(new EncounterLockSpec {
                LockID = LockID,
                Area = EncounterLockSpec.Span(AreaMin, AreaMax, 1080f),
                LevelBounds = new Rect2(1000f, 0f, 10000f, 1080f),
                EntryBarrierX = AreaMin - 10f,
                ExitBarrierX = AreaMax + 10f,
                BarrierFloorY = FloorY,
                Waves = spawns.Length == 0 ? Array.Empty<EncounterLockWave>() : new[] {
                    new EncounterLockWave { RowID = "playtest_bot_test.room", Spawns = spawns, Trigger = EncounterWaveTrigger.OnBuild }
                },
                RequiredBreakables = breakables ?? Array.Empty<DamageableEnvironmentObject>(),
                RequiredPuzzleIDs = puzzleIDs ?? Array.Empty<string>(),
                GoDirection = EncounterGoDirection.Right
            });
            Host.AddChild(encounterLock);
            encounterLock.Hero = Hero;
            encounterLock.Arm();
            foreach (EnemyController member in encounterLock.LivingMembers) member.SetPhysicsProcess(true);
            return encounterLock;
        }

        /// <summary>A solid (Environment) rectangle, in world coordinates.</summary>
        public StaticBody2D AddSolid(string name, Rect2 rect) {
            var body = new StaticBody2D {
                Name = name,
                Position = rect.GetCenter(),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
            Host.AddChild(body);
            return body;
        }

        /// <summary>A 96 x 96 destructible block (scaled), a puzzle_object breakable like Level 4's cell locks and Level 6's wedge.</summary>
        public DestructibleBlock AddBlock(Vector2 center, Vector2 scale, int maxHp) {
            DestructibleBlock block = ResourceLoader.Load<PackedScene>("res://scenes/templates/DestructibleBlockTemplate.tscn")
                .Instantiate<DestructibleBlock>();
            block.Name = $"PlaytestBotBlock{Host.GetChildCount()}";
            block.ObjectID = $"playtest_bot_test.block_{Host.GetChildCount()}";
            block.MaxHP = maxHp;
            block.HitsToBreak = 3;
            block.Position = center;
            block.Scale = scale;
            Host.AddChild(block);
            return block;
        }

        /// <summary>A Florence-style gear with its interaction area beside it (not a child), reporting to <paramref name="manager"/>.</summary>
        public RotatingGear AddGear(string name, Vector2 position, int quarterTurns, string conditionID, PuzzleManager manager) {
            var gear = new RotatingGear {
                Name = name, GearID = $"playtest_bot_test.{name}", Position = position,
                RotationDegrees = quarterTurns * 90f, ConditionID = conditionID, TargetQuarterTurns = 0,
                PuzzleManagerPath = manager.GetPath()
            };
            Host.AddChild(gear);
            var area = new InteractionArea { Name = $"{name}Interaction", Position = position, TargetPath = gear.GetPath() };
            area.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(140f, 160f) } });
            Host.AddChild(area);
            return gear;
        }

        /// <summary>A 200 x 32 moving platform whose body sits at <paramref name="position"/> at its first stop.</summary>
        public PathMovingPlatform AddPlatform(string name, Vector2 position, Vector2[] waypoints) {
            var platform = new PathMovingPlatform {
                Name = name, PlatformID = $"playtest_bot_test.{name}", Position = position, Waypoints = waypoints,
                Speed = 200f, EndpointWaitSeconds = 1f, CollisionLayer = CollisionLayers.Environment, CollisionMask = 0
            };
            platform.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(200f, 32f) } });
            Host.AddChild(platform);
            return platform;
        }

        /// <summary>A Middle fracture behind the hero whose F10 baseline lists the lock: held until it clears.</summary>
        public CheckpointTrigger BuildCheckpoint(string lockID) {
            const string id = "playtest_bot_test_checkpoint";
            StoryManager.Instance.RegisterEncounterBaseline(id, new[] { lockID },
                StoryLevelControllerBase.ExplicitEncounterBaselineVersion);
            var checkpoint = new CheckpointTrigger {
                Name = "PlaytestBotTestCheckpoint",
                CheckpointID = id,
                Role = CheckpointRole.Middle,
                Position = new Vector2(AreaMin - 400f, FloorY)
            };
            Host.AddChild(checkpoint);
            return checkpoint;
        }

        /// <summary>A slasher no lock owns, holding still on the floor (physics off, hurtbox live).</summary>
        public EnemyController SpawnPassiveStranger(float x) {
            EnemyData data = AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_slasher.tres");
            PackedScene packed = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
            _stranger = packed.Instantiate<EnemyController>();
            _stranger.Name = "PlaytestBotTestStranger";
            _stranger.Data = data;
            _stranger.Position = new Vector2(x, FeetY);
            Host.AddChild(_stranger);
            _stranger.OnSpawn();
            _stranger.SetPhysicsProcess(false);
            _stranger.GlobalPosition = new Vector2(x, FeetY);
            return _stranger;
        }

        public void FreeStranger() {
            if (_stranger != null && GodotObject.IsInstanceValid(_stranger)) {
                _stranger.GetParent()?.RemoveChild(_stranger);
                _stranger.Free();
            }
            _stranger = null;
        }

        public void Dispose() {
            InputManager.Instance?.ClearInputSource(0);
            World.Detach();
            FreeStranger();
            if (GodotObject.IsInstanceValid(Host)) {
                // A pooled member whose physics a case stopped must tick again
                // before the pool hands it to another suite (GU-E's FIX-E note).
                Godot.Collections.Array<Node> children = Host.GetChildren();
                using (children.AsDisposable()) {
                    foreach (Node child in children) {
                        if (child is EnemyController enemy) enemy.SetPhysicsProcess(true);
                    }
                }
                PoolManager.Instance?.ReleaseActiveUnder(Host);
                if (_ownsCurrentScene && Tree.CurrentScene == Host) Tree.CurrentScene = _originalCurrentScene;
                Host.GetParent()?.RemoveChild(Host);
                Host.Free();
            }
            StoryEngagementDirector.ResetForTest();
            GameManager.Instance.CurrentSession = _originalSession;
            Tree.Paused = _originalPaused;
        }
    }
}
