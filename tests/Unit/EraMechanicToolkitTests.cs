using System.Collections.Generic;
using System.IO;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 5 A2: behavior contracts for the eleven era-mechanic toolkit
/// components plus the two Story-only PlayerController hooks they drive.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EraMechanicToolkitTests {
    private const double Step = 1.0 / 60.0;

    // === SearchlightZone ===

    [TestCase]
    public void SearchlightDrainsUltimateInParisModeAndStrikesAfterGraceInBerlinMode() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var drain = new SearchlightZone {
            SearchlightID = "test.searchlight.paris",
            Mode = SearchlightMode.UltimateDrain,
            UltimateDrainPerSecond = 5f
        };
        var strike = new SearchlightZone {
            SearchlightID = "test.searchlight.berlin",
            Mode = SearchlightMode.DelayedStrike,
            ExposureGraceSeconds = 1.5f,
            StrikeDamage = 20
        };
        var player = CharacterFactory.CreateCharacter("einstein");
        int detections = 0;
        tree.Root.AddChild(drain);
        tree.Root.AddChild(strike);
        tree.Root.AddChild(player);
        drain.PlayerDetected += _ => detections++;
        try {
            player.RestoreStoryCheckpoint(Vector2.Zero, player.MaximumHP, 50f);
            AssertThat(drain.AddPlayer(player)).IsTrue();
            AssertThat(detections).IsEqual(1);
            AssertThat(drain.AddPlayer(player)).IsFalse();

            // 1 second of exposure at 5%/s.
            drain.TickExposure(1f);
            AssertFloat(player.CurrentUltimateMeter).IsEqualApprox(45f, 0.001f);
            AssertThat(drain.RemovePlayer(player)).IsTrue();
            drain.TickExposure(1f);
            AssertFloat(player.CurrentUltimateMeter).IsEqualApprox(45f, 0.001f);

            int startingHP = player.CurrentHP;
            strike.AddPlayer(player);
            strike.TickExposure(1.4f);
            AssertThat(player.CurrentHP).IsEqual(startingHP);
            AssertFloat(strike.ExposureFor(player)).IsEqualApprox(1.4f, 0.001f);
            strike.TickExposure(0.2f);
            AssertThat(player.CurrentHP).IsEqual(startingHP - 20);
            AssertThat(strike.StrikeCount).IsEqual(1);
            // Exposure restarts after a strike so the beam re-arms instead of chain-hitting.
            AssertFloat(strike.ExposureFor(player)).IsEqualApprox(0f, 0.001f);
        } finally {
            drain.Free();
            strike.Free();
            player.Free();
        }
    }

    [TestCase]
    public void SearchlightSweepsItsArcAndRestoresTheCheckpointPhase() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var light = new SearchlightZone {
            SearchlightID = "test.searchlight.sweep",
            SweepMode = SearchlightSweepMode.Arc,
            SweepArcDegrees = 60f,
            SweepPeriodSeconds = 4f
        };
        tree.Root.AddChild(light);
        try {
            AssertFloat(light.SweepPhase).IsEqualApprox(0f, 0.0001f);
            AssertFloat(light.RotationDegrees).IsEqualApprox(0f, 0.001f);

            // Quarter period reaches one extreme of the half-arc.
            light.AdvanceSweep(1f);
            AssertFloat(light.SweepPhase).IsEqualApprox(0.25f, 0.0001f);
            AssertFloat(light.RotationDegrees).IsEqualApprox(30f, 0.001f);

            light.CaptureCheckpointState("checkpoint");
            light.AdvanceSweep(1f);
            AssertFloat(light.RotationDegrees).IsEqualApprox(0f, 0.001f);
            light.ApplyStoryRewind();
            AssertFloat(light.SweepPhase).IsEqualApprox(0.25f, 0.0001f);
            AssertFloat(light.RotationDegrees).IsEqualApprox(30f, 0.001f);
        } finally {
            light.Free();
        }
    }

    // === RisingWaterZone ===

    [TestCase]
    public void RisingWaterRaisesInStepsSlowsSubmergedPlayersAndDrownsAfterGrace() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var water = new RisingWaterZone {
            WaterID = "test.water",
            StepHeights = new[] { 0f, 180f, 360f },
            AutoAdvanceSeconds = 0f,
            SubmergedMoveMultiplier = 0.5f,
            DrownGraceSeconds = 0.5f,
            DrownTickSeconds = 0.5f,
            DrownDamage = 5
        };
        var player = CharacterFactory.CreateCharacter("einstein");
        var steps = new List<int>();
        tree.Root.AddChild(water);
        tree.Root.AddChild(player);
        water.WaterLevelChanged += step => steps.Add(step);
        try {
            water.GlobalPosition = Vector2.Zero;
            AssertFloat(water.WaterLineGlobalY).IsEqualApprox(0f, 0.001f);

            AssertThat(water.AdvanceWaterLevel()).IsTrue();
            AssertThat(water.CurrentStep).IsEqual(1);
            AssertFloat(water.WaterLineGlobalY).IsEqualApprox(-180f, 0.001f);
            AssertThat(water.AdvanceWaterLevel()).IsTrue();
            AssertThat(water.AdvanceWaterLevel()).IsFalse();
            AssertThat(steps).ContainsExactly(1, 2);

            // Below the line: slowed, and drowning once the grace period elapses.
            player.GlobalPosition = new Vector2(0f, 100f);
            water.AddPlayer(player);
            water.TickSubmersion(0.1f);
            AssertFloat(player.EnvironmentMoveMultiplier).IsEqualApprox(0.5f, 0.001f);
            AssertFloat(player.StatusMovementMultiplier).IsEqualApprox(1f, 0.001f);

            int startingHP = player.CurrentHP;
            for (int tick = 0; tick < 20; tick++) water.TickSubmersion(0.1f);
            AssertThat(player.CurrentHP).IsLess(startingHP);

            // Climbing above the line surfaces the player and restores the speed.
            player.GlobalPosition = new Vector2(0f, -500f);
            water.TickSubmersion(0.1f);
            AssertFloat(player.EnvironmentMoveMultiplier).IsEqualApprox(1f, 0.001f);
        } finally {
            water.Free();
            player.Free();
        }
    }

    // === GravityFieldZone ===

    [TestCase]
    public void GravityFieldScalesStoryGravityWhileInsideAndRestoresOnExit() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var field = new GravityFieldZone { FieldID = "test.lunar", GravityScale = 0.25f };
        PlayerController normal = CharacterFactory.CreateCharacter("einstein");
        PlayerController light = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(field);
        tree.Root.AddChild(normal);
        tree.Root.AddChild(light);
        try {
            normal.GlobalPosition = new Vector2(-4000f, -4000f);
            light.GlobalPosition = new Vector2(4000f, -4000f);
            normal.TransitionTo(CharacterState.Airborne);
            light.TransitionTo(CharacterState.Airborne);
            AssertThat(field.AddPlayer(light)).IsTrue();
            AssertFloat(light.EnvironmentGravityScale).IsEqualApprox(0.25f, 0.001f);

            for (int frame = 0; frame < 6; frame++) {
                normal._PhysicsProcess(Step);
                light._PhysicsProcess(Step);
            }
            AssertFloat(normal.Velocity.Y).IsGreater(0f);
            // Story gravity integration honours the field: a quarter-gravity fall is
            // exactly a quarter of the normal one over the same frame count.
            AssertFloat(light.Velocity.Y).IsEqualApprox(normal.Velocity.Y * 0.25f, 0.5f);

            AssertThat(field.RemovePlayer(light)).IsTrue();
            AssertFloat(light.EnvironmentGravityScale).IsEqualApprox(1f, 0.001f);
        } finally {
            field.Free();
            normal.Free();
            light.Free();
        }
    }

    [TestCase]
    public void GravityFieldsRegisterThePlayerFromGeometryWithoutWaitingForAPhysicsFrame() {
        // An Area2D only reports its authored initial overlaps on the first physics
        // frame, and a checkpoint resume or a Chronal Rewind teleports the player
        // before one ever runs. Level 12 hand-rolled this sweep; levels 13 and 14
        // use GravityFieldZone's shared version.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GravityFieldZone west = BuildRectField("test.sync.west", 0f, 1000f, 0.4f);
        GravityFieldZone east = BuildRectField("test.sync.east", 1000f, 2000f, 0.7f);
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        var fields = new[] { west, east };
        tree.Root.AddChild(west);
        tree.Root.AddChild(east);
        tree.Root.AddChild(player);
        try {
            AssertThat(west.ContainsPoint(new Vector2(500f, 0f))).IsTrue();
            AssertThat(west.ContainsPoint(new Vector2(1500f, 0f))).IsFalse();

            player.GlobalPosition = new Vector2(500f, 0f);
            AssertThat(GravityFieldZone.SyncPlayerToContainingField(fields, player) == west).IsTrue();
            AssertFloat(player.EnvironmentGravityScale).IsEqualApprox(0.4f, 0.001f);
            AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player)).IsEqual(1);

            // Re-running it is a no-op, not a second source.
            GravityFieldZone.SyncPlayerToContainingField(fields, player);
            AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player)).IsEqual(1);

            // A teleport across the seam re-registers into exactly the new field.
            player.GlobalPosition = new Vector2(1500f, 0f);
            AssertThat(GravityFieldZone.SyncPlayerToContainingField(fields, player) == east).IsTrue();
            AssertFloat(player.EnvironmentGravityScale).IsEqualApprox(0.7f, 0.001f);
            AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player)).IsEqual(1);

            // Outside every field the player is released back to Earth-normal.
            player.GlobalPosition = new Vector2(9000f, 0f);
            AssertObject(GravityFieldZone.SyncPlayerToContainingField(fields, player)).IsNull();
            AssertFloat(player.EnvironmentGravityScale).IsEqualApprox(1f, 0.001f);
            AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player)).IsEqual(0);
        } finally {
            west.Free();
            east.Free();
            player.Free();
        }
    }

    private static GravityFieldZone BuildRectField(string id, float startX, float endX, float scale) {
        float width = endX - startX;
        var field = new GravityFieldZone {
            FieldID = id,
            GravityScale = scale,
            Position = new Vector2(startX + width / 2f, 0f)
        };
        field.AddChild(new CollisionShape2D {
            Name = "CollisionShape2D",
            Shape = new RectangleShape2D { Size = new Vector2(width, 2000f) }
        });
        return field;
    }

    [TestCase]
    public void CyclingGravityFieldTelegraphsThenShiftsScaleForEveryoneInside() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var field = new GravityFieldZone {
            FieldID = "test.void",
            CycleScales = new[] { 0.3f, 1.6f },
            CycleSeconds = 1f,
            TelegraphSeconds = 0.25f
        };
        var player = CharacterFactory.CreateCharacter("einstein");
        var shifts = new List<float>();
        tree.Root.AddChild(field);
        tree.Root.AddChild(player);
        field.GravityShifted += scale => shifts.Add(scale);
        try {
            AssertThat(field.IsCycling).IsTrue();
            AssertFloat(field.CurrentScale).IsEqualApprox(0.3f, 0.001f);
            field.AddPlayer(player);
            AssertFloat(player.EnvironmentGravityScale).IsEqualApprox(0.3f, 0.001f);

            field._PhysicsProcess(0.7);
            AssertThat(field.IsTelegraphing).IsFalse();
            field._PhysicsProcess(0.1);
            AssertThat(field.IsTelegraphing).IsTrue();
            AssertFloat(field.NextScale).IsEqualApprox(1.6f, 0.001f);

            field._PhysicsProcess(0.3);
            AssertThat(field.IsTelegraphing).IsFalse();
            AssertThat(field.CycleIndex).IsEqual(1);
            AssertFloat(field.CurrentScale).IsEqualApprox(1.6f, 0.001f);
            AssertFloat(player.EnvironmentGravityScale).IsEqualApprox(1.6f, 0.001f);
            AssertThat(shifts).ContainsExactly(1.6f);
        } finally {
            field.Free();
            player.Free();
        }
    }

    // === Shared environment modifier registry ===

    [TestCase]
    public void OverlappingEnvironmentZonesStackMultiplicativelyAndUnwindIndependently() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var sand = new MovementDampenerZone { ZoneID = "test.sand", MoveMultiplier = 0.5f };
        var mud = new MovementDampenerZone { ZoneID = "test.mud", MoveMultiplier = 0.8f };
        var player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(sand);
        tree.Root.AddChild(mud);
        tree.Root.AddChild(player);
        try {
            sand.AddPlayer(player);
            AssertFloat(player.EnvironmentMoveMultiplier).IsEqualApprox(0.5f, 0.001f);
            mud.AddPlayer(player);
            AssertFloat(player.EnvironmentMoveMultiplier).IsEqualApprox(0.4f, 0.001f);
            AssertThat(EnvironmentPlayerModifiers.MoveSourceCount(player)).IsEqual(2);

            // A real status effect must coexist with terrain, not replace it.
            player.GetNode<StatusController>("StatusController").ApplyStatus(StatusType.Root, 1f, 1f);
            AssertFloat(player.EnvironmentMoveMultiplier).IsEqualApprox(0.4f, 0.001f);
            player.GetNode<StatusController>("StatusController").ClearStatus();

            sand.RemovePlayer(player);
            AssertFloat(player.EnvironmentMoveMultiplier).IsEqualApprox(0.8f, 0.001f);
            mud.RemovePlayer(player);
            AssertFloat(player.EnvironmentMoveMultiplier).IsEqualApprox(1f, 0.001f);
            AssertThat(EnvironmentPlayerModifiers.MoveSourceCount(player)).IsEqual(0);

            // Freeing a zone that still holds a registration restores the player.
            sand.AddPlayer(player);
            AssertFloat(player.EnvironmentMoveMultiplier).IsEqualApprox(0.5f, 0.001f);
            sand.Free();
            AssertFloat(player.EnvironmentMoveMultiplier).IsEqualApprox(1f, 0.001f);
        } finally {
            if (GodotObject.IsInstanceValid(sand)) sand.Free();
            mud.Free();
            player.Free();
        }
    }

    [TestCase]
    public void EnvironmentHooksAreStoryOnlyAndAbsentFromTheFighterSimulation() {
        string[] forbidden = {
            "EnvironmentGravityScale",
            "EnvironmentMoveMultiplier",
            "EnvironmentPlayerModifiers"
        };
        var offenders = new List<string>();
        int scanned = 0;
        foreach (string path in Directory.GetFiles("scripts/FighterSim", "*.cs", SearchOption.AllDirectories)) {
            scanned++;
            string source = File.ReadAllText(path);
            foreach (string token in forbidden) {
                if (source.Contains(token)) offenders.Add($"{path} -> {token}");
            }
        }

        AssertThat(scanned).OverrideFailureMessage(
            "The FighterSim directory walk found no files; the isolation scan is broken.").IsGreater(10);
        AssertThat(offenders.Count).OverrideFailureMessage(
            "Story-only environment hooks leaked into scripts/FighterSim/: " + string.Join(", ", offenders))
            .IsEqual(0);
    }

    // === PendulumAnchor ===

    [TestCase]
    public void PendulumCarriesItsLedgeOccupantAndHandsOffSwingMomentumOnRelease() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var anchor = new PendulumAnchor {
            AnchorID = "test.pendulum",
            AmplitudeDegrees = 45f,
            PeriodSeconds = 2f,
            ReleaseLaunchAssist = 1f
        };
        var ledge = new LedgeGrabPoint { Name = "LedgeGrabPoint", Position = new Vector2(0f, 320f) };
        ledge.AddChild(new Marker2D { Name = "HangAnchor" });
        anchor.AddChild(ledge);
        var player = CharacterFactory.CreateCharacter("einstein");
        Vector2 launched = Vector2.Zero;
        tree.Root.AddChild(anchor);
        tree.Root.AddChild(player);
        anchor.OccupantLaunched += (_, velocity) => launched = velocity;
        try {
            AssertThat(ledge.TryAcquire(player)).IsTrue();
            AssertThat(anchor.Occupant).IsEqual(player);

            for (int frame = 0; frame < 12; frame++) anchor.AdvanceSwing((float)Step);
            // The occupant rides the swing rather than staying where it grabbed.
            AssertFloat(player.GlobalPosition.DistanceTo(ledge.HangPosition)).IsLess(0.01f);
            AssertFloat(anchor.AnchorVelocity.Length()).IsGreater(1f);
            AssertFloat(Mathf.Abs(anchor.RotationDegrees)).IsGreater(0.5f);

            Vector2 anchorVelocity = anchor.AnchorVelocity;
            Vector2 velocityBefore = player.Velocity;
            ledge.Release(player);
            anchor.AdvanceSwing((float)Step);
            // PlayerController's ledge jump only writes Velocity.Y, so the anchor
            // supplies the horizontal hand-off itself (documented launch assist).
            AssertFloat(player.Velocity.X).IsNotEqual(velocityBefore.X);
            AssertFloat(launched.Length()).IsGreater(0f);
            AssertFloat(Mathf.Sign(launched.X)).IsEqual(Mathf.Sign(anchorVelocity.X));

            anchor.ReleaseLaunchAssist = 0f;
            AssertThat(anchor.ApplyReleaseLaunch(player)).IsEqual(Vector2.Zero);
        } finally {
            anchor.Free();
            player.Free();
        }
    }

    // === PathMovingPlatform ===

    [TestCase]
    public void PathPlatformPingPongsBetweenWaypointsWithEndpointWaits() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var platform = new PathMovingPlatform {
            PlatformID = "test.path_platform",
            Waypoints = new[] { Vector2.Zero, new Vector2(120f, 0f) },
            Speed = 120f,
            EndpointWaitSeconds = 0.25f,
            Mode = PathMovingPlatformMode.PingPong
        };
        var reached = new List<int>();
        tree.Root.AddChild(platform);
        platform.WaypointReached += index => reached.Add(index);
        try {
            AssertThat(platform.TargetWaypointIndex).IsEqual(1);
            platform.AdvancePath(0.5f);
            AssertThat(platform.Position).IsEqual(new Vector2(60f, 0f));
            AssertFloat(platform.PlatformVelocity.X).IsEqualApprox(120f, 0.001f);

            platform.AdvancePath(0.5f);
            AssertThat(platform.Position).IsEqual(new Vector2(120f, 0f));
            AssertThat(platform.LastWaypointIndex).IsEqual(1);
            AssertThat(platform.IsWaiting).IsTrue();
            AssertThat(platform.TargetWaypointIndex).IsEqual(0);

            // The endpoint wait holds the platform still before it reverses.
            platform.AdvancePath(0.2f);
            AssertThat(platform.Position).IsEqual(new Vector2(120f, 0f));
            platform.AdvancePath(0.2f);
            platform.AdvancePath(0.5f);
            AssertFloat(platform.Position.X).IsLess(120f);
            AssertThat(reached).ContainsExactly(1);
        } finally {
            platform.Free();
        }
    }

    // === TrapdoorPlatform ===

    [TestCase]
    public void TrapdoorWarnsBeforeDroppingCollisionAndRestoresOnClose() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var trapdoor = new TrapdoorPlatform {
            TrapdoorID = "test.trapdoor",
            AutoCycle = true,
            ClosedDurationSeconds = 0.2f,
            WarningShakeSeconds = 0.1f,
            OpenDurationSeconds = 0.2f
        };
        var shape = new CollisionShape2D {
            Name = "CollisionShape2D",
            Shape = new RectangleShape2D { Size = new Vector2(200f, 24f) }
        };
        var visual = new Polygon2D { Name = "Visual" };
        trapdoor.AddChild(shape);
        trapdoor.AddChild(visual);
        var states = new List<int>();
        tree.Root.AddChild(trapdoor);
        trapdoor.StateChanged += state => states.Add(state);
        try {
            AssertThat(trapdoor.State).IsEqual(TrapdoorState.Closed);
            AssertThat(shape.Disabled).IsFalse();

            trapdoor._PhysicsProcess(0.21);
            AssertThat(trapdoor.State).IsEqual(TrapdoorState.Warning);
            AssertThat(shape.Disabled).IsFalse();

            trapdoor._PhysicsProcess(0.11);
            AssertThat(trapdoor.State).IsEqual(TrapdoorState.Open);
            AssertThat(trapdoor.IsSolid).IsFalse();
            AssertThat(shape.Disabled).IsTrue();

            trapdoor.CaptureCheckpointState("checkpoint");
            trapdoor._PhysicsProcess(0.21);
            AssertThat(trapdoor.State).IsEqual(TrapdoorState.Closed);
            AssertThat(shape.Disabled).IsFalse();

            trapdoor.ApplyStoryRewind();
            AssertThat(trapdoor.State).IsEqual(TrapdoorState.Open);
            AssertThat(states).ContainsExactly(
                (int)TrapdoorState.Warning,
                (int)TrapdoorState.Open,
                (int)TrapdoorState.Closed,
                (int)TrapdoorState.Open);

            // External triggering works without the timer cycle.
            trapdoor.AutoCycle = false;
            trapdoor.Close();
            AssertThat(shape.Disabled).IsFalse();
            trapdoor.Open();
            AssertThat(shape.Disabled).IsTrue();
        } finally {
            trapdoor.Free();
        }
    }

    // === EscapeSequenceController ===

    [TestCase]
    public void EscapeFrontAdvancesCatchesWithoutKillingAndCompletesAtTheFinishLine() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var escape = new EscapeSequenceController {
            SequenceID = "test.escape",
            StartX = 0f,
            EndX = 1000f,
            FinishX = 1200f,
            AdvanceSpeed = 500f,
            CatchDamage = 25,
            CatchCooldownSeconds = 1f
        };
        var front = new Area2D { Name = "DamageFront" };
        front.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(320f, 1080f) } });
        escape.AddChild(front);
        var player = CharacterFactory.CreateCharacter("einstein");
        bool completed = false;
        int caughtEvents = 0;
        tree.Root.AddChild(escape);
        tree.Root.AddChild(player);
        escape.EscapeCompleted += () => completed = true;
        escape.PlayerCaught += (_, _) => caughtEvents++;
        try {
            AssertThat(escape.IsRunning).IsFalse();
            escape.AdvanceFront(1f);
            AssertFloat(escape.FrontX).IsEqualApprox(0f, 0.001f);

            escape.Begin();
            escape.AdvanceFront(1f);
            AssertFloat(escape.FrontX).IsEqualApprox(500f, 0.001f);
            AssertFloat(escape.RemainingSeconds).IsEqualApprox(1f, 0.001f);

            player.GlobalPosition = new Vector2(400f, 0f);
            int startingHP = player.CurrentHP;
            AssertThat(escape.CatchPlayer(player)).IsTrue();
            AssertThat(player.CurrentHP).IsEqual(startingHP - 25);
            // Never an instant kill: rewind must stay the meaningful failure state.
            AssertThat(player.CurrentHP).IsGreater(0);
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.Dead);
            AssertFloat(player.Velocity.X).IsLess(0f);
            // Catch cooldown prevents per-frame chain damage.
            AssertThat(escape.CatchPlayer(player)).IsFalse();
            escape.AdvanceFront(1.1f);
            AssertThat(escape.CatchPlayer(player)).IsTrue();
            AssertThat(caughtEvents).IsEqual(2);

            player.GlobalPosition = new Vector2(1100f, 0f);
            AssertThat(escape.NotifyPlayerReachedFinish(player)).IsFalse();
            player.GlobalPosition = new Vector2(1300f, 0f);
            AssertThat(escape.NotifyPlayerReachedFinish(player)).IsTrue();
            AssertThat(completed).IsTrue();
            AssertThat(escape.IsRunning).IsFalse();

            escape.ResetSequence();
            AssertFloat(escape.FrontX).IsEqualApprox(0f, 0.001f);
            AssertThat(escape.IsCompleted).IsFalse();
        } finally {
            escape.Free();
            player.Free();
        }
    }

    // === SequenceLock ===

    [TestCase]
    public void SequenceLockRequiresAuthoredOrderResetsOnMistakesAndSetsItsCondition() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var room = new Node2D { Name = "TombRoom" };
        var manager = new PuzzleManager {
            Name = "PuzzleManager",
            PuzzleID = "test.hieroglyphs",
            RequiredConditionIDs = new[] { "sequence_complete" },
            PersistCompletionToSave = false
        };
        var sequenceLock = new SequenceLock {
            Name = "SequenceLock",
            LockID = "test.sequence_lock",
            PuzzleManagerPath = "../PuzzleManager",
            ConditionID = "sequence_complete"
        };
        // Deliberately added out of order: the lock sorts by OrderIndex.
        var third = NewGlyph("Third", "glyph_c", 2);
        var first = NewGlyph("First", "glyph_a", 0);
        var second = NewGlyph("Second", "glyph_b", 1);
        sequenceLock.AddChild(third);
        sequenceLock.AddChild(first);
        sequenceLock.AddChild(second);
        room.AddChild(manager);
        room.AddChild(sequenceLock);
        var player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(room);
        tree.Root.AddChild(player);
        try {
            AssertThat(sequenceLock.GlyphCount).IsEqual(3);
            AssertThat(sequenceLock.ExpectedGlyph).IsEqual(first);

            AssertThat(sequenceLock.Activate(first)).IsTrue();
            AssertThat(first.IsLit).IsTrue();
            AssertThat(sequenceLock.Progress).IsEqual(1);

            // A wrong pick unlights everything and restarts.
            AssertThat(sequenceLock.Activate(third)).IsFalse();
            AssertThat(sequenceLock.Progress).IsEqual(0);
            AssertThat(first.IsLit).IsFalse();

            AssertThat(sequenceLock.Activate(first)).IsTrue();
            AssertThat(sequenceLock.Activate(second)).IsTrue();
            AssertThat(sequenceLock.IsCompleted).IsFalse();
            AssertThat(sequenceLock.Activate(third)).IsTrue();
            AssertThat(sequenceLock.IsCompleted).IsTrue();
            AssertThat(manager.IsConditionSatisfied("sequence_complete")).IsTrue();
            AssertThat(manager.IsCompleted).IsTrue();

            // A completed lock refuses further interaction.
            AssertThat(first.CanInteract(player)).IsFalse();
            AssertThat(sequenceLock.Activate(first)).IsFalse();
        } finally {
            room.Free();
            player.Free();
        }
    }

    // === RescuableNPC ===

    [TestCase]
    public void RescuableNpcFlashesFreedThenDespawnsAndSetsItsCondition() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var room = new Node2D { Name = "CellBlock" };
        var manager = new PuzzleManager {
            Name = "PuzzleManager",
            PuzzleID = "test.prisoners",
            RequiredConditionIDs = new[] { "prisoner_freed" },
            PersistCompletionToSave = false
        };
        var npc = new RescuableNPC {
            Name = "Prisoner",
            NpcID = "test.prisoner_a",
            PuzzleManagerPath = "../PuzzleManager",
            ConditionID = "prisoner_freed",
            FreedFlashSeconds = 0.2f
        };
        npc.AddChild(new Polygon2D { Name = "Visual" });
        room.AddChild(manager);
        room.AddChild(npc);
        var player = CharacterFactory.CreateCharacter("einstein");
        var rescued = new List<string>();
        tree.Root.AddChild(room);
        tree.Root.AddChild(player);
        npc.Rescued += id => rescued.Add(id);
        try {
            AssertThat(npc.CanInteract(player)).IsTrue();
            npc.Interact(player);
            AssertThat(npc.IsRescued).IsTrue();
            AssertThat(npc.IsDespawned).IsFalse();
            AssertThat(npc.Visible).IsTrue();
            AssertThat(rescued).ContainsExactly("test.prisoner_a");
            AssertThat(manager.IsConditionSatisfied("prisoner_freed")).IsTrue();

            npc._PhysicsProcess(0.1);
            AssertThat(npc.IsDespawned).IsFalse();
            npc._PhysicsProcess(0.15);
            AssertThat(npc.IsDespawned).IsTrue();
            AssertThat(npc.Visible).IsFalse();
            AssertThat(npc.CanInteract(player)).IsFalse();
            AssertThat(npc.Rescue()).IsFalse();
        } finally {
            room.Free();
            player.Free();
        }
    }

    // === ShieldGeneratorTower ===

    [TestCase]
    public void ShieldGeneratorKeepsBarriersSolidUntilDestroyedAndRestoresThemOnRewind() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PackedScene towerScene = ResourceLoader.Load<PackedScene>(
            "res://scenes/templates/ShieldGeneratorTowerTemplate.tscn");
        var tower = towerScene.Instantiate<ShieldGeneratorTower>();
        var destroyed = new List<string>();
        tree.Root.AddChild(tower);
        tower.TowerDestroyed += id => destroyed.Add(id);
        try {
            AssertThat(tower.ActiveBarrierCount).IsEqual(1);
            var barrier = tower.GetNode<ForcefieldBarrier>("Barrier");
            AssertThat(barrier.IsActive).IsTrue();
            AssertThat(barrier.GetNode<CollisionShape2D>("CollisionShape2D").Disabled).IsFalse();
            AssertThat(barrier.CollisionLayer).IsEqual(CollisionLayers.Environment);

            tower.CaptureCheckpointState("checkpoint");
            tower.TakeEnvironmentDamage(60f);
            AssertThat(tower.IsDestroyed).IsFalse();
            AssertThat(barrier.IsActive).IsTrue();

            tower.TakeEnvironmentDamage(60f);
            AssertThat(tower.IsDestroyed).IsTrue();
            AssertThat(barrier.IsActive).IsFalse();
            AssertThat(barrier.GetNode<CollisionShape2D>("CollisionShape2D").Disabled).IsTrue();
            AssertThat(tower.ActiveBarrierCount).IsEqual(0);
            AssertThat(destroyed).ContainsExactly("placeholder_shield_generator");

            tower.ApplyStoryRewind();
            AssertThat(tower.IsDestroyed).IsFalse();
            AssertThat(tower.ActiveBarrierCount).IsEqual(1);
        } finally {
            tower.Free();
        }
    }

    // === Template sweep ===

    /// <summary>
    /// Every visible string in a shared template must be a translation key, not
    /// English. Six templates shipped literals ("DEEP SAND", "GRAVITY FIELD", ...)
    /// until the Wave B integration; Level 8 was patching one of them from level
    /// code because the template could not be fixed mid-wave.
    /// </summary>
    [TestCase]
    public void NoSharedToolkitTemplateShipsHardcodedEnglishSignage() {
        TranslationServer.SetLocale("en");
        using DirAccess directory = DirAccess.Open("res://scenes/templates");
        AssertObject(directory).IsNotNull();

        var offenders = new List<string>();
        int scannedLabels = 0;
        foreach (string file in directory.GetFiles()) {
            if (!file.EndsWith(".tscn")) continue;
            string path = $"res://scenes/templates/{file}";
            using Godot.FileAccess handle = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (handle == null) continue;
            string[] lines = handle.GetAsText().Split('\n');
            for (int index = 0; index < lines.Length; index++) {
                string line = lines[index].Trim();
                if (!line.StartsWith("text = \"") || !line.EndsWith("\"")) continue;
                scannedLabels++;
                string value = line[8..^1];
                if (TranslationServer.Translate(value) == value) {
                    offenders.Add($"{path}:{index + 1}  text = \"{value}\"");
                }
            }
        }

        // A scan that reached nothing would pass vacuously.
        AssertThat(scannedLabels).OverrideFailureMessage(
            $"Template scan found only {scannedLabels} label strings; the walk is broken.")
            .IsGreater(8);
        AssertThat(offenders.Count).OverrideFailureMessage(
            "Shared toolkit templates must carry a translation key in `text`, with the " +
            "English in localization/en.csv (the component resolves it through " +
            "ToolkitLabel and a level can retarget it with LabelKey). Offenders:\n  " +
            string.Join("\n  ", offenders)).IsEqual(0);
    }

    /// <summary>
    /// The label key is an export on the template ROOT, so a level can retarget the
    /// signage from its own .tscn without the fragile `index=` block that overriding
    /// a child of an instanced scene needs. Level 8's sand drifts rely on this.
    /// </summary>
    [TestCase]
    public void AToolkitTemplateSignCanBeRetargetedFromTheInstancesRootExport() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        TranslationServer.SetLocale("en");
        var zone = ResourceLoader.Load<PackedScene>(
            "res://scenes/templates/MovementDampenerZoneTemplate.tscn").Instantiate<MovementDampenerZone>();
        zone.LabelKey = "egypt_deep_sand";
        tree.Root.AddChild(zone);
        try {
            var label = zone.GetNode<Label>("Label");
            AssertString(label.Text).IsEqual(TranslationServer.Translate("egypt_deep_sand"));
            AssertString(label.Text).IsNotEqual("egypt_deep_sand");
        } finally {
            zone.Free();
        }
    }

    [TestCase]
    public void AllEraMechanicPlaceholderTemplatesInstantiate() {
        string[] paths = {
            "res://scenes/templates/SearchlightZoneTemplate.tscn",
            "res://scenes/templates/RisingWaterZoneTemplate.tscn",
            "res://scenes/templates/GravityFieldZoneTemplate.tscn",
            "res://scenes/templates/PendulumAnchorTemplate.tscn",
            "res://scenes/templates/PathMovingPlatformTemplate.tscn",
            "res://scenes/templates/TrapdoorPlatformTemplate.tscn",
            "res://scenes/templates/EscapeSequenceControllerTemplate.tscn",
            "res://scenes/templates/MovementDampenerZoneTemplate.tscn",
            "res://scenes/templates/SequenceLockTemplate.tscn",
            "res://scenes/templates/RescuableNPCTemplate.tscn",
            "res://scenes/templates/ShieldGeneratorTowerTemplate.tscn"
        };
        foreach (string path in paths) {
            PackedScene scene = ResourceLoader.Load<PackedScene>(path);
            AssertObject(scene).OverrideFailureMessage($"{path} did not load.").IsNotNull();
            Node instance = scene.Instantiate();
            AssertObject(instance).OverrideFailureMessage($"{path} did not instantiate.").IsNotNull();
            instance.Free();
        }
    }

    [TestCase]
    public void EraMechanicTemplatesUseTheDocumentedRootTypesAndTriggerLayers() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var searchlight = ResourceLoader.Load<PackedScene>("res://scenes/templates/SearchlightZoneTemplate.tscn")
            .Instantiate<SearchlightZone>();
        var water = ResourceLoader.Load<PackedScene>("res://scenes/templates/RisingWaterZoneTemplate.tscn")
            .Instantiate<RisingWaterZone>();
        var gravity = ResourceLoader.Load<PackedScene>("res://scenes/templates/GravityFieldZoneTemplate.tscn")
            .Instantiate<GravityFieldZone>();
        var sand = ResourceLoader.Load<PackedScene>("res://scenes/templates/MovementDampenerZoneTemplate.tscn")
            .Instantiate<MovementDampenerZone>();
        var pendulum = ResourceLoader.Load<PackedScene>("res://scenes/templates/PendulumAnchorTemplate.tscn")
            .Instantiate<PendulumAnchor>();
        var sequence = ResourceLoader.Load<PackedScene>("res://scenes/templates/SequenceLockTemplate.tscn")
            .Instantiate<SequenceLock>();
        var npc = ResourceLoader.Load<PackedScene>("res://scenes/templates/RescuableNPCTemplate.tscn")
            .Instantiate<RescuableNPC>();
        tree.Root.AddChild(searchlight);
        tree.Root.AddChild(water);
        tree.Root.AddChild(gravity);
        tree.Root.AddChild(sand);
        tree.Root.AddChild(pendulum);
        tree.Root.AddChild(sequence);
        tree.Root.AddChild(npc);
        try {
            foreach (Area2D zone in new Area2D[] { searchlight, water, gravity, sand }) {
                AssertThat(zone.CollisionLayer).OverrideFailureMessage(
                    $"{zone.Name} must sit on the Trigger layer.").IsEqual(CollisionLayers.Trigger);
                AssertThat(zone.CollisionMask).OverrideFailureMessage(
                    $"{zone.Name} must only detect the Player layer.").IsEqual(CollisionLayers.Player);
            }
            AssertThat(water.StepCount).IsEqual(4);
            AssertObject(pendulum.Ledge).IsNotNull();
            AssertThat(pendulum.Ledge.IsInGroup("Ledge")).IsTrue();
            AssertThat(sequence.GlyphCount).IsEqual(3);
            AssertThat(npc.PromptKey).IsEqual("interaction_rescue");
            AssertThat(sequence.Glyphs[0].PromptKey).IsEqual("interaction_press_glyph");
        } finally {
            searchlight.Free();
            water.Free();
            gravity.Free();
            sand.Free();
            pendulum.Free();
            sequence.Free();
            npc.Free();
        }
    }

    [TestCase]
    public void EraMechanicInteractionPromptKeysAreLocalized() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        TranslationServer.SetLocale("en");
        foreach (string key in new[] { "interaction_rescue", "interaction_press_glyph" }) {
            AssertThat(keys.Contains(key)).OverrideFailureMessage(
                $"'{key}' is missing from localization/en.csv.").IsTrue();
            // The compiled en.en.translation must be in step with the CSV, otherwise
            // Node.Tr() renders the raw key (AGENTS.md localization notes).
            AssertString(TranslationServer.Translate(key).ToString()).OverrideFailureMessage(
                $"'{key}' does not resolve through localization/en.en.translation. Re-import localization/en.csv.")
                .IsNotEqual(key);
        }
    }

    private static SequenceGlyph NewGlyph(string nodeName, string glyphID, int orderIndex) {
        var glyph = new SequenceGlyph {
            Name = nodeName,
            GlyphID = glyphID,
            OrderIndex = orderIndex,
            SequenceLockPath = "..",
            VisualPath = "Visual"
        };
        glyph.AddChild(new Polygon2D { Name = "Visual" });
        return glyph;
    }
}
