using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 12 W8 (GAP-08 / V01b): every campaign puzzle room whose arrangement can
/// be lost or jammed carries a Reset Puzzle station, and every designated puzzle
/// prop and plate in it is owned (V01c).
///
/// <para>The enumeration is a source sweep: a campaign scene or level controller
/// that authors a movable <see cref="WeightedObject"/> or a <see cref="PressurePlate"/>
/// is a losable-state puzzle. Levers, gears, coils, glyph locks, rescues and
/// destructible relays cycle or latch in place and never become unusable, so they
/// need no station (see the W8 handoff for the per-level list). A new losable
/// puzzle that is not added to <see cref="StationCatalog"/> fails the sweep.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PuzzleResetPlacementTests {

    private const int ScratchSlot = 2;

    /// <summary>(scene, the controller's campaign slot, puzzle ID) for every losable-state puzzle.</summary>
    private static readonly (string ScenePath, CampaignLevel Level, string PuzzleID)[] StationCatalog = {
        ("res://scenes/campaign/Level_06_Pompeii.tscn", CampaignLevel.Pompeii, Level06Controller.RockfallPuzzleID)
    };

    private static readonly string[] SceneMarkers = {
        "WeightedObject.cs", "MovableWeightTemplate.tscn", "PressurePlate.cs", "PressurePlateTemplate.tscn"
    };

    private static readonly string[] ScriptMarkers = { "new WeightedObject", "new PressurePlate" };

    [TestCase]
    public void TheSourceSweepFindsExactlyTheCatalogedLosablePuzzleScenes() {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles("scenes/campaign", "*.tscn")) {
            string text = File.ReadAllText(file);
            if (SceneMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal))) {
                found.Add("res://scenes/campaign/" + Path.GetFileName(file));
            }
        }
        foreach (string file in Directory.GetFiles("scripts/Environment", "Level*.cs")) {
            string text = File.ReadAllText(file);
            if (ScriptMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal))) {
                found.Add("script:" + Path.GetFileName(file));
            }
        }
        string legacy = File.ReadAllText("scripts/Environment/LegacyLevelControllerBase.cs");
        if (ScriptMarkers.Any(marker => legacy.Contains(marker, StringComparison.Ordinal))) {
            found.Add("script:LegacyLevelControllerBase.cs");
        }

        var expected = new SortedSet<string>(StationCatalog.Select(entry => entry.ScenePath), StringComparer.Ordinal);
        AssertThat(string.Join(" | ", found)).IsEqual(string.Join(" | ", expected));
    }

    [TestCase]
    public void EveryCatalogedPuzzleRoomHasALiveStationAndOwnedPropsAndPlates() {
        foreach ((string scenePath, CampaignLevel level, string puzzleID) in StationCatalog) {
            using var fixture = new LevelFixture(scenePath, level);
            Node root = fixture.Level;

            var stations = Collect<PuzzleResetStation>(root);
            PuzzleResetStation station = stations.FirstOrDefault(s => s.PuzzleID == puzzleID);
            AssertObject(station).OverrideFailureMessage($"{scenePath}: no Reset Puzzle station bound to '{puzzleID}'.").IsNotNull();
            AssertThat(station.PromptKey).IsEqual(PuzzleResetStation.DefaultPromptKey);
            AssertObject(station.GetNodeOrNull<InteractionArea>("Interaction")).IsNotNull();
            AssertObject(station.GetNodeOrNull<CollisionShape2D>("Interaction/CollisionShape2D")
                ?? station.GetNode("Interaction").GetChildren().OfType<CollisionShape2D>().FirstOrDefault()).IsNotNull();

            var props = Collect<WeightedObject>(root);
            AssertThat(props.Count).IsGreater(0);
            var propIDs = new HashSet<string>(StringComparer.Ordinal);
            foreach (WeightedObject prop in props) {
                AssertThat(prop.PuzzleOwnerID).IsEqual(puzzleID);
                AssertThat(string.IsNullOrWhiteSpace(prop.PropID)).IsFalse();
                AssertThat(propIDs.Add(prop.PropID)).IsTrue();
            }

            foreach (PressurePlate plate in Collect<PressurePlate>(root)) {
                AssertThat(plate.AcceptedPuzzleOwnerIDs.Contains(puzzleID)).IsTrue();
            }

            station.RefreshPropRegistry();
            AssertThat(station.RegisteredPropIDs.Count).IsEqual(props.Count);
            // The station never stands inside a prop's restoration volume.
            foreach (WeightedObject prop in props) {
                AssertThat(prop.AuthoredHome.Origin.DistanceTo(station.GlobalPosition)).IsGreater(120f);
            }
        }
    }

    [TestCase]
    public void ThePompeiiStationResetsTheWinchAndGoesInertOnceTheRockfallIsCleared() {
        using var fixture = new LevelFixture("res://scenes/campaign/Level_06_Pompeii.tscn", CampaignLevel.Pompeii);
        var level = (Level06Controller)fixture.Level;
        PuzzleResetStation station = Collect<PuzzleResetStation>(level).Single();
        station.RefreshPropRegistry();

        Vector2 pumiceHome = level.PumiceBoulder.AuthoredHome.Origin;
        level.PumiceBoulder.GlobalPosition = new Vector2(-5000f, 5000f);
        level.BasaltBoulder.GlobalPosition = new Vector2(2000f, 5000f);
        level.LeftPan.UnregisterBody(level.BasaltBoulder);
        AssertThat(level.LeftPan.IsPressed).IsFalse();

        level.Player.GlobalPosition = Level06Controller.RockfallResetStationPosition;
        AssertThat(station.TryReset(level.Player)).IsEqual(PuzzleResetResult.Reset);
        AssertThat(level.PumiceBoulder.GlobalPosition.DistanceTo(pumiceHome)).IsLess(0.01f);
        AssertThat(level.LeftPan.IsPressed).IsTrue();
        AssertThat(level.RockfallCleared).IsFalse();

        // Solve it: the station must then refuse and leave everything alone.
        level.RightPan.RegisterBody(level.PumiceBoulder);
        level.RightPan.RegisterBody(level.Player);
        AssertThat(level.RockfallPuzzle.IsCompleted).IsTrue();
        level.PumiceBoulder.GlobalPosition = new Vector2(4260f, 872f);
        station.ClearActivationLatchForTest();
        AssertThat(station.TryReset(level.Player)).IsEqual(PuzzleResetResult.Solved);
        AssertThat(level.PumiceBoulder.GlobalPosition.DistanceTo(new Vector2(4260f, 872f))).IsLess(0.01f);
        AssertThat(level.RockfallPuzzle.IsCompleted).IsTrue();
    }

    private static List<T> Collect<T>(Node root) where T : Node {
        var found = new List<T>();
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0) {
            Node node = stack.Pop();
            if (node is T match) found.Add(match);
            foreach (Node child in node.GetChildren()) stack.Push(child);
        }
        return found;
    }

    /// <summary>
    /// Builds a real campaign scene against a scratch save resumed at the entry
    /// checkpoint (a fresh entry defers paused dialogue; see Level06ContentTests),
    /// clears the level's persisted puzzle flags, and hands every singleton back.
    /// </summary>
    private sealed class LevelFixture : IDisposable {
        public readonly StoryLevelControllerBase Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public LevelFixture(string scenePath, CampaignLevel level) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            PackedScene packed = ResourceLoader.Load<PackedScene>(scenePath);
            Level = packed.Instantiate<StoryLevelControllerBase>();
            SaveManager.Instance.SaveSlots[ScratchSlot] = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(level),
                LastCheckpointID = $"{Level.LevelID}_checkpoint_0",
                CurrentHP = 100,
                CurrentUltimateMeter = 50f
            };
            Level.Name = "PuzzleResetPlacementFixture";
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
