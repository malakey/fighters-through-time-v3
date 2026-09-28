using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W8 (GAP-08 / V01b / V01c): the Reset Puzzle station, owned puzzle
/// props and the plate allowlist. Every case builds a scratch room under the root
/// and frees it (and its hero) in <c>finally</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PuzzleResetStationTests {

    private const string PuzzleID = "test.reset_room";
    private static readonly Vector2 HomeA = new(400f, 300f);
    private static readonly Vector2 HomeB = new(700f, 300f);
    private static readonly Vector2 FarAway = new(-20000f, -20000f);

    private sealed class Room {
        public Node2D Root;
        public PuzzleManager Puzzle;
        public PressurePlate Plate;
        public WeightedObject PropA;
        public WeightedObject PropB;
        public PuzzleResetStation Station;
        public PlayerController Player;
    }

    private static WeightedObject NewProp(string name, string propID, string owner, Vector2 position, float weight) {
        var prop = new WeightedObject {
            Name = name,
            PropID = propID,
            PuzzleOwnerID = owner,
            WeightUnits = weight,
            Position = position,
            GravityScale = 0f
        };
        prop.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 28f } });
        return prop;
    }

    private static PressurePlate NewPlate(string name, Vector2 position, params string[] allowlist) {
        var plate = new PressurePlate {
            Name = name,
            RequiredWeight = 3f,
            PlayerWeight = 1f,
            Position = position,
            PuzzleManagerPath = "../Puzzle",
            ConditionID = "plate_loaded",
            AcceptedPuzzleOwnerIDs = allowlist
        };
        plate.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(128f, 24f) } });
        return plate;
    }

    private static Room BuildRoom() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var room = new Room { Root = new Node2D { Name = "ResetRoom" } };
        room.Puzzle = new PuzzleManager {
            Name = "Puzzle",
            PuzzleID = PuzzleID,
            RequiredConditionIDs = new[] { "plate_loaded", "never_satisfied" },
            PersistCompletionToSave = false
        };
        room.Root.AddChild(room.Puzzle);
        room.Plate = NewPlate("Plate", HomeA, PuzzleID);
        room.Root.AddChild(room.Plate);
        room.PropA = NewProp("PropA", "test.prop_a", PuzzleID, HomeA, 3f);
        room.PropB = NewProp("PropB", "test.prop_b", PuzzleID, HomeB, 2f);
        room.Root.AddChild(room.PropA);
        room.Root.AddChild(room.PropB);
        room.Station = PuzzleResetStation.Create("test.reset_room.reset", "../Puzzle", new Vector2(100f, 400f));
        room.Root.AddChild(room.Station);
        tree.Root.AddChild(room.Root);

        room.Player = CharacterFactory.CreateCharacter("einstein");
        room.Player.GlobalPosition = new Vector2(100f, 400f);
        tree.Root.AddChild(room.Player);

        room.Plate.RegisterBody(room.PropA);
        room.Station.RefreshPropRegistry();
        return room;
    }

    private static void Free(Room room) {
        if (room.Player != null && GodotObject.IsInstanceValid(room.Player)) room.Player.Free();
        if (room.Root != null && GodotObject.IsInstanceValid(room.Root)) room.Root.Free();
    }

    [TestCase]
    public void ResetReturnsAnUnsolvedPuzzlesPropsHomeAndRecomputesThePlate() {
        Room room = BuildRoom();
        try {
            AssertThat(room.Plate.IsPressed).IsTrue();
            AssertThat(room.Station.RegisteredPropIDs.Count).IsEqual(2);

            room.PropA.GlobalPosition = new Vector2(900f, 300f);
            room.PropA.LinearVelocity = new Vector2(250f, -40f);
            room.Plate.UnregisterBody(room.PropA);
            room.PropB.GlobalPosition = new Vector2(1100f, 300f);
            AssertThat(room.Plate.IsPressed).IsFalse();

            AssertThat(room.Station.CanInteract(room.Player)).IsTrue();
            AssertThat(room.Station.TryReset(room.Player)).IsEqual(PuzzleResetResult.Reset);

            AssertThat(room.PropA.GlobalPosition.DistanceTo(HomeA)).IsLess(0.01f);
            AssertThat(room.PropB.GlobalPosition.DistanceTo(HomeB)).IsLess(0.01f);
            AssertThat(room.PropA.LinearVelocity.Length()).IsLess(0.001f);
            AssertThat(room.PropA.ResetGeneration).IsEqual(1);
            AssertThat(room.Plate.IsCounting(room.PropA)).IsTrue();
            AssertThat(room.Plate.IsCounting(room.PropB)).IsFalse();
            AssertThat(room.Plate.IsPressed).IsTrue();
            AssertThat(room.Puzzle.IsCompleted).IsFalse();
            AssertThat(room.Puzzle.ArrangementGeneration).IsEqual(1);
            AssertThat(room.Station.ResetCount).IsEqual(1);

            // The same live instances were restored; nothing was duplicated.
            AssertThat(CountProps(room, "test.prop_a")).IsEqual(1);
            AssertThat(CountProps(room, "test.prop_b")).IsEqual(1);
        } finally {
            Free(room);
        }
    }

    [TestCase]
    public void ASecondActivationInTheSameTickIsRefusedAndARepeatedDeliberateResetIsANoOpSuccess() {
        Room room = BuildRoom();
        try {
            AssertThat(room.Station.TryReset(room.Player)).IsEqual(PuzzleResetResult.Reset);
            AssertThat(room.Station.TryReset(room.Player)).IsEqual(PuzzleResetResult.DuplicateActivation);
            AssertThat(room.Station.ResetCount).IsEqual(1);

            room.Station.ClearActivationLatchForTest();
            AssertThat(room.Station.TryReset(room.Player)).IsEqual(PuzzleResetResult.Reset);
            AssertThat(room.PropA.GlobalPosition.DistanceTo(HomeA)).IsLess(0.01f);
            AssertThat(room.Plate.CurrentWeight).IsEqualApprox(3f, 0.001f);
        } finally {
            Free(room);
        }
    }

    [TestCase]
    public void ASolvedPuzzleIsUntouchedAndTheStationGoesInert() {
        Room room = BuildRoom();
        try {
            room.Puzzle.RequiredConditionIDs = new[] { "plate_loaded" };
            AssertThat(room.Puzzle.IsConditionSatisfied("plate_loaded")).IsTrue();
            AssertThat(room.Puzzle.TryComplete()).IsTrue();

            room.PropB.GlobalPosition = new Vector2(1500f, 300f);
            AssertThat(room.Station.CanInteract(room.Player)).IsFalse();
            AssertThat(room.Station.TryReset(room.Player)).IsEqual(PuzzleResetResult.Solved);

            AssertThat(room.PropB.GlobalPosition.DistanceTo(new Vector2(1500f, 300f))).IsLess(0.01f);
            AssertThat(room.Puzzle.IsCompleted).IsTrue();
            AssertThat(room.Puzzle.ArrangementGeneration).IsEqual(0);
            AssertThat(room.Station.ResetCount).IsEqual(0);
        } finally {
            Free(room);
        }
    }

    [TestCase]
    public void AnObstructedHomeRefusesTheWholeResetAndNeverSpawnsInsideThePlayer() {
        Room room = BuildRoom();
        try {
            var displacedA = new Vector2(900f, 300f);
            var displacedB = new Vector2(1100f, 300f);
            room.PropA.GlobalPosition = displacedA;
            room.PropB.GlobalPosition = displacedB;
            // The hero stands in prop B's restoration volume (body spans feet-64..feet).
            room.Player.GlobalPosition = HomeB + new Vector2(0f, 28f);

            AssertThat(room.Station.IsAnyRestorationVolumeObstructed()).IsTrue();
            AssertThat(room.Station.TryReset(room.Player)).IsEqual(PuzzleResetResult.Obstructed);

            // Nothing moved: no partial reset, no displaced hero.
            AssertThat(room.PropA.GlobalPosition.DistanceTo(displacedA)).IsLess(0.01f);
            AssertThat(room.PropB.GlobalPosition.DistanceTo(displacedB)).IsLess(0.01f);
            AssertThat(room.Player.GlobalPosition.DistanceTo(HomeB + new Vector2(0f, 28f))).IsLess(0.01f);
            AssertThat(room.Puzzle.ArrangementGeneration).IsEqual(0);

            // Step clear and retry.
            room.Player.GlobalPosition = new Vector2(100f, 400f);
            AssertThat(room.Station.TryReset(room.Player)).IsEqual(PuzzleResetResult.Reset);
            AssertThat(room.PropB.GlobalPosition.DistanceTo(HomeB)).IsLess(0.01f);
        } finally {
            Free(room);
        }
    }

    [TestCase]
    public void AForeignPuzzleWeightInAHomeAlsoObstructs() {
        Room room = BuildRoom();
        var stray = NewProp("Stray", "other.prop", "other.puzzle", HomeB, 1f);
        try {
            room.Root.AddChild(stray);
            room.PropB.GlobalPosition = new Vector2(1100f, 300f);
            AssertThat(room.Station.TryReset(room.Player)).IsEqual(PuzzleResetResult.Obstructed);
            AssertThat(stray.GlobalPosition.DistanceTo(HomeB)).IsLess(0.01f);
        } finally {
            Free(room);
        }
    }

    [TestCase]
    public void ThePlateAllowlistRejectsCrossPuzzleAndUnownedProps() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node2D { Name = "AllowlistRoom" };
        var owned = NewPlate("Owned", Vector2.Zero, PuzzleID);
        var open = NewPlate("Open", new Vector2(500f, 0f));
        var mine = NewProp("Mine", "test.mine", PuzzleID, new Vector2(0f, 0f), 2f);
        var foreign = NewProp("Foreign", "other.prop", "other.puzzle", new Vector2(0f, 200f), 2f);
        var unowned = NewProp("Unowned", "", "", new Vector2(0f, 400f), 2f);
        root.AddChild(owned);
        root.AddChild(open);
        root.AddChild(mine);
        root.AddChild(foreign);
        root.AddChild(unowned);
        tree.Root.AddChild(root);
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            owned.RegisterBody(foreign);
            owned.RegisterBody(unowned);
            AssertThat(owned.CurrentWeight).IsEqualApprox(0f, 0.001f);
            owned.RegisterBody(mine);
            AssertThat(owned.CurrentWeight).IsEqualApprox(2f, 0.001f);
            // Double contact (a second callback for the same body) counts once.
            owned.RegisterBody(mine);
            AssertThat(owned.CurrentWeight).IsEqualApprox(2f, 0.001f);
            // The hero's occupancy is the explicit opt-in and still counts.
            owned.RegisterBody(player);
            AssertThat(owned.CurrentWeight).IsEqualApprox(3f, 0.001f);

            // Back-compat: an empty allowlist accepts any authored weight.
            open.RegisterBody(foreign);
            open.RegisterBody(unowned);
            AssertThat(open.CurrentWeight).IsEqualApprox(4f, 0.001f);

            // A plate that does not opt into occupancy ignores the hero.
            open.AcceptsPlayerOccupancy = false;
            open.RegisterBody(player);
            AssertThat(open.CurrentWeight).IsEqualApprox(4f, 0.001f);
        } finally {
            player.Free();
            root.Free();
        }
    }

    [TestCase]
    public void ALostPropIsRecoveredFromOutOfBoundsAndAFreedPropIsReplacedOnce() {
        Room room = BuildRoom();
        try {
            room.PropA.GlobalPosition = FarAway;
            room.Plate.UnregisterBody(room.PropA);
            room.PropB.Free();
            AssertThat(room.Station.GetProp("test.prop_b")).IsNull();

            AssertThat(room.Station.TryReset(room.Player)).IsEqual(PuzzleResetResult.Reset);
            AssertThat(room.PropA.GlobalPosition.DistanceTo(HomeA)).IsLess(0.01f);
            AssertThat(room.Plate.IsPressed).IsTrue();

            WeightedObject replacement = room.Station.GetProp("test.prop_b");
            AssertObject(replacement).IsNotNull();
            AssertThat(replacement.GlobalPosition.DistanceTo(HomeB)).IsLess(0.01f);
            AssertThat(replacement.PuzzleOwnerID).IsEqual(PuzzleID);
            AssertThat(replacement.WeightUnits).IsEqualApprox(2f, 0.001f);
            AssertThat(CountProps(room, "test.prop_b")).IsEqual(1);

            // A second reset restores the replacement; it does not spawn another.
            room.Station.ClearActivationLatchForTest();
            replacement.GlobalPosition = FarAway;
            AssertThat(room.Station.TryReset(room.Player)).IsEqual(PuzzleResetResult.Reset);
            AssertThat(CountProps(room, "test.prop_b")).IsEqual(1);
            AssertThat(replacement.GlobalPosition.DistanceTo(HomeB)).IsLess(0.01f);
        } finally {
            Free(room);
        }
    }

    [TestCase]
    public void ResetIsRefusedDuringTimeFreezeTheWorldHoldAndForADeadHero() {
        Room room = BuildRoom();
        try {
            room.PropA.GlobalPosition = FarAway;

            room.Player.TimeFrozen = true;
            AssertThat(room.Station.TryReset(room.Player)).IsEqual(PuzzleResetResult.WorldStopped);
            InteractionArea area = room.Station.GetNode<InteractionArea>("Interaction");
            AssertThat(area.TryInteract(room.Player)).IsFalse();
            AssertThat(room.PropA.GlobalPosition.DistanceTo(FarAway)).IsLess(0.01f);
            room.Player.TimeFrozen = false;

            room.Player.BeginRecoveryHold(StoryRecoveryHoldCause.DeathRewind, 60);
            AssertThat(room.Station.TryReset(room.Player)).IsEqual(PuzzleResetResult.WorldStopped);
            AssertThat(room.PropA.GlobalPosition.DistanceTo(FarAway)).IsLess(0.01f);
            room.Player.CancelRecoveryHold();

            AssertThat(room.Station.TryReset(null)).IsEqual(PuzzleResetResult.InvalidActor);
            AssertThat(room.Puzzle.ArrangementGeneration).IsEqual(0);

            // Nothing was queued: the first eligible press is the one that resets.
            AssertThat(room.PropA.GlobalPosition.DistanceTo(FarAway)).IsLess(0.01f);
            AssertThat(area.TryInteract(room.Player)).IsTrue();
            AssertThat(room.PropA.GlobalPosition.DistanceTo(HomeA)).IsLess(0.01f);
        } finally {
            room.Player.TimeFrozen = false;
            Free(room);
        }
    }

    private static int CountProps(Room room, string propID) {
        int count = 0;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        Godot.Collections.Array<Node> weights = tree.GetNodesInGroup(WeightedObject.MovableWeightGroup);
        using var lifetime = weights.AsDisposable();
        foreach (Node node in weights) {
            if (node is WeightedObject prop && prop.PropID == propID && !prop.IsQueuedForDeletion()) count++;
        }
        return count;
    }
}
