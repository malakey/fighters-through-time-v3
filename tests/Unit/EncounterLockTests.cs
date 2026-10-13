using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Gameplay update 2026-10-12 (GU-E, user request 3): the encounter lock in real
/// physics frames — arming, the engage and both barriers, the clear and its
/// latch, the revert to Armed on a landing outside, Time Freeze holding the
/// engage and the waves, the dormant ward, a pool-recycled member, the fall net,
/// the area clamp (a wall-phasing Rift Phantom included), the HUD payload and
/// widget, the camera lock layer, the stall watchdog, the hard cap, the
/// push-out, the base builder and the test seam. Every scene is built far from
/// the origin and handed back in <c>finally</c> (session, pause, pooled bodies,
/// the seam).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EncounterLockTests {

    private static readonly Vector2 Origin = new(80000f, 0f);
    private const float FloorY = 900f;
    private const float FeetY = 850f;
    private const float AreaMin = 1000f;
    private const float AreaMax = 2400f;
    private const string RowID = "test_lock.room";
    private const float Step = 1f / 60f;

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    // === Arming and engaging =================================================

    [TestCase(Timeout = 60000)]
    public async Task ArmingClosesOnlyTheExitAndPlacesThePrePlacedMembersDormantInsideTheArea() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(2));
        await Frames(3);

        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);
        AssertThat(encounterLock.IsHolding).IsTrue();
        AssertThat(encounterLock.ExitBarrier.IsClosed).IsTrue();
        AssertThat(encounterLock.ExitBarrier.Shape.Disabled).IsFalse();
        AssertThat(encounterLock.EntryBarrier.IsClosed).IsFalse();
        AssertThat(encounterLock.EntryBarrier.Shape.Disabled).IsTrue();
        AssertThat(encounterLock.ExitBarrier.IsInGroup(EncounterLockBarrier.Group)).IsTrue();
        AssertThat(encounterLock.IsInGroup(EncounterLock.Group)).IsTrue();
        AssertThat(encounterLock.IsInGroup(TimeFreezeController.FreezableGroup)).IsTrue();
        AssertThat(encounterLock.ExitBarrier.CollisionLayer).IsEqual(CollisionLayers.Environment);
        AssertThat(encounterLock.ExitBarrier.IsInGroup(StoryGateRules.DoorGroup)).IsFalse();

        IReadOnlyList<EnemyController> members = encounterLock.LivingMembers;
        AssertThat(members.Count).IsEqual(2);
        foreach (EnemyController member in members) {
            AssertThat(member.IsEncounterDormant).IsTrue();
            AssertThat(member.EncounterBounds).IsEqual(encounterLock.Area);
            AssertThat(member.EncounterLockID).IsEqual(encounterLock.LockID);
            AssertThat(member.CurrentState).IsEqual(EnemyState.Patrol);
            // Dormant members face the entry (west).
            AssertThat(member.IsFacingRight).IsFalse();
        }
        AssertThat(encounterLock.PlannedTotal).IsEqual(2);
        AssertThat(encounterLock.RemainingCount).IsEqual(2);
    }

    [TestCase(Timeout = 60000)]
    public async Task TheHeroWalkingInEngagesTheLockClosesBothBarriersAndWakesAndSpawnsItsMembers() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(2), OnEngageWave(1));
        var payloads = new List<EncounterLockPayload>();
        void Record(EncounterLockPayload payload) => payloads.Add(payload);
        EventBus.Instance.OnEncounterLockChanged += Record;
        try {
            await Frames(2);
            AssertThat(encounterLock.LivingMemberCount).IsEqual(2);

            // Just inside the edge is not yet inside the engage inset.
            scene.PlaceHero(AreaMin + 30f);
            await Frames(3);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);

            scene.PlaceHero(AreaMin + 200f);
            await Frames(3);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(encounterLock.EngageCount).IsEqual(1);
            AssertThat(encounterLock.EntryBarrier.IsClosed).IsTrue();
            AssertThat(encounterLock.ExitBarrier.IsClosed).IsTrue();
            // The OnEngage wave spawned in, awake, with its tear.
            AssertThat(encounterLock.AllWavesSpawned).IsTrue();
            AssertThat(encounterLock.LivingMemberCount).IsEqual(3);
            foreach (EnemyController member in encounterLock.LivingMembers) {
                AssertThat(member.IsEncounterDormant).IsFalse();
                AssertThat(member.CurrentState is EnemyState.Chase or EnemyState.Attacking).IsTrue();
            }
            AssertThat(FindChild<EncounterSpawnTear>(scene.Host)).IsNotNull();

            AssertThat(payloads.Count > 0).IsTrue();
            EncounterLockPayload engaged = payloads.Find(p => p.State == EncounterLockState.Engaged);
            AssertThat(engaged.LockID).IsEqual(encounterLock.LockID);
            AssertThat(engaged.Total).IsEqual(3);
            AssertThat(engaged.Remaining).IsEqual(3);
            AssertThat(engaged.GoDirection).IsEqual(EncounterGoDirection.Right);
        } finally {
            EventBus.Instance.OnEncounterLockChanged -= Record;
        }
    }

    [TestCase(Timeout = 60000)]
    public async Task KillingEveryMemberClearsTheLockOpensBothBarriersAndTheClearLatches() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(2));
        var states = new List<EncounterLockState>();
        void Record(EncounterLockPayload payload) => states.Add(payload.State);
        EventBus.Instance.OnEncounterLockChanged += Record;
        try {
            scene.PlaceHero(AreaMin + 200f);
            await Frames(3);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);

            IReadOnlyList<EnemyController> members = encounterLock.LivingMembers;
            members[0].TakeDamage(99999);
            await Frames(2);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(encounterLock.RemainingCount).IsEqual(1);

            members[1].TakeDamage(99999);
            await Frames(2);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
            AssertThat(encounterLock.ForceCleared).IsFalse();
            AssertThat(encounterLock.IsHolding).IsFalse();
            AssertThat(encounterLock.EntryBarrier.IsClosed).IsFalse();
            AssertThat(encounterLock.ExitBarrier.IsClosed).IsFalse();
            AssertThat(encounterLock.ExitBarrier.Shape.Disabled).IsTrue();
            AssertThat(states.Contains(EncounterLockState.Cleared)).IsTrue();

            // Latched: the hero leaving (or a puzzle reset) never re-arms it.
            scene.PlaceHero(AreaMin - 400f);
            await Frames(3);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
            AssertThat(encounterLock.RevertCount).IsEqual(0);
        } finally {
            EventBus.Instance.OnEncounterLockChanged -= Record;
        }
    }

    [TestCase(Timeout = 60000)]
    public async Task AHeroLandingOutsideAnEngagedLockRevertsItToArmedAndWalkingBackInReengages() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(2), OnEngageWave(1));
        scene.PlaceHero(AreaMin + 200f);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        EnemyController member = encounterLock.LivingMembers[0];
        member.TakeDamage(5);
        int hpBefore = member.CurrentHP;

        // A rewind-style landing west of the closed entry: written straight
        // through the barrier, as the death rewind's playback does.
        scene.PlaceHero(AreaMin - 300f);
        await Frames(2);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);
        AssertThat(encounterLock.RevertCount).IsEqual(1);
        AssertThat(encounterLock.EntryBarrier.IsClosed).IsFalse();
        AssertThat(encounterLock.ExitBarrier.IsClosed)
            .OverrideFailureMessage("A revert keeps the exit shut, so a rewind can never skip a lock.")
            .IsTrue();
        foreach (EnemyController body in encounterLock.LivingMembers) AssertThat(body.IsEncounterDormant).IsTrue();
        AssertThat(member.CurrentHP).IsEqual(hpBefore);
        // The waves that spawned stay spawned.
        AssertThat(encounterLock.LivingMemberCount).IsEqual(3);

        scene.PlaceHero(AreaMin + 200f);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(encounterLock.EngageCount).IsEqual(2);
        AssertThat(member.IsEncounterDormant).IsFalse();
    }

    [TestCase(Timeout = 60000)]
    public async Task TimeFreezeHoldsTheEngageAndTheWavesUntilTheThaw() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(1), OnEngageWave(2));
        float originalCooldown = StoryManager.Instance.TimeFreezeCooldownRemaining;
        var freeze = new TimeFreezeController { Name = "EncounterLockFreeze", DrillFreeFreeze = true };
        scene.Host.AddChild(freeze);
        freeze.SetPhysicsProcess(false);
        try {
            await Frames(2);
            AssertThat(freeze.TryBeginTimeFreeze()).IsTrue();
            AssertThat(encounterLock.IsTimeFrozen).IsTrue();

            scene.PlaceHero(AreaMin + 200f);
            await Frames(4);
            AssertThat(encounterLock.State)
                .OverrideFailureMessage("A frozen world cannot engage a lock.")
                .IsEqual(EncounterLockState.Armed);
            AssertThat(encounterLock.LivingMemberCount).IsEqual(1);

            freeze.EndFreeze(early: true);
            AssertThat(encounterLock.IsTimeFrozen).IsFalse();
            await Frames(3);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(encounterLock.LivingMemberCount).IsEqual(3);
        } finally {
            if (GodotObject.IsInstanceValid(freeze) && freeze.IsFrozen) freeze.EndFreeze(early: true);
            StoryManager.Instance.SetTimeFreezeCooldown(originalCooldown);
        }
    }

    // === Members =============================================================

    [TestCase(Timeout = 60000)]
    public async Task ADormantMemberRefusesEveryPlayerHitButAnEngagedOneTakesIt() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(1));
        await Frames(2);
        EnemyController member = encounterLock.LivingMembers[0];
        int hp = member.CurrentHP;

        float dealt = member.TakeDamage(PlayerHit(member));
        AssertThat(dealt).IsEqual(0f);
        AssertThat(member.CurrentHP).IsEqual(hp);
        AssertThat(member.CurrentState).IsNotEqual(EnemyState.Stunned);
        AssertThat(member.Velocity.X).IsEqual(0f);
        AssertThat(member.DamageStatusType).IsEqual(StatusType.None);
        AssertThat(member.EncounterWardedHitsRefused).IsEqual(1);
        member.ApplyStatusEffect(StatusType.Root, 2f);
        AssertThat(member.ControlStatusType)
            .OverrideFailureMessage("A dormant member refuses a hitless status too.")
            .IsEqual(StatusType.None);
        AssertThat(member.IsGrabbable).IsFalse();

        scene.PlaceHero(AreaMin + 200f);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(member.TakeDamage(PlayerHit(member))).IsGreater(0f);
        AssertThat(member.CurrentHP).IsLess(hp);
    }

    [TestCase(Timeout = 60000)]
    public async Task APooledMemberReleasedOrRecycledCountsAsRemoved() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(2));
        await Frames(2);
        IReadOnlyList<EnemyController> members = encounterLock.LivingMembers;
        EnemyController released = members[0];
        EnemyController recycled = members[1];
        int generation = recycled.SpawnGeneration;
        int deaths = 0;
        recycled.Died += _ => deaths++;

        // Parked by the pool: no longer the lock's member.
        PoolManager.Instance.Release(released);
        AssertThat(released.IsEncounterMember).IsFalse();
        await Frames(2);
        AssertThat(encounterLock.LivingMemberCount).IsEqual(1);
        AssertThat(encounterLock.RecycledMembers).IsEqual(0);

        // Recycled into a new spawn (the RecycleOldest overflow reuses the same
        // node): a new generation with no membership, counted as removed and
        // logged — and the old subscribers are gone with the old life.
        recycled.OnSpawn();
        AssertThat(recycled.SpawnGeneration).IsEqual(generation + 1);
        AssertThat(recycled.IsEncounterMember).IsFalse();
        await Frames(2);
        AssertThat(encounterLock.LivingMemberCount).IsEqual(0);
        AssertThat(encounterLock.RecycledMembers).IsEqual(1);
        AssertThat(encounterLock.LivingMembers.Contains(recycled)).IsFalse();
        recycled.TakeDamage(99999);
        AssertThat(deaths)
            .OverrideFailureMessage("A recycled body must not report its next death to its previous owner.")
            .IsEqual(0);
    }

    [TestCase(Timeout = 60000)]
    public async Task AMemberThatFallsBelowTheAreaIsReturnedToItsSpawnPoint() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(1));
        await Frames(3);
        EnemyController member = encounterLock.LivingMembers[0];
        Vector2 anchor = member.EncounterAnchor;
        AssertThat(anchor).IsEqual(scene.At(1500f));

        member.GlobalPosition = new Vector2(anchor.X, encounterLock.Area.End.Y + EncounterLockRules.FallNetMarginPixels + 30f);
        member.Velocity = new Vector2(0f, 900f);
        await Frames(2);
        AssertThat(member.EncounterFallRecoveries).IsEqual(1);
        AssertThat(Mathf.Abs(member.GlobalPosition.X - anchor.X)).IsLess(1f);
        AssertThat(member.GlobalPosition.Y).IsLess(FloorY + 1f);
        AssertThat(member.CurrentState).IsNotEqual(EnemyState.Dead);
    }

    [TestCase(Timeout = 60000)]
    public async Task AMemberPushedPastTheAreaEdgeIsClampedBackInsideInEveryState() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(1));
        await Frames(3);
        EnemyController member = encounterLock.LivingMembers[0];
        Rect2 area = encounterLock.Area;

        // Dormant: written past the exit (a kit pull, a scripted write).
        member.GlobalPosition = new Vector2(area.End.X + 300f, member.GlobalPosition.Y);
        await Frames(2);
        AssertThat(member.GlobalPosition.X <= area.End.X).IsTrue();

        // Engaged: the same, on the entry side.
        scene.PlaceHero(AreaMin + 600f);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        member.GlobalPosition = new Vector2(area.Position.X - 300f, member.GlobalPosition.Y);
        await Frames(2);
        AssertThat(member.GlobalPosition.X >= area.Position.X).IsTrue();
    }

    [TestCase(Timeout = 60000)]
    public async Task AWallPhasingRiftPhantomStaysInsideTheAreaItCouldOtherwisePassThrough() {
        using var scene = new LockScene();
        var wave = new EncounterLockWave {
            RowID = RowID,
            Spawns = new[] { new EncounterLockSpawn("rift_phantom", scene.At(2200f, 760f)) },
            Trigger = EncounterWaveTrigger.OnBuild
        };
        EncounterLock encounterLock = scene.BuildLock(wave);
        await Frames(2);
        EnemyController phantom = encounterLock.LivingMembers[0];
        AssertThat(phantom.Data.PhasesThroughWalls).IsTrue();
        // Chasing, its body ignores Environment — a barrier cannot hold it.
        AssertThat(EnemyController.ResolveBodyMask(true, EnemyState.Chase) & CollisionLayers.Environment).IsEqual(0u);

        scene.PlaceHero(AreaMin + 600f);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        for (int frame = 0; frame < 10; frame++) {
            phantom.GlobalPosition = new Vector2(encounterLock.Area.End.X + 120f, phantom.GlobalPosition.Y);
            await Frames(1);
            AssertThat(phantom.GlobalPosition.X <= encounterLock.Area.End.X)
                .OverrideFailureMessage($"The phantom ended frame {frame} at {phantom.GlobalPosition}, past the exit.")
                .IsTrue();
        }
    }

    // === Presentation and camera =============================================

    [TestCase(Timeout = 60000)]
    public async Task TheHudCountsTheEnemiesLeftAndShowsTheGoArrowAfterTheClear() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(2));
        StoryHUD hud = StoryHUD.CreateDefault();
        scene.Host.AddChild(hud);
        await Frames(2);
        AssertThat(hud.EncounterCounterVisible).IsFalse();
        AssertThat(hud.EncounterGoPromptVisible).IsFalse();

        scene.PlaceHero(AreaMin + 200f);
        await Frames(3);
        AssertThat(hud.EncounterCounterVisible).IsTrue();
        AssertThat(hud.EncounterCounterText).IsEqual(string.Format(TranslationServer.Translate(StoryHUD.EncounterEnemiesLeftKey), 2));
        AssertThat(hud.EncounterCounterText.Contains("2")).IsTrue();

        encounterLock.LivingMembers[0].TakeDamage(99999);
        await Frames(2);
        AssertThat(hud.EncounterCounterText.Contains("1")).IsTrue();

        encounterLock.LivingMembers[0].TakeDamage(99999);
        await Frames(2);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
        AssertThat(hud.EncounterCounterVisible).IsFalse();
        AssertThat(hud.EncounterGoPromptVisible).IsTrue();
        AssertThat(hud.EncounterGoArrow).IsEqual("→");

        hud._Process(EncounterLockRules.GoPromptSeconds + 0.1);
        AssertThat(hud.EncounterGoPromptVisible).IsFalse();
    }

    [TestCase(Timeout = 60000)]
    public async Task TheCameraLockLayerOverridesRoomBoundsAndEasesInAndOut() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(1));
        var room = new Rect2(Origin.X, 0f, 3200f, 1080f);
        var camera = new StoryCameraConfiner { Name = "PlayerCamera", Enabled = true, ActiveBounds = room };
        scene.Hero.AddChild(camera);
        camera.EnableFollow(scene.Hero);
        encounterLock.Camera = camera;
        await Frames(2);
        AssertThat(camera.ActiveBounds).IsEqual(room);

        scene.PlaceHero(AreaMin + 200f);
        await Frames(2);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(camera.HasLockBounds).IsTrue();
        AssertThat(camera.ActiveBounds).IsEqual(encounterLock.CameraBounds);
        // The lock rect is the area grown to the view and clipped to the level.
        AssertThat(encounterLock.CameraBounds.Size.X >= EncounterLockRules.ViewWorldWidth).IsTrue();
        AssertThat(encounterLock.CameraBounds.Position.Y).IsEqual(0f);
        AssertThat(encounterLock.CameraBounds.End.Y).IsEqual(1080f);

        // A room trigger firing under the lock only updates the stored room.
        var nextRoom = new Rect2(Origin.X + 3200f, 0f, 3200f, 1080f);
        camera.SetBounds(nextRoom);
        AssertThat(camera.ActiveBounds).IsEqual(encounterLock.CameraBounds);
        AssertThat(camera.RoomBounds).IsEqual(nextRoom);

        // Eased, not snapped: the limits move part-way each step and settle.
        AssertThat(camera.IsEasingBounds).IsTrue();
        float firstLeft = camera.LimitLeft;
        for (int step = 0; step < 240 && camera.IsEasingBounds; step++) camera.StepBoundsEase(Step);
        AssertThat(camera.IsEasingBounds).IsFalse();
        AssertThat(camera.CurrentLimits).IsEqual(encounterLock.CameraBounds);
        AssertThat(firstLeft).IsNotEqual(camera.CurrentLimits.Position.X);

        encounterLock.LivingMembers[0].TakeDamage(99999);
        await Frames(2);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
        AssertThat(camera.HasLockBounds).IsFalse();
        AssertThat(camera.ActiveBounds).IsEqual(nextRoom);
        for (int step = 0; step < 240 && camera.IsEasingBounds; step++) camera.StepBoundsEase(Step);
        AssertThat(camera.CurrentLimits).IsEqual(nextRoom);
    }

    // === Safeguards ==========================================================

    [TestCase(Timeout = 60000)]
    public async Task TheStallWatchdogRelocatesAMemberStuckOutOfReachAndNeverKillsIt() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(1));
        scene.PlaceHero(AreaMin + 200f);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        // Let the spawn drop land and chase: a landed, moving member's count is 0.
        await Frames(30);
        EnemyController member = encounterLock.LivingMembers[0];
        Vector2 anchor = member.EncounterAnchor;
        // Stuck far above the hero with nothing under it, going nowhere.
        member.SetPhysicsProcess(false);
        scene.Hero.SetPhysicsProcess(false);
        member.GlobalPosition = new Vector2(anchor.X + 300f, FeetY - 400f);
        member.Velocity = Vector2.Zero;
        member.MoveAndSlide();
        AssertThat(member.IsOnFloor()).IsFalse();

        for (int tick = 0; tick < EncounterLockRules.StallFrames - 1; tick++) encounterLock.Tick(Step);
        AssertThat(encounterLock.StallRelocations).IsEqual(0);
        encounterLock.Tick(Step);
        AssertThat(encounterLock.StallRelocations).IsEqual(1);
        AssertThat(member.EncounterRelocations).IsEqual(1);
        AssertThat(member.GlobalPosition.DistanceTo(anchor)).IsLess(1f);
        AssertThat(member.CurrentState).IsNotEqual(EnemyState.Dead);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
    }

    [TestCase(Timeout = 60000)]
    public async Task TheHardCapForceClearsALockWhoseWaveCanNeverSpawn() {
        using var scene = new LockScene();
        var stuck = new EncounterLockWave {
            RowID = RowID,
            Spawns = new[] { new EncounterLockSpawn("chrono_slasher", scene.At(1800f)) },
            Trigger = (EncounterWaveTrigger)99
        };
        EncounterLock encounterLock = scene.BuildLock(stuck);
        await Frames(1);
        scene.Hero.SetPhysicsProcess(false);
        scene.PlaceHero(AreaMin + 200f);
        encounterLock.Tick(Step);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(encounterLock.AllWavesSpawned).IsFalse();

        encounterLock.Tick(EncounterLockRules.HardCapSeconds - 1.0);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        encounterLock.Tick(2.0);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
        AssertThat(encounterLock.ForceCleared).IsTrue();
        AssertThat(encounterLock.ExitBarrier.IsClosed).IsFalse();
    }

    [TestCase(Timeout = 60000)]
    public async Task AClosingBarrierPushesAnOverlappingHeroToTheAreaSide() {
        using var scene = new LockScene();
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(1));
        await Frames(1);
        EncounterLockBarrier entry = encounterLock.EntryBarrier;
        AssertThat(entry.AreaSide).IsEqual(1f);
        scene.Hero.SetPhysicsProcess(false);
        scene.PlaceHero(AreaMin - 10f);
        entry.SetClosed(true);
        AssertThat(entry.PushOut(scene.Hero, 20f)).IsTrue();
        AssertThat(scene.Hero.GlobalPosition.X >= entry.Footprint.End.X + 20f).IsTrue();
        // A hero clear of the wall is left alone.
        AssertThat(entry.PushOut(scene.Hero, 20f)).IsFalse();
    }

    [TestCase(Timeout = 60000)]
    public async Task TheTestSeamBuildsEveryLockCleared() {
        using var scene = new LockScene();
        EncounterLock.SuppressForTesting = true;
        EncounterLock encounterLock = scene.BuildLock(OnBuildWave(2));
        await Frames(2);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
        AssertThat(encounterLock.RestoredCleared).IsTrue();
        AssertThat(encounterLock.RegisteredCount).IsEqual(0);
        AssertThat(encounterLock.ExitBarrier.IsClosed).IsFalse();
        AssertThat(encounterLock.EntryBarrier.IsClosed).IsFalse();
        scene.PlaceHero(AreaMin + 200f);
        await Frames(2);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
    }

    // === The base builder ====================================================

    [TestCase(Timeout = 60000)]
    public async Task ABaseLevelArmsItsLocksWithItsHeroAndCameraAndHandsTheObjectiveBackOnTheClear() {
        SessionData original = GameManager.Instance.CurrentSession;
        bool originalPaused = Tree.Paused;
        EncounterLockTestLevelController level = null;
        try {
            SessionData session = original;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = "einstein";
            session.Difficulty = Difficulty.Normal;
            GameManager.Instance.CurrentSession = session;
            level = new EncounterLockTestLevelController { Name = "EncounterLockTestLevel" };
            Tree.Root.AddChild(level);
            await Frames(2);

            AssertThat(level.EncounterLocks.Count).IsEqual(1);
            EncounterLock encounterLock = level.EncounterLocks[0];
            AssertThat(encounterLock.IsArmedOnce).IsTrue();
            AssertThat(encounterLock.Hero == level.Player).IsTrue();
            AssertThat(encounterLock.Camera == level.Camera).IsTrue();
            AssertThat(encounterLock.LivingMemberCount).IsEqual(1);
            AssertThat(encounterLock.Spec.LevelBounds).IsEqual(level.LevelBounds);

            Label objective = level.HUD.GetNode<Label>("SafeArea/TopLeft/Objective");
            level.Player.GlobalPosition = new Vector2(1600f, FeetY);
            level.Player.Velocity = Vector2.Zero;
            await Frames(3);
            AssertThat(level.ActiveEncounterLock == encounterLock).IsTrue();
            AssertThat(objective.Text).IsEqual(TranslationServer.Translate("encounter_lock_objective_clear").ToString());
            AssertThat(level.Camera.HasLockBounds).IsTrue();

            encounterLock.LivingMembers[0].TakeDamage(99999);
            await Frames(2);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
            AssertThat(objective.Text).IsEqual(TranslationServer.Translate("orleans_objective_advance").ToString());
            AssertThat(level.Camera.HasLockBounds).IsFalse();
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (level != null && GodotObject.IsInstanceValid(level)) {
                level.GetParent()?.RemoveChild(level);
                level.Free();
            }
            GameManager.Instance.CurrentSession = original;
            Tree.Paused = originalPaused;
            StoryManager.Instance?.StopLevelRun();
        }
    }

    // === Helpers =============================================================

    private static EncounterLockWave OnBuildWave(int count) {
        var spawns = new List<EncounterLockSpawn>();
        float[] xs = { 1500f, 2000f, 1750f };
        for (int index = 0; index < count; index++) {
            spawns.Add(new EncounterLockSpawn("chrono_slasher", Origin + new Vector2(xs[index], FeetY)));
        }
        return new EncounterLockWave { RowID = RowID, Spawns = spawns.ToArray(), Trigger = EncounterWaveTrigger.OnBuild };
    }

    private static EncounterLockWave OnEngageWave(int count) {
        var spawns = new List<EncounterLockSpawn>();
        for (int index = 0; index < count; index++) {
            spawns.Add(new EncounterLockSpawn("chrono_slasher", Origin + new Vector2(1800f + 150f * index, FeetY)));
        }
        return new EncounterLockWave { RowID = RowID + "_wave", Spawns = spawns.ToArray(), Trigger = EncounterWaveTrigger.OnEngage };
    }

    private static HitPayload PlayerHit(EnemyController target) => new() {
        AttackerIndex = 0,
        TargetIndex = -1,
        AttackID = "einstein.basic",
        HitboxID = "combo_1",
        AttackClass = AttackClass.Basic,
        Origin = HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = 12f,
        Knockback = new Vector2(3f, -1f),
        HitstunDuration = 0.4f,
        AppliedStatus = StatusType.Venom,
        StatusDuration = 2f,
        StatusIntensity = 1f,
        HitOrigin = target.GlobalPosition + new Vector2(-200f, -30f),
        AttackerFacingRight = true,
        ContactId = HitClassification.NextContactId()
    };

    private static async Task Frames(int count) {
        for (int frame = 0; frame < count; frame++) await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
    }

    private static T FindChild<T>(Node root) where T : Node {
        Godot.Collections.Array<Node> children = root.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is T match) return match;
        }
        return null;
    }

    /// <summary>
    /// A flat floor far from the origin, an Einstein hero west of the area, and
    /// one lock from 1000 to 2400 with its entry at 990 and its exit at 2410.
    /// Hands back the session, the pause, the pooled bodies and the seam.
    /// </summary>
    private sealed class LockScene : IDisposable {
        public readonly Node2D Host;
        public readonly PlayerController Hero;
        private readonly SessionData _originalSession;
        private readonly bool _originalPaused;

        public LockScene() {
            _originalSession = GameManager.Instance.CurrentSession;
            _originalPaused = Tree.Paused;
            SessionData session = _originalSession;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = "einstein";
            session.Difficulty = Difficulty.Normal;
            GameManager.Instance.CurrentSession = session;

            Host = new Node2D { Name = "EncounterLockHost" };
            Tree.Root.AddChild(Host);
            var floor = new StaticBody2D {
                Name = "Floor",
                Position = Origin + new Vector2(1000f, FloorY + 20f),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(8000f, 40f) } });
            Host.AddChild(floor);

            Hero = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
            Host.AddChild(Hero);
            PlaceHero(AreaMin - 400f);
        }

        public Vector2 At(float x, float y = FeetY) => Origin + new Vector2(x, y);

        public void PlaceHero(float x) {
            Hero.GlobalPosition = At(x);
            Hero.Velocity = Vector2.Zero;
        }

        public EncounterLock BuildLock(params EncounterLockWave[] waves) {
            EncounterLock encounterLock = EncounterLock.Create(new EncounterLockSpec {
                LockID = "test_lock",
                Area = EncounterLockSpec.Span(Origin.X + AreaMin, Origin.X + AreaMax, 1080f),
                LevelBounds = new Rect2(Origin.X - 2000f, 0f, 8000f, 1080f),
                EntryBarrierX = Origin.X + AreaMin - 10f,
                ExitBarrierX = Origin.X + AreaMax + 10f,
                BarrierFloorY = FloorY,
                Waves = waves,
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            Host.AddChild(encounterLock);
            encounterLock.Hero = Hero;
            encounterLock.Arm();
            return encounterLock;
        }

        public void Dispose() {
            EncounterLock.SuppressForTesting = false;
            InputManager.Instance?.ClearInputSource(Hero.PlayerIndex);
            PoolManager.Instance?.ReleaseActiveUnder(Host);
            if (GodotObject.IsInstanceValid(Host)) {
                Host.GetParent()?.RemoveChild(Host);
                Host.Free();
            }
            GameManager.Instance.CurrentSession = _originalSession;
            Tree.Paused = _originalPaused;
        }
    }
}

