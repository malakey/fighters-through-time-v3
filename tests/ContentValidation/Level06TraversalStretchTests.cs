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
/// GR-LV2: Level 6's carve, its stretch and its swarm on the real scene. The stretch stays clear
/// of the winch vault's span, 5390-5660 (ruling 12). The shared rules are
/// <c>TraversalStretchContentTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level06TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_06_Pompeii.tscn";

    /// <summary>A carve, not a shift: the road shrank from its west end, par took its +30, and neither the vault nor anything past the road moved.</summary>
    [TestCase]
    public void TheAshRoadWasCarvedClearOfTheVaultAndNothingElseMoved() {
        using var fixture = new Fixture("einstein");
        Level06Controller level = fixture.Level;
        AssertThat(level.AshRoadLock.Area.Position.X).IsEqual(6720f);
        AssertThat(level.AshRoadLock.Area.End.X).IsEqual(8500f);

        TraversalStretch ashfall = Children(level).OfType<TraversalStretch>().Single();
        AssertThat(ashfall.MinX).IsEqual(5680f);
        AssertThat(ashfall.MaxX).IsEqual(6700f);
        AssertThat(ashfall.HasTrench).IsTrue();
        AssertThat(ashfall.MinX >= 5660f).OverrideFailureMessage("The stretch reaches the vault's span.").IsTrue();
        foreach (TraversalSurface surface in ashfall.Line) {
            AssertThat(surface.Left >= 5660f).OverrideFailureMessage($"A line surface reaches the vault's span: {surface}.").IsTrue();
        }

        // 360 seeded + 30 for the stretch.
        AssertThat(level.ParSeconds).IsEqual(390f);
        // Unmoved: the vault and its rockfall, the Middle anchor, the run's ends, the font, the arena, the width.
        AssertThat(level.VaultLock.Area.Position.X).IsEqual(2900f);
        AssertThat(level.VaultLock.Area.End.X).IsEqual(5390f);
        AssertThat(Level06Controller.RockfallX).IsEqual(5400f);
        AssertThat(level.Levels.TryGetCheckpointPosition(Level06Controller.Checkpoint1, out Vector2 middle)).IsTrue();
        AssertThat(middle.X).IsEqual(5600f);
        AssertThat(Level06Controller.EscapeFinishX).IsEqual(8880f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(8920f);
        AssertThat(Level06Controller.BossArenaWestX).IsEqual(9250f);
        AssertThat(Level06Controller.LevelWidth).IsEqual(10560f);
        // The route pays the road's Extractor, at its end.
        AssertThat(Level06Controller.ExtractorPlacements[2].ID).IsEqual(Level06Controller.AshRoadExtractorID);
        AssertThat(Level06Controller.ExtractorPlacements[2].Position).IsEqual(new Vector2(Level06Controller.RooflineEndX, 238f));
    }

    /// <summary>
    /// A hero in the sewer is never stuck: Lincoln walks down the west ramp, across the moved
    /// geyser's lane and up the east ramp onto the road's deck, then back up the west ramp, in real frames.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSlowestHeroWalksThroughTheSewerBothWays() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("lincoln");
        Level06Controller level = fixture.Level;
        PlayerController hero = level.Player;
        var source = new BufferedInputSource();
        try {
            await Settle(tree);
            level.VaultLock.RestoreCleared();
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
            hero.GlobalPosition = new Vector2(5700f, 890f);
            hero.Velocity = Vector2.Zero;

            source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
            float deepest = 0f;
            for (int frame = 0; frame < 480 && hero.GlobalPosition.X < Level06Controller.AshfallEastDeckStartX; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                deepest = Mathf.Max(deepest, hero.GlobalPosition.Y);
            }
            AssertThat(hero.GlobalPosition.X >= Level06Controller.AshfallEastDeckStartX && hero.GlobalPosition.Y <= 901f)
                .OverrideFailureMessage($"Lincoln did not walk out by the east ramp: {hero.GlobalPosition}.").IsTrue();
            AssertThat(deepest >= 1018f).OverrideFailureMessage($"He never reached the sewer's floor: deepest y {deepest:0}.").IsTrue();

            source.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None, GameplayButtons.None));
            for (int frame = 0; frame < 480 && hero.GlobalPosition.X > Level06Controller.AshfallWestDeckEndX - 5f; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(hero.GlobalPosition.X <= Level06Controller.AshfallWestDeckEndX - 5f && hero.GlobalPosition.Y <= 901f)
                .OverrideFailureMessage($"Lincoln did not walk back out by the west ramp: {hero.GlobalPosition}.").IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    /// <summary>The swarm row on the real scene: it comes once two of the Forum's guard are left, and a fodder kill pays nothing and drops no pickup (F05).</summary>
    [TestCase(Timeout = 60000)]
    public async Task TheForumsSwarmComesAtTwoAliveAndItsKillsPayNothing() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level06Controller level = fixture.Level;
        PlayerController hero = level.Player;
        try {
            await Settle(tree);
            hero.GlobalPosition = new Vector2(1000f, 850f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            EncounterLock forum = level.ForumLock;
            AssertThat(forum.State).IsEqual(EncounterLockState.Engaged);
            int guard = forum.LivingMemberCount;
            AssertThat(guard).IsEqual(Level06Controller.Room1Spawns.Length);

            forum.LivingMembers.First().TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            int row = Level06Controller.SwarmSpawnXs.Length;
            AssertThat(row).IsEqual(SwarmWaveRules.RowMobsForLockWidth(forum.Area.Size.X));
            AssertThat(forum.LivingMemberCount).IsEqual(guard - 1 + row);

            int wallet = StoryManager.Instance.ChronalDustCollected;
            EnemyController fodder = forum.LivingMembers.First(member => member.Data.DustWeightZero);
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
        public readonly Level06Controller Level;
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
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level06Controller>();
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
