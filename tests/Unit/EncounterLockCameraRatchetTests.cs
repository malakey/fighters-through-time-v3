using System;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// FU-LOCK (feel and UI update 2026-10-25, ruling R4): an encounter area holds the
/// hero in with invisible walls and never moves the camera on its own. The
/// camera's limits open to the union of the view and the lock rect, then tighten
/// onto the rect only as the follow rig moves past a side, and never back. The
/// "Enemies left" counter and the GO prompt are gone from the Story HUD.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EncounterLockCameraRatchetTests {

    private static readonly Vector2 Origin = new(90000f, 0f);
    private const float FloorY = 900f;
    private const float FeetY = 850f;
    private const float AreaMin = 1000f;
    private const float AreaMax = 2400f;

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    [TestCase(Timeout = 60000)]
    public async Task AHeroWhoEngagesAndStandsStillSeesTheCameraStayPut() {
        using var scene = new RatchetScene();
        await scene.SettleAtTheWestEdge();
        scene.PlaceHero(AreaMin + 52f);
        for (int frame = 0; frame < 10 && scene.Lock.State != EncounterLockState.Engaged; frame++) await Frames(1);
        AssertThat(scene.Lock.State).IsEqual(EncounterLockState.Engaged);

        // The rig only closes on the standing hero by its own dead-zone damping
        // (a hero who stepped 22 px is 83 px off a camera parked at the edge of
        // its 64 px dead zone and still closing). A lock that pulled the view onto its rect would
        // put the centre 600 px or more away from him.
        float before = scene.Camera.RigCenter.X;
        for (int frame = 0; frame < 120; frame++) {
            await Frames(1);
            float centre = scene.Camera.RigCenter.X;
            float heroX = scene.Hero.GlobalPosition.X;
            AssertThat(Math.Abs(centre - heroX) <= 120f)
                .OverrideFailureMessage($"The camera centre {centre} is {centre - heroX} px from the hero on frame {frame}.")
                .IsTrue();
            AssertThat(centre >= before - 1f && centre - before <= 40f)
                .OverrideFailureMessage($"The camera went from {before} to {centre} on frame {frame}.")
                .IsTrue();
        }
        AssertThat(scene.Camera.HasLockBounds).IsTrue();
    }

    [TestCase(Timeout = 60000)]
    public async Task WalkingFurtherInTightensTheLimitsMonotonicallyOntoTheLockRect() {
        using var scene = new RatchetScene();
        await scene.SettleAtTheWestEdge();
        scene.PlaceHero(AreaMin + 52f);
        await Frames(4);
        AssertThat(scene.Lock.State).IsEqual(EncounterLockState.Engaged);
        Rect2 target = scene.Lock.CameraBounds;

        Rect2 previous = scene.Camera.CurrentLimits;
        AssertThat(previous.Position.X <= target.Position.X).IsTrue();
        float x = AreaMin + 52f;
        for (int frame = 0; frame < 700 && x < AreaMax - 400f; frame++) {
            x += 3f;
            scene.PlaceHero(x);
            await Frames(1);
            Rect2 now = scene.Camera.CurrentLimits;
            AssertThat(now.Position.X >= previous.Position.X).IsTrue();
            AssertThat(now.End.X <= previous.End.X).IsTrue();
            AssertThat(now.Position.X <= target.Position.X).IsTrue();
            AssertThat(now.End.X >= target.End.X).IsTrue();
            previous = now;
        }
        // The view now sits inside the lock rect, so the limits are exactly it.
        AssertThat(scene.Camera.IsRatchetingBounds).IsFalse();
        AssertThat(scene.Camera.CurrentLimits).IsEqual(target);
    }

    [TestCase(Timeout = 60000)]
    public async Task TheClearDoesNotMoveTheCameraOnItsOwn() {
        using var scene = new RatchetScene();
        await scene.SettleAtTheWestEdge();
        scene.PlaceHero(AreaMin + 52f);
        await Frames(4);
        AssertThat(scene.Lock.State).IsEqual(EncounterLockState.Engaged);
        await Frames(30);

        float before = scene.Camera.RigCenter.X;
        scene.Lock.LivingMembers[0].TakeDamage(99999);
        await Frames(1);
        AssertThat(scene.Lock.State).IsEqual(EncounterLockState.Cleared);
        AssertThat(scene.Camera.HasLockBounds).IsFalse();
        AssertThat(Math.Abs(scene.Camera.RigCenter.X - before) <= 1f).IsTrue();
    }

    [TestCase(Timeout = 60000)]
    public async Task AClosedBarrierStillBlocksButIsNeverDrawn() {
        using var scene = new RatchetScene();
        await Frames(5);
        scene.PlaceHero(AreaMin + 200f);
        await Frames(4);
        AssertThat(scene.Lock.State).IsEqual(EncounterLockState.Engaged);
        foreach (EncounterLockBarrier barrier in new[] { scene.Lock.EntryBarrier, scene.Lock.ExitBarrier }) {
            AssertThat(barrier.IsClosed).IsTrue();
            AssertThat(barrier.Shape.Disabled).IsFalse();
            AssertThat(barrier.Visible).IsFalse();
            AssertThat(barrier.CollisionLayer).IsEqual(CollisionLayers.Environment);
        }

        // A ray across the exit barrier meets it in real physics.
        await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
        var query = PhysicsRayQueryParameters2D.Create(
            Origin + new Vector2(AreaMax - 60f, 800f), Origin + new Vector2(AreaMax + 60f, 800f), CollisionLayers.Environment);
        Godot.Collections.Dictionary hit = scene.Host.GetWorld2D().DirectSpaceState.IntersectRay(query);
        AssertThat(hit.Count > 0).IsTrue();
    }

    [TestCase(Timeout = 60000)]
    public async Task EnteringFromTheRightKeepsTheCameraStillAndTightensTheFarSide() {
        using var scene = new RatchetScene();
        await Frames(5);
        scene.PlaceHero(AreaMax - 30f);
        await Frames(360);
        AssertThat(scene.Lock.State).IsEqual(EncounterLockState.Armed);
        scene.PlaceHero(AreaMax - 52f);
        for (int frame = 0; frame < 10 && scene.Lock.State != EncounterLockState.Engaged; frame++) await Frames(1);
        AssertThat(scene.Lock.State).IsEqual(EncounterLockState.Engaged);
        Rect2 target = scene.Lock.CameraBounds;

        float before = scene.Camera.RigCenter.X;
        await Frames(45);
        AssertThat(Math.Abs(scene.Camera.RigCenter.X - before) <= 1f).IsTrue();

        Rect2 previous = scene.Camera.CurrentLimits;
        float x = AreaMax - 52f;
        for (int frame = 0; frame < 700 && x > AreaMin + 400f; frame++) {
            x -= 3f;
            scene.PlaceHero(x);
            await Frames(1);
            Rect2 now = scene.Camera.CurrentLimits;
            AssertThat(now.End.X <= previous.End.X).IsTrue();
            AssertThat(now.Position.X >= previous.Position.X).IsTrue();
            AssertThat(now.End.X >= target.End.X).IsTrue();
            previous = now;
        }
        AssertThat(scene.Camera.CurrentLimits).IsEqual(target);
    }

    [TestCase(Timeout = 60000)]
    public async Task AVerticalLockRectTightensTopAndBottomTheSameWay() {
        var host = new Node2D { Name = "RatchetVerticalHost", Position = Origin };
        Tree.Root.AddChild(host);
        var body = new Node2D { Name = "Follow", Position = Origin + new Vector2(1600f, 1500f) };
        host.AddChild(body);
        var camera = new StoryCameraConfiner {
            Name = "VerticalCamera", Enabled = true, ActiveBounds = new Rect2(Origin.X, 0f, 3200f, 2000f)
        };
        body.AddChild(camera);
        try {
            camera.EnableFollow(body);
            await Frames(2);
            var lockRect = new Rect2(Origin.X + 800f, 1000f, 1600f, 700f);
            camera.SetLockBounds(lockRect);
            AssertThat(camera.IsRatchetingBounds).IsTrue();
            Rect2 previous = camera.CurrentLimits;
            AssertThat(previous.Position.Y <= lockRect.Position.Y).IsTrue();
            AssertThat(previous.End.Y >= lockRect.End.Y).IsTrue();

            for (int frame = 0; frame < 600; frame++) {
                body.GlobalPosition += new Vector2(0f, -1f);
                camera.StepFollow(1f / 60f);
                Rect2 now = camera.CurrentLimits;
                AssertThat(now.Position.Y >= previous.Position.Y).IsTrue();
                AssertThat(now.End.Y <= previous.End.Y).IsTrue();
                AssertThat(now.End.Y >= lockRect.End.Y).IsTrue();
                previous = now;
            }
            AssertThat(camera.CurrentLimits.End.Y).IsEqual(lockRect.End.Y);
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    [TestCase(Timeout = 60000)]
    public async Task TheStoryHudHasNoEncounterLockPanel() {
        StoryHUD hud = StoryHUD.CreateDefault();
        Tree.Root.AddChild(hud);
        try {
            await Frames(2);
            AssertThat(hud.GetNodeOrNull("SafeArea/EncounterLockPanel")).IsNull();
        } finally {
            hud.GetParent()?.RemoveChild(hud);
            hud.Free();
        }
    }

    private static async Task Frames(int count) {
        for (int frame = 0; frame < count; frame++) await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
    }

    private sealed class RatchetScene : IDisposable {
        public readonly Node2D Host;
        public readonly PlayerController Hero;
        public readonly EncounterLock Lock;
        public readonly StoryCameraConfiner Camera;
        private readonly SessionData _originalSession;
        private readonly bool _originalPaused;

        public RatchetScene() {
            _originalSession = GameManager.Instance.CurrentSession;
            _originalPaused = Tree.Paused;
            SessionData session = _originalSession;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = "einstein";
            session.Difficulty = Difficulty.Normal;
            GameManager.Instance.CurrentSession = session;

            Host = new Node2D { Name = "RatchetHost" };
            Tree.Root.AddChild(Host);
            var floor = new StaticBody2D {
                Name = "Floor",
                Position = Origin + new Vector2(1600f, FloorY + 20f),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(3200f, 40f) } });
            Host.AddChild(floor);

            Hero = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
            Hero.ScriptedHPFloor = 1; // the lone member may swing at a hero standing in view
            Host.AddChild(Hero);
            Hero.GlobalPosition = Origin + new Vector2(AreaMin - 400f, FeetY);

            Camera = new StoryCameraConfiner {
                Name = "PlayerCamera", Enabled = true, ActiveBounds = new Rect2(Origin.X, 0f, 3200f, 1080f)
            };
            Hero.AddChild(Camera);
            Camera.EnableFollow(Hero);

            Lock = EncounterLock.Create(new EncounterLockSpec {
                LockID = "ratchet_lock",
                Area = EncounterLockSpec.Span(Origin.X + AreaMin, Origin.X + AreaMax, 1080f),
                LevelBounds = new Rect2(Origin.X, 0f, 3200f, 1080f),
                EntryBarrierX = Origin.X + AreaMin - 10f,
                ExitBarrierX = Origin.X + AreaMax + 10f,
                BarrierFloorY = FloorY,
                Waves = new[] {
                    new EncounterLockWave {
                        RowID = "test_lock.room",
                        Spawns = new[] { new EncounterLockSpawn("chrono_slasher", Origin + new Vector2(1750f, FeetY)) },
                        Trigger = EncounterWaveTrigger.OnBuild
                    }
                },
                GoDirection = EncounterGoDirection.Right
            });
            Host.AddChild(Lock);
            Lock.Hero = Hero;
            Lock.Camera = Camera;
            Lock.Arm();
            foreach (var member in Lock.LivingMembers) member.SetPhysicsProcess(true);
        }

        /// <summary>Parks the hero just outside the engage inset and lets the rig settle on it.</summary>
        public async Task SettleAtTheWestEdge() {
            PlaceHero(AreaMin + 30f);
            await Frames(360);
            AssertThat(Lock.State).IsEqual(EncounterLockState.Armed);
        }

        public void PlaceHero(float x) {
            Hero.GlobalPosition = Origin + new Vector2(x, FeetY);
            Hero.Velocity = Vector2.Zero;
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
