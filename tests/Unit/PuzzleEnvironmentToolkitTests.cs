using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
[RequireGodotRuntime]
public class PuzzleEnvironmentToolkitTests {
    [TestCase]
    public void PuzzleManagerRequiresPrerequisitesAndConditionsAndRestoresCheckpointState() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node { Name = "PuzzleRoom" };
        var prerequisite = new PuzzleManager {
            Name = "Prerequisite",
            PuzzleID = "test.prerequisite",
            PersistCompletionToSave = false
        };
        var dependent = new PuzzleManager {
            Name = "Dependent",
            PuzzleID = "test.dependent",
            PrerequisitePuzzleIDs = new[] { "test.prerequisite" },
            RequiredConditionIDs = new[] { "switch_a", "switch_b" },
            PersistCompletionToSave = false
        };
        parent.AddChild(prerequisite);
        parent.AddChild(dependent);
        tree.Root.AddChild(parent);
        try {
            dependent.SetCondition("switch_a", true);
            dependent.SetCondition("switch_b", true);
            AssertThat(dependent.IsCompleted).IsFalse();

            AssertThat(prerequisite.TryComplete()).IsTrue();
            AssertThat(dependent.TryComplete()).IsTrue();
            dependent.CaptureCheckpointState("checkpoint_a");
            dependent.ResetPuzzle();
            AssertThat(dependent.IsCompleted).IsFalse();
            dependent.ApplyStoryRewind();
            AssertThat(dependent.IsCompleted).IsTrue();
            AssertThat(dependent.IsConditionSatisfied("switch_a")).IsTrue();
            AssertThat(dependent.IsConditionSatisfied("switch_b")).IsTrue();
        } finally {
            parent.Free();
        }
    }

    [TestCase]
    public void LeverRotatesPlatformExactlyNinetyDegreesAndBothRestoreCheckpointState() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node2D { Name = "LeverRoom" };
        var platform = new RotatingPlatform { Name = "Platform" };
        var lever = new PuzzleLever {
            Name = "Lever",
            LeverID = "test.lever",
            RotationTargetPaths = new Godot.Collections.Array<NodePath> { "../Platform" }
        };
        parent.AddChild(platform);
        parent.AddChild(lever);
        tree.Root.AddChild(parent);
        try {
            lever.Toggle();
            AssertThat(lever.IsOn).IsTrue();
            AssertThat(platform.QuarterTurns).IsEqual(1);
            AssertThat(platform.RotationDegrees).IsEqualApprox(90f, 0.001f);

            lever.CaptureCheckpointState("checkpoint");
            platform.CaptureCheckpointState("checkpoint");
            lever.Toggle();
            AssertThat(platform.QuarterTurns).IsEqual(0);
            lever.ApplyStoryRewind();
            platform.ApplyStoryRewind();
            AssertThat(lever.IsOn).IsTrue();
            AssertThat(platform.QuarterTurns).IsEqual(1);
        } finally {
            parent.Free();
        }
    }

    [TestCase]
    public void PressurePlatesCountMovableWeightAndPlayerLoads() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var plate = new PressurePlate { RequiredWeight = 3f, PlayerWeight = 1f };
        var weight = new WeightedObject { WeightUnits = 2f };
        var player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(plate);
        tree.Root.AddChild(weight);
        tree.Root.AddChild(player);
        try {
            plate.RegisterBody(weight);
            AssertThat(plate.CurrentWeight).IsEqualApprox(2f, 0.001f);
            AssertThat(plate.IsPressed).IsFalse();
            plate.RegisterBody(player);
            AssertThat(plate.CurrentWeight).IsEqualApprox(3f, 0.001f);
            AssertThat(plate.IsPressed).IsTrue();
            plate.UnregisterBody(weight);
            AssertThat(plate.IsPressed).IsFalse();
        } finally {
            plate.Free();
            weight.Free();
            player.Free();
        }
    }

    [TestCase]
    public void BeamRoutingTracksCoilOrientationAndMultipleIndependentSources() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node2D { Name = "PowerRoom" };
        var receiver = new BeamReceiver { Name = "Receiver" };
        var coil = new ConductiveCoil {
            Name = "Coil",
            EastOutputPaths = new Godot.Collections.Array<NodePath> { "../Receiver" }
        };
        var emitter = new BeamEmitter {
            Name = "Emitter",
            OutputPaths = new Godot.Collections.Array<NodePath> { "../Coil" }
        };
        var redundantEmitter = new BeamEmitter {
            Name = "RedundantEmitter",
            OutputPaths = new Godot.Collections.Array<NodePath> { "../Receiver" }
        };
        parent.AddChild(receiver);
        parent.AddChild(coil);
        parent.AddChild(emitter);
        parent.AddChild(redundantEmitter);
        tree.Root.AddChild(parent);
        try {
            AssertThat(coil.IsPowered).IsTrue();
            AssertThat(receiver.IsPowered).IsTrue();
            redundantEmitter.SetEnabled(false);
            AssertThat(receiver.IsPowered).IsTrue();
            coil.RotateQuarterTurn(1);
            AssertThat(receiver.IsPowered).IsFalse();
        } finally {
            parent.Free();
        }
    }

    [TestCase]
    public void ExtractorUsesHundredHPHazardDrainDamagedStateAndBalancedDustReward() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PackedScene extractorScene = ResourceLoader.Load<PackedScene>("res://scenes/templates/ChronalExtractorTemplate.tscn");
        var extractor = extractorScene.Instantiate<ChronalExtractor>();
        var player = CharacterFactory.CreateCharacter("einstein");
        int dustAwarded = 0;
        void OnDust(int amount) => dustAwarded += amount;
        tree.Root.AddChild(extractor);
        tree.Root.AddChild(player);
        EventBus.Instance.OnChronalDustCollected += OnDust;
        try {
            player.RestoreStoryCheckpoint(Vector2.Zero, player.MaximumHP, 50f);
            int startingHP = player.CurrentHP;
            extractor.ApplyDischargeToPlayer(player);
            AssertThat(player.CurrentHP).IsEqual(startingHP - extractor.HazardDamage);
            AssertThat(player.CurrentUltimateMeter).IsEqualApprox(30f, 0.001f);

            extractor.TakeEnvironmentDamage(60f);
            AssertThat(extractor.CurrentHP).IsEqual(40);
            AssertThat(extractor.VisualState).IsEqual(ChronalExtractorVisualState.Damaged);
            extractor.TakeEnvironmentDamage(40f);
            AssertThat(extractor.IsDestroyed).IsTrue();
            AssertThat(extractor.VisualState).IsEqual(ChronalExtractorVisualState.Destroyed);
            // 15 dust per the Package 3 economy balance pass (docs/DUST_ECONOMY.md Section 1).
            AssertThat(dustAwarded).IsEqual(15);
        } finally {
            EventBus.Instance.OnChronalDustCollected -= OnDust;
            extractor.Free();
            player.Free();
        }
    }

    [TestCase]
    public void DestructibleBlocksUseOneToThreeHitContractAndChestsOpenOnce() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PackedScene blockScene = ResourceLoader.Load<PackedScene>("res://scenes/templates/DestructibleBlockTemplate.tscn");
        PackedScene chestScene = ResourceLoader.Load<PackedScene>("res://scenes/templates/TreasureChestTemplate.tscn");
        var block = blockScene.Instantiate<DestructibleBlock>();
        var chest = chestScene.Instantiate<TreasureChest>();
        var player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(block);
        tree.Root.AddChild(chest);
        tree.Root.AddChild(player);
        try {
            block.TakeEnvironmentDamage(1f);
            block.TakeEnvironmentDamage(100f);
            AssertThat(block.IsDestroyed).IsFalse();
            block.TakeEnvironmentDamage(1f);
            AssertThat(block.IsDestroyed).IsTrue();

            InteractionArea interaction = chest.GetNode<InteractionArea>("InteractionArea");
            AssertThat(interaction.TryInteract(player)).IsTrue();
            AssertThat(chest.IsOpened).IsTrue();
            AssertThat(interaction.TryInteract(player)).IsFalse();
        } finally {
            block.Free();
            chest.Free();
            player.Free();
        }
    }

    [TestCase]
    public void RoomTransitionUpdatesCameraBoundsAndActivatesEncounter() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node2D { Name = "Level" };
        var camera = new StoryCameraConfiner { Name = "Camera" };
        var encounter = new Node2D { Name = "Encounter" };
        var trigger = new RoomTransitionTrigger {
            Name = "Trigger",
            RoomID = "room_2",
            CameraBounds = new Rect2(1920, 0, 1920, 1080),
            CameraPath = "../Camera",
            EncounterRootPath = "../Encounter"
        };
        var player = CharacterFactory.CreateCharacter("einstein");
        parent.AddChild(camera);
        parent.AddChild(encounter);
        parent.AddChild(trigger);
        parent.AddChild(player);
        tree.Root.AddChild(parent);
        try {
            AssertThat(encounter.ProcessMode).IsEqual(Node.ProcessModeEnum.Disabled);
            AssertThat(trigger.ActivateRoom(player)).IsTrue();
            AssertThat(camera.ActiveBounds).IsEqual(new Rect2(1920, 0, 1920, 1080));
            AssertThat(encounter.ProcessMode).IsEqual(Node.ProcessModeEnum.Inherit);
            AssertThat(trigger.ActivateRoom(player)).IsFalse();
        } finally {
            parent.Free();
        }
    }

    [TestCase]
    public void CrumblingAndCyclicHazardsFollowConfiguredStateTimingAndSingleHitRules() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var crumble = new CrumblingPlatform();
        var hazard = new StoryCyclicHazard { Damage = 10 };
        var player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(crumble);
        tree.Root.AddChild(hazard);
        tree.Root.AddChild(player);
        try {
            AssertThat(crumble.TriggerCollapse()).IsTrue();
            crumble._PhysicsProcess(0.81);
            AssertThat(crumble.State).IsEqual(CrumblingPlatformState.Collapsing);
            crumble._PhysicsProcess(1.21);
            AssertThat(crumble.State).IsEqual(CrumblingPlatformState.Disabled);
            crumble._PhysicsProcess(5.01);
            AssertThat(crumble.State).IsEqual(CrumblingPlatformState.Solid);

            hazard.ForcePhase(HazardPhase.Active, 1f);
            int startingHP = player.CurrentHP;
            AssertThat(hazard.ApplyToPlayer(player)).IsTrue();
            AssertThat(hazard.ApplyToPlayer(player)).IsFalse();
            AssertThat(player.CurrentHP).IsEqual(startingHP - 10);
        } finally {
            crumble.Free();
            hazard.Free();
            player.Free();
        }
    }

    [TestCase]
    public void ChronalRiftAppliesHalfSpeedAndSnapsToThreeSecondHistoryWithDamage() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var player = CharacterFactory.CreateCharacter("einstein");
        var rift = new ChronalRiftZone { SnapDelay = 2f, HistoryFramesAgo = 180, SnapDamage = 15 };
        tree.Root.AddChild(player);
        tree.Root.AddChild(rift);
        try {
            TemporalPositionHistory history = player.GetNode<TemporalPositionHistory>("TemporalPositionHistory");
            for (int frame = 0; frame <= 180; frame++) history.Record(new Vector2(frame, 20f));
            player.GlobalPosition = new Vector2(999f, 20f);
            int startingHP = player.CurrentHP;
            rift.AddPlayer(player);
            AssertThat(player.StatusMovementMultiplier).IsEqualApprox(0.5f, 0.001f);
            rift._PhysicsProcess(2.01);
            AssertThat(player.GlobalPosition).IsEqual(new Vector2(0f, 20f));
            AssertThat(player.CurrentHP).IsEqual(startingHP - 15);
            rift.RemovePlayer(player);
            AssertThat(player.StatusMovementMultiplier).IsEqualApprox(1f, 0.001f);
        } finally {
            rift.Free();
            player.Free();
        }
    }

    [TestCase]
    public void AllPuzzleEnvironmentPlaceholderTemplatesInstantiate() {
        string[] paths = {
            "res://scenes/templates/RotatingPlatformTemplate.tscn",
            "res://scenes/templates/PuzzleLeverTemplate.tscn",
            "res://scenes/templates/MovableWeightTemplate.tscn",
            "res://scenes/templates/PressurePlateTemplate.tscn",
            "res://scenes/templates/CounterweightTemplate.tscn",
            "res://scenes/templates/BeamEmitterTemplate.tscn",
            "res://scenes/templates/ConductiveCoilTemplate.tscn",
            "res://scenes/templates/BeamReceiverTemplate.tscn",
            "res://scenes/templates/DestructibleBlockTemplate.tscn",
            "res://scenes/templates/TreasureChestTemplate.tscn",
            "res://scenes/templates/ChronalExtractorTemplate.tscn",
            "res://scenes/templates/RoomTransitionTemplate.tscn",
            "res://scenes/templates/CrumblingPlatformTemplate.tscn",
            "res://scenes/templates/CyclicHazardTemplate.tscn",
            "res://scenes/templates/ChronalRiftZoneTemplate.tscn"
        };
        foreach (string path in paths) {
            PackedScene scene = ResourceLoader.Load<PackedScene>(path);
            AssertObject(scene).IsNotNull();
            Node instance = scene.Instantiate();
            AssertObject(instance).IsNotNull();
            instance.Free();
        }
    }
}
