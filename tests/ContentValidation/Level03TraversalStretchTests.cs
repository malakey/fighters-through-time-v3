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
/// GR-LV1: Level 3's carve, the Skyway and its swarm on the real scene. The shared rules are
/// <c>TraversalStretchContentTests</c>; these are Chicago's own facts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level03TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_03_Chicago.tscn";

    /// <summary>A carve, not a shift: the Midway shrank, par took its +30, and nothing past the stretch moved.</summary>
    [TestCase]
    public void TheMidwayWasCarvedAndNothingPastTheStretchMoved() {
        using var fixture = new Fixture("einstein");
        Level03Controller level = fixture.Level;
        AssertThat(level.MidwayLock.Area.Position.X).IsEqual(700f);
        AssertThat(level.MidwayLock.Area.End.X).IsEqual(2500f);
        AssertThat(level.ElectricityLock.Area.Position.X).IsEqual(3840f);

        TraversalStretch skyway = FindChild<TraversalStretch>(level);
        AssertThat(skyway.MinX).IsEqual(2520f);
        AssertThat(skyway.MaxX).IsEqual(3820f);
        AssertThat(skyway.HasTrench).IsTrue();

        // 540 seeded + 30 for the stretch.
        AssertThat(level.ParSeconds).IsEqual(570f);
        // Unmoved: both later anchors, the font, the arena, the level's width and the secret.
        AssertThat(Level03Controller.MiddleCheckpointX).IsEqual(6000f);
        AssertThat(Level03Controller.PreBossCheckpointX).IsEqual(8500f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(8760f);
        AssertThat(Level03Controller.BossArenaWestX).IsEqual(9000f);
        AssertThat(Level03Controller.LevelWidth).IsEqual(10240f);
        AssertThat(level.AuthoredSecretCachePosition).IsEqual(new Vector2(1000f, 997f));
    }

    /// <summary>
    /// A hero in the trench is never stuck, in real frames: Lincoln walks from the west deck
    /// to the east ramp's foot, past the arc lamp's strip with the lamp off, up to the deck,
    /// and back out by the west ramp. A double jump from the deck clears the wall, and his
    /// longest hop on the route is real too.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSlowestHeroWalksThroughTheTrenchAndClearsTheRoutesLongestHop() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using (var fixture = new Fixture("lincoln")) {
            Level03Controller level = fixture.Level;
            PlayerController hero = level.Player;
            var source = new BufferedInputSource();
            try {
                await Settle(tree);
                FindChild<StoryCyclicHazard>(level, hazard => hazard.HazardID == "level_03.arc_skyway").Enabled = false;
                InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
                hero.GlobalPosition = new Vector2(2540f, 1390f);
                hero.Velocity = Vector2.Zero;

                source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
                float deepest = 0f;
                for (int frame = 0; frame < 480 && hero.GlobalPosition.X < 3670f; frame++) {
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                    deepest = Mathf.Max(deepest, hero.GlobalPosition.Y);
                }
                AssertThat(hero.GlobalPosition.X >= 3670f && hero.GlobalPosition.Y <= Level03Controller.GroundY + 1f)
                    .OverrideFailureMessage($"Lincoln stalled in the trench at {hero.GlobalPosition}.").IsTrue();
                AssertThat(deepest >= Level03Controller.GroundY + 118f)
                    .OverrideFailureMessage($"He never reached the trench floor: deepest y {deepest:0}.").IsTrue();

                source.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None, GameplayButtons.None));
                for (int frame = 0; frame < 480 && hero.GlobalPosition.X > 2550f; frame++) {
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                }
                AssertThat(hero.GlobalPosition.X <= 2550f && hero.GlobalPosition.Y <= Level03Controller.GroundY + 1f)
                    .OverrideFailureMessage($"Lincoln did not walk back out by the west ramp: {hero.GlobalPosition}.").IsTrue();

                // A double jump from the deck, right held, clears the 210 px wall.
                hero.GlobalPosition = new Vector2(3672f, Level03Controller.GroundY - 2f);
                hero.Velocity = Vector2.Zero;
                source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
                for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                GameplayButtons held = GameplayButtons.None;
                int stage = 0;
                for (int frame = 0; frame < 180 && !(hero.GlobalPosition.X > 3745f && hero.IsOnFloor()); frame++) {
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
                AssertThat(hero.GlobalPosition.X > 3745f && hero.GlobalPosition.Y <= Level03Controller.GroundY + 1f)
                    .OverrideFailureMessage($"Lincoln did not clear the deck wall: {hero.GlobalPosition}.").IsTrue();
            } finally {
                InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
                PoolManager.Instance?.ReleaseActiveUnder(level);
            }
        }

        TraversalContentKit.RouteFact route = TraversalContentKit.Levels().Single(level => level.Index == 3).Routes.Single();
        AssertThat(TraversalRules.HopGap(route.BaseLine[^2], route.BaseLine[^1])).IsEqual(245f);
        (bool landed, string report) = await TraversalContentKit.Fly("lincoln", route.BaseLine[^2], route.BaseLine[^1], TraversalContentKit.FlightAbility.None);
        GD.Print($"[reach] {report}");
        AssertThat(landed).OverrideFailureMessage(report).IsTrue();
    }

    /// <summary>
    /// The swarm row on the real scene: it comes once the Electricity lock's third defender
    /// falls, the whole row on Normal, and a fodder kill pays nothing and drops no pickup (F05).
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheElectricitySwarmComesAtTwoAliveAndItsKillsPayNothing() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level03Controller level = fixture.Level;
        PlayerController hero = level.Player;
        try {
            await Settle(tree);
            level.MidwayLock.RestoreCleared();
            // Past the absence trigger at 3870-3930, whose dialogue would pause the tree under this case.
            hero.GlobalPosition = new Vector2(4200f, Level03Controller.GroundY - 50f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            EncounterLock electricity = level.ElectricityLock;
            AssertThat(electricity.State).IsEqual(EncounterLockState.Engaged);
            int defenders = electricity.LivingMemberCount;
            AssertThat(defenders).IsEqual(Level03Controller.ElectricityWave.Length);

            // Three alive: no swarm yet.
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(electricity.LivingMemberCount).IsEqual(defenders);

            // One down: two alive, so the row comes.
            electricity.LivingMembers.First().TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            int row = Level03Controller.SwarmSpawnXs.Length;
            AssertThat(row).IsEqual(SwarmWaveRules.RowMobsForLockWidth(electricity.Area.Size.X));
            AssertThat(electricity.LivingMemberCount).IsEqual(defenders - 1 + row);

            // A fodder kill pays nothing, and drops no pickup.
            int wallet = StoryManager.Instance.ChronalDustCollected;
            EnemyController fodder = electricity.LivingMembers.First(member => member.Data.DustWeightZero);
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
        public readonly Level03Controller Level;
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
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level03Controller>();
            Level.Name = "Chicago_TraversalFixture";
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
