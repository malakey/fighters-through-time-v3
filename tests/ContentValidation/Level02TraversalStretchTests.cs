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
/// GR-PILOT: Level 2's carve, its stretch and its swarm on the real scene. The shared
/// rules are <c>TraversalStretchContentTests</c>; these are the pilot's own facts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level02TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_02_Orleans.tscn";

    /// <summary>A carve, not a shift: the lock shrank, par took its +30, and nothing past the stretch moved.</summary>
    [TestCase]
    public void TheVanguardWasCarvedAndNothingPastTheStretchMoved() {
        using var fixture = new Fixture("einstein");
        Level02Controller level = fixture.Level;
        AssertThat(level.VanguardLock.Area.Position.X).IsEqual(300f);
        AssertThat(level.VanguardLock.Area.End.X).IsEqual(2000f);
        AssertThat(level.SiegeLineLock.Area.Position.X).IsEqual(3220f);

        TraversalStretch rampart = Children(level).OfType<TraversalStretch>().Single();
        AssertThat(rampart.MinX).IsEqual(2020f);
        AssertThat(rampart.MaxX).IsEqual(3200f);
        AssertThat(rampart.HasTrench).IsTrue();

        // 360 seeded + 30 for the stretch.
        AssertThat(level.ParSeconds).IsEqual(390f);
        // Unmoved: both later anchors, the font, the arena, the level's width and the secret.
        AssertThat(Level02Controller.MiddleCheckpointX).IsEqual(6500f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(9180f);
        AssertThat(Level02Controller.BossArenaWestX).IsEqual(9300f);
        AssertThat(Level02Controller.LevelWidth).IsEqual(10560f);
        AssertThat(level.AuthoredSecretCachePosition).IsEqual(new Vector2(900f, 467f));
    }

    /// <summary>
    /// A hero in the trench is never stuck: Lincoln walks down the west ramp, along the floor
    /// to the east ramp, and all the way back up the west ramp, in real frames.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSlowestHeroWalksInAndOutOfTheTrench() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("lincoln");
        Level02Controller level = fixture.Level;
        PlayerController hero = level.Player;
        var source = new BufferedInputSource();
        try {
            await Settle(tree);
            foreach (StoryCyclicHazard lane in Children(level).OfType<StoryCyclicHazard>()) lane.Enabled = false;
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
            hero.GlobalPosition = new Vector2(2040f, 890f);
            hero.Velocity = Vector2.Zero;

            source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
            float deepest = 0f;
            for (int frame = 0; frame < 480 && hero.GlobalPosition.X < 3060f; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                deepest = Mathf.Max(deepest, hero.GlobalPosition.Y);
            }
            AssertThat(hero.GlobalPosition.X >= 3060f)
                .OverrideFailureMessage($"Lincoln stalled in the trench at {hero.GlobalPosition}.").IsTrue();
            AssertThat(deepest >= 1018f).OverrideFailureMessage($"He never reached the trench floor: deepest y {deepest:0}.").IsTrue();

            source.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None, GameplayButtons.None));
            for (int frame = 0; frame < 480 && hero.GlobalPosition.X > 2045f; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(hero.GlobalPosition.X <= 2045f && hero.GlobalPosition.Y <= 901f)
                .OverrideFailureMessage($"Lincoln did not walk back out by the west ramp: {hero.GlobalPosition}.").IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    /// <summary>The Parapet line's longest hop is real: Lincoln clears it with a double jump, in real frames.</summary>
    [TestCase(Timeout = 60000)]
    public async Task LincolnClearsTheParapetLinesLongestHopInRealFrames() {
        TraversalContentKit.RouteFact route = TraversalContentKit.Levels().Single(level => level.Index == 2).Routes.Single();
        TraversalSurface from = route.BaseLine[^2];
        TraversalSurface to = route.BaseLine[^1];
        AssertThat(TraversalRules.HopGap(from, to)).IsEqual(250f);
        (bool landed, string report) = await TraversalContentKit.Fly("lincoln", from, to, TraversalContentKit.FlightAbility.None);
        GD.Print($"[reach] {report}");
        AssertThat(landed).OverrideFailureMessage(report).IsTrue();
    }

    /// <summary>
    /// The swarm row on the real scene: it comes once at most two of the siege line's soldiers are
    /// left, the whole row on Normal, and a fodder kill pays nothing and drops no pickup (F05).
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSiegeLinesSwarmComesAtTwoAliveAndItsKillsPayNothing() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level02Controller level = fixture.Level;
        PlayerController hero = level.Player;
        try {
            await Settle(tree);
            level.VanguardLock.RestoreCleared();
            hero.GlobalPosition = new Vector2(3600f, 850f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            EncounterLock siege = level.SiegeLineLock;
            AssertThat(siege.State).IsEqual(EncounterLockState.Engaged);
            int soldiers = siege.LivingMemberCount;
            AssertThat(soldiers).IsEqual(Level02Controller.Room2Spawns.Length);

            // Three alive: no swarm yet.
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(siege.LivingMemberCount).IsEqual(soldiers);

            // One down: two alive, so the row comes.
            siege.LivingMembers.First().TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            int row = Level02Controller.SwarmSpawnXs.Length;
            AssertThat(row).IsEqual(SwarmWaveRules.RowMobsForLockWidth(siege.Area.Size.X));
            AssertThat(siege.LivingMemberCount).IsEqual(soldiers - 1 + row);

            // A fodder kill pays nothing, and drops no pickup.
            int wallet = StoryManager.Instance.ChronalDustCollected;
            EnemyController fodder = siege.LivingMembers.First(member => member.Data.DustWeightZero);
            int pickupsBefore = tree.GetNodesInGroup("chronal_dust").Count;
            fodder.TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(StoryManager.Instance.ChronalDustCollected).IsEqual(wallet);
            AssertThat(tree.GetNodesInGroup("chronal_dust").Count).IsEqual(pickupsBefore);
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
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
        public readonly Level02Controller Level;
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
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level02Controller>();
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
