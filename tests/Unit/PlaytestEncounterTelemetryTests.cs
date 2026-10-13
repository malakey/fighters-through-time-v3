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
            EncounterLock encounterLock = EncounterLock.Create(new EncounterLockSpec {
                LockID = LockID,
                Area = EncounterLockSpec.Span(AreaMin, AreaMax, 1080f),
                LevelBounds = new Rect2(1000f, 0f, 10000f, 1080f),
                EntryBarrierX = AreaMin - 10f,
                ExitBarrierX = AreaMax + 10f,
                BarrierFloorY = FloorY,
                Waves = new[] {
                    new EncounterLockWave { RowID = "playtest_bot_test.room", Spawns = spawns.ToArray(), Trigger = EncounterWaveTrigger.OnBuild }
                },
                GoDirection = EncounterGoDirection.Right
            });
            Host.AddChild(encounterLock);
            encounterLock.Hero = Hero;
            encounterLock.Arm();
            foreach (EnemyController member in encounterLock.LivingMembers) member.SetPhysicsProcess(true);
            return encounterLock;
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
