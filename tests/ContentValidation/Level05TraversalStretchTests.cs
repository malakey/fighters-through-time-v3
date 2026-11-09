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
/// GR-LV2: Level 5's carve, its stretch and its swarm on the real scene. The flood rises on
/// lock clears, so the stretch must not change when it rises, and its trench is under water
/// from hull step 1. The shared rules are <c>TraversalStretchContentTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level05TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_05_Titanic.tscn";

    /// <summary>A carve, not a shift: the staircase shrank, par took its +30, and neither the decks nor the flood's table moved.</summary>
    [TestCase]
    public void TheStaircaseWasCarvedAndTheDecksAndTheFloodDidNotMove() {
        using var fixture = new Fixture("einstein");
        Level05Controller level = fixture.Level;
        AssertThat(level.StaircaseLock.Area.Position.X).IsEqual(380f);
        AssertThat(level.StaircaseLock.Area.End.X).IsEqual(2380f);
        AssertThat(level.BoilerCasingLock.Area.Position.X).IsEqual(3420f);

        TraversalStretch well = Children(level).OfType<TraversalStretch>().Single();
        AssertThat(well.MinX).IsEqual(2400f);
        AssertThat(well.MaxX).IsEqual(3400f);
        AssertThat(well.DeckY).IsEqual(1140f);
        AssertThat(well.HasTrench).IsTrue();

        // 270 seeded + 30 for the stretch.
        AssertThat(level.ParSeconds).IsEqual(300f);
        AssertThat(Level05Controller.HullWaterOriginY).IsEqual(1360f);
        AssertThat(string.Join(",", Level05Controller.HullStepHeights)).IsEqual("0,170,340,510,640");
        AssertThat(string.Join(",", Level05Controller.HullStepDryFromX)).IsEqual("0,2300,5500,7500,8800");
        AssertThat(Level05Controller.MiddleCheckpointX).IsEqual(6100f);
        AssertThat(Level05Controller.PreBossCheckpointX).IsEqual(9060f);
        AssertThat(Level05Controller.BossArenaWestX).IsEqual(9400f);
        AssertThat(Level05Controller.LevelWidth).IsEqual(11200f);
        // The route's end stays over the flood's last step.
        Vector2 secret = level.AuthoredSecretCachePosition.Value;
        AssertThat(secret.Y < Level05Controller.HullWaterOriginY - Level05Controller.HullStepHeights[^1]).IsTrue();
    }

    /// <summary>
    /// The trench floods at hull step 1 and a hero still walks out of it: a pending step waits
    /// for a hero in the well, then rises once it stands on D2, over the water and clear of it.
    /// Lincoln wades up the east ramp, in real frames, at the water's speed.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheFloodWaitsForAHeroInTheWellAndTheSlowestHeroWadesOut() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("lincoln");
        Level05Controller level = fixture.Level;
        PlayerController hero = level.Player;
        var source = new BufferedInputSource();
        try {
            await Settle(tree);
            float line = Level05Controller.HullWaterOriginY - Level05Controller.HullStepHeights[1];
            TraversalStretch well = Children(level).OfType<TraversalStretch>().Single();
            AssertThat(well.TrenchFloorY > line).OverrideFailureMessage("Step 1 does not reach the trench floor.").IsTrue();
            AssertThat(well.DeckY < line).OverrideFailureMessage("Step 1 floods the deck.").IsTrue();

            // Cleared from Engaged with the hero in the well: the step is armed and waits.
            hero.GlobalPosition = new Vector2(1600f, 1210f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 30 && level.StaircaseLock.State != EncounterLockState.Engaged; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(level.StaircaseLock.State).IsEqual(EncounterLockState.Engaged);
            for (int pass = 0; pass < 6 && level.StaircaseLock.State == EncounterLockState.Engaged; pass++) {
                foreach (EnemyController member in level.StaircaseLock.LivingMembers) member.TakeDamage(99999);
                hero.GlobalPosition = new Vector2(1600f, 1210f);
                for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(level.StaircaseLock.State).IsEqual(EncounterLockState.Cleared);
            hero.GlobalPosition = new Vector2(3000f, well.TrenchFloorY - 2f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(level.PendingHullStep).IsEqual(1);
            AssertThat(level.HullFlood.CurrentStep).IsEqual(0);

            // Raised by hand so the hero wades: the wade is what this case measures.
            level.HullFlood.SetWaterLevel(1);
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
            source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
            for (int frame = 0; frame < 600 && hero.GlobalPosition.X < Level05Controller.WellDeckEastStartX; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(hero.GlobalPosition.X >= Level05Controller.WellDeckEastStartX && hero.GlobalPosition.Y <= well.DeckY + 1f)
                .OverrideFailureMessage($"Lincoln did not wade out by the east ramp: {hero.GlobalPosition}.").IsTrue();
            AssertThat(hero.CurrentHP > 0).IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    /// <summary>
    /// The swarm row on the real scene: each mob on its own listing deck, it comes once two of
    /// the casing's crew are left, a fodder kill pays nothing, and the boiler surge still arms
    /// on the casing's clear, swarm and all.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheCasingsSwarmComesAtTwoAliveAndTheSurgeStillArmsOnItsClear() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level05Controller level = fixture.Level;
        PlayerController hero = level.Player;
        try {
            await Settle(tree);
            level.StaircaseLock.RestoreCleared();
            hero.GlobalPosition = new Vector2(3800f, 1130f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            EncounterLock casing = level.BoilerCasingLock;
            AssertThat(casing.State).IsEqual(EncounterLockState.Engaged);
            int crew = casing.LivingMemberCount;
            AssertThat(crew).IsEqual(4);

            casing.LivingMembers.First().TakeDamage(99999);
            casing.LivingMembers.First().TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            int row = Level05Controller.SwarmSpawns.Length;
            AssertThat(row).IsEqual(SwarmWaveRules.RowMobsForLockWidth(casing.Area.Size.X));
            AssertThat(casing.LivingMemberCount).IsEqual(crew - 2 + row);

            int wallet = StoryManager.Instance.ChronalDustCollected;
            EnemyController fodder = casing.LivingMembers.First(member => member.Data.DustWeightZero);
            fodder.TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(StoryManager.Instance.ChronalDustCollected).IsEqual(wallet);

            for (int pass = 0; pass < 6 && casing.State == EncounterLockState.Engaged; pass++) {
                foreach (EnemyController member in casing.LivingMembers) member.TakeDamage(99999);
                for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(casing.State).IsEqual(EncounterLockState.Cleared);
            AssertThat(casing.ForceCleared).IsFalse();
            AssertThat(level.PendingHullStep).IsEqual(2);
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
        public readonly Level05Controller Level;
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
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level05Controller>();
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
