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
/// GR-LV3: Level 8's carve, the Dune Crossing and the siege line's swarm on the real scene. The
/// shared rules are <c>TraversalStretchContentTests</c>; these are Alexandria's own facts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level08TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_08_Egypt.tscn";

    /// <summary>A carve, not a shift: the dunes shrank, par took its +30, and nothing past the stretch moved.</summary>
    [TestCase]
    public void TheDunesWereCarvedAndNothingPastTheStretchMoved() {
        using var fixture = new Fixture("einstein");
        Level08Controller level = fixture.Level;
        AssertThat(level.DunesLock.Area.Position.X).IsEqual(300f);
        AssertThat(level.DunesLock.Area.End.X).IsEqual(2000f);
        AssertThat(level.SiegeLineLock.Area.Position.X).IsEqual(3220f);

        TraversalStretch crossing = FindChild<TraversalStretch>(level);
        AssertThat(crossing.MinX).IsEqual(2020f);
        AssertThat(crossing.MaxX).IsEqual(3200f);
        AssertThat(crossing.HasTrench).IsTrue();
        // Its camera keeps to the surface band.
        AssertThat(crossing.Room.CameraBounds).IsEqual(Level08Controller.Room1CameraBounds);

        // 360 seeded + 30 for the stretch.
        AssertThat(level.ParSeconds).IsEqual(390f);
        // Unmoved: both later anchors, the font, the arena, the level's width and the secret.
        AssertThat(Level08Controller.MiddleCheckpointX).IsEqual(5640f);
        AssertThat(Level08Controller.PreBossCheckpointX).IsEqual(8490f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(8660f);
        AssertThat(Level08Controller.BossArenaWestX).IsEqual(8900f);
        AssertThat(Level08Controller.LevelWidth).IsEqual(10240f);
        AssertThat(level.AuthoredSecretCachePosition).IsEqual(new Vector2(3950f, 327f));

        // The second drift lies in the trench, and on its floor.
        MovementDampenerZone drift = FindChild<MovementDampenerZone>(level, zone => zone.ZoneID == "level_08.deep_sand_b");
        AssertThat(drift.GlobalPosition.X > crossing.WestDeckEndX && drift.GlobalPosition.X < crossing.EastDeckStartX).IsTrue();
        AssertThat(drift.GlobalPosition.Y + 80f >= crossing.TrenchFloorY).IsTrue();
    }

    /// <summary>
    /// A hero in the trench is never stuck, in real frames: Lincoln wades from the west deck to the
    /// east ramp's top and back out by the west ramp, and the sand really slows him. A double jump
    /// from the deck clears the wall, and his longest hop on the route is real too.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSlowestHeroWadesThroughTheTrenchAndClearsTheRoutesLongestHop() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using (var fixture = new Fixture("lincoln")) {
            Level08Controller level = fixture.Level;
            PlayerController hero = level.Player;
            var source = new BufferedInputSource();
            try {
                await Settle(tree);
                InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
                hero.GlobalPosition = new Vector2(2040f, Level08Controller.SurfaceGroundY - 10f);
                hero.Velocity = Vector2.Zero;

                source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
                float deepest = 0f;
                float slowest = 1f;
                for (int frame = 0; frame < 900 && hero.GlobalPosition.X < 3110f; frame++) {
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                    deepest = Mathf.Max(deepest, hero.GlobalPosition.Y);
                    slowest = Mathf.Min(slowest, hero.EnvironmentMoveMultiplier);
                }
                AssertThat(hero.GlobalPosition.X >= 3110f && hero.GlobalPosition.Y <= Level08Controller.SurfaceGroundY + 1f)
                    .OverrideFailureMessage($"Lincoln stalled in the trench at {hero.GlobalPosition}.").IsTrue();
                AssertThat(deepest >= Level08Controller.SurfaceGroundY + 118f)
                    .OverrideFailureMessage($"He never reached the trench floor: deepest y {deepest:0}.").IsTrue();
                AssertFloat(slowest).OverrideFailureMessage("The trench's drift never slowed him.")
                    .IsEqualApprox(Level08Controller.DeepSandMultiplier, 0.001f);

                source.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None, GameplayButtons.None));
                for (int frame = 0; frame < 900 && hero.GlobalPosition.X > 2050f; frame++) {
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                }
                AssertThat(hero.GlobalPosition.X <= 2050f && hero.GlobalPosition.Y <= Level08Controller.SurfaceGroundY + 1f)
                    .OverrideFailureMessage($"Lincoln did not walk back out by the west ramp: {hero.GlobalPosition}.").IsTrue();

                // A double jump from the deck, right held, clears the 210 px wall.
                hero.GlobalPosition = new Vector2(3112f, Level08Controller.SurfaceGroundY - 2f);
                hero.Velocity = Vector2.Zero;
                source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
                for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                await DoubleJumpOver(tree, hero, source, 3185f);
                AssertThat(hero.GlobalPosition.X > 3185f && hero.GlobalPosition.Y <= Level08Controller.SurfaceGroundY + 1f)
                    .OverrideFailureMessage($"Lincoln did not clear the deck wall: {hero.GlobalPosition}.").IsTrue();
            } finally {
                InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
                PoolManager.Instance?.ReleaseActiveUnder(level);
            }
        }

        TraversalContentKit.RouteFact route = TraversalContentKit.Levels().Single(level => level.Index == 8).Routes.Single();
        AssertThat(TraversalRules.HopGap(route.BaseLine[^2], route.BaseLine[^1])).IsEqual(245f);
        (bool landed, string report) = await TraversalContentKit.Fly("lincoln", route.BaseLine[^2], route.BaseLine[^1], TraversalContentKit.FlightAbility.None);
        GD.Print($"[reach] {report}");
        AssertThat(landed).OverrideFailureMessage(report).IsTrue();
    }

    /// <summary>
    /// The swarm row on the real scene: it comes once the siege line's third soldier falls, the whole
    /// row on Normal, and a fodder kill pays nothing and drops no pickup (F05).
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSiegeLinesSwarmComesAtTwoAliveAndItsKillsPayNothing() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level08Controller level = fixture.Level;
        PlayerController hero = level.Player;
        try {
            await Settle(tree);
            level.DunesLock.RestoreCleared();
            hero.GlobalPosition = new Vector2(3600f, Level08Controller.SurfaceGroundY - 50f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            EncounterLock siege = level.SiegeLineLock;
            AssertThat(siege.State).IsEqual(EncounterLockState.Engaged);

            int soldiers = siege.LivingMemberCount;
            AssertThat(soldiers).IsEqual(Level08Controller.SiegeLineSpawns.Length);

            // Three alive: no swarm yet.
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(siege.LivingMemberCount).IsEqual(soldiers);

            // One down: two alive, so the row comes.
            siege.LivingMembers.First().TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            int row = Level08Controller.SwarmSpawnXs.Length;
            AssertThat(row).IsEqual(SwarmWaveRules.RowMobsForLockWidth(siege.Area.Size.X));
            AssertThat(siege.LivingMemberCount).IsEqual(soldiers - 1 + row);

            // A fodder kill pays nothing, and drops no pickup.
            int wallet = StoryManager.Instance.ChronalDustCollected;
            EnemyController fodder = siege.LivingMembers.First(member => member.Data.DustWeightZero);
            int pickupsBefore = CountPickups(tree);
            fodder.TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(StoryManager.Instance.ChronalDustCollected).IsEqual(wallet);
            AssertThat(CountPickups(tree)).IsEqual(pickupsBefore);
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    /// <summary>Holds right from the deck through a double jump, until the hero stands past <paramref name="pastX"/>.</summary>
    private static async Task DoubleJumpOver(SceneTree tree, PlayerController hero, BufferedInputSource source, float pastX) {
        GameplayButtons held = GameplayButtons.None;
        int stage = 0;
        for (int frame = 0; frame < 180 && !(hero.GlobalPosition.X > pastX && hero.IsOnFloor()); frame++) {
            GameplayButtons buttons = GameplayButtons.None;
            switch (stage) {
                case 0: buttons = GameplayButtons.Jump; if (!hero.IsOnFloor()) stage = 1; break;
                case 1: buttons = GameplayButtons.Jump; if (hero.Velocity.Y >= 0f) stage = 2; break;
                case 2: stage = 3; break;
                default: buttons = GameplayButtons.Jump; break;
            }
            source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, buttons, held));
            held = buttons;
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
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
        public readonly Level08Controller Level;
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
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level08Controller>();
            Level.Name = "Egypt_TraversalFixture";
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
