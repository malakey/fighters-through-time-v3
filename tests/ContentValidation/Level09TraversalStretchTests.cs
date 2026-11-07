using System;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-LV3: Level 9's carve, the Beach Run and the seawall's swarm on the real scene. The shared
/// rules are <c>TraversalStretchContentTests</c>; these are Hakata Bay's own facts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level09TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_09_Berlin.tscn";

    /// <summary>
    /// A carve, not a shift: the seawall shrank, par took its +30, and the stealth gauntlet, the
    /// near-capture and everything else past the stretch did not move.
    /// </summary>
    [TestCase]
    public void TheSeawallWasCarvedAndTheStealthAndTheNearCaptureDidNotMove() {
        using var fixture = new Fixture("einstein");
        Level09Controller level = fixture.Level;
        AssertThat(level.SeawallLock.Area.Position.X).IsEqual(300f);
        AssertThat(level.SeawallLock.Area.End.X).IsEqual(2000f);
        AssertThat(level.LanternWatchLock.Area.Position.X).IsEqual(3150f);
        AssertThat(level.LanternWatchLock.Area.End.X).IsEqual(6350f);

        TraversalStretch beach = FindChild<TraversalStretch>(level);
        AssertThat(beach.MinX).IsEqual(2020f);
        AssertThat(beach.MaxX).IsEqual(3130f);
        AssertThat(beach.HasTrench).IsTrue();
        // Its camera keeps to room 1's bottom-anchored band.
        AssertThat(beach.Room.CameraBounds.Position.Y).IsEqual(Level09Controller.GroundRoomCameraTop);

        // 360 seeded + 30 for the stretch.
        AssertThat(level.ParSeconds).IsEqual(390f);
        // Unmoved: the first cover pocket, both later anchors, the font, the arena, the level's
        // width, the three signal lines, the near-capture and the lanterns.
        AssertThat(Level09Controller.CoverPockets[0].CenterX).IsEqual(3010f);
        AssertThat(Level09Controller.MiddleCheckpointX).IsEqual(6570f);
        AssertThat(Level09Controller.PreBossCheckpointX).IsEqual(8740f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(8640f);
        AssertThat(Level09Controller.BossArenaWestX).IsEqual(8800f);
        AssertThat(Level09Controller.LevelWidth).IsEqual(10560f);
        AssertThat(Level09Controller.NearCaptureTriggerX).IsEqual(6500f);
        AssertThat(level.Searchlights.Count).IsEqual(3);
        AssertThat(level.FeedRelays.Count).IsEqual(3);
        // No Eraser joined, and the swarm stays out of the Lantern Watch.
        AssertThat(level.LanternWatchLock.Spec.Waves.Length).IsEqual(1);
        AssertThat(level.StormAnchorLock.Spec.Waves.Length).IsEqual(1);
    }

    /// <summary>
    /// A hero in the trench is never stuck, in real frames: Lincoln walks from the west deck to the
    /// deck past the east ramp, past the lane's strip with it off, and back out by the west ramp.
    /// A double jump from the last step reaches the secret's perch, and his longest hop on the
    /// route is real too.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSlowestHeroWalksThroughTheTrenchAndClearsTheRoutesLongestHop() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using (var fixture = new Fixture("lincoln")) {
            Level09Controller level = fixture.Level;
            PlayerController hero = level.Player;
            var source = new BufferedInputSource();
            try {
                await Settle(tree);
                FindChild<StoryCyclicHazard>(level, hazard => hazard.HazardID == "level_09.thunderbomb_beach").Enabled = false;
                InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
                hero.GlobalPosition = new Vector2(2040f, Level09Controller.GroundY - 10f);
                hero.Velocity = Vector2.Zero;

                source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
                float deepest = 0f;
                for (int frame = 0; frame < 480 && hero.GlobalPosition.X < 2900f; frame++) {
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                    deepest = Mathf.Max(deepest, hero.GlobalPosition.Y);
                }
                AssertThat(hero.GlobalPosition.X >= 2900f && hero.GlobalPosition.Y <= Level09Controller.GroundY + 1f)
                    .OverrideFailureMessage($"Lincoln stalled in the trench at {hero.GlobalPosition}.").IsTrue();
                AssertThat(deepest >= Level09Controller.GroundY + 118f)
                    .OverrideFailureMessage($"He never reached the trench floor: deepest y {deepest:0}.").IsTrue();

                source.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None, GameplayButtons.None));
                for (int frame = 0; frame < 480 && hero.GlobalPosition.X > 2050f; frame++) {
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                }
                AssertThat(hero.GlobalPosition.X <= 2050f && hero.GlobalPosition.Y <= Level09Controller.GroundY + 1f)
                    .OverrideFailureMessage($"Lincoln did not walk back out by the west ramp: {hero.GlobalPosition}.").IsTrue();
            } finally {
                InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
                PoolManager.Instance?.ReleaseActiveUnder(level);
            }
        }

        var lastStep = new TraversalSurface(2790f, 2950f, 1012f);
        var perch = new TraversalSurface(Level09Controller.SecretPerchX - 80f, Level09Controller.SecretPerchX + 80f, 952f);
        (bool reached, string perchReport) = await TraversalContentKit.Fly("lincoln", lastStep, perch, TraversalContentKit.FlightAbility.None);
        GD.Print($"[reach] {perchReport}");
        AssertThat(reached).OverrideFailureMessage(perchReport).IsTrue();

        TraversalContentKit.RouteFact route = TraversalContentKit.Levels().Single(level => level.Index == 9).Routes.Single();
        AssertThat(TraversalRules.HopGap(route.BaseLine[^2], route.BaseLine[^1])).IsEqual(245f);
        (bool landed, string report) = await TraversalContentKit.Fly("lincoln", route.BaseLine[^2], route.BaseLine[^1], TraversalContentKit.FlightAbility.None);
        GD.Print($"[reach] {report}");
        AssertThat(landed).OverrideFailureMessage(report).IsTrue();
    }

    /// <summary>
    /// The swarm row on the real scene: it comes once the seawall's third fighter falls, the whole
    /// row on Normal, and a fodder kill pays nothing and drops no pickup (F05).
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSeawallsSwarmComesAtTwoAliveAndItsKillsPayNothing() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level09Controller level = fixture.Level;
        PlayerController hero = level.Player;
        try {
            await Settle(tree);
            hero.GlobalPosition = new Vector2(1250f, Level09Controller.GroundY - 10f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            EncounterLock seawall = level.SeawallLock;
            AssertThat(seawall.State).IsEqual(EncounterLockState.Engaged);

            int soldiers = seawall.LivingMemberCount;
            AssertThat(soldiers).IsEqual(3);

            // Three alive: no swarm yet.
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(seawall.LivingMemberCount).IsEqual(soldiers);

            // One down: two alive, so the row comes.
            seawall.LivingMembers.First().TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            int row = Level09Controller.SwarmSpawnXs.Length;
            AssertThat(row).IsEqual(SwarmWaveRules.RowMobsForLockWidth(seawall.Area.Size.X));
            AssertThat(seawall.LivingMemberCount).IsEqual(soldiers - 1 + row);

            // A fodder kill pays nothing, and drops no pickup.
            int wallet = StoryManager.Instance.ChronalDustCollected;
            EnemyController fodder = seawall.LivingMembers.First(member => member.Data.DustWeightZero);
            int pickupsBefore = CountPickups(tree);
            fodder.TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(StoryManager.Instance.ChronalDustCollected).IsEqual(wallet);
            AssertThat(CountPickups(tree)).IsEqual(pickupsBefore);
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    private static T FindChild<T>(Node parent, Func<T, bool> matches = null) where T : Node {
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is T found && (matches == null || matches(found))) return found;
        }
        throw new InvalidOperationException($"No {typeof(T).Name} under {parent.Name}.");
    }

    private static int CountPickups(SceneTree tree) {
        Godot.Collections.Array<Node> pickups = tree.GetNodesInGroup("chronal_dust");
        using var lifetime = pickups.AsDisposable();
        return pickups.Count;
    }

    private static async Task Settle(SceneTree tree) {
        for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        DialogueManager dialogue = DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
        AssertThat(tree.Paused).IsFalse();
    }

    /// <summary>The real scene against no save on Normal, with the session handed back.</summary>
    private sealed class Fixture : IDisposable {
        public readonly Level09Controller Level;
        private readonly SessionData _session;
        private readonly bool _paused;

        public Fixture(string heroID) {
            var tree = (SceneTree)Engine.GetMainLoop();
            _paused = tree.Paused;
            _session = GameManager.Instance.CurrentSession;
            SessionData session = _session;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = heroID;
            session.Difficulty = Difficulty.Normal;
            GameManager.Instance.CurrentSession = session;
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level09Controller>();
            Level.Name = "Hakata_TraversalFixture";
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            PoolManager.Instance?.ReleaseActiveUnder(Level);
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            GameManager.Instance.CurrentSession = _session;
            ((SceneTree)Engine.GetMainLoop()).Paused = _paused;
        }
    }
}
