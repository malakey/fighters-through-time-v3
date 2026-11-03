using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// FIX-ENEMY (Phase C fix round of the 2026-10-12 gameplay update): the members
/// of an engaged encounter lock in real physics frames, against the bot batch's
/// stuck locks. A flyer never holds out of every hero's reach (Level 14's drone
/// 292 px over Lincoln, Level 3's 308 px over the floor), a thrown flyer stops
/// instead of rising forever and no flyer leaves the top of its lock's view
/// (Level 13's Rift Phantom 5000 px over its lock), a melee ground member above
/// the hero steps off its ledge toward it (Level 7's quay platform, Level 10's
/// gallery tier), the stall watchdog never returns a member to the post it is
/// stalled on, and a struck waiter's retaliation is the one melee attack that
/// holds no slot (Level 13's attackers over the slot count). Every scene is built
/// far from the origin and hands back the session, the pause, the director, the
/// seam and the pooled bodies in <c>finally</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EncounterMemberBehaviourTests {

    private static readonly Vector2 Origin = new(90000f, 0f);
    private const float FloorY = 900f;
    private const float AreaMin = 1000f;
    private const float AreaMax = 2400f;
    private const string RowID = "test_member.room";
    private const float Step = 1f / 60f;

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    // === Item 1: flyers within reach ==========================================

    /// <summary>
    /// The cap is one named number; this pins it against the shipped data. A
    /// flyer settled at the cap over a hero standing on the floor is struck by
    /// every hero's air strike: the box (from the hero's own string profile, no
    /// reach minor bought) rides the jump from the floor to the apex, so it
    /// overlaps the flyer's hurtbox at some moment of the rise exactly when its
    /// top at the apex stands above the hurtbox's bottom. The apex is integrated
    /// through <see cref="StoryAirPhysics"/> with each hero's own jump force and
    /// weight — the single jump (the strict case) and the full jump budget — and
    /// the hurtbox comes from the scene every roster flyer spawns from.
    /// </summary>
    [TestCase]
    public void EveryHeroReachesAFlyerHeldAtTheHoverCap() {
        float cap = EncounterLockRules.FlyerHoverCapPixels;
        var issues = new List<string>();
        int pairs = 0;
        var flyers = new[] { "hologram_drone", "voltaic_shock_drone", "rift_phantom" };
        foreach (string flyerID in flyers) {
            EnemyData data = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{flyerID}.tres");
            AssertThat(data.Behavior == DefaultBehavior.Flying).IsTrue();
            Rect2 hurtbox = HurtboxRelativeToFeet(data);
            // Positive up, from the hero's feet on the floor.
            float flyerBottom = cap - hurtbox.End.Y;
            float flyerTop = cap - hurtbox.Position.Y;
            foreach (string heroID in CharacterRoster.IDs) {
                CharacterData hero = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{heroID}_data.tres");
                float launch = StoryAirPhysics.JumpLaunchSpeed(hero.MaxJumpForce);
                PlayerController.ResolveAirAttackBoxPixels(BasicComboRules.StringProfileFor(heroID), 1f,
                    out Vector2 size, out Vector2 offset);
                float boxTopOverFeet = -(offset.Y - size.Y * 0.5f);
                // From the floor the box is under the flyer: the jump is what reaches it.
                AssertThat(boxTopOverFeet < flyerBottom).IsTrue();
                foreach (int jumps in new[] { 1, Math.Max(1, hero.MaxJumpCount) }) {
                    float rise = StoryAirPhysics.MultiJumpRise(launch, hero.Weight, jumps);
                    float apexBoxTop = rise + boxTopOverFeet;
                    pairs++;
                    if (apexBoxTop <= flyerBottom) {
                        issues.Add($"{heroID} ({jumps} jump(s), rise {rise:0.0}) tops its air strike at {apexBoxTop:0.0}, "
                            + $"under a {flyerID} whose hurtbox spans {flyerBottom:0.0}..{flyerTop:0.0} over the floor");
                    }
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(pairs).IsEqual(flyers.Length * CharacterRoster.IDs.Count * 2);
    }

    [TestCase(Timeout = 60000)]
    public async Task AnEngagedDroneHoldingItsBandOverTheHeroSettlesWithinTheHoverCapAndAFreeOneDoesNot() {
        using var scene = new MemberScene();
        // Level 14's fabrication spine: a hologram drone holding its ranged band
        // 292 px over the hero. A second drone outside the lock is the control:
        // flyers outside a lock keep their altitude (the scope is provisional).
        float startFeet = FloorY - 292f;
        EncounterLock encounterLock = scene.BuildLock(Wave(("hologram_drone", scene.At(1720f, startFeet))));
        EnemyController free = EnemyFactory.Spawn("hologram_drone", scene.Host, scene.At(1680f, startFeet));
        free.SetPhysicsProcess(true);
        await Frames(2);
        EnemyController member = encounterLock.LivingMembers[0];

        scene.PlaceHero(1700f);
        scene.Hero.SetPhysicsProcess(false); // the hero stands; its hits and slides are not the point
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);

        // The settle runs in Chase (holding the band between shots): 132 px at the
        // drone's 240 px/s is 33 such frames; after 45 it never holds above the line.
        float line = EncounterLockRules.FlyerHoverLineY(scene.Hero.GlobalPosition.Y);
        int chaseFrames = 0;
        int checkedFrames = 0;
        for (int frame = 0; frame < 420; frame++) {
            await Frames(1);
            if (member.CurrentState != EnemyState.Chase) continue;
            if (++chaseFrames <= 45) continue;
            checkedFrames++;
            AssertThat(member.GlobalPosition.Y >= line - 0.5f)
                .OverrideFailureMessage($"Frame {frame}: the member drone holds at feet {member.GlobalPosition.Y:0.0}, "
                    + $"above the hover line {line:0.0}, after {chaseFrames} chase frames.")
                .IsTrue();
        }
        AssertThat(checkedFrames).IsGreater(0);
        AssertThat(member.GlobalPosition.Y >= line - 0.5f).IsTrue();
        AssertThat(free.CurrentState is EnemyState.Chase or EnemyState.Attacking).IsTrue();
        AssertThat(Mathf.Abs(free.GlobalPosition.Y - startFeet))
            .OverrideFailureMessage($"The drone outside the lock moved from {startFeet} to {free.GlobalPosition.Y:0.0}.")
            .IsLess(2f);
    }

    [TestCase(Timeout = 60000)]
    public async Task ADroneRestingOnAGantryOverTheHeroIsPutDownBesideItByTheWatchdog() {
        using var scene = new MemberScene();
        // Level 3's midway: the voltaic drone settled onto the solid lamp gantry
        // 308 px over the hero standing under it; no settle takes it through.
        const float gantryTop = FloorY - 308f;
        scene.AddStatic(new Rect2(1570f, gantryTop, 260f, 16f));
        EncounterLock encounterLock = scene.BuildLock(Wave(("voltaic_shock_drone", scene.At(1700f, gantryTop - 120f))));
        await Frames(2);
        EnemyController drone = encounterLock.LivingMembers[0];
        scene.PlaceHero(1700f);
        scene.Hero.SetPhysicsProcess(false);
        for (int frame = 0; frame < 240; frame++) await Frames(1);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(drone.GlobalPosition.Y)
            .OverrideFailureMessage($"The drone was meant to come to rest on the gantry; it is at {drone.GlobalPosition}.")
            .IsEqualApprox(Origin.Y + gantryTop, 1f);

        // Held there, it counts as stalled though it shoots, and is put down beside the hero.
        drone.SetPhysicsProcess(false);
        for (int tick = 0; tick < EncounterLockRules.StallFrames; tick++) encounterLock.Tick(Step);
        AssertThat(encounterLock.StallRelocations).IsEqual(1);
        float gap = StoryEngagementRules.WaitGapForRank(0, StoryEngagementDirector.CurrentDifficulty);
        AssertThat(Mathf.Abs(drone.GlobalPosition.X - scene.Hero.GlobalPosition.X)).IsEqualApprox(gap, 1f);
        AssertThat(drone.GlobalPosition.Y >= EncounterLockRules.FlyerHoverLineY(scene.Hero.GlobalPosition.Y))
            .OverrideFailureMessage($"The drone was put down at {drone.GlobalPosition}, still above the hover line.")
            .IsTrue();
        AssertThat(drone.CurrentState).IsNotEqual(EnemyState.Dead);
    }

    // === Item 2: flyers contained ==============================================

    [TestCase(Timeout = 60000)]
    public async Task AThrownFlyerLosesItsSpeedAndHoversInsteadOfRisingForever() {
        using var scene = new MemberScene();
        // Level 13's Threshold: Joan's forward throw on a Rift Phantom (weight 0.6)
        // left it rising at 79 px/s for good, 5000 px over its lock.
        EnemyController phantom = EnemyFactory.Spawn("rift_phantom", scene.Host, scene.At(1700f, FloorY - 40f));
        phantom.SetPhysicsProcess(true);
        await Frames(2);
        Vector2 launched = phantom.GlobalPosition;
        phantom.LaunchThrown(new Vector2(320f, -79f), bowlingDamage: 6, throwDamage: 6);
        AssertThat(phantom.IsThrownFlight).IsTrue();

        float highest = launched.Y;
        int landedFrame = -1;
        for (int frame = 0; frame < 120; frame++) {
            await Frames(1);
            highest = Mathf.Min(highest, phantom.GlobalPosition.Y);
            if (landedFrame < 0 && !phantom.IsThrownFlight) landedFrame = frame;
        }
        AssertThat(landedFrame)
            .OverrideFailureMessage("The thrown flyer was still in its throw flight after 2 s.")
            .IsGreaterEqual(0);
        AssertThat(landedFrame).IsLess(45);
        AssertThat(launched.Y - highest)
            .OverrideFailureMessage($"The thrown flyer rose {launched.Y - highest:0.0} px.")
            .IsLess(40f);
        AssertThat(phantom.CurrentState).IsNotEqual(EnemyState.Dead);
    }

    [TestCase(Timeout = 60000)]
    public async Task AFlyingMemberIsHeldBelowTheTopOfItsLocksView() {
        using var scene = new MemberScene();
        EncounterLock encounterLock = scene.BuildLock(Wave(("rift_phantom", scene.At(1800f, FloorY - 60f))));
        scene.PlaceHero(1400f);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        EnemyController phantom = encounterLock.LivingMembers[0];
        float viewTop = encounterLock.CameraBounds.Position.Y;

        // Carried far above the frame (the Level 13 phantom ended at y -3455).
        phantom.GlobalPosition = new Vector2(phantom.GlobalPosition.X, -3455f);
        phantom.Velocity = new Vector2(0f, -79f);
        phantom.ApplyEncounterBoundsForTest();
        Rect2 body = BodyRect(phantom);
        AssertThat(body.Position.Y >= viewTop - 0.5f)
            .OverrideFailureMessage($"The phantom's body top {body.Position.Y:0.0} is above the lock view's top {viewTop:0.0}.")
            .IsTrue();
        AssertThat(body.Position.Y <= viewTop + 0.5f).IsTrue();
        AssertThat(phantom.Velocity.Y >= 0f).IsTrue();

        // From the frame's edge it settles back down to the hover line.
        float line = EncounterLockRules.FlyerHoverLineY(scene.Hero.GlobalPosition.Y);
        for (int frame = 0; frame < 300 && phantom.GlobalPosition.Y < line - 0.5f; frame++) await Frames(1);
        AssertThat(phantom.GlobalPosition.Y >= line - 0.5f).IsTrue();
    }

    // === Item 3: ground members come down; the watchdog never loops ===========

    [TestCase(Timeout = 60000)]
    public async Task AnEngagedSlasherOnALedgeStepsDownToTheHeroBelowAndAFreeOneHoldsIt() {
        // Level 7's quay: a slasher on a platform 140 px over the deck the hero stands on.
        const float ledgeTop = FloorY - 140f;
        using (var scene = new MemberScene()) {
            scene.AddStatic(new Rect2(1900f, ledgeTop, 200f, 20f));
            EncounterLock encounterLock = scene.BuildLock(Wave(("chrono_slasher", scene.At(2000f, ledgeTop - 2f))));
            await Frames(10);
            EnemyController member = encounterLock.LivingMembers[0];
            AssertThat(member.GlobalPosition.Y).IsEqualApprox(ledgeTop, 1f);
            scene.PlaceHero(1650f);
            scene.Hero.SetPhysicsProcess(false);
            for (int frame = 0; frame < 300 && member.GlobalPosition.Y < FloorY - 1f; frame++) await Frames(1);
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(member.GlobalPosition.Y >= FloorY - 1f)
                .OverrideFailureMessage($"The member never came down: it stands at {member.GlobalPosition}.")
                .IsTrue();
        }
        using (var scene = new MemberScene()) {
            scene.AddStatic(new Rect2(1900f, ledgeTop, 200f, 20f));
            EnemyController free = EnemyFactory.Spawn("chrono_slasher", scene.Host, scene.At(2000f, ledgeTop - 2f));
            free.SetPhysicsProcess(true);
            scene.PlaceHero(1650f);
            scene.Hero.SetPhysicsProcess(false);
            for (int frame = 0; frame < 300; frame++) await Frames(1);
            AssertThat(free.CurrentState).IsNotEqual(EnemyState.Patrol);
            AssertThat(free.GlobalPosition.Y)
                .OverrideFailureMessage("Outside a lock P4 is unchanged: a ground mob never walks off a 140 px ledge.")
                .IsEqualApprox(ledgeTop, 1f);
        }
    }

    [TestCase(Timeout = 60000)]
    public async Task AnEngagedSlasherStraightAboveTheHeroTurnsAtADeadEndAndStepsDownTheOtherWay() {
        // Level 10's galleries: a slasher on a tier 248 px straight over the hero,
        // here with a wall closing the tier's west end, the way it first faces.
        const float tierTop = FloorY - 248f;
        using var scene = new MemberScene();
        scene.AddStatic(new Rect2(1900f, tierTop, 300f, 20f));
        scene.AddStatic(new Rect2(1880f, tierTop - 200f, 20f, 220f));
        EncounterLock encounterLock = scene.BuildLock(Wave(("chrono_slasher", scene.At(2000f, tierTop - 2f))));
        await Frames(10);
        EnemyController member = encounterLock.LivingMembers[0];
        AssertThat(member.IsFacingRight)
            .OverrideFailureMessage("A dormant member faces the entry (west), toward the dead end.")
            .IsFalse();
        scene.PlaceHero(2000f);
        scene.Hero.SetPhysicsProcess(false);
        for (int frame = 0; frame < 400 && member.GlobalPosition.Y < FloorY - 1f; frame++) await Frames(1);
        AssertThat(member.GlobalPosition.Y >= FloorY - 1f)
            .OverrideFailureMessage($"The member never came down from the tier: it stands at {member.GlobalPosition} "
                + $"({member.CurrentState}, {member.EngagementRole}, facing right {member.IsFacingRight}, v {member.Velocity}, "
                + $"lock {encounterLock.State}, dormant {member.IsEncounterDormant}).")
            .IsTrue();
        AssertThat(member.GlobalPosition.X > Origin.X + 2200f - 40f)
            .OverrideFailureMessage($"It came down at x {member.GlobalPosition.X - Origin.X:0}, not off the tier's open east end.")
            .IsTrue();
    }

    [TestCase(Timeout = 60000)]
    public async Task TheWatchdogPutsAMemberStalledOnItsPostDownBesideTheHeroNotBackOnThePost() {
        using var scene = new MemberScene();
        // Level 10's loop: a member stalled on the post it spawned on was
        // "relocated" to that same post every 600 ticks.
        const float postTop = FloorY - 300f;
        scene.AddStatic(new Rect2(2050f, postTop, 100f, 20f));
        EncounterLock encounterLock = scene.BuildLock(Wave(("chrono_slasher", scene.At(2100f, postTop - 2f))));
        await Frames(10);
        EnemyController member = encounterLock.LivingMembers[0];
        Vector2 anchor = member.EncounterAnchor;
        AssertThat(member.GlobalPosition.DistanceTo(anchor)).IsLess(3f);
        // Held on its post: no AI step can walk it off, so only the watchdog moves it.
        member.SetPhysicsProcess(false);
        scene.PlaceHero(1500f);
        scene.Hero.SetPhysicsProcess(false);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(member.CurrentState).IsEqual(EnemyState.Chase);

        for (int tick = 0; tick < EncounterLockRules.StallFrames; tick++) encounterLock.Tick(Step);
        AssertThat(encounterLock.StallRelocations).IsEqual(1);
        AssertThat(member.EncounterRelocations).IsEqual(1);
        AssertThat(member.GlobalPosition.DistanceTo(anchor) > 100f)
            .OverrideFailureMessage($"The watchdog returned the member to its post at {member.GlobalPosition}.")
            .IsTrue();
        AssertThat(member.GlobalPosition.Y).IsEqualApprox(FloorY, 1f);
        float gap = StoryEngagementRules.WaitGapForRank(0, StoryEngagementDirector.CurrentDifficulty);
        AssertThat(member.GlobalPosition.X - scene.Hero.GlobalPosition.X).IsEqualApprox(gap, 1f);
        AssertThat(member.CurrentState).IsNotEqual(EnemyState.Dead);
    }

    // === Item 6: retaliation is the one ungated melee attack ===================

    [TestCase(Timeout = 60000)]
    public async Task AStruckWaiterRetaliatesWithoutASlotWhileTheHolderKeepsTheOnlyOne() {
        using var scene = new MemberScene(Difficulty.Easy);
        // Easy: one melee slot a side. Two slashers east of the hero; the waiter is
        // struck in reach and, as its stun ends, answers through the V7.4 pressure exit —
        // retaliation the director never gates (Level 13's over-slot frames).
        // Unscaled (an @0 row), so Easy's per-row prefix keeps both.
        EncounterLockWave wave = Wave(
            ("chrono_slasher", scene.At(1580f, FloorY - 2f)),
            ("chrono_slasher", scene.At(1660f, FloorY - 2f)));
        wave.Scaled = false;
        EncounterLock encounterLock = scene.BuildLock(wave);
        scene.PlaceHero(1500f);
        scene.Hero.SetPhysicsProcess(false);
        await Frames(4);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        IReadOnlyList<EnemyController> members = encounterLock.LivingMembers;
        EnemyController holder = members.FirstOrDefault(m => m.EngagementRole == StoryEngagementRules.EngagementRole.Slot);
        EnemyController waiter = members.FirstOrDefault(m => m.EngagementRole == StoryEngagementRules.EngagementRole.Waiting);
        AssertObject(holder).IsNotNull();
        AssertObject(waiter).IsNotNull();

        // The waiter is struck where it stands, inside its reach of the hero.
        float side = waiter.GlobalPosition.X < scene.Hero.GlobalPosition.X ? -1f : 1f;
        waiter.GlobalPosition = new Vector2(scene.Hero.GlobalPosition.X + side * 50f, waiter.GlobalPosition.Y);
        waiter.Velocity = Vector2.Zero;
        waiter.ApplyStun(0.2f);

        bool retaliated = false;
        for (int frame = 0; frame < 90 && !retaliated; frame++) {
            await Frames(1);
            AssertThat(StoryEngagementDirector.HolderCountFor(scene.Hero))
                .OverrideFailureMessage($"Frame {frame}: more slot holders than Easy's total.")
                .IsLessEqual(StoryEngagementRules.MeleeSlotsTotal(Difficulty.Easy));
            if (waiter.CurrentState != EnemyState.Attacking) continue;
            retaliated = true;
            AssertThat(waiter.EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.Waiting);
            AssertThat(waiter.EngagementInFlightClass).IsEqual(StoryEngagementRules.EngagementClass.MeleeSlot);
        }
        AssertThat(retaliated)
            .OverrideFailureMessage("The struck waiter never answered once its stun ended in reach.")
            .IsTrue();
        AssertThat(holder.EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.Slot);
    }

    // === Helpers ===============================================================

    private static EncounterLockWave Wave(params (string EnemyID, Vector2 Position)[] spawns) => new() {
        RowID = RowID,
        Spawns = spawns.Select(spawn => new EncounterLockSpawn(spawn.EnemyID, spawn.Position)).ToArray(),
        Trigger = EncounterWaveTrigger.OnBuild
    };

    /// <summary>A flyer's hurtbox relative to its feet pivot, from the scene its tier spawns from.</summary>
    private static Rect2 HurtboxRelativeToFeet(EnemyData data) {
        PackedScene packed = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var body = packed.Instantiate<EnemyController>();
        try {
            var hurtbox = body.GetNode<Area2D>("Hurtbox");
            var shape = hurtbox.GetNode<CollisionShape2D>("CollisionShape2D");
            Rect2 local = shape.Shape.GetRect();
            return new Rect2(local.Position + shape.Position + hurtbox.Position, local.Size);
        } finally {
            body.Free();
        }
    }

    /// <summary>A body's collision rect, global.</summary>
    private static Rect2 BodyRect(EnemyController body) {
        var shape = body.GetNode<CollisionShape2D>("CollisionShape2D");
        Rect2 local = shape.Shape.GetRect();
        return new Rect2(body.GlobalPosition + shape.Position + local.Position, local.Size);
    }

    private static async Task Frames(int count) {
        for (int frame = 0; frame < count; frame++) await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
    }

    /// <summary>
    /// A flat floor far from the origin (its top at <see cref="FloorY"/>), an
    /// Einstein hero west of the area, and at most one lock from 1000 to 2400
    /// with its entry at 990 and its exit at 2410, the level 0..1080 tall (so
    /// the lock's camera rect starts at y 0). Hands back the session, the pause,
    /// the director, the seam and every pooled body (physics restarted).
    /// </summary>
    private sealed class MemberScene : IDisposable {
        public readonly Node2D Host;
        public readonly PlayerController Hero;
        private readonly SessionData _originalSession;
        private readonly bool _originalPaused;

        public MemberScene(Difficulty difficulty = Difficulty.Normal) {
            _originalSession = GameManager.Instance.CurrentSession;
            _originalPaused = Tree.Paused;
            SessionData session = _originalSession;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = "einstein";
            session.Difficulty = difficulty;
            GameManager.Instance.CurrentSession = session;
            StoryEngagementDirector.ResetForTest();

            Host = new Node2D { Name = "EncounterMemberHost" };
            Tree.Root.AddChild(Host);
            AddStatic(new Rect2(-3000f, FloorY, 9000f, 40f));

            Hero = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
            Host.AddChild(Hero);
            PlaceHero(AreaMin - 600f);
        }

        public Vector2 At(float x, float y = FloorY) => Origin + new Vector2(x, y);

        public void PlaceHero(float x) {
            Hero.GlobalPosition = At(x);
            Hero.Velocity = Vector2.Zero;
        }

        /// <summary>An Environment rect in scene coordinates (x and y from <see cref="Origin"/>).</summary>
        public void AddStatic(Rect2 rect) {
            var body = new StaticBody2D {
                Name = $"Static_{rect.Position.X:0}_{rect.Position.Y:0}",
                Position = Origin + rect.GetCenter(),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
            Host.AddChild(body);
        }

        public EncounterLock BuildLock(params EncounterLockWave[] waves) {
            EncounterLock encounterLock = EncounterLock.Create(new EncounterLockSpec {
                LockID = "test_member_lock",
                Area = EncounterLockSpec.Span(Origin.X + AreaMin, Origin.X + AreaMax, 1080f),
                LevelBounds = new Rect2(Origin.X - 2000f, 0f, 8000f, 1080f),
                EntryBarrierX = Origin.X + AreaMin - 10f,
                ExitBarrierX = Origin.X + AreaMax + 10f,
                BarrierFloorY = FloorY,
                Waves = waves,
                GoDirection = EncounterGoDirection.Right
            });
            Host.AddChild(encounterLock);
            encounterLock.Hero = Hero;
            encounterLock.Arm();
            foreach (EnemyController member in encounterLock.LivingMembers) member.SetPhysicsProcess(true);
            return encounterLock;
        }

        public void Dispose() {
            EncounterLock.SuppressForTesting = false;
            InputManager.Instance?.ClearInputSource(Hero.PlayerIndex);
            if (GodotObject.IsInstanceValid(Host)) {
                Godot.Collections.Array<Node> children = Host.GetChildren();
                using var childrenLifetime = children.AsDisposable();
                foreach (Node child in children) {
                    if (child is EnemyController enemy) enemy.SetPhysicsProcess(true);
                }
            }
            PoolManager.Instance?.ReleaseActiveUnder(Host);
            if (GodotObject.IsInstanceValid(Host)) {
                Host.GetParent()?.RemoveChild(Host);
                Host.Free();
            }
            StoryEngagementDirector.ResetForTest();
            GameManager.Instance.CurrentSession = _originalSession;
            Tree.Paused = _originalPaused;
        }
    }
}
