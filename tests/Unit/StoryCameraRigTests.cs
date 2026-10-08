using System;
using FTT.Core;
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
}