/// <summary>
/// A minimal base level with one encounter lock, for the builder case: the base
/// arms it with the level's hero and camera and wires the objective.
/// </summary>
internal partial class EncounterLockTestLevelController : StoryLevelControllerBase {
    public override string LevelID => "level_02_orleans";
    public override CampaignLevel Level => CampaignLevel.Orleans;
    public override string LevelTitleKey => "orleans_level_title";
    public override string DialogueSetPath => "";
    public override Vector2 PlayerSpawnPosition => new(200, 850);
    public override Rect2 LevelBounds => new(0, 0, 6000, 1080);
    protected override string InitialObjectiveKey => "orleans_objective_advance";

    protected override void BuildLevel() {
        BuildFloor(0, 900, 6000);
        BuildWall(-20, 0, 1080);
        BuildCheckpoint(200, 850, "level_02_orleans_checkpoint_0", CheckpointRole.Entry);
        BuildEncounterLock(new EncounterLockSpec {
            LockID = "level_02_orleans_lock_test",
            Area = EncounterLockSpec.Span(1000f, 2400f, 1080f),
            EntryBarrierX = 990f,
            ExitBarrierX = 2410f,
            BarrierFloorY = 900f,
            Waves = new[] {
                new EncounterLockWave {
                    RowID = "level_02.lock_test",
                    Spawns = new[] { new EncounterLockSpawn("chrono_slasher", new Vector2(2000f, 850f)) },
                    Trigger = EncounterWaveTrigger.OnBuild
                }
            },
            ObjectiveKey = "encounter_lock_objective_clear"
        });
    }
}
