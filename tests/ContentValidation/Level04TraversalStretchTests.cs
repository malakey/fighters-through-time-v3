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
/// GR-LV2: Level 4's carve, its stretch and its swarm on the real scene. The shared rules
/// are <c>TraversalStretchContentTests</c>; these are the level's own facts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level04TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_04_Paris.tscn";

    /// <summary>A carve, not a shift: the gatehouse shrank, par took its +30, and nothing past the stretch moved, the puzzle and the pit least of all.</summary>
    [TestCase]
    public void TheGatehouseWasCarvedAndNothingPastTheStretchMoved() {
        using var fixture = new Fixture("einstein");
        Level04Controller level = fixture.Level;
        AssertThat(level.GatehouseLock.Area.Position.X).IsEqual(300f);
        AssertThat(level.GatehouseLock.Area.End.X).IsEqual(2000f);
        AssertThat(level.CorridorLock.Area.Position.X).IsEqual(3220f);

        TraversalStretch glacis = Children(level).OfType<TraversalStretch>().Single();
        AssertThat(glacis.MinX).IsEqual(2020f);
        AssertThat(glacis.MaxX).IsEqual(3200f);
        AssertThat(glacis.HasTrench).IsTrue();

        // 360 seeded + 30 for the stretch.
        AssertThat(level.ParSeconds).IsEqual(390f);
        // Unmoved: the tower gap's lock ends and anchor, the cells, the font, the pit and the level's width.
        AssertThat(Level04Controller.CorridorLockMaxX).IsEqual(4760f);
        AssertThat(Level04Controller.CellsLockMinX).IsEqual(6120f);
        AssertThat(Level04Controller.MiddleCheckpointX).IsEqual(5000f);
        AssertThat(Level04Controller.PreBossCheckpointX).IsEqual(8480f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(8200f);
        AssertThat(Level04Controller.TribunalPitFloorY).IsEqual(1120f);
        AssertThat(Level04Controller.BossArenaBounds.Position.X).IsEqual(8640f);
        AssertThat(Level04Controller.LevelWidth).IsEqual(10240f);
        // The barricade keeps its Extractor; the route pays the secret.
        AssertThat(level.AuthoredSecretCachePosition).IsEqual(new Vector2(Level04Controller.BattlementEndX, 215f));
        AssertThat(Level04Controller.ExtractorPlacements[0].Position).IsEqual(new Vector2(2500f, 628f));
    }

    /// <summary>
    /// A hero in the moat is never stuck: Lincoln walks down the west ramp, along the floor
    /// across the spikes, and all the way back up the west ramp, in real frames.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSlowestHeroWalksInAndOutOfTheMoat() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("lincoln");
        Level04Controller level = fixture.Level;
        PlayerController hero = level.Player;
        var source = new BufferedInputSource();
        try {
            await Settle(tree);
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
            hero.GlobalPosition = new Vector2(2040f, 890f);
            hero.Velocity = Vector2.Zero;

            source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
            float deepest = 0f;
            for (int frame = 0; frame < 480 && hero.GlobalPosition.X < 2900f; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                deepest = Mathf.Max(deepest, hero.GlobalPosition.Y);
            }
            AssertThat(hero.GlobalPosition.X >= 2900f)
                .OverrideFailureMessage($"Lincoln stalled in the moat at {hero.GlobalPosition}.").IsTrue();
            AssertThat(deepest >= 1018f).OverrideFailureMessage($"He never reached the moat's floor: deepest y {deepest:0}.").IsTrue();

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

    /// <summary>The swarm row on the real scene: it comes once two of the gatehouse's mob are left, and a fodder kill pays nothing and drops no pickup (F05).</summary>
    [TestCase(Timeout = 60000)]
    public async Task TheGatehousesSwarmComesAtTwoAliveAndItsKillsPayNothing() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level04Controller level = fixture.Level;
        PlayerController hero = level.Player;
        try {
            await Settle(tree);
            hero.GlobalPosition = new Vector2(1000f, 850f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            EncounterLock gatehouse = level.GatehouseLock;
            AssertThat(gatehouse.State).IsEqual(EncounterLockState.Engaged);
            int crowd = gatehouse.LivingMemberCount;
            AssertThat(crowd).IsEqual(Level04Controller.Room1Spawns.Length);

            gatehouse.LivingMembers.First().TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            int row = Level04Controller.SwarmSpawnXs.Length;
            AssertThat(row).IsEqual(SwarmWaveRules.RowMobsForLockWidth(gatehouse.Area.Size.X));
            AssertThat(gatehouse.LivingMemberCount).IsEqual(crowd - 1 + row);

            int wallet = StoryManager.Instance.ChronalDustCollected;
            EnemyController fodder = gatehouse.LivingMembers.First(member => member.Data.DustWeightZero);
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
        public readonly Level04Controller Level;
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
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level04Controller>();
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
