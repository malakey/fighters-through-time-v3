using System;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-LV5: Level 13's carve and the Fragment Stair on the real scene. The shared rules are
/// <c>TraversalStretchContentTests</c>; these are the level's own facts. The Threshold was
/// carved from its west end, so the stair it held is climbed unfought and the lock starts
/// where the Florence gear's ride does.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level13TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_13_ChronalVoid.tscn";

    /// <summary>A carve, not a shift: the Threshold shrank from the west, par took its +30, and nothing past the stair moved.</summary>
    [TestCase]
    public void TheThresholdWasCarvedFromTheWestAndNothingPastTheStairMoved() {
        using var fixture = new Fixture("einstein");
        Level13Controller level = fixture.Level;
        AssertThat(level.ThresholdLock.Area.Position.X).IsEqual(1880f);
        AssertThat(level.ThresholdLock.Area.End.X).IsEqual(3200f);
        AssertThat(level.DriftLock.Area.Position.X).IsEqual(3220f);

        TraversalStretch stair = Children(level).OfType<TraversalStretch>().Single();
        AssertThat(stair.MinX).IsEqual(320f);
        AssertThat(stair.MaxX).IsEqual(1860f);
        AssertThat(stair.HasTrench).IsFalse();
        // The Florence gear's ride starts inside the carved lock, clear of its entry barrier.
        PathMovingPlatform gear = level.DriftingShards.Single(shard => shard.Name == "ShardFlorenceGear");
        AssertThat(gear.GlobalPosition.X - 120f >= level.ThresholdLock.Area.Position.X).IsTrue();

        // 450 seeded + 30 for the stair.
        AssertThat(level.ParSeconds).IsEqual(480f);
        // Unmoved: both later anchors, the font, the Mirror's arena, the width and the secret.
        AssertThat(Level13Controller.Checkpoint1Position.X).IsEqual(5290f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(6970f);
        AssertThat(Level13Controller.ArenaStartX).IsEqual(7200f);
        AssertThat(Level13Controller.LevelWidth).IsEqual(9600f);
        AssertThat(level.AuthoredSecretCachePosition).IsEqual(new Vector2(4160f, 1375f));
        // The route pays the moved severed conduit, over the Drift line's end.
        AssertThat(Level13Controller.ExtractorPlacements[0].Position).IsEqual(new Vector2(1350f, 996f));
        // The carved lock has its own camera room, not one-shot, as wide as the old Threshold's.
        RoomTransitionTrigger room = level.RoomTriggers.Single(trigger => trigger.RoomID == "void_threshold_lock");
        AssertThat(room.ActivateOnce).IsFalse();
        AssertThat(room.CameraBounds.End.X).IsEqual(Level13Controller.Room2StartX + 100f);
    }

    /// <summary>
    /// In real frames Lincoln walks off the stair's shelf through the Threshold's open entry
    /// barrier: the lock stays Armed while he is on the stretch, and engages once he is inside.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task LincolnWalksFromTheStairIntoTheThresholdAndItEngagesOnlyInside() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("lincoln");
        Level13Controller level = fixture.Level;
        PlayerController hero = level.Player;
        var source = new BufferedInputSource();
        try {
            await Settle(tree);
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
            hero.GlobalPosition = new Vector2(1700f, Level13Controller.ShelfY - 2f);
            hero.Velocity = Vector2.Zero;
            level.SyncGravityFieldToPlayer();
            EncounterLock threshold = level.ThresholdLock;

            bool armedOnTheStretch = true;
            for (int frame = 0; frame < 360 && hero.GlobalPosition.X < 1990f; frame++) {
                source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                if (hero.GlobalPosition.X < Level13Controller.FragmentStairMaxX && threshold.State != EncounterLockState.Armed) {
                    armedOnTheStretch = false;
                }
            }
            AssertThat(armedOnTheStretch).OverrideFailureMessage("The Threshold left Armed while Lincoln was on the stair.").IsTrue();
            AssertThat(hero.GlobalPosition.X >= 1990f)
                .OverrideFailureMessage($"Lincoln did not walk into the Threshold: {hero.GlobalPosition}.").IsTrue();
            AssertThat(threshold.State).IsEqual(EncounterLockState.Engaged);
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    /// <summary>The Drift line's longest hop is real: Lincoln clears it with a double jump at Earth-normal gravity, in real frames.</summary>
    [TestCase(Timeout = 60000)]
    public async Task LincolnClearsTheDriftLinesLongestHopInRealFrames() {
        TraversalContentKit.RouteFact route = TraversalContentKit.Levels().Single(level => level.Index == 13).Routes.Single();
        TraversalSurface from = route.BaseLine[^2];
        TraversalSurface to = route.BaseLine[^1];
        AssertThat(TraversalRules.HopGap(from, to)).IsEqual(245f);
        (bool landed, string report) = await TraversalContentKit.Fly("lincoln", from, to, TraversalContentKit.FlightAbility.None);
        GD.Print($"[reach] {report}");
        AssertThat(landed).OverrideFailureMessage(report).IsTrue();
    }

    private static async Task Settle(SceneTree tree) {
        for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        DialogueManager dialogue = DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
        AssertThat(tree.Paused).IsFalse();
    }

    private static Node[] Children(Node parent) {
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using var lifetime = children.AsDisposable();
        return children.ToArray();
    }

    /// <summary>The real scene against no save, with the session handed back.</summary>
    private sealed class Fixture : IDisposable {
        public readonly Level13Controller Level;
        private readonly int _slot;
        private readonly string _character;
        private readonly bool _paused;

        public Fixture(string heroID) {
            var tree = (SceneTree)Engine.GetMainLoop();
            _paused = tree.Paused;
            _slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _character = GameManager.Instance.CurrentSession.SelectedCharacterID;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession.SelectedCharacterID = heroID;
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level13Controller>();
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _slot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _character;
            ((SceneTree)Engine.GetMainLoop()).Paused = _paused;
        }
    }
}
