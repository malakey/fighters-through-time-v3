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
/// GR-LV3: Level 7's carve, the Careening Yard and the quay's swarm on the real scene. The shared
/// rules are <c>TraversalStretchContentTests</c>; these are Nassau's own facts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level07TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_07_Nassau.tscn";

    /// <summary>
    /// A carve from the lock's west end: the Ranger channel's haul-out still lands inside the quay
    /// lock, the ropes are the same three, par took its +30, and nothing past the quay moved.
    /// </summary>
    [TestCase]
    public void TheQuayWasCarvedFromItsWestEndAndTheRopeChannelDidNotMove() {
        using var fixture = new Fixture("einstein");
        Level07Controller level = fixture.Level;
        AssertThat(level.QuayLock.Area.Position.X).IsEqual(1720f);
        AssertThat(level.QuayLock.Area.End.X).IsEqual(3580f);
        AssertThat(level.BoardingLock.Area.Position.X).IsEqual(5780f);

        TraversalStretch yard = FindChild<TraversalStretch>(level);
        AssertThat(yard.MinX).IsEqual(400f);
        AssertThat(yard.MaxX).IsEqual(1700f);
        AssertThat(yard.HasTrench).IsTrue();

        // The haul-out lands inside the lock that holds the launch yard.
        Vector2 rescue = Level07Controller.WaterSpans[0].Rescue;
        AssertThat(level.QuayLock.Area.HasPoint(rescue)).IsTrue();
        AssertThat(level.RopeAnchors.Count).IsEqual(3);
        AssertThat(Level07Controller.AnchorPivotsX[0]).IsEqual(3950f);

        // 630 seeded + 30 for the stretch.
        AssertThat(level.ParSeconds).IsEqual(660f);
        // Unmoved: both later anchors, the font, the arena and the level's width.
        AssertThat(Level07Controller.CheckpointPlacements[1].Position.X).IsEqual(5650f);
        AssertThat(Level07Controller.CheckpointPlacements[2].Position.X).IsEqual(8680f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(9100f);
        AssertThat(Level07Controller.BossArenaWestX).IsEqual(9350f);
        AssertThat(Level07Controller.LevelWidth).IsEqual(10600f);
        // The route pays the existing secret at its end, and the mortar is a lane on the dock's floor.
        AssertThat(FindChild<OptionalRoute>(level).RewardSourceID).IsEqual("level_07.secret");
        AssertThat(level.AuthoredSecretCachePosition).IsEqual(new Vector2(Level07Controller.CraneLineEndX, 215f));
        AssertThat(level.GetNode<StoryCyclicHazard>("MortarQuayA").GlobalPosition.Y).IsEqual(yard.TrenchFloorY);
    }

    /// <summary>
    /// A hero in the dock is never stuck, in real frames: Lincoln walks from the west deck to the
    /// east ramp's top, past the mortar's strip with it off, and back out by the west ramp. A double
    /// jump from the deck clears the wall, and his longest hop on the route is real too.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSlowestHeroWalksThroughTheDockAndClearsTheRoutesLongestHop() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using (var fixture = new Fixture("lincoln")) {
            Level07Controller level = fixture.Level;
            PlayerController hero = level.Player;
            var source = new BufferedInputSource();
            try {
                await Settle(tree);
                level.GetNode<StoryCyclicHazard>("MortarQuayA").Enabled = false;
                InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
                hero.GlobalPosition = new Vector2(420f, Level07Controller.DeckY - 10f);
                hero.Velocity = Vector2.Zero;

                source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
                float deepest = 0f;
                for (int frame = 0; frame < 480 && hero.GlobalPosition.X < 1550f; frame++) {
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                    deepest = Mathf.Max(deepest, hero.GlobalPosition.Y);
                }
                AssertThat(hero.GlobalPosition.X >= 1550f && hero.GlobalPosition.Y <= Level07Controller.DeckY + 1f)
                    .OverrideFailureMessage($"Lincoln stalled in the dock at {hero.GlobalPosition}.").IsTrue();
                AssertThat(deepest >= Level07Controller.DeckY + 118f)
                    .OverrideFailureMessage($"He never reached the dock's floor: deepest y {deepest:0}.").IsTrue();

                source.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None, GameplayButtons.None));
                for (int frame = 0; frame < 480 && hero.GlobalPosition.X > 440f; frame++) {
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                }
                AssertThat(hero.GlobalPosition.X <= 440f && hero.GlobalPosition.Y <= Level07Controller.DeckY + 1f)
                    .OverrideFailureMessage($"Lincoln did not walk back out by the west ramp: {hero.GlobalPosition}.").IsTrue();

                // A double jump from the deck, right held, clears the 210 px wall.
                hero.GlobalPosition = new Vector2(1552f, Level07Controller.DeckY - 2f);
                hero.Velocity = Vector2.Zero;
                source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
                for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                await DoubleJumpOver(tree, hero, source, 1625f);
                AssertThat(hero.GlobalPosition.X > 1625f && hero.GlobalPosition.Y <= Level07Controller.DeckY + 1f)
                    .OverrideFailureMessage($"Lincoln did not clear the deck wall: {hero.GlobalPosition}.").IsTrue();
            } finally {
                InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
                PoolManager.Instance?.ReleaseActiveUnder(level);
            }
        }

        TraversalContentKit.RouteFact route = TraversalContentKit.Levels().Single(level => level.Index == 7).Routes.Single();
        AssertThat(TraversalRules.HopGap(route.BaseLine[^2], route.BaseLine[^1])).IsEqual(245f);
        (bool landed, string report) = await TraversalContentKit.Fly("lincoln", route.BaseLine[^2], route.BaseLine[^1], TraversalContentKit.FlightAbility.None);
        GD.Print($"[reach] {report}");
        AssertThat(landed).OverrideFailureMessage(report).IsTrue();
    }

    /// <summary>
    /// The swarm row on the real scene: it comes after the Ranger's crew, once two fighters are left,
    /// the whole row on Normal, and a fodder kill pays nothing and drops no pickup (F05).
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheQuaysSwarmComesAtTwoAliveAndItsKillsPayNothing() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level07Controller level = fixture.Level;
        PlayerController hero = level.Player;
        try {
            await Settle(tree);
            hero.GlobalPosition = new Vector2(1850f, Level07Controller.DeckY - 10f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            EncounterLock quay = level.QuayLock;
            AssertThat(quay.State).IsEqual(EncounterLockState.Engaged);

            int soldiers = quay.LivingMemberCount;
            AssertThat(soldiers).IsEqual(3);

            // Waves spawn in order, so the Ranger's crew comes first, at one alive, and two alive
            // before that brings no swarm.
            quay.LivingMembers.First().TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(quay.LivingMemberCount).IsEqual(2);
            quay.LivingMembers.First().TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(quay.LivingMemberCount).IsEqual(3);

            // One of those down: two alive, so the row comes.
            quay.LivingMembers.First().TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            int row = Level07Controller.SwarmSpawnXs.Length;
            AssertThat(row).IsEqual(SwarmWaveRules.RowMobsForLockWidth(quay.Area.Size.X));
            AssertThat(quay.LivingMemberCount).IsEqual(2 + row);

            // A fodder kill pays nothing, and drops no pickup.
            int wallet = StoryManager.Instance.ChronalDustCollected;
            EnemyController fodder = quay.LivingMembers.First(member => member.Data.DustWeightZero);
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
        public readonly Level07Controller Level;
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
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level07Controller>();
            Level.Name = "Nassau_TraversalFixture";
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
