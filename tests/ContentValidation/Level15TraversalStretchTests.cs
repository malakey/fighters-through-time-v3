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
/// GR-LV5: Level 15's carve, the Portico run and the Portico's swarm row on the real scene.
/// The shared rules are <c>TraversalStretchContentTests</c>; these are the level's own facts.
/// The run stands far west of the firestorm (6500–9100), so neither escape rule is touched.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level15TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_15_Alexandria.tscn";

    /// <summary>A carve, not a shift: the Portico shrank, par took its +30, and nothing past the run moved.</summary>
    [TestCase]
    public void ThePorticoWasCarvedAndNothingPastTheRunMoved() {
        using var fixture = new Fixture("einstein");
        Level15Controller level = fixture.Level;
        AssertThat(level.PorticoLock.Area.Position.X).IsEqual(320f);
        AssertThat(level.PorticoLock.Area.End.X).IsEqual(2000f);
        AssertThat(level.StacksLock.Area.Position.X).IsEqual(3020f);

        TraversalStretch run = Children(level).OfType<TraversalStretch>().Single();
        AssertThat(run.MinX).IsEqual(2020f);
        AssertThat(run.MaxX).IsEqual(3000f);
        AssertThat(run.HasTrench).IsTrue();
        AssertThat(run.MaxX < Level15Controller.EscapeStartX).IsTrue();

        // 540 seeded + 30 for the run.
        AssertThat(level.ParSeconds).IsEqual(570f);
        // Unmoved: the hall lock, the escape, the font, the arena, the width and the secret.
        AssertThat(level.CollapsingHallLock.Area.Position.X).IsEqual(6420f);
        AssertThat(level.CollapsingHallLock.Area.End.X).IsEqual(7900f);
        AssertThat(Level15Controller.EscapeStartX).IsEqual(6500f);
        AssertThat(Level15Controller.EscapeFinishX).IsEqual(9100f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(9560f);
        AssertThat(Level15Controller.BossArenaWestX).IsEqual(9750f);
        AssertThat(Level15Controller.LevelWidth).IsEqual(11600f);
        AssertThat(level.AuthoredSecretCachePosition).IsEqual(new Vector2(4790f, 1055f));
        AssertThat(level.FirestormTriggered).IsFalse();
        // The route pays the moved pylon, which stands over the Reading Gallery's end.
        AssertThat(Level15Controller.ExtractorPlacements[0].Position).IsEqual(new Vector2(2830f, 738f));
    }

    /// <summary>
    /// A hero in the trench is never stuck: Lincoln walks down the west ramp, along the floor
    /// to the east ramp, and all the way back up the west ramp, in real frames.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSlowestHeroWalksInAndOutOfTheTrench() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("lincoln");
        Level15Controller level = fixture.Level;
        PlayerController hero = level.Player;
        var source = new BufferedInputSource();
        try {
            await Settle(tree);
            foreach (StoryCyclicHazard lane in Children(level).OfType<StoryCyclicHazard>()) lane.Enabled = false;
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
            hero.GlobalPosition = new Vector2(2040f, 1390f);
            hero.Velocity = Vector2.Zero;

            source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
            float deepest = 0f;
            for (int frame = 0; frame < 480 && hero.GlobalPosition.X < 2970f; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                deepest = Mathf.Max(deepest, hero.GlobalPosition.Y);
            }
            AssertThat(hero.GlobalPosition.X >= 2970f)
                .OverrideFailureMessage($"Lincoln stalled in the trench at {hero.GlobalPosition}.").IsTrue();
            AssertThat(deepest >= 1518f).OverrideFailureMessage($"He never reached the trench floor: deepest y {deepest:0}.").IsTrue();

            source.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None, GameplayButtons.None));
            for (int frame = 0; frame < 480 && hero.GlobalPosition.X > 2045f; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(hero.GlobalPosition.X <= 2045f && hero.GlobalPosition.Y <= 1401f)
                .OverrideFailureMessage($"Lincoln did not walk back out by the west ramp: {hero.GlobalPosition}.").IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    /// <summary>
    /// The swarm row on the real scene (ruling 1b++, Level 15's only one): it comes once at most two
    /// of the Portico's troopers are left, the whole row on Normal, and a fodder kill pays nothing
    /// and drops no pickup (F05). Clearing the Portico starts no firestorm.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task ThePorticosSwarmComesAtTwoAliveAndItsKillsPayNothing() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level15Controller level = fixture.Level;
        PlayerController hero = level.Player;
        try {
            await Settle(tree);
            hero.GlobalPosition = new Vector2(1000f, 1350f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            EncounterLock portico = level.PorticoLock;
            AssertThat(portico.State).IsEqual(EncounterLockState.Engaged);
            int troopers = portico.LivingMemberCount;
            int authored = Level15Controller.SpawnTable.Count(entry => entry.Wave == 1);
            AssertThat(troopers).IsEqual(authored);

            // Three alive: no swarm yet.
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(portico.LivingMemberCount).IsEqual(troopers);

            // One down: two alive, so the row comes.
            portico.LivingMembers.First().TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            int row = Level15Controller.SwarmSpawnXs.Length;
            AssertThat(row).IsEqual(SwarmWaveRules.RowMobsForLockWidth(portico.Area.Size.X));
            AssertThat(portico.LivingMemberCount).IsEqual(troopers - 1 + row);

            // A fodder kill pays nothing, and drops no pickup.
            int wallet = StoryManager.Instance.ChronalDustCollected;
            EnemyController fodder = portico.LivingMembers.First(member => member.Data.DustWeightZero);
            int pickupsBefore = tree.GetNodesInGroup("chronal_dust").Count;
            fodder.TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(StoryManager.Instance.ChronalDustCollected).IsEqual(wallet);
            AssertThat(tree.GetNodesInGroup("chronal_dust").Count).IsEqual(pickupsBefore);
            AssertThat(level.FirestormTriggered).IsFalse();
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
        public readonly Level15Controller Level;
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
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level15Controller>();
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
