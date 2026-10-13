using System;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Playtest pass 2026-10-04, workstream ARENA — F8. The Story camera follows the
/// design's rig (design-godot.md "Story Mode Camera2D Configuration"): a dead
/// zone it holds inside, a soft zone the hero never leaves, separate X/Y damping,
/// a 0.3 s look-ahead in the movement direction and a one-unit follow offset —
/// all in <see cref="StoryCameraRules"/>. It drives the camera node's position,
/// never <see cref="Camera2D.Offset"/> (CameraShake's), and keeps room
/// confinement.
///
/// <para>GU-B (gameplay update 2026-10-12): every Story camera runs the closer
/// <see cref="StoryCameraRules.ViewZoom"/>; a boss or Mirror reveal eases it out
/// to frame the arena until the defeat; and the shake divides the offset it
/// writes to a Story camera by its zoom.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryCameraRigTests {
    private const float Step = 1f / 60f;

    // === The rules ========================================================

    [TestCase]
    public void TheRulesCarryTheDesignTable() {
        AssertFloat(StoryCameraRules.DeadZoneWidth).IsEqual(0.1f);
        AssertFloat(StoryCameraRules.DeadZoneHeight).IsEqual(0.15f);
        AssertFloat(StoryCameraRules.SoftZoneWidth).IsEqual(0.6f);
        AssertFloat(StoryCameraRules.SoftZoneHeight).IsEqual(0.5f);
        AssertFloat(StoryCameraRules.DampingX).IsEqual(0.5f);
        AssertFloat(StoryCameraRules.DampingY).IsEqual(0.3f);
        AssertFloat(StoryCameraRules.LookAheadSeconds).IsEqual(0.3f);
        // "(0, 1) - slightly above the character": one unit up from the feet.
        AssertThat(StoryCameraRules.FollowOffsetPixels).IsEqual(new Vector2(0f, -60f));
        // GU-B (2026-10-12): "a closer view on the player" — 1.5, a 1280 x 720
        // world view, eased out to frame a boss arena no wider than the old 1.0.
        AssertFloat(StoryCameraRules.ViewZoom).IsEqual(1.5f);
        AssertThat(StoryCameraRules.ViewWorldSize).IsEqual(new Vector2(1280f, 720f));
        AssertFloat(StoryCameraRules.FramingMarginPixels).IsEqual(120f);
        AssertFloat(StoryCameraRules.ZoomDampingSeconds).IsEqual(0.6f);
        AssertFloat(StoryCameraRules.BossFramingMinZoom).IsEqual(1.0f);
    }

    [TestCase]
    public void TheFramingZoomFitsTheAreaAndItsMarginsBetweenTheFloorAndTheViewZoom() {
        // Level 2's 1240 px arena plus 120 px a side: 1920 / 1480.
        AssertFloat(StoryCameraRules.FramingZoom(1240f, StoryCameraRules.BossFramingMinZoom))
            .IsEqualApprox(1920f / 1480f, 0.0001f);
        // Florence's 760 px arena already fits the closer view: never zoom past it.
        AssertFloat(StoryCameraRules.FramingZoom(760f, StoryCameraRules.BossFramingMinZoom))
            .IsEqual(StoryCameraRules.ViewZoom);
        // The Mirror's 2380 px leash would want 0.73: the floor holds the old 1.0.
        AssertFloat(StoryCameraRules.FramingZoom(2380f, StoryCameraRules.BossFramingMinZoom)).IsEqual(1.0f);
        AssertFloat(StoryCameraRules.FramingZoom(2380f, 1.2f)).IsEqual(1.2f);
        // A floor above the view zoom cannot push the view closer than ViewZoom.
        AssertFloat(StoryCameraRules.FramingZoom(2380f, 3f)).IsEqual(StoryCameraRules.ViewZoom);
        // A degenerate area asks for nothing.
        AssertFloat(StoryCameraRules.FramingZoom(0f, 1f)).IsEqual(StoryCameraRules.ViewZoom);
        // The room floor: Level 4's 1600 px courtyard fits a view no wider than 1.2.
        AssertFloat(StoryCameraRules.BoundsFitZoom(1600f, 1080f)).IsEqualApprox(1.2f, 0.0001f);
        AssertFloat(StoryCameraRules.BoundsFitZoom(3840f, 1080f)).IsEqual(1.0f);
        AssertFloat(StoryCameraRules.BoundsFitZoom(0f, 1080f)).IsEqual(0f);
        // The framed centre: a view exactly as wide as span + margins sits on its middle...
        AssertFloat(StoryCameraRules.FramedCentre(9900f, 9300f, 10540f, 740f, 370f)).IsEqualApprox(9920f, 0.01f);
        // ...a hero at the span's edge leashes it to the hero's reach...
        AssertFloat(StoryCameraRules.FramedCentre(9310f, 9300f, 10540f, 740f, 370f)).IsEqualApprox(9680f, 0.01f);
        // ...and a span wider than the view confines the hero's point inside it.
        AssertFloat(StoryCameraRules.FramedCentre(7800f, 7200f, 9580f, 960f, 480f)).IsEqualApprox(8040f, 0.01f);
        AssertFloat(StoryCameraRules.FramedCentre(8500f, 7200f, 9580f, 960f, 480f)).IsEqualApprox(8500f, 0.01f);
    }

    [TestCase]
    public void DampingClosesNinetyNinePercentOfAGapInItsDampingTime() {
        float closed = 0f;
        float remaining = 1f;
        for (int frame = 0; frame < 30; frame++) {
            float step = remaining * StoryCameraRules.DampFactor(StoryCameraRules.DampingX, Step);
            closed += step;
            remaining -= step;
        }
        AssertFloat(closed).IsEqualApprox(0.99f, 0.002f);
        AssertFloat(StoryCameraRules.DampFactor(0f, Step)).IsEqual(1f);
        AssertFloat(StoryCameraRules.DampFactor(0.5f, 0f)).IsEqual(0f);
    }

    [TestCase]
    public void AnAxisHoldsInsideTheDeadZoneChasesItsEdgeOutsideAndNeverLetsTheSoftZoneGo() {
        // Inside the dead zone: hold.
        AssertFloat(StoryCameraRules.FollowAxis(1000f, 1050f, 96f, 576f, 0.5f, Step)).IsEqual(1000f);
        // Outside: close on the point that puts the target at the dead-zone edge.
        float next = StoryCameraRules.FollowAxis(1000f, 1300f, 96f, 576f, 0.5f, Step);
        AssertFloat(next).IsGreater(1000f);
        AssertFloat(next).IsLess(1300f - 96f);
        // Past the soft zone: a hard catch-up keeps the target on its edge.
        AssertFloat(StoryCameraRules.FollowAxis(1000f, 2000f, 96f, 576f, 0.5f, Step)).IsEqual(2000f - 576f);
        // Look-ahead is speed x 0.3 s, capped.
        AssertFloat(StoryCameraRules.LookAheadTarget(420f)).IsEqualApprox(126f, 0.001f);
        AssertFloat(StoryCameraRules.LookAheadTarget(-5000f)).IsEqual(-StoryCameraRules.MaxLookAheadPixels);
        // Confinement: a view inside a room, and a room narrower than the view.
        AssertFloat(StoryCameraRules.ConfineCenter(100f, 0f, 3200f, 960f)).IsEqual(960f);
        AssertFloat(StoryCameraRules.ConfineCenter(5000f, 0f, 3200f, 960f)).IsEqual(2240f);
        AssertFloat(StoryCameraRules.ConfineCenter(5000f, 0f, 1600f, 960f)).IsEqual(800f);
    }

    // === The rig ==========================================================

    [TestCase]
    public void AConfinerWithoutAFollowTargetIsThePlainCameraItAlwaysWas() {
        Node2D host = CreateHost("CameraPlainHost");
        try {
            var body = new CharacterBody2D { Name = "Body", Position = new Vector2(500f, 500f) };
            host.AddChild(body);
            var camera = new StoryCameraConfiner { Name = "Camera" };
            body.AddChild(camera);
            AssertThat(camera.IsFollowing).IsFalse();
            AssertThat(camera.TopLevel).IsFalse();
            camera.StepFollow(Step);
            AssertThat(camera.Position).IsEqual(Vector2.Zero);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheRigHoldsItsDeadZoneLeadsARunnerAndNeverWritesTheShakeOffset() {
        Node2D host = CreateHost("CameraRigHost");
        try {
            var body = new CharacterBody2D { Name = "Body", Position = new Vector2(5000f, 500f) };
            host.AddChild(body);
            var camera = new StoryCameraConfiner { Name = "Camera", ActiveBounds = new Rect2(0f, 0f, 20000f, 1080f) };
            body.AddChild(camera);
            camera.EnableFollow(body);

            AssertThat(camera.IsFollowing).IsTrue();
            AssertThat(camera.TopLevel).IsTrue();
            AssertThat(camera.GetParent() == body).IsTrue();
            AssertThat(camera.PositionSmoothingEnabled).IsFalse();
            Vector2 view = ViewSize(camera);
            float halfHeight = view.Y / 2f;
            // Cut to the target at load: X on the hero, Y confined to the room band.
            AssertFloat(camera.RigCenter.X).IsEqualApprox(5000f, 0.01f);
            AssertFloat(camera.RigCenter.Y).IsEqualApprox(
                StoryCameraRules.ConfineCenter(500f + StoryCameraRules.FollowOffsetPixels.Y, 0f, 1080f, halfHeight), 0.01f);

            // A shuffle inside the dead zone moves nothing.
            float deadHalf = StoryCameraRules.DeadZoneWidth * view.X / 2f;
            body.Position = new Vector2(5000f + deadHalf * 0.5f, 500f);
            for (int frame = 0; frame < 20; frame++) camera.StepFollow(Step);
            AssertFloat(camera.RigCenter.X).IsEqualApprox(5000f, 0.01f);

            // CameraShake owns Offset; the rig must never touch it.
            camera.Offset = new Vector2(7f, -3f);

            // Run right for a second: the view leads the hero along the run.
            body.Velocity = new Vector2(420f, 0f);
            for (int frame = 0; frame < 60; frame++) {
                body.Position += new Vector2(7f, 0f);
                camera.StepFollow(Step);
            }
            AssertFloat(camera.LookAheadX).IsGreater(100f);
            // Without a lead a steady runner sits at the dead-zone edge plus the
            // damping lag (v x damping / ln 100) behind the centre; the 0.3 s lead
            // pulls the view forward past that edge.
            AssertFloat(camera.RigCenter.X - body.GlobalPosition.X).IsGreater(-deadHalf);
            AssertFloat(Math.Abs(body.GlobalPosition.X - camera.RigCenter.X))
                .IsLessEqual(StoryCameraRules.SoftZoneWidth * view.X / 2f);
            AssertThat(camera.GlobalPosition).IsEqual(camera.RigCenter);
            AssertThat(camera.Offset).IsEqual(new Vector2(7f, -3f));

            // Stop: the lead decays and the view settles back on the hero.
            body.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 120; frame++) camera.StepFollow(Step);
            AssertFloat(Math.Abs(camera.LookAheadX)).IsLess(1f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ATeleportCutsTheViewAndARoomChangeConfinesItAtOnce() {
        Node2D host = CreateHost("CameraCutHost");
        try {
            var body = new CharacterBody2D { Name = "Body", Position = new Vector2(8000f, 500f) };
            host.AddChild(body);
            var camera = new StoryCameraConfiner { Name = "Camera", ActiveBounds = new Rect2(0f, 0f, 20000f, 1080f) };
            body.AddChild(camera);
            camera.EnableFollow(body);
            Vector2 view = ViewSize(camera);

            // A checkpoint-sized relocation: a cut, not a pan across the level.
            body.Position = new Vector2(1500f, 500f);
            camera.StepFollow(Step);
            AssertFloat(camera.RigCenter.X).IsEqualApprox(Mathf.Max(1500f, view.X / 2f), 0.01f);
            AssertFloat(camera.LookAheadX).IsEqual(0f);

            // A room narrower than the screen centres on itself immediately.
            camera.SetBounds(new Rect2(3000f, 0f, view.X * 0.75f, 1080f));
            AssertFloat(camera.RigCenter.X).IsEqualApprox(3000f + view.X * 0.375f, 0.01f);
            AssertThat(camera.GlobalPosition).IsEqual(camera.RigCenter);
        } finally {
            host.Free();
        }
    }

    // === GU-B: the closer Story zoom and arena framing (2026-10-12) ===========

    /// <summary>
    /// The zoom is the confiner's own: it is in place from construction, so the
    /// first <c>SetBounds</c> and the follow's first cut already confine a
    /// 1280 x 720 view — a hero near the room's left wall sits half that view in.
    /// </summary>
    [TestCase]
    public void EveryStoryCameraStartsAtTheViewZoomBeforeItsFirstBoundsAndCut() {
        var loose = new StoryCameraConfiner { Name = "LooseCamera" };
        try {
            AssertThat(loose.Zoom).IsEqual(Vector2.One * StoryCameraRules.ViewZoom);
            AssertFloat(loose.TargetZoom).IsEqual(StoryCameraRules.ViewZoom);
            AssertThat(loose.FramingRequestCount).IsEqual(0);
        } finally {
            loose.Free();
        }

        Node2D host = CreateHost("CameraZoomHost");
        try {
            var body = new CharacterBody2D { Name = "Body", Position = new Vector2(300f, 900f) };
            host.AddChild(body);
            var camera = new StoryCameraConfiner { Name = "Camera", ActiveBounds = new Rect2(0f, 0f, 20000f, 1080f) };
            body.AddChild(camera);
            camera.EnableFollow(body);
            AssertThat(camera.Zoom).IsEqual(Vector2.One * StoryCameraRules.ViewZoom);
            AssertThat(camera.VisibleWorldSize).IsEqual(StoryCameraRules.ViewWorldSize);
            AssertFloat(camera.RigCenter.X).IsEqualApprox(StoryCameraRules.ViewWorldSize.X / 2f, 0.01f);
            // A 1080-tall room is taller than the 720 view: the feet sit on its bottom band.
            AssertFloat(camera.RigCenter.Y).IsEqualApprox(1080f - StoryCameraRules.ViewWorldSize.Y / 2f, 0.01f);
            AssertThat(camera.VisibleWorldRect)
                .IsEqual(new Rect2(camera.RigCenter - StoryCameraRules.ViewWorldSize / 2f, StoryCameraRules.ViewWorldSize));
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// A framing request eases the zoom out toward the fit of its area over
    /// <see cref="StoryCameraRules.ZoomDampingSeconds"/> (99% of the gap), keeps
    /// the view confined at every intermediate zoom, is floored by the room it is
    /// confined to, and its release eases the view back in.
    /// </summary>
    [TestCase]
    public void AFramingRequestEasesTheZoomOutToFitItsAreaAndAReleaseEasesItBack() {
        Node2D host = CreateHost("CameraFramingHost");
        var owner = new object();
        var room = new Rect2(0f, 0f, 20000f, 1080f);
        try {
            // The hero in the middle of a Level 2-sized arena, against the floor.
            var body = new CharacterBody2D { Name = "Body", Position = new Vector2(9900f, 900f) };
            host.AddChild(body);
            var camera = new StoryCameraConfiner { Name = "Camera", ActiveBounds = room };
            body.AddChild(camera);
            camera.EnableFollow(body);

            var arena = new Rect2(9300f, 0f, 1240f, 1080f);
            float fit = StoryCameraRules.FramingZoom(arena.Size.X, StoryCameraRules.BossFramingMinZoom);
            float gap = StoryCameraRules.ViewZoom - fit;
            camera.RequestFraming(owner, arena, StoryCameraRules.BossFramingMinZoom);
            AssertThat(camera.IsFramingFor(owner)).IsTrue();
            AssertFloat(camera.TargetZoom).IsEqualApprox(fit, 0.0001f);
            // A request eases; it never cuts.
            AssertFloat(camera.Zoom.X).IsEqual(StoryCameraRules.ViewZoom);
            AssertFloat(camera.FramingWeight).IsEqual(0f);

            int easeFrames = Mathf.RoundToInt(StoryCameraRules.ZoomDampingSeconds / Step);
            for (int frame = 0; frame < easeFrames; frame++) {
                camera.StepFollow(Step);
                AssertThat(camera.Zoom.X).IsEqual(camera.Zoom.Y);
                if (frame == 0) {
                    AssertFloat(camera.Zoom.X).IsLess(StoryCameraRules.ViewZoom);
                    AssertFloat(camera.Zoom.X).IsGreater(fit);
                    AssertFloat(camera.FramingWeight).IsGreater(0f);
                    AssertFloat(camera.FramingWeight).IsLess(1f);
                }
                // Confined at every zoom of the ease: the 720 → 833 px tall view
                // keeps its bottom on the room's floor band.
                AssertConfined(camera, room);
            }
            AssertFloat(Math.Abs(camera.Zoom.X - fit)).IsLessEqual(0.011f * gap);
            for (int frame = 0; frame < 120; frame++) camera.StepFollow(Step);
            AssertFloat(camera.Zoom.X).IsEqualApprox(fit, 0.0001f);
            AssertFloat(camera.FramingWeight).IsEqual(1f);
            // Framed: the whole arena is in view.
            Rect2 view = camera.VisibleWorldRect;
            AssertFloat(view.Position.X).IsLessEqual(arena.Position.X);
            AssertFloat(view.End.X).IsGreaterEqual(arena.End.X);

            // The release eases back in to the closer view, centred on the hero again.
            camera.ReleaseFraming(owner);
            AssertThat(camera.IsFramingFor(owner)).IsFalse();
            AssertFloat(camera.TargetZoom).IsEqual(StoryCameraRules.ViewZoom);
            camera.StepFollow(Step);
            AssertFloat(camera.Zoom.X).IsGreater(fit);
            AssertFloat(camera.Zoom.X).IsLess(StoryCameraRules.ViewZoom);
            for (int frame = 0; frame < 120; frame++) {
                camera.StepFollow(Step);
                AssertConfined(camera, room);
            }
            AssertFloat(camera.Zoom.X).IsEqual(StoryCameraRules.ViewZoom);
            AssertFloat(camera.FramingWeight).IsEqual(0f);
            AssertFloat(Math.Abs(camera.RigCenter.X - body.GlobalPosition.X))
                .IsLessEqual(StoryCameraRules.DeadZoneWidth * camera.VisibleWorldSize.X / 2f + 0.01f);

            // The room floor: Level 4's 1580 px arena inside its 1600 px courtyard
            // would fit at 1.05, which shows past the courtyard's walls; the view
            // stops at exactly the courtyard's width.
            var courtyard = new Rect2(8640f, 0f, 1600f, 1080f);
            body.Position = new Vector2(9000f, 900f);
            camera.SetBounds(courtyard);
            camera.SnapToTarget();
            camera.RequestFraming(owner, new Rect2(8660f, 0f, 1580f, 1080f), StoryCameraRules.BossFramingMinZoom);
            AssertFloat(camera.TargetZoom).IsEqualApprox(1.2f, 0.0001f);
            for (int frame = 0; frame < 180; frame++) {
                camera.StepFollow(Step);
                AssertConfined(camera, courtyard);
            }
            AssertFloat(camera.Zoom.X).IsEqualApprox(1.2f, 0.0001f);
            AssertFloat(camera.VisibleWorldSize.X).IsEqualApprox(courtyard.Size.X, 0.1f);
            camera.ReleaseFraming(owner);
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// A zoom that fits an arena only frames it if the view is also over it. A
    /// hero who has just stepped in at the west edge (the reveal point) would
    /// keep a boss on the arena's far side off-screen; the framing slides the
    /// view onto the arena so that boss is in view, while the hero never leaves
    /// the soft zone.
    /// </summary>
    [TestCase]
    public void AFramingSlidesTheViewOntoTheArenaWhileTheHeroStaysInTheSoftZone() {
        Node2D host = CreateHost("CameraFramingSlideHost");
        var owner = new object();
        try {
            var body = new CharacterBody2D { Name = "Body", Position = new Vector2(9310f, 900f) };
            host.AddChild(body);
            var camera = new StoryCameraConfiner { Name = "Camera", ActiveBounds = new Rect2(0f, 0f, 20000f, 1080f) };
            body.AddChild(camera);
            camera.EnableFollow(body);

            // Level 2: the arena is 9300-10540 and the Duke stands at 10200.
            var arena = new Rect2(9300f, 0f, 1240f, 1080f);
            const float farSideBossX = 10200f;
            camera.RequestFraming(owner, arena, StoryCameraRules.BossFramingMinZoom);
            for (int frame = 0; frame < 180; frame++) {
                camera.StepFollow(Step);
                float softHalf = StoryCameraRules.SoftZoneWidth * camera.VisibleWorldSize.X / 2f;
                AssertFloat(Math.Abs(body.GlobalPosition.X - camera.RigCenter.X)).IsLessEqual(softHalf + 0.01f);
            }
            Rect2 view = camera.VisibleWorldRect;
            AssertThat(view.End.X > farSideBossX)
                .OverrideFailureMessage($"The far-side boss must be framed (view ends at {view.End.X}).").IsTrue();
            AssertFloat(view.Position.X).IsLess(body.GlobalPosition.X);

            // Walking out of the arena never frames the hero out.
            body.Position = new Vector2(8400f, 900f);
            for (int frame = 0; frame < 240; frame++) {
                camera.StepFollow(Step);
                float softHalf = StoryCameraRules.SoftZoneWidth * camera.VisibleWorldSize.X / 2f;
                AssertFloat(Math.Abs(body.GlobalPosition.X - camera.RigCenter.X)).IsLessEqual(softHalf + 0.01f);
            }
            camera.ReleaseFraming(owner);
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// Two live owners hold the widest of their views whatever order they asked
    /// in; a repeat request replaces its owner's earlier one; a freed owner frames
    /// nothing; the closer view returns only when every owner has released.
    /// </summary>
    [TestCase]
    public void TwoFramingOwnersHoldTheWidestViewUntilBothReleaseAndAFreedOwnerFramesNothing() {
        Node2D host = CreateHost("CameraTwoOwnersHost");
        var narrow = new object();
        var wide = new object();
        Node freedOwner = null;
        try {
            var body = new CharacterBody2D { Name = "Body", Position = new Vector2(5000f, 900f) };
            host.AddChild(body);
            var camera = new StoryCameraConfiner { Name = "Camera", ActiveBounds = new Rect2(0f, 0f, 20000f, 2000f) };
            body.AddChild(camera);
            camera.EnableFollow(body);

            float narrowFit = StoryCameraRules.FramingZoom(1240f, StoryCameraRules.BossFramingMinZoom);
            camera.RequestFraming(narrow, new Rect2(4400f, 0f, 1240f, 1080f), StoryCameraRules.BossFramingMinZoom);
            camera.RequestFraming(wide, new Rect2(3800f, 0f, 2380f, 2000f), StoryCameraRules.BossFramingMinZoom);
            AssertThat(camera.FramingRequestCount).IsEqual(2);
            AssertFloat(camera.TargetZoom).IsEqual(StoryCameraRules.BossFramingMinZoom);
            for (int frame = 0; frame < 120; frame++) camera.StepFollow(Step);
            AssertFloat(camera.Zoom.X).IsEqual(StoryCameraRules.BossFramingMinZoom);

            // The wide owner leaves: the narrow one still frames its arena.
            camera.ReleaseFraming(wide);
            AssertThat(camera.FramingRequestCount).IsEqual(1);
            AssertFloat(camera.TargetZoom).IsEqualApprox(narrowFit, 0.0001f);

            // A repeat request replaces, never stacks.
            float replacedFit = StoryCameraRules.FramingZoom(1500f, StoryCameraRules.BossFramingMinZoom);
            camera.RequestFraming(narrow, new Rect2(4300f, 0f, 1500f, 1080f), StoryCameraRules.BossFramingMinZoom);
            AssertThat(camera.FramingRequestCount).IsEqual(1);
            AssertFloat(camera.TargetZoom).IsEqualApprox(replacedFit, 0.0001f);

            // An owner freed without releasing (an encounter torn down) frames nothing.
            freedOwner = new Node { Name = "FreedFramingOwner" };
            camera.RequestFraming(freedOwner, new Rect2(3800f, 0f, 2380f, 2000f), StoryCameraRules.BossFramingMinZoom);
            AssertFloat(camera.TargetZoom).IsEqual(StoryCameraRules.BossFramingMinZoom);
            freedOwner.Free();
            AssertThat(camera.FramingRequestCount).IsEqual(1);
            AssertFloat(camera.TargetZoom).IsEqualApprox(replacedFit, 0.0001f);
            camera.StepFollow(Step);
            AssertThat(camera.IsFramingFor(narrow)).IsTrue();

            camera.ReleaseFraming(narrow);
            AssertThat(camera.FramingRequestCount).IsEqual(0);
            for (int frame = 0; frame < 120; frame++) camera.StepFollow(Step);
            AssertFloat(camera.Zoom.X).IsEqual(StoryCameraRules.ViewZoom);
        } finally {
            if (freedOwner != null && GodotObject.IsInstanceValid(freedOwner)) freedOwner.Free();
            host.Free();
        }
    }

    [TestCase]
    public void TheCampaignLevelCameraIsTheRigChildOfTheHero() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        StoryLevelControllerBase level = null;
        try {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            level = ResourceLoader.Load<PackedScene>(StoryManager.GetLevelScenePath(CampaignLevel.Orleans))
                .Instantiate<StoryLevelControllerBase>();
            tree.Root.AddChild(level);
            AssertObject(level.Camera).IsNotNull();
            AssertThat(level.Camera.IsFollowing).IsTrue();
            AssertThat(level.Camera.FollowTarget == level.Player).IsTrue();
            AssertThat(level.Camera.GetParent() == level.Player).IsTrue();
            AssertThat(level.Camera.PositionSmoothingEnabled).IsFalse();
            AssertThat(level.Camera.Enabled).IsTrue();
            // GU-B: the campaign camera is the closer Story view, unframed at load.
            AssertThat(level.Camera.Zoom).IsEqual(Vector2.One * StoryCameraRules.ViewZoom);
            AssertFloat(level.Camera.TargetZoom).IsEqual(StoryCameraRules.ViewZoom);
            AssertThat(level.Camera.FramingRequestCount).IsEqual(0);
            AssertThat(StoryCameraConfiner.FindOn(level.Player) == level.Camera).IsTrue();
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (level != null && GodotObject.IsInstanceValid(level)) {
                level.GetParent()?.RemoveChild(level);
                level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>
    /// The zoom lives in the confiner, so the two Story scenes that build their
    /// own camera outside the level base class — the hub and Florence — get it
    /// with no code of their own.
    /// </summary>
    [TestCase]
    public void TheHubAndFlorenceCamerasCarryTheStoryZoom() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        Node scene = null;
        try {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            foreach (string path in new[] { HubScenePath, StoryManager.GetLevelScenePath(CampaignLevel.Florence) }) {
                scene = ResourceLoader.Load<PackedScene>(path).Instantiate();
                tree.Root.AddChild(scene);
                var player = scene.GetNodeOrNull<Node2D>("Player");
                AssertObject(player).OverrideFailureMessage($"{path} has no Player.").IsNotNull();
                StoryCameraConfiner camera = StoryCameraConfiner.FindOn(player);
                AssertObject(camera).OverrideFailureMessage($"{path}'s hero carries no Story camera.").IsNotNull();
                AssertThat(camera.IsFollowing).IsTrue();
                AssertThat(camera.Zoom).IsEqual(Vector2.One * StoryCameraRules.ViewZoom);
                AssertFloat(camera.TargetZoom).IsEqual(StoryCameraRules.ViewZoom);
                PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
                scene.GetParent()?.RemoveChild(scene);
                scene.Free();
                scene = null;
            }
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (scene != null && GodotObject.IsInstanceValid(scene)) {
                scene.GetParent()?.RemoveChild(scene);
                scene.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>
    /// Level 0 runs the closer view too, and the beam break's Eraser watcher (S10)
    /// stands inside it: at the old fixed spot, x 120 by the tear, it would be
    /// off-screen once the hold has carried the hero east.
    /// </summary>
    [TestCase]
    public void LevelZerosCameraCarriesTheZoomAndTheWatcherStandsInsideTheView() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        Level00Controller level = null;
        try {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            level = ResourceLoader.Load<PackedScene>(Level00ScenePath).Instantiate<Level00Controller>();
            tree.Root.AddChild(level);
            var player = level.GetNode<FTT.Characters.PlayerController>("Player");
            StoryCameraConfiner camera = StoryCameraConfiner.FindOn(player);
            AssertObject(camera).IsNotNull();
            AssertThat(camera.IsFollowing).IsTrue();
            AssertThat(camera.Zoom).IsEqual(Vector2.One * StoryCameraRules.ViewZoom);

            // Where the hold leaves the hero: among the locals, east of the tear.
            player.GlobalPosition = new Vector2(1300f, 850f);
            camera.SnapToTarget();
            Rect2 view = camera.VisibleWorldRect;
            AssertFloat(view.Position.X).IsGreater(120f);

            level.BreakTheBeam();
            var watcher = level.GetNodeOrNull<Node2D>("EraserWatcher");
            AssertObject(watcher).IsNotNull();
            AssertFloat(watcher.Position.X)
                .IsEqualApprox(view.Position.X + Level00Controller.WatcherViewInsetPixels, 0.01f);
            AssertFloat(watcher.Position.Y).IsEqual(Level00Controller.TutorialFloorTopY);
            // The whole figure, silhouette (from -11) to lance (to +18), is in view.
            AssertFloat(watcher.Position.X - 11f).IsGreaterEqual(view.Position.X);
            AssertFloat(watcher.Position.X + 18f).IsLessEqual(view.End.X);
            AssertFloat(watcher.Position.Y - 140f).IsGreaterEqual(view.Position.Y);
            AssertFloat(watcher.Position.Y).IsLessEqual(view.End.Y);
        } finally {
            if (level != null && GodotObject.IsInstanceValid(level)) {
                level.GetParent()?.RemoveChild(level);
                level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>
    /// Every boss reveal frames the arena on the hero's camera — the first
    /// viewing's intro beat and a dormant boss woken by a hit alike — and the
    /// defeat, or the encounter leaving the tree, releases it.
    /// </summary>
    [TestCase]
    public void ABossRevealFramesItsArenaOnTheHerosCameraUntilTheDefeat() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        Node2D host = CreateHost("CameraBossFramingHost");
        FTT.Characters.PlayerController player = null;
        BossData beatBoss = ScratchBoss("gu_b_framing_beat_boss");
        BossData wokenBoss = ScratchBoss("gu_b_framing_woken_boss");
        try {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            player = SpawnHeroWithCamera(host, new Vector2(83500f, -30000f),
                new Rect2(80000f, -32000f, 12000f, 4000f));
            StoryCameraConfiner camera = StoryCameraConfiner.FindOn(player);

            var arena = new Rect2(84000f, -31000f, 1240f, 2000f);
            var encounter = new BossEncounterController {
                Name = "FramingBeatEncounter",
                Data = beatBoss,
                ArenaBounds = arena,
                AwardDustOnDefeat = false,
                Position = new Vector2(84600f, -30000f)
            };
            host.AddChild(encounter);
            encounter._Process(Step);
            AssertThat(encounter.IsRevealed).IsFalse();
            AssertThat(camera.FramingRequestCount).IsEqual(0);

            // The first viewing: the hero steps in and the intro beat runs.
            player.GlobalPosition = new Vector2(84100f, -30000f);
            encounter._Process(Step);
            AssertThat(encounter.IsRevealed).IsTrue();
            AssertThat(encounter.IsIntroBeatRunning).IsTrue();
            AssertThat(camera.IsFramingFor(encounter)).IsTrue();
            AssertFloat(camera.TargetZoom).IsEqualApprox(
                StoryCameraRules.FramingZoom(arena.Size.X, StoryCameraRules.BossFramingMinZoom), 0.0001f);
            encounter._Process(BossEncounterController.IntroBeatSeconds + 0.1f);
            AssertThat(encounter.IsIntroBeatRunning).IsFalse();

            EventBus.Instance.RaiseBossDefeated(new BossDefeatedPayload {
                BossID = beatBoss.BossID,
                Position = encounter.Boss.GlobalPosition
            });
            AssertThat(encounter.IsDefeated).IsTrue();
            AssertThat(camera.IsFramingFor(encounter)).IsFalse();
            AssertFloat(camera.TargetZoom).IsEqual(StoryCameraRules.ViewZoom);

            // A dormant boss hit from outside its arena wakes into a framed reveal.
            var wokenArena = new Rect2(88000f, -31000f, 2380f, 2000f);
            var woken = new BossEncounterController {
                Name = "FramingWokenEncounter",
                Data = wokenBoss,
                ArenaBounds = wokenArena,
                AwardDustOnDefeat = false,
                Position = new Vector2(88600f, -30000f)
            };
            host.AddChild(woken);
            woken.Boss.TakeDamage(5);
            AssertThat(woken.IsRevealed).IsTrue();
            AssertThat(woken.IsIntroBeatRunning).IsFalse();
            AssertThat(camera.IsFramingFor(woken)).IsTrue();
            AssertFloat(camera.TargetZoom).IsEqual(StoryCameraRules.BossFramingMinZoom);

            // An encounter torn down mid-fight (a scene change) releases too.
            host.RemoveChild(woken);
            woken.Free();
            AssertThat(camera.FramingRequestCount).IsEqual(0);
            AssertFloat(camera.TargetZoom).IsEqual(StoryCameraRules.ViewZoom);
        } finally {
            StoryManager.Instance?.SetIntegrityClockPause(IntegrityClockPause.BossIntro, false);
            FreeHero(player);
            PoolManager.Instance?.ReleaseActiveUnder(host);
            host.Free();
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>
    /// The Level 13 Mirror's reveal frames its arena the same way — the two
    /// fighters start 1,200 px apart, wider than the closer view — and its
    /// defeat releases the framing.
    /// </summary>
    [TestCase]
    public void TheMirrorRevealFramesItsArenaUntilTheDefeat() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        Node2D host = CreateHost("CameraMirrorFramingHost");
        FTT.Characters.PlayerController player = null;
        try {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            player = SpawnHeroWithCamera(host, new Vector2(96400f, -30000f),
                new Rect2(94000f, -32000f, 8000f, 4000f));
            StoryCameraConfiner camera = StoryCameraConfiner.FindOn(player);

            var arena = new Rect2(96000f, -31000f, 2380f, 2000f);
            var encounter = new MirrorParadoxEncounterController {
                Name = "FramingMirrorEncounter",
                CharacterIDOverride = "einstein",
                RevealDistance = 700f,
                ArenaBounds = arena,
                AwardDustOnDefeat = false,
                DecisionSeed = 77,
                Position = new Vector2(97600f, -30000f)
            };
            host.AddChild(encounter);
            AssertThat(camera.FramingRequestCount).IsEqual(0);

            encounter.Reveal();
            AssertThat(camera.IsFramingFor(encounter)).IsTrue();
            AssertFloat(camera.TargetZoom).IsEqual(StoryCameraRules.BossFramingMinZoom);

            encounter.Mirror.Clone.ApplyDamage(1000);
            AssertThat(encounter.IsDefeated).IsTrue();
            AssertThat(camera.IsFramingFor(encounter)).IsFalse();
            AssertFloat(camera.TargetZoom).IsEqual(StoryCameraRules.ViewZoom);
        } finally {
            FreeHero(player);
            PoolManager.Instance?.ReleaseActiveUnder(host);
            host.Free();
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>
    /// The shake keeps its on-screen size at the closer view: its intensity stays
    /// in world pixels (StoryEnemyCombatTests reads it), and the offset it writes
    /// to a Story camera is divided by that camera's zoom. A Fighter camera's
    /// offset is left as it was.
    /// </summary>
    [TestCase]
    public void TheShakeKeepsItsWorldIntensityAndDividesAStoryCamerasOffsetByItsZoom() {
        CameraShake shake = CameraShake.Instance;
        AssertObject(shake).IsNotNull();
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Type type = typeof(CameraShake);
        System.Reflection.FieldInfo cameraField = type.GetField("_camera", flags);
        System.Reflection.FieldInfo noiseField = type.GetField("_noise", flags);
        System.Reflection.FieldInfo noiseOffsetField = type.GetField("_noiseOffset", flags);
        System.Reflection.FieldInfo intensityField = type.GetField("_shakeIntensity", flags);
        System.Reflection.FieldInfo durationField = type.GetField("_shakeDuration", flags);
        System.Reflection.FieldInfo timerField = type.GetField("_shakeTimer", flags);
        System.Reflection.FieldInfo scaleField = type.GetField("_intensityScale", flags);
        AssertThat(cameraField != null && noiseField != null && noiseOffsetField != null && intensityField != null
            && durationField != null && timerField != null && scaleField != null).IsTrue();
        object savedCamera = cameraField.GetValue(shake);
        object savedNoiseOffset = noiseOffsetField.GetValue(shake);
        object savedIntensity = intensityField.GetValue(shake);
        object savedDuration = durationField.GetValue(shake);
        object savedTimer = timerField.GetValue(shake);
        object savedScale = scaleField.GetValue(shake);

        Node2D host = CreateHost("CameraShakeHost");
        var fighterCamera = new FTT.Combat.FighterCamera { Name = "ShakeFighterCamera", Zoom = new Vector2(1.4f, 1.4f) };
        try {
            var camera = new StoryCameraConfiner { Name = "ShakeStoryCamera" };
            host.AddChild(camera);
            var worldOffset = new Vector2(12f, -6f);
            AssertThat(CameraShake.ScreenStableOffset(camera, worldOffset))
                .IsEqual(worldOffset / StoryCameraRules.ViewZoom);
            AssertThat(CameraShake.ScreenStableOffset(fighterCamera, worldOffset)).IsEqual(worldOffset);

            // Through the live shake: the authored world intensity is stored as
            // is, and the written offset is the noise at that intensity over the zoom.
            cameraField.SetValue(shake, camera);
            intensityField.SetValue(shake, 0f);
            durationField.SetValue(shake, 0f);
            timerField.SetValue(shake, 0f);
            shake.SetIntensityScale(1f);
            shake.Shake(12f, 1f);
            AssertFloat((float)intensityField.GetValue(shake)).IsEqual(12f);
            shake._Process(Step);
            AssertFloat((float)intensityField.GetValue(shake)).IsEqual(12f);

            var noise = (FastNoiseLite)noiseField.GetValue(shake);
            float noiseOffset = (float)noiseOffsetField.GetValue(shake);
            float timer = (float)timerField.GetValue(shake);
            float duration = (float)durationField.GetValue(shake);
            float intensity = 12f * Mathf.Exp(-5f * (1f - timer / duration));
            var expectedWorld = new Vector2(noise.GetNoise2D(noiseOffset, 0) * intensity,
                noise.GetNoise2D(0, noiseOffset) * intensity);
            AssertFloat(camera.Offset.X).IsEqualApprox(expectedWorld.X / StoryCameraRules.ViewZoom, 0.0001f);
            AssertFloat(camera.Offset.Y).IsEqualApprox(expectedWorld.Y / StoryCameraRules.ViewZoom, 0.0001f);
        } finally {
            cameraField.SetValue(shake, savedCamera);
            noiseOffsetField.SetValue(shake, savedNoiseOffset);
            intensityField.SetValue(shake, savedIntensity);
            durationField.SetValue(shake, savedDuration);
            timerField.SetValue(shake, savedTimer);
            scaleField.SetValue(shake, savedScale);
            fighterCamera.Free();
            host.Free();
        }
    }

    // === Helpers ==========================================================

    private static Node2D CreateHost(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    private static Vector2 ViewSize(Camera2D camera) {
        Vector2 viewport = camera.GetViewportRect().Size;
        if (viewport.X <= 0f || viewport.Y <= 0f) viewport = new Vector2(1920f, 1080f);
        return new Vector2(viewport.X / camera.Zoom.X, viewport.Y / camera.Zoom.Y);
    }

    private const string HubScenePath = "res://scenes/campaign/HubWorld.tscn";
    private const string Level00ScenePath = "res://scenes/campaign/Level_00_Tutorial.tscn";

    /// <summary>The rig's view, at its current zoom, lies inside <paramref name="room"/> (half a pixel of float slack).</summary>
    private static void AssertConfined(StoryCameraConfiner camera, Rect2 room) {
        Vector2 half = camera.VisibleWorldSize / 2f;
        AssertFloat(camera.RigCenter.X - half.X).IsGreaterEqual(room.Position.X - 0.5f);
        AssertFloat(camera.RigCenter.X + half.X).IsLessEqual(room.End.X + 0.5f);
        AssertFloat(camera.RigCenter.Y - half.Y).IsGreaterEqual(room.Position.Y - 0.5f);
        AssertFloat(camera.RigCenter.Y + half.Y).IsLessEqual(room.End.Y + 0.5f);
    }

    /// <summary>A code-built single-phase boss with no kit, so a reveal touches no authored seen-set.</summary>
    private static BossData ScratchBoss(string id) => new() {
        BossID = id,
        DisplayNameKey = "boss",
        MaxHP = 100,
        MoveSpeed = 4f,
        MeleeRangeThreshold = 2f,
        RangedRangeThreshold = 4f,
        ChronalDustDrop = 0
    };

    /// <summary>A campaign-shaped hero: a StoryPlayer carrying its following Story camera, as every Story scene builds it.</summary>
    private static PlayerController SpawnHeroWithCamera(Node host, Vector2 position, Rect2 bounds) {
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0);
        player.Name = "Player";
        host.AddChild(player);
        player.GlobalPosition = position;
        var camera = new StoryCameraConfiner { Name = "PlayerCamera", Enabled = true, ActiveBounds = bounds };
        player.AddChild(camera);
        camera.EnableFollow(player);
        return player;
    }

    /// <summary>A grouped StoryPlayer must never outlive its case (AGENTS.md).</summary>
    private static void FreeHero(PlayerController player) {
        if (player == null || !GodotObject.IsInstanceValid(player)) return;
        player.GetParent()?.RemoveChild(player);
        player.Free();
    }
}
